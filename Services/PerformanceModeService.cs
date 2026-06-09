using System.Runtime;
using System.Runtime.InteropServices;
using ElectroScroll.Interop;
using ElectroScroll.Models;

namespace ElectroScroll.Services;

public sealed class PerformanceModeService : IDisposable
{
    private readonly AppSettings _settings;
    private bool _timerResolutionSet;
    private bool _powerThrottlingChanged;
    private bool _gcChanged;
    private GCLatencyMode _previousLatencyMode;

    public PerformanceModeService(AppSettings settings)
    {
        _settings = settings;
    }

    public PerformanceModeStatus Status { get; private set; } = new(false, false, false);

    public void Apply()
    {
        if (!_settings.EnablePerformanceMode)
        {
            Status = new PerformanceModeStatus(false, false, false);
            return;
        }

        _timerResolutionSet = NativeMethods.timeBeginPeriod(1) == 0;
        _powerThrottlingChanged = TryDisableEcoQoS();
        _previousLatencyMode = GCSettings.LatencyMode;
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        _gcChanged = true;

        Status = new PerformanceModeStatus(true, _timerResolutionSet, _powerThrottlingChanged);
    }

    public void Dispose()
    {
        if (_timerResolutionSet)
        {
            NativeMethods.timeEndPeriod(1);
        }

        if (_gcChanged)
        {
            GCSettings.LatencyMode = _previousLatencyMode;
        }
    }

    private static bool TryDisableEcoQoS()
    {
        var state = new NativeMethods.PROCESS_POWER_THROTTLING_STATE
        {
            Version = NativeMethods.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = NativeMethods.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = 0
        };

        try
        {
            return NativeMethods.SetProcessInformation(
                NativeMethods.GetCurrentProcess(),
                NativeMethods.ProcessPowerThrottling,
                ref state,
                Marshal.SizeOf<NativeMethods.PROCESS_POWER_THROTTLING_STATE>());
        }
        catch
        {
            return false;
        }
    }
}

public readonly record struct PerformanceModeStatus(bool Enabled, bool TimerResolutionSet, bool EcoQosDisabled);
