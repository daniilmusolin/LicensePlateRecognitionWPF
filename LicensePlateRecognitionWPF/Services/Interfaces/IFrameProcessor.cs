using LicensePlateRecognitionWPF.Models;

namespace LicensePlateRecognitionWPF.Services.Interfaces {
    public interface IFrameProcessor {
        Task<DetectionResult> ProcessFrameAsync(VideoFrame frame, CancellationToken cancellationToken = default);
        void Configure(ProcessorOptions options);
    }

}
