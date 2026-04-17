using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using UpsMaintenanceApp.Models;
using UpsMaintenanceApp.Services;
using UpsMaintenanceApp.ViewModels;
using UpsMaintenanceApp.Views;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace UpsMaintenanceApp
{
    public partial class MainWindow : Window
    {
        private readonly DashboardViewModel _vm = new();
        private FinalResult? _lastResult;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            // Upload Visit page fires AnalysisRequested(dataLogPath, alarmLogPath)
            PageUpload.AnalysisRequested += async (dataPath, alarmPath) =>
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    BtnLoadFile.IsEnabled = false;
                    _vm.IsAnalyzing       = true;
                    PageUpload.SetProgress("Parsing log files…", "", true);
                    try
                    {
                        await RunPipeline(dataPath, alarmPath);
                        Nav_Click(NavDashboard, new RoutedEventArgs());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        _vm.IsAnalyzing       = false;
                        BtnLoadFile.IsEnabled = true;
                        PageUpload.SetProgress("", "", false);
                    }
                });
            };
        }

        // ── Sidebar "Load Files" button ───────────────────────────────────────

        private async void BtnLoadFile_Click(object sender, RoutedEventArgs e)
        {
            var dataDialog = new OpenFileDialog
            {
                Title  = "Step 1 of 2 — Select DataLog File",
                Filter = "Data Files (*.txt;*.csv;*.tsv)|*.txt;*.csv;*.tsv|All Files (*.*)|*.*"
            };
            if (dataDialog.ShowDialog() != true) return;

            var alarmDialog = new OpenFileDialog
            {
                Title  = "Step 2 of 2 — Select AlarmLog File",
                Filter = "Data Files (*.txt;*.csv;*.tsv)|*.txt;*.csv;*.tsv|All Files (*.*)|*.*"
            };
            if (alarmDialog.ShowDialog() != true) return;

            string apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show(
                    "Set OPENAI_API_KEY in Settings before running.",
                    "API Key Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnLoadFile.IsEnabled = false;
            _vm.IsAnalyzing       = true;
            _vm.PipelineStatus    = "Parsing log files…";
            try
            {
                await RunPipeline(dataDialog.FileName, alarmDialog.FileName);
            }
            catch (Exception ex)
            {
                _vm.PipelineStatus = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _vm.IsAnalyzing       = false;
                BtnLoadFile.IsEnabled = true;
            }
        }

        // ── Core pipeline ─────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task RunPipeline(string dataPath, string alarmPath)
        {
            _vm.IsAnalyzing    = true;
            _vm.PipelineStatus = "Parsing files…";

            // ── Parse ──────────────────────────────────────────────────────────
            var telemetry = CsvParser.ParseTelemetry(dataPath);
            var alarms    = CsvParser.ParseAlarms(alarmPath);

            // Stamp each telemetry row with alarm-driven operational state flags
            // (IsRectifierOn, IsInverterOn, IsInputPresent, etc.) before feature compute.
            AlarmStateTimeline.Enrich(telemetry, alarms);

            if (telemetry.Count == 0)
                throw new InvalidOperationException(
                    "No telemetry rows parsed. Check the DataLog file has a 'Date Time' column " +
                    "and semicolon/tab/comma-separated values.");

            AlarmGrid.ItemsSource = alarms;
            _vm.TotalAlarms       = alarms.Count;
            _vm.AlarmStorms       = CountStorms(alarms);
            _vm.DataQualityInfo   = $"Duration: {GetDuration(telemetry)}  |  Rows: {telemetry.Count:N0}";
            _vm.LogWindowInfo     = $"Log: {GetLogWindow(telemetry)}";

            // ── Features ───────────────────────────────────────────────────────
            _vm.PipelineStatus = "Computing features…";
            var features = FeatureEngine.ComputeAll(telemetry, alarms);

            features.TryGetValue("VdcMean",          out double vdcMean);
            features.TryGetValue("VdcStd",           out double vdcStd);
            features.TryGetValue("AlarmRatePerHour", out double alarmRate);
            _vm.VdcMean   = vdcMean;
            _vm.VdcStd    = vdcStd;
            _vm.AlarmRate = alarmRate;
            var outputRows = telemetry.Where(r => r.OutputFrequency > 0).ToList();
            _vm.FreqError  = outputRows.Count > 0
                ? outputRows.Average(r => Math.Abs(r.OutputFrequency - 50.0)) : 0;

            BuildElectricalChart(telemetry);
            BuildAlarmChart(alarms);

            // ── Build contexts for LLM ─────────────────────────────────────────
            string alarmContext    = BuildAlarmContext(alarms);
            string timelineContext = AnalysisPipeline.BuildCombinedTimelineContext(telemetry, alarms);

            // ── AI Pipeline ────────────────────────────────────────────────────
            string apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "OPENAI_API_KEY not set. Configure it in Settings.");

            // Progress callback updates the status bar in real time
            var progress = new Progress<string>(msg =>
                Dispatcher.InvokeAsync(() => _vm.PipelineStatus = msg));

            var result = await new AnalysisPipeline(apiKey, GetModel()).RunAsync(features, alarmContext, timelineContext, progress);
            _lastResult = result;

            // ── Populate ViewModel ─────────────────────────────────────────────
            // CoreSignal — 7 domain-specific assessments
            _vm.InputVoltageVariation  = result.CoreSignal.InputVoltageVariation;
            _vm.EfficiencyRating       = result.CoreSignal.EfficiencyRating;
            _vm.BypassFreqVariation    = result.CoreSignal.BypassFreqVariation;
            _vm.BypassVoltageVariation = result.CoreSignal.BypassVoltageVariation;
            _vm.DcLinkVariation        = result.CoreSignal.DcLinkVariation;
            _vm.BatteryBackupStatus    = result.CoreSignal.BatteryBackupStatus;
            _vm.OutputVoltageVariation = result.CoreSignal.OutputVoltageVariation;
            _vm.StressLevel            = result.CoreSignal.StressLevel;
            // Health
            _vm.BatteryHealth       = result.Health.BatteryHealth;
            _vm.DcLinkHealth        = result.Health.DcLinkHealth;
            _vm.InverterHealth      = result.Health.PowerStageHealth;
            _vm.RectifierHealth     = (result.Health.DcLinkHealth + result.Health.PowerStageHealth) / 2;
            _vm.ThermalStress       = result.Health.ThermalStress;
            _vm.OverallHealthIndex  = (result.Health.BatteryHealth +
                                       result.Health.DcLinkHealth +
                                       result.Health.PowerStageHealth) / 3;
            // Prediction
            _vm.InverterFailRisk    = result.Prediction.InverterFail;
            _vm.RectifierFailRisk   = result.Prediction.RectifierFail;
            _vm.BatteryFailRisk     = result.Prediction.BatteryFail;
            _vm.Urgency             = result.Prediction.Urgency;
            // Event Correlation
            _vm.RootCause           = result.EventCorrelation.RootCause;
            _vm.RootCauseConfidence = result.EventCorrelation.Confidence;
            _vm.Patterns            = new ObservableCollection<string>(result.EventCorrelation.Patterns);
            // Insight
            _vm.InsightSummary      = result.Insight.Summary;
            _vm.InsightConfidence   = result.Insight.Confidence;
            _vm.TopIssues           = new ObservableCollection<string>(result.Insight.TopIssues);
            _vm.Actions             = new ObservableCollection<string>(result.Insight.Actions);
            _vm.AlertMessage        = result.Insight.Summary;

            _vm.PipelineStatus = result.HasErrors
                ? $"Done with {result.Errors.Count} error(s)  •  {result.CompletedAt:HH:mm:ss}"
                : $"Analysis complete  •  {result.CompletedAt:HH:mm:ss}";
        }

        // ── API key: env var first, then saved settings.json ──────────────────

        private static string GetApiKey()
        {
            string? env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            return LoadSettings()?.ApiKey ?? string.Empty;
        }

        private static string GetModel()
        {
            return LoadSettings()?.Model ?? "gpt-4o";
        }

        private static AppSettings? LoadSettings()
        {
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "UpsMaintenanceApp", "settings.json");
                if (!System.IO.File.Exists(path)) return null;
                var s = JsonSerializer.Deserialize<AppSettings>(System.IO.File.ReadAllText(path));
                if (!string.IsNullOrWhiteSpace(s?.ApiKey))
                    Environment.SetEnvironmentVariable("OPENAI_API_KEY", s.ApiKey);
                return s;
            }
            catch { return null; }
        }

        // ── Alarm context builder ─────────────────────────────────────────────

        private static string BuildAlarmContext(IList<AlarmEvent> alarms)
        {
            if (alarms.Count == 0) return "No alarms recorded in the log.";

            var active  = alarms.Where(a =>  a.IsActive).OrderBy(a => a.OccurredAt).ToList();
            var cleared = alarms.Where(a => !a.IsActive).OrderBy(a => a.OccurredAt).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"ALARM LOG SUMMARY  ({alarms.Count} total events)");
            sb.AppendLine("Interpretation: no prefix = alarm/status ACTIVE; 'X - ' prefix = alarm/status DEACTIVATED/REMOVED.");
            sb.AppendLine($"  e.g. 'Inverter_Fail'  → inverter has FAILED (active alarm)");
            sb.AppendLine($"  e.g. 'X - Inverter_ON' → inverter ON status was REMOVED, meaning inverter is currently OFF");
            sb.AppendLine();

            if (active.Count > 0)
            {
                sb.AppendLine($"--- ACTIVE ALARMS ({active.Count}) ---");
                foreach (var a in active.Take(30))
                    sb.AppendLine($"  [{a.OccurredAt:dd-MMM HH:mm:ss}] {a.Description}  [{a.Category}]");
            }
            else
            {
                sb.AppendLine("--- NO ACTIVE ALARMS ---");
            }

            sb.AppendLine();
            if (cleared.Count > 0)
            {
                sb.AppendLine($"--- DEACTIVATED / CLEARED ({cleared.Count}) ---");
                foreach (var a in cleared.Take(30))
                    sb.AppendLine($"  [{a.OccurredAt:dd-MMM HH:mm:ss}] {a.Description}  [{a.Category}]");
            }

            return sb.ToString();
        }

        // ── Chart builders ────────────────────────────────────────────────────

        private void BuildElectricalChart(IList<TelemetryRow> rows)
        {
            int step   = Math.Max(1, rows.Count / 60);
            var sample = rows.Where((_, i) => i % step == 0).ToList();

            _vm.ElectricalSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name         = "Vdc Link (V)",
                    Values       = sample.Select(r => r.DcBusVoltage).ToArray(),
                    Stroke       = new SolidColorPaint(SKColors.RoyalBlue, 2),
                    Fill         = null,
                    GeometrySize = 0
                },
                new LineSeries<double>
                {
                    Name         = "Vout (V)",
                    Values       = sample.Select(r => r.OutputVoltageL1).ToArray(),
                    Stroke       = new SolidColorPaint(SKColors.ForestGreen, 2),
                    Fill         = null,
                    GeometrySize = 0
                },
                new LineSeries<double>
                {
                    Name         = "Freq Out (Hz)",
                    Values       = sample.Select(r => r.OutputFrequency).ToArray(),
                    Stroke       = new SolidColorPaint(SKColors.OrangeRed, 2),
                    Fill         = null,
                    GeometrySize = 0
                }
            };

            _vm.TimeAxis = new[]
            {
                new Axis
                {
                    Labels         = sample.Select(r => r.Timestamp.ToString("HH:mm")).ToArray(),
                    TextSize       = 9,
                    LabelsRotation = 15
                }
            };
        }

        private void BuildAlarmChart(IList<AlarmEvent> alarms)
        {
            var groups = alarms
                .GroupBy(a => string.IsNullOrWhiteSpace(a.Category) ? "Unknown" : a.Category)
                .OrderByDescending(g => g.Count())
                .Take(6)
                .ToList();

            _vm.AlarmSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name   = "Alarms",
                    Values = groups.Select(g => g.Count()).ToArray(),
                    Fill   = new SolidColorPaint(SKColor.Parse("#2563EB"))
                }
            };

            _vm.AlarmAxis = new[]
            {
                new Axis { Labels = groups.Select(g => g.Key).ToArray(), TextSize = 9 }
            };
        }

        // ── Navigation ────────────────────────────────────────────────────────

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var tag = btn.Tag?.ToString();

            PageDashboard.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
            PageUpload.Visibility    = tag == "Upload"    ? Visibility.Visible : Visibility.Collapsed;
            PageReport.Visibility    = tag == "Report"    ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility  = tag == "Settings"  ? Visibility.Visible : Visibility.Collapsed;

            foreach (var b in new[] { NavDashboard, NavUpload, NavReport, NavSettings })
            {
                bool active  = b == btn;
                b.Foreground = new System.Windows.Media.SolidColorBrush(active
                    ? System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB)
                    : System.Windows.Media.Color.FromRgb(0x5A, 0x64, 0x74));
                b.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            }

            if (tag == "Report" && _lastResult is not null)
                PageReport.LoadReport(_lastResult);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string GetDuration(IList<TelemetryRow> rows) =>
            rows.Count < 2 ? "—"
            : $"{(rows.Last().Timestamp - rows.First().Timestamp).TotalHours:F1}h";

        private static string GetLogWindow(IList<TelemetryRow> rows) =>
            rows.Count == 0 ? "—"
            : $"{rows.First().Timestamp:dd MMM  HH:mm} → {rows.Last().Timestamp:dd MMM  HH:mm}";

        private static int CountStorms(IList<AlarmEvent> alarms)
        {
            var sorted = alarms.OrderBy(a => a.OccurredAt).ToList();
            int storms = 0, i = 0;
            while (i < sorted.Count)
            {
                var start = sorted[i].OccurredAt;
                int w = sorted.Skip(i).TakeWhile(a => (a.OccurredAt - start).TotalMinutes < 5).Count();
                if (w >= 5) { storms++; i += w; } else i++;
            }
            return storms;
        }
    }
}
