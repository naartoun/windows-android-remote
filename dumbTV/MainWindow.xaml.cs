using Fleck;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;
using static dumbTV.Core.NativeMethods;
using dumbTV.Services;
using dumbTV.Models;

namespace dumbTV
{
    public partial class MainWindow : Window
    {
        private WebSocketServer server;
        private string currentPage = "Home";

        private readonly CursorService _cursorService;
        private readonly InputService _inputService;

        private readonly AppLauncherService _appLauncher;

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


        public MainWindow()
        {
            InitializeComponent();

            _cursorService = new CursorService();
            _inputService  = new InputService();
            _appLauncher   = new AppLauncherService();

            StartWebSocketServer();
            _cursorService.ApplyCustomCursor("C:\\Users\\Radim Kopunec\\Desktop\\dumbTV\\dumbTV\\cursor.cur");
        }

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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BombujButton.Focus();
            AnimateGradientMovement();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cursorService.RestoreDefaultCursors();
            base.OnClosed(e);
        }

        private void AnimateGradientMovement()
        {
            PointAnimation startAnim = new PointAnimation()
            {
                From = new Point(0, 0),
                To = new Point(0.1, 0.1), // posun jen trochu
                Duration = TimeSpan.FromSeconds(5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            // animace pro EndPoint
            PointAnimation endAnim = new PointAnimation()
            {
                From = new Point(1, 1),
                To = new Point(0.9, 0.9), // opačný posun
                Duration = TimeSpan.FromSeconds(5),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            bgGradient.BeginAnimation(LinearGradientBrush.StartPointProperty, startAnim);
            bgGradient.BeginAnimation(LinearGradientBrush.EndPointProperty, endAnim);
        }

        private void HandleWebSocketCommand(string cmd)
        {
            Dispatcher.Invoke(() =>
            {
                if (cmd == "CLICK:MOUSELEFT")
                {
                    // Logic: If we were using keyboard/remote (2) or are on Home screen, act as ENTER key.
                    // Otherwise act as Mouse Click.
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

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            server.Dispose();
        }

        private void GoHome()
        {
            // Pozastavení otevřených aplikací
            PauseBrave();
            PauseVLC();

            // Přepnutí do hlavní obrazovky (WPF okno)
            this.Activate(); // vrátí fokus na hlavní okno
            currentPage = "Home";

            SetCursorPos(0, 0);
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            if (currentPage == "Home")
            {
                SetCursorPos(0, 0);
                e.Handled = true;
                return;
            }
            base.OnPreviewMouseMove(e);
        }

        public bool IsProcessPlayingAudio(string processName)
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (session.State == AudioSessionState.AudioSessionStateActive)
                {
                    int pid = (int)session.GetProcessID;
                    try
                    {
                        var p = Process.GetProcessById(pid);
                        if (p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch { }
                }
            }
            return false;
        }


        private void PauseVLC()
        {
            foreach (var process in Process.GetProcessesByName("vlc"))
            {
                try
                {
                    if (!process.HasExited && process.MainWindowTitle.Contains("VLC"))
                    {
                        if (IsProcessPlayingAudio("vlc"))
                            _inputService.SendSpaceToWindow(process.MainWindowHandle);
                    }
                }
                catch { }
            }
        }

        private void PauseBrave()
        {
            //zatim nefunguje jak chci
            /*
            foreach (var process in Process.GetProcessesByName("brave"))
            {
                try
                {
                    if (!process.HasExited && process.MainWindowTitle.Contains(currentPage))
                    {
                        SendSpaceKeyToWindow(process.MainWindowHandle);
                    }
                }
                catch { }
            }*/
        }

        // YouTube
        private async void YouTubeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appYouTube);
                currentPage = "YouTube";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching YouTube: {ex.Message}"); }
        }


        // VLC
        private async void VLCButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appVLC);
                currentPage = "VLC";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching VLC: {ex.Message}"); }
        }


        // Bombuj
        private async void BombujButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appBombuj);
                currentPage = "bombuj";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching Bombuj: {ex.Message}"); }
        }


        // iVysilani
        private async void iVysilaniButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appIVysilani);
                currentPage = "iVysílání";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching iVysilani: {ex.Message}"); }
        }


        // Internet
        private async void InternetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _appLauncher.LaunchAppAsync(_appInternet);
                currentPage = "Brave";
            }
            catch (Exception ex) { MessageBox.Show($"Error launching Internet: {ex.Message}"); }
        }
    }
}
