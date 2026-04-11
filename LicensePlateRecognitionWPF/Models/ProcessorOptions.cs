namespace LicensePlateRecognition.Models {
    public class ProcessorOptions {
        public int MinPlateWidth { get; set; } = 50;
        public int MinPlateHeight { get; set; } = 20;
        public int MaxPlateWidth { get; set; } = 500;
        public int MaxPlateHeight { get; set; } = 200;
        public bool EnableParallelProcessing { get; set; } = false;
        public int MaxParallelFrames { get; set; } = 3;
    }
}
