using SIPSorcery.Net;
using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace WebRTC_Client.Networking
{
    public class Host : NetworkObject
    {
        public event Action<string> OnRoomCreated;
        public event Func<string, Task> OnPeerJoined;
        public event Func<string, string, Task> OnWebRTCAnswerReceived;
        public event Func<string, string, Task> OnWebRTCIceCandidateReceived;
        public event Action<string, string> OnMessageReceived;

        public ConcurrentDictionary<string, PeerSession> peers =
            new ConcurrentDictionary<string, PeerSession>();

        private string _roomId;

        public async Task StartAsync()
        {
            await ConnectAsync();

            OnRoomCreated += roomId =>
            {
                _roomId = roomId;
                Console.WriteLine($"Room created with ID: {roomId}");
            };

            OnPeerJoined += HandlePeerJoined;
            OnWebRTCAnswerReceived += HandleRTCAnswerReceived;
            OnWebRTCIceCandidateReceived += HandleRTCIceCandidateReceived;
        }

        private async Task HandlePeerJoined(string peerId)
        {
            var pc = new RTCPeerConnection(rtcConfig);

            var peer = new PeerSession
            {
                PeerId = peerId,
                PC = pc
            };

            peers.TryAdd(peerId, peer);

            pc.onicecandidate += async ice =>
            {
                if (ice == null)
                    return;

                if (!IsSignalingConnected)
                {
                    peer.PendingIceToSend.Add(ice.candidate);
                    return;
                }

                await SendSignalingAsync(JsonConvert.SerializeObject(new
                {
                    type = "webrtc_ice_candidate",
                    peer_id = peerId,
                    candidate = ice.candidate
                }));
            };

            peer.DataChannel = await pc.createDataChannel("data");

            peer.DataChannel.onopen += () =>
                Console.WriteLine($"Data channel with peer {peerId} is open.");

            peer.DataChannel.onmessage += (dc, protocol, data) =>
            {
                var message = data != null
                    ? Encoding.UTF8.GetString(data)
                    : string.Empty;

                OnMessageReceived?.Invoke(peerId, message);
            };

            var offer = pc.createOffer(null);
            await pc.setLocalDescription(offer);

            await SendSignalingAsync(JsonConvert.SerializeObject(new
            {
                type = "webrtc_offer",
                peer_id = peerId,
                sdp = offer.sdp
            }));
        }

        private async Task HandleRTCAnswerReceived(string peerId, string sdp)
        {
            if (!peers.TryGetValue(peerId, out var peer))
                return;

            var pc = peer.PC;

            pc.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = sdp
            });

            foreach (var ice in peer.PendingIce)
                pc.addIceCandidate(ice);

            peer.PendingIce.Clear();

            foreach (var candidate in peer.PendingIceToSend)
            {
                await SendSignalingAsync(JsonConvert.SerializeObject(new
                {
                    type = "webrtc_ice_candidate",
                    peer_id = peerId,
                    candidate = candidate
                }));
            }

            peer.PendingIceToSend.Clear();

            return;
        }

        private Task HandleRTCIceCandidateReceived(string peerId, string candidate)
        {
            if (!peers.TryGetValue(peerId, out var peer))
                return Task.CompletedTask;

            var pc = peer.PC;

            var ice = new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = "data",
                sdpMLineIndex = 0
            };

            if (pc.RemoteDescription == null)
                peer.PendingIce.Add(ice);
            else
                pc.addIceCandidate(ice);

            return Task.CompletedTask;
        }

        public Task SendMessageToPeer(string peerId, string message)
        {
            if (!peers.TryGetValue(peerId, out var peer))
                return Task.CompletedTask;

            if (peer.DataChannel?.readyState == RTCDataChannelState.open)
            {
                peer.DataChannel.send(Encoding.UTF8.GetBytes(message));
            }

            return Task.CompletedTask;
        }

        public Task SendMessageToAllPeers(string message)
        {
            foreach (var peerId in peers.Keys)
                _ = SendMessageToPeer(peerId, message);

            return Task.CompletedTask;
        }

        public Task CreateRoom()
        {
            return SendSignalingAsync(JsonConvert.SerializeObject(new
            {
                type = "create_room"
            }));
        }

        public override void HandleSignalingMessage(string msg)
        {
            JObject json = JObject.Parse(msg);
            string type = json["type"]?.ToString();

            switch (type)
            {
                case "room_created":
                    OnRoomCreated?.Invoke(json["room_id"]?.ToString());
                    break;

                case "peer_joined":
                    RaisePeerJoined(json["peer_id"]?.ToString());
                    break;

                case "webrtc_answer":
                    RaiseWebRTCAnswerReceived(
                        json["peer_id"]?.ToString(),
                        json["sdp"]?.ToString());
                    break;

                case "webrtc_ice_candidate":
                    RaiseWebRTCIceCandidateReceived(
                        json["peer_id"]?.ToString(),
                        json["candidate"]?.ToString());
                    break;
            }
        }

        private void RaisePeerJoined(string peerId)
        {
            if (OnPeerJoined == null) return;

            foreach (Func<string, Task> handler in OnPeerJoined.GetInvocationList())
                _ = handler(peerId);
        }

        private void RaiseWebRTCAnswerReceived(string peerId, string sdp)
        {
            if (OnWebRTCAnswerReceived == null) return;

            foreach (Func<string, string, Task> handler in OnWebRTCAnswerReceived.GetInvocationList())
                _ = handler(peerId, sdp);
        }

        private void RaiseWebRTCIceCandidateReceived(string peerId, string candidate)
        {
            if (OnWebRTCIceCandidateReceived == null) return;

            foreach (Func<string, string, Task> handler in OnWebRTCIceCandidateReceived.GetInvocationList())
                _ = handler(peerId, candidate);
        }
    }
}
