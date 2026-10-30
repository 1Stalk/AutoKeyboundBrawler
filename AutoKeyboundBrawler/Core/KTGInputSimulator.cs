using AutoKeyboundBrawler.Interop;
using Dalamud.Game;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AutoKeyboundBrawler.Core;

public unsafe class KTGInputSimulator
{
    public readonly struct KeyAction
    {
        public readonly SeVirtualKey VirtualKey;
        public readonly char Character;
        public readonly bool IsUnicode;
        public readonly bool RequiresShift;

        public KeyAction(SeVirtualKey vk, bool shift = false)
        {
            VirtualKey = vk;
            Character = '\0';
            IsUnicode = false;
            RequiresShift = shift;
        }

        public KeyAction(char c)
        {
            VirtualKey = SeVirtualKey.NO_KEY;
            Character = c;
            IsUnicode = true;
            RequiresShift = false;
        }
    }

    private readonly Random _random = new();
    private readonly Queue<KeyAction> _pendingActions = new();
    private readonly Stopwatch _stepStopwatch = new();

    private long _nextDelayMs;
    private bool _isSubmittingWord;

    public bool IsTyping => _pendingActions.Count > 0 || _isSubmittingWord;
    public string CurrentWord { get; private set; } = string.Empty;
    public int RemainingChars => _pendingActions.Count;

    public bool JustSubmitted { get; set; }
    public string LastSubmittedWord { get; private set; } = string.Empty;

    public void EnqueueWord(string word, Configuration config)
    {
        _pendingActions.Clear();
        CurrentWord = word;
        _isSubmittingWord = false;
        JustSubmitted = false;

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

        foreach (var ch in textToType)
        {
            // For Japanese phrases or Japanese clients, all characters (including numbers, spaces, and punctuation)
            // MUST be sent via SendUnicodeChar (WM_CHAR). Sending physical keydowns for digits (e.g. KEY_3)
            // causes the Japanese IME to treat the digit as an IME candidate selection shortcut, wiping preceding Kana!
            if (isJapanese)
            {
                _pendingActions.Enqueue(new KeyAction(ch));
                continue;
            }

            if (TryEnqueueCircumflex(ch, _pendingActions))
            {
                continue;
            }

            if (TryCharToVirtualKey(ch, out var vk, out var shift))
            {
                _pendingActions.Enqueue(new KeyAction(vk, shift));
            }
            else
            {
                // Accented characters (ä, ö, ü, ß, é, à, etc.) or any non-ASCII character
                _pendingActions.Enqueue(new KeyAction(ch));
            }
        }

        // Always end with Enter to submit the word
        _pendingActions.Enqueue(new KeyAction(SeVirtualKey.RETURN));

        _nextDelayMs = GetRandomCharDelay(config);
        _stepStopwatch.Restart();
    }

    public void Clear()
    {
        _pendingActions.Clear();
        CurrentWord = string.Empty;
        _isSubmittingWord = false;
        _stepStopwatch.Reset();
    }

    public static bool ShouldBlockInput(Configuration? config = null)
    {
        var requireFocus = config?.RequireWindowFocus ?? Plugin.Configuration?.RequireWindowFocus ?? true;
        return requireFocus && !NativeMethods.IsGameFocused();
    }

    public void Update(Configuration config)
    {
        if (!IsTyping)
        {
            return;
        }

        if (ShouldBlockInput(config))
        {
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
        if (action.IsUnicode)
        {
            SendUnicodeChar(action.Character, config);
        }
        else if (action.RequiresShift)
        {
            SendKeyWithShift(action.VirtualKey, config);
        }
        else
        {
            SendKey(action.VirtualKey, config);
        }

        if (!action.IsUnicode && action.VirtualKey == SeVirtualKey.RETURN)
        {
            JustSubmitted = true;
            LastSubmittedWord = CurrentWord;
            CurrentWord = string.Empty;
            _isSubmittingWord = false;
            _pendingActions.Clear();
            _stepStopwatch.Restart();
            return;
        }

        if (_pendingActions.Count > 0)
        {
            var nextAction = _pendingActions.Peek();
            _nextDelayMs = (!nextAction.IsUnicode && nextAction.VirtualKey == SeVirtualKey.RETURN)
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
    public static bool TryClearInputBuffer(Configuration? config = null)
    {
        // 0. NEVER send keys if the game window is not focused!
        if (ShouldBlockInput(config))
        {
            return false;
        }

        // 1. NEVER send Escape if the input buffer is already empty!
        // Pressing Escape on an empty text input in FFXIV cancels target and opens the Exit Duty / System Menu!
        if (AddonKTGInput.IsInputEmpty())
        {
            return false;
        }

        // 2. If the text input is NOT active, NEVER send Escape!
        // Pressing Escape on an unfocused text window cancels target and opens the System Menu.
        // Send Enter first to activate the text window.
        if (!AddonKTGInput.IsInputActive())
        {
            SendKey(SeVirtualKey.RETURN, config);
            return false;
        }

        // 3. Strict throttle: at most once every 600ms to eliminate any chance of double-tap
        if ((DateTime.UtcNow - _lastEscapeTime).TotalMilliseconds < 600)
        {
            return false;
        }

        _lastEscapeTime = DateTime.UtcNow;
        SendKey(SeVirtualKey.ESCAPE, config);
        return true;
    }

    public static void SendEscape(Configuration? config = null)
    {
        TryClearInputBuffer(config);
    }

    private int GetRandomCharDelay(Configuration config)
    {
        var min = Math.Max(10, config.CharDelayMinMs);
        var max = Math.Max(min, config.CharDelayMaxMs);
        return _random.Next(min, max + 1);
    }

    public static void SendKey(SeVirtualKey key, Configuration? config = null)
    {
        if (ShouldBlockInput(config))
        {
            return;
        }

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
        var vk = (byte)key;
        NativeMethods.keybd_event(vk, 0, 0, 0);
        NativeMethods.keybd_event(vk, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
    }

    public static void SendKeyWithShift(SeVirtualKey key, Configuration? config = null)
    {
        if (ShouldBlockInput(config))
        {
            return;
        }

        var vk = (ushort)key;
        var physicalShiftDown = NativeMethods.IsShiftPressed();

        if (physicalShiftDown)
        {
            // Player is already physically holding Shift.
            // Only send the key press/release and DO NOT send Shift Up,
            // which would break the player's physical Shift hold!
            try
            {
                var inputs = stackalloc NativeMethods.INPUT[2];
                inputs[0] = new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    u = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = 0 } }
                };
                inputs[1] = new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    u = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
                };

                if (NativeMethods.SendInput(2, inputs, sizeof(NativeMethods.INPUT)) > 0)
                {
                    return;
                }
            }
            catch
            {
            }

            NativeMethods.keybd_event((byte)vk, 0, 0, 0);
            NativeMethods.keybd_event((byte)vk, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
            return;
        }

        try
        {
            var inputs = stackalloc NativeMethods.INPUT[4];
            // 1. Shift Down
            inputs[0] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_SHIFT, dwFlags = 0 } }
            };
            // 2. Key Down
            inputs[1] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = 0 } }
            };
            // 3. Key Up
            inputs[2] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            };
            // 4. Shift Up
            inputs[3] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_SHIFT, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            };

            if (NativeMethods.SendInput(4, inputs, sizeof(NativeMethods.INPUT)) > 0)
            {
                return;
            }
        }
        catch
        {
        }

        // Fallback using keybd_event
        NativeMethods.keybd_event((byte)NativeMethods.VK_SHIFT, 0, 0, 0);
        NativeMethods.keybd_event((byte)vk, 0, 0, 0);
        NativeMethods.keybd_event((byte)vk, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
        NativeMethods.keybd_event((byte)NativeMethods.VK_SHIFT, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
    }

    public static void SendUnicodeChar(char c, Configuration? config = null)
    {
        if (ShouldBlockInput(config))
        {
            return;
        }

        try
        {
            KeyboardDevice.ProcessKeyboardInputMessage(IntPtr.Zero, NativeMethods.WM_CHAR, (IntPtr)c, IntPtr.Zero);
            return;
        }
        catch
        {
        }

        var inputs = stackalloc NativeMethods.INPUT[2];
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
        inputs[1] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };

        NativeMethods.SendInput(2, inputs, sizeof(NativeMethods.INPUT));
    }

    private static bool TryEnqueueCircumflex(char ch, Queue<KeyAction> queue)
    {
        SeVirtualKey baseVowel;
        switch (ch)
        {
            case 'â':
            case 'Â':
                baseVowel = SeVirtualKey.A;
                break;
            case 'ê':
            case 'Ê':
                baseVowel = SeVirtualKey.E;
                break;
            case 'î':
            case 'Î':
                baseVowel = SeVirtualKey.I;
                break;
            case 'ô':
            case 'Ô':
                baseVowel = SeVirtualKey.O;
                break;
            case 'û':
            case 'Û':
                baseVowel = SeVirtualKey.U;
                break;
            default:
                return false;
        }

        var deadKey = GetCircumflexDeadKey();
        if (deadKey == SeVirtualKey.NO_KEY)
        {
            return false;
        }

        queue.Enqueue(new KeyAction(deadKey));
        queue.Enqueue(new KeyAction(baseVowel));
        return true;
    }

    private static SeVirtualKey GetCircumflexDeadKey()
    {
        var scan = NativeMethods.VkKeyScan('^');
        if (scan != -1)
        {
            var shiftState = (byte)((scan >> 8) & 0xFF);
            var vk = (byte)(scan & 0xFF);
            // Only use hardware dead key if '^' is unshifted on the active layout (e.g. French AZERTY 0xDD or German QWERTZ 0xDC/Wine 0xC0).
            // On US QWERTY, '^' requires Shift (Shift+6) and is NOT a dead key. Attempting to send it as a dead key
            // causes key 6 to be pressed unshifted, typing "6e" instead of "ê".
            if (shiftState == 0 && vk != 0)
            {
                return (SeVirtualKey)vk;
            }
        }

        return SeVirtualKey.NO_KEY;
    }

    private static bool TryCharToVirtualKey(char c, out SeVirtualKey key, out bool shift)
    {
        shift = false;

        // 1. German QWERTZ characters: ä/Ä, ö/Ö, ü/Ü, ß
        // Query active keyboard layout first via VkKeyScan (resolves dynamic Wine OEM virtual keys on Linux/Wine).
        // If unmapped on the active layout (e.g. US QWERTY), returns false so EnqueueWord cleanly falls back
        // to SendUnicodeChar (WM_CHAR) rather than sending wrong physical keys.
        switch (c)
        {
            case 'ä':
            case 'Ä':
            case 'ö':
            case 'Ö':
            case 'ü':
            case 'Ü':
            case 'ß':
                var germanScan = NativeMethods.VkKeyScan(c);
                if (germanScan == -1 && char.IsUpper(c))
                {
                    germanScan = NativeMethods.VkKeyScan(char.ToLowerInvariant(c));
                }

                if (germanScan != -1)
                {
                    var vk = (byte)(germanScan & 0xFF);
                    if (vk != 0)
                    {
                        key = (SeVirtualKey)vk;
                        shift = false; // Keybound Brawler accepts unshifted input for letters
                        return true;
                    }
                }

                key = SeVirtualKey.NO_KEY;
                return false;
        }

        // 2. French AZERTY characters: é/É, è/È, à/À, ç/Ç, ù/Ù
        // Query active layout first with VkKeyScan, falling back to SendUnicodeChar (WM_CHAR) if unmapped.
        switch (c)
        {
            case 'é':
            case 'É':
            case 'è':
            case 'È':
            case 'à':
            case 'À':
            case 'ç':
            case 'Ç':
            case 'ù':
            case 'Ù':
                var frenchScan = NativeMethods.VkKeyScan(c);
                if (frenchScan == -1 && char.IsUpper(c))
                {
                    frenchScan = NativeMethods.VkKeyScan(char.ToLowerInvariant(c));
                }

                if (frenchScan != -1)
                {
                    var vk = (byte)(frenchScan & 0xFF);
                    if (vk != 0)
                    {
                        key = (SeVirtualKey)vk;
                        shift = false;
                        return true;
                    }
                }

                key = SeVirtualKey.NO_KEY;
                return false;
        }

        // 3. Digits '0'..'9': MUST be mapped to top-row number keys (KEY_0..KEY_9, 0x30..0x39).
        // Numpad keys (0x60..0x69) MUST NOT be used because FFXIV treats them as movement/camera binds
        // and does not type them into the minigame input.
        if (c is >= '0' and <= '9')
        {
            var fallbackVk = (SeVirtualKey)((int)SeVirtualKey.KEY_0 + (c - '0'));

            // Check active Windows keyboard layout via VkKeyScan.
            // On French AZERTY, physical top-row keys 0..9 require Shift (shiftState == 1),
            // while on QWERTY, QWERTZ, Japanese, etc. shiftState is 0.
            var digitScan = NativeMethods.VkKeyScan(c);
            if (digitScan != -1)
            {
                var scanVk = (byte)(digitScan & 0xFF);
                var shiftState = (byte)((digitScan >> 8) & 0xFF);
                shift = (shiftState & 1) != 0;
                key = scanVk != 0 ? (SeVirtualKey)scanVk : fallbackVk;
            }
            else
            {
                key = fallbackVk;
            }
            return true;
        }

        // 4. Dynamic lookup via active Windows keyboard layout
        var scan = NativeMethods.VkKeyScan(c);
        if (scan == -1 && char.IsUpper(c))
        {
            scan = NativeMethods.VkKeyScan(char.ToLowerInvariant(c));
        }

        if (scan != -1)
        {
            var vk = (byte)(scan & 0xFF);
            var shiftState = (byte)((scan >> 8) & 0xFF);
            // Only use VkKeyScan if the key does not require modifiers like Shift (shiftState == 0),
            // OR if it is a letter where the game accepts unshifted input.
            if (vk != 0 && (shiftState == 0 || char.IsLetter(c)))
            {
                key = (SeVirtualKey)vk;
                return true;
            }
            else if (vk != 0 && (shiftState & 1) != 0)
            {
                key = (SeVirtualKey)vk;
                shift = true;
                return true;
            }
        }

        // 5. Standard ASCII alphanumeric fallbacks
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
}
