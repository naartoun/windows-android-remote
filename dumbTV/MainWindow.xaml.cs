/*
 * =========================================================================================
 * File: MainWindow.xaml.cs
 * Namespace: dumbTV
 * Author: Radim Kopunec
 * Description: The main entry point and UI logic for the dumbTV application.
 * Handles WebSocket commands, manages the UI state, coordinates services,
 * and handles audio/process detection for the "Smart TV" simulation.
 * =========================================================================================
 */

using dumbTV.Models;
using dumbTV.Services;
using Fleck;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static dumbTV.Core.NativeMethods; // Access to VK constants and low-level methods

namespace dumbTV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// Acts as the central controller for the application.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields & Services

        private WebSocketServer server;

        /// <summary>
        /// Tracks the currently active "application" or screen (e.g., "Home", "YouTube", "VLC").
        /// </summary>
        private string currentPage = "Home";

        private readonly CursorService _cursorService;
        private readonly InputService _inputService;
        private readonly AppLauncherService _appLauncher;

        #endregion

        #region App Configurations

        private readonly AppItem _appYouTube = new AppItem
        {
            Name = "YouTube",
            ExecutablePath = "brave",
            Arguments = "--app=https://www.youtube.com",
            WindowTitleKeyword = "YouTube",
            SendF11 = true
        };

        private readonly AppItem _appVLC = new AppItem
        {
            Name = "VLC",
            ExecutablePath = @"C:\Program Files\VideoLAN\VLC\vlc.exe",
            Arguments = "",
            WindowTitleKeyword = "VLC",
            SendF11 = true
        };

        private readonly AppItem _appBombuj = new AppItem
        {
            Name = "Bombuj",
            ExecutablePath = "brave",
            Arguments = "--app=https://www.bombuj.si",
            WindowTitleKeyword = "bombuj",
            SendF11 = true
        };

        private readonly AppItem _appIVysilani = new AppItem
        {
            Name = "iVysilani",
            ExecutablePath = "brave",
            Arguments = "--app=https://www.ivysilani.cz",
            WindowTitleKeyword = "iVysílání",
            SendF11 = true
        };

        private readonly AppItem _appInternet = new AppItem
        {
            Name = "Brave",
            ExecutablePath = "brave",
            Arguments = "",
            WindowTitleKeyword = "Brave",
            ExcludedTitles = new List<string> { "YouTube", "bombuj", "iVysílání" },
            SendF11 = false
        };

        #endregion

        #region Initialization

        public MainWindow()
        {
            InitializeComponent();

            // Initialize dependency services
            _cursorService = new CursorService();
            _inputService = new InputService();
            _appLauncher = new AppLauncherService();

            StartWebSocketServer();

            // Apply custom cursor on startup
            // TODO: Move path to configuration file
            _cursorService.ApplyCustomCursor("C:\\Users\\Radim Kopunec\\Desktop\\dumbTV\\dumbTV\\cursor.cur");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial focus and start background animation
            BombujButton.Focus();
            AnimateGradientMovement();
        }

        #endregion

        #region WebSocket Server

        /// <summary>
        /// Starts the WebSocket server to listen for mobile controller commands.
        /// </summary>
        private void StartWebSocketServer()
        {
            FleckLog.Level = LogLevel.Info;
            server = new WebSocketServer("ws://0.0.0.0:8080/ws/");
            server.Start(socket =>
            {
                socket.OnOpen = () => Console.WriteLine("Client connected");
                socket.OnClose = () => Console.WriteLine("Client disconnected");
                socket.OnMessage = message => HandleWebSocketCommand(message);
            });
        }

        /// <summary>
        /// Processes incoming commands from the WebSocket client.
        /// Translates commands into UI actions or InputService calls.
        /// </summary>
        /// <param name="cmd">The command string received from the client.</param>
        private void HandleWebSocketCommand(string cmd)
        {
            // UI updates must run on the main thread
            Dispatcher.Invoke(() =>
            {
                if (cmd == "CLICK:MOUSELEFT")
                {
                    // Context-aware click:
                    // If using remote navigation (arrows) or on Home screen, behave like ENTER.
                    // Otherwise, behave like a standard mouse click.
                    if (_inputService.LastInputType == 2 || currentPage == "Home")
                    {
                        _inputService.SendKey(VK_RETURN);
                    }
                    else
                    {
                        _inputService.MouseLeftClick();
                    }
                }
                else if (cmd == "CLICK:MOUSERIGHT")
                    _inputService.MouseRightClick();
                else if (cmd == "CLICK:UP")
                    _inputService.SendKey(VK_UP);
                else if (cmd == "CLICK:DOWN")
                    _inputService.SendKey(VK_DOWN);
                else if (cmd == "CLICK:LEFT")
                    _inputService.SendKey(VK_LEFT);
                else if (cmd == "CLICK:RIGHT")
                    _inputService.SendKey(VK_RIGHT);
                else if (cmd == "CLICK:BACK")
                    _inputService.SendKey(VK_BROWSER_BACK);
                else if (cmd == "CLICK:HOME")
                    GoHome();
                else if (cmd == "CLICK:MUTE")
                    _inputService.SendKey(VK_VOLUME_MUTE);
                else if (cmd == "CLICK:VOLDOWN")
                    _inputService.SendKey(VK_VOLUME_DOWN);
                else if (cmd == "CLICK:VOLUP")
                    _inputService.SendKey(VK_VOLUME_UP);
                else if (cmd == "BACKSPACE")
                    _inputService.SendBackspace();
                else if (cmd.StartsWith("TYPE:") && cmd.Length > 5)
                    _inputService.SendChar(cmd[5]);
                else if (cmd.StartsWith("MOVE:"))
                {
                    // Parse coordinates for cursor movement
                    var parts = cmd.Substring(5).Split(':');
                    if (parts.Length == 2
                        && int.TryParse(parts[0], out var dx)
                        && int.TryParse(parts[1], out var dy))
                    {
                        _inputService.MoveCursor(dx, dy);
                    }
                }
            });
        }

        #endregion

        #region UI & Animation Logic

        /// <summary>
        /// Animates the background gradient to create a dynamic visual effect.
        /// </summary>
        private void AnimateGradientMovement()
        {
            PointAnimation startAnim = new PointAnimation()
            {
                From = new Point(0, 0),
                To = new Point(0.1, 0.1),
                Duration = TimeSpan.FromSeconds(5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            PointAnimation endAnim = new PointAnimation()
            {
                From = new Point(1, 1),
                To = new Point(0.9, 0.9),
                Duration = TimeSpan.FromSeconds(5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            bgGradient.BeginAnimation(LinearGradientBrush.StartPointProperty, startAnim);
            bgGradient.BeginAnimation(LinearGradientBrush.EndPointProperty, endAnim);
        }

        /// <summary>
        /// Prevents mouse movement from interacting with the UI when on the Home screen,
        /// keeping the "dumbTV" feel by forcing cursor reset.
        /// </summary>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            if (currentPage == "Home")
            {
                // Lock cursor to 0,0 on Home screen to hide it effectively
                SetCursorPos(0, 0);
                e.Handled = true;
                return;
            }
            base.OnPreviewMouseMove(e);
        }

        #endregion

        #region App Management & Audio Detection

        /// <summary>
        /// Returns the application to the Home screen and pauses any active media.
        /// </summary>
        private void GoHome()
        {
            // Attempt to pause media players before leaving
            PauseBrave();
            PauseVLC();

            // Reactivate the main window
            this.Activate();
            currentPage = "Home";

            SetCursorPos(0, 0);
        }

        /// <summary>
        /// Checks if a specific process is currently outputting audio.
        /// Uses NAudio to inspect Core Audio API sessions.
        /// </summary>
        /// <param name="processName">The name of the process to check (e.g., "vlc", "brave").</param>
        /// <returns>True if the process has an active audio session, otherwise false.</returns>
        public bool IsProcessPlayingAudio(string processName)
        {
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    var sessions = device.AudioSessionManager.Sessions;

                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var session = sessions[i];

                        // Only consider sessions that are actively playing
                        if (session.State == AudioSessionState.AudioSessionStateActive)
                        {
                            try
                            {
                                int pid = (int)session.GetProcessID;
                                using (var p = Process.GetProcessById(pid))
                                {
                                    if (p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                                        return true;
                                }
                            }
                            catch
                            {
                                // Process might have exited during enumeration, ignore
                            }
                        }
                    }
                }
            }
            catch
            {
                // Handle device enumeration errors gracefully
                return false;
            }

            return false;
        }

        /// <summary>
        /// Tries to pause VLC if it is currently playing audio.
        /// </summary>
        private void PauseVLC()
        {
            // Optimization: First check if VLC is actually making noise.
            // This prevents un-pausing a video that is already paused.
            if (!IsProcessPlayingAudio("vlc")) return;

            foreach (var process in Process.GetProcessesByName("vlc"))
            {
                try
                {
                    if (!process.HasExited && process.MainWindowTitle.Contains("VLC"))
                    {
                        _inputService.SendSpaceToWindow(process.MainWindowHandle);
                    }
                }
                catch { /* Ignore process access errors */ }
            }
        }

        /// <summary>
        /// Tries to pause Brave browser instances if they are playing audio.
        /// Note: This is a "best effort" approach using the Space key.
        /// </summary>
        private void PauseBrave()
        {
            // Only attempt to pause if audio is detected from Brave
            if (!IsProcessPlayingAudio("brave")) return;

            foreach (var process in Process.GetProcessesByName("brave"))
            {
                try
                {
                    // We only target the window that matches the current page title logic
                    // or simply the active one if we could identify it.
                    if (!process.HasExited && !string.IsNullOrEmpty(process.MainWindowTitle))
                    {
                        // Sending Space to browser is risky (scrolls page), 
                        // but acceptable for full-screen video players in this prototype.
                        _inputService.SendSpaceToWindow(process.MainWindowHandle);
                    }
                }
                catch { }
            }
        }

        #endregion

        #region Event Handlers - App Launching

        // NOTE: All handlers are 'async void' to support await within WPF event handlers.

        private async void YouTubeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appYouTube);
                currentPage = "YouTube";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching YouTube: {ex.Message}"); }
        }

        private async void VLCButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appVLC);
                currentPage = "VLC";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching VLC: {ex.Message}"); }
        }

        private async void BombujButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appBombuj);
                currentPage = "bombuj";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching Bombuj: {ex.Message}"); }
        }

        private async void iVysilaniButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appIVysilani);
                currentPage = "iVysílání";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching iVysilani: {ex.Message}"); }
        }

        private async void InternetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appInternet);
                currentPage = "Brave";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching Internet: {ex.Message}"); }
        }

        #endregion

        #region Cleanup

        protected override void OnClosed(EventArgs e)
        {
            _cursorService.RestoreDefaultCursors();
            base.OnClosed(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            server?.Dispose();
        }

        #endregion
    }
}