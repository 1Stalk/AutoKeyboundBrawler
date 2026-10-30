using Dalamud.Game;
using ECommons.DalamudServices;

namespace AutoKeyboundBrawler.UI;

public static class Loc
{
    public static ClientLanguage CurrentLanguage
    {
        get
        {
            if (Plugin.Configuration?.SelectedLanguage.HasValue == true)
            {
                return Plugin.Configuration.SelectedLanguage.Value;
            }

            var gameLang = Svc.ClientState.ClientLanguage;
            return gameLang switch
            {
                ClientLanguage.Japanese => ClientLanguage.Japanese,
                ClientLanguage.German => ClientLanguage.German,
                ClientLanguage.French => ClientLanguage.French,
                _ => ClientLanguage.English
            };
        }
    }

    public static string GetLanguageShortName(ClientLanguage lang)
    {
        return lang switch
        {
            ClientLanguage.Japanese => "JA",
            ClientLanguage.German => "DE",
            ClientLanguage.French => "FR",
            _ => "EN"
        };
    }

    public static string Get(string key)
    {
        var lang = CurrentLanguage;
        return (key, lang) switch
        {
            // --- Toggles ---
            ("TypingSolver", ClientLanguage.Japanese) => "タイピング自動入力",
            ("TypingSolver", ClientLanguage.German) => "Tipp-Automatik",
            ("TypingSolver", ClientLanguage.French) => "Solveur de frappe",
            ("TypingSolver", _) => "Typing Solver",

            ("TypingSolverSub", ClientLanguage.Japanese) => "人間らしい入力速度・バリア検知・敵優先度",
            ("TypingSolverSub", ClientLanguage.German) => "Menschliche Tippgeschwindigkeit & Prioritäten",
            ("TypingSolverSub", ClientLanguage.French) => "Vitesse humaine, bouclier & priorités",
            ("TypingSolverSub", _) => "Humanized typing, shield detection & adds priority",

            ("TypingSolverTip", ClientLanguage.Japanese) => "敵のセリフや技名を一文字ずつ自動入力して素早く撃破します。",
            ("TypingSolverTip", ClientLanguage.German) => "Löst gegnerische Phrasen Buchstabe für Buchstabe mit realistischer Verzögerung.",
            ("TypingSolverTip", ClientLanguage.French) => "Tape automatiquement les phrases ennemies lettre par lettre avec un délai humanisé.",
            ("TypingSolverTip", _) => "Automatically solves enemy phrases letter-by-letter with humanized typing delays.",

            ("AfkLoop", ClientLanguage.Japanese) => "自動周回 (AFK)",
            ("AfkLoop", ClientLanguage.German) => "AFK-Schleife",
            ("AfkLoop", ClientLanguage.French) => "Boucle AFK",
            ("AfkLoop", _) => "AFK Loop",

            ("AfkLoopSub", ClientLanguage.Japanese) => "受付・突入・リザルト退出の全自動化",
            ("AfkLoopSub", ClientLanguage.German) => "Automatische Anmeldung, Beitritt & Abschluss",
            ("AfkLoopSub", ClientLanguage.French) => "Inscription, entrée & sortie des résultats",
            ("AfkLoopSub", _) => "Auto-register, commence & exit results",

            ("AfkLoopTip", ClientLanguage.Japanese) => "受付NPCへの話しかけ、マッチング突入、終了後のリザルト退出を自動化します。",
            ("AfkLoopTip", ClientLanguage.German) => "Übernimmt die Anmeldung am Schalter, den Inhaltsbeitritt und das Verlassen des Ergebnisbildschirms.",
            ("AfkLoopTip", ClientLanguage.French) => "Interagit automatiquement avec la préposée, confirme l'entrée et quitte l'écran de fin.",
            ("AfkLoopTip", _) => "Automatically interacts with Registrar, confirms entry, commences duty, and exits results.",

            ("AutoOpenReg", ClientLanguage.Japanese) => "受付接近時に自動表示",
            ("AutoOpenReg", ClientLanguage.German) => "Autom. Öffnen am Schalter",
            ("AutoOpenReg", ClientLanguage.French) => "Ouverture auto préposée",
            ("AutoOpenReg", _) => "Auto-Open Near Registrar",

            ("AutoOpenRegSub", ClientLanguage.Japanese) => "参加受付NPCの近くでウィンドウを自動で開く",
            ("AutoOpenRegSub", ClientLanguage.German) => "Öffnet Fenster in Interaktionsreichweite",
            ("AutoOpenRegSub", ClientLanguage.French) => "Ouvre l'interface à portée d'interaction",
            ("AutoOpenRegSub", _) => "Open UI automatically in interaction range",

            ("AutoOpenRegTip", ClientLanguage.Japanese) => "キーバウンド・ブロウラーの参加受付NPCに近づいた際、自動でこの画面を開きます。",
            ("AutoOpenRegTip", ClientLanguage.German) => "Öffnet dieses Menü automatisch, wenn du dich der Rezeptionistin näherst.",
            ("AutoOpenRegTip", ClientLanguage.French) => "Ouvre automatiquement cette interface lorsque vous vous approchez de la préposée.",
            ("AutoOpenRegTip", _) => "Automatically opens this interface whenever you approach the Keybound Registrar NPC.",

            ("IgnorePunct", ClientLanguage.Japanese) => "句読点・記号の省略",
            ("IgnorePunct", ClientLanguage.German) => "Satzzeichen ignorieren",
            ("IgnorePunct", ClientLanguage.French) => "Ignorer la ponctuation",
            ("IgnorePunct", _) => "Ignore Punctuation",

            ("IgnorePunctSub", ClientLanguage.Japanese) => "！、？、'、- などの記号を省略して入力",
            ("IgnorePunctSub", ClientLanguage.German) => "Entfernt !, ?, ', - aus Phrasen",
            ("IgnorePunctSub", ClientLanguage.French) => "Supprime auto !, ?, ', - des phrases",
            ("IgnorePunctSub", _) => "Auto-strip !, ?, ', - from phrases",

            ("IgnorePunctTip", ClientLanguage.Japanese) => "「句読点を無視する」仕様に合わせて記号を除外し、より高速に入力します。",
            ("IgnorePunctTip", ClientLanguage.German) => "Entfernt Satzzeichen automatisch für schnelleres, fehlerfreies Tippen.",
            ("IgnorePunctTip", ClientLanguage.French) => "Supprime automatiquement la ponctuation pour une saisie plus rapide et sans erreur.",
            ("IgnorePunctTip", _) => "Automatically strips punctuation symbols to guarantee faster, error-free typing.",

            // --- Sliders & Sections ---
            ("Speed", ClientLanguage.Japanese) => "速度設定",
            ("Speed", ClientLanguage.German) => "GESCHWINDIGKEIT",
            ("Speed", ClientLanguage.French) => "VITESSE",
            ("Speed", _) => "SPEED",

            ("MinCharDelay", ClientLanguage.Japanese) => "キー最小間隔 (ms)",
            ("MinCharDelay", ClientLanguage.German) => "Min. Tastenverzögerung",
            ("MinCharDelay", ClientLanguage.French) => "Délai min. entre frappes",
            ("MinCharDelay", _) => "Min Keystroke Delay",

            ("MaxCharDelay", ClientLanguage.Japanese) => "キー最大間隔 (ms)",
            ("MaxCharDelay", ClientLanguage.German) => "Max. Tastenverzögerung",
            ("MaxCharDelay", ClientLanguage.French) => "Délai max. entre frappes",
            ("MaxCharDelay", _) => "Max Keystroke Delay",

            ("SubDelay", ClientLanguage.Japanese) => "決定 (Enter) 遅延 (ms)",
            ("SubDelay", ClientLanguage.German) => "Bestätigungsverzögerung",
            ("SubDelay", ClientLanguage.French) => "Délai de validation",
            ("SubDelay", _) => "Submit Delay",

            // --- Word Bar & Badges ---
            ("Standby", ClientLanguage.Japanese) => "> 待機中 <",
            ("Standby", ClientLanguage.German) => "> Bereit <",
            ("Standby", ClientLanguage.French) => "> En attente <",
            ("Standby", _) => "> standby <",

            ("BadgeHalt", ClientLanguage.Japanese) => "停止",
            ("BadgeHalt", ClientLanguage.German) => "HALT",
            ("BadgeHalt", ClientLanguage.French) => "ARRÊT",
            ("BadgeHalt", _) => "HALT",

            ("BadgeTyping", ClientLanguage.Japanese) => "入力中",
            ("BadgeTyping", ClientLanguage.German) => "TIPPEN",
            ("BadgeTyping", ClientLanguage.French) => "SAISIE",
            ("BadgeTyping", _) => "TYPING",

            ("BadgeCombat", ClientLanguage.Japanese) => "戦闘中",
            ("BadgeCombat", ClientLanguage.German) => "KAMPF",
            ("BadgeCombat", ClientLanguage.French) => "COMBAT",
            ("BadgeCombat", _) => "COMBAT",

            ("BadgeLoop", ClientLanguage.Japanese) => "周回中",
            ("BadgeLoop", ClientLanguage.German) => "SCHLEIFE",
            ("BadgeLoop", ClientLanguage.French) => "BOUCLE",
            ("BadgeLoop", _) => "LOOP",

            ("BadgeIdle", ClientLanguage.Japanese) => "待機",
            ("BadgeIdle", ClientLanguage.German) => "BEREIT",
            ("BadgeIdle", ClientLanguage.French) => "ATTENTE",
            ("BadgeIdle", _) => "IDLE",

            // --- Window Header Buttons & Tooltips ---
            ("Close", ClientLanguage.Japanese) => "閉じる",
            ("Close", ClientLanguage.German) => "Schließen",
            ("Close", ClientLanguage.French) => "Fermer",
            ("Close", _) => "Close",

            ("Compact", ClientLanguage.Japanese) => "最小化",
            ("Compact", ClientLanguage.German) => "Kompakt",
            ("Compact", ClientLanguage.French) => "Réduire",
            ("Compact", _) => "Compact",

            ("Expand", ClientLanguage.Japanese) => "展開",
            ("Expand", ClientLanguage.German) => "Erweitern",
            ("Expand", ClientLanguage.French) => "Agrandir",
            ("Expand", _) => "Expand",

            ("LangTooltip", ClientLanguage.Japanese) => "言語設定 (Language)",
            ("LangTooltip", ClientLanguage.German) => "Spracheinstellungen (Language)",
            ("LangTooltip", ClientLanguage.French) => "Paramètres de langue (Language)",
            ("LangTooltip", _) => "Language Settings",

            ("LangAuto", ClientLanguage.Japanese) => "自動 (クライアント言語)",
            ("LangAuto", ClientLanguage.German) => "Automatisch (Spiel-Sprache)",
            ("LangAuto", ClientLanguage.French) => "Automatique (Langue du jeu)",
            ("LangAuto", _) => "Auto (Game Client)",

            _ => key
        };
    }
}

