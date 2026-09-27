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

        try
        {
            var isAcknowledged = Handshake(stream);

            if (!isAcknowledged)
            {
                return [];
            }

            Span<byte> packetData = stackalloc byte[Packet.Size];
            List<byte> resultData = [];

            while (true)
            {
                stream.ReadExactly(packetData);
                var packet = Packet.Deserialize(packetData);

                resultData.AddRange(packet.Data);

                if (packet.OpCode == PacketOpCode.SendFinal)
                {
                    break;
                }
            }

            return [.. resultData];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var errorBytes = Packet.Error(_version).Serialize();
            stream.Write(errorBytes);

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

        try
        {
            var isAcknowledged = await HandshakeAsync(stream, token);

            if (!isAcknowledged)
            {
                return [];
            }

            var packetData = new byte[Packet.Size];
            List<byte> resultData = [];

            while (true)
            {
                await stream.ReadExactlyAsync(packetData, token);

                var packet = Packet.Deserialize(packetData);
                // Cannot be preserved across await boundary, thus value is copied beforehand
                var packetOpCode = packet.OpCode;

                resultData.AddRange(packet.Data);

                var successPacket = Packet.Success(PacketVersion.One).Serialize();
                await stream.WriteAsync(successPacket, token);

                if (packetOpCode == PacketOpCode.SendFinal)
                {
                    break;
                }
            }

            return [.. resultData];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var errorBytes = Packet.Error(_version).Serialize();
            await stream.WriteAsync(errorBytes, token);

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
        try
        {
            Span<byte> packetData = stackalloc byte[Packet.Size];
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
            var nackBytes = Packet.NotAcknowledged(_version).Serialize();

            stream.Write(nackBytes);
            stream.Flush();

            return false;
        }
    }

    private async Task<bool> HandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        try
        {
            var packetData = new byte[Packet.Size];
            await stream.ReadExactlyAsync(packetData, token);

            var helloPacket = Packet.Deserialize(packetData);

            PacketException.ThrowIfDifferentVersions(_version, helloPacket.Version);
            PacketException.ThrowIfNotHelloOpCode(helloPacket.OpCode);

            var ackBytes = Packet.Acknowledged(_version).Serialize();

            await stream.WriteAsync(ackBytes, token);
            await stream.FlushAsync(token);

            return true;
        }
        catch
        {
            var nackBytes = Packet.NotAcknowledged(_version).Serialize();

            await stream.WriteAsync(nackBytes, token);
            await stream.FlushAsync(token);

            return false;
        }
    }
}