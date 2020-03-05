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

namespace Controllers
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
    public class Controllers
    {
        String name;
        private String nazwaNodeNowa;

        IPAddress ControllersAddress, NCCAddress, adres1, adres2;

        Socket CCRCSocket, CCRC2Socket, CCRC3Socket;

        public SortedSet<int> FSUDomena2;

        private ConcurrentDictionary<string, IPAddress> nameToIp;
        private ConcurrentDictionary<IPAddress, string> ipToName;
        private ConcurrentDictionary<string, Socket> NodeToSocket;
        private ConcurrentDictionary<Socket, string> SocketToNode;
        private ConcurrentDictionary<string, IPEndPoint> nameToEP;
        private ConcurrentDictionary<string, List<int>> CCRCnameToNode;

        private Dictionary<int, SortedSet<int>> connectionToSlots;
        private Dictionary<int, List<int>> connectionToNodes;
        private Dictionary<int, string> numberToNodeName;
        private Dictionary<int, CallRequest> connectionToCallRequest;
        private Dictionary<string, int> NodeNameToNumber;

        private bool isReconnecting;
        private int capacity1;
        private int NCCPort;
        private int ControllersPort;
        private List<int> traska;

        private int dlugosc_domena2 = 100;
        private int dest_node;
        private int dlugoscDrogi = 0;
        private int przepustowosc = 0;
        private int connectionID = 0;
        private int SNPPIN = 0;
        private int SNPPOUT = 0;
        private int firstSlot;
        private int lastSlot;
        private int liczbaSzczelinDoDom;
        private int s1, s2;
        private int counter = 0;
        public static int MAXSLOTS = 20;

        private List<SortedSet<int>> fsudroga;
        private List<List<int>> wierzcholkidroga;
        private List<LRMRow> LRM;
        private List<Connections> connections;
        private List<int> outnodeArray = new List<int> { 4, 5 };

        private Graf topology;

        bool drugiAlgorytm = false;
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private string nodenameIN = string.Empty;
        private string nodenameOUT = string.Empty;

        private ConnectionResponse response;

        public Controllers(string configFile)
        {
            isReconnecting = false;
            wierzcholkidroga = new List<List<int>>();
            fsudroga = new List<SortedSet<int>>();

            nameToIp = new ConcurrentDictionary<string, IPAddress>();
            ipToName = new ConcurrentDictionary<IPAddress, string>();
            nameToEP = new ConcurrentDictionary<string, IPEndPoint>();
            NodeToSocket = new ConcurrentDictionary<string, Socket>();
            SocketToNode = new ConcurrentDictionary<Socket, string>();
            CCRCnameToNode = new ConcurrentDictionary<string, List<int>>();

            numberToNodeName = new Dictionary<int, string>();
            NodeNameToNumber = new Dictionary<string, int>();
            connectionToNodes = new Dictionary<int, List<int>>();
            connectionToSlots = new Dictionary<int, SortedSet<int>>();
            connectionToCallRequest = new Dictionary<int, CallRequest>();

            response = new ConnectionResponse();

            FSUDomena2 = new SortedSet<int>();
            FSUDomena2 = wypelnijSet();

            numberToNodeName.Add(7, "H2");
            numberToNodeName.Add(6, "H1");
            numberToNodeName.Add(1, "N1");
            numberToNodeName.Add(3, "N3");
            numberToNodeName.Add(2, "N2");
            numberToNodeName.Add(5, "N5");
            numberToNodeName.Add(4, "N4");
            numberToNodeName.Add(10, "N6");
            traska = new List<int>();
            NodeNameToNumber.Add("H2", 7);
            NodeNameToNumber.Add("H1", 6);
            NodeNameToNumber.Add("N1", 1);
            NodeNameToNumber.Add("N3", 3);
            NodeNameToNumber.Add("N2", 2);
            NodeNameToNumber.Add("N5", 5);
            NodeNameToNumber.Add("N4", 4);


            LRM = new List<LRMRow>();
            connections = new List<Connections>();

            ReadConfig(configFile);
            if (name.Equals("CCRC1"))
            {
                topology = new Graf("graf1.txt");
                topology.wezly[2].czyPodsiec = true;
                topology.wezly[4].czyPodsiec = true;
                topology.wezly[3].czyPodsiec = true;
            }
            else if (name.Equals("CCRC3"))
            {
                topology = new Graf("graf3.txt");
            }
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

            readNametoNodes(lines, CCRCnameToNode);
            readNodes(lines);
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

                IPEndPoint CCRC2EP, CCRC3EP;
                IPAddress CCRC2IP, CCRC3IP;
                nameToEP.TryGetValue("CCRC2", out CCRC2EP);
                nameToIp.TryGetValue("CCRC2", out CCRC2IP);
                nameToEP.TryGetValue("CCRC3", out CCRC3EP);
                nameToIp.TryGetValue("CCRC3", out CCRC3IP);
                CCRC2Socket = new Socket(CCRC2IP.AddressFamily,
                   SocketType.Stream, ProtocolType.Tcp);
                CCRC2Socket.Connect(CCRC2EP);
                CCRC3Socket = new Socket(CCRC3IP.AddressFamily,
                   SocketType.Stream, ProtocolType.Tcp);
                CCRC3Socket.Connect(CCRC3EP);
                while (true)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();
                    CCRCSocket.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        CCRCSocket);

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
                Send(handler, Encoding.ASCII.GetBytes("Connection with CCRC established"));
                //Hello H3 e.g.
                var split = state.sb.ToString().Split(' ');
                string nodeName = split[0];

                Console.WriteLine("[{0}:{1}:{2}.{3}] Received a message {4}",
                     DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, state.sb);
                while (true)
                {
                    if (NodeToSocket.TryAdd(nodeName, handler))
                    {
                        Console.WriteLine($"Adding {nodeName}");
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

            if (state.sb.ToString().Contains("CONN_REQ_RESP"))
            {
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received ConnectionRequest_rsp(connection_id: {4}) from CCRC2",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, connectionID);
               
                if (numberToNodeName[dest_node].Equals("N4") || numberToNodeName[dest_node].Equals("N5"))
                {
                    List<byte> new_list = new List<byte>();
                    new_list.AddRange(Encoding.ASCII.GetBytes("FSU")); 
                    new_list.AddRange(BitConverter.GetBytes(liczbaSzczelinDoDom));
                    new_list.AddRange(BitConverter.GetBytes(response.firstFSU));
                    byte[] new_list_byte;
                    new_list_byte = new_list.ToArray();
                    new_list.AddRange(BitConverter.GetBytes(dest_node));
                    new_list_byte = new_list.ToArray();
                    CCRC3Socket.Send(new_list_byte);
                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending ConnectionRequest_req(H3, DomainExitPoint: {4}, firstSlot: {5}, lastSlot: {6}, setup) to CCRC3",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, numberToNodeName[dest_node], response.firstFSU, response.secondFSU);
                }
                else
                {
                      Socket NCCSocket;
                 NCCSocket = new Socket(NCCAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                 NCCSocket.Connect(NCCAddress, NCCPort);
                 NCCSocket.Send(response.convertToByte());
                 Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending ConnectionRequest_rsp(destination: {4}, firstSlot: {5}, lastSlot: {6}) to NCC1",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, numberToNodeName[dest_node], response.firstFSU, response.secondFSU);
                }
                connectionID++;
            }

            if (state.sb.ToString().Contains("Connect") || state.sb.ToString().Contains("RECONNECT")) 
            {
                String source = "";
                String dest = "";
                CallRequest Call_req;
                if (state.sb.ToString().Contains("Connect"))
                {
                    Call_req = CallRequest.convertToRequest(state.buffer);

                    ipToName.TryGetValue(Call_req.source, out source);
                    ipToName.TryGetValue(Call_req.destination, out dest);

                    przepustowosc = Call_req.capacity;

                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received ConnectionRequest_req(source:{4}, destination:{5}, capacity:{6}, setup) from NCC1",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,Call_req.source, Call_req.destination, przepustowosc);
                    capacity1 = przepustowosc;
                    adres1 = Call_req.source;
                    nazwaNodeNowa = source;
                    adres2=Call_req.destination;

                    connectionToCallRequest.Add(connectionID, Call_req);
                }
                else if (state.sb.ToString().Contains("RECONNECT"))
                {
                    var split = state.sb.ToString().Split("#");
                    IPAddress calreqsource = IPAddress.Parse(split[1]);
                    IPAddress calreqdest = IPAddress.Parse(split[2]);

                    ipToName.TryGetValue(calreqsource, out source);
                    ipToName.TryGetValue(calreqdest, out dest);
                    przepustowosc = Int32.Parse(split[3]);
                    isReconnecting = true;
                }
                String key = source + "-" + dest;
                List<int> result = new List<int>();
                int iterator = 0, odleglosc = 0;
                SortedSet<int> fsu = new SortedSet<int>();
                List<int> nodeIndex = new List<int>();

                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending RouteTableQuery_req(source: {4}, destination: {5}, capacity: {6})",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, adres1, adres2, przepustowosc);
                Console.WriteLine("[{0}:{1}:{2}.{3}] [RC] Received RouteTableQuery_req(source: {4}, destination: {5}, capacity: {6})",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, adres1, adres2, przepustowosc);
                Random r = new Random();
                int randomIndex = r.Next(outnodeArray.Count);

                dest_node = 0;
                if (source.Equals("H1") && dest.Equals("H2"))
                {
                    if (drugiAlgorytm)
                        result = topology.dijkstraSzczeliny(6, 7);
                    else
                        result = topology.dijkstra(6, 7);
                    dest_node = 7;
                }
                else if (source.Equals("H2") && dest.Equals("H1"))
                {
                    if (drugiAlgorytm)
                        result = topology.dijkstraSzczeliny(7, 6);
                    else
                        result = topology.dijkstra(7, 6);
                    dest_node = 6;
                }
                else if (source.Equals("H1") && dest.Equals("H3"))
                {
                    if (drugiAlgorytm)
                        result = topology.dijkstraSzczeliny(6, outnodeArray[randomIndex]);
                    else
                        result = topology.dijkstra(6, outnodeArray[randomIndex]);
                    dest_node = outnodeArray[randomIndex];
                }
                else if (source.Equals("H2") && dest.Equals("H3"))
                {
                    if (drugiAlgorytm)
                        result = topology.dijkstraSzczeliny(7, outnodeArray[randomIndex]);
                    else
                        result = topology.dijkstra(7, outnodeArray[randomIndex]);
                    dest_node = outnodeArray[randomIndex];
                }

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

                traska = new List<int>(nodeIndex);
                fsudroga.Add(new SortedSet<int>(fsu));
                wierzcholkidroga.Add(new List<int>(nodeIndex));
                connectionToSlots.Add(connectionID, fsudroga[fsudroga.Count - 1]);
                connectionToNodes.Add(connectionID, new List<int>(nodeIndex));
                dlugoscDrogi += odleglosc;
                int value = 0;
                nodeIndex.Reverse();
                int wejscie_podsiec = -1;
                foreach (int x in nodeIndex)
                {
                    if (topology.wezly[x - 1].czyPodsiec == true)
                    {
                        value = x;
                        break;
                    }
                    wejscie_podsiec = x;
                }

                int wyjscie_podsiec = -1;
                int nodeWyjsciowywPodsieci = nodeIndex.First();
                foreach (int x in nodeIndex)
                {
                    if (topology.wezly[x - 1].czyPodsiec == false && topology.wezly[nodeWyjsciowywPodsieci - 1].czyPodsiec == true)
                    {
                        wyjscie_podsiec = x;
                        break;
                    }
                    nodeWyjsciowywPodsieci = x;
                }
                if (wyjscie_podsiec == -1)
                {
                    wyjscie_podsiec = 10;
                }

                List<int> nodeIndex_number = new List<int>(nodeIndex);

                nodeIndex_number.Reverse();

                List<string> nodeIndex_name = new List<string>();
                List<string> realnodeIndex_name = new List<string>();

                foreach (var elem in nodeIndex_number)
                {
                    nodeIndex_name.Add(numberToNodeName[elem]);
                }

                foreach (var elem in nodeIndex_name)
                {
                    realnodeIndex_name.Add(elem);
                }
                realnodeIndex_name.Reverse();

                string node_podsiec_wyjscie = numberToNodeName[wyjscie_podsiec];
                string node_podsiec_wejscie = numberToNodeName[wejscie_podsiec];
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

                IPEndPoint CCRC2EP;
                IPAddress CCRC2IP;
                List<int> nodesList;
                nodesList = new List<int>();
                ConnectionRequest ConReq = new ConnectionRequest("CCRC2", value, node_podsiec_wejscie, nodeWyjsciowywPodsieci, node_podsiec_wyjscie, connectionID);
                nameToEP.TryGetValue("CCRC2", out CCRC2EP);
                nameToIp.TryGetValue("CCRC2", out CCRC2IP);
                CCRC2Socket.Send(ConReq.convertReqToByte());
            }

            if (state.sb.ToString().Contains("CCRC1")) //wiadomosc od CCRC2 do CCRC1
            {
                counter = 0;
                SortedSet<int> fsupodsiec = new SortedSet<int>();
                int odleglosc_z_podsieci = BitConverter.ToInt32(state.buffer, 5);
                for (int i = 9; i < state.buffer.Length; i = i + 4)
                {
                    if (BitConverter.ToInt32(state.buffer, i) == 777777)
                        break;
                    fsupodsiec.Add(BitConverter.ToInt32(state.buffer, i));
                }
                dlugoscDrogi += odleglosc_z_podsieci;

                if (dest_node == 4 || dest_node == 5)
                {
                    dlugoscDrogi += dlugosc_domena2;
                }

                string nazwa_modulacji = string.Empty;

                int modulation = calc_modulation(dlugoscDrogi);
                if (modulation == 1)
                {
                    nazwa_modulacji = "BPSK";
                }
                else if (modulation == 2)
                {
                    nazwa_modulacji = "4-QAM";
                }
                else if (modulation == 3)
                {
                    nazwa_modulacji = "8-QAM";
                }
                else if (modulation == 4)
                {
                    nazwa_modulacji = "16-QAM";
                }
                else if (modulation == 5)
                {
                    nazwa_modulacji = "32-QAM";
                }
                else if (modulation == 6)
                {
                    nazwa_modulacji = "64-QAM";
                }
                double czestotliwosc_po_modulacji = RoundUp(przepustowosc * 2 / modulation);
                double liczba_szczelin = czestotliwosc_po_modulacji / 12.5;
                int liczba_szczelin_int = (int)liczba_szczelin;

                SortedSet<int> ss = new SortedSet<int>();
                ss = wypelnijSet();
                ss.IntersectWith(fsupodsiec);
                ss.IntersectWith(fsudroga[connectionID]);
                if (dest_node == 4 || dest_node == 5)
                {
                    ss.IntersectWith(FSUDomena2);
                }
                int firstSlot = 0;
                try
                {
                    firstSlot = ss.First();
                }
                catch (System.InvalidOperationException)
                {
                    Console.WriteLine("Trying to get a non-existing value from set.");
                }

                int secondSlot = firstSlot + liczba_szczelin_int - 1;

                this.firstSlot = firstSlot;
                lastSlot = secondSlot;
                List<string> traska_node = new List<string>();

                Console.WriteLine("[{0}:{1}:{2}.{3}] [RC] Sending RouteTableQuery_rsp(SNP_IN {4}:{5}, SNP_OUT {6}:{7}, first_slot: {8}, last_slot: {9})",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, 
                        nodenameIN, SNPPIN, nodenameOUT, SNPPOUT, firstSlot, secondSlot);

                foreach (int wierzcholek in traska)
                {
                    traska_node.Add(numberToNodeName[wierzcholek]);
                }
                traska_node.Reverse();

                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received RouteTableQuery_rsp(SNP_IN {4}:{5}, SNP_OUT {6}:{7}, first_slot: {8}, last_slot: {9})",
                     DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                        nodenameIN, SNPPIN, nodenameOUT, SNPPOUT, firstSlot, secondSlot);

                s1 = firstSlot;
                s2 = secondSlot;

                if (liczba_szczelin_int <= ss.Count())
                {
                    for(int i=0; i<traska_node.Count -1;i++)
                    {
                        if ((traska_node[i].Equals("N3") && traska_node[i + 1].Equals("N4")) || (traska_node[i].Equals("N4") && traska_node[i + 1].Equals("N3")) || (traska_node[i].Equals("N3") && traska_node[i + 1].Equals("N5"))
                            || (traska_node[i].Equals("N5") && traska_node[i + 1].Equals("N3")) || (traska_node[i].Equals("N4") && traska_node[i + 1].Equals("N5")) || (traska_node[i].Equals("N5")) && traska_node[i+1].Equals("N4"))
                        {
                            continue;
                        }
                        else
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
                    }

                    for (int i = 0; i < traska_node.Count - 2; i++)
                    {
                        int firstport = getFirstPort(traska_node[i], traska_node[i + 1]);
                        int secondport = getSecondPort(traska_node[i], traska_node[i + 1]);

                        int in_port = secondport;

                        int out_port = getFirstPort(traska_node[i + 1], traska_node[i + 2]);

                        Socket sock;
                        if (traska_node[i + 1].Equals("N3") || traska_node[i + 1].Equals("N5") || traska_node[i + 1].Equals("N4"))
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
                        }
                    }
                    traska_node.Clear();
                    traska.Clear();

                    LRMactualization(liczba_szczelin_int, firstSlot);
                    liczbaSzczelinDoDom = liczba_szczelin_int;

                    List<byte> new_list = new List<byte>();
                    new_list.AddRange(Encoding.ASCII.GetBytes("FSU")); 
                    new_list.AddRange(BitConverter.GetBytes(liczba_szczelin_int));
                    new_list.AddRange(BitConverter.GetBytes(firstSlot));
                    byte[] new_list_byte;
                    new_list_byte = new_list.ToArray();
                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending ConnectionRequest_req(SNP_IN {4}:{5}, SNP_OUT {6}:{7}, first_slot: {8}, last_slot: {9}, setup) to CCRC2",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond,
                            nodenameIN, SNPPIN, nodenameOUT, SNPPOUT, firstSlot, secondSlot);
                    CCRC2Socket.Send(new_list_byte);
                    if (dest_node == 4 || dest_node == 5)
                    {
                        try
                        {
                            for (int i = firstSlot; i < firstSlot + liczba_szczelin_int; i++)
                            {
                                FSUDomena2.Remove(i);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("There are no more slots available in Domain2");
                        }
                        response.firstFSU = firstSlot;
                        response.secondFSU = firstSlot + liczba_szczelin_int - 1;
                        response.sender = name;
                        response.package_length = 32 + name.Length;
                    }
                    else
                    {
                        response.firstFSU = firstSlot;
                        response.secondFSU = firstSlot + liczba_szczelin_int - 1;
                        response.sender = name;
                        response.package_length = 32 + name.Length;
                    }
                }
                else if (drugiAlgorytm)
                {
                    Console.WriteLine("[{0}:{1}:{2}.{3}] [RC] There is no path with demanded capacity");
                    drugiAlgorytm = false;
                }
                else
                {
                    drugiAlgorytm = true;
                    IPEndPoint CCRC2EP;
                    IPAddress CCRC2IP;
                    nameToEP.TryGetValue("CCRC2", out CCRC2EP);
                    nameToIp.TryGetValue("CCRC2", out CCRC2IP);
                    List<byte> new_list = new List<byte>();
                    new_list.AddRange(Encoding.ASCII.GetBytes("Algorytm2"));
                    byte[] new_list_byte;
                    new_list_byte = new_list.ToArray();
                    CCRC2Socket.Send(new_list_byte);
                }
                dlugoscDrogi = 0;
                if (isReconnecting == false)
                {
                }
                else
                {
                    isReconnecting = false;
                }

            }
            if (state.sb.ToString().Contains("ZESTAWIONO"))
            {

                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received ConnectionRequest_rsp(H3, DomainExitPoint: {4}, firstSlot: {5}, lastSlot: {6}) from CCRC3",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, numberToNodeName[dest_node], firstSlot, lastSlot);
                Socket NCCSocket;
                NCCSocket = new Socket(NCCAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                NCCSocket.Connect(NCCAddress, NCCPort);
                NCCSocket.Send(response.convertToByte());
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Sending ConnectionRequest_rsp(destination: H3, firstSlot: {4}, lastSlot: {5}) to NCC1",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, response.firstFSU, response.secondFSU);
            }

            if (state.sb.ToString().Contains("Adding"))
            {
                var parts = state.sb.ToString().Split('#');
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received ConnectionRequest_rsp(in_port: {4} first_slot: {5}, last_slot: {6}, out_port: {7}, setup) from {8} ", DateTime.Now.Hour, DateTime.Now.Minute,
                    DateTime.Now.Second, DateTime.Now.Millisecond, parts[1], parts[2], parts[3], parts[4], parts[5]);
                counter--;
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

                Socket NCCSocket1 = new Socket(NCCAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);
                NCCSocket1.Connect(NCCAddress, NCCPort);
                foreach (int indeks in indeksy)
                {
                    CallRequest calreq = connectionToCallRequest[indeks];

                    if (ipToName[calreq.destination] == "H3")
                    {
                        string message = "DELETE_SLOTS#" + pierwszy + "#" + ostatni + "#";
                        CCRC3Socket.Send(Encoding.ASCII.GetBytes(message));
                    }

                }
                foreach (int indeks in indeksy)
                {
                    CallRequest calreq = connectionToCallRequest[indeks];
                    string message = "RECONNECT#" + calreq.source.ToString() + "#" + calreq.destination.ToString() + "#" + calreq.capacity + "#";
                    byte[] send = Encoding.ASCII.GetBytes(message);
                    NCCSocket1.Send(send);
                }

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Informing RC to update network topology.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond);
            }

            if (state.sb.ToString().Contains("NCCCLOSECONNECTION#"))
            {
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Received a message from NCC to close a connection. Sending a message to LRM.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                var split = state.sb.ToString().Split("#");

                int pierwszy = Int32.Parse(split[1]);
                int ostatni = Int32.Parse(split[2]);

                List<int> indeksy = new List<int>();
                List<List<int>> nodes = new List<List<int>>();
                List<SortedSet<int>> slots = new List<SortedSet<int>>();

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Freeing up resources on the links, that have been used for broken connection. Slots to delete: from {4} to {5}.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond, pierwszy, ostatni);

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
                        string messageToNodes = "DELETE_FSU#" + pierwsza_szczelina + '#' + druga_szczelina + '#';
                        Send(sock, Encoding.ASCII.GetBytes(messageToNodes));
                    }
                }

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Informing RC to update network topology.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond);

                string message = "CLOSE_FREE_SLOTS#" + pierwszy + "#" + ostatni + "#";
                CCRC2Socket.Send(Encoding.ASCII.GetBytes(message));
                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Informing CC in the subnetwork to free the resources due to connection end.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                        DateTime.Now.Millisecond);

            }

            if (state.sb.ToString().Contains("SHUTDOWN_LINK"))
            {
                var parts = state.sb.ToString().Split('#');
                string nodename1 = parts[1];
                string nodename2 = parts[2];
                int firstnode = NodeNameToNumber[nodename1];
                int secondnode = NodeNameToNumber[nodename2];
                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Got a message that there is something wrong with a link between {4} and {5}.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, nodename1, nodename2);

                List<int> indeksy = new List<int>();
                List<List<int>> nodes = new List<List<int>>();
                List<SortedSet<int>> slots = new List<SortedSet<int>>();
                foreach (List<int> list in connectionToNodes.Values)
                {
                    for (int i = 0; i < list.Count - 1; i++)
                        if (list[i] == firstnode && list[i + 1] == secondnode || list[i + 1] == firstnode && list[i] == secondnode)
                            foreach (int indeks in connectionToNodes.Keys)
                                if (list.Equals(connectionToNodes[indeks]))
                                    indeksy.Add(indeks);
                }

                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Freeing up resources on the links, that have been used for broken connections.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                foreach (int indeks in indeksy)
                {
                    nodes.Add(connectionToNodes[indeks]);
                    slots.Add(connectionToSlots[indeks]);
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
                Console.WriteLine("[{0}:{1}:{2}.{3}] [LRM] Informing RC to update network topology.",
                   DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                Console.WriteLine("[{0}:{1}:{2}.{3}] [CC] Informing CC in the subnetwork to free the resources due to broken connections.",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
                IPEndPoint CCRC2EP;
                IPAddress CCRC2IP;
                Socket NCCSocket;
                NCCSocket = new Socket(NCCAddress.AddressFamily,
                   SocketType.Stream, ProtocolType.Tcp);
                NCCSocket.Connect(NCCAddress, NCCPort);
                nameToEP.TryGetValue("CCRC2", out CCRC2EP);
                nameToIp.TryGetValue("CCRC2", out CCRC2IP);
                foreach (SortedSet<int> fsu in slots)
                {
                    List<byte> new_list = new List<byte>();
                    new_list.AddRange(Encoding.ASCII.GetBytes("DELETE_SLOTS#" + fsu.First() + "#" + fsu.Last() + "#"));
                    byte[] new_list_byte;
                    new_list_byte = new_list.ToArray();
                    CCRC2Socket.Send(new_list_byte);
                }

                foreach (int indeks in indeksy)
                {
                    CallRequest calreq = connectionToCallRequest[indeks];
                    string message = "RECONNECT#" + calreq.source + "#" + calreq.destination + "#" + calreq.capacity + "#";
                    byte[] send = Encoding.ASCII.GetBytes(message);
                    NCCSocket.Send(send);
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
            Console.Title = "CCRC1";
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
                            if ((x == 5 && y == 3 || x == 3 && y == 5 || x == 3 && y == 4 || x == 4 && y == 3 || x == 5 && y == 4 || x == 4 && y == 5) && name.Equals("CCRC1"))
                            {}
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

        private void readNametoNodes(List<string> lines, ConcurrentDictionary<string, List<int>> CCRCNameToNode)
        {
            foreach (var line in lines.FindAll(line => line.StartsWith("NAMETONODE")))
            {
                string[] entries;
                entries = line.Split(' ');
                List<int> result = new List<int> { Int32.Parse(entries[2]), Int32.Parse(entries[3]) };
                CCRCNameToNode.TryAdd(entries[1], result);
            }
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
            foreach (int i in connectionToSlots[idpolaczenia])
                FSUDomena2.Add(i);
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
            List<LRMRow> lrmRowDoUsuniecia = new List<LRMRow>();
            foreach (Krawedz krawedz in krawedzieDoUsuniecia)
            {
                topology.krawedzie.Remove(krawedz);
                foreach (LRMRow row in LRM)
                {
                    if ((krawedz.PodajPoczatek() == row.routerID1 && krawedz.PodajKoniec() == row.routerID2) || (krawedz.PodajPoczatek() == row.routerID2 && krawedz.PodajKoniec() == row.routerID1))
                        lrmRowDoUsuniecia.Add(row);
                }
                for (int i = 0; i < lrmRowDoUsuniecia.Count; i++)
                    LRM.Remove(lrmRowDoUsuniecia[i]);
            }
        }
    }
}