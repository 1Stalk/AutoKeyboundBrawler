using FFXIVClientStructs.FFXIV.Client.System.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

        var textToType = (config.IgnorePunctuation
            ? Data.KTGWordDatabase.StripPunctuation(word)
            : word).Trim();

        foreach (var ch in textToType)
        {
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

        if (_pendingActions.Count == 0)
        {
            CurrentWord = string.Empty;
            _isSubmittingWord = false;
            return;
        }

        var action = _pendingActions.Dequeue();
        if (action.IsUnicode)
        {
            SendUnicodeChar(action.Character);
        }
        else if (action.RequiresShift)
        {
            SendKeyWithShift(action.VirtualKey);
        }
        else
        {
            SendKey(action.VirtualKey);
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

    public static void SendKeyWithShift(SeVirtualKey key)
    {
        const ushort VK_SHIFT = 0x10;
        var vk = (ushort)key;

        try
        {
            var inputs = new INPUT[4];
            // 1. Shift Down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_SHIFT, dwFlags = 0 } }
            };
            // 2. Key Down
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } }
            };
            // 3. Key Up
            inputs[2] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
            };
            // 4. Shift Up
            inputs[3] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion { ki = new KEYBDINPUT { wVk = VK_SHIFT, dwFlags = KEYEVENTF_KEYUP } }
            };

            if (PInvoke.SendInput((uint)inputs.Length, inputs, sizeof(INPUT)) > 0)
            {
                return;
            }
        }
        catch
        {
        }

        // Fallback using keybd_event
        PInvoke.keybd_event((byte)VK_SHIFT, 0, 0, 0);
        PInvoke.keybd_event((byte)vk, 0, 0, 0);
        PInvoke.keybd_event((byte)vk, 0, KEYEVENTF_KEYUP, 0);
        PInvoke.keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    public static void SendUnicodeChar(char c)
    {
        try
        {
            const uint WM_CHAR = 0x0102;
            KeyboardDevice.ProcessKeyboardInputMessage(IntPtr.Zero, WM_CHAR, (IntPtr)c, IntPtr.Zero);
            return;
        }
        catch
        {
        }

        var inputs = new INPUT[2];
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };

        PInvoke.SendInput((uint)inputs.Length, inputs, sizeof(INPUT));
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
        queue.Enqueue(new KeyAction(deadKey));
        queue.Enqueue(new KeyAction(baseVowel));
        return true;
    }

    private static SeVirtualKey GetCircumflexDeadKey()
    {
        var scan = PInvoke.VkKeyScan('^');
        if (scan != -1)
        {
            var vk = (byte)(scan & 0xFF);
            if (vk != 0)
            {
                return (SeVirtualKey)vk;
            }
        }

        // On French AZERTY, VK_OEM_6 (0xDD) is the circumflex dead key
        return (SeVirtualKey)0xDD;
    }

    private static bool TryCharToVirtualKey(char c, out SeVirtualKey key, out bool shift)
    {
        shift = false;

        // 1. Direct German QWERTZ OEM virtual key mapping
        // On German layout: ä/Ä = VK_OEM_7 (0xDE), ö/Ö = VK_OEM_3 (0xC0), ü/Ü = VK_OEM_1 (0xBA), ß = VK_OEM_4 (0xDB)
        switch (c)
        {
            case 'ä':
            case 'Ä':
                key = (SeVirtualKey)0xDE;
                return true;
            case 'ö':
            case 'Ö':
                key = (SeVirtualKey)0xC0;
                return true;
            case 'ü':
            case 'Ü':
                key = (SeVirtualKey)0xBA;
                return true;
            case 'ß':
                key = (SeVirtualKey)0xDB;
                return true;
        }

        // 2. Direct French AZERTY OEM virtual key mapping
        switch (c)
        {
            case 'é':
            case 'É':
                key = (SeVirtualKey)0x32; // VK_2
                return true;
            case 'è':
            case 'È':
                key = (SeVirtualKey)0x37; // VK_7
                return true;
            case 'à':
            case 'À':
                key = (SeVirtualKey)0x30; // VK_0
                return true;
            case 'ç':
            case 'Ç':
                key = (SeVirtualKey)0x39; // VK_9
                return true;
            case 'ù':
            case 'Ù':
                key = (SeVirtualKey)0xC0; // VK_OEM_3
                return true;
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
            var digitScan = PInvoke.VkKeyScan(c);
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
        var scan = PInvoke.VkKeyScan(c);
        if (scan == -1 && char.IsUpper(c))
        {
            scan = PInvoke.VkKeyScan(char.ToLowerInvariant(c));
        }

        if (scan != -1)
        {
            var vk = (byte)(scan & 0xFF);
            var shiftState = (byte)((scan >> 8) & 0xFF);
            // Only use VkKeyScan if the key does not require modifiers like Shift (shiftState == 0),
            // OR if it is a standard letter (A-Z) where the game accepts unshifted input.
            if (vk != 0 && (shiftState == 0 || (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z')))
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

    private static class PInvoke
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    }
}
