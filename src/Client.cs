using System.Net;
using System.Net.Sockets;
using Amorice.Enums;
using Amorice.Exceptions;

namespace Amorice;

public sealed class Client : IDisposable
{
    private readonly TcpClient _client;
    private readonly PacketVersion _version;

    public Client(ushort port, PacketVersion version)
    {
        PacketException.ThrowIfUnsupportedVersion(version);
        
        IPAddress address = new([127, 0, 0, 1]);
        IPEndPoint endpoint = new(address, port);

        _client = new(endpoint);
        _version = version;
    }

    public void Send(IPEndPoint endpoint, Span<byte> data)
    {
        _client.Connect(endpoint);

        using var stream = _client.GetStream();

        Handshake(stream);

        var start = 0;
        Span<byte> packetData = stackalloc byte[Packet.Size];

        while (true)
        {
            var readPacketData = GetPacketDataInfo(data, start);

            Packet packet = new(_version, readPacketData.Type, readPacketData.Data);
            packet.Serialize(packetData);

            stream.Write(packetData);
            stream.ReadExactly(packetData);

            var acknowledgmentPacket = Packet.Deserialize(packetData);

            PacketException.ThrowIfErrorOpcode(acknowledgmentPacket.OpCode);

            if (readPacketData.Type == PacketOpCode.SendFinal)
            {
                break;
            }

            start += Packet.DataSize;
        }
    }

    public async Task SendAsync(IPEndPoint endpoint, byte[] data, CancellationToken token = default)
    {
        await _client.ConnectAsync(endpoint, token);
        await using var stream = _client.GetStream();

        await HandshakeAsync(stream, token);

        var start = 0;
        var packetData = new byte[Packet.Size];

        while (true)
        {
            var packetInfo = GetPacketDataInfo(data, start);
            var shouldBreak = packetInfo.Type == PacketOpCode.SendFinal;

            Packet packet = new(_version, packetInfo.Type, packetInfo.Data);

            await stream.WriteAsync(packet.Serialize(), token);
            await stream.ReadExactlyAsync(packetData, token);

            var acknowledgmentPacket = Packet.Deserialize(packetData);

            PacketException.ThrowIfErrorOpcode(acknowledgmentPacket.OpCode);

            if (shouldBreak)
            {
                break;
            }

            start += Packet.DataSize;
        }
    }

    public void Dispose() => _client.Dispose();

    private static PacketDataInfo GetPacketDataInfo(Span<byte> data, int start)
    {
        var end = Math.Min(start + Packet.DataSize, data.Length);

        var opCode = end == data.Length
            ? PacketOpCode.SendFinal
            : PacketOpCode.Send;

        return new(data[start..end], opCode);
    }

    private void Handshake(NetworkStream stream)
    {
        var helloPacket = Packet.Hello(_version).Serialize();
        stream.Write(helloPacket);

        Span<byte> helloReply = stackalloc byte[Packet.Size];
        stream.ReadExactly(helloReply);

        var helloReplyPacket = Packet.Deserialize(helloReply);

        PacketException.ThrowIfDifferentVersions(_version, helloReplyPacket.Version);
        PacketException.ThrowIfNotAckOpCode(helloReplyPacket.OpCode);
    }

    private async Task HandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        var helloPacket = Packet.Hello(_version).Serialize();
        await stream.WriteAsync(helloPacket, token);

        var helloReply = new byte[Packet.Size];
        await stream.ReadExactlyAsync(helloReply, token);

        var helloReplyPacket = Packet.Deserialize(helloReply);

        PacketException.ThrowIfDifferentVersions(_version, helloReplyPacket.Version);
        PacketException.ThrowIfNotAckOpCode(helloReplyPacket.OpCode);
    }

    private readonly ref struct PacketDataInfo
    {
        public readonly Span<byte> Data { get; }
        public readonly PacketOpCode Type { get; }

        public PacketDataInfo(Span<byte> data, PacketOpCode type)
        {
            Data = data;
            Type = type;
        }
    }
}