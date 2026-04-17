using System;
using System.Collections.Generic;
using UpsMaintenanceApp.Models;

namespace UpsMaintenanceApp.Services
{
    /// <summary>
    /// Builds an operational-state timeline from the AlarmLog and stamps each
    /// TelemetryRow with the correct alarm-driven state flags.
    ///
    /// Each alarm row encodes a state transition:
    ///   No "X - " prefix  → alarm/status became ACTIVE  (e.g. "Inverter_ON"     = inverter started)
    ///   "X - " prefix     → alarm/status DEACTIVATED    (e.g. "X - Inverter_ON" = inverter stopped)
    ///
    /// This lets FeatureEngine filter rows by real operational windows instead of
    /// unreliable voltage/frequency thresholds.
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

            // Alarms are already sorted oldest→newest by CsvParser/ExcelParser.
            // Initial state: assume input/bypass/battery breakers closed; inverter/rectifier unknown.
            var state = new State
            {
                RectifierOn      = false,
                InverterOn       = false,
                InputPresent     = true,
                BypassOn         = true,
                BatteryBreakerOn = true,
            };

            int alarmIdx = 0;
            int alarmCount = alarms.Count;

            foreach (var row in rows)
            {
                // Consume all alarm events that occurred at or before this telemetry row's timestamp.
                while (alarmIdx < alarmCount && alarms[alarmIdx].OccurredAt <= row.Timestamp)
                {
                    Apply(alarms[alarmIdx], ref state);
                    alarmIdx++;
                }

                // Stamp the row with the current state.
                row.IsRectifierOn      = state.RectifierOn;
                row.IsInverterOn       = state.InverterOn;
                row.IsInputPresent     = state.InputPresent;
                row.IsBypassOn         = state.BypassOn;
                row.IsBatteryBreakerOn = state.BatteryBreakerOn;
            }
        }

        private static void Apply(AlarmEvent alarm, ref State s)
        {
            string d = alarm.Description;
            bool   active = alarm.IsActive;

            // Match by substring — alarm text may have trailing context in some files.
            if (Contains(d, "Inverter_ON"))
            {
                s.InverterOn = active;
                return;
            }
            if (Contains(d, "Rectifier_ON"))
            {
                s.RectifierOn = active;
                return;
            }
            // Input_MCCB_OFF active → MCCB is open → input absent; cleared → MCCB closed → input present
            if (Contains(d, "Input_MCCB_OFF") || Contains(d, "Input MCCB OFF"))
            {
                s.InputPresent = !active;
                return;
            }
            // Bypass Breaker OFF active → bypass open; cleared → bypass closed
            if (Contains(d, "Bypass Breaker OFF") || Contains(d, "Bypass_Breaker_OFF"))
            {
                s.BypassOn = !active;
                return;
            }
            // Battery Breaker OFF active → battery disconnected; cleared → battery connected
            if (Contains(d, "Battery Breaker OFF") || Contains(d, "Battery_Breaker_OFF"))
            {
                s.BatteryBreakerOn = !active;
                return;
            }
        }

        private static bool Contains(string source, string value) =>
            source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
