using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebRTC_Client.Networking
{
    public class PeerSession
    {
        public string PeerId;
        public RTCPeerConnection PC;
        public RTCDataChannel DataChannel;
        public List<RTCIceCandidateInit> PendingIce = new List<RTCIceCandidateInit>();
    }
    public abstract class NetworkObject
    {
        public static string SignalingServerURL = "ws://151.240.19.55:8000/ws";
        protected WebSocketClient signaling;
        protected static RTCConfiguration rtcConfig = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>
            {
                new RTCIceServer
                {
                    urls = "turn:151.240.19.55:3478?transport=udp",
                    username = "webrtc",
                    credential = "worldbuildercoop"
                },
                new RTCIceServer
                {
                    urls = "turn:151.240.19.55:3478?transport=tcp",
                    username = "webrtc",
                    credential = "worldbuildercoop"
                },

            }
        };
        public NetworkObject()
        {
            signaling = new WebSocketClient();

            signaling.OnMessage += HandleSignalingMessage;
            signaling.OnError += HandleSignalingError;
            signaling.OnDisconnected += HandleSignalingDisconnected;
            signaling.OnConnected += HandleSignalingConnected;
        }
        public async Task ConnectAsync()
        {
            await signaling.ConnectAsync(SignalingServerURL);
        }
        public virtual void HandleSignalingMessage(string msg)
        {
            // Handle incoming signaling messages (SDP, ICE candidates, etc.)
        }
        public virtual void HandleSignalingError(Exception ex)
        {
            // Handle signaling errors
        }
        public virtual void HandleSignalingDisconnected()
        {

        }
        public virtual void HandleSignalingConnected()
        {

        }
        public virtual async Task DisconnectAsync()
        {
            await signaling.DisconnectedAsync();
        }
        public Task SendSignalingAsync(string msg)
        {
            if (!signaling.IsConnected)
                throw new InvalidOperationException("Signaling WebSocket is not connected.");

            return signaling.SendAsync(msg);
        }
    }
}