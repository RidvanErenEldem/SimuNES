namespace SimuNES.Core.Structs;

public struct Instruction
{
    public string Name;
    public int Cycles;
    public Func<byte> Operate;
    public Func<byte> AddressMode;
}
