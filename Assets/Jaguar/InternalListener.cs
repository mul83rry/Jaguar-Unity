using System.Collections;
using System.Threading.Tasks;
using Jaguar.Attributes;
using Jaguar.Core.Managers;
using Jaguar.Manager;

namespace Jaguar
{
    public class InternalListener : ListenersManager
    {
        public static InternalListener Instance { get; private set; }


        private void Awake() => Instance = this;
        private void Start() => StartCoroutine(IeUpdate());


        [Listener("JTS")]
        public static Task JoinedToServer(string setting)
        {
            if (!UdpClient.Connected)
            {
                PostManagement.Instance.maxPacketSize = int.Parse(setting.Split(',')[0]);
                PostManagement.Instance.maxPacketInQueue = int.Parse(setting.Split(',')[1]);
                JaguarClient.OnConnectedToServer?.Invoke();
            }
            UdpClient.Connected = true;
            return Task.CompletedTask;
        }

        [Listener("PRC")]
        public static Task PacketReceivedCallBack(uint packetIndex)
        {
            PostManagement.Instance.PacketReceivedCallBack(packetIndex);
            return Task.CompletedTask;
        }

        private IEnumerator IeUpdate()
        {
            while (true)
            {
                UpdatePackets();
                yield return null;
            }
        }

    }
}