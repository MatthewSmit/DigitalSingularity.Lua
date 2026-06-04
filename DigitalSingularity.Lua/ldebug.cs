namespace DigitalSingularity.Lua;

using System.Diagnostics.CodeAnalysis;

public static unsafe partial class Lua
{
    private static int pcRel(uint* pc, Proto* p)
    {
        return (int)(pc - p->code) - 1;
    }

    /* Active Lua function (given call info) */
    private static LClosure* ci_func(CallInfo* ci)
    {
        return clLvalue(s2v(ci->func.p));
    }

    private static void resethookcount(lua_State* L)
    {
        L->hookcount = L->basehookcount;
    }

    /*
     ** mark for entries in 'lineinfo' array that has absolute information in
     ** 'abslineinfo' array
     */
    private const int ABSLINEINFO = -0x80;

    /*
     ** MAXimum number of successive Instructions WiTHout ABSolute line
     ** information. (A power of two allows fast divisions.)
     */
    private const int MAXIWTHABS =
#if LUA_TEST
        3;
#else
        128;
#endif

    private static partial int luaG_getfuncline(Proto* f, int pc);

    private static partial string? luaG_findlocal(lua_State* L, CallInfo* ci, int n, StkId* pos);

    [DoesNotReturn]
    private static partial void luaG_typeerror(lua_State* L, TValue* o, string opname);

    [DoesNotReturn]
    private static partial void luaG_callerror(lua_State* L, TValue* o);

    [DoesNotReturn]
    private static partial void luaG_forerror(lua_State* L, TValue* o, string what);

    [DoesNotReturn]
    private static partial void luaG_concaterror(lua_State* L, TValue* p1, TValue* p2);

    [DoesNotReturn]
    private static partial void luaG_opinterror(lua_State* L, TValue* p1, TValue* p2, string msg);

    [DoesNotReturn]
    private static partial void luaG_tointerror(lua_State* L, TValue* p1, TValue* p2);

    [DoesNotReturn]
    private static partial void luaG_ordererror(lua_State* L, TValue* p1, TValue* p2);

    [DoesNotReturn]
    private static partial void luaG_errnnil(lua_State* L, LClosure* cl, int k);

    [DoesNotReturn]
    private static partial void luaG_runerror(lua_State* L, string fmt, params object[] args);

    private static partial string luaG_addinfo(lua_State* L, string msg, TString* src, int line);

    [DoesNotReturn]
    private static partial void luaG_errormsg(lua_State* L);

    private static partial bool luaG_traceexec(lua_State* L, uint* pc);

    private static partial bool luaG_tracecall(lua_State* L);
}
