namespace Amorice.Enums;

public enum PacketOpCode : byte
{
    Hello = 1,
    Ack,
    Nack,
    Send,
    SendFinal,
    Success,
    Error,
}