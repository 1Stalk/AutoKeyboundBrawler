using AutoKeyboundBrawler.Interop;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoKeyboundBrawler.Core;

public static class KTGSafetyGuard
{
    /// <summary>
    /// ContentFinderCondition sheet row ID for "Keybound Brawler" minigame duty.
    /// </summary>
    public const uint DutyContentFinderConditionId = 1095;

    /// <summary>
    /// TerritoryType sheet row ID for "w1en" (The Gold Saucer - Keybound Brawler arena).
    /// </summary>
    public const uint DutyTerritoryId = 1359;

    /// <summary>
    /// TerritoryType sheet row ID for "w1eo" (Keybound Square - event lobby area).
    /// </summary>
    public const uint SquareTerritoryId = 1360;

    private static bool? _inEventDutyOverride;
    private static bool? _inKeyboundSquareOverride;

    internal static void SetTerritoryOverridesForTesting(bool? inDuty, bool? inSquare)
    {
        _inEventDutyOverride = inDuty;
        _inKeyboundSquareOverride = inSquare;
    }

    /// <summary>
    /// Accurately detects whether the player is currently inside the Keybound Brawler event duty.
    /// Strictly requires the player to be inside Keybound Brawler arena (Territory 1359)
    /// and bound by duty (ConditionFlag.BoundByDuty).
    /// </summary>
    public static unsafe bool IsInEventDuty
    {
        get
        {
            if (_inEventDutyOverride.HasValue)
            {
                return _inEventDutyOverride.Value;
            }

            if (Svc.ClientState == null || !Svc.ClientState.IsLoggedIn || Svc.Objects?.LocalPlayer == null)
            {
                return false;
            }

            if (Svc.Condition == null || Svc.Condition[ConditionFlag.BetweenAreas] || !Svc.Condition[ConditionFlag.BoundByDuty])
            {
                return false;
            }

            var territory = Svc.ClientState.TerritoryType;
            if (territory == DutyTerritoryId)
            {
                return true;
            }

            try
            {
                var main = FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance();
                if (main != null && main->CurrentTerritoryTypeId == DutyTerritoryId)
                {
                    return true;
                }
            }
            catch
            {
                // Fall back
            }

            return false;
        }
    }

    /// <summary>
    /// Detects whether the player is currently inside Keybound Square (lobby).
    /// Strictly requires Keybound Square lobby (Territory 1360) and not bound by duty.
    /// </summary>
    public static unsafe bool IsInKeyboundSquare
    {
        get
        {
            if (_inKeyboundSquareOverride.HasValue)
            {
                return _inKeyboundSquareOverride.Value;
            }

            if (Svc.ClientState == null || !Svc.ClientState.IsLoggedIn || Svc.Objects?.LocalPlayer == null)
            {
                return false;
            }

            if (Svc.Condition == null || Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BoundByDuty])
            {
                return false;
            }

            var territory = Svc.ClientState.TerritoryType;
            if (territory == SquareTerritoryId)
            {
                return true;
            }

            try
            {
                var main = FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance();
                if (main != null && main->CurrentTerritoryTypeId == SquareTerritoryId)
                {
                    return true;
                }
            }
            catch
            {
                // Fall back
            }

            return false;
        }
    }

    public static unsafe bool IsPlayerStunned()
    {
        if (Svc.Objects == null) return false;
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null) return false;

        var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)localPlayer.Address;
        if (chara == null) return false;

        return chara->StatusManager.HasStatus(2) ||
               chara->StatusManager.HasStatus(149) ||
               chara->StatusManager.HasStatus(1309);
    }

    public static bool CanType(Configuration config, out string reason)
    {
        if (!config.Enabled)
        {
            reason = "Plugin disabled";
            return false;
        }

        if (!config.MinigameAutoplay)
        {
            reason = "Minigame autoplay disabled";
            return false;
        }

        if (!IsInEventDuty)
        {
            reason = "Not in Keybound Brawler duty";
            return false;
        }

        if (config.RequireWindowFocus && !NativeMethods.IsGameFocused())
        {
            reason = "Game window is not focused";
            return false;
        }

        if (config.StopOnPenaltyCast && AddonKTGEnemyCastBar.IsStopTypingActive(out var stopReason))
        {
            reason = $"Stop Typing Active: {stopReason}";
            return false;
        }

        if (IsPlayerStunned())
        {
            reason = "Player is Stunned!";
            return false;
        }

        reason = "OK";
        return true;
    }
}
