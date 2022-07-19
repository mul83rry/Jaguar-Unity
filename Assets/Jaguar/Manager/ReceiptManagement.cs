using System;
using System.Collections;
using System.Collections.Generic;
using Jaguar.Data;
using UnityEngine;

namespace Jaguar.Manager
{
    public class ReceiptManagement : MonoBehaviour
    {
        public static ReceiptManagement Instance;

        private readonly Dictionary<uint, bool> _receivedPacketsSituation = new Dictionary<uint, bool>();
        private readonly SortedList<uint, Packet> _receivedReliableSequencePackets = new SortedList<uint, Packet>();
        private readonly SortedList<uint, (uint, string, string, byte)> _reliableMessagesSequenced = new SortedList<uint, (uint, string, string, byte)>();

        private static uint _lastReliableMessageIndexReceived;

        internal static Dictionary<uint, Action<Packet>> AsyncPacketReceived = new Dictionary<uint, Action<Packet>>();

        private readonly WaitForSeconds _delay = new WaitForSeconds(.005f);


        private void Awake() => Instance = this;

        public void Init()
        {
            _receivedPacketsSituation.Clear();
            _receivedReliableSequencePackets.Clear();
            _reliableMessagesSequenced.Clear();

            _lastReliableMessageIndexReceived = 0;

            StartCoroutine(nameof(IeCheckSequenceData));
            StartCoroutine(nameof(IeCheckReliableMessagesSequenced));
        }

        internal void ReceivedReliablePacket(Packet packet)
        {
            lock (_receivedPacketsSituation)
            {
                if (_receivedPacketsSituation.ContainsKey(packet.Index))
                {
                    if (!_receivedPacketsSituation[packet.Index]) SendReceivedCallBack(packet.Index);
                    return;
                }

                _receivedPacketsSituation.Add(packet.Index, false);
            }
            //ListenersManager.ReliablePackets.Enqueue(packet);

            SendReceivedCallBack(packet.Index);

            lock (_receivedReliableSequencePackets) _receivedReliableSequencePackets.Add(packet.Index, packet);
        }

        private IEnumerator IeCheckSequenceData()
        {
            while (true)
            {
                yield return _delay;

                for (var i = _lastReliableMessageIndexReceived; i < _receivedReliableSequencePackets.Count; i++)
                {
                    Packet starterPacket;
                    lock (_receivedReliableSequencePackets)
                        starterPacket = _receivedReliableSequencePackets[_receivedReliableSequencePackets.Keys[(int)i]];

                    if (starterPacket.Index != _lastReliableMessageIndexReceived) continue;
                    if (!starterPacket.StarterPack) continue;
                    var msg = starterPacket.Message;
                    var eventName = starterPacket.EventName;
                    var breakFor = false;
                    for (var j = i + 1; j < i + starterPacket.Length; j++)
                    {
                        if (!_receivedReliableSequencePackets.ContainsKey(j))
                        {
                            breakFor = true;
                            continue;
                        }

                        var nextPacket = _receivedReliableSequencePackets[j];

                        msg += nextPacket.Message;
                    }
                    if (breakFor)
                        break;

                    lock (_reliableMessagesSequenced)
                    {
                        if (!_reliableMessagesSequenced.ContainsKey(starterPacket.Index))
                        {
                            _reliableMessagesSequenced.Add(starterPacket.Index, (starterPacket.Length, eventName, msg, starterPacket.SignIndex));
                        }
                    }

                    i += starterPacket.Length - 1;
                }
            }
        }

        private IEnumerator IeCheckReliableMessagesSequenced()
        {
            while (true)
            {
                yield return _delay;

                if (_reliableMessagesSequenced.Count == 0) continue;

                if (_lastReliableMessageIndexReceived == _reliableMessagesSequenced.Keys[0])
                {
                    _lastReliableMessageIndexReceived += _reliableMessagesSequenced[_reliableMessagesSequenced.Keys[0]].Item1;
                    var (length, eventName, message, signIndex) = _reliableMessagesSequenced[_reliableMessagesSequenced.Keys[0]];

                    Packet packet = new Packet(_reliableMessagesSequenced.Keys[0], eventName, message, false, signIndex);

                    if (signIndex > 0)
                    {
                        if (AsyncPacketReceived.ContainsKey(packet.SignIndex))
                            AsyncPacketReceived[packet.SignIndex].Invoke(packet);
                        AsyncPacketReceived.Remove(packet.SignIndex);
                    }
                    else
                        Listener.OnNewMessageReceived?.Invoke(packet);

                    _reliableMessagesSequenced.RemoveAt(0);
                }
            }
        }

        private static void SendReceivedCallBack(uint packetIndex)
        {
            UdpClient.Client.SendPacket(new Packet(0, "PRC", packetIndex.ToString(), false, 0));
        }
    }
}