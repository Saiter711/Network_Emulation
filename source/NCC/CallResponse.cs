using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NCC
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

        public static void write(CallResponse cr)
        {
            Console.WriteLine("Source: {0}, Dest: {1}, Cap: {2}, firstFSU: {3}, secondFSU: {4}",
                cr.source, cr.destination, cr.capacity, cr.firstFSU, cr.secondFSU);
        }

        public CallResponse()
        {
        }

        public static CallResponse convertToResp(byte[] bytes)
        {
            CallResponse cr = new CallResponse();
            byte[] address = new byte[] { bytes[4], bytes[5], bytes[6], bytes[7] };
            cr.source = new IPAddress(address);
            address = new byte[] { bytes[8], bytes[9], bytes[10], bytes[11] };
            cr.destination = new IPAddress(address);
            cr.capacity = BitConverter.ToInt32(bytes, 12);
            cr.firstFSU = BitConverter.ToInt32(bytes, 16);
            cr.secondFSU = BitConverter.ToInt32(bytes, 20);
            return cr;
        }
    }
}