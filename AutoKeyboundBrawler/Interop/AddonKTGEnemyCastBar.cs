using AutoKeyboundBrawler.Data;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoKeyboundBrawler.Interop;

public record KTGActiveCast(int SlotId, string SpellName);

public static unsafe class AddonKTGEnemyCastBar
{
    public const string CastBarAddonName = "KTGEnemyCastBar";
    public const string ScreenTextAddonName = "_ScreenText";
    public const string HudLogAddonName = "KTGHudLog";

    private const float PreCastHaltThresholdSeconds = 1.8f;
    private const int PostCastHoldDurationMs = 3800; // 3.8s post-cast wait (3-4 seconds)

    private static DateTime _postCastHoldUntil = DateTime.MinValue;
    private static string _lastAvoidedSpellName = string.Empty;

    private static bool _wasCastingHaltSpell;
    private static string _lastSeenHaltSpellName = string.Empty;
    private static float _lastSeenCastRemaining;
    private static float _lastSeenCastCurrent;

    private static DateTime _uiCastStartTime = DateTime.MinValue;
    private static string? _uiCastSpellName;

    private static DateTime _lastInputsReEnabledTime = DateTime.MinValue;

    public static void Reset()
    {
        _postCastHoldUntil = DateTime.MinValue;
        _lastAvoidedSpellName = string.Empty;
        _wasCastingHaltSpell = false;
        _lastSeenHaltSpellName = string.Empty;
        _lastSeenCastRemaining = 0;
        _lastSeenCastCurrent = 0;
        _uiCastSpellName = null;
        _uiCastStartTime = DateTime.MinValue;
    }

    public static bool TryGetCastBar(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(CastBarAddonName, out addon) &&
               addon != null &&
               addon->IsVisible;
    }

    public static List<KTGActiveCast> ReadActiveCasts()
    {
        var casts = new List<KTGActiveCast>();
        if (!TryGetCastBar(out var castBar) || !castBar->IsReady)
        {
            return casts;
        }

        for (var i = 0; i < castBar->UldManager.NodeListCount; i++)
        {
            var node = castBar->UldManager.NodeList[i];
            if (node == null || !GUINodeUtils.IsNodeActuallyVisible(node)) continue;

            // Direct text node on castbar
            if (node->Type == NodeType.Text)
            {
                var text = GUINodeUtils.GetNodeText(node);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = text.Trim();
                    if (text.Length > 2 && text.Any(char.IsLetter))
                    {
                        casts.Add(new KTGActiveCast((int)node->NodeId, text));
                    }
                }
                continue;
            }

            // Component node (e.g. enemy cast bar slot)
            if ((int)node->Type >= 1000)
            {
                var comp = (AtkComponentNode*)node;
                if (comp->Component == null) continue;

                for (var j = 0; j < comp->Component->UldManager.NodeListCount; j++)
                {
                    var child = comp->Component->UldManager.NodeList[j];
                    if (child == null || child->Type != NodeType.Text || !GUINodeUtils.IsNodeActuallyVisible(child, node)) continue;

                    // Node 5 is the numerical cast timer (e.g. "3.5s" or "0"), Node 3 is "Interrupted!"
                    if (child->NodeId == 5 || child->NodeId == 3) continue;

                    var text = GUINodeUtils.GetNodeText(child);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        text = text.Trim();
                        if (string.Equals(text, "Interrupted!", StringComparison.OrdinalIgnoreCase)) continue;
                        if (text.Length > 2 && text.Any(char.IsLetter))
                        {
                            casts.Add(new KTGActiveCast((int)node->NodeId, text));
                        }
                    }
                }
            }
        }

        return casts;
    }

    /// <summary>
    /// Checks if a spell name corresponds to a Cactuar Needles cast:
    /// e.g. "??? Needles", "1,000 Needles", "10,000 Needles", "Needles", "Countless Needles", etc.
    /// Supports all client localizations via KTGLocalizationRegistry.
    /// </summary>
    public static bool IsNeedlesSpell(string? spellName, uint actionId = 0)
    {
        return KTGLocalizationRegistry.IsNeedlesSpell(spellName, actionId);
    }

    public static bool HasActiveNeedlesCast(out KTGActiveCast? needlesCast)
    {
        // 1. Primary check: Svc.Objects for any live BattleChara actively casting Needles
        try
        {
            foreach (var obj in Svc.Objects)
            {
                if (obj is Dalamud.Game.ClientState.Objects.Types.IBattleChara chara && chara.IsCasting)
                {
                    var actionId = chara.CastActionId;
                    var name = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(actionId)?.Name.ToString();
                    if (IsNeedlesSpell(name, actionId))
                    {
                        needlesCast = new KTGActiveCast((int)chara.EntityId, name ?? "針？本");
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Object table scan safety
        }

        // 2. Secondary check: KTGEnemyCastBar UI addon
        var casts = ReadActiveCasts();
        needlesCast = casts.FirstOrDefault(c => IsNeedlesSpell(c.SpellName));
        return needlesCast != null;
    }

    /// <summary>
    /// Checks if a spell name or action ID corresponds to the actual Halt casts:
    /// - Dynamic Sensory Jammer (ID 49702)
    /// - Cursed Gaze (ID 49529)
    /// Supports all client localizations via KTGLocalizationRegistry.
    /// </summary>
    public static bool IsHaltAvoidSpell(string? spellName, uint actionId = 0)
    {
        return KTGLocalizationRegistry.IsHaltSpell(spellName, actionId);
    }

    /// <summary>
    /// Checks on-screen text banners (_ScreenText or KTGHudLog) for the "Inputs re-enabled!" notification.
    /// Supports all client localizations via KTGLocalizationRegistry.
    /// </summary>
    public static bool IsInputsReEnabledOnScreen()
    {
        return CheckAddonForPredicate(ScreenTextAddonName, KTGLocalizationRegistry.MatchesInputsReEnabled) ||
               CheckAddonForPredicate(HudLogAddonName, KTGLocalizationRegistry.MatchesInputsReEnabled);
    }

    /// <summary>
    /// Checks on-screen text banners (_ScreenText or KTGHudLog) for the "Inputs disabled!" notification.
    /// Supports all client localizations via KTGLocalizationRegistry.
    /// </summary>
    public static bool IsInputsDisabledOnScreen()
    {
        return CheckAddonForPredicate(ScreenTextAddonName, KTGLocalizationRegistry.MatchesInputsDisabled) ||
               CheckAddonForPredicate(HudLogAddonName, KTGLocalizationRegistry.MatchesInputsDisabled);
    }

    private static bool CheckAddonForPredicate(string addonName, Func<string, bool> predicate)
    {
        if (!GenericHelpers.TryGetAddonByName(addonName, out AtkUnitBase* addon) ||
            addon == null ||
            !addon->IsVisible ||
            !addon->IsReady)
        {
            return false;
        }

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || !GUINodeUtils.IsNodeActuallyVisible(node)) continue;

            if (node->Type == NodeType.Text)
            {
                var text = GUINodeUtils.GetNodeText(node);
                if (!string.IsNullOrEmpty(text) && predicate(text))
                {
                    return true;
                }
            }
            else if ((int)node->Type >= 1000)
            {
                var comp = (AtkComponentNode*)node;
                if (comp->Component != null)
                {
                    for (var j = 0; j < comp->Component->UldManager.NodeListCount; j++)
                    {
                        var child = comp->Component->UldManager.NodeList[j];
                        if (child != null && child->Type == NodeType.Text && GUINodeUtils.IsNodeActuallyVisible(child, node))
                        {
                            var text = GUINodeUtils.GetNodeText(child);
                            if (!string.IsNullOrEmpty(text) && predicate(text))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    private record ActiveHaltCast(string SpellName, float CurrentCastTime, float TotalCastTime, float RemainingCastTime);

    private static bool TryFindActiveHaltCast(out ActiveHaltCast cast)
    {
        // 1. Primary source: Svc.Objects BattleCharas (exact in-game actor casting timers)
        try
        {
            foreach (var obj in Svc.Objects)
            {
                if (obj is Dalamud.Game.ClientState.Objects.Types.IBattleChara chara && chara.IsCasting)
                {
                    var actionId = chara.CastActionId;
                    var name = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(actionId)?.Name.ToString();

                    if (IsHaltAvoidSpell(name, actionId))
                    {
                        var cur = chara.CurrentCastTime;
                        var total = Math.Max(0.1f, chara.TotalCastTime);
                        var rem = Math.Max(0f, total - cur);
                        cast = new ActiveHaltCast(name ?? "Halt Spell", cur, total, rem);
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Object table scan safety
        }

        // 2. Secondary source: KTGEnemyCastBar UI addon
        try
        {
            var activeCasts = ReadActiveCasts();
            foreach (var ac in activeCasts)
            {
                if (IsHaltAvoidSpell(ac.SpellName, 0))
                {
                    if (_uiCastSpellName != ac.SpellName)
                    {
                        _uiCastSpellName = ac.SpellName;
                        _uiCastStartTime = DateTime.UtcNow;
                    }

                    var elapsed = (float)(DateTime.UtcNow - _uiCastStartTime).TotalSeconds;
                    const float estimatedTotal = 5.0f;
                    var rem = Math.Max(0f, estimatedTotal - elapsed);
                    cast = new ActiveHaltCast(ac.SpellName, elapsed, estimatedTotal, rem);
                    return true;
                }
            }
        }
        catch
        {
            // Addon scan safety
        }

        _uiCastSpellName = null;
        cast = null!;
        return false;
    }

    /// <summary>
    /// Evaluates if a halt avoid action is required:
    /// - Towards the end of cast (remaining <= 1.8s): halts typing and holds still.
    /// - After cast finishes: waits 3.8s (3-4 seconds) to ensure the penalty snapshot window has passed.
    /// - Also stops on "Inputs disabled!" screen banners.
    /// </summary>
    public static bool IsStopTypingActive(out string activeReason)
    {
        var now = DateTime.UtcNow;

        // 1. If "Inputs re-enabled!" appears on screen, cancel any lingering wait
        if (IsInputsReEnabledOnScreen())
        {
            _lastInputsReEnabledTime = now;
            _postCastHoldUntil = DateTime.MinValue;
        }

        // 2. Check Post-Cast Hold Window (3-4s wait after halt spell finishes casting)
        if (now < _postCastHoldUntil)
        {
            var remainingSec = (_postCastHoldUntil - now).TotalSeconds;
            activeReason = $"Halt Avoid: Holding {remainingSec:F1}s after \"{_lastAvoidedSpellName}\"";
            return true;
        }

        // 3. Scan for active Dynamic Sensory Jammer or Cursed Gaze casts
        var isCasting = TryFindActiveHaltCast(out var activeCast);

        if (isCasting)
        {
            _lastSeenHaltSpellName = activeCast.SpellName;
            _lastSeenCastRemaining = activeCast.RemainingCastTime;
            _lastSeenCastCurrent = activeCast.CurrentCastTime;
            _wasCastingHaltSpell = true;

            // Stop typing towards the end of the cast (<= 1.8s remaining)
            if (activeCast.RemainingCastTime <= PreCastHaltThresholdSeconds)
            {
                activeReason = $"Halt Avoid: End of \"{activeCast.SpellName}\" ({activeCast.RemainingCastTime:F1}s left - STOPPED)";
                return true;
            }
        }
        else
        {
            // Cast ended. Check if we just transitioned out of a halt cast that reached near completion
            if (_wasCastingHaltSpell)
            {
                _wasCastingHaltSpell = false;

                // If the cast was near completion (current >= 3.0s or remaining <= 2.0s), start 3.8s post-cast hold
                if (_lastSeenCastCurrent >= 3.0f || _lastSeenCastRemaining <= 2.0f)
                {
                    _lastAvoidedSpellName = _lastSeenHaltSpellName;
                    _postCastHoldUntil = now.AddMilliseconds(PostCastHoldDurationMs);
                    Svc.Log.Information($"[AutoKeyboundBrawler] Halt cast \"{_lastAvoidedSpellName}\" completed. Holding idle for 3.8s to avoid halt.");
                    var remainingSec = (_postCastHoldUntil - now).TotalSeconds;
                    activeReason = $"Halt Avoid: Holding {remainingSec:F1}s after \"{_lastAvoidedSpellName}\"";
                    return true;
                }
            }
        }

        // 4. Fallback check: Screen warning "Inputs disabled!"
        if (IsInputsDisabledOnScreen())
        {
            activeReason = "Screen Warning: \"Inputs disabled!\"";
            return true;
        }

        activeReason = string.Empty;
        return false;
    }

    public static bool IsStopTypingActive()
    {
        return IsStopTypingActive(out _);
    }
}
