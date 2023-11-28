using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
 
namespace ConsoleApp4
{
    class Program
    {
        static void Main(string[] args)
        {
            //1、创建socket
            Socket tcpServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //2、绑定ip跟端口号 127.0.0.1
            IPAddress ipaddress = IPAddress.Parse("127.0.0.1");
            //ipendpoint是对ip+端口做了一层封装的类
            EndPoint point = new IPEndPoint(ipaddress, 10001);
            //3、向操作系统申请一个可用的ip跟端口号 用来做通信
            tcpServer.Bind(point);
            //4、开始监听 （等待客户端连接）
            tcpServer.Listen(100);//参数是最大连接数
            Console.WriteLine("开始监听");
 
            //暂停当前线程，直到有一个客户端连接过来，之后进行下面的代码
            Socket clientSocket = tcpServer.Accept();
            //Console.WriteLine("一个客户端连接过来了");
            //使用返回的socket跟客户端做通信
            //string message = "hello 欢迎你";
            //byte[] data = Encoding.UTF8.GetBytes(message);//对字符串做编码，得到一个字符串的字节数组
            //clientSocket.Send(data);
            //Console.WriteLine("向客户端发送了一跳数据");
 
            byte[] data2 = new byte[1024];//创建一个字节数组用来当做容器，去承接客户端发送过来的数据
 
            Thread t1 = new Thread(() => ReadMsg(clientSocket, data2));    //开启线程读取消息
            t1.Start();
 
            Thread t2 = new Thread(() => SendMsg(clientSocket ));    //开启线程读取消息
            t2.Start();
 
            //int length = clientSocket.Receive(data2);
            //string message2 = Encoding.UTF8.GetString(data2, 0, length);//把字节数据转化成 一个字符串
            //Console.WriteLine("接收到了一个从客户端发送过来的消息:" + message2);
 
            //Console.ReadKey();
 
 
        }
 
        private static void SendMsg(Socket clientSocket )
        {
            while (true)
            {
                string message = Console.ReadLine();//读取用户的输入 把输入发送到服务器端
                byte[] data = Encoding.UTF8.GetBytes(message);//对字符串做编码，得到一个字符串的字节数组
                clientSocket.Send(data);
                //Console.WriteLine("向客户端发送了一跳数据");
            }
        }
 
        private static void ReadMsg(Socket clientSocket, byte[] data2)
        {
            while (true)
            {
                int length = clientSocket.Receive(data2);
                string message2 = Encoding.UTF8.GetString(data2, 0, length);//把字节数据转化成 一个字符串
                Console.WriteLine("收到了消息:" + message2);
            }
        }
    }
}