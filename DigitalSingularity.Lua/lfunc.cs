namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.InteropServices;

public static unsafe partial class Lua
{
    /*
    ** $Id: lfunc.h $
    ** Auxiliary functions to manipulate prototypes and closures
    ** See Copyright Notice in lua.h
    */

    private static long sizeCclosure(int n)
    {
        return Marshal.SizeOf<CClosure>() + sizeof(TValue) * (uint)(n - 1);
    }

    // #define sizeLclosure(n)  \
// 	(offsetof(LClosure, upvals) + sizeof(UpVal *) * cast_uint(n))
//
//
// /* test whether thread is in 'twups' list */
// #define isintwups(L)	(L->twups != L)

    /*
    ** maximum number of upvalues in a closure (both C and Lua). (Value
    ** must fit in a VM register.)
    */
    private const int MAXUPVAL = 255;

    private static bool upisopen(UpVal* up)
    {
        return up->v.p != &up->u.value;
    }

    private static StkId uplevel(UpVal* up)
    {
        Debug.Assert(upisopen(up));
        return (StkId)up->v.p;
    }

    // /*
// ** maximum number of misses before giving up the cache of closures
// ** in prototypes
// */
// #define MAXMISS		10
//
//
//
// /* special status to close upvalues preserving the top of the stack */
// #define CLOSEKTOP	(LUA_ERRERR + 1)
//
//
// LUAI_FUNC Proto *luaF_newproto (lua_State *L);

    private static partial CClosure* luaF_newCclosure(lua_State* L, int nupvals);

// LUAI_FUNC LClosure *luaF_newLclosure (lua_State *L, int nupvals);
// LUAI_FUNC void luaF_initupvals (lua_State *L, LClosure *cl);
// LUAI_FUNC UpVal *luaF_findupval (lua_State *L, StkId level);
// LUAI_FUNC void luaF_newtbcupval (lua_State *L, StkId level);
// LUAI_FUNC void luaF_closeupval (lua_State *L, StkId level);
// LUAI_FUNC StkId luaF_close (lua_State *L, StkId level, TStatus status, int yy);
// LUAI_FUNC void luaF_unlinkupval (UpVal *uv);
// LUAI_FUNC lu_mem luaF_protosize (Proto *p);
// LUAI_FUNC void luaF_freeproto (lua_State *L, Proto *f);
// LUAI_FUNC const char *luaF_getlocalname (const Proto *func, int local_number,
//                                          int pc);
}
