using AutoKeyboundBrawler.Data;
using AutoKeyboundBrawler.Interop;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;

namespace AutoKeyboundBrawler.Core;

public class KTGAutomationManager
{
    private enum HaltRecoveryState
    {
        None,
        SendEnter,
        WaitAfterEnter,
        WaitAfterEscape
    }

    private readonly Configuration _config;
    private readonly IPluginLog _log;
    private readonly KTGInputSimulator _simulator = new();
    private readonly Random _rng = new();

    private DateTime _lastActionTime = DateTime.MinValue;
    private DateTime _lastSubmissionTime = DateTime.MinValue;
    private DateTime _lastRegistrationTime = DateTime.MinValue;

    // Humanized delay tracking: a random delay is pre-rolled per named action
    // and reused until the action fires, then re-rolled for the next attempt.
    private readonly Dictionary<string, int> _rolledDelays = new();

    private bool _wasInHalt;
    private HaltRecoveryState _haltRecoveryState = HaltRecoveryState.None;
    private DateTime _lastHaltRecoveryTime = DateTime.MinValue;

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

    public KTGAutomationManager(Configuration config, IPluginLog log)
    {
        _config = config;
        _log = log;
    }

    public void Tick()
    {
        CurrentPhase = KTGStateReader.GetCurrentPhase();

        var isActive = _config.MinigameAutoplay || _config.AutoLoopEnabled || _config.Enabled;
        if (!isActive)
        {
            CurrentStatus = "Idle (Automation paused)";
            if (_simulator.IsTyping)
            {
                _simulator.Clear();
            }
            return;
        }

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

        // 1. Evaluate Safety (Stop on Red Keyboard penalty cast / Stun)
        if (!KTGSafetyGuard.CanType(_config, out var safetyReason))
        {
            CurrentStatus = $"SAFETY HALT: {safetyReason}";
            _wasInHalt = true;
            _haltRecoveryState = HaltRecoveryState.SendEnter;
            _lastTargetSlotId = -1;
            _lastTargetWord = string.Empty;
            if (_simulator.IsTyping)
            {
                _log.Warning($"[AutoKeyboundBrawler] Safety stop triggered ({safetyReason}). Halting typing simulator without sending keystrokes.");
                _simulator.Clear();
            }
            return;
        }

        // 2. Halt Exit Recovery
        // When exiting halt, the text input window is NOT active.
        // We MUST press Enter first to activate the text window, and only then press Esc (if text exists) or proceed.
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

        // 5. Mid-Typing: Commit to current word - never cancel to switch targets.
        // Defense & priority is fully resolved BEFORE typing starts (in step 7).
        // Only KTGSafetyGuard halt (step 1) can abort mid-type.
        if (_simulator.IsTyping)
        {
            CurrentStatus = $"Typing: \"{_simulator.CurrentWord}\" ({_simulator.RemainingChars} left)";
            _simulator.Update(_config);
            return;
        }

        // 6. Pre-Typing Cleanliness: If KTGInput has leftover text, clear it first safely
        if (!AddonKTGInput.IsInputEmpty())
        {
            if (KTGInputSimulator.TryClearInputBuffer())
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
            _log.Information($"[AutoKeyboundBrawler] Target selected {tag} (Slot {target.SlotId}, ScreenY {target.ScreenY:F1}): \"{target.Word}\"");
            _simulator.EnqueueWord(target.Word, _config);
        }
        else
        {
            CurrentStatus = "In Combat (Waiting for word...)";
        }
    }

    private bool HandleHaltRecovery()
    {
        switch (_haltRecoveryState)
        {
            case HaltRecoveryState.SendEnter:
                _log.Information("[AutoKeyboundBrawler] Exiting halt: pressing Enter to activate text input window...");
                CurrentStatus = "Halt Recovery: Activating text input (Enter)...";
                KTGInputSimulator.SendKey(FFXIVClientStructs.FFXIV.Client.System.Input.SeVirtualKey.RETURN);
                _lastHaltRecoveryTime = DateTime.UtcNow;
                _haltRecoveryState = HaltRecoveryState.WaitAfterEnter;
                return true;

            case HaltRecoveryState.WaitAfterEnter:
                // Wait for the game to process Enter and activate the text window
                if ((DateTime.UtcNow - _lastHaltRecoveryTime).TotalMilliseconds < 120)
                {
                    CurrentStatus = "Halt Recovery: Waiting for text input focus...";
                    return true;
                }

                // Window is now active. Check if leftover text needs to be cancelled.
                if (!AddonKTGInput.IsInputEmpty())
                {
                    _log.Information("[AutoKeyboundBrawler] Input has cancelled text after halt. Pressing Escape to clear...");
                    CurrentStatus = "Halt Recovery: Cancelling leftover text (Esc)...";
                    KTGInputSimulator.SendKey(FFXIVClientStructs.FFXIV.Client.System.Input.SeVirtualKey.ESCAPE);
                    _lastHaltRecoveryTime = DateTime.UtcNow;
                    _haltRecoveryState = HaltRecoveryState.WaitAfterEscape;
                    return true;
                }

                // Buffer is already empty, recovery complete!
                _log.Information("[AutoKeyboundBrawler] Halt recovery complete (input clean). Resuming normal typing.");
                _wasInHalt = false;
                _haltRecoveryState = HaltRecoveryState.None;
                return false;

            case HaltRecoveryState.WaitAfterEscape:
                if ((DateTime.UtcNow - _lastHaltRecoveryTime).TotalMilliseconds < 120)
                {
                    CurrentStatus = "Halt Recovery: Waiting for buffer clear...";
                    return true;
                }

                if (!AddonKTGInput.IsInputEmpty())
                {
                    // If buffer is still dirty after 300ms, retry Escape once
                    if ((DateTime.UtcNow - _lastHaltRecoveryTime).TotalMilliseconds > 300)
                    {
                        KTGInputSimulator.SendKey(FFXIVClientStructs.FFXIV.Client.System.Input.SeVirtualKey.ESCAPE);
                        _lastHaltRecoveryTime = DateTime.UtcNow;
                    }
                    return true;
                }

                _log.Information("[AutoKeyboundBrawler] Halt recovery complete (buffer cleared). Resuming normal typing.");
                _wasInHalt = false;
                _haltRecoveryState = HaltRecoveryState.None;
                return false;

            default:
                _wasInHalt = false;
                _haltRecoveryState = HaltRecoveryState.None;
                return false;
        }
    }

    private void HandleResultsPhase()
    {
        CurrentStatus = "Minigame Finished (Results Screen)";
        if (_simulator.IsTyping) _simulator.Clear();
        _wasInHalt = false;
        _haltRecoveryState = HaltRecoveryState.None;
        AddonKTGEnemyCastBar.Reset();
        KTGStateReader.ClearCache();

        if ((_config.AutoLeaveResults || _config.AutoLoopEnabled) && IsHumanizedReady("results", _lastActionTime, 1.5f, 2.0f))
        {
            var delay = GetHumanizedDelay("results", 1.5f, 2.0f);
            _log.Information($"[AutoKeyboundBrawler] Auto-closing results screen (waited {delay}ms)...");
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
        _wasInHalt = false;
        _haltRecoveryState = HaltRecoveryState.None;
        KTGStateReader.ClearCache();

        if ((_config.AutoConfirmRegister || _config.AutoCommenceDuty || _config.AutoLoopEnabled) && IsHumanizedReady("commence", _lastActionTime, 0.8f, 1.2f))
        {
            var delay = GetHumanizedDelay("commence", 0.8f, 1.2f);
            _log.Information($"[AutoKeyboundBrawler] Duty popped! Auto-commencing (waited {delay}ms)...");
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
        _wasInHalt = false;
        _haltRecoveryState = HaltRecoveryState.None;
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
        _wasInHalt = false;
        _haltRecoveryState = HaltRecoveryState.None;
        KTGStateReader.ClearCache();

        if ((_config.AutoConfirmRegister || _config.AutoLoopEnabled) && IsHumanizedReady("register", _lastActionTime, 0.5f, 0.9f))
        {
            var delay = GetHumanizedDelay("register", 0.5f, 0.9f);
            _log.Information($"[AutoKeyboundBrawler] Auto-confirming registration (waited {delay}ms)...");
            if (AddonKTGEnterDialogue.ConfirmRegistration())
            {
                _lastActionTime = DateTime.UtcNow;
                _lastRegistrationTime = DateTime.UtcNow;
                ConsumeHumanizedDelay("register");
                ConsumeHumanizedDelay("postReg");
            }
        }
    }

    private void HandleInSquarePhase()
    {
        if (_simulator.IsTyping) _simulator.Clear();
        _wasInHalt = false;
        _haltRecoveryState = HaltRecoveryState.None;
        KTGStateReader.ClearCache();

        // 1. If currently queued in Duty Finder, do NOT talk to the NPC!
        if (KTGStateReader.IsQueuedForDuty())
        {
            CurrentStatus = "In Keybound Square (Queued for match...)";
            return;
        }

        // 2. Humanized cooldown after registering (gives server time to acknowledge queue,
        //    with natural variance so re-registration doesn't look robotic)
        var postRegDelay = GetHumanizedDelay("postReg", 1.8f, 3.5f);
        if ((DateTime.UtcNow - _lastRegistrationTime).TotalMilliseconds < postRegDelay)
        {
            CurrentStatus = "In Keybound Square (Waiting for queue confirmation...)";
            return;
        }
        ConsumeHumanizedDelay("postReg");

        CurrentStatus = "In Keybound Square";

        // 3. Advance NPC talk dialog if open (with humanized read delay)
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

        if (!_config.AutoTalkRegistrar && !_config.AutoLoopEnabled)
        {
            return;
        }

        // Humanized throttle for NPC interaction
        if (!IsHumanizedReady("npcInteract", _lastActionTime, 1.0f, 1.5f))
        {
            return;
        }

        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInEvent] ||
            Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas])
        {
            return;
        }

        // Find Registrar via unified registry search
        var registrar = KTGLocalizationRegistry.FindNearbyRegistrar(12.0f, out var distance);
        if (registrar == null)
        {
            CurrentStatus = "In Keybound Square (Registrar not in range)";
            return;
        }

        // Distance check: must be within 7 yalms
        if (distance > 7.0f)
        {
            CurrentStatus = $"In Keybound Square (Registrar is {distance:F1}y away - walk closer)";
            return;
        }

        // Step 1: Ensure registrar is actively targeted first
        if (Svc.Targets.Target?.Address != registrar.Address)
        {
            Svc.Targets.Target = registrar;
            CurrentStatus = "In Keybound Square (Targeting Registrar...)";
            return; // Wait 1 frame so the target system establishes before interaction
        }

        // Step 2: Interact with targeted NPC
        unsafe
        {
            FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)registrar.Address, false);
        }

        var delayInteract = GetHumanizedDelay("npcInteract", 1.0f, 1.5f);
        CurrentStatus = "In Keybound Square (Interacting with Registrar...)";
        _log.Information($"[AutoKeyboundBrawler] Auto-interacted with Keybound Registrar (waited {delayInteract}ms).");
        _lastActionTime = DateTime.UtcNow;
        ConsumeHumanizedDelay("npcInteract");
    }

    public void Reset()
    {
        _simulator.Clear();
        _lastTargetSlotId = -1;
        _lastRegistrationTime = DateTime.MinValue;
        _wasInHalt = false;
        _haltRecoveryState = HaltRecoveryState.None;
        _lastHaltRecoveryTime = DateTime.MinValue;
        ConsumeHumanizedDelay();
        AddonKTGEnemyCastBar.Reset();
        KTGStateReader.ClearCache();
        CurrentPhase = EventPhase.None;
        CurrentStatus = "Reset";
    }
}
