namespace DigitalSingularity.Lua;

internal unsafe struct lua_longjmp_data
{
    public lua_longjmp_data* previous;
    public byte status; // error code
}

internal sealed unsafe class lua_longjmp(lua_longjmp_data* jumpData) : Exception
{
    public lua_longjmp_data* JumpData { get; } = jumpData;
}
