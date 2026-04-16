using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace UpsMaintenanceApp.Services
{
    public class CoreSignalResult
    {
        public string       DcStability        { get; set; } = string.Empty;
        public string       BatteryBehavior    { get; set; } = string.Empty;
        public string       FrequencyStability { get; set; } = string.Empty;
        public string       StressLevel        { get; set; } = string.Empty;
        public List<string> KeyObservations    { get; set; } = new();
    }

    public class CoreSignalAgent
    {
        private readonly OpenAIClientService _ai;
        private static readonly JsonSerializerOptions _opts =
            new() { PropertyNameCaseInsensitive = true };

        private const string SystemPrompt =
            "You are a senior UPS electrical engineer analyzing real telemetry and alarm data. " +
            "Your job: assess DC bus stability, battery behavior, and output frequency quality " +
            "ONLY during periods when the UPS input supply was present and the inverter was running. " +
            "Follow all CRITICAL ANALYSIS RULES provided in the context — especially do not " +
            "flag intentional shutdowns or commanded-off states as faults. " +
            "Respond in strict JSON only with keys: " +
            "DcStability (one of: Stable / Minor Ripple / Moderate Ripple / Unstable), " +
            "BatteryBehavior (one of: Healthy / Ageing / Weak / Critical), " +
            "FrequencyStability (one of: Stable / Minor Deviation / Significant Deviation), " +
            "StressLevel (one of: Low / Medium / High / Critical), " +
            "KeyObservations (array of up to 5 concise factual strings about anomalies seen during active operation).";

        public CoreSignalAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<CoreSignalResult> AnalyzeAsync(
            Dictionary<string, double> features,
            string operationalContext,
            string alarmContext)
        {
            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);
            features.TryGetValue("FoutStd",          out double foutStd);
            features.TryGetValue("VinAvg",           out double vinAvg);
            features.TryGetValue("VoutMean",         out double voutMean);
            features.TryGetValue("PowerMean",        out double powerMean);

            string user =
                $"{operationalContext}\n" +
                $"=== TELEMETRY METRICS (computed during online periods only) ===\n" +
                $"DC Bus Voltage Mean      : {vdcMean:F2} V\n" +
                $"DC Bus Voltage Std       : {vdcStd:F4} V\n" +
                $"Battery Resistance Proxy : {rbatt:F4} Ω\n" +
                $"Output Frequency Std     : {foutStd:F4} Hz\n" +
                $"Bypass Voltage Avg       : {vinAvg:F2} V\n" +
                $"Output Voltage Mean (Vout): {voutMean:F2} V\n" +
                $"Output Power Mean        : {powerMean:F2} kW\n" +
                $"Alarm Rate               : {alarmRate:F2} alarms/hr\n\n" +
                $"{alarmContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<CoreSignalResult>(json, _opts)
                   ?? new CoreSignalResult();
        }
    }
}
