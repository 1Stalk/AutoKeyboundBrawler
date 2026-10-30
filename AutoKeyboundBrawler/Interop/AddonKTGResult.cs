using AutoKeyboundBrawler.Data;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace AutoKeyboundBrawler.Interop;

public static unsafe class AddonKTGResult
{
    public const string AddonName = "KTGResult";

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
    /// Presses the return button in the final Results screen to return to Keybound Square.
    /// Node 48 is the native Exit button on KTGResult.
    /// </summary>
    public static bool CloseResults()
    {
        if (!TryGet(out var addon) || !addon->IsReady)
        {
            return false;
        }

        try
        {
            // 1. Button 48 is the native Exit / Leave button on KTGResult
            var button = addon->GetComponentButtonById(48);
            if (button != null && button->IsEnabled)
            {
                button->ClickAddonButton(addon);
                addon->Update(0);
                return true;
            }
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Verbose(ex, "[AddonKTGResult] Button 48 ClickAddonButton failed");
        }

        try
        {
            // 2. Scan component nodes for any button containing localized close/exit text
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
                            if (!string.IsNullOrEmpty(text) && KTGLocalizationRegistry.IsResultCloseButtonText(text))
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
            ECommons.DalamudServices.Svc.Log.Verbose(ex, "[AddonKTGResult] Button search click failed");
        }

        try
        {
            // 3. Fallback to firing callback 0
            Callback.Fire(addon, true, 0);
            return true;
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Verbose(ex, "[AddonKTGResult] Callback.Fire(0) failed");
        }

        return false;
    }
}
