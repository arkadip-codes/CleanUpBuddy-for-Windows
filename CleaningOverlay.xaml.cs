using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CleanupBuddy.Services;

namespace CleanupBuddy;

public partial class CleaningOverlay : Window
{
    private readonly HookService      _hooks;
    private readonly PowerService     _power;
    private readonly BrightnessService _brightness;
    private readonly DispatcherTimer  _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _seconds;

    private const double Radius      = 36.0;
    private const double CenterX     = 48.0;
    private const double CenterY     = 48.0;
    private const double Circumference = 2 * Math.PI * Radius; // ≈ 226.2

    public CleaningOverlay(bool lockKeyboard, bool lockMouse, int brightnessBoost)
    {
        InitializeComponent();

        _power      = new PowerService();
        _brightness = new BrightnessService();
        _hooks      = new HookService(Dispatcher, _power, _brightness);

        _power.ForceStop += () => Dispatcher.BeginInvoke(FinishCleaning);

        _hooks.UnlockCompleted += () => Dispatcher.BeginInvoke(FinishCleaning);
        _hooks.RingProgress    += progress => Dispatcher.BeginInvoke(() => UpdateRing(progress));

        _timer.Tick += (_, _) =>
        {
            _seconds++;
            TimerLabel.Text = $"{_seconds / 60:D2}:{_seconds % 60:D2}";
        };

        Loaded += (_, _) =>
        {
            // Apply accent colour to ring
            var accent = ((App)Application.Current).ThemeService.GetAccentColor();
            RingPath.Stroke = new SolidColorBrush(accent);

            // Draw at 0%
            UpdateRing(0);

            // Set dynamic status text based on what's actually locked
            StatusPill.Text = (lockKeyboard, lockMouse) switch
            {
                (true,  true)  => "⌨️  Keyboard  +  🖱️  Mouse are locked",
                (true,  false) => "⌨️  Keyboard is locked",
                (false, true)  => "🖱️  Mouse is locked",
                _              => "Screen is active"
            };

            _timer.Start();
            _hooks.StartCleaning(lockKeyboard, lockMouse, brightnessBoost);
        };
    }

    private void UpdateRing(double progress)
    {
        // Build arc path geometry
        double angle  = progress * 360.0;
        bool   isLarge = angle > 180;

        double radians = (angle - 90) * Math.PI / 180.0;
        double endX    = CenterX + Radius * Math.Cos((angle - 90) * Math.PI / 180.0);
        double endY    = CenterY + Radius * Math.Sin((angle - 90) * Math.PI / 180.0);

        // Start point = top centre (12 o'clock)
        double startX = CenterX;
        double startY = CenterY - Radius;

        if (progress <= 0)
        {
            RingPath.Data = null;
            return;
        }

        if (progress >= 1.0)
        {
            // Full circle
            RingPath.Data = new EllipseGeometry(new Point(CenterX, CenterY), Radius, Radius);
            return;
        }

        var fig = new PathFigure { StartPoint = new Point(startX, startY) };
        fig.Segments.Add(new ArcSegment(
            point: new Point(endX, endY),
            size: new Size(Radius, Radius),
            rotationAngle: 0,
            isLargeArc: isLarge,
            sweepDirection: SweepDirection.Clockwise,
            isStroked: true));

        RingPath.Data = new PathGeometry(new[] { fig });
    }

    private void FinishCleaning()
    {
        _timer.Stop();
        _hooks.Dispose();
        _power.Dispose();
        Close();
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _timer.Stop();
        _hooks.Dispose();
        _power.Dispose();
        base.OnClosed(e);
    }
}
