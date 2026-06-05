namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /*
     * WARNING: if you change the order of this enumeration,
     * grep "ORDER TM" and "ORDER OP"
     */
    private enum TMS
    {
        INDEX,
        NEWINDEX,
        GC,
        MODE,
        LEN,
        EQ, /* last tag method with fast access */
        ADD,
        SUB,
        MUL,
        MOD,
        POW,
        DIV,
        IDIV,
        BAND,
        BOR,
        BXOR,
        SHL,
        SHR,
        UNM,
        BNOT,
        LT,
        LE,
        CONCAT,
        CALL,
        CLOSE,
        N, /* number of elements in the enum */
    }

    /*
     ** Mask with 1 in all fast-access methods. A 1 in any of these bits
     ** in the flag of a (meta)table means the metatable does not have the
     ** corresponding metamethod field. (Bit 6 of the flag indicates that
     ** the table is using the dummy node; bit 7 is used for 'isrealasize'.)
     */
    private const byte maskflags = (byte)(~(~0u << ((int)TMS.EQ + 1)));

    /*
     ** Test whether there is no tagmethod.
     ** (Because tagmethods use raw accesses, the result may be an "empty" nil.)
     */
    private static bool notm(TValue* tm)
    {
        return ttisnil(tm);
    }

    private static bool checknoTM(Table* mt, TMS e)
    {
        return mt == null || (mt->flags & 1u << (int)e) != 0;
    }

    private static TValue* gfasttm(global_State* g, Table* mt, TMS e)
    {
        return checknoTM(mt, e) ? null : luaT_gettm(mt, e, g->tmname[(int)e]);
    }

    private static TValue* fasttm(lua_State* l, Table* mt, TMS e)
    {
        return gfasttm(G(l), mt, e);
    }

    private static string ttypename(byte x) => luaT_typenames_[x + 1];

    private static readonly string[] luaT_typenames_ =
    [
        "no value",
        "nil", "boolean", udatatypename, "number",
        "string", "table", "function", udatatypename, "thread",
        "upvalue", "proto", /* these last cases are used for tests only */
    ];

    private static partial string luaT_objtypename(lua_State* L, TValue* o);

    private static partial TValue* luaT_gettm(Table* events, TMS @event, TString* ename);

    private static partial TValue* luaT_gettmbyobj(lua_State* L, TValue* o, TMS @event);

    private static partial void luaT_init(lua_State* L);

    private static partial void luaT_callTM(lua_State* L, TValue* f, TValue* p1, TValue* p2, TValue* p3);

    private static partial byte luaT_callTMres(lua_State* L, TValue* f, TValue* p1, TValue* p2, StkId p3);

    private static partial void luaT_trybinTM(lua_State* L, TValue* p1, TValue* p2, StkId res, TMS @event);

    private static partial void luaT_tryconcatTM(lua_State* L);

    private static partial void luaT_trybinassocTM(
        lua_State* L,
        TValue* p1,
        TValue* p2,
        bool flip,
        StkId res,
        TMS @event);

    private static partial void luaT_trybiniTM(lua_State* L, TValue* p1, long i2, bool flip, StkId res, TMS @event);

    private static partial int luaT_callorderTM(lua_State* L, TValue* p1, TValue* p2, TMS @event);

    private static partial bool luaT_callorderiTM(lua_State* L, TValue* p1, int v2, int inv, bool isfloat, TMS @event);

    private static partial void luaT_adjustvarargs(lua_State* L, CallInfo* ci, Proto* p);

    private static partial void luaT_getvararg(CallInfo* ci, StkId ra, TValue* rc);

    private static partial void luaT_getvarargs(lua_State* L, CallInfo* ci, StkId where, int wanted, int vatab);
}
