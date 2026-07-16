namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    // $Id: lvm.c $
    // Lua virtual machine
    // See Copyright Notice in lua.h

#if !LUA_NOCVTN2S
    private static bool cvt2str(TValue* o)
    {
        return ttisnumber(o);
    }
#else
    private static bool cvt2str(TValue* o)
    {
        return false; // no conversion from numbers to strings
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
        return false; // no conversion from strings to numbers
    }
#endif

    /// <summary>
    /// You can define LUA_FLOORN2I if you want to convert floats to integers
    /// by flooring them (instead of raising an error if they are not
    /// integral values)
    /// </summary>
    private const F2Imod LUA_FLOORN2I = F2Imod.F2Ieq;

    /// <summary>
    /// Rounding modes for float-&gt;integer coercion
    /// </summary>
    internal enum F2Imod
    {
        F2Ieq, // no rounding; accepts only integral values
        F2Ifloor, // takes the floor of the number
        F2Iceil, // takes the ceiling of the number
    }

    /// <summary>
    /// convert an object to a float (including string coercion)
    /// </summary>
    private static bool tonumber(TValue* o, double* n)
    {
        if (ttisfloat(o))
        {
            *n = fltvalue(o);
            return true;
        }

        return luaV_tonumber_(o, n);
    }

    /// <summary>
    /// convert an object to a float (without string coercion)
    /// </summary>
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

    /// <summary>
    /// convert an object to an integer (including string coercion)
    /// </summary>
    private static bool tointeger(TValue* o, out long i)
    {
        if (ttisinteger(o))
        {
            i = ivalue(o);
            return true;
        }

        return luaV_tointeger(o, out i, LUA_FLOORN2I);
    }

    /// <summary>
    /// convert an object to an integer (without string coercion)
    /// </summary>
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

    /// <summary>
    /// Special case of 'luaV_fastget' for integers, inlining the fast case
    /// of 'luaH_getint'.
    /// </summary>
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

    /// <summary>
    /// Finish a fast set operation (when fast set succeeds).
    /// </summary>
    private static void luaV_finishfastset(lua_State* L, TValue* t, TValue* v)
    {
        luaC_barrierback(L, gcvalue(t), v);
    }

    /// <summary>
    /// Shift right is the same as shift left with a negative 'y'
    /// </summary>
    private static long luaV_shiftr(long x, long y)
    {
        return luaV_shiftl(x, (long)(0u - (ulong)y));
    }
    
    /// <summary>
    /// limit for table tag-method chains (to avoid infinite loops)
    /// </summary>
    private const int MAXTAGLOOP = 2000;

    // 'l_intfitsf' checks whether a given integer is in the range that
    // can be converted to a float without rounding. Used in comparisons.

    /// <summary>
    /// number of bits in the mantissa of a float
    /// </summary>
    private const int NBM = 53;

    /// <summary>
    /// limit for integers that fit in a float
    /// </summary>
    private const ulong MAXINTFITSF = (ulong)1 << NBM;

    /// <summary>
    /// check whether 'i' is in the interval [-MAXINTFITSF, MAXINTFITSF]
    /// </summary>
    private static bool l_intfitsf(long i)
    {
        return MAXINTFITSF + (ulong)i <= 2 * MAXINTFITSF;
    }

    /// <summary>
    /// Try to convert a value from string to a number value.
    /// If the value is not a string or is a string not representing
    /// a valid numeral (or if coercions from strings to numbers
    /// are disabled via macro 'cvt2num'), do not modify 'result'
    /// and return 0.
    /// </summary>
    private static bool l_strton(TValue* obj, TValue* result)
    {
        Debug.Assert(obj != result);
        if (!cvt2num(obj)) // is object not a string?
        {
            return false;
        }

        TString* st = tsvalue(obj);
        ReadOnlySpan<byte> s = getlstr(st);
        return luaO_str2num(s, result) == s.Length + 1;
    }

    /// <summary>
    /// Try to convert a value to a float. The float case is already handled
    /// by the macro 'tonumber'.
    /// </summary>
    internal static bool luaV_tonumber_(TValue* obj, double* n)
    {
        if (ttisinteger(obj))
        {
            *n = ivalue(obj);
            return true;
        }

        TValue v;
        if (l_strton(obj, &v))
        {
            // string coercible to number?
            *n = nvalue(&v); // convert result of 'luaO_str2num' to a float
            return true;
        }

        return false; // conversion failed
    }

    /// <summary>
    /// try to convert a float to an integer, rounding according to 'mode'.
    /// </summary>
    internal static bool luaV_flttointeger(double n, out long p, F2Imod mode)
    {
        double f = Math.Floor(n);
        if (n != f)
        {
            // not an integral value?
            if (mode == F2Imod.F2Ieq)
            {
                p = 0;
                return false; // fails if mode demands integral value
            }

            if (mode == F2Imod.F2Iceil) // needs ceiling?
            {
                f += 1; // convert floor to ceiling (remember: n != f)
            }
        }

        return lua_numbertointeger(f, out p);
    }

    /// <summary>
    /// try to convert a value to an integer, rounding according to 'mode',
    /// without string coercion.
    /// ("Fast track" handled by macro 'tointegerns'.)
    /// </summary>
    internal static bool luaV_tointegerns(TValue* obj, out long p, F2Imod mode)
    {
        if (ttisfloat(obj))
        {
            return luaV_flttointeger(fltvalue(obj), out p, mode);
        }

        if (ttisinteger(obj))
        {
            p = ivalue(obj);
            return true;
        }

        p = 0;
        return false;
    }

    /// <summary>
    /// try to convert a value to an integer.
    /// </summary>
    internal static bool luaV_tointeger(TValue* obj, out long p, F2Imod mode)
    {
        TValue v;
        if (l_strton(obj, &v)) // does 'obj' point to a numerical string?
        {
            obj = &v; // change it to point to its corresponding number
        }

        return luaV_tointegerns(obj, out p, mode);
    }

    /// <summary>
    /// Try to convert a 'for' limit to an integer, preserving the semantics
    /// of the loop. Return true if the loop must not run; otherwise, '*p'
    /// gets the integer limit.
    /// (The following explanation assumes a positive step; it is valid for
    /// negative steps mutatis mutandis.)
    /// If the limit is an integer or can be converted to an integer,
    /// rounding down, that is the limit.
    /// Otherwise, check whether the limit can be converted to a float. If
    /// the float is too large, clip it to LUA_MAXINTEGER.  If the float
    /// is too negative, the loop should not run, because any initial
    /// integer value is greater than such limit; so, the function returns
    /// true to signal that. (For this latter case, no integer limit would be
    /// correct; even a limit of LUA_MININTEGER would run the loop once for
    /// an initial value equal to LUA_MININTEGER.)
    /// </summary>
    private static bool forlimit(
        lua_State* L,
        long init,
        TValue* lim,
        out long p,
        long step)
    {
        if (!luaV_tointeger(lim, out p, step < 0 ? F2Imod.F2Iceil : F2Imod.F2Ifloor))
        {
            // not coercible to in integer
            double flim; // try to convert to float
            if (!tonumber(lim, &flim)) // cannot convert to float?
            {
                luaG_forerror(L, lim, "limit");
            }

            // else 'flim' is a float out of integer bounds
            if (flim > 0)
            {
                // if it is positive, it is too large
                if (step < 0)
                {
                    return true; // initial value must be less than it
                }

                p = long.MaxValue; // truncate
            }
            else
            {
                // it is less than min integer
                if (step > 0)
                {
                    return true; // initial value must be greater than it
                }

                p = long.MinValue; // truncate
            }
        }

        return step > 0 ? init > p : init < p; // not to run?
    }

    /// <summary>
    /// Prepare a numerical for loop (opcode OP_FORPREP).
    /// Before execution, stack is as follows:
    ///   ra     : initial value
    ///   ra + 1 : limit
    ///   ra + 2 : step
    /// Return true to skip the loop. Otherwise,
    /// after preparation, stack will be as follows:
    ///   ra     : loop counter (integer loops) or limit (float loops)
    ///   ra + 1 : step
    ///   ra + 2 : control variable
    /// </summary>
    private static bool forprep(lua_State* L, StkId ra)
    {
        TValue* pinit = s2v(ra);
        TValue* plimit = s2v(ra + 1);
        TValue* pstep = s2v(ra + 2);
        if (ttisinteger(pinit) && ttisinteger(pstep))
        {
            // integer loop?
            long init = ivalue(pinit);
            long step = ivalue(pstep);
            if (step == 0)
            {
                luaG_runerror(L, "'for' step is zero");
            }

            if (forlimit(L, init, plimit, out long limit, step))
            {
                return true; // skip the loop
            }

            // prepare loop counter
            ulong count;
            if (step > 0)
            {
                // ascending loop?
                count = (ulong)limit - (ulong)init;
                if (step != 1) // avoid division in the too common case
                {
                    count /= (ulong)step;
                }
            }
            else
            {
                // step < 0; descending loop
                count = (ulong)init - (ulong)limit;
                // 'step+1' avoids negating 'mininteger'
                count /= (ulong)-(step + 1) + 1u;
            }

            // use 'chgivalue' for places that for sure had integers
            chgivalue(s2v(ra), (long)count); // change init to count
            setivalue(s2v(ra + 1), step); // change limit to step
            chgivalue(s2v(ra + 2), init); // change step to init
        }
        else
        {
            // try making all values floats
            double init;
            double limit;
            double step;
            if (!tonumber(plimit, &limit))
            {
                luaG_forerror(L, plimit, "limit");
            }

            if (!tonumber(pstep, &step))
            {
                luaG_forerror(L, pstep, "step");
            }

            if (!tonumber(pinit, &init))
            {
                luaG_forerror(L, pinit, "initial value");
            }

            if (step == 0)
            {
                luaG_runerror(L, "'for' step is zero");
            }

            if (step > 0
                    ? limit < init
                    : init < limit)
            {
                return true; // skip the loop
            }

            // make sure all values are floats
            setfltvalue(s2v(ra), limit);
            setfltvalue(s2v(ra + 1), step);
            setfltvalue(s2v(ra + 2), init); // control variable
        }

        return false;
    }

    /// <summary>
    /// Execute a step of a float numerical for loop, returning
    /// true iff the loop must continue. (The integer case is
    /// written online with opcode OP_FORLOOP, for performance.)
    /// </summary>
    private static bool floatforloop(StkId ra)
    {
        double step = fltvalue(s2v(ra + 1));
        double limit = fltvalue(s2v(ra));
        double idx = fltvalue(s2v(ra + 2)); // control variable
        idx += step; // increment index
        if (step > 0
                ? idx <= limit
                : limit <= idx)
        {
            chgfltvalue(s2v(ra + 2), idx); // update control variable
            return true; // jump back
        }

        return false; // finish the loop
    }

    /// <summary>
    /// Finish the table access 'val = t[key]' and return the tag of the result.
    /// </summary>
    internal static byte luaV_finishget(lua_State* L, TValue* t, TValue* key, StkId val, byte tag)
    {
        for (int loop = 0; loop < MAXTAGLOOP; loop++)
        {
            TValue* tm; // metamethod
            if (tag == LUA_VNOTABLE)
            {
                // 't' is not a table?
                Debug.Assert(!ttistable(t));
                tm = luaT_gettmbyobj(L, t, TMS.INDEX);
                if (notm(tm))
                {
                    luaG_typeerror(L, t, "index"); // no metamethod
                }

                // else will try the metamethod
            }
            else
            {
                // 't' is a table
                tm = fasttm(L, hvalue(t)->metatable, TMS.INDEX); // table's metamethod
                if (tm == null)
                {
                    // no metamethod?
                    setnilvalue(s2v(val)); // result is nil
                    return LUA_VNIL;
                }
                // else will try the metamethod
            }

            if (ttisfunction(tm))
            {
                // is metamethod a function?
                tag = luaT_callTMres(L, tm, t, key, val); // call it
                return tag; // return tag of the result
            }

            t = tm; // else try to access 'tm[key]'
            tag = !ttistable(t) ? LUA_VNOTABLE : luaH_get(hvalue(t), key, s2v(val));

            if (!tagisempty(tag))
            {
                return tag; // done
            }
            // else repeat (tail call 'luaV_finishget')
        }

        luaG_runerror(L, "'__index' chain too long; possible loop");
        return 0; // to avoid warnings
    }

    /// <summary>
    ///
    /// Finish a table assignment 't[key] = val'.
    /// About anchoring the table before the call to 'luaH_finishset':
    /// This call may trigger an emergency collection. When loop&gt;0,
    /// the table being accessed is a field in some metatable. If this
    /// metatable is weak and the table is not anchored, this collection
    /// could collect that table while it is being updated.
    ///
    /// </summary>
    internal static void luaV_finishset(lua_State* L, TValue* t, TValue* key, TValue* val, int hres)
    {
        for (int loop = 0; loop < MAXTAGLOOP; loop++)
        {
            TValue* tm; // '__newindex' metamethod
            if (hres != HNOTATABLE)
            {
                // is 't' a table?
                Table* h = hvalue(t); // save 't' table
                tm = fasttm(L, h->metatable, TMS.NEWINDEX); // get metamethod
                if (tm == null)
                {
                    // no metamethod?
                    sethvalue2s(L, L->top.p, h); // anchor 't'
                    L->top.p++; // assume EXTRA_STACK
                    luaH_finishset(L, h, key, val, hres); // set new value
                    L->top.p--;
                    invalidateTMcache(h);
                    luaC_barrierback(L, obj2gco(h), val);
                    return;
                }
                // else will try the metamethod
            }
            else
            {
                // not a table; check metamethod
                tm = luaT_gettmbyobj(L, t, TMS.NEWINDEX);
                if (notm(tm))
                {
                    luaG_typeerror(L, t, "index");
                }
            }

            // try the metamethod
            if (ttisfunction(tm))
            {
                luaT_callTM(L, tm, t, key, val);
                return;
            }

            t = tm; // else repeat assignment over 'tm'
            hres = !ttistable(t) ? HNOTATABLE : luaH_pset(hvalue(t), key, val);
            
            if (hres == HOK)
            {
                luaV_finishfastset(L, t, val);
                return; // done
            }
            // else 'return luaV_finishset(L, t, key, val, slot)' (loop)
        }

        luaG_runerror(L, "'__newindex' chain too long; possible loop");
    }

//
// Function to be used for 0-terminated string order comparison
//
// #if !defined(l_strcoll)
// #define l_strcoll	strcoll
// #endif

    /// <summary>
    /// Compare two strings 'ts1' x 'ts2', returning an integer less-equal-
    /// -greater than zero if 'ts1' is less-equal-greater than 'ts2'.
    /// The code is a little tricky because it allows '\0' in the strings
    /// and it uses 'strcoll' (to respect locales) for each segment
    /// of the strings. Note that segments can compare equal but still
    /// have different lengths.
    /// </summary>
    private static int l_strcmp(TString* ts1, TString* ts2)
    {
        ReadOnlySpan<byte> s1 = getlstr(ts1);
        ReadOnlySpan<byte> s2 = getlstr(ts2);
        return s1.SequenceCompareTo(s2);

// byte* s1 = getlstr(ts1, out long rl1);
// byte* s2 = getlstr(ts2, out long rl2);
// while (true)
// {
// for each segment
// //     int temp = l_strcoll(s1, s2);
// //     if (temp != 0) // not equal?
// //       return temp; // done
// //     else { // strings are equal up to a '\0'
// //       size_t zl1 = strlen(s1); // index of first '\0' in 's1'
// //       size_t zl2 = strlen(s2); // index of first '\0' in 's2'
// //       if (zl2 == rl2) // 's2' is finished?
// //         return (zl1 == rl1) ? 0 : 1; // check 's1'
// //       else if (zl1 == rl1) // 's1' is finished?
// //         return -1; // 's1' is less than 's2' ('s2' is not finished)
// // // both strings longer than 'zl'; go on comparing after the '\0'
// //       zl1++; zl2++;
// //       s1 += zl1; rl1 -= zl1; s2 += zl2; rl2 -= zl2;
// //     }
// throw new NotImplementedException();
// }
    }

    /// <summary>
    /// Check whether integer 'i' is less than float 'f'. If 'i' has an
    /// exact representation as a float ('l_intfitsf'), compare numbers as
    /// floats. Otherwise, use the equivalence 'i &lt; f &lt;=&gt; i &lt; ceil(f)'.
    /// If 'ceil(f)' is out of integer range, either 'f' is greater than
    /// all integers or less than all integers.
    /// (The test with 'l_intfitsf' is only for performance; the else
    /// case is correct for all values, but it is slow due to the conversion
    /// from float to int.)
    /// When 'f' is NaN, comparisons must result in false.
    /// </summary>
    private static bool LTintfloat(long i, double f)
    {
        if (l_intfitsf(i))
        {
            return i < f; // compare them as floats
        }

        // i < f <=> i < ceil(f)
        if (luaV_flttointeger(f, out long fi, F2Imod.F2Iceil)) // fi = ceil(f)
        {
            return i < fi; // compare them as integers
        }

        // 'f' is either greater or less than all integers
        return f > 0; // greater?
    }

    /// <summary>
    /// Check whether integer 'i' is less than or equal to float 'f'.
    /// See comments on previous function.
    /// </summary>
    private static bool LEintfloat(long i, double f)
    {
        if (l_intfitsf(i))
        {
            return i <= f; // compare them as floats
        }

        // i <= f <=> i <= floor(f)
        if (luaV_flttointeger(f, out long fi, F2Imod.F2Ifloor)) // fi = floor(f)
        {
            return i <= fi; // compare them as integers
        }

        // 'f' is either greater or less than all integers
        return f > 0; // greater?
    }

    /// <summary>
    /// Check whether float 'f' is less than integer 'i'.
    /// See comments on previous function.
    /// </summary>
    private static bool LTfloatint(double f, long i)
    {
        if (l_intfitsf(i))
        {
            return f < i; // compare them as floats
        }

        // f < i <=> floor(f) < i
        if (luaV_flttointeger(f, out long fi, F2Imod.F2Ifloor)) // fi = floor(f)
        {
            return fi < i; // compare them as integers
        }

        // 'f' is either greater or less than all integers
        return f < 0; // less?
    }

    /// <summary>
    /// Check whether float 'f' is less than or equal to integer 'i'.
    /// See comments on previous function.
    /// </summary>
    private static bool LEfloatint(double f, long i)
    {
        if (l_intfitsf(i))
        {
            return f <= i; // compare them as floats
        }

        // f <= i <=> ceil(f) <= i
        if (luaV_flttointeger(f, out long fi, F2Imod.F2Iceil)) // fi = ceil(f)
        {
            return fi <= i; // compare them as integers
        }

        // 'f' is either greater or less than all integers
        return f < 0; // less?
    }

    /// <summary>
    /// Return 'l &lt; r', for numbers.
    /// </summary>
    private static bool LTnum(TValue* l, TValue* r)
    {
        Debug.Assert(ttisnumber(l) && ttisnumber(r));
        if (ttisinteger(l))
        {
            long li = ivalue(l);
            if (ttisinteger(r))
            {
                return li < ivalue(r); // both are integers
            }

            // 'l' is int and 'r' is float
            return LTintfloat(li, fltvalue(r)); // l < r ?
        }

        double lf = fltvalue(l); // 'l' must be float
        if (ttisfloat(r))
        {
            return lf < fltvalue(r); // both are float
        }

        // 'l' is float and 'r' is int
        return LTfloatint(lf, ivalue(r));
    }

    /// <summary>
    /// Return 'l &lt;= r', for numbers.
    /// </summary>
    private static bool LEnum(TValue* l, TValue* r)
    {
        Debug.Assert(ttisnumber(l) && ttisnumber(r));
        if (ttisinteger(l))
        {
            long li = ivalue(l);
            if (ttisinteger(r))
            {
                return li <= ivalue(r); // both are integers
            }

            // 'l' is int and 'r' is float
            return LEintfloat(li, fltvalue(r)); // l <= r ?
        }

        double lf = fltvalue(l); // 'l' must be float
        if (ttisfloat(r))
        {
            return lf <= fltvalue(r); // both are float
        }

        // 'l' is float and 'r' is int
        return LEfloatint(lf, ivalue(r));
    }

    /// <summary>
    /// return 'l &lt; r' for non-numbers.
    /// </summary>
    private static bool lessthanothers(lua_State* L, TValue* l, TValue* r)
    {
        Debug.Assert(!ttisnumber(l) || !ttisnumber(r));
        if (ttisstring(l) && ttisstring(r)) // both are strings?
        {
            return l_strcmp(tsvalue(l), tsvalue(r)) < 0;
        }

        return luaT_callorderTM(L, l, r, TMS.LT);
    }

    /// <summary>
    /// Main operation less than; return 'l &lt; r'.
    /// </summary>
    internal static bool luaV_lessthan(lua_State* L, TValue* l, TValue* r)
    {
        if (ttisnumber(l) && ttisnumber(r)) // both operands are numbers?
        {
            return LTnum(l, r);
        }

        return lessthanothers(L, l, r);
    }

    /// <summary>
    /// return 'l &lt;= r' for non-numbers.
    /// </summary>
    private static bool lessequalothers(lua_State* L, TValue* l, TValue* r)
    {
        Debug.Assert(!ttisnumber(l) || !ttisnumber(r));
        if (ttisstring(l) && ttisstring(r)) // both are strings?
        {
            return l_strcmp(tsvalue(l), tsvalue(r)) <= 0;
        }

        return luaT_callorderTM(L, l, r, TMS.LE);
    }

    /// <summary>
    /// Main operation less than or equal to; return 'l &lt;= r'.
    /// </summary>
    internal static bool luaV_lessequal(lua_State* L, TValue* l, TValue* r)
    {
        if (ttisnumber(l) && ttisnumber(r)) // both operands are numbers?
        {
            return LEnum(l, r);
        }

        return lessequalothers(L, l, r);
    }

    /// <summary>
    /// Main operation for equality of Lua values; return 't1 == t2'.
    /// L == null means raw equality (no metamethods)
    /// </summary>
    internal static bool luaV_equalobj(lua_State* L, TValue* t1, TValue* t2)
    {
        if (ttype(t1) != ttype(t2)) // not the same type?
        {
            return false;
        }

        if (ttypetag(t1) != ttypetag(t2))
        {
            switch (ttypetag(t1))
            {
                case LUA_VNUMINT:
                    {
                        // integer == float?
                        // integer and float can only be equal if float has an integer
                        // value equal to the integer
                        return luaV_flttointeger(fltvalue(t2), out long i2, F2Imod.F2Ieq) &&
                               ivalue(t1) == i2;
                    }

                case LUA_VNUMFLT:
                    {
                        // float == integer?
                        return luaV_flttointeger(fltvalue(t1), out long i1, F2Imod.F2Ieq) &&
                               i1 == ivalue(t2);
                    }

                case LUA_VSHRSTR:
                case LUA_VLNGSTR:
                    // compare two strings with different variants: they can be
                    // equal when one string is a short string and the other is
                    // an external string
                    return luaS_eqstr(tsvalue(t1), tsvalue(t2));

                default:
                    // only numbers (integer/float) and strings (long/short) can have
                    // equal values with different variants
                    return false;
            }
        }

        // equal variants
        TValue* tm;
        switch (ttypetag(t1))
        {
            case LUA_VNIL:
            case LUA_VFALSE:
            case LUA_VTRUE:
                return true;

            case LUA_VNUMINT:
                return ivalue(t1) == ivalue(t2);

            case LUA_VNUMFLT:
                return fltvalue(t1) == fltvalue(t2);

            case LUA_VLIGHTUSERDATA:
                return pvalue(t1) == pvalue(t2);

            case LUA_VSHRSTR:
                return eqshrstr(tsvalue(t1), tsvalue(t2));

            case LUA_VLNGSTR:
                return luaS_eqstr(tsvalue(t1), tsvalue(t2));

            case LUA_VUSERDATA:
                if (uvalue(t1) == uvalue(t2))
                {
                    return true;
                }

                if (L == null)
                {
                    return false;
                }

                tm = fasttm(L, uvalue(t1)->metatable, TMS.EQ);
                if (tm == null)
                {
                    tm = fasttm(L, uvalue(t2)->metatable, TMS.EQ);
                }

                break; // will try TM

            case LUA_VTABLE:
                {
                    if (hvalue(t1) == hvalue(t2))
                    {
                        return true;
                    }

                    if (L == null)
                    {
                        return false;
                    }

                    tm = fasttm(L, hvalue(t1)->metatable, TMS.EQ);
                    if (tm == null)
                    {
                        tm = fasttm(L, hvalue(t2)->metatable, TMS.EQ);
                    }

                    break; // will try TM
                }

            case LUA_VLCF:
                return fvalue(t1) == fvalue(t2);

            default: // functions and threads
                return gcvalue(t1) == gcvalue(t2);
        }

        if (tm == null) // no TM?
        {
            return false; // objects are different
        }

        byte tag = luaT_callTMres(L, tm, t1, t2, L->top.p); // call TM
        return !tagisfalse(tag);
    }

    /// <summary>
    /// macro used by 'luaV_concat' to ensure that element at 'o' is a string
    /// </summary>
    private static bool tostring(lua_State* L, TValue* o)
    {
        if (ttisstring(o))
        {
            return true;
        }

        if (!cvt2str(o))
        {
            return false;
        }

        luaO_tostring(L, o);
        return true;
    }

    /// <summary>
    /// Check whether object is a short empty string to optimise concatenation.
    /// (External strings can be empty too; they will be concatenated like
    /// non-empty ones.)
    /// </summary>
    private static bool isemptystr(TValue* o)
    {
        return ttisshrstring(o) && tsvalue(o)->shrlen == 0;
    }

    /// <summary>
    /// copy strings in stack from top - n up to top - 1 to buffer
    /// </summary>
    private static void copy2buff(StkId top, int n, byte* buff)
    {
        long tl = 0; // size already copied
        do
        {
            TString* st = tsvalue(s2v(top - n));
            byte* s = getlstr(st, out int l);
            memcpy(buff + tl, s, l);
            tl += l;
        } while (--n > 0);
    }

    /// <summary>
    /// Main operation for concatenation: concat 'total' values in the stack,
    /// from 'L-&gt;top.p - total' up to 'L-&gt;top.p - 1'.
    /// </summary>
    internal static void luaV_concat(lua_State* L, int total)
    {
        if (total == 1)
        {
            return; // "all" values already concatenated
        }

        byte* buff = stackalloc byte[LUAI_MAXSHORTLEN];
        
        do
        {
            StkId top = L->top.p;
            int n = 2; // number of elements handled in this pass (at least 2)
            if (!(ttisstring(s2v(top - 2)) || cvt2str(s2v(top - 2))) ||
                !tostring(L, s2v(top - 1)))
            {
                luaT_tryconcatTM(L); // may invalidate 'top'
            }
            else if (isemptystr(s2v(top - 1))) // second operand is empty?
            {
                tostring(L, s2v(top - 2)); // result is first operand
            }
            else if (isemptystr(s2v(top - 2)))
            {
                // first operand is empty string?
                setobjs2s(L, top - 2, top - 1); // result is second op.
            }
            else
            {
                // at least two string values; get as many as possible
                int tl = tsslen(tsvalue(s2v(top - 1))); // total length
                // collect total length and number of strings
                for (n = 1; n < total && tostring(L, s2v(top - n - 1)); n++)
                {
                    int l = tsslen(tsvalue(s2v(top - n - 1)));
                    if (l >= int.MaxValue - sizeof(TString) - tl)
                    {
                        L->top.p = top - total; // pop strings to avoid wasting stack
                        luaG_runerror(L, "string length overflow");
                    }

                    tl += l;
                }

                TString* ts;
                if (tl <= LUAI_MAXSHORTLEN)
                {
                    // is result a short string?
                    copy2buff(top, n, buff); // copy strings to buffer
                    ts = luaS_newlstr(L, buff, tl);
                }
                else
                {
                    // long string; copy strings directly to final result
                    ts = luaS_createlngstrobj(L, tl);
                    copy2buff(top, n, getlngstr(ts));
                }

                setsvalue2s(L, top - n, ts); // create result
            }

            total -= n - 1; // got 'n' strings to create one new
            L->top.p -= n - 1; // popped 'n' strings and pushed one
        } while (total > 1); // repeat until only 1 result left
    }

    /// <summary>
    /// Main operation 'ra = #rb'.
    /// </summary>
    internal static void luaV_objlen(lua_State* L, StkId ra, TValue* rb)
    {
        TValue* tm;
        switch (ttypetag(rb))
        {
            case LUA_VTABLE:
                {
                    Table* h = hvalue(rb);
                    tm = fasttm(L, h->metatable, TMS.LEN);
                    if (tm != null)
                    {
                        break; // metamethod? break switch to call it
                    }

                    setivalue(s2v(ra), (long)luaH_getn(L, h)); // else primitive len
                    return;
                }

            case LUA_VSHRSTR:
                setivalue(s2v(ra), tsvalue(rb)->shrlen);
                return;

            case LUA_VLNGSTR:
                setivalue(s2v(ra), tsvalue(rb)->u.lnglen);
                return;

            default:
                // try metamethod
                tm = luaT_gettmbyobj(L, rb, TMS.LEN);
                if (notm(tm)) // no metamethod?
                {
                    luaG_typeerror(L, rb, "get length of");
                }

                break;
        }

        luaT_callTMres(L, tm, rb, rb, ra);
    }

    /// <summary>
    /// Integer division; return 'm // n', that is, floor(m/n).
    /// C division truncates its result (rounds towards zero).
    /// 'floor(q) == trunc(q)' when 'q &gt;= 0' or when 'q' is integer,
    /// otherwise 'floor(q) == trunc(q) - 1'.
    /// </summary>
    internal static long luaV_idiv(lua_State* L, long x, long y)
    {
        if ((ulong)y + 1u <= 1u)
        {
            // special cases: -1 or 0
            if (y == 0)
            {
                luaG_runerror(L, "attempt to divide by zero");
            }

            return -x; // n==-1; avoid overflow with 0x80000...//-1
        }

        long q = x / y; // perform C division
        if ((x ^ y) < 0 && x % y != 0) // 'm/n' would be negative non-integer?
        {
            q -= 1; // correct result for different rounding
        }

        return q;
    }

    /// <summary>
    /// Integer modulus; return 'm % n'. (Assume that C '%' with
    /// negative operands follows C99 behaviour. See previous comment
    /// about luaV_idiv.)
    /// </summary>
    internal static long luaV_mod(lua_State* L, long x, long y)
    {
        if ((ulong)y + 1u <= 1u)
        {
            // special cases: -1 or 0
            if (y == 0)
            {
                luaG_runerror(L, "attempt to perform 'n%%0'");
            }

            return 0; // m % -1 == 0; avoid overflow with 0x80000...%-1
        }

        long r = x % y;
        if (r != 0 && (r ^ y) < 0) // 'm/n' would be non-integer negative?
        {
            r += y; // correct result for different rounding
        }

        return r;
    }

    /// <summary>
    /// Float modulus
    /// </summary>
    internal static double luaV_modf(lua_State* L, double x, double y)
    {
        double r = x % y;
        if (r > 0 ? y < 0 : r < 0 && y > 0)
        {
            r += y;
        }

        return r;
    }

    /// <summary>
    /// Shift left operation. (Shift right just negates 'y'.)
    /// </summary>
    internal static long luaV_shiftl(long x, long y)
    {
        if (y < 0)
        {
            // shift right?
            if (y <= -64)
            {
                return 0;
            }

            return x >>> (int)-y;
        }

        // shift left
        if (y >= 64)
        {
            return 0;
        }

        return x << (int)y;
    }

    /// <summary>
    /// create a new Lua closure, push it in the stack, and initialise
    /// its upvalues.
    /// </summary>
    private static void pushclosure(
        lua_State* L,
        Proto* p,
        LClosure* encup,
        StkId @base,
        StkId ra)
    {
        int nup = p->sizeupvalues;
        Upvaldesc* uv = p->upvalues;
        LClosure* ncl = luaF_newLclosure(L, nup);
        ncl->p = p;
        setclLvalue2s(L, ra, ncl); // anchor new closure in stack
        for (int i = 0; i < nup; i++)
        {
            // fill in its upvalues
            if (uv[i].instack != 0) // upvalue refers to local variable?
            {
                LClosure.GetUpValue(ncl, i) = luaF_findupval(L, @base + uv[i].idx);
            }
            else // get upvalue from enclosing function
            {
                LClosure.GetUpValue(ncl, i) = LClosure.GetUpValue(encup, uv[i].idx);
            }

            luaC_objbarrier(L, (GCObject*)ncl, (GCObject*)LClosure.GetUpValue(ncl, i));
        }
    }

    /// <summary>
    /// finish execution of an opcode interrupted by a yield
    /// </summary>
    private static void luaV_finishOp(lua_State* L)
    {
        CallInfo* ci = L->ci;
        StkId @base = ci->func.p + 1;
        uint inst = *(ci->u.l.savedpc - 1); // interrupted instruction
        OpCode op = GET_OPCODE(inst);
        switch (op)
        {
            // finish its execution
            case OpCode.MMBin:
            case OpCode.MMBinI:
            case OpCode.MMBinK:
                setobjs2s(L, @base + GETARG_A(*(ci->u.l.savedpc - 2)), --L->top.p);
                break;
            
            case OpCode.UNM:
            case OpCode.BNot:
            case OpCode.Len:
            case OpCode.GetTabUp:
            case OpCode.GetTable:
            case OpCode.GetI:
            case OpCode.GetField:
            case OpCode.Self:
                setobjs2s(L, @base + GETARG_A(inst), --L->top.p);
                break;

            case OpCode.LT:
            case OpCode.LE:
            case OpCode.LTI:
            case OpCode.LEI:
            case OpCode.GTI:
            case OpCode.GEI:
            case OpCode.Eq:
                {
                    // note that 'OP_EQI'/'OP_EQK' cannot yield
                    bool res = !l_isfalse(s2v(L->top.p - 1));
                    L->top.p--;
                    Debug.Assert(GET_OPCODE(*ci->u.l.savedpc) == OpCode.Jmp);
                    if (res != GETARG_k(inst)) // condition failed?
                    {
                        ci->u.l.savedpc++; // skip jump instruction
                    }

                    break;
                }

            case OpCode.Concat:
                {
                    StkId top = L->top.p - 1; // top when 'luaT_tryconcatTM' was called
                    int a = GETARG_A(inst); // first element to concatenate
                    int total = (int)(top - 1 - (@base + a)); // yet to concatenate
                    setobjs2s(L, top - 2, top); // put TM result in proper position
                    L->top.p = top - 1; // top is one after last element (at top-2)
                    luaV_concat(L, total); // concat them (may yield again)
                    break;
                }
            
            case OpCode.Close:
                // yielded closing variables
                ci->u.l.savedpc--; // repeat instruction to close other vars.
                break;

            case OpCode.Return:
                {
                    // yielded closing variables
                    StkId ra = @base + GETARG_A(inst);
                    // adjust top to signal correct number of returns, in case the
                    // return is "up to top" ('isIT')
                    L->top.p = ra + ci->u2.nres;
                    // repeat instruction to close other vars. and complete the return
                    ci->u.l.savedpc--;
                    break;
                }
            
            default:
                // only these other opcodes can yield
                Debug.Assert(
                    op is OpCode.TForCall
                        or OpCode.Call
                        or OpCode.TailCall
                        or OpCode.SetTabUp
                        or OpCode.SetTable
                        or OpCode.SetI
                        or OpCode.SetField);
                break;
        }
    }

    // {==================================================================
    // Function 'luaV_execute': main interpreter loop
    // ===================================================================

    /// <summary>
    /// macro executed during Lua functions at points where the
    /// function can yield.
    /// </summary>
    private static void luai_threadyield(lua_State* L)
    {
        lua_unlock(L);
        lua_lock(L);
    }

    private static void luaV_execute(lua_State* L, CallInfo* ci)
    {
        startfunc:
        byte trap = L->hookmask;
        returning: // trap already set
        LClosure* cl = ci_func(ci);
        TValue* k = cl->p->k;
        uint* pc = ci->u.l.savedpc;
        if (trap != 0)
        {
            trap = (byte)(luaG_tracecall(L) ? 1 : 0);
        }

        StkId @base = ci->func.p + 1;
        // main loop of interpreter
        while (true)
        {
            if (trap != 0)
            {
                // stack reallocation or hooks?
                trap = (byte)(luaG_traceexec(L, pc) ? 1 : 0); // handle hooks
                @base = ci->func.p + 1; // correct stack
            }

            uint i = *pc++;
#if false
            {
                // low-level line tracing for debugging Lua
                int pcrel = pcRel(pc, cl->p);
                Console.WriteLine(
                    "line: {0}; {1} ({2})",
                    luaG_getfuncline(cl->p, pcrel),
                    opnames[(int)GET_OPCODE(i)],
                    pcrel);
            }
#endif
            Debug.Assert(@base == ci->func.p + 1);
            Debug.Assert(@base <= L->top.p && L->top.p <= L->stack_last.p);
            // for tests, invalidate top for instructions not expecting it
            if (!luaP_isIT(i))
            {
                L->top.p = @base;
            }

            switch (GET_OPCODE(i))
            {
                case OpCode.Move:
                    {
                        StkId ra = @base + GETARG_A(i);
                        setobjs2s(L, ra, @base + GETARG_B(i));
                        break;
                    }

                case OpCode.LoadI:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int b = GETARG_sBx(i);
                        setivalue(s2v(ra), b);
                        break;
                    }

                case OpCode.LoadF:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int b = GETARG_sBx(i);
                        setfltvalue(s2v(ra), b);
                        break;
                    }

                case OpCode.LoadK:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = k + GETARG_Bx(i);
                        setobj2s(L, ra, rb);
                        break;
                    }

                case OpCode.LoadKX:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = k + GETARG_Ax(*pc);
                        pc++;
                        setobj2s(L, ra, rb);
                        break;
                    }

                case OpCode.LoadFalse:
                    {
                        StkId ra = @base + GETARG_A(i);
                        setbfvalue(s2v(ra));
                        break;
                    }

                case OpCode.LFalseSkip:
                    {
                        StkId ra = @base + GETARG_A(i);
                        setbfvalue(s2v(ra));
                        pc++; // skip next instruction
                        break;
                    }

                case OpCode.LoadTrue:
                    {
                        StkId ra = @base + GETARG_A(i);
                        setbtvalue(s2v(ra));
                        break;
                    }

                case OpCode.LoadNil:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int b = GETARG_B(i);
                        do
                        {
                            setnilvalue(s2v(ra++));
                        } while (b-- != 0);

                        break;
                    }

                case OpCode.GetUpVal:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int b = GETARG_B(i);
                        setobj2s(L, ra, LClosure.GetUpValue(cl, b)->v.p);
                        break;
                    }

                case OpCode.SetUpVal:
                    {
                        StkId ra = @base + GETARG_A(i);
                        UpVal* uv = LClosure.GetUpValue(cl, GETARG_B(i));
                        setobj(L, uv->v.p, s2v(ra));
                        luaC_barrier(L, (GCObject*)uv, s2v(ra));
                        break;
                    }

                case OpCode.GetTabUp:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* upval = LClosure.GetUpValue(cl, GETARG_B(i))->v.p;
                        TValue* rc = k + GETARG_C(i);
                        TString* key = tsvalue(rc); // key must be a short string
                        byte tag = !ttistable(upval) ? LUA_VNOTABLE : luaH_getshortstr(hvalue(upval), key, s2v(ra));
                        if (tagisempty(tag))
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishget(L, upval, rc, ra, tag);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.GetTable:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        TValue* rc = s2v(@base + GETARG_C(i));
                        byte tag;
                        if (ttisinteger(rc))
                        {
                            // fast track for integers?
                            luaV_fastgeti(rb, ivalue(rc), s2v(ra), out tag);
                        }
                        else
                        {
                            tag = !ttistable(rb) ? LUA_VNOTABLE : luaH_get(hvalue(rb), rc, s2v(ra));
                        }

                        if (tagisempty(tag))
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishget(L, rb, rc, ra, tag);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.GetI:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        int c = (int)GETARG_C(i);
                        luaV_fastgeti(rb, c, s2v(ra), out byte tag);
                        if (tagisempty(tag))
                        {
                            TValue key;
                            TValue* keyPtr = &key;
                            setivalue(keyPtr, c);
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishget(L, rb, keyPtr, ra, tag);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.GetField:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        TValue* rc = k + GETARG_C(i);
                        TString* key = tsvalue(rc); // key must be a short string
                        byte tag = !ttistable(rb) ? LUA_VNOTABLE : luaH_getshortstr(hvalue(rb), key, s2v(ra));
                        if (tagisempty(tag))
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishget(L, rb, rc, ra, tag);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.SetTabUp:
                    {
                        TValue* upval = LClosure.GetUpValue(cl, GETARG_A(i))->v.p;
                        TValue* rb = k + GETARG_B(i);
                        TValue* rc = TESTARG_k(i) ? k + GETARG_C(i) : s2v(@base + GETARG_C(i));
                        TString* key = tsvalue(rb); // key must be a short string
                        int hres = !ttistable(upval) ? HNOTATABLE : luaH_psetshortstr(hvalue(upval), key, rc);
                        if (hres == HOK)
                        {
                            luaV_finishfastset(L, upval, rc);
                        }
                        else
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishset(L, upval, rb, rc, hres);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.SetTable:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i)); // key (table is in 'ra')
                        TValue* rc = TESTARG_k(i) ? k + GETARG_C(i) : s2v(@base + GETARG_C(i)); // value

                        int hres;
                        if (ttisinteger(rb))
                        {
                            // fast track for integers?
                            luaV_fastseti(s2v(ra), ivalue(rb), rc, out hres);
                        }
                        else
                        {
                            hres = !ttistable(s2v(ra)) ? HNOTATABLE : luaH_pset(hvalue(s2v(ra)), rb, rc);
                        }

                        if (hres == HOK)
                        {
                            luaV_finishfastset(L, s2v(ra), rc);
                        }
                        else
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishset(L, s2v(ra), rb, rc, hres);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.SetI:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int b = GETARG_B(i);
                        TValue* rc = TESTARG_k(i) ? k + GETARG_C(i) : s2v(@base + GETARG_C(i));
                        luaV_fastseti(s2v(ra), b, rc, out int hres);
                        if (hres == HOK)
                        {
                            luaV_finishfastset(L, s2v(ra), rc);
                        }
                        else
                        {
                            TValue key;
                            TValue* keyPtr = &key;
                            setivalue(keyPtr, b);
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishset(L, s2v(ra), keyPtr, rc, hres);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }
                case OpCode.SetField:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = k + GETARG_B(i);
                        TValue* rc = TESTARG_k(i) ? k + GETARG_C(i) : s2v(@base + GETARG_C(i));
                        TString* key = tsvalue(rb); // key must be a short string
                        int hres = !ttistable(s2v(ra)) ? HNOTATABLE : luaH_psetshortstr(hvalue(s2v(ra)), key, rc);
                        if (hres == HOK)
                        {
                            luaV_finishfastset(L, s2v(ra), rc);
                        }
                        else
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishset(L, s2v(ra), rb, rc, hres);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.NewTable:
                    {
                        StkId ra = @base + GETARG_A(i);
                        uint b = (uint)GETARG_vB(i); // log2(hash size) + 1
                        uint c = (uint)GETARG_vC(i); // array size
                        if (b > 0)
                        {
                            b = 1u << (int)(b - 1); // hash size is 2^(b - 1)
                        }

                        if (TESTARG_k(i))
                        {
                            // non-zero extra argument?
                            Debug.Assert(GETARG_Ax(*pc) != 0);
                            // add it to array size
                            c += (uint)GETARG_Ax(*pc) * (MAXARG_vC + 1);
                        }

                        pc++; // skip extra argument
                        L->top.p = ra + 1; // correct top in case of emergency GC
                        Table* t = luaH_new(L) ; // memory allocation
                        sethvalue2s(L, ra, t);
                        if (b != 0 || c != 0)
                        {
                            luaH_resize(L, t, c, b); // idem
                        }

                        if (G(L)->GCdebt <= 0)
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ra + 1;
                            luaC_step(L);
                            trap = ci->u.l.trap;
                        }

#if HARDMEMTESTS
                        if (gcrunning(G(L)))
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ra + 1;
                            luaC_fullgc(L, false);
                            trap = ci->u.l.trap;
                        }
#endif

                        luai_threadyield(L);
                        break;
                    }

                case OpCode.Self:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        TValue* rc = k + GETARG_C(i);
                        TString* key = tsvalue(rc); // key must be a short string
                        setobj2s(L, ra + 1, rb);
                        byte tag = !ttistable(rb) ? LUA_VNOTABLE : luaH_getshortstr(hvalue(rb), key, s2v(ra));
                        if (tagisempty(tag))
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaV_finishget(L, rb, rc, ra, tag);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.AddI:
                    {
                        TValue* ra = s2v(@base + GETARG_A(i));
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        int imm = (int)GETARG_sC(i);
                        if (ttisinteger(v1))
                        {
                            long iv1 = ivalue(v1);
                            pc++;
                            setivalue(ra, iv1 + imm);
                        }
                        else if (ttisfloat(v1))
                        {
                            double nb = fltvalue(v1);
                            double fimm = imm;
                            pc++;
                            setfltvalue(ra, nb + fimm);
                        }
                        
                        break;
                    }

                case OpCode.AddK:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), i1 + i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n1 + n2);
                        }

                        break;
                    }

                case OpCode.SubK:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), i1 - i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n1 - n2);
                        }

                        break;
                    }

                case OpCode.MulK:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), i1 * i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n1 * n2);
                        }

                        break;
                    }

                case OpCode.ModK:
                    {
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), luaV_mod(L, i1, i2));
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), luaV_modf(L, n1, n2));
                        }

                        break;
                    }

                case OpCode.PowK:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        Debug.Assert(ttisnumber(v2));
                        if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n2 == 2 ? n1 * n1 : Math.Pow(n1, n2));
                        }

                        break;
                    }

                case OpCode.DivK:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        Debug.Assert(ttisnumber(v2));
                        if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n1 / n2);
                        }

                        break;
                    }

                case OpCode.IDivK:
                    {
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), luaV_idiv(L, i1, i2));
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), Math.Floor(n1 / n2));
                        }

                        break;
                    }

                case OpCode.BAndK:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        long i2 = ivalue(v2);
                        if (tointegerns(v1, out long i1))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setivalue(s2v(ra), i1 & i2);
                        }

                        break;
                    }

                case OpCode.BOrK:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        long i2 = ivalue(v2);
                        if (tointegerns(v1, out long i1))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setivalue(s2v(ra), i1 | i2);
                        }

                        break;
                    }

                case OpCode.BXorK:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = k + GETARG_C(i);
                        long i2 = ivalue(v2);
                        if (tointegerns(v1, out long i1))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setivalue(s2v(ra), i1 ^ i2);
                        }

                        break;
                    }

                case OpCode.ShlI:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        int ic = (int)GETARG_sC(i);
                        if (tointegerns(rb, out long ib))
                        {
                            pc++;
                            setivalue(s2v(ra), luaV_shiftl(ic, ib));
                        }

                        break;
                    }

                case OpCode.ShrI:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        int ic = (int)GETARG_sC(i);
                        if (tointegerns(rb, out long ib))
                        {
                            pc++;
                            setivalue(s2v(ra), luaV_shiftl(ib, -ic));
                        }

                        break;
                    }

                case OpCode.Add:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), i1 + i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n1 + n2);
                        }

                        break;
                    }

                case OpCode.Sub:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), i1 - i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n1 - n2);
                        }

                        break;
                    }

                case OpCode.Mul:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), i1 * i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n1 * n2);
                        }

                        break;
                    }

                case OpCode.Mod:
                    {
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), luaV_mod(L, i1, i2));
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), luaV_modf(L, n1, n2));
                        }

                        break;
                    }

                case OpCode.Pow:
                    {
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));
                        if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n2 == 2 ? n1 * n1 : Math.Pow(n1, n2));
                        }

                        break;
                    }

                case OpCode.Div:
                    {
                        // float division (always with floats)
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));
                        if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), n1 / n2);
                        }

                        break;
                    }

                case OpCode.IDiv:
                    {
                        // floor division
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        TValue* v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            pc++;
                            setivalue(s2v(ra), luaV_idiv(L, i1, i2));
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setfltvalue(s2v(ra), Math.Floor(n1 / n2));
                        }

                        break;
                    }

                case OpCode.BAnd:
                    {
                        TValue *v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));

                        if (tointegerns(v1, out long i1) && tointegerns(v2, out long i2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setivalue(s2v(ra), i1 & i2);
                        }

                        break;
                    }

                case OpCode.BOr:
                    {
                        TValue *v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));

                        if (tointegerns(v1, out long i1) && tointegerns(v2, out long i2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setivalue(s2v(ra), i1 | i2);
                        }

                        break;
                    }

                case OpCode.BXor:
                    {
                        TValue *v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));

                        if (tointegerns(v1, out long i1) && tointegerns(v2, out long i2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setivalue(s2v(ra), i1 ^ i2);
                        }

                        break;
                    }

                case OpCode.Shl:
                    {
                        TValue *v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));

                        if (tointegerns(v1, out long i1) && tointegerns(v2, out long i2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setivalue(s2v(ra), luaV_shiftl(i1, i2));
                        }

                        break;
                    }

                case OpCode.Shr:
                    {
                        TValue *v1 = s2v(@base + GETARG_B(i));
                        TValue* v2 = s2v(@base + GETARG_C(i));

                        if (tointegerns(v1, out long i1) && tointegerns(v2, out long i2))
                        {
                            StkId ra = @base + GETARG_A(i);
                            pc++;
                            setivalue(s2v(ra), luaV_shiftr(i1, i2));
                        }

                        break;
                    }

                case OpCode.MMBin:
                    {
                        StkId ra = @base + GETARG_A(i);
                        uint pi = *(pc - 2); // original arith. expression
                        TValue* rb = s2v(@base + GETARG_B(i));
                        TMS tm = (TMS)GETARG_C(i);
                        StkId result = @base + GETARG_A(pi);
                        Debug.Assert(OpCode.Add <= GET_OPCODE(pi) && GET_OPCODE(pi) <= OpCode.Shr);
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        luaT_trybinTM(L, s2v(ra), rb, result, tm);
                        trap = ci->u.l.trap;
                        break;
                    }

                case OpCode.MMBinI:
                    {
                        StkId ra = @base + GETARG_A(i);
                        uint pi = *(pc - 2); // original arith. expression
                        int imm = (int)GETARG_sB(i);
                        TMS tm = (TMS)GETARG_C(i);
                        bool flip = GETARG_k(i);
                        StkId result = @base + GETARG_A(pi);
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        luaT_trybiniTM(L, s2v(ra), imm, flip, result, tm);
                        trap = ci->u.l.trap;
                        break;
                    }

                case OpCode.MMBinK:
                    {
                        StkId ra = @base + GETARG_A(i);
                        uint pi = *(pc - 2); // original arith. expression
                        TValue* imm = k + GETARG_B(i);
                        TMS tm = (TMS)GETARG_C(i);
                        bool flip = GETARG_k(i);
                        StkId result = @base + GETARG_A(pi);
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        luaT_trybinassocTM(L, s2v(ra), imm, flip, result, tm);
                        trap = ci->u.l.trap;
                        break;
                    }

                case OpCode.UNM:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        if (ttisinteger(rb))
                        {
                            long ib = ivalue(rb);
                            setivalue(s2v(ra), -ib);
                        }
                        else if (tonumberns(rb, out double nb))
                        {
                            setfltvalue(s2v(ra), -nb);
                        }
                        else
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaT_trybinTM(L, rb, rb, ra, TMS.UNM);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.BNot:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        if (tointegerns(rb, out long ib))
                        {
                            setivalue(s2v(ra), ~0L ^ ib);
                        }
                        else
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaT_trybinTM(L, rb, rb, ra, TMS.BNOT);
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.Not:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        if (l_isfalse(rb))
                        {
                            setbtvalue(s2v(ra));
                        }
                        else
                        {
                            setbfvalue(s2v(ra));
                        }

                        break;
                    }

                case OpCode.Len:
                    {
                        StkId ra = @base + GETARG_A(i);
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        luaV_objlen(L, ra, s2v(@base + GETARG_B(i)));
                        trap = ci->u.l.trap;
                        break;
                    }

                case OpCode.Concat:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int n = GETARG_B(i); // number of elements to concatenate
                        L->top.p = ra + n; // mark the end of concat operands
                        ci->u.l.savedpc = pc;
                        luaV_concat(L, n);
                        trap = ci->u.l.trap;
                        
                        if (G(L)->GCdebt <= 0)
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = L->top.p;
                            luaC_step(L);
                            trap = ci->u.l.trap;
                        }

#if HARDMEMTESTS
                        if (gcrunning(G(L)))
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = L->top.p;
                            luaC_fullgc(L, false);
                            trap = ci->u.l.trap;
                        }
#endif

                        luai_threadyield(L);
                        break;
                    }

                case OpCode.Close:
                    {
                        StkId ra = @base + GETARG_A(i);
                        Debug.Assert(GETARG_B(i) == 0); // 'close must be alive
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        luaF_close(L, ra, LUA_OK, true);
                        trap = ci->u.l.trap;
                        break;
                    }

                case OpCode.TBC:
                    {
                        StkId ra = @base + GETARG_A(i);
                        // create new to-be-closed upvalue
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        luaF_newtbcupval(L, ra);
                        break;
                    }

                case OpCode.Jmp:
                    pc += GETARG_sJ(i) + 0;
                    trap = ci->u.l.trap;
                    break;

                case OpCode.Eq:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        bool cond = luaV_equalobj(L, s2v(ra), rb);
                        trap = ci->u.l.trap;
                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.LT:
                    {
                        TValue* ra = s2v(@base + GETARG_A(i));
                        TValue* rb = s2v(@base + GETARG_B(i));
                        bool cond;
                        if (ttisinteger(ra) && ttisinteger(rb))
                        {
                            long ia = ivalue(ra);
                            long ib = ivalue(rb);
                            cond = ia < ib;
                        }
                        else if (ttisnumber(ra) && ttisnumber(rb))
                        {
                            cond = LTnum(ra, rb);
                        }
                        else
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            cond = lessthanothers(L, ra, rb);
                            trap = ci->u.l.trap;
                        }

                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.LE:
                    {
                        TValue* ra = s2v(@base + GETARG_A(i));
                        TValue* rb = s2v(@base + GETARG_B(i));
                        bool cond;
                        if (ttisinteger(ra) && ttisinteger(rb))
                        {
                            long ia = ivalue(ra);
                            long ib = ivalue(rb);
                            cond = ia <= ib;
                        }
                        else if (ttisnumber(ra) && ttisnumber(rb))
                        {
                            cond = LEnum(ra, rb);
                        }
                        else
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            cond = lessequalothers(L, ra, rb);
                            trap = ci->u.l.trap;
                        }

                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.EqK:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = k + GETARG_B(i);
                        // basic types do not use '__eq'; we can use raw equality
                        bool cond = luaV_rawequalobj(s2v(ra), rb);
                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.EqI:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int im = (int)GETARG_sB(i);
                        bool cond;
                        if (ttisinteger(s2v(ra)))
                        {
                            cond = ivalue(s2v(ra)) == im;
                        }
                        else if (ttisfloat(s2v(ra)))
                        {
                            cond = fltvalue(s2v(ra)) == im;
                        }
                        else
                        {
                            cond = false; // other types cannot be equal to a number
                        }

                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.LTI:
                    {
                        TValue* ra = s2v(@base + GETARG_A(i));
                        int im = (int)GETARG_sB(i);
                        bool cond;
                        if (ttisinteger(ra))
                        {
                            cond = ivalue(ra) < im;
                        }
                        else if (ttisfloat(ra))
                        {
                            double fa = fltvalue(ra);
                            double fim = im;
                            cond = fa < fim;
                        }
                        else
                        {
                            bool isf = GETARG_C(i) != 0;
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            cond = luaT_callorderiTM(L, ra, im, false, isf, TMS.LT);
                            trap = ci->u.l.trap;
                        }

                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.LEI:
                    {
                        TValue* ra = s2v(@base + GETARG_A(i));
                        int im = (int)GETARG_sB(i);
                        bool cond;
                        if (ttisinteger(ra))
                        {
                            cond = ivalue(ra) <= im;
                        }
                        else if (ttisfloat(ra))
                        {
                            double fa = fltvalue(ra);
                            double fim = im;
                            cond = fa <= fim;
                        }
                        else
                        {
                            bool isf = GETARG_C(i) != 0;
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            cond = luaT_callorderiTM(L, ra, im, false, isf, TMS.LE);
                            trap = ci->u.l.trap;
                        }

                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.GTI:
                    {
                        TValue* ra = s2v(@base + GETARG_A(i));
                        int im = (int)GETARG_sB(i);
                        bool cond;
                        if (ttisinteger(ra))
                        {
                            cond = ivalue(ra) > im;
                        }
                        else if (ttisfloat(ra))
                        {
                            double fa = fltvalue(ra);
                            double fim = im;
                            cond = fa > fim;
                        }
                        else
                        {
                            bool isf = GETARG_C(i) != 0;
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            cond = luaT_callorderiTM(L, ra, im, true, isf, TMS.LT);
                            trap = ci->u.l.trap;
                        }

                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.GEI:
                    {
                        TValue* ra = s2v(@base + GETARG_A(i));
                        int im = (int)GETARG_sB(i);
                        bool cond;
                        if (ttisinteger(ra))
                        {
                            cond = ivalue(ra) >= im;
                        }
                        else if (ttisfloat(ra))
                        {
                            double fa = fltvalue(ra);
                            double fim = im;
                            cond = fa >= fim;
                        }
                        else
                        {
                            bool isf = GETARG_C(i) != 0;
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            cond = luaT_callorderiTM(L, ra, im, true, isf, TMS.LE);
                            trap = ci->u.l.trap;
                        }

                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.Test:
                    {
                        StkId ra = @base + GETARG_A(i);
                        bool cond = !l_isfalse(s2v(ra));
                        if (cond != GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.TestSet:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rb = s2v(@base + GETARG_B(i));
                        if (l_isfalse(rb) == GETARG_k(i))
                        {
                            pc++;
                        }
                        else
                        {
                            setobj2s(L, ra, rb);
                            uint ni = *pc;
                            pc += GETARG_sJ(ni) + 1;
                            trap = ci->u.l.trap;
                        }

                        break;
                    }

                case OpCode.Call:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int b = GETARG_B(i);
                        int nresults = (int)(GETARG_C(i) - 1);
                        if (b != 0) // fixed number of arguments?
                        {
                            L->top.p = ra + b; // top signals number of arguments
                        }
                        // else previous instruction set top

                        ci->u.l.savedpc = pc; // in case of errors

                        CallInfo* newci;
                        if ((newci = luaD_precall(L, ra, nresults)) == null)
                        {
                            trap = ci->u.l.trap; // C call; nothing else to be done
                        }
                        else
                        {
                            // Lua call: run function in this same C frame
                            ci = newci;
                            goto startfunc;
                        }

                        break;
                    }

                case OpCode.TailCall:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int b = GETARG_B(i); // number of arguments + 1 (function)
                        int nparams1 = (int)GETARG_C(i);
                        // delta is virtual 'func' - real 'func' (vararg functions)
                        int delta = nparams1 != 0 ? ci->u.l.nextraargs + nparams1 : 0;
                        if (b != 0)
                        {
                            L->top.p = ra + b;
                        }
                        else // previous instruction set top
                        {
                            b = (int)(L->top.p - ra);
                        }

                        ci->u.l.savedpc = pc; // several calls here can raise errors
                        if (TESTARG_k(i))
                        {
                            luaF_closeupval(L, @base); // close upvalues from current call
                            Debug.Assert(L->tbclist.p < @base); // no pending tbc variables
                            Debug.Assert(@base == ci->func.p + 1);
                        }

                        int n;
                        if ((n = luaD_pretailcall(L, ci, ra, b, delta)) < 0) // Lua function?
                        {
                            goto startfunc; // execute the callee
                        }

                        // C function?
                        ci->func.p -= delta; // restore 'func' (if vararg)
                        luaD_poscall(L, ci, n); // finish caller
                        trap = ci->u.l.trap; // 'luaD_poscall' can change hooks
                        goto ret; // caller returns after the tail call
                    }

                case OpCode.Return:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int n = GETARG_B(i) - 1; // number of results
                        int nparams1 = (int)GETARG_C(i);
                        if (n < 0) // not fixed?
                        {
                            n = (int)(L->top.p - ra); // get what is available
                        }

                        ci->u.l.savedpc = pc;
                        if (TESTARG_k(i))
                        {
                            // may there be open upvalues?
                            ci->u2.nres = n; // save number of returns
                            if (L->top.p < ci->top.p)
                            {
                                L->top.p = ci->top.p;
                            }

                            luaF_close(L, @base, CLOSEKTOP, true);
                            trap = ci->u.l.trap;
                            if (trap != 0)
                            {
                                @base = ci->func.p + 1;
                                ra = @base + GETARG_A(i);
                            }
                        }

                        if (nparams1 != 0) // vararg function?
                        {
                            ci->func.p -= ci->u.l.nextraargs + nparams1;
                        }

                        L->top.p = ra + n; // set call for 'luaD_poscall'
                        luaD_poscall(L, ci, n);
                        trap = ci->u.l.trap; // 'luaD_poscall' can change hooks
                        goto ret;
                    }

                case OpCode.Return0:
                    {
                        if (L->hookmask != 0)
                        {
                            StkId ra = @base + GETARG_A(i);
                            L->top.p = ra;
                            ci->u.l.savedpc = pc;
                            luaD_poscall(L, ci, 0); // no hurry...
                            trap = 1;
                        }
                        else
                        {
                            // do the 'poscall' here
                            int nres = get_nresults(ci->callstatus);
                            L->ci = ci->previous; // back to caller
                            L->top.p = @base - 1;
                            for (; nres > 0; nres--)
                            {
                                setnilvalue(s2v(L->top.p++)); // all results are nil
                            }
                        }

                        goto ret;
                    }

                case OpCode.Return1:
                    if (L->hookmask != 0)
                    {
                        StkId ra = @base + GETARG_A(i);
                        L->top.p = ra + 1;
                        ci->u.l.savedpc = pc;
                        luaD_poscall(L, ci, 1); // no hurry...
                        trap = 1;
                    }
                    else
                    {
                        // do the 'poscall' here
                        int nres = get_nresults(ci->callstatus);
                        L->ci = ci->previous; // back to caller
                        if (nres == 0)
                        {
                            L->top.p = @base - 1; // asked for no results
                        }
                        else
                        {
                            StkId ra = @base + GETARG_A(i);
                            setobjs2s(L, @base - 1, ra); // at least this result
                            L->top.p = @base;
                            for (; nres > 1; nres--)
                            {
                                setnilvalue(s2v(L->top.p++)); // complete missing results
                            }
                        }
                    }

                    goto ret;

                case OpCode.ForLoop:
                    {
                        StkId ra = @base + GETARG_A(i);
                        if (ttisinteger(s2v(ra + 1)))
                        {
                            // integer loop?
                            ulong count = (ulong)ivalue(s2v(ra));
                            if (count > 0)
                            {
                                // still more iterations?
                                long step = ivalue(s2v(ra + 1));
                                long idx = ivalue(s2v(ra + 2)); // control variable
                                chgivalue(s2v(ra), (long)(count - 1)); // update counter
                                idx = idx + step; // add step to index
                                chgivalue(s2v(ra + 2), idx); // update control variable
                                pc -= GETARG_Bx(i); // jump back
                            }
                        }
                        else if (floatforloop(ra)) // float loop
                        {
                            pc -= GETARG_Bx(i); // jump back
                        }

                        trap = ci->u.l.trap; // allows a signal to break the loop
                        break;
                    }

                case OpCode.ForPrep:
                    {
                        StkId ra = @base + GETARG_A(i);
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        if (forprep(L, ra))
                        {
                            pc += GETARG_Bx(i) + 1; // skip the loop
                        }

                        break;
                    }

                case OpCode.TForPrep:
                    {
                        // before: 'ra' has the iterator function, 'ra + 1' has the state,
                        // 'ra + 2' has the initial value for the control variable, and
                        // 'ra + 3' has the closing variable. This opcode then swaps the
                        // control and the closing variables and marks the closing variable
                        // as to-be-closed.
                        StkId ra = @base + GETARG_A(i);
                        TValue temp; // to swap control and closing variables
                        setobj(L, &temp, s2v(ra + 3));
                        setobjs2s(L, ra + 3, ra + 2);
                        setobj2s(L, ra + 2, &temp);
                        // create to-be-closed upvalue (if closing var. is not nil)
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        luaF_newtbcupval(L, ra + 2);
                        pc += GETARG_Bx(i); // go to end of the loop
                        i = *pc++; // fetch next instruction
                        Debug.Assert(GET_OPCODE(i) == OpCode.TForCall && ra == @base + GETARG_A(i));
                        goto l_tforcall;
                    }

                case OpCode.TForCall:
                    goto l_tforcall;

                case OpCode.TForLoop:
                    goto l_tforloop;

                case OpCode.SetList:
                    {
                        StkId ra = @base + GETARG_A(i);
                        uint n = (uint)GETARG_vB(i);
                        uint last = (uint)GETARG_vC(i);
                        Table* h = hvalue(s2v(ra));
                        if (n == 0)
                        {
                            n = (uint)(L->top.p - ra) - 1; // get up to the top
                        }
                        else
                        {
                            L->top.p = ci->top.p; // correct top in case of emergency GC
                        }

                        last += n;
                        if (TESTARG_k(i))
                        {
                            last += (uint)GETARG_Ax(*pc) * (MAXARG_vC + 1);
                            pc++;
                        }

                        // when 'n' is known, table should have proper size
                        if (last > h->asize)
                        {
                            // needs more space?
                            // fixed-size sets should have space preallocated
                            Debug.Assert(GETARG_vB(i) == 0);
                            luaH_resizearray(L, h, last); // preallocate it at once
                        }

                        for (; n > 0; n--)
                        {
                            TValue* val = s2v(ra + n);
                            obj2arr(h, last - 1, val);
                            last--;
                            luaC_barrierback(L, obj2gco(h), val);
                        }

                        break;
                    }

                case OpCode.Closure:
                    {
                        StkId ra = @base + GETARG_A(i);
                        Proto* p = cl->p->p[GETARG_Bx(i)];
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        pushclosure(L, p, cl, @base, ra);

                        if (G(L)->GCdebt <= 0)
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ra + 1;
                            luaC_step(L);
                            trap = ci->u.l.trap;
                        }

#if HARDMEMTESTS
                        if (gcrunning(G(L)))
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ra + 1;
                            luaC_fullgc(L, false);
                            trap = ci->u.l.trap;
                        }
#endif

                        luai_threadyield(L);
                        break;
                    }

                case OpCode.VarArg:
                    {
                        StkId ra = @base + GETARG_A(i);
                        int n = (int)(GETARG_C(i) - 1); // required results (-1 means all)
                        int vatab = GETARG_k(i) ? GETARG_B(i) : -1;
                        ci->u.l.savedpc = pc;
                        L->top.p = ci->top.p;
                        luaT_getvarargs(L, ci, ra, n, vatab);
                        trap = ci->u.l.trap;
                        break;
                    }

                case OpCode.GetVArg:
                    {
                        StkId ra = @base + GETARG_A(i);
                        TValue* rc = s2v(@base + GETARG_C(i));
                        luaT_getvararg(ci, ra, rc);
                        break;
                    }

                case OpCode.ErrNNil:
                    {
                        TValue* ra = s2v(@base + GETARG_A(i));
                        if (!ttisnil(ra))
                        {
                            ci->u.l.savedpc = pc;
                            L->top.p = ci->top.p;
                            luaG_errnnil(L, cl, GETARG_Bx(i));
                        }

                        break;
                    }

                case OpCode.VarArgPrep:
                    {
                        ci->u.l.savedpc = pc;
                        luaT_adjustvarargs(L, ci, cl->p);
                        trap = ci->u.l.trap;
                        if (trap != 0)
                        {
                            // previous "Protect" updated trap
                            luaD_hookcall(L, ci);
                            L->oldpc = 1; // next opcode will be seen as a "new" line
                        }

                        @base = ci->func.p + 1; // function has new base after adjustment
                        break;
                    }

                case OpCode.ExtraArg:
                    throw new InvalidOperationException();
            }

            continue;

            ret: // return from a Lua function
            if ((ci->callstatus & CIST_FRESH) != 0)
            {
                return; // end this frame
            }

            ci = ci->previous;
            goto returning; // continue running caller in this frame
            
            l_tforcall:
            {
                // 'ra' has the iterator function, 'ra + 1' has the state,
                // 'ra + 2' has the closing variable, and 'ra + 3' has the control
                // variable. The call will use the stack starting at 'ra + 3',
                // so that it preserves the first three values, and the first
                // return will be the new value for the control variable.
                StkId ra = @base + GETARG_A(i);
                setobjs2s(L, ra + 5, ra + 3); // copy the control variable
                setobjs2s(L, ra + 4, ra + 1); // copy state
                setobjs2s(L, ra + 3, ra); // copy function
                L->top.p = ra + 3 + 3;
                ci->u.l.savedpc = pc;
                luaD_call(L, ra + 3, (int)GETARG_C(i));
                trap = ci->u.l.trap;
                if (trap != 0)
                {
                    @base = ci->func.p + 1;
                    ra = @base + GETARG_A(i);
                }

                i = *pc++; // go to next instruction
                Debug.Assert(GET_OPCODE(i) == OpCode.TForLoop && ra == @base + GETARG_A(i));
            }
            
            l_tforloop:
            {
                StkId ra = @base + GETARG_A(i);
                if (!ttisnil(s2v(ra + 3))) // continue loop?
                {
                    pc -= GETARG_Bx(i); // jump back
                }
            }
        }
    }
}
