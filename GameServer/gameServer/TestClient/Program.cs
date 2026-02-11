using System.Buffers.Binary;
using System.Net.Sockets;
using MessagePack;

var msg = new NetworkMessage
{
    Type = "Placement pion",
    Payload = "hello"
};

var payload = MessagePackSerializer.Serialize(msg);
var lengthPrefix = new byte[4];
BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, payload.Length);

using var client = new TcpClient("127.0.0.1", 13000);
using var stream = client.GetStream();
stream.Write(lengthPrefix, 0, lengthPrefix.Length);
stream.Write(payload, 0, payload.Length);
stream.Flush();

Console.WriteLine("Sent");
