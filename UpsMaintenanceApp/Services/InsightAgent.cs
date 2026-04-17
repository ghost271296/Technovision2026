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
            "You receive aggregated analysis scores from 4 prior AI agents plus a time-correlated log.\n" +
            "Rules:\n" +
            "- Only report issues observed during normal online operation (inverter running).\n" +
            "- 'Input_MCCB_OFF' causing Rectifier_Fail = maintenance action, NOT a fault.\n" +
            "- '*** STATUS REMOVED' entries = intentional deactivations, not faults.\n" +
            "- Reference specific timestamps from the timeline when possible.\n" +
            "Respond in strict JSON only with: " +
            "Summary (2-3 sentences), " +
            "TopIssues (array max 4, genuine faults with timestamps), " +
            "Actions (array max 4, specific prioritised maintenance actions), " +
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

            // Use timelineContext (already contains alarm events inline) — do NOT append
            // alarmContext separately to avoid token overflow from duplicate data.
            string user =
                $"{operationalContext}\n" +
                $"=== ANALYSIS SUMMARY ===\n" +
                $"Urgency={prediction.Urgency}  Stress={signal.StressLevel}  " +
                $"Risks: Inv={prediction.InverterFail}%  Rect={prediction.RectifierFail}%  Batt={prediction.BatteryFail}%\n" +
                $"Health: Batt={health.BatteryHealth}  DCLink={health.DcLinkHealth}  " +
                $"PowerStage={health.PowerStageHealth}  Thermal={health.ThermalStress}\n" +
                $"Input={signal.InputVoltageVariation}  Eff={signal.EfficiencyRating}  " +
                $"DCLink={signal.DcLinkVariation}  Output={signal.OutputVoltageVariation}  " +
                $"BypFreq={signal.BypassFreqVariation}  BypVolt={signal.BypassVoltageVariation}  " +
                $"Battery={signal.BatteryBackupStatus}\n" +
                $"RootCause={events.RootCause} ({events.Confidence})\n" +
                $"Patterns: {patterns}\n" +
                $"Observations: {obs}\n\n" +
                $"{timelineContext}";

            string json = await _ai.CallAsync(SystemPrompt, user, maxTokens: 3000);
            return JsonSerializer.Deserialize<InsightResult>(json, _opts)
                   ?? new InsightResult();
        }
    }
}
