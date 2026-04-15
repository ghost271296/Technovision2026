using System;
using System.Collections.Generic;
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

        public AnalysisPipeline(string apiKey)
        {
            var ai   = new OpenAIClientService(apiKey);
            _coreSignal = new CoreSignalAgent(ai);
            _health     = new HealthAgent(ai);
            _eventCorr  = new EventCorrelationAgent(ai);
            _prediction = new PredictionAgent(ai);
            _insight    = new InsightAgent(ai);
        }

        /// <summary>
        /// Runs all 5 agents sequentially.
        /// </summary>
        /// <param name="features">Numeric features from FeatureEngine.</param>
        /// <param name="alarmContext">Full alarm log summary text built from parsed AlarmEvents.</param>
        /// <param name="progress">Optional callback to report status label for each agent.</param>
        public async Task<FinalResult> RunAsync(
            Dictionary<string, double> features,
            string alarmContext,
            IProgress<string>? progress = null)
        {
            var result = new FinalResult();

            // ── Agent 1: Core Signal Intelligence ──────────────────────────────
            progress?.Report("Running AI pipeline — Agent 1 / 5  (Signal Analysis)…");
            try
            {
                result.CoreSignal = await _coreSignal.AnalyzeAsync(features, alarmContext);
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
                result.Health = await _health.AnalyzeAsync(features, result.CoreSignal);
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
                result.EventCorrelation = await _eventCorr.AnalyzeAsync(features, alarmContext);
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
                    result.CoreSignal,
                    result.Health,
                    result.EventCorrelation);
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
                    result.CoreSignal,
                    result.Health,
                    result.EventCorrelation,
                    result.Prediction,
                    alarmContext);
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
