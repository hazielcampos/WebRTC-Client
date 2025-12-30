using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebRTC_Client.Networking
{
    public class WebSocketClient
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _receiveCts;
        private readonly object _lock = new object();
        private bool _disconnectedRaised;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnMessage;
        public event Action<Exception> OnError;

        public bool IsConnected =>
            _ws != null && _ws.State == WebSocketState.Open;

        public async Task ConnectAsync(string url)
        {
            lock (_lock)
            {
                _ws = new ClientWebSocket();
                _receiveCts = new CancellationTokenSource();
                _disconnectedRaised = false;
            }

            await _ws.ConnectAsync(new Uri(url), CancellationToken.None);
            OnConnected?.Invoke();

            _ = Task.Run(ReceiveLoopAsync);
        }

        public async Task SendAsync(string message)
        {
            if (!IsConnected)
                return;

            await _sendLock.WaitAsync();
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);

                await _ws.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                TriggerDisconnected();
            }
            finally
            {
                _sendLock.Release();
            }
        }


        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[4096];

            try
            {
                while (IsConnected && !_receiveCts.IsCancellationRequested)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _ws.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            _receiveCts.Token
                        );

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await CloseInternalAsync();
                            return;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    } while (!result.EndOfMessage);

                    OnMessage?.Invoke(sb.ToString());
                }
            }
            catch (OperationCanceledException)
            {
                // cierre normal, no error
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
            finally
            {
                TriggerDisconnected();
            }
        }

        public async Task DisconnectAsync()
        {
            await CloseInternalAsync();
            TriggerDisconnected();
        }

        private async Task CloseInternalAsync()
        {
            lock (_lock)
            {
                if (_receiveCts?.IsCancellationRequested == false)
                    _receiveCts.Cancel();
            }

            try
            {
                if (_ws != null &&
                    (_ws.State == WebSocketState.Open ||
                     _ws.State == WebSocketState.CloseReceived))
                {
                    await _ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None
                    );
                }
            }
            catch
            {
                // ignore
            }
        }

        private void TriggerDisconnected()
        {
            lock (_lock)
            {
                if (_disconnectedRaised)
                    return;

                _disconnectedRaised = true;
            }

            OnDisconnected?.Invoke();
        }
    }
}
