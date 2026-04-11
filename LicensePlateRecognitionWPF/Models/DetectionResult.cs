namespace LicensePlateRecognition.Models {
    public class DetectionResult {
        public bool Success { get; set; }
        public DetectedPlate LicensePlate { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}
