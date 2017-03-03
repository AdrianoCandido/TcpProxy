using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SimpleTcpProxy
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                if (args.Length != 4)
                {
                    Console.WriteLine("Invalid arguments!");
                    return;
                }

                string fromAddr = args[0];
                int fromPort = int.Parse(args[1]);
                string toAddr = args[2];
                int toPort = int.Parse(args[3]);

                CreateListenner(toPort, async (TcpClient server) =>
                {
                    TcpClient client = await ConnectClientAsync(fromAddr, fromPort);

                    if (client != null)
                    {
                        await CreateDuplexChannel(client, server);
                    }
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static void CreateListenner(int port, Action<TcpClient> clientConnected)
        {
            TcpListener listenner = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            listenner.Start();

            TcpClient client = null;
            while ((client = listenner.AcceptTcpClient()) != null)
                clientConnected?.Invoke(client);
        }

        public static async Task<TcpClient> ConnectClientAsync(string ipAddress, int port)
        {
            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync(ipAddress, port);

                return client;
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Client error \n {e.Message} {e.ErrorCode}");
                return null;
            }
        }

        public async static Task CreateDuplexChannel(TcpClient leftClient, TcpClient rigthClient)
        {
            NetworkStream leftClientStream = leftClient.GetStream();
            NetworkStream rigthClientStream = rigthClient.GetStream();

            while (leftClient.Client.Connected && rigthClient.Client.Connected)
            {
                await WriteFromTo(leftClientStream, rigthClientStream, (int bytesRead, byte[] buffer) => PrintFeedback(leftClient.Client.RemoteEndPoint, rigthClient.Client.RemoteEndPoint, buffer, bytesRead));
                await WriteFromTo(rigthClientStream, leftClientStream, (int bytesRead, byte[] buffer) => PrintFeedback(rigthClient.Client.RemoteEndPoint, leftClient.Client.RemoteEndPoint, buffer, bytesRead));

                await Task.Delay(1);
            }
        }

        public static void PrintFeedback(EndPoint fromEndpoint, EndPoint toEndpoint, byte[] buffer, int bytesRead)
        {
            Console.WriteLine($"{DateTime.UtcNow.ToString("ddMMyyyy HH:mm:ss.ffff")} {fromEndpoint} > {toEndpoint} [{BitConverter.ToString(buffer, 0, bytesRead).ToString().Replace("-", "")}]");
        }

        public async static Task WriteFromTo(NetworkStream from, NetworkStream to, Action<int, byte[]> feedback)
        {
            if (from == null || to == null)
            {
                throw new ArgumentNullException();
            }

            try
            {
                if (from.DataAvailable)
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead = await from.ReadAsync(buffer, 0, buffer.Length);
                    await to.WriteAsync(buffer, 0, bytesRead);
                    feedback?.Invoke(bytesRead, buffer);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Error: {e.Message} {e.ErrorCode}");
                from?.Close();
                to?.Close();
            }
        }
    }
}