using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace UpsMaintenanceApp.Services
{
    public class CoreSignalResult
    {
        // Upstream / Input
        public string InputVoltageVariation  { get; set; } = "N/A";
        public string EfficiencyRating       { get; set; } = "N/A";
        // Bypass
        public string BypassFreqVariation    { get; set; } = "N/A";
        public string BypassVoltageVariation { get; set; } = "N/A";
        // DC Link
        public string DcLinkVariation        { get; set; } = "N/A";
        // Battery
        public string BatteryBackupStatus    { get; set; } = "N/A";
        // Output
        public string OutputVoltageVariation { get; set; } = "N/A";
        // Overall
        public string       StressLevel     { get; set; } = string.Empty;
        public List<string> KeyObservations { get; set; } = new();
    }

    public class CoreSignalAgent
    {
        private readonly OpenAIClientService _ai;
        private static readonly JsonSerializerOptions _opts =
            new() { PropertyNameCaseInsensitive = true };

        private const string SystemPrompt =
            "You are a senior Power Electronics UPS Design Engineer analyzing real DataLog and AlarmLog data. " +
            "Assess each subsystem ONLY during the correct operational window:\n\n" +

            "UPSTREAM / INPUT VOLTAGE (Vr Input, Vy Input, Vb Input — or Vbypass as single-phase proxy):\n" +
            "  Analyze input voltage variation ONLY when 'Input_MCCB_OFF' is NOT active " +
            "(i.e. 'X - Input_MCCB_OFF' present in alarm log = MCCB back ON, or no MCCB_OFF alarm at all).\n" +
            "  Categorize: Stable / Minor Variation / Significant Variation / N/A\n\n" +

            "EFFICIENCY (Output Power / Input Power × 100):\n" +
            "  3-phase: (Vout×Iout) / (Vr×Ir + Vy×Iy + Vb×Ib) — convert to same kW units.\n" +
            "  Single-phase fallback: P_Out_kW / (Vbypass×Ibypass/1000) × 100.\n" +
            "  Compute ONLY when all quantities are non-zero simultaneously.\n" +
            "  Categorize: Good >92% / Acceptable 85-92% / Poor <85% / N/A\n\n" +

            "BYPASS FREQUENCY VARIATION (Frequency Bypass):\n" +
            "  Analyze ONLY when bypass/mains is active " +
            "(alarm log shows 'X - Bypass Breaker OFF' = bypass breaker ON, or no active 'Bypass Breaker OFF').\n" +
            "  Categorize: Stable / Minor Deviation / Significant Deviation / N/A\n\n" +

            "BYPASS VOLTAGE VARIATION (Vbypass):\n" +
            "  Same operational window as Bypass Frequency.\n" +
            "  Categorize: Stable / Minor Variation / Significant Variation / N/A\n\n" +

            "DC LINK VARIATION (VdcLink):\n" +
            "  Analyze ONLY when 'Rectifier_ON' alarm is present (rectifier running).\n" +
            "  'Input_MCCB_OFF' causing 'X - Rectifier_ON' = intentional, NOT a fault.\n" +
            "  Categorize: Stable / Minor Ripple / Moderate Ripple / Unstable / N/A\n\n" +

            "BATTERY BACKUP:\n" +
            "  Report estimated total backup duration ONLY when 'Input_MCCB_OFF' AND battery breaker " +
            "status shows battery supplying load (mains absent, DC bus still energised).\n" +
            "  Categorize: Not Used / Short <5min / Moderate 5-30min / Extended >30min / N/A\n\n" +

            "OUTPUT VOLTAGE VARIATION (Vout):\n" +
            "  Analyze ONLY when 'Inverter_ON' alarm is present (inverter actively running).\n" +
            "  Categorize: Stable / Minor Variation / Significant Variation / N/A\n\n" +

            "RULES:\n" +
            "- 'Input_MCCB_OFF' is an intentional engineer action. It will trigger Rectifier_Fail — NOT a fault.\n" +
            "- 'X - ' prefix = alarm/status DEACTIVATED (component intentionally OFF).\n" +
            "- No 'X - ' prefix = alarm is ACTIVE (e.g. 'Inverter_ON' = inverter is ON and running).\n" +
            "- MCCB, bypass breaker, and battery breaker operations are maintenance actions — never faults.\n" +
            "- Only report genuine anomalies during active operation in KeyObservations.\n\n" +

            "Respond in strict JSON only with keys:\n" +
            "InputVoltageVariation, EfficiencyRating, BypassFreqVariation, BypassVoltageVariation,\n" +
            "DcLinkVariation, BatteryBackupStatus, OutputVoltageVariation,\n" +
            "StressLevel (Low/Medium/High/Critical),\n" +
            "KeyObservations (array of up to 5 concise strings).";

        public CoreSignalAgent(OpenAIClientService ai) => _ai = ai;

        public async Task<CoreSignalResult> AnalyzeAsync(
            Dictionary<string, double> features,
            string operationalContext,
            string alarmContext)
        {
            features.TryGetValue("VinAvg",           out double vinAvg);
            features.TryGetValue("VinStd",           out double vinStd);
            features.TryGetValue("VinUnbalancePu",   out double vinUnbal);
            features.TryGetValue("BypassVoltageStd", out double bypVStd);
            features.TryGetValue("BypassFreqStd",    out double bypFStd);
            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("RbattProxy",       out double rbatt);
            features.TryGetValue("BatteryBackupMin", out double battMin);
            features.TryGetValue("VoutMean",         out double voutMean);
            features.TryGetValue("VoutStd",          out double voutStd);
            features.TryGetValue("PowerMean",        out double powerMean);
            features.TryGetValue("EfficiencyMean",   out double effMean);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);

            string user =
                $"{operationalContext}\n" +
                $"=== COMPUTED TELEMETRY METRICS ===\n" +
                $"INPUT  : VinAvg={vinAvg:F1}V  VinStd={vinStd:F3}V  Unbalance={vinUnbal:F4}pu\n" +
                $"BYPASS : VbypassStd={bypVStd:F3}V  FreqBypassStd={bypFStd:F4}Hz\n" +
                $"DC LINK: VdcMean={vdcMean:F1}V  VdcStd={vdcStd:F4}V\n" +
                $"BATTERY: RbattProxy={rbatt:F4}Ω  BackupTime={battMin:F1}min\n" +
                $"OUTPUT : VoutMean={voutMean:F1}V  VoutStd={voutStd:F3}V  PowerMean={powerMean:F2}kW\n" +
                $"EFFICIENCY: {effMean:F1}% (0=not computable when Vbypass/Ibypass both zero)\n" +
                $"AlarmRate: {alarmRate:F2}/hr\n\n" +
                $"{alarmContext}";

            string json = await _ai.CallAsync(SystemPrompt, user);
            return JsonSerializer.Deserialize<CoreSignalResult>(json, _opts)
                   ?? new CoreSignalResult();
        }
    }
}
