using SIPSorcery.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

namespace WebRTC_Client.Networking
{
    public class Client : NetworkObject
    {
        public event Func<string, Task> onRoomJoined;
        public event Func<string, Task> onOfferReceived;
        public event Func<string, Task> onIceCandidateReceived;
        public event Action<string> onError;
        public event Action<string> onMessageReceived;
        public PeerSession session;
        private string _roomId;
        public Client(string roomId)
        {
            _roomId = roomId;
            onRoomJoined += HandleRoomJoined;
            onOfferReceived += HandleOfferReceived;
            onIceCandidateReceived += HandleIceCandidateReceived;
        }
        public async Task StartAsync()
        {
            await ConnectAsync();

            var json = JsonConvert.SerializeObject(new
            {
                type = "join_room",
                room_id = _roomId
            });
            await SendSignalingAsync(json);
        }
        public async Task HandleRoomJoined(string peerId)
        {
            var pc = new RTCPeerConnection(rtcConfig);
            session = new PeerSession() { 
                PC = pc,
                PeerId = peerId
            };
        }
        public async Task HandleOfferReceived(string sdp)
        {
            var offer = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = sdp
            };
            session.PC.setRemoteDescription(offer);
            foreach (var ice in session.PendingIce)
                session.PC.addIceCandidate(ice);

            session.PendingIce.Clear();

            session.PC.onicecandidate += (ice) =>
            {
                if (ice != null)
                {
                    SendSignalingAsync(JsonConvert.SerializeObject(new
                    {
                        type = "webrtc_ice_candidate",
                        candidate = ice.candidate,
                        peer_id = session.PeerId
                    }));
                }
            };
            session.PC.ondatachannel += (channel) =>
            {
                session.DataChannel = channel;
                Console.WriteLine($"Data channel '{channel.label}' received");
                channel.onopen += () =>
                {
                    Console.WriteLine("Data channel oppened");
                };

                channel.onmessage += (dc, protocol, data) =>
                {
                    string msg = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                    onMessageReceived.Invoke(msg);
                };
            };
            var answer = session.PC.createAnswer();
            await session.PC.setLocalDescription(answer);
            await SendSignalingAsync(JsonConvert.SerializeObject(new
            {
                type = "webrtc_answer",
                room_id = _roomId,
                sdp = answer.sdp
            }));
        }
        public async Task HandleIceCandidateReceived(string candidate)
        {
            var ice = new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = "data",
                sdpMLineIndex = 0
            };
            if (session.PC.RemoteDescription == null)
            {
                session.PendingIce.Add(ice);
            } else
            {
                session.PC.addIceCandidate(ice);
            }
        }
        public override void HandleSignalingMessage(string msg)
        {
            JObject json = JObject.Parse(msg);

            string type = json["type"]?.ToString();

            switch(type)
            {
                case "joined_room":
                    _ = RaiseJoinedRoom(json["peer_id"]?.ToString());
                    break;
                case "webrtc_offer":
                    _ = RaiseRTCOffer(json["sdp"]?.ToString());
                    break;
                case "webrtc_ice_candidate":
                    _ = RaiseIceCandidate(json["candidate"]?.ToString());
                    break;
                case "error":
                    var error = json["message"]?.ToString();
                    Console.WriteLine(error);
                    onError.Invoke(error);
                    break;
            }
        }
        private async Task RaiseJoinedRoom(string peerId)
        {
            if(onRoomJoined != null)
            {
                foreach (Func<string, Task> handler in onRoomJoined.GetInvocationList())
                    await handler(peerId);
            }
        }
        private async Task RaiseRTCOffer(string sdp)
        {
            if (onOfferReceived != null)
            {
                foreach (Func<string, Task> handler in onOfferReceived.GetInvocationList())
                    await handler(sdp);
            }
        }
        private async Task RaiseIceCandidate(string candidate)
        {
            if (onIceCandidateReceived != null)
            {
                foreach (Func<string, Task> handler in onIceCandidateReceived.GetInvocationList())
                    await handler(candidate);
            }
        }

    }
}
