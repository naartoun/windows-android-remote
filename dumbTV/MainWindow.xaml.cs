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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
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

        private List<int> _suspendedProcessIds = new List<int>();
        private string _lastAppPage = "";

        // Accumulators for joystick movement translation
        private double _joyAccX = 0;
        private double _joyAccY = 0;
        // Threshold: How much movement is needed to trigger a key press (higher = slower/less sensitive)
        private const double JOYSTICK_THRESHOLD = 200.0;

        // Flag to track if the last interaction was via mouse (joystick) or keyboard (d-pad)
        private bool _wasLastInputMouse = false;

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
            string tempCursorPath = Path.Combine(Path.GetTempPath(), "dumbTV_cursor.cur");
            ExtractResource("dumbTV.cursor.cur", tempCursorPath);
            _cursorService.ApplyCustomCursor(tempCursorPath);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set initial focus and start background animation
            BombujButton.Focus();
            AnimateGradientMovement();
            ShowIpAddress();
        }

        private void ExtractResource(string resourceName, string outputPath)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    MessageBox.Show($"Chyba: Resource '{resourceName}' nenalezen!");
                    return;
                }

                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }
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
        /// Moves the mouse cursor relatively by dx, dy.
        /// </summary>
        private void MoveCursor(int dx, int dy)
        {
            if (GetCursorPos(out POINT currentPos))
            {
                SetCursorPos(currentPos.X + dx, currentPos.Y + dy);
            }
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
                    if (currentPage == "Home" || !_wasLastInputMouse)
                    {
                        _inputService.SendKey(VK_RETURN);
                    }
                    else
                    {
                        _inputService.MouseLeftClick();
                    }
                }
                else if (cmd == "CLICK:MOUSERIGHT")
                {
                    _inputService.MouseRightClick();
                }
                else if (cmd == "CLICK:UP")
                {
                    _wasLastInputMouse = false;
                    _inputService.SendKey(VK_UP);
                }
                else if (cmd == "CLICK:DOWN")
                {
                    _wasLastInputMouse = false;
                    _inputService.SendKey(VK_DOWN);
                }
                else if (cmd == "CLICK:LEFT")
                {
                    _wasLastInputMouse = false;
                    _inputService.SendKey(VK_LEFT);
                }
                else if (cmd == "CLICK:RIGHT")
                {
                    _wasLastInputMouse = false;
                    _inputService.SendKey(VK_RIGHT);
                }
                else if (cmd == "CLICK:BACK")
                {
                    _inputService.SendKey(VK_BROWSER_BACK);
                }
                else if (cmd == "CLICK:HOME")
                {
                    GoHome();
                }
                else if (cmd == "CLICK:MUTE")
                {
                    _inputService.SendKey(VK_VOLUME_MUTE);
                }
                else if (cmd == "CLICK:VOLDOWN")
                {
                    _inputService.SendKey(VK_VOLUME_DOWN);
                }
                else if (cmd == "CLICK:VOLUP")
                {
                    _inputService.SendKey(VK_VOLUME_UP);
                }
                else if (cmd == "BACKSPACE")
                {
                    _inputService.SendBackspace();
                }
                else if (cmd.StartsWith("TYPE:") && cmd.Length > 5)
                {
                    _inputService.SendChar(cmd[5]);
                }
                else if (cmd.StartsWith("MOVE:"))
                {
                    var parts = cmd.Split(':');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int dx) && int.TryParse(parts[2], out int dy))
                    {
                        // --- INTELLIGENT NAVIGATION LOGIC ---

                        if (currentPage == "Home")
                        {
                            // MODE: JOYSTICK NAVIGATION (Simulates D-Pad)
                            // Instead of moving the mouse, we accumulate the vectors.
                            // When threshold is reached, we press an Arrow Key.

                            _joyAccX += dx;
                            _joyAccY += dy;

                            // Check Horizontal Threshold
                            if (Math.Abs(_joyAccX) > JOYSTICK_THRESHOLD)
                            {
                                _wasLastInputMouse = false;
                                if (_joyAccX > 0) _inputService.SendKey(VK_RIGHT); // or HandleCommand("CLICK:RIGHT");
                                else _inputService.SendKey(VK_LEFT);  // or HandleCommand("CLICK:LEFT");

                                _joyAccX = 0; // Reset after trigger
                            }

                            // Check Vertical Threshold
                            if (Math.Abs(_joyAccY) > JOYSTICK_THRESHOLD)
                            {
                                _wasLastInputMouse = false;
                                if (_joyAccY > 0) _inputService.SendKey(VK_DOWN);  // Y is inverted in screens? Usually Down is +
                                else _inputService.SendKey(VK_UP);

                                _joyAccY = 0; // Reset after trigger
                            }
                        }
                        else
                        {
                            // MODE: MOUSE CURSOR (Standard)
                            // On other pages (Browser, VLC...), act as a mouse.
                            _wasLastInputMouse = true;
                            MoveCursor(dx, dy);
                        }
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
            if (currentPage != "Home" && _suspendedProcessIds.Count == 0)
            {
                string procName = "";
                if (currentPage == "YouTube" || currentPage == "bombuj" || currentPage == "iVysílání" || currentPage == "Brave")
                    procName = "brave";
                else if (currentPage == "VLC")
                    procName = "vlc";

                if (!string.IsNullOrEmpty(procName))
                {
                    var processes = Process.GetProcessesByName(procName);
                    foreach (var p in processes)
                    {
                        try
                        {
                            Core.NativeMethods.NtSuspendProcess(p.Handle);
                            _suspendedProcessIds.Add(p.Id);
                        }
                        catch { }
                    }
                    if (_suspendedProcessIds.Count > 0) _lastAppPage = currentPage;
                }
            }

            this.Activate();
            currentPage = "Home";
            _joyAccX = 0;
            _joyAccY = 0;
            SetCursorPos(0, 0);
            BombujButton.Focus();
        }

        #endregion

        #region Event Handlers - App Launching

        private async void YouTubeButton_Click(object sender, RoutedEventArgs e)
        {
            await ResumeOrLaunch(_appYouTube, "YouTube");
        }

        private async void VLCButton_Click(object sender, RoutedEventArgs e)
        {
            await ResumeOrLaunch(_appVLC, "VLC");
        }

        private async void BombujButton_Click(object sender, RoutedEventArgs e)
        {
            await ResumeOrLaunch(_appBombuj, "bombuj");
        }

        private async void iVysilaniButton_Click(object sender, RoutedEventArgs e)
        {
            await ResumeOrLaunch(_appIVysilani, "iVysílání");
        }

        private async void InternetButton_Click(object sender, RoutedEventArgs e)
        {
            await ResumeOrLaunch(_appInternet, "Brave");
        }

        private async Task ResumeOrLaunch(AppItem app, string pageName)
        {
            try
            {
                if (_suspendedProcessIds.Count > 0 && _lastAppPage == pageName)
                {
                    foreach (var pid in _suspendedProcessIds)
                    {
                        try
                        {
                            var p = Process.GetProcessById(pid);
                            Core.NativeMethods.NtResumeProcess(p.Handle);

                            if (p.MainWindowHandle != IntPtr.Zero)
                            {
                                Core.NativeMethods.ShowWindow(p.MainWindowHandle, Core.NativeMethods.SW_MAXIMIZE);
                                Core.NativeMethods.SetForegroundWindow(p.MainWindowHandle);
                            }
                        }
                        catch { }
                    }
                    _suspendedProcessIds.Clear();
                }
                else
                {
                    if (_suspendedProcessIds.Count > 0)
                    {
                        foreach (var pid in _suspendedProcessIds)
                        {
                            try { Process.GetProcessById(pid).Kill(); } catch { }
                        }
                        _suspendedProcessIds.Clear();
                    }

                    await _appLauncher.LaunchAppAsync(app);
                }

                currentPage = pageName;
            }
            catch (Exception ex) { MessageBox.Show("Chyba: " + ex.Message); }
        }

        private void ShowIpAddress()
        {
            try
            {
                string bestIp = "Nenalezeno";
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up ||
                        nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                    var ipProps = nic.GetIPProperties();
                    if (ipProps.GatewayAddresses.Count == 0) continue;

                    foreach (var ip in ipProps.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            bestIp = ip.Address.ToString();
                            break;
                        }
                    }
                    if (bestIp != "Nenalezeno") break;
                }
                IpAddressLabel.Text = bestIp;
            }
            catch { IpAddressLabel.Text = "Chyba sítě"; }
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