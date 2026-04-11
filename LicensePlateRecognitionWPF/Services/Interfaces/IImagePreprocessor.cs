using OpenCvSharp;

namespace LicensePlateRecognition.Services.Interfaces {
    public interface IImagePreprocessor {
        Mat PreprocessForRecognition(string imagePath);
        Mat PreprocessForRecognition(Mat sourceImage);
        Mat DetectAndExtractPlateRegion(Mat sourceImage);
        byte[] ConvertToTesseractFormat(Mat processedImage);
        Bitmap ConvertMatToBitmap(Mat image);
    }
}