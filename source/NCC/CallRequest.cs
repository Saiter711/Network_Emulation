using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NCC
{
    class CallRequest
    {
        public IPAddress source;
        public IPAddress destination;
        public int capacity;
        public int firstFSU;
        public int secondFSU;
        public String Req;

        public CallRequest(IPAddress source, IPAddress destination, int capacity)
        {
            this.source = source;
            this.destination = destination;
            this.capacity = capacity;
        }

        public CallRequest()
        {
        }

        public byte[] convertToByte()
        {
            List<byte> new_list = new List<byte>();
            new_list.AddRange(Encoding.ASCII.GetBytes("Connect"));
            new_list.AddRange(source.GetAddressBytes());
            new_list.AddRange(destination.GetAddressBytes());
            new_list.AddRange(BitConverter.GetBytes(capacity));
            return new_list.ToArray();
        }

        public static CallRequest convertToRequest(byte[] bytes)
        {
            CallRequest cr = new CallRequest();
            byte[] address = new byte[] { bytes[7], bytes[8], bytes[9], bytes[10] };
            cr.source = new IPAddress(address);
            address = new byte[] { bytes[11], bytes[12], bytes[13], bytes[14] };
            cr.destination = new IPAddress(address);
            cr.capacity = BitConverter.ToInt32(bytes, 15);
            return cr;
        }

    }
}
