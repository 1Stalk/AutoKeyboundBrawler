using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    public static bool IsLoaded { get; private set; }
    public static ClientLanguage CurrentLanguage { get; private set; } = ClientLanguage.English;

    private static readonly List<WordEntry> Entries = new();
    private static readonly ConcurrentDictionary<uint, WordEntry> ByRowId = new();
    private static readonly ConcurrentDictionary<string, WordEntry> ByExactMatch = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, WordEntry> ByStrippedMatch = new(StringComparer.OrdinalIgnoreCase);

    public static bool Load(IDataManager? data, IPluginLog? log = null)
    {
        if (data == null) return false;

        var targetLang = data.Language;

        try
        {
            var sheet = data.GetExcelSheet<KTGTypeWordTextData>(targetLang);
            if (sheet == null)
            {
                log?.Error($"[KTGWordDatabase] Failed to open KTGTypeWordTextData sheet for language {targetLang}.");
                return false;
            }

            // Retrieve English sheet for cross-language heuristics
            var englishSheet = targetLang != ClientLanguage.English
                ? data.GetExcelSheet<KTGTypeWordTextData>(ClientLanguage.English)
                : sheet;

            Entries.Clear();
            ByRowId.Clear();
            ByExactMatch.Clear();
            ByStrippedMatch.Clear();

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

                var isCactuar = KTGLocalizationRegistry.CactuarRowIds.Contains(row.RowId) ||
                                KTGLocalizationRegistry.IsCactuarPrompt(text);

                // Compute defense flag strictly to avoid false positives on normal wave mobs
                var isDefense = false;
                if (!isCactuar)
                {
                    var enText = string.Empty;
                    if (englishSheet != null)
                    {
                        try
                        {
                            var enRow = englishSheet.GetRow(row.RowId);
                            enText = enRow.Unknown0.ExtractText()?.Trim() ?? string.Empty;
                        }
                        catch
                        {
                            // English row fallback
                        }
                    }

                    isDefense = DetermineIsDefense(row.RowId, text, enText);
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

                // Index wave dash (U+301C) alternate if fullwidth tilde (U+FF5E) is present
                if (normalized.Contains('\uFF5E'))
                {
                    ByExactMatch.TryAdd(normalized.Replace('\uFF5E', '\u301C'), entry);
                }

                if (!string.IsNullOrWhiteSpace(reading))
                {
                    var normReading = reading.ToLowerInvariant();
                    ByExactMatch.TryAdd(normReading, entry);
                    ByStrippedMatch.TryAdd(StripPunctuation(normReading), entry);
                    if (normReading.Contains('\uFF5E'))
                    {
                        ByExactMatch.TryAdd(normReading.Replace('\uFF5E', '\u301C'), entry);
                    }
                }
            }

            CurrentLanguage = targetLang;
            IsLoaded = true;
            log?.Information($"[KTGWordDatabase] Successfully loaded {Entries.Count} words for language {CurrentLanguage} (Defense: {Entries.Count(e => e.IsDefense)}).");
            return true;
        }
        catch (Exception ex)
        {
            log?.Error(ex, $"[KTGWordDatabase] Error while loading KTGTypeWordTextData for language {targetLang}.");
            return false;
        }
    }

    private static bool DetermineIsDefense(uint rowId, string localText, string enText)
    {
        var lowerLocal = localText.ToLowerInvariant();
        var lowerEn = enText.ToLowerInvariant();
        var enWords = enText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Magic pot items are enemy requests, never player defense
        if (localText.EndsWith("ちょうだい！") || lowerEn.StartsWith("gimme "))
        {
            return false;
        }

        // Mobs / enemies with barrier in name
        if (localText is "バリア" or "マバリア" or "デバリア" || lowerEn is "barrier" or "manaward" or "breach")
        {
            return false;
        }

        // Explicit defense skills / shield prompts across all languages
        if (lowerEn.Contains("defense") || lowerEn.Contains("defend") || lowerEn.Contains("shield") ||
            localText.Contains("防御") || localText.Contains("守護") || localText.Contains("シールド") ||
            localText.Contains("光の盾") || localText.Contains("ダブルガード") || localText.Contains("ジャストガード") ||
            localText.Contains("おまもりチアー") || localText.Contains("Lustrous Shield") || localText.Contains("Iron Defense"))
        {
            return true;
        }

        // Full quote sentences synchronized by row
        if (enWords.Length >= 4 && (enText.EndsWith('.') || enText.EndsWith('!') || enText.EndsWith('?') || enText.EndsWith("...")))
        {
            return true;
        }

        // Local Japanese quote check: must have Japanese quote brackets
        if (localText.Contains('「') && localText.Contains('」'))
        {
            return true;
        }

        // Known boss defense quotes in Japanese
        if (localText is "我が剣のサビとなれ！" or "命は星を巡る" or "マタ・タビ" or "星の輝きを力に！")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the given word is a known Cactuar / Sabotender name.
    /// </summary>
    public static bool DetermineIsCactuar(string text, uint rowId)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = StripPunctuation(text);
        if (clean.Contains("サボテンダー", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("サボテン", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("Cactuar", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("Sabotender", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("Kaktor", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("Pampa", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes punctuation marks (!, ?, ', -, and Japanese punctuation ！, ？, 、, 。, 「, 」, ・, ～, 〜) while preserving words.
    /// In French typography, exclamation and question marks are preceded by a space (e.g. "tous !").
    /// Stripping the punctuation also removes any trailing space before it and trims the result.
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
            if (c == ' ' && i + 1 < input.Length && input[i + 1] is '!' or '?' or '！' or '？')
            {
                continue;
            }

            if (c is not ('!' or '?' or '\'' or '-' or '！' or '？' or '、' or '。' or '「' or '」' or '・' or '～' or '〜'))
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

        // Normalize wave dash (U+301C) to fullwidth tilde (U+FF5E)
        var waveNormalized = normalized.Replace('\u301C', '\uFF5E');
        if (ByExactMatch.TryGetValue(waveNormalized, out var waveMatch))
        {
            return waveMatch;
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

        // Suffix/prefix match for quotes obscured at head or tail (minimum 8 characters)
        if (stripped.Length >= 8)
        {
            var match = Entries.FirstOrDefault(e => e.IsDefense && (e.Stripped.EndsWith(stripped) || e.Stripped.StartsWith(stripped)));
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
