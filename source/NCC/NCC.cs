using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NCC
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

    class NCC
    {
        private IPAddress NCCAddress;
        private int NCCPort;
        private IPAddress NCC2Address;
        private int NCC2Port;
        private Socket NCC2Socket;
        private Socket CCRCSocket;
        private IPAddress ControllersAddress;
        private int ControllersPort;
        private Socket NCCSocket;
        private string name;
        private String sourcehost;
        IPAddress sourcadd, h3add;
        private string desthost;
        private int capacity;
        private string desthostLog;
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private ConcurrentDictionary<string, IPAddress> nameToIp;
        private ConcurrentDictionary<IPAddress, string> ipToName;
        private ConcurrentDictionary<string, Socket> NameToSocket;
        private ConcurrentDictionary<Socket, string> SocketToName;

        public NCC(string configFile)
        {
            nameToIp = new ConcurrentDictionary<string, IPAddress>();
            ipToName = new ConcurrentDictionary<IPAddress, string>();
            NameToSocket = new ConcurrentDictionary<string, Socket>();
            SocketToName = new ConcurrentDictionary<Socket, string>();
            List<string> lines;
            lines = File.ReadAllLines(configFile).ToList();
            SetValues(lines);
            readIP(lines, nameToIp, ipToName);
            StartNCC();
        }

        public void StartNCC()
        {
            // Create a TCP/IP socket.  
            NCCSocket = new Socket(NCCAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            NCC2Socket = new Socket(NCC2Address.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            CCRCSocket = new Socket(ControllersAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            CCRCSocket.Connect(new IPEndPoint(ControllersAddress, ControllersPort));

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                NCCSocket.Bind(new IPEndPoint(NCCAddress, NCCPort));
                NCCSocket.Listen(100);
                NCC2Socket.Connect(new IPEndPoint(NCC2Address, NCC2Port));

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();
                    NCCSocket.BeginAccept(new AsyncCallback(AcceptCallback), NCCSocket);

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
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return;
            }
            state.sb.Clear();
            state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

            if (state.sb.ToString().Contains("hello"))
            {
                var split = state.sb.ToString().Split(' ');
                string host_name = split[0];
                while (true)
                {
                    if (NameToSocket.TryAdd(host_name, handler))
                    {
                        Console.WriteLine($"Adding{host_name}");
                        break;
                    }
                    Thread.Sleep(100);
                }
                while (true)
                {
                    if (SocketToName.TryAdd(handler, host_name))
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }
            }
            else if (state.sb.ToString().Contains("Connect")) //Dostaje CallRequest, które zawiera Connect - "połącz mnie z kimś tam"
            {
                CallRequest Call_req = CallRequest.convertToRequest(state.buffer);
                ipToName.TryGetValue(Call_req.source, out sourcehost);

                if (ipToName.TryGetValue(Call_req.destination, out desthost)) //jesli mam host docelowy w słowniku
                {
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Received CallRequest_req(source: {4}, destination: {5}, capacity: {6})",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost,
                            desthost, Call_req.capacity);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_req({4})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_rsp({4})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, Call_req.source);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_req({4})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, desthost);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_rsp({4})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, Call_req.destination);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] PolicyRequest_req({4}, {5}, {6}, {7}, {8})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost, desthost, Call_req.capacity, Call_req.source, Call_req.destination);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] PolicyRequest_rsp(\"Accepted\")", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond);


                    capacity = Call_req.capacity;
                    Socket sock;
                    NameToSocket.TryGetValue(desthost, out sock);//wyciaga handler hosta do, które chcemy wysłać wiadomość
                    Send(sock, Encoding.ASCII.GetBytes("CallAccept" + "#" + sourcehost + "#"+capacity+"##"));
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending CallAccept_req(source: {4} destination: {5} capacity: {6}) to {7}",
                               DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost, desthost, capacity, desthost);
                    desthostLog =desthost;
                }
                else // jesli nie mam hosta docelowego w słowniku
                {
                    h3add = Call_req.destination;
                    sourcadd = Call_req.source;
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Received CallRequest_req(source: {4}, destination: H3, capacity: {5})",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost,  Call_req.capacity); //Wysyłam zapytanie do drugiej domeny czy host chce przyjac polaczenie

                    Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_req({4})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_rsp({4})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, Call_req.source);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_req(H3)", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_rsp({4})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, Call_req.destination);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] PolicyRequest_req({4}, H3, {5}, {6}, {7})", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost, Call_req.capacity, Call_req.source, Call_req.destination);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] PolicyRequest_rsp(\"Accepted\")", DateTime.Now.Hour, DateTime.Now.Minute,
                        DateTime.Now.Second, DateTime.Now.Millisecond);

                    capacity = Call_req.capacity;
                    ipToName.TryGetValue(Call_req.source, out sourcehost);
                    NCC2Socket.Send(Encoding.ASCII.GetBytes("CallCoordination" + "#" + sourcehost + "#" + Call_req.destination.ToString() + "#" + capacity));
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending CallCoordination_req( source: {4}, destination: H3, capacity: {5}) to NCC2",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost, capacity);
                    desthostLog = "H3";
                }

            }

            if (state.sb.ToString().Contains("CallCoordination"))
            {
                var message = Encoding.ASCII.GetString(state.buffer).Split('#');
                ipToName.TryGetValue(IPAddress.Parse(message[2]), out desthost);
                sourcehost = message[1];
                int capacity = Int32.Parse(message[3]);

                Console.WriteLine("[{0}:{1}:{2}.{3}] Received CallCoordination_req( source: {4}, destination: {5}, capacity: {6}) from NCC1",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost, desthost, capacity);

                Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_req(H3)", DateTime.Now.Hour, DateTime.Now.Minute,
                    DateTime.Now.Second, DateTime.Now.Millisecond);

                Console.WriteLine("[{0}:{1}:{2}.{3}] DirectoryRequest_rsp({4})", DateTime.Now.Hour, DateTime.Now.Minute,
                    DateTime.Now.Second, DateTime.Now.Millisecond, message[2]);

                Socket sock;
                NameToSocket.TryGetValue(desthost, out sock); //wyciaga handler hosta do, które chcemy wysłać wiadomość
                Console.WriteLine("[{0}:{1}:{2}.{3}] Sending CallAccept_req(source: {4} destination: {5} capacity: {6}) to {7}",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost, desthost, capacity, desthost);
                Send(sock, Encoding.ASCII.GetBytes("CallAccept" + "#" + message[1].ToString() + "#" + capacity+"##"));


            }

            if (state.sb.ToString().Contains("Accept")) //Accept od hosta
            {
                Console.WriteLine("[{0}:{1}:{2}.{3}] Received CallAccept_rsp(\"Accepted\" source: {4}, destination: {5}) from {6}",
                     DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost, desthost, desthost);
                string hostnam = "";
                IPAddress destadd;
                if (SocketToName.TryGetValue(handler, out hostnam) && nameToIp.TryGetValue(sourcehost, out sourcadd)) //czyli host jest w naszej domenie - mozemy rozpoczac zestawianie
                {                                                                                                     //jesli nasz dest jest w domenie oraz source w domenie
                    
                    nameToIp.TryGetValue(sourcehost, out sourcadd);
                    nameToIp.TryGetValue(desthost, out destadd);
                    CallRequest toCCRC = new CallRequest(sourcadd, destadd, capacity);
               
                    CCRCSocket.Send(toCCRC.convertToByte());
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending ConnectionRequest_req(source:{4}, destination:{5}, capacity:{6}, setup) to CCRC1",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcadd, destadd, capacity);
                }
                else
                {
                    nameToIp.TryGetValue(desthost, out destadd);
                    NCC2Socket.Send(Encoding.ASCII.GetBytes("approved#" + destadd.ToString() + "##"));  //host nie jest z naszej domeny - trzeba wyslac dalej, jest takie słowo bo inaczej sie sypało :)
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending CallCoordination_rsp(\"Accepted\", source: {4}, destination: {5}) to NCC1",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcehost, desthost);
                }
            }

            if (state.sb.ToString().Contains("Discard"))
            {
                NCC2Socket.Send(Encoding.ASCII.GetBytes("Discarded"));
                Console.WriteLine("[{0}:{1}:{2}.{3}] Got message discard",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
            }

            if (state.sb.ToString().Contains("approved")) //Zaczynam zestawiac polaczenie
            {
                Console.WriteLine("[{0}:{1}:{2}.{3}] Received CallCoordination_rsp(\"Accepted\", source: {4}, destination: H3) from NCC2",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,sourcehost);
                var message = Encoding.ASCII.GetString(state.buffer).Split('#');

                Console.WriteLine("[{0}:{1}:{2}.{3}] Sending ConnectionRequest_req(source: {4}, destination: {5}, capacity: {6}, setup) to CC.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, sourcadd, h3add ,capacity);
                nameToIp.TryGetValue(sourcehost, out sourcadd);
                CallRequest toCCRC = new CallRequest(sourcadd, IPAddress.Parse(message[1]), capacity);
                CCRCSocket.Send(toCCRC.convertToByte());
            }

            if (state.sb.ToString().Contains("Discarded")) // Mowie hostowi, że nie chcę połączenia
            {
            }

            if (state.sb.ToString().Contains("Response"))// PRZYCHODZI OD CCRC
            {
                try
                {
                    ConnectionResponse Connection_resp = ConnectionResponse.convertToResp(state.buffer);
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Received ConnectionRequest_rsp(destination: {4}, firstSlot: {5}, lastSlot{6}) from CCRC1", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, 
                        DateTime.Now.Millisecond, desthostLog, Connection_resp.firstFSU, Connection_resp.secondFSU);
                    Socket sock;
                    Connection_resp.sender = name;
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending CallRequest_rsp(\"Accepted\" destination: {7}, " +
                        "firstSlot: {5}, lastSlot: {6}) to {4} ", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                            DateTime.Now.Millisecond, sourcehost, Connection_resp.firstFSU, Connection_resp.secondFSU, desthostLog);
                    NameToSocket.TryGetValue(sourcehost, out sock);//wyciaga handler dla hosta, ktory chcial zestawic polaczenie
                    Send(sock, Connection_resp.convertToByte());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            if (state.sb.ToString().Contains("RECONNECT"))
            {
                CCRCSocket.Send(state.buffer);
            }

            if(state.sb.ToString().Contains("HOSTCLOSE"))
            {
                
                var split = state.sb.ToString().Split("#");
                string sourceNodeName = split[1];
                string destNodeName = split[2];
                int firstFSU = Int32.Parse(split[3]);
                int secondFSU = Int32.Parse(split[4]);
                Console.WriteLine("[{0}:{1}:{2}.{3}] Received message to close connection between {4} and {5} ", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                    DateTime.Now.Millisecond, sourceNodeName, destNodeName);

                if (ipToName.Values.Contains(destNodeName))
                {
                    string message = "NCCCLOSECONNECTION#" + firstFSU + "#" + secondFSU + "#";
                    CCRCSocket.Send(Encoding.ASCII.GetBytes(message));
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending message to CCRC to close connection", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond, sourceNodeName, destNodeName);
                }
                else
                {
                    string message = "NCCCLOSECONNECTION#" + firstFSU + "#" + secondFSU + "#";
                    CCRCSocket.Send(Encoding.ASCII.GetBytes(message));
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending message to CCRC to close connection", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond, sourceNodeName, destNodeName);


                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending message to close connection to other domain.",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
                    string message2 = "HOSTCLOSE#" + sourceNodeName + "#" + destNodeName + "#" + firstFSU + "#" + secondFSU + "#";
                    NCC2Socket.Send(Encoding.ASCII.GetBytes(message2));
                }
            }
            state.sb.Clear();
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
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


        public void SetValues(List<string> lines)
        {
            this.name = GetValueFromConfig("NAME", lines);
            Console.Title = name;
            Console.SetWindowSize(60, 13);
            this.NCCAddress = IPAddress.Parse(GetValueFromConfig("NCC_ADDRESS", lines));
            this.NCCPort = Convert.ToInt32(GetValueFromConfig("NCC_PORT", lines));
            this.ControllersAddress = IPAddress.Parse(GetValueFromConfig("CONTROL_ADDRESS", lines));
            this.ControllersPort = Convert.ToInt32(GetValueFromConfig("CONTROL_PORT", lines));
            this.NCC2Address = IPAddress.Parse(GetValueFromConfig("NCC2_ADDRESS", lines));
            this.NCC2Port = Convert.ToInt32(GetValueFromConfig("NCC2_PORT", lines));
        }
        public string GetValueFromConfig(string name, List<string> lines)
        {
            string[] entries;
            entries = lines.Find(line => line.StartsWith(name)).Split(' ');
            return entries[1];
        }

        private void readIP(List<string> lines, ConcurrentDictionary<string, IPAddress> nameToIp, ConcurrentDictionary<IPAddress, string> ipToName)
        {
            foreach (var line in lines.FindAll(line => line.StartsWith("HOST")))
            {
                string[] entries;
                entries = line.Split(' ');
                nameToIp.TryAdd(entries[1], IPAddress.Parse(entries[2]));
                Console.WriteLine(entries[1]);
                ipToName.TryAdd(IPAddress.Parse(entries[2]), entries[1]);
            }
        }

    }
}
