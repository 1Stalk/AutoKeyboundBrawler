using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoKeyboundBrawler.Interop;

public static unsafe class GUINodeUtils
{
    /// <summary>
    /// Checks true effective visibility: validates the node itself, its color alpha,
    /// and every parent up the ancestral chain within its ULD tree.
    /// If componentScope is provided, validates the component node and its ancestry as well.
    /// </summary>
    public static bool IsNodeActuallyVisible(AtkResNode* node, AtkResNode* componentScope = null)
    {
        if (node == null) return false;

        // 1. Check local node flags
        if ((node->NodeFlags & NodeFlags.Visible) == 0) return false;
        if (node->Color.A == 0) return false;

        // 2. Check parents within current ULD tree
        var current = node->ParentNode;
        var depth = 0;
        while (current != null && depth++ < 32)
        {
            if ((current->NodeFlags & NodeFlags.Visible) == 0) return false;
            if (current->Color.A == 0) return false;
            current = current->ParentNode;
        }

        // 3. If scoped inside a component, check the component node and its parent hierarchy
        if (componentScope != null)
        {
            if ((componentScope->NodeFlags & NodeFlags.Visible) == 0) return false;
            if (componentScope->Color.A == 0) return false;

            var compParent = componentScope->ParentNode;
            depth = 0;
            while (compParent != null && depth++ < 32)
            {
                if ((compParent->NodeFlags & NodeFlags.Visible) == 0) return false;
                if (compParent->Color.A == 0) return false;
                compParent = compParent->ParentNode;
            }
        }

        return true;
    }

    public static string? GetNodeText(AtkResNode* maybeTextNode)
    {
        if (maybeTextNode == null || maybeTextNode->Type != NodeType.Text)
        {
            return null;
        }

        var textNode = (AtkTextNode*)maybeTextNode;
        var ptr = (byte*)textNode->NodeText.StringPtr;
        if (ptr == null)
        {
            return null;
        }

        return Marshal.PtrToStringUTF8((nint)ptr);
    }

    /// <summary>Diagnostic helper to dump full addon hierarchy if needed.</summary>
    public static string DumpAddon(AtkUnitBase* addon)
    {
        if (addon == null) return "Addon is null";

        var sb = new StringBuilder();
        var name = addon->NameString;
        sb.AppendLine($"Addon: {name} (ID: {addon->Id}, Visible: {addon->IsVisible}, Ready: {addon->IsReady})");
        sb.AppendLine($"NodeCount: {addon->UldManager.NodeListCount}");

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null) continue;

            var nodeType = node->Type;
            var isVis = IsNodeActuallyVisible(node);
            var text = nodeType == NodeType.Text ? GetNodeText(node) : null;
            sb.AppendLine($"  [{i}] NodeID: {node->NodeId}, Type: {nodeType}, Visible: {isVis}" +
                          (text != null ? $", Text: \"{text}\"" : ""));

            if ((int)nodeType >= 1000)
            {
                var compNode = (AtkComponentNode*)node;
                if (compNode->Component != null)
                {
                    var compCount = compNode->Component->UldManager.NodeListCount;
                    sb.AppendLine($"    Component NodeCount: {compCount}");
                    for (var j = 0; j < compCount; j++)
                    {
                        var cNode = compNode->Component->UldManager.NodeList[j];
                        if (cNode == null) continue;
                        var cText = cNode->Type == NodeType.Text ? GetNodeText(cNode) : null;
                        var cVis = IsNodeActuallyVisible(cNode, node);
                        sb.AppendLine($"      [{j}] SubNodeID: {cNode->NodeId}, Type: {cNode->Type}, Visible: {cVis}" +
                                      (cText != null ? $", Text: \"{cText}\"" : ""));
                    }
                }
            }
        }

        return sb.ToString();
    }
}
