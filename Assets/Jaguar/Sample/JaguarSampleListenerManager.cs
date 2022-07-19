using Jaguar.Attributes;
using Jaguar.Core.Managers;
using UnityEngine;

public class JaguarSampleListenerManager : ListenersManager
{
    [Listener("Login")]
    public void Login(string s)
    {
        Debug.Log(s);

        Jaguar.JaguarClient.SendReliablePacket("Login", 12345);
        Jaguar.JaguarClient.SendBytes(new byte[] { 0, 1, 0 });
    }
}
