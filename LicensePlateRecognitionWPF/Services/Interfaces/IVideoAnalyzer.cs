using LicensePlateRecognitionWPF.Models;
using System.Drawing;

namespace LicensePlateRecognitionWPF.Services.Interfaces {
    public interface IVideoAnalyzer {
        event Action<DetectedPlate> OnPlateDetected;
        event Action<string> OnStatusChanged;
        event Action<Bitmap> OnFrameReady;
        event Action<Exception> OnError;

        bool IsRunning { get; }
        VideoAnalyzerOptions Options { get; }

        Task StartAsync(string rtspUrl, CancellationToken cancellationToken = default);
        Task StopAsync();
        void Configure(VideoAnalyzerOptions options);
    }
}
