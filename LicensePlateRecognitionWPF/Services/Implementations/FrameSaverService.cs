using LicensePlateRecognitionWPF.Models;
using LicensePlateRecognitionWPF.Services.Interfaces;
using OpenCvSharp;
using System.IO;

namespace LicensePlateRecognitionWPF.Services.Implementations {
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
