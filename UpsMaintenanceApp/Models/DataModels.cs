using System;

namespace UpsMaintenanceApp.Models
{
    public class TelemetryRow
    {
        public DateTime Timestamp { get; set; } = DateTime.MinValue;
        public string UpsId { get; set; } = string.Empty;

        // Input electrical
        public double InputVoltageL1 { get; set; } = 0.0;
        public double InputVoltageL2 { get; set; } = 0.0;
        public double InputVoltageL3 { get; set; } = 0.0;
        public double InputCurrentL1 { get; set; } = 0.0;
        public double InputCurrentL2 { get; set; } = 0.0;
        public double InputCurrentL3 { get; set; } = 0.0;
        public double InputFrequency { get; set; } = 0.0;

        // Output electrical
        public double OutputVoltageL1 { get; set; } = 0.0;
        public double OutputVoltageL2 { get; set; } = 0.0;
        public double OutputVoltageL3 { get; set; } = 0.0;
        public double OutputCurrentL1 { get; set; } = 0.0;
        public double OutputCurrentL2 { get; set; } = 0.0;
        public double OutputCurrentL3 { get; set; } = 0.0;
        public double OutputFrequency { get; set; } = 0.0;
        public double OutputPowerKva { get; set; } = 0.0;
        public double OutputPowerKw { get; set; } = 0.0;
        public double PowerFactor { get; set; } = 0.0;
        public double LoadPercent { get; set; } = 0.0;

        // DC bus
        public double DcBusVoltage { get; set; } = 0.0;

        // Battery
        public double BatteryVoltage { get; set; } = 0.0;
        public double BatteryCurrent { get; set; } = 0.0;
        public double BatteryTemperatureCelsius { get; set; } = 0.0;
        public int BatteryStateOfChargePercent { get; set; } = 0;
        public int BatteryRuntimeMinutes { get; set; } = 0;

        // Thermal
        public double AmbientTemperatureCelsius { get; set; } = 0.0;
        public double InternalTemperatureCelsius { get; set; } = 0.0;

        // UPS mode
        public string OperationMode { get; set; } = string.Empty;
    }

    public class AlarmEvent
    {
        public int Id { get; set; } = 0;
        public string UpsId { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; } = DateTime.MinValue;
        public DateTime ClearedAt { get; set; } = DateTime.MinValue;
        public int DurationSeconds { get; set; } = 0;

        public string AlarmCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public bool IsActive { get; set; } = false;
        public bool IsAcknowledged { get; set; } = false;
    }
}
