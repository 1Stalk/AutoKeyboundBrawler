using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoKeyboundBrawler.Data;

public class WordEntry
{
    public uint RowId { get; init; }
    public string Original { get; init; } = string.Empty;
    public string Normalized { get; init; } = string.Empty;
    public string Stripped { get; init; } = string.Empty;
}

public static class KTGWordDatabase
{
    private static readonly List<WordEntry> Entries = [];
    private static readonly Dictionary<string, WordEntry> ByExactMatch = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, WordEntry> ByStrippedMatch = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsLoaded { get; private set; }
    public static int Count => Entries.Count;

    public static bool Load(IDataManager dataManager, IPluginLog? log = null)
    {
        if (IsLoaded && Entries.Count > 0)
        {
            return true;
        }

        try
        {
            var sheet = dataManager.GetExcelSheet<KTGTypeWordTextData>();
            if (sheet == null)
            {
                log?.Error("[KTGWordDatabase] Failed to open Excel sheet KTGTypeWordTextData.");
                return false;
            }

            Entries.Clear();
            ByExactMatch.Clear();
            ByStrippedMatch.Clear();

            foreach (var row in sheet)
            {
                var text = row.Unknown0.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var normalized = text.ToLowerInvariant();
                var stripped = StripPunctuation(normalized);

                var entry = new WordEntry
                {
                    RowId = row.RowId,
                    Original = text,
                    Normalized = normalized,
                    Stripped = stripped
                };

                Entries.Add(entry);
                ByExactMatch.TryAdd(normalized, entry);
                ByStrippedMatch.TryAdd(stripped, entry);
            }

            IsLoaded = true;
            log?.Information($"[KTGWordDatabase] Successfully loaded {Entries.Count} words from KTGTypeWordTextData.");
            return true;
        }
        catch (Exception ex)
        {
            log?.Error(ex, "[KTGWordDatabase] Error while loading KTGTypeWordTextData.");
            return false;
        }
    }

    /// <summary>
    /// Removes the 4 allowed punctuation marks (!, ?, ', -) while preserving spaces.
    /// </summary>
    public static string StripPunctuation(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c is not ('!' or '?' or '\'' or '-'))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public static WordEntry? FindMatch(string promptText)
    {
        if (string.IsNullOrWhiteSpace(promptText))
        {
            return null;
        }

        var normalized = promptText.Trim().ToLowerInvariant();
        if (ByExactMatch.TryGetValue(normalized, out var exact))
        {
            return exact;
        }

        var stripped = StripPunctuation(normalized);
        if (ByStrippedMatch.TryGetValue(stripped, out var strippedMatch))
        {
            return strippedMatch;
        }

        // Fuzzy match for partially obscured words (e.g. asterisks or missing characters)
        if (normalized.Contains('*') || normalized.Contains('?'))
        {
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(stripped).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return Entries.FirstOrDefault(e => regex.IsMatch(e.Stripped));
        }

        return null;
    }
}

