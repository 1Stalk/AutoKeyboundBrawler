using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Runtime.InteropServices;

namespace AutoKeyboundBrawler.Core;

public static unsafe class KTGAntiAfk
{
    private static DateTime _lastOsPulseTime = DateTime.MinValue;
    private static DateTime _lastDiagLogTime = DateTime.MinValue;

    /// <summary>
    /// Resets both FFXIV client-internal duty inactivity timers and OS-level idle timers
    /// to prevent the game engine from executing an inactivity auto-kick
    /// ("一定時間操作がなかったため、コンテンツから強制退出されました").
    /// </summary>
    public static void KeepAlive()
    {
        // 1. Reset FFXIV's internal InputTimerModule timers (top-level AND per-device)
        try
        {
            var timerModule = InputTimerModule.Instance();
            if (timerModule != null)
            {
                // Reset top-level activity timers
                timerModule->ContentInputTimer = 0f;
                timerModule->AfkTimer = 0f;
                timerModule->InputTimer = 0f;

                // Reset per-device timers: the game checks controller and mouse InputTimerData
                // arrays independently. If these aren't zeroed the game considers the player
                // inactive even when the top-level timers are reset.
                ResetInputTimerDataSpan(timerModule->ControllerInputTimers);
                ResetInputTimerDataSpan(timerModule->MouseInputTimers);
            }
        }
        catch (Exception ex)
        {
            // Log once per 60 seconds to diagnose silent failures
            if ((DateTime.UtcNow - _lastDiagLogTime).TotalSeconds >= 60.0)
            {
                _lastDiagLogTime = DateTime.UtcNow;
                ECommons.DalamudServices.Svc.Log.Warning($"[AntiAfk] InputTimerModule reset failed: {ex.Message}");
            }
        }

        // 2. Send a harmless OS-level input pulse every 5 seconds to keep Windows GetLastInputInfo fresh
        if ((DateTime.UtcNow - _lastOsPulseTime).TotalSeconds >= 5.0)
        {
            _lastOsPulseTime = DateTime.UtcNow;
            try
            {
                // mouse_event with dx=0, dy=0 does not move the mouse cursor at all,
                // but immediately resets Windows GetLastInputInfo (OS idle timer)
                mouse_event(MOUSEEVENTF_MOVE, 0, 0, 0, 0);
            }
            catch
            {
                // Ignore fallback
            }
        }
    }

    /// <summary>
    /// Resets the Timer field of every InputTimerData element in a Span.
    /// This ensures the game's per-device inactivity tracking is also zeroed.
    /// </summary>
    private static void ResetInputTimerDataSpan(Span<InputTimerModule.InputTimerData> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            span[i].Timer = 0f;
        }
    }

    private const uint MOUSEEVENTF_MOVE = 0x0001;

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
}
