using OpenQA.Selenium.DevTools.V117.Page;
using OpenQA.Selenium.DevTools.V117.Schema;
using OpenQA.Selenium.Internal;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Screenshoter
{

    class Program
    {
        public static string IP = "127.0.0.1";
        public static int PORT = 9018;
        public static string OUTPUT_DIR = Directory.CreateDirectory("output").FullName;
        public static int TIMEOUT_MS = 15000;

        public delegate Task OnLastQueueEmpty();
        public static OnLastQueueEmpty finishedExecution = SendLastMsg;

        private static TcpListener? _listener;

        private static bool finished = false;
        private static bool lastMessageSent = false;
        public static bool IsFinished() => finished;


        private static TcpClient? finishClient = null;

        public static async Task SendDomainToManager(object? domainEnvelope)
        {
            if (domainEnvelope == null) return;
            try
            {
                DomainEnvelope envelope = (DomainEnvelope)domainEnvelope;
                await BrowserManager.AddToQueue(envelope);
            }
            catch(Exception)
            {
                return;
            }
            
        }

        private static async Task RunListener()
        {
            _listener = new TcpListener(IPAddress.Parse(IP), PORT);
            _listener.Start();
            Console.WriteLine("Started socket");
            try
            {
                _listener.Start();
                
                while (!lastMessageSent)
                {
                    using (TcpClient client = await _listener.AcceptTcpClientAsync())
                    {
                        await Task.Run(async () =>
                            {
                                if (client != null)
                                {
                                    await HandleNewClient(client);
                                }
                            }
                        );
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                _listener.Stop();
            }

            Console.WriteLine("\nFinished execution...\nThank you for using Screenshoter");
            return;
        }

        private static async Task HandleNewClient(TcpClient client)
        {
            byte[] bytes = new byte[256];
            string? domain = null;

            NetworkStream stream = client.GetStream();

            int i;

            // Loop to receive all the data sent by the client.
            try
            {
                if ((i = await stream.ReadAsync(bytes, 0, bytes.Length)) != 0)
                {
                    Console.WriteLine("Received data...");
                    // Translate data bytes to a ASCII string.
                    domain = System.Text.Encoding.ASCII.GetString(bytes, 0, i);

                    Console.WriteLine("Received Message {0}", domain);

                    // Process the data sent by the client.
                    domain = domain.ToLower();
                    if (domain != "finished")
                    {
                        await SendDomainToManager(new DomainEnvelope(domain, client));
                    }
                    else
                    {
                        Console.WriteLine("Received END MESSAGE from application");
                        finished = true;
                        finishClient = client;
                        await BrowserManager.anyQueueChanged.Invoke();
                    }
                }
            }
            catch (IOException)
            {
                // Ignore
            }
        }

        private static async Task SendLastMsg()
        {
            if(finishClient != null)
            {
                NetworkStream stream = finishClient.GetStream();
                string outMsg = $"Finished";
                byte[] text = System.Text.Encoding.UTF8.GetBytes(outMsg);
                await stream.WriteAsync(text);
                lastMessageSent = true;
            }
        }

        static async Task Main(string[] args)
        {
            IP = (args.Length > 0) ? args[0] : IP;
            PORT = (args.Length > 1) ? int.Parse(args[1]) : PORT;
            int numThreads = (args.Length > 2) ? int.Parse(args[2]) : 4;
            OUTPUT_DIR = (args.Length > 3) ? Directory.CreateDirectory(args[3]).FullName : OUTPUT_DIR;

            if (!BrowserManager.HasStarted())
            {
                BrowserManager.StartBrowserManager(numThreads);
            }

            await RunListener();
            return;
        }
    }
}

