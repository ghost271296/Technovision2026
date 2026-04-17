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
            "(b) a time-correlated log merging real telemetry snapshots and alarm events by timestamp.\n" +
            "Follow all CRITICAL ANALYSIS RULES in the context:\n" +
            "- Only report issues during normal online operation (DC bus energised, inverter running).\n" +
            "- Distinguish GENUINE FAULTS ('*** ALARM') from INTENTIONAL ACTIONS ('*** STATUS REMOVED' and MCCB events).\n" +
            "- 'Input_MCCB_OFF' causing Rectifier_Fail = maintenance action, NOT a fault — do not report.\n" +
            "- If UPS was intentionally off/bypassed for much of the log, state this explicitly.\n" +
            "Respond in strict JSON only with: " +
            "Summary (2-3 sentences referencing key timestamps where possible), " +
            "TopIssues (array max 4 — genuine faults only with timestamps), " +
            "Actions (array max 4 specific prioritised maintenance actions), " +
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
                $"Failure Risks: Battery={prediction.BatteryFail}%  Inverter={prediction.InverterFail}%  Rectifier={prediction.RectifierFail}%\n" +
                $"Health Scores: Battery={health.BatteryHealth}  DC Link={health.DcLinkHealth}  " +
                $"Power Stage={health.PowerStageHealth}  Thermal Stress={health.ThermalStress}\n" +
                $"Input={signal.InputVoltageVariation}  Efficiency={signal.EfficiencyRating}  " +
                $"DC Link={signal.DcLinkVariation}  Output={signal.OutputVoltageVariation}\n" +
                $"Bypass Freq={signal.BypassFreqVariation}  Bypass Volt={signal.BypassVoltageVariation}  " +
                $"Battery={signal.BatteryBackupStatus}\n" +
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
