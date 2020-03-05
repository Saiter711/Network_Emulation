using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Net;

namespace Host
{
    class DataPackage
    {
       
        public string message;
        public IPAddress source;
        public IPAddress destination;
        public int firstFSU;
        public int secondFSU;
        // wartosc tej zmiennej trzeba okreslic przy zmienianiu tablicy bytow na pakiet, jest to tak naprawde dlugosc wiadomości w bytach + zmienna withoutMessage
        public int package_length;
        public int port;
        public int withoutMessage = 24;
        public DataPackage(string message, IPAddress source, IPAddress destination, int port)
        {
            this.message = message;
            this.source = source;            // 4 bajty
            this.port = port;                // 4 bajty
            this.destination = destination;  // 4 bajty
            //this.firstFSU = firstFSU;        // 4 bajty
           // this.secondFSU = secondFSU;      // 4 bajty
            this.package_length = withoutMessage + message.Length;  // 4 bajty

        }
        public DataPackage() { }
        public byte[] convertToByte()
        {
            List<byte> new_list = new List<byte>();
            new_list.AddRange(BitConverter.GetBytes(firstFSU));
            new_list.AddRange(BitConverter.GetBytes(secondFSU));
            new_list.AddRange(source.GetAddressBytes());
            new_list.AddRange(destination.GetAddressBytes());
            new_list.AddRange(BitConverter.GetBytes(port));
            new_list.AddRange(BitConverter.GetBytes(package_length));
            new_list.AddRange(Encoding.ASCII.GetBytes(message));
            return new_list.ToArray();
        }

        public static DataPackage convertToPackage(byte[] bytes)
        {
            DataPackage dp = new DataPackage();
            //   dp.ls = LabelStack.toStack(bytes);
            // var length = dp.ls.getByteLength();
            dp.firstFSU = BitConverter.ToInt32(bytes,0);
            dp.secondFSU = BitConverter.ToInt32(bytes,4);
            byte[] address = new byte[] {bytes[8],bytes[9],bytes[10], bytes[11] };
            dp.source = new IPAddress(address);
            address = new byte[] { bytes[12], bytes[13], bytes[14], bytes[15] };
            dp.destination = new IPAddress(address);
            dp.port = BitConverter.ToInt32(bytes, 16);
            dp.package_length = BitConverter.ToInt32(bytes, 20);
            byte[] array = new byte[dp.package_length - dp.withoutMessage]; //package_length wiadomosc + dlugosc bez wiadomosci
            int j = 24;
            for (int i = 0; i < dp.package_length - dp.withoutMessage; i++)
                array[i] = bytes[j++];
            dp.message = Encoding.ASCII.GetString(array);
            return dp;
        }

    }
}
