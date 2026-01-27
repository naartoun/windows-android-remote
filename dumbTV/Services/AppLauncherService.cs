/*
 * =========================================================================================
 * File: AppLauncherService.cs
 * Namespace: dumbTV.Services
 * Author: Radim Kopunec
 * Description: Service responsible for managing external application processes.
 * Handles launching, detecting existing windows (Single Instance), and maximizing windows.
 * =========================================================================================
 */

using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using dumbTV.Models;
using dumbTV.Core;
using static dumbTV.Core.NativeMethods;

namespace dumbTV.Services
{
    /// <summary>
    /// Provides methods to launch external applications and manage their window state.
    /// Ensures only one instance of a specific app configuration runs at a time.
    /// </summary>
    public class AppLauncherService
    {
        /// <summary>
        /// Launches the specified application or activates its window if it is already running.
        /// </summary>
        /// <param name="app">The application configuration object.</param>
        public async Task LaunchAppAsync(AppItem app)
        {
            // 1. Check if the application is already running (Single Instance Logic)
            IntPtr existingHwnd = FindWindowByConfig(app);

            if (existingHwnd != IntPtr.Zero)
            {
                // App is already running -> Maximize it and bring it to the foreground
                ShowWindow(existingHwnd, SW_MAXIMIZE);
                SetForegroundWindow(existingHwnd);
            }
            else
            {
                // 2. App is not running -> Start a new process
                var psi = new ProcessStartInfo
                {
                    FileName = app.ExecutablePath,
                    Arguments = app.Arguments,
                    UseShellExecute = true
                };

                try
                {
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to start application {app.Name}: {ex.Message}");
                }

                // 3. Wait for the window to appear and configure it (Maximize/F11)
                await WaitForWindowAndSetupAsync(app);
            }
        }

        /// <summary>
        /// Polls for the application window to appear, then maximizes it and optionally sends F11.
        /// </summary>
        /// <param name="app">The application configuration.</param>
        private async Task WaitForWindowAndSetupAsync(AppItem app)
        {
            IntPtr hwnd = IntPtr.Zero;
            int attempts = 0;
            const int maxAttempts = 50; // Timeout: 50 * 100ms = 5 seconds

            // Loop until the window is found or timeout is reached
            while (hwnd == IntPtr.Zero && attempts < maxAttempts)
            {
                await Task.Delay(100);
                hwnd = FindWindowByConfig(app); // Re-check for window
                attempts++;
            }

            if (hwnd != IntPtr.Zero)
            {
                // Wait a brief moment for the window to become responsive
                await Task.Delay(500);

                // Maximize and focus
                ShowWindow(hwnd, SW_MAXIMIZE);
                SetForegroundWindow(hwnd);

                // Trigger Fullscreen if requested
                if (app.SendF11)
                {
                    PostMessage(hwnd, WM_KEYDOWN, (IntPtr)VK_F11, IntPtr.Zero);
                    PostMessage(hwnd, WM_KEYUP, (IntPtr)VK_F11, IntPtr.Zero);
                }
            }
        }

        /// <summary>
        /// Searches for a visible window that matches the AppItem's title keywords 
        /// and does NOT contain any excluded titles.
        /// </summary>
        /// <param name="app">The application configuration to match against.</param>
        /// <returns>Handle (IntPtr) to the window if found, otherwise IntPtr.Zero.</returns>
        private IntPtr FindWindowByConfig(AppItem app)
        {
            if (string.IsNullOrEmpty(app.WindowTitleKeyword)) return IntPtr.Zero;

            IntPtr foundHwnd = IntPtr.Zero;

            // Enumerate all top-level windows
            EnumWindows((hWnd, lParam) =>
            {
                // Optimization: Ignore invisible windows
                if (!IsWindowVisible(hWnd)) return true;

                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                // 1. Does the title contain the required keyword? (e.g., "YouTube")
                bool match = title.IndexOf(app.WindowTitleKeyword, StringComparison.OrdinalIgnoreCase) >= 0;

                // 2. Does the title contain any excluded words? (e.g., "YouTube" when looking for generic "Brave")
                if (match && app.ExcludedTitles != null && app.ExcludedTitles.Count > 0)
                {
                    foreach (var excluded in app.ExcludedTitles)
                    {
                        if (title.IndexOf(excluded, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            match = false; // It's a match, but an excluded one
                            break;
                        }
                    }
                }

                if (match)
                {
                    foundHwnd = hWnd;
                    return false; // Found it, stop enumeration
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return foundHwnd;
        }
    }
}