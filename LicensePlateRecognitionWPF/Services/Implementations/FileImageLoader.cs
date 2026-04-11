using OpenCvSharp;
using LicensePlateRecognition.Services.Interfaces;

namespace LicensePlateRecognition.Services.Implementations {
    public class FileImageLoader : IImageLoader {
        private readonly string[] _supportedFormats = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        public Image LoadImage(string filePath) {
            if (!IsValidImageFile(filePath))
                throw new ArgumentException($"Некорректный файл: {filePath}");

            return Image.FromFile(filePath);
        }

        public Mat LoadImageAsMat(string filePath) {
            if (!IsValidImageFile(filePath))
                throw new ArgumentException($"Некорректный файл: {filePath}");

            var mat = Cv2.ImRead(filePath);
            if (mat.Empty())
                throw new InvalidOperationException("Не удалось загрузить изображение");

            return mat;
        }

        public bool IsValidImageFile(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            if (!File.Exists(filePath)) return false;

            var extension = Path.GetExtension(filePath).ToLower();
            return _supportedFormats.Contains(extension);
        }

        public string[] GetSupportedFormats() => _supportedFormats.ToArray();
    }
}