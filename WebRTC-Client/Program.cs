using System;
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
                var cmd = split[0].ToLower();

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
                        return;

                    default:
                        Console.WriteLine("Unknown command");
                        break;
                }
            }
        }

        static Task StartHost()
        {
            if (host != null)
            {
                Console.WriteLine("Host already running.");
                return Task.CompletedTask;
            }

            host = new Host();

            host.OnRoomCreated += (roomId) =>
            {
                Console.WriteLine($"[HOST] Room created: {roomId}");
            };

            host.OnMessageReceived += (peerId, message) =>
            {
                Console.WriteLine($"[FROM {peerId}] {message}");
            };

            // 🔥 NO BLOQUEAR LA CONSOLA
            Task.Run(async () =>
            {
                await host.StartAsync();
                await host.CreateRoom();
            });

            return Task.CompletedTask;
        }


        static async Task StartClient(string roomId)
        {
            if (client != null)
            {
                Console.WriteLine("Client already running.");
                return;
            }

            client = new Client(roomId);

            client.onMessageReceived += (msg) =>
            {
                Console.WriteLine($"[HOST] {msg}");
            };

            client.onError += (err) =>
            {
                Console.WriteLine($"[ERROR] {err}");
            };

            await client.StartAsync();

            Console.WriteLine($"[CLIENT] Joining room {roomId}");
        }

        static async Task SendMessage(string message)
        {
            if (host != null)
            {
                await host.SendMessageToAllPeers(message);
                Console.WriteLine("[HOST -> ALL] " + message);
                return;
            }

            if (client != null)
            {
                // Solo hay un DataChannel
                client.session?.DataChannel?.send(
                    System.Text.Encoding.UTF8.GetBytes(message)
                );
                return;
            }

            Console.WriteLine("No host or client running.");
        }
    }
}
