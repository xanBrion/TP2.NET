using MessagePack;

[MessagePackObject]
public sealed class NetworkMessage
{
    [Key(0)]
    public string Type { get; set; } = "";

    [Key(1)]
    public string Payload { get; set; } = "";
}
