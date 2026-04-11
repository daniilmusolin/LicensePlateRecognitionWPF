namespace LicensePlateRecognition.Services.Interfaces {
    public interface IPlateNumberNormalizer {
        string Normalize(string plateNumber);
        string ExtractRegionCode(string plateNumber);
        bool IsValidFormat(string plateNumber);
    }
}
