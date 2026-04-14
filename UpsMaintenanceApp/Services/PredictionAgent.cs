using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
 
namespace UpsMaintenanceApp.Services
{
    public class PredictionResult
    {
        public int    InverterFail  { get; set; }
        public int    RectifierFail { get; set; }
        public int    BatteryFail   { get; set; }
        public string Urgency       { get; set; } = string.Empty;
    }
 
    public class PredictionAgent
    {
        private readonly OpenAIClientService _ai;
        private static readonly JsonSerializerOptions _opts =
            new() { PropertyNameCaseInsensitive = true };
 
        private const string SystemPrompt =
            "You are a UPS predictive maintenance expert. Predict component failure risk scores 0-100 " +
            "(100=imminent failure). Respond in JSON only with: " +
            "InverterFail (int), RectifierFail (int), BatteryFail (int), " +
            "Urgency (Immediate/High/Medium/Low).";
 
        public PredictionAgent(OpenAIClientService ai) => _ai = ai;
 
        public async Task<PredictionResult> AnalyzeAsync(
            CoreSignalResult       signal,
            HealthResult           health,
            EventCorrelationResult events)
        {
            string patterns = string.Join("; ", events.Patterns);
            string user =
                $"Health Scores: Battery={health.BatteryHealth}, DC Link={health.DcLinkHealth}, " +
                $"Power Stage={health.PowerStageHealth}, Thermal={health.ThermalStress}\n" +
                $"Signal: Stress={signal.StressLevel}, DC={signal.DcStability}, " +
                $"Battery={signal.BatteryBehavior}\n" +
                $"Root Cause={events.RootCause} (Confidence={events.Confidence})\n" +
                $"Failure Patterns: {patterns}";
 
            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<PredictionResult>(json, _opts)
                   ?? new PredictionResult();
        }
    }
}