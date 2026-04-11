namespace LicensePlateRecognitionWPF.Models {
    public class LocationFinderOptions {
        public int CannyThreshold1 { get; set; } = 100;
        public int CannyThreshold2 { get; set; } = 200;
        public double MinAspectRatio { get; set; } = 2.0;
        public double MaxAspectRatio { get; set; } = 6.0;
        public double MinAreaRatio { get; set; } = 0.01;
        public double MaxAreaRatio { get; set; } = 0.5;
        public int MinPlateWidth { get; set; } = 50;
        public int MinPlateHeight { get; set; } = 20;
        public int MaxPlateWidth { get; set; } = 500;
        public int MaxPlateHeight { get; set; } = 200;
    }
}
