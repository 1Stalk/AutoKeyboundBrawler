using AutoKeyboundBrawler.Interop;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using System;

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

    private DateTime _lastActionTime = DateTime.MinValue;
    private DateTime _lastSubmissionTime = DateTime.MinValue;
    private DateTime _lastPreemptTime = DateTime.MinValue;
    private DateTime _lastRegistrationTime = DateTime.MinValue;

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
    public EventPhase CurrentPhase => KTGStateReader.GetCurrentPhase();

    public KTGAutomationManager(Configuration config, IPluginLog log)
    {
        _config = config;
        _log = log;
    }

    public void Tick()
    {
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

        var phase = KTGStateReader.GetCurrentPhase();

        switch (phase)
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

        // 5. Mid-Typing Processing & Preemption
        if (_simulator.IsTyping)
        {
            // CRITICAL: We DO NOT cancel typing when words disappear or hide on screen!
            // In Keybound Brawler, the "Phrases partially obscured!" / confusion debuff periodically hides
            // on-screen words for several seconds. When words are hidden, the player is expected to finish
            // typing the word from memory. The simulator smoothly types the word to completion and sends Enter.

            // The ONLY mid-typing preemption allowed is if we are typing the BOSS (Slot 2)
            // and an urgent Defensive Sentence appears to survive a Tankbuster/ultimate attack.
            if (_lastTargetSlotId == 2 && (DateTime.UtcNow - _lastPreemptTime).TotalMilliseconds > 2000)
            {
                if (_config.PrioritizeDefense &&
                    _config.AbortForDefense &&
                    KTGStateReader.HasActiveDefenseSentenceOnScreen(out var defenseTarget) &&
                    defenseTarget != null &&
                    defenseTarget.SlotId != 2 &&
                    !string.Equals(defenseTarget.Word, _simulator.CurrentWord, StringComparison.OrdinalIgnoreCase))
                {
                    _lastPreemptTime = DateTime.UtcNow;
                    _log.Information($"[AutoKeyboundBrawler] Urgent defense prompt \"{defenseTarget.Word}\" (Slot {defenseTarget.SlotId}) detected while typing boss! Preempting boss to raise defense immediately.");
                    _simulator.Clear();
                    KTGInputSimulator.TryClearInputBuffer();
                    _lastTargetSlotId = -1;
                    CurrentStatus = "Switching to urgent defense prompt!";
                    return;
                }
            }

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

            var isDefense = KTGStateReader.IsDefenseSentence(target.Word, target.SlotId);
            var isBoss = target.SlotId == 2;
            var tag = isDefense ? "[DEFENSE SENTENCE]" : (isBoss ? "[BOSS]" : "[ADD/MOB]");

            CurrentStatus = $"{tag} Enqueued: \"{target.Word}\"";
            _log.Information($"[AutoKeyboundBrawler] Target selected {tag} (Slot {target.SlotId}): \"{target.Word}\"");
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

        if ((_config.AutoLeaveResults || _config.AutoLoopEnabled) && (DateTime.UtcNow - _lastActionTime).TotalMilliseconds > 1000)
        {
            _log.Information("[AutoKeyboundBrawler] Auto-closing results screen...");
            if (AddonKTGResult.CloseResults())
            {
                _lastActionTime = DateTime.UtcNow;
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

        if ((_config.AutoConfirmRegister || _config.AutoCommenceDuty || _config.AutoLoopEnabled) && (DateTime.UtcNow - _lastActionTime).TotalMilliseconds > 500)
        {
            _log.Information("[AutoKeyboundBrawler] Duty popped! Auto-commencing...");
            if (AddonContentsFinderConfirm.CommenceDuty())
            {
                _lastActionTime = DateTime.UtcNow;
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
        if (Svc.Targets.Target != null &&
            (Svc.Targets.Target.BaseId == 1058751 ||
             Svc.Targets.Target.Name.TextValue.Contains("Keybound Registrar", StringComparison.OrdinalIgnoreCase)))
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

        if ((_config.AutoConfirmRegister || _config.AutoLoopEnabled) && (DateTime.UtcNow - _lastActionTime).TotalMilliseconds > 600)
        {
            _log.Information("[AutoKeyboundBrawler] Auto-confirming registration (pressing Register)...");
            if (AddonKTGEnterDialogue.ConfirmRegistration())
            {
                _lastActionTime = DateTime.UtcNow;
                _lastRegistrationTime = DateTime.UtcNow;
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

        // 2. Cooldown debounce after registering (gives server time to acknowledge queue)
        if ((DateTime.UtcNow - _lastRegistrationTime).TotalSeconds < 8.0)
        {
            CurrentStatus = "In Keybound Square (Waiting for queue confirmation...)";
            return;
        }

        CurrentStatus = "In Keybound Square";

        // 3. Advance NPC talk dialog if open
        unsafe
        {
            if (ECommons.GenericHelpers.TryGetAddonByName("Talk", out FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase* talk) &&
                talk != null &&
                talk->IsVisible &&
                talk->IsReady)
            {
                ECommons.Automation.Callback.Fire(talk, true, 0);
                return;
            }
        }

        if (!_config.AutoTalkRegistrar && !_config.AutoLoopEnabled)
        {
            return;
        }

        // Throttle NPC interaction
        if ((DateTime.UtcNow - _lastActionTime).TotalMilliseconds < 1500)
        {
            return;
        }

        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInEvent] ||
            Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas])
        {
            return;
        }

        // Find Registrar
        Dalamud.Game.ClientState.Objects.Types.IGameObject? registrar = null;

        // Fast path: Current target is already Keybound Registrar
        if (Svc.Targets.Target != null &&
            (Svc.Targets.Target.BaseId == 1058751 ||
             Svc.Targets.Target.Name.TextValue.Contains("Keybound Registrar", StringComparison.OrdinalIgnoreCase)))
        {
            registrar = Svc.Targets.Target;
        }
        else
        {
            // Full enumeration of all game objects (including EventNPCs > index 200)
            foreach (var obj in Svc.Objects)
            {
                if (obj == null) continue;

                if (obj.BaseId == 1058751 ||
                    obj.Name.TextValue.Contains("Keybound Registrar", StringComparison.OrdinalIgnoreCase))
                {
                    registrar = obj;
                    break;
                }
            }
        }

        if (registrar == null)
        {
            CurrentStatus = "In Keybound Square (Registrar not in range)";
            return;
        }

        // Distance check: must be within 7 yalms
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer != null)
        {
            var distance = System.Numerics.Vector3.Distance(localPlayer.Position, registrar.Position);
            if (distance > 7.0f)
            {
                CurrentStatus = $"In Keybound Square (Registrar is {distance:F1}y away - walk closer)";
                return;
            }
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

        CurrentStatus = "In Keybound Square (Interacting with Registrar...)";
        _log.Information("[AutoKeyboundBrawler] Auto-interacted with Keybound Registrar.");
        _lastActionTime = DateTime.UtcNow;
    }

    public void Reset()
    {
        _simulator.Clear();
        _lastTargetSlotId = -1;
        _lastPreemptTime = DateTime.MinValue;
        _lastRegistrationTime = DateTime.MinValue;
        _wasInHalt = false;
        _haltRecoveryState = HaltRecoveryState.None;
        _lastHaltRecoveryTime = DateTime.MinValue;
        AddonKTGEnemyCastBar.Reset();
        KTGStateReader.ClearCache();
        CurrentStatus = "Reset";
    }
}
