using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UpsMaintenanceApp.Services;

namespace UpsMaintenanceApp.Views
{
    public partial class ReportPage : UserControl
    {
        public ReportPage() => InitializeComponent();

        // ── Public entry point ────────────────────────────────────────────────

        public void LoadReport(FinalResult r, Dictionary<string, double> features)
        {

            NoDataPanel.Visibility = Visibility.Collapsed;
            ReportBody.Visibility  = Visibility.Visible;

            int healthIndex = (r.Health.BatteryHealth +
                               r.Health.DcLinkHealth  +
                               r.Health.PowerStageHealth) / 3;

            // ── Section 1: Executive Summary ──────────────────────────────────
            TxtReportDate.Text = $"Generated: {r.CompletedAt:dd MMM yyyy   HH:mm} UTC";
            TxtSiteDate.Text   = $"Analysis date: {r.CompletedAt:dddd, dd MMMM yyyy}   " +
                                 $"|   Urgency: {r.Prediction.Urgency}   " +
                                 $"|   Root cause: {r.EventCorrelation.RootCause}";
            TxtSummary.Text    = r.Insight.Summary;
            TxtHealthIndex.Text   = healthIndex.ToString();
            TxtUrgency.Text       = r.Prediction.Urgency;
            TxtConfidence.Text    = r.Insight.Confidence;
            TxtHealthIndex.Foreground = MakeBrush(HealthColor(healthIndex, false));

            // ── Section 2: Health Dashboard ───────────────────────────────────
            SetGauge(TxtBattHealth,  PbBatt,  TxtBattStatus,  r.Health.BatteryHealth,    inverted: false);
            SetGauge(TxtDcHealth,    PbDc,    TxtDcStatus,    r.Health.DcLinkHealth,     inverted: false);
            SetGauge(TxtPwrHealth,   PbPwr,   TxtPwrStatus,   r.Health.PowerStageHealth, inverted: false);
            SetGauge(TxtThermHealth, PbTherm, TxtThermStatus, r.Health.ThermalStress,    inverted: true);
            TxtOverallHealth.Text       = healthIndex.ToString();
            TxtOverallHealth.Foreground = MakeBrush(HealthColor(healthIndex, false));

            // ── Section 3: Detected Issues ────────────────────────────────────
            var issues = r.Insight.TopIssues.Select(s => new IssueItem(s)).ToList();
            IssuesList.ItemsSource  = issues;
            TxtNoIssues.Visibility  = issues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // ── Section 4: Root Cause Analysis ────────────────────────────────
            TxtRootCause.Text    = r.EventCorrelation.RootCause;
            TxtRcConfidence.Text = r.EventCorrelation.Confidence;
            RcConfBadge.Background = ConfidenceBrush(r.EventCorrelation.Confidence);
            PatternsList.ItemsSource = r.EventCorrelation.Patterns;

            // ── Section 5: Fishbone Diagram ───────────────────────────────────
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,
                new Action(() => DrawFishbone(r, features)));

            // ── Section 6: Corrective Actions ─────────────────────────────────
            var (immediate, scheduled, monitor) = CategoriseActions(r.Insight.Actions);
            ImmediateList.ItemsSource = immediate;
            ScheduledList.ItemsSource = scheduled;
            MonitorList.ItemsSource   = monitor;
            TxtNoImmediate.Visibility = immediate.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtNoScheduled.Visibility = scheduled.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtNoMonitor.Visibility   = monitor.Count   == 0 ? Visibility.Visible : Visibility.Collapsed;

            // ── Section 7: Predictive Risks ───────────────────────────────────
            RiskProjectionGrid.ItemsSource = new[]
            {
                MakeRiskRow("Inverter",  r.Prediction.InverterFail),
                MakeRiskRow("Rectifier", r.Prediction.RectifierFail),
                MakeRiskRow("Battery",   r.Prediction.BatteryFail),
            };

            // ── Section 8: Engineering Appendix ───────────────────────────────
            FeaturesGrid.ItemsSource = features
                .OrderBy(kv => kv.Key)
                .Select(kv => new FeatureRow(kv.Key, kv.Value))
                .ToList();

            features.TryGetValue("UpsOnlinePct",      out double onlinePct);
            features.TryGetValue("InverterActivePct", out double invPct);
            features.TryGetValue("MainsPresentPct",   out double mainsPct);
            TxtDqReport.Text =
                $"Data quality summary:  " +
                $"UPS online {onlinePct:F0}% of log duration  |  " +
                $"Inverter active {invPct:F0}%  |  " +
                $"Mains present {mainsPct:F0}%  |  " +
                $"Total computed features: {features.Count}";

            // ── Section 9: Confidence & Provenance ────────────────────────────
            TxtModelInfo.Text      = "GPT-4o  (OpenAI)";
            TxtAnalysisTime.Text   = $"{r.CompletedAt:dd MMM yyyy   HH:mm:ss} UTC";
            TxtProvConfidence.Text = r.Insight.Confidence;
            TxtErrorLog.Text       = r.HasErrors
                ? string.Join("\n", r.Errors)
                : "All 5 pipeline agents completed successfully.  No errors detected.";
        }

        // ── Section 2 helpers ─────────────────────────────────────────────────

        private static void SetGauge(TextBlock txt, ProgressBar pb,
                                     TextBlock status, int value, bool inverted)
        {
            txt.Text = value.ToString();
            pb.Value = inverted ? Math.Max(0, 100 - value) : value;

            var color = HealthColor(value, inverted);
            var brush = MakeBrush(color);
            txt.Foreground    = brush;
            pb.Foreground     = brush;
            status.Foreground = brush;
            status.Text       = StatusLabel(value, inverted);
        }

        private static Color HealthColor(int value, bool inverted)
        {
            bool good    = inverted ? value <= 30 : value >= 80;
            bool warning = !good && (inverted ? value <= 60 : value >= 60);
            if (good)    return Color.FromRgb(0x16, 0xA3, 0x4A);
            if (warning) return Color.FromRgb(0xD9, 0x77, 0x06);
            return           Color.FromRgb(0xDC, 0x26, 0x26);
        }

        private static string StatusLabel(int value, bool inverted)
        {
            bool good    = inverted ? value <= 30 : value >= 80;
            bool warning = !good && (inverted ? value <= 60 : value >= 60);
            return good ? "Good" : warning ? "Warning" : "Critical";
        }

        // ── Section 4 helpers ─────────────────────────────────────────────────

        private static Brush ConfidenceBrush(string confidence) =>
            (confidence?.ToUpperInvariant()) switch
            {
                "HIGH"   => MakeBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
                "MEDIUM" => MakeBrush(Color.FromRgb(0xD9, 0x77, 0x06)),
                _        => MakeBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
            };

        // ── Section 6 helpers ─────────────────────────────────────────────────

        private static (List<string> immediate, List<string> scheduled, List<string> monitor)
            CategoriseActions(List<string> actions)
        {
            var immediate = new List<string>();
            var scheduled = new List<string>();
            var monitor   = new List<string>();

            foreach (var a in actions)
            {
                string lower = a.ToLowerInvariant();
                if (lower.Contains("monitor") || lower.Contains("watch") ||
                    lower.Contains("observe") || lower.Contains("track"))
                    monitor.Add(a);
                else if (lower.Contains("schedule") || lower.Contains("within") ||
                         lower.Contains("next ")    || lower.Contains("week")   ||
                         lower.Contains("month")    || lower.Contains("priority 2") ||
                         lower.Contains("priority 3"))
                    scheduled.Add(a);
                else
                    immediate.Add(a);
            }

            if (immediate.Count == 0 && scheduled.Count == 0 && monitor.Count == 0)
                immediate.AddRange(actions);

            return (immediate, scheduled, monitor);
        }

        // ── Section 7 helpers ─────────────────────────────────────────────────

        private static object MakeRiskRow(string component, int current)
        {
            int d30 = current;
            int d60 = Math.Min(99, (int)Math.Round(current * 1.3));
            int d90 = Math.Min(99, (int)Math.Round(current * 1.6));
            string priority = current >= 70 ? "Immediate"
                            : current >= 40 ? "High"
                            : current >= 20 ? "Medium"
                            :                 "Low";
            return new
            {
                Component = component,
                Current   = $"{current}%",
                D30       = $"{d30}%",
                D60       = $"{d60}%",
                D90       = $"{d90}%",
                Priority  = priority
            };
        }

        // ── Section 5: Fishbone Canvas ────────────────────────────────────────

        private void DrawFishbone(FinalResult r, Dictionary<string, double> features)
        {
            FishboneCanvas.Children.Clear();

            const double H      = 300;
            const double SpineY = 150;
            const double X0     = 20;
            const double X1     = 575;

            // Spine
            FbLine(X0, SpineY, X1, SpineY, "#1E293B", 2.5);

            // Arrow head toward effect box
            FbLine(X1, SpineY, X1 - 12, SpineY - 8, "#1E293B", 2);
            FbLine(X1, SpineY, X1 - 12, SpineY + 8, "#1E293B", 2);

            // Effect box
            var rect = new Rectangle
            {
                Width           = 138,
                Height          = 62,
                Fill            = MakeBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)),
                Stroke          = MakeBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
                StrokeThickness = 1.5,
                RadiusX         = 5,
                RadiusY         = 5,
            };
            Canvas.SetLeft(rect, X1 + 4);
            Canvas.SetTop(rect, SpineY - 31);
            FishboneCanvas.Children.Add(rect);

            FbText("EFFECT", X1 + 73, SpineY - 27, 9, "#2563EB", bold: true, TextAlignment.Center);
            string effect = r.EventCorrelation.RootCause ?? "Unknown";
            if (effect.Length > 22) effect = effect.Substring(0, 20) + "…";
            FbText(effect, X1 + 8, SpineY - 10, 10, "#1E293B", bold: false, TextAlignment.Left, 126);

            // 3 upper ribs + 3 lower ribs with per-category analysis
            string[] upper = { "Input Power", "DC Link", "Inverter" };
            string[] lower = { "Bypass",      "Battery", "Thermal"  };
            double[] ribX  = { 130, 290, 450 };

            for (int i = 0; i < 3; i++)
            {
                double sx  = ribX[i];
                double utx = sx - 68, uty = 48;
                double ltx = sx - 68, lty = H - 48;

                // Upper rib
                FbLine(sx, SpineY, utx, uty, "#3B82F6", 1.5);
                FbText(upper[i], utx + 2, uty - 18, 10, "#1E40AF", bold: true, TextAlignment.Left);
                DrawRibAnalysis(sx, SpineY, utx, uty,
                    GetRibAnalysis(upper[i], features, r.Health), isUpper: true);

                // Lower rib
                FbLine(sx, SpineY, ltx, lty, "#3B82F6", 1.5);
                FbText(lower[i], ltx + 2, lty + 5, 10, "#1E40AF", bold: true, TextAlignment.Left);
                DrawRibAnalysis(sx, SpineY, ltx, lty,
                    GetRibAnalysis(lower[i], features, r.Health), isUpper: false);
            }
        }

        // Draws a short perpendicular branch at 45% along the rib, with a 2-line analysis label.
        private void DrawRibAnalysis(double baseX, double baseY, double tipX, double tipY,
                                     string analysis, bool isUpper)
        {
            if (string.IsNullOrWhiteSpace(analysis)) return;

            double t  = 0.45;
            double mx = baseX + t * (tipX - baseX);
            double my = baseY + t * (tipY - baseY);

            double endY  = isUpper ? my - 20 : my + 20;
            FbLine(mx, my, mx, endY, "#60A5FA", 1);

            double textY = isUpper ? endY - 26 : endY + 2;
            FbText(analysis, mx - 52, textY, 9, "#374151", bold: false, TextAlignment.Center, 104);
        }

        // Returns a 2-line analysis string computed from features for each rib category.
        private static string GetRibAnalysis(string rib, Dictionary<string, double> features,
                                             HealthResult health)
        {
            double F(string k) { features.TryGetValue(k, out double v); return v; }

            return rib switch
            {
                "Input Power" => RibInputPower(F("VinAvg"),    F("VinStd"),
                                               F("VinUnbalancePu"), F("MainsPresentPct")),
                "DC Link"     => RibDcLink(    F("VdcMean"),   F("VdcStd"),    health.DcLinkHealth),
                "Inverter"    => RibInverter(  F("FoutStd"),   F("VoutStd"),
                                               F("EfficiencyMean"), health.PowerStageHealth),
                "Bypass"      => RibBypass(    F("BypassVoltageStd"), F("BypassFreqStd")),
                "Battery"     => RibBattery(   F("RbattProxy"), F("BatteryBackupMin"), health.BatteryHealth),
                "Thermal"     => RibThermal(   health.ThermalStress, F("AlarmRatePerHour")),
                _             => string.Empty,
            };
        }

        private static string RibInputPower(double vinAvg, double vinStd, double unbal, double mainsPct)
        {
            string l1 = vinStd > 5    ? "High input voltage variation"
                      : vinStd > 2    ? "Moderate input fluctuation"
                      : mainsPct < 90 ? "Frequent mains interruptions"
                      : vinAvg  > 1   ? "Input supply stable"
                      :                 "No input data available";

            string l2 = unbal > 0.03  ? "Significant phase unbalance"
                      : unbal > 0.01  ? "Minor phase unbalance detected"
                      : mainsPct < 95 ? $"Mains absent {100-mainsPct:F0}% of log"
                      : vinStd  > 0.01 ? $"Within tolerance (σ {vinStd:F2}V)"
                      :                  "All phases balanced";

            return $"{l1}\n{l2}";
        }

        private static string RibDcLink(double vdcMean, double vdcStd, int health)
        {
            string l1 = health < 60  ? "DC link health critical"
                      : health < 80  ? "DC link health degraded"
                      : vdcStd > 5   ? "DC bus ripple — check caps"
                      : vdcStd > 2   ? "DC link instability detected"
                      : vdcMean > 1  ? "DC bus operating normally"
                      :                "DC bus inactive";

            string l2 = vdcStd > 2   ? "Possible capacitor degradation"
                      : health < 80  ? $"Health score: {health}/100"
                      : vdcMean > 1  ? $"Vdc stable at {vdcMean:F0}V"
                      :                "No DC bus data";

            return $"{l1}\n{l2}";
        }

        private static string RibInverter(double foutStd, double voutStd, double eff, int health)
        {
            string l1 = health < 60           ? "Power stage health critical"
                      : eff > 0.1 && eff < 85 ? "Low conversion efficiency"
                      : voutStd > 2           ? "Output voltage unstable"
                      : foutStd > 0.05        ? "Output frequency deviation"
                      :                         "Inverter operating normally";

            string l2 = eff > 0.1     ? $"Efficiency: {eff:F1}%  H:{health}/100"
                      : health < 80   ? $"Health score: {health}/100"
                      :                 "Output waveform stable";

            return $"{l1}\n{l2}";
        }

        private static string RibBypass(double bypVStd, double bypFStd)
        {
            string l1 = bypVStd > 5    ? "Bypass supply highly unstable"
                      : bypVStd > 2    ? "Bypass voltage fluctuating"
                      : bypFStd > 0.5  ? "Bypass frequency unstable"
                      : bypFStd > 0.05 ? "Bypass freq minor deviation"
                      :                  "Bypass supply stable";

            string l2 = bypVStd > 0.5  ? $"Vbyp σ = ±{bypVStd:F2}V — investigate"
                      : bypFStd > 0.01 ? $"Freq σ = {bypFStd:F4}Hz"
                      :                  "No bypass anomalies";

            return $"{l1}\n{l2}";
        }

        private static string RibBattery(double rbatt, double battMin, int health)
        {
            string l1 = health < 60   ? "Battery health critical"
                      : health < 80   ? "Battery health degraded"
                      : rbatt > 0.05  ? "High internal resistance"
                      : battMin > 0.1 ? "Battery backup activated"
                      :                 "Battery in good condition";

            string l2 = rbatt > 0.05  ? "Battery ageing — test/replace"
                      : rbatt > 0.01  ? $"Rbatt {rbatt:F3}Ω — monitor trend"
                      : battMin > 0.1 ? $"Backup duration: {battMin:F0} min"
                      :                 $"Health {health}/100 — no events";

            return $"{l1}\n{l2}";
        }

        private static string RibThermal(int thermalStress, double alarmRate)
        {
            string l1 = thermalStress > 70 ? "High thermal stress — urgent"
                      : thermalStress > 40 ? "Moderate thermal load"
                      : alarmRate > 5      ? "High alarm frequency"
                      : alarmRate > 1      ? "Elevated alarm rate"
                      :                      "Thermal conditions normal";

            string l2 = thermalStress > 40 ? "Check cooling / ventilation"
                      : alarmRate > 1      ? $"Rate: {alarmRate:F2} alarms/hr"
                      :                      "No thermal concerns";

            return $"{l1}\n{l2}";
        }

        private void FbLine(double x1, double y1, double x2, double y2,
                             string hex, double thick)
        {
            FishboneCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke          = ParseHex(hex),
                StrokeThickness = thick,
            });
        }

        private void FbText(string text, double x, double y, double size,
                             string hex, bool bold, TextAlignment align, double maxWidth = 0)
        {
            var tb = new TextBlock
            {
                Text         = text,
                FontSize     = size,
                FontWeight   = bold ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground   = ParseHex(hex),
                TextAlignment = align,
                TextWrapping = maxWidth > 0 ? TextWrapping.Wrap : TextWrapping.NoWrap,
            };
            if (maxWidth > 0) tb.Width = maxWidth;
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            FishboneCanvas.Children.Add(tb);
        }

        // ── Brush / color utilities ───────────────────────────────────────────

        private static SolidColorBrush MakeBrush(Color c) => new(c);

        private static SolidColorBrush ParseHex(string hex)
        {
            try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
            catch { return new SolidColorBrush(Colors.Black); }
        }

        // ── Print ─────────────────────────────────────────────────────────────

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
                pd.PrintVisual(ReportContent, "VPMS UPS Analysis Report");
        }
    }

    // ── Binding model for Section 3 ───────────────────────────────────────────

    internal sealed class IssueItem
    {
        public string Text          { get; }
        public string Severity      { get; }
        public SolidColorBrush SeverityColor { get; }

        public IssueItem(string text)
        {
            Text = text;
            string lower = text.ToLowerInvariant();

            if (lower.Contains("fail") || lower.Contains("fault") ||
                lower.Contains("critical") || lower.Contains("overload") ||
                lower.Contains("dsat"))
            {
                Severity      = "Critical";
                SeverityColor = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            }
            else if (lower.Contains("warn")      || lower.Contains("high")    ||
                     lower.Contains("low")        || lower.Contains("variation") ||
                     lower.Contains("instab")     || lower.Contains("fluctuat"))
            {
                Severity      = "Warning";
                SeverityColor = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));
            }
            else
            {
                Severity      = "Info";
                SeverityColor = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            }
        }
    }

    // ── Binding model for Section 8 ───────────────────────────────────────────

    internal sealed class FeatureRow
    {
        private static readonly Dictionary<string, string> _lookup = new()
        {
            ["VinAvg"]            = "Average input voltage (V)",
            ["VinStd"]            = "Input voltage std deviation (V)",
            ["VinUnbalancePu"]    = "3-phase input voltage unbalance (pu)",
            ["BypassVoltageStd"]  = "Bypass voltage std deviation (V)",
            ["BypassFreqStd"]     = "Bypass frequency std deviation (Hz)",
            ["VdcMean"]           = "DC bus mean voltage (V)",
            ["VdcStd"]            = "DC bus voltage std deviation (V)",
            ["RbattProxy"]        = "Battery internal resistance proxy (Ω)",
            ["BatteryBackupMin"]  = "Total battery backup time (min)",
            ["FoutStd"]           = "Output frequency std deviation (Hz)",
            ["VoutMean"]          = "Mean output voltage (V)",
            ["VoutStd"]           = "Output voltage std deviation (V)",
            ["PowerMean"]         = "Mean output power when inverter active (kW)",
            ["EfficiencyMean"]    = "Mean conversion efficiency under real load (%)",
            ["AlarmRatePerHour"]  = "Alarm event rate (events / hour)",
            ["UpsOnlinePct"]      = "DC bus energised (% of log duration)",
            ["InverterActivePct"] = "Inverter actively outputting (% of log)",
            ["MainsPresentPct"]   = "Mains / bypass input present (% of log)",
            ["ActiveAlarmCount"]  = "Active alarm count (no X- prefix)",
            ["ClearedAlarmCount"] = "Cleared / deactivated alarm count (X- prefix)",
        };

        public string Metric { get; }
        public string Value  { get; }
        public string Desc   { get; }

        public FeatureRow(string metric, double value)
        {
            Metric = metric;
            Value  = $"{value:F4}";
            Desc   = _lookup.TryGetValue(metric, out string? d) ? d : string.Empty;
        }
    }
}
