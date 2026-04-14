using System;
using System.Collections.Generic;
using System.Linq;
using UpsMaintenanceApp.Models;

namespace UpsMaintenanceApp.Services
{
    public static class FeatureEngine
    {
        private const double Epsilon = 1e-9;

        public static double VinAvg(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Average(r =>
                (r.InputVoltageL1 + r.InputVoltageL2 + r.InputVoltageL3) / 3.0);
        }

        public static double VinUnbalancePu(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Average(r =>
            {
                double mean = (r.InputVoltageL1 + r.InputVoltageL2 + r.InputVoltageL3) / 3.0;
                if (Math.Abs(mean) < Epsilon) return 0.0;
                double maxDev = Math.Max(Math.Abs(r.InputVoltageL1 - mean),
                                Math.Max(Math.Abs(r.InputVoltageL2 - mean),
                                         Math.Abs(r.InputVoltageL3 - mean)));
                return maxDev / mean;
            });
        }

        public static double VdcMean(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Average(r => r.DcBusVoltage);
        }

        public static double VdcStd(IList<TelemetryRow> rows)
        {
            if (rows.Count < 2) return 0.0;
            double mean  = rows.Average(r => r.DcBusVoltage);
            double sumSq = rows.Sum(r => Math.Pow(r.DcBusVoltage - mean, 2));
            return Math.Sqrt(sumSq / (rows.Count - 1));
        }

        public static double RbattProxy(IList<TelemetryRow> rows)
        {
            if (rows.Count < 2) return 0.0;
            double deltaI = rows.Max(r => r.BatteryCurrent) - rows.Min(r => r.BatteryCurrent);
            if (Math.Abs(deltaI) < Epsilon) return 0.0;
            return (rows.Max(r => r.BatteryVoltage) - rows.Min(r => r.BatteryVoltage)) / deltaI;
        }

        public static double AlarmRatePerHour(IList<AlarmEvent> alarms)
        {
            if (alarms.Count == 0) return 0.0;
            var valid = alarms.Where(a => a.OccurredAt != DateTime.MinValue).ToList();
            if (valid.Count == 0) return 0.0;
            double hours = (valid.Max(a => a.OccurredAt) - valid.Min(a => a.OccurredAt)).TotalHours;
            if (hours < (1.0 / 60.0)) return 0.0;
            return valid.Count / hours;
        }

        public static Dictionary<string, double> ComputeAll(IList<TelemetryRow> rows, IList<AlarmEvent> alarms)
        {
            return new Dictionary<string, double>
            {
                ["VinAvg"]           = VinAvg(rows),
                ["VinUnbalancePu"]   = VinUnbalancePu(rows),
                ["VdcMean"]          = VdcMean(rows),
                ["VdcStd"]           = VdcStd(rows),
                ["RbattProxy"]       = RbattProxy(rows),
                ["AlarmRatePerHour"] = AlarmRatePerHour(alarms)
            };
        }
    }
}
