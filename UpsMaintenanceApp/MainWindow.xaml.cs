using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using UpsMaintenanceApp.Models;
using UpsMaintenanceApp.Services;
using UpsMaintenanceApp.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows.Controls;

namespace UpsMaintenanceApp
{
    public partial class MainWindow : Window
    {
        private readonly DashboardViewModel _vm = new();

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
                        // var telemetry = ExcelParser.ParseTelemetry(filePath);
                        // var alarms    = ExcelParser.ParseAlarms(filePath);
                        // AlarmGrid.ItemsSource   = alarms;
                        // _vm.TotalAlarms         = alarms.Count;
                        // _vm.AlarmStorms         = CountStorms(alarms);
                        // _vm.DataQualityInfo     = $"Duration: {GetDuration(telemetry)}  |  Rows: {telemetry.Count:N0}";
                        // _vm.LogWindowInfo       = $"Log: {GetLogWindow(telemetry)}";
                        // var features = FeatureEngine.ComputeAll(telemetry, alarms);
                        // features.TryGetValue("VdcMean",          out double vdcMean);
                        // features.TryGetValue("VdcStd",           out double vdcStd);
                        // features.TryGetValue("AlarmRatePerHour", out double alarmRate);
                        // _vm.VdcMean = vdcMean; _vm.VdcStd = vdcStd; _vm.AlarmRate = alarmRate;
                        // _vm.FreqError = telemetry.Count > 0 ? telemetry.Average(r => Math.Abs(r.InputFrequency - 50.0)) : 0;
                        // BuildElectricalChart(telemetry);
                        // BuildAlarmChart(alarms);

                        // PageUpload.SetProgress("Running AI pipeline…", "5 agents processing…", true);
                        // string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
                        // if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("OPENAI_API_KEY not set. Configure it in Settings.");
                        // var result = await new AnalysisPipeline(apiKey).RunAsync(features);
                        // _lastResult = result;

                        // _vm.BatteryHealth   = result.Health.BatteryHealth;
                        // _vm.DcLinkHealth    = result.Health.DcLinkHealth;
                        // _vm.InverterHealth  = result.Health.PowerStageHealth;
                        // _vm.RectifierHealth = (result.Health.DcLinkHealth + result.Health.PowerStageHealth) / 2;
                        // _vm.ThermalStress   = result.Health.ThermalStress;
                        // _vm.OverallHealthIndex = (result.Health.BatteryHealth + result.Health.DcLinkHealth + result.Health.PowerStageHealth) / 3;
                        // _vm.InverterFailRisk = result.Prediction.InverterFail;
                        // _vm.RectifierFailRisk = result.Prediction.RectifierFail;
                        // _vm.BatteryFailRisk  = result.Prediction.BatteryFail;
                        // _vm.Urgency          = result.Prediction.Urgency;
                        // _vm.DcStability      = result.CoreSignal.DcStability;
                        // _vm.BatteryBehavior  = result.CoreSignal.BatteryBehavior;
                        // _vm.FrequencyStability = result.CoreSignal.FrequencyStability;
                        // _vm.StressLevel      = result.CoreSignal.StressLevel;
                        // _vm.RootCause        = result.EventCorrelation.RootCause;
                        // _vm.RootCauseConfidence = result.EventCorrelation.Confidence;
                        // _vm.Patterns         = new ObservableCollection<string>(result.EventCorrelation.Patterns);
                        // _vm.InsightSummary   = result.Insight.Summary;
                        // _vm.InsightConfidence = result.Insight.Confidence;
                        // _vm.TopIssues        = new ObservableCollection<string>(result.Insight.TopIssues);
                        // _vm.Actions          = new ObservableCollection<string>(result.Insight.Actions);
                        // _vm.AlertMessage     = result.Insight.Summary;
                        // _vm.PipelineStatus   = $"Analysis complete  •  {result.CompletedAt:HH:mm:ss}";
                        // await RunPipeline(dataPath, alarmPath);
                        // Navigate to Dashboard
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

        private async void BtnLoadFile_Click(object sender, RoutedEventArgs e)
        {
            // var dialog = new OpenFileDialog
            // {
            //     Title  = "Select UPS Excel DataLog",
            //     Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*"
            // };
            // if (dialog.ShowDialog() != true) return;
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

            //string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
            string apiKey = "sk - proj - ul3eclYQOX8KfjlECLLbQ9NCgpH8P5mQo8nW7CuHW5x2sijX3b0GoWpWgdLEqshDPRYstZbf2qT3BlbkFJE - PGpLLiaN7XiRbjDI3qHkm9eMKHIYtBW7TK7zX8c449M1HsS5CDopWUzanoFedj2QBZ - GuTYA";

			if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show(
                    "Set OPENAI_API_KEY in Settings or as an environment variable before running.",
                    "API Key Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnLoadFile.IsEnabled = false;
            _vm.IsAnalyzing    = true;
            _vm.PipelineStatus = "Parsing Log files…";

            try
            {
                // // ── Parse ──────────────────────────────────────────────────
                // var telemetry = ExcelParser.ParseTelemetry(dialog.FileName);
                // var alarms    = ExcelParser.ParseAlarms(dialog.FileName);

                // AlarmGrid.ItemsSource = alarms;

                // _vm.TotalAlarms     = alarms.Count;
                // _vm.AlarmStorms     = CountStorms(alarms);
                // _vm.DataQualityInfo = $"Duration: {GetDuration(telemetry)}  |  Rows: {telemetry.Count:N0}";
                // _vm.LogWindowInfo   = $"Log: {GetLogWindow(telemetry)}";

                // // ── Features ───────────────────────────────────────────────
                // _vm.PipelineStatus = "Computing features…";
                // var features = FeatureEngine.ComputeAll(telemetry, alarms);

                // features.TryGetValue("VdcMean",          out double vdcMean);
                // features.TryGetValue("VdcStd",           out double vdcStd);
                // features.TryGetValue("AlarmRatePerHour", out double alarmRate);

                // _vm.VdcMean   = vdcMean;
                // _vm.VdcStd    = vdcStd;
                // _vm.AlarmRate = alarmRate;

                // // Frequency error: average deviation from 50 Hz
                // _vm.FreqError = telemetry.Count > 0
                //     ? telemetry.Average(r => Math.Abs(r.InputFrequency - 50.0))
                //     : 0;

                // BuildElectricalChart(telemetry);
                // BuildAlarmChart(alarms);

                // // ── AI Pipeline ────────────────────────────────────────────
                // _vm.PipelineStatus = "Running AI pipeline — Agent 1 / 5…";
                // var result = await new AnalysisPipeline(apiKey).RunAsync(features);
                // _lastResult = result;
                // // CoreSignal
                // _vm.DcStability        = result.CoreSignal.DcStability;
                // _vm.BatteryBehavior    = result.CoreSignal.BatteryBehavior;
                // _vm.FrequencyStability = result.CoreSignal.FrequencyStability;
                // _vm.StressLevel        = result.CoreSignal.StressLevel;

                // // Health
                // _vm.BatteryHealth   = result.Health.BatteryHealth;
                // _vm.DcLinkHealth    = result.Health.DcLinkHealth;
                // _vm.InverterHealth  = result.Health.PowerStageHealth;
                // _vm.RectifierHealth = (result.Health.DcLinkHealth + result.Health.PowerStageHealth) / 2;
                // _vm.ThermalStress   = result.Health.ThermalStress;
                // _vm.OverallHealthIndex = (result.Health.BatteryHealth +
                //                           result.Health.DcLinkHealth +
                //                           result.Health.PowerStageHealth) / 3;

                // // Prediction
                // _vm.InverterFailRisk  = result.Prediction.InverterFail;
                // _vm.RectifierFailRisk = result.Prediction.RectifierFail;
                // _vm.BatteryFailRisk   = result.Prediction.BatteryFail;
                // _vm.Urgency           = result.Prediction.Urgency;

                // // Event Correlation
                // _vm.RootCause           = result.EventCorrelation.RootCause;
                // _vm.RootCauseConfidence = result.EventCorrelation.Confidence;
                // _vm.Patterns = new ObservableCollection<string>(result.EventCorrelation.Patterns);

                // // Insight
                // _vm.InsightSummary    = result.Insight.Summary;
                // _vm.InsightConfidence = result.Insight.Confidence;
                // _vm.TopIssues  = new ObservableCollection<string>(result.Insight.TopIssues);
                // _vm.Actions    = new ObservableCollection<string>(result.Insight.Actions);
                // _vm.AlertMessage = result.Insight.Summary;

                // _vm.PipelineStatus = result.HasErrors
                //     ? $"Done with {result.Errors.Count} error(s)  •  {result.CompletedAt:HH:mm:ss}"
                //     : $"Analysis complete  •  {result.CompletedAt:HH:mm:ss}";
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

        private async System.Threading.Tasks.Task RunPipeline(string dataPath, string alarmPath)
        {
            _vm.IsAnalyzing    = true;
            _vm.PipelineStatus = "Parsing files…";

            // ── Parse ──────────────────────────────────────────────────────────
            var telemetry = CsvParser.ParseTelemetry(dataPath);
            var alarms    = CsvParser.ParseAlarms(alarmPath);

            if (telemetry.Count == 0)
                throw new InvalidOperationException(
                    "No telemetry rows parsed. Check that the DataLog file has the expected tab-separated format with a 'Date Time' column.");

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
            // FreqError: deviation from 50 Hz, only for rows where UPS is outputting
            var outputRows = telemetry.Where(r => r.OutputFrequency > 0).ToList();
            _vm.FreqError = outputRows.Count > 0
                ? outputRows.Average(r => Math.Abs(r.OutputFrequency - 50.0)) : 0;

            BuildElectricalChart(telemetry);
            BuildAlarmChart(alarms);

            // ── AI Pipeline ────────────────────────────────────────────────────
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "OPENAI_API_KEY not set. Configure it in Settings or as an environment variable.");

            _vm.PipelineStatus = "Running AI pipeline — Agent 1 / 5…";
            var result = await new AnalysisPipeline(apiKey).RunAsync(features);
            _lastResult = result;

            // CoreSignal
            _vm.DcStability         = result.CoreSignal.DcStability;
            _vm.BatteryBehavior     = result.CoreSignal.BatteryBehavior;
            _vm.FrequencyStability  = result.CoreSignal.FrequencyStability;
            _vm.StressLevel         = result.CoreSignal.StressLevel;
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

        private void BuildElectricalChart(IList<TelemetryRow> rows)
        {
            int step   = Math.Max(1, rows.Count / 60);
            var sample = rows.Where((_, i) => i % step == 0).ToList();

            _vm.ElectricalSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Name         = "Vdc Link(V)",
                    Values       = sample.Select(r => r.DcBusVoltage).ToArray(),
                    Stroke       = new SolidColorPaint(SKColors.RoyalBlue, 2),
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

        // private static string GetDuration(IList<TelemetryRow> rows) =>
        //     rows.Count < 2 ? "—"
        //     : $"{(rows.Last().Timestamp - rows.First().Timestamp).TotalHours:F1}h";

        private static string GetDuration(IList<TelemetryRow> rows) =>
            rows.Count < 2 ? "—"
            : $"{(rows.Last().Timestamp - rows.First().Timestamp).TotalHours:F1}h";

        // private static string GetLogWindow(IList<TelemetryRow> rows) =>
        //     rows.Count == 0 ? "—"
        //     : $"{rows.First().Timestamp:HH:mm} → {rows.Last().Timestamp:HH:mm}";

        // private static int CountStorms(IList<AlarmEvent> alarms)
        // {
        //     int storms = 0, i = 0;
        //     while (i < alarms.Count)
        //     {
        //         var start = alarms[i].OccurredAt;
        //         int w = alarms.Skip(i).TakeWhile(a => (a.OccurredAt - start).TotalMinutes < 5).Count();
        //         if (w >= 5) { storms++; i += w; } else i++;
        //     }
        //     return storms;
        // }

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
        
        // Add at the top of the class, after _vm declaration:
        private FinalResult? _lastResult;

        // Add this method:
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

        
    }
}