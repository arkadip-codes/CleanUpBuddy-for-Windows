using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace CleanupBuddy.Services;

public class HookService : IDisposable
{
    // ── P/Invoke ────────────────────────────────────────────────────────────
    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc fn, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] static extern bool   UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] static extern bool   GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] static extern bool   TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern bool   PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern uint  GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hWnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; }
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    private const int  WH_KEYBOARD_LL = 13;
    private const int  WH_MOUSE_LL    = 14;
    private const int  WM_KEYDOWN     = 0x0100;
    private const int  WM_KEYUP       = 0x0101;
    private const int  WM_SYSKEYDOWN  = 0x0104;
    private const int  WM_SYSKEYUP    = 0x0105;
    private const int  HC_ACTION      = 0;
    private const uint WM_QUIT        = 0x0012;
    private const uint VK_LALT        = 0xA4;
    private const uint VK_RALT        = 0xA5;

    // ── State ────────────────────────────────────────────────────────────────
    private volatile bool _lockKeyboard;
    private volatile bool _lockMouse;
    private volatile bool _active;

    private IntPtr _kbHook  = IntPtr.Zero;
    private IntPtr _msHook  = IntPtr.Zero;
    private uint   _hookThreadId;
    private Thread? _hookThread;

    private bool   _lAltDown;
    private bool   _rAltDown;
    private DateTime _bothPressedAt = DateTime.MinValue;

    private readonly System.Windows.Threading.Dispatcher _uiDispatcher;
    private readonly PowerService? _power;
    private readonly BrightnessService? _brightness;

    public event Action?         UnlockCompleted;
    public event Action<double>? RingProgress;   // 0.0 – 1.0

    private const int HoldMs = 3000;

    public HookService(System.Windows.Threading.Dispatcher uiDispatcher,
                       PowerService? power, BrightnessService? brightness)
    {
        _uiDispatcher = uiDispatcher;
        _power        = power;
        _brightness   = brightness;
    }

    // ── Public API ───────────────────────────────────────────────────────────
    public void StartCleaning(bool lockKb, bool lockMs, int brightnessBoost)
    {
        _lockKeyboard = lockKb;
        _lockMouse    = lockMs;
        _active       = true;

        _hookThread = new Thread(() =>
        {
            _hookThreadId = GetCurrentThreadId();

            // Prevent sleep from this thread
            _power?.PreventSleep();

            // Set brightness boost
            if (brightnessBoost > 0)
                _brightness?.SaveAndBoost(brightnessBoost);

            using var proc = Process.GetCurrentProcess();
            using var mod  = proc.MainModule!;
            var hMod = GetModuleHandle(mod.ModuleName);

            var kbProc = new LowLevelProc(KeyboardProc);
            var msProc = new LowLevelProc(MouseProc);

            // Always install keyboard hook (needed for Alt-key unlock detection even in test mode)
            _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, kbProc, hMod, 0);
            if (lockMs) _msHook = SetWindowsHookEx(WH_MOUSE_LL, msProc, hMod, 0);

            // Message pump
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            // Cleanup
            if (_kbHook != IntPtr.Zero) { UnhookWindowsHookEx(_kbHook); _kbHook = IntPtr.Zero; }
            if (_msHook != IntPtr.Zero) { UnhookWindowsHookEx(_msHook); _msHook = IntPtr.Zero; }

            _brightness?.Restore();
            _power?.AllowSleep();
        })
        { IsBackground = true, Name = "HookThread" };

        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    public void StopCleaning()
    {
        _active = false;
        if (_hookThreadId != 0)
            PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    // ── Keyboard hook ────────────────────────────────────────────────────────
    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool isDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
            bool isUp   = wParam == WM_KEYUP   || wParam == WM_SYSKEYUP;

            if (kb.vkCode == VK_LALT)      { _lAltDown = isDown; if (isUp) ResetRing(); }
            else if (kb.vkCode == VK_RALT) { _rAltDown = isDown; if (isUp) ResetRing(); }

            CheckUnlock();

            // Block all keys while active
            if (_active && _lockKeyboard)
                return (IntPtr)1;
        }
        return CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    // ── Mouse hook ───────────────────────────────────────────────────────────
    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION && _active && _lockMouse)
            return (IntPtr)1;
        return CallNextHookEx(_msHook, nCode, wParam, lParam);
    }

    // ── Unlock logic ─────────────────────────────────────────────────────────
    private readonly System.Timers.Timer _ringTimer = new(50) { AutoReset = true };
    private bool _ringTimerStarted;

    private void CheckUnlock()
    {
        if (_lAltDown && _rAltDown)
        {
            if (_bothPressedAt == DateTime.MinValue)
            {
                _bothPressedAt = DateTime.UtcNow;
                if (!_ringTimerStarted)
                {
                    _ringTimerStarted = true;
                    _ringTimer.Elapsed += OnRingTick;
                    _ringTimer.Start();
                }
            }
        }
        else
        {
            ResetRing();
        }
    }

    private void OnRingTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_lAltDown || !_rAltDown)
        {
            ResetRing();
            return;
        }

        double elapsed = (DateTime.UtcNow - _bothPressedAt).TotalMilliseconds;
        double progress = Math.Min(1.0, elapsed / HoldMs);

        _uiDispatcher.BeginInvoke(() => RingProgress?.Invoke(progress));

        if (elapsed >= HoldMs)
        {
            _ringTimer.Stop();
            _ringTimer.Elapsed -= OnRingTick;
            _ringTimerStarted = false;
            _uiDispatcher.BeginInvoke(() => UnlockCompleted?.Invoke());
            StopCleaning();
        }
    }

    private void ResetRing()
    {
        if (_bothPressedAt != DateTime.MinValue)
        {
            _bothPressedAt = DateTime.MinValue;
            _lAltDown = false;
            _rAltDown = false;
            _ringTimer.Stop();
            _ringTimer.Elapsed -= OnRingTick;
            _ringTimerStarted = false;
            _uiDispatcher.BeginInvoke(() => RingProgress?.Invoke(0.0));
        }
    }

    public void Dispose()
    {
        _ringTimer.Dispose();
        StopCleaning();
    }
}
