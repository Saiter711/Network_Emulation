
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

namespace CCRC2
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
    public class CCRC2
    {
        String name;
        IPAddress ControllersAddress;
        int ControllersPort;
        Socket CCRCSocket;
        IPAddress NCCAddress;
        int NCCPort;
        private ConcurrentDictionary<string, IPAddress> nameToIp;
        private ConcurrentDictionary<IPAddress, string> ipToName;

        private ConcurrentDictionary<string, Socket> NodeToSocket;
        private ConcurrentDictionary<Socket, string> SocketToNode;
        private ConcurrentDictionary<string, IPEndPoint> nameToEP;
        private Dictionary<int, int> nodeMaping;
        private Dictionary<int, SortedSet<int>> connectionToSlots;
        private Dictionary<int, List<int>> connectionToNodes;
        private List<Connections> connections;

        private Dictionary<int, string> NumberToNodeName;
        private Dictionary<string, int> NodeNameToNumber;
        private List<int> traska;

        private string wyjscie_z_podsieci;

        private string wejscie_do_podsieci;

        private List<string> traska_node;

        private Dictionary<int, List<int>> connectionsToNodes;

        public static int MAXSLOTS = 20;
        bool czyzaladowalo = false;
        bool drugiAlgorytm = false;
        int connectionID = 0;
        private List<List<int>> wierzcholkidroga;
        private List<LRMRow> LRM;
        private Graf topology;
        int value = 0;
        int dest_node = 0;
        int SNPPIN = 0;
        string nodenameIN = string.Empty;
        int SNPPOUT = 0;
        string nodenameOUT = string.Empty;
        int counter = 0;

        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public CCRC2(string configFile)
        {
            connectionToSlots = new Dictionary<int, SortedSet<int>>();

            connectionToNodes = new Dictionary<int, List<int>>();

            wierzcholkidroga = new List<List<int>>();
            nameToIp = new ConcurrentDictionary<string, IPAddress>();
            ipToName = new ConcurrentDictionary<IPAddress, string>();
            nameToEP = new ConcurrentDictionary<string, IPEndPoint>();
            NodeToSocket = new ConcurrentDictionary<string, Socket>();
            SocketToNode = new ConcurrentDictionary<Socket, string>();
            LRM = new List<LRMRow>();

            traska_node = new List<string>();

            connectionsToNodes = new Dictionary<int, List<int>>();

            connections = new List<Connections>();

            NumberToNodeName = new Dictionary<int, string>();
            NodeNameToNumber = new Dictionary<string, int>();

            NumberToNodeName.Add(1, "N3");
            NumberToNodeName.Add(3, "N4");
            NumberToNodeName.Add(2, "N5");
            NumberToNodeName.Add(7, "H2");

            NodeNameToNumber.Add("N3", 1);
            NodeNameToNumber.Add("N4", 3);
            NodeNameToNumber.Add("N5", 2);
            NodeNameToNumber.Add("H2", 7);


            ReadConfig(configFile);
            topology = new Graf("graf4.txt");
            nodeMaping = new Dictionary<int, int>();
            nodeMaping.TryAdd(3, 1);
            nodeMaping.TryAdd(5, 2);
            nodeMaping.TryAdd(7, 2);
            nodeMaping.TryAdd(6, 1);
            nodeMaping.TryAdd(1, 1);
            nodeMaping.TryAdd(4, 3);

            int i = 0;
            foreach (Krawedz krawedz in topology.krawedzie)
            {
                LRM.Add(new LRMRow(krawedz.PodajPoczatek(), krawedz.PodajKoniec(), MAXSLOTS));
                krawedz.szczeliny = LRM[i++].frequencySlots;
            }
            StartCCRC();
        }

        private void slotsRelease(int idpolaczenia)
        {
            List<int> pathNodes = new List<int>();
            pathNodes = connectionToNodes[idpolaczenia];
            pathNodes.Reverse();
            foreach (LRMRow row in LRM)
            {
                for (int i = 0; i < pathNodes.Count - 1; i++)
                {
                    if ((row.routerID1 == pathNodes[i] && row.routerID2 == pathNodes[i + 1]) || (row.routerID1 == pathNodes[i + 1] && row.routerID2 == pathNodes[i]))
                    {
                        foreach (int j in connectionToSlots[idpolaczenia])
                        {
                            row.frequencySlots.Add(j);
                        }
                    }
                }
            }
            connectionToSlots.Remove(idpolaczenia);
            connectionToNodes.Remove(idpolaczenia);
        }

        private void removeEdge(int idwezel1, int idwezel2)
        {
            List<Krawedz> krawedzieDoUsuniecia = new List<Krawedz>();
            foreach (Krawedz krawedz in topology.krawedzie)
            {
                if ((krawedz.PodajPoczatek() == idwezel1 && krawedz.PodajKoniec() == idwezel2) || (krawedz.PodajPoczatek() == idwezel2 && krawedz.PodajKoniec() == idwezel1))
                    krawedzieDoUsuniecia.Add(krawedz);
            }
            List<LRMRow> lrmrowDoUsuniecia = new List<LRMRow>();
            foreach (Krawedz krawedz in krawedzieDoUsuniecia)
            {
                topology.krawedzie.Remove(krawedz);
                foreach (LRMRow row in LRM)
                {
                    if ((krawedz.PodajPoczatek() == row.routerID1 && krawedz.PodajKoniec() == row.routerID2) || (krawedz.PodajPoczatek() == row.routerID2 && krawedz.PodajKoniec() == row.routerID1))
                        lrmrowDoUsuniecia.Add(row);
                }
                for (int i = 0; i < lrmrowDoUsuniecia.Count; i++)
                    LRM.Remove(lrmrowDoUsuniecia[i]);
            }
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
            CCRCSocket = new Socket(NCCAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                ControllersAddress = IPAddress.Parse("127.0.0.1");

                CCRCSocket.Bind(new IPEndPoint(ControllersAddress, ControllersPort));
                CCRCSocket.Listen(100);

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
            state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
            if (state.sb.ToString().Contains("hello"))
            {
                Console.WriteLine("[{0}:{1}:{2}.{3}] Received message {4}",
                     DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, state.sb);

                Send(handler, Encoding.ASCII.GetBytes("Connection with CCRC established"));
                var split = state.sb.ToString().Split(' ');
                string nodeName = split[0];

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
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received ConnectionRequest_rsp(in_port: {4} first_slot: {5}, last_slot: {6}, out_port: {7}, setup) from {8} ", 
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, 
                        parts[1], parts[2], parts[3], parts[4], parts[5]);
                counter--;
                if(counter == 0)
                {
                    Socket CCRC1Socket;
                    IPEndPoint CCRC1EP;
                    IPAddress CCRC1IP;
                    nameToEP.TryGetValue("CCRC1", out CCRC1EP);
                    nameToIp.TryGetValue("CCRC1", out CCRC1IP);
                    CCRC1Socket = new Socket(CCRC1IP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    CCRC1Socket.Connect(CCRC1EP);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending ConnectionRequest_rsp(connection_id: {4})",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, connectionID);

                    List<byte> new_list = new List<byte>();
                    new_list.AddRange(Encoding.ASCII.GetBytes("CONN_REQ_RESP#"));
                    byte[] new_list_byte;
                    new_list_byte = new_list.ToArray();
                    CCRC1Socket.Send(new_list_byte);
                }
            }

            if (state.sb.ToString().Contains("CCRC2")) //obslugujemy wiadomosc w CCRC2
            {
                Socket CCRC1Socket;
                IPEndPoint CCRC1EP;
                IPAddress CCRC1IP;

                ConnectionRequest Req = ConnectionRequest.convertToConRequest(state.buffer);
                value = Req.SubnetworkIn;
                dest_node = Req.SubnetworkOut;
                SNPPIN = 0;
                nodenameIN = string.Empty;
                SNPPOUT = 0;
                nodenameOUT = string.Empty;
                if (value == 3 && dest_node == 4)
                {
                    SNPPIN = 3100;
                    nodenameIN = "N3";
                    SNPPOUT = 4600;
                    nodenameOUT = "N4";
                }
                else if (value == 3 && dest_node == 5)
                {
                    SNPPIN = 3100;
                    nodenameIN = "N3";
                    SNPPOUT = 5600;
                    nodenameOUT = "N5";
                }
                else if (value == 5 && dest_node == 4)
                {
                    SNPPIN = 5222;
                    nodenameIN = "N5";
                    SNPPOUT = 4600;
                    nodenameOUT = "N4";
                }
                else if (value == 5 && dest_node == 5)
                {
                    SNPPIN = 5222;
                    nodenameIN = "N5";
                    SNPPOUT = 5600;
                    nodenameOUT = "N5";
                }
                else if (value == 5 && dest_node == 7)
                {
                    SNPPIN = 3100;
                    nodenameIN = "N3";
                    SNPPOUT = 5222;
                    nodenameOUT = "N5";
                }
                else if (value == 5 && dest_node == 6)
                {
                    SNPPIN = 5222;
                    nodenameIN = "N5";
                    SNPPOUT = 3100;
                    nodenameOUT = "N3";

                }
                else if (value == 3 && dest_node == 7)
                {
                    SNPPIN = 3100;
                    nodenameIN = "N3";
                    SNPPOUT = 5222;
                    nodenameOUT = "N5";
                }

                wejscie_do_podsieci = Req.SubnetworkWejsciePrzed;

                wyjscie_z_podsieci = Req.SubnetworkWyjsciePo;
                int firstnodeid = 0;
                int secondnodeid = 0;
                connectionID = Req.id_polaczenia;
                nodeMaping.TryGetValue(Req.SubnetworkIn, out firstnodeid);
                nodeMaping.TryGetValue(Req.SubnetworkOut, out secondnodeid);

                List<int> result = new List<int>();
                if (drugiAlgorytm)
                    result = topology.dijkstraSzczeliny(firstnodeid, secondnodeid);
                else
                    result = topology.dijkstra(firstnodeid, secondnodeid);

                int iterator = 0, odleglosc = 0;
                SortedSet<int> fsu = new SortedSet<int>();
                List<int> nodeIndex = new List<int>();
                foreach (int x in result)
                {
                    if (x == -777)
                        iterator++;
                    if (iterator == 0 && x != -777)
                        odleglosc = x;
                    else if (iterator == 1 && x != -777)
                        fsu.Add(x);
                    else if (iterator == 2 && x != -777)
                        nodeIndex.Add(x);

                }
                connectionToSlots.Add(Req.id_polaczenia, fsu);
                connectionToNodes.Add(Req.id_polaczenia, new List<int>(nodeIndex));
               
                wierzcholkidroga.Add(new List<int>(nodeIndex));

                traska = new List<int>(nodeIndex);
                traska.Reverse();

                traska_node = new List<string>();
                traska_node.Add(wejscie_do_podsieci);

                foreach (var element in traska)
                {
                    traska_node.Add(NumberToNodeName[element]);
                }
                traska_node.Add(wyjscie_z_podsieci);
                nameToEP.TryGetValue("CCRC1", out CCRC1EP);
                nameToIp.TryGetValue("CCRC1", out CCRC1IP);

                List<byte> new_list = new List<byte>();
                new_list.AddRange(Encoding.ASCII.GetBytes("CCRC1"));
                new_list.AddRange(BitConverter.GetBytes(odleglosc));

                foreach (int i in fsu)
                    new_list.AddRange(BitConverter.GetBytes(i));

                new_list.AddRange(BitConverter.GetBytes(777777));
                byte[] new_list_byte;
                new_list_byte = new_list.ToArray();

                CCRC1Socket = new Socket(CCRC1IP.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);
                CCRC1Socket.Connect(CCRC1EP);
                CCRC1Socket.Send(new_list_byte);
            }

            if (state.sb.ToString().Contains("DELETE_SLOTS"))
            {
                var parts = state.sb.ToString().Split('#');

                int pierwszy = Convert.ToInt32(parts[1]);
                int ostatni = Convert.ToInt32(parts[2]);
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Got message to free the resources. Sending a message to LRM.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                List<int> indeksy = new List<int>();
                List<List<int>> nodes = new List<List<int>>();
                List<SortedSet<int>> slots = new List<SortedSet<int>>();

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Freeing up resources on the links, that have been used for received broken connection. Slots to delete: from {4} to {5}.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, pierwszy, ostatni);

                foreach (int key in connectionToSlots.Keys)
                {
                    if (connectionToSlots[key].First().Equals(pierwszy) && connectionToSlots[key].Last().Equals(ostatni))
                        indeksy.Add(key);
                }

                foreach (int indeks in indeksy)
                {
                    nodes.Add(connectionToNodes[indeks]);
                    slots.Add(connectionToSlots[indeks]);
                }

                foreach (int i in indeksy)
                    slotsRelease(i);

                foreach (var nodeName in NodeToSocket.Keys)
                {
                    if (nodeName.StartsWith('N'))
                    {
                        int pierwsza_szczelina = pierwszy, druga_szczelina = ostatni;
                        Socket sock = NodeToSocket[nodeName];
                        string message = "DELETE_FSU#" + pierwsza_szczelina + '#' + druga_szczelina + '#';
                        Send(sock, Encoding.ASCII.GetBytes(message));
                    }
                }
            }

            if (state.sb.ToString().Contains("SHUTDOWN_LINK"))
            {
                var parts = state.sb.ToString().Split('#');
                string nodename1 = parts[1];
                string nodename2 = parts[2];
                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Broken link between {4} and {5}.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, nodename1, nodename2);
                int firstnode = NodeNameToNumber[nodename1];
                int secondnode = NodeNameToNumber[nodename2];
                List<int> indeksy = new List<int>();
                // wierzcholki
                List<List<int>> nodes = new List<List<int>>();
                List<SortedSet<int>> slots = new List<SortedSet<int>>();

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Freeing up resources on the links, that have been used for broken connections.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                foreach (List<int> list in connectionToNodes.Values)
                {
                    for (int i = 0; i < list.Count - 1; i++)
                        if (list[i] == firstnode && list[i + 1] == secondnode || list[i + 1] == firstnode && list[i] == secondnode)

                            foreach (int indeks in connectionToNodes.Keys)
                                if (list.Equals(connectionToNodes[indeks]))
                                    indeksy.Add(indeks);
                }

                foreach (int indeks in indeksy)
                {
                    nodes.Add(connectionToNodes[indeks]);
                    slots.Add(connectionToSlots[indeks]);
                }

                foreach(var i in slots)
                {
                    Console.WriteLine("jol men");
                    foreach(var j in i)
                    {
                        Console.WriteLine("siema: " + j);
                    }
                }

                foreach (int i in indeksy)
                    slotsRelease(i);

                removeEdge(firstnode, secondnode);
                foreach (var nodeName in NodeToSocket.Keys)
                {
                    foreach (var fsu in slots)
                        if (nodeName.StartsWith('N'))
                        {
                            int pierwsza_szczelina = fsu.First(), druga_szczelina = fsu.Last();
                            Socket sock = NodeToSocket[nodeName];
                            string message = "DELETE_FSU#" + pierwsza_szczelina + '#' + druga_szczelina + '#';
                            Send(sock, Encoding.ASCII.GetBytes(message));
                        }

                }

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Sending LocalTopology to RC.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond);

                Console.WriteLine("[{0}:{1}:{2}.{3}] [RC] Received LocalTopology.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond);

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Sending Information to CC that the link is broken.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond);

                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Informing CC in the domain to free the resources due to broken connections.",
                   DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                       DateTime.Now.Millisecond);

                Socket CCRC1Socket;
                IPEndPoint CCRC1EP;
                IPAddress CCRC1IP;
                nameToEP.TryGetValue("CCRC1", out CCRC1EP);
                nameToIp.TryGetValue("CCRC1", out CCRC1IP);
                CCRC1Socket = new Socket(CCRC1IP.AddressFamily,
                   SocketType.Stream, ProtocolType.Tcp);
                CCRC1Socket.Connect(CCRC1EP);
                List<string> newlist_string = new List<string>();
                foreach (SortedSet<int> fsu in slots)
                {
                    List<byte> new_list = new List<byte>();
                    new_list.AddRange(Encoding.ASCII.GetBytes("DELETE_SLOTS#" + fsu.First() + "#" + fsu.Last() + "#"));
                    byte[] new_list_byte;
                    new_list_byte = new_list.ToArray();
                    CCRC1Socket.Send(new_list_byte);
                    Console.WriteLine(Encoding.ASCII.GetString(new_list_byte));
                }
            }

            if (state.sb.ToString().Contains("CLOSE_FREE_SLOTS"))
            {
                Console.WriteLine("[{0}:{1}:{2}.{3}] Received message {4}",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, state.sb);
                var parts = state.sb.ToString().Split('#');

                int pierwszy = Convert.ToInt32(parts[1]);
                int ostatni = Convert.ToInt32(parts[2]);
                List<int> indeksy = new List<int>();
                List<List<int>> nodes = new List<List<int>>();
                List<SortedSet<int>> slots = new List<SortedSet<int>>();

                Console.WriteLine("[{0}:{1}:{2}.{3}] Freeing up resources with given slots",
                     DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                foreach (int key in connectionToSlots.Keys)
                {
                    if (connectionToSlots[key].First().Equals(pierwszy) && connectionToSlots[key].Last().Equals(ostatni))
                        indeksy.Add(key);
                }

                foreach (int indeks in indeksy)
                {
                    nodes.Add(connectionToNodes[indeks]);
                    slots.Add(connectionToSlots[indeks]);
                }

                foreach (int i in indeksy)
                    slotsRelease(i);

                foreach (var nodeName in NodeToSocket.Keys)
                {
                    if (nodeName.StartsWith('N'))
                    {
                        int pierwsza_szczelina = pierwszy, druga_szczelina = ostatni;
                        Socket sock = NodeToSocket[nodeName];
                        string message = "DELETE_FSU#" + pierwsza_szczelina + '#' + druga_szczelina + '#';
                        Send(sock, Encoding.ASCII.GetBytes(message));
                    }
                }
            }

            if (state.sb.ToString().Contains("FSU"))//Tu CCRC2 dostaje szczeliny od CCRC1 - te które ma zająć
            {
                counter = 0;

                int liczba_szczelin_podsiec = BitConverter.ToInt32(state.buffer, 3);
                int firstSlot = BitConverter.ToInt32(state.buffer, 7);
                int second_slot = firstSlot + liczba_szczelin_podsiec - 1;

                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received ConnetionRequest_req(SNP_IN {4}:{5}, SNP_OUT {6}:{7}, first_slot: {8}, last_slot: {9}, setup) from CCRC1",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, nodenameIN, SNPPIN, nodenameOUT, SNPPOUT, firstSlot, second_slot);
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending RouteTableQuery_req(SNP_IN {4}:{5}, SNP_OUT {6}:{7}, first_slot: {8}, last_slot: {9})",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, nodenameIN, SNPPIN, nodenameOUT, SNPPOUT, firstSlot, second_slot);
                Console.WriteLine("[{0}:{1}:{2}.{3}] [RC] Received RouteTableQuery_req(SNP_IN {4}:{5}, SNP_OUT {6}:{7}, first_slot: {8}, last_slot: {9})",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, nodenameIN, SNPPIN, nodenameOUT, SNPPOUT, firstSlot, second_slot);
                Thread.Sleep(100);

                string trasa = string.Empty;
                for (int i = 0; i < traska_node.Count - 2; i++)
                {
                    if (traska_node[i + 1].Equals("H2") || traska_node[i + 1].Equals("N1") || traska_node[i + 1].Equals("N2"))
                        continue;
                    else
                    {
                        trasa += traska_node[i + 1];
                        trasa += ' ';
                    }
                }
                Console.WriteLine("[{0}:{1}:{2}.{3}] [RC] Sending RouteTableQuery_rsp({4})",
                           DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, trasa);
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received RouteTableQuery_rsp({4})",
                           DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, trasa);
                int y = -5;

               for(int i=0; i<traska_node.Count-1;i++)
                {
                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending SNPLinkConnectionRequest_req({4}, {5},  first_slot: {6}, last_slot: {7}, setup)",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            traska_node[i], traska_node[i + 1], firstSlot, second_slot);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Received SNPLinkConnectionRequest_req({4}, {5},  first_slot: {6}, last_slot: {7}, setup)",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            traska_node[i], traska_node[i + 1], firstSlot, second_slot);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Sending SNPLinkConnectionRequest_rsp(\"Allocated\", {4}, {5},  first_slot: {6}, last_slot: {7})",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            traska_node[i], traska_node[i + 1], firstSlot, second_slot);

                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received SNPLinkConnectionRequest_rsp(\"Allocated\", {4}, {5},  first_slot: {6}, last_slot: {7})",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            traska_node[i], traska_node[i + 1], firstSlot, second_slot);
                }

                int secondSlot = firstSlot + liczba_szczelin_podsiec - 1;
                LRMactualization(liczba_szczelin_podsiec, firstSlot);

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
                            Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending ConnectionRequest_req(in_port: {4} first_slot: {5}, last_slot: {6}, out_port: {7}, setup) to {8}", DateTime.Now.Hour, DateTime.Now.Minute, 
                                DateTime.Now.Second, DateTime.Now.Millisecond, in_port.ToString(), firstSlot.ToString(), secondSlot.ToString(), out_port.ToString(), traska_node[i + 1]);
                            counter++;
                        }
                        else
                        {
                        } 
                    }
                }
                traska_node.Clear();
                traska.Clear();
            }

            if (state.sb.ToString().Contains("Algorytm2"))
            {
                drugiAlgorytm = true;
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

        private void LRMactualization(int liczba, int pierwszyIndeks)
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

        public int calc_modulation(int odleglosc)
        {
            if (odleglosc > 0 && odleglosc < 100)
                return 6;
            else if (odleglosc >= 100 && odleglosc < 200)
                return 5;
            else if (odleglosc >= 200 && odleglosc < 300)
                return 4;
            else if (odleglosc >= 300 && odleglosc < 400)
                return 3;
            else if (odleglosc >= 400 && odleglosc < 500)
                return 2;
            else if (odleglosc >= 500)
                return 1;
            else return -1;
        }

        //zaokrągla do wielokrotności 12.5GHz
        double RoundUp(double numToRound)
        {
            double remainder = numToRound % 12.5;
            if (remainder == 0)
                return numToRound;

            return numToRound + 12.5 - remainder;
        }

    }
}