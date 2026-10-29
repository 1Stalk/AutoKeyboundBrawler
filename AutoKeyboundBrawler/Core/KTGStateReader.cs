using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.Interop;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoKeyboundBrawler.Core;

public enum EventPhase
{
    None,
    InSquare,
    InQueue,
    Registering,
    Commencing,
    InMinigame,
    Results
}

public class CachedTarget
{
    public int SlotId { get; init; }
    public string Word { get; init; } = string.Empty;
    public float ScreenY { get; set; }
    public float ScreenX { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

public static unsafe class KTGStateReader
{
    private static readonly Dictionary<int, CachedTarget> _targetCache = new();

    /// <summary>
    /// Checks if the player is currently in a Duty Finder / Content matching queue.
    /// Evaluates native ContentsFinder queue state and timestamps, as well as Dalamud condition flags.
    /// </summary>
    public static bool IsQueuedForDuty()
    {
        // 1. Check native FFXIV ContentsFinder Queue State
        try
        {
            var cf = ContentsFinder.Instance();
            if (cf != null)
            {
                var state = cf->QueueInfo.QueueState;
                if (state is ContentsFinderQueueState.Pending
                         or ContentsFinderQueueState.Queued
                         or ContentsFinderQueueState.Ready
                         or ContentsFinderQueueState.Accepted)
                {
                    return true;
                }

                if (cf->QueueInfo.EnteredQueueTimestamp > 0 &&
                    state != ContentsFinderQueueState.None &&
                    state != ContentsFinderQueueState.InContent)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Safety fallback if native struct access throws
        }

        // 2. Check Dalamud Condition flags
        if (Svc.Condition[ConditionFlag.InDutyQueue] ||
            Svc.Condition[ConditionFlag.WaitingForDuty] ||
            Svc.Condition[ConditionFlag.WaitingForDutyFinder] ||
            Svc.Condition[ConditionFlag.WaitingForRaceOrMatch] ||
            Svc.Condition[ConditionFlag.RegisteringForRaceOrMatch] ||
            Svc.Condition[ConditionFlag.WaitingForTripleTriadMatch] ||
            Svc.Condition[ConditionFlag.WaitingForTripleTriadMatch83] ||
            Svc.Condition[ConditionFlag.RegisteringForTripleTriadMatch])
        {
            return true;
        }

        return false;
    }

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

        if (IsQueuedForDuty())
        {
            return EventPhase.InQueue;
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
    /// - SlotId == 2 is STRICTLY the Boss enemy. The Boss can NEVER be a player defensive prompt.
    /// - SlotId >= 20000 or screenY >= 680f: Dedicated player defensive prompt in KTGEWord.
    /// - Checks KTGWordDatabase cross-language defense flag (1:1 row sync with English sheet).
    /// - Localized defense keywords via KTGLocalizationRegistry.
    /// - Japanese sentence punctuation (、, 。, 「, 」) or Japanese quote length.
    /// - Full sentence structure (3 or more words for EN, DE, FR).
    /// Never matches single-word or 2-word adds like "whisper", "Tifa", "SOLDIER", "Cloud Jr.".
    /// </summary>
    public static bool IsDefenseSentence(string word, int slotId = -1, float screenY = 0f)
    {
        // Slot 2 in KTGEWord is strictly the Boss enemy. The Boss can NEVER be a player defensive prompt!
        if (slotId == 2)
        {
            return false;
        }

        // Dedicated player defensive prompt in KTGEWord UI is positioned at the bottom of the screen (ScreenY >= 700)
        if (screenY >= 680f)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(word)) return false;

        // 1. Check loaded database entry (which resolved defense cross-language via English sheet row matching)
        var match = KTGWordDatabase.FindMatch(word);
        if (match != null && match.IsDefense)
        {
            return true;
        }

        // 2. Explicit defense keywords via KTGLocalizationRegistry (EN, DE, FR, JA)
        if (KTGLocalizationRegistry.ContainsDefenseKeyword(word))
        {
            return true;
        }

        // 3. Japanese sentence punctuation (comma, period, quotes)
        if (word.Contains('、') || word.Contains('。') || word.Contains('「') || word.Contains('」'))
        {
            return true;
        }

        // 4. Full quote / sentence structure (3 or more words for space-separated languages)
        var words = word.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 3)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a prompt word corresponds to a Cactuar / Sabotender mob.
    /// Supports all client localizations via KTGWordDatabase and KTGLocalizationRegistry.
    /// </summary>
    public static bool IsCactuarWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;

        var match = KTGWordDatabase.FindMatch(word);
        if (match != null && match.IsCactuar)
        {
            return true;
        }

        return KTGLocalizationRegistry.IsCactuarPrompt(word);
    }

    /// <summary>
    /// Checks if an active defensive prompt is currently visible on screen.
    /// Excludes recently submitted defense words to prevent false preemption while shield animations linger.
    /// </summary>
    public static bool IsSameWord(string? wordA, string? wordB)
    {
        if (string.IsNullOrWhiteSpace(wordA) || string.IsNullOrWhiteSpace(wordB)) return false;
        if (string.Equals(wordA.Trim(), wordB.Trim(), StringComparison.OrdinalIgnoreCase)) return true;

        var cleanA = KTGWordDatabase.StripPunctuation(wordA).ToLowerInvariant();
        var cleanB = KTGWordDatabase.StripPunctuation(wordB).ToLowerInvariant();
        if (string.Equals(cleanA, cleanB, StringComparison.OrdinalIgnoreCase)) return true;

        var matchA = KTGWordDatabase.FindMatch(wordA);
        var matchB = KTGWordDatabase.FindMatch(wordB);
        if (matchA != null && matchB != null && matchA.RowId == matchB.RowId) return true;

        return false;
    }

    /// <summary>
    /// Checks if an active defensive prompt is currently visible on screen.
    /// Excludes recently submitted defense words for 4.5 seconds while shield animations play
    /// to prevent false preemption loops.
    /// </summary>
    public static bool HasActiveDefenseSentenceOnScreen(out KTGTargetWord? defenseTarget, string? recentlySubmittedWord = null, double recentSubmitAgeMs = 9999)
    {
        var targets = AddonKTGEWord.ReadActiveTargets();
        defenseTarget = targets.FirstOrDefault(t => t.SlotId != 2 &&
            IsDefenseSentence(t.Word, t.SlotId, t.ScreenY) &&
            !(recentSubmitAgeMs < 4500 && IsSameWord(t.Word, recentlySubmittedWord)));
        if (defenseTarget != null) return true;

        var cutoff = DateTime.UtcNow.AddSeconds(-4.0);
        var cached = _targetCache.Values
            .FirstOrDefault(c => c.SlotId != 2 && c.LastSeenUtc >= cutoff &&
                IsDefenseSentence(c.Word, c.SlotId, c.ScreenY) &&
                !(recentSubmitAgeMs < 4500 && IsSameWord(c.Word, recentlySubmittedWord)));
        if (cached != null)
        {
            defenseTarget = new KTGTargetWord(cached.SlotId, cached.Word, cached.ScreenY, cached.ScreenX);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if an active Cactuar casting "Needles" (or with Cactuar phrase) is currently visible on screen.
    /// </summary>
    public static bool HasActiveNeedlesTargetOnScreen(out KTGTargetWord? needlesTarget)
    {
        var isNeedlesCasting = AddonKTGEnemyCastBar.HasActiveNeedlesCast(out var needlesCast);
        var targets = AddonKTGEWord.ReadActiveTargets();

        // 1. If Needles is actively being cast (verified via Svc.Objects or UI castbar)
        if (isNeedlesCasting)
        {
            // Highest confidence: Live add on screen whose word is a Cactuar word
            needlesTarget = targets.FirstOrDefault(t => t.SlotId != 2 && IsCactuarWord(t.Word));
            if (needlesTarget != null) return true;

            // Direct slot match from castbar
            if (needlesCast != null)
            {
                needlesTarget = targets.FirstOrDefault(t => t.SlotId != 2 && t.SlotId == needlesCast.SlotId);
                if (needlesTarget != null) return true;
            }

            // Fallback: If only 1 non-boss add exists while Needles is casting, that add is the Cactuar
            var nonBossTargets = targets.Where(t => t.SlotId != 2 && !IsDefenseSentence(t.Word, t.SlotId, t.ScreenY)).ToList();
            if (nonBossTargets.Count == 1)
            {
                needlesTarget = nonBossTargets[0];
                return true;
            }

            // Fallback to cached memory if briefly obscured
            var cutoff = DateTime.UtcNow.AddSeconds(-4.0);
            var cached = _targetCache.Values
                .FirstOrDefault(c => c.SlotId != 2 && c.LastSeenUtc >= cutoff && IsCactuarWord(c.Word));
            if (cached != null)
            {
                needlesTarget = new KTGTargetWord(cached.SlotId, cached.Word, cached.ScreenY, cached.ScreenX);
                return true;
            }
        }

        // 2. Direct slot match from castbar even if Svc.Objects did not catch it
        var casts = AddonKTGEnemyCastBar.ReadActiveCasts();
        var needleSlots = casts
            .Where(c => AddonKTGEnemyCastBar.IsNeedlesSpell(c.SpellName))
            .Select(c => c.SlotId)
            .ToHashSet();

        needlesTarget = targets.FirstOrDefault(t => t.SlotId != 2 && needleSlots.Contains(t.SlotId));
        if (needlesTarget != null) return true;

        // 3. Target whose word matches Cactuar and is actively casting any spell
        var castingSlots = casts.Select(c => c.SlotId).ToHashSet();
        needlesTarget = targets.FirstOrDefault(t => t.SlotId != 2 && castingSlots.Contains(t.SlotId) && IsCactuarWord(t.Word));
        if (needlesTarget != null) return true;

        needlesTarget = null;
        return false;
    }

    /// <summary>
    /// Checks if active adds (smaller mobs) are present on screen alongside a boss (slot 2).
    /// </summary>
    public static bool HasActiveAddsOnScreen()
    {
        var targets = AddonKTGEWord.ReadActiveTargets();
        var hasBoss = targets.Any(t => t.SlotId == 2);
        return hasBoss && targets.Any(t => t.SlotId != 2 && !IsDefenseSentence(t.Word, t.SlotId, t.ScreenY));
    }

    /// <summary>
    /// Computes multi-tier target priority:
    /// Tier 0: Defensive Sentence (urgent shield before strong attack - strictly non-boss slot only!)
    /// Tier 1: Cactuar casting Needles (EMERGENCY INTERRUPT - kill cactuar first!)
    /// Tier 2: Other casting adds (interrupt explosion/skills e.g. Burst)
    /// Tier 3: Cactuar mob idle/spawning on screen
    /// Tier 4: Regular Adds (with boss on screen)
    /// Tier 5: Standard wave regular enemies
    /// Tier 6: Casting Boss
    /// Tier 7: Idle Boss
    /// </summary>
    public static int GetTargetPriority(
        KTGTargetWord target,
        bool hasBossOnScreen,
        HashSet<int> needleSlotIds,
        HashSet<int> otherCastingSlotIds,
        bool isAnyNeedlesCasting,
        Configuration config)
    {
        // Tier 0: Defensive Sentence (strictly non-boss slots only!)
        if (config.PrioritizeDefense && target.SlotId != 2 && IsDefenseSentence(target.Word, target.SlotId, target.ScreenY))
        {
            return 0;
        }

        // Tier 1: Cactuar casting "??? Needles" / "針？本" (EMERGENCY INTERRUPT - highest kill priority!)
        if (target.SlotId != 2)
        {
            if (needleSlotIds.Contains(target.SlotId) ||
                (isAnyNeedlesCasting && IsCactuarWord(target.Word)) ||
                (otherCastingSlotIds.Contains(target.SlotId) && IsCactuarWord(target.Word)))
            {
                return 1;
            }
        }

        // Tier 2: Other casting adds (e.g. Burst)
        if (target.SlotId != 2 && otherCastingSlotIds.Contains(target.SlotId))
        {
            return 2;
        }

        // Tier 3: Cactuar mob on screen (even before cast starts)
        if (target.SlotId != 2 && IsCactuarWord(target.Word))
        {
            return 3;
        }

        // When a Boss is present on screen (Slot 2)
        if (hasBossOnScreen)
        {
            if (target.SlotId != 2) // It's a Regular Add
            {
                return config.PrioritizeAdds ? 4 : 5;
            }
            else // It's the Boss (Slot 2)
            {
                return otherCastingSlotIds.Contains(2) || needleSlotIds.Contains(2) ? 6 : 7;
            }
        }

        // Standard wave (no boss on screen)
        if (target.SlotId == 2)
        {
            return otherCastingSlotIds.Contains(2) || needleSlotIds.Contains(2) ? 6 : 7;
        }

        return 5; // Standard wave regular enemy
    }

    /// <summary>
    /// Selects the best active target word currently verified on screen using
    /// priority hierarchy: Defense Sentences > Cactuar Needles > Other Casting Adds > Regular Adds > Boss.
    /// When selecting among regular mobs/adds, prioritizes the lowest enemy on screen
    /// (closest to the players' wall).
    /// If phrases are obscured/hidden by confusion debuff, falls back to short-term target memory.
    /// </summary>
    public static KTGTargetWord? GetBestTarget(
        Configuration config,
        string? recentlySubmittedWord = null,
        double recentSubmitAgeMs = 9999)
    {
        // 0. ABSOLUTE TOP PRIORITY: Defensive Sentence (Player Shield)
        // If an active, unsubmitted defense prompt is on screen, IMMEDIATELY select it before anything else!
        // Never targets boss first when a defense phrase is already on screen.
        if (config.PrioritizeDefense && HasActiveDefenseSentenceOnScreen(out var defenseTarget, recentlySubmittedWord, recentSubmitAgeMs) && defenseTarget != null)
        {
            var match = KTGWordDatabase.FindMatch(defenseTarget.Word);
            return defenseTarget with { Word = match?.Original ?? defenseTarget.Word };
        }

        // 1. ABSOLUTE SECOND PRIORITY: Cactuar casting Needles (Emergency Interrupt)
        // If a Cactuar is actively casting Needles / 針？本, IMMEDIATELY kill it before regular adds and boss!
        if (HasActiveNeedlesTargetOnScreen(out var needlesTarget) && needlesTarget != null)
        {
            var match = KTGWordDatabase.FindMatch(needlesTarget.Word);
            return needlesTarget with { Word = match?.Original ?? needlesTarget.Word };
        }

        var targets = AddonKTGEWord.ReadActiveTargets();

        // 2. If live targets are visible on screen, update our short-term memory cache
        if (targets.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var t in targets)
            {
                if (_targetCache.TryGetValue(t.SlotId, out var existing))
                {
                    existing.LastSeenUtc = now;
                    existing.ScreenY = t.ScreenY;
                    existing.ScreenX = t.ScreenX;
                    if (existing.Word != t.Word)
                    {
                        _targetCache[t.SlotId] = new CachedTarget
                        {
                            SlotId = t.SlotId,
                            Word = t.Word,
                            ScreenY = t.ScreenY,
                            ScreenX = t.ScreenX,
                            LastSeenUtc = now
                        };
                    }
                }
                else
                {
                    _targetCache[t.SlotId] = new CachedTarget
                    {
                        SlotId = t.SlotId,
                        Word = t.Word,
                        ScreenY = t.ScreenY,
                        ScreenX = t.ScreenX,
                        LastSeenUtc = now
                    };
                }
            }

            // Only expire cache entries not seen for over 5.0 seconds (prevents cache wiping during confusion debuff)
            var staleSlots = _targetCache.Where(kvp => (now - kvp.Value.LastSeenUtc).TotalSeconds > 5.0).Select(kvp => kvp.Key).ToList();
            foreach (var s in staleSlots)
            {
                _targetCache.Remove(s);
            }
        }

        // 3. Determine candidate pool
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
                .Select(c => new KTGTargetWord(c.SlotId, c.Word, c.ScreenY, c.ScreenX))
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
            var filtered = candidates.Where(t => !IsSameWord(t.Word, recentlySubmittedWord)).ToList();
            if (filtered.Count > 0)
            {
                candidates = filtered;
            }
        }

        var activeCasts = AddonKTGEnemyCastBar.ReadActiveCasts();
        var needleSlotIds = activeCasts
            .Where(c => AddonKTGEnemyCastBar.IsNeedlesSpell(c.SpellName))
            .Select(c => c.SlotId)
            .ToHashSet();
        var otherCastingSlotIds = activeCasts
            .Where(c => !AddonKTGEnemyCastBar.IsNeedlesSpell(c.SpellName))
            .Select(c => c.SlotId)
            .ToHashSet();
        var hasBossOnScreen = candidates.Any(t => t.SlotId == 2);

        // Multi-tier spatial sort:
        // 1. Priority tier: Defense Sentence (0) > Needles Cactuar (1) > Other Casting Adds (2) > Cactuar mob (3) > Regular Adds (4) / Wave Mobs (5) > Boss (6/7)
        // 2. Spatial proximity: For mobs marching towards players, target the lowest enemy on screen first
        //    (largest ScreenY = closest to the players' wall)
        // 3. Shorter word length as tie-breaker
        var isAnyNeedlesCasting = AddonKTGEnemyCastBar.HasActiveNeedlesCast(out _);
        var sorted = candidates
            .OrderBy(t => GetTargetPriority(t, hasBossOnScreen, needleSlotIds, otherCastingSlotIds, isAnyNeedlesCasting, config))
            .ThenByDescending(t => t.ScreenY)
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
