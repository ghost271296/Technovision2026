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
            "You are a UPS signal analysis expert. Analyze the provided UPS telemetry. " +
            "Respond in JSON only with keys: DcStability (string), BatteryBehavior (string), " +
            "FrequencyStability (string), StressLevel (Low/Medium/High/Critical), " +
            "KeyObservations (array of concise strings).";
 
        public CoreSignalAgent(OpenAIClientService ai) => _ai = ai;
 
        public async Task<CoreSignalResult> AnalyzeAsync(Dictionary<string, double> features)
        {
            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);
            features.TryGetValue("FoutStd",          out double foutStd);
 
            string user =
                $"DC Bus Voltage Mean : {vdcMean:F2} V\n" +
                $"DC Bus Voltage Std  : {vdcStd:F4} V\n" +
                $"Battery R Proxy     : {rbatt:F4} Ω\n" +
                $"Output Freq Std     : {foutStd:F4} Hz\n" +
                $"Alarm Rate          : {alarmRate:F2} alarms/hr";
 
            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<CoreSignalResult>(json, _opts)
                   ?? new CoreSignalResult();
        }
    }
}