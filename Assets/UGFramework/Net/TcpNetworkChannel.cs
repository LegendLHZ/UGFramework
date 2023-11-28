using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UGFramework.Net
{
    public class TcpNetworkChannel : INetworkChannel
    {
        public string Name { get; }
        public Socket Socket { get; private set; }
        public TcpClient Client { get; private set; }
        public bool Connected { get; }
        public long SentPacketCount { get; }
        public long ReceivedPacketCount { get; }

        public TcpNetworkChannel(string name)
        {
            Name = name;
        }

        public void Connect(string ip, int port)
        {
              Connect(IPAddress.Parse(ip), port);
        }

        public void Connect(IPAddress address, int port)
        {
            Client = new TcpClient();
            Client.NoDelay = true;

            Client.Connect(address, port);

            var s = Client.GetStream(); 
            s.Write(Encoding.UTF8.GetBytes("hello"));
        }
    }
}