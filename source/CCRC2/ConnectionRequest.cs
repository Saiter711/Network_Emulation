using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace CCRC2
{

    class ConnectionRequest
    {
        public String Request;
        public int SubnetworkIn;
        public string SubnetworkWejsciePrzed;
        public string SubnetworkWyjsciePo;
        public int SubnetworkOut;
        public int id_polaczenia;


        public ConnectionRequest(String Request, int subnetworkIn, string subnetworkInJedenMniej, int subnetworkOut, string subnetworkpowyjsciu, int id)
        {
            this.Request = Request;
            this.SubnetworkIn = subnetworkIn;
            this.SubnetworkWejsciePrzed = subnetworkInJedenMniej;
            this.SubnetworkOut = subnetworkOut;
            this.SubnetworkWyjsciePo = subnetworkpowyjsciu;
            this.id_polaczenia = id;
        }

        public ConnectionRequest()
        {
        }

        public byte[] convertReqToByte()
        {
            List<byte> new_list = new List<byte>();
            new_list.AddRange(Encoding.ASCII.GetBytes(Request));
            new_list.AddRange(BitConverter.GetBytes(SubnetworkIn));
            new_list.AddRange(BitConverter.GetBytes(SubnetworkOut));
            new_list.AddRange(BitConverter.GetBytes(id_polaczenia));
            new_list.AddRange(Encoding.ASCII.GetBytes(SubnetworkWejsciePrzed));
            new_list.AddRange(Encoding.ASCII.GetBytes(SubnetworkWyjsciePo));
            return new_list.ToArray();
        }

        public static ConnectionRequest convertToConRequest(byte[] bytes)
        {
            ConnectionRequest cr = new ConnectionRequest();
            cr.SubnetworkIn = BitConverter.ToInt32(bytes, 5);
            cr.SubnetworkOut = BitConverter.ToInt32(bytes, 9);
            cr.id_polaczenia = BitConverter.ToInt32(bytes, 13);
            cr.SubnetworkWejsciePrzed = Encoding.ASCII.GetString(bytes, 17, 2);
            cr.SubnetworkWyjsciePo = Encoding.ASCII.GetString(bytes, 19, 2);
            return cr;
        }
    }
}
