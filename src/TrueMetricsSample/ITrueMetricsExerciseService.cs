using System;
using System.Threading.Tasks;

namespace TrueMetricsSample
{
    public interface ITrueMetricsExerciseService
    {
        Task<string> InitAsync(string apiKey);
        Task<string> GetStatusAsync();
        Task<string> GetDeviceIdAsync();
        Task<string> GetSensorStatisticsAsync();
        Task<string> StartRecordingAsync();
        Task<string> EnableSensorsAsync();
        Task<string> DisableSensorsAsync();
        Task<string> StopRecordingAsync();
        Task<string> MetadataDemoAsync();
        Task<string> DeinitializeAsync();
        Task<string> RunAllAsync(string apiKey);
    }
}

