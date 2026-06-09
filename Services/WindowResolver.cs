using System.Diagnostics;
using ElectroScroll.Interop;

namespace ElectroScroll.Services;

internal sealed class WindowResolver
{
    private readonly Dictionary<nint, WindowTarget> _cache = [];

    public WindowTarget Resolve(NativeMethods.POINT point)
    {
        var hwnd = NativeMethods.WindowFromPoint(point);
        if (hwnd == 0)
        {
            return WindowTarget.Empty;
        }

        var target = ResolveWindow(hwnd);
        if (target.IsDesktopShell)
        {
            var foreground = ResolveWindow(NativeMethods.GetForegroundWindow());
            if (!foreground.IsEmpty)
            {
                return foreground with { Hwnd = foreground.RootHwnd };
            }
        }

        return target with { Hwnd = hwnd };
    }

    private WindowTarget ResolveWindow(nint hwnd)
    {
        if (hwnd == 0)
        {
            return WindowTarget.Empty;
        }

        var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        var rootHwnd = root == 0 ? hwnd : root;
        if (_cache.TryGetValue(rootHwnd, out var cached))
        {
            return cached with { Hwnd = hwnd };
        }

        var title = GetTitle(rootHwnd);
        var processName = GetProcessName(rootHwnd);
        var target = new WindowTarget(hwnd, rootHwnd, processName, title);

        _cache[rootHwnd] = target;
        if (_cache.Count > 256)
        {
            _cache.Clear();
        }

        return target;
    }

    private static string GetTitle(nint hwnd)
    {
        var buffer = new char[256];
        var length = NativeMethods.GetWindowText(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : "(untitled)";
    }

    private static string GetProcessName(nint hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }
}

internal readonly record struct WindowTarget(nint Hwnd, nint RootHwnd, string ProcessName, string Title)
{
    public static WindowTarget Empty { get; } = new(0, 0, "unknown", "(none)");
    public bool IsEmpty => Hwnd == 0;
    public bool IsDesktopShell =>
        string.Equals(ProcessName, "explorer", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(Title, "Program Manager", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Title, "Desktop", StringComparison.OrdinalIgnoreCase));
}
