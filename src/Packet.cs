using System.Buffers.Binary;
using Amorice.Enums;
using Amorice.Exceptions;

namespace Amorice;

public readonly ref struct Packet
{
    private const byte MagicSize = 2;
    private const byte HeaderWithoutLengthSize = MagicSize + sizeof(PacketVersion) + sizeof(PacketOpCode);
    private const byte HeaderSize = HeaderWithoutLengthSize + sizeof(int);

    private static readonly byte[] _magic = new byte[MagicSize] { 1, 7 };
    
    public const int Size = 4096;
    public const int DataSize = Size - HeaderSize;

    public static Packet Hello(PacketVersion version) => new(version, PacketOpCode.Hello, []);
    public static Packet Acknowledged(PacketVersion version) => new(version, PacketOpCode.Ack, []);
    public static Packet NotAcknowledged(PacketVersion version) => new(version, PacketOpCode.Nack, []);
    public static Packet Success(PacketVersion version) => new(version, PacketOpCode.Success, []);
    public static Packet Error(PacketVersion version) => new(version, PacketOpCode.Error, []);
    
    public PacketVersion Version { get; }
    public PacketOpCode OpCode { get; }
    public Span<byte> Data { get; }

    public int Length => Data.Length;

    public Packet(PacketVersion version, PacketOpCode type, Span<byte> data)
    {
        Version = version;
        OpCode = type;
        Data = data;
    }

    public void Serialize(Span<byte> destination)
    {
        destination[0] = _magic[0];
        destination[1] = _magic[1];
        destination[2] = (byte)Version;
        destination[3] = (byte)OpCode;

        BinaryPrimitives.WriteInt32BigEndian(destination[HeaderWithoutLengthSize..HeaderSize], Length);

        Data.CopyTo(destination[HeaderSize..(HeaderSize + Length)]);
    }

    public static Packet Deserialize(Span<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new PacketException("Headerless packet");
        }

        var magic = data[0.._magic.Length];

        if (!magic.SequenceEqual(_magic))
        {
            throw new PacketException("Incorrect protocol magic");
        }

        var version = (PacketVersion)data[2];
        var opCode = (PacketOpCode)data[3];

        PacketException.ThrowIfUnsupportedVersion(version);
        PacketException.ThrowIfIncorrectOpCode(opCode);

        var length = BinaryPrimitives.ReadInt32BigEndian(data[HeaderWithoutLengthSize..HeaderSize]);
        var packetData = data[HeaderSize..(HeaderSize + length)];

        return new(version, opCode, packetData);
    }
}