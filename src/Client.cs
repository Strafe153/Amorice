using System.Net;
using System.Net.Sockets;
using Amorice.Enums;
using Amorice.Exceptions;

namespace Amorice;

public sealed class Client : IDisposable
{
    private readonly TcpClient _client;
    private readonly PacketVersion _version;

    public Client(IPEndPoint endpoint, PacketVersion version)
    {
        PacketException.ThrowIfUnsupportedVersion(version);
        
        _client = new(endpoint);
        _version = version;
    }

    public Client(PacketVersion version) : this(new(IPAddress.Any, 0), version)
    {
    }

    public void Send(IPEndPoint serverEndpoint, Span<byte> data)
    {
        _client.Connect(serverEndpoint);

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
            packet.Serialize(packetData);

            await stream.WriteAsync(packetData, token);
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
        Span<byte> packetData = stackalloc byte[Packet.Size];

        Packet.Hello(_version).Serialize(packetData);
        stream.Write(packetData);

        stream.ReadExactly(packetData);

        var helloReplyPacket = Packet.Deserialize(packetData);

        PacketException.ThrowIfDifferentVersions(_version, helloReplyPacket.Version);
        PacketException.ThrowIfNotAckOpCode(helloReplyPacket.OpCode);
    }

    private async Task HandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        var packetData = new byte[Packet.Size];
        Packet.Hello(_version).Serialize(packetData);

        await stream.WriteAsync(packetData, token);
        await stream.ReadExactlyAsync(packetData, token);

        var helloReplyPacket = Packet.Deserialize(packetData);

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