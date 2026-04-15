using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace UpsMaintenanceApp.Views
{
    public partial class UploadVisitPage : UserControl
    {
        private string? _selectedFile;
        public event Action<string>? AnalysisRequested;

        public UploadVisitPage() => InitializeComponent();

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Select UPS Excel DataLog", Filter = "Excel Files (*.xlsx)|*.xlsx" };
            if (dlg.ShowDialog() == true) SetFile(dlg.FileName);
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0) SetFile(files[0]);
        }

        private void SetFile(string path)
        {
            _selectedFile           = path;
            var info                = new FileInfo(path);
            TxtFileName.Text        = info.Name;
            TxtFileInfo.Text        = $"{info.Length / 1024:N0} KB  •  Modified {info.LastWriteTime:dd MMM yyyy}";
            FileInfoPanel.Visibility = Visibility.Visible;
            BtnAnalyze.IsEnabled    = true;
        }

        private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            _selectedFile            = null;
            FileInfoPanel.Visibility = Visibility.Collapsed;
            BtnAnalyze.IsEnabled     = false;
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFile is not null) AnalysisRequested?.Invoke(_selectedFile);
        }

        public void SetProgress(string step, string detail, bool visible)
        {
            ProgressPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            TxtProgressStep.Text     = step;
            TxtProgressDetail.Text   = detail;
            BtnAnalyze.IsEnabled     = !visible;
            BtnBrowse.IsEnabled      = !visible;
        }
    }
}