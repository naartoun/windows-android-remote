/*
 * =========================================================================================
 * File: InputService.cs
 * Namespace: dumbTV.Services
 * Author: Radim Kopunec
 * Description: Provides high-level methods for simulating user input (Keyboard & Mouse).
 * It abstracts the low-level P/Invoke calls defined in NativeMethods.
 * =========================================================================================
 */

using System;
using dumbTV.Core;
using static dumbTV.Core.NativeMethods;

namespace dumbTV.Services
{
    public class InputService
    {
        // Tracks if the last input was mouse (1) or keyboard/remote (2). 
        // 0 = Initial state.
        public int LastInputType { get; private set; } = 0;

        /// <summary>
        /// Moves the mouse cursor relative to its current position.
        /// </summary>
        public void MoveCursor(int dx, int dy)
        {
            if (GetCursorPos(out var point))
            {
                SetCursorPos(point.X + dx, point.Y + dy);
                LastInputType = 1; // Mark as Mouse Mode
            }
        }

        /// <summary>
        /// Simulates a standard left mouse click at the current cursor position.
        /// </summary>
        public void MouseLeftClick()
        {
            if (GetCursorPos(out var point))
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, point.X, point.Y, 0, UIntPtr.Zero);
            }
        }

        /// <summary>
        /// Simulates a standard right mouse click at the current cursor position.
        /// </summary>
        public void MouseRightClick()
        {
            if (GetCursorPos(out var point))
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, point.X, point.Y, 0, UIntPtr.Zero);
            }
        }

        /// <summary>
        /// Simulates a keyboard key press and release (Virtual Key).
        /// </summary>
        /// <param name="vkCode">Virtual Key code (e.g., VK_RETURN).</param>
        public void SendKey(int vkCode)
        {
            keybd_event((byte)vkCode, 0, 0, (int)UIntPtr.Zero);
            keybd_event((byte)vkCode, 0, (int)KEYEVENTF_KEYUP, (int)UIntPtr.Zero);

            if (vkCode == VK_UP || vkCode == VK_DOWN || vkCode == VK_LEFT || vkCode == VK_RIGHT)
            {
                LastInputType = 2;
            }
        }

        /// <summary>
        /// Sends a specific character by translating it to Virtual Keys and Shift states.
        /// </summary>
        public void SendChar(char c)
        {
            short vk = VkKeyScan(c);
            if (vk == -1) return; // Character not found in current layout

            byte vkCode = (byte)(vk & 0xFF);
            byte shiftState = (byte)((vk >> 8) & 0xFF);

            // Press Modifiers (Shift, Ctrl, Alt)
            if ((shiftState & 1) != 0) keybd_event(0x10, 0, 0, 0); // Shift
            if ((shiftState & 2) != 0) keybd_event(0x11, 0, 0, 0); // Ctrl
            if ((shiftState & 4) != 0) keybd_event(0x12, 0, 0, 0); // Alt

            // Press Key
            keybd_event(vkCode, 0, 0, 0);
            keybd_event(vkCode, 0, (int)KEYEVENTF_KEYUP, 0);

            // Release Modifiers
            if ((shiftState & 4) != 0) keybd_event(0x12, 0, (int)KEYEVENTF_KEYUP, 0);
            if ((shiftState & 2) != 0) keybd_event(0x11, 0, (int)KEYEVENTF_KEYUP, 0);
            if ((shiftState & 1) != 0) keybd_event(0x10, 0, (int)KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// Sends a Space key specifically to a window handle (used for pausing players).
        /// </summary>
        public void SendSpaceToWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_SPACE, IntPtr.Zero);
            PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_SPACE, IntPtr.Zero);
        }

        /// <summary>
        /// Simulates the 'Backspace' action (Press and Release).
        /// </summary>
        public void SendBackspace()
        {
            SendKey(VK_BACK);
        }
    }
}