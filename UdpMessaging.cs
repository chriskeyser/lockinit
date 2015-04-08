using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace LockInitClient
{
    /**
     * Manages messaging at the UDP level, sending and receiving sync or async.  If sync, then messages
     * are queued and client polls.  If async then a callback is provided for received messages and is
     * invoked when received.
     */
    class UdpMessaging
    {
        private readonly Queue<UdpMessage> receivedQueue;
        private IAsyncResult lastReceive;
        private UdpClient udpClient;
        private Func<UdpMessage, bool> msgReceiver;

        public UdpMessaging(int port)
        {
            receivedQueue = new Queue<UdpMessage>();
            udpClient = new UdpClient(port);

            var state = new ReceiveState(udpClient);
            lastReceive = udpClient.BeginReceive(ReceiveMessage, state);
        }

        /*
         * Constructs the messaging class. msgReceive is a callback for
         * messages that are received.
         */
        public UdpMessaging(int port, Func<UdpMessage, bool> msgReceiver) :
        this(port)
        {
            this.msgReceiver = msgReceiver;
        }

        /**
         * Callback handler for an async received message.
         */
        public void ReceiveMessage(IAsyncResult ar)
        {
            if (ar == lastReceive)
            {
                var state = (ReceiveState)ar.AsyncState;
                byte[] initReply;
                IPEndPoint deviceIP = new IPEndPoint(IPAddress.Any, 0);

                initReply = state.Client.EndReceive(ar, ref deviceIP);
                string msg = UTF8Encoding.ASCII.GetString(initReply);
                var message = new UdpMessage(msg, deviceIP);

                if (msgReceiver != null)
                {
                    if (msgReceiver(message))
                    {
                        receivedQueue.Enqueue(message);
                    }
                }
                else
                {
                    receivedQueue.Enqueue(message);
                }
                lastReceive = udpClient.BeginReceive(ReceiveMessage, state);
            }
            else
            {
                System.Diagnostics.Trace.TraceWarning("UpdMessaging::Received invalid async result (shutting down?)");
            }
        }

        public bool HasMessage()
        {
            return receivedQueue.Count > 0;
        }

        public UdpMessage GetMessage()
        {
            if(!HasMessage())
            {
                throw new InvalidOperationException("Reading from an empty queue");
            }
            return receivedQueue.Dequeue();
        }

        public void SendMessage(UdpMessage sendMessage)
        {
            udpClient.Send(sendMessage.Data, sendMessage.Data.Length, sendMessage.Endpoint);
        }

        public void SendMessageAsync(UdpMessage sendMessage, Action<int> callback)
        {
            udpClient.SendAsync(sendMessage.Data, sendMessage.Data.Length, sendMessage.Endpoint).ContinueWith(task => 
            {
                if (task.IsCompleted)
                {
                    int result = task.Result;
                    callback(result);
                }
                else
                {
                    if (task.Exception != null)
                    {
                        System.Diagnostics.Trace.TraceError("failed on UdpMessaging::SendMesageAsync {0}", task.Exception.ToString());
                    }
                    else
                    {
                        System.Diagnostics.Trace.TraceError("failed on UdpMessaging::SendMesageAsync faulted: {0} completed: {1}", 
                            task.IsFaulted, task.IsCanceled);
                    }
                }
            });
        }

        public void Stop()
        {
            udpClient.Close();
        }
    }
}
