using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AutoKeyboundBrawler.Data;

public static class KTGLocalizationRegistry
{
    // ------------------------------------------------------------------------
    // 1. NPC Identification (Keybound Registrar)
    // ------------------------------------------------------------------------
    public const uint KeyboundRegistrarDataId = 1058751;

    public static readonly string[] LocalizedRegistrarNames =
    [
        "Keybound registrar",       // English
        "参加受付",                  // Japanese
        "Rezeptionistin",           // German
        "préposée aux inscriptions" // French
    ];

    public static bool IsRegistrar(uint dataId, string? name = null)
    {
        if (dataId == KeyboundRegistrarDataId)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var regName in LocalizedRegistrarNames)
        {
            if (name.Contains(regName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsRegistrar(IGameObject? obj)
    {
        if (obj == null) return false;
        if (obj.BaseId == KeyboundRegistrarDataId) return true;
        return IsRegistrar(obj.BaseId, obj.Name.TextValue);
    }

    /// <summary>
    /// Searches for Keybound Registrar near the local player.
    /// Prioritizes current target (fast path), then enumerates game objects.
    /// Only allocates strings for objects whose BaseId doesn't match the known DataId.
    /// </summary>
    public static IGameObject? FindNearbyRegistrar(float maxDistance, out float distance)
    {
        distance = float.MaxValue;
        if (Svc.Objects == null) return null;
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null) return null;

        var playerPos = localPlayer.Position;
        IGameObject? closestRegistrar = null;
        var minDistance = float.MaxValue;

        // Fast path: Current target is already Keybound Registrar and within interact range
        var target = Svc.Targets?.Target;
        if (target != null && IsRegistrar(target))
        {
            var d = Vector3.Distance(playerPos, target.Position);
            if (d <= 7.0f)
            {
                distance = d;
                return target;
            }

            if (d <= maxDistance)
            {
                minDistance = d;
                closestRegistrar = target;
            }
        }

        // Full enumeration to find closest registrar
        foreach (var obj in Svc.Objects)
        {
            if (obj == null) continue;

            if (IsRegistrar(obj))
            {
                var d = Vector3.Distance(playerPos, obj.Position);
                if (d < minDistance && d <= maxDistance)
                {
                    minDistance = d;
                    closestRegistrar = obj;
                    if (minDistance <= 2.0f) break;
                }
            }
        }

        distance = minDistance;
        return closestRegistrar;
    }

    // ------------------------------------------------------------------------
    // 2. Halt Avoidance Spells (Dynamic Sensory Jammer & Cursed Gaze)
    // ------------------------------------------------------------------------
    public const uint DynamicSensoryJammerActionId = 49702;
    public const uint DynamicSensoryJammerKeyboundActionId = 49532;
    public const uint CursedGazeActionId = 49529;

    public static readonly FrozenSet<uint> HaltActionIds = new HashSet<uint>
    {
        DynamicSensoryJammerActionId,
        DynamicSensoryJammerKeyboundActionId,
        CursedGazeActionId,
        21396, 17407, 8342, 8341, 7354, 7353, 32448, 512
    }.ToFrozenSet();

    public static readonly string[] LocalizedHaltSpells =
    [
        // English
        "Dynamic Sensory Jammer",
        "Cursed Gaze",
        // Japanese
        "動体感知ジャマー",
        "呪いの視線",
        // German
        "Dynamischer Störsender",
        "Fluchblick",
        // French
        "Brouilleur sensoriel",
        "Regard maudit"
    ];

    public static bool IsHaltSpell(string? spellName, uint actionId = 0)
    {
        if (actionId > 0 && HaltActionIds.Contains(actionId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(spellName))
        {
            return false;
        }

        foreach (var haltName in LocalizedHaltSpells)
        {
            if (spellName.Contains(haltName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------------------
    // 3. Inputs Notification Banners (Addon Sheet Rows 17839 & 17840)
    // ------------------------------------------------------------------------
    public const uint InputsDisabledAddonRowId = 17839;
    public const uint InputsReEnabledAddonRowId = 17840;

    public static readonly string[] LocalizedInputsDisabledBanners =
    [
        "Inputs disabled",
        "が付与された",
        "Schreibblockade verursacht",
        "Impossible de saisir du texte"
    ];

    public static readonly string[] LocalizedInputsReEnabledBanners =
    [
        "Inputs re-enabled",
        "が切れた",
        "Schreibblockade aufgehoben",
        "s'est dissipé"
    ];

    public static bool MatchesInputsDisabled(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return LocalizedInputsDisabledBanners.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    public static bool MatchesInputsReEnabled(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return LocalizedInputsReEnabledBanners.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------
    // 4. Needles Spells & Cactuar Identification
    // ------------------------------------------------------------------------
    public static readonly FrozenSet<uint> CactuarRowIds = new HashSet<uint>
    {
        501,  // サボテンダー / Cactuar
        841,  // ゴールドサボテンダー / Gold Cactuar
        844,  // コルテンダー
        845,  // ジャボテンダー / Gigantender
        950,  // サボテンダー・カーニバル / Cactuar Carnival
        987,  // サボテン・ノック / Cactuar Knock
        991,  // ハッピーテンダーチャレンジ / Happy Tender Challenge
        1224, // 金の針
        1453, // 針千本 / 1,000 Needles
        1454, // サボテンばくだん
        1455, // サボテンまばりあ
        1728  // サボテンダー千本ノッカー
    }.ToFrozenSet();

    public static readonly FrozenSet<uint> NeedlesActionIds = new HashSet<uint>
    {
        49524, // 針？本 (Cast 7.0s, Keybound Brawler)
        48580, // 針？本 (Cast 8.0s, Keybound Brawler)
        48581, // 針？本 (Cast 8.0s, Keybound Brawler)
        39023, // Needles (Dawntrail minigame)
        501, 502, 503, 2678, 2679, 2680, 3104, 3105, 3106, 10375, 10730, 10731, 10876, 11397, 15087
    }.ToFrozenSet();

    public static readonly string[] LocalizedNeedlesSpellRoots =
    [
        // English
        "needle",           // ??? Needles, 1,000 Needles, etc.
        "??? needles",
        // Japanese
        "針？本",           // Fullwidth question mark (Row 49524, 48580, 48581)
        "針?本",            // ASCII question mark
        "針千本",
        "針万本",
        "針幾万本",
        "針",               // Kanji root covering needle actions
        "はり",             // Hiragana root
        // German
        "stachel",          // ???-Stacheln
        "nadel",            // 1.000 Nadeln, ??? Nadeln
        // French
        "épine",            // ??? Épines, 1 000 épines
        "epine",            // unaccented fallback
        "aiguille"          // 1000 aiguilles
    ];

    public static readonly string[] LocalizedCactuarRoots =
    [
        // English
        "cactuar",
        "gigantender",
        "sabotender",
        "tender",
        // Japanese
        "サボテン",          // Matches サボテンダー, サボテン・ノック, サボテンばくだん, サボテンまばりあ
        "さぼてん",
        "テンダー",          // Matches コルテンダー, ジャボテンダー, ハッピーテンダーチャレンジ
        "てんだー",
        "ジャボテン",
        "じゃぼてん",
        "針千本",
        // German
        "kaktor",
        // French
        "pampa"
    ];

    public static bool IsNeedlesSpell(string? spellName, uint actionId = 0)
    {
        if (actionId > 0 && NeedlesActionIds.Contains(actionId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(spellName)) return false;

        return LocalizedNeedlesSpellRoots.Any(root => spellName.Contains(root, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsCactuarPrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return false;
        return LocalizedCactuarRoots.Any(root => prompt.Contains(root, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------
    // 5. Defensive Sentences (Shield prompts)
    // ------------------------------------------------------------------------
    public static readonly string[] LocalizedDefenseKeywords =
    [
        // English
        "defense", "defend", "shield", "protect",
        // German
        "verteidigung", "schild", "schützen",
        // French
        "défense", "bouclier", "protéger",
        // Japanese
        "防御", "守護", "シールド"
    ];

    public static bool ContainsDefenseKeyword(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        return LocalizedDefenseKeywords.Any(kw => word.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------
    // 6. Boss Ultimate Attacks (Demand Immediate Player Defense Shield)
    // ------------------------------------------------------------------------
    public static readonly FrozenSet<uint> BossUltimateActionIds = new HashSet<uint>
    {
        49538, // メインカノン / Main Cannon (Cast 12.0s)
        49699, // バスターキャノン / Tankbuster (Cast 30.0s)
        49555, // エクリプスメテオ / Ecliptic Meteor (Cast 30.0s)
        49703, // ビッグボンバー / Big Bomber (Cast 12.0s)
        49554, // ミールストーム / Charybdis (Cast 12.0s)
        49677, // ダンスマカブル / Sword Danse Macabre (Cast 12.0s)
        49749, // ファーストソング / The First Song (Cast 10.0s)
        49750  // ラストソング / The Last Song (Cast 10.0s)
    }.ToFrozenSet();

    public static readonly string[] LocalizedBossUltimateSpellRoots =
    [
        "メインカノン", "main cannon",
        "バスターキャノン", "tankbuster",
        "エクリプスメテオ", "ecliptic meteor",
        "ビッグボンバー", "big bomber",
        "ミールストーム", "charybdis",
        "ダンスマカブル", "danse macabre",
        "ファーストソング", "the first song",
        "ラストソング", "the last song"
    ];

    public static bool IsBossUltimateCast(string? spellName, uint actionId = 0)
    {
        if (actionId > 0 && BossUltimateActionIds.Contains(actionId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(spellName)) return false;

        return LocalizedBossUltimateSpellRoots.Any(root => spellName.Contains(root, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------
    // 7. Registration Confirmation Button
    // ------------------------------------------------------------------------
    public static readonly string[] LocalizedRegisterButtonTexts =
    [
        "Register",     // English
        "参加登録",      // Japanese
        "登録",          // Japanese
        "Anmelden",     // German
        "S'inscrire"    // French
    ];

    public static bool IsRegisterButtonText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return LocalizedRegisterButtonTexts.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------
    // 8. Duty Finder Commence Button
    // ------------------------------------------------------------------------
    public static readonly string[] LocalizedCommenceButtonTexts =
    [
        "Commence",     // English
        "突入",          // Japanese
        "Teilnehmen",   // German
        "Commencer"     // French
    ];

    public static bool IsCommenceButtonText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return LocalizedCommenceButtonTexts.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------
    // 9. Results Screen Close Button
    // ------------------------------------------------------------------------
    public static readonly string[] LocalizedResultCloseButtonTexts =
    [
        "Exit", "Leave", "Return", "Close", "OK", // English
        "退出", "閉じる", "戻る",                     // Japanese
        "Verlassen", "Beenden", "Schließen",       // German
        "Quitter", "Fermer"                        // French
    ];

    public static bool IsResultCloseButtonText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return LocalizedResultCloseButtonTexts.Any(b => text.Contains(b, StringComparison.OrdinalIgnoreCase));
    }
}
