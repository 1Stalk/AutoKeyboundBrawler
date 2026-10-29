using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AutoKeyboundBrawler.Core;

/// <summary>
/// Multi-layered anti-AFK system that prevents the game from kicking the player
/// for inactivity inside instanced content.
///
/// Layer 1 (Primary): Post WM_MOUSEMOVE to the game window — this is processed
///     by the game's WndProc and resets all internal input timers through the
///     game's own code path, which is the most reliable approach.
/// Layer 2: Directly zero InputTimerModule timer fields (top-level + per-device).
/// Layer 3: OS-level mouse_event(MOVE,0,0) to reset Windows GetLastInputInfo.
/// </summary>
public static unsafe class KTGAntiAfk
{
    private static DateTime _lastGameMsgTime = DateTime.MinValue;
    private static DateTime _lastOsPulseTime = DateTime.MinValue;
    private static DateTime _lastDiagLogTime = DateTime.MinValue;
    private static IntPtr _cachedGameHwnd = IntPtr.Zero;

    public static void KeepAlive()
    {
        // ── Layer 1: Post WM_MOUSEMOVE to the game's own window ──────────
        // This goes through the game's normal WndProc and resets all input
        // timers exactly as real mouse movement would. We send it every 3
        // seconds — far more often than any inactivity threshold.
        if ((DateTime.UtcNow - _lastGameMsgTime).TotalSeconds >= 3.0)
        {
            _lastGameMsgTime = DateTime.UtcNow;
            try
            {
                var hwnd = GetGameWindowHandle();
                if (hwnd != IntPtr.Zero)
                {
                    // WM_MOUSEMOVE with position (1,1) — a harmless no-op that
                    // the game processes as real input activity.
                    PostMessage(hwnd, WM_MOUSEMOVE, IntPtr.Zero, MakeLParam(1, 1));
                }
            }
            catch
            {
                // Ignore — Layer 2 and 3 will cover us
            }
        }

        // ── Layer 2: Direct InputTimerModule field zeroing ───────────────
        try
        {
            var timerModule = InputTimerModule.Instance();
            if (timerModule != null)
            {
                // Diagnostic: log timer values periodically to help debug kicks
                if ((DateTime.UtcNow - _lastDiagLogTime).TotalSeconds >= 10.0)
                {
                    _lastDiagLogTime = DateTime.UtcNow;
                    ECommons.DalamudServices.Svc.Log.Verbose(
                        $"[AntiAfk] Timers before reset: Content={timerModule->ContentInputTimer:F2} " +
                        $"Afk={timerModule->AfkTimer:F2} Input={timerModule->InputTimer:F2} " +
                        $"Limit={timerModule->InstanceContentAfkTimeLimit:F0} " +
                        $"Status={timerModule->Status}");
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

        // ── Layer 3: OS-level idle timer reset ───────────────────────────
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

    private static IntPtr GetGameWindowHandle()
    {
        if (_cachedGameHwnd != IntPtr.Zero)
            return _cachedGameHwnd;

        try
        {
            _cachedGameHwnd = Process.GetCurrentProcess().MainWindowHandle;
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
    private const uint MOUSEEVENTF_MOVE = 0x0001;

    private static IntPtr MakeLParam(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
}
