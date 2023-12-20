using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        public bool Connected => Socket != null && Socket.Connected;
        public long SentPacketCount { get; private set; }
        public long ReceivedPacketCount { get; private set; }
        /// <summary>
        /// 重连次数
        /// </summary>
        public int ReconnectionCount { get; set; } = 3;

        /// <summary>
        /// 开始重连
        /// </summary>
        public event Action<int> ReconnectEvent;
        /// <summary>
        /// 连接成功
        /// </summary>
        public event Action ConnectSuccessEvent;
        /// <summary>
        /// 连接失败
        /// </summary>
        public event Action<SocketError> ConnectFailureEvent;
        
        private readonly AsyncCallback connectCallback;
        private readonly AsyncCallback sendCallback;
        private readonly AsyncCallback receiveCallback;
        
        /// <summary>
        /// 已重连次数
        /// </summary>
        private int _reconnectedCount;
        
        /// <summary>
        /// 正在重连
        /// </summary>
        private bool _isReconnecting;

        private IPAddress _address;
        private int _port;

        private MemoryStream _receiveMemoryStream = new MemoryStream(1024 * 64);

        public TcpNetworkChannel(string name)
        {
            Name = name;
            
            connectCallback = ConnectCallback;
            sendCallback = SendCallback;
            receiveCallback = ReceiveCallback;
        }

        private void SendCallback(IAsyncResult ar)
        {
            
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket.EndConnect(ar);
            }
            catch (Exception exception)
            {
                Debug.Log("UGNet---->" + exception);
            }

            if (!Socket.Connected)
            {
                if (_isReconnecting)
                {
                    _reconnectedCount++;
                }
                if (_reconnectedCount < ReconnectionCount)
                {
                    ReConnect();
                }
                else
                {
                    ConnectFailureEvent?.Invoke(SocketError.Fault);
                }
                return;
            }

            _isReconnecting = false;
            _reconnectedCount = 0;
            ConnectSuccessEvent?.Invoke();
            ReceiveAsync();
        }
        
        private void ReceiveAsync()
        {
            try
            {
                var buff = _receiveMemoryStream.GetBuffer();
                Socket.BeginReceive(buff, 0, buff.Length, SocketFlags.None, receiveCallback, Socket);
            }
            catch (Exception exception)
            {
                Debug.Log("UGNet---->" + exception);
            }
        }
        
        private void ReceiveCallback(IAsyncResult ar)
        {
            if (!Socket.Connected)
            {
                return;
            }

            int bytesReceived = 0;
            try
            {
                bytesReceived = Socket.EndReceive(ar);
            }
            catch (Exception exception)
            {
                return;
            }

            if (bytesReceived <= 0)
            {
                Close();
                return;
            }

            ReceiveAsync();
        }

        /// <summary>
        /// 重连
        /// </summary>
        private void ReConnect()
        {
            if (_reconnectedCount < ReconnectionCount)
            {
                _isReconnecting = true;
                ReconnectEvent?.Invoke(_reconnectedCount + 1);
                Connect(_address, _port);
            }
        }

        /// <summary>
        /// 连接到远程主机
        /// </summary>
        public void Connect(string ip, int port)
        {
            Connect(IPAddress.Parse(ip), port);
        }

        /// <summary>
        /// 连接到远程主机
        /// </summary>
        public void Connect(IPAddress address, int port)
        {
            _address = address;
            _port = port;
            if (Socket != null)
            {
                Close();
            }

            try
            {
                Socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                Socket.BeginConnect(address, port, connectCallback, Socket);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }

        /// <summary>
        /// 关闭连接，释放资源
        /// </summary>
        public void Close()
        {
            if (Socket == null)
            {
                return;
            }

            try
            {
                Socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // ignored
            }
            finally
            {
                Socket.Close();
                Socket = null;
            }
        }
    }
}