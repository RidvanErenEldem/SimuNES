using SimuNES.Core.Enums;
using SimuNES.Core.Interfaces;
using SimuNES.Core.Structs;

namespace SimuNES.Core;

/// <summary>
/// Represents the Ricoh 2A03 CPU (6502-based) used in the Nintendo Entertainment System.
/// Responsible for instruction fetching, decoding, execution and addressing mode handling.
/// </summary>
public class Cpu
{
    /// <summary>Accumulator register (A)</summary>
    public byte AccumulatorRegister;

    /// <summary>X index register (X)</summary>
    public byte XIndexRegister;

    /// <summary>Y index register (Y)</summary>
    public byte YIndexRegister;

    /// <summary>Processor status register (P)</summary>
    public byte StatusRegister;

    /// <summary>Stack pointer register (SP)</summary>
    public byte StackPointer;

    /// <summary>Program counter register (PC)</summary>
    public ushort ProgramCounter;

    /// <summary>Currently executing opcode</summary>
    private byte currentOpcode;

    /// <summary>Resolved absolute memory address after addressing mode calculation</summary>
    private ushort absoluteMemoryAddress;

    /// <summary>Fetched value used by instructions that require memory access</summary>
    private byte fetchedValue;

    /// <summary>Remaining CPU cycles for the current instruction</summary>
    private int remainingCycles;

    /// <summary>System bus used to communicate with memory and other devices</summary>
    private readonly IBusDevice bus;

    /// <summary>Opcode lookup table containing all 256 CPU instructions</summary>
    private readonly Instruction[] instructionLookupTable = new Instruction[256];

    /// <summary>
    /// Gets the number of cycles remaining before the CPU can fetch a new instruction.
    /// </summary>
    public int Cycles => remainingCycles;

    /// <summary>
    /// Initializes a new CPU instance with the given system bus.
    /// </summary>
    /// <param name="bus">System bus implementation</param>
    public Cpu(IBusDevice bus)
    {
        this.bus = bus;
    }

    /// <summary>
    /// Sets or clears a specific processor status flag.
    /// </summary>
    /// <param name="flag">Flag to modify</param>
    /// <param name="value">True to set the flag, false to clear it</param>
    public void SetFlag(Flags flag, bool value)
    {
        if (value)
            StatusRegister |= (byte)(1 << (int)flag);
        else
            StatusRegister &= (byte)~(1 << (int)flag);
    }

    /// <summary>
    /// Gets the current value of a processor status flag.
    /// </summary>
    /// <param name="flag">Flag to read</param>
    /// <returns>True if the flag is set, otherwise false</returns>
    public bool GetFlag(Flags flag)
    {
        return (StatusRegister & (1 << (int)flag)) != 0;
    }

    /// <summary>
    /// Resets the CPU to its power-up state.
    /// Reads the reset vector at memory addresses 0xFFFC and 0xFFFD.
    /// </summary>
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

    /// <summary>
    /// Executes a single CPU clock cycle.
    /// Handles instruction fetch, decode and execution timing.
    /// </summary>
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

    // =========================
    // Addressing Modes
    // =========================

    /// <summary>
    /// Implied addressing mode.
    /// Operand is implicitly the accumulator register.
    /// </summary>
    /// <returns>Additional cycles required</returns>
    private byte ImpliedAddressing()
    {
        fetchedValue = AccumulatorRegister;
        return 0;
    }

    /// <summary>
    /// Immediate addressing mode.
    /// Operand is the next byte in memory.
    /// </summary>
    /// <returns>Additional cycles required</returns>
    private byte ImmediateAddressing()
    {
        absoluteMemoryAddress = ProgramCounter++;
        return 0;
    }

    /// <summary>
    /// Zero Page addressing mode.
    /// Accesses memory within the first 256 bytes.
    /// </summary>
    /// <returns>Additional cycles required</returns>
    private byte ZeroPageAddressing()
    {
        absoluteMemoryAddress = bus.Read(ProgramCounter);
        ProgramCounter++;
        absoluteMemoryAddress &= 0x00FF;
        return 0;
    }

    /// <summary>
    /// Absolute addressing mode.
    /// Uses a full 16-bit memory address.
    /// </summary>
    /// <returns>Additional cycles required</returns>
    private byte AbsoluteAddressing()
    {
        byte lowByte = bus.Read(ProgramCounter++);
        byte highByte = bus.Read(ProgramCounter++);
        absoluteMemoryAddress = (ushort)((highByte << 8) | lowByte);
        return 0;
    }

    /// <summary>
    /// Absolute X indexed addressing mode.
    /// Adds the X register to a 16-bit base address.
    /// </summary>
    /// <returns>1 if a page boundary is crossed, otherwise 0</returns>
    private byte AbsoluteXAddressing()
    {
        byte lowByte = bus.Read(ProgramCounter++);
        byte highByte = bus.Read(ProgramCounter++);

        ushort baseAddress = (ushort)((highByte << 8) | lowByte);
        absoluteMemoryAddress = (ushort)(baseAddress + XIndexRegister);

        return (absoluteMemoryAddress & 0xFF00) != (baseAddress & 0xFF00) ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Absolute Y indexed addressing mode.
    /// Adds the Y register to a 16-bit base address.
    /// </summary>
    /// <returns>1 if a page boundary is crossed, otherwise 0</returns>
    private byte AbsoluteYAddressing()
    {
        byte lowByte = bus.Read(ProgramCounter++);
        byte highByte = bus.Read(ProgramCounter++);

        ushort baseAddress = (ushort)((highByte << 8) | lowByte);
        absoluteMemoryAddress = (ushort)(baseAddress + YIndexRegister);

        return (absoluteMemoryAddress & 0xFF00) != (baseAddress & 0xFF00) ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Zero Page X indexed addressing mode.
    /// Adds X register to an 8-bit zero page address with wraparound.
    /// </summary>
    /// <returns>No additional cycles</returns>
    private byte ZeroPageXAddressing()
    {
        byte zeroPageAddress = bus.Read(ProgramCounter++);
        absoluteMemoryAddress = (ushort)((zeroPageAddress + XIndexRegister) & 0x00FF);
        return 0;
    }

    /// <summary>
    /// Zero Page Y indexed addressing mode.
    /// Adds Y register to an 8-bit zero page address with wraparound.
    /// </summary>
    /// <returns>No additional cycles</returns>
    private byte ZeroPageYAddressing()
    {
        byte zeroPageAddress = bus.Read(ProgramCounter++);
        absoluteMemoryAddress = (ushort)((zeroPageAddress + YIndexRegister) & 0x00FF);
        return 0;
    }

    /// <summary>
    /// Indirect addressing mode.
    /// Emulates the original 6502 hardware bug when crossing page boundaries.
    /// </summary>
    /// <returns>No additional cycles</returns>
    private byte IndirectAddressing()
    {
        byte lowBytePointer = bus.Read(ProgramCounter++);
        byte highBytePointer = bus.Read(ProgramCounter++);

        ushort pointerAddress = (ushort)((highBytePointer << 8) | lowBytePointer);

        if ((pointerAddress & 0x00FF) == 0x00FF)
        {
            absoluteMemoryAddress =
                (ushort)((bus.Read((ushort)(pointerAddress & 0xFF00)) << 8) | bus.Read(pointerAddress));
        }
        else
        {
            absoluteMemoryAddress =
                (ushort)((bus.Read((ushort)(pointerAddress + 1)) << 8) | bus.Read(pointerAddress));
        }

        return 0;
    }

    /// <summary>
    /// Indexed Indirect (X) addressing mode.
    /// Adds X register to a zero page pointer and reads the final address.
    /// </summary>
    /// <returns>No additional cycles</returns>
    private byte IndirectXAddressing()
    {
        byte t = bus.Read(ProgramCounter++);
        ushort ptr = (ushort)((t + XIndexRegister) & 0x00FF);

        byte lowByte = bus.Read(ptr);
        byte highByte = bus.Read((ushort)((ptr + 1) & 0x00FF));

        absoluteMemoryAddress = (ushort)((highByte << 8) | lowByte);
        return 0;
    }
}
