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
            // Defer until after layout so Canvas.ActualWidth is available
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,
                new Action(() => DrawFishbone(r)));

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

        private void DrawFishbone(FinalResult r)
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
            string effect = (r.EventCorrelation.RootCause ?? "Unknown Root Cause");
            if (effect.Length > 22) effect = effect.Substring(0, 20) + "…";
            FbText(effect, X1 + 8, SpineY - 10, 10, "#1E293B", bold: false, TextAlignment.Left, 126);

            // 6 ribs — 3 upper, 3 lower
            string[] upper = { "Input Power", "DC Link", "Inverter" };
            string[] lower = { "Bypass",      "Battery", "Thermal"  };
            double[] ribX  = { 130, 290, 450 };

            var patterns = r.EventCorrelation.Patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Concat(Enumerable.Repeat(string.Empty, 6))
                .Take(6)
                .ToList();

            for (int i = 0; i < 3; i++)
            {
                double sx  = ribX[i];
                double utx = sx - 68, uty = 48;
                double ltx = sx - 68, lty = H - 48;

                // Upper rib
                FbLine(sx, SpineY, utx, uty, "#3B82F6", 1.5);
                FbText(upper[i], utx - 4, uty - 19, 10, "#1E40AF", bold: true, TextAlignment.Right);

                string upPat = patterns[i * 2];
                if (!string.IsNullOrWhiteSpace(upPat))
                {
                    double mx = (sx + utx) / 2, my = (SpineY + uty) / 2;
                    FbLine(mx, my, mx, my - 24, "#93C5FD", 1);
                    if (upPat.Length > 20) upPat = upPat.Substring(0, 18) + "…";
                    FbText(upPat, mx - 52, my - 40, 9, "#64748B", bold: false, TextAlignment.Center, 104);
                }

                // Lower rib
                FbLine(sx, SpineY, ltx, lty, "#3B82F6", 1.5);
                FbText(lower[i], ltx - 4, lty + 5, 10, "#1E40AF", bold: true, TextAlignment.Right);

                string loPat = patterns[i * 2 + 1];
                if (!string.IsNullOrWhiteSpace(loPat))
                {
                    double mx = (sx + ltx) / 2, my = (SpineY + lty) / 2;
                    FbLine(mx, my, mx, my + 24, "#93C5FD", 1);
                    if (loPat.Length > 20) loPat = loPat.Substring(0, 18) + "…";
                    FbText(loPat, mx - 52, my + 28, 9, "#64748B", bold: false, TextAlignment.Center, 104);
                }
            }
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
