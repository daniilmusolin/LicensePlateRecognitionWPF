namespace LicensePlateRecognition.Models {
    public class DetectedPlate {
        public string Number { get; set; }
        public string RegionCode { get; set; }
        public double Confidence { get; set; }
        public Rectangle Location { get; set; }
        public DateTime DetectionTime { get; set; }
    }
}
