using SimuNES.Core.Enums;
using SimuNES.Core.Interfaces;
using SimuNES.Core.Structs;

namespace SimuNES.Core;

public class Cpu
{
    // CPU Registers
    public byte AccumulatorRegister, XIndexRegister, YIndexRegister, StatusRegister, StackPointer;
    public ushort ProgramCounter;

    // Internal execution state
    private byte currentOpcode;
    private ushort absoluteMemoryAddress;
    private byte fetchedValue;
    private int remainingCycles;
    private readonly IBusDevice bus;

    // Opcode lookup table
    private readonly Instruction[] instructionLookupTable = new Instruction[256];

    public int Cycles => remainingCycles;

    public Cpu(IBusDevice bus)
    {
        this.bus = bus;
    }

    public void SetFlag(Flags flag, bool value)
    {
        if (value)
            StatusRegister |= (byte)(1 << (int)flag);
        else
            StatusRegister &= (byte)~(1 << (int)flag);
    }

    public bool GetFlag(Flags flag)
    {
        return (StatusRegister & (1 << (int)flag)) != 0;
    }

    public void Reset()
    {
        AccumulatorRegister = 0;
        XIndexRegister = 0;
        YIndexRegister = 0;

        StackPointer = 0xFD;
        StatusRegister = 0x24;

        byte resetVectorLow = bus.Read(0xFFFC);
        byte resetVectorHigh = bus.Read(0xFFFD);

        ProgramCounter = (ushort)((resetVectorHigh << 8) | resetVectorLow);
        remainingCycles = 0;
    }

    public void Clock()
    {
        if (remainingCycles == 0)
        {
            currentOpcode = bus.Read(ProgramCounter);
            ProgramCounter++;

            SetFlag(Flags.Unused, true);

            Instruction instruction = instructionLookupTable[currentOpcode];
            remainingCycles = instruction.Cycles;

            byte additionalCyclesFromAddressingMode = instruction.AddressMode();
            byte additionalCyclesFromOperation = instruction.Operate();

            remainingCycles += additionalCyclesFromAddressingMode;
            remainingCycles += additionalCyclesFromOperation;
        }

        remainingCycles--;
    }

    // Addressing Modes

    private byte ImpliedAddressing()
    {
        fetchedValue = AccumulatorRegister;
        return 0;
    }

    private byte ImmediateAddressing()
    {
        absoluteMemoryAddress = ProgramCounter++;
        return 0;
    }

    private byte ZeroPageAddressing()
    {
        absoluteMemoryAddress = bus.Read(ProgramCounter);
        ProgramCounter++;
        absoluteMemoryAddress &= 0x00FF; // Ensure high byte is 0
        return 0;
    }

    private byte AbsoluteAddressing()
    {
        byte lowByte = bus.Read(ProgramCounter);
        ProgramCounter++;
        byte highByte = bus.Read(ProgramCounter);
        ProgramCounter++;
        absoluteMemoryAddress = (ushort)((highByte << 8) | lowByte);
        return 0;
    }

    private byte AbsoluteXAddressing()
    {
        byte lowByte = bus.Read(ProgramCounter);
        ProgramCounter++;
        byte highByte = bus.Read(ProgramCounter);
        ProgramCounter++;

        ushort baseAddress = (ushort)((highByte << 8) | lowByte);
        absoluteMemoryAddress = (ushort)(baseAddress + XIndexRegister);

        // Check for page crossing
        if ((absoluteMemoryAddress & 0xFF00) != (baseAddress & 0xFF00))
            return 1;
        return 0;
    }

    private byte AbsoluteYAddressing()
    {
        byte lowByte = bus.Read(ProgramCounter);
        ProgramCounter++;
        byte highByte = bus.Read(ProgramCounter);
        ProgramCounter++;

        ushort baseAddress = (ushort)((highByte << 8) | lowByte);
        absoluteMemoryAddress = (ushort)(baseAddress + YIndexRegister);

        // Check for page crossing
        if ((absoluteMemoryAddress & 0xFF00) != (baseAddress & 0xFF00))
            return 1;
        return 0;
    }

    private byte ZeroPageXAddressing()
    {
        // Read the zero page address
        byte zeroPageAddress = bus.Read(ProgramCounter);
        ProgramCounter++;

        // Add X register and wrap around within the zero page
        absoluteMemoryAddress = (ushort)((zeroPageAddress + XIndexRegister) & 0x00FF);

        // No additional cycles for zero page indexed addressing
        return 0;
    }

    private byte ZeroPageYAddressing()
    {
        // Read the zero page address
        byte zeroPageAddress = bus.Read(ProgramCounter);
        ProgramCounter++;

        // Add Y register and wrap around within the zero page
        // Note: Zero Page Y addressing is less common but exists for some instructions.
        // For example, LDX (Zero Page, Y) is a valid instruction.
        // The logic is identical to ZeroPageXAddressing for the address calculation.
        absoluteMemoryAddress = (ushort)((zeroPageAddress + YIndexRegister) & 0x00FF);

        // No additional cycles for zero page indexed addressing
        return 0;
    }
}
