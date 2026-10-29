using System;
using System.Collections.Generic;
using System.Linq;

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

    public static bool IsRegistrar(uint dataId, string name)
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

    // ------------------------------------------------------------------------
    // 2. Halt Avoidance Spells (Dynamic Sensory Jammer & Cursed Gaze)
    // ------------------------------------------------------------------------
    public const uint DynamicSensoryJammerActionId = 49702;
    public const uint CursedGazeActionId = 49529;

    public static readonly uint[] HaltActionIds =
    [
        DynamicSensoryJammerActionId,
        CursedGazeActionId,
        21396, 17407, 8342, 8341, 7354, 7353, 32448, 512
    ];

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
    public static readonly uint[] CactuarRowIds =
    [
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
    ];

    public static readonly uint[] NeedlesActionIds =
    [
        49524, // 針？本 (Cast 7.0s, Keybound Brawler)
        48580, // 針？本 (Cast 8.0s, Keybound Brawler)
        48581, // 針？本 (Cast 8.0s, Keybound Brawler)
        39023, // Needles (Dawntrail minigame)
        501, 502, 503, 2678, 2679, 2680, 3104, 3105, 3106, 10375, 10730, 10731, 10876, 11397, 15087
    ];

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
        "defense", "defend", "barrier", "shield", "protect",
        // German
        "verteidigung", "schild", "barriere", "schützen",
        // French
        "défense", "bouclier", "barrière", "protéger",
        // Japanese
        "防", "守", "バリア", "シールド"
    ];

    public static bool ContainsDefenseKeyword(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        return LocalizedDefenseKeywords.Any(kw => word.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------
    // 6. Registration Confirmation Button
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
}

