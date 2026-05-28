using System.Runtime.InteropServices;

namespace Electrolite;

/// <summary>
/// Win32 P/Invoke declarations for global hotkey registration and DWM window styling.
/// </summary>
internal static partial class NativeMethods
{
    // ── Global Hotkey ──────────────────────────────────────────────────

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;

    // ── DWM Rounded Corners (Windows 11) ───────────────────────────────

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    /// <summary>DWMWA_WINDOW_CORNER_PREFERENCE (attribute 33)</summary>
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    /// <summary>Round the window corners.</summary>
    public const int DWMWCP_ROUND = 2;

    // ── Drop Shadow via SetClassLong ────────────────────────────────────

    public const int CS_DROPSHADOW = 0x00020000;

    // ── Destroy HICON handle ────────────────────────────────────────────

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr hIcon);
}
