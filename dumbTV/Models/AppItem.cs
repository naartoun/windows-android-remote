/*
 * =========================================================================================
 * File: AppItem.cs
 * Namespace: dumbTV.Models
 * Author: Radim Kopunec
 * Description: Represents the configuration for a launchable external application.
 * Defines how to identify the application window and launch parameters.
 * =========================================================================================
 */

using System.Collections.Generic;

namespace dumbTV.Models
{
    /// <summary>
    /// Data model representing an external application that can be launched by dumbTV.
    /// </summary>
    public class AppItem
    {
        /// <summary>
        /// Human-readable name of the application (e.g., "YouTube").
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Absolute path to the executable file or the command (e.g., "brave" or "C:\...\vlc.exe").
        /// </summary>
        public required string ExecutablePath { get; set; }

        /// <summary>
        /// Command-line arguments passed to the executable (e.g., "--app=https://youtube.com").
        /// </summary>
        public string Arguments { get; set; } = "";

        /// <summary>
        /// A substring that MUST appear in the window title to identify the running application.
        /// Used to check if the app is already running (Single Instance).
        /// </summary>
        public required string WindowTitleKeyword { get; set; }

        /// <summary>
        /// A list of substrings that MUST NOT appear in the window title.
        /// Useful for the "Internet" button to distinguish a generic browser window 
        /// from specific web apps like "YouTube" or "Bombuj" running in the same browser.
        /// </summary>
        public List<string> ExcludedTitles { get; set; } = new List<string>();

        /// <summary>
        /// If true, the F11 key will be sent to the window after launching to trigger fullscreen mode.
        /// </summary>
        public bool SendF11 { get; set; } = true;
    }
}