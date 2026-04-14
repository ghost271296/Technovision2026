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
 
        public async Task<FinalResult> RunAsync(Dictionary<string, double> features)
        {
            var result = new FinalResult();
 
            // Step 1 — Core Signal Intelligence
            try
            {
                result.CoreSignal = await _coreSignal.AnalyzeAsync(features);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"[CoreSignalAgent] {ex.Message}");
                result.CoreSignal = new CoreSignalResult { StressLevel = "Unknown" };
            }
 
            // Step 2 — Health Estimation
            try
            {
                result.Health = await _health.AnalyzeAsync(features, result.CoreSignal);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"[HealthAgent] {ex.Message}");
                result.Health = new HealthResult();
            }
 
            // Step 3 — Event Correlation
            try
            {
                result.EventCorrelation = await _eventCorr.AnalyzeAsync(features);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"[EventCorrelationAgent] {ex.Message}");
                result.EventCorrelation = new EventCorrelationResult { Confidence = "Low" };
            }
 
            // Step 4 — Prediction
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
 
            // Step 5 — Insight Generation
            try
            {
                result.Insight = await _insight.AnalyzeAsync(
                    result.CoreSignal,
                    result.Health,
                    result.EventCorrelation,
                    result.Prediction);
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