namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public static unsafe partial class Lua
{
    // Macro to check stack size and grow stack if needed.  Parameters
    // 'pre'/'pos' allow the macro to preserve a pointer into the
    // stack across reallocations, doing the work only when needed.
    // It also allows the running of one GC step when the stack is
    // reallocated.
    // 'condmovestack' is used in heavy tests to force a stack reallocation
    // at every check.

    /// <summary>
    /// In general, 'pre'/'pos' are empty (nothing to save)
    /// </summary>
    private static void luaD_checkstack(lua_State* L, int n)
    {
        if (L->stack_last.p - L->top.p <= n)
        {
            luaD_growstack(L, n, true);
        }
        else
        {
#if HARDSTACKTESTS
            int sz_ = stacksize(L);
            luaD_reallocstack(L, sz_, false);
#endif
        }
    }

    private static nint savestack(lua_State* L, StackValue* pt)
    {
        return (nint)((byte*)pt - (byte*)L->stack.p);
    }

    private static StkId restorestack(lua_State* L, nint n)
    {
        return (StkId)((byte*)L->stack.p + n);
    }

    /// <summary>
    /// macro to check stack size, preserving 'p'
    /// </summary>
    private static void checkstackp(lua_State* L, int n, ref StkId p)
    {
        if (L->stack_last.p - L->top.p <= n)
        {
            nint t = savestack(L, p); // save 'p'
            luaD_growstack(L, n, true);
            p = restorestack(L, t); // 'pos' part: restore 'p'
        }
        else
        {
#if HARDSTACKTESTS
            {
                int sz = stacksize(L);
                nint t = savestack(L, p); // save 'p'
                luaD_reallocstack(L, sz, false);
                p = restorestack(L, t); // 'pos' part: restore 'p'
            }
#endif
        }
    }

    /// <summary>
    /// Maximum depth for nested C calls, syntactical nested non-terminals,
    /// and other features implemented through recursion in C. (Value must
    /// fit in a 16-bit unsigned integer. It must also be compatible with
    /// the size of the C stack.)
    /// </summary>
    internal const int LUAI_MAXCCALLS =
#if LUA_TEST
        180;
#else
        200;
#endif
    
    private static bool errorstatus(byte s)
    {
        return s > LUA_YIELD;
    }

    /// <summary>
    /// these macros allow user-specific actions when a thread is
    /// resumed/yielded.
    /// </summary>
    private static void luai_userstateresume(lua_State* L, int n)
    {
    }

    private static void luai_userstateyield(lua_State* L, int n) { }

//
// {======================================================
// Error-recovery functions
// =======================================================
//
//
// chained list of long jump buffers
// typedef struct lua_longjmp {
// struct lua_longjmp *previous;
// jmp_buf b;
// volatile TStatus status; // error code
// } lua_longjmp;
//
//
//
// LUAI_THROW/LUAI_TRY define how Lua does exception handling. By
// default, Lua handles errors with exceptions when compiling as
// C++ code, with _longjmp/_setjmp when available (POSIX), and with
// longjmp/setjmp otherwise.
//
// #if !defined(LUAI_THROW) // {
// C++ exceptions
// #define LUAI_THROW(L,c)		throw(c)
//
// static void LUAI_TRY (lua_State *L, lua_longjmp *c, Pfunc f, void *ud) {
// try {
// f(L, ud); // call function protected
// }
// catch (lua_longjmp *c1) { // Lua error
// if (c1 != c) // not the correct level?
// throw; // rethrow to upper level
// }
// catch (...) { // non-Lua exception
// c->status = -1; // create some error code
// }
// }
// #endif // }

    private static void luaD_seterrorobj(lua_State* L, byte errcode, StkId oldtop)
    {
        if (errcode == LUA_ERRMEM)
        {
            // memory error?
            setsvalue2s(L, oldtop, G(L)->memerrmsg); // reuse preregistered msg.
        }
        else
        {
            Debug.Assert(errorstatus(errcode)); // must be a real error
            Debug.Assert(!ttisnil(s2v(L->top.p - 1))); // with a non-nil object
            setobjs2s(L, oldtop, L->top.p - 1); // move it to 'oldtop'
        }

        L->top.p = oldtop + 1; // top goes back to old top plus error object
    }

    [DoesNotReturn]
    private static void luaD_throw(lua_State* L, byte errcode)
    {
        if (L->errorJmp != null)
        {
            // thread has an error handler?
            L->errorJmp->status = errcode; // set status
            throw new lua_longjmp(L->errorJmp); // jump to it
        }

        // thread has no error handler
        global_State* g = G(L);
        lua_State* mainth = mainthread(g);
        errcode = luaE_resetthread(L, errcode); // close all upvalues
        L->status = errcode;
        if (mainth->errorJmp != null)
        {
            // main thread has a handler?
            setobjs2s(L, mainth->top.p++, L->top.p - 1); // copy error obj.
            luaD_throw(mainth, errcode); // re-throw in main thread
        }
        else
        {
            // no handler at all; abort
            if (g->panic != default)
            {
                // panic function?
                lua_unlock(L);
                g->panic.Call(L); // call panic function (last chance to jump out)
            }

            throw new lua_longjmp(null);
        }
    }

    [DoesNotReturn]
    private static void luaD_throwbaselevel(lua_State* L, byte errcode)
    {
        if (L->errorJmp != null)
        {
            // unroll error entries up to the first level
            while (L->errorJmp->previous != null)
            {
                L->errorJmp = L->errorJmp->previous;
            }
        }

        luaD_throw(L, errcode);
    }

    internal static byte luaD_rawrunprotected(lua_State* L, Pfunc f, void* ud)
    {
        uint oldnCcalls = L->nCcalls;
        lua_longjmp_data lj = new()
        {
            status = LUA_OK,
            previous = L->errorJmp, // chain new error handler
        };
        L->errorJmp = &lj;
        try
        {
            f(L, ud);
        }
        catch (lua_longjmp c1)
        {
            if (c1.JumpData != &lj) // not the correct level?
            {
                throw; // rethrow to upper level
            }
        }
        // TODO
        // catch (Exception) // non-Lua exception
        // {
        // lj.status = unchecked((byte)-1); // create some error code
        // }

        L->errorJmp = lj.previous; // restore old error handler
        L->nCcalls = oldnCcalls;
        return lj.status;
    }

    // {==================================================================
    // Stack reallocation
    // ===================================================================

    /// <summary>
    /// some stack space for error handling
    /// </summary>
    private const int STACKERRSPACE = 200;

    // LUAI_MAXSTACK limits the size of the Lua stack.
    // It must fit into INT_MAX/2.
#if LUA_TEST
    /// <summary>
    /// Reduce maximum stack size to make stack-overflow tests run faster.
    /// (But value is still large enough to overflow smaller integers.)
    /// </summary>
    private const int LUAI_MAXSTACK = 68000;
#else
    private const int LUAI_MAXSTACK = 1000000;
#endif

    /// <summary>
    /// Minimum between LUAI_MAXSTACK and MAXSTACK_BYSIZET
    /// (Maximum size for the stack must respect size_t.)
    /// </summary>
    private const int MAXSTACK = LUAI_MAXSTACK;

    /// <summary>
    /// stack size with extra space for error handling
    /// </summary>
    private const int ERRORSTACKSIZE = MAXSTACK + STACKERRSPACE;

    /// <summary>
    /// raise a stack error while running the message handler
    /// </summary>
    private static void luaD_errerr(lua_State* L)
    {
        TString* msg = luaS_newliteral(L, "error in error handling");
        setsvalue2s(L, L->top.p, msg);
        L->top.p++; // assume EXTRA_STACK
        luaD_throw(L, LUA_ERRERR);
    }

    /// <summary>
    /// Check whether stack has enough space to run a simple function (such
    /// as a finaliser): At least BASIC_STACK_SIZE in the Lua stack and
    /// 2 slots in the C stack.
    /// </summary>
    private static bool luaD_checkminstack(lua_State* L)
    {
        return stacksize(L) < MAXSTACK - BASIC_STACK_SIZE && getCcalls(L) < LUAI_MAXCCALLS - 2;
    }

    // In ISO C, any pointer use after the pointer has been deallocated is
    // undefined behaviour. So, before a stack reallocation, all pointers
    // should be changed to offsets, and after the reallocation they should
    // be changed back to pointers. As during the reallocation the pointers
    // are invalid, the reallocation cannot run emergency collections.
    // Alternatively, we can use the old address after the deallocation.
    // That is not strict ISO C, but seems to work fine everywhere.
    // The following macro chooses how strict is the code.
#if LUAI_STRICT_ADDRESS
    /// <summary>
    /// Change all pointers to the stack into offsets.
    /// </summary>
    private static void relstack(lua_State* L)
    {
        CallInfo* ci;
        UpVal* up;
        L->top.offset = savestack(L, L->top.p);
        L->tbclist.offset = savestack(L, L->tbclist.p);
        for (up = L->openupval; up != null; up = up->u.open.next)
        {
            up->v.offset = savestack(L, uplevel(up));
        }

        for (ci = L->ci; ci != null; ci = ci->previous)
        {
            ci->top.offset = savestack(L, ci->top.p);
            ci->func.offset = savestack(L, ci->func.p);
        }
    }

    /// <summary>
    /// Change back all offsets into pointers.
    /// </summary>
    private static void correctstack(lua_State* L, StkId oldstack)
    {
        L->top.p = restorestack(L, L->top.offset);
        L->tbclist.p = restorestack(L, L->tbclist.offset);
        for (UpVal* up = L->openupval; up != null; up = up->u.open.next)
        {
            up->v.p = s2v(restorestack(L, up->v.offset));
        }

        for (CallInfo* ci = L->ci; ci != null; ci = ci->previous)
        {
            ci->top.p = restorestack(L, ci->top.offset);
            ci->func.p = restorestack(L, ci->func.offset);
            if (isLua(ci))
            {
                ci->u.l.trap = 1; // signal to update 'trap' in 'luaV_execute'
            }
        }
    }
#else
    // Assume that it is fine to use an address after its deallocation,
    // as long as we do not dereference it.

    private static void relstack(lua_State* L)
    {
        // do nothing
    }

    /// <summary>
    /// Correct pointers into 'oldstack' to point into 'L-&gt;stack'.
    /// </summary>
    private static void correctstack(lua_State* L, StkId oldstack)
    {
        StkId newstack = L->stack.p;
        if (oldstack == newstack)
        {
            return;
        }

        L->top.p = L->top.p - oldstack + newstack;
        L->tbclist.p = L->tbclist.p - oldstack + newstack;
        for (UpVal* up = L->openupval; up != null; up = up->u.open.next)
        {
            up->v.p = s2v(uplevel(up) - oldstack + newstack);
        }

        for (CallInfo* ci = L->ci; ci != null; ci = ci->previous)
        {
            ci->top.p = ci->top.p - oldstack + newstack;
            ci->func.p = ci->func.p - oldstack + newstack;
            if (isLua(ci))
            {
                ci->u.l.trap = 1; // signal to update 'trap' in 'luaV_execute'
            }
        }
    }
#endif

    /// <summary>
    /// Reallocate the stack to a new size, correcting all pointers into it.
    /// In case of allocation error, raise an error or return false according
    /// to 'raiseerror'.
    /// </summary>
    private static bool luaD_reallocstack(lua_State* L, int newsize, bool raiseerror)
    {
        int oldsize = stacksize(L);
        StkId oldstack = L->stack.p;
        bool oldgcstop = G(L)->gcstopem;
        Debug.Assert(newsize is <= MAXSTACK or ERRORSTACKSIZE);
        relstack(L); // change pointers to offsets
        G(L)->gcstopem = true; // stop emergency collection
        StkId newstack = luaM_reallocvector<StackValue>(L, oldstack, oldsize + EXTRA_STACK, newsize + EXTRA_STACK);
        G(L)->gcstopem = oldgcstop; // restore emergency collection
        if (newstack == null)
        {
            // reallocation failed?
            correctstack(L, oldstack); // change offsets back to pointers
            if (raiseerror)
            {
                luaM_error(L);
            }

            else
            {
                return false; // do not raise an error
            }
        }

        L->stack.p = newstack;
        correctstack(L, oldstack); // change offsets back to pointers
        L->stack_last.p = L->stack.p + newsize;
        for (int i = oldsize + EXTRA_STACK; i < newsize + EXTRA_STACK; i++)
        {
            setnilvalue(s2v(newstack + i)); // erase new segment
        }

        return true;
    }

    /// <summary>
    /// Try to grow the stack by at least 'n' elements. When 'raiseerror'
    /// is true, raises any error; otherwise, return 0 in case of errors.
    /// </summary>
    private static bool luaD_growstack(lua_State* L, int n, bool raiseerror)
    {
        int size = stacksize(L);
        if (size > MAXSTACK)
        {
            // if stack is larger than maximum, thread is already using the
            // extra space reserved for errors, that is, thread is handling
            // a stack error; cannot grow further than that.
            Debug.Assert(stacksize(L) == ERRORSTACKSIZE);
            if (raiseerror)
            {
                luaD_errerr(L); // stack error inside message handler
            }

            return false; // if not 'raiseerror', just signal it
        }

        if (n < MAXSTACK)
        {
            // avoids arithmetic overflows
            int newsize = size + (size >> 1); // tentative new size (size * 1.5)
            int needed = (int)(L->top.p - L->stack.p) + n;
            if (newsize > MAXSTACK) // cannot cross the limit
            {
                newsize = MAXSTACK;
            }

            if (newsize < needed) // but must respect what was asked for
            {
                newsize = needed;
            }

            if (newsize <= MAXSTACK)
            {
                return luaD_reallocstack(L, newsize, raiseerror);
            }
        }

        // else stack overflow
        // add extra size to be able to handle the error message
        luaD_reallocstack(L, ERRORSTACKSIZE, raiseerror);
        if (raiseerror)
        {
            luaG_runerror(L, "stack overflow");
        }

        return false;
    }

    /// <summary>
    /// Compute how much of the stack is being used, by computing the
    /// maximum top of all call frames in the stack and the current top.
    /// </summary>
    private static int stackinuse(lua_State* L)
    {
        StkId lim = L->top.p;
        for (CallInfo* ci = L->ci; ci != null; ci = ci->previous)
        {
            if (lim < ci->top.p)
            {
                lim = ci->top.p;
            }
        }

        Debug.Assert(lim <= L->stack_last.p + EXTRA_STACK);
        int res = (int)(lim - L->stack.p) + 1; // part of stack in use
        if (res < LUA_MINSTACK)
        {
            res = LUA_MINSTACK; // ensure a minimum size
        }

        return res;
    }

    /// <summary>
    /// If stack size is more than 3 times the current use, reduce that size
    /// to twice the current use. (So, the final stack size is at most 2/3 the
    /// previous size, and half of its entries are empty.)
    /// As a particular case, if stack was handling a stack overflow and now
    /// it is not, 'max' (limited by MAXSTACK) will be smaller than
    /// stacksize (equal to ERRORSTACKSIZE in this case), and so the stack
    /// will be reduced to a "regular" size.
    /// </summary>
    private static void luaD_shrinkstack(lua_State* L)
    {
        int inuse = stackinuse(L);
        int max = inuse > MAXSTACK / 3 ? MAXSTACK : inuse * 3;
        // if thread is currently not handling a stack overflow and its
        // size is larger than maximum "reasonable" size, shrink it
        if (inuse <= MAXSTACK && stacksize(L) > max)
        {
            int nsize = inuse > MAXSTACK / 2 ? MAXSTACK : inuse * 2;
            luaD_reallocstack(L, nsize, false); // ok if that fails
        }
        else // don't change stack
        {
#if HARDSTACKTESTS
            int sz_ = stacksize(L);
            luaD_reallocstack(L, sz_, false);
#endif
        }

        luaE_shrinkCI(L); // shrink CI list
    }

    private static void luaD_inctop(lua_State* L)
    {
        L->top.p++;
        luaD_checkstack(L, 1);
    }

    // }==================================================================
    
    /// <summary>
    /// Call a hook for the given event. Make sure there is a hook to be
    /// called. (Both 'L-&gt;hook' and 'L-&gt;hookmask', which trigger this
    /// function, can be changed asynchronously by signals.)
    /// </summary>
    private static void luaD_hook(lua_State* L, int @event, int line, int fTransfer, int nTransfer)
    {
        lua_Hook hook = L->hook;
        if (hook != null && L->allowhook)
        {
            // make sure there is a hook
            CallInfo* ci = L->ci;
            nint top = savestack(L, L->top.p); // preserve original 'top'
            nint ci_top = savestack(L, ci->top.p); // idem for 'ci->top'
            lua_Debug ar = new()
            {
                @event = @event,
                currentline = line,
                i_ci = ci,
            };
            L->transferinfo.ftransfer = fTransfer;
            L->transferinfo.ntransfer = nTransfer;
            if (isLua(ci) && L->top.p < ci->top.p)
            {
                L->top.p = ci->top.p; // protect entire activation register
            }

            luaD_checkstack(L, LUA_MINSTACK); // ensure minimum stack size
            if (ci->top.p < L->top.p + LUA_MINSTACK)
            {
                ci->top.p = L->top.p + LUA_MINSTACK;
            }

            L->allowhook = false; // cannot call hooks inside a hook
            ci->callstatus |= CIST_HOOKED;
            lua_unlock(L);
            hook(L, ref ar);
            lua_lock(L);
            Debug.Assert(!L->allowhook);
            L->allowhook = true;
            ci->top.p = restorestack(L, ci_top);
            L->top.p = restorestack(L, top);
            ci->callstatus &= ~CIST_HOOKED;
        }
    }

    /// <summary>
    /// Executes a call hook for Lua functions. This function is called
    /// whenever 'hookmask' is not zero, so it checks whether call hooks are
    /// active.
    /// </summary>
    private static void luaD_hookcall(lua_State* L, CallInfo* ci)
    {
        L->oldpc = 0; // set 'oldpc' for new function
        if ((L->hookmask & LUA_MASKCALL) != 0)
        {
            // is call hook on?
            int @event = (ci->callstatus & CIST_TAIL) != 0
                ? LUA_HOOKTAILCALL
                : LUA_HOOKCALL;
            Proto* p = ci_func(ci)->p;
            ci->u.l.savedpc++; // hooks assume 'pc' is already incremented
            luaD_hook(L, @event, -1, 1, p->numparams);
            ci->u.l.savedpc--; // correct 'pc'
        }
    }

    /// <summary>
    /// Executes a return hook for Lua and C functions and sets/corrects
    /// 'oldpc'. (Note that this correction is needed by the line hook, so it
    /// is done even when return hooks are off.)
    /// </summary>
    private static void rethook(lua_State* L, CallInfo* ci, int nres)
    {
        if ((L->hookmask & LUA_MASKRET) != 0)
        {
            // is return hook on?
            StkId firstres = L->top.p - nres; // index of first result
            int delta = 0; // correction for vararg functions
            if (isLua(ci))
            {
                Proto* p = ci_func(ci)->p;
                if ((p->flag & PF_VAHID) != 0)
                {
                    delta = ci->u.l.nextraargs + p->numparams + 1;
                }
            }

            ci->func.p += delta; // if vararg, back to virtual 'func'
            int ftransfer = (int)(firstres - ci->func.p);
            luaD_hook(L, LUA_HOOKRET, -1, ftransfer, nres); // call it
            ci->func.p -= delta;
        }

        if (isLua(ci = ci->previous))
        {
            L->oldpc = pcRel(ci->u.l.savedpc, ci_func(ci)->p); // set 'oldpc'
        }
    }

    /// <summary>
    /// Check whether 'func' has a '__call' metafield. If so, put it in the
    /// stack, below original 'func', so that 'luaD_precall' can call it.
    /// Raise an error if there is no '__call' metafield.
    /// Bits CIST_CCMT in status count how many _call metamethods were
    /// invoked and how many corresponding extra arguments were pushed.
    /// (This count will be saved in the 'callstatus' of the call).
    ///  Raise an error if this counter overflows.
    /// </summary>
    private static uint tryfuncTM(lua_State* L, StkId func, uint status)
    {
        TValue* tm = luaT_gettmbyobj(L, s2v(func), TMS.CALL);
        if (ttisnil(tm)) // no metamethod?
        {
            luaG_callerror(L, s2v(func));
        }

        for (StkId p = L->top.p; p > func; p--) // open space for metamethod
        {
            setobjs2s(L, p, p - 1);
        }

        L->top.p++; // stack space pre-allocated by the caller
        setobj2s(L, func, tm); // metamethod is the new function to be called
        if ((status & MAX_CCMT) == MAX_CCMT) // is counter full?
        {
            luaG_runerror(L, "'__call' chain too long");
        }

        return status + (1u << CIST_CCMT); // increment counter
    }

    /// <summary>
    /// Generic case for 'moveresult'
    /// </summary>
    private static void genmoveresults(lua_State* L, StkId res, int nres, int wanted)
    {
        StkId firstresult = L->top.p - nres; // index of first result
        if (nres > wanted) // extra results?
        {
            nres = wanted; // don't need them
        }

        int i;
        for (i = 0; i < nres; i++) // move all results to correct place
        {
            setobjs2s(L, res + i, firstresult + i);
        }

        for (; i < wanted; i++) // complete wanted number of results
        {
            setnilvalue(s2v(res + i));
        }

        L->top.p = res + wanted; // top points after the last result
    }

    /// <summary>
    /// Given 'nres' results at 'firstResult', move 'fwanted-1' of them
    /// to 'res'.  Handle most typical cases (zero results for commands,
    /// one result for expressions, multiple results for tail calls/single
    /// parameters) separated. The flag CIST_TBC in 'fwanted', if set,
    /// forces the switch to go to the default case.
    /// </summary>
    private static void moveresults(lua_State* L, StkId res, int nres, uint fwanted)
    {
        switch (fwanted)
        {
            // handle typical cases separately
            case 0 + 1: // no values needed
                L->top.p = res;
                return;

            case 1 + 1: // one value needed
                if (nres == 0) // no results?
                {
                    setnilvalue(s2v(res)); // adjust with nil
                }
                else // at least one result
                {
                    setobjs2s(L, res, L->top.p - nres); // move it to proper place
                }

                L->top.p = res + 1;
                return;

            case LUA_MULTRET + 1:
                genmoveresults(L, res, nres, nres); // we want all results
                break;

            default:
                // two/more results and/or to-be-closed variables
                int wanted = get_nresults(fwanted);
                if ((fwanted & CIST_TBC) != 0)
                {
                    // to-be-closed variables?
                    L->ci->u2.nres = nres;
                    L->ci->callstatus |= CIST_CLSRET; // in case of yields
                    res = luaF_close(L, res, CLOSEKTOP, true);
                    L->ci->callstatus &= ~CIST_CLSRET;
                    if (L->hookmask != 0)
                    {
                        // if needed, call hook after '__close's
                        nint savedres = savestack(L, res);
                        rethook(L, L->ci, nres);
                        res = restorestack(L, savedres); // hook can move stack
                    }

                    if (wanted == LUA_MULTRET)
                    {
                        wanted = nres; // we want all results
                    }
                }

                genmoveresults(L, res, nres, wanted);
                break; }
    }

    /// <summary>
    /// Finishes a function call: calls hook if necessary, moves current
    /// number of results to proper place, and returns to previous call
    /// info. If function has to close variables, hook must be called after
    /// that.
    /// </summary>
    private static void luaD_poscall(lua_State* L, CallInfo* ci, int nres)
    {
        uint fwanted = ci->callstatus & (CIST_TBC | CIST_NRESULTS);
        if (L->hookmask != 0 && (fwanted & CIST_TBC) == 0)
        {
            rethook(L, ci, nres);
        }

        // move results to proper place
        moveresults(L, ci->func.p, nres, fwanted);
        // function cannot be in any of these cases when returning
        Debug.Assert((ci->callstatus & (CIST_HOOKED | CIST_YPCALL | CIST_FIN | CIST_CLSRET)) == 0);
        L->ci = ci->previous; // back to caller (after closing variables)
    }

    private static CallInfo* next_ci(lua_State* L)
    {
        return L->ci->next != null ? L->ci->next : luaE_extendCI(L);
    }

    /// <summary>
    /// Allocate and initialize CallInfo structure. At this point, the
    /// only valid fields in the call status are number of results,
    /// CIST_C (if it's a C function), and number of extra arguments.
    /// (All these bit-fields fit in 16-bit values.)
    /// </summary>
    private static CallInfo* prepCallInfo(
        lua_State* L,
        StkId func,
        uint status,
        StkId top)
    {
        CallInfo* ci = L->ci = next_ci(L); // new frame
        ci->func.p = func;
        Debug.Assert((status & ~(CIST_NRESULTS | CIST_C | MAX_CCMT)) == 0);
        ci->callstatus = status;
        ci->top.p = top;
        return ci;
    }

    /// <summary>
    /// precall for C functions
    /// </summary>
    private static int precallC(lua_State* L, StkId func, uint status, CFunction f)
    {
        checkstackp(L, LUA_MINSTACK, ref func); // ensure minimum stack size
        CallInfo* ci = L->ci = prepCallInfo(L, func, status | CIST_C, L->top.p + LUA_MINSTACK);
        Debug.Assert(ci->top.p <= L->stack_last.p);
        if ((L->hookmask & LUA_MASKCALL) != 0)
        {
            int narg = (int)(L->top.p - func) - 1;
            luaD_hook(L, LUA_HOOKCALL, -1, 1, narg);
        }

        lua_unlock(L);
        int n = f.Call(L); // do the actual call
        lua_lock(L);
        api_checknelems(L, n);
        luaD_poscall(L, ci, n);
        return n;
    }

    /// <summary>
    /// Prepare a function for a tail call, building its call info on top
    /// of the current call info. 'narg1' is the number of arguments plus 1
    /// (so that it includes the function itself). Return the number of
    /// results, if it was a C function, or -1 for a Lua function.
    /// </summary>
    private static int luaD_pretailcall(lua_State* L, CallInfo* ci, StkId func, int narg1, int delta)
    {
        uint status = LUA_MULTRET + 1;
        retry:
        switch (ttypetag(s2v(func)))
        {
            case LUA_VCCL: // C closure
                return precallC(L, func, status, clCvalue(s2v(func))->f);

            case LUA_VLCF: // light C function
                return precallC(L, func, status, fvalue(s2v(func)));

            case LUA_VLCL:
                {
                    // Lua function
                    Proto* p = clLvalue(s2v(func))->p;
                    int fsize = p->maxstacksize; // frame size
                    int nfixparams = p->numparams;
                    checkstackp(L, fsize - delta, ref func);
                    ci->func.p -= delta; // restore 'func' (if vararg)
                    for (int i = 0; i < narg1; i++) // move down function and arguments
                    {
                        setobjs2s(L, ci->func.p + i, func + i);
                    }

                    func = ci->func.p; // moved-down function
                    for (; narg1 <= nfixparams; narg1++)
                    {
                        setnilvalue(s2v(func + narg1)); // complete missing arguments
                    }

                    ci->top.p = func + 1 + fsize; // top for new function
                    Debug.Assert(ci->top.p <= L->stack_last.p);
                    ci->u.l.savedpc = p->code; // starting point
                    ci->callstatus |= CIST_TAIL;
                    L->top.p = func + narg1; // set top
                    return -1;
                }

            default:
                // not a function
                checkstackp(L, 1, ref func); // space for metamethod
                status = tryfuncTM(L, func, status); // try '__call' metamethod
                narg1++;
                goto retry; // try again
        }
    }

    /// <summary>
    /// Prepares the call to a function (C or Lua). For C functions, also do
    /// the call. The function to be called is at '*func'.  The arguments
    /// are on the stack, right after the function.  Returns the CallInfo
    /// to be executed, if it was a Lua function. Otherwise (a C function)
    /// returns null, with all the results on the stack, starting at the
    /// original function position.
    /// </summary>
    private static CallInfo* luaD_precall(lua_State* L, StkId func, int nresults)
    {
        uint status = (uint)(nresults + 1);
        Debug.Assert(status <= MAXRESULTS + 1);
        
        retry:
        switch (ttypetag(s2v(func)))
        {
            case LUA_VCCL: // C closure
                precallC(L, func, status, clCvalue(s2v(func))->f);
                return null;

            case LUA_VLCF: // light C function
                precallC(L, func, status, fvalue(s2v(func)));
                return null;

            case LUA_VLCL:
                {
                    // Lua function
                    Proto* p = clLvalue(s2v(func))->p;
                    int narg = (int)(L->top.p - func) - 1; // number of real arguments
                    int nfixparams = p->numparams;
                    int fsize = p->maxstacksize; // frame size
                    checkstackp(L, fsize, ref func);
                    CallInfo* ci = L->ci = prepCallInfo(L, func, status, func + 1 + fsize);
                    ci->u.l.savedpc = p->code; // starting point
                    for (; narg < nfixparams; narg++)
                    {
                        setnilvalue(s2v(L->top.p++)); // complete missing arguments
                    }

                    Debug.Assert(ci->top.p <= L->stack_last.p);
                    return ci;
                }

            default:
                // not a function
                checkstackp(L, 1, ref func); // space for metamethod
                status = tryfuncTM(L, func, status); // try '__call' metamethod
                goto retry; // try again with metamethod
        }
    }

    /// <summary>
    /// Call a function (C or Lua) through C. 'inc' can be 1 (increment
    /// number of recursive invocations in the C stack) or nyci (the same
    /// plus increment number of non-yieldable calls).
    /// This function can be called with some use of EXTRA_STACK, so it should
    /// check the stack before doing anything else. 'luaD_precall' already
    /// does that.
    /// </summary>
    private static void ccall(lua_State* L, StkId func, int nResults, uint inc)
    {
        CallInfo* ci;
        L->nCcalls += inc;
        if (getCcalls(L) >= LUAI_MAXCCALLS)
        {
            checkstackp(L, 0, ref func); // free any use of EXTRA_STACK
            luaE_checkcstack(L);
        }

        if ((ci = luaD_precall(L, func, nResults)) != null)
        {
            // Lua function?
            ci->callstatus |= CIST_FRESH; // mark that it is a "fresh" execute
            luaV_execute(L, ci); // call it
        }

        L->nCcalls -= inc;
    }

    /// <summary>
    /// External interface for 'ccall'
    /// </summary>
    private static void luaD_call(lua_State* L, StkId func, int nResults)
    {
        ccall(L, func, nResults, 1);
    }

    /// <summary>
    /// Similar to 'luaD_call', but does not allow yields during the call.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void luaD_callnoyield(lua_State* L, StkId func, int nResults)
    {
        ccall(L, func, nResults, nyci);
    }

    /// <summary>
    /// Finish the job of 'lua_pcallk' after it was interrupted by an yield.
    /// (The caller, 'finishCcall', does the final call to 'adjustresults'.)
    /// The main job is to complete the 'luaD_pcall' called by 'lua_pcallk'.
    /// If a '__close' method yields here, eventually control will be back
    /// to 'finishCcall' (when that '__close' method finally returns) and
    /// 'finishpcallk' will run again and close any still pending '__close'
    /// methods. Similarly, if a '__close' method errs, 'precover' calls
    /// 'unroll' which calls ''finishCcall' and we are back here again, to
    /// close any pending '__close' methods.
    /// Note that, up to the call to 'luaF_close', the corresponding
    /// 'CallInfo' is not modified, so that this repeated run works like the
    /// first one (except that it has at least one less '__close' to do). In
    /// particular, field CIST_RECST preserves the error status across these
    /// multiple runs, changing only if there is a new error.
    /// </summary>
    private static byte finishpcallk(lua_State* L, CallInfo* ci)
    {
        byte status = getcistrecst(ci); // get original status
        if (status == LUA_OK) // no error?
        {
            status = LUA_YIELD; // was interrupted by an yield
        }
        else
        {
            // error
            StkId func = restorestack(L, ci->u2.funcidx);
            L->allowhook = getoah(ci); // restore 'allowhook'
            func = luaF_close(L, func, status, true); // can yield or raise an error
            luaD_seterrorobj(L, status, func);
            luaD_shrinkstack(L); // restore stack size in case of overflow
            setcistrecst(ci, LUA_OK); // clear original status
        }

        ci->callstatus &= ~CIST_YPCALL;
        L->errfunc = ci->u.c.old_errfunc;
        // if it is here, there were errors or yields; unlike 'lua_pcallk',
        // do not change status
        return status;
    }

    /// <summary>
    /// Completes the execution of a C function interrupted by an yield.
    /// The interruption must have happened while the function was either
    /// closing its tbc variables in 'moveresults' or executing
    /// 'lua_callk'/'lua_pcallk'. In the first case, it just redoes
    /// 'luaD_poscall'. In the second case, the call to 'finishpcallk'
    /// finishes the interrupted execution of 'lua_pcallk'.  After that, it
    /// calls the continuation of the interrupted function and finally it
    /// completes the job of the 'luaD_call' that called the function.  In
    /// the call to 'adjustresults', we do not know the number of results
    /// of the function called by 'lua_callk'/'lua_pcallk', so we are
    /// conservative and use LUA_MULTRET (always adjust).
    /// </summary>
    private static void finishCcall(lua_State* L, CallInfo* ci)
    {
        int n; // actual number of results from C function
        if ((ci->callstatus & CIST_CLSRET) != 0)
        {
            // was closing TBC variable?
            Debug.Assert((ci->callstatus & CIST_TBC) != 0);
            n = ci->u2.nres; // just redo 'luaD_poscall'
            // don't need to reset CIST_CLSRET, as it will be set again anyway
        }
        else
        {
            byte status = LUA_YIELD; // default if there were no errors
            lua_KFunction kf = ci->u.c.k; // continuation function
            // must have a continuation and must be able to call it
            Debug.Assert(kf != null! && yieldable(L));
            if ((ci->callstatus & CIST_YPCALL) != 0) // was inside a 'lua_pcallk'?
            {
                status = finishpcallk(L, ci); // finish it
            }

            adjustresults(L, LUA_MULTRET); // finish 'lua_callk'
            lua_unlock(L);
            n = kf(L, status, ci->u.c.ctx); // call continuation
            lua_lock(L);
            api_checknelems(L, n);
        }

        luaD_poscall(L, ci, n); // finish 'luaD_call'
    }

    /// <summary>
    /// Executes "full continuation" (everything in the stack) of a
    /// previously interrupted coroutine until the stack is empty (or another
    /// interruption long-jumps out of the loop).
    /// </summary>
    private static void unroll(lua_State* L, void* ud)
    {
        CallInfo* ci;
        while ((ci = L->ci) != &L->base_ci)
        {
            // something in the stack
            if (!isLua(ci)) // C function?
            {
                finishCcall(L, ci); // complete its execution
            }
            else
            {
                // Lua function
                luaV_finishOp(L); // finish interrupted instruction
                luaV_execute(L, ci); // execute down to higher C 'boundary'
            }
        }
    }

    /// <summary>
    /// Try to find a suspended protected call (a "recover point") for the
    /// given thread.
    /// </summary>
    private static CallInfo* findpcall(lua_State* L)
    {
        for (CallInfo* ci = L->ci; ci != null; ci = ci->previous)
        {
            // search for a pcall
            if ((ci->callstatus & CIST_YPCALL) != 0)
            {
                return ci;
            }
        }

        return null; // no pending pcall
    }

    /// <summary>
    /// Signal an error in the call to 'lua_resume', not in the execution
    /// of the coroutine itself. (Such errors should not be handled by any
    /// coroutine error handler and should not kill the coroutine.)
    /// </summary>
    private static int resume_error(lua_State* L, string msg, int narg)
    {
        api_checkpop(L, narg);
        L->top.p -= narg; // remove args from the stack
        setsvalue2s(L, L->top.p, luaS_new(L, msg)); // push error message
        api_incr_top(L);
        lua_unlock(L);
        return LUA_ERRRUN;
    }

    /// <summary>
    /// Do the work for 'lua_resume' in protected mode. Most of the work
    /// depends on the status of the coroutine: initial state, suspended
    /// inside a hook, or regularly suspended (optionally with a continuation
    /// function), plus erroneous cases: non-suspended coroutine or dead
    /// coroutine.
    /// </summary>
    private static void resume(lua_State* L, void* ud)
    {
        int n = *(int*)ud; // number of arguments
        StkId firstArg = L->top.p - n; // first argument
        CallInfo* ci = L->ci;
        if (L->status == LUA_OK) // starting a coroutine?
        {
            ccall(L, firstArg - 1, LUA_MULTRET, 0); // just call its body
        }
        else
        {
            // resuming from previous yield
            Debug.Assert(L->status == LUA_YIELD);
            L->status = LUA_OK; // mark that it is running (again)
            if (isLua(ci))
            {
                // yielded inside a hook?
                // undo increment made by 'luaG_traceexec': instruction was not
                // executed yet
                Debug.Assert((ci->callstatus & CIST_HOOKYIELD) != 0);
                ci->u.l.savedpc--;
                L->top.p = firstArg; // discard arguments
                luaV_execute(L, ci); // just continue running Lua code
            }
            else
            {
                // 'common' yield
                if (ci->u.c.k != null!)
                {
                    // does it have a continuation function?
                    lua_unlock(L);
                    n = ci->u.c.k(L, LUA_YIELD, ci->u.c.ctx); // call continuation
                    lua_lock(L);
                    api_checknelems(L, n);
                }

                luaD_poscall(L, ci, n); // finish 'luaD_call'
            }

            unroll(L, null); // run continuation
        }
    }

    /// <summary>
    /// Unrolls a coroutine in protected mode while there are recoverable
    /// errors, that is, errors inside a protected call. (Any error
    /// interrupts 'unroll', and this loop protects it again so it can
    /// continue.) Stops with a normal end (status == LUA_OK), an yield
    /// (status == LUA_YIELD), or an unprotected error ('findpcall' doesn't
    /// find a recover point).
    /// </summary>
    private static byte precover(lua_State* L, byte status)
    {
        CallInfo* ci;
        while (errorstatus(status) && (ci = findpcall(L)) != null)
        {
            L->ci = ci; // go down to recovery functions
            setcistrecst(ci, status); // status to finish 'pcall'
            status = luaD_rawrunprotected(L, unroll, null);
        }

        return status;
    }

    public static int lua_resume(lua_State* L, lua_State* from, int narg, int* nres)
    {
        lua_lock(L);
        if (L->status == LUA_OK)
        {
            // may be starting a coroutine
            if (L->ci != &L->base_ci) // not in base level?
            {
                return resume_error(L, "cannot resume non-suspended coroutine", narg);
            }

            if (L->top.p - (L->ci->func.p + 1) == narg) // no function?
            {
                return resume_error(L, "cannot resume dead coroutine", narg);
            }
        }
        else if (L->status != LUA_YIELD) // ended with errors?
        {
            return resume_error(L, "cannot resume dead coroutine", narg);
        }

        L->nCcalls = from != null ? getCcalls(from) : 0;
        if (getCcalls(L) >= LUAI_MAXCCALLS)
        {
            return resume_error(L, "C stack overflow", narg);
        }

        L->nCcalls++;
        luai_userstateresume(L, narg);
        api_checkpop(L, L->status == LUA_OK ? narg + 1 : narg);
        byte status = luaD_rawrunprotected(L, resume, &narg);
        // continue running after recoverable errors
        status = precover(L, status);
        if (!errorstatus(status))
        {
            Debug.Assert(status == L->status); // normal end or yield
        }
        else
        {
            // unrecoverable error
            L->status = status; // mark thread as 'dead'
            luaD_seterrorobj(L, status, L->top.p); // push error message
            L->ci->top.p = L->top.p;
        }

        *nres = status == LUA_YIELD
            ? L->ci->u2.nyield
            : (int)(L->top.p - (L->ci->func.p + 1));
        lua_unlock(L);
        return status;
    }

    public static bool lua_isyieldable(lua_State* L)
    {
        return yieldable(L);
    }

    public static int lua_yieldk(lua_State* L, int nresults, nint ctx, lua_KFunction k)
    {
        luai_userstateyield(L, nresults);
        lua_lock(L);
        CallInfo* ci = L->ci;
        api_checkpop(L, nresults);
        if (!yieldable(L))
        {
            if (L != mainthread(G(L)))
            {
                luaG_runerror(L, "attempt to yield across a C-call boundary");
            }
            else
            {
                luaG_runerror(L, "attempt to yield from outside a coroutine");
            }
        }

        L->status = LUA_YIELD;
        ci->u2.nyield = nresults; // save number of results
        if (isLua(ci))
        {
            // inside a hook?
            Debug.Assert(!isLuacode(ci));
            Debug.Assert(nresults == 0, "hooks cannot yield values");
            Debug.Assert(k == null, "hooks cannot continue after yielding");
        }
        else
        {
            if ((ci->u.c.k = k) != null) // is there a continuation?
            {
                ci->u.c.ctx = ctx; // save context
            }

            luaD_throw(L, LUA_YIELD);
        }

        Debug.Assert((ci->callstatus & CIST_HOOKED) != 0); // must be inside a hook
        lua_unlock(L);
        return 0; // return to 'luaD_hook'
    }

    /// <summary>
    /// Auxiliary structure to call 'luaF_close' in protected mode.
    /// </summary>
    private struct CloseP
    {
        public StkId level;
        public byte status;
    }

    /// <summary>
    /// Auxiliary function to call 'luaF_close' in protected mode.
    /// </summary>
    private static void closepaux(lua_State* L, void* ud)
    {
        CloseP* pcl = (CloseP*)ud;
        luaF_close(L, pcl->level, pcl->status, false);
    }

    /// <summary>
    /// Calls 'luaF_close' in protected mode. Return the original status
    /// or, in case of errors, the new status.
    /// </summary>
    private static byte luaD_closeprotected(lua_State* L, IntPtr level, byte status)
    {
        CallInfo* old_ci = L->ci;
        bool old_allowhooks = L->allowhook;
        while (true)
        {
            // keep closing upvalues until no more errors
            CloseP pcl;
            pcl.level = restorestack(L, level);
            pcl.status = status;
            status = luaD_rawrunprotected(L, closepaux, &pcl);
            if (status == LUA_OK) // no more errors?
            {
                return pcl.status;
            }

            // an error occurred; restore saved state and repeat
            L->ci = old_ci;
            L->allowhook = old_allowhooks;
        }
    }

    /// <summary>
    /// Call the C function 'func' in protected mode, restoring basic
    /// thread information ('allowhook', etc.) and in particular
    /// its stack level in case of errors.
    /// </summary>
    private static byte luaD_pcall(lua_State* L, Pfunc func, void* u, nint oldtop, nint ef)
    {
        CallInfo* old_ci = L->ci;
        bool old_allowhooks = L->allowhook;
        nint old_errfunc = L->errfunc;
        L->errfunc = ef;
        byte status = luaD_rawrunprotected(L, func, u);
        if (status != LUA_OK)
        {
            // an error occurred?
            L->ci = old_ci;
            L->allowhook = old_allowhooks;
            status = luaD_closeprotected(L, oldtop, status);
            luaD_seterrorobj(L, status, restorestack(L, oldtop));
            luaD_shrinkstack(L); // restore stack size in case of overflow
        }

        L->errfunc = old_errfunc;
        return status;
    }

    /// <summary>
    /// Execute a protected parser.
    /// </summary>
    private struct SParser
    {
        /// <summary>
        /// data to 'f_parser'
        /// </summary>
        public Zio* z;
        public Mbuffer buff; // dynamic structure used by the scanner
        public Dyndata dyd; // dynamic structures used by the parser
        public char* mode;
        public char* name;
    }

    private static void checkmode(lua_State* L, string mode, string x)
    {
        if (!mode.Contains(x[0]))
        {
            luaO_pushfstring(
                L,
                "attempt to load a %s chunk (mode is '%s')",
                x,
                mode);
            luaD_throw(L, LUA_ERRSYNTAX);
        }
    }

    private static void f_parser(lua_State* L, void* ud)
    {
        SParser* p = (SParser*)ud;
        string mode = p->mode != null ? new string(p->mode) : "bt";
        int c = zgetc(p->z); // read first character

        LClosure* cl;
        if (c == LUA_SIGNATURE[0])
        {
            bool @fixed = false;
            if (mode.Contains('B'))
            {
                @fixed = true;
            }
            else
            {
                checkmode(L, mode, "binary");
            }

            cl = luaU_undump(L, p->z, new string(p->name), @fixed);
        }
        else
        {
            checkmode(L, mode, "text");
            cl = luaY_parser(L, p->z, &p->buff, &p->dyd, new string(p->name), c);
        }

        Debug.Assert(cl->nupvalues == cl->p->sizeupvalues);
        luaF_initupvals(L, cl);
    }

    private static byte luaD_protectedparser(lua_State* L, Zio* z, string name, string? mode)
    {
        fixed (char* nameP = name)
        {
            fixed (char* modeP = mode)
            {
                incnny(L); // cannot yield during parsing
                SParser p;
                p.z = z;
                p.name = nameP;
                p.mode = modeP;
                p.dyd.actvar.arr = null;
                p.dyd.actvar.size = 0;
                p.dyd.gt.arr = null;
                p.dyd.gt.size = 0;
                p.dyd.label.arr = null;
                p.dyd.label.size = 0;
                p.buff.buffer = null;
                p.buff.buffsize = 0;
                byte status = luaD_pcall(L, f_parser, &p, savestack(L, L->top.p), L->errfunc);
                luaZ_freebuffer(L, &p.buff);
                luaM_freearray(L, p.dyd.actvar.arr, p.dyd.actvar.size);
                luaM_freearray(L, p.dyd.gt.arr, p.dyd.gt.size);
                luaM_freearray(L, p.dyd.label.arr, p.dyd.label.size);
                decnny(L);
                return status;
            }
        }
    }
}
