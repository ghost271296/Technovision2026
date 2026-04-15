using System.Windows;
using System.Windows.Controls;
using UpsMaintenanceApp.Services;

namespace UpsMaintenanceApp.Views
{
    public partial class ReportPage : UserControl
    {
        public ReportPage() => InitializeComponent();

        public void LoadReport(FinalResult r)
        {
            NoDataPanel.Visibility = Visibility.Collapsed;
            ReportBody.Visibility  = Visibility.Visible;

            TxtReportDate.Text     = $"Generated: {r.CompletedAt:dd MMM yyyy  HH:mm}";
            TxtSummary.Text        = r.Insight.Summary;
            TxtRptHealth.Text      = ((r.Health.BatteryHealth + r.Health.DcLinkHealth + r.Health.PowerStageHealth) / 3).ToString();
            TxtRptUrgency.Text     = r.Prediction.Urgency;
            TxtRptConfidence.Text  = r.Insight.Confidence;
            TxtRptRootCause.Text   = $"{r.EventCorrelation.RootCause}  (Confidence: {r.EventCorrelation.Confidence})";
            ActionsList.ItemsSource = r.Insight.Actions;

            HealthGrid.ItemsSource = new[]
            {
                new { Component = "Battery",     Score = r.Health.BatteryHealth,    Status = Score(r.Health.BatteryHealth) },
                new { Component = "DC Link",      Score = r.Health.DcLinkHealth,     Status = Score(r.Health.DcLinkHealth) },
                new { Component = "Power Stage",  Score = r.Health.PowerStageHealth, Status = Score(r.Health.PowerStageHealth) },
                new { Component = "Thermal Stress", Score = r.Health.ThermalStress,  Status = ThermalScore(r.Health.ThermalStress) }
            };

            RiskGrid.ItemsSource = new[]
            {
                new { Component = "Inverter",  Risk = $"{r.Prediction.InverterFail}%",  Priority = Priority(r.Prediction.InverterFail) },
                new { Component = "Rectifier", Risk = $"{r.Prediction.RectifierFail}%", Priority = Priority(r.Prediction.RectifierFail) },
                new { Component = "Battery",   Risk = $"{r.Prediction.BatteryFail}%",   Priority = Priority(r.Prediction.BatteryFail) }
            };
        }

        private static string Score(int v)        => v >= 80 ? "Good" : v >= 60 ? "Warning" : "Critical";
        private static string ThermalScore(int v) => v <= 40 ? "Good" : v <= 70 ? "Warning" : "Critical";
        private static string Priority(int v)     => v >= 70 ? "Immediate" : v >= 40 ? "High" : v >= 20 ? "Medium" : "Low";

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var pd = new PrintDialog();
            if (pd.ShowDialog() == true)
                pd.PrintVisual(ReportContent, "UPS Analysis Report");
        }
    }
}