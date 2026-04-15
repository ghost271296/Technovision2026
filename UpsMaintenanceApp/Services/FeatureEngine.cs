using System;
using System.Collections.Generic;
using System.Linq;
using UpsMaintenanceApp.Models;

namespace UpsMaintenanceApp.Services
{
    public static class FeatureEngine
    {
        private const double Epsilon = 1e-9;

        // ── Input ─────────────────────────────────────────────────────────────

        public static double VinAvg(IList<TelemetryRow> rows)
        {
            var live = rows.Where(r => r.InputVoltageL1 > 10).ToList();
            if (live.Count == 0) return 0.0;
            return live.Average(r =>
                (r.InputVoltageL1 + r.InputVoltageL2 + r.InputVoltageL3) / 3.0);
        }

        public static double VinUnbalancePu(IList<TelemetryRow> rows)
        {
            var live = rows.Where(r => r.InputVoltageL1 > 10).ToList();
            if (live.Count == 0) return 0.0;
            return live.Average(r =>
            {
                double mean = (r.InputVoltageL1 + r.InputVoltageL2 + r.InputVoltageL3) / 3.0;
                if (Math.Abs(mean) < Epsilon) return 0.0;
                double maxDev = Math.Max(Math.Abs(r.InputVoltageL1 - mean),
                                Math.Max(Math.Abs(r.InputVoltageL2 - mean),
                                         Math.Abs(r.InputVoltageL3 - mean)));
                return maxDev / mean;
            });
        }

        // ── DC bus ────────────────────────────────────────────────────────────

        /// <summary>VdcMean computed only while input supply is ON (InputVoltageL1 > 10 V).</summary>
        public static double VdcMean(IList<TelemetryRow> rows)
        {
            var online = rows.Where(r => r.InputVoltageL1 > 10).ToList();
            if (online.Count == 0) return 0.0;
            return online.Average(r => r.DcBusVoltage);
        }

        /// <summary>VdcStd computed only while input supply is ON.</summary>
        public static double VdcStd(IList<TelemetryRow> rows)
        {
            var online = rows.Where(r => r.InputVoltageL1 > 10).ToList();
            if (online.Count < 2) return 0.0;
            double mean  = online.Average(r => r.DcBusVoltage);
            double sumSq = online.Sum(r => Math.Pow(r.DcBusVoltage - mean, 2));
            return Math.Sqrt(sumSq / (online.Count - 1));
        }

        // ── Battery ───────────────────────────────────────────────────────────

        public static double RbattProxy(IList<TelemetryRow> rows)
        {
            if (rows.Count < 2) return 0.0;
            double deltaI = rows.Max(r => r.BatteryCurrent) - rows.Min(r => r.BatteryCurrent);
            if (Math.Abs(deltaI) < Epsilon) return 0.0;
            return (rows.Max(r => r.BatteryVoltage) - rows.Min(r => r.BatteryVoltage)) / deltaI;
        }

        // ── Output ────────────────────────────────────────────────────────────

        /// <summary>Output frequency std dev — only rows where inverter is actively outputting.</summary>
        public static double FoutStd(IList<TelemetryRow> rows)
        {
            var active = rows.Where(r => r.OutputFrequency > 0).ToList();
            if (active.Count < 2) return 0.0;
            double mean  = active.Average(r => r.OutputFrequency);
            double sumSq = active.Sum(r => Math.Pow(r.OutputFrequency - mean, 2));
            return Math.Sqrt(sumSq / (active.Count - 1));
        }

        // ── Operational state ─────────────────────────────────────────────────

        /// <summary>Percentage of log time where mains input supply is present (InputVoltageL1 > 10 V).</summary>
        public static double UpsOnlinePct(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Count(r => r.InputVoltageL1 > 10) * 100.0 / rows.Count;
        }

        /// <summary>Percentage of log time where UPS inverter is actively outputting (OutputFrequency > 0).</summary>
        public static double InverterActivePct(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Count(r => r.OutputFrequency > 0) * 100.0 / rows.Count;
        }

        // ── Alarms ────────────────────────────────────────────────────────────

        public static double AlarmRatePerHour(IList<AlarmEvent> alarms)
        {
            if (alarms.Count == 0) return 0.0;
            var valid = alarms.Where(a => a.OccurredAt != DateTime.MinValue).ToList();
            if (valid.Count == 0) return 0.0;
            double hours = (valid.Max(a => a.OccurredAt) - valid.Min(a => a.OccurredAt)).TotalHours;
            if (hours < (1.0 / 60.0)) return 0.0;
            return valid.Count / hours;
        }

        // ── Aggregate ─────────────────────────────────────────────────────────

        public static Dictionary<string, double> ComputeAll(IList<TelemetryRow> rows, IList<AlarmEvent> alarms)
        {
            return new Dictionary<string, double>
            {
                ["VinAvg"]            = VinAvg(rows),
                ["VinUnbalancePu"]    = VinUnbalancePu(rows),
                ["VdcMean"]           = VdcMean(rows),
                ["VdcStd"]            = VdcStd(rows),
                ["RbattProxy"]        = RbattProxy(rows),
                ["FoutStd"]           = FoutStd(rows),
                ["AlarmRatePerHour"]  = AlarmRatePerHour(alarms),
                ["UpsOnlinePct"]      = UpsOnlinePct(rows),
                ["InverterActivePct"] = InverterActivePct(rows),
                ["ActiveAlarmCount"]  = alarms.Count(a => a.IsActive),
                ["ClearedAlarmCount"] = alarms.Count(a => !a.IsActive),
            };
        }
    }
}
