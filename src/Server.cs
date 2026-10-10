using System.Net;
using System.Net.Sockets;
using Amorice.Enums;
using Amorice.Exceptions;

namespace Amorice;

public sealed class Server : IDisposable
{
    private readonly TcpListener _listener;
    private readonly PacketVersion _version;

    public Server(ushort port, PacketVersion version)
    {
        PacketException.ThrowIfUnsupportedVersion(version);

        IPAddress address = new([127, 0, 0, 1]);
        IPEndPoint endpoint = new(address, port);

        _listener = new(endpoint);
        _version = version;
    }

    public byte[] Listen()
    {
        _listener.Start();

        using var client = _listener.AcceptTcpClient();
        using var stream = client.GetStream();

        Span<byte> packetData = stackalloc byte[Packet.Size];

        try
        {
            var isAcknowledged = Handshake(stream);

            if (!isAcknowledged)
            {
                return [];
            }

            List<byte> resultData = [];

            while (true)
            {
                stream.ReadExactly(packetData);
                var packet = Packet.Deserialize(packetData);

                resultData.AddRange(packet.Data);

                Packet.Success(PacketVersion.One).Serialize(packetData);
                stream.Write(packetData);

                if (packet.OpCode == PacketOpCode.SendFinal)
                {
                    break;
                }
            }

            return [.. resultData];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Packet.Error(_version).Serialize(packetData);
            stream.Write(packetData);

            return [];
        }
        finally
        {
            _listener.Stop();
        }
    }

    public async Task<byte[]> ListenAsync(CancellationToken token = default)
    {
        _listener.Start();
        
        using var client = await _listener.AcceptTcpClientAsync(token);
        await using var stream = client.GetStream();

        var packetData = new byte[Packet.Size];

        try
        {
            var isAcknowledged = await HandshakeAsync(stream, token);

            if (!isAcknowledged)
            {
                return [];
            }

            List<byte> resultData = [];

            while (true)
            {
                await stream.ReadExactlyAsync(packetData, token);

                var packet = Packet.Deserialize(packetData);
                // Cannot be preserved across await boundary, thus value is copied beforehand
                var packetOpCode = packet.OpCode;

                resultData.AddRange(packet.Data);

                Packet.Success(PacketVersion.One).Serialize(packetData);
                await stream.WriteAsync(packetData, token);

                if (packetOpCode == PacketOpCode.SendFinal)
                {
                    break;
                }
            }

            return [.. resultData];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Packet.Error(_version).Serialize(packetData);
            await stream.WriteAsync(packetData, token);

            return [];
        }
        finally
        {
            _listener.Stop();
        }
    }

    public void Dispose() => _listener.Dispose();

    private bool Handshake(NetworkStream stream)
    {
        Span<byte> packetData = stackalloc byte[Packet.Size];

        try
        {
            stream.ReadExactly(packetData);

            var helloPacket = Packet.Deserialize(packetData);

            PacketException.ThrowIfDifferentVersions(_version, helloPacket.Version);
            PacketException.ThrowIfNotHelloOpCode(helloPacket.OpCode);

            Packet.Acknowledged(_version).Serialize(packetData);

            stream.Write(packetData);
            stream.Flush();

            return true;
        }
        catch
        {
            Packet.NotAcknowledged(_version).Serialize(packetData);

            stream.Write(packetData);
            stream.Flush();

            return false;
        }
    }

    private async Task<bool> HandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        var packetData = new byte[Packet.Size];

        try
        {
            await stream.ReadExactlyAsync(packetData, token);

            var helloPacket = Packet.Deserialize(packetData);

            PacketException.ThrowIfDifferentVersions(_version, helloPacket.Version);
            PacketException.ThrowIfNotHelloOpCode(helloPacket.OpCode);

            Packet.Acknowledged(_version).Serialize(packetData);

            await stream.WriteAsync(packetData, token);
            await stream.FlushAsync(token);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Packet.NotAcknowledged(_version).Serialize(packetData);

            await stream.WriteAsync(packetData, token);
            await stream.FlushAsync(token);

            return false;
        }
    }
}