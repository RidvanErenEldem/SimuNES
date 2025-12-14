using SimuNES.Core.Enums;
using SimuNES.Core.Interfaces;

namespace SimuNES.Core;

public class Cpu(IBusDevice bus)
{
    public byte Accumulator, XIndex, YIndex, Status, StackPointer;
    public ushort ProgramCounter;
    private byte Opcode = 0x00;
    private readonly IBusDevice Bus = bus;
    public int Cycles = 0;
    public void SetFlag(Flags flag, bool value)
    {
        if (value)
            Status |= (byte)(1 << (int)flag);
        else
            Status &= (byte)~(1 << (int)flag);
    }

    public bool GetFlag(Flags flag)
    {
        return (Status & (1 << (int)flag)) != 0;
    }

    public void Reset()
    {
        Accumulator = XIndex = YIndex = 0;
        StackPointer = 0xFD;
        Status = 0x24;

        byte low = Bus.Read(0xFFFC);
        byte high = Bus.Read(0xFFFD);

        ProgramCounter = (ushort)((high << 8) | low);
    }

    public void Clock()
    {
        if (Cycles == 0)
        {
            Opcode = Bus.Read(ProgramCounter);
            ProgramCounter++;

            SetFlag(Flags.Unused, true);
        }

        if (Cycles > 0)
        {
            Cycles--;
        }
    }
}
