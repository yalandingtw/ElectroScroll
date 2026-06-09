using System.Runtime.InteropServices;
using ElectroScroll.Interop;

namespace ElectroScroll.Services;

public sealed class LowLevelMouseHook : IDisposable
{
    private readonly ScrollController _controller;
    private readonly NativeMethods.LowLevelMouseProc _callback;
    private nint _hook;

    public LowLevelMouseHook(ScrollController controller)
    {
        _controller = controller;
        _callback = HookCallback;
    }

    public void Start()
    {
        if (_hook != 0)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _callback,
            NativeMethods.GetCurrentModuleHandle(),
            0);

        if (_hook == 0)
        {
            throw new InvalidOperationException("Unable to install the low-level mouse hook.");
        }
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == NativeMethods.WM_MOUSEWHEEL)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            if (NativeMethods.IsInjectedMouseEvent(data))
            {
                return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            var delta = unchecked((short)((data.MouseData >> 16) & 0xffff));

            if (_controller.OnWheel(delta, data.Pt))
            {
                return 1;
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
