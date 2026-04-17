using System;
using System.Collections.Generic;
using UpsMaintenanceApp.Models;

namespace UpsMaintenanceApp.Services
{
    /// <summary>
    /// Builds an operational-state timeline from the AlarmLog and stamps each
    /// TelemetryRow with alarm-driven state flags.
    ///
    /// Alarm encoding:
    ///   No "X - " prefix  → alarm ACTIVE  (e.g. "Inverter_ON" = inverter running)
    ///   "X - " prefix     → alarm CLEARED  (e.g. "X - Inverter_ON" = inverter stopped)
    ///
    /// Fallback: if a UPS-state alarm type (Inverter_ON / Rectifier_ON) never appears
    /// in the log (system was already running at log start), we fall back to voltage/
    /// frequency thresholds so FeatureEngine still receives useful data.
    /// </summary>
    public static class AlarmStateTimeline
    {
        private struct State
        {
            public bool RectifierOn;
            public bool InverterOn;
            public bool InputPresent;      // false when Input_MCCB_OFF is active
            public bool BypassOn;          // false when Bypass Breaker OFF is active
            public bool BatteryBreakerOn;  // false when Battery Breaker OFF is active
        }

        public static void Enrich(IList<TelemetryRow> rows, IList<AlarmEvent> alarms)
        {
            if (rows.Count == 0) return;

            // Scan alarm list once to see which state-alarm types are present.
            bool hasInverterAlarm  = false;
            bool hasRectifierAlarm = false;
            foreach (var a in alarms)
            {
                if (Contains(a.Description, "Inverter_ON"))  hasInverterAlarm  = true;
                if (Contains(a.Description, "Rectifier_ON")) hasRectifierAlarm = true;
                if (hasInverterAlarm && hasRectifierAlarm) break;
            }

            // Initial state: breakers assumed closed; inverter/rectifier default to
            // the fallback value so rows before the first alarm are handled correctly.
            var state = new State
            {
                RectifierOn      = !hasRectifierAlarm,   // true when no explicit alarm → assume on
                InverterOn       = !hasInverterAlarm,    // true when no explicit alarm → assume on
                InputPresent     = true,
                BypassOn         = true,
                BatteryBreakerOn = true,
            };

            int alarmIdx  = 0;
            int alarmCount = alarms.Count;

            foreach (var row in rows)
            {
                // Consume all alarm events at or before this row's timestamp.
                while (alarmIdx < alarmCount && alarms[alarmIdx].OccurredAt <= row.Timestamp)
                {
                    Apply(alarms[alarmIdx], ref state);
                    alarmIdx++;
                }

                row.IsRectifierOn      = state.RectifierOn;
                row.IsInverterOn       = state.InverterOn;
                row.IsInputPresent     = state.InputPresent;
                row.IsBypassOn         = state.BypassOn;
                row.IsBatteryBreakerOn = state.BatteryBreakerOn;
            }

            // Secondary fallback: even after alarm processing, if still all false,
            // use voltage/frequency thresholds (handles partial or non-standard logs).
            if (!hasInverterAlarm)
                foreach (var row in rows)
                    if (!row.IsInverterOn && row.OutputFrequency > 0.0)
                        row.IsInverterOn = true;

            if (!hasRectifierAlarm)
                foreach (var row in rows)
                    if (!row.IsRectifierOn && row.DcBusVoltage > 100.0)
                        row.IsRectifierOn = true;
        }

        private static void Apply(AlarmEvent alarm, ref State s)
        {
            string d      = alarm.Description;
            bool   active = alarm.IsActive;

            if (Contains(d, "Inverter_ON"))
            { s.InverterOn = active; return; }

            if (Contains(d, "Rectifier_ON"))
            { s.RectifierOn = active; return; }

            if (Contains(d, "Input_MCCB_OFF") || Contains(d, "Input MCCB OFF"))
            { s.InputPresent = !active; return; }

            if (Contains(d, "Bypass Breaker OFF") || Contains(d, "Bypass_Breaker_OFF"))
            { s.BypassOn = !active; return; }

            if (Contains(d, "Battery Breaker OFF") || Contains(d, "Battery_Breaker_OFF"))
            { s.BatteryBreakerOn = !active; return; }
        }

        private static bool Contains(string source, string value) =>
            source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
