using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebRTC_Client.Networking
{
    public class WebSocketClient
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnMessage;
        public event Action<Exception> OnError;

        public bool IsConnected => _ws?.State == WebSocketState.Open;

        public async Task ConnectAsync(string url)
        {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            OnConnected?.Invoke();

            _ = Task.Run(ReceiveLoop);
        }
        public async Task SendAsync(string message)
        {
            if (_ws.State != WebSocketState.Open)
                return;

            var data = Encoding.UTF8.GetBytes(message);
            await _ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, _cts.Token);
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[1024 * 4];
            try
            {
                while (_ws.State == WebSocketState.Open)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            OnDisconnected?.Invoke();
                            return;
                        }
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    OnMessage?.Invoke(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                OnDisconnected?.Invoke();
            }
        }

        public async Task DisconnectedAsync()
        {
            _cts.Cancel();
            if(_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Manual close", CancellationToken.None);
                OnDisconnected?.Invoke();
            }
        }
    }
}
