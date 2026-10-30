using AutoKeyboundBrawler.Data;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace AutoKeyboundBrawler.Interop;

public static unsafe class AddonContentsFinderConfirm
{
    public const string AddonName = "ContentsFinderConfirm";

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

    public static bool CommenceDuty()
    {
        if (!TryGet(out var addon) || !addon->IsReady)
        {
            return false;
        }

        try
        {
            // 1. Scan for button labeled with localized "Commence" (Commence / 突入 / Teilnehmen / Commencer)
            for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node == null || (int)node->Type < 1000) continue;

                var compNode = (AtkComponentNode*)node;
                if (compNode->Component != null)
                {
                    for (var j = 0; j < compNode->Component->UldManager.NodeListCount; j++)
                    {
                        var child = compNode->Component->UldManager.NodeList[j];
                        if (child != null && child->Type == NodeType.Text)
                        {
                            var text = GUINodeUtils.GetNodeText(child);
                            if (!string.IsNullOrEmpty(text) && KTGLocalizationRegistry.IsCommenceButtonText(text))
                            {
                                var btn = (AtkComponentButton*)compNode->Component;
                                if (btn != null && btn->IsEnabled)
                                {
                                    btn->ClickAddonButton(addon);
                                    addon->Update(0);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Verbose(ex, "[AddonContentsFinderConfirm] Commence button click failed");
        }

        try
        {
            // 2. Standard FFXIV Commence callback is 8
            Callback.Fire(addon, true, 8);
            return true;
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Verbose(ex, "[AddonContentsFinderConfirm] Callback.Fire(8) failed");
        }

        return false;
    }
}
