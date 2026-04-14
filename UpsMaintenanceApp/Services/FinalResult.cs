using System;
using System.Collections.Generic;
 
namespace UpsMaintenanceApp.Services
{
    public class FinalResult
    {
        public CoreSignalResult       CoreSignal       { get; set; } = new();
        public HealthResult           Health           { get; set; } = new();
        public EventCorrelationResult EventCorrelation { get; set; } = new();
        public PredictionResult       Prediction       { get; set; } = new();
        public InsightResult          Insight          { get; set; } = new();
        public List<string>           Errors           { get; set; } = new();
        public DateTime               CompletedAt      { get; set; } = DateTime.MinValue;
        public bool                   HasErrors        => Errors.Count > 0;
    }
}