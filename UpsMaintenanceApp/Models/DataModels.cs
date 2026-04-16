using System;

namespace UpsMaintenanceApp.Models
{
    public class TelemetryRow
    {
        public DateTime Timestamp { get; set; } = DateTime.MinValue;

        // ── DC bus (VdcLink / IdcLink) ────────────────────────────────────────
        public double DcBusVoltage  { get; set; } = 0.0;   // VdcLink
        public double DcLinkCurrent { get; set; } = 0.0;   // IdcLink

        // ── Battery ───────────────────────────────────────────────────────────
        public double BatteryVoltage    { get; set; } = 0.0;   // Vbatt
        public double BatteryCurrent    { get; set; } = 0.0;   // Ibatt
        public double BatteryVoltagePos { get; set; } = 0.0;   // VBatt Pos
        public double BatteryVoltageNeg { get; set; } = 0.0;   // VBatt Neg

        // ── Bypass / mains ────────────────────────────────────────────────────
        public double BypassVoltage   { get; set; } = 0.0;   // Vbypass
        public double BypassCurrent   { get; set; } = 0.0;   // Ibypass
        public double BypassFrequency { get; set; } = 0.0;   // Frequency Bypass
        // InputFrequency kept as alias for BypassFrequency (used in older code paths)
        public double InputFrequency  => BypassFrequency;

        // ── Inverter output ───────────────────────────────────────────────────
        public double InverterVoltage   { get; set; } = 0.0;   // Vinv
        public double InverterCurrent   { get; set; } = 0.0;   // Iout_UPS
        public double InverterFrequency { get; set; } = 0.0;   // Frequency Inv

        // ── UPS output (load side) ────────────────────────────────────────────
        public double OutputVoltageL1 { get; set; } = 0.0;   // Vout
        public double OutputCurrentL1 { get; set; } = 0.0;   // Iout
        public double OutputFrequency { get; set; } = 0.0;   // Frequency Out
        public double OutputPowerKw   { get; set; } = 0.0;   // P Out
        public double OutputPowerKva  { get; set; } = 0.0;   // KVA_output
        public double PowerFactor     { get; set; } = 0.0;   // PF_out

        // ── Convenience: is the UPS actively running? ─────────────────────────
        /// <summary>True when the DC bus is energised (> 100 V) — best proxy for UPS online.</summary>
        public bool IsOnline => DcBusVoltage > 100.0;
        /// <summary>True when the inverter is actively generating output.</summary>
        public bool IsInverterActive => OutputFrequency > 0.0;
        /// <summary>True when bypass/mains supply is present.</summary>
        public bool IsMainsPresent => BypassVoltage > 10.0;
    }

    public class AlarmEvent
    {
        public int      Id          { get; set; } = 0;
        public DateTime OccurredAt  { get; set; } = DateTime.MinValue;
        public string   AlarmCode   { get; set; } = string.Empty;
        public string   Description { get; set; } = string.Empty;
        public string   Category    { get; set; } = string.Empty;
        public string   Severity    { get; set; } = string.Empty;
        public string   Status      { get; set; } = string.Empty;
        public bool     IsActive    { get; set; } = false;
    }
}
