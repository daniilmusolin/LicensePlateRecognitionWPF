using System.Text.RegularExpressions;
using LicensePlateRecognition.Models;

namespace LicensePlateRecognition.Helpers {
    public class LicensePlateValidator {
        // Российский номер: Б 123 ББ 123
        private readonly Regex _russianPlatePattern = new Regex(
            @"([АВЕКМНОРСТУХ]{1})(\d{3})([АВЕКМНОРСТУХ]{2})(\d{2,3})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public LicensePlate ExtractLicensePlate(string text) {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var match = _russianPlatePattern.Match(text);
            if (match.Success) {
                string number = match.Groups[1].Value.ToUpper() +
                               match.Groups[2].Value +
                               match.Groups[3].Value.ToUpper();
                string region = match.Groups[4].Value;

                return new LicensePlate {
                    Number = number,
                    CountryCode = "RUS",
                    Confidence = 0.95,
                    IsValid = true
                };
            }

            return null;
        }

        public string ExtractRegionCode(string plateNumber) {
            var match = _russianPlatePattern.Match(plateNumber);
            if (match.Success) {
                return match.Groups[4].Value;
            }
            return "??";
        }

        public string ExtractCleanNumber(string plateNumber) {
            var match = _russianPlatePattern.Match(plateNumber);
            if (match.Success) {
                return match.Groups[1].Value.ToUpper() +
                       match.Groups[2].Value +
                       match.Groups[3].Value.ToUpper();
            }
            return plateNumber;
        }
    }
}