using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MessagePack;
using System.Buffers.Binary;

namespace gameServer.ServerHandling
{
    internal class TCPServer
    {
        private const int MaxMessageSize = 64 * 1024;

        private TcpListener _tcpListener;
        public TCPServer() { 
        
        }

        public void Start()
        {
            StartServer();
        }

        private void StartServer()
        {
            var port = 13000;
            var hostAddress = IPAddress.Parse("127.0.0.1");
            _tcpListener = new TcpListener(hostAddress, port); 
            _tcpListener.Start();

            using TcpClient client = _tcpListener.AcceptTcpClient();
            var TCPStream = client.GetStream();
            Console.WriteLine($"[Main] Client connected: {client.Client.RemoteEndPoint}");

            while (true)
            {   

            if (TryReadMessage<NetworkMessage>(TCPStream, out var receivedMessage))
            {
            
                Console.WriteLine($"[Main] Received: {receivedMessage.Type} / {receivedMessage.Payload}");
            }

            }

        }

        private static bool TryReadMessage<T>(NetworkStream stream, out T? message) where T : class
        {
            message = null;

            var lengthBuffer = new byte[4];
            if (!ReadExactly(stream, lengthBuffer, 0, lengthBuffer.Length))
            {
                return false;
            }

            int length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
            if (length <= 0 || length > MaxMessageSize)
            {
                return false;
            }

            var payload = new byte[length];
            if (!ReadExactly(stream, payload, 0, payload.Length))
            {
                return false;
            }

            message = MessagePackSerializer.Deserialize<T>(payload);
            return true;
        }

        private static bool ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            int read;
            while (count > 0 && (read = stream.Read(buffer, offset, count)) > 0)
            {
                offset += read;
                count -= read;
            }

            return count == 0;
        }
    }

    [MessagePackObject]
    internal sealed class NetworkMessage
    {
        [Key(0)]
        public string Type { get; set; } = "";

        [Key(1)]
        public string Payload { get; set; } = "";
    }
}
