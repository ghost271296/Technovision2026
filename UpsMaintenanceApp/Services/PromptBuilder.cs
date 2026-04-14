using System.Collections.Generic;
 
namespace UpsMaintenanceApp.Services
{
    public static class PromptBuilder
    {
        public static string Build(Dictionary<string, double> features)
        {
            features.TryGetValue("VdcMean",         out double vdcMean);
            features.TryGetValue("VinUnbalancePu",  out double vinUnbalance);
            features.TryGetValue("AlarmRatePerHour",out double alarmRate);
 
            return
                $"You are a UPS predictive maintenance expert. " +
                $"Analyze the following UPS health indicators and respond in JSON with keys: " +
                $"\"risk_level\" (Low/Medium/High), \"finding\", \"recommendation\".\n\n" +
                $"DC Bus Voltage Mean : {vdcMean:F2} V\n" +
                $"Input Voltage Unbalance : {vinUnbalance:F4} pu\n" +
                $"Alarm Rate : {alarmRate:F2} alarms/hr\n\n" +
                $"Be concise. JSON only.";
        }
    }
}