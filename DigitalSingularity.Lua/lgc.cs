namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
// /*
// ** Collectable objects may have one of three colors: white, which means
// ** the object is not marked; gray, which means the object is marked, but
// ** its references may be not marked; and black, which means that the
// ** object and all its references are marked.  The main invariant of the
// ** garbage collector, while marking objects, is that a black object can
// ** never point to a white one. Moreover, any gray object must be in a
// ** "gray list" (gray, grayagain, weak, allweak, ephemeron) so that it
// ** can be visited again before finishing the collection cycle. (Open
// ** upvalues are an exception to this rule, as they are attached to
// ** a corresponding thread.)  These lists have no meaning when the
// ** invariant is not being enforced (e.g., sweep phase).
// */

    /*
    ** Possible states of the Garbage Collector
    */
    private const byte GCSpropagate = 0;
    private const byte GCSenteratomic = 1;
    private const byte GCSatomic = 2;
    private const byte GCSswpallgc = 3;
    private const byte GCSswpfinobj = 4;
    private const byte GCSswptobefnz = 5;
    private const byte GCSswpend = 6;
    private const byte GCScallfin = 7;
    private const byte GCSpause = 8;

// #define issweepphase(g)  \
// 	(GCSswpallgc <= (g)->gcstate && (g)->gcstate <= GCSswpend)
//
//
// /*
// ** macro to tell when main invariant (white objects cannot point to black
// ** ones) must be kept. During a collection, the sweep phase may break
// ** the invariant, as objects turned white may point to still-black
// ** objects. The invariant is restored when sweep ends and all objects
// ** are white again.
// */
//
// #define keepinvariant(g)	((g)->gcstate <= GCSatomic)

    /*
    ** some useful bit tricks
    */
    private static void resetbits(ref byte x, byte m)
    {
        x &= (byte)~m;
    }

    private static void setbits(ref byte x, byte m)
    {
        x |= m;
    }

    private static bool testbits(byte x, byte m)
    {
        return (x & m) != 0;
    }

    private static byte bitmask(byte b)
    {
        return (byte)(1 << b);
    }

// #define l_setbit(x,b)		setbits(x, bitmask(b))
// #define resetbit(x,b)		resetbits(x, bitmask(b))
    private static bool testbit(byte x, byte b)
    {
        return testbits(x, bitmask(b));
    }

    /*
     ** Layout for bit use in 'marked' field. First three bits are
     ** used for object "age" in generational mode. Last bit is used
     ** by tests.
     */
    private const byte WHITE0BIT = 3;  /* object is white (type 0) */
    private const byte WHITE1BIT = 4;  /* object is white (type 1) */
    private const byte BLACKBIT = 5;  /* object is black */
    private const byte FINALIZEDBIT = 6;  /* object has been marked for finalisation */

    private const byte TESTBIT = 7;

    private const byte WHITEBITS = 1 << WHITE0BIT | 1 << WHITE1BIT;

    private static bool iswhite(GCObject* x)
    {
        return testbits(x->marked, WHITEBITS);
    }

    private static bool isblack(GCObject* x)
    {
        return testbit(x->marked, BLACKBIT);
    }

    // #define isgray(x)  /* neither white nor black */  \
// 	(!testbits((x)->marked, WHITEBITS | bitmask(BLACKBIT)))
//
// #define tofinalize(x)	testbit((x)->marked, FINALIZEDBIT)

    private static byte otherwhite(global_State* g)
    {
        return (byte)(g->currentwhite ^ WHITEBITS);
    }

    private static bool isdeadm(byte ow, byte m)
    {
        return (m & ow) != 0;
    }

    private static bool isdead(global_State* g, GCObject* v)
    {
        return isdeadm(otherwhite(g), v->marked);
    }

    private static void changewhite(GCObject* x)
    {
        x->marked ^= WHITEBITS;
    }
    
    // #define nw2black(x)  \
// 	check_exp(!iswhite(x), l_setbit((x)->marked, BLACKBIT))

    private static byte luaC_white(global_State* g)
    {
        return (byte)(g->currentwhite & WHITEBITS);
    }

    /* object age in generational mode */
    private const byte G_NEW = 0;	/* created in current cycle */
    private const byte G_SURVIVAL = 1;	/* created in previous cycle */
    private const byte G_OLD0 = 2;	/* marked old by frw. barrier in this cycle */
    private const byte G_OLD1 = 3;	/* first full cycle as old */
    private const byte G_OLD = 4;	/* really old object (not to be visited) */
    private const byte G_TOUCHED1 = 5;	/* old object touched this cycle */
    private const byte G_TOUCHED2 = 6;	/* old object touched in previous cycle */

    private const byte AGEBITS = 7;  /* all age bits (111) */

    private static byte getage(GCObject* o)
    {
        return (byte)(o->marked & AGEBITS);
    }

    private static void setage(GCObject* o, byte a)
    {
        o->marked = (byte)(o->marked & ~AGEBITS | a);
    }

    private static bool isold(GCObject* o)
    {
        return getage(o) > G_SURVIVAL;
    }

    // /*
// ** In generational mode, objects are created 'new'. After surviving one
// ** cycle, they become 'survival'. Both 'new' and 'survival' can point
// ** to any other object, as they are traversed at the end of the cycle.
// ** We call them both 'young' objects.
// ** If a survival object survives another cycle, it becomes 'old1'.
// ** 'old1' objects can still point to survival objects (but not to
// ** new objects), so they still must be traversed. After another cycle
// ** (that, being old, 'old1' objects will "survive" no matter what)
// ** finally the 'old1' object becomes really 'old', and then they
// ** are no more traversed.
// **
// ** To keep its invariants, the generational mode uses the same barriers
// ** also used by the incremental mode. If a young object is caught in a
// ** forward barrier, it cannot become old immediately, because it can
// ** still point to other young objects. Instead, it becomes 'old0',
// ** which in the next cycle becomes 'old1'. So, 'old0' objects is
// ** old but can point to new and survival objects; 'old1' is old
// ** but cannot point to new objects; and 'old' cannot point to any
// ** young object.
// **
// ** If any old object ('old0', 'old1', 'old') is caught in a back
// ** barrier, it becomes 'touched1' and goes into a gray list, to be
// ** visited at the end of the cycle.  There it evolves to 'touched2',
// ** which can point to survivals but not to new objects. In yet another
// ** cycle then it becomes 'old' again.
// **
// ** The generational mode must also control the colors of objects,
// ** because of the barriers.  While the mutator is running, young objects
// ** are kept white. 'old', 'old1', and 'touched2' objects are kept black,
// ** as they cannot point to new objects; exceptions are threads and open
// ** upvalues, which age to 'old1' and 'old' but are kept gray. 'old0'
// ** objects may be gray or black, as in the incremental mode. 'touched1'
// ** objects are kept gray, as they must be visited again at the end of
// ** the cycle.
// */

    /*
    ** {======================================================
    ** Default Values for GC parameters
    ** =======================================================
    */

    /*
    ** Minor collections will shift to major ones after LUAI_MINORMAJOR%
    ** bytes become old.
    */
    private const byte LUAI_MINORMAJOR = 70;

    /*
    ** Major collections will shift to minor ones after a collection
    ** collects at least LUAI_MAJORMINOR% of the new bytes.
    */
    private const byte LUAI_MAJORMINOR = 50;

    /*
    ** A young (minor) collection will run after creating LUAI_GENMINORMUL%
    ** new bytes.
    */
    private const byte LUAI_GENMINORMUL = 20;


    /* incremental */

    /* Number of bytes must be LUAI_GCPAUSE% before starting new cycle */
    private const byte LUAI_GCPAUSE = 250;

    /*
    ** Step multiplier: The collector handles LUAI_GCMUL% work units for
    ** each new allocated word. (Each "work unit" corresponds roughly to
    ** sweeping one object or traversing one slot.)
    */
    private const byte LUAI_GCMUL = 200;

    /* How many bytes to allocate before next GC step */
    private static readonly uint LUAI_GCSTEPSIZE = (uint)(200 * sizeof(Table));

    private static void setgcparam(global_State* g, byte p, uint v)
    {
        g->gcparams[p] = luaO_codeparam(v);
    }
    
    // #define applygcparam(g,p,x)  luaO_applyparam(g->gcparams[LUA_GCP##p], x)
//
// /* }====================================================== */

    /*
    ** Control when GC is running:
    */
    private const byte GCSTPUSR = 1;  /* bit true when GC stopped by user */
    private const byte GCSTPGC = 2;  /* bit true when GC stopped by itself */
    private const byte GCSTPCLS = 4;  /* bit true when closing Lua state */

    private static bool gcrunning(global_State* g)
    {
        return g->gcstp == 0;
    }

    // /*
// ** Does one step of collection when debt becomes zero. 'pre'/'pos'
// ** allows some adjustments to be done only when needed. macro
// ** 'condchangemem' is used only for heavy tests (forcing a full
// ** GC cycle on every opportunity)
// */
//
// #if !defined(HARDMEMTESTS)
// #define condchangemem(L,pre,pos,emg)	((void)0)
// #else
// #define condchangemem(L,pre,pos,emg)  \
// 	{ if (gcrunning(G(L))) { pre; luaC_fullgc(L, emg); pos; } }
// #endif
//
// #define luaC_condGC(L,pre,pos) \
// 	{ if (G(L)->GCdebt <= 0) { pre; luaC_step(L); pos;}; \
// 	  condchangemem(L,pre,pos,0); }

    /* more often than not, 'pre'/'pos' are empty */
    private static void luaC_checkGC(lua_State* L)
    {
        {
            if (G(L)->GCdebt <= 0)
            {
                luaC_step(L);
            }

            if (gcrunning(G(L)))
            {
                luaC_fullgc(L, false);
            }
        }
    }

    // #define luaC_objbarrier(L,p,o) (  \
// 	(isblack(p) && iswhite(o)) ? \
// 	luaC_barrier_(L,obj2gco(p),obj2gco(o)) : cast_void(0))
//
// #define luaC_barrier(L,p,v) (  \
// 	iscollectable(v) ? luaC_objbarrier(L,p,gcvalue(v)) : cast_void(0))

    private static void luaC_objbarrierback(lua_State* L, GCObject* p, GCObject* o)
    {
        if (isblack(p) && iswhite(o))
        {
            luaC_barrierback_(L, p);
        }
    }

    private static void luaC_barrierback(lua_State* L, GCObject* p, TValue* v)
    {
        if (iscollectable(v))
        {
            luaC_objbarrierback(L, p, gcvalue(v));
        }
    }

    private static partial void luaC_fix(lua_State* L, GCObject* o);
    
// LUAI_FUNC void luaC_freeallobjects (lua_State *L);

    private static partial void luaC_step(lua_State* L);

// LUAI_FUNC void luaC_runtilstate (lua_State *L, int state, int fast);

    private static partial void luaC_fullgc(lua_State* L, bool isemergency);

    private static partial GCObject* luaC_newobj(lua_State* L, byte tt, long sz);

    private static partial GCObject* luaC_newobjdt(lua_State* L, byte tt, long sz, long offset);
    
// LUAI_FUNC void luaC_barrier_ (lua_State *L, GCObject *o, GCObject *v);

    private static partial void luaC_barrierback_(lua_State* L, GCObject* o);

// LUAI_FUNC void luaC_checkfinalizer (lua_State *L, GCObject *o, Table *mt);
// LUAI_FUNC void luaC_changemode (lua_State *L, int newmode);
}
