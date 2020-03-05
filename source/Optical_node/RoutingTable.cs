using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalNode
{
    class RoutingTable
    {
        public ConcurrentBag<TableRow> table { get; set; }
        public RoutingTable()
        {
            table = new ConcurrentBag<TableRow>();
        }
    }
}
