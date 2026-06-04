namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /*
     ** $Id: lvm.h $
     ** Lua virtual machine
     ** See Copyright Notice in lua.h
     */

#if !LUA_NOCVTN2S
    private static bool cvt2str(TValue* o)
    {
        return ttisnumber(o);
    }
#else
    private static bool cvt2str(TValue* o)
    {
        return false; /* no conversion from numbers to strings */
    }
#endif


#if !LUA_NOCVTS2N
    private static bool cvt2num(TValue* o)
    {
        return ttisstring(o);
    }
#else
    private static bool cvt2num(TValue* o)
    {
        return false; /* no conversion from strings to numbers */
    }
#endif

    /*
     ** You can define LUA_FLOORN2I if you want to convert floats to integers
     ** by flooring them (instead of raising an error if they are not
     ** integral values)
     */
    private const F2Imod LUA_FLOORN2I = F2Imod.F2Ieq;

    /*
     ** Rounding modes for float->integer coercion
     */
    private enum F2Imod
    {
        F2Ieq, /* no rounding; accepts only integral values */
        F2Ifloor, /* takes the floor of the number */
        F2Iceil, /* takes the ceiling of the number */
    }

    /* convert an object to a float (including string coercion) */
    private static bool tonumber(TValue* o, double* n)
    {
        if (ttisfloat(o))
        {
            *n = fltvalue(o);
            return true;
        }

        return luaV_tonumber_(o, n);
    }

    /* convert an object to a float (without string coercion) */
    private static bool tonumberns(TValue* o, out double n)
    {
        if (ttisfloat(o))
        {
            n = fltvalue(o);
            return true;
        }

        if (ttisinteger(o))
        {
            n = ivalue(o);
            return true;
        }

        n = 0;
        return false;
    }

    /* convert an object to an integer (including string coercion) */
    private static bool tointeger(TValue* o, long* i)
    {
        if (ttisinteger(o))
        {
            *i = ivalue(o);
            return true;
        }

        return luaV_tointeger(o, i, LUA_FLOORN2I);
    }

    /* convert an object to an integer (without string coercion) */
    private static bool tointegerns(TValue* o, out long i)
    {
        if (ttisinteger(o))
        {
            i = ivalue(o);
            return true;
        }

        return luaV_tointegerns(o, out i, LUA_FLOORN2I);
    }

    // #define intop(op,v1,v2) l_castU2S(l_castS2U(v1) op l_castS2U(v2)) TODO

    private static bool luaV_rawequalobj(TValue* t1, TValue* t2)
    {
        return luaV_equalobj(null, t1, t2);
    }

    private delegate byte FastGetDelegate<T1, T2>(Table* a, T1 b, T2 c);

    /*
     ** fast track for 'gettable' TODO
     */
    // private static void luaV_fastget<T1, T2>(TValue* t, T1 k, T2 res, FastGetDelegate<T1, T2> f, out byte tag)
    // {
    //     tag = !ttistable(t) ? LUA_VNOTABLE : f(hvalue(t), k, res);
    // }

    /*
     ** Special case of 'luaV_fastget' for integers, inlining the fast case
     ** of 'luaH_getint'.
     */
    private static void luaV_fastgeti(TValue* t, long k, TValue* res, out byte tag)
    {
        if (!ttistable(t))
        {
            tag = LUA_VNOTABLE;
        }
        else
        {
            luaH_fastgeti(hvalue(t), k, res, out tag);
        }
    }

    // private static void luaV_fastset(TValue* t, void k, void val, void hres, void f) TODO
    // {
    //     hres = !ttistable(t) ? HNOTATABLE : f(hvalue(t), k, val);
    // }

    private static void luaV_fastseti(TValue* t, long k, TValue* val, out int hres)
    {
        if (!ttistable(t))
        {
            hres = HNOTATABLE;
        }
        else
        {
            luaH_fastseti(hvalue(t), k, val, out hres);
        }
    }

    /*
     ** Finish a fast set operation (when fast set succeeds).
     */
    private static void luaV_finishfastset(lua_State* L, TValue* t, TValue* v)
    {
        luaC_barrierback(L, gcvalue(t), v);
    }

    /*
     ** Shift right is the same as shift left with a negative 'y'
     */
    private static long luaV_shiftr(long x, long y)
    {
        return luaV_shiftl(x, (long)(0u - (ulong)y));
    }

    private static partial bool luaV_equalobj(lua_State* L, TValue* t1, TValue* t2);

    private static partial bool luaV_lessthan(lua_State* L, TValue* l, TValue* r);

    private static partial bool luaV_lessequal(lua_State* L, TValue* l, TValue* r);

    private static partial bool luaV_tonumber_(TValue* obj, double* n);

    private static partial bool luaV_tointeger(TValue* obj, long* p, F2Imod mode);

    private static partial bool luaV_tointegerns(TValue* obj, out long p, F2Imod mode);

    private static partial bool luaV_flttointeger(double n, out long p, F2Imod mode);

    private static partial byte luaV_finishget(lua_State* L, TValue* t, TValue* key, StkId val, byte tag);

    private static partial void luaV_finishset(lua_State* L, TValue* t, TValue* key, TValue* val, int aux);

    private static partial void luaV_finishOp(lua_State* L);

    private static partial void luaV_execute(lua_State* L, CallInfo* ci);

    private static partial void luaV_concat(lua_State* L, int total);

    private static partial long luaV_idiv(lua_State* L, long x, long y);

    private static partial long luaV_mod(lua_State* L, long x, long y);

    private static partial double luaV_modf(lua_State* L, double x, double y);

    private static partial long luaV_shiftl(long x, long y);

    private static partial void luaV_objlen(lua_State* L, StkId ra, TValue* rb);
}
