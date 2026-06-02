namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
// #if defined(LUA_USE_APICHECK)
// #include <assert.h>
// #define api_check(l,e,msg)	assert(e)
// #else	/* for testing */
// #define api_check(l,e,msg)	((void)(l), lua_assert((e) && msg))
// #endif

    /* Increments 'L->top.p', checking for stack overflows */
    private static void api_incr_top(lua_State* L)
    {
        L->top.p++;
        Debug.Assert(L->top.p <= L->ci->top.p, "stack overflow");
    }

// /*
//  ** macros that are executed whenever program enters the Lua core
//  ** ('lua_lock') and leaves the core ('lua_unlock')
//  */
// #if !defined(lua_lock)
// #define lua_lock(L)	((void) 0)
// #define lua_unlock(L)	((void) 0)
// #endif

    /*
    ** If a call returns too many multiple returns, the callee may not have
    ** stack space to accommodate all results. In this case, this macro
    ** increases its stack space ('L->ci->top.p').
    */
    private static void adjustresults(lua_State* L, int nres)
    {
        if (nres <= LUA_MULTRET && L->ci->top.p < L->top.p)
        {
            L->ci->top.p = L->top.p;
        }
    }

    /* Ensure the stack has at least 'n' elements */
    private static void api_checknelems(lua_State* L, int n)
    {
        Debug.Assert(n < L->top.p - L->ci->func.p, "not enough elements in the stack");
    }

    /* Ensure the stack has at least 'n' elements to be popped. (Some
     ** functions only update a slot after checking it for popping, but that
     ** is only an optimization for a pop followed by a push.)
     */
    private static void api_checkpop(lua_State* L, int n)
    {
        Debug.Assert(
            n < L->top.p - L->ci->func.p && L->tbclist.p < L->top.p - n,
            "not enough free elements in the stack");
    }
}
