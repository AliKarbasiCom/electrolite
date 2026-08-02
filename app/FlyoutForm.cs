using System.Drawing.Drawing2D;

namespace Electrolite;

/// <summary>
/// Sleek dark-themed borderless flyout dashboard that renders above the taskbar.
/// Displays battery telemetry, mode toggle buttons, and a predictive charging banner.
/// </summary>
internal sealed class FlyoutForm : Form
{
    // ── Color Palette ──────────────────────────────────────────────────

    private static readonly Color BackgroundColor = Color.FromArgb(30, 30, 30);
    private static readonly Color SurfaceColor = Color.FromArgb(42, 42, 42);
    private static readonly Color SurfaceHoverColor = Color.FromArgb(55, 55, 55);
    private static readonly Color AccentColor = Color.FromArgb(0, 180, 216);       // Teal
    private static readonly Color AccentHoverColor = Color.FromArgb(0, 150, 190);
    private static readonly Color TextPrimary = Color.FromArgb(240, 240, 240);
    private static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);
    private static readonly Color TextMuted = Color.FromArgb(120, 120, 120);
    private static readonly Color DividerColor = Color.FromArgb(55, 55, 55);
    private static readonly Color ProgressTrackColor = Color.FromArgb(50, 50, 50);

    // ── Layout Constants ───────────────────────────────────────────────

    private const int FlyoutWidth = 360;
    private const int FlyoutBaseHeight = 310;
    private const int FlyoutExpandedHeight = 360;
    private const int ContentPadding = 20;
    private const int CornerRadius = 12;

    // ── Controls ───────────────────────────────────────────────────────

    private readonly Label _titleLabel;
    private readonly Label _percentageLabel;
    private readonly Label _statusLabel;
    private readonly Panel _progressBarTrack;
    private readonly Panel _progressBarFill;
    private readonly Label _progressPercentText;
    private readonly Button _balancedButton;
    private readonly Button _electroliteButton;
    private readonly Label _bannerLabel;
    private readonly Panel _aboutPanel;
    private readonly Button _infoButton;
    private readonly Button _aboutUsButton;
    private readonly LinkLabel _versionLink;
    private readonly Button _starButton;
    private readonly Label _aboutText;
    private readonly Button _githubButton;
    private readonly Button _quitButton;
    private readonly Panel _settingsPanel;
    private readonly ToggleSwitch _autoStartToggle;

    // ── State ──────────────────────────────────────────────────────────

    private BatteryMode _currentMode = BatteryMode.Unknown;
    private bool _aboutVisible;
    private bool _hardwareSupported = true;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private readonly System.Windows.Forms.Timer _animTimer;
    private int _targetHeight;

    // ── Constructor ────────────────────────────────────────────────────

    public FlyoutForm()
    {
        // Window properties
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = BackgroundColor;
        Size = new Size(FlyoutWidth, FlyoutBaseHeight);
        DoubleBuffered = true;
        Opacity = 0;
        Visible = false;

        // Fade-in timer
        _fadeTimer = new System.Windows.Forms.Timer { Interval = 12 };
        _fadeTimer.Tick += FadeTimer_Tick;

        // Height animation timer
        _animTimer = new System.Windows.Forms.Timer { Interval = 12 };
        _animTimer.Tick += AnimTimer_Tick;
        _targetHeight = FlyoutBaseHeight;

        // ── Header Zone ────────────────────────────────────────────────

        _titleLabel = new Label
        {
            Text = "Electrolite",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(ContentPadding, ContentPadding),
            BackColor = Color.Transparent
        };

        _infoButton = new Button
        {
            Text = "ℹ",
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            ForeColor = TextSecondary,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(30, 30),
            Location = new Point(FlyoutWidth - ContentPadding - 30, ContentPadding),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _infoButton.FlatAppearance.BorderSize = 0;
        _infoButton.FlatAppearance.MouseOverBackColor = SurfaceColor;
        _infoButton.Click += InfoButton_Click;

        // ── About Panel (hidden by default) ─────────────────────────────

        _aboutPanel = new Panel
        {
            Size = new Size(FlyoutWidth - ContentPadding * 2, 214),
            Location = new Point(ContentPadding, 55),
            BackColor = SurfaceColor,
            Visible = false
        };

        _aboutText = new Label
        {
            Text = "A simple, bloat-free utility to charge your ASUS laptop to 100% before you leave. If you find it helpful, please star the project on GitHub to support it!",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextSecondary,
            AutoSize = false,
            Size = new Size(300, 52),
            Location = new Point(10, 8),
            BackColor = Color.Transparent
        };

        _versionLink = new LinkLabel
        {
            Text = "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            LinkColor = AccentColor,
            ActiveLinkColor = AccentHoverColor,
            AutoSize = true,
            Location = new Point(10, 64),
            BackColor = Color.Transparent
        };
        _versionLink.LinkClicked += (_, _) => OpenUrl("https://github.com/AliKarbasiCom/electrolite/releases");

        _aboutUsButton = new Button
        {
            Text = "About us",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            BackColor = SurfaceHoverColor,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 88),
            Size = new Size(300, 26),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            TabStop = false
        };
        _aboutUsButton.FlatAppearance.BorderSize = 0;
        _aboutUsButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 70);
        _aboutUsButton.Click += (_, _) => OpenUrl("https://alikarbasi.com");

        _starButton = new Button
        {
            Text = "⭐ Star on GitHub",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = AccentColor,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 118),
            Size = new Size(300, 26),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            TabStop = false
        };
        _starButton.FlatAppearance.BorderSize = 0;
        _starButton.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        _starButton.Click += (_, _) => OpenUrl("https://github.com/AliKarbasiCom/electrolite");

        // ── Settings Section (inside About) ────────────────────────────

        _settingsPanel = new Panel
        {
            Size = new Size(300, 56),
            Location = new Point(10, 152),
            BackColor = Color.FromArgb(50, 50, 50)
        };

        var settingsHeader = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(8, 6),
            BackColor = Color.Transparent
        };

        var autoStartLabel = new Label
        {
            Text = "Start with Windows",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(8, 32),
            BackColor = Color.Transparent
        };

        _autoStartToggle = new ToggleSwitch
        {
            Location = new Point(300 - 40 - 8, 30)
        };
        _autoStartToggle.SetCheckedSilent(AutoStartManager.IsEnabled());
        _autoStartToggle.CheckedChanged += (_, _) =>
        {
            if (_autoStartToggle.Checked)
                AutoStartManager.Enable();
            else
                AutoStartManager.Disable();
        };

        _settingsPanel.Controls.AddRange([settingsHeader, autoStartLabel, _autoStartToggle]);

        _aboutPanel.Controls.AddRange([_aboutText, _versionLink, _aboutUsButton, _starButton, _settingsPanel]);

        // ── Telemetry Zone ─────────────────────────────────────────────

        int telemetryTop = 60;

        _percentageLabel = new Label
        {
            Text = "—%",
            Font = new Font("Segoe UI", 28f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(ContentPadding, telemetryTop),
            BackColor = Color.Transparent
        };

        _statusLabel = new Label
        {
            Text = "Detecting battery status…",
            Font = new Font("Segoe UI", 9f),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(ContentPadding, telemetryTop + 48),
            BackColor = Color.Transparent
        };

        // Progress bar track
        _progressBarTrack = new Panel
        {
            Size = new Size(FlyoutWidth - ContentPadding * 2, 6),
            Location = new Point(ContentPadding, telemetryTop + 75),
            BackColor = ProgressTrackColor
        };
        MakeRoundedPanel(_progressBarTrack, 3);

        _progressBarFill = new Panel
        {
            Size = new Size(0, 6),
            Location = new Point(0, 0),
            BackColor = AccentColor
        };
        _progressBarTrack.Controls.Add(_progressBarFill);

        _progressPercentText = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(FlyoutWidth - ContentPadding - 30, telemetryTop + 84),
            BackColor = Color.Transparent
        };

        // ── Mode Buttons ───────────────────────────────────────────────

        int buttonTop = telemetryTop + 100;
        int buttonWidth = (FlyoutWidth - ContentPadding * 2 - 10) / 2;
        int buttonHeight = 42;

        _balancedButton = CreateModeButton("⚖  Balanced", new Point(ContentPadding, buttonTop), new Size(buttonWidth, buttonHeight));
        _balancedButton.Click += (_, _) => OnModeButtonClick(BatteryMode.Balanced);

        _electroliteButton = CreateModeButton("⚡ Electrolite", new Point(ContentPadding + buttonWidth + 10, buttonTop), new Size(buttonWidth, buttonHeight));
        _electroliteButton.Click += (_, _) => OnModeButtonClick(BatteryMode.Electrolite);

        // ── Predictive Banner ──────────────────────────────────────────

        _bannerLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9f),
            ForeColor = AccentColor,
            AutoSize = false,
            Size = new Size(FlyoutWidth - ContentPadding * 2, 30),
            Location = new Point(ContentPadding, buttonTop + buttonHeight + 16),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        // Bottom buttons (GitHub and Quit)
        int bottomButtonTop = FlyoutBaseHeight - ContentPadding - 36;
        _githubButton = CreateModeButton("🔗  GitHub", new Point(ContentPadding, bottomButtonTop), new Size(buttonWidth, 36));
        _githubButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _githubButton.Click += (_, _) => OpenUrl("https://github.com/AliKarbasiCom/electrolite");

        _quitButton = CreateModeButton("✕  Quit", new Point(ContentPadding + buttonWidth + 10, bottomButtonTop), new Size(buttonWidth, 36));
        _quitButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _quitButton.Click += (_, _) => QuitRequested?.Invoke();

        // ── Add Controls ───────────────────────────────────────────────

        Controls.AddRange([
            _titleLabel, _infoButton, _aboutPanel,
            _percentageLabel, _statusLabel,
            _progressBarTrack, _progressPercentText,
            _balancedButton, _electroliteButton,
            _bannerLabel, _githubButton, _quitButton
        ]);

        // ── Dismiss on focus loss ──────────────────────────────────────

        Deactivate += (_, _) => HideFlyout();
    }

    // ── Drop Shadow via CreateParams ───────────────────────────────────

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= NativeMethods.CS_DROPSHADOW;
            return cp;
        }
    }

    // Prevent the form from appearing in Alt+Tab
    protected override void SetVisibleCore(bool value)
    {
        base.SetVisibleCore(value);
    }

    // ── Paint: Rounded Background ──────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = CreateRoundedRectPath(ClientRectangle, CornerRadius);
        using var brush = new SolidBrush(BackgroundColor);
        e.Graphics.FillPath(brush, path);

        // Top accent line
        using var accentPen = new Pen(AccentColor, 2f);
        e.Graphics.DrawLine(accentPen, CornerRadius, 0, Width - CornerRadius, 0);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Apply Windows 11 rounded corners
        int preference = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(
            Handle,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref preference,
            sizeof(int));
    }

    // ── Show / Hide Logic ──────────────────────────────────────────────

    public void ShowFlyout()
    {
        if (Visible)
        {
            HideFlyout();
            return;
        }

        PositionAboveTaskbar();
        UpdateModeButtons();
        Opacity = 0;
        Show();
        _fadeTimer.Tag = "in";
        _fadeTimer.Start();
        Activate();
    }

    public void HideFlyout()
    {
        if (!Visible) return;
        _fadeTimer.Tag = "out";
        _fadeTimer.Start();
    }

    private void FadeTimer_Tick(object? sender, EventArgs e)
    {
        string direction = _fadeTimer.Tag as string ?? "in";

        if (direction == "in")
        {
            Opacity += 0.12;
            if (Opacity >= 1.0)
            {
                Opacity = 1.0;
                _fadeTimer.Stop();
            }
        }
        else
        {
            Opacity -= 0.15;
            if (Opacity <= 0)
            {
                Opacity = 0;
                _fadeTimer.Stop();
                Hide();
                // Collapse about panel on hide
                if (_aboutVisible)
                {
                    _aboutVisible = false;
                    _aboutPanel.Visible = false;
                    ShiftControlsForAbout(false);
                }
            }
        }
    }

    private void PositionAboveTaskbar()
    {
        var workArea = Screen.PrimaryScreen!.WorkingArea;
        int x = workArea.Right - Width - 12;
        int y = workArea.Bottom - Height - 12;
        Location = new Point(x, y);
    }

    // ── Height Animation ───────────────────────────────────────────────

    private void AnimateToHeight(int target)
    {
        _targetHeight = target;
        _animTimer.Start();
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        int diff = _targetHeight - Height;
        if (Math.Abs(diff) < 4)
        {
            Height = _targetHeight;
            _animTimer.Stop();
            PositionAboveTaskbar();
            return;
        }

        Height += diff / 4;
        PositionAboveTaskbar();
    }

    // ── Telemetry Update (called from BatteryService callback) ─────────

    public void UpdateTelemetry(BatteryTelemetry telemetry)
    {
        if (InvokeRequired)
        {
            try { Invoke(() => UpdateTelemetry(telemetry)); }
            catch { /* form disposed */ }
            return;
        }

        if (!telemetry.IsAvailable)
        {
            _percentageLabel.Text = "—%";
            _statusLabel.Text = "Battery not detected";
            _progressBarFill.Width = 0;
            _progressPercentText.Text = "";
            return;
        }

        int pct = Math.Clamp(telemetry.ChargePercent, 0, 100);
        _percentageLabel.Text = $"{pct}%";
        _progressPercentText.Text = $"{pct}%";

        // Animate progress bar fill
        int targetWidth = (int)(_progressBarTrack.Width * (pct / 100.0));
        _progressBarFill.Width = targetWidth;

        // Color the fill based on percentage
        _progressBarFill.BackColor = pct switch
        {
            <= 20 => Color.FromArgb(255, 85, 85),      // Red
            <= 40 => Color.FromArgb(255, 170, 50),      // Orange
            <= 60 => Color.FromArgb(255, 215, 0),       // Yellow
            _ => AccentColor                             // Teal
        };

        // Status text
        if (!_hardwareSupported)
        {
            _statusLabel.Text = "⚠  Hardware Not Supported";
            _statusLabel.ForeColor = Color.FromArgb(255, 170, 50);
        }
        else if (telemetry.IsCharging)
        {
            _statusLabel.Text = "⚡ Charging on AC Power";
            _statusLabel.ForeColor = AccentColor;
        }
        else if (telemetry.IsFullyCharged)
        {
            _statusLabel.Text = "✓  Fully Charged on AC Power";
            _statusLabel.ForeColor = Color.FromArgb(80, 220, 120);
        }
        else if (telemetry.IsHoldingAtLimit)
        {
            _statusLabel.Text = $"🔌 Holding at {telemetry.ChargePercent}% — Charge Limit Active";
            _statusLabel.ForeColor = TextSecondary;
        }
        else if (telemetry.IsOnAcPower)
        {
            _statusLabel.Text = "🔌 Running on AC Power";
            _statusLabel.ForeColor = TextSecondary;
        }
        else
        {
            _statusLabel.Text = "🔋 Running on Battery";
            _statusLabel.ForeColor = TextSecondary;
        }

        // Predictive banner
        UpdateBanner(telemetry);
    }

    public void SetHardwareSupported(bool supported)
    {
        _hardwareSupported = supported;
        if (!supported)
        {
            _balancedButton.Enabled = false;
            _electroliteButton.Enabled = false;
        }
    }

    // ── Mode Buttons ───────────────────────────────────────────────────

    public void SetCurrentMode(BatteryMode mode)
    {
        _currentMode = mode;
        if (IsHandleCreated)
        {
            if (InvokeRequired)
                Invoke(UpdateModeButtons);
            else
                UpdateModeButtons();
        }
    }

    private void UpdateModeButtons()
    {
        bool isBalanced = _currentMode == BatteryMode.Balanced;

        StyleModeButton(_balancedButton, isBalanced);
        StyleModeButton(_electroliteButton, !isBalanced);

        // Show/hide predictive banner based on mode
        bool showBanner = _currentMode == BatteryMode.Electrolite;
        _bannerLabel.Visible = showBanner;

        int newHeight = showBanner ? FlyoutExpandedHeight : FlyoutBaseHeight;
        if (_aboutVisible) newHeight += 222;
        AnimateToHeight(newHeight);
    }

    private void StyleModeButton(Button btn, bool active)
    {
        if (active)
        {
            btn.BackColor = AccentColor;
            btn.ForeColor = Color.White;
            btn.FlatAppearance.MouseOverBackColor = AccentHoverColor;
        }
        else
        {
            btn.BackColor = SurfaceColor;
            btn.ForeColor = TextSecondary;
            btn.FlatAppearance.MouseOverBackColor = SurfaceHoverColor;
        }
    }

    /// <summary>Event raised when the user clicks a mode button.</summary>
    public event Action<BatteryMode>? ModeChangeRequested;

    /// <summary>Event raised when the user clicks the quit button.</summary>
    public event Action? QuitRequested;

    private void OnModeButtonClick(BatteryMode mode)
    {
        if (mode == _currentMode) return;
        ModeChangeRequested?.Invoke(mode);
    }

    // ── Predictive Banner ──────────────────────────────────────────────

    private void UpdateBanner(BatteryTelemetry telemetry)
    {
        if (_currentMode != BatteryMode.Electrolite)
        {
            _bannerLabel.Visible = false;
            return;
        }

        _bannerLabel.Visible = true;

        if (telemetry.IsFullyCharged || telemetry.ChargePercent >= 100)
        {
            _bannerLabel.Text = "✓  Battery Fully Saturated";
            _bannerLabel.ForeColor = Color.FromArgb(80, 220, 120);
        }
        else if (telemetry.IsCharging && telemetry.TimeToFullChargeMinutes > 0)
        {
            _bannerLabel.Text = $"⏱  Estimated time to full capacity: {telemetry.TimeToFullChargeMinutes} minutes";
            _bannerLabel.ForeColor = AccentColor;
        }
        else if (telemetry.IsCharging)
        {
            _bannerLabel.Text = "⏱  Calculating time to full charge…";
            _bannerLabel.ForeColor = TextMuted;
        }
        else if (telemetry.IsHoldingAtLimit && telemetry.IsOnAcPower)
        {
            _bannerLabel.Text = $"⚡ Limit removed — charging will resume shortly";
            _bannerLabel.ForeColor = AccentColor;
        }
        else if (!telemetry.IsOnAcPower)
        {
            _bannerLabel.Text = "🔌 Plug in to charge to 100%";
            _bannerLabel.ForeColor = TextMuted;
        }
        else
        {
            _bannerLabel.Text = "⚡ Charge limit removed — waiting for EC update";
            _bannerLabel.ForeColor = TextMuted;
        }
    }

    // ── About Panel Toggle ─────────────────────────────────────────────

    private void InfoButton_Click(object? sender, EventArgs e)
    {
        _aboutVisible = !_aboutVisible;
        _aboutPanel.Visible = _aboutVisible;
        ShiftControlsForAbout(_aboutVisible);

        int height = _currentMode == BatteryMode.Electrolite ? FlyoutExpandedHeight : FlyoutBaseHeight;
        if (_aboutVisible) height += 222;
        AnimateToHeight(height);
    }

    private void ShiftControlsForAbout(bool expanded)
    {
        int offset = expanded ? 222 : -222;

        _percentageLabel.Top += offset;
        _statusLabel.Top += offset;
        _progressBarTrack.Top += offset;
        _progressPercentText.Top += offset;
        _balancedButton.Top += offset;
        _electroliteButton.Top += offset;
        _bannerLabel.Top += offset;
    }

    // ── Helper: Create Mode Button ─────────────────────────────────────

    private static Button CreateModeButton(string text, Point location, Size size)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = TextSecondary,
            BackColor = SurfaceColor,
            FlatStyle = FlatStyle.Flat,
            Location = location,
            Size = size,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            TabStop = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = SurfaceHoverColor;
        return btn;
    }

    // ── Helper: Rounded Rectangle Path ─────────────────────────────────

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

    private static void MakeRoundedPanel(Panel panel, int radius)
    {
        panel.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedRectPath(panel.ClientRectangle, radius);
            panel.Region = new Region(path);
        };
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { /* Silently ignore if browser launch fails */ }
    }

    // ── Cleanup ────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
            _animTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
