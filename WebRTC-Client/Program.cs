using SIPSorcery.Net;
using System;
using System.Text;
using System.Threading.Tasks;
using WebRTC_Client.Networking;

namespace WebRTC_Client
{
    class Program
    {
        static Host host;
        static Client client;

        static async Task Main(string[] args)
        {
            Console.WriteLine("WebRTC Console App");
            Console.WriteLine("Commands:");
            Console.WriteLine("  host");
            Console.WriteLine("  client <room_id>");
            Console.WriteLine("  msg <text>");
            Console.WriteLine("  exit");

            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var split = input.Split(new[] { ' ' }, 2);
                var cmd = split[0].ToLowerInvariant();

                try
                {
                    switch (cmd)
                    {
                        case "host":
                            await StartHost();
                            break;

                        case "client":
                            if (split.Length < 2)
                            {
                                Console.WriteLine("Usage: client <room_id>");
                                break;
                            }
                            await StartClient(split[1]);
                            break;

                        case "msg":
                            if (split.Length < 2)
                            {
                                Console.WriteLine("Usage: msg <text>");
                                break;
                            }
                            await SendMessage(split[1]);
                            break;

                        case "exit":
                            await Shutdown();
                            return;

                        default:
                            Console.WriteLine("Unknown command");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FATAL] {ex.Message}");
                }
            }
        }

        static async Task StartHost()
        {
            if (host != null || client != null)
            {
                Console.WriteLine("Host or Client already running.");
                return;
            }

            host = new Host();

            host.OnRoomCreated += roomId =>
            {
                Console.WriteLine($"[HOST] Room created: {roomId}");
            };

            host.OnMessageReceived += (peerId, message) =>
            {
                Console.WriteLine($"[FROM {peerId}] {message}");
            };

            Console.WriteLine("[HOST] Connecting...");
            await host.StartAsync();
            await host.CreateRoom();
        }

        static async Task StartClient(string roomId)
        {
            if (client != null || host != null)
            {
                Console.WriteLine("Host or Client already running.");
                return;
            }

            client = new Client(roomId);

            client.OnMessageReceived += msg =>
            {
                Console.WriteLine($"[HOST] {msg}");
            };

            client.OnError += err =>
            {
                Console.WriteLine($"[ERROR] {err}");
            };

            Console.WriteLine("[CLIENT] Connecting...");
            await client.StartAsync();

            Console.WriteLine($"[CLIENT] Joining room {roomId}");
        }

        static async Task SendMessage(string message)
        {
            if (host != null)
            {
                if (host.peers.Count == 0)
                {
                    Console.WriteLine("[HOST] No peers connected.");
                    return;
                }

                await host.SendMessageToAllPeers(message);
                Console.WriteLine("[HOST -> ALL] " + message);
                return;
            }

            if (client?.session?.DataChannel != null &&
                client.session.DataChannel.readyState == RTCDataChannelState.open)
            {
                client.session.DataChannel.send(
                    Encoding.UTF8.GetBytes(message)
                );
                Console.WriteLine("[CLIENT -> HOST] " + message);
                return;
            }

            Console.WriteLine("No active connection or DataChannel not open.");
        }

        static async Task Shutdown()
        {
            Console.WriteLine("Shutting down...");

            if (host != null)
            {
                await host.DisconnectAsync();
                host = null;
            }

            if (client != null)
            {
                await client.DisconnectAsync();
                client = null;
            }
        }
    }
}
