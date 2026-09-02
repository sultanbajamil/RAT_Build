using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace StealthRAT.Utilities
{
    /// <summary>
    /// Encapsulates Windows native Win32 APIs (user32.dll) for simulating cursor moves, 
    /// clicks, and keystrokes.
    /// </summary>
    public static class NativeInputHelper
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(
            uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(
            byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        #endregion

        #region Mouse Event Constants

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        #endregion

        #region Keyboard Event Constants

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        #endregion

        #region Virtual Key Code Mappings

        private static readonly Dictionary<string, byte> SpecialKeyMap = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["ENTER"] = 0x0D,
            ["SPACE"] = 0x20,
            ["TAB"] = 0x09,
            ["BACKSPACE"] = 0x08,
            ["ESC"] = 0x1B,
            ["DELETE"] = 0x2E,
            ["HOME"] = 0x24,
            ["END"] = 0x23,
            ["UP"] = 0x26,
            ["DOWN"] = 0x28,
            ["LEFT"] = 0x25,
            ["RIGHT"] = 0x27,
            ["F1"] = 0x70,
            ["F2"] = 0x71,
            ["F3"] = 0x72,
            ["F4"] = 0x73,
            ["F5"] = 0x74,
            ["F6"] = 0x75,
            ["F7"] = 0x76,
            ["F8"] = 0x77,
            ["F9"] = 0x78,
            ["F10"] = 0x79,
            ["F11"] = 0x7A,
            ["F12"] = 0x7B
        };

        #endregion

        public static bool MoveCursor(int x, int y)
        {
            return SetCursorPos(x, y);
        }

        public static void SimulateClick(bool isRightClick, int clickDelayMs = 50)
        {
            uint downFlag = isRightClick ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
            uint upFlag = isRightClick ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;

            mouse_event(downFlag, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(clickDelayMs);
            mouse_event(upFlag, 0, 0, 0, UIntPtr.Zero);
        }

        public static bool SimulateKeyPress(string keyName, int pressDelayMs = 30)
        {
            byte virtualKeyCode = ResolveVirtualKeyCode(keyName);
            if (virtualKeyCode == 0) return false;

            keybd_event(virtualKeyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(pressDelayMs);
            keybd_event(virtualKeyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            return true;
        }

        public static byte ResolveVirtualKeyCode(string keyName)
        {
            if (SpecialKeyMap.TryGetValue(keyName, out byte specialKey))
            {
                return specialKey;
            }

            string upper = keyName.ToUpperInvariant();
            if (upper.Length == 1 && char.IsLetterOrDigit(upper[0]))
            {
                return (byte)upper[0];
            }

            return 0;
        }
    }
}
