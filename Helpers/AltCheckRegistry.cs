using Microsoft.Win32;

namespace CleanupBuddy.Helpers;

/// <summary>
/// Stores the "skip Alt-key check on startup" flag in the registry.
/// This key is deleted by the Inno Setup uninstaller, so a fresh install
/// always prompts the user again.
/// </summary>
public static class AltCheckRegistry
{
    private const string SubKey    = @"Software\CleanupBuddy";
    private const string ValueName = "SkipAltCheckOnStart";

    public static bool GetSkip()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SubKey);
            return key?.GetValue(ValueName) is int v && v == 1;
        }
        catch { return false; }
    }

    public static void SetSkip(bool skip)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SubKey);
            key.SetValue(ValueName, skip ? 1 : 0, RegistryValueKind.DWord);
        }
        catch { }
    }
}
