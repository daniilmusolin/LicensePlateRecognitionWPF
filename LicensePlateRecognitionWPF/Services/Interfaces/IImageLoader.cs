using OpenCvSharp;
using System.Drawing;

namespace LicensePlateRecognitionWPF.Services.Interfaces {
    /// <summary>
    /// Интерфейс загрузчика изображений
    /// </summary>
    public interface IImageLoader {
        Image LoadImage(string filePath);
        Bitmap LoadImageAsBitmap(string filePath); 
        Mat LoadImageAsMat(string filePath);
        bool IsValidImageFile(string filePath);
        string[] GetSupportedFormats();
    }
}