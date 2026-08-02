using System.Drawing.Drawing2D;

namespace Electrolite;

internal sealed class ToggleSwitch : Control
{
    private bool _checked;
    private float _knobPosition; // 0.0 = off, 1.0 = on
    private readonly System.Windows.Forms.Timer _animTimer;

    // Colors
    private static readonly Color TrackOffColor = Color.FromArgb(70, 70, 70);
    private static readonly Color TrackOnColor = Color.FromArgb(0, 180, 216);
    private static readonly Color KnobColor = Color.FromArgb(240, 240, 240);

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            _animTimer.Start();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetCheckedSilent(bool value)
    {
        if (_checked == value) return;
        _checked = value;
        _knobPosition = value ? 1.0f : 0.0f;
        Invalidate();
    }

    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);
        Size = new Size(40, 22);
        Cursor = Cursors.Hand;

        _animTimer = new System.Windows.Forms.Timer { Interval = 12 };
        _animTimer.Tick += AnimTick;
    }

    private void AnimTick(object? sender, EventArgs e)
    {
        float target = _checked ? 1.0f : 0.0f;
        float diff = target - _knobPosition;

        if (Math.Abs(diff) < 0.05f)
        {
            _knobPosition = target;
            _animTimer.Stop();
        }
        else
        {
            _knobPosition += diff * 0.3f;
        }
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Checked = !_checked;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int trackHeight = Height;
        int trackWidth = Width;
        int knobDiameter = trackHeight - 4;

        // Track
        Color trackColor = InterpolateColor(TrackOffColor, TrackOnColor, _knobPosition);
        using var trackBrush = new SolidBrush(trackColor);
        using var trackPath = CreatePillPath(0, 0, trackWidth, trackHeight);
        g.FillPath(trackBrush, trackPath);

        // Knob
        float knobX = 2 + _knobPosition * (trackWidth - knobDiameter - 4);
        using var knobBrush = new SolidBrush(KnobColor);
        g.FillEllipse(knobBrush, knobX, 2, knobDiameter, knobDiameter);
    }

    private static Color InterpolateColor(Color a, Color b, float t)
    {
        return Color.FromArgb(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    private static GraphicsPath CreatePillPath(float x, float y, float width, float height)
    {
        var path = new GraphicsPath();
        float radius = height / 2f;
        path.AddArc(x, y, radius * 2, height, 90, 180);
        path.AddArc(x + width - radius * 2, y, radius * 2, height, 270, 180);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animTimer.Dispose();
        base.Dispose(disposing);
    }
}
