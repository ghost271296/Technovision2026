using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UpsMaintenanceApp.Services
{
    public class AnalysisPipeline
    {
        private readonly CoreSignalAgent       _coreSignal;
        private readonly HealthAgent           _health;
        private readonly EventCorrelationAgent _eventCorr;
        private readonly PredictionAgent       _prediction;
        private readonly InsightAgent          _insight;

        public AnalysisPipeline(string apiKey, string model = "gpt-4o")
        {
            var ai   = new OpenAIClientService(apiKey, model);
            _coreSignal = new CoreSignalAgent(ai);
            _health     = new HealthAgent(ai);
            _eventCorr  = new EventCorrelationAgent(ai);
            _prediction = new PredictionAgent(ai);
            _insight    = new InsightAgent(ai);
        }

        /// <summary>
        /// Builds a short operational-state preamble from features so every agent
        /// understands when the UPS was actually online vs intentionally off.
        /// </summary>
        public static string BuildOperationalContext(Dictionary<string, double> features)
        {
            features.TryGetValue("UpsOnlinePct",      out double onlinePct);
            features.TryGetValue("InverterActivePct", out double invPct);
            features.TryGetValue("MainsPresentPct",   out double mainsPct);
            features.TryGetValue("ActiveAlarmCount",  out double activeAlarms);
            features.TryGetValue("ClearedAlarmCount", out double clearedAlarms);
            features.TryGetValue("VoutMean",          out double voutMean);
            features.TryGetValue("PowerMean",         out double powerMean);

            var sb = new StringBuilder();
            sb.AppendLine("=== UPS OPERATIONAL STATE ===");
            sb.AppendLine($"DC bus energised (UPS online)  : {onlinePct:F0}% of log duration");
            sb.AppendLine($"Inverter actively outputting   : {invPct:F0}% of log duration");
            sb.AppendLine($"Bypass/mains supply present    : {mainsPct:F0}% of log duration");
            sb.AppendLine($"Average output voltage (Vout)  : {voutMean:F1} V  (when inverter active)");
            sb.AppendLine($"Average output power           : {powerMean:F2} kW (when inverter active)");
            sb.AppendLine($"Active alarms (no X- prefix)   : {(int)activeAlarms}");
            sb.AppendLine($"Cleared/deactivated (X- prefix): {(int)clearedAlarms}");
            sb.AppendLine();
            sb.AppendLine("=== CRITICAL ANALYSIS RULES ===");
            sb.AppendLine("1. ONLY flag faults/concerns during NORMAL OPERATION (DC bus energised, inverter running).");
            sb.AppendLine("2. MCCB = Molded Case Circuit Breaker (manual breaker operated by engineer):");
            sb.AppendLine("   - 'Input_MCCB_OFF' or any '*_MCCB_OFF' = engineer INTENTIONALLY opened the breaker. NOT A FAULT.");
            sb.AppendLine("   - 'X - Rectifier_ON' following an MCCB_OFF = rectifier stopped because input breaker opened. NOT A FAULT.");
            sb.AppendLine("   - ANY alarm containing 'MCCB' is a deliberate maintenance/switching action. IGNORE as fault.");
            sb.AppendLine("3. Alarm prefix rules:");
            sb.AppendLine("   - No prefix   = alarm/status is ACTIVE  (e.g. 'Inverter_Fail' = inverter currently failed)");
            sb.AppendLine("   - 'X - ' prefix = alarm/status DEACTIVATED (e.g. 'X - Inverter_ON' = inverter commanded OFF intentionally)");
            sb.AppendLine("4. Other intentional actions — do NOT flag as faults:");
            sb.AppendLine("   - 'X - Load_ON_Inverter' = load deliberately moved off inverter");
            sb.AppendLine("   - 'X - Bypass_ON'        = bypass mode ended normally");
            sb.AppendLine("   - 'X - Rectifier_ON'     = rectifier deactivated (check if MCCB_OFF preceded it)");
            sb.AppendLine("5. If UPS online% is low, attribute to planned maintenance — not failure.");
            return sb.ToString();
        }

        public async Task<FinalResult> RunAsync(
            Dictionary<string, double> features,
            string alarmContext,
            IProgress<string>? progress = null)
        {
            var result = new FinalResult();
            string opContext = BuildOperationalContext(features);

            // ── Agent 1: Core Signal Intelligence ──────────────────────────────
            progress?.Report("Running AI pipeline — Agent 1 / 5  (Signal Analysis)…");
            try
            {
                result.CoreSignal = await _coreSignal.AnalyzeAsync(features, opContext, alarmContext);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"[CoreSignalAgent] {ex.Message}");
                result.CoreSignal = new CoreSignalResult { StressLevel = "Unknown" };
            }

            // ── Agent 2: Health Estimation ─────────────────────────────────────
            progress?.Report("Running AI pipeline — Agent 2 / 5  (Health Estimation)…");
            try
            {
                result.Health = await _health.AnalyzeAsync(features, result.CoreSignal, opContext, alarmContext);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"[HealthAgent] {ex.Message}");
                result.Health = new HealthResult();
            }

            // ── Agent 3: Event Correlation ─────────────────────────────────────
            progress?.Report("Running AI pipeline — Agent 3 / 5  (Event Correlation)…");
            try
            {
                result.EventCorrelation = await _eventCorr.AnalyzeAsync(features, opContext, alarmContext);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"[EventCorrelationAgent] {ex.Message}");
                result.EventCorrelation = new EventCorrelationResult { Confidence = "Low" };
            }

            // ── Agent 4: Failure Prediction ────────────────────────────────────
            progress?.Report("Running AI pipeline — Agent 4 / 5  (Failure Prediction)…");
            try
            {
                result.Prediction = await _prediction.AnalyzeAsync(
                    result.CoreSignal, result.Health, result.EventCorrelation,
                    opContext, alarmContext);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"[PredictionAgent] {ex.Message}");
                result.Prediction = new PredictionResult { Urgency = "Unknown" };
            }

            // ── Agent 5: Insight Generation ────────────────────────────────────
            progress?.Report("Running AI pipeline — Agent 5 / 5  (Insight Generation)…");
            try
            {
                result.Insight = await _insight.AnalyzeAsync(
                    result.CoreSignal, result.Health, result.EventCorrelation,
                    result.Prediction, opContext, alarmContext);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"[InsightAgent] {ex.Message}");
                result.Insight = new InsightResult { Summary = "Analysis incomplete due to error." };
            }

            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }
}
