/*
 * =========================================================================================
 * File: MainViewModel.cs
 * Namespace: dumbRemote.ViewModels
 * Author: Radim Kopunec
 * Description: ViewModel for the main screen. 
 * Handles user interactions, connection logic, and sends commands via WebSocketService.
 * =========================================================================================
 */

using System.Windows.Input;
using Microsoft.Maui.Graphics;
using dumbRemote.Services;
using Microsoft.Maui.Storage;
using System.Net.Sockets;
using System.Net;

namespace dumbRemote.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly IWebSocketService _webSocketService;

        // --- Properties (Data Binding) ---

        private string _ipAddress;
        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                if (SetProperty(ref _ipAddress, value))
                {
                    Preferences.Set("LastIpAddress", value);
                }
            }
        }

        private bool _isIpEntryVisible;
        public bool IsIpEntryVisible
        {
            get => _isIpEntryVisible;
            set => SetProperty(ref _isIpEntryVisible, value);
        }

        private string _connectButtonText = "Připojit";
        public string ConnectButtonText
        {
            get => _connectButtonText;
            set => SetProperty(ref _connectButtonText, value);
        }

        private Color _connectButtonColor = Color.FromArgb("#505050");
        public Color ConnectButtonColor
        {
            get => _connectButtonColor;
            set => SetProperty(ref _connectButtonColor, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    UpdateConnectionStatus();
                }
            }
        }

        private string _macAddress;
        public string MacAddress
        {
            get => _macAddress;
            set
            {
                if (SetProperty(ref _macAddress, value))
                    Preferences.Set("LastMacAddress", value);
            }
        }

        private bool _isMacEntryVisible;
        public bool IsMacEntryVisible
        {
            get => _isMacEntryVisible;
            set => SetProperty(ref _isMacEntryVisible, value);
        }

        private string _shutdownButtonText = "Vypnout";
        public string ShutdownButtonText
        {
            get => _shutdownButtonText;
            set => SetProperty(ref _shutdownButtonText, value);
        }

        private Color _shutdownButtonColor = Color.FromArgb("#2D2D2D");
        public Color ShutdownButtonColor
        {
            get => _shutdownButtonColor;
            set => SetProperty(ref _shutdownButtonColor, value);
        }

        private bool _isShutdownConfirm = false;

        // --- Commands (Buttons) ---
        public ICommand WakeOnLanCommand { get; }
        public ICommand ToggleMacEntryCommand { get; }
        public ICommand ShutdownCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand ToggleIpEntryCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand TypeTextCommand { get; }

        // --- Constructor ---

        public MainViewModel(IWebSocketService webSocketService)
        {
            _webSocketService = webSocketService;

            IpAddress = Preferences.Get("LastIpAddress", "192.168.0.x");
            IsIpEntryVisible = false;

            _webSocketService.Connected += (s, e) => IsConnected = true;
            _webSocketService.Disconnected += (s, e) => IsConnected = false;

            IpAddress = Preferences.Get("LastIpAddress", "192.168.0.x");
            MacAddress = Preferences.Get("LastMacAddress", "00:00:00:00:00:00");

            IsIpEntryVisible = false;
            IsMacEntryVisible = false;

            ConnectCommand = new Command(async () => await OnConnectAsync());
            ToggleIpEntryCommand = new Command(() => IsIpEntryVisible = !IsIpEntryVisible);
            SendCommand = new Command<string>(async (cmd) => await _webSocketService.SendMessageAsync(cmd));
            TypeTextCommand = new Command<string>(async (txt) => await SendTypeCommand(txt));

            ToggleMacEntryCommand = new Command(() => IsMacEntryVisible = !IsMacEntryVisible);
            WakeOnLanCommand = new Command(async () => await OnWakeOnLanAsync());
            ShutdownCommand = new Command(OnShutdown);
        }

        // --- Logic ---

        private async Task OnConnectAsync()
        {
            if (IsConnected)
            {
                await _webSocketService.DisconnectAsync();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(IpAddress)) return;

                IsIpEntryVisible = false;

                IsIpEntryVisible = false;
                ConnectButtonText = "Připojování...";
                ConnectButtonColor = Color.FromArgb("#D5E40F");

                bool success = false;

                // Retry Loop: Try to connect 5 times (approx 2.5 - 5 seconds total)
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        // Standard timeout inside ConnectAsync is usually short for local network,
                        // so we loop to give it "more time" and persistence.
                        await _webSocketService.ConnectAsync(IpAddress, 8080);

                        // If we reach here, we are connected
                        success = true;
                        break;
                    }
                    catch
                    {
                        // Failed attempt, wait a bit before retrying
                        await Task.Delay(500);
                    }
                }

                if (!success)
                {
                    // If all attempts failed
                    UpdateConnectionStatus(); // Reset to "Disconnected" state
                    // Optional: Show toast/alert here if needed, but the button reset indicates failure
                }
            }
        }

        private void UpdateConnectionStatus()
        {
            if (IsConnected)
            {
                ConnectButtonText = "Připojeno";
                ConnectButtonColor = Colors.LightGreen;
            }
            else
            {
                ConnectButtonText = "Připojit";
                ConnectButtonColor = Color.FromArgb("#505050");
            }
        }

        private async Task SendTypeCommand(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // Prefix for typing command
            await _webSocketService.SendMessageAsync($"TYPE:{text}");
        }

        /// <summary>
        /// Public method to be called from CodeBehind for high-frequency events (Touchpad).
        /// Commands are too slow/heavy for real-time pan gestures.
        /// </summary>
        public async Task SendMove(int dx, int dy)
        {
            if (!IsConnected) return;
            await _webSocketService.SendMessageAsync($"MOVE:{dx}:{dy}");
        }

        private async Task OnWakeOnLanAsync()
        {
            if (string.IsNullOrWhiteSpace(MacAddress)) return;

            IsMacEntryVisible = false;

            try
            {
                string mac = MacAddress.Replace(":", "").Replace("-", "").Trim();
                if (mac.Length != 12) return;

                byte[] macBytes = new byte[6];
                for (int i = 0; i < 6; i++)
                {
                    macBytes[i] = Convert.ToByte(mac.Substring(i * 2, 2), 16);
                }

                byte[] packet = new byte[6 + 16 * 6];
                for (int i = 0; i < 6; i++) packet[i] = 0xFF;
                for (int i = 1; i <= 16; i++)
                    Buffer.BlockCopy(macBytes, 0, packet, i * 6, 6);

                using var client = new UdpClient();
                client.EnableBroadcast = true;
                await client.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
            }
            catch {  }
        }

        private void OnShutdown()
        {
            if (!_isShutdownConfirm)
            {
                _isShutdownConfirm = true;
                ShutdownButtonText = "Opravdu?";
                ShutdownButtonColor = Colors.DarkRed;

                Task.Delay(3000).ContinueWith(_ =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _isShutdownConfirm = false;
                        ShutdownButtonText = "Vypnout";
                        ShutdownButtonColor = Color.FromArgb("#2D2D2D");
                    });
                });
            }
            else
            {
                if (IsConnected)
                {
                    _ = _webSocketService.SendMessageAsync("POWER:OFF");
                }

                _isShutdownConfirm = false;
                ShutdownButtonText = "Vypnout";
                ShutdownButtonColor = Color.FromArgb("#2D2D2D");
            }
        }
    }
}