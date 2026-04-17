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
            "You are given a TIME-CORRELATED LOG that merges real telemetry snapshots and alarm events " +
            "in chronological order. Each line is timestamped. " +
            "Telemetry lines show: Vdc (DC bus V), Vout (output V), Freq (Hz), Vbatt (battery V), " +
            "Iout (output current A), Pout (power kW). " +
            "Alarm lines are marked '*** ALARM' (active fault) or '*** STATUS REMOVED' (cleared/intentional). " +
            "Use this merged view to identify cause-effect chains by correlating voltage/current changes " +
            "with alarm events at the same or nearby timestamps.\n" +
            "Follow all CRITICAL ANALYSIS RULES in the context strictly:\n" +
            "- Only flag faults that occurred while the DC bus was energised (Vdc > 100 V).\n" +
            "- '*** STATUS REMOVED' entries are CLEARED or INTENTIONAL states — not new faults.\n" +
            "- MCCB events are deliberate engineer actions — never faults.\n" +
            "- Look for sequences: e.g. Vdc drops → ALARM fires → STATUS REMOVED (UPS protective response).\n" +
            "Respond in strict JSON only with: " +
            "Patterns (array of up to 5 strings describing distinct fault chains with timestamps if available), " +
            "RootCause (string — the single most probable underlying cause), " +
            "Confidence (Low / Medium / High).";

        public EventCorrelationAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<EventCorrelationResult> AnalyzeAsync(
            Dictionary<string, double> features,
            string operationalContext,
            string timelineContext)
        {
            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("VinAvg",           out double vinAvg);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);

            string user =
                $"{operationalContext}\n" +
                $"=== AGGREGATE TELEMETRY METRICS ===\n" +
                $"VdcMean={vdcMean:F2} V, VdcStd={vdcStd:F4} V\n" +
                $"VinAvg={vinAvg:F2} V, RbattProxy={rbatt:F4} Ω, AlarmRate={alarmRate:F2}/hr\n\n" +
                $"{timelineContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<EventCorrelationResult>(json, _opts)
                   ?? new EventCorrelationResult();
        }
    }
}
