using OpenCvSharp;

namespace LicensePlateRecognition.Services.Interfaces {
    /// <summary>
    /// Интерфейс загрузчика изображений
    /// </summary>
    public interface IImageLoader {
        Image LoadImage(string filePath);
        Mat LoadImageAsMat(string filePath);
        bool IsValidImageFile(string filePath);
        string[] GetSupportedFormats();
    }
}