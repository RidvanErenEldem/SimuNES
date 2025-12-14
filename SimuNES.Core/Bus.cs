using SimuNES.Core.Interfaces;

namespace SimuNES.Core;

public class Bus : IBusDevice
{
    public byte[] ram;
    public Bus()
    {
        Console.WriteLine("Initilazing Bus...");
        ram = new byte[2048]; // 2KB RAM
        Console.WriteLine("Bus Initialized");
    }
    public byte Read(ushort address)
    {
        if (address >= 0x0000 && address <= 0x1FFF)
            return ram[address & 0x07FF];
        return 0;
    }

    public void Write(ushort address, byte value)
    {
        if (address >= 0x0000 && address <= 0x1FFF) //
            ram[address & 0x07FF] = value;
    }
}
