using Dalamud.Game;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoKeyboundBrawler.Interop;

/// <summary>
/// Handles the "A usable keyboard could not be found... Are you sure you wish to play?" confirmation
/// that the game shows when it fails to detect a keyboard (Addon sheet row 17826).
/// The prompt is a plain SelectYesno dialog; it blocks the duty-finder confirmation / registration
/// and therefore must be answered "Yes" so unattended loops don't get stuck.
/// </summary>
public static unsafe class AddonKTGKeyboardWarning
{
    public const string AddonName = "SelectYesno";
    public const uint KeyboardWarningAddonRowId = 17826;

    /// <summary>Fallback fragment used when the Addon sheet lookup fails (English client only).</summary>
    internal const string EnglishFallbackFragment = "usable keyboard could not be found";

    private static string[]? _knownTexts;

    /// <summary>
    /// Normalizes text for comparison: drops all whitespace/control characters and lowercases,
    /// so line-break differences between the sheet and the rendered node don't matter.
    /// </summary>
    internal static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Pure matcher (unit-testable): does <paramref name="promptText"/> equal/contain any known keyboard warning text?</summary>
    internal static bool IsKeyboardWarningText(string? promptText, IEnumerable<string> knownTexts)
    {
        var normalized = Normalize(promptText);
        if (normalized.Length == 0) return false;

        if (normalized.Contains(Normalize(EnglishFallbackFragment), StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var known in knownTexts)
        {
            var k = Normalize(known);
            if (k.Length == 0) continue;
            if (normalized.Contains(k, StringComparison.Ordinal) || k.Contains(normalized, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] GetKnownTexts()
    {
        if (_knownTexts != null) return _knownTexts;

        var list = new List<string>();
        try
        {
            foreach (var lang in new[] { ClientLanguage.English, ClientLanguage.Japanese, ClientLanguage.German, ClientLanguage.French })
            {
                var sheet = Svc.Data?.GetExcelSheet<Addon>(lang);
                var row = sheet?.GetRowOrDefault(KeyboardWarningAddonRowId);
                var text = row?.Text.ExtractText();
                if (!string.IsNullOrWhiteSpace(text)) list.Add(text);
            }
        }
        catch (Exception ex)
        {
            Svc.Log?.Verbose(ex, "[AddonKTGKeyboardWarning] Addon sheet lookup failed");
        }

        // Only cache when we actually resolved something, otherwise retry later.
        if (list.Count > 0) _knownTexts = list.ToArray();
        return list.ToArray();
    }

    public static bool TryGet(out AtkUnitBase* addon)
    {
        try
        {
            return GenericHelpers.TryGetAddonByName(AddonName, out addon) &&
                   addon != null &&
                   addon->IsVisible &&
                   addon->IsReady;
        }
        catch
        {
            addon = null;
            return false;
        }
    }

    /// <summary>True when a SelectYesno dialog with the "no keyboard" text is currently visible.</summary>
    public static bool IsOpen()
    {
        return TryGetMatching(out _, out _);
    }

    private static bool TryGetMatching(out AtkUnitBase* addon, out string text)
    {
        text = string.Empty;
        if (!TryGet(out addon)) return false;

        try
        {
            text = new AddonMaster.SelectYesno(addon).Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            Svc.Log?.Verbose(ex, "[AddonKTGKeyboardWarning] Failed reading SelectYesno text");
            return false;
        }

        return IsKeyboardWarningText(text, GetKnownTexts());
    }

    /// <summary>Clicks "Yes" on the keyboard warning if it is open. Returns true if clicked.</summary>
    public static bool Proceed()
    {
        if (!TryGetMatching(out var addon, out var text)) return false;

        try
        {
            new AddonMaster.SelectYesno(addon).Yes();
            Svc.Log?.Information($"[AddonKTGKeyboardWarning] Dismissed keyboard warning with Yes: \"{text.Replace('\r', ' ').Replace('\n', ' ')}\"");
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log?.Information($"[AddonKTGKeyboardWarning] Yes() threw, falling back to callback 0: {ex.Message}");
        }

        try
        {
            Callback.Fire(addon, true, 0);
            return true;
        }
        catch (Exception ex)
        {
            Svc.Log?.Verbose(ex, "[AddonKTGKeyboardWarning] Callback.Fire(0) failed");
        }

        return false;
    }
}
