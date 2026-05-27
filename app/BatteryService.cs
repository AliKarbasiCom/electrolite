using Microsoft.Win32;
using System.Management;
using System.Runtime.InteropServices;

namespace Electrolite;

/// <summary>
/// Encapsulates all ASUS hardware interaction: WMI ACPI-based charge limit control,
/// registry fallback, and WMI battery telemetry extraction. Auto-detects the correct
/// registry layout across different ASUS hardware generations.
/// </summary>
internal sealed class BatteryService : IDisposable
{
    // ── ASUS ACPI Driver (ATKACPI) P/Invoke Declarations ────────────────
    // This is the direct native driver access method.

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        byte[] lpInBuffer,
        uint nInBufferSize,
        byte[] lpOutBuffer,
        uint nOutBufferSize,
        ref uint lpBytesReturned,
        IntPtr lpOverlapped
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint FILE_SHARE_READ = 1;
    private const uint FILE_SHARE_WRITE = 2;
    private const string AtkAcpiDeviceName = @"\\.\ATKACPI";
    private const uint AtkAcpiControlCode = 0x0022240C;
    private const uint AtkAcpiMethodSet = 0x53564544; // "DEVS"
    private const uint BatteryLimitDeviceId = 0x00120057;

    // ── ASUS ACPI WMI Constants ────────────────────────────────────────
    // The ASUS EC (Embedded Controller) is commanded via WMI ACPI calls,
    // not just registry writes. This is how MyASUS and official services work.

    private const string AsusWmiNamespace = @"root\wmi";
    private const string AsusWmiClass = "AsusAtkWmi_WMNB";
    private const string AsusWmiMethodSet = "DEVS";
    private const string AsusWmiMethodGet = "DSTS";
    private const uint BatteryChargeDeviceId = 0x00120057;

    // ── Registry Variants ──────────────────────────────────────────────

    private static readonly RegistryVariant[] Variants =
    [
        // Newer ROG models (G14/G16 2023+, Zephyrus, etc.)
        new(
            Path: @"SOFTWARE\ASUS\ASUS System Control Interface\AsusOptimization\ASUS Keyboard Hotkeys",
            ValueName: "ChargingRate",
            BalancedValue: 80,
            ElectroliteValue: 100
        ),
        // Older ASUS models (VivoBook, ZenBook, older ROG, etc.)
        new(
            Path: @"SOFTWARE\WOW6432Node\ASUS\ASUS System Control Interface\AsusOptimization\ASUS_Optimize_Setting",
            ValueName: "Battery_HealthCharging",
            BalancedValue: 80,
            ElectroliteValue: 0
        ),
    ];

    /// <summary>The detected registry variant, or null if unsupported.</summary>
    private static readonly RegistryVariant? DetectedVariant = DetectVariant();

    /// <summary>Whether the ASUS ACPI WMI interface is available.</summary>
    private static readonly bool HasAcpiWmi = DetectAcpiWmi();

    /// <summary>Whether the ASUS ATKACPI direct driver interface is available.</summary>
    private static readonly bool HasAtkAcpi = DetectAtkAcpi();

    // ── Polling ────────────────────────────────────────────────────────

    private readonly System.Threading.Timer _pollTimer;
    private readonly Action<BatteryTelemetry> _onTelemetryUpdated;
    private bool _disposed;

    /// <summary>
    /// Creates the service and begins polling WMI every 5 seconds.
    /// <paramref name="onTelemetryUpdated"/> is invoked on the thread-pool;
    /// callers must marshal to the UI thread themselves.
    /// </summary>
    public BatteryService(Action<BatteryTelemetry> onTelemetryUpdated)
    {
        _onTelemetryUpdated = onTelemetryUpdated;
        _pollTimer = new System.Threading.Timer(PollBattery, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    }

    private void SystemEvents_PowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.StatusChange)
        {
            // Immediately query battery telemetry on power status change
            System.Threading.ThreadPool.QueueUserWorkItem(_ => PollBattery(null));
        }
    }

    // ── Hardware Support Check ─────────────────────────────────────────

    /// <summary>Returns true if any known ASUS interface is available.</summary>
    public static bool IsHardwareSupported() => HasAtkAcpi || HasAcpiWmi || DetectedVariant is not null;

    private static bool DetectAtkAcpi()
    {
        IntPtr handle = CreateFile(
            AtkAcpiDeviceName,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero
        );
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
        {
            CloseHandle(handle);
            return true;
        }
        return false;
    }

    private static RegistryVariant? DetectVariant()
    {
        foreach (var variant in Variants)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(variant.Path, writable: false);
                if (key?.GetValue(variant.ValueName) is not null)
                    return variant;
            }
            catch { /* try next variant */ }
        }
        return null;
    }

    private static bool DetectAcpiWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(AsusWmiNamespace,
                $"SELECT * FROM {AsusWmiClass}");
            using var results = searcher.Get();
            return results.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    // ── Mode Read / Write ──────────────────────────────────────────────

    /// <summary>Reads the current charge limit mode from the registry.</summary>
    public static BatteryMode GetCurrentMode()
    {
        if (DetectedVariant is not { } v) return BatteryMode.Unknown;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(v.Path, writable: false);
            if (key is null) return BatteryMode.Unknown;

            var value = key.GetValue(v.ValueName);
            if (value is int intVal)
                return intVal == v.BalancedValue ? BatteryMode.Balanced : BatteryMode.Electrolite;

            return BatteryMode.Unknown;
        }
        catch
        {
            return BatteryMode.Unknown;
        }
    }

    /// <summary>
    /// Sets the battery charge mode. Sends the command to the ASUS EC via direct driver IOCTL,
    /// falls back to WMI ACPI, then updates the registry cache for consistency.
    /// </summary>
    public static bool SetMode(BatteryMode mode)
    {
        if (mode is BatteryMode.Unknown) return false;

        int chargeLimit = mode == BatteryMode.Balanced ? 80 : 100;
        bool success = false;

        // 1. Primary: Direct ATKACPI driver IOCTL
        if (HasAtkAcpi)
        {
            success = SetChargeViaAtkAcpi(chargeLimit);
        }

        // 2. Secondary: Send via ASUS ACPI WMI to the Embedded Controller
        if (!success && HasAcpiWmi)
        {
            success = SetChargeViaAcpiWmi(chargeLimit);
        }

        // 3. Tertiary: Update registry cache so the UI and ASUS services stay in sync
        if (DetectedVariant is { } v)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(v.Path, writable: true);
                if (key is not null)
                {
                    int regValue = mode == BatteryMode.Balanced ? v.BalancedValue : v.ElectroliteValue;
                    key.SetValue(v.ValueName, regValue, RegistryValueKind.DWord);
                    success = true;
                }
            }
            catch { /* registry update failed, but ACPI might have worked */ }
        }

        return success;
    }

    /// <summary>
    /// Calls the ASUS ACPI driver (ATKACPI) directly via DeviceIoControl to set the charge limit.
    /// This is the primary mechanism used to interact with the ACPI driver.
    /// </summary>
    private static bool SetChargeViaAtkAcpi(int chargeLimit)
    {
        IntPtr handle = CreateFile(
            AtkAcpiDeviceName,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero
        );

        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return false;
        }

        try
        {
            byte[] args = new byte[8];
            BitConverter.GetBytes(BatteryLimitDeviceId).CopyTo(args, 0);
            BitConverter.GetBytes((uint)chargeLimit).CopyTo(args, 4);

            byte[] acpiBuf = new byte[8 + args.Length];
            byte[] outBuffer = new byte[16];

            BitConverter.GetBytes(AtkAcpiMethodSet).CopyTo(acpiBuf, 0);
            BitConverter.GetBytes((uint)args.Length).CopyTo(acpiBuf, 4);
            Array.Copy(args, 0, acpiBuf, 8, args.Length);

            uint bytesReturned = 0;
            bool success = DeviceIoControl(
                handle,
                AtkAcpiControlCode,
                acpiBuf,
                (uint)acpiBuf.Length,
                outBuffer,
                (uint)outBuffer.Length,
                ref bytesReturned,
                IntPtr.Zero
            );

            if (success)
            {
                int result = BitConverter.ToInt32(outBuffer, 0);
                return result != -1;
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            CloseHandle(handle);
        }

        return false;
    }

    /// <summary>
    /// Calls the ASUS ACPI WMI DEVS method to push the charge limit to the EC.
    /// This is the same mechanism used by MyASUS and official tools.
    /// </summary>
    private static bool SetChargeViaAcpiWmi(int chargeLimit)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(AsusWmiNamespace,
                $"SELECT * FROM {AsusWmiClass}");
            using var results = searcher.Get();

            foreach (ManagementObject obj in results)
            {
                // DEVS takes two uint32 params: Device_ID and Control_status (or Control_Status)
                var inParams = obj.GetMethodParameters(AsusWmiMethodSet);
                
                if (inParams.Properties["Device_ID"] is not null)
                    inParams["Device_ID"] = BatteryChargeDeviceId;
                else if (inParams.Properties["device_id"] is not null)
                    inParams["device_id"] = BatteryChargeDeviceId;
                else
                    inParams["Device_ID"] = BatteryChargeDeviceId;

                if (inParams.Properties["Control_status"] is not null)
                    inParams["Control_status"] = (uint)chargeLimit;
                else if (inParams.Properties["Control_Status"] is not null)
                    inParams["Control_Status"] = (uint)chargeLimit;
                else
                    inParams["Control_status"] = (uint)chargeLimit;

                var outParams = obj.InvokeMethod(AsusWmiMethodSet, inParams, null);
                obj.Dispose();

                if (outParams is null) return false;

                // ReturnValue is standard for WMI method output parameters
                var retValObj = outParams["ReturnValue"];
                if (retValObj is null) return false;

                long retVal = Convert.ToInt64(retValObj);
                // 0xFFFFFFFF typically means unsupported device or error in ASUS WMI
                return retVal != -1 && retVal != 0xFFFFFFFF;
            }
        }
        catch
        {
            // ACPI WMI call failed
        }

        return false;
    }

    /// <summary>Toggles between Balanced and Electrolite modes.</summary>
    public static BatteryMode ToggleMode()
    {
        var current = GetCurrentMode();
        var next = current == BatteryMode.Balanced ? BatteryMode.Electrolite : BatteryMode.Balanced;
        SetMode(next);
        return next;
    }

    // ── WMI Polling ────────────────────────────────────────────────────

    private void PollBattery(object? state)
    {
        if (_disposed) return;

        var telemetry = QueryBatteryTelemetry();
        try
        {
            _onTelemetryUpdated(telemetry);
        }
        catch
        {
            // Swallow exceptions from the callback (e.g., form disposed during shutdown)
        }
    }

    /// <summary>Queries battery status using SystemInformation.PowerStatus and Win32_Battery.</summary>
    public static BatteryTelemetry QueryBatteryTelemetry()
    {
        try
        {
            var powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;

            // If there's no battery, return unavailable
            if (powerStatus.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery))
            {
                return BatteryTelemetry.Unavailable;
            }

            int chargePercent = (int)Math.Round(powerStatus.BatteryLifePercent * 100);
            if (chargePercent < 0 || chargePercent > 100) chargePercent = 0;

            bool isOnAc = powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
            bool isCharging = (powerStatus.BatteryChargeStatus & System.Windows.Forms.BatteryChargeStatus.Charging) != 0;

            // Default logic using SystemInformation.PowerStatus
            bool isFullyCharged = !isCharging && chargePercent >= 98;
            bool isHoldingAtLimit = false;

            if (isOnAc && !isCharging)
            {
                var mode = GetCurrentMode();
                if (mode == BatteryMode.Balanced && chargePercent >= 78 && chargePercent <= 83)
                {
                    isHoldingAtLimit = true;
                }
                else if (chargePercent >= 95)
                {
                    isFullyCharged = true;
                }
            }

            int timeToFull = 0;

            // Query WMI for supplementary telemetry if available
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT BatteryStatus, TimeToFullCharge FROM Win32_Battery");
                using var results = searcher.Get();
                foreach (ManagementObject battery in results)
                {
                    var ttfValue = battery["TimeToFullCharge"];
                    if (ttfValue is not null)
                    {
                        try { timeToFull = Convert.ToInt32(ttfValue); }
                        catch { }
                    }

                    int batteryStatus = Convert.ToInt32(battery["BatteryStatus"] ?? 0);
                    if (batteryStatus == 2) // AC connected, not charging (holding at limit)
                    {
                        isHoldingAtLimit = true;
                        isFullyCharged = false;
                    }
                    else if (batteryStatus == 3) // Fully charged
                    {
                        isFullyCharged = true;
                        isHoldingAtLimit = false;
                    }

                    battery.Dispose();
                    break;
                }
            }
            catch
            {
                // WMI fallback failed, keep PowerStatus estimation
            }

            return new BatteryTelemetry(
                ChargePercent: chargePercent,
                IsOnAcPower: isOnAc,
                IsCharging: isCharging,
                IsFullyCharged: isFullyCharged,
                IsHoldingAtLimit: isHoldingAtLimit,
                TimeToFullChargeMinutes: timeToFull,
                IsAvailable: true
            );
        }
        catch
        {
            return BatteryTelemetry.Unavailable;
        }
    }

    // ── Cleanup ────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        _pollTimer.Dispose();
    }
}

// ── Data Models ────────────────────────────────────────────────────────

internal enum BatteryMode
{
    Unknown,
    Balanced,
    Electrolite
}

internal readonly record struct BatteryTelemetry(
    int ChargePercent,
    bool IsOnAcPower,
    bool IsCharging,
    bool IsFullyCharged,
    bool IsHoldingAtLimit,
    int TimeToFullChargeMinutes,
    bool IsAvailable)
{
    public static readonly BatteryTelemetry Unavailable = new(0, false, false, false, false, 0, false);
}

/// <summary>
/// Describes a specific ASUS registry layout for battery charge control.
/// </summary>
internal sealed record RegistryVariant(
    string Path,
    string ValueName,
    int BalancedValue,
    int ElectroliteValue);
