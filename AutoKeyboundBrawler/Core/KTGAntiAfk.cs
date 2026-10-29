using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Runtime.InteropServices;

namespace AutoKeyboundBrawler.Core;

/// <summary>
/// Multi-layered anti-AFK system that prevents the game from kicking the player
/// for inactivity inside instanced content.
///
/// Layer 1 (Primary): Inject a harmless keystroke (Shift press/release) through
///     the game's own KeyboardDevice every 3 seconds. This is the ONLY method
///     that reliably resets all internal input timers on ALL clients including
///     Japanese (where PostMessage/memory writes alone are insufficient).
/// Layer 2: SendMessage WM_KEYDOWN/WM_KEYUP to the actual FFXIV game window
///     (found via FindWindow("FFXIVGAME") to avoid hitting an IME window).
/// Layer 3: Directly zero InputTimerModule timer fields (top-level + per-device).
/// Layer 4: OS-level mouse_event(MOVE,0,0) to reset Windows GetLastInputInfo.
/// </summary>
public static unsafe class KTGAntiAfk
{
    private static DateTime _lastKeystrokeTime = DateTime.MinValue;
    private static DateTime _lastWndMsgTime = DateTime.MinValue;
    private static DateTime _lastOsPulseTime = DateTime.MinValue;
    private static DateTime _lastDiagLogTime = DateTime.MinValue;
    private static IntPtr _cachedGameHwnd = IntPtr.Zero;

    public static void KeepAlive()
    {
        // ── Layer 1: Inject a harmless key via KeyboardDevice ─────────────
        // Send a Shift press/release through the game's internal keyboard
        // device every 3 seconds. This goes through the same code path as
        // real player keypresses and is the only method proven to reset the
        // content inactivity timer on Japanese clients.
        // Shift alone has no gameplay effect (no character produced, no action).
        if ((DateTime.UtcNow - _lastKeystrokeTime).TotalSeconds >= 3.0)
        {
            _lastKeystrokeTime = DateTime.UtcNow;
            try
            {
                var manager = InputDeviceManager.Instance();
                if (manager != null && manager->KeyboardDevice != null)
                {
                    manager->KeyboardDevice->SendKey(SeVirtualKey.SHIFT);
                }
            }
            catch
            {
                // Layer 2+ will cover us
            }
        }

        // ── Layer 2: SendMessage to the actual FFXIV game window ─────────
        // Use FindWindow("FFXIVGAME") to find the real game window, NOT
        // Process.MainWindowHandle which can return an IME composition window
        // on Japanese systems. Use SendMessage (synchronous) for guaranteed
        // delivery, sending both WM_MOUSEMOVE and WM_KEYDOWN/WM_KEYUP.
        if ((DateTime.UtcNow - _lastWndMsgTime).TotalSeconds >= 3.0)
        {
            _lastWndMsgTime = DateTime.UtcNow;
            try
            {
                var hwnd = GetGameWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    // Mouse activity signal
                    SendMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, MakeLParam(1, 1));

                    // Keyboard activity signal (Shift down + up)
                    SendMessage(hwnd, WM_KEYDOWN, (IntPtr)VK_SHIFT, IntPtr.Zero);
                    SendMessage(hwnd, WM_KEYUP, (IntPtr)VK_SHIFT, unchecked((IntPtr)(int)0xC0100001));
                }
            }
            catch
            {
                // Ignore — other layers will cover us
            }
        }

        // ── Layer 3: Direct InputTimerModule field zeroing ────────────────
        try
        {
            var timerModule = InputTimerModule.Instance();
            if (timerModule != null)
            {
                // Diagnostic: log timer values periodically to help debug kicks
                if ((DateTime.UtcNow - _lastDiagLogTime).TotalSeconds >= 10.0)
                {
                    _lastDiagLogTime = DateTime.UtcNow;
                    var hwnd = GetGameWindowHandle();
                    ECommons.DalamudServices.Svc.Log.Debug(
                        $"[AntiAfk] Content={timerModule->ContentInputTimer:F2} " +
                        $"Afk={timerModule->AfkTimer:F2} Input={timerModule->InputTimer:F2} " +
                        $"Limit={timerModule->InstanceContentAfkTimeLimit:F0} " +
                        $"Status={timerModule->Status} HWND={hwnd:X}");
                }

                // Zero top-level timers
                timerModule->ContentInputTimer = 0f;
                timerModule->AfkTimer = 0f;
                timerModule->InputTimer = 0f;

                // Zero per-device timers (controller + mouse InputTimerData arrays)
                ResetInputTimerDataSpan(timerModule->ControllerInputTimers);
                ResetInputTimerDataSpan(timerModule->MouseInputTimers);
            }
            else
            {
                LogDiag("[AntiAfk] InputTimerModule.Instance() returned null!");
            }
        }
        catch (Exception ex)
        {
            LogDiag($"[AntiAfk] InputTimerModule reset failed: {ex.Message}");
        }

        // ── Layer 4: OS-level idle timer reset ───────────────────────────
        if ((DateTime.UtcNow - _lastOsPulseTime).TotalSeconds >= 5.0)
        {
            _lastOsPulseTime = DateTime.UtcNow;
            try
            {
                mouse_event(MOUSEEVENTF_MOVE, 0, 0, 0, 0);
            }
            catch
            {
                // Ignore
            }
        }
    }

    private static void ResetInputTimerDataSpan(Span<InputTimerModule.InputTimerData> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            span[i].Timer = 0f;
        }
    }

    /// <summary>
    /// Finds the actual FFXIV game window by its window class name "FFXIVGAME".
    /// This is critical on Japanese systems where Process.MainWindowHandle can
    /// return an IME composition window instead of the real game window.
    /// </summary>
    private static IntPtr GetGameWindowHandle()
    {
        if (_cachedGameHwnd != IntPtr.Zero)
            return _cachedGameHwnd;

        try
        {
            // FindWindow by class name — reliable across all locales
            _cachedGameHwnd = FindWindow("FFXIVGAME", null);
        }
        catch
        {
            // Ignore
        }

        return _cachedGameHwnd;
    }

    /// <summary>Log diagnostic message at most once per 30 seconds to avoid spam.</summary>
    private static void LogDiag(string message)
    {
        if ((DateTime.UtcNow - _lastDiagLogTime).TotalSeconds >= 30.0)
        {
            _lastDiagLogTime = DateTime.UtcNow;
            ECommons.DalamudServices.Svc.Log.Warning(message);
        }
    }

    // ── Win32 Interop ────────────────────────────────────────────────────

    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const int VK_SHIFT = 0x10;
    private const uint MOUSEEVENTF_MOVE = 0x0001;

    private static IntPtr MakeLParam(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
}
