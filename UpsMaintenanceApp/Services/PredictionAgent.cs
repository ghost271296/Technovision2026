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
            "You are a UPS predictive maintenance expert. " +
            "Predict the probability of component failure within the next maintenance window (0-100, " +
            "where 100 = imminent/certain failure, 0 = no risk detected). " +
            "Base predictions ONLY on faults observed during normal online operation — " +
            "follow all CRITICAL ANALYSIS RULES in the context. " +
            "An intentionally commanded-off inverter ('X - Inverter_ON') must NOT increase InverterFail risk. " +
            "Consider both active alarms (genuine faults) and cleared alarms (transient events). " +
            "Respond in strict JSON only with integer fields: " +
            "InverterFail (0-100), RectifierFail (0-100), BatteryFail (0-100), " +
            "Urgency (Immediate / High / Medium / Low).";

        public PredictionAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<PredictionResult> AnalyzeAsync(
            CoreSignalResult       signal,
            HealthResult           health,
            EventCorrelationResult events,
            string                 operationalContext,
            string                 alarmContext)
        {
            string patterns = string.Join("; ", events.Patterns);
            string user =
                $"{operationalContext}\n" +
                $"=== HEALTH SCORES (0-100, higher=healthier) ===\n" +
                $"Battery={health.BatteryHealth}, DC Link={health.DcLinkHealth}, " +
                $"Power Stage={health.PowerStageHealth}, Thermal Stress={health.ThermalStress}\n\n" +
                $"=== SIGNAL ANALYSIS ===\n" +
                $"Stress={signal.StressLevel}, DC={signal.DcStability}, " +
                $"Battery={signal.BatteryBehavior}, Freq={signal.FrequencyStability}\n\n" +
                $"=== EVENT CORRELATION ===\n" +
                $"Root Cause: {events.RootCause} (Confidence: {events.Confidence})\n" +
                $"Fault Patterns: {patterns}\n\n" +
                $"{alarmContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<PredictionResult>(json, _opts)
                   ?? new PredictionResult();
        }
    }
}
