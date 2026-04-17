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

        public static double VinAvg(IList<TelemetryRow> rows)
        {
            var live = rows.Where(r => r.IsMainsPresent).ToList();
            if (live.Count == 0) return 0.0;
            return live.Any(r => r.Has3PhaseInput)
                ? live.Where(r => r.Has3PhaseInput).Average(r => (r.InputVoltageR + r.InputVoltageY + r.InputVoltageB) / 3.0)
                : live.Average(r => r.BypassVoltage);
        }

        public static double VinStd(IList<TelemetryRow> rows)
        {
            var live = rows.Where(r => r.IsMainsPresent).ToList();
            if (live.Count < 2) return 0.0;
            IList<double> vals = live.Any(r => r.Has3PhaseInput)
                ? live.Where(r => r.Has3PhaseInput).Select(r => r.InputVoltageR).ToList()
                : live.Select(r => r.BypassVoltage).ToList();
            double mean  = vals.Average();
            double sumSq = vals.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSq / (vals.Count - 1));
        }

        public static double VinUnbalancePu(IList<TelemetryRow> rows)
        {
            var ph = rows.Where(r => r.Has3PhaseInput).ToList();
            if (ph.Count == 0) return 0.0;
            double vr = ph.Average(r => r.InputVoltageR);
            double vy = ph.Average(r => r.InputVoltageY);
            double vb = ph.Average(r => r.InputVoltageB);
            double avg = (vr + vy + vb) / 3.0;
            if (avg < Epsilon) return 0.0;
            return new[] { Math.Abs(vr - avg), Math.Abs(vy - avg), Math.Abs(vb - avg) }.Max() / avg;
        }

        // ── Bypass ────────────────────────────────────────────────────────────

        public static double BypassVoltageStd(IList<TelemetryRow> rows)
        {
            var live = rows.Where(r => r.IsMainsPresent).ToList();
            if (live.Count < 2) return 0.0;
            double mean  = live.Average(r => r.BypassVoltage);
            double sumSq = live.Sum(r => Math.Pow(r.BypassVoltage - mean, 2));
            return Math.Sqrt(sumSq / (live.Count - 1));
        }

        public static double BypassFreqStd(IList<TelemetryRow> rows)
        {
            var live = rows.Where(r => r.IsMainsPresent && r.BypassFrequency > 0).ToList();
            if (live.Count < 2) return 0.0;
            double mean  = live.Average(r => r.BypassFrequency);
            double sumSq = live.Sum(r => Math.Pow(r.BypassFrequency - mean, 2));
            return Math.Sqrt(sumSq / (live.Count - 1));
        }

        // ── DC bus ────────────────────────────────────────────────────────────

        public static double VdcMean(IList<TelemetryRow> rows)
        {
            var online = rows.Where(r => r.IsOnline).ToList();
            if (online.Count == 0) return 0.0;
            return online.Average(r => r.DcBusVoltage);
        }

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

        public static double BatteryBackupMinutes(IList<TelemetryRow> rows)
        {
            var backup = rows.Where(r => r.IsBatteryOnBackup).OrderBy(r => r.Timestamp).ToList();
            if (backup.Count < 2) return 0.0;
            double totalMinutes = 0;
            DateTime? segStart = null;
            DateTime prev = DateTime.MinValue;
            foreach (var r in backup)
            {
                if (segStart == null) { segStart = r.Timestamp; prev = r.Timestamp; continue; }
                if ((r.Timestamp - prev).TotalMinutes > 5)
                {
                    totalMinutes += (prev - segStart.Value).TotalMinutes;
                    segStart = r.Timestamp;
                }
                prev = r.Timestamp;
            }
            if (segStart.HasValue) totalMinutes += (prev - segStart.Value).TotalMinutes;
            return totalMinutes;
        }

        // ── Output ────────────────────────────────────────────────────────────

        public static double FoutStd(IList<TelemetryRow> rows)
        {
            var active = rows.Where(r => r.IsInverterActive).ToList();
            if (active.Count < 2) return 0.0;
            double mean  = active.Average(r => r.OutputFrequency);
            double sumSq = active.Sum(r => Math.Pow(r.OutputFrequency - mean, 2));
            return Math.Sqrt(sumSq / (active.Count - 1));
        }

        public static double VoutMean(IList<TelemetryRow> rows)
        {
            var active = rows.Where(r => r.IsInverterActive).ToList();
            if (active.Count == 0) return 0.0;
            return active.Average(r => r.OutputVoltageL1);
        }

        public static double VoutStd(IList<TelemetryRow> rows)
        {
            var active = rows.Where(r => r.IsInverterActive).ToList();
            if (active.Count < 2) return 0.0;
            double mean  = active.Average(r => r.OutputVoltageL1);
            double sumSq = active.Sum(r => Math.Pow(r.OutputVoltageL1 - mean, 2));
            return Math.Sqrt(sumSq / (active.Count - 1));
        }

        public static double PowerMean(IList<TelemetryRow> rows)
        {
            var active = rows.Where(r => r.IsInverterActive).ToList();
            if (active.Count == 0) return 0.0;
            return active.Average(r => r.OutputPowerKw);
        }

        // ── Efficiency ────────────────────────────────────────────────────────

        public static double EfficiencyMean(IList<TelemetryRow> rows)
        {
            var valid = rows.Where(r =>
            {
                double inputPw = r.Has3PhaseInput
                    ? (r.InputVoltageR * r.InputCurrentR + r.InputVoltageY * r.InputCurrentY + r.InputVoltageB * r.InputCurrentB) / 1000.0
                    : r.BypassVoltage * r.BypassCurrent / 1000.0;
                return r.OutputPowerKw > 0.01 && inputPw > 0.01;
            }).ToList();
            if (valid.Count == 0) return 0.0;
            return valid.Average(r =>
            {
                double inputPw = r.Has3PhaseInput
                    ? (r.InputVoltageR * r.InputCurrentR + r.InputVoltageY * r.InputCurrentY + r.InputVoltageB * r.InputCurrentB) / 1000.0
                    : r.BypassVoltage * r.BypassCurrent / 1000.0;
                return Math.Min(100.0, r.OutputPowerKw / inputPw * 100.0);
            });
        }

        // ── Operational state ─────────────────────────────────────────────────

        public static double UpsOnlinePct(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Count(r => r.IsOnline) * 100.0 / rows.Count;
        }

        public static double InverterActivePct(IList<TelemetryRow> rows)
        {
            if (rows.Count == 0) return 0.0;
            return rows.Count(r => r.IsInverterActive) * 100.0 / rows.Count;
        }

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
                ["VinStd"]            = VinStd(rows),
                ["VinUnbalancePu"]    = VinUnbalancePu(rows),
                ["BypassVoltageStd"]  = BypassVoltageStd(rows),
                ["BypassFreqStd"]     = BypassFreqStd(rows),
                ["VdcMean"]           = VdcMean(rows),
                ["VdcStd"]            = VdcStd(rows),
                ["RbattProxy"]        = RbattProxy(rows),
                ["BatteryBackupMin"]  = BatteryBackupMinutes(rows),
                ["FoutStd"]           = FoutStd(rows),
                ["VoutMean"]          = VoutMean(rows),
                ["VoutStd"]           = VoutStd(rows),
                ["PowerMean"]         = PowerMean(rows),
                ["EfficiencyMean"]    = EfficiencyMean(rows),
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
