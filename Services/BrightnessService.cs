using System.Management;
using System.Runtime.InteropServices;

namespace CleanupBuddy.Services;

public class BrightnessService
{
    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

    [DllImport("gdi32.dll")]
    private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    private int _originalBrightness = -1;
    private RAMP _originalRamp;
    private bool _hasOriginalRamp;

    public int GetCurrentBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\wmi", "SELECT * FROM WmiMonitorBrightness");
            foreach (ManagementObject obj in searcher.Get())
            {
                return Convert.ToInt32(obj["CurrentBrightness"]);
            }
        }
        catch { }
        return 100;
    }

    public void SaveAndBoost(int boostPercent)
    {
        _originalBrightness = GetCurrentBrightness();
        if (boostPercent <= 0) return;

        // Try WMI first
        if (TrySetWmiBrightness(boostPercent)) return;

        // Fallback: gamma ramp
        IntPtr hDC = GetDC(IntPtr.Zero);
        try
        {
            _originalRamp = new RAMP { Red = new ushort[256], Green = new ushort[256], Blue = new ushort[256] };
            _hasOriginalRamp = GetDeviceGammaRamp(hDC, ref _originalRamp);
            SetGammaRamp(hDC, boostPercent);
        }
        finally { ReleaseDC(IntPtr.Zero, hDC); }
    }

    public void Restore()
    {
        if (_originalBrightness >= 0)
        {
            if (!TrySetWmiBrightness(_originalBrightness) && _hasOriginalRamp)
            {
                IntPtr hDC = GetDC(IntPtr.Zero);
                try { SetDeviceGammaRamp(hDC, ref _originalRamp); }
                finally { ReleaseDC(IntPtr.Zero, hDC); }
            }
        }
        _originalBrightness = -1;
        _hasOriginalRamp = false;
    }

    private bool TrySetWmiBrightness(int level)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("WmiSetBrightness", new object[] { 1u, (byte)level });
                return true;
            }
        }
        catch { }
        return false;
    }

    private void SetGammaRamp(IntPtr hDC, int brightnessPercent)
    {
        var ramp = new RAMP
        {
            Red   = new ushort[256],
            Green = new ushort[256],
            Blue  = new ushort[256]
        };
        double factor = brightnessPercent / 100.0;
        for (int i = 0; i < 256; i++)
        {
            ushort val = (ushort)Math.Min(65535, (int)(i * factor * 256));
            ramp.Red[i] = ramp.Green[i] = ramp.Blue[i] = val;
        }
        SetDeviceGammaRamp(hDC, ref ramp);
    }
}
