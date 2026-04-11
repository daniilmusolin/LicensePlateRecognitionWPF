using LicensePlateRecognitionWPF.Services.Interfaces;

namespace LicensePlateRecognitionWPF.Helpers {
    public class PlateNumberNormalizer : IPlateNumberNormalizer {
        public string Normalize(string plateNumber) {
            if (string.IsNullOrWhiteSpace(plateNumber))
                return plateNumber;

            // Извлечение букв и цифр
            var letters = new string(plateNumber.Where(char.IsLetter).ToArray());
            var digits = new string(plateNumber.Where(char.IsDigit).ToArray());

            if (letters.Length >= 3 && digits.Length >= 3) {
                // Формат: БУКВА + 3 ЦИФРЫ + 2 БУКВЫ
                return (letters.Substring(0, 1) +
                        digits.Substring(0, Math.Min(3, digits.Length)) +
                        letters.Substring(1, Math.Min(2, letters.Length - 1))).ToUpper();
            }

            return plateNumber.ToUpper();
        }

        public string ExtractRegionCode(string plateNumber) {
            var digits = new string(plateNumber.Where(char.IsDigit).ToArray());

            if (digits.Length >= 2) {
                return digits.Length >= 3
                    ? digits.Substring(digits.Length - 3)
                    : digits.Substring(digits.Length - 2);
            }

            return "??";
        }

        public bool IsValidFormat(string plateNumber) {
            if (string.IsNullOrWhiteSpace(plateNumber))
                return false;

            // Пример: A123BC или A123BC77
            var regex = new System.Text.RegularExpressions.Regex(@"^[АВЕКМНОРСТУХ]\d{3}[АВЕКМНОРСТУХ]{2}$");
            return regex.IsMatch(plateNumber.ToUpper());
        }
    }
}
