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
            "You are a UPS signal analysis expert. Analyze the provided UPS telemetry metrics and alarm log. " +
            "IMPORTANT: In the alarm log, entries WITHOUT 'X - ' prefix are ACTIVE alarms. " +
            "Entries WITH 'X - ' prefix mean that alarm or status was DEACTIVATED/REMOVED (e.g. 'X - Inverter_ON' means the inverter switched OFF). " +
            "Respond in JSON only with keys: DcStability (string), BatteryBehavior (string), " +
            "FrequencyStability (string), StressLevel (Low/Medium/High/Critical), " +
            "KeyObservations (array of up to 5 concise strings).";

        public CoreSignalAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<CoreSignalResult> AnalyzeAsync(
            Dictionary<string, double> features,
            string alarmContext)
        {
            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);
            features.TryGetValue("FoutStd",          out double foutStd);
            features.TryGetValue("VinAvg",           out double vinAvg);
            features.TryGetValue("VinUnbalancePu",   out double vinUnbal);

            string user =
                $"=== TELEMETRY METRICS ===\n" +
                $"DC Bus Voltage Mean  : {vdcMean:F2} V\n" +
                $"DC Bus Voltage Std   : {vdcStd:F4} V\n" +
                $"Battery R Proxy      : {rbatt:F4} Ω\n" +
                $"Output Freq Std      : {foutStd:F4} Hz\n" +
                $"Input Voltage Avg    : {vinAvg:F2} V\n" +
                $"Input Unbalance      : {vinUnbal:F4} pu\n" +
                $"Alarm Rate           : {alarmRate:F2} alarms/hr\n\n" +
                $"=== ALARM LOG ===\n{alarmContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<CoreSignalResult>(json, _opts)
                   ?? new CoreSignalResult();
        }
    }
}
