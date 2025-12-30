using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SIPSorcery.SIP.App;

namespace WebRTC_Client.Networking
{
    public class Host : NetworkObject
    {
        public event Action<string> OnRoomCreated;
        public event Func<string, Task> OnPeerJoined;
        public event Func<string, string, Task> OnWebRTCAnswerReceived;
        public event Func<string, string, Task> OnWebRTCIceCandidateReceived;
        public event Action<string, string> OnMessageReceived;
        public Dictionary<string, PeerSession> peers = new Dictionary<string, PeerSession>();
        private string _roomId;
        public Host()
        {
        }
        public async Task StartAsync()
        {
            await ConnectAsync();

            OnRoomCreated += (roomId) =>
            {
                _roomId = roomId;
                Console.WriteLine($"Room created with ID: {roomId}");
            };
            OnPeerJoined += HandlePeerJoined;
            OnWebRTCAnswerReceived += HandleRTCAnswerReceived;
            OnWebRTCIceCandidateReceived += HandleRTCIceCandidateReceived;
        }
        public async Task HandlePeerJoined(string peerId)
        {
            var pc = new RTCPeerConnection(rtcConfig);
            var peer = new PeerSession
            {
                PeerId = peerId,
                PC = pc
            };

            pc.onicecandidate += (ice) =>
            {
                if(ice != null)
                {
                    SendSignalingAsync(JsonConvert.SerializeObject(new
                    {
                        type = "webrtc_ice_candidate",
                        peer_id = peerId,
                        candidate = ice.candidate
                    }));
                }
            };

            peer.DataChannel = await pc.createDataChannel("data");
            peer.DataChannel.onopen += () =>
                Console.WriteLine($"Data channel with peer {peerId} is open.");

            peer.DataChannel.onmessage += (dc, protocol, data) =>
            {
                string message = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                OnMessageReceived?.Invoke(peerId, message);
            };

            peers[peerId] = peer;

            var offer = pc.createOffer(null);
            await pc.setLocalDescription(offer);

            await SendSignalingAsync(JsonConvert.SerializeObject(new
            {
                type = "webrtc_offer",
                peer_id = peerId,
                sdp = offer.sdp
            }));
        }
        public async Task HandleRTCAnswerReceived(string peerId, string sdp)
        {
            var pc = peers[peerId].PC;
            var description = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = sdp
            };
            pc.setRemoteDescription(description);

            foreach (var ice in peers[peerId].PendingIce)
                pc.addIceCandidate(ice);

            peers[peerId].PendingIce.Clear();
        }
        public Task HandleRTCIceCandidateReceived(string peerId, string candidate)
        {
            var pc = peers[peerId].PC;
            var ice = new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = "data",
                sdpMLineIndex = 0
            };
            if (pc.RemoteDescription == null)
            {
                peers[peerId].PendingIce.Add(ice);
            }
            else
            {
                pc.addIceCandidate(ice);
            }
            return Task.CompletedTask;
        }
        public Task SendMessageToPeer(string peerId, string message)
        {
            if (peers.ContainsKey(peerId))
            {
                var dataChannel = peers[peerId].DataChannel;
                if (dataChannel.readyState == RTCDataChannelState.open)
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    dataChannel.send(data);
                }
            }
            return Task.CompletedTask;
        }
        public Task SendMessageToAllPeers(string message)
        {
            foreach (var peerId in peers.Keys)
            {
                SendMessageToPeer(peerId, message);
            }
            return Task.CompletedTask;
        }

        public async Task CreateRoom()
        {
            await SendSignalingAsync(JsonConvert.SerializeObject(new
            {
                type = "create_room"
            }));
        }
        
        public override void HandleSignalingMessage(string msg)
        {
            JObject json = JObject.Parse(msg);

            string type = json["type"]?.ToString();
            switch(type)
            {
                case "room_created":
                    OnRoomCreated?.Invoke(json["room_id"]?.ToString());
                    break;
                case "peer_joined":
                    _ = RaisePeerJoined(json["peer_id"]?.ToString());
                    break;
                case "webrtc_answer":
                    _ = RaiseWebRTCAnswerReceived(json["peer_id"]?.ToString(), json["sdp"]?.ToString());
                    break;
                case "webrtc_ice_candidate":
                    _ = RaiseWebRTCIceCandidateReceived(json["peer_id"]?.ToString(), json["candidate"]?.ToString());
                    break;
            }
        }
        private async Task RaisePeerJoined(string peerId)
        {
            if (OnPeerJoined != null)
                foreach (Func<string, Task> handler in OnPeerJoined.GetInvocationList())
                    await handler(peerId);
        }
        private async Task RaiseWebRTCAnswerReceived(string peerId, string sdp)
        {
            if (OnWebRTCAnswerReceived != null)
                foreach (Func<string, string, Task> handler in OnWebRTCAnswerReceived.GetInvocationList())
                    await handler(peerId, sdp);
        }
        private async Task RaiseWebRTCIceCandidateReceived(string peerId, string candidate)
        {
            if (OnWebRTCIceCandidateReceived != null)
                foreach (Func<string, string, Task> handler in OnWebRTCIceCandidateReceived.GetInvocationList())
                    await handler(peerId, candidate);
        }
    }
}
