using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;

namespace AutoKeyboundBrawler.Interop;

public record KTGTargetWord(int SlotId, string Word);

public static unsafe class AddonKTGEWord
{
    public const string AddonName = "KTGEWord";

    public static bool TryGet(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(AddonName, out addon) &&
               addon != null &&
               addon->IsVisible;
    }

    /// <summary>
    /// Reads all truly visible enemy target words from KTGEWord.
    /// Strictly verifies ancestral visibility: both the parent component node and the text node must be visible.
    /// </summary>
    public static List<KTGTargetWord> ReadActiveTargets()
    {
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
                                targets.Add(new KTGTargetWord((int)node->NodeId, text.Trim()));
                            }
                        }
                    }
                }
            }
        }

        return targets;
    }
}
