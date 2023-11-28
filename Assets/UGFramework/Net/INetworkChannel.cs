using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace UGFramework.Net
{
    public interface INetworkChannel
    {
        string Name { get; }

        Socket Socket { get; }

        bool Connected { get; }
        
        long SentPacketCount { get; }

        long ReceivedPacketCount { get; }
    }
}
