/*
 * =========================================================================================
 * File: NativeMethods.cs
 * Namespace: dumbTV.Core
 * Author: Radim Kopunec
 * Description: Contains all P/Invoke signatures (DllImports), constants, and structs 
 * required for interacting with the Windows API (User32.dll).
 * This isolates unmanaged code from the main application logic.
 * =========================================================================================
 */

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace dumbTV.Core
{
    /// <summary>
    /// Exposes Windows API (User32.dll) functions, constants, and structures.
    /// Used for low-level window management, input simulation, and cursor control.
    /// </summary>
    public static class NativeMethods
    {
        #region Constants - Window Management

        public const int SW_MAXIMIZE = 3;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP   = 0x0101;

        #endregion


        #region Constants - Virtual Keys (VK)

        public const int VK_F11          = 0x7A;
        public const int VK_BROWSER_BACK = 0xA6;
        public const int VK_HOME         = 0x24;
        public const int VK_VOLUME_MUTE  = 0xAD;
        public const int VK_VOLUME_DOWN  = 0xAE;
        public const int VK_VOLUME_UP    = 0xAF;
        public const int VK_BACK         = 0x08;
        public const int VK_SPACE        = 0x20;
        public const int VK_RETURN       = 0x0D;
        public const int VK_LEFT         = 0x25;
        public const int VK_UP           = 0x26;
        public const int VK_RIGHT        = 0x27;
        public const int VK_DOWN         = 0x28;

        // Key Event Flags
        public const uint KEYEVENTF_KEYUP   = 0x0002;
        public const uint KEYEVENTF_UNICODE = 0x0004;

        #endregion


        #region Constants - Mouse & Cursor

        // Mouse Event Flags
        public const uint MOUSEEVENTF_LEFTDOWN  = 0x02;
        public const uint MOUSEEVENTF_LEFTUP    = 0x04;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const uint MOUSEEVENTF_RIGHTUP   = 0x10;

        // System Parameters
        public const uint SPI_SETCURSORS = 0x57;

        // OEM Cursor Resource IDs (OCR)
        public const uint OCR_NORMAL      = 32512;
        public const uint OCR_IBEAM       = 32513;
        public const uint OCR_WAIT        = 32514;
        public const uint OCR_CROSS       = 32515;
        public const uint OCR_UP          = 32516;
        public const uint OCR_SIZE        = 32640;
        public const uint OCR_ICON        = 32641;
        public const uint OCR_SIZENWSE    = 32642;
        public const uint OCR_SIZENESW    = 32643;
        public const uint OCR_SIZEWE      = 32644;
        public const uint OCR_SIZENS      = 32645;
        public const uint OCR_SIZEALL     = 32646;
        public const uint OCR_NO          = 32648;
        public const uint OCR_HAND        = 32649;
        public const uint OCR_APPSTARTING = 32650;

        #endregion


        #region Structs

        /// <summary>
        /// Defines the coordinates of the upper-left and lower-right corners of a rectangle.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Defines the x- and y-coordinates of a point.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion


        #region Methods - Window & Process Management

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        #endregion


        #region Methods - Input Simulation (Keyboard/Mouse)

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern short VkKeyScan(char ch);

        #endregion


        #region Methods - System Cursor Customization

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr LoadCursorFromFile(string lpFileName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        #endregion
    }
}