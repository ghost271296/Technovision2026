using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using UpsMaintenanceApp.Services;
 
namespace UpsMaintenanceApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
 
        private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Select UPS Excel DataLog",
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*"
            };
 
            if (dialog.ShowDialog() != true) return;
 
            try
            {
                TxtStatus.Text = $"Parsing {Path.GetFileName(dialog.FileName)}…";
 
                var telemetry = ExcelParser.ParseTelemetry(dialog.FileName);
                var alarms    = ExcelParser.ParseAlarms(dialog.FileName);
 
                AlarmGrid.ItemsSource             = alarms;
                TxtPlaceholder.Visibility         = Visibility.Collapsed;
 
                TxtStatus.Text = $"Loaded {telemetry.Count} telemetry rows · {alarms.Count} alarm events  |  {dialog.FileName}";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}