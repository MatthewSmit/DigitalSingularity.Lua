namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
//     /*
// ** $Id: lvm.h $
// ** Lua virtual machine
// ** See Copyright Notice in lua.h
// */
//
// #ifndef lvm_h
// #define lvm_h
//
//
// #include "ldo.h"
// #include "lobject.h"
// #include "ltm.h"
//
//
// #if !defined(LUA_NOCVTN2S)
// #define cvt2str(o)	ttisnumber(o)
// #else
// #define cvt2str(o)	0	/* no conversion from numbers to strings */
// #endif
//
//
// #if !defined(LUA_NOCVTS2N)
// #define cvt2num(o)	ttisstring(o)
// #else
// #define cvt2num(o)	0	/* no conversion from strings to numbers */
// #endif
//
//
// /*
// ** You can define LUA_FLOORN2I if you want to convert floats to integers
// ** by flooring them (instead of raising an error if they are not
// ** integral values)
// */
// #if !defined(LUA_FLOORN2I)
// #define LUA_FLOORN2I		F2Ieq
// #endif

// /*
// ** Rounding modes for float->integer coercion
//  */
// typedef enum {
//   F2Ieq,     /* no rounding; accepts only integral values */
//   F2Ifloor,  /* takes the floor of the number */
//   F2Iceil    /* takes the ceiling of the number */
// } F2Imod;

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

    // /* convert an object to a float (without string coercion) */
// #define tonumberns(o,n) \
// 	(ttisfloat(o) ? ((n) = fltvalue(o), 1) : \
// 	(ttisinteger(o) ? ((n) = cast_num(ivalue(o)), 1) : 0))

    /* convert an object to an integer (including string coercion) */
    private static bool tointeger(TValue* o, long* i)
    {
        if (ttisinteger(o))
        {
            *i = ivalue(o);
            return true;
        }

        // return luaV_tointeger(o, i, LUA_FLOORN2I);
        throw new NotImplementedException();
    }

    // /* convert an object to an integer (without string coercion) */
// #define tointegerns(o,i) \
//   (l_likely(ttisinteger(o)) ? (*(i) = ivalue(o), 1) \
//                           : luaV_tointegerns(o,i,LUA_FLOORN2I))
//
//
// #define intop(op,v1,v2) l_castU2S(l_castS2U(v1) op l_castS2U(v2))

    private static bool luaV_rawequalobj(TValue* t1, TValue* t2)
    {
        return luaV_equalobj(null, t1, t2);
    }

    private delegate byte FastGetDelegate<T1, T2>(Table* a, T1 b, T2 c);

    /*
    ** fast track for 'gettable'
    */
    private static void luaV_fastget<T1, T2>(TValue* t, T1 k, T2 res, FastGetDelegate<T1, T2> f, out byte tag)
    {
        tag = !ttistable(t) ? LUA_VNOTABLE : f(hvalue(t), k, res);
    }
      
// /*
// ** Special case of 'luaV_fastget' for integers, inlining the fast case
// ** of 'luaH_getint'.
// */
// #define luaV_fastgeti(t,k,res,tag) \
//   if (!ttistable(t)) tag = LUA_VNOTABLE; \
//   else { luaH_fastgeti(hvalue(t), k, res, tag); }
//
//
// #define luaV_fastset(t,k,val,hres,f) \
//   (hres = (!ttistable(t) ? HNOTATABLE : f(hvalue(t), k, val)))
//
// #define luaV_fastseti(t,k,val,hres) \
//   if (!ttistable(t)) hres = HNOTATABLE; \
//   else { luaH_fastseti(hvalue(t), k, val, hres); }

    /*
    ** Finish a fast set operation (when fast set succeeds).
    */
    private static void luaV_finishfastset(lua_State* L, TValue* t, TValue* v)
    {
        luaC_barrierback(L, gcvalue(t), v);
    }

    // /*
// ** Shift right is the same as shift left with a negative 'y'
// */
// #define luaV_shiftr(x,y)	luaV_shiftl(x,intop(-, 0, y))

    private static partial bool luaV_equalobj(lua_State* L, TValue* t1, TValue* t2);
    
// LUAI_FUNC int luaV_lessthan (lua_State *L, const TValue *l, const TValue *r);
// LUAI_FUNC int luaV_lessequal (lua_State *L, const TValue *l, const TValue *r);

    private static partial bool luaV_tonumber_(TValue* obj, double* n);
    
// LUAI_FUNC int luaV_tointeger (const TValue *obj, lua_Integer *p, F2Imod mode);
// LUAI_FUNC int luaV_tointegerns (const TValue *obj, lua_Integer *p,
//                                 F2Imod mode);
// LUAI_FUNC int luaV_flttointeger (lua_Number n, lua_Integer *p, F2Imod mode);

    private static partial byte luaV_finishget(lua_State* L, TValue* t, TValue* key, StkId val, byte tag);

    private static partial void luaV_finishset(lua_State* L, TValue* t, TValue* key, TValue* val, int aux);
    
// LUAI_FUNC void luaV_finishOp (lua_State *L);

    private static partial void luaV_execute(lua_State* L, CallInfo* ci);
    
// LUAI_FUNC void luaV_concat (lua_State *L, int total);
// LUAI_FUNC lua_Integer luaV_idiv (lua_State *L, lua_Integer x, lua_Integer y);
// LUAI_FUNC lua_Integer luaV_mod (lua_State *L, lua_Integer x, lua_Integer y);
// LUAI_FUNC lua_Number luaV_modf (lua_State *L, lua_Number x, lua_Number y);
// LUAI_FUNC lua_Integer luaV_shiftl (lua_Integer x, lua_Integer y);
// LUAI_FUNC void luaV_objlen (lua_State *L, StkId ra, const TValue *rb);
}
