using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
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
        c.Connect("127.5.9.100", 10001);
    }

    void  Start()
    {
    }

    private void OnDestroy()
    {
    }


    // Update is called once per frame
    void Update()
    {
       
    }
}
