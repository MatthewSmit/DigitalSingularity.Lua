namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static unsafe partial class Lua
{
    // Some notes about garbage-collected objects: All objects in Lua must
    // be kept somehow accessible until being freed, so all objects always
    // belong to one (and only one) of these lists, using field 'next' of
    // the 'CommonHeader' for the link:
    //
    // 'allgc': all objects not marked for finalisation;
    // 'finobj': all objects marked for finalisation;
    // 'tobefnz': all objects ready to be finalised;
    // 'fixedgc': all objects that are not to be collected (currently
    // only small strings, such as reserved words).
    //
    // For the generational collector, some of these lists have marks for
    // generations. Each mark points to the first element in the list for
    // that particular generation; that generation goes until the next mark.
    //
    // 'allgc' -> 'survival': new objects;
    // 'survival' -> 'old': objects that survived one collection;
    // 'old1' -> 'reallyold': objects that became old in last collection;
    // 'reallyold' -> null: objects old for more than one cycle.
    //
    // 'finobj' -> 'finobjsur': new objects marked for finalization;
    // 'finobjsur' -> 'finobjold1': survived   """";
    // 'finobjold1' -> 'finobjrold': just old  """";
    // 'finobjrold' -> null: really old       """".
    //
    // All lists can contain elements older than their main ages, due
    // to 'luaC_checkfinaliser' and 'udata2finalise', which move
    // objects between the normal lists and the "marked for finalisation"
    // lists. Moreover, barriers can age young objects in young lists as
    // OLD0, which then become OLD1. However, a list never contains
    // elements younger than their main ages.
    //
    // The generational collector also uses a pointer 'firstold1', which
    // points to the first OLD1 object in the list. It is used to optimise
    // 'markold'. (Potentially OLD1 objects can be anywhere between 'allgc'
    // and 'reallyold', but often the list has no OLD1 objects or they are
    // after 'old1'.) Note the difference between it and 'old1':
    // 'firstold1': no OLD1 objects before this point; there can be all
    //   ages after it.
    // 'old1': no objects younger than OLD1 after this point.

    // Moreover, there is another set of lists that control grey objects.
    // These lists are linked by fields 'gclist'. (All objects that
    // can become grey have such a field. The field is not the same
    // in all objects, but it always has this name.)  Any grey object
    // must belong to one of these lists, and all objects in these lists
    // must be grey (with two exceptions explained below):
    //
    // 'grey': regular grey objects, still waiting to be visited.
    // 'greyagain': objects that must be revisited at the atomic phase.
    //   That includes
    //   - black objects got in a write barrier;
    //   - all kinds of weak tables during propagation phase;
    //   - all threads.
    // 'weak': tables with weak values to be cleared;
    // 'ephemeron': ephemeron tables with white->white entries;
    // 'allweak': tables with weak keys and/or weak values to be cleared.
    //
    // The exceptions to that "grey rule" are:
    // - TOUCHED2 objects in generational mode stay in a grey list (because
    // they must be visited again at the end of the cycle), but they are
    // marked black because assignments to them must activate barriers (to
    // move them back to TOUCHED1).
    // - Open upvalues are kept grey to avoid barriers, but they stay out
    // of grey lists. (They don't even have a 'gclist' field.)

    // About 'nCcalls':  This count has two parts: the lower 16 bits counts
    // the number of recursive invocations in the C stack; the higher
    // 16 bits counts the number of non-yieldable calls in the stack.
    // (They are together so that we can change and save both with one
    // instruction.)

    /// <summary>
    /// true if this thread does not have non-yieldable calls in the stack
    /// </summary>
    private static bool yieldable(lua_State* L)
    {
        return (L->nCcalls & 0xffff0000) == 0;
    }

    /// <summary>
    /// real number of C calls
    /// </summary>
    private static uint getCcalls(lua_State* L)
    {
        return L->nCcalls & 0xffff;
    }

    /// <summary>
    /// Increment the number of non-yieldable calls
    /// </summary>
    private static void incnny(lua_State* L)
    {
        L->nCcalls += 0x10000;
    }

    /// <summary>
    /// Decrement the number of non-yieldable calls
    /// </summary>
    private static void decnny(lua_State* L)
    {
        L->nCcalls -= 0x10000;
    }

    /// <summary>
    /// Non-yieldable call increment
    /// </summary>
    private const int nyci = 0x10000 | 1;

//
// Atomic type (relative to signals) to better ensure that 'lua_sethook'
// is thread safe
//
// #if !defined(l_signalT)
// #include <signal.h>
// #define l_signalT	sig_atomic_t
// #endif

    /// <summary>
    /// Extra stack space to handle TM calls and some other extras. This
    /// space is not included in 'stack_last'. It is used only to avoid stack
    /// checks, either because the element will be promptly popped or because
    /// there will be a stack check soon after the push. Function frames
    /// never use this extra space, so it does not need to be kept clean.
    /// </summary>
    internal const int EXTRA_STACK = 5;

    // Size of cache for strings in the API. 'N' is the number of
    // sets (better be a prime) and "M" is the size of each set.
    // (M == 1 makes a direct cache.)
#if !LUA_TEST
    private const int STRCACHE_N = 53;
    private const int STRCACHE_M = 2;
#else
    private const int STRCACHE_N = 23;
    private const int STRCACHE_M = 5;
#endif

    internal const int BASIC_STACK_SIZE = 2 * LUA_MINSTACK;

    internal static int stacksize(lua_State* th)
    {
        return (int)(th->stack_last.p - th->stack.p);
    }

    /// <summary>
    /// kinds of Garbage Collection
    /// </summary>
    internal const byte KGC_INC = 0; // incremental gc
    internal const byte KGC_GENMINOR = 1; // generational gc in minor (regular) mode
    internal const byte KGC_GENMAJOR = 2; // generational in major mode

    internal struct stringtable
    {
        public TString** hash; // array of buckets (linked lists of strings)
        public int nuse; // number of elements
        public int size; // number of buckets
    }

    /// <summary>
    /// Information about a call.
    /// About union 'u':
    /// - field 'l' is used only for Lua functions;
    /// - field 'c' is used only for C functions.
    /// About union 'u2':
    /// - field 'funcidx' is used only by C functions while doing a
    /// protected call;
    /// - field 'nyield' is used only while a function is "doing" an
    /// yield (from the yield until the next resume);
    /// - field 'nres' is used only while closing tbc variables when
    /// returning from a function;
    /// </summary>
    internal struct CallInfo
    {
        /// <summary>
        /// only for Lua functions
        /// </summary>
        public struct L
        {
            public uint* savedpc;
            public byte trap; // function is tracing lines/counts
            public int nextraargs; // # of extra arguments in vararg functions
        }

        /// <summary>
        /// only for C functions
        /// </summary>
        public struct C
        {
            public lua_KFunction k; // continuation in case of yields
            public nint old_errfunc;
            public nint ctx; // context info. in case of yields
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct U
        {
            [FieldOffset(0)] public L l;
            [FieldOffset(0)] public C c;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct U2
        {
            [FieldOffset(0)] public int funcidx; // called-function index
            [FieldOffset(0)] public int nyield; // number of values yielded
            [FieldOffset(0)] public int nres; // number of values returned
        }

        public StkIdRel func; // function index in the stack
        public StkIdRel top; // top for this function

        public CallInfo* previous, next; // dynamic call link
        public U u;
        public U2 u2;
        public uint callstatus;
    }

    /// <summary>
    /// Maximum expected number of results from a function
    /// (must fit in CIST_NRESULTS).
    /// </summary>
    private const int MAXRESULTS = 250;

    /// <summary>
    /// Bits in CallInfo status
    /// bits 0-7 are the expected number of results from this function + 1
    /// </summary>
    private const uint CIST_NRESULTS = 0xffu;

    /// <summary>
    /// bits 8-11 count call metamethods (and their extra arguments)
    /// </summary>
    private const int CIST_CCMT = 8; // the offset, not the mask
    private const uint MAX_CCMT = 0xfu << CIST_CCMT;

    /// <summary>
    /// Bits 12-14 are used for CIST_RECST (see below)
    /// </summary>
    private const int CIST_RECST = 12; // the offset, not the mask

    /// <summary>
    /// call is running a C function (still in first 16 bits)
    /// </summary>
    internal const uint CIST_C = 1u << CIST_RECST + 3;

    /// <summary>
    /// call is on a fresh "luaV_execute" frame
    /// </summary>
    private const uint CIST_FRESH = CIST_C << 1;

    /// <summary>
    /// function is closing tbc variables
    /// </summary>
    private const uint CIST_CLSRET = CIST_FRESH << 1;

    /// <summary>
    /// function has tbc variables to close
    /// </summary>
    private const uint CIST_TBC = CIST_CLSRET << 1;

    /// <summary>
    /// original value of 'allowhook'
    /// </summary>
    private const uint CIST_OAH = CIST_TBC << 1;

    /// <summary>
    /// call is running a debug hook
    /// </summary>
    private const uint CIST_HOOKED = CIST_OAH << 1;

    /// <summary>
    /// doing a yieldable protected call
    /// </summary>
    private const uint CIST_YPCALL = CIST_HOOKED << 1;

    /// <summary>
    /// call was tail called
    /// </summary>
    private const uint CIST_TAIL = CIST_YPCALL << 1;

    /// <summary>
    /// last hook called yielded
    /// </summary>
    private const uint CIST_HOOKYIELD = CIST_TAIL << 1;

    /// <summary>
    /// function "called" a finaliser
    /// </summary>
    private const uint CIST_FIN = CIST_HOOKYIELD << 1;

    private static int get_nresults(uint cs)
    {
        return (int)(cs & CIST_NRESULTS) - 1;
    }

    /// <summary>
    /// Field CIST_RECST stores the "recover status", used to keep the error
    /// status while closing to-be-closed variables in coroutines, so that
    /// Lua can correctly resume after an yield from a __close method called
    /// because of an error.  (Three bits are enough for error status.)
    /// </summary>
    private static byte getcistrecst(CallInfo* ci)
    {
        return (byte)(ci->callstatus >> CIST_RECST & 7);
    }

    private static void setcistrecst(CallInfo* ci, byte st)
    {
        Debug.Assert((st & 7) == st); // status must fit in three bits
        ci->callstatus = ci->callstatus & ~(7u << CIST_RECST) | (uint)st << CIST_RECST;
    }

    /// <summary>
    /// active function is a Lua function
    /// </summary>
    private static bool isLua(CallInfo* ci)
    {
        return (ci->callstatus & CIST_C) == 0;
    }

    /// <summary>
    /// call is running Lua code (not a hook)
    /// </summary>
    private static bool isLuacode(CallInfo* ci)
    {
        return (ci->callstatus & (CIST_C | CIST_HOOKED)) == 0;
    }

    private static void setoah(CallInfo* ci, bool v)
    {
        ci->callstatus = v ? ci->callstatus | CIST_OAH : ci->callstatus & ~CIST_OAH;
    }

    private static bool getoah(CallInfo* ci)
    {
        return (ci->callstatus & CIST_OAH) != 0;
    }

    /// <summary>
    /// 'per thread' state
    /// </summary>
    public struct lua_State
    {
        public struct TransferInfo
        {
            public int ftransfer; // offset of first value transferred
            public int ntransfer; // number of values transferred
        }
        
        internal GCObject* next;
        internal byte tt;
        internal byte marked;
        internal bool allowhook;
        internal byte status;
        internal StkIdRel top; // first free slot in the stack
        internal global_State* l_G;
        internal CallInfo* ci; // call info for current function
        internal StkIdRel stack_last; // end of stack (last element + 1)
        internal StkIdRel stack; // stack base
        internal UpVal* openupval; // list of open upvalues in this stack
        internal StkIdRel tbclist; // list of to-be-closed variables
        internal GCObject* gclist;
        internal lua_State* twups; // list of threads with open upvalues
        internal lua_longjmp_data* errorJmp; // current error recover point
        internal CallInfo base_ci; // CallInfo for first level (C host)
        internal lua_Hook hook;
        internal nint errfunc; // current error handling function (stack index)
        internal uint nCcalls; // number of nested non-yieldable or C calls
        internal int oldpc; // last pc traced
        internal int nci; // number of items in 'ci' list
        internal int basehookcount;
        internal int hookcount;
        internal byte hookmask;
        internal TransferInfo transferinfo; // info about transferred values (for call/return hooks)
    }

    /// <summary>
    /// thread state + extra space
    /// </summary>
    internal struct LX
    {
        public fixed byte extra_[LUA_EXTRASPACE];
        public lua_State l;
    }

    internal static readonly nint LX_l_offset = Marshal.OffsetOf<LX>(nameof(LX.l));

    [InlineArray(LUA_NUMTYPES)]
    internal struct TableArray
    {
        private nint _element0;
    }

    internal struct TableArrayWrapper
    {
        private TableArray array;

        public Table* this[int index]
        {
            get
            {
                return (Table*)this.array[index];
            }
            set
            {
                this.array[index] = (nint)value;
            }
        }
    }

    [InlineArray((int)TMS.N)]
    internal struct TStringTag
    {
        private nint _element0;
    }

    internal struct TStringTagWrapper
    {
        private TStringTag array;

        public TString* this[int index]
        {
            get
            {
                return (TString*)this.array[index];
            }
            set
            {
                this.array[index] = (nint)value;
            }
        }
    }

    [InlineArray(STRCACHE_M)]
    internal struct TStringCacheInner
    {
        private nint _element0;
    }

    [InlineArray(STRCACHE_N)]
    internal struct TStringCacheOuter
    {
        private TStringCacheInner _element0;
    }

    internal struct TStringCacheWrapper
    {
        private TStringCacheOuter _outer;

        public TString* this[int outer, int inner]
        {
            get
            {
                return (TString*)this._outer[outer][inner];
            }
            set
            {
                this._outer[outer][inner] = (nint)value;
            }
        }
    }

    /// <summary>
    /// 'global state', shared by all threads of this state
    /// </summary>
    internal struct global_State
    {
        public AllocFunction frealloc; // function to reallocate memory
        public void* ud; // auxiliary data to 'frealloc'
        public long GCtotalbytes; // number of bytes currently allocated + debt
        public long GCdebt; // bytes counted but not yet allocated
        public long GCmarked; // number of objects marked in a GC cycle
        public long GCmajorminor; // auxiliary counter to control major-minor shifts
        public stringtable strt; // hash table for strings
        public TValue l_registry;
        public TValue nilvalue; // a nil value
        public uint seed; // randomised seed for hashes
        public fixed byte gcparams[LUA_GCPN];
        public byte currentwhite;
        public byte gcstate; // state of garbage collector
        public byte gckind; // kind of GC running
        public bool gcstopem; // stops emergency collections
        public byte gcstp; // control whether GC is running
        public bool gcemergency; // true if this is an emergency collection
        public GCObject* allgc; // list of all collectable objects
        public GCObject** sweepgc; // current position of sweep in list
        public GCObject* finobj; // list of collectable objects with finalizers
        public GCObject* grey; // list of gray objects
        public GCObject* greyagain; // list of objects to be traversed atomically
        public GCObject* weak; // list of tables with weak values
        public GCObject* ephemeron; // list of ephemeron tables (weak keys)
        public GCObject* allweak; // list of all-weak tables
        public GCObject* tobefnz; // list of userdata to be GC

        public GCObject* fixedgc; // list of objects not to be collected

        /// <summary>
        /// fields for generational collector
        /// </summary>
        public GCObject* survival; // start of objects that survived one GC cycle
        public GCObject* old1; // start of old1 objects
        public GCObject* reallyold; // objects more than one cycle old ("really old")
        public GCObject* firstold1; // first OLD1 object in the list (if any)
        public GCObject* finobjsur; // list of survival objects with finalisers
        public GCObject* finobjold1; // list of old1 objects with finalisers
        public GCObject* finobjrold; // list of really old objects with finalisers
        public lua_State* twups; // list of threads with open upvalues
        public CFunction panic; // to be called in unprotected errors
        public TString* memerrmsg; // message for memory-allocation errors
        public TStringTagWrapper tmname; // array with tag-method names
        public TableArrayWrapper mt; // metatables for basic types
        public TStringCacheWrapper strcache; // cache for strings in API
        public lua_WarnFunction warnf; // warning function
        public void* ud_warn; // auxiliary data to 'warnf'
        public LX mainth; // main thread of this state
    }

    internal static ref global_State* G(lua_State* L)
    {
        return ref L->l_G;
    }

    private static lua_State* mainthread(global_State* G)
    {
        return &G->mainth.l;
    }

    /// <summary>
    /// 'g-&gt;nilvalue' being a nil value flags that the state was completely
    /// build.
    /// </summary>
    private static bool completestate(global_State* g)
    {
        return ttisnil(&g->nilvalue);
    }

    /// <summary>
    /// macros to convert a GCObject into a specific value
    /// </summary>
    private static TString* gco2ts(GCObject* o)
    {
        Debug.Assert(novariant(o->tt) == LUA_TSTRING);
        return (TString*)o;
    }

    private static Udata* gco2u(GCObject* o)
    {
        Debug.Assert(o->tt == LUA_VUSERDATA);
        return (Udata*)o;
    }

    private static LClosure* gco2lcl(GCObject* o)
    {
        Debug.Assert(o->tt == LUA_VLCL);
        return (LClosure*)o;
    }

    internal static CClosure* gco2ccl(GCObject* o)
    {
        Debug.Assert(o->tt == LUA_VCCL);
        return (CClosure*)o;
    }

    private static Closure* gco2cl(GCObject* o)
    {
        Debug.Assert(novariant(o->tt) == LUA_TFUNCTION);
        return (Closure*)o;
    }

    private static Table* gco2t(GCObject* o)
    {
        Debug.Assert(o->tt == LUA_VTABLE);
        return (Table*)o;
    }

    private static Proto* gco2p(GCObject* o)
    {
        Debug.Assert(o->tt == LUA_VPROTO);
        return (Proto*)o;
    }

    internal static lua_State* gco2th(GCObject* o)
    {
        Debug.Assert(o->tt == LUA_VTHREAD);
        return (lua_State*)o;
    }

    private static UpVal* gco2upv(GCObject* o)
    {
        Debug.Assert(o->tt == LUA_VUPVAL);
        return (UpVal*)o;
    }

    /// <summary>
    /// macro to convert a Lua object into a GCObject
    /// </summary>
    internal static GCObject* obj2gco(lua_State* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return (GCObject*)v;
    }

    internal static GCObject* obj2gco(GCObject* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return v;
    }

    internal static GCObject* obj2gco(Table* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return (GCObject*)v;
    }

    internal static GCObject* obj2gco(TString* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return (GCObject*)v;
    }

    internal static GCObject* obj2gco(CClosure* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return (GCObject*)v;
    }

    internal static GCObject* obj2gco(Udata* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return (GCObject*)v;
    }

    private static GCObject* obj2gco(LClosure* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return (GCObject*)v;
    }

    private static GCObject* obj2gco(UpVal* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return (GCObject*)v;
    }

    private static GCObject* obj2gco(Proto* v)
    {
        Debug.Assert(novariant(v->tt) >= LUA_TSTRING);
        return (GCObject*)v;
    }

    /// <summary>
    /// actual number of total memory allocated
    /// </summary>
    internal static long gettotalbytes(global_State* g)
    {
        return g->GCtotalbytes - g->GCdebt;
    }
    
    private static LX* fromstate(lua_State* L) => (LX*)((byte*)L - LX_l_offset);

    // these macros allow user-specific actions when a thread is
    // created/deleted

#if LUA_TEST
    private static L_EXTRA* getlock(lua_State* l)
    {
        return (L_EXTRA*)lua_getextraspace(l);
    }

    private static void luai_userstateopen(lua_State* l)
    {
        getlock(l)->@lock = 0;
        getlock(l)->plock = &getlock(l)->@lock;
    }

    private static void luai_userstateclose(lua_State* l)
    {
        Debug.Assert(getlock(l)->@lock == 1 && getlock(l)->plock == &getlock(l)->@lock);
    }

    private static void luai_userstatethread(lua_State* l, lua_State* l1)
    {
        Debug.Assert(getlock(l1)->plock == getlock(l)->plock);
    }

    private static void luai_userstatefree(lua_State* l, lua_State* l1)
    {
        Debug.Assert(getlock(l)->plock == getlock(l1)->plock);
    }

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
#else
    private static void luai_userstateopen(lua_State* l) { }
    private static void luai_userstateclose(lua_State* l) { }

    private static void luai_userstatethread(lua_State* l, lua_State* l1) { }
    private static void luai_userstatefree(lua_State* l, lua_State* l1) { }

    private static void lua_lock(lua_State* l) { }
    private static void lua_unlock(lua_State* l) { }
#endif

    /// <summary>
    /// set GCdebt to a new value keeping the real number of allocated
    /// objects (GCtotalobjs - GCdebt) invariant and avoiding overflows in
    /// 'GCtotalobjs'.
    /// </summary>
    internal static void luaE_setdebt(global_State* g, long debt)
    {
        const long MAX_LMEM = 0x7FFFFFFFFFFFFFFFL;

        long tb = gettotalbytes(g);
        Debug.Assert(tb > 0);
        if (debt > MAX_LMEM - tb)
        {
            debt = MAX_LMEM - tb; // will make GCtotalbytes == MAX_LMEM
        }

        g->GCtotalbytes = tb + debt;
        g->GCdebt = debt;
    }

    internal static CallInfo* luaE_extendCI(lua_State* L)
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

    /// <summary>
    /// free all CallInfo structures not in use by a thread
    /// </summary>
    private static void freeCI(lua_State* L)
    {
        CallInfo* ci = L->ci;
        CallInfo* next = ci->next;
        ci->next = null;
        while ((ci = next) != null)
        {
            next = ci->next;
            luaM_free(L, ci);
            L->nci--;
        }
    }

    /// <summary>
    /// free half of the CallInfo structures not in use by a thread,
    /// keeping the first one.
    /// </summary>
    internal static void luaE_shrinkCI(lua_State* L)
    {
        CallInfo* ci = L->ci->next; // first free CallInfo
        if (ci == null)
        {
            return; // no extra elements
        }

        CallInfo* next;
        while ((next = ci->next) != null)
        {
            // two extra elements?
            CallInfo* next2 = next->next; // next's next
            ci->next = next2; // remove next from the list
            L->nci--;
            luaM_free(L, next); // free next
            if (next2 == null)
            {
                break; // no more elements
            }

            next2->previous = ci;
            ci = next2; // continue
        }
    }

    /// <summary>
    /// Called when 'getCcalls(L)' larger or equal to LUAI_MAXCCALLS.
    /// If equal, raises an overflow error. If value is larger than
    /// LUAI_MAXCCALLS (which means it is handling an overflow) but
    /// not much larger, does not report an error (to allow overflow
    /// handling to work).
    /// </summary>
    internal static void luaE_checkcstack(lua_State* L)
    {
        if (getCcalls(L) == LUAI_MAXCCALLS)
        {
            luaG_runerror(L, "C stack overflow");
        }
        else if (getCcalls(L) >= LUAI_MAXCCALLS / 10 * 11)
        {
            luaD_errerr(L); // error while handling stack error
        }
    }

    internal static void luaE_incCstack(lua_State* L)
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
        setnilvalue(s2v(ci->func.p)); // 'function' entry for basic 'ci'
        ci->top.p = ci->func.p + 1 + LUA_MINSTACK; // +1 for 'function' entry
        ci->u.c.k = null;
        ci->callstatus = CIST_C;
        L->status = LUA_OK;
        L->errfunc = 0; // stack unwind can "throw away" the error function
    }

    private static void stack_init(lua_State* L1, lua_State* L)
    {
        int i;
        // initialise stack array
        L1->stack.p = luaM_newvector<StackValue>(L, BASIC_STACK_SIZE + EXTRA_STACK);
        L1->tbclist.p = L1->stack.p;
        for (i = 0; i < BASIC_STACK_SIZE + EXTRA_STACK; i++)
        {
            setnilvalue(s2v(L1->stack.p + i)); // erase new stack
        }

        L1->stack_last.p = L1->stack.p + BASIC_STACK_SIZE;
        // initialise first ci
        resetCI(L1);
        L1->top.p = L1->stack.p + 1; // +1 for 'function' entry
    }

    private static void freestack(lua_State* L)
    {
        if (L->stack.p == null!)
        {
            return; // stack not completely built yet
        }

        L->ci = &L->base_ci; // free the entire 'ci' list
        freeCI(L);
        Debug.Assert(L->nci == 0);
        // free stack
        luaM_freearray(L, L->stack.p, stacksize(L) + EXTRA_STACK);
    }

    /// <summary>
    /// Create registry table and its predefined values
    /// </summary>
    private static void init_registry(lua_State* L, global_State* g)
    {
        // create registry
        TValue aux;
        Table* registry = luaH_new(L);
        sethvalue(L, &g->l_registry, registry);
        luaH_resize(L, registry, LUA_RIDX_LAST, 0);
        // registry[1] = false
        setbfvalue(&aux);
        luaH_setint(L, registry, 1, &aux);
        // registry[LUA_RIDX_MAINTHREAD] = L
        setthvalue(L, &aux, L);
        luaH_setint(L, registry, LUA_RIDX_MAINTHREAD, &aux);
        // registry[LUA_RIDX_GLOBALS] = new table (table of globals)
        sethvalue(L, &aux, luaH_new(L));
        luaH_setint(L, registry, LUA_RIDX_GLOBALS, &aux);
    }

    /// <summary>
    /// open parts of the state that may cause memory-allocation errors.
    /// </summary>
    private static void f_luaopen(lua_State* L, void* ud)
    {
        global_State* g = G(L);
        stack_init(L, L); // init stack
        init_registry(L, g);
        luaS_init(L);
        luaT_init(L);
        luaX_init(L);
        g->gcstp = 0; // allow gc
        setnilvalue(&g->nilvalue); // now state is complete
        luai_userstateopen(L);
    }

    /// <summary>
    /// preinitialise a thread with consistent values without allocating
    /// any memory (to avoid errors)
    /// </summary>
    private static void preinit_thread(lua_State* L, global_State* g)
    {
        G(L) = g;
        L->stack.p = null;
        L->ci = null;
        L->nci = 0;
        L->twups = L; // thread has no upvalues
        L->nCcalls = 0;
        L->errorJmp = null;
        L->hook = null;
        L->hookmask = 0;
        L->basehookcount = 0;
        L->allowhook = true;
        resethookcount(L);
        L->openupval = null;
        L->status = LUA_OK;
        L->errfunc = 0;
        L->oldpc = 0;
        L->base_ci.previous = L->base_ci.next = null;
    }

    internal static long luaE_threadsize(lua_State* L)
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
        global_State* g = G(L);
        if (!completestate(g)) // closing a partially built state?
        {
            luaC_freeallobjects(L); // just collect its objects
        }
        else
        {
            // closing a fully built state
            resetCI(L);
            luaD_closeprotected(L, 1, LUA_OK); // close all upvalues
            L->top.p = L->stack.p + 1; // empty the stack to run finalisers
            luaC_freeallobjects(L); // collect all objects
            luai_userstateclose(L);
        }

        luaM_freearray(L, G(L)->strt.hash, G(L)->strt.size);
        freestack(L);
        Debug.Assert(gettotalbytes(g) == sizeof(global_State));
        g->frealloc.Call(g->ud, g, sizeof(global_State), 0); // free main block
    }

    public static lua_State* lua_newthread(lua_State* L)
    {
        global_State* g = G(L);
        lua_lock(L);
        luaC_checkGC(L);
        // create new thread
        GCObject* o = luaC_newobjdt(L, LUA_TTHREAD, sizeof(LX), LX_l_offset);
        lua_State* L1 = gco2th(o);
        // anchor it on L stack
        setthvalue2s(L, L->top.p, L1);
        api_incr_top(L);
        preinit_thread(L1, g);
        L1->hookmask = L->hookmask;
        L1->basehookcount = L->basehookcount;
        L1->hook = L->hook;
        resethookcount(L1);
        // initialise L1 extra space
        memcpy(
            lua_getextraspace(L1),
            lua_getextraspace(mainthread(g)),
            LUA_EXTRASPACE);
        luai_userstatethread(L, L1);
        stack_init(L1, L); // init stack
        lua_unlock(L);
        return L1;
    }

    private static void luaE_freethread(lua_State* L, lua_State* L1)
    {
        LX* l = fromstate(L1);
        luaF_closeupval(L1, L1->stack.p); // close all upvalues
        Debug.Assert(L1->openupval == null);
        luai_userstatefree(L, L1);
        freestack(L1);
        luaM_free(L, l);
    }

    internal static byte luaE_resetthread(lua_State* L, byte status)
    {
        resetCI(L);
        if (status == LUA_YIELD)
        {
            status = LUA_OK;
        }

        status = luaD_closeprotected(L, 1, status);
        if (status != LUA_OK) // errors?
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

    public static int lua_closethread(lua_State* L, lua_State* from)
    {
        lua_lock(L);
        L->nCcalls = from != null ? getCcalls(from) : 0;
        byte status = luaE_resetthread(L, L->status);
        if (L == from) // closing itself?
        {
            luaD_throwbaselevel(L, status);
        }

        lua_unlock(L);
        return status;
    }

    public static lua_State* lua_newstate(
        AllocFunction f,
        void* ud,
        uint seed)
    {
        global_State* g = (global_State*)f.Call(ud, null, LUA_TTHREAD, sizeof(global_State));
        if (g == null)
        {
            return null;
        }

        lua_State* L = &g->mainth.l;
        L->tt = LUA_VTHREAD;
        g->currentwhite = bitmask(WHITE0BIT);
        L->marked = luaC_white(g);
        preinit_thread(L, g);
        g->allgc = obj2gco(L); // by now, only object is the main thread
        L->next = null;
        incnny(L); // main thread is always non yieldable
        g->frealloc = f;
        g->ud = ud;
        g->warnf = null;
        g->ud_warn = null;
        g->seed = seed;
        g->gcstp = GCSTPGC; // no GC while building state
        g->strt.size = g->strt.nuse = 0;
        g->strt.hash = null;
        setnilvalue(&g->l_registry);
        g->panic = default;
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
        setivalue(&g->nilvalue, 0); // to signal that state is not yet built
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
            // memory allocation error: free partial state
            close_state(L);
            L = null;
        }

        return L;
    }

    public static void lua_close(lua_State* L)
    {
        lua_lock(L);
        L = mainthread(G(L)); // only the main thread can be closed
        close_state(L);
    }

    internal static void luaE_warning(lua_State* L, ReadOnlySpan<char> msg, bool tocont)
    {
        lua_WarnFunction wf = G(L)->warnf;
        if (wf != null)
        {
            wf(G(L)->ud_warn, msg, tocont);
        }
    }

    /// <summary>
    /// Generate a warning from an error message
    /// </summary>
    internal static void luaE_warnerror(lua_State* L, string where)
    {
        TValue* errobj = s2v(L->top.p - 1); // error object
        string msg = ttisstring(errobj)
            ? getnetstr(tsvalue(errobj))
            : "error object is not a string";
        // produce warning "error in %s (%s)" (where, msg)
        luaE_warning(L, "error in ", true);
        luaE_warning(L, where, true);
        luaE_warning(L, " (", true);
        luaE_warning(L, msg, true);
        luaE_warning(L, ")", false);
    }
}
