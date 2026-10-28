using AutoKeyboundBrawler.Data;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace AutoKeyboundBrawler.Interop;

public static unsafe class AddonKTGEnterDialogue
{
    public const string AddonName = "KTGEnterDialogue";

    public static bool TryGet(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(AddonName, out addon) &&
               addon != null &&
               addon->IsVisible;
    }

    /// <summary>
    /// Presses the "Register" button in the "Keybound Brawler" window (Node 9).
    /// </summary>
    public static bool ConfirmRegistration()
    {
        if (!TryGet(out var addon) || !addon->IsReady)
        {
            return false;
        }

        try
        {
            // 1. In KTGEnterDialogue, Node 9 is the native "Register" AtkComponentButton (language-independent)
            var button = addon->GetComponentButtonById(9);
            if (button != null && button->IsEnabled)
            {
                button->ClickAddonButton(addon);
                addon->Update(0);
                return true;
            }
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Verbose(ex, "[AddonKTGEnterDialogue] Node 9 ClickAddonButton failed");
        }

        try
        {
            // 2. Scan component nodes for any button containing localized "Register" text
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
                            if (!string.IsNullOrEmpty(text) && KTGLocalizationRegistry.IsRegisterButtonText(text))
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
            ECommons.DalamudServices.Svc.Log.Verbose(ex, "[AddonKTGEnterDialogue] Button search click failed");
        }

        try
        {
            // 3. Fallback callback 0
            Callback.Fire(addon, true, 0);
            return true;
        }
        catch (Exception ex)
        {
            ECommons.DalamudServices.Svc.Log.Verbose(ex, "[AddonKTGEnterDialogue] Callback.Fire(0) failed");
        }

        return false;
    }
}
