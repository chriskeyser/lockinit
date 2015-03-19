using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace LockInitClient
{
    public class UdpMessage
    {
        public string Data { get; private set; }
        public IPEndPoint Endpoint { get; private set; }

        public UdpMessage(string message, IPEndPoint from)
        {
            this.Data = message;
            this.Endpoint = from;
        }
    }
}
