namespace LicensePlateRecognition.Services.Interfaces {
    public interface IPlateDetectionCache {
        bool IsPlateOnCooldown(string plateNumber);
        void RegisterDetection(string plateNumber);
        void Cleanup();
        int GetActiveDetectionsCount();
    }
}
