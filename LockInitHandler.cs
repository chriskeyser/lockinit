using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LockInitClient
{
    public class LockInitHandler
    {
        private const int broadcastPort = 9998;
        private const int listenPort = 9997;
        private const string signature = "LINIT";
        private const string replySig = "LRPLY";
        private char[] msgDelim = { ':' };

        private readonly StateInformation state;
        private readonly UdpMessaging messaging;
        private readonly UdpClient broadcastClient;

        private Dictionary<String, IPEndPoint> discoveredDevices = new Dictionary<string, IPEndPoint>();
        private Action<string> deviceCallback;
        private Action<string> logger;

        public LockInitHandler(Action<string> discoverCallback, Action<string> log)
        {
            state  = new StateInformation();
            broadcastClient = new UdpClient();
            deviceCallback = discoverCallback;
            logger = log;
            messaging = new UdpMessaging(listenPort, (udpMessage) =>
            {
                HandleReceiveMessage(udpMessage);
                return false;
            });
        }

        private void HandleReceiveMessage(UdpMessage message)
        {
            string[] msgParts;
            int deviceState = ParseHeader(message.Data, out msgParts);

            if (state.CurrentState == InitializationState.Assigning && deviceState == (int)InitializationState.Assigning)
            {
                if (msgParts.Length == 3)
                {
                    deviceCallback(msgParts[2]);
                    discoveredDevices[msgParts[2]] = message.Endpoint;
                    logger(string.Format("Discovered: {0}", msgParts[2]));
                }
                else
                {
                    logger(string.Format("Protocol Error in InitializeConfig, expected 3 message parts, got: {0}", msgParts.Length));
                }
            }
        }

        internal bool InitDevice(string device, string mqttServer, int mqttPort)
        {
            if(this.discoveredDevices.ContainsKey(device))
            {
                IPEndPoint devEndpoint = this.discoveredDevices[device];
                var configMsg = string.Format("{0}:{1}:{2}:{3}", signature, (int)InitializationState.InitializeConfig, mqttServer, mqttPort);
                var message = new UdpMessage(configMsg, devEndpoint);
                messaging.SendMessageAsync(message, sendSize =>
                {
                    logger(string.Format("Sent init"));
                });
            }

            return false;
        }

        private int ParseHeader(string message, out string[] parts)
        {
            parts = message.Split(msgDelim);

            if (parts.Length < 2 || !parts[0].Equals(replySig))
            {
                logger(string.Format("ParseHeader: Incorrect init msg: {0}", message));
            }
            else
            {
                int devState;
                if (int.TryParse(parts[1], out devState))
                {
                    return devState;
                }
            }

            return -1;
        }

        public void QueryDevices()
        {
            var initMsg = string.Format("{0}:{1}", signature, (int)InitializationState.Assigning);

            byte[] message = UTF8Encoding.ASCII.GetBytes(initMsg);
            var ep = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
            broadcastClient.SendAsync(message, message.Length, ep).ContinueWith( tr =>
            {
                if (tr.IsCompleted)
                {
                    logger("Sent broadcast");
                }
                else if(tr.IsFaulted)
                {
                    logger(string.Format("Failed send: {0}", tr.Exception.ToString()));
                }
            });
        }
    }
}
