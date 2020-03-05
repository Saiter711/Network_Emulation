using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpticalNode
{
    class TableRow
    {
        public int in_port;
        public int freq_slot1;
        public int freq_slot2;
        public int out_port;

        public TableRow(String[] parts)
        {
            this.in_port = Int32.Parse(parts[0]);
            this.freq_slot1 = Int32.Parse(parts[1]);
            this.freq_slot2 = Int32.Parse(parts[2]);
            this.out_port = Int32.Parse(parts[3]);
        }
        public TableRow(int inport, int freq_slot1, int freq_slot2, int out_port)
        {
            this.in_port = inport;
            this.freq_slot1 = freq_slot1;
            this.freq_slot2 = freq_slot2;
            this.out_port = out_port;
        }
    }
}
