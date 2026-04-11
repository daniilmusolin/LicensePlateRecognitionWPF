using LicensePlateRecognitionWPF.Services.Interfaces;
using System.Collections.Concurrent;
using Timer = System.Threading.Timer;

namespace LicensePlateRecognitionWPF.Services.Implementations {
    public class PlateDetectionCache : IPlateDetectionCache {
        private readonly ConcurrentDictionary<string, DateTime> _detectedPlates;
        private readonly TimeSpan _cooldownPeriod;
        private readonly Timer _cleanupTimer;

        public PlateDetectionCache(TimeSpan cooldownPeriod) {
            _cooldownPeriod = cooldownPeriod;
            _detectedPlates = new ConcurrentDictionary<string, DateTime>();
            _cleanupTimer = new Timer(
                 _ => Cleanup(),           // Callback метод (вызывает Cleanup)
                 null,                     // State object (не используется)
                 TimeSpan.FromMinutes(1),  // Начать через 1 минуту
                 TimeSpan.FromMinutes(1)   // Повторять каждую минуту
             );
        }

        public bool IsPlateOnCooldown(string plateNumber) {
            if (string.IsNullOrWhiteSpace(plateNumber))
                return false;

            if (_detectedPlates.TryGetValue(plateNumber, out var lastDetection)) {
                return (DateTime.Now - lastDetection) < _cooldownPeriod;
            }

            return false;
        }

        public void RegisterDetection(string plateNumber) {
            if (string.IsNullOrWhiteSpace(plateNumber))
                return;

            _detectedPlates.AddOrUpdate(plateNumber, DateTime.Now, (_, _) => DateTime.Now);
        }

        public void Cleanup() {
            var threshold = DateTime.Now - TimeSpan.FromMinutes(5);

            foreach (var kvp in _detectedPlates) {
                if (kvp.Value < threshold) {
                    _detectedPlates.TryRemove(kvp.Key, out _);
                }
            }
        }

        public int GetActiveDetectionsCount() => _detectedPlates.Count;
    }
}
