using LicensePlateRecognition.Models;

namespace LicensePlateRecognition.Utils {
    public static class PlateMappingExtensions {
        public static DetectedPlate ToDetectedPlate(this LicensePlate licensePlate) {
            if (licensePlate == null)
                return null;

            return new DetectedPlate {
                Number = licensePlate.Number,
                Confidence = licensePlate.Confidence,
                DetectionTime = DateTime.Now,
                RegionCode = ExtractRegionCode(licensePlate.Number),
                Location = Rectangle.Empty // Или получите из контекста
            };
        }

        private static string ExtractRegionCode(string plateNumber) {
            var digits = new string(plateNumber.Where(char.IsDigit).ToArray());
            if (digits.Length >= 2) {
                return digits.Length >= 3 ? digits.Substring(digits.Length - 3) : digits.Substring(digits.Length - 2);
            }
            return "??";
        }
    }
}
