using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MauiApp1
{
    public class WebSocketClient
    {
        private ClientWebSocket client;
        private CancellationTokenSource receiveLoopCts;

        public event EventHandler Disconnected;

        public bool IsConnected => client?.State == WebSocketState.Open;

        public async Task ConnectAsync(string uri)
        {
            if (client != null && client.State == WebSocketState.Open)
                return;
            client = new ClientWebSocket();
            await client.ConnectAsync(new Uri(uri), CancellationToken.None);

            // Start listening for connection loss
            receiveLoopCts = new CancellationTokenSource();
            _ = Task.Run(() => MonitorConnection(receiveLoopCts.Token));
        }

        private async Task MonitorConnection(CancellationToken token)
        {
            var buffer = new byte[1];
            try
            {
                while (!token.IsCancellationRequested && client?.State == WebSocketState.Open)
                {
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
            }
            catch
            {
                // Ignorujeme chyby při čtení
            }

            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (client != null)
                {
                    receiveLoopCts?.Cancel();

                    if (client.State == WebSocketState.Open || client.State == WebSocketState.CloseReceived)
                    {
                        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
                    }

                    client.Dispose();
                    client = null;
                }
            }
            catch (Exception)
            {
                // Zatím nic
            }
            finally
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }


        public async Task SendMessageAsync(string message)
        {
            if (IsConnected)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
