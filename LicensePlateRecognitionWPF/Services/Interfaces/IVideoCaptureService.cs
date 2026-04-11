using LicensePlateRecognitionWPF.Models;

namespace LicensePlateRecognitionWPF.Services.Interfaces {
    public interface IVideoCaptureService : IDisposable {
        event Action<string> OnStatusChanged;
        event Action<Exception> OnError;

        bool IsConnected { get; }
        VideoCaptureInfo CaptureInfo { get; }

        Task<bool> ConnectAsync(string rtspUrl, CancellationToken cancellationToken = default);
        Task<VideoFrame> ReadFrameAsync(CancellationToken cancellationToken = default);
        Task ReconnectAsync();
        void Disconnect();
    }
}
