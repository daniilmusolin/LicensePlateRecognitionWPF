namespace LicensePlateRecognition.Models {
    /// <summary>
    /// Результат распознавания
    /// </summary>
    public class RecognitionResult {
        public bool Success { get; set; }
        public LicensePlate LicensePlate { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime RecognitionTime { get; set; }
        public double ProcessingTimeMs { get; set; }
        public string RawRecognizedText { get; set; }

        public RecognitionResult() {
            LicensePlate = new LicensePlate();
            ErrorMessage = string.Empty;
            RecognitionTime = DateTime.Now;
            RawRecognizedText = string.Empty;
        }

        public static RecognitionResult CreateSuccess(LicensePlate plate, double processingTime, string rawText = "") {
            return new RecognitionResult {
                Success = true,
                LicensePlate = plate,
                ProcessingTimeMs = processingTime,
                ErrorMessage = string.Empty,
                RawRecognizedText = rawText
            };
        }

        public static RecognitionResult CreateFailure(string errorMessage, string rawText = "") {
            return new RecognitionResult {
                Success = false,
                ErrorMessage = errorMessage,
                ProcessingTimeMs = 0,
                RawRecognizedText = rawText
            };
        }
    }
}