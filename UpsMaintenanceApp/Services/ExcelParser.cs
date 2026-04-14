using System;
using System.Collections.Generic;
using System.Globalization;
using ClosedXML.Excel;
using UpsMaintenanceApp.Models;

namespace UpsMaintenanceApp.Services
{
    public static class ExcelParser
    {
        public static List<TelemetryRow> ParseTelemetry(string filePath)
        {
            var rows = new List<TelemetryRow>();
            using var workbook = new XLWorkbook(filePath);
            if (!workbook.TryGetWorksheet("FullDataLog", out var sheet))
                return rows;

            var h = ReadHeaders(sheet);
            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

            for (int r = 2; r <= lastRow; r++)
            {
                try
                {
                    var row = new TelemetryRow
                    {
                        Timestamp                   = GetDateTime(sheet, r, h, "Timestamp"),
                        UpsId                       = GetString  (sheet, r, h, "UPS ID"),
                        InputVoltageL1              = GetDouble  (sheet, r, h, "Input Voltage L1"),
                        InputVoltageL2              = GetDouble  (sheet, r, h, "Input Voltage L2"),
                        InputVoltageL3              = GetDouble  (sheet, r, h, "Input Voltage L3"),
                        InputCurrentL1              = GetDouble  (sheet, r, h, "Input Current L1"),
                        InputCurrentL2              = GetDouble  (sheet, r, h, "Input Current L2"),
                        InputCurrentL3              = GetDouble  (sheet, r, h, "Input Current L3"),
                        InputFrequency              = GetDouble  (sheet, r, h, "Input Frequency"),
                        OutputVoltageL1             = GetDouble  (sheet, r, h, "Output Voltage L1"),
                        OutputVoltageL2             = GetDouble  (sheet, r, h, "Output Voltage L2"),
                        OutputVoltageL3             = GetDouble  (sheet, r, h, "Output Voltage L3"),
                        OutputCurrentL1             = GetDouble  (sheet, r, h, "Output Current L1"),
                        OutputCurrentL2             = GetDouble  (sheet, r, h, "Output Current L2"),
                        OutputCurrentL3             = GetDouble  (sheet, r, h, "Output Current L3"),
                        OutputFrequency             = GetDouble  (sheet, r, h, "Output Frequency"),
                        OutputPowerKva              = GetDouble  (sheet, r, h, "Output Power kVA"),
                        OutputPowerKw               = GetDouble  (sheet, r, h, "Output Power kW"),
                        PowerFactor                 = GetDouble  (sheet, r, h, "Power Factor"),
                        LoadPercent                 = GetDouble  (sheet, r, h, "Load %"),
                        DcBusVoltage                = GetDouble  (sheet, r, h, "DC Bus Voltage"),
                        BatteryVoltage              = GetDouble  (sheet, r, h, "Battery Voltage"),
                        BatteryCurrent              = GetDouble  (sheet, r, h, "Battery Current"),
                        BatteryTemperatureCelsius   = GetDouble  (sheet, r, h, "Battery Temperature"),
                        BatteryStateOfChargePercent = (int)GetDouble(sheet, r, h, "Battery SoC"),
                        BatteryRuntimeMinutes       = (int)GetDouble(sheet, r, h, "Battery Runtime"),
                        AmbientTemperatureCelsius   = GetDouble  (sheet, r, h, "Ambient Temperature"),
                        InternalTemperatureCelsius  = GetDouble  (sheet, r, h, "Internal Temperature"),
                        OperationMode               = GetString  (sheet, r, h, "Operation Mode")
                    };

                    if (row.Timestamp == DateTime.MinValue) continue;
                    rows.Add(row);
                }
                catch { /* skip invalid row */ }
            }
            return rows;
        }

        public static List<AlarmEvent> ParseAlarms(string filePath)
        {
            var alarms = new List<AlarmEvent>();
            using var workbook = new XLWorkbook(filePath);
            if (!workbook.TryGetWorksheet("AlarmLog", out var sheet))
                return alarms;

            var h = ReadHeaders(sheet);
            int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            int id = 1;

            for (int r = 2; r <= lastRow; r++)
            {
                try
                {
                    string raw = GetString(sheet, r, h, "Description");
                    bool isActive = true;

                    if (raw.StartsWith("X - ", StringComparison.OrdinalIgnoreCase))
                    {
                        isActive = false;
                        raw = raw.Substring(4).Trim();
                    }

                    var alarm = new AlarmEvent
                    {
                        Id              = id++,
                        UpsId           = GetString  (sheet, r, h, "UPS ID"),
                        OccurredAt      = GetDateTime(sheet, r, h, "Occurred At"),
                        ClearedAt       = GetDateTime(sheet, r, h, "Cleared At"),
                        DurationSeconds = (int)GetDouble(sheet, r, h, "Duration"),
                        AlarmCode       = GetString  (sheet, r, h, "Alarm Code"),
                        Description     = raw,
                        Category        = GetString  (sheet, r, h, "Category"),
                        Severity        = GetString  (sheet, r, h, "Severity"),
                        Status          = GetString  (sheet, r, h, "Status"),
                        IsActive        = isActive,
                        IsAcknowledged  = GetString  (sheet, r, h, "Acknowledged")
                                            .Equals("Yes", StringComparison.OrdinalIgnoreCase)
                    };

                    if (alarm.OccurredAt == DateTime.MinValue) continue;
                    alarms.Add(alarm);
                }
                catch { /* skip invalid row */ }
            }
            return alarms;
        }

        private static Dictionary<string, int> ReadHeaders(IXLWorksheet sheet)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 1;
            for (int c = 1; c <= lastCol; c++)
            {
                string name = sheet.Row(1).Cell(c).GetString().Trim();
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                    map[name] = c;
            }
            return map;
        }

        private static string GetString(IXLWorksheet sheet, int row, Dictionary<string, int> h, string key)
        {
            if (!h.TryGetValue(key, out int col)) return string.Empty;
            return sheet.Cell(row, col).GetString()?.Trim() ?? string.Empty;
        }

        private static double GetDouble(IXLWorksheet sheet, int row, Dictionary<string, int> h, string key)
        {
            if (!h.TryGetValue(key, out int col)) return 0.0;
            var cell = sheet.Cell(row, col);
            if (cell.IsEmpty()) return 0.0;
            return double.TryParse(cell.GetString(), NumberStyles.Any,CultureInfo.InvariantCulture, out double v) ? v : 0.0;
        }

        private static DateTime GetDateTime(IXLWorksheet sheet, int row, Dictionary<string, int> h, string key)
        {
            if (!h.TryGetValue(key, out int col)) return DateTime.MinValue;
            var cell = sheet.Cell(row, col);
            if (cell.IsEmpty()) return DateTime.MinValue;
            try   { return cell.GetDateTime(); }
            catch { return DateTime.TryParse(cell.GetString(), CultureInfo.InvariantCulture,DateTimeStyles.None, out DateTime dt) ? dt : DateTime.MinValue; }
        }
    }
}
