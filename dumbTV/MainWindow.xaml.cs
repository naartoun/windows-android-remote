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

namespace dumbTV
{
    public static class GlobalCursor
    {
        private const uint OCR_NORMAL = 32512; // šipka
        private const uint OCR_IBEAM = 32513; // textový kurzor
        private const uint OCR_WAIT = 32514; // hodiny/kolečko
        private const uint OCR_CROSS = 32515; // kříž
        private const uint OCR_UP = 32516; // šipka nahoru
        private const uint OCR_SIZE = 32640; // všeobecný resize
        private const uint OCR_ICON = 32641;
        private const uint OCR_SIZENWSE = 32642; // resize ↘↖
        private const uint OCR_SIZENESW = 32643; // resize ↗↙
        private const uint OCR_SIZEWE = 32644; // resize ↔
        private const uint OCR_SIZENS = 32645; // resize ↕
        private const uint OCR_SIZEALL = 32646; // čtyřšipka
        private const uint OCR_NO = 32648; // přeškrtnutý kruh
        private const uint OCR_HAND = 32649; // ručička (odkaz)
        private const uint OCR_APPSTARTING = 32650; // šipka + kolečko

        private const uint SPI_SETCURSORS = 0x57;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr LoadCursorFromFile(string lpFileName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        public static void ApplyCustomCursor(string cursorFilePath)
        {
            uint[] ids = new uint[]
            {
                OCR_NORMAL, OCR_IBEAM, OCR_WAIT, OCR_CROSS, OCR_UP,
                OCR_SIZE, OCR_ICON, OCR_SIZENWSE, OCR_SIZENESW,
                OCR_SIZEWE, OCR_SIZENS, OCR_SIZEALL, OCR_NO,
                OCR_HAND, OCR_APPSTARTING
            };

            foreach (var id in ids)
            {
                IntPtr hCursor = LoadCursorFromFile(cursorFilePath);
                if (hCursor != IntPtr.Zero)
                {
                    SetSystemCursor(hCursor, id);
                }
            }
        }


        public static void RestoreDefaultCursors()
        {
            // obnoví všechny kurzory na výchozí Windows vzhled
            SystemParametersInfo(0x57, 0, IntPtr.Zero, 0);
        }
    }

    public partial class MainWindow : Window
    {
        private WebSocketServer server;
        private string currentPage = "Home";
        private int lastInputType = 0;

        public MainWindow()
        {
            InitializeComponent();
            StartWebSocketServer();
            GlobalCursor.ApplyCustomCursor("C:\\Users\\Radim Kopunec\\Desktop\\dumbTV\\dumbTV\\cursor.cur");
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
            GlobalCursor.RestoreDefaultCursors();
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
                    MouseLeftClick();
                else if (cmd == "CLICK:MOUSERIGHT")
                    MouseRightClick();
                else if (cmd == "CLICK:UP")
                    UpClick();
                else if (cmd == "CLICK:DOWN")
                    DownClick();
                else if (cmd == "CLICK:LEFT")
                    LeftClick();
                else if (cmd == "CLICK:RIGHT")
                    RightClick();
                else if (cmd == "CLICK:BACK")
                    SendVk(VK_BROWSER_BACK);
                else if (cmd == "CLICK:HOME")
                    GoHome();
                else if (cmd == "CLICK:MUTE")
                    SendVk(VK_VOLUME_MUTE);
                else if (cmd == "CLICK:VOLDOWN")
                    SendVk(VK_VOLUME_DOWN);
                else if (cmd == "CLICK:VOLUP")
                    SendVk(VK_VOLUME_UP);
                else if (cmd == "BACKSPACE")
                {
                    keybd_event(VK_BACK, 0, 0, 0);
                    keybd_event(VK_BACK, 0, (int)KEYEVENTF_KEYUP, 0);
                }
                else if (cmd.StartsWith("TYPE:") && cmd.Length > 5)
                    SendChar(cmd[5]);
                else if (cmd.StartsWith("MOVE:"))
                {
                    var parts = cmd.Substring(5).Split(':');
                    if (parts.Length == 2
                        && int.TryParse(parts[0], out var dx)
                        && int.TryParse(parts[1], out var dy))
                    {
                        MoveCursor(dx, dy);
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
                            SendSpaceKeyToWindow(process.MainWindowHandle);
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

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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


        //

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

        // WinAPI declarations
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private const int SW_MAXIMIZE = 3;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const int VK_F11 = 0x7A;
        private const int VK_BROWSER_BACK = 0xA6;
        private const int VK_HOME = 0x24;
        private const int VK_VOLUME_MUTE = 0xAD;
        private const int VK_VOLUME_DOWN = 0xAE;
        private const int VK_VOLUME_UP = 0xAF;
        private const int VK_BACK = 0x08;

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        const uint MOUSEEVENTF_LEFTUP = 0x04;
        const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        const uint MOUSEEVENTF_RIGHTUP = 0x10;

        public void MoveCursor(int dx, int dy)
        {
            GetCursorPos(out var p);
            SetCursorPos(p.X + dx, p.Y + dy);
            lastInputType = 1;
        }

        public void MouseLeftClick()
        {
            if (lastInputType == 2 || currentPage == "Home")
            {
                keybd_event(VK_RETURN, 0, 0, 0);
                keybd_event(VK_RETURN, 0, (int)KEYEVENTF_KEYUP, 0);
            }
            else
            {
                GetCursorPos(out var p);
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, p.X, p.Y, 0, UIntPtr.Zero);
            }
        }

        public void MouseRightClick()
        {
            GetCursorPos(out var p);
            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, p.X, p.Y, 0, UIntPtr.Zero);
        }

        // Cursor moves
        const int VK_RETURN = 0x0D;
        const int VK_LEFT = 0x25;
        const int VK_UP = 0x26;
        const int VK_RIGHT = 0x27;
        const int VK_DOWN = 0x28;

        public void UpClick()
        {
            //if (currentPage == "Home" || currentPage == "VLC")
            {
                keybd_event(VK_UP, 0, 0, 0);
                keybd_event(VK_UP, 0, (int)KEYEVENTF_KEYUP, 0);
            }
            //else
            //    MoveCursor(0, -20);
            lastInputType = 2;
        }
        public void DownClick()
        {
            //if (currentPage == "Home" || currentPage == "VLC")
            {
                keybd_event(VK_DOWN, 0, 0, 0);
                keybd_event(VK_DOWN, 0, (int)KEYEVENTF_KEYUP, 0);
            }
            //else
            //    MoveCursor(0, 20);
            lastInputType = 2;
        }
        public void LeftClick()
        {
            //if (currentPage == "Home" || currentPage == "VLC")
            {
                keybd_event(VK_LEFT, 0, 0, 0);
                keybd_event(VK_LEFT, 0, (int)KEYEVENTF_KEYUP, 0);
            }
            //else
            //    MoveCursor(-20, 0);
            lastInputType = 2;
        }
        public void RightClick()
        {
            //if (currentPage == "Home" || currentPage == "VLC")
            {
                keybd_event(VK_RIGHT, 0, 0, 0);
                keybd_event(VK_RIGHT, 0, (int)KEYEVENTF_KEYUP, 0);
            }
            //else
            //    MoveCursor(20, 0);
            lastInputType = 2;
        }

        // Keyboard input
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion U; }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_UNICODE = 0x0004;
        const uint KEYEVENTF_KEYUP = 0x0002;

    ////// Vyžaduje certifikát a uiAcces="true" (zatím nebudu řešit)
        /*public void SendChar(char c)
        {
            INPUT[] inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk     = 0,
                            wScan   = c,
                            dwFlags = KEYEVENTF_UNICODE
                        }
                    }
                },
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk     = 0,
                            wScan   = c,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                        }
                    }
                }
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        public void SendVk(int vk)
        {
            INPUT[] inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk     = (ushort)vk,
                            wScan   = 0,
                            dwFlags = 0
                        }
                    }
                },
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk     = (ushort)vk,
                            wScan   = 0,
                            dwFlags = KEYEVENTF_KEYUP
                        }
                    }
                }
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }*/

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        public void SendChar(char c)
        {
            short vk = VkKeyScan(c);
            if (vk == -1) return; // znak nelze napsat přes aktuální layout

            byte vkCode = (byte)(vk & 0xFF);
            byte shiftState = (byte)((vk >> 8) & 0xFF);

            // SHIFT
            if ((shiftState & 1) != 0) keybd_event(0x10, 0, 0, 0);

            // CTRL (nepoužívá se u běžných znaků, ale pro jistotu)
            if ((shiftState & 2) != 0) keybd_event(0x11, 0, 0, 0);

            // ALT
            if ((shiftState & 4) != 0) keybd_event(0x12, 0, 0, 0);

            // Znak
            keybd_event(vkCode, 0, 0, 0);
            keybd_event(vkCode, 0, (int)KEYEVENTF_KEYUP, 0);

            // Uvolnit modifikátory
            if ((shiftState & 4) != 0) keybd_event(0x12, 0, (int)KEYEVENTF_KEYUP, 0);
            if ((shiftState & 2) != 0) keybd_event(0x11, 0, (int)KEYEVENTF_KEYUP, 0);
            if ((shiftState & 1) != 0) keybd_event(0x10, 0, (int)KEYEVENTF_KEYUP, 0);
        }

        public void SendVk(int vk)
        {
            keybd_event((byte)vk, 0, 0, (int)UIntPtr.Zero);
            keybd_event((byte)vk, 0, (int)KEYEVENTF_KEYUP, (int)UIntPtr.Zero);
        }

        [DllImport("user32.dll")]
        static extern short VkKeyScan(char ch);

        private const int VK_SPACE = 0x20;

        private void SendSpaceKeyToWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_SPACE, IntPtr.Zero);
            PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_SPACE, IntPtr.Zero);
        }
    }
}
