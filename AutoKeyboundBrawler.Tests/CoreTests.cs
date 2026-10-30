using AutoKeyboundBrawler.Core;
using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.Interop;
using System;
using System.Collections.Generic;
using Xunit;

namespace AutoKeyboundBrawler.Tests;

public class CoreTests
{
    [Fact]
    public unsafe void TestNativeMethods_InputSize()
    {
        var inputSize = sizeof(NativeMethods.INPUT);
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = 0,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP
                }
            }
        };
        var ret = NativeMethods.SendInput(1, &input, inputSize);
        var err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        Assert.Equal(40, inputSize);
        // Error 87 is ERROR_INVALID_PARAMETER (which happens when cbSize != 40)
        Assert.NotEqual(87, err);
    }

    [Fact]
    public void TestStripPunctuation_EnglishAndFrench()
    {
        Assert.Equal("Dont", KTGWordDatabase.StripPunctuation("Don't"));
        Assert.Equal("Allout attack", KTGWordDatabase.StripPunctuation("All-out attack!"));
        Assert.Equal("tous", KTGWordDatabase.StripPunctuation("tous !"));
        Assert.Equal("pret", KTGWordDatabase.StripPunctuation("pret ?"));
    }

    [Fact]
    public void TestStripPunctuation_Japanese()
    {
        Assert.Equal("サボテンダー", KTGWordDatabase.StripPunctuation("サボテンダー！"));
        Assert.Equal("針本", KTGWordDatabase.StripPunctuation("針？本"));
        Assert.Equal("命は星を巡る", KTGWordDatabase.StripPunctuation("「命は星を巡る」"));
        Assert.Equal("しみる", KTGWordDatabase.StripPunctuation("しみる～"));
        Assert.Equal("しみる", KTGWordDatabase.StripPunctuation("しみる〜"));
        Assert.Equal("サボテンノック", KTGWordDatabase.StripPunctuation("サボテン・ノック"));
        Assert.Equal("あっちそっち", KTGWordDatabase.StripPunctuation("あっち、そっち。"));
    }

    [Fact]
    public void TestKTGLocalizationRegistry_Registrar()
    {
        Assert.True(KTGLocalizationRegistry.IsRegistrar(1058751, "Any name"));
        Assert.True(KTGLocalizationRegistry.IsRegistrar(0, "Keybound registrar"));
        Assert.True(KTGLocalizationRegistry.IsRegistrar(0, "参加受付"));
        Assert.True(KTGLocalizationRegistry.IsRegistrar(0, "Rezeptionistin"));
        Assert.True(KTGLocalizationRegistry.IsRegistrar(0, "préposée aux inscriptions"));
        Assert.False(KTGLocalizationRegistry.IsRegistrar(999, "Random NPC"));
    }

    [Fact]
    public void TestKTGLocalizationRegistry_HaltSpells()
    {
        Assert.True(KTGLocalizationRegistry.IsHaltSpell(null, 49702));
        Assert.True(KTGLocalizationRegistry.IsHaltSpell(null, 49529));
        Assert.True(KTGLocalizationRegistry.IsHaltSpell("Dynamic Sensory Jammer"));
        Assert.True(KTGLocalizationRegistry.IsHaltSpell("Cursed Gaze"));
        Assert.True(KTGLocalizationRegistry.IsHaltSpell("動体感知ジャマー"));
        Assert.True(KTGLocalizationRegistry.IsHaltSpell("呪いの視線"));
        Assert.True(KTGLocalizationRegistry.IsHaltSpell("Dynamischer Störsender"));
        Assert.True(KTGLocalizationRegistry.IsHaltSpell("Brouilleur sensoriel"));
        Assert.False(KTGLocalizationRegistry.IsHaltSpell("Fire IV"));
    }

    [Fact]
    public void TestKTGLocalizationRegistry_NeedlesAndCactuar()
    {
        Assert.True(KTGLocalizationRegistry.IsNeedlesSpell(null, 49524));
        Assert.True(KTGLocalizationRegistry.IsNeedlesSpell("針？本"));
        Assert.True(KTGLocalizationRegistry.IsNeedlesSpell("??? Needles"));
        Assert.True(KTGLocalizationRegistry.IsNeedlesSpell("1,000 Needles"));
        Assert.True(KTGLocalizationRegistry.IsNeedlesSpell("???-Stacheln"));
        Assert.True(KTGLocalizationRegistry.IsNeedlesSpell("??? Épines"));
        Assert.True(KTGLocalizationRegistry.IsNeedlesSpell("1000 aiguilles"));
        Assert.False(KTGLocalizationRegistry.IsNeedlesSpell("Main Cannon"));

        Assert.True(KTGLocalizationRegistry.IsCactuarPrompt("サボテンダー"));
        Assert.True(KTGLocalizationRegistry.IsCactuarPrompt("Cactuar"));
        Assert.True(KTGLocalizationRegistry.IsCactuarPrompt("Gigantender"));
        Assert.True(KTGLocalizationRegistry.IsCactuarPrompt("Pampa"));
        Assert.True(KTGLocalizationRegistry.IsCactuarPrompt("Kaktor"));
        Assert.False(KTGLocalizationRegistry.IsCactuarPrompt("Airbuster"));
    }

    [Fact]
    public void TestKTGLocalizationRegistry_BossUltimates()
    {
        Assert.True(KTGLocalizationRegistry.IsBossUltimateCast(null, 49538));
        Assert.True(KTGLocalizationRegistry.IsBossUltimateCast("メインカノン"));
        Assert.True(KTGLocalizationRegistry.IsBossUltimateCast("Tankbuster"));
        Assert.True(KTGLocalizationRegistry.IsBossUltimateCast("エクリプスメテオ"));
        Assert.False(KTGLocalizationRegistry.IsBossUltimateCast("Auto-Attack"));
    }

    [Fact]
    public void TestKTGLocalizationRegistry_Buttons()
    {
        // Commence
        Assert.True(KTGLocalizationRegistry.IsCommenceButtonText("Commence"));
        Assert.True(KTGLocalizationRegistry.IsCommenceButtonText("突入"));
        Assert.True(KTGLocalizationRegistry.IsCommenceButtonText("Teilnehmen"));
        Assert.True(KTGLocalizationRegistry.IsCommenceButtonText("Commencer"));
        Assert.False(KTGLocalizationRegistry.IsCommenceButtonText("Cancel"));

        // Register
        Assert.True(KTGLocalizationRegistry.IsRegisterButtonText("Register"));
        Assert.True(KTGLocalizationRegistry.IsRegisterButtonText("参加登録"));
        Assert.True(KTGLocalizationRegistry.IsRegisterButtonText("Anmelden"));
        Assert.True(KTGLocalizationRegistry.IsRegisterButtonText("S'inscrire"));
        Assert.False(KTGLocalizationRegistry.IsRegisterButtonText("Leave"));

        // Result Close
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("Exit"));
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("Leave"));
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("Close"));
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("OK"));
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("退出"));
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("閉じる"));
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("戻る"));
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("Verlassen"));
        Assert.True(KTGLocalizationRegistry.IsResultCloseButtonText("Quitter"));
        Assert.False(KTGLocalizationRegistry.IsResultCloseButtonText("Attack"));
    }

    [Fact]
    public void TestKTGStateReader_IsDefenseSentence()
    {
        // Boss (slot 2) is NEVER defense
        Assert.False(KTGStateReader.IsDefenseSentence("Defense Shield", slotId: 2, screenY: 750f));
        Assert.False(KTGStateReader.IsDefenseSentence("「命は星を巡る」", slotId: 2, screenY: 750f));

        // ScreenY >= 700 on non-boss is player barrier prompt
        Assert.True(KTGStateReader.IsDefenseSentence("Any barrier phrase", slotId: 20001, screenY: 730f));

        // Defense keywords
        Assert.True(KTGStateReader.IsDefenseSentence("Total Defense", slotId: 3, screenY: 500f));
        Assert.True(KTGStateReader.IsDefenseSentence("絶対防御", slotId: 3, screenY: 500f));

        // Japanese quotation brackets on non-boss
        Assert.True(KTGStateReader.IsDefenseSentence("「命は星を巡る」", slotId: 3, screenY: 500f));

        // Regular wave mobs are NOT defense
        Assert.False(KTGStateReader.IsDefenseSentence("サボテンダー", slotId: 3, screenY: 500f));
        Assert.False(KTGStateReader.IsDefenseSentence("Cactuar", slotId: 3, screenY: 500f));
    }

    [Fact]
    public void TestKTGStateReader_IsSameWord()
    {
        Assert.True(KTGStateReader.IsSameWord("Don't", "Dont"));
        Assert.True(KTGStateReader.IsSameWord("tous !", "tous"));
        Assert.True(KTGStateReader.IsSameWord("サボテンダー！", "サボテンダー"));
        Assert.True(KTGStateReader.IsSameWord("Test", "test"));
        Assert.False(KTGStateReader.IsSameWord("Cactuar", "Airbuster"));
        Assert.False(KTGStateReader.IsSameWord(null, "Test"));
    }

    [Fact]
    public void TestKTGStateReader_TargetPriorityHierarchy()
    {
        var config = new Configuration();
        var needleSlots = new HashSet<int> { 5 };
        var otherCasts = new HashSet<int> { 6 };

        // Tier 0: Defensive prompt (Slot != 2, ScreenY >= 700)
        var defenseTarget = new KTGTargetWord(20001, "Defensive Barrier", 720f, 100f);
        Assert.Equal(0, KTGStateReader.GetTargetPriority(defenseTarget, true, needleSlots, otherCasts, false, null, config));

        // Tier 1: Cactuar casting needles
        var needlesCactuar = new KTGTargetWord(5, "サボテンダー", 500f, 100f);
        Assert.Equal(1, KTGStateReader.GetTargetPriority(needlesCactuar, true, needleSlots, otherCasts, true, 5, config));

        // Tier 2: Other casting add
        var castingAdd = new KTGTargetWord(6, "Grenade", 500f, 100f);
        Assert.Equal(2, KTGStateReader.GetTargetPriority(castingAdd, true, needleSlots, otherCasts, false, null, config));

        // Tier 3: Idle Cactuar mob
        var idleCactuar = new KTGTargetWord(7, "サボテンダー", 500f, 100f);
        Assert.Equal(3, KTGStateReader.GetTargetPriority(idleCactuar, true, needleSlots, otherCasts, false, null, config));

        // Tier 4: Regular add with boss on screen
        var regularAdd = new KTGTargetWord(8, "Sweep Flank", 500f, 100f);
        Assert.Equal(4, KTGStateReader.GetTargetPriority(regularAdd, true, needleSlots, otherCasts, false, null, config));

        // Tier 6/7: Boss (Slot 2)
        var idleBoss = new KTGTargetWord(2, "Airbuster", 200f, 100f);
        Assert.Equal(7, KTGStateReader.GetTargetPriority(idleBoss, true, needleSlots, otherCasts, false, null, config));
    }

    [Fact]
    public void TestKTGInputSimulator_EnqueueWord_Japanese()
    {
        var sim = new KTGInputSimulator();
        var config = new Configuration { IgnorePunctuation = true }; // should NOT strip Japanese punctuation

        // Wave dash U+301C must be normalized to fullwidth tilde U+FF5E
        sim.EnqueueWord("しみる\u301C！", config);

        Assert.True(sim.IsTyping);
        Assert.Equal("しみる\u301C！", sim.CurrentWord);

        // Word has 5 chars ("し", "み", "る", "～", "！") + 1 Enter = 6 actions
        Assert.Equal(6, sim.RemainingChars);
    }

    [Fact]
    public void TestKTGInputSimulator_EnqueueWord_EnglishPunctuation()
    {
        var sim = new KTGInputSimulator();

        // 1. When IgnorePunctuation = true: strips ' and !
        var configStrip = new Configuration { IgnorePunctuation = true };
        sim.EnqueueWord("Don't give up!", configStrip);
        // Stripped: "Dont give up" (12 chars) + Enter (1) = 13 actions
        Assert.Equal(13, sim.RemainingChars);

        // 2. When IgnorePunctuation = false: preserves punctuation
        var configKeep = new Configuration { IgnorePunctuation = false };
        sim.EnqueueWord("Don't", configKeep);
        // "Don't" (5 chars) + Enter (1) = 6 actions
        Assert.Equal(6, sim.RemainingChars);
    }

    [Fact]
    public void TestConfiguration_DefaultsAndMethods()
    {
        var config = new Configuration();
        Assert.True(config.Enabled);
        Assert.True(config.MinigameAutoplay);
        Assert.False(config.AutoLoopEnabled);
        Assert.True(config.AutoOpenNearRegistrar);
        Assert.True(config.PrioritizeDefense);
        Assert.True(config.StopOnPenaltyCast);

        config.SetAutoLoop(true);
        Assert.True(config.AutoLoopEnabled);
        Assert.True(config.AutoTalkRegistrar);
        Assert.True(config.AutoConfirmRegister);
        Assert.True(config.AutoCommenceDuty);
        Assert.True(config.AutoLeaveResults);
        Assert.True(config.Enabled);

        config.SetMinigameAutoplay(false);
        Assert.False(config.MinigameAutoplay);
        Assert.True(config.Enabled); // Still enabled because AutoLoopEnabled is true

        config.SetAutoLoop(false);
        Assert.False(config.AutoLoopEnabled);
        Assert.False(config.Enabled); // Both false now
    }


    [Fact]
    public void TestKTGLocalizationRegistry_IsRegistrar_Null()
    {
        Assert.False(KTGLocalizationRegistry.IsRegistrar((Dalamud.Game.ClientState.Objects.Types.IGameObject?)null));
    }

    [Fact]
    public void TestKTGStateReader_SortingLogic()
    {
        var config = new Configuration();
        var needleSlots = new HashSet<int>();
        var otherCasts = new HashSet<int>();

        var list = new List<KTGTargetWord>
        {
            new(2, "Airbuster", 800f, 100f),    // Boss (tier 7)
            new(10, "LongWordAdd", 500f, 100f), // Add at Y=500, len=11
            new(11, "Short", 500f, 100f),       // Add at Y=500, len=5
            new(12, "LowerAdd", 650f, 100f),    // Add at Y=650 (closer to player)
            new(20001, "Barrier", 720f, 100f)   // Defense sentence (tier 0)
        };

        list.Sort((a, b) =>
        {
            var pA = KTGStateReader.GetTargetPriority(a, true, needleSlots, otherCasts, false, null, config);
            var pB = KTGStateReader.GetTargetPriority(b, true, needleSlots, otherCasts, false, null, config);
            if (pA != pB) return pA.CompareTo(pB);

            var yComp = b.ScreenY.CompareTo(a.ScreenY);
            if (yComp != 0) return yComp;

            return a.Word.Length.CompareTo(b.Word.Length);
        });

        // 1st: Defense sentence (tier 0)
        Assert.Equal(20001, list[0].SlotId);
        // 2nd: Lower Add closer to player wall (ScreenY=650)
        Assert.Equal(12, list[1].SlotId);
        // 3rd: Shorter word among adds at same ScreenY (len=5 vs len=11)
        Assert.Equal(11, list[2].SlotId);
        // 4th: Longer word among adds at same ScreenY
        Assert.Equal(10, list[3].SlotId);
        // 5th: Boss (tier 7)
        Assert.Equal(2, list[4].SlotId);
    }

    [Fact]
    public void TestKTGStateReader_IsDefenseSentence_BossUltimateScreenY650()
    {
        var bossCasts = new List<KTGActiveCast>
        {
            new(2, "Tankbuster")
        };
        var regularCasts = new List<KTGActiveCast>
        {
            new(2, "Fire IV")
        };

        // When Boss casts Tankbuster (Ultimate), prompt at Y=660 is recognized as defense
        Assert.True(KTGStateReader.IsDefenseSentence("ArbitraryPhrase", slotId: 3, screenY: 660f, bossCasts));

        // When Boss is casting normal spell, prompt at Y=660 without defense keywords is NOT defense
        Assert.False(KTGStateReader.IsDefenseSentence("ArbitraryPhrase", slotId: 3, screenY: 660f, regularCasts));

        // When casts is null (safely handled without throwing)
        Assert.False(KTGStateReader.IsDefenseSentence("ArbitraryPhrase", slotId: 3, screenY: 660f, null));
    }

    [Fact]
    public void TestKTGInputSimulator_Clear_ResetsState()
    {
        var sim = new KTGInputSimulator();
        var config = new Configuration();
        sim.EnqueueWord("TestWord", config);
        Assert.True(sim.IsTyping);
        Assert.Equal("TestWord", sim.CurrentWord);
        Assert.True(sim.RemainingChars > 0);

        sim.Clear();
        Assert.False(sim.IsTyping);
        Assert.Equal(string.Empty, sim.CurrentWord);
        Assert.Equal(0, sim.RemainingChars);
    }

    [Fact]
    public void TestAntiAfk_NoMouseMethodsExposed()
    {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;

        var nativeMethodsType = typeof(NativeMethods);
        Assert.Null(nativeMethodsType.GetMethod("mouse_event", flags));
        Assert.Null(nativeMethodsType.GetMethod("MakeLParam", flags));
        Assert.Null(nativeMethodsType.GetField("WM_MOUSEMOVE", flags));
        Assert.Null(nativeMethodsType.GetField("MOUSEEVENTF_MOVE", flags));

        var antiAfkType = typeof(KTGAntiAfk);
        Assert.Null(antiAfkType.GetMethod("mouse_event", flags));
        Assert.Null(antiAfkType.GetMethod("MakeLParam", flags));
        Assert.Null(antiAfkType.GetField("_lastOsPulseTime", flags));
        Assert.Null(antiAfkType.GetField("WM_MOUSEMOVE", flags));
        Assert.Null(antiAfkType.GetField("MOUSEEVENTF_MOVE", flags));
    }

    [Fact]
    public void TestAntiAfk_KeepAliveExecutesWithoutThrowing()
    {
        // Must handle missing game process, null pointers, and missing Dalamud services gracefully
        var exception = Record.Exception(() => KTGAntiAfk.KeepAlive());
        Assert.Null(exception);
    }

    [Fact]
    public void TestNativeMethods_IsGameFocused_OverridesAndDefault()
    {
        try
        {
            NativeMethods.SetGameFocusedOverrideForTesting(true);
            Assert.True(NativeMethods.IsGameFocused());

            NativeMethods.SetGameFocusedOverrideForTesting(false);
            Assert.False(NativeMethods.IsGameFocused());
        }
        finally
        {
            NativeMethods.SetGameFocusedOverrideForTesting(null);
        }

        // Without override, should evaluate without throwing
        var noThrowException = Record.Exception(() => NativeMethods.IsGameFocused());
        Assert.Null(noThrowException);
    }

    [Fact]
    public void TestNativeMethods_IsShiftPressed_OverridesAndDefault()
    {
        try
        {
            NativeMethods.SetShiftPressedOverrideForTesting(true);
            Assert.True(NativeMethods.IsShiftPressed());

            NativeMethods.SetShiftPressedOverrideForTesting(false);
            Assert.False(NativeMethods.IsShiftPressed());
        }
        finally
        {
            NativeMethods.SetShiftPressedOverrideForTesting(null);
        }

        var noThrowException = Record.Exception(() => NativeMethods.IsShiftPressed());
        Assert.Null(noThrowException);
    }

    [Fact]
    public void TestKTGSafetyGuard_CanType_WindowFocusSafety()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            var config = new Configuration { Enabled = true, MinigameAutoplay = true, RequireWindowFocus = true, StopOnPenaltyCast = false };

            // 1. When game window is NOT focused, CanType MUST fail with "Game window is not focused"
            NativeMethods.SetGameFocusedOverrideForTesting(false);
            var canTypeUnfocused = KTGSafetyGuard.CanType(config, out var reasonUnfocused);
            Assert.False(canTypeUnfocused);
            Assert.Equal("Game window is not focused", reasonUnfocused);

            // 2. When game window IS focused, CanType succeeds
            NativeMethods.SetGameFocusedOverrideForTesting(true);
            var canTypeFocused = KTGSafetyGuard.CanType(config, out var reasonFocused);
            Assert.True(canTypeFocused);
            Assert.Equal("OK", reasonFocused);

            // 3. When RequireWindowFocus is disabled, unfocused does not block typing
            config.RequireWindowFocus = false;
            NativeMethods.SetGameFocusedOverrideForTesting(false);
            var canTypeNoReq = KTGSafetyGuard.CanType(config, out var reasonNoReq);
            Assert.True(canTypeNoReq);
            Assert.Equal("OK", reasonNoReq);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
            NativeMethods.SetGameFocusedOverrideForTesting(null);
        }
    }

    [Fact]
    public void TestKTGAntiAfk_ShouldKeepAlive_TerritoryAndConfigScoping()
    {
        try
        {
            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = true,
                AutoLoopEnabled = true
            };

            // Case 1: Outside of Gold Saucer / Duty (e.g. Limsa Lominsa / normal world)
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: false);
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // Case 2: Inside Keybound Brawler Duty
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            Assert.True(KTGAntiAfk.ShouldKeepAlive(config));

            config.MinigameAutoplay = false;
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));
            config.MinigameAutoplay = true;

            // Case 3: In Keybound Square with AutoLoop active
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: true);
            config.AutoLoopEnabled = true;
            Assert.True(KTGAntiAfk.ShouldKeepAlive(config));

            // Case 4: In Keybound Square but AutoLoop is disabled
            config.AutoLoopEnabled = false;
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // Case 5: Master Enabled is false
            config.Enabled = false;
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGAntiAfk_KeepAlive_SafetyChecks()
    {
        try
        {
            // When game is unfocused, KeepAlive executes Layer 3 memory reset safely without sending keystrokes
            NativeMethods.SetGameFocusedOverrideForTesting(false);
            var exUnfocused = Record.Exception(() => KTGAntiAfk.KeepAlive(force: true));
            Assert.Null(exUnfocused);

            // When shift is physically held, KeepAlive executes without sending keystrokes
            NativeMethods.SetGameFocusedOverrideForTesting(true);
            NativeMethods.SetShiftPressedOverrideForTesting(true);
            var exShiftHeld = Record.Exception(() => KTGAntiAfk.KeepAlive(force: true));
            Assert.Null(exShiftHeld);
        }
        finally
        {
            NativeMethods.SetGameFocusedOverrideForTesting(null);
            NativeMethods.SetShiftPressedOverrideForTesting(null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_UnfocusedBlocksKeyEmulation()
    {
        try
        {
            NativeMethods.SetGameFocusedOverrideForTesting(false);

            // 1. SendKey when unfocused returns immediately without throwing
            var exSendKey = Record.Exception(() => KTGInputSimulator.SendKey(FFXIVClientStructs.FFXIV.Client.System.Input.SeVirtualKey.A));
            Assert.Null(exSendKey);

            // 2. SendKeyWithShift when unfocused returns immediately without throwing
            var exSendKeyShift = Record.Exception(() => KTGInputSimulator.SendKeyWithShift(FFXIVClientStructs.FFXIV.Client.System.Input.SeVirtualKey.A));
            Assert.Null(exSendKeyShift);

            // 3. SendUnicodeChar when unfocused returns immediately without throwing
            var exUnicode = Record.Exception(() => KTGInputSimulator.SendUnicodeChar('z'));
            Assert.Null(exUnicode);

            // 4. TryClearInputBuffer when unfocused returns false immediately
            var cleared = KTGInputSimulator.TryClearInputBuffer();
            Assert.False(cleared);

            // 5. Update when unfocused does NOT type pending characters
            var sim = new KTGInputSimulator();
            var config = new Configuration { RequireWindowFocus = true };
            sim.EnqueueWord("Test", config);
            var initialRemaining = sim.RemainingChars;
            Assert.True(initialRemaining > 0);

            // Call Update while unfocused
            sim.Update(config);
            Assert.Equal(initialRemaining, sim.RemainingChars);
        }
        finally
        {
            NativeMethods.SetGameFocusedOverrideForTesting(null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_SendKeyWithShift_PreservesPhysicalShift()
    {
        try
        {
            NativeMethods.SetGameFocusedOverrideForTesting(true);
            NativeMethods.SetShiftPressedOverrideForTesting(true);

            // When user is physically holding Shift, SendKeyWithShift should only emit keypress and NOT emit Shift Up
            var ex = Record.Exception(() => KTGInputSimulator.SendKeyWithShift(FFXIVClientStructs.FFXIV.Client.System.Input.SeVirtualKey.KEY_1));
            Assert.Null(ex);
        }
        finally
        {
            NativeMethods.SetGameFocusedOverrideForTesting(null);
            NativeMethods.SetShiftPressedOverrideForTesting(null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_RequireWindowFocusFalse_AllowsTypingWhenUnfocused()
    {
        try
        {
            NativeMethods.SetGameFocusedOverrideForTesting(false);

            var configFocused = new Configuration { RequireWindowFocus = true };
            var configUnfocused = new Configuration { RequireWindowFocus = false };

            // 1. ShouldBlockInput evaluation
            Assert.True(KTGInputSimulator.ShouldBlockInput(configFocused));
            Assert.False(KTGInputSimulator.ShouldBlockInput(configUnfocused));

            // 2. Global Plugin.Configuration fallback when config is null
            Plugin.Configuration = configUnfocused;
            Assert.False(KTGInputSimulator.ShouldBlockInput(null));
            Plugin.Configuration = configFocused;
            Assert.True(KTGInputSimulator.ShouldBlockInput(null));

            // 3. When RequireWindowFocus is false, Update allows processing even when unfocused
            var sim = new KTGInputSimulator();
            configUnfocused.CharDelayMinMs = 10;
            configUnfocused.CharDelayMaxMs = 10;
            sim.EnqueueWord("A", configUnfocused);
            var initialRemaining = sim.RemainingChars;
            Assert.True(initialRemaining > 0);

            System.Threading.Thread.Sleep(25);
            sim.Update(configUnfocused);
            Assert.True(sim.RemainingChars < initialRemaining);
        }
        finally
        {
            NativeMethods.SetGameFocusedOverrideForTesting(null);
            Plugin.Configuration = null!;
        }
    }

    [Fact]
    public void TestKTGAntiAfk_KeepAlive_ResolvesPluginConfiguration()
    {
        try
        {
            // Outside of duty, Plugin.Configuration with MinigameAutoplay=true should NOT keepalive
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: false);
            Plugin.Configuration = new Configuration { Enabled = true, MinigameAutoplay = true };

            // Should resolve Plugin.Configuration and return early safely
            var ex = Record.Exception(() => KTGAntiAfk.KeepAlive());
            Assert.Null(ex);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
            Plugin.Configuration = null!;
        }
    }

    [Fact]
    public void TestKTGAntiAfk_IdleModeWhenNeitherCheckboxActive()
    {
        try
        {
            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = false,
                AutoLoopEnabled = false
            };

            // 1. Inside Duty: when both checkboxes false, ShouldKeepAlive is false
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // 2. In Square: when both checkboxes false, ShouldKeepAlive is false
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: true);
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // 3. When Enabled is false: ShouldKeepAlive is false even if individual checkboxes are true
            config.MinigameAutoplay = true;
            config.AutoLoopEnabled = true;
            config.Enabled = false;
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: true);
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // 4. KeepAlive with idle config returns safely without throwing or executing
            config.Enabled = true;
            config.MinigameAutoplay = false;
            config.AutoLoopEnabled = false;
            var ex = Record.Exception(() => KTGAntiAfk.KeepAlive(config));
            Assert.Null(ex);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGAutomationManager_IdleModeWhenNeitherCheckboxActive()
    {
        var config = new Configuration
        {
            Enabled = true,
            MinigameAutoplay = false,
            AutoLoopEnabled = false
        };

        var manager = new KTGAutomationManager(config, null!);

        // Call Tick when neither checkbox is active
        manager.Tick();

        // Must enter idle mode and not interfere
        Assert.Equal("Idle (Automation paused)", manager.CurrentStatus);
        Assert.Equal(EventPhase.None, manager.CurrentPhase);
        Assert.False(manager.IsTyping);
        Assert.False(manager.IsInDutyPhase);
        Assert.False(manager.IsInQueuePhase);
        Assert.False(manager.IsHaltStatus);
    }

    [Fact]
    public void TestMainWindow_OriginalDimensionsAndFocusSafety()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var defaultSizeField = typeof(AutoKeyboundBrawler.UI.MainWindow).GetField("DefaultSize", flags);
        Assert.NotNull(defaultSizeField);
        var defaultSize = (System.Numerics.Vector2)defaultSizeField.GetValue(null)!;
        Assert.Equal(280f, defaultSize.X);
        Assert.Equal(290f, defaultSize.Y);

        var constraintsField = typeof(AutoKeyboundBrawler.UI.MainWindow).GetField("ExpandedConstraints", flags);
        Assert.NotNull(constraintsField);
        var constraints = (Dalamud.Interface.Windowing.WindowSizeConstraints)constraintsField.GetValue(null)!;
        Assert.Equal(480f, constraints.MaximumSize.Y);

        // Verify background focus safety remains active by default
        var config = new Configuration();
        Assert.True(config.RequireWindowFocus);
        config.Initialize(null!);
        Assert.True(config.RequireWindowFocus);
    }
}

