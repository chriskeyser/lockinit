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
    /*
     * Mages the interactions with the lock device over udp to complete the initialization
     * process.
     */
    public class LockInitHandler
    {
        private const int broadcastPort = 9998;
        private const int listenPort = 9997;
        private const string signature = "LINIT";
        private const string replySig = "LRPLY";

        private readonly UdpMessaging messaging;
        private readonly UdpClient broadcastClient;

        private Dictionary<String, IPEndPoint> discoveredDevices = new Dictionary<string, IPEndPoint>();
        private Action<string> deviceCallback;
        private Action<string> logger;

        /*
         * Constructs a lock handler.  The discoverCallback is called whenever a response to a broadcast
         * query is received, and the device has not previously been seen.  Status messages are reported back
         * via the log.
         */
        public LockInitHandler(Action<string> discoverCallback, Action<string> log)
        {
            broadcastClient = new UdpClient();
            deviceCallback = discoverCallback;
            logger = log;
            messaging = new UdpMessaging(listenPort, (udpMessage) =>
            {
                HandleReceiveMessage(udpMessage);
                return false;
            });
        }

        private void AddDevice(string deviceId, IPEndPoint devEndpoint)
        {
            deviceCallback(deviceId);
            discoveredDevices[deviceId] = devEndpoint;
            logger(string.Format("Discovered: {0}", deviceId));
        }

        private string GetDeviceForEndpoint(IPEndPoint endpoint)
        {
            string device = (from e in discoveredDevices where e.Value.Equals(endpoint) select e.Key).FirstOrDefault();
            return device;
        }

        private void LogDeviceState(IPEndPoint endpoint, string state)
        {
            string device = GetDeviceForEndpoint(endpoint);
            if (device != null)
            {
                logger(string.Format("state transition({0}) from {1}", state, device));
            }
            else
            {
                logger(string.Format("state transition({0}) from unknown device at endpoint {1}", state, endpoint));
            }
        }

        private void HandleReceiveMessage(UdpMessage message)
        {
            int startData = 0;
            int deviceState = ParseHeader(message.Data, out startData);

            switch((InitializationState)deviceState)
            { 
                case InitializationState.InitializeConfig:
                    string deviceId = ASCIIEncoding.ASCII.GetString(message.Data, startData, message.Data.Length - startData);
                    AddDevice(deviceId, message.Endpoint);
                    LogDeviceState(message.Endpoint, "initializing");
                    break;
                case InitializationState.TestingConfig:
                    LogDeviceState(message.Endpoint, "testing");
                    break;
                case InitializationState.Done:
                    LogDeviceState(message.Endpoint, "done");
                    break;
                case InitializationState.Running:
                    LogDeviceState(message.Endpoint, "running");
                    break;
                default:
                    LogDeviceState(message.Endpoint, string.Format("undefined state({0})", deviceState));
                    string err = ASCIIEncoding.ASCII.GetString(message.Data, startData, message.Data.Length - startData);
                    break;
            }
        }

        internal bool InitDevice(string device, string mqttServer, int mqttPort, byte[] key)
        {
            if(this.discoveredDevices.ContainsKey(device))
            {
                IPEndPoint devEndpoint = this.discoveredDevices[device];              
                List<Byte> msgData = new List<Byte>();
                byte highVal = (byte)((mqttPort & 0xFF00) >> 8);
                byte lowVal = (byte)mqttPort;

                msgData.AddRange(UTF8Encoding.ASCII.GetBytes(signature));
                msgData.Add((byte)InitializationState.InitializeConfig);
                msgData.Add(highVal);
                msgData.Add(lowVal);

                msgData.AddRange(key);
                msgData.AddRange(UTF8Encoding.ASCII.GetBytes(mqttServer));

                var message = new UdpMessage(msgData.ToArray(), devEndpoint);
                messaging.SendMessageAsync(message, sendSize =>
                {
                    logger(string.Format("Sent init"));
                });
            }
            return false;
        }

        private int ParseHeader(byte[] message, out int next)
        {
            int headerLength = signature.Length + sizeof(byte);
            next = 0;

            if (message.Length >= headerLength)
            {
                string header = ASCIIEncoding.ASCII.GetString(message, 0, replySig.Length);
                int stateInfo = (int) message[replySig.Length];

                if (Enum.IsDefined(typeof(InitializationState), stateInfo))
                {
                    if (!header.Equals(replySig))
                    {
                        logger(string.Format("ParseHeader: Incorrect init msg: {0}", header));
                    }
                    else
                    {
                        next = headerLength;
                        return (int)stateInfo;
                    }
                }
            }

            return -1;
        }

        public void QueryDevices()
        {
            List<Byte> msgData = new List<Byte>();
            msgData.AddRange(UTF8Encoding.ASCII.GetBytes(signature));
            msgData.Add((byte)InitializationState.Assigning);

            byte[] message = msgData.ToArray();
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
