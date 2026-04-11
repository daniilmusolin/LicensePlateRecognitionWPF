using System.Diagnostics;
using LicensePlateRecognitionWPF.Models;
using LicensePlateRecognitionWPF.Services.Interfaces;
using LicensePlateRecognitionWPF.Helpers;
using OpenCvSharp;
using System.Drawing;
using LicensePlateRecognitionWPF.Services.Interfaces;
using System.IO;

namespace LicensePlateRecognitionWPF.Services.Implementations {
    public class VideoAnalyzer : IVideoAnalyzer {
        public event Action<DetectedPlate> OnPlateDetected;
        public event Action<string> OnStatusChanged;
        public event Action<Bitmap> OnFrameReady;
        public event Action<Exception> OnError;

        private readonly IVideoCaptureService _captureService;
        private readonly IFrameProcessor _frameProcessor;
        private readonly IPlateDetectionCache _detectionCache;
        private readonly IFrameSaverService _frameSaver;

        private CancellationTokenSource _cts;
        private Task _analysisTask;
        private bool _isRunning;
        private int _frameCounter;
        private int _processedFrames;

        public bool IsRunning => _isRunning;
        public VideoAnalyzerOptions Options { get; private set; }

        // Конструктор с двумя параметрами (для обратной совместимости)
        public VideoAnalyzer(
            IImagePreprocessor preprocessor,
            ILicensePlateRecognizer recognizer) {
            if (preprocessor == null) throw new ArgumentNullException(nameof(preprocessor));
            if (recognizer == null) throw new ArgumentNullException(nameof(recognizer));

            Options = new VideoAnalyzerOptions();

            // Создаем зависимости
            var locationFinder = new PlateLocationFinder();
            var plateNormalizer = new PlateNumberNormalizer();

            _captureService = new VideoCaptureService(Options);
            _frameProcessor = new FrameProcessor(preprocessor, recognizer, locationFinder, plateNormalizer);
            _detectionCache = new PlateDetectionCache(TimeSpan.FromMilliseconds(Options.CooldownPeriodMs));
            _frameSaver = new FrameSaverService();

            SubscribeToServices();
        }

        // Полный конструктор с внедрением всех зависимостей (для тестирования)
        public VideoAnalyzer(
            IImagePreprocessor preprocessor,
            ILicensePlateRecognizer recognizer,
            IVideoCaptureService captureService,
            IFrameProcessor frameProcessor,
            IPlateDetectionCache detectionCache,
            IFrameSaverService frameSaver) {
            if (preprocessor == null) throw new ArgumentNullException(nameof(preprocessor));
            if (recognizer == null) throw new ArgumentNullException(nameof(recognizer));
            if (captureService == null) throw new ArgumentNullException(nameof(captureService));
            if (frameProcessor == null) throw new ArgumentNullException(nameof(frameProcessor));
            if (detectionCache == null) throw new ArgumentNullException(nameof(detectionCache));
            if (frameSaver == null) throw new ArgumentNullException(nameof(frameSaver));

            Options = new VideoAnalyzerOptions();

            _captureService = captureService;
            _frameProcessor = frameProcessor;
            _detectionCache = detectionCache;
            _frameSaver = frameSaver;

            SubscribeToServices();
        }

        private void SubscribeToServices() {
            _captureService.OnStatusChanged += msg => OnStatusChanged?.Invoke(msg);
            _captureService.OnError += ex => OnError?.Invoke(ex);
        }

        public async Task StartAsync(string rtspUrl, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(rtspUrl))
                throw new ArgumentException("RTSP URL не может быть пустым", nameof(rtspUrl));

            await StopAsync();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isRunning = true;
            _frameCounter = 0;
            _processedFrames = 0;

            _analysisTask = Task.Run(() => AnalyzeVideoStreamAsync(rtspUrl, _cts.Token), _cts.Token);
            OnStatusChanged?.Invoke($"Запуск анализа потока: {rtspUrl}");
        }

        public async Task StopAsync() {
            _isRunning = false;

            if (_cts != null) {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_analysisTask != null) {
                try {
                    await _analysisTask.ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    // Ожидаемая отмена
                } catch (Exception ex) {
                    OnError?.Invoke(ex);
                } finally {
                    _analysisTask = null;
                }
            }

            _captureService.Disconnect();
            OnStatusChanged?.Invoke("Анализ потока остановлен");
        }

        public void Configure(VideoAnalyzerOptions options) {
            Options = options ?? new VideoAnalyzerOptions();

            if (Options.EnableFrameSaving) {
                _frameSaver.EnsureDirectoryExists(Options.FramesSavePath);
            }
        }

        private async Task AnalyzeVideoStreamAsync(string rtspUrl, CancellationToken cancellationToken) {
            if (!await _captureService.ConnectAsync(rtspUrl, cancellationToken)) {
                OnStatusChanged?.Invoke("Не удалось подключиться к видеопотоку");
                _isRunning = false;
                return;
            }

            var lastDetectionTime = DateTime.MinValue;

            while (!cancellationToken.IsCancellationRequested && _isRunning) {
                try {
                    var frame = await _captureService.ReadFrameAsync(cancellationToken);

                    if (frame == null) {
                        await Task.Delay(Options.ReconnectDelayMs, cancellationToken);
                        continue;
                    }

                    using (frame) {
                        _frameCounter++;

                        // Отображение кадров с пропуском для производительности
                        if (_frameCounter % Options.DisplayFrameSkip == 0) {
                            await DisplayFrameAsync(frame);
                        }

                        // Анализ кадров с заданным интервалом
                        var now = DateTime.Now;
                        if ((now - lastDetectionTime).TotalMilliseconds >= Options.DetectionIntervalMs) {
                            await AnalyzeFrameAsync(frame, cancellationToken);
                            lastDetectionTime = now;
                            _processedFrames++;

                            if (_processedFrames % 100 == 0) {
                                OnStatusChanged?.Invoke($"Статус: обработано {_processedFrames} кадров, " +
                                    $"найдено {_detectionCache.GetActiveDetectionsCount()} номеров");
                            }
                        }
                    }

                    await Task.Delay(Options.FrameProcessIntervalMs, cancellationToken);
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception ex) {
                    OnError?.Invoke(ex);
                    OnStatusChanged?.Invoke($"Ошибка: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }

            _isRunning = false;
        }

        private async Task DisplayFrameAsync(VideoFrame frame) {
            try {
                var bitmap = await Task.Run(() => ConvertMatToBitmap(frame.Image));
                if (bitmap != null) {
                    OnFrameReady?.Invoke(bitmap);
                    bitmap.Dispose();
                }
            } catch (Exception ex) {
                Debug.WriteLine($"Ошибка отображения кадра: {ex.Message}");
            }
        }

        private async Task AnalyzeFrameAsync(VideoFrame frame, CancellationToken cancellationToken) {
            var result = await _frameProcessor.ProcessFrameAsync(frame, cancellationToken);

            if (result.Success && result.LicensePlate != null) {
                var plate = result.LicensePlate;

                if (plate.Confidence >= Options.MinPlateConfidence) {
                    await HandleDetectionAsync(plate, frame, cancellationToken);
                }
            }
        }

        private async Task HandleDetectionAsync(DetectedPlate plate, VideoFrame frame, CancellationToken cancellationToken) {
            // Проверка кулдауна
            if (_detectionCache.IsPlateOnCooldown(plate.Number))
                return;

            _detectionCache.RegisterDetection(plate.Number);

            // Сохранение кадра
            if (Options.EnableFrameSaving) {
                await _frameSaver.SaveFrameAsync(frame, plate, Options.FramesSavePath);
            }

            OnStatusChanged?.Invoke($"🚗 Найден номер: {plate.Number} ({(plate.Confidence * 100):F0}%)");
            OnPlateDetected?.Invoke(plate);
        }

        private Bitmap ConvertMatToBitmap(Mat image) {
            if (image == null || image.Empty())
                return null;

            try {
                Cv2.ImEncode(".png", image, out byte[] buffer);
                using var ms = new MemoryStream(buffer);
                return new Bitmap(ms);
            } catch {
                return null;
            }
        }

        private async Task TestCameraConnection(string rtspUrl) {
            OnStatusChanged?.Invoke("🔧 Тестирование подключения к камере...");

            var testCapture = new VideoCapture();

            // Пробуем разные варианты
            var testUrls = new[] {
                rtspUrl,
                rtspUrl + "?transport=tcp",
                "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4" // Тестовый поток
            };

            foreach (var url in testUrls) {
                OnStatusChanged?.Invoke($"Тестируем: {url}");

                if (testCapture.Open(url, VideoCaptureAPIs.FFMPEG)) {
                    var testFrame = new Mat();
                    for (int i = 0; i < 10; i++) {
                        testCapture.Read(testFrame);
                        if (!testFrame.Empty()) {
                            OnStatusChanged?.Invoke($"✅ УСПЕХ! Получен кадр {testFrame.Width}x{testFrame.Height}");
                            testFrame.Dispose();
                            testCapture.Release();
                            return;
                        }
                        await Task.Delay(100);
                    }
                    testFrame.Dispose();
                }
                testCapture.Release();
            }

            OnStatusChanged?.Invoke("❌ Не удалось получить кадры ни с одного URL");
        }

        public void Dispose() {
            _captureService?.Dispose();
            _cts?.Dispose();
            (_detectionCache as IDisposable)?.Dispose();
        }
    }
}