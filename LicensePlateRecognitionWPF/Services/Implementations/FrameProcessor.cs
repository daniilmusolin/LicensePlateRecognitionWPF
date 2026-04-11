using System.Diagnostics;
using LicensePlateRecognition.Models;
using LicensePlateRecognition.Services.Interfaces;
using OpenCvSharp;

namespace LicensePlateRecognition.Services.Implementations;

public class FrameProcessor : IFrameProcessor {
    private readonly IImagePreprocessor _preprocessor;
    private readonly ILicensePlateRecognizer _recognizer;
    private readonly IPlateLocationFinder _locationFinder;
    private readonly IPlateNumberNormalizer _plateNormalizer;
    private ProcessorOptions _options;

    public FrameProcessor(
        IImagePreprocessor preprocessor,
        ILicensePlateRecognizer recognizer,
        IPlateLocationFinder locationFinder,
        IPlateNumberNormalizer plateNormalizer) {
        _preprocessor = preprocessor ?? throw new ArgumentNullException(nameof(preprocessor));
        _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
        _locationFinder = locationFinder ?? throw new ArgumentNullException(nameof(locationFinder));
        _plateNormalizer = plateNormalizer ?? throw new ArgumentNullException(nameof(plateNormalizer));
        _options = new ProcessorOptions();
    }

    public void Configure(ProcessorOptions options) {
        _options = options ?? new ProcessorOptions();
    }

    public async Task<DetectionResult> ProcessFrameAsync(
        VideoFrame frame,
        CancellationToken cancellationToken = default) {
        var stopwatch = Stopwatch.StartNew();

        try {
            // Валидация входных данных
            if (!IsValidFrame(frame)) {
                return CreateErrorResult("Пустой кадр", stopwatch.Elapsed);
            }

            // Шаг 1: Извлечение области номерного знака
            using var plateRegion = await ExtractPlateRegionAsync(frame.Image, cancellationToken);

            if (!IsValidPlateRegion(plateRegion)) {
                return CreateErrorResult("Область номера не найдена или невалидна", stopwatch.Elapsed);
            }

            // Шаг 2: Предобработка для распознавания
            using var processed = await PreprocessPlateRegionAsync(plateRegion, cancellationToken);

            // Шаг 3: Распознавание номера
            var recognitionResult = await RecognizePlateAsync(processed, cancellationToken);

            // Шаг 4: Конвертация результата
            if (IsSuccessfulRecognition(recognitionResult)) {
                var detectedPlate = await MapToDetectedPlateAsync(recognitionResult.LicensePlate, frame);

                return CreateSuccessResult(detectedPlate, stopwatch.Elapsed);
            }

            return CreateErrorResult(
                recognitionResult.ErrorMessage ?? "Номер не распознан",
                stopwatch.Elapsed);
        } catch (OperationCanceledException) {
            return CreateErrorResult("Операция отменена", stopwatch.Elapsed);
        } catch (Exception ex) {
            Debug.WriteLine($"Ошибка обработки кадра: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
            return CreateErrorResult($"Ошибка: {ex.Message}", stopwatch.Elapsed);
        }
    }

    private bool IsValidFrame(VideoFrame frame) {
        return frame?.Image != null && !frame.Image.Empty();
    }

    private bool IsSuccessfulRecognition(RecognitionResult result) {
        return result?.Success == true && result.LicensePlate != null;
    }

    private async Task<Mat> ExtractPlateRegionAsync(Mat image, CancellationToken cancellationToken) {
        return await Task.Run(() => _preprocessor.DetectAndExtractPlateRegion(image), cancellationToken);
    }

    private async Task<Mat> PreprocessPlateRegionAsync(Mat plateRegion, CancellationToken cancellationToken) {
        return await Task.Run(() => _preprocessor.PreprocessForRecognition(plateRegion), cancellationToken);
    }

    private async Task<RecognitionResult> RecognizePlateAsync(Mat processed, CancellationToken cancellationToken) {
        return await Task.Run(() => _recognizer.RecognizeFromMat(processed), cancellationToken);
    }

    private async Task<DetectedPlate> MapToDetectedPlateAsync(LicensePlate licensePlate, VideoFrame frame) {
        if (licensePlate == null)
            return null;

        var cleanNumber = _plateNormalizer.Normalize(licensePlate.Number);
        var regionCode = _plateNormalizer.ExtractRegionCode(cleanNumber);

        var location = await _locationFinder.FindPlateLocationAsync(frame.Image);

        return new DetectedPlate {
            Number = cleanNumber,
            RegionCode = regionCode,
            Confidence = licensePlate.Confidence,
            DetectionTime = DateTime.Now,
            Location = location
        };
    }

    private bool IsValidPlateRegion(Mat region) {
        return region != null &&
               !region.Empty() &&
               region.Width >= _options.MinPlateWidth &&
               region.Height >= _options.MinPlateHeight &&
               region.Width <= _options.MaxPlateWidth &&
               region.Height <= _options.MaxPlateHeight;
    }

    private DetectionResult CreateErrorResult(string errorMessage, TimeSpan processingTime) {
        return new DetectionResult {
            Success = false,
            ErrorMessage = errorMessage,
            ProcessingTime = processingTime
        };
    }

    private DetectionResult CreateSuccessResult(DetectedPlate plate, TimeSpan processingTime) {
        return new DetectionResult {
            Success = true,
            LicensePlate = plate,
            ProcessingTime = processingTime
        };
    }
}