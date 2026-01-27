/*
 * =========================================================================================
 * File: CursorService.cs
 * Namespace: dumbTV.Services
 * Author: Radim Kopunec
 * Description: Handles the customization of the system cursor (changing the .cur file)
 * and restoring it to the Windows default.
 * =========================================================================================
 */

using System;
using dumbTV.Core;
using static dumbTV.Core.NativeMethods; // Allows using OCR_ constants directly

namespace dumbTV.Services
{
    /// <summary>
    /// Provides functionality to change the system-wide cursor appearance and restore defaults.
    /// </summary>
    public class CursorService
    {
        /// <summary>
        /// Replaces all standard system cursors (Arrow, Wait, IBeam, etc.) with a custom cursor file.
        /// </summary>
        /// <param name="cursorFilePath">The absolute path to the .cur file.</param>
        public void ApplyCustomCursor(string cursorFilePath)
        {
            // List of standard system cursors to replace
            uint[] cursorIds = new uint[]
            {
                OCR_NORMAL, OCR_IBEAM, OCR_WAIT, OCR_CROSS, OCR_UP,
                OCR_SIZE, OCR_ICON, OCR_SIZENWSE, OCR_SIZENESW,
                OCR_SIZEWE, OCR_SIZENS, OCR_SIZEALL, OCR_NO,
                OCR_HAND, OCR_APPSTARTING
            };

            foreach (var id in cursorIds)
            {
                // Load the custom cursor from file
                IntPtr hCursor = LoadCursorFromFile(cursorFilePath);

                if (hCursor != IntPtr.Zero)
                {
                    // Replace the system cursor
                    SetSystemCursor(hCursor, id);
                }
            }
        }

        /// <summary>
        /// Restores all system cursors to their default Windows appearance.
        /// </summary>
        public void RestoreDefaultCursors()
        {
            // SPI_SETCURSORS (0x57) resets system cursors when the last two parameters are 0
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }
    }
}