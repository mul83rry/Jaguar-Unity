using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Jaguar.Core.Managers
{
    internal static class EventsManager
    {
        internal static readonly Dictionary<string, MuTask> ListenersDic = new Dictionary<string, MuTask>();

        internal static void AddListener(string eventName, MuTask task)
        {
            if (!Regex.IsMatch(eventName, @"^[a-zA-Z_$][a-zA-Z_$0-9]*$"))
                throw new Exception("not a valid format for event name, use like a variable name"); // todo: check message

            lock (ListenersDic)
            {
                ListenersDic.Add(eventName, task);
            }
        }

        public static void RemoveListener(string eventName)
        {
            lock (ListenersDic)
            {
                ListenersDic.Remove(eventName);
            }
        }
    }
}
