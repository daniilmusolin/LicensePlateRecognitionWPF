using LicensePlateRecognition.Models;
using OpenCvSharp;

namespace LicensePlateRecognition.Services.Interfaces {
    public interface IPlateLocationFinder {
        Task<Rectangle> FindPlateLocationAsync(Mat image);
        void Configure(LocationFinderOptions options);
    }
}
