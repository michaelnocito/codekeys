namespace CodeKeys.Core.Input;

/// <summary>
/// The subset of Windows virtual-key codes CodeKeys cares about.
/// Kept as plain ints so Core has no dependency on WinForms/Win32.
/// Values match the Win32 VK_* constants.
/// </summary>
public static class VirtualKey
{
    public const int Back = 0x08;   // Backspace
    public const int Tab = 0x09;
    public const int Enter = 0x0D;
    public const int Escape = 0x1B;
    public const int Space = 0x20;

    // Pure modifiers — always silent.
    public const int ShiftL = 0xA0;
    public const int ShiftR = 0xA1;
    public const int ControlL = 0xA2;
    public const int ControlR = 0xA3;
    public const int MenuL = 0xA4;   // Alt
    public const int MenuR = 0xA5;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int CapsLock = 0x14;

    // The generic modifier codes some hooks report instead of the L/R variants.
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;

    /// <summary>True for keys that only modify other keys; these never make a sound.</summary>
    public static bool IsPureModifier(int vk) => vk is
        ShiftL or ShiftR or ControlL or ControlR or MenuL or MenuR or
        LWin or RWin or CapsLock or Shift or Control or Menu;
}
