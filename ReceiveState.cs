using System.Net;
using System.Net.Sockets;

namespace LockInitClient
{
    class ReceiveState
    {
        public ReceiveState(UdpClient client)
        {
            this.Client = client;
        }

        public UdpClient Client { get; private set; }
        public IPEndPoint Endpoint { get; private set; }
    }
}
