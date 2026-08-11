using LicensePlateRecognitionWPF.Models;
using LicensePlateRecognitionWPF.Services.Interfaces;
using OpenCvSharp;
using System.Diagnostics;

namespace LicensePlateRecognition.Services.Implementations;

public class VideoCaptureService : IVideoCaptureService {
    public event Action<string> OnStatusChanged;
    public event Action<Exception> OnError;

    private VideoCapture _capture;
    private readonly object _lockObject = new object();
    private readonly VideoAnalyzerOptions _options;
    private string _currentRtspUrl;
    private int _reconnectAttempts;
    private int _emptyFrameCount;
    private bool _isDisconnecting;

    public bool IsConnected { get; private set; }
    public VideoCaptureInfo CaptureInfo { get; private set; }

    public VideoCaptureService(VideoAnalyzerOptions options) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        CaptureInfo = new VideoCaptureInfo();
    }

    public async Task<bool> ConnectAsync(string rtspUrl, CancellationToken cancellationToken = default) {
        _currentRtspUrl = rtspUrl;
        _reconnectAttempts = 0;
        _emptyFrameCount = 0;
        _isDisconnecting = false;

        // Пробуем разные варианты URL
        var urlsToTry = new[]
        {
            rtspUrl,
            rtspUrl + "?transport=tcp",
            rtspUrl + "?tcp",
            "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4" // Тестовый поток
        };

        foreach (var url in urlsToTry) {
            if (cancellationToken.IsCancellationRequested) return false;

            OnStatusChanged?.Invoke($"Пробуем: {url}");

            if (await TryConnect(url, cancellationToken)) {
                OnStatusChanged?.Invoke($"Подключено! {CaptureInfo.Width}x{CaptureInfo.Height}, {CaptureInfo.Fps:F1} FPS");
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }

        OnStatusChanged?.Invoke("Не удалось подключиться");
        return false;
    }

    private async Task<bool> TryConnect(string rtspUrl, CancellationToken cancellationToken) {
        return await Task.Run(() => {
            try {
                using var testCapture = new VideoCapture();

                // Пробуем открыть с FFMPEG
                if (!testCapture.Open(rtspUrl, VideoCaptureAPIs.FFMPEG)) {
                    return false;
                }

                // Ждем первый кадр (до 3 секунд)
                var frame = new Mat();
                for (int i = 0; i < 30; i++) {
                    if (cancellationToken.IsCancellationRequested) return false;

                    testCapture.Read(frame);
                    if (!frame.Empty()) {
                        // Успешно!
                        lock (_lockObject) {
                            _capture = new VideoCapture(rtspUrl, VideoCaptureAPIs.FFMPEG);
                            if (_capture.IsOpened()) {
                                CaptureInfo = new VideoCaptureInfo {
                                    Width = _capture.FrameWidth,
                                    Height = _capture.FrameHeight,
                                    Fps = _capture.Get(VideoCaptureProperties.Fps) > 0
                                        ? _capture.Get(VideoCaptureProperties.Fps) : 25,
                                    IsConnected = true
                                };
                                IsConnected = true;
                                frame.Dispose();
                                return true;
                            }
                        }
                        frame.Dispose();
                        return true;
                    }
                    Thread.Sleep(100);
                }
                frame.Dispose();
                return false;
            } catch (Exception ex) {
                OnStatusChanged?.Invoke($"Ошибка: {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }

    public async Task<VideoFrame> ReadFrameAsync(CancellationToken cancellationToken = default) {
        if (!IsConnected || _capture == null || _isDisconnecting)
            return null;

        var frame = new Mat();

        try {
            // Используем Task с таймаутом
            var readTask = Task.Run(() => {
                lock (_lockObject) {
                    if (_capture != null && _capture.IsOpened() && !_isDisconnecting) {
                        _capture.Read(frame);
                        return !frame.Empty();
                    }
                    return false;
                }
            }, cancellationToken);

            // Таймаут 100 мс на чтение кадра
            var completedTask = await Task.WhenAny(readTask, Task.Delay(100, cancellationToken));

            if (completedTask != readTask) {
                frame.Dispose();
                return null; // Таймаут
            }

            if (!readTask.Result || frame == null || frame.Empty()) {
                frame?.Dispose();
                _emptyFrameCount++;

                if (_emptyFrameCount >= 10) {
                    OnStatusChanged?.Invoke("Слишком много пустых кадров");
                    IsConnected = false;
                }
                return null;
            }

            _emptyFrameCount = 0;

            return new VideoFrame {
                Image = frame,
                Timestamp = DateTime.Now,
                Fps = CaptureInfo.Fps
            };
        } catch (OperationCanceledException) {
            frame?.Dispose();
            return null;
        } catch (Exception ex) {
            frame?.Dispose();
            OnError?.Invoke(ex);
            return null;
        }
    }

    public async Task ReconnectAsync() {
        if (_reconnectAttempts >= _options.ReconnectAttempts) {
            OnStatusChanged?.Invoke("Превышено количество попыток переподключения");
            IsConnected = false;
            return;
        }

        _reconnectAttempts++;
        OnStatusChanged?.Invoke($"Переподключение... Попытка {_reconnectAttempts}/{_options.ReconnectAttempts}");

        await Task.Delay(_options.ReconnectDelayMs);

        if (!string.IsNullOrEmpty(_currentRtspUrl)) {
            await ConnectAsync(_currentRtspUrl);
        }
    }

    public void Disconnect() {
        _isDisconnecting = true;

        lock (_lockObject) {
            if (_capture != null) {
                try {
                    if (_capture.IsOpened())
                        _capture.Release();
                    _capture.Dispose();
                } catch (Exception ex) {
                    Debug.WriteLine($"Ошибка при отключении: {ex.Message}");
                }
                _capture = null;
            }
            IsConnected = false;
        }

        _isDisconnecting = false;
    }

    public void Dispose() {
        Disconnect();
    }
}
