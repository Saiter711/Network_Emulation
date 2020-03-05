using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CableCloud
{
    class CloudConfig
    {
        private Dictionary<ConnectionPoint, ConnectionPoint> linkTable;
        private Dictionary<string, string> NodeNameToIP;
        private Dictionary<string, string> IPToNodeName;

        public CloudConfig(StreamReader file)
        {
            //H1 ip
            //klucz to nazwa czyli H1
            NodeNameToIP = new Dictionary<string, string>();

            //klucz to adres ip
            IPToNodeName = new Dictionary<string, string>();

            // 100.0.0.0 1234 , 123.123.123.123 1233
            linkTable = new Dictionary<ConnectionPoint, ConnectionPoint>();


            string line = string.Empty;
            while (!(line = file.ReadLine()).Equals(""))
            {
                //H1 x.x.x.x
                var parts = line.Split(' ');
                NodeNameToIP.Add(parts[0], parts[1]);

                IPToNodeName.Add(parts[1], parts[0]);
            }
            //3 linie przerwy

            file.ReadLine(); file.ReadLine();

            while ((line = file.ReadLine()) != null)
            {
                //H1:1234 R1:4000
                var parts = line.Split(' ');

                string node1 = parts[0];
                int port1 = int.Parse(parts[1]);
                string node2 = parts[2];
                int port2 = int.Parse(parts[3]);

                linkTable.Add(new ConnectionPoint(node1, port1), new ConnectionPoint(node2, port2));
                linkTable.Add(new ConnectionPoint(node2, port2), new ConnectionPoint(node1, port1));
            }
        }

        public ConnectionPoint GetNextConnectionPoint(ConnectionPoint cp)
        {
            foreach (var element in linkTable)
            {
                if (element.Key.ipAddress == cp.ipAddress && element.Key.Port == cp.Port)
                {
                    return element.Value;
                }
            }
            return new ConnectionPoint("bad address", 0);
        }

        public string getNodeIP(string nodeName)
        {
            return NodeNameToIP[nodeName];
        }

        public string getNodeName(string nodeIP)
        {
            return IPToNodeName[nodeIP];
        }

    }



}
