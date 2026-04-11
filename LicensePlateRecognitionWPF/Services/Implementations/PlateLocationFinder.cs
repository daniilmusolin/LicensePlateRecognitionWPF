using LicensePlateRecognitionWPF.Models;
using LicensePlateRecognitionWPF.Services.Interfaces;
using OpenCvSharp;
using System.Diagnostics;
using System.Drawing;

namespace LicensePlateRecognitionWPF.Services.Implementations {
    public class PlateLocationFinder : IPlateLocationFinder {
        private LocationFinderOptions _options;

        public PlateLocationFinder() {
            _options = new LocationFinderOptions();
        }

        public void Configure(LocationFinderOptions options) {
            _options = options ?? new LocationFinderOptions();
        }

        public Task<Rectangle> FindPlateLocationAsync(Mat image) {
            return Task.Run(() => FindPlateLocation(image));
        }

        private Rectangle FindPlateLocation(Mat image) {
            if (image == null || image.Empty())
                return Rectangle.Empty;

            try {
                Rectangle bestRect = Rectangle.Empty;
                double maxArea = 0;

                using (var gray = new Mat())
                using (var edges = new Mat()) {
                    // Конвертация в оттенки серого
                    if (image.Channels() == 3)
                        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
                    else
                        image.CopyTo(gray);

                    // Поиск границ
                    Cv2.Canny(gray, edges, _options.CannyThreshold1, _options.CannyThreshold2);

                    // Поиск контуров
                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] hierarchy;
                    Cv2.FindContours(edges, out contours, out hierarchy,
                        RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                    double imageArea = image.Width * image.Height;

                    foreach (var contour in contours) {
                        var rect = Cv2.BoundingRect(contour);

                        if (IsValidPlateRect(rect, imageArea)) {
                            double area = rect.Width * rect.Height;
                            if (area > maxArea) {
                                maxArea = area;
                                bestRect = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
                            }
                        }
                    }
                }

                return bestRect;
            } catch (Exception ex) {
                Debug.WriteLine($"Ошибка поиска локации номера: {ex.Message}");
                return Rectangle.Empty;
            }
        }

        private bool IsValidPlateRect(Rect rect, double imageArea) {
            double aspectRatio = (double)rect.Width / rect.Height;
            double areaRatio = (rect.Width * rect.Height) / imageArea;

            return aspectRatio > _options.MinAspectRatio &&
                   aspectRatio < _options.MaxAspectRatio &&
                   areaRatio > _options.MinAreaRatio &&
                   areaRatio < _options.MaxAreaRatio &&
                   rect.Width > _options.MinPlateWidth &&
                   rect.Height > _options.MinPlateHeight &&
                   rect.Width < _options.MaxPlateWidth &&
                   rect.Height < _options.MaxPlateHeight;
        }
    }
}
