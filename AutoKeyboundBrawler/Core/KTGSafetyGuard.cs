using AutoKeyboundBrawler.Interop;
using ECommons.DalamudServices;
using System;

namespace AutoKeyboundBrawler.Core;

public static class KTGSafetyGuard
{
    public const uint DutyTerritoryId = 1359;
    public const uint SquareTerritoryId = 1360;

    public static bool IsInEventDuty => Svc.ClientState.TerritoryType == DutyTerritoryId;
    public static bool IsInKeyboundSquare => Svc.ClientState.TerritoryType == SquareTerritoryId;

    public static unsafe bool IsPlayerStunned()
    {
        var chara = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)ECommons.GameHelpers.Player.Character;
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
