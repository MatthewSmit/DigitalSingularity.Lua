namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
// #define fromstate(L)	(cast(LX *, cast(lu_byte *, (L)) - offsetof(LX, l)))

    /*
    ** these macros allow user-specific actions when a thread is
    ** created/deleted
    */

    private static L_EXTRA* getlock(lua_State* l)
    {
        return (L_EXTRA*)lua_getextraspace(l);
    }
    
#if LUA_TEST
    private static void luai_userstateopen(lua_State* l)
    {
        getlock(l)->@lock = 0;
        getlock(l)->plock = &getlock(l)->@lock;
    }
#else
    private static void luai_userstateopen(lua_State* l) { }
#endif

// #define luai_userstateclose(l)  \
//   Debug.Assert(getlock(l)->lock == 1 && getlock(l)->plock == &(getlock(l)->lock))
// #define luai_userstatethread(l,l1) \
//   Debug.Assert(getlock(l1)->plock == getlock(l)->plock)
// #define luai_userstatefree(l,l1) \
//   Debug.Assert(getlock(l)->plock == getlock(l1)->plock)

    private static void lua_lock(lua_State* l)
    {
        int result = (*getlock(l)->plock)++;
        Debug.Assert(result == 0);
    }

    private static void lua_unlock(lua_State* l)
    {
        int result = --*getlock(l)->plock;
        Debug.Assert(result == 0);
    }

    // #if !defined(luai_userstateopen)
// #define luai_userstateopen(L)		((void)L)
// #endif
//
// #if !defined(luai_userstateclose)
// #define luai_userstateclose(L)		((void)L)
// #endif
//
// #if !defined(luai_userstatethread)
// #define luai_userstatethread(L,L1)	((void)L)
// #endif
//
// #if !defined(luai_userstatefree)
// #define luai_userstatefree(L,L1)	((void)L)
// #endif


    /*
    ** set GCdebt to a new value keeping the real number of allocated
    ** objects (GCtotalobjs - GCdebt) invariant and avoiding overflows in
    ** 'GCtotalobjs'.
    */
    private static partial void luaE_setdebt(global_State* g, long debt)
    {
        const long MAX_LMEM = 0x7FFFFFFFFFFFFFFFL;
            
        long tb = gettotalbytes(g);
        Debug.Assert(tb > 0);
        if (debt > MAX_LMEM - tb)
        {
            debt = MAX_LMEM - tb; /* will make GCtotalbytes == MAX_LMEM */
        }

        g->GCtotalbytes = tb + debt;
        g->GCdebt = debt;
    }

    private static partial CallInfo* luaE_extendCI(lua_State* L)
    {
        Debug.Assert(L->ci->next == null);
        CallInfo* ci = luaM_new<CallInfo>(L);
        Debug.Assert(L->ci->next == null);
        L->ci->next = ci;
        ci->previous = L->ci;
        ci->next = null;
        ci->u.l.trap = 0;
        L->nci++;
        return ci;
    }

// /*
// ** free all CallInfo structures not in use by a thread
// */
// static void freeCI (lua_State *L) {
//   CallInfo *ci = L->ci;
//   CallInfo *next = ci->next;
//   ci->next = null;
//   while ((ci = next) != null) {
//     next = ci->next;
//     luaM_free(L, ci);
//     L->nci--;
//   }
// }

    /*
    ** free half of the CallInfo structures not in use by a thread,
    ** keeping the first one.
    */
    private static partial void luaE_shrinkCI(lua_State* L)
    {
        CallInfo* ci = L->ci->next; /* first free CallInfo */
        if (ci == null)
        {
            return; /* no extra elements */
        }

        CallInfo* next;
        while ((next = ci->next) != null)
        {
            /* two extra elements? */
            CallInfo* next2 = next->next; /* next's next */
            ci->next = next2; /* remove next from the list */
            L->nci--;
            luaM_free(L, next); /* free next */
            if (next2 == null)
            {
                break; /* no more elements */
            }

            next2->previous = ci;
            ci = next2; /* continue */
        }
    }

    /*
    ** Called when 'getCcalls(L)' larger or equal to LUAI_MAXCCALLS.
    ** If equal, raises an overflow error. If value is larger than
    ** LUAI_MAXCCALLS (which means it is handling an overflow) but
    ** not much larger, does not report an error (to allow overflow
    ** handling to work).
    */
    private static partial void luaE_checkcstack(lua_State* L)
    {
        if (getCcalls(L) == LUAI_MAXCCALLS)
        {
            luaG_runerror(L, "C stack overflow");
        }
        else if (getCcalls(L) >= (LUAI_MAXCCALLS / 10 * 11))
        {
            // luaD_errerr(L); /* error while handling stack error */
            throw new NotImplementedException();
        }
    }

    private static partial void luaE_incCstack(lua_State* L)
    {
        L->nCcalls++;
        if (getCcalls(L) >= LUAI_MAXCCALLS)
        {
            luaE_checkcstack(L);
        }
    }

    private static void resetCI(lua_State* L)
    {
        CallInfo* ci = L->ci = &L->base_ci;
        ci->func.p = L->stack.p;
        setnilvalue(s2v(ci->func.p)); /* 'function' entry for basic 'ci' */
        ci->top.p = ci->func.p + 1 + LUA_MINSTACK; /* +1 for 'function' entry */
        ci->u.c.k = null;
        ci->callstatus = CIST_C;
        L->status = LUA_OK;
        L->errfunc = 0; /* stack unwind can "throw away" the error function */
    }

    private static void stack_init(lua_State* L1, lua_State* L)
    {
        int i;
        /* initialise stack array */
        L1->stack.p = luaM_newvector<StackValue>(L, BASIC_STACK_SIZE + EXTRA_STACK);
        L1->tbclist.p = L1->stack.p;
        for (i = 0; i < BASIC_STACK_SIZE + EXTRA_STACK; i++)
        {
            setnilvalue(s2v(L1->stack.p + i)); /* erase new stack */
        }

        L1->stack_last.p = L1->stack.p + BASIC_STACK_SIZE;
        // initialise first ci 
        resetCI(L1);
        L1->top.p = L1->stack.p + 1; /* +1 for 'function' entry */
    }

// static void freestack (lua_State *L) {
//   if (L->stack.p == null)
//     return;  /* stack not completely built yet */
//   L->ci = &L->base_ci;  /* free the entire 'ci' list */
//   freeCI(L);
//   Debug.Assert(L->nci == 0);
//   /* free stack */
//   luaM_freearray(L, L->stack.p, cast_sizet(stacksize(L) + EXTRA_STACK));
// }

    /*
    ** Create registry table and its predefined values
    */
    private static void init_registry(lua_State* L, global_State* g)
    {
        /* create registry */
        TValue aux;
        Table* registry = luaH_new(L);
        sethvalue(L, &g->l_registry, registry);
        luaH_resize(L, registry, LUA_RIDX_LAST, 0);
        /* registry[1] = false */
        setbfvalue(&aux);
        luaH_setint(L, registry, 1, &aux);
        /* registry[LUA_RIDX_MAINTHREAD] = L */
        setthvalue(L, &aux, L);
        luaH_setint(L, registry, LUA_RIDX_MAINTHREAD, &aux);
        /* registry[LUA_RIDX_GLOBALS] = new table (table of globals) */
        sethvalue(L, &aux, luaH_new(L));
        luaH_setint(L, registry, LUA_RIDX_GLOBALS, &aux);
    }

    /*
     ** open parts of the state that may cause memory-allocation errors.
     */
    private static void f_luaopen(lua_State* L, void* ud)
    {
        global_State* g = G(L);
        stack_init(L, L);  /* init stack */
        init_registry(L, g);
        luaS_init(L);
        luaT_init(L);
        luaX_init(L);
        g->gcstp = 0;  /* allow gc */
        setnilvalue(&g->nilvalue);  /* now state is complete */
        luai_userstateopen(L);
    }

    /*
     ** preinitialise a thread with consistent values without allocating
     ** any memory (to avoid errors)
     */
    private static void preinit_thread(lua_State* L, global_State* g)
    {
        G(L) = g;
        L->stack.p = null;
        L->ci = null;
        L->nci = 0;
        L->twups = L; /* thread has no upvalues */
        L->nCcalls = 0;
        // L->errorJmp = null;
        L->hook = null;
        // L->hookmask = 0; TODO
        L->basehookcount = 0;
        L->allowhook = true;
        resethookcount(L);
        L->openupval = null;
        L->status = LUA_OK;
        L->errfunc = 0;
        L->oldpc = 0;
        L->base_ci.previous = L->base_ci.next = null;
    }

    private static partial long luaE_threadsize(lua_State* L)
    {
        long sz = sizeof(LX) + (uint)L->nci * sizeof(CallInfo);
        if (L->stack.p != null!)
        {
            sz += (uint)(stacksize(L) + EXTRA_STACK) * sizeof(StackValue);
        }

        return sz;
    }

    private static void close_state(lua_State* L)
    {
//   global_State *g = G(L);
//   if (!completestate(g))  /* closing a partially built state? */
//     luaC_freeallobjects(L);  /* just collect its objects */
//   else {  /* closing a fully built state */
//     resetCI(L);
//     luaD_closeprotected(L, 1, LUA_OK);  /* close all upvalues */
//     L->top.p = L->stack.p + 1;  /* empty the stack to run finalizers */
//     luaC_freeallobjects(L);  /* collect all objects */
//     luai_userstateclose(L);
//   }
//   luaM_freearray(L, G(L)->strt.hash, cast_sizet(G(L)->strt.size));
//   freestack(L);
//   Debug.Assert(gettotalbytes(g) == sizeof(global_State));
//   (*g->frealloc)(g->ud, g, sizeof(global_State), 0);  /* free main block */
        throw new NotImplementedException();
    }

    public static partial lua_State* lua_newthread(lua_State* L)
    {
//   global_State *g = G(L);
//   GCObject *o;
//   lua_State *L1;
//   lua_lock(L);
//   luaC_checkGC(L);
//   /* create new thread */
//   o = luaC_newobjdt(L, LUA_TTHREAD, sizeof(LX), offsetof(LX, l));
//   L1 = gco2th(o);
//   /* anchor it on L stack */
//   setthvalue2s(L, L->top.p, L1);
//   api_incr_top(L);
//   preinit_thread(L1, g);
//   L1->hookmask = L->hookmask;
//   L1->basehookcount = L->basehookcount;
//   L1->hook = L->hook;
//   resethookcount(L1);
//   /* initialize L1 extra space */
//   memcpy(lua_getextraspace(L1), lua_getextraspace(mainthread(g)),
//          LUA_EXTRASPACE);
//   luai_userstatethread(L, L1);
//   stack_init(L1, L);  /* init stack */
//   lua_unlock(L);
//   return L1;
        throw new NotImplementedException();
    }

    private static partial void luaE_freethread(lua_State* L, lua_State* L1)
    {
//   LX *l = fromstate(L1);
//   luaF_closeupval(L1, L1->stack.p);  /* close all upvalues */
//   Debug.Assert(L1->openupval == null);
//   luai_userstatefree(L, L1);
//   freestack(L1);
//   luaM_free(L, l);
        throw new NotImplementedException();
    }

    private static partial byte luaE_resetthread(lua_State* L, byte status)
    {
        resetCI(L);
        if (status == LUA_YIELD)
        {
            status = LUA_OK;
        }

        status = luaD_closeprotected(L, 1, status);
        if (status != LUA_OK) /* errors? */
        {
            luaD_seterrorobj(L, status, L->stack.p + 1);
        }
        else
        {
            L->top.p = L->stack.p + 1;
        }

        luaD_reallocstack(L, (int)(L->ci->top.p - L->stack.p), false);
        return status;
    }

    public static partial int lua_closethread(lua_State* L, lua_State* from)
    {
        lua_lock(L);
        L->nCcalls = from != null ? getCcalls(from) : 0;
        byte status = luaE_resetthread(L, L->status);
        if (L == from) /* closing itself? */
        {
            luaD_throwbaselevel(L, status);
        }

        lua_unlock(L);
        return status;
    }

    public static partial lua_State* lua_newstate(
        delegate* managed<void*, void*, long, long, void*> f,
        void* ud,
        uint seed)
    {
        global_State* g = (global_State*)f(ud, null, LUA_TTHREAD, sizeof(global_State));
        if (g == null)
        {
            return null;
        }

        lua_State* L = &g->mainth.l;
        L->tt = LUA_VTHREAD;
        g->currentwhite = bitmask(WHITE0BIT);
        L->marked = luaC_white(g);
        preinit_thread(L, g);
        g->allgc = obj2gco(L); /* by now, only object is the main thread */
        L->next = null;
        incnny(L); /* main thread is always non yieldable */
        g->frealloc = f;
        g->ud = ud;
        g->warnf = null;
        g->ud_warn = null;
        g->seed = seed;
        g->gcstp = GCSTPGC; /* no GC while building state */
        g->strt.size = g->strt.nuse = 0;
        g->strt.hash = null;
        setnilvalue(&g->l_registry);
        g->panic = null;
        g->gcstate = GCSpause;
        g->gckind = KGC_INC;
        g->gcstopem = false;
        g->gcemergency = false;
        g->finobj = g->tobefnz = g->fixedgc = null;
        g->firstold1 = g->survival = g->old1 = g->reallyold = null;
        g->finobjsur = g->finobjold1 = g->finobjrold = null;
        g->sweepgc = null;
        g->grey = g->greyagain = null;
        g->weak = g->ephemeron = g->allweak = null;
        g->twups = null;
        g->GCtotalbytes = sizeof(global_State);
        g->GCmarked = 0;
        g->GCdebt = 0;
        setivalue(&g->nilvalue, 0); /* to signal that state is not yet built */
        setgcparam(g, LUA_GCPPAUSE, LUAI_GCPAUSE);
        setgcparam(g, LUA_GCPSTEPMUL, LUAI_GCMUL);
        setgcparam(g, LUA_GCPSTEPSIZE, LUAI_GCSTEPSIZE);
        setgcparam(g, LUA_GCPMINORMUL, LUAI_GENMINORMUL);
        setgcparam(g, LUA_GCPMINORMAJOR, LUAI_MINORMAJOR);
        setgcparam(g, LUA_GCPMAJORMINOR, LUAI_MAJORMINOR);
        for (int i = 0; i < LUA_NUMTYPES; i++)
        {
            g->mt[i] = null;
        }

        if (luaD_rawrunprotected(L, f_luaopen, null) != LUA_OK)
        {
            /* memory allocation error: free partial state */
            close_state(L);
            L = null;
        }

        return L;
    }

    public static partial void lua_close(lua_State* L)
    {
        lua_lock(L);
        // L = mainthread(G(L)); /* only the main thread can be closed */
        // close_state(L);
        throw new NotImplementedException();
    }

    private static partial void luaE_warning(lua_State* L, string msg, bool tocont)
    {
        lua_WarnFunction wf = G(L)->warnf;
        if (wf != null)
        {
            wf(G(L)->ud_warn, msg, tocont);
        }
    }

    /*
    ** Generate a warning from an error message
    */
    private static partial void luaE_warnerror(lua_State* L, string where)
    {
//   TValue *errobj = s2v(L->top.p - 1);  /* error object */
//   const char *msg = (ttisstring(errobj))
//                   ? getstr(tsvalue(errobj))
//                   : "error object is not a string";
//   /* produce warning "error in %s (%s)" (where, msg) */
//   luaE_warning(L, "error in ", 1);
//   luaE_warning(L, where, 1);
//   luaE_warning(L, " (", 1);
//   luaE_warning(L, msg, 1);
//   luaE_warning(L, ")", 0);
        throw new NotImplementedException();
    }
}
