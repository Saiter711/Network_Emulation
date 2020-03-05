using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CableCloud
{
    class ConnectionPoint
    {
        public string ipAddress { get; set; }
        public int Port { get; set; }
        public ConnectionPoint(string ipAddress, int port)
        {
            this.ipAddress = ipAddress;
            this.Port = port;
        }
    }
}
