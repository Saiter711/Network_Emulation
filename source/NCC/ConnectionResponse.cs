using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NCC
{
    class ConnectionResponse
    {
        public string header;
        public int firstFSU;
        public int secondFSU;
        public string sender;
        public int package_length;
        public int info_length = 32;

        public ConnectionResponse(int firstFSU, int secondFSU, string sender)
        {
            this.header = "Response";
            this.firstFSU = firstFSU;
            this.secondFSU = secondFSU;
            this.sender = sender;
            this.package_length = info_length + sender.Length;
        }

        public ConnectionResponse()
        {
            this.header = "Response";
            this.sender = "";
        }

        public static ConnectionResponse convertToResp(byte[] bytes)
        {
            ConnectionResponse cr = new ConnectionResponse();
            cr.firstFSU = BitConverter.ToInt32(bytes, 8);
            cr.secondFSU = BitConverter.ToInt32(bytes, 12);
            cr.package_length = BitConverter.ToInt32(bytes, 16);
            byte[] array = new byte[cr.package_length - cr.info_length];
            for (int i = 0; i < cr.package_length - cr.info_length; i++)
                array[i] = bytes[20 + i];
            cr.sender = Encoding.ASCII.GetString(array);
            return cr;
        }

        public byte[] convertToByte()
        {
            List<byte> new_list = new List<byte>();
            new_list.AddRange(Encoding.ASCII.GetBytes(header));
            new_list.AddRange(BitConverter.GetBytes(firstFSU));
            new_list.AddRange(BitConverter.GetBytes(secondFSU));
            new_list.AddRange(BitConverter.GetBytes(package_length));
            new_list.AddRange(Encoding.ASCII.GetBytes(sender));
            return new_list.ToArray();
        }
    }
}
