using AutoKeyboundBrawler.Interop;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;

namespace AutoKeyboundBrawler.Core;

/// <summary>
/// Simple and reliable anti-AFK keep-alive for Keybound Brawler instanced duty.
/// Uses only proven in-memory mechanisms that surely work when minimized or in background mode:
/// 1. Direct in-memory reset of the minigame agent inactivity timer:
///    Agent 398 + 0x50 = 0 when the director indicates match is underway, verified
///    against the Agent 397 HUD pointer at +0x28. This eliminates the 2-minute kick.
/// 2. Direct in-memory reset of FFXIV native InputTimerModule timers:
///    ContentInputTimer, AfkTimer, InputTimer, and per-device timer arrays.
///    This eliminates the 10-minute duty kick.
/// </summary>
public static unsafe class KTGAntiAfk
{
    // Offsets and IDs for the minigame internal agents and director
    public const int AgentHudPointerOffset = 0x28;
    public const int AgentInactivityTimerOffset = 0x50;
    public const int DirectorContentTypeOffset = 0xDCE;
    public const byte DirectorContentType = 23;
    public const int DirectorMatchUnderwayOffset = 0x1FC0;
    public const uint HudAgentId = 397;
    public const uint InputAgentId = 398;

    private static DateTime _lastKeepAliveTime = DateTime.MinValue;
    private static DateTime _lastAgentResetTime = DateTime.MinValue;
    private static DateTime _lastInputTimerResetTime = DateTime.MinValue;

    // Test overrides
    private static bool? _matchUnderwayOverrideForTesting;
    private static IntPtr _testAgentHudBuffer = IntPtr.Zero;
    private static IntPtr _testAgentInputBuffer = IntPtr.Zero;
    private static IntPtr _testDirectorBuffer = IntPtr.Zero;
    private static IntPtr _testInputTimerModuleBuffer = IntPtr.Zero;

    public static bool ShouldKeepAlive(Configuration? config)
    {
        if (config == null || !config.Enabled)
        {
            return false;
        }

        if (!config.MinigameAutoplay && !config.AutoLoopEnabled && !config.AutoBuyCoffers)
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

        // 1. Authoritative client timer reset via native InputTimerModule (prevents 10-minute duty kick)
        try
        {
            var timerModule = _testInputTimerModuleBuffer != IntPtr.Zero
                ? (InputTimerModule*)_testInputTimerModuleBuffer
                : InputTimerModule.Instance();

            if (timerModule != null)
            {
                timerModule->ContentInputTimer = 0f;
                timerModule->AfkTimer = 0f;
                timerModule->InputTimer = 0f;

                ResetInputTimerDataSpan(timerModule->ControllerInputTimers);
                ResetInputTimerDataSpan(timerModule->MouseInputTimers);
                _lastInputTimerResetTime = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            Svc.Log?.Verbose($"[AntiAfk] InputTimerModule reset failed: {ex.Message}");
        }

        // 2. Minigame agent inactivity timer reset (prevents 2-minute kick in background/minimized mode)
        try
        {
            ResetAgentInactivityTimer();
        }
        catch (Exception ex)
        {
            Svc.Log?.Verbose($"[AntiAfk] Minigame agent reset failed: {ex.Message}");
        }

        _lastKeepAliveTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks whether the Keybound Brawler match is currently underway via the instance content director.
    /// Director content type must be 23 and the match underway flag at offset 0x1FC0 must be non-zero.
    /// </summary>
    public static bool IsMatchUnderway()
    {
        if (_matchUnderwayOverrideForTesting.HasValue)
        {
            return _matchUnderwayOverrideForTesting.Value;
        }

        if (_testDirectorBuffer != IntPtr.Zero)
        {
            var pDirector = (byte*)_testDirectorBuffer;
            var contentType = *(pDirector + DirectorContentTypeOffset);
            var matchUnderway = *(pDirector + DirectorMatchUnderwayOffset);
            return contentType == DirectorContentType && matchUnderway != 0;
        }

        try
        {
            var framework = EventFramework.Instance();
            if (framework == null)
            {
                return false;
            }

            var director = framework->GetInstanceContentDirector();
            if (director == null)
            {
                return false;
            }

            var contentType = *((byte*)director + DirectorContentTypeOffset);
            var matchUnderway = *((byte*)director + DirectorMatchUnderwayOffset);

            return contentType == DirectorContentType && matchUnderway != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resets the minigame internal agent inactivity timer (Agent 398 + 0x50 = 0)
    /// when the director indicates a match is underway and Agent 398 +0x28 points to Agent 397 HUD.
    /// </summary>
    public static bool ResetAgentInactivityTimer()
    {
        if (!IsMatchUnderway())
        {
            return false;
        }

        if (_testAgentInputBuffer != IntPtr.Zero || _testAgentHudBuffer != IntPtr.Zero)
        {
            return ResetAgentInactivityTimerCore((byte*)_testAgentHudBuffer, (byte*)_testAgentInputBuffer);
        }

        try
        {
            var agentModule = AgentModule.Instance();
            if (agentModule == null)
            {
                return false;
            }

            var hud = agentModule->GetAgentByInternalId((AgentId)HudAgentId);
            if (hud == null)
            {
                return false;
            }

            var input = agentModule->GetAgentByInternalId((AgentId)InputAgentId);
            if (input == null)
            {
                return false;
            }

            return ResetAgentInactivityTimerCore((byte*)hud, (byte*)input);
        }
        catch (Exception ex)
        {
            Svc.Log?.Verbose($"[AntiAfk] Minigame agent reset failed: {ex.Message}");
            return false;
        }
    }

    private static bool ResetAgentInactivityTimerCore(byte* hud, byte* input)
    {
        if (hud == null || input == null)
        {
            return false;
        }

        // Verify that Agent 398 at +0x28 points to Agent 397 HUD pointer
        if (*(nint*)(input + AgentHudPointerOffset) != (nint)hud)
        {
            return false;
        }

        // Zero the inactivity timer at +0x50
        *(ulong*)(input + AgentInactivityTimerOffset) = 0;
        _lastAgentResetTime = DateTime.UtcNow;
        return true;
    }

    public static void ResetTimers()
    {
        _lastKeepAliveTime = DateTime.MinValue;
        _lastAgentResetTime = DateTime.MinValue;
        _lastInputTimerResetTime = DateTime.MinValue;
    }

    public static DateTime LastKeepAliveTime => _lastKeepAliveTime;
    public static DateTime LastAgentResetTime => _lastAgentResetTime;
    public static DateTime LastInputTimerResetTime => _lastInputTimerResetTime;

    internal static void ResetTimersForTesting()
    {
        _lastKeepAliveTime = DateTime.MinValue;
        _lastAgentResetTime = DateTime.MinValue;
        _lastInputTimerResetTime = DateTime.MinValue;
        _matchUnderwayOverrideForTesting = null;
        _testAgentHudBuffer = IntPtr.Zero;
        _testAgentInputBuffer = IntPtr.Zero;
        _testDirectorBuffer = IntPtr.Zero;
        _testInputTimerModuleBuffer = IntPtr.Zero;
    }

    internal static void SetMatchUnderwayOverrideForTesting(bool? matchUnderway)
    {
        _matchUnderwayOverrideForTesting = matchUnderway;
    }

    internal static void SetAgentBuffersForTesting(IntPtr hudBuffer, IntPtr inputBuffer)
    {
        _testAgentHudBuffer = hudBuffer;
        _testAgentInputBuffer = inputBuffer;
    }

    internal static void SetDirectorBufferForTesting(IntPtr directorBuffer)
    {
        _testDirectorBuffer = directorBuffer;
    }

    internal static void SetInputTimerModuleForTesting(IntPtr inputTimerModuleBuffer)
    {
        _testInputTimerModuleBuffer = inputTimerModuleBuffer;
    }

    private static void ResetInputTimerDataSpan(Span<InputTimerModule.InputTimerData> span)
    {
        for (var i = 0; i < span.Length; i++)
        {
            span[i].Timer = 0f;
        }
    }
}
