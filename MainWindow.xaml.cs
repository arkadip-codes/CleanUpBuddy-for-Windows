using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CleanupBuddy.Helpers;
using CleanupBuddy.Models;
using CleanupBuddy.Services;

namespace CleanupBuddy;

public partial class MainWindow : Window
{
    private App App => (App)Application.Current;

    // ── Alt test state ───────────────────────────────────────────────────────
    private HookService?    _testHooks;
    private Grid?           _testReturnView;   // which view to go back to after test
    private DispatcherTimer _testTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int             _testSeconds;
    private const double    TestRadius  = 36.0;
    private const double    TestCenterX = 48.0;
    private const double    TestCenterY = 48.0;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
        App.ThemeService.ThemeChanged += () =>
        {
            if (App.Settings.Theme == ThemeChoice.System)
                App.ApplyTheme(ThemeChoice.System);
            App.RefreshAccent();
        };
        Loaded += (_, _) => FixInitialThumbPositions();
        StateChanged += Window_StateChanged;

        _testTimer.Tick += (_, _) =>
        {
            _testSeconds++;
            TestTimerLabel.Text = $"{_testSeconds / 60:D2}:{_testSeconds % 60:D2}";
        };
    }

    // ── Rounded clip (WPF Border.ClipToBounds is rectangular, must use Clip geometry) ─
    private void RootClipBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RootClipBorder.Clip = new RectangleGeometry
        {
            Rect     = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
            RadiusX  = 12,
            RadiusY  = 12
        };
    }

    // ── Toggle thumb animation ───────────────────────────────────────────────
    private void FixInitialThumbPositions()
    {
        SnapThumb(KeyboardToggle);
        SnapThumb(MouseToggle);
        UpdateStartBtn();
    }

    private static void SnapThumb(ToggleButton toggle)
    {
        var tt = GetThumbTranslate(toggle);
        if (tt != null) tt.X = toggle.IsChecked == true ? 21 : 3;
    }

    private static TranslateTransform? GetThumbTranslate(ToggleButton toggle)
    {
        if (toggle.Template.FindName("Thumb", toggle) is Ellipse thumb)
        {
            if (thumb.RenderTransform is not TranslateTransform)
                thumb.RenderTransform = new TranslateTransform(3, 0);
            return (TranslateTransform)thumb.RenderTransform;
        }
        return null;
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton toggle)
            AnimateThumb(toggle, toggle.IsChecked == true ? 21 : 3);
        UpdateStartBtn();
    }

    private static void AnimateThumb(ToggleButton toggle, double toX)
    {
        var tt = GetThumbTranslate(toggle);
        if (tt == null) return;
        var anim = new DoubleAnimation(toX, new Duration(TimeSpan.FromMilliseconds(150)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        tt.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void UpdateStartBtn()
    {
        if (StartBtn == null) return;
        bool anyOn = KeyboardToggle.IsChecked == true || MouseToggle.IsChecked == true;
        StartBtn.IsEnabled = anyOn;
        if (StartBtn.ToolTip is ToolTip tip)
            tip.Visibility = anyOn ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Title bar / window chrome ────────────────────────────────────────────
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => DragMove();

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(120));
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var scaleX = new DoubleAnimation(1, 0.85, dur) { EasingFunction = ease };
        var scaleY = new DoubleAnimation(1, 0.85, dur) { EasingFunction = ease };
        var fade   = new DoubleAnimation(1, 0,    dur) { EasingFunction = ease };

        fade.Completed += (_, _) =>
        {
            WindowState = WindowState.Minimized;
            // Reset instantly while hidden so restore looks clean
            WindowScale.ScaleX = 1;
            WindowScale.ScaleY = 1;
            RootClipBorder.Opacity = 1;
        };

        WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        RootClipBorder.BeginAnimation(OpacityProperty, fade);
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Normal) return;

        // Restore: pop in from slightly small
        WindowScale.ScaleX = 0.92;
        WindowScale.ScaleY = 0.92;
        RootClipBorder.Opacity = 0;

        var dur  = new Duration(TimeSpan.FromMilliseconds(180));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.92, 1, dur) { EasingFunction = ease });
        WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.92, 1, dur) { EasingFunction = ease });
        RootClipBorder.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
    }

    // ── Brightness slider ────────────────────────────────────────────────────
    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrightnessLabel == null) return;
        int v = (int)e.NewValue;
        BrightnessLabel.Text = $"{v}%";
        App.Settings.BrightnessBoost = v;
        SettingsStore.Save(App.Settings);
    }

    // ── View navigation ──────────────────────────────────────────────────────
    private void ShowView(Grid view)
    {
        HomeView.Visibility       = Visibility.Collapsed;
        AppearanceView.Visibility = Visibility.Collapsed;
        TestView.Visibility       = Visibility.Collapsed;
        AltCheckView.Visibility   = Visibility.Collapsed;
        view.Visibility           = Visibility.Visible;
    }

    // ── Alt check prompt (inline AltCheckView) ───────────────────────────────
    public void ShowAltCheckPrompt()
    {
        var bmp = new System.Windows.Media.Imaging.BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri("pack://application:,,,/Assets/warning.png");
        bmp.DecodePixelWidth = 44;   // 2× for crisp rendering at 22px display size
        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bmp.EndInit();
        AltCheckWarningIcon.Source = bmp;
        ShowView(AltCheckView);
    }

    private void AltCheckSkipBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveAltCheckChoice();
        ShowView(HomeView);
    }

    private void AltCheckTestBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveAltCheckChoice();
        StartAltTest(returnTo: HomeView);
    }

    private void SaveAltCheckChoice()
    {
        if (NeverAskCheck.IsChecked == true)
            AltCheckRegistry.SetSkip(true);
    }

    private void AppearanceBtn_Click(object sender, RoutedEventArgs e)
    {
        ShowView(AppearanceView);
        RefreshThemeChecks();
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e)
        => ShowView(HomeView);

    // ── Alt key test (inline TestView) ──────────────────────────────────────
    private void AltTestRow_Click(object sender, MouseButtonEventArgs e)
        => StartAltTest(returnTo: AppearanceView);

    public void StartAltTest(Grid? returnTo = null)
    {
        _testReturnView = returnTo ?? HomeView;   // default back to Home

        _testSeconds = 0;
        TestTimerLabel.Text = "00:00";

        // Accent colour on ring
        var accent = App.ThemeService.GetAccentColor();
        TestRingPath.Stroke = new SolidColorBrush(accent);
        UpdateTestRing(0);

        ShowView(TestView);

        _testHooks = new HookService(Dispatcher, null, null);
        _testHooks.RingProgress    += p => Dispatcher.BeginInvoke(() => UpdateTestRing(p));
        _testHooks.UnlockCompleted += ()  => Dispatcher.BeginInvoke(() => FinishAltTest(passed: true));

        _testTimer.Start();
        _testHooks.StartCleaning(false, false, 0);
    }

    private void FinishAltTest(bool passed)
    {
        _testTimer.Stop();
        _testSeconds = 0;
        _testHooks?.Dispose();
        _testHooks = null;
        var returnTo = _testReturnView ?? HomeView;
        _testReturnView = null;
        ShowView(returnTo);
        if (returnTo == AppearanceView) RefreshThemeChecks();
    }

    private void TestCancelBtn_Click(object sender, RoutedEventArgs e)
        => FinishAltTest(passed: false);

    private void UpdateTestRing(double progress)
    {
        double angle   = progress * 360.0;
        bool   isLarge = angle > 180;
        double endX    = TestCenterX + TestRadius * Math.Cos((angle - 90) * Math.PI / 180.0);
        double endY    = TestCenterY + TestRadius * Math.Sin((angle - 90) * Math.PI / 180.0);

        if (progress <= 0)   { TestRingPath.Data = null; return; }
        if (progress >= 1.0) { TestRingPath.Data = new EllipseGeometry(new Point(TestCenterX, TestCenterY), TestRadius, TestRadius); return; }

        var fig = new PathFigure { StartPoint = new Point(TestCenterX, TestCenterY - TestRadius) };
        fig.Segments.Add(new ArcSegment(
            new Point(endX, endY), new Size(TestRadius, TestRadius),
            0, isLarge, SweepDirection.Clockwise, true));
        TestRingPath.Data = new PathGeometry(new[] { fig });
    }

    // ── Theme / Appearance ───────────────────────────────────────────────────
    private void ThemeLight_Click(object sender, MouseButtonEventArgs e)  => SelectTheme(ThemeChoice.Light);
    private void ThemeDark_Click(object sender, MouseButtonEventArgs e)   => SelectTheme(ThemeChoice.Dark);
    private void ThemeSystem_Click(object sender, MouseButtonEventArgs e) => SelectTheme(ThemeChoice.System);

    private void SelectTheme(ThemeChoice choice)
    {
        App.Settings.Theme = choice;
        SettingsStore.Save(App.Settings);
        App.ApplyTheme(choice);
        RefreshThemeChecks();
        UpdateSelectedBorder(choice);
    }

    private void RefreshThemeChecks()
    {
        LightCheck.Visibility  = App.Settings.Theme == ThemeChoice.Light  ? Visibility.Visible : Visibility.Collapsed;
        DarkCheck.Visibility   = App.Settings.Theme == ThemeChoice.Dark   ? Visibility.Visible : Visibility.Collapsed;
        SystemCheck.Visibility = App.Settings.Theme == ThemeChoice.System ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelectedBorder(App.Settings.Theme);
    }

    private void UpdateSelectedBorder(ThemeChoice choice)
    {
        var accent  = (SolidColorBrush)App.Resources["AccentBrush"];
        var neutral = (SolidColorBrush)App.Resources["BorderBrush2"];
        ThemeLight.BorderBrush  = choice == ThemeChoice.Light  ? accent : neutral;
        ThemeDark.BorderBrush   = choice == ThemeChoice.Dark   ? accent : neutral;
        ThemeSystem.BorderBrush = choice == ThemeChoice.System ? accent : neutral;
    }

    // ── Start Cleaning ───────────────────────────────────────────────────────
    private void StartBtn_Click(object sender, RoutedEventArgs e)
        => TriggerStartCleaning();

    public void TriggerStartCleaning()
    {
        App.Settings.LockKeyboard = KeyboardToggle.IsChecked == true;
        App.Settings.LockMouse    = MouseToggle.IsChecked    == true;
        SettingsStore.Save(App.Settings);

        var overlay = new CleaningOverlay(
            App.Settings.LockKeyboard,
            App.Settings.LockMouse,
            App.Settings.BrightnessBoost);
        overlay.Show();
    }

    private void LoadSettings()
    {
        KeyboardToggle.IsChecked = App.Settings.LockKeyboard;
        MouseToggle.IsChecked    = App.Settings.LockMouse;
        BrightnessSlider.Value   = App.Settings.BrightnessBoost;
    }

    // Called from App after settings are loaded in the background pass
    public void ApplyLoadedSettings() => LoadSettings();
}
