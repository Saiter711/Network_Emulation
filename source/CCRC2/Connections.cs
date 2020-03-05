using System;
using System.Collections.Generic;
using System.Text;

namespace CCRC2
{
    class Connections
    {
        public string firstNode;
        public string secondNode;
        public int firstPort;
        public int secondPort;
        public Connections(string firstnode, string secondnode, int firstport, int secondport)
        {
            firstNode = firstnode;
            secondNode = secondnode;
            firstPort = firstport;
            secondPort = secondport;
        }
    }
}
