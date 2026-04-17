using System;

namespace UpsMaintenanceApp.Models
{
    public class TelemetryRow
    {
        public DateTime Timestamp { get; set; } = DateTime.MinValue;

        // ── DC bus ────────────────────────────────────────────────────────────
        public double DcBusVoltage  { get; set; } = 0.0;   // VdcLink
        public double DcLinkCurrent { get; set; } = 0.0;   // IdcLink

        // ── Battery ───────────────────────────────────────────────────────────
        public double BatteryVoltage    { get; set; } = 0.0;   // Vbatt
        public double BatteryCurrent    { get; set; } = 0.0;   // Ibatt
        public double BatteryVoltagePos { get; set; } = 0.0;   // VBatt Pos
        public double BatteryVoltageNeg { get; set; } = 0.0;   // VBatt Neg

        // ── Bypass / mains (single-phase proxy for input) ─────────────────────
        public double BypassVoltage   { get; set; } = 0.0;   // Vbypass
        public double BypassCurrent   { get; set; } = 0.0;   // Ibypass
        public double BypassFrequency { get; set; } = 0.0;   // Frequency Bypass
        public double InputFrequency  => BypassFrequency;

        // ── 3-phase input (populated when file contains these columns) ─────────
        public double InputVoltageR { get; set; } = 0.0;   // Vr Input
        public double InputVoltageY { get; set; } = 0.0;   // Vy Input
        public double InputVoltageB { get; set; } = 0.0;   // Vb Input
        public double InputCurrentR { get; set; } = 0.0;   // Ir / Ir Input
        public double InputCurrentY { get; set; } = 0.0;   // Iy / Iy Input
        public double InputCurrentB { get; set; } = 0.0;   // Ib / Ib Input

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

        // ── Convenience properties ────────────────────────────────────────────
        public bool IsOnline         => DcBusVoltage > 100.0;
        public bool IsInverterActive => OutputFrequency > 0.0;
        public bool IsMainsPresent   => BypassVoltage > 10.0;
        // True when DC bus is live but mains/bypass is absent (battery supplying load)
        public bool IsBatteryOnBackup => IsOnline && !IsMainsPresent && BatteryVoltage > 100.0;
        // True only when all three phase voltages are populated
        public bool Has3PhaseInput   => InputVoltageR > 1.0 && InputVoltageY > 1.0 && InputVoltageB > 1.0;
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
