using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LicensePlateRecognition.Services.Implementations;
using LicensePlateRecognitionWPF.Models;
using LicensePlateRecognitionWPF.Services.Implementations;
using LicensePlateRecognitionWPF.Services.Interfaces;
using Microsoft.Win32;
using OpenCvSharp;
using Brush = System.Windows.Media.Brush;
using Window = System.Windows.Window;

namespace LicensePlateRecognitionWPF;

public partial class MainWindow : Window {
    private readonly IImageLoader _imageLoader;
    private readonly IImagePreprocessor _preprocessor;
    private readonly ILicensePlateRecognizer _recognizer;
    private IVideoAnalyzer _videoAnalyzer;

    private string _currentImagePath;
    private Mat _currentMatImage;
    private int _recognitionCount;
    private DetectedPlate _currentPlate;
    private bool _isCameraMode;

    public MainWindow() {
        InitializeComponent();

        var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        if (!Directory.Exists(tessDataPath)) {
            Directory.CreateDirectory(tessDataPath);
            ShowTessDataWarning(tessDataPath);
        }

        _imageLoader = new FileImageLoader();
        _preprocessor = new OpenCvPreprocessor();
        _recognizer = new TesseractRecognizer(tessDataPath);
        _videoAnalyzer = new VideoAnalyzer(_preprocessor, _recognizer);

        _videoAnalyzer.OnPlateDetected += OnPlateDetected;
        _videoAnalyzer.OnStatusChanged += OnStatusChanged;
        _videoAnalyzer.OnFrameReady += OnFrameReady;

        CmbPreset.SelectionChanged += (s, e) => UpdatePresetUrl();

        Loaded += (s, e) => StartStatusUpdater();
    }

    private void UpdatePresetUrl() {
        var selectedItem = CmbPreset.SelectedItem as ComboBoxItem;
        var selected = selectedItem?.Content?.ToString();

        switch (selected) {
            case "Hikvision":
                TxtRtspUrl.Text = "rtsp://admin:admin@192.168.1.64:554/Streaming/Channels/101";
                break;
            case "Dahua":
                TxtRtspUrl.Text = "rtsp://admin:admin@192.168.1.108:554/cam/realmonitor?channel=1&subtype=0";
                break;
            case "Axis":
                TxtRtspUrl.Text = "rtsp://root:pass@192.168.1.90:554/axis-media/media.amp";
                break;
            case "Тестовый поток":
                TxtRtspUrl.Text = "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4";
                break;
            default:
                TxtRtspUrl.Text = "rtsp://";
                break;
        }
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e) {
        if (string.IsNullOrWhiteSpace(TxtRtspUrl.Text) || TxtRtspUrl.Text == "rtsp://") {
            MessageBox.Show("Введите RTSP URL", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try {
            _isCameraMode = true;
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            BtnLoadImage.IsEnabled = false;

            var options = new VideoAnalyzerOptions {
                DetectionIntervalMs = 500,
                MinPlateConfidence = 0.6,
                CooldownPeriodMs = 3000,
                EnableFrameSaving = true,
                FramesSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DetectedPlates"),
                DisplayFrameSkip = 1,
                FrameProcessIntervalMs = 33
            };

            _videoAnalyzer.Configure(options);

            string url = TxtRtspUrl.Text;
            if (!url.Contains("transport=tcp") && !url.Contains(".mp4")) {
                url += "?transport=tcp";
            }

            LblCameraStatus.Text = "Статус: подключение...";
            LblCameraStatus.Foreground = (Brush)FindResource("WarningColor");
            VideoStatus.Text = "Подключение...";

            await _videoAnalyzer.StartAsync(url);
        } catch (Exception ex) {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            ResetCameraUI();
        }
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e) {
        try {
            // Блокируем кнопки сразу для обратной связи
            BtnStop.IsEnabled = false;
            BtnStop.Content = "ОСТАНОВКА...";

            LblCameraStatus.Text = "Статус: остановка...";
            VideoStatus.Text = "Остановка...";

            // Останавливаем с таймаутом
            var stopTask = _videoAnalyzer.StopAsync();
            var completedTask = await Task.WhenAny(stopTask, Task.Delay(3000));

            if (completedTask != stopTask) {
                // Принудительная остановка
                LblStatus.Text = "Принудительная остановка";
            }

            ResetCameraUI();

            LblStatus.Text = "Поток остановлен";
            LblStatus.Foreground = (Brush)FindResource("SuccessColor");
        } catch (Exception ex) {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            ResetCameraUI();
        } finally {
            BtnStop.Content = "СТОП";
        }
    }

    private void ResetCameraUI() {
        Dispatcher.Invoke(() =>
        {
            _isCameraMode = false;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            BtnLoadImage.IsEnabled = true;
            LblCameraStatus.Text = "Статус: не подключен";
            LblCameraStatus.Foreground = (Brush)FindResource("WarningColor");
            VideoStatus.Text = "Готов";
            LblResolution.Text = "Разрешение: -";
            LblFps.Text = "FPS: -";

            // Очищаем изображение
            var oldImage = VideoImage.Source;
            VideoImage.Source = null;
            oldImage?.Freeze();

            PlateOverlay.Visibility = Visibility.Collapsed;
        });
    }

    private void OnPlateDetected(DetectedPlate plate) {
        Dispatcher.Invoke(() => {
            _currentPlate = plate;
            TxtPlateNumber.Text = plate.Number;
            TxtRegion.Text = plate.RegionCode;
            ConfidenceBar.Value = plate.Confidence * 100;

            ListViewHistory.Items.Insert(0, new {
                plate.Number,
                Region = plate.RegionCode,
                Confidence = $"{plate.Confidence * 100:F0}%"
            });

            _recognitionCount++;
            LblStats.Text = $"Распознано: {_recognitionCount}";
            LblStatus.Text = $"Распознан номер: {plate.Number}";
            LblStatus.Foreground = (Brush)FindResource("SuccessColor");

            BtnCopy.IsEnabled = true;
            BtnSaveFrame.IsEnabled = true;

            DrawPlateOverlay(plate.Location);
        });
    }

    private void DrawPlateOverlay(Rectangle rect) {
        if (rect == Rectangle.Empty || VideoImage.Source == null) {
            PlateOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var image = VideoImage.Source as BitmapImage;
        if (image == null) return;

        // Ждем, пока Image загрузится и получит актуальный размер
        if (VideoImage.ActualWidth == 0 || VideoImage.ActualHeight == 0) {
            PlateOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        double scaleX = VideoImage.ActualWidth / image.PixelWidth;
        double scaleY = VideoImage.ActualHeight / image.PixelHeight;
        double scale = Math.Min(scaleX, scaleY);

        double offsetX = (VideoImage.ActualWidth - image.PixelWidth * scale) / 2;
        double offsetY = (VideoImage.ActualHeight - image.PixelHeight * scale) / 2;

        PlateOverlay.Width = rect.Width * scale;
        PlateOverlay.Height = rect.Height * scale;
        PlateOverlay.Margin = new Thickness(
            rect.X * scale + offsetX,
            rect.Y * scale + offsetY,
            0, 0);
        PlateOverlay.Visibility = Visibility.Visible;
    }

    private void OnStatusChanged(string status) {
        Dispatcher.Invoke(() => {
            LblCameraStatus.Text = $"Статус: {status}";
            VideoStatus.Text = status;

            if (status.Contains("подключен") || status.Contains("Подключено")) {
                LblCameraStatus.Foreground = (Brush)FindResource("SuccessColor");
                VideoStatus.Foreground = (Brush)FindResource("SuccessColor");

                // Попытка извлечь разрешение из статуса
                if (status.Contains("x")) {
                    try {
                        var parts = status.Split(new[] { 'x', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < parts.Length; i++) {
                            if (int.TryParse(parts[i], out _) && i + 1 < parts.Length && int.TryParse(parts[i + 1], out _)) {
                                LblResolution.Text = $"Разрешение: {parts[i]}x{parts[i + 1]}";
                                break;
                            }
                        }
                    } catch { }
                }
            } else if (status.Contains("ошибка")) {
                LblCameraStatus.Foreground = (Brush)FindResource("ErrorColor");
                VideoStatus.Foreground = (Brush)FindResource("ErrorColor");
            } else {
                LblCameraStatus.Foreground = (Brush)FindResource("WarningColor");
                VideoStatus.Foreground = (Brush)FindResource("WarningColor");
            }
        });
    }

    private void OnFrameReady(Bitmap frame) {
        Dispatcher.Invoke(() => {
            var oldImage = VideoImage.Source;
            VideoImage.Source = ConvertBitmapToImageSource(frame);
            oldImage?.Freeze();
        });
    }

    private BitmapImage ConvertBitmapToImageSource(Bitmap bitmap) {
        using var memory = new MemoryStream();
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
        memory.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = memory;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();

        return image;
    }

    private async void BtnLoadImage_Click(object sender, RoutedEventArgs e) {
        var dialog = new OpenFileDialog {
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp",
            Title = "Выберите изображение"
        };

        if (dialog.ShowDialog() == true) {
            try {
                _currentImagePath = dialog.FileName;
                ProgressBar.Visibility = Visibility.Visible;
                BtnLoadImage.IsEnabled = false;
                BtnRecognize.IsEnabled = false;

                await Task.Run(() =>
                {
                    _currentMatImage = _imageLoader.LoadImageAsMat(_currentImagePath);
                });

                var bitmap = await Task.Run(() => _imageLoader.LoadImageAsBitmap(_currentImagePath));
                VideoImage.Source = ConvertBitmapToImageSource(bitmap);

                BtnRecognize.IsEnabled = true;
                _currentPlate = null;
                TxtPlateNumber.Text = string.Empty;
                TxtRegion.Text = string.Empty;
                ConfidenceBar.Value = 0;
                PlateOverlay.Visibility = Visibility.Collapsed;

                LblStatus.Text = $"Загружено: {Path.GetFileName(_currentImagePath)}";
                LblStatus.Foreground = (Brush)FindResource("SuccessColor");
            } catch (Exception ex) {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                LblStatus.Text = "Ошибка загрузки";
                LblStatus.Foreground = (Brush)FindResource("ErrorColor");
            } finally {
                ProgressBar.Visibility = Visibility.Collapsed;
                BtnLoadImage.IsEnabled = true;
            }
        }
    }

    private async void BtnRecognize_Click(object sender, RoutedEventArgs e) {
        if (_currentMatImage == null || _currentMatImage.Empty()) {
            MessageBox.Show("Сначала загрузите изображение", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try {
            BtnRecognize.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            LblStatus.Text = "Распознавание...";
            LblStatus.Foreground = (Brush)FindResource("WarningColor");

            var result = await Task.Run(() => {
                var plateRegion = _preprocessor.DetectAndExtractPlateRegion(_currentMatImage);
                var recognition = _recognizer.RecognizeFromMat(plateRegion);
                plateRegion.Dispose();
                return recognition;
            });

            if (result.Success && result.LicensePlate != null) {
                var plateNumber = result.LicensePlate.Number;
                var regionCode = ExtractRegionCode(plateNumber);
                var cleanNumber = ExtractCleanNumber(plateNumber);

                _currentPlate = new DetectedPlate {
                    Number = cleanNumber,
                    RegionCode = regionCode,
                    Confidence = result.LicensePlate.Confidence,
                    DetectionTime = DateTime.Now
                };

                TxtPlateNumber.Text = cleanNumber;
                TxtRegion.Text = regionCode;
                ConfidenceBar.Value = result.LicensePlate.Confidence * 100;

                ListViewHistory.Items.Insert(0, new {
                    Number = cleanNumber,
                    Region = regionCode,
                    Confidence = $"{result.LicensePlate.Confidence * 100:F0}%"
                });

                _recognitionCount++;
                LblStats.Text = $"Распознано: {_recognitionCount}";
                LblStatus.Text = $"Распознан номер: {cleanNumber}";
                LblStatus.Foreground = (Brush)FindResource("SuccessColor");

                BtnCopy.IsEnabled = true;
                BtnSaveFrame.IsEnabled = true;
            } else {
                TxtPlateNumber.Text = "НЕ НАЙДЕН";
                TxtRegion.Text = "---";
                ConfidenceBar.Value = 0;
                LblStatus.Text = "Номер не распознан";
                LblStatus.Foreground = (Brush)FindResource("ErrorColor");
            }
        } catch (Exception ex) {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            LblStatus.Text = "Ошибка распознавания";
            LblStatus.Foreground = (Brush)FindResource("ErrorColor");
        } finally {
            BtnRecognize.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private string ExtractRegionCode(string plateNumber) {
        var digits = new string(plateNumber.Where(char.IsDigit).ToArray());
        if (digits.Length >= 2) {
            return digits.Length >= 3 ? digits.Substring(digits.Length - 3) : digits.Substring(digits.Length - 2);
        }
        return "??";
    }

    private string ExtractCleanNumber(string plateNumber) {
        var letters = new string(plateNumber.Where(char.IsLetter).ToArray());
        var digits = new string(plateNumber.Where(char.IsDigit).ToArray());

        if (letters.Length >= 3 && digits.Length >= 3) {
            return (letters.Substring(0, 1) + digits.Substring(0, 3) + letters.Substring(1, 2)).ToUpper();
        }
        return plateNumber;
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e) {
        if (!string.IsNullOrEmpty(TxtPlateNumber.Text) && TxtPlateNumber.Text != "НЕ НАЙДЕН") {
            Clipboard.SetText($"{TxtPlateNumber.Text} {TxtRegion.Text}");
            LblStatus.Text = "Номер скопирован";
            LblStatus.Foreground = (Brush)FindResource("SuccessColor");
        }
    }

    private void BtnSaveFrame_Click(object sender, RoutedEventArgs e) {
        if (VideoImage.Source == null || _currentPlate == null) return;

        var dialog = new SaveFileDialog {
            Filter = "PNG изображение|*.png",
            FileName = $"plate_{_currentPlate.Number}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (dialog.ShowDialog() == true) {
            try {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)VideoImage.Source));

                using var stream = File.OpenWrite(dialog.FileName);
                encoder.Save(stream);

                LblStatus.Text = "Изображение сохранено";
                LblStatus.Foreground = (Brush)FindResource("SuccessColor");
            } catch (Exception ex) {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e) {
        if (ListViewHistory.Items.Count == 0) {
            MessageBox.Show("Нет данных для экспорта", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog {
            Filter = "CSV файл|*.csv",
            FileName = $"plates_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true) {
            try {
                var sb = new StringBuilder();
                sb.AppendLine("Номер;Регион;Точность;Дата");

                for (int i = 0; i < ListViewHistory.Items.Count; i++) {
                    dynamic item = ListViewHistory.Items[i];
                    sb.AppendLine($"{item.Number};{item.Region};{item.Confidence};{DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                LblStatus.Text = "Экспорт завершен";
                LblStatus.Foreground = (Brush)FindResource("SuccessColor");
            } catch (Exception ex) {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void StartStatusUpdater() {
        while (true) {
            await Task.Delay(1000);
            if (_isCameraMode && _videoAnalyzer.IsRunning) {
                Dispatcher.Invoke(() => {
                    LblStats.Text = $"Распознано: {_recognitionCount} | RTSP режим";
                });
            }
        }
    }

    private void ShowTessDataWarning(string tessDataPath) {
        var result = MessageBox.Show(
            "Для работы необходимы файлы Tesseract:\n" +
            "rus.traineddata и eng.traineddata\n\n" +
            $"Поместите их в папку:\n{tessDataPath}\n\n" +
            "Продолжить?",
            "Внимание",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No) {
            Application.Current.Shutdown();
        }
    }

    protected override void OnClosed(EventArgs e) {
        if (_videoAnalyzer.IsRunning) {
            _ = _videoAnalyzer.StopAsync();
        }
        _currentMatImage?.Dispose();
        base.OnClosed(e);
    }
}
