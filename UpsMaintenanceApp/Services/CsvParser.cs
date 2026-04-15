using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UpsMaintenanceApp.Models;

namespace UpsMaintenanceApp.Services
{
    /// <summary>
    /// Parses Vertiv/Liebert UPS tab-separated DataLog and AlarmLog files.
    /// Date format: "20 January 2026   06:50:04:730 AM" (custom with milliseconds).
    /// Row 0 = headers, Row 1 = metadata "Data/Alarm Log has created on…" (skipped),
    /// Rows 2+ = data.
    /// </summary>
    /// to commit
    public static class CsvParser
    {
        private static readonly string[] DateFormats =
        {
            "d MMMM yyyy hh:mm:ss:fff tt",
            "d MMMM yyyy h:mm:ss:fff tt",
        };

        private static DateTime ParseDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DateTime.MinValue;
            // Collapse multiple spaces (the format uses 3 spaces between date and time)
            string s = Regex.Replace(raw.Trim(), @"\s+", " ");
            return DateTime.TryParseExact(s, DateFormats,
                       CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                   ? dt : DateTime.MinValue;
        }

        private static char DetectSep(string header) =>
            header.Count(c => c == '\t') >= header.Count(c => c == ',') ? '\t' : ',';

        private static Dictionary<string, int> HeaderMap(string headerLine, char sep)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var parts = headerLine.Split(sep);
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i].Trim();
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                    map[name] = i;
            }
            return map;
        }

        private static string Str(string[] cols, Dictionary<string, int> h, string key)
        {
            if (!h.TryGetValue(key, out int i) || i >= cols.Length) return string.Empty;
            return cols[i].Trim();
        }

        private static double Dbl(string[] cols, Dictionary<string, int> h, string key)
        {
            string s = Str(cols, h, key);
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0.0;
        }

        // ── DataLog ──────────────────────────────────────────────────────────

        public static List<TelemetryRow> ParseTelemetry(string filePath)
        {
            var rows = new List<TelemetryRow>();
            if (!File.Exists(filePath)) return rows;

            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 3) return rows;   // header + metadata + ≥1 row

            char sep = DetectSep(lines[0]);
            var  h   = HeaderMap(lines[0], sep);

            for (int i = 2; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cols = lines[i].Split(sep);
                try
                {
                    var row = new TelemetryRow
                    {
                        Timestamp       = ParseDate(Str(cols, h, "Date Time")),
                        // Input 3-phase
                        InputVoltageL1  = Dbl(cols, h, "Vr Input (V)"),
                        InputCurrentL1  = Dbl(cols, h, "Ir Input (A)"),
                        InputVoltageL2  = Dbl(cols, h, "Vy Input (V)"),
                        InputCurrentL2  = Dbl(cols, h, "Iy Input (A)"),
                        InputVoltageL3  = Dbl(cols, h, "Vb Input (V)"),
                        InputCurrentL3  = Dbl(cols, h, "Ib Input (A)"),
                        // Mains/bypass frequency used as InputFrequency
                        InputFrequency  = Dbl(cols, h, "Frequency Bypass"),
                        // DC bus & battery
                        DcBusVoltage    = Dbl(cols, h, "VdcLink"),
                        BatteryVoltage  = Dbl(cols, h, "Vbatt"),
                        BatteryCurrent  = Dbl(cols, h, "Ibatt"),
                        // Output (single-phase measured at UPS output)
                        OutputVoltageL1 = Dbl(cols, h, "Vout"),
                        OutputCurrentL1 = Dbl(cols, h, "Iout"),
                        OutputFrequency = Dbl(cols, h, "Frequency Out"),
                        // Inverter channel stored in L2/L3 spare slots
                        OutputVoltageL2 = Dbl(cols, h, "Vinv"),
                        OutputCurrentL2 = Dbl(cols, h, "Iout_UPS"),
                        OutputVoltageL3 = Dbl(cols, h, "Frequency Inv")
                    };
                    if (row.Timestamp == DateTime.MinValue) continue;
                    rows.Add(row);
                }
                catch { /* skip malformed rows */ }
            }
            // DataLog is stored newest-first; reverse so charts show time left→right
            rows.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            return rows;
        }

        // ── AlarmLog ─────────────────────────────────────────────────────────

        public static List<AlarmEvent> ParseAlarms(string filePath)
        {
            var list = new List<AlarmEvent>();
            if (!File.Exists(filePath)) return list;

            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 3) return list;

            char sep = DetectSep(lines[0]);
            var  h   = HeaderMap(lines[0], sep);

            for (int i = 2; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cols = lines[i].Split(sep);
                try
                {
                    string rawDesc = Str(cols, h, "Alarm Log");
                    if (string.IsNullOrWhiteSpace(rawDesc)) continue;

                    bool isActive = !rawDesc.StartsWith("X - ", StringComparison.OrdinalIgnoreCase);
                    string desc   = isActive ? rawDesc : rawDesc.Substring(4).Trim();

                    int id = int.TryParse(Str(cols, h, "Index"), out int idx) ? idx : (i - 1);

                    list.Add(new AlarmEvent
                    {
                        Id          = id,
                        OccurredAt  = ParseDate(Str(cols, h, "Date Time")),
                        AlarmCode   = desc.Length <= 20 ? desc : desc.Substring(0, 17) + "…",
                        Description = desc,
                        Category    = InferCategory(desc),
                        Severity    = InferSeverity(desc),
                        Status      = isActive ? "Active" : "Cleared",
                        IsActive    = isActive,
                    });
                }
                catch { /* skip malformed rows */ }
            }

            // Remove rows where timestamp couldn't be parsed, then sort oldest→newest
            list.RemoveAll(a => a.OccurredAt == DateTime.MinValue);
            list.Sort((a, b) => a.OccurredAt.CompareTo(b.OccurredAt));
            return list;
        }

        // ── Inference helpers ─────────────────────────────────────────────────

        private static string InferCategory(string desc)
        {
            string d = desc.ToUpperInvariant();
            if (d.Contains("INVERTER") || d.Contains("INV_") || d.Contains("DSAT"))  return "Inverter";
            if (d.Contains("BATTERY")  || d.Contains("BATT"))                         return "Battery";
            if (d.Contains("BYPASS"))                                                  return "Bypass";
            if (d.Contains("RECTIFIER") || d.Contains("RECT") || d.Contains("MCCB")) return "Rectifier";
            if (d.Contains("OVERLOAD") || d.Contains("LOAD"))                         return "Load";
            if (d.Contains("THERMAL")  || d.Contains("TEMP") || d.Contains("FAN"))   return "Thermal";
            if (d.Contains("SYNC")     || d.Contains("COMM"))                         return "Communication";
            if (d.Contains("INPUT")    || d.Contains("MAINS") || d.Contains("AC"))   return "Input";
            return "General";
        }

        private static string InferSeverity(string desc)
        {
            string d = desc.ToUpperInvariant();
            if (d.Contains("FAIL") || d.Contains("FAULT") || d.Contains("DSAT") ||
                d.Contains("CRITICAL") || d.Contains("OVERLOAD"))                     return "Critical";
            if (d.Contains("WARN") || d.Contains("LOW") || d.Contains("HIGH"))       return "Warning";
            return "Info";
        }
    }
}