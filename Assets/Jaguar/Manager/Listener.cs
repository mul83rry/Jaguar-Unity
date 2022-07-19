using System;
using Jaguar.Data;

namespace Jaguar.Manager
{
    internal static class Listener
    {
        internal static Action<Packet> OnNewMessageReceived;
        internal static Action<byte[]> OnNewBytesMessageReceived;
    }
}
