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
using System.Timers;

namespace Host
{
    public class Host
    {
        private string hostName;
        private IPAddress address;
        private Socket cloudSocket;
        private Socket NCCSocket;
        private int port;
        private IPAddress cloudAddress;
        private int cloudPort;
        private IPAddress NCCAddress;
        private int NCCPort;
        private static System.Timers.Timer aTimer;
        private DataPackage dp;
        private CallRequest cr;
        private string dest_host;
        private int period;
        private int capacity;
        private ConcurrentDictionary<string, Boolean> nodeIsConnected;
        private ConcurrentDictionary<string, IPAddress> nameToIp;
        private ConcurrentDictionary<IPAddress, string> ipToName;
        private ConcurrentDictionary<string, List<int>> nodeToFsu;
        private Boolean sending = false;
        private string sourceHostAccept;

        public Host(string configFile)
        {
            nameToIp = new ConcurrentDictionary<string, IPAddress>();
            ipToName = new ConcurrentDictionary<IPAddress, string>();
            nodeIsConnected = new ConcurrentDictionary<string, bool>();
            nodeToFsu = new ConcurrentDictionary<string, List<int>>();
            List<string> lines;
            lines = File.ReadAllLines(configFile).ToList();
            SetValues(lines);
            readIP(lines, nameToIp, ipToName);
            StartNCC();
            StartCloud();
            Menu();
        }

        private void StartCloud()
        {
            // Connect to a CloudCable.
            try
            {
                cloudSocket = new Socket(cloudAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                cloudSocket.Connect(new IPEndPoint(cloudAddress, cloudPort));
                //Send "hello" message with its name to the cloud
                cloudSocket.Send(Encoding.ASCII.GetBytes($"{hostName} : hello"));
                Console.WriteLine("[{0}:{1}:{2}.{3}] Sending 'hello' to cabe cloud",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                byte[] response = new byte[1024];
                cloudSocket.Receive(response);
                Console.WriteLine("[{0}:{1}:{2}.{3}] Message from cable cloud:  " + Encoding.ASCII.GetString(response),
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                Task.Run(() => ListenToCloud());
                Menu();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void StartNCC()
        {
            try
            {
                NCCSocket = new Socket(NCCAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);
                NCCSocket.Connect(new IPEndPoint(NCCAddress, NCCPort));
                NCCSocket.Send(Encoding.ASCII.GetBytes($"{hostName} : hello"));
                Console.WriteLine("[{0}:{1}:{2}.{3}] Sending 'hello' to NCC",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond); //To hello będzie wysłane z pierwszą wiadomością

                Task.Run(() => ListenToNCC());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void SetTimer(int period)
        {
            aTimer = new System.Timers.Timer(period * 1000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            Console.WriteLine("{0}:{1}:{2}.{3} Sending message to: {4}", DateTime.Now.Hour, DateTime.Now.Minute,
                DateTime.Now.Second, DateTime.Now.Millisecond, dest_host);
            cloudSocket.Send(dp.convertToByte());
        }

        public void Menu()
        {
            IPAddress dest;
            int action = 0;
            Console.WriteLine("\tMENU");
            Console.WriteLine("1. Send a message");
            Console.WriteLine("2. Clean console window");
            Console.WriteLine("3. Close connection");
            try
            {
                action = Convert.ToInt32(Console.ReadLine());
                if (action == 1)
                {
                    Boolean is_connected;
                    List<string> keyList = new List<string>(this.nameToIp.Keys);
                    Console.WriteLine("Write a message");
                    string message = Console.ReadLine();
                    Console.WriteLine($"Select the target host from folowing: ");
                    keyList.ForEach(i => Console.Write("{0} ", i));
                    Console.WriteLine("");
                    dest_host = Console.ReadLine();
                    nameToIp.TryGetValue(dest_host, out dest);
                    dp = new DataPackage(message, address, dest, port);
                    if (!keyList.Contains(dest_host))
                    {
                        Console.WriteLine("Non existing host");
                        Menu();
                    }
                    nodeIsConnected.TryGetValue(dest_host, out is_connected);
                    if (!is_connected)
                    {
                        Console.WriteLine("Select preffered capacity: 50, 100, 150 [Gb/s]");
                        try
                        {
                            capacity = Convert.ToInt32(Console.ReadLine());
                            if (capacity != 50 && capacity != 100 && capacity != 150)
                            {
                                Console.WriteLine("Non existing capacity");
                                Menu();

                            }
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Wrong format. \nTo select the capacity write a number 50, 100 or 150");
                            Menu();
                        }

                        //Wysłanie wiadomości z requestem do NCC
                        CallRequest cr = new CallRequest(address, dest, capacity);
                        NCCSocket.Send(cr.convertToByte());
                        string dest_name;
                        ipToName.TryGetValue(dest, out dest_name);
                        Console.WriteLine("[{0}:{1}:{2}.{3}] [CPCC] Sending CallRequest_req(source: {4}, destination: {5}, capacity: {6})",
                            DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, hostName, dest_name, capacity);
                    }
                    else
                    {
                        List<int> list = new List<int>();
                        nodeToFsu.TryGetValue(dest_host, out list);
                        dp.firstFSU = list[0];
                        dp.secondFSU = list[1];
                        Console.WriteLine("[{0}:{1}:{2}.{3} Sending the message to host {4}", DateTime.Now.Hour, DateTime.Now.Minute,
                              DateTime.Now.Second, DateTime.Now.Millisecond, dest_host);
                        cloudSocket.Send(dp.convertToByte());
                        Menu();
                    }
                    Menu();
                }
                else if (action == 2)
                {
                    Console.Clear();
                    Menu();
                }
                else if (action == 3)
                {
                    try
                    {
                        List<string> nodesConnected = new List<string>();
                        Console.WriteLine("Choose host: ");
                        foreach (var key in nodeIsConnected.Keys)
                        {
                            if (nodeIsConnected[key])
                            {
                                Console.Write(key);
                                nodesConnected.Add(key);
                            }
                        }
                        Console.WriteLine();
                        string nodeName = Console.ReadLine();
                        if (nodeIsConnected.Keys.Contains(nodeName))
                        {
                            List<int> list = new List<int>();
                            nodeToFsu.TryGetValue(nodeName, out list);
                            int firstFSU1 = list[0];
                            int secondFSU2 = list[1];
                            Console.WriteLine("[{0}:{1}:{2}.{3} [CPCC] Sending CallTeardown_req({4}, {5})", DateTime.Now.Hour, DateTime.Now.Minute,
                                  DateTime.Now.Second, DateTime.Now.Millisecond, hostName, nodeName);
                            string message = "HOSTCLOSE#" + this.hostName + "#" + nodeName + "#" + firstFSU1 + "#" + secondFSU2 + "#";
                            NCCSocket.Send(Encoding.ASCII.GetBytes(message));
                            nodeIsConnected[nodeName] = false;
                            Menu();
                        }
                        else
                        {
                            Console.WriteLine("Wrong host name");
                            Menu();
                        }

                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Wrong format");
                        Menu();
                    }
                    
                    Menu();
                }
                else if (action == 4) //Odsyłam ze chce
                {
                    NCCSocket.Send(Encoding.ASCII.GetBytes("Accept"));
                    Console.WriteLine("[{0}:{1}:{2}.{3}] [CPCC] Sending CallAccept_rsp(\"Accepted\" source: {4}, destination: {5})", DateTime.Now.Hour, DateTime.Now.Minute,
                           DateTime.Now.Second, DateTime.Now.Millisecond, sourceHostAccept, hostName);
                    Menu();
                }
                else if (action == 5) //Odsylam ze nie chce
                {
                    NCCSocket.Send(Encoding.ASCII.GetBytes("Discard"));
                    Menu();
                }
                else
                {
                    Console.WriteLine("Non existing action");
                    Menu();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Wrong format. \nTo select the action, write a number from 1 to 4.");
                Menu();
            }
        }

        private void ListenToCloud()
        {
            byte[] receiveByte = new byte[1024];
            string sourceNodeName = string.Empty;
            while (true)
            {
                try
                {
                    cloudSocket.Receive(receiveByte);
                    DataPackage dp = DataPackage.convertToPackage(receiveByte);
                    if (receiveByte.Length != 0)
                    {
                        ipToName.TryGetValue(dp.source, out sourceNodeName);
                        Console.WriteLine("[{0}:{1}:{2}.{3}] Received message from {4} : {5}", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                            DateTime.Now.Millisecond, sourceNodeName, dp.message);
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.Shutdown)
                    {
                        Console.WriteLine("[{0}:{1}:{2}.{3}] The conncetion to the cable cloud was lost", DateTime.Now.Hour, DateTime.Now.Minute,
                            DateTime.Now.Second, DateTime.Now.Millisecond);
                    }
                }
            }
        }

        private void ListenToNCC()
        {
            byte[] receiveByte = new byte[1024];
            string sourceNodeName = string.Empty;

            while (true)
            {
                try
                {
                    //Wysyła nam informacje o szczelinach, które mamy użyć - wsadzamy to do pakietu
                    NCCSocket.Receive(receiveByte);
                    if (receiveByte.Length != 0)
                    {
                        if (Encoding.ASCII.GetString(receiveByte).Contains("CallAccept"))
                        {
                            var message = Encoding.ASCII.GetString(receiveByte).Split('#');
                            Console.WriteLine("[{0}:{1}:{2}.{3}] [CPCC] Received CallAccept_req(source: {4}, destination: {5}, capacity: {6}) ", DateTime.Now.Hour, DateTime.Now.Minute,
                           DateTime.Now.Second, DateTime.Now.Millisecond, message[1], hostName, message[2]);
                            sourceHostAccept = message[1];
                            capacity = Int32.Parse(message[2]);
                            Console.WriteLine("Do you want to receive call from {0}? \n" +
                                "Press 4 if Yes, Press 5 if No", message[1]);
                        }
                        else if (Encoding.ASCII.GetString(receiveByte).Contains("CON_DELETE"))
                        {
                            var parts = Encoding.ASCII.GetString(receiveByte).Split('#');
                            string slot1 = parts[1];
                            string slotLast = parts[2];
                            int firstslot = Convert.ToInt32(slot1);
                            int lastSlot = Convert.ToInt32(slotLast);
                            int iter = 0;
                            string usun = "";
                            foreach (string i in nodeToFsu.Keys)
                            {
                                foreach (int j in nodeToFsu[i])
                                {
                                    if (j == firstslot || j == lastSlot)
                                        iter++;
                                }
                                if (iter == 2)
                                {
                                    usun = i;
                                    nodeIsConnected[i] = false;
                                    iter = 0;
                                    break;
                                }
                            }
                            List<int> listUsun = new List<int>();
                            bool a = nodeToFsu.TryRemove(usun, out listUsun);
                        }
                        else
                        {
                            ConnectionResponse cr = ConnectionResponse.convertToResp(receiveByte);
                            Console.WriteLine("[{0}:{1}:{2}.{3}] [CPCC] Received CallRequest_rsp(\"Accepted\" destination: {6}, firstSlot: {4}, secondSlot: {5})", DateTime.Now.Hour,
                                DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, cr.firstFSU, cr.secondFSU, dest_host);
                            nodeIsConnected[dest_host] = true;
                            dp.firstFSU = cr.firstFSU;
                            dp.secondFSU = cr.secondFSU;
                            List<int> slots = new List<int>();
                            slots.Add(cr.firstFSU);
                            slots.Add(cr.secondFSU);
                            nodeToFsu.TryAdd(dest_host, slots);
                            Console.WriteLine("[{0}:{1}:{2}.{3} Sending the message to host {4}", DateTime.Now.Hour, DateTime.Now.Minute,
                                DateTime.Now.Second, DateTime.Now.Millisecond, dest_host);
                            Thread.Sleep(1000);
                            cloudSocket.Send(dp.convertToByte());
                        }
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.Shutdown)
                    {
                        Console.WriteLine("[{0}:{1}:{2}.{3}] The connection was lost", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second,
                            DateTime.Now.Millisecond);
                    }
                }
            }
        }

        public void SetValues(List<string> lines)
        {
            this.hostName = GetValueFromConfig("HOST_NAME", lines);
            Console.Title = hostName;
            Console.SetWindowSize(60, 13);
            this.address = IPAddress.Parse(GetValueFromConfig("ADDRESS", lines));
            this.cloudAddress = IPAddress.Parse(GetValueFromConfig("CLOUD_ADDRESS", lines));
            this.cloudPort = Convert.ToInt32(GetValueFromConfig("CLOUD_PORT", lines));
            this.port = Convert.ToInt32(GetValueFromConfig("PORT_ADDRESS", lines));
            this.NCCAddress = IPAddress.Parse(GetValueFromConfig("NCC_ADDRESS", lines));
            this.NCCPort = Convert.ToInt32(GetValueFromConfig("NCC_PORT", lines)); ;
        }

        public string GetValueFromConfig(string name, List<string> lines)
        {
            string[] entries;
            entries = lines.Find(line => line.StartsWith(name)).Split(' ');
            return entries[1];
        }

        private void readIP(List<string> lines, ConcurrentDictionary<string, IPAddress> nameToIp, ConcurrentDictionary<IPAddress, string> ipToName)
        {
            foreach (var line in lines.FindAll(line => line.StartsWith("ROW")))
            {
                string[] entries;
                entries = line.Split(' ');
                nameToIp.TryAdd(entries[1], IPAddress.Parse(entries[2]));
                ipToName.TryAdd(IPAddress.Parse(entries[2]), entries[1]);
                nodeIsConnected.TryAdd(entries[1], false);
            }
        }

    }
}