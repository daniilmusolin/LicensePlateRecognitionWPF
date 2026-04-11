using LicensePlateRecognition.Models;

namespace LicensePlateRecognition.Services.Interfaces {
    public interface IFrameProcessor {
        Task<DetectionResult> ProcessFrameAsync(VideoFrame frame, CancellationToken cancellationToken = default);
        void Configure(ProcessorOptions options);
    }

}
