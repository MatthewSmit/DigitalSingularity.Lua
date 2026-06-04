namespace DigitalSingularity.Lua;

internal sealed class lua_longjmp : Exception
{
    public lua_longjmp? previous;
//   jmp_buf b; TODO
    public byte status; /* error code */
}
