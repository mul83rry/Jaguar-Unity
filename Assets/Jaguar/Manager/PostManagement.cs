using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jaguar.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace Jaguar.Manager
{
    public class PostManagement : MonoBehaviour
    {
        public static PostManagement Instance;

        private readonly JsonSerializerSettings _jsonSerializeSettings = new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
        private readonly Dictionary<uint, bool> _sentPacketsSituation = new Dictionary<uint, bool>();

        [HideInInspector] public int maxPacketSize = 12; // Todo: in setting
        [HideInInspector] public int maxPacketInQueue = 1000; // Todo: in setting

        private readonly Dictionary<uint, Action<uint>> _onReliablePacketsArrived = new Dictionary<uint, Action<uint>>();

        private readonly Queue<Packet> _packetsInQueue = new Queue<Packet>();
        private uint _packetIndex;



        private readonly WaitForSeconds _delay = new WaitForSeconds(.1f);
        private readonly WaitForSeconds _delayForResendPacket = new WaitForSeconds(.3f);


        private void Awake() => Instance = this;

        internal void Init()
        {
            _sentPacketsSituation.Clear();

            _packetsInQueue.Clear();
            _packetIndex = 0;


            StartCoroutine(nameof(IeSendReliablePacketsAsync));
        }

        public void SendReliablePacket(string eventName, object message, Action<uint> onPacketArrived = null)
        {
            var msg = message is string ? message.ToString() : JsonConvert.SerializeObject(message);

            var packet = new Packet(0, eventName, msg, true, 0);
            if (onPacketArrived != null)
                packet.OnPacketArrived = onPacketArrived;
            lock (_packetsInQueue)
            {
                _packetsInQueue.Enqueue(packet);
            }
        }

        internal void SendCallBack(string eventName, object message, byte signIndex, Action<uint> onPacketArrived = null)
        {
            var msg = message is string ? message.ToString() : JsonConvert.SerializeObject(message);

            var packet = new Packet(0, eventName, msg, true, signIndex);
            if (onPacketArrived != null)
                packet.OnPacketArrived = onPacketArrived;
            lock (_packetsInQueue)
            {
                _packetsInQueue.Enqueue(packet);
            }
        }

        public void Send(string eventName, object message)
        {
            var msg = message is string ? message.ToString() : JsonConvert.SerializeObject(message, Formatting.None, _jsonSerializeSettings);

            var packet = new Packet(0, eventName, msg, false, 0);
            SendPacket(packet);
        }

        public void SendBytes(byte[] bytes)
        {
            UdpClient.Client.SendBytes(bytes);
        }

        private IEnumerator IeSendReliablePacketsAsync()
        {
            while (true)
            {
                while (_packetsInQueue.Count > 0)
                {
                    Packet packet;
                    lock (_packetsInQueue)
                        packet = _packetsInQueue.Dequeue();

                    if (!packet.Reliable) continue;

                    var chunkPackets = ChunksUpto(packet.Message, maxPacketSize).ToArray();
                    var packets = new Packet[Math.Max(1, chunkPackets.Length)];

                    packets[0] = new Packet(_packetIndex, packet.EventName, chunkPackets.Length > 0 ? chunkPackets[0] : "", true, packets.Length > 1, true, (uint)packets.Length, packet.SignIndex);
                    if (packet.OnPacketArrived != null)
                    {
                        packets[0].OnPacketArrived = packet.OnPacketArrived;
                        _onReliablePacketsArrived.Add(packets[0].Index, packets[0].OnPacketArrived);
                    }

                    for (uint i = 1; i < chunkPackets.Length; i++)
                    {
                        //packetIndex++;
                        packets[i] = new Packet(++_packetIndex, packet.EventName, chunkPackets[i], true, packets.Length > 1, false, (uint)(packets.Length - i), 0);
                    }

                    lock (_sentPacketsSituation)
                    {
                        for (uint i = 0; i < packets.Length; i++)
                        {
                            _sentPacketsSituation.Add(packets[i].Index, false);
                            StartCoroutine(IeSendPacketAndWaitForResponse(packets[i]));
                        }
                    }
                    _packetIndex++;
                }
                yield return _delay;
            }
        }

        private IEnumerator IeSendPacketAndWaitForResponse(Packet packet)
        {
            var sent = _sentPacketsSituation[packet.Index];
            while (!sent/*Todo: && client server is connected*/)
            {
                SendPacket(packet);
                yield return _delayForResendPacket;
                try
                {
                    lock (_sentPacketsSituation)
                        if (_sentPacketsSituation.ContainsKey(packet.Index))
                            sent = _sentPacketsSituation[packet.Index];
                }
                catch
                {
                    sent = false;
                }
            }
            lock (_sentPacketsSituation)
            {
                _sentPacketsSituation.Remove(packet.Index);
            }

            if (!packet.StarterPack) yield break;
            if (!_onReliablePacketsArrived.ContainsKey(packet.Index)) yield break;
            var arrived = true;
            for (var i = packet.Index; i < packet.Index + packet.Length; i++)
            {
                if (_sentPacketsSituation.ContainsKey(i) && _sentPacketsSituation[i]) continue;
                if (_sentPacketsSituation.ContainsKey(i) && !_sentPacketsSituation[i])
                    arrived = false;
            }
            if (arrived)
            {
                packet.OnPacketArrived.Invoke(packet.Index);
            }
        }


        private void SendPacket(Packet packet)
        {
            if (packet.Reliable)
            {
                lock (_sentPacketsSituation)
                {
                    if (!_sentPacketsSituation.ContainsKey(packet.Index)) return;
                    if (_sentPacketsSituation[packet.Index]) return;
                }
            }

            UdpClient.Client.SendPacket(packet);
        }

        internal void PacketReceivedCallBack(uint packetIndex)
        {
            lock (_sentPacketsSituation)
            {
                if (!_sentPacketsSituation.ContainsKey(packetIndex)) return;
                if (_sentPacketsSituation[packetIndex]) return;
            }

            lock (_sentPacketsSituation)
            {
                _sentPacketsSituation[packetIndex] = true;
            }
        }

        private IEnumerable<string> ChunksUpto(string str, int maxChunkSize)
        {
            for (var i = 0; i < str.Length; i += maxChunkSize)
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
        }

    }
}