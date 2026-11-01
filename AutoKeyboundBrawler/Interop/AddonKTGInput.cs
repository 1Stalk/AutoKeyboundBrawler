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

    private static bool? _directSetTextOverride;
    private static bool? _directSubmitOverride;

    internal static void SetDirectOverridesForTesting(bool? setTextOverride, bool? submitOverride)
    {
        _directSetTextOverride = setTextOverride;
        _directSubmitOverride = submitOverride;
    }

    /// <summary>
    /// Injects text directly into KTGInput's AtkComponentTextInput buffer in memory.
    /// Does not simulate OS or hardware keystrokes.
    /// </summary>
    public static bool DirectSetText(string text)
    {
        if (_directSetTextOverride.HasValue) return _directSetTextOverride.Value;
        try
        {
            var input = GetTextInput();
            if (input == null) return false;
            input->SetText(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clears KTGInput's AtkComponentTextInput buffer directly in memory without sending Escape.
    /// </summary>
    public static bool DirectClearInput()
    {
        if (_directSetTextOverride.HasValue) return _directSetTextOverride.Value;
        try
        {
            var input = GetTextInput();
            if (input == null) return false;
            input->SetText(string.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Triggers the native submission callback on KTGInput's AtkComponentTextInput (simulating Enter)
    /// without sending physical OS keystrokes.
    /// </summary>
    public static bool DirectSubmit(out bool cleared)
    {
        return DirectSubmit(null, out cleared);
    }

    /// <summary>
    /// Triggers the native submission callback on KTGInput's AtkComponentTextInput (simulating Enter)
    /// without sending physical OS keystrokes, using the provided text or buffer text.
    /// </summary>
    public static bool DirectSubmit(string? text, out bool cleared)
    {
        cleared = false;
        if (_directSubmitOverride.HasValue)
        {
            cleared = true;
            return _directSubmitOverride.Value;
        }
        try
        {
            var input = GetTextInput();
            if (input == null || input->Callback == null) return false;

            var addon = input->OwnerAddon;
            if (addon == null && !TryGet(out addon)) return false;

            // Ensure text is populated in the component buffer if provided
            if (!string.IsNullOrEmpty(text))
            {
                input->SetText(text);
            }

            var textPtr = (byte*)input->EvaluatedString.StringPtr;
            if (textPtr == null || *textPtr == 0)
            {
                textPtr = (byte*)input->RawString.StringPtr;
            }

            // Fallback: encode text if internal pointers are null or empty
            byte[]? utf8Buffer = null;
            if (textPtr == null || *textPtr == 0)
            {
                var candidate = text ?? GetCurrentInputText();
                if (string.IsNullOrEmpty(candidate)) return false;
                utf8Buffer = System.Text.Encoding.UTF8.GetBytes(candidate + "\0");
            }

            fixed (byte* fallbackPtr = utf8Buffer)
            {
                var targetPtr = textPtr != null && *textPtr != 0 ? textPtr : fallbackPtr;
                if (targetPtr == null || *targetPtr == 0) return false;

                input->Callback(
                    addon,
                    InputCallbackType.Enter,
                    targetPtr,
                    targetPtr,
                    input->CallbackEventKind);

                // Always clear the text buffer upon submission to prevent stale leftover input
                input->SetText(string.Empty);
                cleared = true;
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}
