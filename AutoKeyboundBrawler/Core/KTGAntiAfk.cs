using AutoKeyboundBrawler.Interop;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;

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
/// </summary>
public static unsafe class KTGAntiAfk
{
    private static DateTime _lastKeystrokeTime = DateTime.MinValue;
    private static DateTime _lastWndMsgTime = DateTime.MinValue;
    private static DateTime _lastDiagLogTime = DateTime.MinValue;
    private static DateTime _lastWarnLogTime = DateTime.MinValue;
    private static IntPtr _cachedGameHwnd = IntPtr.Zero;

    public static bool ShouldKeepAlive(Configuration config)
    {
        if (!config.Enabled)
        {
            return false;
        }

        if (!config.MinigameAutoplay && !config.AutoLoopEnabled)
        {
            return false;
        }

        // Anti-AFK functions ONLY work when the player is currently inside the Keybound Brawler event duty
        if (!KTGSafetyGuard.IsInEventDuty)
        {
            return false;
        }

        return true;
    }

    public static void KeepAlive(Configuration? config = null, bool force = false)
    {
        config ??= Plugin.Configuration;
        if (!force && (config == null || !ShouldKeepAlive(config)))
        {
            return;
        }

        // Hard safety gate: never execute anti-AFK outside Keybound Brawler event duty
        if (!force && !KTGSafetyGuard.IsInEventDuty)
        {
            return;
        }

        var now = DateTime.UtcNow;

        // ── Layer 3: Direct InputTimerModule field zeroing ────────────────
        // Safely zero top-level and per-device timers in game memory.
        // This is completely silent, does not touch OS keyboard state,
        // and works even when the game window is in the background.
        try
        {
            var timerModule = InputTimerModule.Instance();
            if (timerModule != null)
            {
                // Diagnostic: log timer values periodically to help debug kicks
                if ((now - _lastDiagLogTime).TotalSeconds >= 10.0)
                {
                    _lastDiagLogTime = now;
                    var hwnd = GetGameWindowHandle();
                    Svc.Log?.Debug(
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

        // Hardware / OS keystroke layers (Layer 1 & Layer 2):
        // STRICT SAFETY RULES:
        // 1. If game window does NOT have foreground focus, only execute when ExperimentalBackgroundMode
        //    is active (or RequireWindowFocus is disabled). In background mode, keystrokes are routed
        //    strictly to FFXIV's internal KeyboardDevice and direct window HWND without OS-level leakage.
        // 2. In foreground mode, suppress synthetic Shift if the user is physically holding Shift in FFXIV
        //    to avoid releasing their physical hold. When unfocused (e.g. Background Mode), any physical Shift
        //    belongs to an external app (Discord, browser) and must NOT block FFXIV keepalives.
        // 3. Throttle keystrokes to at most once per 10 seconds (content kick limits are 120s+).
        if (!NativeMethods.IsGameFocused() && config?.ExperimentalBackgroundMode != true && (config?.RequireWindowFocus ?? true))
        {
            return;
        }

        if (NativeMethods.IsGameFocused() && NativeMethods.IsShiftPressed())
        {
            return;
        }

        // ── Layer 1: Inject a harmless key via KeyboardDevice ─────────────
        if ((now - _lastKeystrokeTime).TotalSeconds >= 10.0)
        {
            _lastKeystrokeTime = now;
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

            try
            {
                var hwnd = GetGameWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    KeyboardDevice.ProcessKeyboardInputMessage(hwnd, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_SHIFT, IntPtr.Zero);
                    KeyboardDevice.ProcessKeyboardInputMessage(hwnd, NativeMethods.WM_KEYUP, (IntPtr)NativeMethods.VK_SHIFT, unchecked((IntPtr)(int)0xC0100001));
                }
            }
            catch
            {
                // Ignore if sig scanner uninitialized or native call throws
            }
        }

        // ── Layer 2: SendMessage to the actual FFXIV game window ─────────
        if ((now - _lastWndMsgTime).TotalSeconds >= 10.0)
        {
            _lastWndMsgTime = now;
            try
            {
                var hwnd = GetGameWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    // Keyboard activity signal (Shift down + up)
                    NativeMethods.SendMessage(hwnd, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_SHIFT, IntPtr.Zero);
                    NativeMethods.SendMessage(hwnd, NativeMethods.WM_KEYUP, (IntPtr)NativeMethods.VK_SHIFT, unchecked((IntPtr)(int)0xC0100001));
                }
            }
            catch
            {
                // Ignore — other layers will cover us
            }
        }
    }

    public static void ResetTimers()
    {
        _lastKeystrokeTime = DateTime.MinValue;
        _lastWndMsgTime = DateTime.MinValue;
    }

    internal static DateTime LastKeystrokeTime => _lastKeystrokeTime;
    internal static DateTime LastWndMsgTime => _lastWndMsgTime;

    internal static void ResetTimersForTesting()
    {
        _lastKeystrokeTime = DateTime.MinValue;
        _lastWndMsgTime = DateTime.MinValue;
        _lastDiagLogTime = DateTime.MinValue;
        _lastWarnLogTime = DateTime.MinValue;
        _cachedGameHwnd = IntPtr.Zero;
        _hwndOverrideForTesting = null;
    }

    private static IntPtr? _hwndOverrideForTesting;
    internal static void SetGameHwndOverrideForTesting(IntPtr? hwnd) => _hwndOverrideForTesting = hwnd;

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
    /// Falls back to Process.MainWindowHandle if FindWindow returns Zero.
    /// </summary>
    private static IntPtr GetGameWindowHandle()
    {
        if (_hwndOverrideForTesting.HasValue)
            return _hwndOverrideForTesting.Value;

        if (_cachedGameHwnd != IntPtr.Zero)
            return _cachedGameHwnd;

        try
        {
            _cachedGameHwnd = NativeMethods.FindWindow("FFXIVGAME", null);
            if (_cachedGameHwnd == IntPtr.Zero)
            {
                _cachedGameHwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            }
        }
        catch
        {
            // Ignore
        }

        return _cachedGameHwnd;
    }

    /// <summary>Log diagnostic warning at most once per 30 seconds to avoid spam.</summary>
    private static void LogDiag(string message)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastWarnLogTime).TotalSeconds >= 30.0)
        {
            _lastWarnLogTime = now;
            Svc.Log?.Warning(message);
        }
    }
}
