using OpenCvSharp;

namespace LicensePlateRecognitionWPF.Models {
    public class VideoFrame : IDisposable {
        public Mat Image { get; set; }
        public int FrameNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public double Fps { get; set; }

        public void Dispose() {
            Image?.Dispose();
        }
    }
}
