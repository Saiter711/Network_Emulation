using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

namespace OpticalNode
{
    class OpticalNode
    {
        private string nodeName { set; get; }
        private IPAddress nodeAddress { set; get; }
        private IPAddress cloudAddress { set; get; }
        public IPAddress controllersAddress { set; get; }
        private int cloudPort { set; get; }
        public int controllersPort { set; get; }
        string Data;
        Socket controllersSocket;
        Socket cloudSocket;
        private ConcurrentBag<TableRow> routing_table;

        List<string> lines;
        public OpticalNode(string configFile)
        {
            routing_table = new ConcurrentBag<TableRow>();
            ReadConfigFile(configFile);
            StartCloud();
            StartController();
            ShutDownLink();
        }
        private void ReadConfigFile(string configFile)
        {
            lines = File.ReadAllLines(configFile).ToList();
            this.nodeName = GetValueFromConfig("NODE");
            Console.Title = nodeName;
            Console.SetWindowSize(40, 7);
            this.nodeAddress = IPAddress.Parse(GetValueFromConfig("NODE_ADDRESS"));
            this.cloudAddress = IPAddress.Parse(GetValueFromConfig("CLOUD_ADDRESS"));
            this.cloudPort = Convert.ToInt32(GetValueFromConfig("CLOUD_PORT"));
            this.controllersAddress = IPAddress.Parse(GetValueFromConfig("CONTROLLER_ADDRESS"));
            this.controllersPort = Convert.ToInt32(GetValueFromConfig("CONTROLLER_PORT"));
        }
        private string GetValueFromConfig(string name)
        {
            string[] entries;
            entries = lines.Find(line => line.StartsWith(name)).Split(' ');
            return entries[1];
        }

        private void StartCloud()
        {
            IPEndPoint cloudRemoteEP = new IPEndPoint(cloudAddress, cloudPort);
            cloudSocket = new Socket(cloudAddress.AddressFamily,
                       SocketType.Stream, ProtocolType.Tcp);
            cloudSocket.Connect(cloudRemoteEP);
            cloudSocket.Send(Encoding.ASCII.GetBytes($"{nodeName} : hello"));
            Console.WriteLine("[{0}:{1}:{2}.{3}] Sending 'hello' message to cable cloud",
               DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
            byte[] CloudResponse = new byte[1024];
            cloudSocket.Receive(CloudResponse);
            Console.WriteLine("[{0}:{1}:{2}.{3}] Received message from cable cloud:  " + Encoding.ASCII.GetString(CloudResponse),
                DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
            Task.Run(() => ListenToCloud());
        }

        private void ListenToCloud()
        {
            Byte[] receiveByte = new Byte[1024];
            string sourceNodeName = string.Empty;
            DataPacket data = new DataPacket();
            DataPacket proccessedData = new DataPacket();
            while (true)
            {
                try
                {
                    cloudSocket.Receive(receiveByte);
                    if (receiveByte.Length != 0)
                    {
                        Console.WriteLine("[{0}:{1}:{2}.{3}] Got a message at port:{4}",
                            DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, DataPacket.convertToPackage(receiveByte).port);

                        proccessedData = HandleIncomingPacket(DataPacket.convertToPackage(receiveByte));
                        if (proccessedData == null)
                        {
                            continue;
                        }
                        else if(proccessedData.port == -1)
                        {
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("[{0}:{1}:{2}.{3}] Sending message through port:{4}",
                                DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, proccessedData.port);
                            cloudSocket.Send(proccessedData.convertToByte());
                        }
                    }
                    Thread.Sleep(10);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.Shutdown)
                    {
                        Console.WriteLine("[{0}:{1}:{2}.{3}] Connection with Cloud broken!", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
                    }
                }
                Array.Clear(receiveByte, 0, receiveByte.Length);
            }
        }

        private void StartController()
        {
            try
            {
                IPEndPoint controllerRemoteEP = new IPEndPoint(controllersAddress, controllersPort);
                // Connect to Controller
                controllersSocket = new Socket(controllersAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                controllersSocket.Connect(controllerRemoteEP);
                controllersSocket.Send(Encoding.ASCII.GetBytes($"{nodeName} : hello"));
                Console.WriteLine("[{0}:{1}:{2}.{3}] Sending 'Hello' message to CCRC",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                byte[] controllersResponse = new byte[1024];

                controllersSocket.Receive(controllersResponse);

                Console.WriteLine("[{0}:{1}:{2}.{3}] Connection with CCRC established",
                    DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                Data = Encoding.ASCII.GetString(controllersResponse);
                Task.Run(() => ListenToControllers());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void ListenToControllers()
        {
            byte[] receiveByte = new byte[1024];
            string sourceNodeName = string.Empty;
            string message = string.Empty;
            while (true)
            {
                try
                {
                    controllersSocket.Receive(receiveByte);
                    message = Encoding.ASCII.GetString(receiveByte);
                    if (receiveByte.Length != 0)
                    {
                        if (message.Contains("ADD_ROW"))
                        {
                            var parts = message.Split('#');
                            int in_port = Int32.Parse(parts[1]);
                            int freq_slot1 = Int32.Parse(parts[2]);
                            int freq_slot2 = Int32.Parse(parts[3]);
                            int out_port = Int32.Parse(parts[4]);
                            Console.WriteLine("[{0}:{1}:{2}.{3}] Received ConnectionRequest_req(in_port: {4} first_slot: {5}, last_slot: {6}, out_port: {7}, setup) from CC", DateTime.Now.Hour, DateTime.Now.Minute,
                                 DateTime.Now.Second, DateTime.Now.Millisecond, in_port.ToString(), freq_slot1, freq_slot2, out_port);

                            routing_table.Add(new TableRow(in_port, freq_slot1, freq_slot2, out_port));
                            Console.WriteLine("[{0}:{1}:{2}.{3}] Sending ConnectionRequest_resp(in_port: {4} first_slot: {5}, last_slot: {6}, out_port: {7}, setup) to CC", DateTime.Now.Hour, DateTime.Now.Minute,
                                DateTime.Now.Second, DateTime.Now.Millisecond, in_port.ToString(), freq_slot1, freq_slot2, out_port);
                            string messageS = "Adding_a_row#" + in_port.ToString() + '#'+ freq_slot1 + '#' + freq_slot2 + '#' + out_port + '#' + nodeName;
                            controllersSocket.Send(Encoding.ASCII.GetBytes(messageS));
                        }

                        else if (message.Contains("DELETE_ROW"))
                        {
                            Console.WriteLine("[{0}:{1}:{2}.{3}] Received message from controllers:  DELETE_ROW",
                                DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                            var parts = message.Split('#');
                            int in_port = Int32.Parse(parts[1]);
                            int freq_slot1 = Int32.Parse(parts[2]);
                            int freq_slot2 = Int32.Parse(parts[3]);
                            int out_port = Int32.Parse(parts[4]);
                            bool success = false;

                            foreach (var row in routing_table)
                            {
                                if (row.in_port == in_port)
                                {
                                    if (row.freq_slot1 == freq_slot1)
                                    {
                                        if (row.freq_slot2 == freq_slot2)
                                        {
                                            if (row.out_port == out_port)
                                                routing_table = new ConcurrentBag<TableRow>(routing_table.Except(new[] { row }));
                                            success = true;
                                        }
                                    }
                                }
                            }

                            if (success == true)
                            {
                                controllersSocket.Send(Encoding.ASCII.GetBytes("The row was successfully deleted from the routing table"));
                            }
                            else
                            {
                                controllersSocket.Send(Encoding.ASCII.GetBytes("Deleting the row from the routing table failed"));
                            }
                        }
                        else if (message.Contains("DELETE_FSU"))
                        {
                            var parts = message.Split('#');
                            int firstFSU = Int32.Parse(parts[1]);
                            int secondFSU = Int32.Parse(parts[2]);
                            delete_FSU(firstFSU, secondFSU);
                        }
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.Shutdown)
                    {
                        Console.WriteLine("[{0}:{1}:{2}.{3}] Connection with Controllers is broken!", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
                    }
                }
                Array.Clear(receiveByte, 0, receiveByte.Length);
            }
        }

        public DataPacket HandleIncomingPacket(DataPacket incdata)
        {
            bool success = false;
            if (routing_table.IsEmpty)
            {
                Console.WriteLine("Cannot route this packet");
                incdata.port = -1;
                return incdata;
            }
            foreach (var row in routing_table)
            {
                if (row.in_port == incdata.port)
                {
                    if (row.freq_slot1 == incdata.firstFSU)
                    {
                        if (row.freq_slot2 == incdata.secondFSU)
                        {
                            incdata.port = row.out_port;
                            success = true;
                            break;
                        }
                    }
                }
            }
            if (success == false)
            {
                Console.WriteLine("Cannot route this packet");
                incdata.port = -1;
                return incdata;
            }
            else
            {
                return incdata;
            }
        }

        private void ShutDownLink()
        {
            Console.ReadLine();
            Console.WriteLine("Insert a neighbor node name to shutdown link between the nodes.");
            string secondNodeName = Console.ReadLine();
            if (secondNodeName[0].Equals('H') || secondNodeName[0].Equals('N'))
            {
                if (char.IsDigit(secondNodeName[1]))
                {
                    string message = "SHUTDOWN_LINK#" + secondNodeName + '#' + nodeName.ToString();
                    Console.WriteLine("[{0}:{1}:{2}.{3}] Sending SHUTDOWN_LINK to CCRC",
                        DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
                    controllersSocket.Send(Encoding.ASCII.GetBytes(message));
                }
            }
            else
                Console.WriteLine("Bad node name");
            ShutDownLink();
        }

        private void delete_FSU(int firstFSU, int secondFSU)
        {
            foreach (var row in routing_table)
            {
                if (row.freq_slot1.Equals(firstFSU))
                {
                    if (row.freq_slot2.Equals(secondFSU))
                    {
                        Console.WriteLine("[{0}:{1}:{2}.{3}] Got message: DELETE_FSU#{4}#{5}",
                            DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond, firstFSU, secondFSU);
                        routing_table = new ConcurrentBag<TableRow>(routing_table.Except(new[] { row }));
                        break;
                    }
                }
            }
        }
    }
}
    

