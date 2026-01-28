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

        // --- Commands (Buttons) ---

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

            // Subscribe to service events to keep UI in sync
            _webSocketService.Connected += (s, e) => IsConnected = true;
            _webSocketService.Disconnected += (s, e) => IsConnected = false;

            // Initialize Commands
            ConnectCommand = new Command(async () => await OnConnectAsync());

            // Command for showing IP Address entry
            ToggleIpEntryCommand = new Command(() => IsIpEntryVisible = !IsIpEntryVisible);

            // Generic command for simple buttons (e.g., CommandParameter="CLICK:HOME")
            SendCommand = new Command<string>(async (cmd) => await _webSocketService.SendMessageAsync(cmd));

            // Command for typing text
            TypeTextCommand = new Command<string>(async (txt) => await SendTypeCommand(txt));
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
    }
}