using Dalamud.Game;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
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
    public string Reading { get; init; } = string.Empty;
    public bool IsDefense { get; init; }
    public bool IsCactuar { get; init; }
}

public static class KTGWordDatabase
{
    private static readonly List<WordEntry> Entries = [];
    private static readonly Dictionary<string, WordEntry> ByExactMatch = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, WordEntry> ByStrippedMatch = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, WordEntry> ByRowId = [];

    public static bool IsLoaded { get; private set; }
    public static int Count => Entries.Count;
    public static ClientLanguage CurrentLanguage { get; private set; } = ClientLanguage.English;

    public static bool Load(IDataManager dataManager, IPluginLog? log = null, ClientLanguage? forcedLanguage = null)
    {
        var targetLang = forcedLanguage ?? Svc.ClientState.ClientLanguage;
        if (IsLoaded && Entries.Count > 0 && CurrentLanguage == targetLang)
        {
            return true;
        }

        try
        {
            // 1. Get sheet for client's active language
            var sheet = dataManager.GetExcelSheet<KTGTypeWordTextData>(targetLang) ??
                        dataManager.GetExcelSheet<KTGTypeWordTextData>();

            if (sheet == null)
            {
                log?.Error($"[KTGWordDatabase] Failed to open Excel sheet KTGTypeWordTextData for language {targetLang}.");
                return false;
            }

            // 2. Also open English sheet to reliably establish defense and cactuar flags across all languages
            var englishSheet = dataManager.GetExcelSheet<KTGTypeWordTextData>(ClientLanguage.English);

            Entries.Clear();
            ByExactMatch.Clear();
            ByStrippedMatch.Clear();
            ByRowId.Clear();

            foreach (var row in sheet)
            {
                var text = row.Unknown0.ExtractText()?.Trim() ?? string.Empty;
                var reading = row.Unknown1.ExtractText()?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(reading))
                {
                    continue;
                }

                // If text is blank but reading exists (common in Japanese furigana), use reading as text
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = reading;
                }

                var normalized = text.ToLowerInvariant();
                var stripped = StripPunctuation(normalized);

                // Compute defense flag based on English sheet metadata
                var isDefense = false;
                var isCactuar = KTGLocalizationRegistry.CactuarRowIds.Contains(row.RowId) ||
                                KTGLocalizationRegistry.IsCactuarPrompt(text);

                if (englishSheet != null)
                {
                    try
                    {
                        var enRow = englishSheet.GetRow(row.RowId);
                        var enText = enRow.Unknown0.ExtractText()?.Trim() ?? string.Empty;
                        var lowerEn = enText.ToLowerInvariant();
                        var enWords = enText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        if (lowerEn.Contains("defense") ||
                            lowerEn.Contains("defend") ||
                            lowerEn.Contains("barrier") ||
                            lowerEn.Contains("shield") ||
                            lowerEn.Contains("protect") ||
                            enWords.Length >= 3)
                        {
                            isDefense = true;
                        }

                        if (!isCactuar && (lowerEn.Contains("cactuar") || lowerEn.Contains("needle")))
                        {
                            isCactuar = true;
                        }
                    }
                    catch
                    {
                        // Fallback to local text check
                        isDefense = KTGLocalizationRegistry.ContainsDefenseKeyword(text);
                    }
                }
                else
                {
                    isDefense = KTGLocalizationRegistry.ContainsDefenseKeyword(text);
                }

                var entry = new WordEntry
                {
                    RowId = row.RowId,
                    Original = text,
                    Normalized = normalized,
                    Stripped = stripped,
                    Reading = reading,
                    IsDefense = isDefense,
                    IsCactuar = isCactuar
                };

                Entries.Add(entry);
                ByRowId.TryAdd(entry.RowId, entry);
                ByExactMatch.TryAdd(normalized, entry);
                ByStrippedMatch.TryAdd(stripped, entry);

                if (!string.IsNullOrWhiteSpace(reading))
                {
                    ByExactMatch.TryAdd(reading.ToLowerInvariant(), entry);
                    ByStrippedMatch.TryAdd(StripPunctuation(reading.ToLowerInvariant()), entry);
                }
            }

            CurrentLanguage = targetLang;
            IsLoaded = true;
            log?.Information($"[KTGWordDatabase] Successfully loaded {Entries.Count} words for language {CurrentLanguage}.");
            return true;
        }
        catch (Exception ex)
        {
            log?.Error(ex, $"[KTGWordDatabase] Error while loading KTGTypeWordTextData for language {targetLang}.");
            return false;
        }
    }

    public static WordEntry? GetByRowId(uint rowId)
    {
        return ByRowId.TryGetValue(rowId, out var entry) ? entry : null;
    }

    /// <summary>
    /// Removes the 4 allowed punctuation marks (!, ?, ', -) while preserving spaces.
    /// In French typography, exclamation and question marks are preceded by a space (e.g. "tous !").
    /// Stripping the punctuation also removes any trailing space before it and trims the result,
    /// preventing a phantom trailing space from being typed at the end of the sentence.
    /// </summary>
    public static string StripPunctuation(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new System.Text.StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            // If a space is immediately followed by a punctuation mark being stripped (common in French: " !"),
            // skip the space as well so it doesn't become a dangling trailing space or double space.
            if (c == ' ' && i + 1 < input.Length && input[i + 1] is '!' or '?')
            {
                continue;
            }

            if (c is not ('!' or '?' or '\'' or '-'))
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Trim();
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
