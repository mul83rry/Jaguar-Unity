using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Jaguar.Core.Managers;
using Jaguar.Data;
using Jaguar.Manager;
using UnityEngine;

namespace Jaguar
{
    internal struct Received
    {
        internal IPEndPoint Sender;
        internal Packet Packet;
        internal byte[] BytesArray;

        public Received(string eventName) : this()
        {
            Packet = new Packet() { EventName = "UDPBYTES" };
        }
    }

    internal abstract class UdpBase
    {
        protected System.Net.Sockets.UdpClient Client;
        private bool _onErrorConnectingToServerInvoked;

        protected UdpBase()
        {
            Client = new System.Net.Sockets.UdpClient();
        }

        public async Task<Received> Receive()
        {
            try
            {
                var result = await Client.ReceiveAsync();

                var buffer = new byte[result.Buffer.Length - 1];
                Array.Copy(result.Buffer, 1, buffer, 0, result.Buffer.Length - 1);

                if (result.Buffer[0] == 0)
                {
                    return new Received
                    {
                        Packet = new Packet(buffer),
                        Sender = result.RemoteEndPoint
                    };
                }
                else // byte[] data
                {
                    return new Received("UDPBYTES") { BytesArray = buffer };
                }
            }
            catch (SocketException e)
            {
                if (_onErrorConnectingToServerInvoked)
                    return new Received();

                UdpClient.TryingToConnect = false;

                _onErrorConnectingToServerInvoked = true;

                JaguarClient.OnErrorConnectingToServer?.Invoke();
                UdpClient.ReceivedThreadCancellationToken.Cancel();
                return new Received();
            }
        }
    }

    internal class UdpClient
    {
        public UdpClient(string ip, int port)
        {
            this._ip = ip;
            this._port = port;
        }

        internal static bool Connected;
        internal static bool TryingToConnect;
        private readonly int _port;
        private readonly string _ip;
        internal static CancellationTokenSource ReceivedThreadCancellationToken { get; set; }


        internal static UdpUser Client { get; private set; }

        private async void ReceivedThread()
        {
            while (true)
            {
                try
                {
                    if (ReceivedThreadCancellationToken.IsCancellationRequested)
                        break;

                    var received = await Client.Receive();

                    if (received.Packet.EventName == "UDPBYTES")
                    {
                        Listener.OnNewBytesMessageReceived?.Invoke(received.BytesArray);
                        continue;
                    }

                    if (string.IsNullOrEmpty(received.Packet.EventName))
                        continue;

                    if (!received.Packet.Reliable)
                    {
                        lock (ListenersManager.PacketsToProcess)
                        {
                            ListenersManager.PacketsToProcess.Enqueue(received.Packet);
                        }
                    }
                    else
                    {
                        ReceiptManagement.Instance.ReceivedReliablePacket(received.Packet);
                    }

                }
                catch (Exception e)
                {
                    // ignored
                }
            }
            TryingToConnect = false;
            UdpUser.CloseConnection();
        }

        internal void Start()
        {
            if (TryingToConnect)
                return;

            TryingToConnect = true;

            Connected = false;

            PostManagement.Instance.Init();
            ReceiptManagement.Instance.Init();

            //create a new client
            Client = UdpUser.ConnectTo(_ip, _port);

            Listener.OnNewMessageReceived = null;
            Listener.OnNewMessageReceived += packet =>
            {
                lock (ListenersManager.PacketsToProcess)
                {
                    if (packet.Reliable)
                        ListenersManager.ReliablePackets.Enqueue(packet);
                    else
                        ListenersManager.PacketsToProcess.Enqueue(packet);
                }
            };

            Listener.OnNewBytesMessageReceived = null;
            Listener.OnNewBytesMessageReceived += bytes =>
            {
                lock (ListenersManager.BytesToProcess)
                {
                    ListenersManager.BytesToProcess.Enqueue(bytes);
                }
            };

            Task.Factory.StartNew(async () =>
            {
                // Todo: move to while
                while (!Connected)
                {
                    //SenderManager.Instance.SendReliablePacket("JTS", string.Empty);
                    Client.SendPacket(new Packet(0, "JTS", string.Empty, false, 0));
                    await Task.Delay(100);
                }
            });

            ReceivedThreadCancellationToken = new CancellationTokenSource();
            _ = Task.Factory.StartNew(ReceivedThread, ReceivedThreadCancellationToken.Token);

            TryingToConnect = false;
        }

    }

    //JaguarClient
    internal class UdpUser : UdpBase
    {
        internal static UdpUser Connection;
        private UdpUser() { }

        public static UdpUser ConnectTo(string hostname, int port)
        {
            Connection = new UdpUser();
            Connection.Client.Connect(hostname, port);
            return Connection;

        }

        public static void StopReceivedThread()
        {
            UdpClient.ReceivedThreadCancellationToken.Cancel();
        }

        public static void CloseConnection()
        {
            Connection.Client.Client.Close();
        }

        public void SendPacket(Packet packet)
        {
            var packetAsBytes = packet.ToByte();
            var datagram = new byte[packetAsBytes.Length + 1];
            Array.Copy(packetAsBytes, 0, datagram, 1, packetAsBytes.Length);

            try
            {
                Client?.Send(datagram, datagram.Length);
            }
            catch (ObjectDisposedException e)
            {
                UdpClient.TryingToConnect = false;
                Client?.Close();
            }
        }

        public void SendBytes(byte[] bytes)
        {
            var datagram = new byte[bytes.Length + 1];
            datagram[0] = 1;
            Array.Copy(bytes, 0, datagram, 1, bytes.Length);

            try
            {
                Client?.Send(datagram, datagram.Length);
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
        }
    }

}