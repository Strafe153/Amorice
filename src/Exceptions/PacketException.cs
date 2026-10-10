using Amorice.Enums;

namespace Amorice.Exceptions;

public sealed class PacketException : Exception
{
    public PacketException() : base()
    {
    }
    
    public PacketException(string message) : base(message)
    {
    }

    public PacketException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public static void ThrowIfIncorrectLength(Span<byte> data, int length)
    {
        if (length < 0 || length > data.Length - Packet.HeaderSize)
        {
            throw new PacketException($"Incorrect packet length: {length}. Must be in range from 0 to {Packet.DataSize}");
        }
    }

    public static void ThrowIfUnsupportedVersion(PacketVersion version)
    {
        if (!Enum.IsDefined(version))
        {
            throw new PacketException($"Packet version ({version}) is not supported");
        }
    }

    public static void ThrowIfIncorrectOpCode(PacketOpCode opCode)
    {
        if (!Enum.IsDefined(opCode))
        {
            throw new PacketException($"Incorrect operation code: {opCode}");
        }
    }

    public static void ThrowIfDifferentVersions(PacketVersion first, PacketVersion second)
    {
        if (first != second)
        {
            throw new PacketException($"Packet versions differ: {first} and {second}");
        }
    }

    public static void ThrowIfNotHelloOpCode(PacketOpCode opCode)
    {
        if (opCode != PacketOpCode.Hello)
        {
            throw new PacketException("Handshake failed");
        }
    }

    public static void ThrowIfNotAckOpCode(PacketOpCode opCode)
    {
        if (opCode != PacketOpCode.Ack)
        {
            throw new PacketException("Packet was not acknowledged");
        }
    }

    public static void ThrowIfErrorOpcode(PacketOpCode opCode)
    {
        if (opCode == PacketOpCode.Error)
        {
            throw new PacketException("Packet was not sent");
        }
    }
}