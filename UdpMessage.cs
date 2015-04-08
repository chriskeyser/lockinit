using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace LockInitClient
{
    /*
     * Represents the data and endpoint for a message sent or received via udp.
    */
    public class UdpMessage
    {
        private String strData;

        public String StringData
        {
            get
            {
                if (strData == null)
                {
                    strData = ASCIIEncoding.ASCII.GetString(Data);
                }
                return strData;
            }
        }

        public byte[] Data { get; private set; }
        public IPEndPoint Endpoint { get; private set; }

        public UdpMessage(string message, IPEndPoint from)
        {
            this.Data = UTF8Encoding.ASCII.GetBytes(message);
            this.Endpoint = from;
        }

        public UdpMessage(byte[] message, IPEndPoint from)
        {
            this.Data = message;
            this.Endpoint = from;
        }
    }
}
