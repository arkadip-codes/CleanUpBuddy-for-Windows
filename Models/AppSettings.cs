using System.Text.Json.Serialization;

namespace CleanupBuddy.Models;

public enum ThemeChoice
{
    Light,
    Dark,
    System
}

public class AppSettings
{
    public ThemeChoice Theme         { get; set; } = ThemeChoice.System;
    public int  BrightnessBoost      { get; set; } = 100;
    public bool LockKeyboard         { get; set; } = true;
    public bool LockMouse            { get; set; } = true;
    // NOTE: SkipAltCheckOnStart is intentionally NOT here.
    // It is stored in the registry so it gets wiped on uninstall.
}
