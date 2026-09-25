using Microsoft.Win32;
using System.Windows.Media;

namespace CleanupBuddy.Services;

public enum SystemTheme { Light, Dark }

public class ThemeService
{
    private const string ThemeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string DwmKey   = @"Software\Microsoft\Windows\DWM";

    public event Action? ThemeChanged;

    public ThemeService()
    {
        // Subscribe on a background thread so it doesn't force WinForms
        // message-pump initialisation on the UI thread at startup
        Task.Run(() => SystemEvents.UserPreferenceChanged += (s, e) => ThemeChanged?.Invoke());
    }

    public SystemTheme GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeKey);
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int i && i == 0) return SystemTheme.Dark;
        }
        catch { }
        return SystemTheme.Light;
    }

    public Color GetAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
            var val = key?.GetValue("AccentColor");
            if (val is int abgr)
            {
                // ABGR → ARGB
                byte a = (byte)((abgr >> 24) & 0xFF);
                byte b = (byte)((abgr >> 16) & 0xFF);
                byte g = (byte)((abgr >> 8)  & 0xFF);
                byte r = (byte)( abgr        & 0xFF);
                return Color.FromArgb(255, r, g, b);
            }
        }
        catch { }
        // Default Windows 11 blue
        return Color.FromRgb(0x00, 0x67, 0xC0);
    }
}
