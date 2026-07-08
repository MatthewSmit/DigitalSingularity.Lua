namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /*
     * WARNING: if you change the order of this enumeration,
     * grep "ORDER TM" and "ORDER OP"
     */
    internal enum TMS
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
    private const byte maskflags = (byte)~(~0u << (int)TMS.EQ + 1);

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

    private static string ttypename(int x) => luaT_typenames_[x + 1];

    private static readonly string[] luaT_typenames_ =
    [
        "no value",
        "nil", "boolean", udatatypename, "number",
        "string", "table", "function", udatatypename, "thread",
        "upvalue", "proto", /* these last cases are used for tests only */
    ];
    
    private const string udatatypename = "userdata";

    private static readonly string[] luaT_eventname =
    [
        /* ORDER TM */
        "__index", "__newindex",
        "__gc", "__mode", "__len", "__eq",
        "__add", "__sub", "__mul", "__mod", "__pow",
        "__div", "__idiv",
        "__band", "__bor", "__bxor", "__shl", "__shr",
        "__unm", "__bnot", "__lt", "__le",
        "__concat", "__call", "__close",
    ];

    private static void luaT_init(lua_State* L)
    {
        for (int i = 0; i < (int)TMS.N; i++)
        {
            G(L)->tmname[i] = luaS_new(L, luaT_eventname[i]);
            luaC_fix(L, obj2gco(G(L)->tmname[i])); /* never collect these names */
        }
    }

    /*
     ** function to be used with macro "fasttm": optimized for absence of
     ** tag methods
     */
    internal static TValue* luaT_gettm(Table* events, TMS @event, TString* ename)
    {
        TValue* tm = luaH_Hgetshortstr(events, ename);
        Debug.Assert(@event <= TMS.EQ);
        if (notm(tm))
        {
            /* no tag method? */
            events->flags |= (byte)(1u << (int)@event); /* cache this fact */
            return null;
        }

        return tm;
    }

    internal static TValue* luaT_gettmbyobj(lua_State* L, TValue* o, TMS @event)
    {
        Table* mt = ttype(o) switch
        {
            LUA_TTABLE => hvalue(o)->metatable,
            LUA_TUSERDATA => uvalue(o)->metatable,
            _ => G(L)->mt[ttype(o)],
        };

        return mt != null ? luaH_Hgetshortstr(mt, G(L)->tmname[(int)@event]) : &G(L)->nilvalue;
    }

    /*
     ** Return the name of the type of an object. For tables and userdata
     ** with metatable, use their '__name' metafield, if present.
     */
    internal static string luaT_objtypename(lua_State* L, TValue* o)
    {
        Table* mt;
        if (ttistable(o) && (mt = hvalue(o)->metatable) != null ||
            ttisfulluserdata(o) && (mt = uvalue(o)->metatable) != null)
        {
            TValue* name = luaH_Hgetshortstr(mt, luaS_new(L, "__name"));
            if (ttisstring(name)) /* is '__name' a string? */
            {
                return getnetstr(tsvalue(name)); /* use it as type name */
            }
        }

        return ttypename(ttype(o)); /* else use standard type name */
    }

    private static void luaT_callTM(lua_State* L, TValue* f, TValue* p1, TValue* p2, TValue* p3)
    {
        StkId func = L->top.p;
        setobj2s(L, func, f); /* push function (assume EXTRA_STACK) */
        setobj2s(L, func + 1, p1); /* 1st argument */
        setobj2s(L, func + 2, p2); /* 2nd argument */
        setobj2s(L, func + 3, p3); /* 3rd argument */
        L->top.p = func + 4;
        /* metamethod may yield only when called from Lua code */
        if (isLuacode(L->ci))
        {
            luaD_call(L, func, 0);
        }
        else
        {
            luaD_callnoyield(L, func, 0);
        }
    }

    private static byte luaT_callTMres(lua_State* L, TValue* f, TValue* p1, TValue* p2, StkId p3)
    {
        IntPtr result = savestack(L, p3);
        StkId func = L->top.p;
        setobj2s(L, func, f); /* push function (assume EXTRA_STACK) */
        setobj2s(L, func + 1, p1); /* 1st argument */
        setobj2s(L, func + 2, p2); /* 2nd argument */
        L->top.p += 3;
        /* metamethod may yield only when called from Lua code */
        if (isLuacode(L->ci))
        {
            luaD_call(L, func, 1);
        }
        else
        {
            luaD_callnoyield(L, func, 1);
        }

        p3 = restorestack(L, result);
        setobjs2s(L, p3, --L->top.p); /* move result to its place */
        return ttypetag(s2v(p3)); /* return tag of the result */
    }

    private static int callbinTM(
        lua_State* L,
        TValue* p1,
        TValue* p2,
        StkId res,
        TMS @event)
    {
        TValue* tm = luaT_gettmbyobj(L, p1, @event); /* try first operand */
        if (notm(tm))
        {
            tm = luaT_gettmbyobj(L, p2, @event); /* try second operand */
        }

        if (notm(tm))
        {
            return -1; /* tag method not found */
        }

        // call tag method and return the tag of the result 
        return luaT_callTMres(L, tm, p1, p2, res);
    }

    private static void luaT_trybinTM(lua_State* L, TValue* p1, TValue* p2, StkId res, TMS @event)
    {
        if (callbinTM(L, p1, p2, res, @event) < 0)
        {
            switch (@event)
            {
                case TMS.BAND:
                case TMS.BOR:
                case TMS.BXOR:
                case TMS.SHL:
                case TMS.SHR:
                case TMS.BNOT:
                    if (ttisnumber(p1) && ttisnumber(p2))
                    {
                        luaG_tointerror(L, p1, p2);
                    }
                    else
                    {
                        luaG_opinterror(L, p1, p2, "perform bitwise operation on");
                    }

                    break;

                default:
                    luaG_opinterror(L, p1, p2, "perform arithmetic on");
                    break;
            }
        }
    }

    /*
     ** The use of 'p1' after 'callbinTM' is safe because, when a tag
     ** method is not found, 'callbinTM' cannot change the stack.
     */
    private static void luaT_tryconcatTM(lua_State* L)
    {
        StkId p1 = L->top.p - 2; /* first argument */
        if (callbinTM(L, s2v(p1), s2v(p1 + 1), p1, TMS.CONCAT) < 0)
        {
            luaG_concaterror(L, s2v(p1), s2v(p1 + 1));
        }
    }

    private static void luaT_trybinassocTM(
        lua_State* L,
        TValue* p1,
        TValue* p2,
        bool flip,
        StkId res,
        TMS @event)
    {
        if (flip)
        {
            luaT_trybinTM(L, p2, p1, res, @event);
        }
        else
        {
            luaT_trybinTM(L, p1, p2, res, @event);
        }
    }

    private static void luaT_trybiniTM(lua_State* L, TValue* p1, long i2, bool flip, StkId res, TMS @event)
    {
        TValue aux;
        setivalue(&aux, i2);
        luaT_trybinassocTM(L, p1, &aux, flip, res, @event);
    }

    /*
     ** Calls an order tag method.
     */
    private static bool luaT_callorderTM(lua_State* L, TValue* p1, TValue* p2, TMS @event)
    {
        int tag = callbinTM(L, p1, p2, L->top.p, @event); /* try original event */
        if (tag >= 0) /* found tag method? */
        {
            return !tagisfalse((byte)tag);
        }

        luaG_ordererror(L, p1, p2); /* no metamethod found */
        return false; /* to avoid warnings */
    }

    private static bool luaT_callorderiTM(lua_State* L, TValue* p1, int v2, bool flip, bool isfloat, TMS @event)
    {
        TValue aux;
        if (isfloat)
        {
            setfltvalue(&aux, v2);
        }
        else
        {
            setivalue(&aux, v2);
        }

        TValue* p2;
        if (flip)
        {
            /* arguments were exchanged? */
            p2 = p1;
            p1 = &aux; /* correct them */
        }
        else
        {
            p2 = &aux;
        }

        return luaT_callorderTM(L, p1, p2, @event);
    }

    /*
     ** Create a vararg table at the top of the stack, with 'n' elements
     ** starting at 'f'.
     */
    private static void createvarargtab(lua_State* L, StkId f, int n)
    {
        Table* t = luaH_new(L);
        sethvalue(L, s2v(L->top.p), t);
        L->top.p++;
        luaH_resize(L, t, (uint)n, 1);
        TValue key;
        setsvalue(L, &key, luaS_new(L, "n")); /* key is "n" */
        TValue value;
        setivalue(&value, n); /* value is n */
        /* No need to anchor the key: Due to the resize, the next operation
           cannot trigger a garbage collection */
        luaH_set(L, t, &key, &value); /* t.n = n */
        for (int i = 0; i < n; i++)
        {
            luaH_setint(L, t, i + 1, s2v(f + i));
        }

        luaC_checkGC(L);
    }

    /*
     ** initial stack:  func arg1 ... argn extra1 ...
     **                 ^ ci->func                    ^ L->top
     ** final stack: func nil ... nil extra1 ... func arg1 ... argn
     **                                          ^ ci->func
     */
    private static void buildhiddenargs(
        lua_State* L,
        CallInfo* ci,
        Proto* p,
        int totalargs,
        int nfixparams,
        int nextra)
    {
        ci->u.l.nextraargs = nextra;
        luaD_checkstack(L, p->maxstacksize + 1);
        /* copy function to the top of the stack, after extra arguments */
        setobjs2s(L, L->top.p++, ci->func.p);
        /* move fixed parameters to after the copied function */
        for (int i = 1; i <= nfixparams; i++)
        {
            setobjs2s(L, L->top.p++, ci->func.p + i);
            setnilvalue(s2v(ci->func.p + i)); /* erase original parameter (for GC) */
        }

        ci->func.p += totalargs + 1; /* 'func' now lives after hidden arguments */
        ci->top.p += totalargs + 1;
    }

    private static void luaT_adjustvarargs(lua_State* L, CallInfo* ci, Proto* p)
    {
        int totalargs = (int)(L->top.p - ci->func.p) - 1;
        int nfixparams = p->numparams;
        int nextra = totalargs - nfixparams; /* number of extra arguments */
        if ((p->flag & PF_VATAB) != 0)
        {
            /* does it need a vararg table? */
            Debug.Assert((p->flag & PF_VAHID) == 0);
            createvarargtab(L, ci->func.p + nfixparams + 1, nextra);
            /* move table to proper place (last parameter) */
            setobjs2s(L, ci->func.p + nfixparams + 1, L->top.p - 1);
        }
        else
        {
            /* no table */
            Debug.Assert((p->flag & PF_VAHID) != 0);
            buildhiddenargs(L, ci, p, totalargs, nfixparams, nextra);
            /* set vararg parameter to nil */
            setnilvalue(s2v(ci->func.p + nfixparams + 1));
            Debug.Assert(L->top.p <= ci->top.p && ci->top.p <= L->stack_last.p);
        }
    }

    private static void luaT_getvararg(CallInfo* ci, StkId ra, TValue* rc)
    {
        int nextra = ci->u.l.nextraargs;
        if (tointegerns(rc, out long n))
        {
            /* integral value? */
            if ((ulong)n - 1 < (uint)nextra)
            {
                StkId slot = ci->func.p - nextra + (int)n - 1;
                setobjs2s(null, ra, slot);
                return;
            }
        }
        else if (ttisstring(rc))
        {
            /* string value? */
            byte* s = getlstr(tsvalue(rc), out int len);
            if (len == 1 && s[0] == 'n')
            {
                /* key is "n"? */
                setivalue(s2v(ra), nextra);
                return;
            }
        }

        setnilvalue(s2v(ra)); /* else produce nil */
    }

    /*
     ** Get the number of extra arguments in a vararg function. If vararg
     ** table has been optimised away, that number is in the call info.
     ** Otherwise, get the field 'n' from the vararg table and check that it
     ** has a proper value (non-negative integer not larger than the stack
     ** limit).
     */
    private static int getnumargs(lua_State* L, CallInfo* ci, Table* h)
    {
        if (h == null) /* no vararg table? */
        {
            return ci->u.l.nextraargs;
        }

        TValue res;
        if (luaH_getshortstr(h, luaS_new(L, "n"), &res) != LUA_VNUMINT ||
            (ulong)ivalue(&res) > int.MaxValue / 2)
        {
            luaG_runerror(L, "vararg table has no proper 'n'");
        }

        return (int)ivalue(&res);
    }

    /*
    ** Get 'wanted' vararg arguments and put them in 'where'. 'vatab' is
    ** the register of the vararg table or -1 if there is no vararg table.
    */
    private static void luaT_getvarargs(lua_State* L, CallInfo* ci, StkId where, int wanted, int vatab)
    {
        Table* h = vatab < 0 ? null : hvalue(s2v(ci->func.p + vatab + 1));
        int nargs = getnumargs(L, ci, h); /* number of available vararg args. */
        int touse; /* 'touse' is minimum between 'wanted' and 'nargs' */
        if (wanted < 0)
        {
            touse = wanted = nargs; /* get all extra arguments available */
            checkstackp(L, nargs, ref where); /* ensure stack space */
            L->top.p = where + nargs; /* next instruction will need top */
        }
        else
        {
            touse = nargs > wanted ? wanted : nargs;
        }

        int i;
        if (h == null)
        {
            /* no vararg table? */
            for (i = 0; i < touse; i++) /* get vararg values from the stack */
            {
                setobjs2s(L, where + i, ci->func.p - nargs + i);
            }
        }
        else
        {
            /* get vararg values from vararg table */
            for (i = 0; i < touse; i++)
            {
                byte tag = luaH_getint(h, i + 1, s2v(where + i));
                if (tagisempty(tag))
                {
                    setnilvalue(s2v(where + i));
                }
            }
        }

        for (; i < wanted; i++) /* complete required results with nil */
        {
            setnilvalue(s2v(where + i));
        }
    }
}
