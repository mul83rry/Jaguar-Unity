using System;
using System.Collections;
using Jaguar.Manager;
using UnityEngine;
using Newtonsoft.Json;

namespace Jaguar.Core
{
    public class CallBackPacket<T> : IEnumerator, IDisposable
    {
        public string EventsName { get; set; }
        public object Message { get; set; }
        private float TimeOut { get; }
        public string Error { get; set; }

        private string data;
        private bool sent;

        private byte myIndex = byte.MaxValue;

        private float sentTime;
        public float Delay { get; private set; }

        private bool ok;
        private static byte signIndexCounter = 0;


        public CallBackPacket(string eventsName, object message, float timeOut = 4)
        {
            EventsName = eventsName;
            Message = message;
            TimeOut = timeOut;
        }

        public bool MoveNext()
        {
            if (!sent)
            {
                signIndexCounter = (byte)Mathf.Clamp((signIndexCounter + 1) % 250, 1, 200);
                myIndex = signIndexCounter;

                ReceiptManagement.AsyncPacketReceived.Add(myIndex, (packet) =>
                {
                    Delay = Time.time - sentTime;
                    Error = string.Empty;
                    data = packet.Message;
                    ok = true;
                });

                PostManagement.Instance.SendCallBack(EventsName, Message, myIndex);
                sentTime = Time.time;
                sent = true;
            }

            if (!ok)
            {
                if (Time.time > sentTime + TimeOut)
                {
                    ReceiptManagement.AsyncPacketReceived.Remove(myIndex);
                    Error = "Time out";
                    return false;
                }
                return true;
            }

            return !ok;
        }

        public object Current
        {
            get
            {
                return new WaitForSeconds(.025f);
            }
        }

        public T GetValue() => JsonConvert.DeserializeObject<T>(data);

        public void Reset() { }

        public void Dispose()
        {

        }
    }
}