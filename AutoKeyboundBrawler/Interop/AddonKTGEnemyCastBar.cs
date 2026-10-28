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

    private static string? _localizedObstructionName;
    private static bool _localizedNameInitialized;

    private static DateTime _lastStopTypingSeenTime = DateTime.MinValue;
    private static DateTime _lastInputsReEnabledTime = DateTime.MinValue;
    private const int PostCastSafetyGraceMs = 1200; // 1.2s buffer after cast finishes

    public static string? LocalizedObstructionName
    {
        get
        {
            if (!_localizedNameInitialized)
            {
                try
                {
                    var sheet = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>();
                    if (sheet != null && sheet.HasRow(49701))
                    {
                        _localizedObstructionName = sheet.GetRow(49701).Name.ToString();
                    }
                }
                catch
                {
                    _localizedObstructionName = null;
                }
                _localizedNameInitialized = true;
            }
            return _localizedObstructionName;
        }
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

                    var text = GUINodeUtils.GetNodeText(child);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        text = text.Trim();
                        // Ignore pure timers/percentages like "3.0s", "100%"
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
    /// Checks if a spell name corresponds to the Red Keyboard Block Cast or other penalty/stop spells.
    /// </summary>
    public static bool IsStopTypingSpell(string spellName)
    {
        if (string.IsNullOrWhiteSpace(spellName)) return false;

        // 1. Primary Red Keyboard skill in Keybound Brawler (Action RowId 49701: "Obstruction")
        if (spellName.Contains("Obstruction", StringComparison.OrdinalIgnoreCase))
            return true;

        var localized = LocalizedObstructionName;
        if (!string.IsNullOrWhiteSpace(localized) &&
            spellName.Contains(localized, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Generic penalty / stop keywords
        if (spellName.Contains("Inputs disabled", StringComparison.OrdinalIgnoreCase) ||
            spellName.Contains("Disable", StringComparison.OrdinalIgnoreCase) ||
            spellName.Contains("Block", StringComparison.OrdinalIgnoreCase) ||
            spellName.Contains("Penalty", StringComparison.OrdinalIgnoreCase) ||
            spellName.Contains("Stop", StringComparison.OrdinalIgnoreCase) ||
            spellName.Contains("Freeze", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks on-screen text banners (_ScreenText or KTGHudLog) for the "Inputs disabled!" notification.
    /// </summary>
    public static bool IsInputsDisabledOnScreen()
    {
        return CheckAddonForText(ScreenTextAddonName, "Inputs disabled") ||
               CheckAddonForText(HudLogAddonName, "Inputs disabled");
    }

    /// <summary>
    /// Checks on-screen text banners (_ScreenText or KTGHudLog) for the "Inputs re-enabled!" notification.
    /// </summary>
    public static bool IsInputsReEnabledOnScreen()
    {
        return CheckAddonForText(ScreenTextAddonName, "Inputs re-enabled") ||
               CheckAddonForText(HudLogAddonName, "Inputs re-enabled");
    }

    private static bool CheckAddonForText(string addonName, string targetText)
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
                if (!string.IsNullOrEmpty(text) && text.Contains(targetText, StringComparison.OrdinalIgnoreCase))
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
                            if (!string.IsNullOrEmpty(text) && text.Contains(targetText, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Evaluates if a stop-typing attack is currently casting, banner warning is active,
    /// or safety grace window is holding.
    /// </summary>
    public static bool IsStopTypingActive(out string activeReason)
    {
        var now = DateTime.UtcNow;

        // 1. Check if "Inputs re-enabled!" is shown on screen -> cancels grace window
        if (IsInputsReEnabledOnScreen())
        {
            _lastInputsReEnabledTime = now;
        }

        // 2. Check active casts on enemy cast bar
        var activeCasts = ReadActiveCasts();
        foreach (var cast in activeCasts)
        {
            if (IsStopTypingSpell(cast.SpellName))
            {
                _lastStopTypingSeenTime = now;
                _lastInputsReEnabledTime = DateTime.MinValue;
                activeReason = $"Enemy cast: \"{cast.SpellName}\" (Red Keyboard block cast)";
                return true;
            }
        }

        // 3. Check Screen Text banner / HUD log for "Inputs disabled!"
        if (IsInputsDisabledOnScreen())
        {
            _lastStopTypingSeenTime = now;
            _lastInputsReEnabledTime = DateTime.MinValue;
            activeReason = "Screen Warning: \"Inputs disabled!\"";
            return true;
        }

        // 4. Post-Cast Safety Grace Window:
        // After Obstruction finishes casting, keep typing stopped for 1200ms to allow server hit
        // resolution and animations to settle, unless "Inputs re-enabled!" explicitly arrived.
        var msSinceStop = (now - _lastStopTypingSeenTime).TotalMilliseconds;
        var msSinceReEnable = (now - _lastInputsReEnabledTime).TotalMilliseconds;

        if (msSinceStop < PostCastSafetyGraceMs && msSinceReEnable > 500)
        {
            activeReason = $"Safety grace hold after stop cast ({PostCastSafetyGraceMs - (int)msSinceStop}ms remaining)";
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
