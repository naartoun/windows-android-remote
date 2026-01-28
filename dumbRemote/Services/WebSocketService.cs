/*
 * =========================================================================================
 * File: WebSocketService.cs
 * Namespace: dumbRemote.Services
 * Author: Radim Kopunec
 * Description: Implementation of the WebSocket service.
 * Handles low-level socket connections, message sending, and connection monitoring.
 * =========================================================================================
 */

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace dumbRemote.Services
{
    /// <summary>
    /// Manages the WebSocket connection to the desktop server.
    /// </summary>
    public class WebSocketService : IWebSocketService
    {
        private ClientWebSocket _client;
        private CancellationTokenSource _connectionCts;

        public event EventHandler Disconnected;
        public event EventHandler Connected;

        public bool IsConnected => _client != null && _client.State == WebSocketState.Open;

        public async Task ConnectAsync(string ipAddress, int port)
        {
            // If already connected, do nothing
            if (IsConnected) return;

            // Clean up previous instance if exists
            if (_client != null) _client.Dispose();

            _client = new ClientWebSocket();
            _connectionCts = new CancellationTokenSource();

            var uri = new Uri($"ws://{ipAddress}:{port}/ws/");

            try
            {
                // Attempt to connect with a timeout/cancellation token
                await _client.ConnectAsync(uri, _connectionCts.Token);

                // Notify subscribers that we are connected
                Connected?.Invoke(this, EventArgs.Empty);

                // Start a background task to listen for disconnects (or incoming messages)
                _ = Task.Run(() => MonitorConnectionAsync(_connectionCts.Token));
            }
            catch (Exception ex)
            {
                // Ensure cleanup on failure
                _client?.Dispose();
                _client = null;
                throw new Exception($"Failed to connect to {uri}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Background loop that monitors the socket state.
        /// Detects if the server closes the connection.
        /// </summary>
        private async Task MonitorConnectionAsync(CancellationToken token)
        {
            var buffer = new byte[1024];
            try
            {
                while (_client != null && _client.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    // We need to receive data to detect closure, even if we don't use the data
                    var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await DisconnectAsync();
                    }
                }
            }
            catch
            {
                // Any error in receiving usually means the connection is dead
                if (IsConnected) await DisconnectAsync();
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client == null) return;

            try
            {
                _connectionCts?.Cancel();

                if (_client.State == WebSocketState.Open)
                {
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                }
            }
            catch
            {
                // Ignore errors during disconnect sequence
            }
            finally
            {
                _client.Dispose();
                _client = null;
                // Notify UI that we are disconnected
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (!IsConnected || _client == null) return;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                // If sending fails, assume connection is broken
                await DisconnectAsync();
            }
        }
    }
}