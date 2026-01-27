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

namespace dumbTV
{
    public partial class MainWindow : Window
    {
        private WebSocketServer server;
        private string currentPage = "Home";

        private readonly CursorService _cursorService;
        private readonly InputService _inputService;

        public MainWindow()
        {
            InitializeComponent();

            _cursorService = new CursorService();
            _inputService = new InputService();

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
                if (!IsYouTubeAppRunning())
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "brave",
                        Arguments = "--app=https://www.youtube.com",
                        UseShellExecute = true,
                    });

                    await SendF11ToWindow("YouTube");
                    await MaximizeWindowByTitle("YouTube");
                }
                currentPage = "YouTube";
            }
            catch (Exception ex)
            {
                MessageBox.Show("YouTube nelze otevřít: " + ex.Message);
            }
        }

        private bool IsYouTubeAppRunning()
        {
            // Hledejte přímo okno podle titulku, ne přes process.MainWindowTitle
            bool found = false;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (title.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Volitelně ověřit, že okno patří procesu "brave"
                    GetWindowThreadProcessId(hWnd, out var pid);
                    try
                    {
                        var p = Process.GetProcessById((int)pid);
                        if (!p.HasExited && p.ProcessName.Equals("brave", StringComparison.OrdinalIgnoreCase))
                        {
                            ShowWindow(hWnd, SW_MAXIMIZE);
                            SetForegroundWindow(hWnd);
                            found = true;
                            return false;
                        }
                    }
                    catch { }
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        // VLC
        private async void VLCButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsVLCAppRunning())
                {
                    var process = Process.Start("C:/Program Files/VideoLAN/VLC/vlc.exe");
                    while (process.MainWindowHandle == IntPtr.Zero)
                    {
                        await Task.Delay(50);
                        process.Refresh();
                    }
                    SetForegroundWindow(process.MainWindowHandle);
                    await Task.Delay(100);
                    await SendF11ToWindow("VLC");
                }
                currentPage = "VLC";
            }
            catch (Exception ex)
            {
                MessageBox.Show("VLC nelze otevřít: " + ex.Message);
            }
        }

        private bool IsVLCAppRunning()
        {
            foreach (var process in Process.GetProcessesByName("vlc"))
            {
                try
                {
                    if (!process.HasExited && process.MainWindowTitle.Contains("VLC"))
                    {
                        EnumWindows((hWnd, lParam) =>
                        {
                            var sb = new StringBuilder(256);
                            GetWindowText(hWnd, sb, sb.Capacity);
                            string title = sb.ToString();

                            if (title.Contains("VLC") && IsWindowVisible(hWnd))
                            {
                                ShowWindow(hWnd, SW_MAXIMIZE);
                                SetForegroundWindow(hWnd);
                                return false;
                            }
                            return true;
                        }, IntPtr.Zero);
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        // Bombuj
        private async void BombujButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsBombujAppRunning())
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "brave",
                        Arguments = "--app=https://www.bombuj.si",
                        UseShellExecute = true,
                    });

                    await SendF11ToWindow("bombuj");
                    await MaximizeWindowByTitle("bombuj");
                }
                currentPage = "bombuj";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Bombuj nelze otevřít: " + ex.Message);
            }
        }


        private bool IsBombujAppRunning()
        {
            bool found = false;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (title.IndexOf("bombuj", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    GetWindowThreadProcessId(hWnd, out var pid);
                    try
                    {
                        var p = Process.GetProcessById((int)pid);
                        if (!p.HasExited && p.ProcessName.Equals("brave", StringComparison.OrdinalIgnoreCase))
                        {
                            ShowWindow(hWnd, SW_MAXIMIZE);
                            SetForegroundWindow(hWnd);
                            found = true;
                            return false;
                        }
                    }
                    catch { }
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        // iVysilani
        private async void iVysilaniButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsiVysilaniAppRunning())
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "brave",
                        Arguments = "--app=https://www.ivysilani.cz",
                        UseShellExecute = true,
                    });

                    await SendF11ToWindow("iVysílání");
                    await MaximizeWindowByTitle("iVysílání");
                }
                currentPage = "iVysílání";
            }
            catch (Exception ex)
            {
                MessageBox.Show("iVysíláni nelze otevřít: " + ex.Message);
            }
        }

        private bool IsiVysilaniAppRunning()
        {
            // Hledejte přímo okno podle titulku, ne přes process.MainWindowTitle
            bool found = false;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (title.IndexOf("iVysílání", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Volitelně ověřit, že okno patří procesu "brave"
                    GetWindowThreadProcessId(hWnd, out var pid);
                    try
                    {
                        var p = Process.GetProcessById((int)pid);
                        if (!p.HasExited && p.ProcessName.Equals("brave", StringComparison.OrdinalIgnoreCase))
                        {
                            ShowWindow(hWnd, SW_MAXIMIZE);
                            SetForegroundWindow(hWnd);
                            found = true;
                            return false;
                        }
                    }
                    catch { }
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        // Internet
        private void InternetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!IsInternetRunning())
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "brave",
                        UseShellExecute = true,
                    });
                }
                currentPage = "Brave";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Internet nelze otevřít: " + ex.Message);
            }
        }

        private bool IsInternetRunning()
        {
            // Hledejte přímo okno podle titulku, ne přes process.MainWindowTitle
            bool found = false;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (title.IndexOf("Brave", StringComparison.OrdinalIgnoreCase) >= 0
                    && title.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) < 0
                    && title.IndexOf("bombuj", StringComparison.OrdinalIgnoreCase) < 0
                    && title.IndexOf("iVysílání", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // Volitelně ověřit, že okno patří procesu "brave"
                    GetWindowThreadProcessId(hWnd, out var pid);
                    try
                    {
                        var p = Process.GetProcessById((int)pid);
                        if (!p.HasExited && p.ProcessName.Equals("brave", StringComparison.OrdinalIgnoreCase))
                        {
                            ShowWindow(hWnd, SW_MAXIMIZE);
                            SetForegroundWindow(hWnd);
                            found = true;
                            return false;
                        }
                    }
                    catch { }
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }


        private async Task MaximizeWindowByTitle(string AppName, int timeoutMs = 5000)
        {
            await Task.Run(() =>
            {
                const int intervalMs = 100;
                int waited = 0;
                while (waited < timeoutMs)
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        var sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, sb.Capacity);
                        if (sb.ToString().Contains(AppName) && IsWindowVisible(hWnd))
                        {
                            ShowWindow(hWnd, SW_MAXIMIZE);
                            SetForegroundWindow(hWnd);
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);
                    Thread.Sleep(intervalMs);
                    waited += intervalMs;
                }
            });
        }

        private async Task SendF11ToWindow(string AppName)
        {
            IntPtr targetHwnd = IntPtr.Zero;
            const int intervalMs = 100;
            int waited = 0;
            while (targetHwnd == IntPtr.Zero && waited < 5000)
            {
                EnumWindows((hWnd, lParam) =>
                {
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    if (sb.ToString().Contains(AppName) && IsWindowVisible(hWnd))
                    {
                        targetHwnd = hWnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);

                await Task.Delay(intervalMs);
                waited += intervalMs;
            }

            if (targetHwnd != IntPtr.Zero)
            {
                PostMessage(targetHwnd, WM_KEYDOWN, (IntPtr)VK_F11, IntPtr.Zero);
                PostMessage(targetHwnd, WM_KEYUP, (IntPtr)VK_F11, IntPtr.Zero);
            }
        }

        private bool IsWindowFullscreen(string AppName)
        {
            IntPtr targetHwnd = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().Contains(AppName) && IsWindowVisible(hWnd))
                {
                    targetHwnd = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (!IsWindowVisible(targetHwnd)) return false;
            GetWindowRect(targetHwnd, out RECT rect);

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            return rect.Left <= 0 &&
                   rect.Top <= 0 &&
                   width >= (int)screenWidth &&
                   height >= (int)screenHeight;
        }
    }
}
