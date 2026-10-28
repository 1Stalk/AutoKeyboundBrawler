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

        public KeyAction(SeVirtualKey vk)
        {
            VirtualKey = vk;
            Character = '\0';
            IsUnicode = false;
        }

        public KeyAction(char c)
        {
            VirtualKey = SeVirtualKey.NO_KEY;
            Character = c;
            IsUnicode = true;
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
            if (TryCharToVirtualKey(ch, out var vk))
            {
                _pendingActions.Enqueue(new KeyAction(vk));
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

    private static bool TryCharToVirtualKey(char c, out SeVirtualKey key)
    {
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
            case 'ê':
            case 'ë':
                key = SeVirtualKey.E;
                return true;
            case 'â':
                key = SeVirtualKey.A;
                return true;
            case 'î':
            case 'ï':
                key = SeVirtualKey.I;
                return true;
            case 'ô':
                key = SeVirtualKey.O;
                return true;
            case 'û':
                key = SeVirtualKey.U;
                return true;
        }

        // 3. Dynamic lookup via active Windows keyboard layout
        var scan = PInvoke.VkKeyScan(c);
        if (scan == -1 && char.IsUpper(c))
        {
            scan = PInvoke.VkKeyScan(char.ToLowerInvariant(c));
        }

        if (scan != -1)
        {
            var vk = (byte)(scan & 0xFF);
            if (vk != 0)
            {
                key = (SeVirtualKey)vk;
                return true;
            }
        }

        // 4. Standard ASCII alphanumeric fallbacks
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
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    }
}
