using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace UpsMaintenanceApp.Views
{
    public partial class UploadVisitPage : UserControl
    {
        private string? _dataLogPath;
        private string? _alarmLogPath;

        public event Action<string, string>? AnalysisRequested;

        public UploadVisitPage()
        {
            InitializeComponent();
        }

        // =========================
        // DATA LOG HANDLING
        // =========================
        private void BtnBrowseData_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select DataLog File",
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };

            if (dlg.ShowDialog() == true)
                SetDataLog(dlg.FileName);
        }

        private void DropZoneData_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0)
                SetDataLog(files[0]);
        }

        private void SetDataLog(string path)
        {
            _dataLogPath = path;

            var info = new FileInfo(path);
            TxtDataFile.Text = $"{info.Name}  •  {info.Length / 1024:N0} KB";

            DataFileInfo.Visibility = Visibility.Visible;
            UpdateAnalyzeState();
        }

        // =========================
        // ALARM LOG HANDLING
        // =========================
        private void BtnBrowseAlarm_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select AlarmLog File",
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };

            if (dlg.ShowDialog() == true)
                SetAlarmLog(dlg.FileName);
        }

        private void DropZoneAlarm_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0)
                SetAlarmLog(files[0]);
        }

        private void SetAlarmLog(string path)
        {
            _alarmLogPath = path;

            var info = new FileInfo(path);
            TxtAlarmFile.Text = $"{info.Name}  •  {info.Length / 1024:N0} KB";

            AlarmFileInfo.Visibility = Visibility.Visible;
            UpdateAnalyzeState();
        }

        // =========================
        // COMMON DRAG OVER
        // =========================
        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;

            e.Handled = true;
        }

        // =========================
        // ANALYZE BUTTON
        // =========================
        private void UpdateAnalyzeState()
        {
            BtnAnalyze.IsEnabled =
                !string.IsNullOrEmpty(_dataLogPath) &&
                !string.IsNullOrEmpty(_alarmLogPath);
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (_dataLogPath != null && _alarmLogPath != null)
            {
                AnalysisRequested?.Invoke(_dataLogPath, _alarmLogPath);
            }
        }

        // =========================
        // PROGRESS UI
        // =========================
        public void SetProgress(string step, string detail, bool visible)
        {
            ProgressPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            TxtProgressStep.Text = step;
            TxtProgressDetail.Text = detail;

            BtnAnalyze.IsEnabled = !visible;
            BtnBrowseData.IsEnabled = !visible;
            BtnBrowseAlarm.IsEnabled = !visible;
        }
    }
}