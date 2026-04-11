namespace LicensePlateRecognition.Models {
    public class VideoAnalyzerOptions {
        public int DetectionIntervalMs { get; set; } = 500;
        public int CooldownPeriodMs { get; set; } = 5000;
        public double MinPlateConfidence { get; set; } = 0.7;
        public bool EnableFrameSaving { get; set; } = false;
        public string FramesSavePath { get; set; } = "DetectedPlates";
        public int MaxConcurrentDetections { get; set; } = 5;
        public int ReconnectAttempts { get; set; } = 3;
        public int ReconnectDelayMs { get; set; } = 1000;
        public int FrameProcessIntervalMs { get; set; } = 30;
        public int DisplayFrameSkip { get; set; } = 3;
    }
}
