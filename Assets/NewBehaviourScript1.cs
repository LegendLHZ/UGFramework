using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UGFramework.Net;
using UnityEngine;

public class NewBehaviourScript1 : MonoBehaviour
{
    // Start is called before the first frame update
    private TcpNetworkChannel channel;
    void Start()
    {
        TcpNetworkChannel c = new TcpNetworkChannel("1");
        c.Connect("127.0.0.1", 10001);
        
        
        channel = c;
    }

    // Update is called once per frame
    void Update()
    {
        if (!channel.Client.Connected)
        {
            return;
        }
        if (channel.Client.GetStream().DataAvailable)
        {
            byte[] data = new byte[channel.Client.ReceiveBufferSize];
            int len = channel.Client.GetStream().Read(data, 0, channel.Client.ReceiveBufferSize);
            var message = Encoding.UTF8.GetString(data, 0, len);
            Debug.Log(message);
        }
    }
}
