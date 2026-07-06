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
}
