using System.Collections;
using UnityEngine;

namespace Jaguar.Sample
{
    public class JaguarSampleConnectToServer : MonoBehaviour
    {
        [SerializeField] private string ip = "127.0.0.1";
        [SerializeField] private int port = 5001;

        private void Start()
        {
            global::Jaguar.JaguarClient.OnConnectedToServer += () =>
            {
                Debug.Log("connected");

                global::Jaguar.JaguarClient.SendReliablePacket("LoginMethod", "13");
                global::Jaguar.JaguarClient.SendBytes(new byte[] { 0, 1, 2, 3, 4 });

                StartCoroutine(IeCallBack());
            };

            global::Jaguar.JaguarClient.OnErrorConnectingToServer += () =>
            {
                Debug.Log("Failed to Connect");
            };

            global::Jaguar.JaguarClient.ConnectToServer(ip, port);
        }

        private IEnumerator IeCallBack()
        {
            var callBack = new global::Jaguar.Core.CallBackPacket<bool>("TestCallBack", 123);
            yield return callBack;

            if (!string.IsNullOrEmpty(callBack.Error))
            {
                Debug.Log($"Error {callBack.Error}");
            }
            else
            {
                var data = callBack.GetValue();
                Debug.Log($"Data = {data}");
            }
        }

    }
}
