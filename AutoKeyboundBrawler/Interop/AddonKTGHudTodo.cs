using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace AutoKeyboundBrawler.Interop;

public static unsafe class AddonKTGHudTodo
{
    public const string AddonName = "KTGHudTodo";

    public static bool TryGet(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(AddonName, out addon) &&
               addon != null &&
               addon->IsVisible;
    }

    public static string GetCurrentObjective()
    {
        if (!TryGet(out var addon) || !addon->IsReady)
        {
            return string.Empty;
        }

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null || !node->IsVisible()) continue;

            if ((int)node->Type >= 1000)
            {
                var comp = (AtkComponentNode*)node;
                if (comp->Component == null) continue;

                for (var j = 0; j < comp->Component->UldManager.NodeListCount; j++)
                {
                    var child = comp->Component->UldManager.NodeList[j];
                    if (child != null && child->Type == NodeType.Text && child->NodeId == 2)
                    {
                        var text = GUINodeUtils.GetNodeText(child);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text.Trim();
                        }
                    }
                }
            }
        }

        return string.Empty;
    }

    public static bool IsBossWave()
    {
        var objective = GetCurrentObjective();
        if (string.IsNullOrEmpty(objective)) return false;

        return objective.Contains("airbuster", StringComparison.OrdinalIgnoreCase) ||
               objective.Contains("boss", StringComparison.OrdinalIgnoreCase);
    }
}

