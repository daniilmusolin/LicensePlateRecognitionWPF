using LicensePlateRecognitionWPF.Models;

namespace LicensePlateRecognitionWPF.Services.Interfaces {
    public interface IFrameSaverService {
        Task SaveFrameAsync(VideoFrame frame, DetectedPlate plate, string path);
        void EnsureDirectoryExists(string path);
    }
}
