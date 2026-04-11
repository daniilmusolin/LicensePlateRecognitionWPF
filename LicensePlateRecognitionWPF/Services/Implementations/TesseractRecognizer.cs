using Tesseract;
using OpenCvSharp;
using LicensePlateRecognition.Models;
using LicensePlateRecognition.Services.Interfaces;
using LicensePlateRecognition.Helpers;

namespace LicensePlateRecognition.Services.Implementations {
    public class TesseractRecognizer : ILicensePlateRecognizer {
        private readonly string _tessDataPath;
        private readonly LicensePlateValidator _validator;
        private Dictionary<string, string> _configuration;

        public TesseractRecognizer(string tessDataPath) {
            _tessDataPath = tessDataPath ?? throw new ArgumentNullException(nameof(tessDataPath));
            _validator = new LicensePlateValidator();
            _configuration = new Dictionary<string, string>();
            InitializeDefaultConfiguration();
        }

        private void InitializeDefaultConfiguration() {
            _configuration["tessedit_char_whitelist"] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789АВЕКМНОРСТУХ";
            _configuration["tessedit_pageseg_mode"] = "7"; // Single text line
            _configuration["tessedit_ocr_engine_mode"] = "3";
        }

        public RecognitionResult Recognize(byte[] imageData) {
            var startTime = DateTime.Now;

            try {
                using (var engine = new TesseractEngine(_tessDataPath, "rus+eng", EngineMode.Default)) {
                    foreach (var config in _configuration)
                        engine.SetVariable(config.Key, config.Value);

                    using (var image = Pix.LoadFromMemory(imageData))
                    using (var page = engine.Process(image)) {
                        var recognizedText = page.GetText().ToUpper().Trim();
                        var confidence = page.GetMeanConfidence();
                        var licensePlate = _validator.ExtractLicensePlate(recognizedText);

                        if (licensePlate != null) {
                            licensePlate.Confidence = confidence;
                            var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                            return RecognitionResult.CreateSuccess(licensePlate, processingTime, recognizedText);
                        }
                    }
                }

                return RecognitionResult.CreateFailure("Номер не найден");
            } catch (Exception ex) {
                return RecognitionResult.CreateFailure($"Ошибка: {ex.Message}");
            }
        }

        public RecognitionResult RecognizeFromFile(string imagePath) {
            if (!File.Exists(imagePath))
                return RecognitionResult.CreateFailure($"Файл не найден: {imagePath}");

            return Recognize(File.ReadAllBytes(imagePath));
        }

        public RecognitionResult RecognizeFromMat(Mat image) {
            if (image == null || image.Empty())
                return RecognitionResult.CreateFailure("Изображение пустое");

            string tempFile = Path.GetTempFileName() + ".png";
            try {
                Cv2.ImWrite(tempFile, image);
                return Recognize(File.ReadAllBytes(tempFile));
            } finally {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        public void Configure(Dictionary<string, string> parameters) {
            if (parameters != null) {
                foreach (var param in parameters)
                    _configuration[param.Key] = param.Value;
            }
        }
    }
}