using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using System.Linq;

namespace AutoKeyboundBrawler.Interop;

public record KTGTargetWord(int SlotId, string Word, float ScreenY = 0f, float ScreenX = 0f);

public static unsafe class AddonKTGEWord
{
    public const string AddonName = "KTGEWord";

    private static List<KTGTargetWord>? _activeTargetsOverride;
    private static List<int>? _activeSlotsOverride;

    internal static void SetActiveTargetsOverrideForTesting(List<KTGTargetWord>? targets)
    {
        _activeTargetsOverride = targets;
    }

    internal static void SetActiveSlotsOverrideForTesting(List<int>? slots)
    {
        _activeSlotsOverride = slots;
    }

    public static bool TryGet(out AtkUnitBase* addon)
    {
        try
        {
            return GenericHelpers.TryGetAddonByName(AddonName, out addon) &&
                   addon != null &&
                   addon->IsVisible;
        }
        catch
        {
            addon = null;
            return false;
        }
    }

    /// <summary>
    /// Reads all truly visible enemy target words from KTGEWord.
    /// Strictly verifies ancestral visibility: both the parent component node and the text node must be visible.
    /// Also records the screen position (ScreenY, ScreenX) for spatial target prioritization.
    /// </summary>
    public static List<KTGTargetWord> ReadActiveTargets()
    {
        if (_activeTargetsOverride != null)
        {
            return _activeTargetsOverride;
        }

        var targets = new List<KTGTargetWord>();
        if (!TryGet(out var addon) || !addon->IsReady)
        {
            return targets;
        }

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null) continue;

            // Must be an active, visible component slot
            if ((int)node->Type >= 1000)
            {
                if (!GUINodeUtils.IsNodeActuallyVisible(node))
                {
                    continue;
                }

                var compNode = (AtkComponentNode*)node;
                if (compNode->Component == null) continue;

                var compUld = compNode->Component->UldManager;
                for (var j = 0; j < compUld.NodeListCount; j++)
                {
                    var child = compUld.NodeList[j];
                    if (child == null || child->Type != NodeType.Text) continue;

                    // Node 20 is the prompt text
                    if (child->NodeId == 20)
                    {
                        if (GUINodeUtils.IsNodeActuallyVisible(child, node))
                        {
                            var text = GUINodeUtils.GetNodeText(child);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                var screenY = node->ScreenY != 0 ? node->ScreenY : (child->ScreenY != 0 ? child->ScreenY : node->Y);
                                var screenX = node->ScreenX != 0 ? node->ScreenX : (child->ScreenX != 0 ? child->ScreenX : node->X);

                                if (screenY < -500f) continue; // Ignore off-screen placeholder

                                targets.Add(new KTGTargetWord((int)node->NodeId, text.Trim(), screenY, screenX));
                            }
                        }
                    }
                }
            }
        }

        return targets;
    }

    /// <summary>
    /// Reads all active enemy component slot IDs currently present on screen in KTGEWord.
    /// Unlike ReadActiveTargets, this detects whether the enemy plate itself is active on screen (Y > -500),
    /// even if the prompt text node or plate is temporarily hidden, transparent, or obscured by mechanics (e.g. Cryptic Communications).
    /// </summary>
    public static List<int> ReadActiveSlotIds()
    {
        if (_activeSlotsOverride != null)
        {
            return _activeSlotsOverride;
        }

        if (_activeTargetsOverride != null)
        {
            return _activeTargetsOverride.Select(t => t.SlotId).Distinct().ToList();
        }

        var slots = new List<int>();
        if (!TryGet(out var addon) || !addon->IsReady)
        {
            return slots;
        }

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null) continue;

            if ((int)node->Type >= 1000)
            {
                var screenY = node->ScreenY != 0 ? node->ScreenY : node->Y;
                if (screenY < -500f) continue;

                slots.Add((int)node->NodeId);
            }
        }

        return slots;
    }
}
