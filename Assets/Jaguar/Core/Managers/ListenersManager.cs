using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jaguar.Attributes;
using Jaguar.Data;
using UnityEngine;
using Newtonsoft.Json;

namespace Jaguar.Core.Managers
{
    public abstract class ListenersManager : MonoBehaviour
    {
        internal static Queue<Packet> ReliablePackets = new Queue<Packet>();

        internal static Queue<Packet> PacketsToProcess = new Queue<Packet>();
        public static Queue<byte[]> BytesToProcess = new Queue<byte[]>();

        private readonly string[] reservedEventsName = new string[] { "JTS", "PRC", "IA" };

        public void OnEnable() => AddListeners();
        public void OnDisable() => RemoveListeners();

        private void AddListeners()
        {
            var type = GetType();

            foreach (var method in type.GetMethods())
            {
                foreach (Attribute attribute in method.GetCustomAttributes(true))
                {
                    if (!(attribute is ListenerAttribute listener)) continue;

                    var eventName = !string.IsNullOrEmpty(listener.Name) ? listener.Name : method.Name;
                    var parameters = method.GetParameters();

                    /*if (method.ReturnType != typeof(void))
                        throw new Exception("Type of method must be Void.");*/

                    var task = new MuTask()
                    {
                        FunctionType = parameters[0].ParameterType,
                        Method = method,
                        ListenersManager = this
                    };

                    if (transform.name != "InternalListener")
                    {
                        if (reservedEventsName.Contains(eventName))
                        {
                            throw new Exception($"This events name are reserved, please use another '{eventName}'");
                        }
                    }

                    EventsManager.AddListener(eventName, task);
                }
            }
        }
        private void RemoveListeners()
        {
            var type = GetType();

            foreach (var method in type.GetMethods())
            {
                foreach (Attribute attribute in method.GetCustomAttributes(true))
                {
                    if (!(attribute is ListenerAttribute listener)) continue;

                    var eventName = !string.IsNullOrEmpty(listener.Name) ? listener.Name : method.Name;
                    EventsManager.RemoveListener(eventName);
                }
            }
        }


        private static Packet packet;
        private static Packet reliablePacket;
        private static byte[] bytes;
        public static void UpdatePackets()
        {
            while (ReliablePackets.Count > 0)
            {
                lock (ReliablePackets)
                {
                    reliablePacket = ReliablePackets.Dequeue(); // Todo: check

                    if (EventsManager.ListenersDic[reliablePacket.EventName].FunctionType == typeof(string))
                    {
                        EventsManager.ListenersDic[reliablePacket.EventName].Method.Invoke(EventsManager.ListenersDic[reliablePacket.EventName].ListenersManager
                                        , new object[] { reliablePacket.Message });
                    }
                    else
                    {
                        try
                        {
                            var data = JsonConvert.DeserializeObject(reliablePacket.Message, EventsManager.ListenersDic[reliablePacket.EventName].FunctionType);
                            EventsManager.ListenersDic[reliablePacket.EventName].Method.Invoke(EventsManager.ListenersDic[reliablePacket.EventName].ListenersManager
                                , new[] { data });
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.ToString());
                        }
                    }
                }
            }

            while (BytesToProcess.Count > 0)
            {
                lock (BytesToProcess)
                    bytes = BytesToProcess.Dequeue();

                EventsManager.ListenersDic["UDPBYTES"].Method.Invoke(EventsManager.ListenersDic["UDPBYTES"].ListenersManager
                                , new object[] { bytes });
            }

            while (PacketsToProcess.Count > 0)
            {
                lock (PacketsToProcess)
                {
                    packet = PacketsToProcess.Dequeue();
                }

                if (EventsManager.ListenersDic[packet.EventName].FunctionType == typeof(string))
                {
                    EventsManager.ListenersDic[packet.EventName].Method.Invoke(EventsManager.ListenersDic[packet.EventName].ListenersManager
                                    , new object[] { packet.Message });
                }
                else
                {
                    try
                    {
                        var data = JsonConvert.DeserializeObject(packet.Message, EventsManager.ListenersDic[packet.EventName].FunctionType);
                        EventsManager.ListenersDic[packet.EventName].Method.Invoke(EventsManager.ListenersDic[packet.EventName].ListenersManager
                            , new[] { data });
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.ToString());
                    }
                }
            }
        }
    }

    public class MuTask
    {
        public Type FunctionType;
        public MethodInfo Method;
        public ListenersManager ListenersManager;
    }
}
