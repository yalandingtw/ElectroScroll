using ElectroScroll.Interop;
using ElectroScroll.Models;

namespace ElectroScroll.Services;

internal sealed class InterferenceGuard
{
    private const double CacheMs = 500;
    private readonly AppSettings _settings;
    private readonly Dictionary<nint, GuardDecision> _cache = [];

    public InterferenceGuard(AppSettings settings)
    {
        _settings = settings;
    }

    public GuardDecision Evaluate(WindowTarget target, double nowMs)
    {
        if (target.IsEmpty)
        {
            return GuardDecision.Allow;
        }

        if (target.IsDesktopShell)
        {
            return GuardDecision.AllowAt(nowMs);
        }

        if (_cache.TryGetValue(target.RootHwnd, out var cached) && nowMs - cached.CheckedAtMs < CacheMs)
        {
            return cached;
        }

        var decision = EvaluateUncached(target, nowMs);
        _cache[target.RootHwnd] = decision;

        if (_cache.Count > 128)
        {
            _cache.Clear();
        }

        return decision;
    }

    private GuardDecision EvaluateUncached(WindowTarget target, double nowMs)
    {
        if (_settings.AutoBypassKnownGames && IsKnownGame(target.ProcessName))
        {
            return GuardDecision.Bypass("game process", nowMs);
        }

        if (_settings.AutoBypassFullscreen && IsFullscreen(target.RootHwnd))
        {
            return GuardDecision.Bypass("fullscreen", nowMs);
        }

        return GuardDecision.AllowAt(nowMs);
    }

    private bool IsKnownGame(string processName)
    {
        return _settings.GameProcessNames.Any(name =>
            string.Equals(name, processName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFullscreen(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        if (IsOrdinaryMaximizedWindow(hwnd))
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == 0)
        {
            return false;
        }

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        const int tolerance = 2;
        return rect.Left <= info.rcMonitor.Left + tolerance
            && rect.Top <= info.rcMonitor.Top + tolerance
            && rect.Right >= info.rcMonitor.Right - tolerance
            && rect.Bottom >= info.rcMonitor.Bottom - tolerance;
    }

    private static bool IsOrdinaryMaximizedWindow(nint hwnd)
    {
        if (NativeMethods.IsZoomed(hwnd))
        {
            return true;
        }

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_STYLE);
        var hasNormalFrame = (style & NativeMethods.WS_CAPTION) != 0
            || (style & NativeMethods.WS_THICKFRAME) != 0;

        return hasNormalFrame;
    }
}

public readonly record struct GuardDecision(bool ShouldBypass, string Reason, double CheckedAtMs)
{
    public static GuardDecision Allow { get; } = new(false, "native", 0);

    public static GuardDecision AllowAt(double checkedAtMs)
    {
        return new GuardDecision(false, "native", checkedAtMs);
    }

    public static GuardDecision Bypass(string reason, double checkedAtMs)
    {
        return new GuardDecision(true, reason, checkedAtMs);
    }
}
