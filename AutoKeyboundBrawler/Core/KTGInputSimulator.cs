using AutoKeyboundBrawler.Interop;
using Dalamud.Game;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AutoKeyboundBrawler.Core;

public unsafe class KTGInputSimulator
{
    public readonly struct KeyAction
    {
        public readonly char Character;
        public readonly bool IsSubmit;

        public KeyAction(char c)
        {
            Character = c;
            IsSubmit = false;
        }

        public static KeyAction Submit => new('\0', true);

        private KeyAction(char c, bool isSubmit)
        {
            Character = c;
            IsSubmit = isSubmit;
        }
    }

    private readonly Random _random = new();
    private readonly Queue<KeyAction> _pendingActions = new();
    private readonly Stopwatch _stepStopwatch = new();
    private readonly System.Text.StringBuilder _backgroundTypedText = new();

    private long _nextDelayMs;
    private bool _isSubmittingWord;

    public bool IsTyping => _pendingActions.Count > 0 || _isSubmittingWord;
    public string CurrentWord { get; private set; } = string.Empty;
    public int RemainingChars => _pendingActions.Count;
    public bool IsDefenseWord { get; set; }

    public bool JustSubmitted { get; set; }
    public string LastSubmittedWord { get; private set; } = string.Empty;

    public void EnqueueWord(string word, Configuration config, bool isDefense = false)
    {
        _pendingActions.Clear();
        _backgroundTypedText.Clear();
        CurrentWord = word;
        _isSubmittingWord = false;
        JustSubmitted = false;
        IsDefenseWord = isDefense;

        var isJapanese = (Svc.ClientState != null && Svc.ClientState.ClientLanguage == ClientLanguage.Japanese) ||
                         word.Any(c => (c >= 0x3000 && c <= 0x9FFF) || (c >= 0xFF00 && c <= 0xFFEF));

        // In Japanese, Keybound Brawler prompts strictly require exact character input,
        // including fullwidth tildes/wave dashes (～), middle dots (・), exclamation marks (！),
        // question marks (？), and commas (、). Stripping punctuation causes the minigame to fail the input check.
        // IgnorePunctuation is strictly for Latin-script languages (e.g. English apostrophes/hyphens).
        var textToType = (config.IgnorePunctuation && !isJapanese
            ? Data.KTGWordDatabase.StripPunctuation(word)
            : word).Trim();

        // For Japanese phrases, normalize wave dash (U+301C) to fullwidth tilde (U+FF5E)
        // to strictly match FFXIV's Lumina KTGTypeWordTextData game sheet encoding.
        if (isJapanese)
        {
            textToType = textToType.Replace('\u301C', '\uFF5E');
        }

        // If human typing simulation is disabled,
        // write the full word directly and submit immediately without delay.
        if (!config.SimulateHumanTyping)
        {
            if (!AddonKTGInput.CanAcceptInput())
            {
                CurrentWord = string.Empty;
                _pendingActions.Clear();
                return;
            }

            if (AddonKTGInput.DirectSetText(textToType) && AddonKTGInput.DirectSubmit(textToType, out _))
            {
                JustSubmitted = true;
                LastSubmittedWord = CurrentWord;
                CurrentWord = string.Empty;
                _isSubmittingWord = false;
                _pendingActions.Clear();
                _backgroundTypedText.Clear();
                _stepStopwatch.Restart();
                KTGAntiAfk.KeepAlive(config);
                return;
            }
            // If direct instant submission failed (e.g. addon not ready), queue below so Update handles it
        }

        foreach (var ch in textToType)
        {
            _pendingActions.Enqueue(new KeyAction(ch));
        }
        _pendingActions.Enqueue(KeyAction.Submit);
        _nextDelayMs = GetRandomCharDelay(config);
        _stepStopwatch.Restart();
    }

    public void Clear(Configuration? config = null)
    {
        _pendingActions.Clear();
        _backgroundTypedText.Clear();
        IsDefenseWord = false;
        CurrentWord = string.Empty;
        _isSubmittingWord = false;
        _stepStopwatch.Reset();
        AddonKTGInput.DirectClearInput();
    }

    public void Update(Configuration config)
    {
        if (!IsTyping)
        {
            return;
        }

        if (!AddonKTGInput.CanAcceptInput())
        {
            Clear(config);
            return;
        }

        if (_stepStopwatch.ElapsedMilliseconds < _nextDelayMs)
        {
            return;
        }

        if (_pendingActions.Count == 0)
        {
            CurrentWord = string.Empty;
            _isSubmittingWord = false;
            return;
        }

        var action = _pendingActions.Dequeue();

        if (action.IsSubmit)
        {
            if (!AddonKTGInput.CanAcceptInput())
            {
                Clear(config);
                return;
            }

            var submittedText = _backgroundTypedText.Length > 0 ? _backgroundTypedText.ToString() : CurrentWord;
            if (!AddonKTGInput.DirectSubmit(submittedText, out _))
            {
                // Direct submission failed (e.g. addon transitioning); discard stale submission to avoid sending stale words later
                Clear(config);
                return;
            }

            JustSubmitted = true;
            LastSubmittedWord = CurrentWord;
            CurrentWord = string.Empty;
            _isSubmittingWord = false;
            _pendingActions.Clear();
            _backgroundTypedText.Clear();
            _stepStopwatch.Restart();
            KTGAntiAfk.KeepAlive(config);
            return;
        }

        if (action.Character != '\0')
        {
            if (!AddonKTGInput.CanAcceptInput())
            {
                Clear(config);
                return;
            }

            _backgroundTypedText.Append(action.Character);
            AddonKTGInput.DirectSetText(_backgroundTypedText.ToString());
        }

        if (_pendingActions.Count > 0)
        {
            var nextAction = _pendingActions.Peek();
            _nextDelayMs = nextAction.IsSubmit
                ? Math.Max(30, config.SubmissionDelayMs)
                : GetRandomCharDelay(config);
        }
        else
        {
            CurrentWord = string.Empty;
            _isSubmittingWord = false;
        }

        _stepStopwatch.Restart();
    }

    /// <summary>
    /// Safely clears the typing input buffer directly in memory without sending Escape keystrokes.
    /// </summary>
    public static bool TryClearInputBuffer(Configuration? config = null)
    {
        if (AddonKTGInput.IsInputEmpty())
        {
            return false;
        }
        return AddonKTGInput.DirectClearInput();
    }

    private int GetRandomCharDelay(Configuration config)
    {
        var min = Math.Max(10, config.CharDelayMinMs);
        var max = Math.Max(min, config.CharDelayMaxMs);
        return _random.Next(min, max + 1);
    }
}
