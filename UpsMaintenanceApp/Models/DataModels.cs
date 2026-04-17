using System;

namespace UpsMaintenanceApp.Models
{
    public class TelemetryRow
    {
        public DateTime Timestamp { get; set; } = DateTime.MinValue;

        // ── Identity ──────────────────────────────────────────────────────────
        public string UpsId { get; set; } = string.Empty;

        // ── 3-phase input (CSV: Vr/Vy/Vb Input; Excel: Input Voltage L1/L2/L3) ──
        public double InputVoltageL1 { get; set; } = 0.0;
        public double InputVoltageL2 { get; set; } = 0.0;
        public double InputVoltageL3 { get; set; } = 0.0;
        public double InputCurrentL1 { get; set; } = 0.0;
        public double InputCurrentL2 { get; set; } = 0.0;
        public double InputCurrentL3 { get; set; } = 0.0;
        public double InputFrequency { get; set; } = 0.0;   // settable; CSV sets from BypassFrequency

        // ── DC bus ────────────────────────────────────────────────────────────
        public double DcBusVoltage  { get; set; } = 0.0;   // VdcLink
        public double DcLinkCurrent { get; set; } = 0.0;   // IdcLink

        // ── Battery ───────────────────────────────────────────────────────────
        public double BatteryVoltage              { get; set; } = 0.0;   // Vbatt
        public double BatteryCurrent              { get; set; } = 0.0;   // Ibatt
        public double BatteryVoltagePos           { get; set; } = 0.0;   // VBatt Pos
        public double BatteryVoltageNeg           { get; set; } = 0.0;   // VBatt Neg
        public double BatteryTemperatureCelsius   { get; set; } = 0.0;
        public int    BatteryStateOfChargePercent { get; set; } = 0;
        public int    BatteryRuntimeMinutes       { get; set; } = 0;

        // ── Bypass / mains (single-phase) ─────────────────────────────────────
        public double BypassVoltage   { get; set; } = 0.0;   // Vbypass
        public double BypassCurrent   { get; set; } = 0.0;   // Ibypass
        public double BypassFrequency { get; set; } = 0.0;   // Frequency Bypass

        // ── Inverter output ───────────────────────────────────────────────────
        public double InverterVoltage   { get; set; } = 0.0;   // Vinv
        public double InverterCurrent   { get; set; } = 0.0;   // Iout_UPS
        public double InverterFrequency { get; set; } = 0.0;   // Frequency Inv

        // ── UPS output (load side) ────────────────────────────────────────────
        public double OutputVoltageL1 { get; set; } = 0.0;   // Vout / Output Voltage L1
        public double OutputVoltageL2 { get; set; } = 0.0;
        public double OutputVoltageL3 { get; set; } = 0.0;
        public double OutputCurrentL1 { get; set; } = 0.0;   // Iout / Output Current L1
        public double OutputCurrentL2 { get; set; } = 0.0;
        public double OutputCurrentL3 { get; set; } = 0.0;
        public double OutputFrequency { get; set; } = 0.0;   // Frequency Out
        public double OutputPowerKw   { get; set; } = 0.0;   // P Out
        public double OutputPowerKva  { get; set; } = 0.0;   // KVA_output
        public double PowerFactor     { get; set; } = 0.0;   // PF_out
        public double LoadPercent     { get; set; } = 0.0;   // Load %

        // ── Thermal ───────────────────────────────────────────────────────────
        public double AmbientTemperatureCelsius  { get; set; } = 0.0;
        public double InternalTemperatureCelsius { get; set; } = 0.0;

        // ── Operating mode (from Excel format) ────────────────────────────────
        public string OperationMode { get; set; } = string.Empty;

        // ── Alarm-driven operational state (set by AlarmStateTimeline.Enrich) ─
        // Default values represent the "safe initial" assumption before any alarms arrive.
        public bool IsRectifierOn      { get; set; } = false;  // Rectifier_ON alarm active
        public bool IsInverterOn       { get; set; } = false;  // Inverter_ON alarm active
        public bool IsInputPresent     { get; set; } = true;   // Input_MCCB_OFF NOT active
        public bool IsBypassOn         { get; set; } = true;   // Bypass Breaker OFF NOT active
        public bool IsBatteryBreakerOn { get; set; } = true;   // Battery Breaker OFF NOT active

        // Derived: inverter is running on battery (input absent, battery connected)
        public bool IsBatteryOnBackup => IsInverterOn && !IsInputPresent && IsBatteryBreakerOn;

        // Data-availability check: true only when all three phase voltages are populated
        public bool Has3PhaseInput => InputVoltageL1 > 1.0 && InputVoltageL2 > 1.0 && InputVoltageL3 > 1.0;
    }

    public class AlarmEvent
    {
        public int      Id              { get; set; } = 0;
        public string   UpsId           { get; set; } = string.Empty;
        public DateTime OccurredAt      { get; set; } = DateTime.MinValue;
        public DateTime ClearedAt       { get; set; } = DateTime.MinValue;
        public int      DurationSeconds { get; set; } = 0;
        public string   AlarmCode       { get; set; } = string.Empty;
        public string   Description     { get; set; } = string.Empty;
        public string   Category        { get; set; } = string.Empty;
        public string   Severity        { get; set; } = string.Empty;
        public string   Status          { get; set; } = string.Empty;
        public bool     IsActive        { get; set; } = false;
        public bool     IsAcknowledged  { get; set; } = false;
    }
}
