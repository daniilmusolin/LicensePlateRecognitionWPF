using OpenCvSharp;
using OpenCvSharp.Extensions;
using LicensePlateRecognition.Services.Interfaces;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace LicensePlateRecognition.Services.Implementations {
    /// <summary>
    /// Предобработчик изображений на основе OpenCV 4.13.0
    /// </summary>
    public class OpenCvPreprocessor : IImagePreprocessor {
        private readonly PreprocessingOptions _options;

        public OpenCvPreprocessor() {
            _options = new PreprocessingOptions();
        }

        public OpenCvPreprocessor(PreprocessingOptions options) {
            _options = options ?? new PreprocessingOptions();
        }

        public Mat PreprocessForRecognition(string imagePath) {
            using (var sourceImage = Cv2.ImRead(imagePath)) {
                if (sourceImage.Empty())
                    throw new ArgumentException($"Не удалось загрузить: {imagePath}");

                return PreprocessForRecognition(sourceImage);
            }
        }

        public Mat PreprocessForRecognition(Mat sourceImage) {
            if (sourceImage == null || sourceImage.Empty())
                throw new ArgumentException("Изображение пустое");

            var processedImage = sourceImage.Clone();

            // Увеличение размера
            if (_options.ResizeForBetterRecognition) {
                var newWidth = (int)(processedImage.Width * _options.ResizeScale);
                var newHeight = (int)(processedImage.Height * _options.ResizeScale);
                Cv2.Resize(processedImage, processedImage, new Size(newWidth, newHeight));
            }

            // Оттенки серого
            if (processedImage.Channels() == 3)
                Cv2.CvtColor(processedImage, processedImage, ColorConversionCodes.BGR2GRAY);

            // Уменьшение шума
            if (_options.ApplyGaussianBlur) {
                Cv2.GaussianBlur(processedImage, processedImage,
                    new Size(_options.GaussianKernelSize, _options.GaussianKernelSize), 0);
            }

            // Адаптивная пороговая обработка
            if (_options.ApplyAdaptiveThreshold) {
                Mat binaryImage = new Mat();
                Cv2.AdaptiveThreshold(processedImage, binaryImage, 255,
                    AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.Binary, _options.ThresholdBlockSize, _options.ThresholdConstant);
                processedImage = binaryImage;
            }

            // Увеличение контраста
            if (_options.ApplyContrastEnhancement)
                Cv2.EqualizeHist(processedImage, processedImage);

            // Морфологические операции
            if (_options.ApplyMorphologicalOperations) {
                var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
                Cv2.MorphologyEx(processedImage, processedImage, MorphTypes.Close, kernel);
                Cv2.MorphologyEx(processedImage, processedImage, MorphTypes.Open, kernel);
            }

            return processedImage;
        }

        /// <summary>
        /// Детекция и извлечение области номерного знака
        /// </summary>
        public Mat DetectAndExtractPlateRegion(Mat sourceImage) {
            if (sourceImage == null || sourceImage.Empty())
                throw new ArgumentException("Изображение пустое");

            // 1. Оттенки серого
            Mat gray = new Mat();
            if (sourceImage.Channels() == 3)
                Cv2.CvtColor(sourceImage, gray, ColorConversionCodes.BGR2GRAY);
            else
                gray = sourceImage.Clone();

            // 2. Поиск границ (Canny)
            Mat edges = new Mat();
            Cv2.Canny(gray, edges, 100, 200);

            // 3. Морфологическое закрытие для соединения букв
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(17, 3));
            Mat closed = new Mat();
            Cv2.MorphologyEx(edges, closed, MorphTypes.Close, kernel);

            // 4. Поиск контуров
            Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(closed, out contours, out hierarchy,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // 5. Поиск контура, похожего на номерной знак
            Rect bestRect = new Rect();
            double maxArea = 0;

            foreach (var contour in contours) {
                var rect = Cv2.BoundingRect(contour);
                double aspectRatio = (double)rect.Width / rect.Height;
                double area = rect.Width * rect.Height;
                double imageArea = sourceImage.Width * sourceImage.Height;
                double areaRatio = area / imageArea;

                // Критерии номерного знака:
                // - Соотношение сторон 2:1 до 5:1
                // - Площадь не менее 1% и не более 30% от изображения
                // - Минимальная ширина 100 пикселей
                if (aspectRatio > 2.0 && aspectRatio < 6.0 &&
                    areaRatio > 0.01 && areaRatio < 0.3 &&
                    rect.Width > 100 && rect.Height > 30) {
                    if (area > maxArea) {
                        maxArea = area;
                        bestRect = rect;
                    }
                }
            }

            // Очистка временных матриц
            gray.Dispose();
            edges.Dispose();
            closed.Dispose();
            kernel.Dispose();

            // Если нашли область номера - вырезаем и возвращаем
            if (bestRect.Width > 0 && bestRect.Height > 0) {
                // Добавляем небольшой отступ
                int padding = 10;
                int x = Math.Max(0, bestRect.X - padding);
                int y = Math.Max(0, bestRect.Y - padding);
                int w = Math.Min(sourceImage.Width - x, bestRect.Width + padding * 2);
                int h = Math.Min(sourceImage.Height - y, bestRect.Height + padding * 2);

                Rect paddedRect = new Rect(x, y, w, h);
                return new Mat(sourceImage, paddedRect);
            }

            // Если не нашли - возвращаем исходное изображение
            return sourceImage.Clone();
        }

        public byte[] ConvertToTesseractFormat(Mat processedImage) {
            if (processedImage == null || processedImage.Empty())
                throw new ArgumentException("Изображение пустое");

            string tempFile = Path.GetTempFileName() + ".png";
            try {
                Cv2.ImWrite(tempFile, processedImage);
                return File.ReadAllBytes(tempFile);
            } finally {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        public Bitmap ConvertMatToBitmap(Mat image) {
            if (image == null || image.Empty())
                throw new ArgumentException("Изображение пустое");

            return BitmapConverter.ToBitmap(image);
        }
    }

    public class PreprocessingOptions {
        public bool ResizeForBetterRecognition { get; set; } = true;
        public double ResizeScale { get; set; } = 1.5;
        public bool ApplyGaussianBlur { get; set; } = true;
        public int GaussianKernelSize { get; set; } = 5;
        public bool ApplyAdaptiveThreshold { get; set; } = true;
        public int ThresholdBlockSize { get; set; } = 11;
        public int ThresholdConstant { get; set; } = 2;
        public bool ApplyContrastEnhancement { get; set; } = true;
        public bool ApplyMorphologicalOperations { get; set; } = true;
    }
}