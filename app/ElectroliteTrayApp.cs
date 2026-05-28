using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Electrolite;

/// <summary>
/// Headless application context that manages the system tray icon, global hotkey,
/// battery service, and flyout dashboard lifecycle.
/// </summary>
internal sealed class ElectroliteTrayApp : ApplicationContext
{
    // ── Constants ──────────────────────────────────────────────────────

    private const int HotkeyId = 9001;
    private const uint VK_B = 0x42;

    // ── Components ─────────────────────────────────────────────────────

    private readonly NotifyIcon _trayIcon;
    private readonly FlyoutForm _flyout;
    private readonly BatteryService _batteryService;
    private readonly HotkeyWindow _hotkeyWindow;
    private BatteryMode _currentMode = BatteryMode.Unknown;
    private readonly bool _hardwareSupported;

    // Cached Icons and HICON handles to prevent GDI/User resource leaks
    private readonly Icon _balancedIcon;
    private readonly Icon _electroliteIcon;
    private readonly IntPtr _balancedHicon;
    private readonly IntPtr _electroliteHicon;

    // ── Constructor ────────────────────────────────────────────────────

    public ElectroliteTrayApp()
    {
        // Check hardware support
        _hardwareSupported = BatteryService.IsHardwareSupported();
        _currentMode = _hardwareSupported ? BatteryService.GetCurrentMode() : BatteryMode.Unknown;

        // Cache tray icons
        _balancedIcon = CreateTrayIcon(BatteryMode.Balanced, out _balancedHicon);
        _electroliteIcon = CreateTrayIcon(BatteryMode.Electrolite, out _electroliteHicon);

        // ── Flyout ─────────────────────────────────────────────────────
        _flyout = new FlyoutForm();
        _flyout.SetHardwareSupported(_hardwareSupported);
        _flyout.SetCurrentMode(_currentMode);
        _flyout.ModeChangeRequested += OnModeChangeRequested;
        _flyout.QuitRequested += ExitApplication;

        // ── Tray Icon ──────────────────────────────────────────────────
        _trayIcon = new NotifyIcon
        {
            Icon = GetTrayIcon(_currentMode),
            Text = GetTooltipText(),
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };
        _trayIcon.MouseClick += TrayIcon_MouseClick;

        // ── Global Hotkey (Ctrl+Shift+B) ───────────────────────────────
        _hotkeyWindow = new HotkeyWindow(OnHotkeyPressed);
        NativeMethods.RegisterHotKey(
            _hotkeyWindow.Handle,
            HotkeyId,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            VK_B);

        // ── Battery Service ────────────────────────────────────────────
        _batteryService = new BatteryService(OnTelemetryUpdated);
    }

    // ── Tray Icon Click ────────────────────────────────────────────────

    private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _flyout.ShowFlyout();
        }
    }

    // ── Context Menu ───────────────────────────────────────────────────

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.FromArgb(220, 220, 220),
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9f)
        };

        menu.Renderer = new DarkMenuRenderer();

        var toggleItem = new ToolStripMenuItem("Toggle Mode (Ctrl+Shift+B)");
        toggleItem.Click += (_, _) => ToggleMode();

        var balancedItem = new ToolStripMenuItem("Balanced Mode (80%)");
        balancedItem.Click += (_, _) => OnModeChangeRequested(BatteryMode.Balanced);

        var electroliteItem = new ToolStripMenuItem("Electrolite Mode (100%)");
        electroliteItem.Click += (_, _) => OnModeChangeRequested(BatteryMode.Electrolite);

        var separator = new ToolStripSeparator();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.AddRange([toggleItem, new ToolStripSeparator(), balancedItem, electroliteItem, separator, exitItem]);

        if (!_hardwareSupported)
        {
            toggleItem.Enabled = false;
            balancedItem.Enabled = false;
            electroliteItem.Enabled = false;
        }

        return menu;
    }

    // ── Mode Change ────────────────────────────────────────────────────

    private void OnModeChangeRequested(BatteryMode mode)
    {
        if (!_hardwareSupported) return;

        if (BatteryService.SetMode(mode))
        {
            _currentMode = mode;
            _flyout.SetCurrentMode(mode);
            _trayIcon.Icon = GetTrayIcon(mode);
            _trayIcon.Text = GetTooltipText();

            // Brief balloon notification (only when the GUI is not open)
            if (!_flyout.Visible)
            {
                _trayIcon.BalloonTipTitle = "Electrolite";
                _trayIcon.BalloonTipText = mode == BatteryMode.Balanced
                    ? "Balanced Mode — Charge limit set to 80%"
                    : "Electrolite Mode — Charging to 100%";
                _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                _trayIcon.ShowBalloonTip(2000);
            }
        }
    }

    private void ToggleMode()
    {
        if (!_hardwareSupported) return;

        var newMode = _currentMode == BatteryMode.Balanced
            ? BatteryMode.Electrolite
            : BatteryMode.Balanced;

        OnModeChangeRequested(newMode);
    }

    // ── Global Hotkey Handler ──────────────────────────────────────────

    private void OnHotkeyPressed()
    {
        ToggleMode();
    }

    // ── Telemetry Callback (from background thread) ────────────────────

    private void OnTelemetryUpdated(BatteryTelemetry telemetry)
    {
        _flyout.UpdateTelemetry(telemetry);

        // Also refresh mode from registry in case external tools changed it
        if (_hardwareSupported)
        {
            var liveMode = BatteryService.GetCurrentMode();
            if (liveMode != _currentMode)
            {
                _currentMode = liveMode;
                _flyout.SetCurrentMode(liveMode);

                // Update tray icon on UI thread
                if (_flyout.InvokeRequired)
                    _flyout.Invoke(() => _trayIcon.Icon = GetTrayIcon(liveMode));
                else
                    _trayIcon.Icon = GetTrayIcon(liveMode);
            }
        }
    }

    private Icon GetTrayIcon(BatteryMode mode)
    {
        return mode == BatteryMode.Electrolite ? _electroliteIcon : _balancedIcon;
    }

    // ── Tray Icon Rendering ────────────────────────────────────────────

    private static Icon CreateTrayIcon(BatteryMode mode, out IntPtr hIcon)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        // Battery body
        var bodyRect = new Rectangle(4, 8, 22, 16);
        var tipRect = new Rectangle(26, 12, 3, 8);

        if (mode == BatteryMode.Electrolite)
        {
            // Vibrant teal battery
            using var bodyBrush = new LinearGradientBrush(bodyRect,
                Color.FromArgb(0, 200, 230), Color.FromArgb(0, 140, 180), 0f);
            g.FillRoundedRectangle(bodyBrush, bodyRect, 3);

            using var tipBrush = new SolidBrush(Color.FromArgb(0, 180, 216));
            g.FillRectangle(tipBrush, tipRect);

            // Lightning bolt
            var bolt = new PointF[]
            {
                new(17, 8), new(12, 17), new(16, 17),
                new(13, 24), new(20, 15), new(16, 15),
                new(17, 8)
            };
            using var boltBrush = new SolidBrush(Color.FromArgb(255, 240, 60));
            g.FillPolygon(boltBrush, bolt);
        }
        else
        {
            // Neutral gray battery silhouette
            using var bodyPen = new Pen(Color.FromArgb(200, 200, 200), 1.5f);
            g.DrawRoundedRectangle(bodyPen, bodyRect, 3);

            using var tipBrush = new SolidBrush(Color.FromArgb(200, 200, 200));
            g.FillRectangle(tipBrush, tipRect);

            // Partial fill to indicate 80%
            var fillRect = new Rectangle(bodyRect.X + 2, bodyRect.Y + 2,
                (int)((bodyRect.Width - 4) * 0.8), bodyRect.Height - 4);
            using var fillBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
            g.FillRectangle(fillBrush, fillRect);
        }

        hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private string GetTooltipText()
    {
        return _currentMode switch
        {
            BatteryMode.Balanced => "Electrolite — Balanced Mode (80%)",
            BatteryMode.Electrolite => "Electrolite — Full Charge Mode (100%)",
            _ => "Electrolite — Hardware Not Supported"
        };
    }

    // ── Exit ───────────────────────────────────────────────────────────

    private void ExitApplication()
    {
        _batteryService.Dispose();
        NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, HotkeyId);
        _hotkeyWindow.DestroyHandle();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _flyout.Dispose();

        // Release cached icons and their unmanaged HICON handles
        _balancedIcon.Dispose();
        _electroliteIcon.Dispose();
        if (_balancedHicon != IntPtr.Zero) NativeMethods.DestroyIcon(_balancedHicon);
        if (_electroliteHicon != IntPtr.Zero) NativeMethods.DestroyIcon(_electroliteHicon);

        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _batteryService.Dispose();
            NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, HotkeyId);
            _hotkeyWindow.DestroyHandle();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _flyout.Dispose();

            // Release cached icons and their unmanaged HICON handles
            _balancedIcon.Dispose();
            _electroliteIcon.Dispose();
            if (_balancedHicon != IntPtr.Zero) NativeMethods.DestroyIcon(_balancedHicon);
            if (_electroliteHicon != IntPtr.Zero) NativeMethods.DestroyIcon(_electroliteHicon);
        }
        base.Dispose(disposing);
    }
}

// ── Hidden Message Window for WM_HOTKEY ────────────────────────────────

internal sealed class HotkeyWindow : NativeWindow
{
    private readonly Action _onHotkey;

    public HotkeyWindow(Action onHotkey)
    {
        _onHotkey = onHotkey;
        CreateHandle(new CreateParams
        {
            Caption = "ElectroliteHotkeyReceiver",
            Style = 0 // invisible message-only window
        });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            _onHotkey();
        }
        base.WndProc(ref m);
    }
}

// ── Dark Context Menu Renderer ─────────────────────────────────────────

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(Point.Empty, e.Item.Size);
        var color = e.Item.Selected
            ? Color.FromArgb(60, 60, 60)
            : Color.FromArgb(40, 40, 40);
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, rc);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(Color.FromArgb(65, 65, 65));
        e.Graphics.DrawLine(pen, 0, y, e.Item.Width, y);
    }
}

internal sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(55, 55, 55);
    public override Color MenuItemBorder => Color.Transparent;
    public override Color ToolStripDropDownBackground => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientBegin => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(40, 40, 40);
    public override Color ImageMarginGradientEnd => Color.FromArgb(40, 40, 40);
    public override Color MenuStripGradientBegin => Color.FromArgb(40, 40, 40);
    public override Color MenuStripGradientEnd => Color.FromArgb(40, 40, 40);
}

// ── GDI+ Extension: Rounded Rectangle ──────────────────────────────────

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = CreateRoundedRectPath(rect, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
    {
        using var path = CreateRoundedRectPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        return path;
    }
}
