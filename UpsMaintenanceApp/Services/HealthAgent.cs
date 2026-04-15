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
            "You are a UPS component health scoring expert. " +
            "Score each component 0-100 where 100 = perfect health, 0 = failed/critical. " +
            "Base scores ONLY on conditions observed while the UPS was in normal online operation " +
            "(input supply present, inverter running). " +
            "Follow all CRITICAL ANALYSIS RULES in the context — an intentionally commanded-off " +
            "inverter ('X - Inverter_ON' alarm) must NOT reduce the PowerStageHealth score. " +
            "Respond in strict JSON only with integer keys: " +
            "BatteryHealth (0-100), DcLinkHealth (0-100), PowerStageHealth (0-100), ThermalStress (0-100, " +
            "where 100 = maximum thermal stress, 0 = no thermal stress).";

        public HealthAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<HealthResult> AnalyzeAsync(
            Dictionary<string, double> features,
            CoreSignalResult           signal,
            string                     operationalContext,
            string                     alarmContext)
        {
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);
            features.TryGetValue("VinUnbalancePu",   out double vinUnbal);

            string obs  = string.Join("; ", signal.KeyObservations);
            string user =
                $"{operationalContext}\n" +
                $"=== SIGNAL ANALYSIS RESULTS ===\n" +
                $"DC Stability     : {signal.DcStability}\n" +
                $"Battery Behavior : {signal.BatteryBehavior}\n" +
                $"Freq Stability   : {signal.FrequencyStability}\n" +
                $"Stress Level     : {signal.StressLevel}\n" +
                $"Key Observations : {obs}\n\n" +
                $"=== TELEMETRY METRICS ===\n" +
                $"VdcStd={vdcStd:F4} V, RbattProxy={rbatt:F4} Ω\n" +
                $"AlarmRate={alarmRate:F2}/hr, VinUnbalance={vinUnbal:F4} pu\n\n" +
                $"{alarmContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<HealthResult>(json, _opts)
                   ?? new HealthResult();
        }
    }
}
