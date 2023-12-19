using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace UGFramework.Net
{
    public interface INetworkChannel
    {
        /// <summary>
        /// 标志符
        /// </summary>
        string Name { get; }

        Socket Socket { get; }

        /// <summary>
        /// 已连接
        /// </summary>
        bool Connected { get; }
        
        /// <summary>
        /// 发送包数量
        /// </summary>
        long SentPacketCount { get; }

        /// <summary>
        /// 接受包数量
        /// </summary>
        long ReceivedPacketCount { get; }
    }
}
