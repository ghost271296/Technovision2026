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
            "You are a UPS failure pattern analyst. Detect failure chains and correlations. " +
            "Respond in JSON only with: Patterns (array of concise strings describing each chain), " +
            "RootCause (string), Confidence (Low/Medium/High).";
 
        public EventCorrelationAgent(OpenAIClientService ai) => _ai = ai;
 
        public async Task<EventCorrelationResult> AnalyzeAsync(Dictionary<string, double> features)
        {
            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("VinAvg",           out double vinAvg);
            features.TryGetValue("VinUnbalancePu",   out double vinUnbal);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);
 
            string user =
                $"VdcMean={vdcMean:F2}V, VdcStd={vdcStd:F4}V\n" +
                $"VinAvg={vinAvg:F2}V, VinUnbalance={vinUnbal:F4}pu\n" +
                $"RbattProxy={rbatt:F4}Ω, AlarmRate={alarmRate:F2}/hr";
 
            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<EventCorrelationResult>(json, _opts)
                   ?? new EventCorrelationResult();
        }
    }
}