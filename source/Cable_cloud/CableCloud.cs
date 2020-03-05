using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CableCloud
{
    public class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }
    class CableCloud
    {
        // Thread signal.  
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private IPAddress CloudAddress;
        private int CloudPort;
        private CloudConfig Config { get; set; }
        private Socket serverSocket;
        private ConcurrentDictionary<string, Socket> NodeToSocket;
        private ConcurrentDictionary<Socket, string> SocketToNode;

        public CableCloud(string FilePath)
        {
            Console.Title = "CableCloud";
            Console.SetWindowSize(40, 21);
            StreamReader sr = new StreamReader(FilePath);
            string line = sr.ReadLine();
            //CLOUD_ADDRESS: adres
            var parts = line.Split(' ');
            CloudAddress = IPAddress.Parse(parts[1]);
            //CLOUD_PORT: port
            line = sr.ReadLine();
            parts = line.Split(' ');
            CloudPort = int.Parse(parts[1]);
            sr.ReadLine(); sr.ReadLine(); sr.ReadLine();

            NodeToSocket = new ConcurrentDictionary<string, Socket>();
            SocketToNode = new ConcurrentDictionary<Socket, string>();

            Config = new CloudConfig(sr);
            StartServer();
        }

        public void StartServer()
        {
            // Create a TCP/IP socket.  
            serverSocket = new Socket(CloudAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                serverSocket.Bind(new IPEndPoint(CloudAddress, CloudPort));
                serverSocket.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();
                    serverSocket.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        serverSocket);
                    // Wait until a connection is made before continuing.  
                    allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            allDone.Set();

            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        public void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;
            // Read data from the client socket.
            int bytesRead;

            try
            {
                bytesRead = handler.EndReceive(ar);
            }
            catch (Exception)
            {
                string outString;
                Socket outSocket;

                string nodeName = SocketToNode[handler];
                SocketToNode.TryRemove(handler, out outString);
                NodeToSocket.TryRemove(nodeName, out outSocket);
                Console.WriteLine("{0}:{1}:{2}.{3} Network node {4} was shut down",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, nodeName);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
                return;
            }
            state.sb.Clear();
            state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

            //Check what package contains
            if (state.sb.ToString().Contains("hello"))
            {
                Send(handler, Encoding.ASCII.GetBytes("Connection with cable cloud established"));
                //Hello H3 e.g.
                var split = state.sb.ToString().Split(' ');
                string nodeName = split[0];

                Console.WriteLine(" {0}:{1}:{2}.{3} Received message {4}",
                     DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, state.sb);
                while (true)
                {
                    if (NodeToSocket.TryAdd(nodeName, handler))
                    {
                        Console.WriteLine($"Adding{nodeName}");
                        break;
                    }
                    Thread.Sleep(100);
                }
                while (true)
                {
                    if (SocketToNode.TryAdd(handler, nodeName))
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }


            }
            else if (state.sb.ToString().Contains("keepalive"))
            {
                //do nothing, socket is working
            }
            else
            {
                try
                {  //przychodzi MPLSPackage
                    DataFromPacket(state, handler, ar);
                    //Send(handler, Encoding.ASCII.GetBytes("Package successfully sent"));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            state.sb.Clear();
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
               new AsyncCallback(ReadCallback), state);
        }

        private void DataFromPacket(StateObject state, Socket handler, IAsyncResult ar)
        {
            try
            {
                var package = DataPackage.convertToPackage(state.buffer);
                string node = SocketToNode[handler];
                string ipAddress = Config.getNodeIP(node);
                int port = package.port;

                Console.WriteLine("{0}:{1}:{2}.{3} Received a package from {4}:{5}",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, node, port);
                ConnectionPoint cp = new ConnectionPoint(ipAddress, port);
                ConnectionPoint nextCP = Config.GetNextConnectionPoint(cp);

                string nextIpAddress = nextCP.ipAddress;
                int nextPort = nextCP.Port;

                string nextNode = Config.getNodeName(nextIpAddress);

                package.port = nextPort;
                //tutaj port jest intem, bo tak mam zdefiniowany w klasie ConnectionPoint
                Socket sock;
                if (NodeToSocket.TryGetValue(nextNode, out sock))
                {
                    Send(sock, package.convertToByte());
                    Console.WriteLine("{0}:{1}:{2}.{3} Sending the package to {4}:{5}",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, nextNode, nextPort);
                }
                else
                {
                    Console.WriteLine("{0}:{1}:{2}.{3} Network node {4} is disabled ",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, nextNode);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Send(Socket handler, byte[] byteData)
        {
            // Begin sending the data to the remote device.  
            handler.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;
                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static int Main(String[] args)
        {
            args = Environment.GetCommandLineArgs();
            //var cableCloud = new CableCloud(args[1]);
            var cloud = new CableCloud("cloudconfig.txt");
            Console.ReadLine();
            return 0;
        }
    }
}
