using System;
using System.Runtime.InteropServices;

namespace AutoKeyboundBrawler.Interop;

internal static unsafe class NativeMethods
{
    // Window message constants
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_CHAR = 0x0102;

    // Virtual key codes
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_LSHIFT = 0xA0;
    public const ushort VK_RSHIFT = 0xA1;

    // SendInput / keybd_event flags
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;

    // Note: MOUSEINPUT is retained strictly for the 64-bit ABI union layout sizing (40 bytes total for INPUT)
    // required by Win32 SendInput. Do not remove even though mouse simulation has been removed.
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "VkKeyScanW")]
    public static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT* pInputs, int cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    private static bool? _gameFocusedOverride;
    private static bool? _shiftPressedOverride;

    internal static void SetGameFocusedOverrideForTesting(bool? isFocused)
    {
        _gameFocusedOverride = isFocused;
    }

    internal static void SetShiftPressedOverrideForTesting(bool? isPressed)
    {
        _shiftPressedOverride = isPressed;
    }

    /// <summary>
    /// Checks whether the FFXIV game window currently has foreground input focus.
    /// Returns false when the player is switched into Discord, web browser, or another external window.
    /// </summary>
    public static bool IsGameFocused()
    {
        if (_gameFocusedOverride.HasValue)
        {
            return _gameFocusedOverride.Value;
        }

        try
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                return false;
            }

            GetWindowThreadProcessId(foreground, out var pid);
            return pid == (uint)Environment.ProcessId;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the Shift key is currently held down physically by the user.
    /// Prevents synthetic Shift KeyUp events from resetting the user's physical Shift hold.
    /// </summary>
    public static bool IsShiftPressed()
    {
        if (_shiftPressedOverride.HasValue)
        {
            return _shiftPressedOverride.Value;
        }

        try
        {
            return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0 ||
                   (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0 ||
                   (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;
        }
        catch
        {
            return false;
        }
    }
}
