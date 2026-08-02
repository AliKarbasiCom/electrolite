using Microsoft.Win32;

namespace Electrolite;

internal static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppKeyPath = @"Software\Electrolite";
    private const string AppName = "Electrolite";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(AppName) != null;
    }

    public static void Enable()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.SetValue(AppName, $"\"{exePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public static void EnsureFirstRunSetup()
    {
        using var appKey = Registry.CurrentUser.CreateSubKey(AppKeyPath);
        if (appKey.GetValue("SetupDone") == null)
        {
            Enable();
            appKey.SetValue("SetupDone", 1, RegistryValueKind.DWord);
        }
    }
}
