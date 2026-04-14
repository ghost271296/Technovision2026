using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
 
namespace UpsMaintenanceApp.Services
{
    public class HealthResult
    {
        public int BatteryHealth    { get; set; }
        public int DcLinkHealth     { get; set; }
        public int PowerStageHealth { get; set; }
        public int ThermalStress    { get; set; }
    }
 
    public class HealthAgent
    {
        private readonly OpenAIClientService _ai;
        private static readonly JsonSerializerOptions _opts =
            new() { PropertyNameCaseInsensitive = true };
 
        private const string SystemPrompt =
            "You are a UPS health scoring expert. Score each component 0-100 where 100=perfect health. " +
            "Respond in JSON only with integer keys: BatteryHealth, DcLinkHealth, PowerStageHealth, ThermalStress.";
 
        public HealthAgent(OpenAIClientService ai) => _ai = ai;
 
        public async Task<HealthResult> AnalyzeAsync(
            Dictionary<string, double> features,
            CoreSignalResult           signal)
        {
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);
 
            string obs  = string.Join("; ", signal.KeyObservations);
            string user =
                $"Signal Analysis:\n" +
                $"  DC Stability={signal.DcStability}, Battery={signal.BatteryBehavior}\n" +
                $"  Frequency={signal.FrequencyStability}, Stress={signal.StressLevel}\n" +
                $"  Observations: {obs}\n" +
                $"Metrics: VdcStd={vdcStd:F4}V, RbattProxy={rbatt:F4}Ω, AlarmRate={alarmRate:F2}/hr";
 
            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<HealthResult>(json, _opts)
                   ?? new HealthResult();
        }
    }
}