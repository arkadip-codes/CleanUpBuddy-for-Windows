using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CleanupBuddy.Services;

public class PowerService : IDisposable
{
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS       = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED  = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    private bool _isCleaning;
    public event Action? ForceStop;

    public PowerService()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch    += OnSessionSwitch;
    }

    public void PreventSleep()
    {
        _isCleaning = true;
        SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
    }

    public void AllowSleep()
    {
        _isCleaning = false;
        SetThreadExecutionState(ES_CONTINUOUS);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend && _isCleaning)
            ForceStop?.Invoke();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock && _isCleaning)
            ForceStop?.Invoke();
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch    -= OnSessionSwitch;
        AllowSleep();
    }
}
