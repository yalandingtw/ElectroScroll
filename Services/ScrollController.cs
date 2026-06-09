using System.Diagnostics;
using System.Threading;
using ElectroScroll.Interop;
using ElectroScroll.Models;

namespace ElectroScroll.Services;

public sealed class ScrollController : IDisposable
{
    private const double TimerMs = 4.0;
    private const double MetricsMs = 50.0;
    private const double HoverMetricsMs = 250.0;
    private readonly object _gate = new();
    private readonly WindowResolver _resolver = new();
    private readonly InterferenceGuard _interferenceGuard;
    private readonly System.Threading.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly AppSettings _settings;

    private double _lastWheelMs;
    private double _lastFrameMs;
    private double _speedEma;
    private double _boost = 1.0;
    private double _velocity;
    private double _accumulator;
    private double _glideUntilMs;
    private double _lastMetricsMs;
    private double _brakeUntilMs;
    private double _lastHoverMetricsMs;
    private double _inputSignal;
    private double _outputSignal;
    private int _burstCount;
    private int _brakeTicksRemaining;
    private int _brakeDirection;
    private int _lastDirection;
    private WindowTarget _target = WindowTarget.Empty;
    private NativeMethods.POINT _targetPoint;
    private nint _lastOutputHwnd;
    private NativeMethods.POINT _lastOutputPoint;
    private AppProfile _activeProfile;

    public event EventHandler<ScrollMetrics>? MetricsUpdated;
    public event EventHandler? EnabledChanged;

    public ScrollController(AppSettings settings)
    {
        _settings = settings;
        _interferenceGuard = new InterferenceGuard(settings);
        _activeProfile = settings.DefaultProfile;
        _lastFrameMs = NowMs;
        _timer = new System.Threading.Timer(Tick, null, TimeSpan.FromMilliseconds(TimerMs), TimeSpan.FromMilliseconds(TimerMs));
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

            _settings.Enabled = value;
            if (!value)
            {
                ResetMotion();
            }

            EnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ScrollTuning DefaultTuning => _settings.DefaultProfile.Tuning;

    internal bool OnWheel(int wheelDelta, NativeMethods.POINT point)
    {
        if (!_settings.Enabled || wheelDelta == 0)
        {
            return false;
        }

        if (_settings.BypassWithModifiers && NativeMethods.IsModifierDown())
        {
            ResetMotion();
            return false;
        }

        var now = NowMs;
        var target = _resolver.Resolve(point);
        if (target.IsEmpty)
        {
            return false;
        }

        var guard = _interferenceGuard.Evaluate(target, now);
        if (guard.ShouldBypass)
        {
            ResetMotion();
            PublishMetrics(false, target, $"Bypassed: {guard.Reason}");
            return false;
        }

        lock (_gate)
        {
            var profile = MatchProfile(target.ProcessName);
            if (!profile.Enabled)
            {
                ResetMotionLocked();
                return false;
            }

            var tuning = profile.Tuning;
            var dt = _lastWheelMs > 0 ? Clamp(now - _lastWheelMs, 8, 240) : 80;
            var notches = wheelDelta / (double)NativeMethods.WHEEL_DELTA;
            var direction = Math.Sign(notches);
            var sameWindow = _target.Hwnd == target.Hwnd;
            if (direction != 0 && _brakeTicksRemaining > 0 && direction == _brakeDirection && now <= _brakeUntilMs)
            {
                _target = target;
                _targetPoint = point;
                _activeProfile = profile;
                _lastWheelMs = now;
                _lastDirection = direction;
                _brakeTicksRemaining--;
                _velocity = 0;
                _accumulator = 0;
                _speedEma *= 0.2;
                PublishMetricsLocked(false, force: true);
                return true;
            }

            if (!sameWindow)
            {
                _accumulator = 0;
                _velocity = 0;
                _speedEma = 0;
                _burstCount = 0;
                _brakeTicksRemaining = 0;
            }
            else if (direction != 0 && _lastDirection != 0 && direction != _lastDirection)
            {
                var wasInertial = Math.Abs(_velocity) > 0.006 || Math.Abs(_accumulator) > 0.2;
                _velocity = 0;
                _accumulator = 0;
                _speedEma *= 0.25;
                _burstCount = 0;

                if (wasInertial)
                {
                    _target = target;
                    _targetPoint = point;
                    _activeProfile = profile;
                    _lastWheelMs = now;
                    _lastDirection = direction;
                    _brakeDirection = direction;
                    _brakeTicksRemaining = 3;
                    _brakeUntilMs = now + 180;
                    PublishMetricsLocked(false, force: true);
                    return true;
                }
            }

            if (sameWindow && dt < 135 && Math.Abs(notches) >= 0.9)
            {
                _burstCount = Math.Min(_burstCount + 1, 5);
            }
            else if (dt < 220 && Math.Abs(notches) >= 0.9)
            {
                _burstCount = Math.Min(_burstCount, 2);
            }
            else
            {
                _burstCount = 0;
            }

            _target = target;
            _targetPoint = point;
            _activeProfile = profile;
            _lastWheelMs = now;
            _lastDirection = direction == 0 ? _lastDirection : direction;

            var instantSpeed = Math.Abs(notches) / dt;
            _inputSignal = Math.Max(_inputSignal, instantSpeed);
            var alpha = 1 - Math.Exp(-dt / 95.0);
            _speedEma += (instantSpeed - _speedEma) * alpha;
            var burstPrime = _burstCount * tuning.Threshold * 0.22;
            var triggerSpeed = Math.Max(_speedEma, instantSpeed * 0.76 + burstPrime);

            _boost = Clamp(
                1 + Math.Max(0, triggerSpeed - tuning.Threshold) * tuning.Acceleration,
                1,
                tuning.MaxBoost);

            var inertiaMix = SmoothStep((triggerSpeed - tuning.Threshold) / Math.Max(0.0001, tuning.Threshold));
            if (inertiaMix <= 0.001 && Math.Abs(_velocity) < 0.004 && Math.Abs(_accumulator) < 0.01)
            {
                PublishMetricsLocked(false, force: true);
                return false;
            }

            var inertiaShare = inertiaMix * (1 - tuning.DirectShare / 100.0);
            var impulse = notches * tuning.Step * _boost;
            _accumulator += impulse * (1 - inertiaShare);
            _velocity = Clamp(_velocity + impulse * inertiaShare / tuning.KickMs, -0.16, 0.16);

            if (inertiaMix > 0.08)
            {
                _glideUntilMs = now + tuning.FrictionMs * (0.9 + tuning.Flywheel);
            }

            EmitAccumulatorLocked(tuning);
            PublishMetricsLocked(true, force: true);
            return true;
        }
    }

    public void ApplyPreset(Func<ScrollTuning> presetFactory)
    {
        var tuning = presetFactory();
        var target = DefaultTuning;
        target.Step = tuning.Step;
        target.Threshold = tuning.Threshold;
        target.Acceleration = tuning.Acceleration;
        target.MaxBoost = tuning.MaxBoost;
        target.KickMs = tuning.KickMs;
        target.FrictionMs = tuning.FrictionMs;
        target.Flywheel = tuning.Flywheel;
        target.DirectShare = tuning.DirectShare;
        target.Smoothness = tuning.Smoothness;
        ResetMotion();
    }

    public void ResetMotion()
    {
        lock (_gate)
        {
            ResetMotionLocked();
            PublishMetricsLocked(false, force: true);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private void Tick(object? state)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        lock (_gate)
        {
            var now = NowMs;
            var dt = Clamp(now - _lastFrameMs, 1, 48);
            _lastFrameMs = now;

            if (_target.IsEmpty)
            {
                PublishHoverTargetLocked(now);
                return;
            }

            var tuning = _activeProfile.Tuning;
            if (Math.Abs(_velocity) > 0.001)
            {
                _accumulator += _velocity * dt;

                var flywheelMultiplier = 1 + Clamp(tuning.Flywheel, 0, 1.5);
                var decay = tuning.FrictionMs * flywheelMultiplier;
                if (now > _glideUntilMs)
                {
                    decay = Math.Max(45, decay / 2.8);
                }

                _velocity *= Math.Exp(-dt / Math.Max(45, decay));
                if (Math.Abs(_velocity) < 0.001)
                {
                    _velocity = 0;
                }
            }

            _speedEma *= Math.Exp(-dt / 720.0);
            _boost += (1 - _boost) * (1 - Math.Exp(-dt / 220.0));
            _inputSignal *= Math.Exp(-dt / 160.0);
            if (_inputSignal < 0.0005)
            {
                _inputSignal = 0;
            }

            _outputSignal *= Math.Exp(-dt / 130.0);
            if (_outputSignal < 0.001)
            {
                _outputSignal = 0;
            }

            EmitAccumulatorLocked(tuning);
            var active = Math.Abs(_velocity) > 0.001 || Math.Abs(_accumulator) > 0.01;
            if (active)
            {
                PublishMetricsLocked(true);
            }
            else
            {
                PublishHoverTargetLocked(now);
            }
        }
    }

    private AppProfile MatchProfile(string processName)
    {
        return _settings.Profiles.FirstOrDefault(profile => profile.ProcessNames.Count > 0 && profile.Matches(processName))
            ?? _settings.DefaultProfile;
    }

    private void EmitAccumulatorLocked(ScrollTuning tuning)
    {
        if (_target.Hwnd == 0)
        {
            _accumulator = 0;
            return;
        }

        var substeps = Math.Clamp(tuning.Smoothness, 1, 8);
        var quantum = 1.0 / substeps;
        var emitted = 0;

        while (Math.Abs(_accumulator) >= quantum && emitted < 20)
        {
            var packet = Math.Sign(_accumulator) * Math.Min(Math.Abs(_accumulator), quantum);
            var delta = (int)Math.Round(packet * NativeMethods.WHEEL_DELTA);
            if (delta != 0)
            {
                EmitWheelPacket(delta);
            }

            _accumulator -= packet;
            emitted++;
        }
    }

    private void EmitWheelPacket(int delta)
    {
        TrackOutputSignal(delta);
        EmitPostMessageWheel(delta);
    }

    private void TrackOutputSignal(int delta)
    {
        _outputSignal = Math.Min(6, _outputSignal + Math.Abs(delta) / (double)NativeMethods.WHEEL_DELTA);
    }

    private void EmitPostMessageWheel(int delta)
    {
        var target = _target;
        var point = _targetPoint;

        if (NativeMethods.GetCursorPos(out var cursorPoint))
        {
            var cursorTarget = _resolver.Resolve(cursorPoint);
            if (!cursorTarget.IsEmpty && cursorTarget.RootHwnd == _target.RootHwnd)
            {
                target = cursorTarget;
                point = cursorPoint;
            }
        }

        var hwnd = ShouldPostToRootWindow(_activeProfile, point) ? target.RootHwnd : target.Hwnd;
        _lastOutputHwnd = hwnd;
        _lastOutputPoint = point;
        NativeMethods.PostMouseWheel(hwnd, delta, point);
    }

    private static bool ShouldPostToRootWindow(AppProfile profile, NativeMethods.POINT point)
    {
        return string.Equals(profile.Name, "Codex Desktop", StringComparison.OrdinalIgnoreCase)
            || !IsPrimaryMonitor(point);
    }

    private void ResetMotionLocked()
    {
        _speedEma = 0;
        _boost = 1;
        _velocity = 0;
        _accumulator = 0;
        _glideUntilMs = 0;
        _lastWheelMs = 0;
        _brakeUntilMs = 0;
        _burstCount = 0;
        _brakeTicksRemaining = 0;
        _brakeDirection = 0;
        _lastDirection = 0;
        _target = WindowTarget.Empty;
        _targetPoint = new NativeMethods.POINT(0, 0);
        _lastOutputHwnd = 0;
        _lastOutputPoint = new NativeMethods.POINT(0, 0);
        _inputSignal = 0;
        _outputSignal = 0;
        _activeProfile = _settings.DefaultProfile;
    }

    private void PublishMetricsLocked(bool isIntercepting, bool force = false)
    {
        var now = NowMs;
        if (!force && now - _lastMetricsMs < MetricsMs)
        {
            return;
        }

        _lastMetricsMs = now;
        MetricsUpdated?.Invoke(this, new ScrollMetrics(
            _speedEma,
            _boost,
            _velocity,
            _inputSignal,
            _outputSignal,
            _activeProfile.Name,
            _target.ProcessName,
            _target.Title,
            isIntercepting,
            isIntercepting ? "Intercepting" : "Native",
            BuildDiagnostics(_target, _lastOutputHwnd == 0 ? _targetPoint : _lastOutputPoint, _lastOutputHwnd)));
    }

    private void PublishMetrics(bool isIntercepting, WindowTarget target, string status)
    {
        MetricsUpdated?.Invoke(this, new ScrollMetrics(
            _speedEma,
            _boost,
            _velocity,
            _inputSignal,
            _outputSignal,
            MatchProfile(target.ProcessName).Name,
            target.ProcessName,
            target.Title,
            isIntercepting,
            status,
            BuildDiagnostics(target, _targetPoint, _lastOutputHwnd)));
    }

    private double NowMs => _clock.Elapsed.TotalMilliseconds;

    private void PublishHoverTargetLocked(double now)
    {
        if (now - _lastHoverMetricsMs < HoverMetricsMs)
        {
            return;
        }

        _lastHoverMetricsMs = now;
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        var target = _resolver.Resolve(point);
        if (target.IsEmpty)
        {
            return;
        }

        MetricsUpdated?.Invoke(this, new ScrollMetrics(
            _speedEma,
            _boost,
            _velocity,
            _inputSignal,
            _outputSignal,
            MatchProfile(target.ProcessName).Name,
            target.ProcessName,
            target.Title,
            false,
            "Hover",
            BuildDiagnostics(target, point, 0)));
    }

    private string BuildDiagnostics(WindowTarget target, NativeMethods.POINT point, nint outputHwnd)
    {
        return $"pt=({point.X},{point.Y}) mon={DescribeMonitor(point)} hwnd={FormatHwnd(target.Hwnd)} root={FormatHwnd(target.RootHwnd)} fg={FormatHwnd(NativeMethods.GetForegroundWindow())} out={FormatHwnd(outputHwnd)}";
    }

    private static string DescribeMonitor(NativeMethods.POINT point)
    {
        var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == 0)
        {
            return "unknown";
        }

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return "unknown";
        }

        return $"{info.rcMonitor.Left},{info.rcMonitor.Top},{info.rcMonitor.Width}x{info.rcMonitor.Height}";
    }

    private static bool IsPrimaryMonitor(NativeMethods.POINT point)
    {
        var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == 0)
        {
            return true;
        }

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        return NativeMethods.GetMonitorInfo(monitor, ref info)
            && (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
    }

    private static string FormatHwnd(nint hwnd)
    {
        return $"0x{hwnd.ToInt64():X}";
    }

    private static double SmoothStep(double value)
    {
        var x = Clamp(value, 0, 1);
        return x * x * (3 - 2 * x);
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Min(max, Math.Max(min, value));
    }
}
