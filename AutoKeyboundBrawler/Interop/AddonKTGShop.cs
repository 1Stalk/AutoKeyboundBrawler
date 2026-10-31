using AutoKeyboundBrawler.Data;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Linq;

namespace AutoKeyboundBrawler.Interop;

/// <summary>
/// Authoritative interop handler for MGC Trader shop interactions and Keybound Coffer exchanges.
/// Supports ShopExchangeCurrency, InclusionShop, SpecialShop, and their confirmation dialogs.
/// </summary>
public static unsafe class AddonKTGShop
{
    public const string ShopExchangeCurrencyAddonName = "ShopExchangeCurrency";
    public const string ShopExchangeCurrencyDialogAddonName = "ShopExchangeCurrencyDialog";
    public const string InclusionShopAddonName = "InclusionShop";
    public const string SpecialShopAddonName = "SpecialShop";
    public const string ExchangeDialogAddonName = "ShopExchangeItemDialog";
    public const string SelectYesnoAddonName = "SelectYesno";
    public const string SelectIconStringAddonName = "SelectIconString";
    public const string SelectStringAddonName = "SelectString";
    public const string TalkAddonName = "Talk";

    /// <summary>
    /// Checks whether any shop exchange or vendor window is currently open and ready.
    /// </summary>
    public static bool IsShopOpen()
    {
        return TryGetShopExchangeCurrency(out _) ||
               TryGetShopExchangeCurrencyDialog(out _) ||
               TryGetSelectYesno(out _) ||
               TryGetInclusionShop(out _) ||
               TryGetSpecialShop(out _) ||
               TryGetExchangeDialog(out _);
    }

    /// <summary>
    /// Checks whether any prerequisite dialogue window (Talk, SelectString, SelectIconString) is open.
    /// </summary>
    public static bool IsDialogueOpen()
    {
        return TryGetTalk(out _) || TryGetSelectString(out _) || TryGetSelectIconString(out _);
    }

    public static bool TryGetShopExchangeCurrency(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(ShopExchangeCurrencyAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool TryGetShopExchangeCurrencyDialog(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(ShopExchangeCurrencyDialogAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool TryGetInclusionShop(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(InclusionShopAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool TryGetSpecialShop(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(SpecialShopAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool TryGetExchangeDialog(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(ExchangeDialogAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool TryGetSelectYesno(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(SelectYesnoAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool TryGetTalk(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(TalkAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool TryGetSelectString(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(SelectStringAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    public static bool TryGetSelectIconString(out AtkUnitBase* addon)
    {
        return GenericHelpers.TryGetAddonByName(SelectIconStringAddonName, out addon) &&
               addon != null && addon->IsVisible && addon->IsReady;
    }

    /// <summary>
    /// Advances opening dialogue (Talk or selection menu) if present.
    /// Returns true if an action was performed.
    /// </summary>
    public static bool HandleOpeningDialogue()
    {
        if (TryGetTalk(out var talk))
        {
            Callback.Fire(talk, true, 0);
            return true;
        }

        if (TryGetSelectIconString(out var selectIcon))
        {
            Callback.Fire(selectIcon, true, 0);
            return true;
        }

        if (TryGetSelectString(out var selectString))
        {
            Callback.Fire(selectString, true, 0);
            return true;
        }

        return false;
    }

    public delegate void NodeScanCallback(AtkResNode* node, AtkComponentNode* parentComp);

    /// <summary>
    /// Traverses all nodes in an AtkUnitBase hierarchy recursively down to maxDepth 10.
    /// Safely invokes action for each node.
    /// </summary>
    public static void ForEachNode(AtkUnitBase* addon, NodeScanCallback action)
    {
        if (addon == null) return;
        ScanUld(&addon->UldManager, null, action, 0);
    }

    private static void ScanUld(AtkUldManager* uld, AtkComponentNode* parentComp, NodeScanCallback action, int depth)
    {
        if (uld == null || depth > 10) return;
        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null) continue;

            try
            {
                action(node, parentComp);
            }
            catch { }

            if ((int)node->Type >= 1000)
            {
                var comp = (AtkComponentNode*)node;
                if (comp->Component != null)
                {
                    ScanUld(&comp->Component->UldManager, comp, action, depth + 1);
                }
            }
        }
    }

    public static uint ReadAtkUInt(AtkUnitBase* addon, int index)
    {
        if (addon == null || addon->AtkValues == null || index < 0 || index >= addon->AtkValuesCount)
            return 0;
        var val = addon->AtkValues[index];
        if (val.Type == AtkValueType.UInt) return val.UInt;
        if (val.Type == AtkValueType.Int) return (uint)Math.Max(0, val.Int);
        return 0;
    }

    /// <summary>
    /// Searches all nodes in the addon recursively to find and activate the "Others" tab.
    /// Sets wasAlreadyActive to true if the Others tab was already checked.
    /// Returns true if the Others tab is active or was clicked.
    /// </summary>
    public static bool EnsureOthersTabSelected(AtkUnitBase* addon, out bool wasAlreadyActive)
    {
        wasAlreadyActive = false;
        if (addon == null || !addon->IsReady) return false;

        AtkComponentRadioButton* othersRadio = null;
        AtkComponentButton* othersButton = null;

        ForEachNode(addon, (node, parentComp) =>
        {
            if (node == null) return;

            // Direct component check
            if ((int)node->Type >= 1000)
            {
                var compNode = (AtkComponentNode*)node;
                var comp = compNode->Component;
                if (comp != null)
                {
                    if (compNode->AtkResNode.Type == (NodeType)(1000 + (int)ComponentType.RadioButton))
                    {
                        var radio = (AtkComponentRadioButton*)comp;
                        if (radio->ButtonTextNode != null)
                        {
                            var text = GUINodeUtils.GetNodeText((AtkResNode*)radio->ButtonTextNode);
                            if (KTGLocalizationRegistry.IsOthersTabText(text))
                            {
                                othersRadio = radio;
                            }
                        }
                    }
                    else if (compNode->AtkResNode.Type == (NodeType)(1000 + (int)ComponentType.Button))
                    {
                        var btn = (AtkComponentButton*)comp;
                        if (btn->ButtonTextNode != null)
                        {
                            var text = GUINodeUtils.GetNodeText((AtkResNode*)btn->ButtonTextNode);
                            if (KTGLocalizationRegistry.IsOthersTabText(text))
                            {
                                othersButton = btn;
                            }
                        }
                    }
                }
            }

            // Check text child inside component
            if (node->Type == NodeType.Text && parentComp != null && parentComp->Component != null)
            {
                var text = GUINodeUtils.GetNodeText(node);
                if (KTGLocalizationRegistry.IsOthersTabText(text))
                {
                    if (parentComp->AtkResNode.Type == (NodeType)(1000 + (int)ComponentType.RadioButton))
                    {
                        othersRadio = (AtkComponentRadioButton*)parentComp->Component;
                    }
                    else if (parentComp->AtkResNode.Type == (NodeType)(1000 + (int)ComponentType.Button))
                    {
                        othersButton = (AtkComponentButton*)parentComp->Component;
                    }
                }
            }
        });

        if (othersRadio != null)
        {
            if (othersRadio->IsChecked)
            {
                wasAlreadyActive = true;
                return true;
            }

            othersRadio->ClickRadioButton(addon);
            addon->Update(0);
            Svc.Log.Information("[AddonKTGShop] Switched to 'Others' tab via ClickRadioButton.");
            return true;
        }

        if (othersButton != null)
        {
            othersButton->ClickAddonButton(addon);
            addon->Update(0);
            Svc.Log.Information("[AddonKTGShop] Clicked 'Others' tab button via ClickAddonButton.");
            return true;
        }

        // Fallback: Fire tab switch callback (1, 1, 0)
        Callback.Fire(addon, true, 1, 1, 0);
        wasAlreadyActive = true;
        Svc.Log.Information("[AddonKTGShop] Fired tab switch callback (1, 1, 0).");
        return true;
    }

    /// <summary>
    /// Searches rendered UI nodes in the open shop for the Keybound Coffer item and clicks it.
    /// </summary>
    public static bool ClickCofferItemNode(AtkUnitBase* addon)
    {
        if (addon == null || !addon->IsReady) return false;

        AtkComponentButton* cofferButton = null;
        AtkComponentNode* cofferCompNode = null;

        ForEachNode(addon, (node, parentComp) =>
        {
            if (node == null) return;
            if (node->Type == NodeType.Text && parentComp != null && parentComp->Component != null)
            {
                var text = GUINodeUtils.GetNodeText(node);
                if (!string.IsNullOrEmpty(text) && KTGLocalizationRegistry.IsKeyboundCofferText(text))
                {
                    if (parentComp->AtkResNode.Type == (NodeType)(1000 + (int)ComponentType.Button))
                    {
                        cofferButton = (AtkComponentButton*)parentComp->Component;
                        cofferCompNode = parentComp;
                    }
                    else
                    {
                        cofferCompNode = parentComp;
                    }
                }
            }
        });

        if (cofferButton != null && cofferButton->IsEnabled)
        {
            cofferButton->ClickAddonButton(addon);
            addon->Update(0);
            Svc.Log.Information("[AddonKTGShop] Clicked Keybound Coffer item button.");
            return true;
        }

        if (cofferCompNode != null)
        {
            var resNode = &cofferCompNode->AtkResNode;
            if (resNode->AtkEventManager.Event != null)
            {
                var evt = resNode->AtkEventManager.Event;
                addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, evt, null);
                addon->Update(0);
                Svc.Log.Information("[AddonKTGShop] Clicked Keybound Coffer component via ReceiveEvent.");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Purchases one Keybound Coffer (Item ID 52450) from the open shop window.
    /// Returns true if a purchase action or tab switch was performed.
    /// </summary>
    public static bool BuyOneCoffer()
    {
        // 0. Confirm Yes/No confirmation dialog if open
        if (TryGetSelectYesno(out var yesno))
        {
            try
            {
                var dialog = new AddonMaster.SelectYesno(yesno);
                dialog.Yes();
                Svc.Log.Information("[AddonKTGShop] Confirmed purchase on SelectYesno via Yes().");
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Information($"[AddonKTGShop] SelectYesno Yes() threw, falling back to callback 0: {ex.Message}");
                Callback.Fire(yesno, true, 0);
                return true;
            }
        }

        // 1. Confirm purchase dialog if already open
        if (TryGetShopExchangeCurrencyDialog(out var curDialog))
        {
            try
            {
                var dialog = new AddonMaster.ShopExchangeCurrencyDialog(curDialog);
                dialog.Exchange();
                Svc.Log.Information("[AddonKTGShop] Confirmed purchase on ShopExchangeCurrencyDialog via Exchange().");
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Information($"[AddonKTGShop] ShopExchangeCurrencyDialog Exchange threw, falling back to callback 0: {ex.Message}");
                Callback.Fire(curDialog, true, 0);
                return true;
            }
        }

        if (TryGetExchangeDialog(out var itemDialog))
        {
            try
            {
                var dialog = new AddonMaster.ShopExchangeItemDialog(itemDialog);
                dialog.Exchange();
                Svc.Log.Information("[AddonKTGShop] Confirmed purchase on ShopExchangeItemDialog via Exchange().");
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Information($"[AddonKTGShop] ShopExchangeItemDialog Exchange threw, falling back to callback 0: {ex.Message}");
                Callback.Fire(itemDialog, true, 0);
                return true;
            }
        }

        // 2. Main MGC Shop: ShopExchangeCurrency
        if (TryGetShopExchangeCurrency(out var curShop))
        {
            try
            {
                // Attempt A: Direct lookup via AgentShop.Instance()->ItemReceiveSpan
                var agentShop = AgentShop.Instance();
                if (agentShop != null)
                {
                    var span = agentShop->ItemReceiveSpan;
                    for (var i = 0; i < span.Length; i++)
                    {
                        if (span[i].ItemId == KTGLocalizationRegistry.KeyboundCofferItemId)
                        {
                            Callback.Fire(curShop, true, 0, i, 1, 0);
                            Svc.Log.Information($"[AddonKTGShop] Selected Keybound Coffer via AgentShop (Index: {i}) using Callback (0, {i}, 1, 0).");
                            return true;
                        }
                    }
                }

                // Attempt B: Check current AtkValues list
                if (curShop->AtkValues != null && curShop->AtkValuesCount > 1310)
                {
                    var count = ReadAtkUInt(curShop, 4);
                    for (var i = 0; i < count; i++)
                    {
                        if (ReadAtkUInt(curShop, 1064 + i) == KTGLocalizationRegistry.KeyboundCofferItemId)
                        {
                            var rowIdx = (int)ReadAtkUInt(curShop, 1308 + i);
                            Callback.Fire(curShop, true, 0, rowIdx, 1, 0);
                            Svc.Log.Information($"[AddonKTGShop] Selected Keybound Coffer via AtkValues (Row: {rowIdx}) using Callback (0, {rowIdx}, 1, 0).");
                            return true;
                        }
                    }
                }

                // Attempt C: Direct UI node click if visible
                if (ClickCofferItemNode(curShop))
                {
                    return true;
                }

                // Attempt D: If not yet visible in UI, switch to "Others" tab
                if (EnsureOthersTabSelected(curShop, out var wasAlreadyActive))
                {
                    if (!wasAlreadyActive)
                    {
                        Svc.Log.Information("[AddonKTGShop] Switched to 'Others' tab. Awaiting item populate on next tick.");
                        return true;
                    }
                }

                // Attempt E: Fallback row selection (0-based Index 8 on Others tab, 4 parameters: 0, 8, 1, 0)
                Callback.Fire(curShop, true, 0, 8, 1, 0);
                Svc.Log.Information("[AddonKTGShop] Fired fallback purchase callback (0, 8, 1, 0) on ShopExchangeCurrency for Keybound Coffer.");
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Warning(ex, "[AddonKTGShop] ShopExchangeCurrency buy attempt threw");
            }
        }

        // 3. Fallback: InclusionShop
        if (TryGetInclusionShop(out var incAddon))
        {
            try
            {
                var incMaster = new AddonMaster.InclusionShop(incAddon);
                var coffer = incMaster.ShopItems.FirstOrDefault(i => i.ItemId == KTGLocalizationRegistry.KeyboundCofferItemId);

                if (coffer != null)
                {
                    coffer.Select();
                    Svc.Log.Information("[AddonKTGShop] Selected Keybound Coffer from InclusionShop.");
                    return true;
                }

                var agent = AgentInclusionShop.Instance();
                if (agent != null && agent->Data != null)
                {
                    var catCount = agent->Data->CategoryCount;
                    var currentCat = agent->Data->SelectedCategoryIndex;
                    var nextCat = (byte)((currentCat + 1) % Math.Max(1, (int)catCount));
                    if (nextCat != currentCat)
                    {
                        agent->SelectCategory(nextCat);
                        Svc.Log.Information($"[AddonKTGShop] Switched InclusionShop category from {currentCat} to {nextCat}.");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Verbose(ex, "[AddonKTGShop] InclusionShop purchase attempt failed");
            }
        }

        // 4. Fallback: SpecialShop
        if (TryGetSpecialShop(out var specAddon))
        {
            try
            {
                if (EnsureOthersTabSelected(specAddon, out _))
                {
                    return true;
                }
                Callback.Fire(specAddon, true, 1, 0);
                Svc.Log.Information("[AddonKTGShop] Fired selection callback on SpecialShop.");
                return true;
            }
            catch (Exception ex)
            {
                Svc.Log.Verbose(ex, "[AddonKTGShop] SpecialShop callback failed");
            }
        }

        return false;
    }

    /// <summary>
    /// Closes all active shop and dialog windows.
    /// </summary>
    public static void CloseShop()
    {
        try
        {
            if (TryGetSelectYesno(out var yesno))
            {
                Callback.Fire(yesno, true, 1);
            }

            if (TryGetShopExchangeCurrencyDialog(out var curDiag))
            {
                Callback.Fire(curDiag, true, -1);
            }

            if (TryGetExchangeDialog(out var itemDiag))
            {
                Callback.Fire(itemDiag, true, -1);
            }

            if (TryGetShopExchangeCurrency(out var curShop))
            {
                Callback.Fire(curShop, true, -1);
            }

            if (TryGetInclusionShop(out var inc))
            {
                Callback.Fire(inc, true, -1);
            }

            if (TryGetSpecialShop(out var spec))
            {
                Callback.Fire(spec, true, -1);
            }

            if (TryGetSelectIconString(out var sis))
            {
                Callback.Fire(sis, true, -1);
            }

            if (TryGetSelectString(out var ss))
            {
                Callback.Fire(ss, true, -1);
            }

            if (TryGetTalk(out var talk))
            {
                Callback.Fire(talk, true, -1);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Verbose(ex, "[AddonKTGShop] CloseShop threw");
        }
    }
}
