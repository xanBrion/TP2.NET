using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

using var client = new TcpClient("127.0.0.1", 13000);
using var stream = client.GetStream();
using var reader = new MessagePackStreamReader(stream);

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
bool pseudoSent = false;

while (!pseudoSent)
{
    var msgpack = await reader.ReadAsync(cts.Token).ConfigureAwait(false);
    if (msgpack == null)
    {
        break;
    }

    var request = MessagePackSerializer.Deserialize<NetworkMessage>(msgpack.Value, cancellationToken: cts.Token);
    if (request.Type != "request")
    {
        continue;
    }

    if (!pseudoSent && request.Payload == "Pseudo")
    {
        SendResponse(stream, "TestPlayer");
        pseudoSent = true;
    }
}

var msg = new NetworkMessage
{
    Type = "Placement pion",
    Payload = "hello"
};

MessagePackSerializer.Serialize(stream, msg);
stream.Flush();

Console.WriteLine("Sent");

static void SendResponse(NetworkStream stream, string responseValue)
{
    MessagePackSerializer.Serialize(stream, new NetworkMessage
    {
        Type = "response",
        Payload = responseValue
    });
    stream.Flush();
}
