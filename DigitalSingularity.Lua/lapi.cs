namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    /// <summary>
    /// Increments 'L->top.p', checking for stack overflows.
    /// </summary>
    private static void api_incr_top(lua_State* L)
    {
        L->top.p++;
        Debug.Assert(L->top.p <= L->ci->top.p, "stack overflow");
    }

    /// <summary>
    /// If a call returns too many multiple returns, the callee may not have
    /// stack space to accommodate all results. In this case, this macro
    /// increases its stack space ('L->ci->top.p').
    /// </summary>
    private static void adjustresults(lua_State* L, int nres)
    {
        if (nres <= LUA_MULTRET && L->ci->top.p < L->top.p)
        {
            L->ci->top.p = L->top.p;
        }
    }

    /// <summary>
    /// Ensure the stack has at least 'n' elements.
    /// </summary>
    private static void api_checknelems(lua_State* L, int n)
    {
        Debug.Assert(n < L->top.p - L->ci->func.p, "not enough elements in the stack");
    }

    /// <summary>
    /// Ensure the stack has at least 'n' elements to be popped. (Some
    /// functions only update a slot after checking it for popping, but that
    /// is only an optimisation for a pop followed by a push.)
    /// </summary>
    private static void api_checkpop(lua_State* L, int n)
    {
        Debug.Assert(
            n < L->top.p - L->ci->func.p && L->tbclist.p < L->top.p - n,
            "not enough free elements in the stack");
    }
    
    /// <summary>
    /// Test for a valid index (one that is not the 'nilvalue').
    /// </summary>
    private static bool isvalid(lua_State* L, TValue* o)
    {
        return o != &G(L)->nilvalue;
    }

    /// <summary>
    /// Test for pseudo index.
    /// </summary>
    private static bool ispseudo(int i)
    {
        return i <= LUA_REGISTRYINDEX;
    }

    /// <summary>
    /// Test for upvalue.
    /// </summary>
    private static bool isupvalue(int i)
    {
        return i < LUA_REGISTRYINDEX;
    }

    /// <summary>
    /// Convert an acceptable index to a pointer to its respective value.
    /// Non-valid indices return the special nil value 'G(L)->nilvalue'.
    /// </summary>
    private static TValue* index2value(lua_State* L, int idx)
    {
        CallInfo* ci = L->ci;
        if (idx > 0)
        {
            StkId o = ci->func.p + idx;
            Debug.Assert(idx <= ci->top.p - (ci->func.p + 1), "unacceptable index");
            if (o >= L->top.p)
            {
                return &G(L)->nilvalue;
            }

            return s2v(o);
        }

        if (!ispseudo(idx))
        {
            // negative index
            Debug.Assert(idx != 0 && -idx <= L->top.p - (ci->func.p + 1), "invalid index");
            return s2v(L->top.p + idx);
        }

        if (idx == LUA_REGISTRYINDEX)
        {
            return &G(L)->l_registry;
        }

        // upvalues
        idx = LUA_REGISTRYINDEX - idx;
        Debug.Assert(idx <= MAXUPVAL + 1, "upvalue index too large");
        if (ttisCclosure(s2v(ci->func.p)))
        {
            // C closure?
            CClosure* func = clCvalue(s2v(ci->func.p));
            return idx <= func->nupvalues
                ? CClosure.GetUpValuePtr(func, idx - 1)
                : &G(L)->nilvalue;
        }

        // light C function or Lua function (through a hook)?)
        Debug.Assert(ttislcf(s2v(ci->func.p)), "caller not a C function");
        return &G(L)->nilvalue; // no upvalues
    }

    /// <summary>
    /// Convert a valid actual index (not a pseudo-index) to its address.
    /// </summary>
    private static StkId index2stack(lua_State* L, int idx)
    {
        CallInfo* ci = L->ci;
        if (idx > 0)
        {
            StkId o = ci->func.p + idx;
            Debug.Assert(o < L->top.p, "invalid index");
            return o;
        }

        // non-positive index
        Debug.Assert(idx != 0 && -idx <= L->top.p - (ci->func.p + 1), "invalid index");
        Debug.Assert(!ispseudo(idx), "invalid index");
        return L->top.p + idx;
    }

    public static bool lua_checkstack(lua_State* L, int n)
    {
        lua_lock(L);
        CallInfo* ci = L->ci;
        Debug.Assert(n >= 0, "negative 'n'");
        bool res;
        if (L->stack_last.p - L->top.p > n) // stack large enough?
        {
            res = true; // yes; check is OK
        }
        else
        {
            // need to grow stack
            res = luaD_growstack(L, n, false);
        }

        if (res && ci->top.p < L->top.p + n)
        {
            ci->top.p = L->top.p + n; // adjust frame top
        }

        lua_unlock(L);
        return res;
    }

    public static void lua_xmove(lua_State* from, lua_State* to, int n)
    {
        if (from == to)
        {
            return;
        }

        lua_lock(to);
        api_checkpop(from, n);
        Debug.Assert(G(from) == G(to), "moving among independent states");
        Debug.Assert(to->ci->top.p - to->top.p >= n, "stack overflow");
        from->top.p -= n;
        for (int i = 0; i < n; i++)
        {
            setobjs2s(to, to->top.p, from->top.p + i);
            to->top.p++; // stack already checked by previous 'api_check'
        }

        lua_unlock(to);
    }

    public static lua_CFunction lua_atpanic(lua_State* L, lua_CFunction panicf)
    {
        lua_lock(L);
        lua_CFunction old = G(L)->panic;
        G(L)->panic = panicf;
        lua_unlock(L);
        return old;
    }

    public static long lua_version(lua_State* L)
    {
        return LUA_VERSION_NUM;
    }

    /// <summary>
    /// Convert an acceptable stack index into an absolute index.
    /// </summary>
    public static int lua_absindex(lua_State* L, int idx)
    {
        return idx > 0 || ispseudo(idx)
            ? idx
            : (int)(L->top.p - L->ci->func.p) + idx;
    }

    public static int lua_gettop(lua_State* L)
    {
        return (int)(L->top.p - (L->ci->func.p + 1));
    }

    public static void lua_settop(lua_State* L, int idx)
    {
        lua_lock(L);
        CallInfo* ci = L->ci;
        StkId func = ci->func.p;

        nint diff; // difference for new top
        if (idx >= 0)
        {
            Debug.Assert(idx <= ci->top.p - (func + 1), "new top too large");
            diff = (IntPtr)(func + 1 + idx - L->top.p);
            for (; diff > 0; diff--)
            {
                setnilvalue(s2v(L->top.p++)); // clear new slots
            }
        }
        else
        {
            Debug.Assert(-(idx + 1) <= L->top.p - (func + 1), "invalid new top");
            diff = idx + 1; // will "subtract" index (as it is negative)
        }

        StkId newtop = L->top.p + diff;
        if (diff < 0 && L->tbclist.p >= newtop)
        {
            Debug.Assert((ci->callstatus & CIST_TBC) != 0);
            newtop = luaF_close(L, newtop, CLOSEKTOP, false);
        }

        L->top.p = newtop; // correct top only after closing any upvalue
        lua_unlock(L);
    }

    public static void lua_closeslot(lua_State* L, int idx)
    {
        lua_lock(L);
        StkId level = index2stack(L, idx);
        Debug.Assert(
            (L->ci->callstatus & CIST_TBC) != 0 && L->tbclist.p == level,
            "no variable to close at given level");
        level = luaF_close(L, level, CLOSEKTOP, false);
        setnilvalue(s2v(level));
        lua_unlock(L);
    }

    /// <summary>
    /// Reverse the stack segment from 'from' to 'to'
    /// (auxiliary to 'lua_rotate')
    /// Note that we move(copy) only the value inside the stack.
    /// (We do not move additional fields that may exist.)
    /// </summary>
    private static void reverse(lua_State* L, StkId from, StkId to)
    {
        for (; from < to; from++, to--)
        {
            TValue temp;
            setobj(L, &temp, s2v(from));
            setobjs2s(L, from, to);
            setobj2s(L, to, &temp);
        }
    }

    /// <summary>
    /// Let x = AB, where A is a prefix of length 'n'. Then,
    /// rotate x n == BA. But BA == (A^r . B^r)^r.
    /// </summary>
    public static void lua_rotate(lua_State* L, int idx, int n)
    {
        lua_lock(L);
        StkId t = L->top.p - 1; // end of stack segment being rotated
        StkId p = index2stack(L, idx); // start of segment
        Debug.Assert(L->tbclist.p < p, "moving a to-be-closed slot");
        Debug.Assert((n >= 0 ? n : -n) <= t - p + 1, "invalid 'n'");
        StkId m = n >= 0 ? t - n : p - n - 1; // end of prefix
        reverse(L, p, m); // reverse the prefix with length 'n'
        reverse(L, m + 1, t); // reverse the suffix
        reverse(L, p, t); // reverse the entire segment
        lua_unlock(L);
    }

    public static void lua_copy(lua_State* L, int fromidx, int toidx)
    {
        lua_lock(L);
        TValue* fr = index2value(L, fromidx);
        TValue* to = index2value(L, toidx);
        Debug.Assert(isvalid(L, to), "invalid index");
        setobj(L, to, fr);
        if (isupvalue(toidx)) // function upvalue?
        {
            luaC_barrier(L, (GCObject*)clCvalue(s2v(L->ci->func.p)), fr);
        }

        // LUA_REGISTRYINDEX does not need gc barrier
        // (collector revisits it before finishing collection)
        lua_unlock(L);
    }

    public static void lua_pushvalue(lua_State* L, int idx)
    {
        lua_lock(L);
        setobj2s(L, L->top.p, index2value(L, idx));
        api_incr_top(L);
        lua_unlock(L);
    }

    public static int lua_type(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return isvalid(L, o) ? ttype(o) : LUA_TNONE;
    }

    public static string lua_typename(lua_State* L, int t)
    {
        Debug.Assert(t is >= LUA_TNONE and < LUA_NUMTYPES, "invalid type");
        return ttypename(t);
    }

    public static bool lua_iscfunction(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return ttislcf(o) || ttisCclosure(o);
    }

    public static bool lua_isinteger(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return ttisinteger(o);
    }

    public static bool lua_isnumber(lua_State* L, int idx)
    {
        double n;
        TValue* o = index2value(L, idx);
        return tonumber(o, &n);
    }

    public static bool lua_isstring(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return ttisstring(o) || cvt2str(o);
    }

    public static bool lua_isuserdata(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return ttisfulluserdata(o) || ttislightuserdata(o);
    }

    public static bool lua_rawequal(lua_State* L, int index1, int index2)
    {
        TValue* o1 = index2value(L, index1);
        TValue* o2 = index2value(L, index2);
        return isvalid(L, o1) && isvalid(L, o2) && luaV_rawequalobj(o1, o2);
    }

    public static void lua_arith(lua_State* L, int op)
    {
        lua_lock(L);
        if (op != LUA_OPUNM && op != LUA_OPBNOT)
        {
            api_checkpop(L, 2); // all other operations expect two operands
        }
        else
        {
            // for unary operations, add fake 2nd operand
            api_checkpop(L, 1);
            setobjs2s(L, L->top.p, L->top.p - 1);
            api_incr_top(L);
        }

        // first operand at top - 2, second at top - 1; result go to top - 2
        luaO_arith(L, op, s2v(L->top.p - 2), s2v(L->top.p - 1), L->top.p - 2);
        L->top.p--; // pop second operand
        lua_unlock(L);
    }

    public static bool lua_compare(lua_State* L, int idx1, int idx2, int op)
    {
        bool i = false;
        lua_lock(L); // may call tag method
        TValue* o1 = index2value(L, idx1);
        TValue* o2 = index2value(L, idx2);
        if (isvalid(L, o1) && isvalid(L, o2))
        {
            switch (op)
            {
                case LUA_OPEQ: i = luaV_equalobj(L, o1, o2); break;
                case LUA_OPLT: i = luaV_lessthan(L, o1, o2); break;
                case LUA_OPLE: i = luaV_lessequal(L, o1, o2); break;
                default:
                    Debug.Fail("invalid option");
                    throw new InvalidOperationException();
            }
        }

        lua_unlock(L);
        return i;
    }

    public static int lua_numbertocstring(lua_State* L, int idx, Span<byte> buff)
    {
        TValue* o = index2value(L, idx);
        if (ttisnumber(o))
        {
            int len = luaO_tostringbuff(o, buff);
            buff[len++] = 0; // add final zero
            return len;
        }

        return 0;
    }

    public static long lua_stringtonumber(lua_State* L, ReadOnlySpan<byte> s)
    {
        long sz = luaO_str2num(s, s2v(L->top.p));
        if (sz != 0)
        {
            api_incr_top(L);
        }

        return sz;
    }

    public static long lua_stringtonumber(lua_State* L, string s)
    {
        long sz = luaO_str2num(Encoding.UTF8.GetBytes(s), s2v(L->top.p));
        if (sz != 0)
        {
            api_incr_top(L);
        }

        return sz;
    }

    public static double lua_tonumberx(lua_State* L, int idx, out bool isnum)
    {
        double n = 0;
        TValue* o = index2value(L, idx);
        isnum = tonumber(o, &n);
        return n;
    }

    public static long lua_tointegerx(lua_State* L, int idx, out bool isnum)
    {
        TValue* o = index2value(L, idx);
        isnum = tointeger(o, out long res);
        return res;
    }

    public static bool lua_toboolean(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return !l_isfalse(o);
    }

    public static string? lua_tonetstring(lua_State* L, int idx)
    {
        lua_lock(L);
        TValue* o = index2value(L, idx);
        if (!ttisstring(o))
        {
            if (!cvt2str(o))
            {
                // not convertible?
                lua_unlock(L);
                return null;
            }

            luaO_tostring(L, o);
            luaC_checkGC(L);
            o = index2value(L, idx); // previous call may reallocate the stack
        }

        lua_unlock(L);
        return getnetstr(tsvalue(o));
    }

    public static byte* lua_tolstring(lua_State* L, int idx, out int len)
    {
        lua_lock(L);
        TValue* o = index2value(L, idx);
        if (!ttisstring(o))
        {
            if (!cvt2str(o))
            {
                // not convertible?
                len = 0;
                lua_unlock(L);
                return null;
            }

            luaO_tostring(L, o);
            luaC_checkGC(L);
            o = index2value(L, idx); // previous call may reallocate the stack
        }

        lua_unlock(L);
        return getlstr(tsvalue(o), out len);
    }

    public static ulong lua_rawlen(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return ttypetag(o) switch
        {
            LUA_VSHRSTR => (ulong)tsvalue(o)->shrlen,
            LUA_VLNGSTR => (ulong)tsvalue(o)->u.lnglen,
            LUA_VUSERDATA => (ulong)uvalue(o)->len,
            LUA_VTABLE => GetTableRawLength(),
            _ => 0,
        };

        ulong GetTableRawLength()
        {
            lua_lock(L);
            ulong res = luaH_getn(L, hvalue(o));
            lua_unlock(L);
            return res;
        }
    }

    public static lua_CFunction lua_tocfunction(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        if (ttislcf(o))
        {
            return fvalue(o);
        }

        if (ttisCclosure(o))
        {
            return clCvalue(o)->f;
        }

        return null; // not a C function
    }

    private static void* touserdata(TValue* o)
    {
        return ttype(o) switch
        {
            LUA_TUSERDATA => getudatamem(uvalue(o)),
            LUA_TLIGHTUSERDATA => pvalue(o),
            _ => null,
        };
    }

    public static void* lua_touserdata(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return touserdata(o);
    }

    public static lua_State* lua_tothread(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return !ttisthread(o) ? null : thvalue(o);
    }

    /// <summary>
    /// Returns a pointer to the internal representation of an object.
    /// Note that ISO C does not allow the conversion of a pointer to
    /// function to a 'void*', so the conversion here goes through
    /// a 'size_t'. (As the returned pointer is only informative, this
    /// conversion should not be a problem.)
    /// </summary>
    public static void* lua_topointer(lua_State* L, int idx)
    {
        TValue* o = index2value(L, idx);
        return ttypetag(o) switch
        {
            LUA_VLCF => (void*)(nint)fvalue(o),
            LUA_VUSERDATA or LUA_VLIGHTUSERDATA => touserdata(o),
            _ => iscollectable(o) ? gcvalue(o) : null,
        };
    }

    public static void lua_pushnil(lua_State* L)
    {
        lua_lock(L);
        setnilvalue(s2v(L->top.p));
        api_incr_top(L);
        lua_unlock(L);
    }

    public static void lua_pushnumber(lua_State* L, double n)
    {
        lua_lock(L);
        setfltvalue(s2v(L->top.p), n);
        api_incr_top(L);
        lua_unlock(L);
    }

    public static void lua_pushinteger(lua_State* L, long n)
    {
        lua_lock(L);
        setivalue(s2v(L->top.p), n);
        api_incr_top(L);
        lua_unlock(L);
    }

    /// <summary>
    /// Pushes on the stack a string with given length. Avoid using 's' when
    /// 'len' == 0 (as 's' can be null in that case), due to later use of
    /// 'memcmp' and 'memcpy'.
    /// </summary>
    public static void lua_pushlstring(lua_State* L, ReadOnlySpan<byte> s)
    {
        lua_lock(L);
        TString* ts;
        if (s.IsEmpty)
        {
            ts = luaS_new(L, "");
        }
        else
        {
            fixed (byte* ptr = s)
            {
                ts = luaS_newlstr(L, ptr, s.Length);
            }
        }

        setsvalue2s(L, L->top.p, ts);
        api_incr_top(L);
        luaC_checkGC(L);
        lua_unlock(L);
    }

    /// <summary>
    /// Pushes on the stack a string with given length. Avoid using 's' when
    /// 'len' == 0 (as 's' can be null in that case), due to later use of
    /// 'memcmp' and 'memcpy'.
    /// </summary>
    public static void lua_pushlstring(lua_State* L, ReadOnlySpan<char> s)
    {
        lua_lock(L);
        TString* ts;
        if (s.IsEmpty)
        {
            ts = luaS_new(L, "");
        }
        else
        {
            byte[] data = new byte[Encoding.UTF8.GetByteCount(s)];
            Encoding.UTF8.GetBytes(s, data);
            fixed (byte* ptr = data)
            {
                ts = luaS_newlstr(L, ptr, data.Length);
            }
        }

        setsvalue2s(L, L->top.p, ts);
        api_incr_top(L);
        luaC_checkGC(L);
        lua_unlock(L);
    }

    public static void lua_pushexternalstring(lua_State* L, byte* s, int len, lua_Alloc falloc, void* ud)
    {
        lua_lock(L);
        TString* ts = luaS_newextlstr(L, s, len, falloc, ud);
        setsvalue2s(L, L->top.p, ts);
        api_incr_top(L);
        luaC_checkGC(L);
        lua_unlock(L);
    }

    [Obsolete]
    public static void lua_pushstring(lua_State* L, byte* s)
    {
        lua_lock(L);
        if (s == null)
        {
            setnilvalue(s2v(L->top.p));
        }
        else
        {
            TString* ts = luaS_new(L, s);
            setsvalue2s(L, L->top.p, ts);
        }

        api_incr_top(L);
        luaC_checkGC(L);
        lua_unlock(L);
    }

    public static void lua_pushstring(lua_State* L, ReadOnlySpan<byte> s)
    {
        fixed (byte* ptr = s)
        {
            lua_pushstring(L, ptr);
        }
    }

    public static void lua_pushstring(lua_State* L, string? s)
    {
        lua_lock(L);
        if (s == null)
        {
            setnilvalue(s2v(L->top.p));
        }
        else
        {
            TString* ts = luaS_new(L, s);
            setsvalue2s(L, L->top.p, ts);
        }

        api_incr_top(L);
        luaC_checkGC(L);
        lua_unlock(L);
    }

    public static string lua_pushfstring(lua_State* L, string fmt, params object[] args)
    {
        lua_lock(L);
        pushvfstring(L, args, fmt, out string ret);
        luaC_checkGC(L);
        lua_unlock(L);
        return ret;
    }

    public static void lua_pushcclosure(lua_State* L, lua_CFunction fn, int n)
    {
        lua_lock(L);
        if (n == 0)
        {
            setfvalue(s2v(L->top.p), fn);
            api_incr_top(L);
        }
        else
        {
            api_checkpop(L, n);
            Debug.Assert(n <= MAXUPVAL, "upvalue index too large");
            CClosure* cl = luaF_newCclosure(L, n);
            cl->f = fn;
            for (int i = 0; i < n; i++)
            {
                setobj2n(L, CClosure.GetUpValuePtr(cl, i), s2v(L->top.p - n + i));
                // does not need barrier because closure is white
                Debug.Assert(iswhite((GCObject*)cl));
            }

            L->top.p -= n;
            setclCvalue(L, s2v(L->top.p), cl);
            api_incr_top(L);
            luaC_checkGC(L);
        }

        lua_unlock(L);
    }

    public static void lua_pushboolean(lua_State* L, bool b)
    {
        lua_lock(L);
        if (b)
        {
            setbtvalue(s2v(L->top.p));
        }
        else
        {
            setbfvalue(s2v(L->top.p));
        }

        api_incr_top(L);
        lua_unlock(L);
    }

    public static void lua_pushlightuserdata(lua_State* L, void* p)
    {
        lua_lock(L);
        setpvalue(s2v(L->top.p), p);
        api_incr_top(L);
        lua_unlock(L);
    }

    public static bool lua_pushthread(lua_State* L)
    {
        lua_lock(L);
        setthvalue(L, s2v(L->top.p), L);
        api_incr_top(L);
        lua_unlock(L);
        return mainthread(G(L)) == L;
    }

    private static int auxgetstr(lua_State* L, TValue* t, string k)
    {
        TString* str = luaS_new(L, k);
        byte tag = !ttistable(t) ? LUA_VNOTABLE : luaH_getstr(hvalue(t), str, s2v(L->top.p));
        if (!tagisempty(tag))
        {
            api_incr_top(L);
        }
        else
        {
            setsvalue2s(L, L->top.p, str);
            api_incr_top(L);
            tag = luaV_finishget(L, t, s2v(L->top.p - 1), L->top.p - 1, tag);
        }

        lua_unlock(L);
        return novariant(tag);
    }

    /// <summary>
    /// The following function assumes that the registry cannot be a weak
    /// table; so, an emergency collection while using the global table
    /// cannot collect it.
    /// </summary>
    private static void getGlobalTable(lua_State* L, TValue* gt)
    {
        Table* registry = hvalue(&G(L)->l_registry);
        byte tag = luaH_getint(registry, LUA_RIDX_GLOBALS, gt);
        Debug.Assert(novariant(tag) == LUA_TTABLE, "global table must exist");
    }

    public static int lua_getglobal(lua_State* L, string name)
    {
        TValue gt;
        lua_lock(L);
        getGlobalTable(L, &gt);
        return auxgetstr(L, &gt, name);
    }

    public static int lua_gettable(lua_State* L, int idx)
    {
        lua_lock(L);
        api_checkpop(L, 1);
        TValue* t = index2value(L, idx);
        byte tag = !ttistable(t) ? LUA_VNOTABLE : luaH_get(hvalue(t), s2v(L->top.p - 1), s2v(L->top.p - 1));

        if (tagisempty(tag))
        {
            tag = luaV_finishget(L, t, s2v(L->top.p - 1), L->top.p - 1, tag);
        }

        lua_unlock(L);
        return novariant(tag);
    }

    public static int lua_getfield(lua_State* L, int idx, string k)
    {
        lua_lock(L);
        return auxgetstr(L, index2value(L, idx), k);
    }

    public static int lua_geti(lua_State* L, int idx, long n)
    {
        lua_lock(L);
        TValue* t = index2value(L, idx);
        luaV_fastgeti(t, n, s2v(L->top.p), out byte tag);
        if (tagisempty(tag)) {
            TValue key;
            setivalue(&key, n);
            tag = luaV_finishget(L, t, &key, L->top.p, tag);
        }
        api_incr_top(L);
        lua_unlock(L);
        return novariant(tag);
    }

    private static int finishrawget(lua_State* L, byte tag)
    {
        if (tagisempty(tag)) // avoid copying empty items to the stack
        {
            setnilvalue(s2v(L->top.p));
        }

        api_incr_top(L);
        lua_unlock(L);
        return novariant(tag);
    }

    private static Table* gettable(lua_State* L, int idx)
    {
        TValue* t = index2value(L, idx);
        Debug.Assert(ttistable(t), "table expected");
        return hvalue(t);
    }

    public static int lua_rawget(lua_State* L, int idx)
    {
        lua_lock(L);
        api_checkpop(L, 1);
        Table* t = gettable(L, idx);
        byte tag = luaH_get(t, s2v(L->top.p - 1), s2v(L->top.p - 1));
        L->top.p--; // pop key
        return finishrawget(L, tag);
    }

    public static int lua_rawgeti(lua_State* L, int idx, long n)
    {
        lua_lock(L);
        Table* t = gettable(L, idx);
        luaH_fastgeti(t, n, s2v(L->top.p), out byte tag);
        return finishrawget(L, tag);
    }

    public static int lua_rawgetp(lua_State* L, int idx, void* p)
    {
        lua_lock(L);
        Table* t = gettable(L, idx);
        TValue k;
        setpvalue(&k, p);
        return finishrawget(L, luaH_get(t, &k, s2v(L->top.p)));
    }

    public static void lua_createtable(lua_State* L, int narr, int nrec)
    {
        lua_lock(L);
        Table* t = luaH_new(L);
        sethvalue2s(L, L->top.p, t);
        api_incr_top(L);
        if (narr > 0 || nrec > 0)
        {
            luaH_resize(L, t, (uint)narr, (uint)nrec);
        }

        luaC_checkGC(L);
        lua_unlock(L);
    }

    public static bool lua_getmetatable(lua_State* L, int objindex)
    {
        lua_lock(L);
        TValue* obj = index2value(L, objindex);

        Table* mt = ttype(obj) switch
        {
            LUA_TTABLE => hvalue(obj)->metatable,
            LUA_TUSERDATA => uvalue(obj)->metatable,
            _ => G(L)->mt[ttype(obj)],
        };

        if (mt != null)
        {
            sethvalue2s(L, L->top.p, mt);
            api_incr_top(L);
            lua_unlock(L);
            return true;
        }

        lua_unlock(L);
        return false;
    }

    public static int lua_getiuservalue(lua_State* L, int idx, int n)
    {
        lua_lock(L);
        TValue* o = index2value(L, idx);
        Debug.Assert(ttisfulluserdata(o), "full userdata expected");

        int t;
        if (n <= 0 || n > uvalue(o)->nuvalue)
        {
            setnilvalue(s2v(L->top.p));
            t = LUA_TNONE;
        }
        else
        {
            setobj2s(L, L->top.p, &((TValue*)uvalue(o)->uv)[n - 1]);
            t = ttype(s2v(L->top.p));
        }

        api_incr_top(L);
        lua_unlock(L);
        return t;
    }

    /// <summary>
    /// t[k] = value at the top of the stack (where 'k' is a string)
    /// </summary>
    private static void auxsetstr(lua_State* L, TValue* t, string k)
    {
        TString* str = luaS_new(L, k);
        api_checkpop(L, 1);
        int hres = !ttistable(t) ? HNOTATABLE : luaH_psetstr(hvalue(t), str, s2v(L->top.p - 1));

        if (hres == HOK)
        {
            luaV_finishfastset(L, t, s2v(L->top.p - 1));
            L->top.p--; // pop value
        }
        else
        {
            setsvalue2s(L, L->top.p, str); // push 'str' (to make it a TValue)
            api_incr_top(L);
            luaV_finishset(L, t, s2v(L->top.p - 1), s2v(L->top.p - 2), hres);
            L->top.p -= 2; // pop value and key
        }

        lua_unlock(L); // lock done by caller
    }

    public static void lua_setglobal(lua_State* L, string name)
    {
        lua_lock(L); // unlock done in 'auxsetstr'
        TValue gt;
        getGlobalTable(L, &gt);
        auxsetstr(L, &gt, name);
    }

    public static void lua_settable(lua_State* L, int idx)
    {
        lua_lock(L);
        api_checkpop(L, 2);
        TValue* t = index2value(L, idx);
        int hres = !ttistable(t) ? HNOTATABLE : luaH_pset(hvalue(t), s2v(L->top.p - 2), s2v(L->top.p - 1));
        if (hres == HOK)
        {
            luaV_finishfastset(L, t, s2v(L->top.p - 1));
        }
        else
        {
            luaV_finishset(L, t, s2v(L->top.p - 2), s2v(L->top.p - 1), hres);
        }

        L->top.p -= 2; // pop index and value
        lua_unlock(L);
    }

    public static void lua_setfield(lua_State* L, int idx, string k)
    {
        lua_lock(L); // unlock done in 'auxsetstr'
        auxsetstr(L, index2value(L, idx), k);
    }

    public static void lua_seti(lua_State* L, int idx, long n)
    {
        lua_lock(L);
        api_checkpop(L, 1);
        TValue* t = index2value(L, idx);
        luaV_fastseti(t, n, s2v(L->top.p - 1), out int hres);
        if (hres == HOK)
        {
            luaV_finishfastset(L, t, s2v(L->top.p - 1));
        }
        else
        {
            TValue temp;
            setivalue(&temp, n);
            luaV_finishset(L, t, &temp, s2v(L->top.p - 1), hres);
        }

        L->top.p--; // pop value
        lua_unlock(L);
    }

    private static void aux_rawset(lua_State* L, int idx, TValue* key, int n)
    {
        lua_lock(L);
        api_checkpop(L, n);
        Table* t = gettable(L, idx);
        luaH_set(L, t, key, s2v(L->top.p - 1));
        invalidateTMcache(t);
        luaC_barrierback(L, obj2gco(t), s2v(L->top.p - 1));
        L->top.p -= n;
        lua_unlock(L);
    }

    public static void lua_rawset(lua_State* L, int idx)
    {
        aux_rawset(L, idx, s2v(L->top.p - 2), 2);
    }

    public static void lua_rawsetp(lua_State* L, int idx, void* p)
    {
        TValue k;
        setpvalue(&k, p);
        aux_rawset(L, idx, &k, 1);
    }

    public static void lua_rawseti(lua_State* L, int idx, long n)
    {
        lua_lock(L);
        api_checkpop(L, 1);
        Table* t = gettable(L, idx);
        luaH_setint(L, t, n, s2v(L->top.p - 1));
        luaC_barrierback(L, obj2gco((GCObject*)t), s2v(L->top.p - 1));
        L->top.p--;
        lua_unlock(L);
    }

    public static bool lua_setmetatable(lua_State* L, int objindex)
    {
        lua_lock(L);
        api_checkpop(L, 1);
        TValue* obj = index2value(L, objindex);

        Table* mt;
        if (ttisnil(s2v(L->top.p - 1)))
        {
            mt = null;
        }
        else
        {
            Debug.Assert(ttistable(s2v(L->top.p - 1)), "table expected");
            mt = hvalue(s2v(L->top.p - 1));
        }

        switch (ttype(obj))
        {
            case LUA_TTABLE:
                hvalue(obj)->metatable = mt;
                if (mt != null)
                {
                    luaC_objbarrier(L, gcvalue(obj), (GCObject*)mt);
                    luaC_checkfinaliser(L, gcvalue(obj), mt);
                }

                break;

            case LUA_TUSERDATA:
                uvalue(obj)->metatable = mt;
                if (mt != null)
                {
                    luaC_objbarrier(L, (GCObject*)uvalue(obj), (GCObject*)mt);
                    luaC_checkfinaliser(L, gcvalue(obj), mt);
                }

                break;

            default:
                G(L)->mt[ttype(obj)] = mt;
                break;
        }

        L->top.p--;
        lua_unlock(L);
        return true;
    }

    public static bool lua_setiuservalue(lua_State* L, int idx, int n)
    {
        lua_lock(L);
        api_checkpop(L, 1);
        TValue* o = index2value(L, idx);
        Debug.Assert(ttisfulluserdata(o), "full userdata expected");

        bool res;
        if (!((uint)n - 1u < uvalue(o)->nuvalue))
        {
            res = false; // 'n' not in [1, uvalue(o)->nuvalue]
        }
        else
        {
            setobj(L, &((TValue*)uvalue(o)->uv)[n - 1], s2v(L->top.p - 1));
            luaC_barrierback(L, gcvalue(o), s2v(L->top.p - 1));
            res = true;
        }

        L->top.p--;
        lua_unlock(L);
        return res;
    }

    private static void checkresults(lua_State* L, int na, int nr)
    {
        Debug.Assert(
            nr == LUA_MULTRET || L->ci->top.p - L->top.p >= nr - na,
            "results from function overflow current stack size");
        Debug.Assert(nr is >= LUA_MULTRET and <= MAXRESULTS, "invalid number of results");
    }

    public static void lua_callk(
        lua_State* L,
        int nargs,
        int nresults,
        nint ctx,
        lua_KFunction k)
    {
        lua_lock(L);
        Debug.Assert(k == null || !isLua(L->ci), "cannot use continuations inside hooks");
        api_checkpop(L, nargs + 1);
        Debug.Assert(L->status == LUA_OK, "cannot do calls on non-normal thread");
        checkresults(L, nargs, nresults);
        StkId func = L->top.p - (nargs + 1);
        if (k != null && yieldable(L))
        {
            // need to prepare continuation?
            L->ci->u.c.k = k; // save continuation
            L->ci->u.c.ctx = ctx; // save context
            luaD_call(L, func, nresults); // do the call
        }
        else
        {
            // no continuation or no yieldable
            luaD_callnoyield(L, func, nresults); // just do the call
        }

        adjustresults(L, nresults);
        lua_unlock(L);
    }

    /// <summary>
    /// Execute a protected call.
    /// </summary>
    private struct CallS
    {
        /// <summary>
        /// data to 'f_call'
        /// </summary>
        public StkId func;
        
        public int nresults;
    }

    private static void f_call(lua_State* L, void* ud)
    {
        CallS* c = (CallS*)ud;
        luaD_callnoyield(L, c->func, c->nresults);
    }

    public static int lua_pcallk(
        lua_State* L,
        int nargs,
        int nresults,
        int errfunc,
        nint ctx,
        lua_KFunction k)
    {
        lua_lock(L);
        Debug.Assert(k == null || !isLua(L->ci), "cannot use continuations inside hooks");
        api_checkpop(L, nargs + 1);
        Debug.Assert(L->status == LUA_OK, "cannot do calls on non-normal thread");
        checkresults(L, nargs, nresults);

        nint func;
        if (errfunc == 0)
        {
            func = 0;
        }
        else
        {
            StkId o = index2stack(L, errfunc);
            Debug.Assert(ttisfunction(s2v(o)), "error handler must be a function");
            func = savestack(L, o);
        }

        byte status;
        CallS c;
        c.func = L->top.p - (nargs + 1); // function to be called
        if (k == null || !yieldable(L))
        {
            // no continuation or no yieldable?
            c.nresults = nresults; // do a 'conventional' protected call
            status = luaD_pcall(L, f_call, &c, savestack(L, c.func), func);
        }
        else
        {
            // prepare continuation (call is already protected by 'resume')
            CallInfo* ci = L->ci;
            ci->u.c.k = k; // save continuation
            ci->u.c.ctx = ctx; // save context
            // save information for error recovery
            ci->u2.funcidx = (int)savestack(L, c.func);
            ci->u.c.old_errfunc = L->errfunc;
            L->errfunc = func;
            setoah(ci, L->allowhook); // save value of 'allowhook'
            ci->callstatus |= CIST_YPCALL; // function can do error recovery
            luaD_call(L, c.func, nresults); // do the call
            ci->callstatus &= ~CIST_YPCALL;
            L->errfunc = ci->u.c.old_errfunc;
            status = LUA_OK; // if it is here, there were no errors
        }

        adjustresults(L, nresults);
        lua_unlock(L);
        return status;
    }

    public static int lua_load(lua_State* L, lua_Reader reader, void* data, string? chunkname, string? mode)
    {
        lua_lock(L);
        chunkname ??= "?";

        Zio z;
        luaZ_init(L, &z, reader, data);
        byte status = luaD_protectedparser(L, &z, chunkname, mode);
        if (status == LUA_OK)
        {
            // no errors?
            LClosure* f = clLvalue(s2v(L->top.p - 1)); // get new function
            if (f->nupvalues >= 1)
            {
                // does it have an upvalue?
                // get global table from registry
                TValue gt;
                getGlobalTable(L, &gt);
                // set global table as 1st upvalue of 'f' (may be LUA_ENV)
                setobj(L, LClosure.GetUpValue(f, 0)->v.p, &gt);
                luaC_barrier(L, (GCObject*)LClosure.GetUpValue(f, 0), &gt);
            }
        }

        lua_unlock(L);
        return status;
    }

    /// <summary>
    /// Dump a Lua function, calling 'writer' to write its parts. Ensure
    /// the stack returns with its original size.
    /// </summary>
    public static int lua_dump(lua_State* L, lua_Writer writer, void* data, bool strip)
    {
        nint otop = savestack(L, L->top.p); // original top
        TValue* f = s2v(L->top.p - 1); // function to be dumped
        lua_lock(L);
        api_checkpop(L, 1);
        Debug.Assert(isLfunction(f), "Lua function expected");
        int status = luaU_dump(L, clLvalue(f)->p, writer, data, strip);
        L->top.p = restorestack(L, otop); // restore top
        lua_unlock(L);
        return status;
    }

    public static int lua_status(lua_State* L)
    {
        return L->status;
    }

    /// <summary>
    /// Garbage-collection function
    /// </summary>
    public static int lua_gc(lua_State* L, int what, params object[] args)
    {
        int res = 0;
        global_State* g = G(L);
        if ((g->gcstp & (GCSTPGC | GCSTPCLS)) != 0) // internal stop?
        {
            return -1; // all options are invalid when stopped
        }

        lua_lock(L);
        switch (what)
        {
            case LUA_GCSTOP:
                g->gcstp = GCSTPUSR; // stopped by the user
                break;

            case LUA_GCRESTART:
                luaE_setdebt(g, 0);
                g->gcstp = 0; // (other bits must be zero here)
                break;

            case LUA_GCCOLLECT:
                luaC_fullgc(L, false);
                break;

            case LUA_GCCOUNT:
                // GC values are expressed in Kbytes: #bytes/2^10
                res = (int)(gettotalbytes(g) >> 10);
                break;

            case LUA_GCCOUNTB:
                res = (int)(gettotalbytes(g) & 0x3ff);
                break;

            case LUA_GCSTEP:
                {
                    byte oldstp = g->gcstp;
                    long n = (long)args[0];
                    bool work = false; // true if GC did some work
                    g->gcstp = 0; // allow GC to run (other bits must be zero here)
                    if (n <= 0)
                    {
                        n = g->GCdebt; // force to run one basic step
                    }

                    luaE_setdebt(g, g->GCdebt - n);
                    if (G(L)->GCdebt <= 0)
                    {
                        luaC_step(L);
                        work = true;

#if HARDMEMTESTS
                        if (gcrunning(G(L)))
                        {
                            luaC_fullgc(L, false);
                        }
#endif
                    }

                    if (work && g->gcstate == GCSpause) // end of cycle?
                    {
                        res = 1; // signal it
                    }

                    g->gcstp = oldstp; // restore previous state
                    break;
                }

            case LUA_GCISRUNNING:
                res = gcrunning(g) ? 1 : 0;
                break;

            case LUA_GCGEN:
                res = g->gckind == KGC_INC ? LUA_GCINC : LUA_GCGEN;
                luaC_changemode(L, KGC_GENMINOR);
                break;

            case LUA_GCINC:
                res = g->gckind == KGC_INC ? LUA_GCINC : LUA_GCGEN;
                luaC_changemode(L, KGC_INC);
                break;

            case LUA_GCPARAM:
                {
                    int param = (int)args[0];
                    int value = (int)args[1];
                    Debug.Assert(param is >= 0 and < LUA_GCPN, "invalid parameter");
                    res = (int)luaO_applyparam(g->gcparams[param], 100);
                    if (value >= 0)
                    {
                        g->gcparams[param] = luaO_codeparam((uint)value);
                    }

                    break;
                }

            default:
                res = -1; // invalid option
                break;
        }

        lua_unlock(L);
        return res;
    }

    [DoesNotReturn]
    public static int lua_error(lua_State* L)
    {
        lua_lock(L);
        TValue* errobj = s2v(L->top.p - 1);
        api_checkpop(L, 1);
        // error object is the memory error message?
        if (ttisshrstring(errobj) && eqshrstr(tsvalue(errobj), G(L)->memerrmsg))
        {
            luaM_error(L); // raise a memory error
        }
        else
        {
            luaG_errormsg(L); // raise a regular error
        }

        // code unreachable; will unlock when control actually leaves the kernel
        return 0; // to avoid warnings
    }

    public static bool lua_next(lua_State* L, int idx)
    {
        lua_lock(L);
        api_checkpop(L, 1);
        Table* t = gettable(L, idx);
        bool more = luaH_next(L, t, L->top.p - 1);
        if (more)
        {
            api_incr_top(L);
        }
        else // no more elements
        {
            L->top.p--; // pop key
        }

        lua_unlock(L);
        return more;
    }

    public static void lua_toclose(lua_State* L, int idx)
    {
        lua_lock(L);
        StkId o = index2stack(L, idx);
        Debug.Assert(L->tbclist.p < o, "given index below or equal a marked one");
        luaF_newtbcupval(L, o); // create new to-be-closed upvalue
        L->ci->callstatus |= CIST_TBC; // mark that function has TBC slots
        lua_unlock(L);
    }

    public static void lua_concat(lua_State* L, int n)
    {
        lua_lock(L);
        api_checknelems(L, n);
        if (n > 0)
        {
            luaV_concat(L, n);
            luaC_checkGC(L);
        }
        else
        {
            // nothing to concatenate
            setsvalue2s(L, L->top.p, luaS_new(L, "")); // push empty string
            api_incr_top(L);
        }

        lua_unlock(L);
    }

    public static void lua_len(lua_State* L, int idx)
    {
        lua_lock(L);
        TValue* t = index2value(L, idx);
        luaV_objlen(L, L->top.p, t);
        api_incr_top(L);
        lua_unlock(L);
    }

    public static lua_Alloc lua_getallocf(lua_State* L, out void* ud)
    {
        lua_Alloc f;
        lua_lock(L);
        ud = G(L)->ud;
        f = G(L)->frealloc;
        lua_unlock(L);
        return f;
    }

    public static void lua_setallocf(lua_State* L, lua_Alloc f, void* ud)
    {
        lua_lock(L);
        G(L)->ud = ud;
        G(L)->frealloc = f;
        lua_unlock(L);
    }

    public static void lua_setwarnf(lua_State* L, lua_WarnFunction f, void* ud)
    {
        lua_lock(L);
        G(L)->ud_warn = ud;
        G(L)->warnf = f;
        lua_unlock(L);
    }

    public static void lua_warning(lua_State* L, ReadOnlySpan<char> msg, bool tocont)
    {
        lua_lock(L);
        luaE_warning(L, msg, tocont);
        lua_unlock(L);
    }

    public static void* lua_newuserdatauv(lua_State* L, long size, int nuvalue)
    {
        lua_lock(L);
        Debug.Assert(nuvalue is >= 0 and < ushort.MaxValue, "invalid value");
        Udata* u = luaS_newudata(L, size, (ushort)nuvalue);
        setuvalue(L, s2v(L->top.p), u);
        api_incr_top(L);
        luaC_checkGC(L);
        lua_unlock(L);
        return getudatamem(u);
    }

    private static string? aux_upvalue(
        TValue* fi,
        int n,
        TValue** val,
        GCObject** owner)
    {
        switch (ttypetag(fi))
        {
            case LUA_VCCL:
                {
                    // C closure
                    CClosure* f = clCvalue(fi);
                    if (!((uint)n - 1u < f->nupvalues))
                    {
                        return null; // 'n' not in [1, f->nupvalues]
                    }

                    *val = CClosure.GetUpValuePtr(f, n - 1);
                    if (owner != null)
                    {
                        *owner = obj2gco(f);
                    }

                    return "";
                }

            case LUA_VLCL:
                {
                    // Lua closure
                    LClosure* f = clLvalue(fi);
                    Proto* p = f->p;
                    if (!((uint)n - 1u < (uint)p->sizeupvalues))
                    {
                        return null; // 'n' not in [1, p->sizeupvalues]
                    }

                    *val = LClosure.GetUpValue(f, n - 1)->v.p;
                    if (owner != null)
                    {
                        *owner = obj2gco(LClosure.GetUpValue(f, n - 1));
                    }

                    TString* name = p->upvalues[n - 1].name;
                    return name == null ? "(no name)" : getnetstr(name);
                }

            default:
                return null; // not a closure
        }
    }

    public static string? lua_getupvalue(lua_State* L, int funcindex, int n)
    {
        lua_lock(L);
        TValue* val = null; // to avoid warnings
        string? name = aux_upvalue(index2value(L, funcindex), n, &val, null);
        if (name != null)
        {
            setobj2s(L, L->top.p, val);
            api_incr_top(L);
        }

        lua_unlock(L);
        return name;
    }

    public static string? lua_setupvalue(lua_State* L, int funcindex, int n)
    {
        lua_lock(L);
        TValue* fi = index2value(L, funcindex);
        api_checknelems(L, 1);
        TValue* val = null; // to avoid warnings
        GCObject* owner = null; // to avoid warnings
        string? name = aux_upvalue(fi, n, &val, &owner);
        if (name != null)
        {
            L->top.p--;
            setobj(L, val, s2v(L->top.p));
            luaC_barrier(L, owner, val);
        }

        lua_unlock(L);
        return name;
    }
    
    private static readonly UpVal** nullup = (UpVal**)NativeMemory.AllocZeroed((nuint)nint.Size);

    private static UpVal** getupvalref(lua_State* L, int fidx, int n, LClosure** pf)
    {
        TValue* fi = index2value(L, fidx);
        Debug.Assert(ttisLclosure(fi), "Lua function expected");
        LClosure* f = clLvalue(fi);
        if (pf != null)
        {
            *pf = f;
        }
        
        if (1 <= n && n <= f->p->sizeupvalues)
        {
            return LClosure.GetUpValuePtr(f, n - 1); // get its upvalue pointer
        }

        return nullup;
    }

    public static void* lua_upvalueid(lua_State* L, int fidx, int n)
    {
        TValue* fi = index2value(L, fidx);
        switch (ttypetag(fi))
        {
            case LUA_VLCL:
                // lua closure
                return *getupvalref(L, fidx, n, null);

            case LUA_VCCL:
                {
                    // C closure
                    CClosure* f = clCvalue(fi);
                    if (1 <= n && n <= f->nupvalues)
                    {
                        return CClosure.GetUpValuePtr(f, n - 1);
                    }

                    return null;
                }

            case LUA_VLCF:
                return null; // light C functions have no upvalues
            
            default:
                Debug.Fail("function expected");
                return null;
        }
    }

    public static void lua_upvaluejoin(lua_State* L, int fidx1, int n1, int fidx2, int n2)
    {
        LClosure* f1;
        UpVal** up1 = getupvalref(L, fidx1, n1, &f1);
        UpVal** up2 = getupvalref(L, fidx2, n2, null);
        Debug.Assert(*up1 != null && *up2 != null, "invalid upvalue index");
        *up1 = *up2;
        luaC_objbarrier(L, (GCObject*)f1, (GCObject*)*up1);
    }
}
