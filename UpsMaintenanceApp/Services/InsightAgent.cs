using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UpsMaintenanceApp.Services
{
    public class InsightResult
    {
        public string       Summary    { get; set; } = string.Empty;
        [JsonConverter(typeof(StringOrObjectListConverter))]
        public List<string> TopIssues  { get; set; } = new();
        [JsonConverter(typeof(StringOrObjectListConverter))]
        public List<string> Actions    { get; set; } = new();
        public string       Confidence { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tolerant converter: accepts a JSON array whose elements are either plain strings
    /// or nested objects (GPT sometimes returns {timestamp, description} objects).
    /// Objects are flattened into a single readable string.
    /// </summary>
    public class StringOrObjectListConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<string>();
            if (reader.TokenType != JsonTokenType.StartArray) return list;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    list.Add(reader.GetString() ?? string.Empty);
                }
                else if (reader.TokenType == JsonTokenType.StartObject)
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    var sb = new StringBuilder();
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        string val = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString() ?? string.Empty
                            : prop.Value.ToString();
                        if (!string.IsNullOrWhiteSpace(val))
                            sb.Append(val.Trim()).Append(' ');
                    }
                    list.Add(sb.ToString().Trim());
                }
            }
            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var s in value) writer.WriteStringValue(s);
            writer.WriteEndArray();
        }
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
            "- Reference specific timestamps from the timeline when possible.\n\n" +
            "Respond ONLY with this exact JSON structure. " +
            "Every array element MUST be a plain string — do NOT use nested objects:\n" +
            "{\n" +
            "  \"Summary\": \"2-3 sentence executive summary\",\n" +
            "  \"TopIssues\": [\"[dd-Mon HH:mm] issue description\", \"...\"],\n" +
            "  \"Actions\": [\"Priority 1: specific action\", \"...\"],\n" +
            "  \"Confidence\": \"Low|Medium|High\"\n" +
            "}\n" +
            "TopIssues: array of up to 4 plain strings — genuine faults during inverter operation with timestamps.\n" +
            "Actions: array of up to 4 plain strings — specific prioritised maintenance actions.\n" +
            "CRITICAL: each TopIssues and Actions element must be a STRING, not an object.";

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

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<InsightResult>(json, _opts)
                   ?? new InsightResult();
        }
    }
}
