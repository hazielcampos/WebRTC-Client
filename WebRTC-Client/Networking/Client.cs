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
        public event Func<string, Task> OnRoomJoined;
        public event Func<string, Task> OnOfferReceived;
        public event Func<string, Task> OnIceCandidateReceived;
        public event Action<string> OnError;
        public event Action<string> OnMessageReceived;

        public PeerSession session;
        private readonly string _roomId;

        public Client(string roomId)
        {
            _roomId = roomId;

            OnRoomJoined += HandleRoomJoined;
            OnOfferReceived += HandleOfferReceived;
            OnIceCandidateReceived += HandleIceCandidateReceived;
        }

        public async Task StartAsync()
        {
            await ConnectAsync();

            await SendSignalingAsync(JsonConvert.SerializeObject(new
            {
                type = "join_room",
                room_id = _roomId
            }));
        }

        private Task HandleRoomJoined(string peerId)
        {
            var pc = new RTCPeerConnection(rtcConfig);

            session = new PeerSession
            {
                PC = pc,
                PeerId = peerId
            };

            pc.onicecandidate += ice =>
            {
                if (ice == null) return;

                _ = SendSignalingAsync(JsonConvert.SerializeObject(new
                {
                    type = "webrtc_ice_candidate",
                    candidate = ice.candidate,
                    peer_id = peerId
                }));
            };

            pc.ondatachannel += channel =>
            {
                session.DataChannel = channel;
                Console.WriteLine($"Data channel '{channel.label}' received");

                channel.onopen += () =>
                    Console.WriteLine("Data channel opened");

                channel.onmessage += (dc, protocol, data) =>
                {
                    var msg = data != null
                        ? Encoding.UTF8.GetString(data)
                        : string.Empty;

                    OnMessageReceived?.Invoke(msg);
                };
            };

            return Task.CompletedTask;
        }

        private async Task HandleOfferReceived(string sdp)
        {
            if (session?.PC == null)
                return;

            var offer = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = sdp
            };

            session.PC.setRemoteDescription(offer);

            foreach (var ice in session.PendingIce)
                session.PC.addIceCandidate(ice);

            session.PendingIce.Clear();

            var answer = session.PC.createAnswer();
            await session.PC.setLocalDescription(answer);

            await SendSignalingAsync(JsonConvert.SerializeObject(new
            {
                type = "webrtc_answer",
                room_id = _roomId,
                sdp = answer.sdp,
                peer_id = 0
            }));
        }

        private Task HandleIceCandidateReceived(string candidate)
        {
            if (session?.PC == null)
                return Task.CompletedTask;

            var ice = new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = "data",
                sdpMLineIndex = 0
            };

            if (session.PC.RemoteDescription == null)
                session.PendingIce.Add(ice);
            else
                session.PC.addIceCandidate(ice);

            return Task.CompletedTask;
        }

        public override void HandleSignalingMessage(string msg)
        {
            JObject json = JObject.Parse(msg);
            string type = json["type"]?.ToString();

            switch (type)
            {
                case "joined_room":
                    RaiseJoinedRoom(json["peer_id"]?.ToString());
                    break;

                case "webrtc_offer":
                    RaiseRTCOffer(json["sdp"]?.ToString());
                    break;

                case "webrtc_ice_candidate":
                    RaiseIceCandidate(json["candidate"]?.ToString());
                    break;

                case "error":
                    var error = json["message"]?.ToString();
                    Console.WriteLine(error);
                    OnError?.Invoke(error);
                    break;
            }
        }

        private void RaiseJoinedRoom(string peerId)
        {
            if (OnRoomJoined == null) return;

            foreach (Func<string, Task> handler in OnRoomJoined.GetInvocationList())
                _ = handler(peerId);
        }

        private void RaiseRTCOffer(string sdp)
        {
            if (OnOfferReceived == null) return;

            foreach (Func<string, Task> handler in OnOfferReceived.GetInvocationList())
                _ = handler(sdp);
        }

        private void RaiseIceCandidate(string candidate)
        {
            if (OnIceCandidateReceived == null) return;

            foreach (Func<string, Task> handler in OnIceCandidateReceived.GetInvocationList())
                _ = handler(candidate);
        }
    }
}
