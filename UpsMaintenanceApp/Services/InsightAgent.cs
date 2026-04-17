using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace UpsMaintenanceApp.Services
{
    public class InsightResult
    {
        public string       Summary    { get; set; } = string.Empty;
        public List<string> TopIssues  { get; set; } = new();
        public List<string> Actions    { get; set; } = new();
        public string       Confidence { get; set; } = string.Empty;
    }

    public class InsightAgent
    {
        private readonly OpenAIClientService _ai;
        private static readonly JsonSerializerOptions _opts =
            new() { PropertyNameCaseInsensitive = true };

        private const string SystemPrompt =
            "You are a senior UPS maintenance advisor writing an executive summary for a field engineer. " +
            "You receive: (a) aggregated analysis scores from 4 prior AI agents, " +
            "(b) a time-correlated log merging real telemetry snapshots and alarm events by timestamp. " +
            "Use the timeline to ground your summary in specific events with timestamps. " +
            "Follow all CRITICAL ANALYSIS RULES in the context:\n" +
            "- Only report issues observed during normal online operation (Vdc > 100 V).\n" +
            "- Clearly distinguish GENUINE FAULTS ('*** ALARM' entries) from " +
            "INTENTIONAL ACTIONS ('*** STATUS REMOVED' entries and MCCB events).\n" +
            "- If the UPS was intentionally off/bypassed for much of the log, state this explicitly.\n" +
            "Respond in strict JSON only with: " +
            "Summary (2-3 sentence plain-English summary referencing key timestamps if available), " +
            "TopIssues (array of max 4 strings — only genuine faults with timestamps where possible), " +
            "Actions (array of max 4 specific, prioritised maintenance action strings), " +
            "Confidence (Low / Medium / High).";

        public InsightAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<InsightResult> AnalyzeAsync(
            CoreSignalResult       signal,
            HealthResult           health,
            EventCorrelationResult events,
            PredictionResult       prediction,
            string                 operationalContext,
            string                 alarmContext,
            string                 timelineContext)
        {
            string patterns = string.Join("; ", events.Patterns);
            string obs      = string.Join("; ", signal.KeyObservations);

            string user =
                $"{operationalContext}\n" +
                $"=== FULL ANALYSIS RESULTS ===\n" +
                $"Urgency: {prediction.Urgency}  |  Stress: {signal.StressLevel}\n" +
                $"Failure Risks: Battery={prediction.BatteryFail}%, " +
                $"Inverter={prediction.InverterFail}%, Rectifier={prediction.RectifierFail}%\n" +
                $"Health Scores: Battery={health.BatteryHealth}, DC Link={health.DcLinkHealth}, " +
                $"Power Stage={health.PowerStageHealth}, Thermal Stress={health.ThermalStress}\n" +
                $"DC={signal.DcStability}, Battery={signal.BatteryBehavior}, Freq={signal.FrequencyStability}\n" +
                $"Root Cause: {events.RootCause} (Confidence: {events.Confidence})\n" +
                $"Patterns: {patterns}\n" +
                $"Observations: {obs}\n\n" +
                $"{alarmContext}\n" +
                $"{timelineContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<InsightResult>(json, _opts)
                   ?? new InsightResult();
        }
    }
}
