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
            "You are a UPS maintenance advisor. Generate concise, actionable maintenance insights. " +
            "Respond in JSON only with: Summary (1-2 sentence string), " +
            "TopIssues (array of max 4 strings), Actions (array of max 4 strings), " +
            "Confidence (Low/Medium/High).";
 
        public InsightAgent(OpenAIClientService ai) => _ai = ai;
 
        public async Task<InsightResult> AnalyzeAsync(
            CoreSignalResult       signal,
            HealthResult           health,
            EventCorrelationResult events,
            PredictionResult       prediction)
        {
            string patterns = string.Join("; ", events.Patterns);
            string obs      = string.Join("; ", signal.KeyObservations);
 
            string user =
                $"Urgency={prediction.Urgency}, Stress={signal.StressLevel}\n" +
                $"Failure Risks: Battery={prediction.BatteryFail}%, " +
                $"Inverter={prediction.InverterFail}%, Rectifier={prediction.RectifierFail}%\n" +
                $"Health: Battery={health.BatteryHealth}, DC Link={health.DcLinkHealth}, " +
                $"Power Stage={health.PowerStageHealth}, Thermal={health.ThermalStress}\n" +
                $"Root Cause: {events.RootCause} (Confidence={events.Confidence})\n" +
                $"Patterns: {patterns}\n" +
                $"Observations: {obs}";
 
            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<InsightResult>(json, _opts)
                   ?? new InsightResult();
        }
    }
}