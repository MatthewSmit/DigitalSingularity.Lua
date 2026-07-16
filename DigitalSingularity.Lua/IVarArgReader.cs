namespace DigitalSingularity.Lua;

internal interface IVarArgReader
{
    string? NextString();
    
    byte NextByte();
    
    int NextInt();
    
    long NextLong();
    
    double NextDouble();

    unsafe void* NextPointer();
}
