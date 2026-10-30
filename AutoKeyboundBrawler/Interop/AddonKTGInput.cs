using ECommons;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace AutoKeyboundBrawler.Interop;

public static unsafe class AddonKTGInput
{
    public const string AddonName = "KTGInput";

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

    public static AtkComponentTextInput* GetTextInput()
    {
        if (!TryGet(out var addon) || !addon->IsReady)
        {
            return null;
        }

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node != null && node->NodeId == 7 && (int)node->Type >= 1000)
            {
                var compNode = (AtkComponentNode*)node;
                return (AtkComponentTextInput*)compNode->Component;
            }
        }

        return null;
    }

    public static bool IsInputActive()
    {
        try
        {
            var input = GetTextInput();
            return input != null && input->IsActive;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Reads the currently evaluated text in the player's input buffer (Node 16 inside TextInput Component 7).
    /// </summary>
    public static string? GetCurrentInputText()
    {
        if (!TryGet(out var addon) || !addon->IsReady)
        {
            return null;
        }

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node != null && node->NodeId == 7 && (int)node->Type >= 1000)
            {
                var compNode = (AtkComponentNode*)node;
                if (compNode->Component == null) continue;

                var compUld = compNode->Component->UldManager;
                for (var j = 0; j < compUld.NodeListCount; j++)
                {
                    var child = compUld.NodeList[j];
                    if (child != null && child->NodeId == 16 && child->Type == NodeType.Text)
                    {
                        var text = GUINodeUtils.GetNodeText(child);
                        return text?.Trim();
                    }
                }
            }
        }

        return null;
    }

    public static bool IsInputEmpty()
    {
        var text = GetCurrentInputText();
        return string.IsNullOrEmpty(text);
    }
}
