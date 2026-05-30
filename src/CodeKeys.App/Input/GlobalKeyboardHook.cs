using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CodeKeys.App.Input;

/// <summary>
/// A low-level system-wide keyboard hook (WH_KEYBOARD_LL). Raises <see cref="KeyDown"/>
/// and <see cref="KeyUp"/> for every key press anywhere in Windows.
///
/// PRIVACY: this is the same mechanism a keylogger uses. CodeKeys inspects the
/// virtual-key code only to pick a sound — it never stores or transmits anything.
///
/// PERFORMANCE: the hook callback runs on the thread that installs it (the UI thread,
/// which has a message loop). It does the bare minimum — read the vk code, raise an
/// event, and immediately call the next hook — so it never delays the user's keystroke.
/// We never swallow keys (always pass them through).
/// </summary>
public sealed class GlobalKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    /// <summary>Fired on key-down with the virtual-key code. May fire repeatedly while held.</summary>
    public event Action<int>? KeyDown;
    /// <summary>Fired on key-up with the virtual-key code.</summary>
    public event Action<int>? KeyUp;

    // Keep the delegate alive for the life of the hook so the GC can't collect it.
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookHandle = IntPtr.Zero;

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
    }

    public bool IsInstalled => _hookHandle != IntPtr.Zero;

    public void Install()
    {
        if (IsInstalled) return;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(module.ModuleName), 0);
        if (_hookHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Failed to install keyboard hook (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = (int)wParam;
            int vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode is the first field

            switch (message)
            {
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                    KeyDown?.Invoke(vk);
                    break;
                case WM_KEYUP:
                case WM_SYSKEYUP:
                    KeyUp?.Invoke(vk);
                    break;
            }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();

    // ---- Win32 ----

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
