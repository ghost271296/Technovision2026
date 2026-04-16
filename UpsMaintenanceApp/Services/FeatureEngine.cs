using System;
using System.Collections.Generic;
using System.Linq;
using UpsMaintenanceApp.Models;

namespace UpsMaintenanceApp.Services
{
    public static class FeatureEngine
    {
        private const double Epsilon = 1e-9;

        // ── Input / bypass ────────────────────────────────────────────────────

        /// <summary>Average bypass voltage while mains is present (BypassVoltage > 10 V).</summary>
        public static double VinAvg(IList<TelemetryRow> rows)
        {
            var live = rows.Where(r => r.IsMainsPresent).ToList();
            if (live.Count == 0) return 0.0;
            return live.Average(r => r.BypassVoltage);
        }

        /// <summary>Not applicable without 3-phase input — returns 0.</summary>
        public static double VinUnbalancePu(IList<TelemetryRow> rows) => 0.0;

        // ── DC bus ────────────────────────────────────────────────────────────

        /// <summary>VdcMean computed only while the DC bus is energised (IsOnline).</summary>
        public static double VdcMean(IList<TelemetryRow> rows)
        {
            var online = rows.Where(r => r.IsOnline).ToList();
            if (online.Count == 0) return 0.0;
            return online.Average(r => r.DcBusVoltage);
        }

        /// <summary>VdcStd computed only while the DC bus is energised.</summary>
        public static double VdcStd(IList<TelemetryRow> rows)
        {
            var online = rows.Where(r => r.IsOnline).ToList();
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

        /// <summary>Output frequency std dev — only when inverter is actively outputting.</summary>
        public static double FoutStd(IList<TelemetryRow> rows)
        {
            var active = rows.Where(r => r.IsInverterActive).ToList();
            if (active.Count < 2) return 0.0;
            double mean  = active.Average(r => r.OutputFrequency);
            double sumSq = active.Sum(r => Math.Pow(r.OutputFrequency - mean, 2));
            return Math.Sqrt(sumSq / (active.Count - 1));
        }

        /// <summary>Average output voltage while inverter is running.</summary>
        public static double VoutMean(IList<TelemetryRow> rows)
        {
            var active = rows.Where(r => r.IsInverterActive).ToList();
            if (active.Count == 0) return 0.0;
            return active.Average(r => r.OutputVoltageL1);
        }

        /// <summary>Average output power (kW) while inverter is running.</summary>
        public static double PowerMean(IList<TelemetryRow> rows)
        {
            var active = rows.Where(r => r.IsInverterActive).ToList();
            if (active.Count == 0) return 0.0;
            return active.Average(r => r.OutputPowerKw);
        }

        // ── Operational state ─────────────────────────────────────────────────

        /// <summary>Percentage of log time where DC bus is energised (UPS online).</summary>
        public static double UpsOnlinePct(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Count(r => r.IsOnline) * 100.0 / rows.Count;
        }

        /// <summary>Percentage of log time where inverter is actively outputting.</summary>
        public static double InverterActivePct(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Count(r => r.IsInverterActive) * 100.0 / rows.Count;
        }

        /// <summary>Percentage of log time where bypass/mains is present.</summary>
        public static double MainsPresentPct(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Count(r => r.IsMainsPresent) * 100.0 / rows.Count;
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
                ["VoutMean"]          = VoutMean(rows),
                ["PowerMean"]         = PowerMean(rows),
                ["AlarmRatePerHour"]  = AlarmRatePerHour(alarms),
                ["UpsOnlinePct"]      = UpsOnlinePct(rows),
                ["InverterActivePct"] = InverterActivePct(rows),
                ["MainsPresentPct"]   = MainsPresentPct(rows),
                ["ActiveAlarmCount"]  = alarms.Count(a =>  a.IsActive),
                ["ClearedAlarmCount"] = alarms.Count(a => !a.IsActive),
            };
        }
    }
}
