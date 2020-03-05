
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CCRC3
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

    public class CCRC3
    {
        String name;
        IPAddress ControllersAddress;
        int ControllersPort;
        Socket CCRCSocket;
        Socket CCRC1Socket;
        IPAddress NCCAddress;
        int NCCPort;
        private ConcurrentDictionary<string, IPAddress> nameToIp;
        private ConcurrentDictionary<IPAddress, string> ipToName;
        private ConcurrentDictionary<string, Socket> NodeToSocket;
        private ConcurrentDictionary<Socket, string> SocketToNode;
        private ConcurrentDictionary<string, IPEndPoint> nameToEP;

        private Dictionary<int, int> nodeMaping;
        private Dictionary<int, SortedSet<int>> connectionToSlots;
        private Dictionary<int, string> NumberToNodeName;

        private List<Connections> connections;
        private List<string> traska_node;
        private List<List<int>> wierzcholkidroga;
        private List<int> traska;
        private List<LRMRow> LRM;

        private Graf topology;
        
        public static int MAXSLOTS = 20;
        int connectionID = 0;
        private int counter = 0;
        private string wejscie1;
        private int s1, s2;

        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public CCRC3(string configFile)
        {
            connectionToSlots = new Dictionary<int, SortedSet<int>>();
            NumberToNodeName = new Dictionary<int, string>();

            nameToIp = new ConcurrentDictionary<string, IPAddress>();
            ipToName = new ConcurrentDictionary<IPAddress, string>();
            nameToEP = new ConcurrentDictionary<string, IPEndPoint>();
            NodeToSocket = new ConcurrentDictionary<string, Socket>();
            SocketToNode = new ConcurrentDictionary<Socket, string>();

            traska_node = new List<string>();
            connections = new List<Connections>();
            wierzcholkidroga = new List<List<int>>();

            NumberToNodeName.Add(1, "N6");
            NumberToNodeName.Add(2, "N7");
            NumberToNodeName.Add(3, "H3");
            NumberToNodeName.Add(4, "N4");
            NumberToNodeName.Add(5, "N5");

            LRM = new List<LRMRow>();

            ReadConfig(configFile);
            topology = new Graf("graf3.txt");
            int i = 0;
            foreach (Krawedz krawedz in topology.krawedzie)
            {
                LRM.Add(new LRMRow(krawedz.PodajPoczatek(), krawedz.PodajKoniec(), MAXSLOTS));
                krawedz.szczeliny = LRM[i++].frequencySlots;
            }
            StartCCRC();
        }

        public void ReadConfig(string configFile)
        {
            List<string> lines;
            lines = File.ReadAllLines(configFile).ToList();
            SetValues(lines);
            readIP(lines, nameToIp, ipToName);
            int port;
            IPAddress ip;
            foreach (var line in lines.FindAll(line => line.StartsWith("CCRC")))
            {
                string[] entries;
                entries = line.Split(' ');
                ip = IPAddress.Parse(entries[2]);
                port = Convert.ToInt32(entries[3]);
                IPEndPoint ep = new IPEndPoint(ip, port);
                nameToEP.TryAdd(entries[1], ep);
                nameToIp.TryAdd(entries[1], ip);
            }
            readNodes(lines);
        }

        private void readNodes(List<string> lines)
        {
            foreach (var line in lines.FindAll(line => line.StartsWith("CONNECTIONS")))
            {
                string[] entries;
                entries = line.Split(' ');
                string firstnode = entries[1];
                int firstport = Int32.Parse(entries[2]);
                string secondnode = entries[3];
                int secondport = Int32.Parse(entries[4]);
                connections.Add(new Connections(firstnode, secondnode, firstport, secondport));
                connections.Add(new Connections(secondnode, firstnode, secondport, firstport));
            }
        }

        private int getFirstPort(string firstnode, string secondnode)
        {
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].firstNode.Equals(firstnode))
                {
                    if (connections[i].secondNode.Equals(secondnode))
                    {
                        return connections[i].firstPort;
                    }
                }
            }
            return -1;
        }

        private int getSecondPort(string firstnode, string secondnode)
        {
            for (int i = 0; i < connections.Count; i++)
            {
                if (connections[i].firstNode.Equals(firstnode))
                {
                    if (connections[i].secondNode.Equals(secondnode))
                    {
                        return connections[i].secondPort;
                    }
                }
            }
            return -1;
        }

        public void StartCCRC()
        {
            // Create a TCP/IP socket.  
            CCRCSocket = new Socket(NCCAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                ControllersAddress = IPAddress.Parse("127.0.0.1");

                CCRCSocket.Bind(new IPEndPoint(ControllersAddress, ControllersPort));
                CCRCSocket.Listen(100);

                IPEndPoint CCRC1EP;
                IPAddress CCRC1IP;
                nameToEP.TryGetValue("CCRC1", out CCRC1EP);
                nameToIp.TryGetValue("CCRC1", out CCRC1IP);

                CCRC1Socket = new Socket(CCRC1IP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                CCRC1Socket.Connect(CCRC1EP);

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();
                    CCRCSocket.BeginAccept(new AsyncCallback(AcceptCallback), CCRCSocket);
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
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        public void ReadCallback(IAsyncResult ar)
        {
            //Console.WriteLine("dostalem cos");
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
            state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
            if (state.sb.ToString().Contains("hello"))
            {
                Send(handler, Encoding.ASCII.GetBytes("Connection with CCRC established"));
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

            if (state.sb.ToString().Contains("Adding"))
            {
                var parts = state.sb.ToString().Split('#');
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received ConnectionRequest_rsp(in_port: {4} first_slot: {5}, last_slot: {6}, out_port: {7}, setup) from {8} ", DateTime.Now.Hour, DateTime.Now.Minute,
                    DateTime.Now.Second, DateTime.Now.Millisecond, parts[1], parts[2], parts[3], parts[4], parts[5]);
                counter--;
                if (counter == 0)
                {
                    CCRC1Socket.Send(Encoding.ASCII.GetBytes("ZESTAWIONO"));
                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending ConnectionRequest_rsp(H3, FirstDomainExit: {4}, FirstSlot: {5}, SecondSlot: {6})",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, wejscie1, s1, s2);
                }
            }

            if (state.sb.ToString().Contains("FSU"))
            {
                counter = 0;
                int liczba_szczelin_podsiec = BitConverter.ToInt32(state.buffer, 3);
                int firstSlot = BitConverter.ToInt32(state.buffer, 7);
                int wejscie = BitConverter.ToInt32(state.buffer, 11);
                int secondSlot = firstSlot + liczba_szczelin_podsiec - 1;
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received ConnectionRequest_req(H3, FirstDomainExit: {4}, FirstSlot: {5}, LastSlot: {6}, setup).",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, NumberToNodeName[wejscie], firstSlot, secondSlot);

                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending RouteTableQuery_req(FirstDomainExit: {4}, H3, FirstSlot: {5}, LastSlot: {6})",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, NumberToNodeName[wejscie], firstSlot, secondSlot);
                Console.WriteLine("[{0}:{1}:{2}.{3}] [RC] Received RouteTableQuery_req(FirstDomainExit: {4}, H3, FirstSlot: {5}, LastSlot: {6})",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, NumberToNodeName[wejscie], firstSlot, secondSlot);

                SortedSet<int> fsu = new SortedSet<int>();
                List<int> nodeIndex = new List<int>();
                nodeIndex.Add(3);
                nodeIndex.Add(2);
                nodeIndex.Add(1);
                nodeIndex.Add(wejscie);
                
                wierzcholkidroga.Add(new List<int>(nodeIndex));
           
                traska = new List<int>(nodeIndex);
                
                traska.Reverse();
                actualizationLRM(liczba_szczelin_podsiec, firstSlot);

                traska_node = new List<string>();
               
                foreach (var element in traska)
                {
                    traska_node.Add(NumberToNodeName[element]);
                }

                for(int i=0; i<traska_node.Count -1;i++)
                {
                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending SNPLinkConnectionRequest_req({4}, {5},  first_slot: {6}, last_slot: {7}, setup)",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            traska_node[i], traska_node[i + 1], firstSlot, secondSlot);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Received SNPLinkConnectionRequest_req({4}, {5},  first_slot: {6}, last_slot: {7}, setup)",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            traska_node[i], traska_node[i + 1], firstSlot, secondSlot);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Sending SNPLinkConnectionRequest_rsp(\"Allocated\", {4}, {5},  first_slot: {6}, last_slot: {7})",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            traska_node[i], traska_node[i + 1], firstSlot, secondSlot);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received SNPLinkConnectionRequest_rsp(\"Allocated\", {4}, {5},  first_slot: {6}, last_slot: {7})",
                         DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            traska_node[i], traska_node[i + 1], firstSlot, secondSlot);
                }
                for (int i = 0; i < traska_node.Count - 2; i++)
                {
                    int firstport = getFirstPort(traska_node[i], traska_node[i + 1]);
                    int secondport = getSecondPort(traska_node[i], traska_node[i + 1]);

                    int in_port = secondport;

                    int out_port = getFirstPort(traska_node[i + 1], traska_node[i + 2]);
                    Socket sock;

                    if (traska_node[i + 1].Equals("H2") || traska_node[i + 1].Equals("N1") || traska_node[i + 1].Equals("N2"))
                    {
                        continue;
                    }
                    else
                    {
                        string message = "ADD_ROW" + "#" + in_port.ToString() + "#" + firstSlot.ToString() + '#' + secondSlot.ToString() + '#' + out_port.ToString();
                        if (NodeToSocket.TryGetValue(traska_node[i + 1], out sock))
                        {
                            Send(sock, Encoding.ASCII.GetBytes(message));
                            Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending ConnectionRequest_req(in_port: {4} first_slot: {5}, last_slot: {6}, out_port: {7}, setup) to {8} ",
                                DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                                    in_port.ToString(), firstSlot.ToString(), secondSlot.ToString(), out_port.ToString(), traska_node[i + 1]);
                            counter++;
                        }
                    }
                }
                wejscie1 = NumberToNodeName[wejscie];
                s1 = firstSlot;
                s2 = secondSlot;
                traska_node.Clear();
                traska.Clear();
            }

            if(state.sb.ToString().Contains("NCCCLOSECONNECTION#"))
            {
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received message from NCC to close connections with given slots", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                    DateTime.Now.Millisecond);

                var split = state.sb.ToString().Split("#");
                int firstFSU = Int32.Parse(split[1]);
                int secondFSU = Int32.Parse(split[2]);

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Updating resources...", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                    DateTime.Now.Millisecond);

                foreach (var node in NodeToSocket.Keys)
                {
                    if (node.StartsWith('N'))
                    {
                        Socket sock = NodeToSocket[node];
                        string messageToNodes = "DELETE_FSU#" + firstFSU + '#' + secondFSU + '#';
                        Send(sock, Encoding.ASCII.GetBytes(messageToNodes));
                        Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending message to node to update it's routing table.", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                            DateTime.Now.Millisecond);
                    }
                }
                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Resources updated.", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                    DateTime.Now.Millisecond);
            }

            if (state.sb.ToString().Contains("DELETE_SLOTS"))
            {
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received message to close connection with given slots", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                    DateTime.Now.Millisecond);
                var split = state.sb.ToString().Split("#");
                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Freeing up resources that used connection with H3 with given slots", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                    DateTime.Now.Millisecond);
                int firstFsu = Int32.Parse(split[1]);
                int secondFsu = Int32.Parse(split[2]);
                
                foreach (var node in NodeToSocket.Keys)
                {
                    if (node.StartsWith('N'))
                    {
                        Socket sock = NodeToSocket[node];
                        string messageToNodes = "DELETE_FSU#" + firstFsu + '#' + secondFsu + '#';
                        Send(sock, Encoding.ASCII.GetBytes(messageToNodes));
                        Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending message to node to update it's routing table.", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                            DateTime.Now.Millisecond);
                    }
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
            Console.SetWindowSize(50, 7);
            this.ControllersAddress = IPAddress.Parse(GetValueFromConfig("CONTROL_ADDRESS", lines));
            this.ControllersPort = Convert.ToInt32(GetValueFromConfig("CONTROL_PORT", lines));
            this.NCCAddress = IPAddress.Parse(GetValueFromConfig("NCC_ADDRESS", lines));
            this.NCCPort = Convert.ToInt32(GetValueFromConfig("NCC_PORT", lines));
        }

        public string GetValueFromConfig(string name, List<string> lines)
        {
            string[] entries;
            entries = lines.Find(line => line.StartsWith(name)).Split(' ');
            return entries[1];
        }

        private SortedSet<int> wypelnijSet()
        {
            SortedSet<int> ss = new SortedSet<int>();
            for (int i = 0; i < MAXSLOTS; i++)
                ss.Add(i);
            return ss;
        }

        private void readIP(List<string> lines, ConcurrentDictionary<string, IPAddress> nameToIp, ConcurrentDictionary<IPAddress, string> ipToName)
        {
            foreach (var line in lines.FindAll(line => line.StartsWith("ROW")))
            {
                string[] entries;
                entries = line.Split(' ');
                nameToIp.TryAdd(entries[1], IPAddress.Parse(entries[2]));
                ipToName.TryAdd(IPAddress.Parse(entries[2]), entries[1]);
            }
        }

        private void actualizationLRM(int liczba, int pierwszyIndeks)
        {
            int y = -5;
            foreach (int x in wierzcholkidroga[connectionID])
            {
                if (y != -5)
                {
                    foreach (LRMRow row in LRM)
                    {
                        if ((row.routerID1 == x && y == row.routerID2) || (row.routerID2 == x && y == row.routerID1))
                        {

                            if ((x == 5 && y == 3 || x == 3 && y == 5) && name.Equals("CCRC1"))
                            { }
                            else
                            {
                                int iterator = 0;
                                bool licz = false;
                                List<int> dousuniecia = new List<int>();
                                foreach (int i in row.frequencySlots)
                                {
                                    if (i == pierwszyIndeks)
                                        licz = true;
                                    if (licz == true)
                                    {
                                        if (iterator < liczba)
                                            dousuniecia.Add(i);
                                        iterator++;
                                    }
                                }
                                if (dousuniecia.Count > 0)
                                    connectionToSlots[connectionID] = new SortedSet<int>(dousuniecia);
                                for (int i = 0; i < dousuniecia.Count; i++)
                                    row.frequencySlots.Remove(dousuniecia[i]);
                            }
                        }
                    }
                }
                y = x;
            }
        }


    }
}