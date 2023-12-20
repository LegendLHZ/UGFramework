using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UGFramework.Net;
using UnityEngine;

public class NewBehaviourScript1 : MonoBehaviour
{
    // Start is called before the first frame update
    private TcpNetworkChannel channel;

    private void Awake()
    {
        TcpNetworkChannel c = new TcpNetworkChannel("1");
        Action onCon = () =>
        {
            c.Socket.Send(new byte[8] { 0, 1, 2, 3, 4, 5, 6, 70 });
        };
        c.ConnectSuccessEvent += onCon;
        c.Connect("127.0.0.1", 10001);
    }

    void  Start()
    {
        
    }


    // Update is called once per frame
    void Update()
    {
       
    }
}
