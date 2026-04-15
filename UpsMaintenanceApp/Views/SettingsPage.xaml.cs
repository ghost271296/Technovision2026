using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace UpsMaintenanceApp.Views
{
    public record AppSettings(string ApiKey, string Model, double Temperature, string AssetId, string UpsModel);

    public partial class SettingsPage : UserControl
    {
        private static readonly string SettingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "UpsMaintenanceApp", "settings.json");

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += (_, _) => { LoadSettings(); CheckEnvKey(); };
        }

        private void CheckEnvKey()
        {
            string? env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            TxtApiKeyStatus.Text = string.IsNullOrWhiteSpace(env)
                ? "⚠ OPENAI_API_KEY environment variable not found."
                : "✓ OPENAI_API_KEY environment variable is set.";
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (s is null) return;
                TxtApiKey.Password    = s.ApiKey;
                SldTemperature.Value  = s.Temperature;
                TxtAssetId.Text       = s.AssetId;
                TxtUpsModel.Text      = s.UpsModel;
                CboModel.SelectedIndex = s.Model switch { "gpt-4o-mini" => 1, "gpt-4-turbo" => 2, _ => 0 };

                // Apply saved API key to environment so pipeline works on every launch
                if (!string.IsNullOrWhiteSpace(s.ApiKey))
                    Environment.SetEnvironmentVariable("OPENAI_API_KEY", s.ApiKey);
            }
            catch { }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var model = (CboModel.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "gpt-4o";
                var s     = new AppSettings(TxtApiKey.Password, model, SldTemperature.Value,
                                            TxtAssetId.Text, TxtUpsModel.Text);
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s));

                if (!string.IsNullOrWhiteSpace(TxtApiKey.Password))
                    Environment.SetEnvironmentVariable("OPENAI_API_KEY", TxtApiKey.Password);

                TxtSaveStatus.Text       = "✓ Settings saved successfully.";
                TxtSaveStatus.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                TxtSaveStatus.Text       = $"Error: {ex.Message}";
                TxtSaveStatus.Visibility = Visibility.Visible;
            }
        }

        public string GetApiKey() => TxtApiKey.Password;
        public string GetModel()  => (CboModel.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "gpt-4o";
    }
}