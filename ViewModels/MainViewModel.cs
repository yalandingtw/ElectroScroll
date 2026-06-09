using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ElectroScroll.Models;
using ElectroScroll.Services;

namespace ElectroScroll.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const int ChartSampleLimit = 180;
    private const double ChartWidth = 560;
    private const double ChartHeight = 140;
    private const double ChartSampleIntervalMs = 33;
    private readonly AppSettings _settings;
    private readonly ScrollController _controller;
    private readonly SettingsStore _store;
    private readonly Queue<double> _inputSamples = new();
    private readonly Queue<double> _outputSamples = new();
    private readonly Stopwatch _chartClock = Stopwatch.StartNew();
    private string _profileName = "Default";
    private string _processName = "none";
    private string _windowTitle = "Move the cursor over a scrollable window";
    private string _speed = "0.000";
    private string _boost = "1.00x";
    private string _velocity = "0.000";
    private string _inputSignal = "0.000";
    private string _outputSignal = "0.00";
    private string _status = "Native";
    private string _diagnostics = "";
    private bool _isIntercepting;
    private string _saveStatus;
    private double _lastChartSampleMs;
    private PointCollection _inputChartPoints = new();
    private PointCollection _outputChartPoints = new();
    private readonly PerformanceModeStatus _performanceModeStatus;

    public MainViewModel(
        AppSettings settings,
        ScrollController controller,
        SettingsStore store,
        PerformanceModeStatus performanceModeStatus)
    {
        _settings = settings;
        _controller = controller;
        _store = store;
        _performanceModeStatus = performanceModeStatus;
        Text = new LocalizedText(settings.Language);
        _saveStatus = Text["SettingsLoaded"];

        ApplyPreciseCommand = new RelayCommand(_ => ApplyPreset(ScrollTuning.Precise, Text["PreciseApplied"]));
        ApplyBalancedCommand = new RelayCommand(_ => ApplyPreset(ScrollTuning.Balanced, Text["BalancedApplied"]));
        ApplyFreeSpinCommand = new RelayCommand(_ => ApplyPreset(ScrollTuning.FreeSpin, Text["FreeSpinApplied"]));
        SaveCommand = new RelayCommand(_ => Save());
        ResetMotionCommand = new RelayCommand(_ => _controller.ResetMotion());
        UseEnglishCommand = new RelayCommand(_ => SetLanguage(UiLanguage.English));
        UseTraditionalChineseCommand = new RelayCommand(_ => SetLanguage(UiLanguage.TraditionalChinese));

        _controller.MetricsUpdated += OnMetricsUpdated;
        _controller.EnabledChanged += OnControllerEnabledChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ApplyPreciseCommand { get; }
    public ICommand ApplyBalancedCommand { get; }
    public ICommand ApplyFreeSpinCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ResetMotionCommand { get; }
    public ICommand UseEnglishCommand { get; }
    public ICommand UseTraditionalChineseCommand { get; }
    public LocalizedText Text { get; }

    public bool ChartsEnabled
    {
        get => _settings.DiagnosticsChartEnabled;
        set
        {
            if (_settings.DiagnosticsChartEnabled == value)
            {
                return;
            }

            _settings.DiagnosticsChartEnabled = value;
            if (!value)
            {
                ClearChartSamples();
            }

            _lastChartSampleMs = 0;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ChartVisibility));
        }
    }

    public Visibility ChartVisibility => ChartsEnabled ? Visibility.Visible : Visibility.Collapsed;

    public PointCollection InputChartPoints
    {
        get => _inputChartPoints;
        private set => SetField(ref _inputChartPoints, value);
    }

    public PointCollection OutputChartPoints
    {
        get => _outputChartPoints;
        private set => SetField(ref _outputChartPoints, value);
    }

    public bool Enabled
    {
        get => _settings.Enabled;
        set
        {
            if (_settings.Enabled == value)
            {
                return;
            }

            _controller.Enabled = value;
            OnPropertyChanged();
        }
    }

    public bool BypassWithModifiers
    {
        get => _settings.BypassWithModifiers;
        set
        {
            if (_settings.BypassWithModifiers == value)
            {
                return;
            }

            _settings.BypassWithModifiers = value;
            OnPropertyChanged();
        }
    }

    public bool AutoBypassFullscreen
    {
        get => _settings.AutoBypassFullscreen;
        set
        {
            if (_settings.AutoBypassFullscreen == value)
            {
                return;
            }

            _settings.AutoBypassFullscreen = value;
            _controller.ResetMotion();
            OnPropertyChanged();
        }
    }

    public bool AutoBypassKnownGames
    {
        get => _settings.AutoBypassKnownGames;
        set
        {
            if (_settings.AutoBypassKnownGames == value)
            {
                return;
            }

            _settings.AutoBypassKnownGames = value;
            _controller.ResetMotion();
            OnPropertyChanged();
        }
    }

    public string PerformanceStatus =>
        _performanceModeStatus.Enabled
            ? $"{Text["PerformanceMode"]}: {Text["Timer"]}={(_performanceModeStatus.TimerResolutionSet ? "1ms" : Text["Default"])}, {Text["EcoQos"]}={(_performanceModeStatus.EcoQosDisabled ? Text["Off"] : Text["Unchanged"])}, {Text["GcLowLatency"]}, Output=PostMessage"
            : Text["PerformanceOff"];

    public double Step
    {
        get => Tuning.Step;
        set => SetTuning(value, tuning => tuning.Step = value);
    }

    public double Threshold
    {
        get => Tuning.Threshold;
        set => SetTuning(value, tuning => tuning.Threshold = value);
    }

    public double Acceleration
    {
        get => Tuning.Acceleration;
        set => SetTuning(value, tuning => tuning.Acceleration = value);
    }

    public double MaxBoost
    {
        get => Tuning.MaxBoost;
        set => SetTuning(value, tuning => tuning.MaxBoost = value);
    }

    public double KickMs
    {
        get => Tuning.KickMs;
        set => SetTuning(value, tuning => tuning.KickMs = value);
    }

    public double FrictionMs
    {
        get => Tuning.FrictionMs;
        set => SetTuning(value, tuning => tuning.FrictionMs = value);
    }

    public double Flywheel
    {
        get => Tuning.Flywheel;
        set => SetTuning(value, tuning => tuning.Flywheel = value);
    }

    public double DirectShare
    {
        get => Tuning.DirectShare;
        set => SetTuning(value, tuning => tuning.DirectShare = value);
    }

    public int Smoothness
    {
        get => Tuning.Smoothness;
        set => SetTuning(value, tuning => tuning.Smoothness = value);
    }

    public string ProfileName
    {
        get => _profileName;
        private set => SetField(ref _profileName, value);
    }

    public string ProcessName
    {
        get => _processName;
        private set => SetField(ref _processName, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetField(ref _windowTitle, value);
    }

    public string Speed
    {
        get => _speed;
        private set => SetField(ref _speed, value);
    }

    public string Boost
    {
        get => _boost;
        private set => SetField(ref _boost, value);
    }

    public string Velocity
    {
        get => _velocity;
        private set => SetField(ref _velocity, value);
    }

    public string InputSignal
    {
        get => _inputSignal;
        private set => SetField(ref _inputSignal, value);
    }

    public string OutputSignal
    {
        get => _outputSignal;
        private set => SetField(ref _outputSignal, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public string Diagnostics
    {
        get => _diagnostics;
        private set => SetField(ref _diagnostics, value);
    }

    public bool IsIntercepting
    {
        get => _isIntercepting;
        private set => SetField(ref _isIntercepting, value);
    }

    public string SaveStatus
    {
        get => _saveStatus;
        private set => SetField(ref _saveStatus, value);
    }

    private ScrollTuning Tuning => _settings.DefaultProfile.Tuning;

    private void ApplyPreset(Func<ScrollTuning> presetFactory, string status)
    {
        _controller.ApplyPreset(presetFactory);
        NotifyTuningChanged();
        SaveStatus = status;
    }

    private void Save()
    {
        _store.Save(_settings);
        SaveStatus = $"{Text["Saved"]} {DateTime.Now:HH:mm:ss}";
    }

    private void SetLanguage(UiLanguage language)
    {
        _settings.Language = language;
        Text.Language = language;
        SaveStatus = Text["LanguageChanged"];
        OnPropertyChanged(nameof(PerformanceStatus));
    }

    private void SetTuning(double value, Action<ScrollTuning> setter, [CallerMemberName] string? propertyName = null)
    {
        setter(Tuning);
        _controller.ResetMotion();
        OnPropertyChanged(propertyName);
    }

    private void SetTuning(int value, Action<ScrollTuning> setter, [CallerMemberName] string? propertyName = null)
    {
        setter(Tuning);
        _controller.ResetMotion();
        OnPropertyChanged(propertyName);
    }

    private void NotifyTuningChanged()
    {
        OnPropertyChanged(nameof(Step));
        OnPropertyChanged(nameof(Threshold));
        OnPropertyChanged(nameof(Acceleration));
        OnPropertyChanged(nameof(MaxBoost));
        OnPropertyChanged(nameof(KickMs));
        OnPropertyChanged(nameof(FrictionMs));
        OnPropertyChanged(nameof(Flywheel));
        OnPropertyChanged(nameof(DirectShare));
        OnPropertyChanged(nameof(Smoothness));
    }

    private void OnMetricsUpdated(object? sender, ScrollMetrics metrics)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Speed = metrics.Speed.ToString("0.000");
            Boost = $"{metrics.Boost:0.00}x";
            Velocity = metrics.Velocity.ToString("0.000");
            InputSignal = metrics.InputSignal.ToString("0.000");
            OutputSignal = metrics.OutputSignal.ToString("0.00");
            ProfileName = metrics.ProfileName;
            ProcessName = metrics.ProcessName;
            WindowTitle = metrics.WindowTitle;
            IsIntercepting = metrics.IsIntercepting;
            Status = metrics.Status;
            Diagnostics = metrics.Diagnostics;
            AppendChartSample(metrics);
        });
    }

    private void OnControllerEnabledChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(Enabled)));
    }

    private void AppendChartSample(ScrollMetrics metrics)
    {
        if (!ChartsEnabled)
        {
            return;
        }

        var now = _chartClock.Elapsed.TotalMilliseconds;
        if (now - _lastChartSampleMs < ChartSampleIntervalMs)
        {
            return;
        }

        _lastChartSampleMs = now;
        AddSample(_inputSamples, NormalizeInput(metrics.InputSignal));
        AddSample(_outputSamples, NormalizeOutput(metrics.OutputSignal));
        InputChartPoints = BuildChartPoints(_inputSamples);
        OutputChartPoints = BuildChartPoints(_outputSamples);
    }

    private void ClearChartSamples()
    {
        _inputSamples.Clear();
        _outputSamples.Clear();
        InputChartPoints = new PointCollection();
        OutputChartPoints = new PointCollection();
        InputSignal = "0.000";
        OutputSignal = "0.00";
    }

    private double NormalizeInput(double speed)
    {
        var scale = Math.Max(Tuning.Threshold * 2.2, 0.03);
        return ClampUnit(1 - Math.Exp(-Math.Max(0, speed) / scale));
    }

    private static double NormalizeOutput(double outputSignal)
    {
        return ClampUnit(1 - Math.Exp(-Math.Max(0, outputSignal) / 1.25));
    }

    private static void AddSample(Queue<double> samples, double value)
    {
        samples.Enqueue(ClampUnit(value));
        while (samples.Count > ChartSampleLimit)
        {
            samples.Dequeue();
        }
    }

    private static PointCollection BuildChartPoints(Queue<double> samples)
    {
        var points = new PointCollection();
        if (samples.Count == 0)
        {
            return points;
        }

        var step = ChartWidth / (ChartSampleLimit - 1);
        var x = ChartWidth - step * (samples.Count - 1);
        foreach (var sample in samples)
        {
            points.Add(new System.Windows.Point(x, ChartHeight - sample * ChartHeight));
            x += step;
        }

        return points;
    }

    private static double ClampUnit(double value)
    {
        return Math.Min(1, Math.Max(0, value));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
