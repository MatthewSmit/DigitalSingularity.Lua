namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /*
    ** $Id: lundump.h $
    ** load precompiled Lua chunks
    ** See Copyright Notice in lua.h
    */

    /* data to catch conversion errors */
    private const string LUAC_DATA = "\x19\x93" + "\r\n\x1a" + "\n";

    private const int LUAC_INT = -0x5678;
    private const int LUAC_INST = 0x12345678;
    private const double LUAC_NUM = -370.5;

    /*
    ** Encode major-minor version in one byte, one nibble for each
    */
    private const int LUAC_VERSION = LUA_VERSION_MAJOR_N * 16 + LUA_VERSION_MINOR_N;

    private const int LUAC_FORMAT = 0; /* this is the official format */

    /* load one chunk; from lundump.c */
    private static partial LClosure* luaU_undump(lua_State* L, Zio* Z, string name, int @fixed);

    /* dump one chunk; from ldump.c */
    private static partial int luaU_dump(lua_State* L, Proto* f, lua_Writer w, void* data, int strip);
}
