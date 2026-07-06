namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /*
     ** $Id: lfunc.h $
     ** Auxiliary functions to manipulate prototypes and closures
     ** See Copyright Notice in lua.h
     */

    internal static long sizeCclosure(int n)
    {
        return sizeof(CClosure) + sizeof(TValue) * Math.Max(0, n - 1);
    }

    private static long sizeLclosure(int n)
    {
        return sizeof(LClosure) + sizeof(UpVal*) * Math.Max(0, n - 1);
    }

    /* test whether thread is in 'twups' list */
    private static bool isintwups(lua_State* L)
    {
        return L->twups != L;
    }

    /*
     ** maximum number of upvalues in a closure (both C and Lua). (Value
     ** must fit in a VM register.)
     */
    private const int MAXUPVAL = 255;

    internal static bool upisopen(UpVal* up)
    {
        return up->v.p != &up->u.value;
    }

    private static StkId uplevel(UpVal* up)
    {
        Debug.Assert(upisopen(up));
        return (StkId)up->v.p;
    }

    /* special status to close upvalues preserving the top of the stack */
    private const byte CLOSEKTOP = LUA_ERRERR + 1;

    internal static partial Proto* luaF_newproto(lua_State* L);

    internal static partial CClosure* luaF_newCclosure(lua_State* L, int nupvals);

    internal static partial LClosure* luaF_newLclosure(lua_State* L, int nupvals);

    internal static partial void luaF_initupvals(lua_State* L, LClosure* cl);

    internal static partial UpVal* luaF_findupval(lua_State* L, StkId level);

    internal static partial void luaF_newtbcupval(lua_State* L, StkId level);

    internal static partial void luaF_closeupval(lua_State* L, StkId level);

    internal static partial StkId luaF_close(lua_State* L, StkId level, byte status, bool yy);

    internal static partial void luaF_unlinkupval(UpVal* uv);


    internal static partial long luaF_protosize(Proto* p);

    private static partial void luaF_freeproto(lua_State* L, Proto* f);

    internal static partial string? luaF_getlocalname(Proto* func, int localNumber, int pc);
}
