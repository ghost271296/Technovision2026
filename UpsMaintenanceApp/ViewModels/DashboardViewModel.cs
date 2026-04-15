using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System.Collections.ObjectModel;

namespace UpsMaintenanceApp.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        // KPI Strip
        [ObservableProperty] private int    _overallHealthIndex;
        [ObservableProperty] private double _alarmRate;
        [ObservableProperty] private int    _totalAlarms;
        [ObservableProperty] private int    _alarmStorms;
        [ObservableProperty] private double _vdcStd;
        [ObservableProperty] private double _freqError;
        [ObservableProperty] private double _vdcMean;

        // Topbar
        [ObservableProperty] private string _assetInfo       = "⚡ HiPulse V UPS  |  Asset: UPS-01";
        [ObservableProperty] private string _dataQualityInfo = "Duration: —  |  Rows: —";
        [ObservableProperty] private string _logWindowInfo   = "Log Window: —";

        // Alert Banner
        [ObservableProperty] private string _urgency      = "Ready";
        [ObservableProperty] private string _alertMessage = "Load a DataLog file to begin analysis.";

        // Subsystem Health
        [ObservableProperty] private int _rectifierHealth;
        [ObservableProperty] private int _inverterHealth;
        [ObservableProperty] private int _batteryHealth;
        [ObservableProperty] private int _dcLinkHealth;
        [ObservableProperty] private int _thermalStress;

        // Risk Forecast
        [ObservableProperty] private int _inverterFailRisk;
        [ObservableProperty] private int _rectifierFailRisk;
        [ObservableProperty] private int _batteryFailRisk;

        // Root Cause
        [ObservableProperty] private string _rootCause           = "—";
        [ObservableProperty] private string _rootCauseConfidence = "—";
        [ObservableProperty] private ObservableCollection<string> _patterns = new();

        // Signal Analysis
        [ObservableProperty] private string _dcStability        = "—";
        [ObservableProperty] private string _batteryBehavior    = "—";
        [ObservableProperty] private string _frequencyStability = "—";
        [ObservableProperty] private string _stressLevel        = "—";

        // AI Insight
        [ObservableProperty] private string _insightSummary    = "—";
        [ObservableProperty] private string _insightConfidence = "—";
        [ObservableProperty] private ObservableCollection<string> _topIssues = new();
        [ObservableProperty] private ObservableCollection<string> _actions   = new();

        // Charts
        [ObservableProperty] private ISeries[]        _electricalSeries = System.Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[]        _alarmSeries      = System.Array.Empty<ISeries>();
        [ObservableProperty] private ICartesianAxis[] _timeAxis         = new[] { new Axis() };
        [ObservableProperty] private ICartesianAxis[] _alarmAxis        = new[] { new Axis() };

        // Pipeline State
        [ObservableProperty] private string _pipelineStatus = "Ready";
        [ObservableProperty] private bool   _isAnalyzing;
    }
}