using LicensePlateRecognition.Models;
using LicensePlateRecognition.Services.Interfaces;
using OpenCvSharp;

namespace LicensePlateRecognition.Services.Implementations {
    public class FrameSaverService : IFrameSaverService {
        public async Task SaveFrameAsync(VideoFrame frame, DetectedPlate plate, string path) {
            if (frame?.Image == null || plate == null)
                return;

            var fileName = $"plate_{plate.Number}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
            var fullPath = Path.Combine(path, fileName);

            await Task.Run(() => Cv2.ImWrite(fullPath, frame.Image));
        }

        public void EnsureDirectoryExists(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
        }
    }
}
