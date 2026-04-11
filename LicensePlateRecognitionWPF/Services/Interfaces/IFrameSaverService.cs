using LicensePlateRecognition.Models;

namespace LicensePlateRecognition.Services.Interfaces {
    public interface IFrameSaverService {
        Task SaveFrameAsync(VideoFrame frame, DetectedPlate plate, string path);
        void EnsureDirectoryExists(string path);
    }
}
