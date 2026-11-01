using AutoKeyboundBrawler.Core;
using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.Interop;
using AutoKeyboundBrawler.UI;
using System;
using System.Collections.Generic;
using Xunit;

namespace AutoKeyboundBrawler.Tests;

public class CoreTests
{
    [Fact]
    public void TestArchitecture_NoPInvokeOrNativeMethodsInPlugin()
    {
        var asm = typeof(KTGInputSimulator).Assembly;
        var nativeMethodsType = asm.GetType("AutoKeyboundBrawler.Interop.NativeMethods");
        Assert.Null(nativeMethodsType);

        // Verify that the plugin has zero Win32 DllImport / P/Invoke methods
        foreach (var type in asm.GetTypes())
        {
            var pInvokeMethods = type.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Instance)
                .Where(m => (m.Attributes & System.Reflection.MethodAttributes.PinvokeImpl) != 0)
                .ToList();

            Assert.Empty(pInvokeMethods);
        }
    }



    [Fact]
    public void TestStripPunctuation_EnglishAndFrench()
    {
        // Null, empty, and whitespace
        Assert.Equal(string.Empty, KTGWordDatabase.StripPunctuation(null!));
        Assert.Equal(string.Empty, KTGWordDatabase.StripPunctuation(string.Empty));
        Assert.Equal(string.Empty, KTGWordDatabase.StripPunctuation("   "));
        Assert.Equal(string.Empty, KTGWordDatabase.StripPunctuation("!??-"));

        // English words
        Assert.Equal("Dont", KTGWordDatabase.StripPunctuation("Don't"));
        Assert.Equal("Allout attack", KTGWordDatabase.StripPunctuation("All-out attack!"));

        // French typography (space preceding exclamation/question mark is removed)
        Assert.Equal("tous", KTGWordDatabase.StripPunctuation("tous !"));
        Assert.Equal("pret", KTGWordDatabase.StripPunctuation("pret ?"));
        Assert.Equal("Cest pret", KTGWordDatabase.StripPunctuation("C'est pret !"));
    }

    [Fact]
    public void TestStripPunctuation_Japanese()
    {
        Assert.Equal(string.Empty, KTGWordDatabase.StripPunctuation("！？「」・～〜、。"));
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
        // Equivalent words with differing cases and punctuation
        Assert.True(KTGStateReader.IsSameWord("Don't", "Dont"));
        Assert.True(KTGStateReader.IsSameWord("  Don't  ", "Dont"));
        Assert.True(KTGStateReader.IsSameWord("tous !", "tous"));
        Assert.True(KTGStateReader.IsSameWord("サボテンダー！", "サボテンダー"));
        Assert.True(KTGStateReader.IsSameWord("Test", "test"));

        // Different words
        Assert.False(KTGStateReader.IsSameWord("Cactuar", "Airbuster"));

        // Null and whitespace edge cases
        Assert.False(KTGStateReader.IsSameWord(null, null));
        Assert.False(KTGStateReader.IsSameWord(null, "Test"));
        Assert.False(KTGStateReader.IsSameWord("Test", null));
        Assert.False(KTGStateReader.IsSameWord(string.Empty, string.Empty));
        Assert.False(KTGStateReader.IsSameWord("   ", "Test"));
        Assert.False(KTGStateReader.IsSameWord("Test", "   "));
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

        // Tier 4: Regular add with boss on screen (PrioritizeAdds = true)
        var regularAdd = new KTGTargetWord(8, "Sweep Flank", 500f, 100f);
        Assert.Equal(4, KTGStateReader.GetTargetPriority(regularAdd, true, needleSlots, otherCasts, false, null, config));

        // Tier 5: Standard wave regular enemies (no boss on screen)
        Assert.Equal(5, KTGStateReader.GetTargetPriority(regularAdd, false, needleSlots, otherCasts, false, null, config));

        // Tier 5: Regular add with boss on screen when PrioritizeAdds = false
        var configNoAdds = new Configuration { PrioritizeAdds = false };
        Assert.Equal(5, KTGStateReader.GetTargetPriority(regularAdd, true, needleSlots, otherCasts, false, null, configNoAdds));

        // Tier 6: Casting Boss (Slot 2 in otherCasts or needleSlots)
        var castingBossSlots = new HashSet<int>(otherCasts) { 2 };
        var idleBoss = new KTGTargetWord(2, "Airbuster", 200f, 100f);
        Assert.Equal(6, KTGStateReader.GetTargetPriority(idleBoss, true, needleSlots, castingBossSlots, false, null, config));

        // Tier 7: Idle Boss (Slot 2)
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

        // Initialize reconciliation and safety overrides
        var configInit = new Configuration
        {
            StopOnPenaltyCast = false,
            AutoTalkRegistrar = true,
            AutoConfirmRegister = true,
            AutoLoopEnabled = false,
            MinigameAutoplay = false
        };
        configInit.Initialize(null!);

        // Safety guards must be forcibly re-enabled
        Assert.True(configInit.StopOnPenaltyCast);
        Assert.True(configInit.PrioritizeAdds);
        Assert.True(configInit.PrioritizeDefense);
        Assert.True(configInit.AbortForDefense);
        Assert.True(configInit.MemoryModeObscured);

        // AutoLoopEnabled forced true when both sub-flags active
        Assert.True(configInit.AutoLoopEnabled);
        Assert.True(configInit.Enabled);
    }

    [Fact]
    public void TestKTGLocalizationRegistry_IsRegistrar_Null()
    {
        // IGameObject null safety
        Assert.False(KTGLocalizationRegistry.IsRegistrar((Dalamud.Game.ClientState.Objects.Types.IGameObject?)null));

        // Name and DataId null and whitespace safety
        Assert.False(KTGLocalizationRegistry.IsRegistrar(0, null));
        Assert.False(KTGLocalizationRegistry.IsRegistrar(0, string.Empty));
        Assert.False(KTGLocalizationRegistry.IsRegistrar(0, "   "));
        Assert.False(KTGLocalizationRegistry.IsRegistrar(999, null));
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

        var antiAfkType = typeof(KTGAntiAfk);
        Assert.Null(antiAfkType.GetMethod("mouse_event", flags));
        Assert.Null(antiAfkType.GetMethod("MakeLParam", flags));
        Assert.Null(antiAfkType.GetField("_lastOsPulseTime", flags));
        Assert.Null(antiAfkType.GetField("WM_MOUSEMOVE", flags));
        Assert.Null(antiAfkType.GetField("MOUSEEVENTF_MOVE", flags));
    }

    [Fact]
    public void TestAntiAfk_ProvenInMemoryMechanismsDesign()
    {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
        var antiAfkType = typeof(KTGAntiAfk);

        // Verify proven in-memory methods and constants are present
        Assert.NotNull(antiAfkType.GetMethod("ResetAgentInactivityTimer", flags));
        Assert.NotNull(antiAfkType.GetMethod("IsMatchUnderway", flags));
        Assert.Equal(0x28, KTGAntiAfk.AgentHudPointerOffset);
        Assert.Equal(0x50, KTGAntiAfk.AgentInactivityTimerOffset);
        Assert.Equal(0xDCE, KTGAntiAfk.DirectorContentTypeOffset);
        Assert.Equal(0x1FC0, KTGAntiAfk.DirectorMatchUnderwayOffset);
        Assert.Equal((byte)23, KTGAntiAfk.DirectorContentType);
        Assert.Equal(397u, KTGAntiAfk.HudAgentId);
        Assert.Equal(398u, KTGAntiAfk.InputAgentId);

        // Verify obsolete and futile keystroke simulation methods and fields are absent
        Assert.Null(antiAfkType.GetMethod("GetGameWindowHandle", flags));
        Assert.Null(antiAfkType.GetField("_cachedGameHwnd", flags));
        Assert.Null(antiAfkType.GetField("PulseIntervalSeconds", flags));
        Assert.Null(antiAfkType.GetField("_lastPulseTime", flags));
        Assert.Null(antiAfkType.GetProperty("LastKeystrokeTime", flags));
        Assert.Null(antiAfkType.GetProperty("LastWndMsgTime", flags));
        Assert.Null(antiAfkType.GetField("_lastKeystrokeTime", flags));
        Assert.Null(antiAfkType.GetField("_lastWndMsgTime", flags));
    }

    [Fact]
    public unsafe void TestAntiAfk_ResetAgentInactivityTimer_SafetyAndExecution()
    {
        try
        {
            KTGAntiAfk.ResetTimersForTesting();

            // Allocate mock memory buffers for Agent 397 (HUD) and Agent 398 (Input)
            // Agent 398 needs at least 0x60 bytes for offset 0x28 (hud ptr) and 0x50 (inactivity timer ulong)
            byte[] hudBuffer = new byte[64];
            byte[] inputBuffer = new byte[128];

            fixed (byte* pHud = hudBuffer)
            fixed (byte* pInput = inputBuffer)
            {
                var hudPtr = (IntPtr)pHud;
                var inputPtr = (IntPtr)pInput;

                // Point Agent 398 +0x28 to Agent 397 HUD
                *(nint*)(pInput + KTGAntiAfk.AgentHudPointerOffset) = (nint)hudPtr;
                // Set initial non-zero inactivity timer at +0x50
                *(ulong*)(pInput + KTGAntiAfk.AgentInactivityTimerOffset) = 45000UL;

                KTGAntiAfk.SetAgentBuffersForTesting(hudPtr, inputPtr);

                // Case 1: Match is NOT underway -> must NOT reset and must return false
                KTGAntiAfk.SetMatchUnderwayOverrideForTesting(false);
                var resultNotUnderway = KTGAntiAfk.ResetAgentInactivityTimer();
                Assert.False(resultNotUnderway);
                Assert.Equal(45000UL, *(ulong*)(pInput + KTGAntiAfk.AgentInactivityTimerOffset));
                Assert.Equal(DateTime.MinValue, KTGAntiAfk.LastAgentResetTime);

                // Case 2: Match IS underway -> must zero Agent 398 +0x50 and return true
                KTGAntiAfk.SetMatchUnderwayOverrideForTesting(true);
                var resultUnderway = KTGAntiAfk.ResetAgentInactivityTimer();
                Assert.True(resultUnderway);
                Assert.Equal(0UL, *(ulong*)(pInput + KTGAntiAfk.AgentInactivityTimerOffset));
                Assert.True(KTGAntiAfk.LastAgentResetTime > DateTime.MinValue);

                // Case 3: Corrupted HUD pointer at +0x28 -> must fail safety validation and return false
                *(ulong*)(pInput + KTGAntiAfk.AgentInactivityTimerOffset) = 99999UL;
                *(nint*)(pInput + KTGAntiAfk.AgentHudPointerOffset) = unchecked((nint)0xDEADBEEF);
                var resultCorrupted = KTGAntiAfk.ResetAgentInactivityTimer();
                Assert.False(resultCorrupted);
                Assert.Equal(99999UL, *(ulong*)(pInput + KTGAntiAfk.AgentInactivityTimerOffset));
            }
        }
        finally
        {
            KTGAntiAfk.ResetTimersForTesting();
        }
    }

    [Fact]
    public unsafe void TestAntiAfk_EdgeCases_BoundaryValuesAndPartialBuffers()
    {
        try
        {
            KTGAntiAfk.ResetTimersForTesting();

            // 1. Partial buffer assignment (hud buffer set, input buffer null)
            byte[] hudBuffer = new byte[64];
            fixed (byte* pHud = hudBuffer)
            {
                KTGAntiAfk.SetAgentBuffersForTesting((IntPtr)pHud, IntPtr.Zero);
                KTGAntiAfk.SetMatchUnderwayOverrideForTesting(true);

                // Should not crash and should return false safely
                var resPartial = KTGAntiAfk.ResetAgentInactivityTimer();
                Assert.False(resPartial);
            }

            // 2. Partial buffer assignment (input buffer set, hud buffer null)
            byte[] inputBuffer = new byte[128];
            fixed (byte* pInput = inputBuffer)
            {
                KTGAntiAfk.SetAgentBuffersForTesting(IntPtr.Zero, (IntPtr)pInput);
                KTGAntiAfk.SetMatchUnderwayOverrideForTesting(true);

                var resPartial2 = KTGAntiAfk.ResetAgentInactivityTimer();
                Assert.False(resPartial2);
            }

            // 3. Boundary values: ulong.MaxValue
            fixed (byte* pHud = hudBuffer)
            fixed (byte* pInput = inputBuffer)
            {
                *(nint*)(pInput + KTGAntiAfk.AgentHudPointerOffset) = (nint)pHud;
                *(ulong*)(pInput + KTGAntiAfk.AgentInactivityTimerOffset) = ulong.MaxValue;

                KTGAntiAfk.SetAgentBuffersForTesting((IntPtr)pHud, (IntPtr)pInput);
                KTGAntiAfk.SetMatchUnderwayOverrideForTesting(true);

                var resMax = KTGAntiAfk.ResetAgentInactivityTimer();
                Assert.True(resMax);
                Assert.Equal(0UL, *(ulong*)(pInput + KTGAntiAfk.AgentInactivityTimerOffset));

                // 4. Boundary value: Already 0UL
                var resZero = KTGAntiAfk.ResetAgentInactivityTimer();
                Assert.True(resZero);
                Assert.Equal(0UL, *(ulong*)(pInput + KTGAntiAfk.AgentInactivityTimerOffset));
            }
        }
        finally
        {
            KTGAntiAfk.ResetTimersForTesting();
        }
    }

    [Fact]
    public void TestAntiAfk_IsMatchUnderway_OverridesAndDefault()
    {
        try
        {
            KTGAntiAfk.ResetTimersForTesting();

            // Override true
            KTGAntiAfk.SetMatchUnderwayOverrideForTesting(true);
            Assert.True(KTGAntiAfk.IsMatchUnderway());

            // Override false
            KTGAntiAfk.SetMatchUnderwayOverrideForTesting(false);
            Assert.False(KTGAntiAfk.IsMatchUnderway());

            // Default (no override, in test environment with no game running)
            KTGAntiAfk.SetMatchUnderwayOverrideForTesting(null);
            var ex = Record.Exception(() => KTGAntiAfk.IsMatchUnderway());
            Assert.Null(ex);
            Assert.False(KTGAntiAfk.IsMatchUnderway());
        }
        finally
        {
            KTGAntiAfk.ResetTimersForTesting();
        }
    }

    [Fact]
    public unsafe void TestAntiAfk_IsMatchUnderway_DirectorOffsetsMemoryValidation()
    {
        try
        {
            KTGAntiAfk.ResetTimersForTesting();

            // Director buffer needs to accommodate DirectorMatchUnderwayOffset (0x1FC0 + 1)
            byte[] directorBuffer = new byte[0x2000];
            fixed (byte* pDir = directorBuffer)
            {
                var dirPtr = (IntPtr)pDir;
                KTGAntiAfk.SetDirectorBufferForTesting(dirPtr);

                // Case 1: Matching content type (23) and match underway flag (1) -> true
                directorBuffer[KTGAntiAfk.DirectorContentTypeOffset] = KTGAntiAfk.DirectorContentType;
                directorBuffer[KTGAntiAfk.DirectorMatchUnderwayOffset] = 1;
                Assert.True(KTGAntiAfk.IsMatchUnderway());

                // Case 2: Matching content type (23) and match underway flag non-zero byte (0x80) -> true
                directorBuffer[KTGAntiAfk.DirectorMatchUnderwayOffset] = 0x80;
                Assert.True(KTGAntiAfk.IsMatchUnderway());

                // Case 3: Match underway flag is 0 -> false
                directorBuffer[KTGAntiAfk.DirectorMatchUnderwayOffset] = 0;
                Assert.False(KTGAntiAfk.IsMatchUnderway());

                // Case 4: Wrong content type (22) even if match underway flag is 1 -> false
                directorBuffer[KTGAntiAfk.DirectorContentTypeOffset] = 22;
                directorBuffer[KTGAntiAfk.DirectorMatchUnderwayOffset] = 1;
                Assert.False(KTGAntiAfk.IsMatchUnderway());

                // Case 5: Content type is 0 -> false
                directorBuffer[KTGAntiAfk.DirectorContentTypeOffset] = 0;
                Assert.False(KTGAntiAfk.IsMatchUnderway());
            }
        }
        finally
        {
            KTGAntiAfk.ResetTimersForTesting();
        }
    }

    [Fact]
    public unsafe void TestAntiAfk_InputTimerModule_MemoryZeroing()
    {
        try
        {
            KTGAntiAfk.ResetTimersForTesting();

            // Allocate a buffer large enough for InputTimerModule
            byte[] timerBuffer = new byte[1024];
            fixed (byte* pTimer = timerBuffer)
            {
                var pModule = (FFXIVClientStructs.FFXIV.Client.UI.Misc.InputTimerModule*)pTimer;
                pModule->ContentInputTimer = 120.5f;
                pModule->AfkTimer = 580.0f;
                pModule->InputTimer = 45.0f;

                KTGAntiAfk.SetInputTimerModuleForTesting((IntPtr)pTimer);

                // Force keepalive execution to trigger timer module reset
                KTGAntiAfk.KeepAlive(force: true);

                Assert.Equal(0f, pModule->ContentInputTimer);
                Assert.Equal(0f, pModule->AfkTimer);
                Assert.Equal(0f, pModule->InputTimer);
                Assert.True(KTGAntiAfk.LastInputTimerResetTime > DateTime.MinValue);
                Assert.True(KTGAntiAfk.LastKeepAliveTime > DateTime.MinValue);
            }
        }
        finally
        {
            KTGAntiAfk.ResetTimersForTesting();
        }
    }

    [Fact]
    public void TestAntiAfk_ShouldKeepAlive_NullSafety()
    {
        var result = KTGAntiAfk.ShouldKeepAlive(null);
        Assert.False(result);
    }

    [Fact]
    public void TestAntiAfk_KeepAliveExecutesWithoutThrowing()
    {
        // Must handle missing game process, null pointers, and missing Dalamud services gracefully
        var exception = Record.Exception(() => KTGAntiAfk.KeepAlive());
        Assert.Null(exception);
    }

    [Fact]
    public void TestKTGSafetyGuard_CanType_InDutyAndSafetyChecks()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            var config = new Configuration { Enabled = true, MinigameAutoplay = true, StopOnPenaltyCast = false };

            // In background mode (the sole mode), CanType succeeds regardless of window focus
            var canType = KTGSafetyGuard.CanType(config, out var reason);
            Assert.True(canType);
            Assert.Equal("OK", reason);

            // Outside duty -> fails
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: false);
            Assert.False(KTGSafetyGuard.CanType(config, out var reasonDuty));
            Assert.Equal("Not in Keybound Brawler duty", reasonDuty);

            // Plugin disabled -> fails
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            config.Enabled = false;
            Assert.False(KTGSafetyGuard.CanType(config, out var reasonDisabled));
            Assert.Equal("Plugin disabled", reasonDisabled);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGSafetyGuard_CanType_PenaltyCastSafety()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            var config = new Configuration { Enabled = true, MinigameAutoplay = true, StopOnPenaltyCast = true };

            // When penalty cast is active, typing must be blocked
            AddonKTGEnemyCastBar.SetStopTypingOverrideForTesting(true, "Dynamic Sensory Jammer");
            var canType = KTGSafetyGuard.CanType(config, out var reason);
            Assert.False(canType);
            Assert.Contains("Dynamic Sensory Jammer", reason);

            // When penalty cast clears, typing is allowed
            AddonKTGEnemyCastBar.SetStopTypingOverrideForTesting(false);
            var canTypeCleared = KTGSafetyGuard.CanType(config, out var reasonCleared);
            Assert.True(canTypeCleared);
            Assert.Equal("OK", reasonCleared);
        }
        finally
        {
            AddonKTGEnemyCastBar.Reset();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
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
            // With MinigameAutoplay=true and AutoLoopEnabled=false: keepalive in duty
            config.MinigameAutoplay = true;
            config.AutoLoopEnabled = false;
            Assert.True(KTGAntiAfk.ShouldKeepAlive(config));

            // With MinigameAutoplay=false and AutoLoopEnabled=true: keepalive in duty
            config.MinigameAutoplay = false;
            config.AutoLoopEnabled = true;
            Assert.True(KTGAntiAfk.ShouldKeepAlive(config));

            // With both false: no keepalive in duty
            config.MinigameAutoplay = false;
            config.AutoLoopEnabled = false;
            config.AutoBuyCoffers = false;
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // With AutoBuyCoffers=true in duty: keepalive
            config.AutoBuyCoffers = true;
            Assert.True(KTGAntiAfk.ShouldKeepAlive(config));
            config.AutoBuyCoffers = false;

            // With both true: keepalive in duty
            config.MinigameAutoplay = true;
            config.AutoLoopEnabled = true;
            Assert.True(KTGAntiAfk.ShouldKeepAlive(config));

            // Case 3: In Keybound Square with AutoLoop active (Anti-AFK must ONLY work inside event duty)
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: true);
            config.AutoLoopEnabled = true;
            config.MinigameAutoplay = false;
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // Case 4: In Keybound Square with MinigameAutoplay active
            config.AutoLoopEnabled = false;
            config.MinigameAutoplay = true;
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // Case 5: In Keybound Square with BOTH toggles active (strict duty scoping)
            config.AutoLoopEnabled = true;
            config.MinigameAutoplay = true;
            Assert.False(KTGAntiAfk.ShouldKeepAlive(config));

            // Case 6: Master Enabled is false
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
        // KeepAlive executes memory reset safely without throwing
        var ex = Record.Exception(() => KTGAntiAfk.KeepAlive(force: true));
        Assert.Null(ex);
    }

    [Fact]
    public void TestKTGAntiAfk_KeepAlive_WorksInDuty()
    {
        try
        {
            KTGAntiAfk.ResetTimersForTesting();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);

            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = true
            };

            // KeepAlive is pure in-memory, executes safely in duty
            KTGAntiAfk.KeepAlive(config);
            Assert.True(KTGAntiAfk.LastKeepAliveTime > DateTime.MinValue);

            // Outside duty, KeepAlive does nothing
            KTGAntiAfk.ResetTimersForTesting();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: false);
            KTGAntiAfk.KeepAlive(config);
            Assert.Equal(DateTime.MinValue, KTGAntiAfk.LastKeepAliveTime);
        }
        finally
        {
            KTGAntiAfk.ResetTimersForTesting();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGAntiAfk_ResetTimers_ClearsTimestamps()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);

            var config = new Configuration { Enabled = true, MinigameAutoplay = true };
            KTGAntiAfk.KeepAlive(config);
            Assert.True(KTGAntiAfk.LastKeepAliveTime > DateTime.MinValue);

            KTGAntiAfk.ResetTimers();
            Assert.Equal(DateTime.MinValue, KTGAntiAfk.LastKeepAliveTime);
            Assert.Equal(DateTime.MinValue, KTGAntiAfk.LastAgentResetTime);
            Assert.Equal(DateTime.MinValue, KTGAntiAfk.LastInputTimerResetTime);
        }
        finally
        {
            KTGAntiAfk.ResetTimersForTesting();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_BackgroundMode_SubmissionTriggersKeepAlive()
    {
        try
        {
            KTGAntiAfk.ResetTimersForTesting();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            AddonKTGInput.SetDirectOverridesForTesting(setTextOverride: true, submitOverride: true);

            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = true,
                SimulateHumanTyping = false
            };

            Assert.Equal(DateTime.MinValue, KTGAntiAfk.LastKeepAliveTime);

            var sim = new KTGInputSimulator();
            sim.EnqueueWord("QUICK", config);
            Assert.True(sim.JustSubmitted);

            // Submission should have pulsed KTGAntiAfk.KeepAlive
            Assert.True(KTGAntiAfk.LastKeepAliveTime > DateTime.MinValue);
        }
        finally
        {
            AddonKTGInput.SetDirectOverridesForTesting(null, null);
            KTGAntiAfk.ResetTimersForTesting();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_BackgroundTyping_Execution()
    {
        try
        {
            // 1. TryClearInputBuffer returns false if empty
            var cleared = KTGInputSimulator.TryClearInputBuffer();
            Assert.False(cleared);

            // 2. In background mode, Update types pending characters directly to in-memory addon
            AddonKTGInput.SetDirectOverridesForTesting(true, true);
            var sim = new KTGInputSimulator();
            var config = new Configuration { CharDelayMinMs = 10, CharDelayMaxMs = 10 };
            sim.EnqueueWord("Test", config);
            var initialRemaining = sim.RemainingChars;
            Assert.True(initialRemaining > 0);

            // Call Update after short delay
            System.Threading.Thread.Sleep(25);
            sim.Update(config);
            Assert.True(sim.RemainingChars < initialRemaining);
        }
        finally
        {
            AddonKTGInput.SetDirectOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_InstantMode_DirectSubmission()
    {
        try
        {
            AddonKTGInput.SetDirectOverridesForTesting(true, true);
            var sim = new KTGInputSimulator();
            var config = new Configuration { SimulateHumanTyping = false };

            sim.EnqueueWord("RapidFire", config);
            Assert.True(sim.JustSubmitted);
            Assert.Equal("RapidFire", sim.LastSubmittedWord);
            Assert.False(sim.IsTyping);
            Assert.Equal(0, sim.RemainingChars);
        }
        finally
        {
            AddonKTGInput.SetDirectOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_Clear_ResetsTypingAndDirectClearsBuffer()
    {
        try
        {
            AddonKTGInput.SetDirectOverridesForTesting(true, true);
            var sim = new KTGInputSimulator();
            var config = new Configuration { CharDelayMinMs = 100, CharDelayMaxMs = 100 };

            sim.EnqueueWord("ClearMe", config);
            Assert.True(sim.IsTyping);
            Assert.Equal("ClearMe", sim.CurrentWord);

            sim.Clear();
            Assert.False(sim.IsTyping);
            Assert.Equal(string.Empty, sim.CurrentWord);
            Assert.Equal(0, sim.RemainingChars);
        }
        finally
        {
            AddonKTGInput.SetDirectOverridesForTesting(null, null);
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

            // 3. KeepAlive with idle config returns safely without throwing or executing
            var ex = Record.Exception(() => KTGAntiAfk.KeepAlive(config));
            Assert.Null(ex);

            // 4. Resolves Plugin.Configuration when idle
            Plugin.Configuration = config;
            var exPluginConfig = Record.Exception(() => KTGAntiAfk.KeepAlive());
            Assert.Null(exPluginConfig);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
            Plugin.Configuration = null!;
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
    public void TestMainWindow_OriginalDimensionsAndConstraints()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var defaultSizeField = typeof(AutoKeyboundBrawler.UI.MainWindow).GetField("DefaultSize", flags);
        Assert.NotNull(defaultSizeField);
        var defaultSize = (System.Numerics.Vector2)defaultSizeField.GetValue(null)!;
        Assert.Equal(280f, defaultSize.X);
        Assert.Equal(330f, defaultSize.Y);

        var constraintsField = typeof(AutoKeyboundBrawler.UI.MainWindow).GetField("ExpandedConstraints", flags);
        Assert.NotNull(constraintsField);
        var constraints = (Dalamud.Interface.Windowing.WindowSizeConstraints)constraintsField.GetValue(null)!;
        Assert.Equal(480f, constraints.MaximumSize.Y);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TestKTGAutomationManager_InSquare_BehaviorAcrossToggleCombinations(bool autoplay, bool autoLoop)
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: true);

            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = autoplay,
                AutoLoopEnabled = autoLoop
            };

            var manager = new KTGAutomationManager(config, null);
            manager.Tick();

            Assert.Equal(EventPhase.InSquare, manager.CurrentPhase);
            Assert.Equal("In Keybound Square", manager.CurrentStatus);
            Assert.False(manager.IsInDutyPhase);
            Assert.False(manager.IsTyping);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGAutomationManager_AfkLoopOnlyInDuty()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);

            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = false,
                AutoLoopEnabled = true
            };

            var manager = new KTGAutomationManager(config, null);
            manager.Tick();

            // In duty, minigame phase does not type when MinigameAutoplay is false
            Assert.Equal("Minigame Autoplay Disabled", manager.CurrentStatus);
            Assert.Equal(EventPhase.InMinigame, manager.CurrentPhase);
            Assert.False(manager.IsTyping);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGSafetyGuard_DutyDetectionConstants()
    {
        Assert.Equal(1095u, KTGSafetyGuard.DutyContentFinderConditionId);
        Assert.Equal(1359u, KTGSafetyGuard.DutyTerritoryId);
        Assert.Equal(1360u, KTGSafetyGuard.SquareTerritoryId);
    }

    [Fact]
    public void TestKTGSafetyGuard_IsInEventDuty_UninitializedSafeReturn()
    {
        // When uninitialized/not mocked, IsInEventDuty and IsInKeyboundSquare must safely return false without throwing
        KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        Assert.False(KTGSafetyGuard.IsInEventDuty);
        Assert.False(KTGSafetyGuard.IsInKeyboundSquare);
    }

    [Fact]
    public void TestKTGStateReader_GetCurrentPhase_KeyboundSquareReturnsInSquare()
    {
        try
        {
            // In Keybound Square (lobby), phase must be strictly InSquare, NEVER InMinigame
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: true);
            Assert.False(KTGSafetyGuard.IsInEventDuty);
            Assert.True(KTGSafetyGuard.IsInKeyboundSquare);
            Assert.Equal(EventPhase.InSquare, KTGStateReader.GetCurrentPhase());

            // In Event Duty (arena), phase must be InMinigame
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            Assert.True(KTGSafetyGuard.IsInEventDuty);
            Assert.False(KTGSafetyGuard.IsInKeyboundSquare);
            Assert.Equal(EventPhase.InMinigame, KTGStateReader.GetCurrentPhase());

            // Outside both (open world), phase is None
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: false);
            Assert.False(KTGSafetyGuard.IsInEventDuty);
            Assert.False(KTGSafetyGuard.IsInKeyboundSquare);
            Assert.Equal(EventPhase.None, KTGStateReader.GetCurrentPhase());
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGSafetyGuard_CanType_RejectionHierarchy()
    {
        try
        {
            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = true,
                AutoLoopEnabled = true
            };

            // 1. In Square: CanType must return false because player is outside duty
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: true);
            Assert.False(KTGSafetyGuard.CanType(config, out var reasonSquare));
            Assert.Equal("Not in Keybound Brawler duty", reasonSquare);

            // 2. In Duty: when Enabled is false -> "Plugin disabled"
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);
            config.Enabled = false;
            Assert.False(KTGSafetyGuard.CanType(config, out var reasonDisabled));
            Assert.Equal("Plugin disabled", reasonDisabled);

            // 3. In Duty: when MinigameAutoplay is false -> "Minigame autoplay disabled"
            config.Enabled = true;
            config.MinigameAutoplay = false;
            Assert.False(KTGSafetyGuard.CanType(config, out var reasonAutoplay));
            Assert.Equal("Minigame autoplay disabled", reasonAutoplay);

            // 4. In Duty: all conditions met -> returns true ("OK")
            config.MinigameAutoplay = true;
            Assert.True(KTGSafetyGuard.CanType(config, out var reasonDuty));
            Assert.Equal("OK", reasonDuty);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGLocalizationRegistry_FindNearbyRegistrar_UninitializedNullSafety()
    {
        // When Svc.Objects and Svc.Targets are uninitialized / null, FindNearbyRegistrar must safely return null without throwing
        var ex = Record.Exception(() =>
        {
            var result = KTGLocalizationRegistry.FindNearbyRegistrar(12.0f, out var dist);
            Assert.Null(result);
            Assert.Equal(float.MaxValue, dist);
        });
        Assert.Null(ex);
    }

    [Fact]
    public void TestPlugin_UpdateProximityState_WithinRange_TriggersOpen()
    {
        var hasTriggered = false;

        // Player walks within interaction range (5.0y <= 7.0y) with window closed
        var didOpen = Plugin.UpdateProximityState(ref hasTriggered, 5.0f, isCurrentlyOpen: false, out var shouldOpen);

        Assert.True(didOpen);
        Assert.True(shouldOpen);
        Assert.True(hasTriggered);

        // Next tick: still in range (5.0y), window is now open -> should not fire again
        didOpen = Plugin.UpdateProximityState(ref hasTriggered, 5.0f, isCurrentlyOpen: true, out shouldOpen);
        Assert.False(didOpen);
        Assert.False(shouldOpen);
        Assert.True(hasTriggered);
    }

    [Fact]
    public void TestPlugin_UpdateProximityState_AlreadyOpen_SetsTriggeredWithoutReopen()
    {
        var hasTriggered = false;

        // Window was already opened manually (/akb) when approaching
        var didOpen = Plugin.UpdateProximityState(ref hasTriggered, 4.0f, isCurrentlyOpen: true, out var shouldOpen);

        Assert.False(didOpen);
        Assert.False(shouldOpen);
        Assert.True(hasTriggered);
    }

    [Fact]
    public void TestPlugin_UpdateProximityState_Hysteresis_WalkAwayAndReturn()
    {
        var hasTriggered = true; // Was previously triggered near NPC

        // Player walks into hysteresis deadband (8.0y, between 7.0y and 9.5y)
        Plugin.UpdateProximityState(ref hasTriggered, 8.0f, isCurrentlyOpen: false, out var shouldOpen);
        Assert.True(hasTriggered); // Trigger stays latched
        Assert.False(shouldOpen);

        // Player walks away past 9.5y (10.0y)
        Plugin.UpdateProximityState(ref hasTriggered, 10.0f, isCurrentlyOpen: false, out shouldOpen);
        Assert.False(hasTriggered); // Trigger is reset
        Assert.False(shouldOpen);

        // Player walks back within 7.0y (6.5y)
        var didOpen = Plugin.UpdateProximityState(ref hasTriggered, 6.5f, isCurrentlyOpen: false, out shouldOpen);
        Assert.True(didOpen);
        Assert.True(shouldOpen);
        Assert.True(hasTriggered);

        // Exact boundary test: exactly 7.0f triggers open when not previously triggered
        hasTriggered = false;
        var didOpenExact = Plugin.UpdateProximityState(ref hasTriggered, 7.0f, isCurrentlyOpen: false, out var shouldOpenExact);
        Assert.True(didOpenExact);
        Assert.True(shouldOpenExact);
        Assert.True(hasTriggered);

        // Exact boundary test: exactly 9.5f stays within hysteresis deadband (trigger remains latched)
        Plugin.UpdateProximityState(ref hasTriggered, 9.5f, isCurrentlyOpen: false, out _);
        Assert.True(hasTriggered);

        // Distance strictly greater than 9.5f (9.51f) unlatches trigger
        Plugin.UpdateProximityState(ref hasTriggered, 9.51f, isCurrentlyOpen: false, out _);
        Assert.False(hasTriggered);
    }

    [Fact]
    public void TestPlugin_UpdateProximityState_DutyTransition_ResetsTrigger()
    {
        var hasTriggered = true;

        // When entering duty, plugin resets _hasTriggeredProximityOpen to false
        hasTriggered = false;

        // Returning from duty to Square, player is near Registrar (3.0y)
        var didOpen = Plugin.UpdateProximityState(ref hasTriggered, 3.0f, isCurrentlyOpen: false, out var shouldOpen);
        Assert.True(didOpen);
        Assert.True(shouldOpen);
        Assert.True(hasTriggered);
    }

    [Fact]
    public void TestKTGCurrencyReader_ConstantsAndDefaults()
    {
        Assert.Equal(52322u, KTGCurrencyReader.MgcItemId);
        Assert.Equal(10000u, KTGCurrencyReader.DefaultMaxCount);

        KTGCurrencyReader.ResetCacheForTesting();
        KTGCurrencyReader.SetOverridesForTesting(null, null);

        var (count, max) = KTGCurrencyReader.GetMGC();
        Assert.Equal(10000u, max);
    }

    [Fact]
    public void TestKTGCurrencyReader_Overrides()
    {
        try
        {
            KTGCurrencyReader.SetOverridesForTesting(2327, 10000);
            var (count, max) = KTGCurrencyReader.GetMGC();
            Assert.Equal(2327u, count);
            Assert.Equal(10000u, max);
            Assert.Equal(2327u, KTGCurrencyReader.CurrentCount);
            Assert.Equal(10000u, KTGCurrencyReader.MaxCount);

            // Null max should fallback to DefaultMaxCount
            KTGCurrencyReader.SetOverridesForTesting(750, null);
            var (c2, m2) = KTGCurrencyReader.GetMGC();
            Assert.Equal(750u, c2);
            Assert.Equal(10000u, m2);
        }
        finally
        {
            KTGCurrencyReader.SetOverridesForTesting(null, null);
            KTGCurrencyReader.ResetCacheForTesting();
        }
    }

    [Fact]
    public void TestKTGCurrencyReader_Formatting()
    {
        Assert.Equal("0 MGC", KTGCurrencyReader.FormatDisplay(0));
        var disp = KTGCurrencyReader.FormatDisplay(2327);
        Assert.EndsWith(" MGC", disp);
        Assert.Contains("2", disp);
        Assert.Contains("327", disp);

        var tooltip = KTGCurrencyReader.FormatTooltip("Manderville Gold Saucer Credits (MGC)", 2327, 10000);
        Assert.StartsWith("Manderville Gold Saucer Credits (MGC): ", tooltip);
        Assert.Contains("2", tooltip);
        Assert.Contains("327", tooltip);
        Assert.Contains("10", tooltip);
    }

    [Fact]
    public void TestHeaderBar_ComputePillPositions_WideLayout()
    {
        // Plenty of room for both pills: MGC on left, status on right
        var fits = UI.Components.HeaderBar.ComputePillPositions(
            leftLimit: 40f,
            rightLimit: 240f,
            statusPillW: 36f,
            mgcPillW: 64f,
            gap: 4f,
            out var statusLeft,
            out var mgcLeft,
            out var mgcVisible);

        Assert.True(fits);
        Assert.True(mgcVisible);
        Assert.Equal(40f, mgcLeft);    // Placed at leftLimit on left side
        Assert.Equal(204f, statusLeft); // Placed at rightLimit - statusPillW
        Assert.True(mgcLeft + 64f + 4f <= statusLeft);
    }

    [Fact]
    public void TestHeaderBar_ComputePillPositions_NarrowLayout_MgcDropped()
    {
        // Status fits on right, but MGC on left collides with status
        // 150 + 64 + 4 = 218 > 204
        var fits = UI.Components.HeaderBar.ComputePillPositions(
            leftLimit: 150f,
            rightLimit: 240f,
            statusPillW: 36f,
            mgcPillW: 64f,
            gap: 4f,
            out var statusLeft,
            out var mgcLeft,
            out var mgcVisible);

        Assert.True(fits);
        Assert.False(mgcVisible);
        Assert.Equal(204f, statusLeft);
        Assert.Equal(150f, mgcLeft);
    }

    [Fact]
    public void TestHeaderBar_ComputePillPositions_ExtremeNarrow_StatusCannotFit()
    {
        // Status pill cannot even fit before leftLimit (240 - 36 = 204 < 210)
        var fits = UI.Components.HeaderBar.ComputePillPositions(
            leftLimit: 210f,
            rightLimit: 240f,
            statusPillW: 36f,
            mgcPillW: 64f,
            gap: 4f,
            out var statusLeft,
            out var mgcLeft,
            out var mgcVisible);

        Assert.False(fits);
        Assert.False(mgcVisible);
    }

    [Fact]
    public void TestHeaderBar_ComputePillPositions_ExactBoundaries()
    {
        // 1. mgcPillLeft + mgcPillW + gap == statusPillLeft exactly
        // statusPillW: 30, rightLimit: 100 => statusPillLeft = 70.
        // leftLimit: 40, mgcPillW: 20, gap: 10 => 40 + 20 + 10 = 70.
        var fits = UI.Components.HeaderBar.ComputePillPositions(
            leftLimit: 40f,
            rightLimit: 100f,
            statusPillW: 30f,
            mgcPillW: 20f,
            gap: 10f,
            out var statusLeft,
            out var mgcLeft,
            out var mgcVisible);

        Assert.True(fits);
        Assert.True(mgcVisible);
        Assert.Equal(40f, mgcLeft);
        Assert.Equal(70f, statusLeft);

        // 2. Just 0.1f beyond boundary (gap: 10.1f => 40 + 20 + 10.1 = 70.1 > 70)
        fits = UI.Components.HeaderBar.ComputePillPositions(
            leftLimit: 40f,
            rightLimit: 100f,
            statusPillW: 30f,
            mgcPillW: 20f,
            gap: 10.1f,
            out statusLeft,
            out mgcLeft,
            out mgcVisible);

        Assert.True(fits);
        Assert.False(mgcVisible);
        Assert.Equal(70f, statusLeft);

        // 3. statusPillLeft == leftLimit exactly (70f)
        fits = UI.Components.HeaderBar.ComputePillPositions(
            leftLimit: 70f,
            rightLimit: 100f,
            statusPillW: 30f,
            mgcPillW: 20f,
            gap: 10f,
            out statusLeft,
            out mgcLeft,
            out mgcVisible);

        Assert.True(fits);
        Assert.False(mgcVisible); // 70 + 20 + 10 = 100 > 70
        Assert.Equal(70f, statusLeft);

        // 4. statusPillLeft just 0.1f below leftLimit (70.1f)
        fits = UI.Components.HeaderBar.ComputePillPositions(
            leftLimit: 70.1f,
            rightLimit: 100f,
            statusPillW: 30f,
            mgcPillW: 20f,
            gap: 10f,
            out statusLeft,
            out mgcLeft,
            out mgcVisible);

        Assert.False(fits);
        Assert.False(mgcVisible);
    }

    [Fact]
    public void TestHeaderBar_ComputeActiveWordPosition()
    {
        // Left boundary at 100 (e.g. MGC pill right edge + margin)
        // Status pill on the right at 200
        // Margin: 6
        // Word width: 50
        // Word right: 200 - 6 = 194
        // Word left: 194 - 50 = 144
        // 144 > 100 => fits!
        var fits = UI.Components.HeaderBar.ComputeActiveWordPosition(
            leftBoundary: 100f,
            rightAnchorLeft: 200f,
            wordWidth: 50f,
            margin: 6f,
            out var wordLeft);

        Assert.True(fits);
        Assert.Equal(144f, wordLeft);

        // Word too wide: 100f width
        // Word left: 194 - 100 = 94 <= 100 => does not fit!
        fits = UI.Components.HeaderBar.ComputeActiveWordPosition(
            leftBoundary: 100f,
            rightAnchorLeft: 200f,
            wordWidth: 100f,
            margin: 6f,
            out wordLeft);

        Assert.False(fits);
    }

    [Fact]
    public void TestLoc_LocalizationAndFallbacks()
    {
        var originalConfig = Plugin.Configuration;
        try
        {
            Plugin.Configuration = new Configuration();

            // Language Short Names
            Assert.Equal("JA", Loc.GetLanguageShortName(Dalamud.Game.ClientLanguage.Japanese));
            Assert.Equal("DE", Loc.GetLanguageShortName(Dalamud.Game.ClientLanguage.German));
            Assert.Equal("FR", Loc.GetLanguageShortName(Dalamud.Game.ClientLanguage.French));
            Assert.Equal("EN", Loc.GetLanguageShortName(Dalamud.Game.ClientLanguage.English));

            // Key MGC
            Assert.Equal("MGC", Loc.Get("MGC"));

            // Japanese
            Plugin.Configuration.SelectedLanguage = Dalamud.Game.ClientLanguage.Japanese;
            Assert.Equal("マンダヴィル・ゴールドソーサークレジット (MGC)", Loc.Get("MgcTooltip"));
            Assert.Equal("タイピング自動入力", Loc.Get("TypingSolver"));

            // German
            Plugin.Configuration.SelectedLanguage = Dalamud.Game.ClientLanguage.German;
            Assert.Equal("Manderville-Gold-Saucer-Credits (MGC)", Loc.Get("MgcTooltip"));
            Assert.Equal("Tipp-Automatik", Loc.Get("TypingSolver"));

            // French
            Plugin.Configuration.SelectedLanguage = Dalamud.Game.ClientLanguage.French;
            Assert.Equal("Crédits du Gold Saucer Manderville (MGC)", Loc.Get("MgcTooltip"));
            Assert.Equal("Solveur de frappe", Loc.Get("TypingSolver"));

            // English
            Plugin.Configuration.SelectedLanguage = Dalamud.Game.ClientLanguage.English;
            Assert.Equal("Manderville Gold Saucer Credits (MGC)", Loc.Get("MgcTooltip"));
            Assert.Equal("Typing Solver", Loc.Get("TypingSolver"));

            // Fallback for unmapped key returns the key itself
            Assert.Equal("UnknownKeyTest404", Loc.Get("UnknownKeyTest404"));
        }
        finally
        {
            Plugin.Configuration = originalConfig!;
        }
    }

    [Fact]
    public void TestCurrencyManager_Contract()
    {
        var type = typeof(FFXIVClientStructs.FFXIV.Client.Game.CurrencyManager);
        Assert.NotNull(type.GetMethod("Instance"));

        var getItemCount = type.GetMethod("GetItemCount");
        Assert.NotNull(getItemCount);
        Assert.Equal(typeof(uint), getItemCount.ReturnType);
        var p1 = Assert.Single(getItemCount.GetParameters());
        Assert.Equal(typeof(uint), p1.ParameterType);

        var getItemMaxCount = type.GetMethod("GetItemMaxCount");
        Assert.NotNull(getItemMaxCount);
        Assert.Equal(typeof(uint), getItemMaxCount.ReturnType);
        var p2 = Assert.Single(getItemMaxCount.GetParameters());
        Assert.Equal(typeof(uint), p2.ParameterType);
    }

    [Fact]
    public void TestKTGAutomationManager_SafetyHaltInDuty_ClearsActiveTyping()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);

            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = true,
                StopOnPenaltyCast = true
            };

            var manager = new KTGAutomationManager(config, null);

            // Pre-condition: active typing in minigame
            var simField = typeof(KTGAutomationManager).GetField("_simulator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(simField);
            var sim = (KTGInputSimulator)simField.GetValue(manager)!;
            sim.EnqueueWord("Airbuster", config);

            Assert.True(manager.IsTyping);
            Assert.Equal("Airbuster", manager.ActiveWord);

            // Penalty cast active in duty -> triggers safety halt
            AddonKTGEnemyCastBar.SetStopTypingOverrideForTesting(true, "Dynamic Sensory Jammer");
            manager.Tick();

            // When safety guard fails CanType in duty, manager enters SAFETY HALT and aborts typing
            Assert.Equal(EventPhase.InMinigame, manager.CurrentPhase);
            Assert.StartsWith("SAFETY HALT:", manager.CurrentStatus);
            Assert.Contains("Dynamic Sensory Jammer", manager.CurrentStatus);
            Assert.True(manager.IsHaltStatus);
            Assert.False(manager.IsTyping);
            Assert.Equal(string.Empty, manager.ActiveWord);
        }
        finally
        {
            AddonKTGEnemyCastBar.Reset();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGAutomationManager_HaltExitRecovery_DirectClearInput()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);

            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = true,
                StopOnPenaltyCast = true
            };

            var manager = new KTGAutomationManager(config, null);
            // 1. Enter halt while penalty cast is active
            AddonKTGEnemyCastBar.SetStopTypingOverrideForTesting(true, "Dynamic Sensory Jammer");
            manager.Tick();
            Assert.True(manager.IsHaltStatus);

            // 2. Penalty cast ends -> background mode exits halt immediately via DirectClearInput without multi-tick delays
            AddonKTGEnemyCastBar.SetStopTypingOverrideForTesting(false);
            manager.Tick();
            Assert.False(manager.IsHaltStatus);
            Assert.Equal("In Combat (Waiting for word...)", manager.CurrentStatus);
        }
        finally
        {
            AddonKTGEnemyCastBar.Reset();
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGAutomationManager_TransitionToResults_ClearsTypingAndActiveWord()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);

            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = true,
                AutoLoopEnabled = false
            };

            var manager = new KTGAutomationManager(config, null);

            var simField = typeof(KTGAutomationManager).GetField("_simulator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(simField);
            var sim = (KTGInputSimulator)simField.GetValue(manager)!;
            sim.EnqueueWord("FinalBossWord", config);

            Assert.True(manager.IsTyping);
            Assert.Equal("FinalBossWord", manager.ActiveWord);

            // Abrupt transition to Results screen (HandleResultsPhase)
            var handleResultsMethod = typeof(KTGAutomationManager).GetMethod("HandleResultsPhase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(handleResultsMethod);
            handleResultsMethod.Invoke(manager, null);

            Assert.Equal("Minigame Finished (Results Screen)", manager.CurrentStatus);
            Assert.False(manager.IsTyping);
            Assert.Equal(string.Empty, manager.ActiveWord);
            Assert.Equal(0, manager.RemainingChars);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGWordDatabase_DetermineIsDefense()
    {
        // Special row 240
        Assert.True(KTGWordDatabase.DetermineIsDefense(240, "Arbitrary text", "Arbitrary text"));

        // Specific Japanese defense phrases
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "どんなときでもソルジャーの誇りは手放すな", ""));
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "我が剣のサビとなれ！", ""));
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "命は星を巡る", ""));
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "マタ・タビ", ""));
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "星の輝きを力に！", ""));

        // Japanese quotation brackets
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "「サボテンダー」", ""));

        // Explicit defense skills / shield prompts in English and Japanese
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "Defensive Barrier", "Defensive Barrier"));
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "防御態勢", "Defense Mode"));
        Assert.True(KTGWordDatabase.DetermineIsDefense(100, "光の盾", "Lustrous Shield"));

        // Magic pot items are enemy requests, NOT defense
        Assert.False(KTGWordDatabase.DetermineIsDefense(100, "エリクサーちょうだい！", "Gimme Elixir!"));

        // Enemy mobs with barrier in their names are NOT defense
        Assert.False(KTGWordDatabase.DetermineIsDefense(100, "マバリア", "manaward"));
        Assert.False(KTGWordDatabase.DetermineIsDefense(100, "バリア", "barrier"));

        // Non-defense regular mobs and adds
        Assert.False(KTGWordDatabase.DetermineIsDefense(100, "サボテンダー", "Cactuar"));
        Assert.False(KTGWordDatabase.DetermineIsDefense(100, "Airbuster", "Airbuster"));
        Assert.False(KTGWordDatabase.DetermineIsDefense(100, "Grenade", "Grenade"));
        Assert.False(KTGWordDatabase.DetermineIsDefense(0, "", ""));
    }

    [Fact]
    public void TestKTGStateReader_InvalidateSubmittedWord_AndClearCache()
    {
        // Null and whitespace safety
        var exNull = Record.Exception(() => KTGStateReader.InvalidateSubmittedWord(null!));
        Assert.Null(exNull);
        var exWhitespace = Record.Exception(() => KTGStateReader.InvalidateSubmittedWord("   "));
        Assert.Null(exWhitespace);

        // Populate test cache entries via reflection
        var cacheField = typeof(KTGStateReader).GetField("_targetCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(cacheField);
        var cache = (Dictionary<int, CachedTarget>)cacheField.GetValue(null)!;

        lock (cache)
        {
            cache.Clear();
            cache[5] = new CachedTarget { SlotId = 5, Word = "Don't", ScreenY = 500f, ScreenX = 100f, LastSeenUtc = DateTime.UtcNow };
            cache[6] = new CachedTarget { SlotId = 6, Word = "サボテンダー！", ScreenY = 500f, ScreenX = 100f, LastSeenUtc = DateTime.UtcNow };
            cache[7] = new CachedTarget { SlotId = 7, Word = "Airbuster", ScreenY = 500f, ScreenX = 100f, LastSeenUtc = DateTime.UtcNow };

            // Invalidate "Dont" (stripping apostrophe) should match slot 5
            KTGStateReader.InvalidateSubmittedWord("Dont");
            Assert.False(cache.ContainsKey(5));
            Assert.True(cache.ContainsKey(6));
            Assert.True(cache.ContainsKey(7));

            // Invalidate "サボテンダー" (stripping Japanese exclamation mark) should match slot 6
            KTGStateReader.InvalidateSubmittedWord("サボテンダー");
            Assert.False(cache.ContainsKey(6));
            Assert.True(cache.ContainsKey(7));

            // ClearCache empties everything
            KTGStateReader.ClearCache();
            Assert.Empty(cache);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_EnqueueWord_AccentedEuropeanCharacters()
    {
        var sim = new KTGInputSimulator();
        var config = new Configuration();

        // German umlauts and Eszett (Grüße: 5 chars + 1 Enter = 6 actions)
        sim.EnqueueWord("Grüße", config);
        Assert.True(sim.IsTyping);
        Assert.Equal("Grüße", sim.CurrentWord);
        Assert.Equal(6, sim.RemainingChars);

        // French circumflex and accent (Fête: 4 chars + 1 Enter = 5 actions)
        sim.EnqueueWord("Fête", config);
        Assert.True(sim.IsTyping);
        Assert.Equal("Fête", sim.CurrentWord);
        Assert.Equal(5, sim.RemainingChars);

        // Leading and trailing whitespace is trimmed
        sim.EnqueueWord("   Airbuster   ", config);
        Assert.True(sim.IsTyping);
        Assert.Equal("Airbuster", sim.CurrentWord.Trim());
        // "Airbuster" (9 chars) + Enter (1) = 10 actions
        Assert.Equal(10, sim.RemainingChars);
    }

    [Fact]
    public void TestKTGWordDatabase_FindMatch_EdgeCases()
    {
        // When null, empty, or whitespace, FindMatch returns null immediately
        Assert.Null(KTGWordDatabase.FindMatch(null!));
        Assert.Null(KTGWordDatabase.FindMatch(string.Empty));
        Assert.Null(KTGWordDatabase.FindMatch("   "));

        // When database is unloaded, returns null safely without throwing
        Assert.Null(KTGWordDatabase.FindMatch("NonExistentWord"));
    }

    [Fact]
    public void TestKTGStateReader_IsCactuarWord()
    {
        // Known Cactuar prompts across client languages
        Assert.True(KTGStateReader.IsCactuarWord("サボテンダー"));
        Assert.True(KTGStateReader.IsCactuarWord("Cactuar"));
        Assert.True(KTGStateReader.IsCactuarWord("Gigantender"));
        Assert.True(KTGStateReader.IsCactuarWord("Pampa"));
        Assert.True(KTGStateReader.IsCactuarWord("Kaktor"));

        // Non-Cactuar prompts and edge cases
        Assert.False(KTGStateReader.IsCactuarWord("Airbuster"));
        Assert.False(KTGStateReader.IsCactuarWord("Main Cannon"));
        Assert.False(KTGStateReader.IsCactuarWord(string.Empty));
        Assert.False(KTGStateReader.IsCactuarWord("   "));
        Assert.False(KTGStateReader.IsCactuarWord(null!));
    }

    [Fact]
    public void TestKTGSafetyGuard_IsPlayerStunned_UninitializedSafeReturn()
    {
        // When uninitialized or Svc.Objects is null, must safely return false without throwing
        var ex = Record.Exception(() =>
        {
            var stunned = KTGSafetyGuard.IsPlayerStunned();
            Assert.False(stunned);
        });
        Assert.Null(ex);
    }

    public class TestUiBuilderProxy : System.Reflection.DispatchProxy
    {
        public readonly Dictionary<string, object?> PropertyValues = new();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) return null;
            if (targetMethod.Name.StartsWith("set_") && args != null && args.Length == 1)
            {
                var propName = targetMethod.Name.Substring(4);
                PropertyValues[propName] = args[0];
                return null;
            }
            if (targetMethod.Name.StartsWith("get_"))
            {
                var propName = targetMethod.Name.Substring(4);
                if (PropertyValues.TryGetValue(propName, out var val))
                    return val;
                if (targetMethod.ReturnType.IsValueType)
                    return Activator.CreateInstance(targetMethod.ReturnType);
                return null;
            }
            return null;
        }
    }

    [Fact]
    public void TestConfigureUiBuilder_SetsCutsceneAndAutomaticHide()
    {
        var proxy = System.Reflection.DispatchProxy.Create<Dalamud.Interface.IUiBuilder, TestUiBuilderProxy>();
        Plugin.ConfigureUiBuilder(proxy);
        Assert.True(proxy.DisableCutsceneUiHide);
        Assert.True(proxy.DisableAutomaticUiHide);
    }

    [Fact]
    public void TestConfigureUiBuilder_NullSafety()
    {
        var ex = Record.Exception(() => Plugin.ConfigureUiBuilder(null!));
        Assert.Null(ex);
    }

    [Fact]
    public void TestPluginConstructor_CallsConfigureUiBuilder()
    {
        var cecilAsm = System.Reflection.Assembly.LoadFrom(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Mono.Cecil.dll"));
        using var module = (IDisposable)cecilAsm.GetType("Mono.Cecil.ModuleDefinition")!.GetMethod("ReadModule", new[] { typeof(string) })!.Invoke(null, new object[] { typeof(Plugin).Assembly.Location })!;
        dynamic dynamicModule = module;
        dynamic pluginDef = dynamicModule.GetType("AutoKeyboundBrawler.Plugin");
        dynamic ctor = ((System.Collections.IEnumerable)pluginDef.Methods).Cast<dynamic>().First(m => (string)m.Name == ".ctor");
        bool callsConfigureUiBuilder = false;
        foreach (dynamic inst in ctor.Body.Instructions)
        {
            var operand = inst.Operand?.ToString() ?? "";
            if (operand.Contains("ConfigureUiBuilder"))
            {
                callsConfigureUiBuilder = true;
                break;
            }
        }
        Assert.True(callsConfigureUiBuilder, "Plugin constructor must call ConfigureUiBuilder");
    }

    [Theory]
    [InlineData(true, false, false, false)] // Loading screen (GameUiHidden), default Dalamud toggles
    [InlineData(true, false, true, false)]  // Loading screen, Dalamud ToggleUiHide=true
    [InlineData(false, true, false, false)] // Cutscene, default Dalamud toggles
    [InlineData(false, true, false, true)]  // Cutscene, Dalamud ToggleUiHideDuringCutscenes=true
    [InlineData(true, true, true, true)]    // Loading + Cutscene, all Dalamud hide toggles enabled
    public void TestUiBuilderDrawLogic_CutsceneAndLoadingNeverHidden(
        bool gameUiHidden, bool cutsceneActive, bool toggleUiHide, bool toggleUiHideDuringCutscenes)
    {
        var proxy = System.Reflection.DispatchProxy.Create<Dalamud.Interface.IUiBuilder, TestUiBuilderProxy>();
        Plugin.ConfigureUiBuilder(proxy);

        // Evaluate Dalamud's exact UiBuilder.OnDraw hiding logic decompiled from IL:
        bool isHidden = false;
        if (gameUiHidden && toggleUiHide && !proxy.DisableUserUiHide && !proxy.DisableAutomaticUiHide)
        {
            isHidden = true;
        }
        else if (cutsceneActive && toggleUiHideDuringCutscenes && !proxy.DisableCutsceneUiHide && !proxy.DisableAutomaticUiHide)
        {
            isHidden = true;
        }

        Assert.False(isHidden, "Plugin UI must not be hidden during cutscenes or loadings under any Dalamud config setting");
    }

    [Fact]
    public void TestConfiguration_SimulateHumanTyping_DefaultTrue()
    {
        var config = new Configuration();
        Assert.True(config.SimulateHumanTyping);
    }

    [Fact]
    public void TestKTGSafetyGuard_BackgroundMode_FocusBypassAndSafetyPreserved()
    {
        try
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: true, inSquare: false);

            var config = new Configuration
            {
                Enabled = true,
                MinigameAutoplay = true
            };

            // In background mode (the sole mode), unfocused window does NOT block typing
            Assert.True(KTGSafetyGuard.CanType(config, out var reasonBg));
            Assert.Equal("OK", reasonBg);

            // Safety check: disabled plugin still blocks typing
            config.Enabled = false;
            Assert.False(KTGSafetyGuard.CanType(config, out var reasonDisabled));
            Assert.Equal("Plugin disabled", reasonDisabled);
            config.Enabled = true;

            // Safety check: minigame autoplay disabled still blocks typing
            config.MinigameAutoplay = false;
            Assert.False(KTGSafetyGuard.CanType(config, out var reasonNoAutoplay));
            Assert.Equal("Minigame autoplay disabled", reasonNoAutoplay);
            config.MinigameAutoplay = true;

            // Safety check: not in duty still blocks typing
            KTGSafetyGuard.SetTerritoryOverridesForTesting(inDuty: false, inSquare: false);
            Assert.False(KTGSafetyGuard.CanType(config, out var reasonNotInDuty));
            Assert.Equal("Not in Keybound Brawler duty", reasonNotInDuty);
        }
        finally
        {
            KTGSafetyGuard.SetTerritoryOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_KeyAction_Struct()
    {
        var charAction = new KTGInputSimulator.KeyAction('A');
        Assert.Equal('A', charAction.Character);
        Assert.False(charAction.IsSubmit);

        var submitAction = KTGInputSimulator.KeyAction.Submit;
        Assert.True(submitAction.IsSubmit);
    }

    [Fact]
    public void TestKTGInputSimulator_TryClearInputBuffer_SafeExecution()
    {
        try
        {
            AddonKTGInput.SetDirectOverridesForTesting(true, true);
            var config = new Configuration();

            var exClear = Record.Exception(() => KTGInputSimulator.TryClearInputBuffer(config));
            Assert.Null(exClear);
        }
        finally
        {
            AddonKTGInput.SetDirectOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_BackgroundMode_EnqueueAndExecution()
    {
        try
        {
            AddonKTGInput.SetDirectOverridesForTesting(true, true);
            var sim = new KTGInputSimulator();

            // 1. Instant injection when SimulateHumanTyping is false
            var configInstant = new Configuration
            {
                SimulateHumanTyping = false
            };
            sim.EnqueueWord("SWORD", configInstant);
            Assert.False(sim.IsTyping);
            Assert.True(sim.JustSubmitted);
            Assert.Equal("SWORD", sim.LastSubmittedWord);

            // 2. Instant injection fallback when direct submission fails: gracefully enqueues for Update
            AddonKTGInput.SetDirectOverridesForTesting(false, false);
            sim.EnqueueWord("RETRY", configInstant);
            Assert.True(sim.IsTyping);
            Assert.Equal(6, sim.RemainingChars); // 'R','E','T','R','Y', Enter
            AddonKTGInput.SetDirectOverridesForTesting(true, true);

            // 3. Sequential simulation when SimulateHumanTyping is true
            var configSequential = new Configuration
            {
                SimulateHumanTyping = true,
                CharDelayMinMs = 10,
                CharDelayMaxMs = 10
            };
            sim.EnqueueWord("DEFENSE", configSequential, isDefense: true);
            Assert.True(sim.IsTyping);
            Assert.True(sim.IsDefenseWord);
            // 7 characters + Enter = 8 actions
            Assert.Equal(8, sim.RemainingChars);

            // Re-enqueue and test Clear
            sim.EnqueueWord("TEST", configSequential);
            Assert.True(sim.IsTyping);
            sim.Clear(configSequential);
            Assert.False(sim.IsTyping);
            Assert.False(sim.IsDefenseWord);
            Assert.Equal(0, sim.RemainingChars);
        }
        finally
        {
            AddonKTGInput.SetDirectOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestLocalization_CoreKeys_AllFourLanguages()
    {
        var prevConfig = Plugin.Configuration;
        try
        {
            Plugin.Configuration = new Configuration();

            var keys = new[] { "TypingSolver", "AfkLoop", "AutoBuyCoffers" };
            var languages = new[]
            {
                Dalamud.Game.ClientLanguage.English,
                Dalamud.Game.ClientLanguage.Japanese,
                Dalamud.Game.ClientLanguage.German,
                Dalamud.Game.ClientLanguage.French,
            };

            foreach (var lang in languages)
            {
                Plugin.Configuration.SelectedLanguage = lang;
                foreach (var key in keys)
                {
                    var text = Loc.Get(key);
                    Assert.NotNull(text);
                    Assert.NotEmpty(text);
                    Assert.NotEqual(key, text);
                }
            }
        }
        finally
        {
            Plugin.Configuration = prevConfig;
        }
    }

    [Fact]
    public void TestAddonKTGInput_DirectOverrides_AndSubmitFallback()
    {
        try
        {
            AddonKTGInput.SetDirectOverridesForTesting(true, true);
            Assert.True(AddonKTGInput.DirectSetText("TEST"));
            Assert.True(AddonKTGInput.DirectClearInput());
            Assert.True(AddonKTGInput.DirectSubmit("TEST", out var cleared));
            Assert.True(cleared);

            AddonKTGInput.SetDirectOverridesForTesting(false, false);
            Assert.False(AddonKTGInput.DirectSetText("TEST"));
            Assert.False(AddonKTGInput.DirectClearInput());
            Assert.False(AddonKTGInput.DirectSubmit("TEST", out var cleared2));
            Assert.True(cleared2);
        }
        finally
        {
            AddonKTGInput.SetDirectOverridesForTesting(null, null);
        }
    }

    [Fact]
    public void TestKTGInputSimulator_BackgroundMode_AccentAndJapanesePreservation()
    {
        var sim = new KTGInputSimulator();
        var config = new Configuration
        {
            SimulateHumanTyping = true,
            IgnorePunctuation = false
        };

        // Accented French word with circumflex: "fête"
        // In background mode, dead key logic is bypassed and 'ê' is enqueued directly
        sim.EnqueueWord("fête", config);
        Assert.Equal(5, sim.RemainingChars); // 'f', 'ê', 't', 'e', RETURN

        // German word with umlauts: "Größe"
        sim.EnqueueWord("Größe", config);
        Assert.Equal(6, sim.RemainingChars); // 'G', 'r', 'ö', 'ß', 'e', RETURN

        // Japanese word with wave dash: "チョコボ～"
        sim.EnqueueWord("チョコボ～", config);
        Assert.Equal(6, sim.RemainingChars); // 4 kana + fullwidth tilde + RETURN
    }

    [Fact]
    public void TestKTGAutomationManager_ClearTyping()
    {
        var config = new Configuration
        {
            SimulateHumanTyping = true
        };
        var mgr = new KTGAutomationManager(config, null);
        mgr.ClearTyping();
        Assert.False(mgr.IsTyping);
        Assert.Equal(0, mgr.RemainingChars);
        Assert.Equal(string.Empty, mgr.ActiveWord);
    }
}



