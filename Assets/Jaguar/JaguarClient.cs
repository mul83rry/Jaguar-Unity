using System;
using Jaguar.Manager;
using UnityEngine;

namespace Jaguar
{
    public class JaguarClient : MonoBehaviour
    {
        private static PostManagement _postManagement;

        public static Action OnConnectedToServer;
        public static Action OnErrorConnectingToServer;

        private void Awake()
        {
            _postManagement = GetComponent<PostManagement>();
        }

        public static void ConnectToServer(string ip, int port)
        {
            var client = new UdpClient(ip, port);
            client.Start();
        }

        public static void SendReliablePacket(string eventName, object message, Action<uint> onPacketArrived = null)
        {
            _postManagement.SendReliablePacket(eventName, message, onPacketArrived);
        }

        public static void Send(string eventName, object message)
        {
            _postManagement.Send(eventName, message);
        }

        public static void SendBytes(byte[] data)
        {
            _postManagement.SendBytes(data);
        }
    }
}