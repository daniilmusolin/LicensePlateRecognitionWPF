using LicensePlateRecognitionWPF.Models;
using OpenCvSharp;

namespace LicensePlateRecognitionWPF.Services.Interfaces {
    public interface ILicensePlateRecognizer {
        RecognitionResult Recognize(byte[] imageData);
        RecognitionResult RecognizeFromFile(string imagePath);
        RecognitionResult RecognizeFromMat(Mat image);
        void Configure(Dictionary<string, string> parameters);
    }
}