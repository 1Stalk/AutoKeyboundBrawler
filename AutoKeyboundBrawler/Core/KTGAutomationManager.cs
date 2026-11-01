using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.Interop;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoKeyboundBrawler.Core;

public class KTGAutomationManager
{

    public enum SquareSubPhase
    {
        Default,
        WalkingToTrader,
        InteractingTrader,
        Shopping,
        WalkingToRegistrar
    }

    private readonly Configuration _config;
    private readonly IPluginLog? _log;
    private readonly KTGInputSimulator _simulator = new();
    private readonly Random _rng = new();

    private DateTime _lastActionTime = DateTime.MinValue;
    private DateTime _lastSubmissionTime = DateTime.MinValue;
    private DateTime _lastRegistrationTime = DateTime.MinValue;

    // Humanized delay tracking: a random delay is pre-rolled per named action
    // and reused until the action fires, then re-rolled for the next attempt.
    private readonly Dictionary<string, int> _rolledDelays = new();

    private bool _wasInHalt;


    private string _lastSubmittedWord = string.Empty;
    private string _lastTargetWord = string.Empty;
    private int _lastTargetSlotId = -1;

    public string CurrentStatus { get; private set; } = "Idle";
    public string ActiveWord => _simulator.IsTyping ? _simulator.CurrentWord : _lastTargetWord;
    public bool IsTyping => _simulator.IsTyping;
    public int RemainingChars => _simulator.RemainingChars;
    public EventPhase CurrentPhase { get; private set; } = EventPhase.None;

    public bool IsInDutyPhase => CurrentPhase == EventPhase.InMinigame || CurrentStatus.Contains("In Combat");
    public bool IsInQueuePhase => CurrentPhase is EventPhase.InQueue or EventPhase.Commencing or EventPhase.Registering ||
                                  CurrentStatus.Contains("Queued") || CurrentStatus.Contains("Commencing") || CurrentStatus.Contains("Registration");
    public bool IsHaltStatus => CurrentStatus.StartsWith("SAFETY") || CurrentStatus.Contains("Halt") || CurrentStatus.Contains("Obstruction");

    private readonly KTGNavigationManager _nav;
    private SquareSubPhase _squareSubPhase = SquareSubPhase.Default;
    public SquareSubPhase CurrentSquareSubPhase => _squareSubPhase;
    public bool IsNavigationReady => _nav.IsVNavReady;
    private EventPhase _previousPhase = EventPhase.None;

    public KTGAutomationManager(Configuration config, IDalamudPluginInterface? pi, IPluginLog? log)
    {
        _config = config;
        _log = log;
        _nav = new KTGNavigationManager(pi, config, log);
    }

    public KTGAutomationManager(Configuration config, IPluginLog? log) : this(config, null, log)
    {
    }

    public void Tick()
    {
        var isActive = _config.Enabled && (_config.MinigameAutoplay || _config.AutoLoopEnabled || _config.AutoBuyCoffers);
        if (!isActive)
        {
            CurrentPhase = EventPhase.None;
            CurrentStatus = "Idle (Automation paused)";
            if (_simulator.IsTyping)
            {
                _simulator.Clear();
            }
            if (_squareSubPhase != SquareSubPhase.Default)
            {
                _nav.Stop();
                AddonKTGShop.CloseShop();
                _squareSubPhase = SquareSubPhase.Default;
            }
            _lastTargetSlotId = -1;
            _lastTargetWord = string.Empty;
            return;
        }

        CurrentPhase = KTGStateReader.GetCurrentPhase();
        if (_previousPhase != EventPhase.InMinigame && CurrentPhase == EventPhase.InMinigame)
        {
            KTGAntiAfk.ResetTimers();
        }
        _previousPhase = CurrentPhase;

        if (!KTGWordDatabase.IsLoaded || KTGWordDatabase.CurrentLanguage != Svc.ClientState.ClientLanguage)
        {
            KTGWordDatabase.Load(Svc.Data, _log);
        }

        // Keep-alive: Reset game-internal ContentInputTimer and input timers so FFXIV never auto-kicks for inactivity
        if (KTGAntiAfk.ShouldKeepAlive(_config))
        {
            KTGAntiAfk.KeepAlive(_config);
        }

        switch (CurrentPhase)
        {
            case EventPhase.InMinigame:
                HandleMinigamePhase();
                break;

            case EventPhase.Results:
                HandleResultsPhase();
                break;

            case EventPhase.Commencing:
                HandleCommencingPhase();
                break;

            case EventPhase.InQueue:
                HandleInQueuePhase();
                break;

            case EventPhase.Registering:
                HandleRegisteringPhase();
                break;

            case EventPhase.InSquare:
                HandleInSquarePhase();
                break;

            default:
                CurrentStatus = "Waiting for Event Duty...";
                if (_simulator.IsTyping)
                {
                    _simulator.Clear();
                }
                _lastTargetSlotId = -1;
                _lastTargetWord = string.Empty;
                KTGStateReader.ClearCache();
                break;
        }
    }

    // ── Humanized Delay Helpers ─────────────────────────────────────────

    /// <summary>
    /// Returns a pre-rolled random delay (ms) for the named action.
    /// The delay is scaled from config LoopDelayMinMs / LoopDelayMaxMs by the given factors.
    /// </summary>
    private int GetHumanizedDelay(string action, float minFactor = 1f, float maxFactor = 1f)
    {
        if (!_rolledDelays.TryGetValue(action, out var delay))
        {
            var baseMin = Math.Max(200, (int)(_config.LoopDelayMinMs * minFactor));
            var baseMax = Math.Max(baseMin + 100, (int)(_config.LoopDelayMaxMs * maxFactor));
            delay = _rng.Next(baseMin, baseMax + 1);
            _rolledDelays[action] = delay;
        }
        return delay;
    }

    /// <summary>
    /// Returns true when enough time has passed since <paramref name="since"/>
    /// for the named humanized action delay.
    /// </summary>
    private bool IsHumanizedReady(string action, DateTime since, float minFactor = 1f, float maxFactor = 1f)
    {
        var delay = GetHumanizedDelay(action, minFactor, maxFactor);
        return (DateTime.UtcNow - since).TotalMilliseconds > delay;
    }

    /// <summary>
    /// Clears the pre-rolled delay for the named action (or all actions if null).
    /// </summary>
    private void ConsumeHumanizedDelay(string? action = null)
    {
        if (action != null)
        {
            _rolledDelays.Remove(action);
        }
        else
        {
            _rolledDelays.Clear();
        }
    }

    // ── Phase Handlers ──────────────────────────────────────────────────

    private void HandleMinigamePhase()
    {
        if (!_config.MinigameAutoplay)
        {
            CurrentStatus = "Minigame Autoplay Disabled";
            if (_simulator.IsTyping) _simulator.Clear();
            return;
        }

        // 1. Evaluate Safety (Stop on Red Keyboard penalty cast / Stun / Wave Transition)
        if (!KTGSafetyGuard.CanType(_config, out var safetyReason))
        {
            CurrentStatus = $"SAFETY HALT: {safetyReason}";
            _wasInHalt = true;

            _lastTargetSlotId = -1;
            _lastTargetWord = string.Empty;
            if (_simulator.IsTyping)
            {
                _log?.Warning($"[AutoKeyboundBrawler] Safety stop triggered ({safetyReason}). Halting typing simulator without sending keystrokes.");
                _simulator.Clear();
                KTGInputSimulator.TryClearInputBuffer(_config);
            }
            return;
        }

        // 2. Halt Exit Recovery
        // In background mode, clear the minigame text input buffer directly in memory
        if (_wasInHalt)
        {
            if (HandleHaltRecovery())
            {
                return;
            }
        }

        // 3. Handle Just Submitted Event
        if (_simulator.JustSubmitted)
        {
            _lastSubmittedWord = _simulator.LastSubmittedWord;
            _lastSubmissionTime = DateTime.UtcNow;
            _simulator.JustSubmitted = false;

            // Invalidate submitted word from memory cache immediately
            KTGStateReader.InvalidateSubmittedWord(_lastSubmittedWord);
        }

        var submitAgeMs = (DateTime.UtcNow - _lastSubmissionTime).TotalMilliseconds;

        // 4. Post-submission debounce: give game engine 100ms to despawn defeated mob and clear buffer
        if (submitAgeMs < 100)
        {
            CurrentStatus = "Waiting for screen update...";
            return;
        }

        // 5. Read active enemy slots and active targets currently visible on screen
        var activeSlotIds = AddonKTGEWord.ReadActiveSlotIds();
        var currentTargets = AddonKTGEWord.ReadActiveTargets();

        // 5a. If NO enemies are on screen, abort any in-progress typing immediately to avoid failed attack emote
        if (activeSlotIds.Count == 0 && currentTargets.Count == 0)
        {
            if (_simulator.IsTyping)
            {
                _log?.Information($"[AutoKeyboundBrawler] No enemies remaining on screen. Aborting in-progress typing (\"{_simulator.CurrentWord}\").");
                _simulator.Clear();
                KTGInputSimulator.TryClearInputBuffer(_config);
            }
            _lastTargetSlotId = -1;
            _lastTargetWord = string.Empty;
            CurrentStatus = "In Combat (Waiting for word...)";
            return;
        }

        // 5b. Mid-Typing: Validate whether the target we are typing for is still active on screen
        if (_simulator.IsTyping)
        {
            var isTargetStillValid = _simulator.IsDefenseWord;

            if (!isTargetStillValid)
            {
                if (_lastTargetSlotId >= 0)
                {
                    // Target is valid if its enemy slot plate is still active on screen (even if words are obscured / hidden!)
                    // or if it still appears in currentTargets
                    isTargetStillValid = activeSlotIds.Contains(_lastTargetSlotId) ||
                                         currentTargets.Any(t => t.SlotId == _lastTargetSlotId || KTGStateReader.IsSameWord(t.Word, _simulator.CurrentWord));
                }
                else
                {
                    // SlotId was not tracked (-1): valid if living enemies are on screen or same word matches
                    isTargetStillValid = activeSlotIds.Count > 0 || currentTargets.Any(t => KTGStateReader.IsSameWord(t.Word, _simulator.CurrentWord));
                }
            }

            if (!isTargetStillValid)
            {
                _log?.Information($"[AutoKeyboundBrawler] Target for \"{_simulator.CurrentWord}\" (Slot {_lastTargetSlotId}) was defeated. Aborting typing.");
                _simulator.Clear();
                KTGInputSimulator.TryClearInputBuffer(_config);
                _lastTargetSlotId = -1;
                _lastTargetWord = string.Empty;
                CurrentStatus = "Target defeated; selecting new target...";
                // Fall through to select next living target on this tick
            }
            else
            {
                if (_config.AbortForDefense && !_simulator.IsDefenseWord)
                {
                    var currentCasts = AddonKTGEnemyCastBar.ReadActiveCasts();
                    if (KTGStateReader.HasActiveDefenseSentenceOnScreen(currentTargets, currentCasts, out var defenseTarget) &&
                        defenseTarget != null && !string.IsNullOrWhiteSpace(defenseTarget.Word))
                    {
                        _log?.Information($"[AutoKeyboundBrawler] Aborting typing for urgent defense prompt: \"{defenseTarget.Word}\"");
                        _simulator.Clear();
                        KTGInputSimulator.TryClearInputBuffer(_config);
                        _lastTargetSlotId = defenseTarget.SlotId;
                        _lastTargetWord = defenseTarget.Word;
                        CurrentStatus = $"[DEFENSE SENTENCE] Enqueued: \"{defenseTarget.Word}\"";
                        _simulator.EnqueueWord(defenseTarget.Word, _config, isDefense: true);
                        return;
                    }
                }

                CurrentStatus = $"Typing: \"{_simulator.CurrentWord}\" ({_simulator.RemainingChars} left)";
                _simulator.Update(_config);
                return;
            }
        }

        // 6. Pre-Typing Cleanliness: If KTGInput has leftover text, clear it first safely
        if (!AddonKTGInput.IsInputEmpty())
        {
            if (KTGInputSimulator.TryClearInputBuffer(_config))
            {
                CurrentStatus = "Clearing dirty input buffer...";
            }
            return;
        }

        // 7. Look for active prompt word from verified on-screen targets or memory cache
        var target = KTGStateReader.GetBestTarget(_config, _lastSubmittedWord, submitAgeMs);
        if (target != null && !string.IsNullOrWhiteSpace(target.Word))
        {
            _lastTargetSlotId = target.SlotId;
            _lastTargetWord = target.Word;

            var isDefense = KTGStateReader.IsDefenseSentence(target.Word, target.SlotId, target.ScreenY);
            var isNeedles = target.SlotId != 2 && (KTGStateReader.IsCactuarWord(target.Word) ||
                            (KTGStateReader.HasActiveNeedlesTargetOnScreen(out var activeNeedles) && activeNeedles?.SlotId == target.SlotId));
            var isBoss = target.SlotId == 2;
            var tag = isDefense ? "[DEFENSE SENTENCE]" : (isNeedles ? "[CACTUAR/NEEDLES]" : (isBoss ? "[BOSS]" : "[ADD/MOB]"));

            CurrentStatus = $"{tag} Enqueued: \"{target.Word}\"";
            _log?.Information($"[AutoKeyboundBrawler] Target selected {tag} (Slot {target.SlotId}, ScreenY {target.ScreenY:F1}): \"{target.Word}\"");
            _simulator.EnqueueWord(target.Word, _config, isDefense);
        }
        else
        {
            CurrentStatus = "In Combat (Waiting for word...)";
        }
    }

    private bool HandleHaltRecovery()
    {
        AddonKTGInput.DirectClearInput();
        _wasInHalt = false;
        return false;
    }

    private void HandleResultsPhase()
    {
        CurrentStatus = "Minigame Finished (Results Screen)";
        if (_simulator.IsTyping) _simulator.Clear();
        _lastTargetSlotId = -1;
        _lastTargetWord = string.Empty;
        _wasInHalt = false;

        AddonKTGEnemyCastBar.Reset();
        KTGStateReader.ClearCache();

        if (_config.AutoLoopEnabled && IsHumanizedReady("results", _lastActionTime, 1.5f, 2.0f))
        {
            var delay = GetHumanizedDelay("results", 1.5f, 2.0f);
            _log?.Information($"[AutoKeyboundBrawler] Auto-closing results screen (waited {delay}ms)...");
            if (AddonKTGResult.CloseResults())
            {
                _lastActionTime = DateTime.UtcNow;
                ConsumeHumanizedDelay("results");
            }
        }
    }

    private void HandleCommencingPhase()
    {
        CurrentStatus = "Duty Found (Commencing...)";
        if (_simulator.IsTyping) _simulator.Clear();
        _lastTargetSlotId = -1;
        _lastTargetWord = string.Empty;
        _wasInHalt = false;
        KTGStateReader.ClearCache();

        if (_config.AutoLoopEnabled && IsHumanizedReady("commence", _lastActionTime, 0.8f, 1.2f))
        {
            var delay = GetHumanizedDelay("commence", 0.8f, 1.2f);
            _log?.Information($"[AutoKeyboundBrawler] Duty popped! Auto-commencing (waited {delay}ms)...");
            if (AddonContentsFinderConfirm.CommenceDuty())
            {
                _lastActionTime = DateTime.UtcNow;
                ConsumeHumanizedDelay("commence");
            }
        }
    }

    private void HandleInQueuePhase()
    {
        CurrentStatus = "In Keybound Square (Queued for match...)";
        if (_simulator.IsTyping) _simulator.Clear();
        _lastTargetSlotId = -1;
        _lastTargetWord = string.Empty;
        _wasInHalt = false;
        KTGStateReader.ClearCache();

        // Safely clear target if it is Keybound Registrar so we don't hold lock on the NPC while in queue
        if (KTGLocalizationRegistry.IsRegistrar(Svc.Targets.Target))
        {
            Svc.Targets.Target = null;
        }
    }

    private void HandleRegisteringPhase()
    {
        if (KTGStateReader.IsQueuedForDuty())
        {
            CurrentStatus = "In Keybound Square (Queued for match...)";
            return;
        }

        CurrentStatus = "Registration Window Open";
        if (_simulator.IsTyping) _simulator.Clear();
        _lastTargetSlotId = -1;
        _lastTargetWord = string.Empty;
        _wasInHalt = false;
        KTGStateReader.ClearCache();

        if (_config.AutoLoopEnabled && IsHumanizedReady("register", _lastActionTime, 0.5f, 0.9f))
        {
            var delay = GetHumanizedDelay("register", 0.5f, 0.9f);
            _log?.Information($"[AutoKeyboundBrawler] Auto-confirming registration (waited {delay}ms)...");
            if (AddonKTGEnterDialogue.ConfirmRegistration())
            {
                _lastActionTime = DateTime.UtcNow;
                _lastRegistrationTime = DateTime.UtcNow;
                ConsumeHumanizedDelay("register");
                ConsumeHumanizedDelay("postReg");
            }
        }
    }

    private static unsafe int GetEmptyBagSlots()
    {
        try
        {
            var inv = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
            if (inv != null)
            {
                return (int)inv->GetEmptySlotsInBag();
            }
        }
        catch { }
        return 1;
    }

    private void HandleInSquarePhase()
    {
        if (_simulator.IsTyping) _simulator.Clear();
        _lastTargetSlotId = -1;
        _lastTargetWord = string.Empty;
        _wasInHalt = false;
        KTGStateReader.ClearCache();

        // 1. If currently queued in Duty Finder, do NOT talk to any NPC or shop!
        if (KTGStateReader.IsQueuedForDuty())
        {
            CurrentStatus = "In Keybound Square (Queued for match...)";
            if (_squareSubPhase != SquareSubPhase.Default)
            {
                _nav.Stop();
                AddonKTGShop.CloseShop();
                _squareSubPhase = SquareSubPhase.Default;
            }
            return;
        }

        // 2. If user unchecked Auto-Buy Coffers while in an auto-buy subphase, immediately abort!
        if (!_config.AutoBuyCoffers && _squareSubPhase != SquareSubPhase.Default)
        {
            _nav.Stop();
            AddonKTGShop.CloseShop();
            _squareSubPhase = SquareSubPhase.Default;
            _log?.Information("[AutoKeyboundBrawler] Auto-Buy Coffers was disabled by user. Movement and shopping cancelled.");
        }

        // 3. Check Auto-Buy Coffers trigger (MGC >= AutoBuyThreshold)
        var (mgcCount, _) = KTGCurrencyReader.GetMGC();
        if (_config.AutoBuyCoffers && _nav.IsVNavReady && mgcCount >= (uint)_config.AutoBuyThreshold && _squareSubPhase == SquareSubPhase.Default)
        {
            var emptySlots = GetEmptyBagSlots();
            if (emptySlots <= 0)
            {
                CurrentStatus = "In Keybound Square (Inventory full - pausing coffer buy)";
            }
            else
            {
                _squareSubPhase = SquareSubPhase.WalkingToTrader;
                _nav.NavigateTo(KTGLocalizationRegistry.TraderWaypoint, 2.0f);
                _lastActionTime = DateTime.UtcNow;
                CurrentStatus = $"Walking to MGC Trader (MGC: {mgcCount:N0})...";
                return;
            }
        }

        // 4. Handle Auto-Buy Subphases (independent of AutoLoopEnabled!)
        switch (_squareSubPhase)
        {
            case SquareSubPhase.WalkingToTrader:
                HandleWalkingToTrader();
                return;

            case SquareSubPhase.InteractingTrader:
                HandleInteractingTrader();
                return;

            case SquareSubPhase.Shopping:
                HandleShoppingPhase(mgcCount);
                return;

            case SquareSubPhase.WalkingToRegistrar:
                HandleWalkingToRegistrar();
                return;
        }

        // 5. Default subphase: Registrar interaction & queue loop (only if AutoLoopEnabled!)
        CurrentStatus = "In Keybound Square";
        if (!_config.AutoLoopEnabled)
        {
            return;
        }

        HandleRegistrarLoop();
    }

    private void HandleWalkingToTrader()
    {
        _nav.Update();
        var trader = KTGLocalizationRegistry.FindNearbyTrader(15.0f, out var distTrader);
        if (distTrader <= 2.5f || !_nav.IsNavigating)
        {
            _nav.Stop();
            _squareSubPhase = SquareSubPhase.InteractingTrader;
            _lastActionTime = DateTime.UtcNow;
            CurrentStatus = "In Keybound Square (Arrived at MGC Trader)";
        }
        else
        {
            CurrentStatus = $"Walking to MGC Trader ({distTrader:F1}y left)...";
        }
    }

    private void HandleInteractingTrader()
    {
        if (AddonKTGShop.IsShopOpen())
        {
            _squareSubPhase = SquareSubPhase.Shopping;
            CurrentStatus = "Shopping at MGC Trader...";
            return;
        }

        if (AddonKTGShop.HandleOpeningDialogue())
        {
            _lastActionTime = DateTime.UtcNow;
            CurrentStatus = "Opening MGC Trader Shop...";
            return;
        }

        var trader = KTGLocalizationRegistry.FindNearbyTrader(7.0f, out var distTrader);
        if (trader == null)
        {
            _squareSubPhase = SquareSubPhase.WalkingToTrader;
            _nav.NavigateTo(KTGLocalizationRegistry.TraderWaypoint, 2.0f);
            return;
        }

        if (Svc.Targets != null && Svc.Targets.Target?.Address != trader.Address)
        {
            Svc.Targets.Target = trader;
            CurrentStatus = "Targeting MGC Trader...";
            return;
        }

        if (IsHumanizedReady("traderInteract", _lastActionTime, 0.8f, 1.2f))
        {
            try
            {
                unsafe
                {
                    var targetSystem = FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance();
                    if (targetSystem != null)
                    {
                        targetSystem->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)trader.Address, false);
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.Verbose(ex, "[AutoKeyboundBrawler] InteractWithObject Trader threw");
            }

            _lastActionTime = DateTime.UtcNow;
            ConsumeHumanizedDelay("traderInteract");
            CurrentStatus = "Interacting with MGC Trader...";
        }
    }

    private void HandleShoppingPhase(uint mgcCount)
    {
        var emptySlots = GetEmptyBagSlots();
        if (mgcCount < KTGLocalizationRegistry.KeyboundCofferCost || emptySlots <= 0)
        {
            _log?.Information($"[AutoKeyboundBrawler] Finished buying coffers (MGC: {mgcCount}, empty slots: {emptySlots}). Closing shop and returning to Registrar.");
            AddonKTGShop.CloseShop();
            _squareSubPhase = SquareSubPhase.WalkingToRegistrar;
            _nav.NavigateTo(KTGLocalizationRegistry.RegistrarWaypoint, 2.0f);
            _lastActionTime = DateTime.UtcNow;
            CurrentStatus = "Returning to Keybound Registrar...";
            return;
        }

        if (!AddonKTGShop.IsShopOpen())
        {
            if (AddonKTGShop.HandleOpeningDialogue())
            {
                _lastActionTime = DateTime.UtcNow;
            }
            CurrentStatus = "Waiting for Shop window...";
            return;
        }

        if (IsHumanizedReady("buyCoffer", _lastActionTime, 0.4f, 0.7f))
        {
            if (AddonKTGShop.BuyOneCoffer())
            {
                _lastActionTime = DateTime.UtcNow;
                ConsumeHumanizedDelay("buyCoffer");
                CurrentStatus = $"Purchased Keybound Coffer (Remaining MGC: {mgcCount:N0})...";
            }
        }
    }

    private void HandleWalkingToRegistrar()
    {
        if (AddonKTGShop.IsShopOpen() || AddonKTGShop.IsDialogueOpen())
        {
            AddonKTGShop.CloseShop();
        }

        _nav.Update();
        var registrar = KTGLocalizationRegistry.FindNearbyRegistrar(15.0f, out var distReg);
        if (distReg <= 2.5f || !_nav.IsNavigating)
        {
            _nav.Stop();
            _squareSubPhase = SquareSubPhase.Default;
            _lastActionTime = DateTime.UtcNow;
            CurrentStatus = "In Keybound Square";
            _log?.Information("[AutoKeyboundBrawler] Returned to Keybound Registrar.");
        }
        else
        {
            CurrentStatus = $"Returning to Registrar ({distReg:F1}y left)...";
        }
    }

    private void HandleRegistrarLoop()
    {
        // 1. Humanized cooldown after registering (gives server time to acknowledge queue,
        //    with natural variance so re-registration doesn't look robotic)
        var postRegDelay = GetHumanizedDelay("postReg", 1.8f, 3.5f);
        if ((DateTime.UtcNow - _lastRegistrationTime).TotalMilliseconds < postRegDelay)
        {
            CurrentStatus = "In Keybound Square (Waiting for queue confirmation...)";
            return;
        }
        ConsumeHumanizedDelay("postReg");

        CurrentStatus = "In Keybound Square";

        // 2. Advance NPC talk dialog if open (with humanized read delay)
        try
        {
            unsafe
            {
                if (ECommons.GenericHelpers.TryGetAddonByName("Talk", out FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase* talk) &&
                    talk != null &&
                    talk->IsVisible &&
                    talk->IsReady)
                {
                    if (IsHumanizedReady("talk", _lastActionTime, 0.3f, 0.6f))
                    {
                        ECommons.Automation.Callback.Fire(talk, true, 0);
                        _lastActionTime = DateTime.UtcNow;
                        ConsumeHumanizedDelay("talk");
                    }
                    return;
                }
            }
        }
        catch
        {
            // UI or GameGui unavailable
        }

        // Humanized throttle for NPC interaction
        if (!IsHumanizedReady("npcInteract", _lastActionTime, 1.0f, 1.5f))
        {
            return;
        }

        if (Svc.Condition != null && (
            Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInEvent] ||
            Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]))
        {
            return;
        }

        if (Svc.Objects == null)
        {
            return;
        }

        // Find Registrar via unified registry search
        var registrar = KTGLocalizationRegistry.FindNearbyRegistrar(15.0f, out var distance);
        if (registrar == null)
        {
            CurrentStatus = "In Keybound Square (Registrar not in range)";
            return;
        }

        // Distance check: must be within 7 yalms. If farther, navigate to RegistrarWaypoint
        if (distance > 7.0f)
        {
            _nav.NavigateTo(KTGLocalizationRegistry.RegistrarWaypoint, 2.0f);
            _nav.Update();
            CurrentStatus = $"In Keybound Square (Walking to Registrar: {distance:F1}y away)";
            return;
        }
        else if (_nav.IsNavigating)
        {
            _nav.Stop();
        }

        // Step 1: Ensure registrar is actively targeted first
        if (Svc.Targets != null && Svc.Targets.Target?.Address != registrar.Address)
        {
            Svc.Targets.Target = registrar;
            CurrentStatus = "In Keybound Square (Targeting Registrar...)";
            return; // Wait 1 frame so the target system establishes before interaction
        }

        // Step 2: Interact with targeted NPC
        try
        {
            unsafe
            {
                var targetSystem = FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance();
                if (targetSystem != null)
                {
                    targetSystem->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)registrar.Address, false);
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Verbose(ex, "[AutoKeyboundBrawler] InteractWithObject threw");
        }

        var delayInteract = GetHumanizedDelay("npcInteract", 1.0f, 1.5f);
        CurrentStatus = "In Keybound Square (Interacting with Registrar...)";
        _log?.Information($"[AutoKeyboundBrawler] Auto-interacted with Keybound Registrar (waited {delayInteract}ms).");
        _lastActionTime = DateTime.UtcNow;
        ConsumeHumanizedDelay("npcInteract");
    }

    public void ClearTyping()
    {
        _simulator.Clear(_config);
        KTGInputSimulator.TryClearInputBuffer(_config);
        _lastTargetSlotId = -1;
        _lastTargetWord = string.Empty;
    }

    public void Reset()
    {
        _simulator.Clear(_config);
        _nav.Stop();
        _squareSubPhase = SquareSubPhase.Default;
        _lastTargetSlotId = -1;
        _lastRegistrationTime = DateTime.MinValue;
        _wasInHalt = false;
        ConsumeHumanizedDelay();
        AddonKTGEnemyCastBar.Reset();
        KTGStateReader.ClearCache();
        CurrentPhase = EventPhase.None;
        CurrentStatus = "Reset";
    }
}
