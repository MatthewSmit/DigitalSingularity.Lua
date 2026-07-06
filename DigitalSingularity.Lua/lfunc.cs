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
        return sizeof(CClosure) + sizeof(TValue) * n;
    }

    private static long sizeLclosure(int n)
    {
        return sizeof(LClosure) + sizeof(UpVal*) * n;
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
}
