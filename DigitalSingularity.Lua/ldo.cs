namespace DigitalSingularity.Lua;

using System.Diagnostics.CodeAnalysis;

public static unsafe partial class Lua
{
    /*
    ** Macro to check stack size and grow stack if needed.  Parameters
    ** 'pre'/'pos' allow the macro to preserve a pointer into the
    ** stack across reallocations, doing the work only when needed.
    ** It also allows the running of one GC step when the stack is
    ** reallocated.
    ** 'condmovestack' is used in heavy tests to force a stack reallocation
    ** at every check.
    */

// #if !defined(HARDSTACKTESTS)
// #define condmovestack(L,pre,pos)	((void)0)
// #else
// /* realloc stack keeping its size */
// #define condmovestack(L,pre,pos)  \
//   { int sz_ = stacksize(L); pre; luaD_reallocstack((L), sz_, 0); pos; }
// #endif
//
// #define luaD_checkstackaux(L,n,pre,pos)  \
// 	if (l_unlikely(L->stack_last.p - L->top.p <= (n))) \
// 	  { pre; luaD_growstack(L, n, 1); pos; } \
// 	else { condmovestack(L,pre,pos); }
//
// /* In general, 'pre'/'pos' are empty (nothing to save) */
// #define luaD_checkstack(L,n)	luaD_checkstackaux(L,n,(void)0,(void)0)

    private static nint savestack(lua_State* L, StackValue* pt)
    {
        return (nint)((byte*)pt - (byte*)L->stack.p);
    }

    private static StkId restorestack(lua_State* L, nint n)
    {
        return (StkId)((byte*)L->stack.p + n);
    }

    /* macro to check stack size, preserving 'p' */
    private static void checkstackp(lua_State* L, int n, ref StkId p)
    {
        if (L->stack_last.p - L->top.p <= n)
        {
            nint t = savestack(L, p); /* save 'p' */
            luaD_growstack(L, n, true);
            p = restorestack(L, t); /* 'pos' part: restore 'p' */
        }
        else
        {
#if HARDSTACKTESTS
            {
                int sz = stacksize(L);
                nint t = savestack(L, p); /* save 'p' */
                luaD_reallocstack(L, sz, false);
                p = restorestack(L, t); /* 'pos' part: restore 'p' */
            }
#endif
        }
    }

    /*
     ** Maximum depth for nested C calls, syntactical nested non-terminals,
     ** and other features implemented through recursion in C. (Value must
     ** fit in a 16-bit unsigned integer. It must also be compatible with
     ** the size of the C stack.)
     */
    private const int LUAI_MAXCCALLS = 200;

// LUAI_FUNC l_noret luaD_errerr (lua_State *L);
// LUAI_FUNC void luaD_seterrorobj (lua_State *L, TStatus errcode, StkId oldtop);
// LUAI_FUNC TStatus luaD_protectedparser (lua_State *L, ZIO *z,
//                                                   const char *name,
//                                                   const char *mode);

    private static partial void luaD_hook(lua_State* L, int @event, int line, int fTransfer, int nTransfer);
    
// LUAI_FUNC void luaD_hookcall (lua_State *L, CallInfo *ci);
// LUAI_FUNC int luaD_pretailcall (lua_State *L, CallInfo *ci, StkId func,
//                                               int narg1, int delta);
    private static partial CallInfo* luaD_precall(lua_State* L, StkId func, int nResults);
// LUAI_FUNC void luaD_call (lua_State *L, StkId func, int nResults);

    private static partial void luaD_callnoyield(lua_State* L, StackValue* func, int nResults);

// LUAI_FUNC TStatus luaD_closeprotected (lua_State *L, ptrdiff_t level,
//                                                      TStatus status);

    private static partial byte luaD_pcall(lua_State* L, Pfunc func, void* u, nint oldtop, nint ef);

    private static partial void luaD_poscall(lua_State* L, CallInfo* ci, int nres);

    private static partial bool luaD_reallocstack(lua_State* L, int newsize, bool raiseerror);

    private static partial bool luaD_growstack(lua_State* L, int n, bool raiseerror);
    
// LUAI_FUNC void luaD_shrinkstack (lua_State *L);
// LUAI_FUNC void luaD_inctop (lua_State *L);
// LUAI_FUNC int luaD_checkminstack (lua_State *L);

    [DoesNotReturn]
    private static partial void luaD_throw(lua_State* L, byte errcode);

    // LUAI_FUNC l_noret luaD_throwbaselevel (lua_State *L, TStatus errcode);

    private static partial byte luaD_rawrunprotected(lua_State* L, Pfunc f, void* ud);
}
