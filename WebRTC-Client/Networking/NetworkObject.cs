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

        // ICE recibidos antes de tener RemoteDescription
        public List<RTCIceCandidateInit> PendingIce = new List<RTCIceCandidateInit>();

        // ICE generados antes de que signaling esté listo
        public List<string> PendingIceToSend = new List<string>();
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
                }
            }
        };

        protected bool IsSignalingConnected =>
            signaling != null && signaling.IsConnected;

        protected NetworkObject()
        {
            signaling = new WebSocketClient();

            signaling.OnMessage += HandleSignalingMessage;
            signaling.OnError += HandleSignalingError;
            signaling.OnDisconnected += HandleSignalingDisconnected;
            signaling.OnConnected += HandleSignalingConnected;
        }

        public Task ConnectAsync()
        {
            return signaling.ConnectAsync(SignalingServerURL);
        }

        public virtual void HandleSignalingMessage(string msg) { }

        public virtual void HandleSignalingError(Exception ex)
        {
            Console.WriteLine("[SIGNALING ERROR] " + ex);
        }

        public virtual void HandleSignalingDisconnected()
        {
            Console.WriteLine("[SIGNALING] Disconnected");
        }

        public virtual void HandleSignalingConnected()
        {
            Console.WriteLine("[SIGNALING] Connected");
        }

        public Task DisconnectAsync()
        {
            return signaling.DisconnectAsync();
        }

        protected Task SendSignalingAsync(string msg)
        {
            if (!IsSignalingConnected)
            {
                Console.WriteLine("[SIGNALING] Dropped message (not connected)");
                return Task.CompletedTask;
            }

            return signaling.SendAsync(msg);
        }
    }
}