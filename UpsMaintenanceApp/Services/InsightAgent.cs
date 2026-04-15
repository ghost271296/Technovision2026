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
            "You are a UPS maintenance advisor. Generate concise, actionable maintenance insights " +
            "based on all previous analysis stages AND the alarm log. " +
            "IMPORTANT alarm log rules: entries WITHOUT 'X - ' prefix = ACTIVE alarm/failure. " +
            "Entries WITH 'X - ' prefix = that status was DEACTIVATED (e.g. 'X - Inverter_ON' = inverter is currently OFF). " +
            "Respond in JSON only with: Summary (1-2 sentence string), " +
            "TopIssues (array of max 4 strings), Actions (array of max 4 actionable strings), " +
            "Confidence (Low/Medium/High).";

        public InsightAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<InsightResult> AnalyzeAsync(
            CoreSignalResult       signal,
            HealthResult           health,
            EventCorrelationResult events,
            PredictionResult       prediction,
            string                 alarmContext)
        {
            string patterns = string.Join("; ", events.Patterns);
            string obs      = string.Join("; ", signal.KeyObservations);

            string user =
                $"=== ANALYSIS RESULTS ===\n" +
                $"Urgency={prediction.Urgency}, Stress={signal.StressLevel}\n" +
                $"Failure Risks: Battery={prediction.BatteryFail}%, " +
                $"Inverter={prediction.InverterFail}%, Rectifier={prediction.RectifierFail}%\n" +
                $"Health: Battery={health.BatteryHealth}, DC Link={health.DcLinkHealth}, " +
                $"Power Stage={health.PowerStageHealth}, Thermal={health.ThermalStress}\n" +
                $"Root Cause: {events.RootCause} (Confidence={events.Confidence})\n" +
                $"Patterns: {patterns}\n" +
                $"Observations: {obs}\n\n" +
                $"=== ALARM LOG ===\n{alarmContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<InsightResult>(json, _opts)
                   ?? new InsightResult();
        }
    }
}
