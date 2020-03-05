using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Controllers
{
    class CallResponse
    {
        public IPAddress source;
        public IPAddress destination;
        public int capacity;
        public int firstFSU;
        public int secondFSU;

        public CallResponse(IPAddress source, IPAddress destination, int capacity, int firstFSU, int secondFSU)
        {
            this.source = source;
            this.destination = destination;
            this.capacity = capacity;
            this.firstFSU = firstFSU;
            this.secondFSU = secondFSU;
        }

        public CallResponse()
        {
        }
        public static void write(CallResponse cr)
        {
            Console.WriteLine("Source: {0}, Dest: {1}, Cap: {2}, firstFSU: {3}, secondFSU: {4}",
                                            cr.source, cr.destination, cr.capacity, cr.firstFSU, cr.secondFSU);
        }
        public byte[] convertToByte()
        {
            List<byte> new_list = new List<byte>();
            new_list.AddRange(Encoding.ASCII.GetBytes("Resp")); //Po to żeby rozpoznać jakie wiadomości przychodzą do NCC
            new_list.AddRange(source.GetAddressBytes());
            new_list.AddRange(destination.GetAddressBytes());
            new_list.AddRange(BitConverter.GetBytes(capacity));
            new_list.AddRange(BitConverter.GetBytes(firstFSU));
            new_list.AddRange(BitConverter.GetBytes(secondFSU));
            return new_list.ToArray();
        }
        public static CallResponse convertToResp(byte[] bytes)
        {
            CallResponse cr = new CallResponse();
            byte[] address = new byte[] { bytes[4], bytes[5], bytes[6], bytes[7] };
            cr.source = new IPAddress(address);
            address = new byte[] { bytes[5], bytes[6], bytes[7], bytes[8] };
            cr.destination = new IPAddress(address);
            cr.capacity = BitConverter.ToInt32(bytes, 9);
            cr.firstFSU = BitConverter.ToInt32(bytes, 13);
            cr.secondFSU = BitConverter.ToInt32(bytes, 17);
            return cr;
        }
    }
}
