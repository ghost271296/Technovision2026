using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace UpsMaintenanceApp.Services
{
    public class EventCorrelationResult
    {
        public List<string> Patterns   { get; set; } = new();
        public string       RootCause  { get; set; } = string.Empty;
        public string       Confidence { get; set; } = string.Empty;
    }

    public class EventCorrelationAgent
    {
        private readonly OpenAIClientService _ai;
        private static readonly JsonSerializerOptions _opts =
            new() { PropertyNameCaseInsensitive = true };

        private const string SystemPrompt =
            "You are a UPS failure pattern analyst. Detect failure chains and correlations from telemetry metrics AND the alarm log. " +
            "IMPORTANT alarm log rules: " +
            "- Entries WITHOUT 'X - ' prefix = alarm/status is ACTIVE (e.g. 'Inverter_Fail' means inverter has failed). " +
            "- Entries WITH 'X - ' prefix = alarm/status was DEACTIVATED/REMOVED (e.g. 'X - Inverter_ON' means inverter switched OFF, 'X - Load_ON_Inverter' means load removed from inverter). " +
            "Use the sequence and timing of alarms to identify cause-effect chains. " +
            "Respond in JSON only with: Patterns (array of up to 5 concise strings describing each chain), " +
            "RootCause (string — the most probable underlying cause), Confidence (Low/Medium/High).";

        public EventCorrelationAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<EventCorrelationResult> AnalyzeAsync(
            Dictionary<string, double> features,
            string alarmContext)
        {
            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("VinAvg",           out double vinAvg);
            features.TryGetValue("VinUnbalancePu",   out double vinUnbal);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);

            string user =
                $"=== TELEMETRY METRICS ===\n" +
                $"VdcMean={vdcMean:F2}V, VdcStd={vdcStd:F4}V\n" +
                $"VinAvg={vinAvg:F2}V, VinUnbalance={vinUnbal:F4}pu\n" +
                $"RbattProxy={rbatt:F4}Ω, AlarmRate={alarmRate:F2}/hr\n\n" +
                $"=== ALARM LOG (chronological) ===\n{alarmContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<EventCorrelationResult>(json, _opts)
                   ?? new EventCorrelationResult();
        }
    }
}
