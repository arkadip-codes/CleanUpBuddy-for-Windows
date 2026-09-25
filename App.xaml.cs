using System.Threading;
using System.Windows;
using System.Windows.Media;
using CleanupBuddy.Helpers;
using CleanupBuddy.Models;
using CleanupBuddy.Services;
using Hardcodet.Wpf.TaskbarNotification;

namespace CleanupBuddy;

public partial class App : Application
{
    private Mutex?           _mutex;
    private TaskbarIcon?     _trayIcon;
    public  ThemeService     ThemeService   { get; } = new();
    public  AppSettings      Settings       { get; private set; } = new();
    private MainWindow?      _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "CleanupBuddy_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("Cleanup Buddy is already running.", "Cleanup Buddy",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Apply a default theme immediately so the window paints on first frame
        ApplyTheme(ThemeChoice.System);

        // Show the window right away — don't block on I/O or registry reads
        _mainWindow = new MainWindow();
        _mainWindow.Show();

        // Finish the rest after the window is visible
        Dispatcher.InvokeAsync(() =>
        {
            Settings = SettingsStore.Load();
            ApplyTheme(Settings.Theme);

            RefreshAccent();

            ThemeService.ThemeChanged += () =>
            {
                if (Settings.Theme == ThemeChoice.System)
                    ApplyTheme(ThemeChoice.System);
                RefreshAccent();
            };

            _mainWindow.ApplyLoadedSettings();

            BuildTrayIcon();

            if (!AltCheckRegistry.GetSkip())
                _mainWindow.ShowAltCheckPrompt();

        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void BuildTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/tray.ico")),
            ToolTipText = "Cleanup Buddy"
        };

        var menu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "Open" };
        openItem.Click += (_, _) => { _mainWindow?.Show(); _mainWindow?.Activate(); };

        var cleanItem = new System.Windows.Controls.MenuItem { Header = "Start Cleaning" };
        cleanItem.Click += (_, _) => { _mainWindow?.Show(); _mainWindow?.TriggerStartCleaning(); };

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Shutdown();

        menu.Items.Add(openItem);
        menu.Items.Add(cleanItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => { _mainWindow?.Show(); _mainWindow?.Activate(); };
    }

    public void ApplyTheme(ThemeChoice choice)
    {
        bool dark = choice switch
        {
            ThemeChoice.Dark   => true,
            ThemeChoice.Light  => false,
            _                  => ThemeService.GetSystemTheme() == SystemTheme.Dark
        };

        var r = Resources;
        if (dark)
        {
            r["BgBrush"]        = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
            r["CardBrush"]      = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
            r["TitleBarBrush"]  = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
            r["BorderBrush2"]   = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
            r["RowBgBrush"]     = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            r["RowHoverBrush"]  = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
            r["TextBrush"]      = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            r["MutedBrush"]     = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            r["ToggleOffBrush"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            r["IconTintBrush"]  = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        }
        else
        {
            r["BgBrush"]        = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
            r["CardBrush"]      = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            r["TitleBarBrush"]  = new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xF9));
            r["BorderBrush2"]   = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            r["RowBgBrush"]     = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
            r["RowHoverBrush"]  = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            r["TextBrush"]      = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            r["MutedBrush"]     = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            r["ToggleOffBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
            r["IconTintBrush"]  = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
        }
    }

    public void RefreshAccent()
    {
        var c = ThemeService.GetAccentColor();
        Resources["AccentBrush"]  = new SolidColorBrush(c);
        Resources["AccentColor"]  = c;

        // Lighter variant for hover
        var lighter = Color.FromRgb(
            (byte)Math.Min(255, c.R + 20),
            (byte)Math.Min(255, c.G + 20),
            (byte)Math.Min(255, c.B + 20));
        Resources["AccentHoverBrush"] = new SolidColorBrush(lighter);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}

