using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Runtime.InteropServices;

namespace AutoKeyboundBrawler.Core;

public static unsafe class KTGAntiAfk
{
    private static DateTime _lastOsPulseTime = DateTime.MinValue;

    /// <summary>
    /// Resets both FFXIV client-internal duty inactivity timers and OS-level idle timers
    /// to prevent the game engine from executing an inactivity auto-kick
    /// ("一定時間操作がなかったため、コンテンツから強制退出されました").
    /// </summary>
    public static void KeepAlive()
    {
        // 1. Reset FFXIV's internal InputTimerModule timers
        try
        {
            var timerModule = InputTimerModule.Instance();
            if (timerModule != null)
            {
                timerModule->ContentInputTimer = 0f;
                timerModule->AfkTimer = 0f;
                timerModule->InputTimer = 0f;
            }
        }
        catch
        {
            // Ignore if struct is inaccessible
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

    private const uint MOUSEEVENTF_MOVE = 0x0001;

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
}
