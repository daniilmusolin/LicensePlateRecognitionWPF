using LicensePlateRecognitionWPF.Models;
using OpenCvSharp;
using System.Drawing;

namespace LicensePlateRecognitionWPF.Services.Interfaces {
    public interface IPlateLocationFinder {
        Task<Rectangle> FindPlateLocationAsync(Mat image);
        void Configure(LocationFinderOptions options);
    }
}
