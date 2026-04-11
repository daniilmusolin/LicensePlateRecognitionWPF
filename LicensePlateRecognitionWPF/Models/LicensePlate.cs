using System.Drawing;

namespace LicensePlateRecognitionWPF.Models {
    public class LicensePlate {
        public string Number { get; set; }
        public string CountryCode { get; set; }
        public double Confidence { get; set; }
        public bool IsValid { get; set; }
        public Rectangle? Location { get; set; }
        public DateTime DetectionTime { get; set; }
        public string RegionCode { get; set; }

        public LicensePlate() {
            Number = string.Empty;
            CountryCode = "RUS";
            Confidence = 0.0;
            IsValid = false;
            Location = null;
            DetectionTime = DateTime.Now;
            RegionCode = string.Empty;
        }

        public override string ToString() {
            return $"{Number} {RegionCode} ({(Confidence * 100):F0}%)";
        }
    }
}