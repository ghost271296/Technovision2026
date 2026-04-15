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
            "You are a UPS failure pattern analyst. " +
            "Analyze the alarm sequence and telemetry to identify cause-effect chains and the root cause. " +
            "Follow all CRITICAL ANALYSIS RULES in the context strictly:\n" +
            "- Only identify fault patterns that occurred during active online operation.\n" +
            "- Entries with 'X - ' prefix are DEACTIVATED/CLEARED states, not new faults.\n" +
            "  Example: 'X - Inverter_ON' means the inverter was commanded OFF (intentional), NOT a failure.\n" +
            "  Example: 'X - Load_ON_Inverter' means load moved off inverter (intentional action).\n" +
            "- Entries WITHOUT 'X - ' prefix are ACTIVE faults/alerts at that timestamp.\n" +
            "- Look for sequences: a fault alarm followed shortly by 'X - SomeStatus' often means the " +
            "  UPS responded to the fault by switching modes (expected protective behavior).\n" +
            "Respond in strict JSON only with: " +
            "Patterns (array of up to 5 strings describing distinct fault chains during online operation), " +
            "RootCause (string — the single most probable underlying cause), " +
            "Confidence (Low / Medium / High).";

        public EventCorrelationAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<EventCorrelationResult> AnalyzeAsync(
            Dictionary<string, double> features,
            string operationalContext,
            string alarmContext)
        {
            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("VinAvg",           out double vinAvg);
            features.TryGetValue("VinUnbalancePu",   out double vinUnbal);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);

            string user =
                $"{operationalContext}\n" +
                $"=== TELEMETRY METRICS ===\n" +
                $"VdcMean={vdcMean:F2} V, VdcStd={vdcStd:F4} V\n" +
                $"VinAvg={vinAvg:F2} V, VinUnbalance={vinUnbal:F4} pu\n" +
                $"RbattProxy={rbatt:F4} Ω, AlarmRate={alarmRate:F2}/hr\n\n" +
                $"{alarmContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<EventCorrelationResult>(json, _opts)
                   ?? new EventCorrelationResult();
        }
    }
}
