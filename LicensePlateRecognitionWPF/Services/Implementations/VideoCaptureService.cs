using LicensePlateRecognitionWPF.Models;
using LicensePlateRecognitionWPF.Services.Interfaces;
using OpenCvSharp;
using System.Diagnostics;

namespace LicensePlateRecognitionWPF.Services.Implementations {
    public class VideoCaptureService : IVideoCaptureService {
        public event Action<string> OnStatusChanged;
        public event Action<Exception> OnError;

        private VideoCapture _capture;
        private readonly object _lockObject = new object();
        private readonly VideoAnalyzerOptions _options;
        private string _currentRtspUrl;
        private int _reconnectAttempts;
        private int _emptyFrameCount;

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

            // Пробуем разные варианты URL с разными параметрами
            var urlsToTry = new[] {
                rtspUrl,
                rtspUrl + "?transport=tcp",
                rtspUrl + "?tcp",
                rtspUrl + "?udp",
                rtspUrl.Replace("rtsp://", "rtsp://admin:admin@"),
                "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4" // Тестовый поток
            };

            foreach (var url in urlsToTry) {
                OnStatusChanged?.Invoke($"Пробуем: {url}");

                if (await TryConnect(url, cancellationToken)) {
                    OnStatusChanged?.Invoke($"✅ Подключено! Размер: {CaptureInfo.Width}x{CaptureInfo.Height}, FPS: {CaptureInfo.Fps:F1}");
                    return true;
                }

                await Task.Delay(500, cancellationToken);
            }

            OnStatusChanged?.Invoke("❌ Не удалось подключиться ни к одному из адресов");
            return false;
        }

        private async Task<bool> TryConnect(string rtspUrl, CancellationToken cancellationToken) {
            return await Task.Run(() => {
                try {
                    // Создаем capture с FFMPEG
                    using (var testCapture = new VideoCapture()) {
                        if (!testCapture.Open(rtspUrl, VideoCaptureAPIs.FFMPEG)) {
                            return false;
                        }

                        // Ждем до 3 секунд для получения первого кадра
                        var frame = new Mat();
                        var startTime = DateTime.Now;
                        int frameAttempts = 0;

                        while ((DateTime.Now - startTime).TotalSeconds < 3 && frameAttempts < 30) {
                            testCapture.Read(frame);
                            frameAttempts++;

                            if (!frame.Empty()) {
                                // Успешно получили кадр!
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
                    }
                } catch (Exception ex) {
                    OnStatusChanged?.Invoke($"Ошибка: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        public async Task<VideoFrame> ReadFrameAsync(CancellationToken cancellationToken = default) {
            if (!IsConnected || _capture == null) {
                return null;
            }

            var frame = new Mat();
            bool readSuccess = false;

            await Task.Run(() => {
                lock (_lockObject) {
                    if (_capture != null && _capture.IsOpened()) {
                        readSuccess = _capture.Read(frame);
                    }
                }
            }, cancellationToken);

            if (!readSuccess || frame == null || frame.Empty()) {
                frame?.Dispose();
                _emptyFrameCount++;

                if (_emptyFrameCount >= 10) {
                    OnStatusChanged?.Invoke($"⚠️ {_emptyFrameCount} пустых кадров, пробуем переподключиться...");
                    _emptyFrameCount = 0;
                    IsConnected = false;
                    await ReconnectAsync();
                }
                return null;
            }

            _emptyFrameCount = 0;

            return new VideoFrame {
                Image = frame,
                Timestamp = DateTime.Now,
                Fps = CaptureInfo.Fps
            };
        }

        public async Task ReconnectAsync() {
            if (_reconnectAttempts >= _options.ReconnectAttempts) {
                OnStatusChanged?.Invoke("❌ Превышено количество попыток переподключения");
                IsConnected = false;
                return;
            }

            _reconnectAttempts++;
            OnStatusChanged?.Invoke($"🔄 Переподключение... Попытка {_reconnectAttempts}/{_options.ReconnectAttempts}");

            await Task.Delay(_options.ReconnectDelayMs);

            if (!string.IsNullOrEmpty(_currentRtspUrl)) {
                await ConnectAsync(_currentRtspUrl);
            }
        }

        public void Disconnect() {
            lock (_lockObject) {
                if (_capture != null) {
                    if (_capture.IsOpened())
                        _capture.Release();
                    _capture.Dispose();
                    _capture = null;
                }
                IsConnected = false;
            }
        }

        public void Dispose() {
            Disconnect();
        }
    }
}