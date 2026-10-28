using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoKeyboundBrawler.Core;

public enum EventPhase
{
    None,
    InSquare,
    Registering,
    Commencing,
    InMinigame,
    Results
}

public class CachedTarget
{
    public int SlotId { get; init; }
    public string Word { get; init; } = string.Empty;
    public DateTime LastSeenUtc { get; set; }
}

public static unsafe class KTGStateReader
{
    private static readonly Dictionary<int, CachedTarget> _targetCache = new();

    public static EventPhase GetCurrentPhase()
    {
        if (AddonKTGResult.TryGet(out _))
        {
            return EventPhase.Results;
        }

        if (AddonContentsFinderConfirm.TryGet(out _))
        {
            return EventPhase.Commencing;
        }

        if (KTGSafetyGuard.IsInEventDuty)
        {
            return EventPhase.InMinigame;
        }

        if (AddonKTGEnterDialogue.TryGet(out _))
        {
            return EventPhase.Registering;
        }

        if (KTGSafetyGuard.IsInKeyboundSquare)
        {
            return EventPhase.InSquare;
        }

        return EventPhase.None;
    }

    /// <summary>
    /// Checks if a given target is a true Defensive Sentence / Player Shield Prompt:
    /// - SlotId == 2 is STRICTLY the Boss and can NEVER be a player defensive prompt.
    /// - Contains explicit defensive keywords ("defense", "defend", "barrier", "shield", "protect")
    /// - OR has a full quote/sentence structure (3 or more words, e.g. "Just doing my job. Nothing more.", "Listen to the Cries of the Planet")
    /// Never matches single-word or 2-word adds like "whisper", "Tifa", "SOLDIER", "Cloud Jr.".
    /// </summary>
    public static bool IsDefenseSentence(string word, int slotId = -1)
    {
        // Slot 2 in KTGEWord is strictly the Boss enemy. The Boss can NEVER be a player defensive prompt!
        if (slotId == 2)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(word)) return false;

        var lower = word.ToLowerInvariant();

        // 1. Explicit defense keywords (e.g. "Iron Defense", "precision defense focus", "Lustrous Shield", "Barrier")
        if (lower.Contains("defense") ||
            lower.Contains("defend") ||
            lower.Contains("barrier") ||
            lower.Contains("shield") ||
            lower.Contains("protect"))
        {
            return true;
        }

        var words = word.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // 2. Full English quote / sentence structure (3 or more words)
        // (e.g. "Just doing my job. Nothing more.", "Listen to the Cries of the Planet", "Feels like we're flying high these days!")
        if (words.Length >= 3)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if an active defensive prompt is currently visible on screen.
    /// </summary>
    public static bool HasActiveDefenseSentenceOnScreen(out KTGTargetWord? defenseTarget)
    {
        var targets = AddonKTGEWord.ReadActiveTargets();
        defenseTarget = targets.FirstOrDefault(t => t.SlotId != 2 && IsDefenseSentence(t.Word, t.SlotId));
        if (defenseTarget != null) return true;

        var cutoff = DateTime.UtcNow.AddSeconds(-4.0);
        var cached = _targetCache.Values
            .FirstOrDefault(c => c.SlotId != 2 && c.LastSeenUtc >= cutoff && IsDefenseSentence(c.Word, c.SlotId));
        if (cached != null)
        {
            defenseTarget = new KTGTargetWord(cached.SlotId, cached.Word);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if active adds (smaller mobs) are present on screen alongside a boss (slot 2).
    /// </summary>
    public static bool HasActiveAddsOnScreen()
    {
        var targets = AddonKTGEWord.ReadActiveTargets();
        var hasBoss = targets.Any(t => t.SlotId == 2);
        return hasBoss && targets.Any(t => t.SlotId != 2 && !IsDefenseSentence(t.Word, t.SlotId));
    }

    /// <summary>
    /// Computes multi-tier target priority:
    /// Tier 0: Defensive Sentence (urgent shield before strong attack - strictly non-boss slot only!)
    /// Tier 1: Casting Adds (interrupt explosion/skills e.g. Burst)
    /// Tier 2: Regular Adds (kill small mobs to clear the field and build fever)
    /// Tier 3: Standard wave regular enemies
    /// Tier 4: Casting Boss (when all adds are down)
    /// Tier 5: Idle Boss (when all adds are down)
    /// </summary>
    public static int GetTargetPriority(
        KTGTargetWord target,
        bool hasBossOnScreen,
        HashSet<int> castingSlotIds,
        Configuration config)
    {
        // Tier 0: Defensive Sentence (strictly non-boss slots only!)
        if (config.PrioritizeDefense && target.SlotId != 2 && IsDefenseSentence(target.Word, target.SlotId))
        {
            return 0;
        }

        // When a Boss is present on screen (Slot 2)
        if (hasBossOnScreen)
        {
            if (target.SlotId != 2) // It's an Add
            {
                if (config.PrioritizeAdds)
                {
                    // Tier 1: Casting Add (e.g. Burst)
                    if (castingSlotIds.Contains(target.SlotId))
                    {
                        return 1;
                    }

                    // Tier 2: Regular Add
                    return 2;
                }
                else
                {
                    return castingSlotIds.Contains(target.SlotId) ? 1 : 3;
                }
            }
            else // It's the Boss (Slot 2)
            {
                // Tier 4: Casting Boss
                if (castingSlotIds.Contains(target.SlotId))
                {
                    return 4;
                }

                // Tier 5: Idle Boss
                return 5;
            }
        }

        // Standard wave (no boss on screen)
        if (target.SlotId != 2 && castingSlotIds.Contains(target.SlotId))
        {
            return 1; // Interrupt casting enemy
        }

        if (target.SlotId == 2)
        {
            return castingSlotIds.Contains(2) ? 4 : 5;
        }

        return 3; // Standard enemy
    }

    /// <summary>
    /// Selects the best active target word currently verified on screen using
    /// priority hierarchy: Defense Sentences > Casting Adds > Regular Adds > Boss.
    /// If phrases are obscured/hidden by confusion debuff, falls back to short-term target memory.
    /// </summary>
    public static KTGTargetWord? GetBestTarget(
        Configuration config,
        string? recentlySubmittedWord = null,
        double recentSubmitAgeMs = 9999)
    {
        var targets = AddonKTGEWord.ReadActiveTargets();

        // 1. If live targets are visible on screen, update our short-term memory cache
        if (targets.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var t in targets)
            {
                if (_targetCache.TryGetValue(t.SlotId, out var existing))
                {
                    existing.LastSeenUtc = now;
                    if (existing.Word != t.Word)
                    {
                        _targetCache[t.SlotId] = new CachedTarget { SlotId = t.SlotId, Word = t.Word, LastSeenUtc = now };
                    }
                }
                else
                {
                    _targetCache[t.SlotId] = new CachedTarget { SlotId = t.SlotId, Word = t.Word, LastSeenUtc = now };
                }
            }

            // Remove cache entries for slots that are no longer in live targets
            var liveSlots = new HashSet<int>(targets.Select(t => t.SlotId));
            var staleSlots = _targetCache.Keys.Where(k => !liveSlots.Contains(k)).ToList();
            foreach (var s in staleSlots)
            {
                _targetCache.Remove(s);
            }
        }

        // 2. Determine candidate pool
        List<KTGTargetWord> candidates;
        if (targets.Count > 0)
        {
            candidates = targets;
        }
        else if (config.MemoryModeObscured)
        {
            // Words are currently hidden/obscured (e.g. confusion debuff or "Phrases partially obscured!")
            // Retrieve candidates from memory cache seen within the last 4.0 seconds
            var cutoff = DateTime.UtcNow.AddSeconds(-4.0);
            candidates = _targetCache.Values
                .Where(c => c.LastSeenUtc >= cutoff)
                .Select(c => new KTGTargetWord(c.SlotId, c.Word))
                .ToList();
        }
        else
        {
            return null;
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // Filter out recently submitted word (prevents re-targeting same mob within 1.2s)
        if (!string.IsNullOrEmpty(recentlySubmittedWord) && recentSubmitAgeMs < 1200)
        {
            var filtered = candidates.Where(t => !string.Equals(t.Word, recentlySubmittedWord, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count > 0)
            {
                candidates = filtered;
            }
        }

        var activeCasts = AddonKTGEnemyCastBar.ReadActiveCasts();
        var castingSlotIds = new HashSet<int>(activeCasts.Select(c => c.SlotId));
        var hasBossOnScreen = candidates.Any(t => t.SlotId == 2);

        // Multi-tier sort: Priority first, then shortest word length
        var sorted = candidates
            .OrderBy(t => GetTargetPriority(t, hasBossOnScreen, castingSlotIds, config))
            .ThenBy(t => t.Word.Length)
            .ToList();

        foreach (var target in sorted)
        {
            var match = KTGWordDatabase.FindMatch(target.Word);
            if (match != null)
            {
                return target with { Word = match.Original };
            }

            if (target.Word.Length >= 2)
            {
                return target;
            }
        }

        return null;
    }

    public static string? GetBestTargetWord(
        Configuration config,
        string? recentlySubmittedWord = null,
        double recentSubmitAgeMs = 9999)
    {
        var target = GetBestTarget(config, recentlySubmittedWord, recentSubmitAgeMs);
        return target?.Word;
    }

    public static void InvalidateSubmittedWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        var cleanWord = KTGWordDatabase.StripPunctuation(word).ToLowerInvariant();

        var matches = _targetCache
            .Where(kvp => string.Equals(KTGWordDatabase.StripPunctuation(kvp.Value.Word).ToLowerInvariant(), cleanWord, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in matches)
        {
            _targetCache.Remove(key);
        }
    }

    public static void ClearCache()
    {
        _targetCache.Clear();
    }
}
