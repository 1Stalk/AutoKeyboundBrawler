using FFXIVClientStructs.FFXIV.Client.System.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AutoKeyboundBrawler.Core;

public unsafe class KTGInputSimulator
{
    private readonly Random _random = new();
    private readonly Queue<SeVirtualKey> _pendingKeys = new();
    private readonly Stopwatch _stepStopwatch = new();

    private long _nextDelayMs;
    private bool _isSubmittingWord;

    public bool IsTyping => _pendingKeys.Count > 0 || _isSubmittingWord;
    public string CurrentWord { get; private set; } = string.Empty;
    public int RemainingChars => _pendingKeys.Count;

    public bool JustSubmitted { get; set; }
    public string LastSubmittedWord { get; private set; } = string.Empty;

    public void EnqueueWord(string word, Configuration config)
    {
        _pendingKeys.Clear();
        CurrentWord = word;
        _isSubmittingWord = false;
        JustSubmitted = false;

        var textToType = config.IgnorePunctuation
            ? Data.KTGWordDatabase.StripPunctuation(word)
            : word;

        foreach (var ch in textToType)
        {
            if (TryCharToVirtualKey(ch, out var vk))
            {
                _pendingKeys.Enqueue(vk);
            }
        }

        // Always end with Enter to submit the word
        _pendingKeys.Enqueue(SeVirtualKey.RETURN);

        _nextDelayMs = GetRandomCharDelay(config);
        _stepStopwatch.Restart();
    }

    public void Clear()
    {
        _pendingKeys.Clear();
        CurrentWord = string.Empty;
        _isSubmittingWord = false;
        _stepStopwatch.Reset();
    }

    public void Update(Configuration config)
    {
        if (!IsTyping)
        {
            return;
        }

        if (_stepStopwatch.ElapsedMilliseconds < _nextDelayMs)
        {
            return;
        }

        if (_pendingKeys.Count == 0)
        {
            CurrentWord = string.Empty;
            _isSubmittingWord = false;
            return;
        }

        var key = _pendingKeys.Dequeue();
        SendKey(key);

        if (key == SeVirtualKey.RETURN)
        {
            JustSubmitted = true;
            LastSubmittedWord = CurrentWord;
            CurrentWord = string.Empty;
            _isSubmittingWord = false;
            _pendingKeys.Clear();
            _stepStopwatch.Restart();
            return;
        }

        if (_pendingKeys.Count > 0)
        {
            var nextKey = _pendingKeys.Peek();
            _nextDelayMs = nextKey == SeVirtualKey.RETURN
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

    private static DateTime _lastEscapeTime = DateTime.MinValue;

    /// <summary>
    /// Safely clears the typing input buffer:
    /// - Checks that KTGInput actually contains text before sending Escape.
    /// - Verifies that KTGInput is active before sending Escape (if inactive, presses Enter first to activate).
    /// - Strictly throttles Escape to at most once per 600ms to prevent double-press triggering the Exit Duty / System Menu.
    /// </summary>
    public static bool TryClearInputBuffer()
    {
        // 1. NEVER send Escape if the input buffer is already empty!
        // Pressing Escape on an empty text input in FFXIV cancels target and opens the Exit Duty / System Menu!
        if (Interop.AddonKTGInput.IsInputEmpty())
        {
            return false;
        }

        // 2. If the text input is NOT active, NEVER send Escape!
        // Pressing Escape on an unfocused text window cancels target and opens the System Menu.
        // Send Enter first to activate the text window.
        if (!Interop.AddonKTGInput.IsInputActive())
        {
            SendKey(SeVirtualKey.RETURN);
            return false;
        }

        // 3. Strict throttle: at most once every 600ms to eliminate any chance of double-tap
        if ((DateTime.UtcNow - _lastEscapeTime).TotalMilliseconds < 600)
        {
            return false;
        }

        _lastEscapeTime = DateTime.UtcNow;
        SendKey(SeVirtualKey.ESCAPE);
        return true;
    }

    public static void SendEscape()
    {
        TryClearInputBuffer();
    }

    private int GetRandomCharDelay(Configuration config)
    {
        var min = Math.Max(10, config.CharDelayMinMs);
        var max = Math.Max(min, config.CharDelayMaxMs);
        return _random.Next(min, max + 1);
    }

    public static void SendKey(SeVirtualKey key)
    {
        try
        {
            var manager = InputDeviceManager.Instance();
            if (manager != null && manager->KeyboardDevice != null)
            {
                manager->KeyboardDevice->SendKey(key);
                return;
            }
        }
        catch
        {
            // Fallback to Win32 keybd_event if KeyboardDevice is inaccessible
        }

        SendWin32Key(key);
    }

    private static void SendWin32Key(SeVirtualKey key)
    {
        const uint KEYEVENTF_KEYUP = 0x0002;
        var vk = (byte)key;
        PInvoke.keybd_event(vk, 0, 0, 0);
        PInvoke.keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
    }

    private static bool TryCharToVirtualKey(char c, out SeVirtualKey key)
    {
        if (c is >= 'a' and <= 'z')
        {
            key = (SeVirtualKey)((int)SeVirtualKey.A + (c - 'a'));
            return true;
        }

        if (c is >= 'A' and <= 'Z')
        {
            key = (SeVirtualKey)((int)SeVirtualKey.A + (c - 'A'));
            return true;
        }

        if (c is >= '0' and <= '9')
        {
            key = (SeVirtualKey)((int)SeVirtualKey.KEY_0 + (c - '0'));
            return true;
        }

        if (c == ' ')
        {
            key = SeVirtualKey.SPACE;
            return true;
        }

        // VK_OEM_PERIOD = 0xBE
        if (c == '.')
        {
            key = (SeVirtualKey)0xBE;
            return true;
        }

        // VK_OEM_COMMA = 0xBC
        if (c == ',')
        {
            key = (SeVirtualKey)0xBC;
            return true;
        }

        // VK_OEM_MINUS = 0xBD
        if (c == '-')
        {
            key = (SeVirtualKey)0xBD;
            return true;
        }

        // VK_OEM_7 = 0xDE (single quote / apostrophe)
        if (c == '\'')
        {
            key = (SeVirtualKey)0xDE;
            return true;
        }

        if (c is '\r' or '\n')
        {
            key = SeVirtualKey.RETURN;
            return true;
        }

        key = SeVirtualKey.NO_KEY;
        return false;
    }

    private static class PInvoke
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
    }
}
