namespace LicensePlateRecognition.Models {
    public class VideoCaptureInfo {
        public int Width { get; set; }
        public int Height { get; set; }
        public double Fps { get; set; }
        public bool IsConnected { get; set; }
        public TimeSpan Uptime { get; set; }
    }
}
