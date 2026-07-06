namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /*
     ** $Id: lvm.c $
     ** Lua virtual machine
     ** See Copyright Notice in lua.h
     */

// /*
// ** By default, use jump tables in the main interpreter loop on gcc
// ** and compatible compilers.
// */
// #if !defined(LUA_USE_JUMPTABLE)
// #if defined(__GNUC__)
// #define LUA_USE_JUMPTABLE	1
// #else
// #define LUA_USE_JUMPTABLE	0
// #endif
// #endif

    /* limit for table tag-method chains (to avoid infinite loops) */
    private const int MAXTAGLOOP = 2000;

// /*
// ** 'l_intfitsf' checks whether a given integer is in the range that
// ** can be converted to a float without rounding. Used in comparisons.
// */
//
// /* number of bits in the mantissa of a float */
// #define NBM		(l_floatatt(MANT_DIG))
//
// /*
// ** Check whether some integers may not fit in a float, testing whether
// ** (maxinteger >> NBM) > 0. (That implies (1 << NBM) <= maxinteger.)
// ** (The shifts are done in parts, to avoid shifting by more than the size
// ** of an integer. In a worst case, NBM == 113 for long double and
// ** sizeof(long) == 32.)
// */
// #if ((((LUA_MAXINTEGER >> (NBM / 4)) >> (NBM / 4)) >> (NBM / 4)) \
// 	>> (NBM - (3 * (NBM / 4))))  >  0
//
// /* limit for integers that fit in a float */
// #define MAXINTFITSF	((lua_Unsigned)1 << NBM)
//
// /* check whether 'i' is in the interval [-MAXINTFITSF, MAXINTFITSF] */
// #define l_intfitsf(i)	((MAXINTFITSF + l_castS2U(i)) <= (2 * MAXINTFITSF))

    /*
     ** Try to convert a value from string to a number value.
     ** If the value is not a string or is a string not representing
     ** a valid numeral (or if coercions from strings to numbers
     ** are disabled via macro 'cvt2num'), do not modify 'result'
     ** and return 0.
     */
    private static bool l_strton(TValue* obj, TValue* result)
    {
        Debug.Assert(obj != result);
        if (!cvt2num(obj)) /* is object not a string? */
        {
            return false;
        }

        TString* st = tsvalue(obj);
        byte* s = getlstr(st, out long stlen);
        return luaO_str2num(s, result) == stlen + 1;
    }

    /*
     ** Try to convert a value to a float. The float case is already handled
     ** by the macro 'tonumber'.
     */
    internal static partial bool luaV_tonumber_(TValue* obj, double* n)
    {
        if (ttisinteger(obj))
        {
            *n = ivalue(obj);
            return true;
        }

        TValue v;
        if (l_strton(obj, &v))
        {
            /* string coercible to number? */
            *n = nvalue(&v); /* convert result of 'luaO_str2num' to a float */
            return true;
        }

        return false; /* conversion failed */
    }

    /*
     ** try to convert a float to an integer, rounding according to 'mode'.
     */
    internal static partial bool luaV_flttointeger(double n, out long p, F2Imod mode)
    {
        double f = Math.Floor(n);
        if (n != f)
        {
            /* not an integral value? */
            if (mode == F2Imod.F2Ieq)
            {
                p = 0;
                return false; /* fails if mode demands integral value */
            }

            if (mode == F2Imod.F2Iceil) /* needs ceiling? */
            {
                f += 1; /* convert floor to ceiling (remember: n != f) */
            }
        }

        return lua_numbertointeger(f, out p);
    }

    /*
     ** try to convert a value to an integer, rounding according to 'mode',
     ** without string coercion.
     ** ("Fast track" handled by macro 'tointegerns'.)
     */
    internal static partial bool luaV_tointegerns(TValue* obj, out long p, F2Imod mode)
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

    /*
     ** try to convert a value to an integer.
     */
    internal static partial bool luaV_tointeger(TValue* obj, out long p, F2Imod mode)
    {
        TValue v;
        if (l_strton(obj, &v)) /* does 'obj' point to a numerical string? */
        {
            obj = &v; /* change it to point to its corresponding number */
        }

        return luaV_tointegerns(obj, out p, mode);
    }

    /*
     ** Try to convert a 'for' limit to an integer, preserving the semantics
     ** of the loop. Return true if the loop must not run; otherwise, '*p'
     ** gets the integer limit.
     ** (The following explanation assumes a positive step; it is valid for
     ** negative steps mutatis mutandis.)
     ** If the limit is an integer or can be converted to an integer,
     ** rounding down, that is the limit.
     ** Otherwise, check whether the limit can be converted to a float. If
     ** the float is too large, clip it to LUA_MAXINTEGER.  If the float
     ** is too negative, the loop should not run, because any initial
     ** integer value is greater than such limit; so, the function returns
     ** true to signal that. (For this latter case, no integer limit would be
     ** correct; even a limit of LUA_MININTEGER would run the loop once for
     ** an initial value equal to LUA_MININTEGER.)
     */
    private static bool forlimit(
        lua_State* L,
        long init,
        TValue* lim,
        out long p,
        long step)
    {
        if (!luaV_tointeger(lim, out p, step < 0 ? F2Imod.F2Iceil : F2Imod.F2Ifloor))
        {
//     /* not coercible to in integer */
//     double flim;  /* try to convert to float */
//     if (!tonumber(lim, &flim)) /* cannot convert to float? */
//       luaG_forerror(L, lim, "limit");
//     /* else 'flim' is a float out of integer bounds */
//     if (luai_numlt(0, flim)) {  /* if it is positive, it is too large */
//       if (step < 0) return 1;  /* initial value must be less than it */
//       *p = LUA_MAXINTEGER;  /* truncate */
//     }
//     else {  /* it is less than min integer */
//       if (step > 0) return 1;  /* initial value must be greater than it */
//       *p = LUA_MININTEGER;  /* truncate */
//     }
            throw new NotImplementedException();
        }

        return step > 0 ? init > p : init < p; /* not to run? */
    }

    /*
     ** Prepare a numerical for loop (opcode OP_FORPREP).
     ** Before execution, stack is as follows:
     **   ra     : initial value
     **   ra + 1 : limit
     **   ra + 2 : step
     ** Return true to skip the loop. Otherwise,
     ** after preparation, stack will be as follows:
     **   ra     : loop counter (integer loops) or limit (float loops)
     **   ra + 1 : step
     **   ra + 2 : control variable
     */
    private static bool forprep(lua_State* L, StkId ra)
    {
        TValue* pinit = s2v(ra);
        TValue* plimit = s2v(ra + 1);
        TValue* pstep = s2v(ra + 2);
        if (ttisinteger(pinit) && ttisinteger(pstep))
        {
            /* integer loop? */
            long init = ivalue(pinit);
            long step = ivalue(pstep);
            if (step == 0)
            {
                luaG_runerror(L, "'for' step is zero");
            }

            if (forlimit(L, init, plimit, out long limit, step))
            {
                return true; /* skip the loop */
            }

            /* prepare loop counter */
            ulong count;
            if (step > 0)
            {
                /* ascending loop? */
                count = (ulong)limit - (ulong)init;
                if (step != 1) /* avoid division in the too common case */
                {
                    count /= (ulong)step;
                }
            }
            else
            {
                /* step < 0; descending loop */
                count = (ulong)init - (ulong)limit;
                /* 'step+1' avoids negating 'mininteger' */
                count /= (ulong)-(step + 1) + 1u;
            }

            /* use 'chgivalue' for places that for sure had integers */
            chgivalue(s2v(ra), (long)count); /* change init to count */
            setivalue(s2v(ra + 1), step); /* change limit to step */
            chgivalue(s2v(ra + 2), init); /* change step to init */
        }
        else
        {
            /* try making all values floats */
//     double init; double limit; double step;
//     if (l_unlikely(!tonumber(plimit, &limit)))
//       luaG_forerror(L, plimit, "limit");
//     if (l_unlikely(!tonumber(pstep, &step)))
//       luaG_forerror(L, pstep, "step");
//     if (l_unlikely(!tonumber(pinit, &init)))
//       luaG_forerror(L, pinit, "initial value");
//     if (step == 0)
//       luaG_runerror(L, "'for' step is zero");
//     if (luai_numlt(0, step) ? luai_numlt(limit, init)
//                             : luai_numlt(init, limit))
//       return 1;  /* skip the loop */
//     else {
//       /* make sure all values are floats */
//       setfltvalue(s2v(ra), limit);
//       setfltvalue(s2v(ra + 1), step);
//       setfltvalue(s2v(ra + 2), init);  /* control variable */
//     }
            throw new NotImplementedException();
        }

        return false;
    }

    /*
    ** Execute a step of a float numerical for loop, returning
    ** true iff the loop must continue. (The integer case is
    ** written online with opcode OP_FORLOOP, for performance.)
    */
    private static bool floatforloop(StkId ra)
    {
//   double step = fltvalue(s2v(ra + 1));
//   double limit = fltvalue(s2v(ra));
//   double idx = fltvalue(s2v(ra + 2));  /* control variable */
//   idx = luai_numadd(L, idx, step);  /* increment index */
//   if (luai_numlt(0, step) ? luai_numle(idx, limit)
//                           : luai_numle(limit, idx)) {
//     chgfltvalue(s2v(ra + 2), idx);  /* update control variable */
//     return 1;  /* jump back */
//   }
//   else
//     return 0;  /* finish the loop */
        throw new NotImplementedException();
    }

    /*
     ** Finish the table access 'val = t[key]' and return the tag of the result.
     */
    internal static partial byte luaV_finishget(lua_State* L, TValue* t, TValue* key, StkId val, byte tag)
    {
        for (int loop = 0; loop < MAXTAGLOOP; loop++)
        {
            TValue* tm; /* metamethod */
            if (tag == LUA_VNOTABLE)
            {
                /* 't' is not a table? */
                Debug.Assert(!ttistable(t));
                tm = luaT_gettmbyobj(L, t, TMS.INDEX);
                if (notm(tm))
                {
                    luaG_typeerror(L, t, "index"); /* no metamethod */
                }

                /* else will try the metamethod */
            }
            else
            {
                /* 't' is a table */
                tm = fasttm(L, hvalue(t)->metatable, TMS.INDEX); /* table's metamethod */
                if (tm == null)
                {
                    /* no metamethod? */
                    setnilvalue(s2v(val)); /* result is nil */
                    return LUA_VNIL;
                }
                /* else will try the metamethod */
            }

            if (ttisfunction(tm))
            {
                /* is metamethod a function? */
                tag = luaT_callTMres(L, tm, t, key, val); /* call it */
                return tag; /* return tag of the result */
            }

            t = tm; /* else try to access 'tm[key]' */
            tag = !ttistable(t) ? LUA_VNOTABLE : luaH_get(hvalue(t), key, s2v(val));

            if (!tagisempty(tag))
            {
                return tag; /* done */
            }
            /* else repeat (tail call 'luaV_finishget') */
        }

        luaG_runerror(L, "'__index' chain too long; possible loop");
        return 0; /* to avoid warnings */
    }

    // /*
    // ** Finish a table assignment 't[key] = val'.
    // ** About anchoring the table before the call to 'luaH_finishset':
    // ** This call may trigger an emergency collection. When loop>0,
    // ** the table being accessed is a field in some metatable. If this
    // ** metatable is weak and the table is not anchored, this collection
    // ** could collect that table while it is being updated.
    // */
    internal static partial void luaV_finishset(lua_State* L, TValue* t, TValue* key, TValue* val, int hres)
    {
        for (int loop = 0; loop < MAXTAGLOOP; loop++)
        {
            TValue* tm; /* '__newindex' metamethod */
            if (hres != HNOTATABLE)
            {
                /* is 't' a table? */
                Table* h = hvalue(t); /* save 't' table */
                tm = fasttm(L, h->metatable, TMS.NEWINDEX); /* get metamethod */
                if (tm == null)
                {
                    /* no metamethod? */
                    sethvalue2s(L, L->top.p, h); /* anchor 't' */
                    L->top.p++; /* assume EXTRA_STACK */
                    luaH_finishset(L, h, key, val, hres); /* set new value */
                    L->top.p--;
                    invalidateTMcache(h);
                    luaC_barrierback(L, obj2gco(h), val);
                    return;
                }
                /* else will try the metamethod */
            }
            else
            {
                /* not a table; check metamethod */
                tm = luaT_gettmbyobj(L, t, TMS.NEWINDEX);
                if (notm(tm))
                {
                    luaG_typeerror(L, t, "index");
                }
            }

            /* try the metamethod */
            if (ttisfunction(tm))
            {
                luaT_callTM(L, tm, t, key, val);
                return;
            }

            t = tm; /* else repeat assignment over 'tm' */
            hres = !ttistable(t) ? HNOTATABLE : luaH_pset(hvalue(t), key, val);
            
            if (hres == HOK)
            {
                luaV_finishfastset(L, t, val);
                return; /* done */
            }
            /* else 'return luaV_finishset(L, t, key, val, slot)' (loop) */
        }

        luaG_runerror(L, "'__newindex' chain too long; possible loop");
    }

// /*
// ** Function to be used for 0-terminated string order comparison
// */
// #if !defined(l_strcoll)
// #define l_strcoll	strcoll
// #endif

    /*
    ** Compare two strings 'ts1' x 'ts2', returning an integer less-equal-
    ** -greater than zero if 'ts1' is less-equal-greater than 'ts2'.
    ** The code is a little tricky because it allows '\0' in the strings
    ** and it uses 'strcoll' (to respect locales) for each segment
    ** of the strings. Note that segments can compare equal but still
    ** have different lengths.
    */
    private static int l_strcmp(TString* ts1, TString* ts2)
    {
        string s1 = getnetstr(ts1);
        string s2 = getnetstr(ts2);
        return string.Compare(s1, s2, StringComparison.CurrentCulture);
        
//         byte* s1 = getlstr(ts1, out long rl1);
//         byte* s2 = getlstr(ts2, out long rl2);
//         while (true)
//         {
//             /* for each segment */
// //     int temp = l_strcoll(s1, s2);
// //     if (temp != 0)  /* not equal? */
// //       return temp;  /* done */
// //     else {  /* strings are equal up to a '\0' */
// //       size_t zl1 = strlen(s1);  /* index of first '\0' in 's1' */
// //       size_t zl2 = strlen(s2);  /* index of first '\0' in 's2' */
// //       if (zl2 == rl2)  /* 's2' is finished? */
// //         return (zl1 == rl1) ? 0 : 1;  /* check 's1' */
// //       else if (zl1 == rl1)  /* 's1' is finished? */
// //         return -1;  /* 's1' is less than 's2' ('s2' is not finished) */
// //       /* both strings longer than 'zl'; go on comparing after the '\0' */
// //       zl1++; zl2++;
// //       s1 += zl1; rl1 -= zl1; s2 += zl2; rl2 -= zl2;
// //     }
//             throw new NotImplementedException();
//         }
    }

    /*
     ** Check whether integer 'i' is less than float 'f'. If 'i' has an
     ** exact representation as a float ('l_intfitsf'), compare numbers as
     ** floats. Otherwise, use the equivalence 'i < f <=> i < ceil(f)'.
     ** If 'ceil(f)' is out of integer range, either 'f' is greater than
     ** all integers or less than all integers.
     ** (The test with 'l_intfitsf' is only for performance; the else
     ** case is correct for all values, but it is slow due to the conversion
     ** from float to int.)
     ** When 'f' is NaN, comparisons must result in false.
     */
    private static bool LTintfloat(long i, double f)
    {
   // if (l_intfitsf(i))
   //   return luai_numlt(cast_num(i), f);  /* compare them as floats */
//   else {  /* i < f <=> i < ceil(f) */
//     long fi;
//     if (luaV_flttointeger(f, &fi, F2Iceil))  /* fi = ceil(f) */
//       return i < fi;   /* compare them as integers */
//     else  /* 'f' is either greater or less than all integers */
//       return f > 0;  /* greater? */
//   }
        throw new NotImplementedException();
    }

    /*
    ** Check whether integer 'i' is less than or equal to float 'f'.
    ** See comments on previous function.
    */
    private static bool LEintfloat(long i, double f)
    {
//   if (l_intfitsf(i))
//     return luai_numle(cast_num(i), f);  /* compare them as floats */
//   else {  /* i <= f <=> i <= floor(f) */
//     long fi;
//     if (luaV_flttointeger(f, &fi, F2Ifloor))  /* fi = floor(f) */
//       return i <= fi;   /* compare them as integers */
//     else  /* 'f' is either greater or less than all integers */
//       return f > 0;  /* greater? */
//   }
        throw new NotImplementedException();
    }

    /*
     ** Check whether float 'f' is less than integer 'i'.
     ** See comments on previous function.
     */
    private static bool LTfloatint(double f, long i)
    {
//   if (l_intfitsf(i))
//     return luai_numlt(f, cast_num(i));  /* compare them as floats */
//   else {  /* f < i <=> floor(f) < i */
//     long fi;
//     if (luaV_flttointeger(f, &fi, F2Ifloor))  /* fi = floor(f) */
//       return fi < i;   /* compare them as integers */
//     else  /* 'f' is either greater or less than all integers */
//       return f < 0;  /* less? */
//   }
        throw new NotImplementedException();
    }
    
    /*
    ** Check whether float 'f' is less than or equal to integer 'i'.
    ** See comments on previous function.
    */
    private static bool LEfloatint(double f, long i)
    {
//   if (l_intfitsf(i))
//     return luai_numle(f, cast_num(i));  /* compare them as floats */
//   else {  /* f <= i <=> ceil(f) <= i */
//     long fi;
//     if (luaV_flttointeger(f, &fi, F2Iceil))  /* fi = ceil(f) */
//       return fi <= i;   /* compare them as integers */
//     else  /* 'f' is either greater or less than all integers */
//       return f < 0;  /* less? */
//   }
        throw new NotImplementedException();
    }

    /*
    ** Return 'l < r', for numbers.
    */
    private static bool LTnum(TValue* l, TValue* r)
    {
        Debug.Assert(ttisnumber(l) && ttisnumber(r));
        if (ttisinteger(l))
        {
            long li = ivalue(l);
            if (ttisinteger(r))
            {
                return li < ivalue(r); /* both are integers */
            }

            /* 'l' is int and 'r' is float */
            return LTintfloat(li, fltvalue(r)); /* l < r ? */
        }

        double lf = fltvalue(l); /* 'l' must be float */
        if (ttisfloat(r))
        {
            return lf < fltvalue(r); /* both are float */
        }

        /* 'l' is float and 'r' is int */
        return LTfloatint(lf, ivalue(r));
    }

    /*
     ** Return 'l <= r', for numbers.
     */
    private static bool LEnum(TValue* l, TValue* r)
    {
        Debug.Assert(ttisnumber(l) && ttisnumber(r));
        if (ttisinteger(l))
        {
            long li = ivalue(l);
            if (ttisinteger(r))
            {
                return li <= ivalue(r); /* both are integers */
            }

            // 'l' is int and 'r' is float 
            return LEintfloat(li, fltvalue(r)); /* l <= r ? */
        }

        double lf = fltvalue(l); /* 'l' must be float */
        if (ttisfloat(r))
        {
            return lf <= fltvalue(r); /* both are float */
        }

        // 'l' is float and 'r' is int 
        return LEfloatint(lf, ivalue(r));
    }

    /*
     ** return 'l < r' for non-numbers.
     */
    private static bool lessthanothers(lua_State* L, TValue* l, TValue* r)
    {
        Debug.Assert(!ttisnumber(l) || !ttisnumber(r));
        if (ttisstring(l) && ttisstring(r)) /* both are strings? */
        {
            return l_strcmp(tsvalue(l), tsvalue(r)) < 0;
        }

        return luaT_callorderTM(L, l, r, TMS.LT);
    }

    /*
     ** Main operation less than; return 'l < r'.
     */
    internal static partial bool luaV_lessthan(lua_State* L, TValue* l, TValue* r)
    {
        if (ttisnumber(l) && ttisnumber(r)) /* both operands are numbers? */
        {
            return LTnum(l, r);
        }

        return lessthanothers(L, l, r);
    }

    /*
     ** return 'l <= r' for non-numbers.
     */
    private static bool lessequalothers(lua_State* L, TValue* l, TValue* r)
    {
//   Debug.Assert(!ttisnumber(l) || !ttisnumber(r));
//   if (ttisstring(l) && ttisstring(r))  /* both are strings? */
//     return l_strcmp(tsvalue(l), tsvalue(r)) <= 0;
//   else
//     return luaT_callorderTM(L, l, r, TM_LE);
        throw new NotImplementedException();
    }

    /*
     ** Main operation less than or equal to; return 'l <= r'.
     */
    internal static partial bool luaV_lessequal(lua_State* L, TValue* l, TValue* r)
    {
        if (ttisnumber(l) && ttisnumber(r)) /* both operands are numbers? */
        {
            return LEnum(l, r);
        }

        return lessequalothers(L, l, r);
    }

    /*
     ** Main operation for equality of Lua values; return 't1 == t2'.
     ** L == null means raw equality (no metamethods)
     */
    internal static partial bool luaV_equalobj(lua_State* L, TValue* t1, TValue* t2)
    {
        if (ttype(t1) != ttype(t2)) /* not the same type? */
        {
            return false;
        }

        if (ttypetag(t1) != ttypetag(t2))
        {
            switch (ttypetag(t1))
            {
                case LUA_VNUMINT:
                    {
                        /* integer == float? */
                        /* integer and float can only be equal if float has an integer
                           value equal to the integer */
                        return (luaV_flttointeger(fltvalue(t2), out long i2, F2Imod.F2Ieq) &&
                                ivalue(t1) == i2);
                    }

                case LUA_VNUMFLT:
                    {
                        /* float == integer? */
                        return (luaV_flttointeger(fltvalue(t1), out long i1, F2Imod.F2Ieq) &&
                                i1 == ivalue(t2));
                    }

                case LUA_VSHRSTR:
                case LUA_VLNGSTR:
                    /* compare two strings with different variants: they can be
                       equal when one string is a short string and the other is
                       an external string  */
                    return luaS_eqstr(tsvalue(t1), tsvalue(t2));

                default:
                    /* only numbers (integer/float) and strings (long/short) can have
                       equal values with different variants */
                    return false;
            }
        }

        /* equal variants */
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

                break; /* will try TM */

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

                    break; /* will try TM */
                }

            case LUA_VLCF:
                return fvalue(t1) == fvalue(t2);

            default: /* functions and threads */
                return gcvalue(t1) == gcvalue(t2);
        }

        if (tm == null) /* no TM? */
        {
            return false; /* objects are different */
        }

//       int tag = luaT_callTMres(L, tm, t1, t2, L->top.p);  /* call TM */
//       return !tagisfalse(tag);
        throw new NotImplementedException();
    }

    /* macro used by 'luaV_concat' to ensure that element at 'o' is a string */
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

    /*
     ** Check whether object is a short empty string to optimise concatenation.
     ** (External strings can be empty too; they will be concatenated like
     ** non-empty ones.)
     */
    private static bool isemptystr(TValue* o)
    {
        return ttisshrstring(o) && tsvalue(o)->shrlen == 0;
    }

    /* copy strings in stack from top - n up to top - 1 to buffer */
    private static void copy2buff(StkId top, int n, byte* buff)
    {
        long tl = 0; /* size already copied */
        do
        {
            TString* st = tsvalue(s2v(top - n));
            byte* s = getlstr(st, out long l);
            memcpy(buff + tl, s, l);
            tl += l;
        } while (--n > 0);
    }

    /*
     ** Main operation for concatenation: concat 'total' values in the stack,
     ** from 'L->top.p - total' up to 'L->top.p - 1'.
     */
    internal static partial void luaV_concat(lua_State* L, int total)
    {
        if (total == 1)
        {
            return; /* "all" values already concatenated */
        }

        byte* buff = stackalloc byte[LUAI_MAXSHORTLEN];
        
        do
        {
            StkId top = L->top.p;
            int n = 2; /* number of elements handled in this pass (at least 2) */
            if (!(ttisstring(s2v(top - 2)) || cvt2str(s2v(top - 2))) ||
                !tostring(L, s2v(top - 1)))
            {
                luaT_tryconcatTM(L); /* may invalidate 'top' */
            }
            else if (isemptystr(s2v(top - 1))) /* second operand is empty? */
            {
                tostring(L, s2v(top - 2)); /* result is first operand */
            }
            else if (isemptystr(s2v(top - 2)))
            {
                /* first operand is empty string? */
                setobjs2s(L, top - 2, top - 1); /* result is second op. */
            }
            else
            {
                /* at least two string values; get as many as possible */
                long tl = tsslen(tsvalue(s2v(top - 1))); /* total length */
                /* collect total length and number of strings */
                for (n = 1; n < total && tostring(L, s2v(top - n - 1)); n++)
                {
                    long l = tsslen(tsvalue(s2v(top - n - 1)));
                    if (l >= nint.MaxValue - sizeof(TString) - tl)
                    {
                        L->top.p = top - total; /* pop strings to avoid wasting stack */
                        luaG_runerror(L, "string length overflow");
                    }

                    tl += l;
                }

                TString* ts;
                if (tl <= LUAI_MAXSHORTLEN)
                {
                    /* is result a short string? */
                    copy2buff(top, n, buff);  /* copy strings to buffer */
                    ts = luaS_newlstr(L, buff, (int)tl);
                }
                else
                {
                    /* long string; copy strings directly to final result */
                    ts = luaS_createlngstrobj(L, tl);
                    copy2buff(top, n, getlngstr(ts));
                }

                setsvalue2s(L, top - n, ts); /* create result */
            }

            total -= n - 1; /* got 'n' strings to create one new */
            L->top.p -= n - 1; /* popped 'n' strings and pushed one */
        } while (total > 1); /* repeat until only 1 result left */
    }

    /*
     ** Main operation 'ra = #rb'.
     */
    internal static partial void luaV_objlen(lua_State* L, StkId ra, TValue* rb)
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
                        break; /* metamethod? break switch to call it */
                    }

                    setivalue(s2v(ra), (long)(luaH_getn(L, h))); /* else primitive len */
                    return;
                }

            case LUA_VSHRSTR:
                setivalue(s2v(ra), tsvalue(rb)->shrlen);
                return;

            case LUA_VLNGSTR:
                setivalue(s2v(ra), tsvalue(rb)->u.lnglen);
                return;

            default:
                /* try metamethod */
                tm = luaT_gettmbyobj(L, rb, TMS.LEN);
                if (notm(tm)) /* no metamethod? */
                {
                    luaG_typeerror(L, rb, "get length of");
                }

                break;
        }

        luaT_callTMres(L, tm, rb, rb, ra);
    }

    /*
     ** Integer division; return 'm // n', that is, floor(m/n).
     ** C division truncates its result (rounds towards zero).
     ** 'floor(q) == trunc(q)' when 'q >= 0' or when 'q' is integer,
     ** otherwise 'floor(q) == trunc(q) - 1'.
     */
    internal static partial long luaV_idiv(lua_State* L, long x, long y)
    {
        if ((ulong)y + 1u <= 1u)
        {
            /* special cases: -1 or 0 */
            if (y == 0)
            {
                luaG_runerror(L, "attempt to divide by zero");
            }

            return -x; /* n==-1; avoid overflow with 0x80000...//-1 */
        }

        long q = x / y; /* perform C division */
        if ((x ^ y) < 0 && x % y != 0) /* 'm/n' would be negative non-integer? */
        {
            q -= 1; /* correct result for different rounding */
        }

        return q;
    }

    /*
     ** Integer modulus; return 'm % n'. (Assume that C '%' with
     ** negative operands follows C99 behaviour. See previous comment
     ** about luaV_idiv.)
     */
    internal static partial long luaV_mod(lua_State* L, long x, long y)
    {
        if ((ulong)y + 1u <= 1u)
        {
            /* special cases: -1 or 0 */
            if (y == 0)
            {
                luaG_runerror(L, "attempt to perform 'n%%0'");
            }

            return 0; /* m % -1 == 0; avoid overflow with 0x80000...%-1 */
        }

        long r = x % y;
        if (r != 0 && (r ^ y) < 0) /* 'm/n' would be non-integer negative? */
        {
            r += y; /* correct result for different rounding */
        }

        return r;
    }

    /*
     ** Float modulus
     */
    internal static partial double luaV_modf(lua_State* L, double x, double y)
    {
        double r = x % y;
        if (r > 0 ? y < 0 : r < 0 && y > 0)
        {
            r += y;
        }

        return r;
    }

// /* number of bits in an integer */
// #define NBITS	l_numbits(long)

    /*
     ** Shift left operation. (Shift right just negates 'y'.)
     */
    internal static partial long luaV_shiftl(long x, long y)
    {
        if (y < 0)
        {
            /* shift right? */
            if (y <= -64)
            {
                return 0;
            }

            return x >>> (int)-y;
        }

        /* shift left */
        if (y >= 64)
        {
            return 0;
        }

        return x << (int)y;
    }

    /*
     ** create a new Lua closure, push it in the stack, and initialise
     ** its upvalues.
     */
    private static void pushclosure(
        lua_State* L,
        Proto* p,
        UpVal** encup,
        StkId @base,
        StkId ra)
    {
        int nup = p->sizeupvalues;
        Upvaldesc* uv = p->upvalues;
        LClosure* ncl = luaF_newLclosure(L, nup);
        ncl->p = p;
        setclLvalue2s(L, ra, ncl); /* anchor new closure in stack */
        for (int i = 0; i < nup; i++)
        {
            /* fill in its upvalues */
            if (uv[i].instack != 0) /* upvalue refers to local variable? */
            {
                (&ncl->upvals)[i] = luaF_findupval(L, @base + uv[i].idx);
            }
            else /* get upvalue from enclosing function */
            {
                (&ncl->upvals)[i] = encup[uv[i].idx];
            }

            luaC_objbarrier(L, (GCObject*)ncl, (GCObject*)(&ncl->upvals)[i]);
        }
    }

    /*
     ** finish execution of an opcode interrupted by a yield
     */
    private static partial void luaV_finishOp(lua_State* L)
    {
        CallInfo* ci = L->ci;
        StkId @base = ci->func.p + 1;
        uint inst = *(ci->u.l.savedpc - 1); /* interrupted instruction */
        OpCode op = GET_OPCODE(inst);
        switch (op)
        {
            /* finish its execution */
            case OpCode.OP_MMBIN:
            case OpCode.OP_MMBINI:
            case OpCode.OP_MMBINK:
                {
//       setobjs2s(L, base + GETARG_A(*(ci->u.l.savedpc - 2)), --L->top.p);
//       break;
                    throw new NotImplementedException();
                }
            case OpCode.OP_UNM:
            case OpCode.OP_BNOT:
            case OpCode.OP_LEN:
            case OpCode.OP_GETTABUP:
            case OpCode.OP_GETTABLE:
            case OpCode.OP_GETI:
            case OpCode.OP_GETFIELD:
            case OpCode.OP_SELF:
                {
//       setobjs2s(L, base + GETARG_A(inst), --L->top.p);
//       break;
                    throw new NotImplementedException();
                }
            case OpCode.OP_LT:
            case OpCode.OP_LE:
            case OpCode.OP_LTI:
            case OpCode.OP_LEI:
            case OpCode.OP_GTI:
            case OpCode.OP_GEI:
            case OpCode.OP_EQ:
                {
                    /* note that 'OP_EQI'/'OP_EQK' cannot yield */
//       int res = !l_isfalse(s2v(L->top.p - 1));
//       L->top.p--;
//       Debug.Assert(GET_OPCODE(*ci->u.l.savedpc) == OP_JMP);
//       if (res != GETARG_k(inst))  /* condition failed? */
//         ci->u.l.savedpc++;  /* skip jump instruction */
//       break;
                    throw new NotImplementedException();
                }
            case OpCode.OP_CONCAT:
                {
//       StkId top = L->top.p - 1;  /* top when 'luaT_tryconcatTM' was called */
//       int a = GETARG_A(inst);      /* first element to concatenate */
//       int total = cast_int(top - 1 - (base + a));  /* yet to concatenate */
//       setobjs2s(L, top - 2, top);  /* put TM result in proper position */
//       L->top.p = top - 1;  /* top is one after last element (at top-2) */
//       luaV_concat(L, total);  /* concat them (may yield again) */
//       break;
                    throw new NotImplementedException();
                }
            case OpCode.OP_CLOSE:
                {
                    /* yielded closing variables */
//       ci->u.l.savedpc--;  /* repeat instruction to close other vars. */
//       break;
                    throw new NotImplementedException();
                }
            case OpCode.OP_RETURN:
                {
                    /* yielded closing variables */
//       StkId ra = base + GETARG_A(inst);
//       /* adjust top to signal correct number of returns, in case the
//          return is "up to top" ('isIT') */
//       L->top.p = ra + ci->u2.nres;
//       /* repeat instruction to close other vars. and complete the return */
//       ci->u.l.savedpc--;
//       break;
                    throw new NotImplementedException();
                }
            default:
                /* only these other opcodes can yield */
                Debug.Assert(
                    op is OpCode.OP_TFORCALL
                        or OpCode.OP_CALL
                        or OpCode.OP_TAILCALL
                        or OpCode.OP_SETTABUP
                        or OpCode.OP_SETTABLE
                        or OpCode.OP_SETI
                        or OpCode.OP_SETFIELD);
                throw new NotImplementedException();
                break;
        }
    }

// /*
// ** {==================================================================
// ** Macros for arithmetic/bitwise/comparison opcodes in 'luaV_execute'
// **
// ** All these macros are to be used exclusively inside the main
// ** iterpreter loop (function luaV_execute) and may access directly
// ** the local variables of that function (L, i, pc, ci, etc.).
// ** ===================================================================
// */
//
// #define l_addi(L,a,b)	intop(+, a, b)
// #define l_subi(L,a,b)	intop(-, a, b)
// #define l_muli(L,a,b)	intop(*, a, b)
// #define l_band(a,b)	intop(&, a, b)
// #define l_bor(a,b)	intop(|, a, b)
// #define l_bxor(a,b)	intop(^, a, b)
//
// #define l_lti(a,b)	(a < b)
// #define l_lei(a,b)	(a <= b)
// #define l_gti(a,b)	(a > b)
// #define l_gei(a,b)	(a >= b)
//
// /*
// ** Bitwise operations with constant operand.
// */
// #define op_bitwiseK(L,op) {  \
//   TValue *v1 = s2v(@base + GETARG_B(i));  \
//   TValue *v2 = KC(i);  \
//   long i1;  \
//   long i2 = ivalue(v2);  \
//   if (tointegerns(v1, &i1)) {  \
//     StkId ra = @base + GETARG_A(i); \
//     pc++; setivalue(s2v(ra), op(i1, i2));  \
//   }}
//
//
// /*
// ** Bitwise operations with register operands.
// */
// #define op_bitwise(L,op) {  \
//   TValue *v1 = s2v(@base + GETARG_B(i));  \
//   TValue *v2 = s2v(@base + GETARG_C(i));  \
//   long i1; long i2;  \
//   if (tointegerns(v1, &i1) && tointegerns(v2, &i2)) {  \
//     StkId ra = @base + GETARG_A(i); \
//     pc++; setivalue(s2v(ra), op(i1, i2));  \
//   }}

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Function 'luaV_execute': main interpreter loop
     ** ===================================================================
     */

    /*
     ** some macros for common tasks in 'luaV_execute'
     */

    private static StkId RA(ref ExecuteState state)
    {
        return state.@base + GETARG_A(state.i);
    }

    private static StkId RA(ref ExecuteState state, uint i)
    {
        return state.@base + GETARG_A(i);
    }

    private static TValue* vRA(ref ExecuteState state)
    {
        return s2v(RA(ref state));
    }

    private static StkId RB(ref ExecuteState state)
    {
        return state.@base + GETARG_B(state.i);
    }

    private static TValue* vRB(ref ExecuteState state)
    {
        return s2v(RB(ref state));
    }

    private static TValue* KB(ref ExecuteState state)
    {
        return state.k + GETARG_B(state.i);
    }

    private static StkId RC(ref ExecuteState state)
    {
        return state.@base + GETARG_C(state.i);
    }

    private static TValue* vRC(ref ExecuteState state)
    {
        return s2v(RC(ref state));
    }

    private static TValue* KC(ref ExecuteState state)
    {
        return state.k + GETARG_C(state.i);
    }

    private static TValue* RKC(ref ExecuteState state)
    {
        return TESTARG_k(state.i) ? state.k + GETARG_C(state.i) : s2v(state.@base + GETARG_C(state.i));
    }

    private static void updatetrap(ref ExecuteState state)
    {
        state.trap = state.ci->u.l.trap;
    }

    private static void updatebase(ref ExecuteState state)
    {
        state.@base = state.ci->func.p + 1;
    }

    private static void updatestack(ref ExecuteState state, ref StkId ra)
    {
        if (state.trap != 0)
        {
            updatebase(ref state);
            ra = state.@base + GETARG_A(state.i);
        }
    }

    /*
     ** Execute a jump instruction. The 'updatetrap' allows signals to stop
     ** tight loops. (Without it, the local copy of 'trap' could never change.)
     */
    private static void dojump(ref ExecuteState state, uint i, int e)
    {
        state.pc += GETARG_sJ(i) + e;
        updatetrap(ref state);
    }

    /* for test instructions, execute the jump instruction that follows it */
    private static void donextjump(ref ExecuteState state)
    {
        uint ni = *state.pc;
        dojump(ref state, ni, 1);
    }

    /*
     ** do a conditional jump: skip next instruction if 'cond' is not what
     ** was expected (parameter 'k'), else do next instruction, which must
     ** be a jump.
     */
    private static void docondjump(ref ExecuteState state)
    {
        if (state.cond != GETARG_k(state.i))
        {
            state.pc++;
        }
        else
        {
            donextjump(ref state);
        }
    }

    /*
     ** Correct global 'pc'.
     */
    private static void savepc(ref ExecuteState state)
    {
        state.ci->u.l.savedpc = state.pc;
    }

    /*
     ** Whenever code can raise errors, the global 'pc' and the global
     ** 'top' must be correct to report occasional errors.
     */
    private static void savestate(ref ExecuteState state)
    {
        state.ci->u.l.savedpc = state.pc;
        state.L->top.p = state.ci->top.p;
    }

    /*
     ** Protect code that, in general, can raise errors, reallocate the
     ** stack, and change the hooks.
     */
    private static void Protect(ref ExecuteState state, Execute exp)
    {
        savestate(ref state);
        exp(ref state);
        updatetrap(ref state);
    }

    private static void Protect(ref ExecuteState state, Action exp)
    {
        savestate(ref state);
        exp();
        updatetrap(ref state);
    }

    private delegate void Execute(ref ExecuteState state);

    /* special version that does not change the top */
    private static void ProtectNT(ref ExecuteState state, Execute exp)
    {
        savepc(ref state);
        exp(ref state);
        updatetrap(ref state);
    }

    private static void ProtectNT(ref ExecuteState state, Action exp)
    {
        savepc(ref state);
        exp();
        updatetrap(ref state);
    }

    /*
     ** Protect code that can only raise errors. (That is, it cannot change
     ** the stack or hooks.)
     */
    private static void halfProtect(ref ExecuteState state, Execute exp)
    {
        savestate(ref state);
        exp(ref state);
    }

    private static void halfProtect(ref ExecuteState state, Action exp)
    {
        savestate(ref state);
        exp();
    }

    /*
     ** macro executed during Lua functions at points where the
     ** function can yield.
     */
    private static void luai_threadyield(lua_State* L)
    {
        lua_unlock(L);
        lua_lock(L);
    }

    /* 'c' is the limit of live values in the stack */
    private static void checkGC(ref ExecuteState state, StkId c)
    {
        if (G(state.L)->GCdebt <= 0)
        {
            state.ci->u.l.savedpc = state.pc;
            state.L->top.p = c;
            luaC_step(state.L);
            updatetrap(ref state);
        }

#if HARDMEMTESTS
        if (gcrunning(G(state.L)))
        {
            state.ci->u.l.savedpc = state.pc;
            state.L->top.p = c;
            luaC_fullgc(state.L, false);
            updatetrap(ref state);
        }
#endif

        luai_threadyield(state.L);
    }

    private static void vmfetch(ref ExecuteState state)
    {
        if (state.trap != 0)
        {
            /* stack reallocation or hooks? */
            state.trap = (byte)(luaG_traceexec(state.L, state.pc) ? 1 : 0); /* handle hooks */
            updatebase(ref state); /* correct stack */
        }

        state.i = *(state.pc++);
    }

    private struct ExecuteState
    {
        public lua_State* L;
        public CallInfo* ci;
        public LClosure* cl;
        public TValue* k;
        public StkId @base;
        public uint* pc;
        public byte trap;
        public uint i; /* instruction being executed */
        public bool cond;
    }

    private static partial void luaV_execute(lua_State* L, CallInfo* cix)
    {
        ExecuteState state = default;
        state.L = L;
        state.ci = cix;
        startfunc:
        state.trap = L->hookmask;
        returning: /* trap already set */
        state.cl = ci_func(state.ci);
        state.k = state.cl->p->k;
        state.pc = state.ci->u.l.savedpc;
        if (state.trap != 0)
        {
            state.trap = (byte)(luaG_tracecall(L) ? 1 : 0);
        }

        state.@base = state.ci->func.p + 1;
        /* main loop of interpreter */
        while (true)
        {
            vmfetch(ref state);
#if true
            {
                /* low-level line tracing for debugging Lua */
                int pcrel = pcRel(state.pc, state.cl->p);
                Console.WriteLine(
                    "line: {0}; {1} ({2})",
                    luaG_getfuncline(state.cl->p, pcrel),
                    opnames[(int)GET_OPCODE(state.i)],
                    pcrel);
            }
#endif
            Debug.Assert(state.@base == state.ci->func.p + 1);
            Debug.Assert(state.@base <= L->top.p && L->top.p <= L->stack_last.p);
            /* for tests, invalidate top for instructions not expecting it */
            if (luaP_isIT(state.i))
            {
                L->top.p = state.@base;
            }

            switch (GET_OPCODE(state.i))
            {
                case OpCode.OP_MOVE:
                    {
                        StkId ra = RA(ref state);
                        setobjs2s(L, ra, RB(ref state));
                        break;
                    }

                case OpCode.OP_LOADI:
                    {
                        StkId ra = RA(ref state);
                        int b = GETARG_sBx(state.i);
                        setivalue(s2v(ra), b);
                        break;
                    }

                case OpCode.OP_LOADF:
                    {
                        StkId ra = RA(ref state);
                        int b = GETARG_sBx(state.i);
                        setfltvalue(s2v(ra), b);
                        break;
                    }

                case OpCode.OP_LOADK:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = state.k + GETARG_Bx(state.i);
                        setobj2s(L, ra, rb);
                        break;
                    }

                case OpCode.OP_LOADKX:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = state.k + GETARG_Ax(*state.pc);
                        state.pc++;
                        setobj2s(L, ra, rb);
                        break;
                    }

                case OpCode.OP_LOADFALSE:
                    {
                        StkId ra = RA(ref state);
                        setbfvalue(s2v(ra));
                        break;
                    }

                case OpCode.OP_LFALSESKIP:
                    {
                        StkId ra = RA(ref state);
                        setbfvalue(s2v(ra));
                        state.pc++; /* skip next instruction */
                        break;
                    }

                case OpCode.OP_LOADTRUE:
                    {
                        StkId ra = RA(ref state);
                        setbtvalue(s2v(ra));
                        break;
                    }

                case OpCode.OP_LOADNIL:
                    {
                        StkId ra = RA(ref state);
                        int b = GETARG_B(state.i);
                        do
                        {
                            setnilvalue(s2v(ra++));
                        } while (b-- != 0);

                        break;
                    }

                case OpCode.OP_GETUPVAL:
                    {
                        StkId ra = RA(ref state);
                        int b = GETARG_B(state.i);
                        setobj2s(L, ra, (&state.cl->upvals)[b]->v.p);
                        break;
                    }

                case OpCode.OP_SETUPVAL:
                    {
                        StkId ra = RA(ref state);
                        UpVal* uv = (&state.cl->upvals)[GETARG_B(state.i)];
                        setobj(L, uv->v.p, s2v(ra));
                        luaC_barrier(L, (GCObject*)uv, s2v(ra));
                        break;
                    }

                case OpCode.OP_GETTABUP:
                    {
                        StkId ra = RA(ref state);
                        TValue* upval = (&state.cl->upvals)[GETARG_B(state.i)]->v.p;
                        TValue* rc = KC(ref state);
                        TString* key = tsvalue(rc); /* key must be a short string */
                        byte tag = !ttistable(upval) ? LUA_VNOTABLE : luaH_getshortstr(hvalue(upval), key, s2v(ra));
                        if (tagisempty(tag))
                        {
                            Protect(ref state, () => luaV_finishget(L, upval, rc, ra, tag));
                        }

                        break;
                    }

                case OpCode.OP_GETTABLE:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        TValue* rc = vRC(ref state);
                        byte tag;
                        if (ttisinteger(rc))
                        {
                            /* fast track for integers? */
                            luaV_fastgeti(rb, ivalue(rc), s2v(ra), out tag);
                        }
                        else
                        {
                            tag = !ttistable(rb) ? LUA_VNOTABLE : luaH_get(hvalue(rb), rc, s2v(ra));
                        }

                        if (tagisempty(tag))
                        {
                            Protect(ref state, () => luaV_finishget(L, rb, rc, ra, tag));
                        }

                        break;
                    }

                case OpCode.OP_GETI:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        int c = (int)GETARG_C(state.i);
                        luaV_fastgeti(rb, c, s2v(ra), out byte tag);
                        if (tagisempty(tag))
                        {
                            TValue key;
                            TValue* keyPtr = &key;
                            setivalue(keyPtr, c);
                            Protect(ref state, () => luaV_finishget(L, rb, keyPtr, ra, tag));
                        }

                        break;
                    }

                case OpCode.OP_GETFIELD:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        TValue* rc = KC(ref state);
                        TString* key = tsvalue(rc); /* key must be a short string */
                        byte tag = !ttistable(rb) ? LUA_VNOTABLE : luaH_getshortstr(hvalue(rb), key, s2v(ra));
                        if (tagisempty(tag))
                        {
                            Protect(ref state, () => luaV_finishget(L, rb, rc, ra, tag));
                        }

                        break;
                    }

                case OpCode.OP_SETTABUP:
                    {
                        // int hres;
                        TValue* upval = (&state.cl->upvals)[GETARG_A(state.i)]->v.p;
                        TValue* rb = KB(ref state);
                        TValue* rc = RKC(ref state);
                        TString* key = tsvalue(rb); /* key must be a short string */
                        // luaV_fastset(upval, key, rc, hres, luaH_psetshortstr);
                        // if (hres == HOK)
                        //     luaV_finishfastset(L, upval, rc);
                        // else
                        //     Protect(luaV_finishset(L, upval, rb, rc, hres));
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_SETTABLE:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state); /* key (table is in 'ra') */
                        TValue* rc = RKC(ref state); /* value */

                        int hres;
                        if (ttisinteger(rb))
                        {
                            /* fast track for integers? */
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
                            Protect(ref state, () => luaV_finishset(L, s2v(ra), rb, rc, hres));
                        }

                        break;
                    }

                case OpCode.OP_SETI:
                    {
                        StkId ra = RA(ref state);
                        int b = GETARG_B(state.i);
                        TValue* rc = RKC(ref state);
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
                            Protect(ref state, () => luaV_finishset(L, s2v(ra), keyPtr, rc, hres));
                        }

                        break;
                    }
                case OpCode.OP_SETFIELD:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = KB(ref state);
                        TValue* rc = RKC(ref state);
                        TString* key = tsvalue(rb); /* key must be a short string */
                        int hres = !ttistable(s2v(ra)) ? HNOTATABLE : luaH_psetshortstr(hvalue(s2v(ra)), key, rc);
                        if (hres == HOK)
                        {
                            luaV_finishfastset(L, s2v(ra), rc);
                        }
                        else
                        {
                            Protect(ref state, () => luaV_finishset(L, s2v(ra), rb, rc, hres));
                        }

                        break;
                    }

                case OpCode.OP_NEWTABLE:
                    {
                        StkId ra = RA(ref state);
                        uint b = (uint)GETARG_vB(state.i); /* log2(hash size) + 1 */
                        uint c = (uint)GETARG_vC(state.i); /* array size */
                        if (b > 0)
                        {
                            b = 1u << (int)(b - 1); /* hash size is 2^(b - 1) */
                        }

                        if (TESTARG_k(state.i))
                        {
                            /* non-zero extra argument? */
                            Debug.Assert(GETARG_Ax(*state.pc) != 0);
                            /* add it to array size */
                            c += (uint)GETARG_Ax(*state.pc) * (MAXARG_vC + 1);
                        }

                        state.pc++; /* skip extra argument */
                        L->top.p = ra + 1; /* correct top in case of emergency GC */
                        Table* t = luaH_new(L) /* memory allocation */;
                        sethvalue2s(L, ra, t);
                        if (b != 0 || c != 0)
                        {
                            luaH_resize(L, t, c, b); /* idem */
                        }

                        checkGC(ref state, ra + 1);
                        break;
                    }

                case OpCode.OP_SELF:
                    {
                        StkId ra = RA(ref state);
                        // lu_byte tag;
                        TValue* rb = vRB(ref state);
                        TValue* rc = KC(ref state);
                        TString* key = tsvalue(rc); /* key must be a short string */
                        setobj2s(L, ra + 1, rb);
                        // luaV_fastget(rb, key, s2v(ra), luaH_getshortstr, tag);
                        // if (tagisempty(tag))
                        //     Protect(luaV_finishget(L, rb, rc, ra, tag));
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_ADDI:
                    {
                        TValue* ra = vRA(ref state);
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        int imm = (int)GETARG_sC(state.i);
                        if (ttisinteger(v1))
                        {
                            long iv1 = ivalue(v1);
                            state.pc++;
                            setivalue(ra, iv1 + imm);
                        }
                        else if (ttisfloat(v1))
                        {
                            double nb = fltvalue(v1);
                            double fimm = imm;
                            state.pc++;
                            setfltvalue(ra, nb + fimm);
                        }
                        
                        break;
                    }

                case OpCode.OP_ADDK:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = KC(ref state);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), i1 + i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n1 + n2);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_SUBK:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = KC(ref state);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), i1 - i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n1 - n2);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_MULK:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = KC(ref state);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), i1 * i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n1 * n2);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_MODK:
                    {
                        savestate(ref state); /* in case of division by 0 */
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = KC(ref state);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), luaV_mod(L, i1, i2));
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), luaV_modf(L, n1, n2));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_POWK:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = KC(ref state);
                        Debug.Assert(ttisnumber(v2));
                        if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n2 == 2 ? n1 * n1 : Math.Pow(n1, n2));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_DIVK:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = KC(ref state);
                        Debug.Assert(ttisnumber(v2));
                        if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n1 / n2);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_IDIVK:
                    {
                        savestate(ref state); /* in case of division by 0 */
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = KC(ref state);
                        Debug.Assert(ttisnumber(v2));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), luaV_idiv(L, i1, i2));
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), Math.Floor(n1 / n2));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_BANDK:
                    {
                        // op_bitwiseK(L, l_band);
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_BORK:
                    {
                        // op_bitwiseK(L, l_bor);
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_BXORK:
                    {
                        // op_bitwiseK(L, l_bxor);
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_SHLI:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        int ic = (int)GETARG_sC(state.i);
                        if (tointegerns(rb, out long ib))
                        {
                            state.pc++;
                            setivalue(s2v(ra), luaV_shiftl(ic, ib));
                        }

                        break;
                    }

                case OpCode.OP_SHRI:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        int ic = (int)GETARG_sC(state.i);
                        if (tointegerns(rb, out long ib))
                        {
                            state.pc++;
                            setivalue(s2v(ra), luaV_shiftl(ib, -ic));
                        }

                        break;
                    }

                case OpCode.OP_ADD:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = s2v(state.@base + GETARG_C(state.i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), i1 + i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n1 + n2);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_SUB:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = s2v(state.@base + GETARG_C(state.i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), i1 - i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n1 - n2);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_MUL:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = s2v(state.@base + GETARG_C(state.i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), i1 * i2);
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n1 * n2);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_MOD:
                    {
                        savestate(ref state); /* in case of division by 0 */
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = s2v(state.@base + GETARG_C(state.i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), luaV_mod(L, i1, i2));
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), luaV_modf(L, n1, n2));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_POW:
                    {
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = s2v(state.@base + GETARG_C(state.i));
                        if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n2 == 2 ? n1 * n1 : Math.Pow(n1, n2));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_DIV:
                    {
                        /* float division (always with floats) */
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = s2v(state.@base + GETARG_C(state.i));
                        if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), n1 / n2);
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_IDIV:
                    {
                        /* floor division */
                        savestate(ref state); /* in case of division by 0 */
                        TValue* v1 = s2v(state.@base + GETARG_B(state.i));
                        TValue* v2 = s2v(state.@base + GETARG_C(state.i));
                        if (ttisinteger(v1) && ttisinteger(v2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            long i1 = ivalue(v1);
                            long i2 = ivalue(v2);
                            state.pc++;
                            setivalue(s2v(ra), luaV_idiv(L, i1, i2));
                        }
                        else if (tonumberns(v1, out double n1) && tonumberns(v2, out double n2))
                        {
                            StkId ra = state.@base + GETARG_A(state.i);
                            state.pc++;
                            setfltvalue(s2v(ra), Math.Floor(n1 / n2));
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }

                        break;
                    }

                case OpCode.OP_BAND:
                    {
                        // op_bitwise(L, l_band);
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_BOR:
                    {
                        // op_bitwise(L, l_bor);
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_BXOR:
                    {
                        // op_bitwise(L, l_bxor);
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_SHL:
                    {
                        // op_bitwise(L, luaV_shiftl);
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_SHR:
                    {
                        // op_bitwise(L, luaV_shiftr);
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_MMBIN:
                    {
                        StkId ra = RA(ref state);
                        uint pi = *(state.pc - 2); /* original arith. expression */
                        TValue* rb = vRB(ref state);
                        TMS tm = (TMS)GETARG_C(state.i);
                        StkId result = RA(ref state, pi);
                        Debug.Assert(OpCode.OP_ADD <= GET_OPCODE(pi) && GET_OPCODE(pi) <= OpCode.OP_SHR);
                        Protect(ref state, () => luaT_trybinTM(L, s2v(ra), rb, result, tm));
                        break;
                    }

                case OpCode.OP_MMBINI:
                    {
                        StkId ra = RA(ref state);
                        uint pi = *(state.pc - 2); /* original arith. expression */
                        int imm = (int)GETARG_sB(state.i);
                        TMS tm = (TMS)GETARG_C(state.i);
                        bool flip = GETARG_k(state.i);
                        StkId result = RA(ref state, pi);
                        Protect(ref state, () => luaT_trybiniTM(L, s2v(ra), imm, flip, result, tm));
                        break;
                    }

                case OpCode.OP_MMBINK:
                    {
                        StkId ra = RA(ref state);
                        uint pi = *(state.pc - 2); /* original arith. expression */
                        TValue* imm = KB(ref state);
                        TMS tm = (TMS)GETARG_C(state.i);
                        bool flip = GETARG_k(state.i);
                        StkId result = RA(ref state, pi);
                        Protect(ref state, () => luaT_trybinassocTM(L, s2v(ra), imm, flip, result, tm));
                        break;
                    }

                case OpCode.OP_UNM:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        if (ttisinteger(rb))
                        {
                            long ib = ivalue(rb);
                            setivalue(s2v(ra), -ib);
                        }
                        else if (tonumberns(rb, out double nb))
                        {
                            //     setfltvalue(s2v(ra), luai_numunm(L, nb));
                            throw new NotImplementedException();
                        }
                        else
                        {
                            Protect(ref state, () => luaT_trybinTM(L, rb, rb, ra, TMS.UNM));
                        }

                        break;
                    }

                case OpCode.OP_BNOT:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        // lua_Integer ib;
                        // if (tointegerns(rb, &ib))
                        // {
                        //     setivalue(s2v(ra), ~0L ^ ib);
                        // }
                        // else
                        //     Protect(luaT_trybinTM(L, rb, rb, ra, TM_BNOT));
                        //
                        // break;
                        throw new NotImplementedException();
                    }

                case OpCode.OP_NOT:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
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

                case OpCode.OP_LEN:
                    {
                        StkId ra = RA(ref state);
                        Protect(ref state, (ref state) => luaV_objlen(L, ra, vRB(ref state)));
                        break;
                    }

                case OpCode.OP_CONCAT:
                    {
                        StkId ra = RA(ref state);
                        int n = GETARG_B(state.i); /* number of elements to concatenate */
                        L->top.p = ra + n; /* mark the end of concat operands */
                        ProtectNT(ref state, () => luaV_concat(L, n));
                        checkGC(ref state, L->top.p); /* 'luaV_concat' ensures correct top */
                        break;
                    }

                case OpCode.OP_CLOSE:
                    {
                        StkId ra = RA(ref state);
                        Debug.Assert(GETARG_B(state.i) == 0); /* 'close must be alive */
                        Protect(ref state, () => luaF_close(L, ra, LUA_OK, true));
                        break;
                    }

                case OpCode.OP_TBC:
                    {
                        StkId ra = RA(ref state);
                        /* create new to-be-closed upvalue */
                        halfProtect(ref state, () => luaF_newtbcupval(L, ra));
                        break;
                    }

                case OpCode.OP_JMP:
                    dojump(ref state, state.i, 0);
                    break;

                case OpCode.OP_EQ:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        Protect(ref state, (ref state) => state.cond = luaV_equalobj(L, s2v(ra), rb));
                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_LT:
                    {
                        TValue* ra = vRA(ref state);
                        TValue* rb = s2v(state.@base + GETARG_B(state.i));
                        if (ttisinteger(ra) && ttisinteger(rb))
                        {
                            long ia = ivalue(ra);
                            long ib = ivalue(rb);
                            state.cond = ia < ib;
                        }
                        else if (ttisnumber(ra) && ttisnumber(rb))
                        {
                            state.cond = LTnum(ra, rb);
                        }
                        else
                        {
                            Protect(ref state, (ref state) => state.cond = lessthanothers(L, ra, rb));
                        }

                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_LE:
                    {
                        TValue* ra = vRA(ref state);
                        TValue* rb = s2v(state.@base + GETARG_B(state.i));
                        if (ttisinteger(ra) && ttisinteger(rb))
                        {
                            long ia = ivalue(ra);
                            long ib = ivalue(rb);
                            state.cond = ia <= ib;
                        }
                        else if (ttisnumber(ra) && ttisnumber(rb))
                        {
                            state.cond = LEnum(ra, rb);
                        }
                        else
                        {
                            Protect(ref state, (ref state) => state.cond = lessequalothers(L, ra, rb));
                        }

                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_EQK:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = KB(ref state);
                        /* basic types do not use '__eq'; we can use raw equality */
                        state.cond = luaV_rawequalobj(s2v(ra), rb);
                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_EQI:
                    {
                        StkId ra = RA(ref state);
                        int im = (int)GETARG_sB(state.i);
                        if (ttisinteger(s2v(ra)))
                        {
                            state.cond = ivalue(s2v(ra)) == im;
                        }
                        else if (ttisfloat(s2v(ra)))
                        {
                            state.cond = fltvalue(s2v(ra)) == im;
                        }
                        else
                        {
                            state.cond = false; /* other types cannot be equal to a number */
                        }

                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_LTI:
                    {
                        TValue* ra = vRA(ref state);
                        int im = (int)GETARG_sB(state.i);
                        if (ttisinteger(ra))
                        {
                            state.cond = ivalue(ra) < im;
                        }
                        else if (ttisfloat(ra))
                        {
                            double fa = fltvalue(ra);
                            double fim = im;
                            state.cond = fa < fim;
                        }
                        else
                        {
                            bool isf = GETARG_C(state.i) != 0;
                            Protect(ref state, (ref state) => state.cond = luaT_callorderiTM(L, ra, im, 0, isf, TMS.LT));
                        }

                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_LEI:
                    {
                        TValue* ra = vRA(ref state);
                        int im = (int)GETARG_sB(state.i);
                        if (ttisinteger(ra))
                        {
                            state.cond = ivalue(ra) <= im;
                        }
                        else if (ttisfloat(ra))
                        {
                            double fa = fltvalue(ra);
                            double fim = im;
                            state.cond = fa <= fim;
                        }
                        else
                        {
                            bool isf = GETARG_C(state.i) != 0;
                            Protect(ref state, (ref state) => state.cond = luaT_callorderiTM(L, ra, im, 0, isf, TMS.LE));
                        }

                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_GTI:
                    {
                        TValue* ra = vRA(ref state);
                        int im = (int)GETARG_sB(state.i);
                        if (ttisinteger(ra))
                        {
                            state.cond = ivalue(ra) > im;
                        }
                        else if (ttisfloat(ra))
                        {
                            double fa = fltvalue(ra);
                            double fim = im;
                            state.cond = fa > fim;
                        }
                        else
                        {
                            bool isf = GETARG_C(state.i) != 0;
                            Protect(ref state, (ref state) => state.cond = luaT_callorderiTM(L, ra, im, 1, isf, TMS.LT));
                        }

                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_GEI:
                    {
                        TValue* ra = vRA(ref state);
                        int im = (int)GETARG_sB(state.i);
                        if (ttisinteger(ra))
                        {
                            state.cond = ivalue(ra) >= im;
                        }
                        else if (ttisfloat(ra))
                        {
                            double fa = fltvalue(ra);
                            double fim = im;
                            state.cond = fa >= fim;
                        }
                        else
                        {
                            bool isf = GETARG_C(state.i) != 0;
                            Protect(ref state, (ref state) => state.cond = luaT_callorderiTM(L, ra, im, 1, isf, TMS.LE));
                        }

                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_TEST:
                    {
                        StkId ra = RA(ref state);
                        state.cond = !l_isfalse(s2v(ra));
                        docondjump(ref state);
                        break;
                    }

                case OpCode.OP_TESTSET:
                    {
                        StkId ra = RA(ref state);
                        TValue* rb = vRB(ref state);
                        if (l_isfalse(rb) == GETARG_k(state.i))
                        {
                            state.pc++;
                        }
                        else
                        {
                            setobj2s(L, ra, rb);
                            donextjump(ref state);
                        }

                        break;
                    }

                case OpCode.OP_CALL:
                    {
                        StkId ra = RA(ref state);
                        int b = GETARG_B(state.i);
                        int nresults = (int)(GETARG_C(state.i) - 1);
                        if (b != 0) /* fixed number of arguments? */
                        {
                            L->top.p = ra + b; /* top signals number of arguments */
                        }
                        /* else previous instruction set top */

                        savepc(ref state); /* in case of errors */

                        CallInfo* newci;
                        if ((newci = luaD_precall(L, ra, nresults)) == null)
                        {
                            updatetrap(ref state); /* C call; nothing else to be done */
                        }
                        else
                        {
                            /* Lua call: run function in this same C frame */
                            state.ci = newci;
                            goto startfunc;
                        }

                        break;
                    }

                case OpCode.OP_TAILCALL:
                    {
                        StkId ra = RA(ref state);
                        int b = GETARG_B(state.i); /* number of arguments + 1 (function) */
                        // int n; /* number of results when calling a C function */
                        // int nparams1 = GETARG_C(i);
                        // /* delta is virtual 'func' - real 'func' (vararg functions) */
                        // int delta = (nparams1) ? ci->u.l.nextraargs + nparams1 : 0;
                        // if (b != 0)
                        //     L->top.p = ra + b;
                        // else /* previous instruction set top */
                        //     b = cast_int(L->top.p - ra);
                        // savepc(ci); /* several calls here can raise errors */
                        // if (TESTARG_k(i))
                        // {
                        //     luaF_closeupval(L, base); /* close upvalues from current call */
                        //     lua_assert(L->tbclist.p < base); /* no pending tbc variables */
                        //     lua_assert(base == ci->func.p + 1);
                        // }
                        //
                        // if ((n = luaD_pretailcall(L, ci, ra, b, delta)) < 0) /* Lua function? */
                        //     goto startfunc; /* execute the callee */
                        // else
                        // {
                        //     /* C function? */
                        //     ci->func.p -= delta; /* restore 'func' (if vararg) */
                        //     luaD_poscall(L, ci, n); /* finish caller */
                        //     updatetrap(ci); /* 'luaD_poscall' can change hooks */
                        //     goto ret; /* caller returns after the tail call */
                        // }
                        throw new NotImplementedException();
                    }

                case OpCode.OP_RETURN:
                    {
                        StkId ra = RA(ref state);
                        int n = GETARG_B(state.i) - 1; /* number of results */
                        int nparams1 = (int)GETARG_C(state.i);
                        if (n < 0) /* not fixed? */
                        {
                            n = (int)(L->top.p - ra); /* get what is available */
                        }

                        savepc(ref state);
                        if (TESTARG_k(state.i))
                        {
                            /* may there be open upvalues? */
                            state.ci->u2.nres = n; /* save number of returns */
                            if (L->top.p < state.ci->top.p)
                            {
                                L->top.p = state.ci->top.p;
                            }

                            luaF_close(L, state.@base, CLOSEKTOP, true);
                            updatetrap(ref state);
                            updatestack(ref state, ref ra);
                        }

                        if (nparams1 != 0) /* vararg function? */
                        {
                            state.ci->func.p -= state.ci->u.l.nextraargs + nparams1;
                        }

                        L->top.p = ra + n; /* set call for 'luaD_poscall' */
                        luaD_poscall(L, state.ci, n);
                        updatetrap(ref state); /* 'luaD_poscall' can change hooks */
                        goto ret;
                    }

                case OpCode.OP_RETURN0:
                    {
                        if (L->hookmask != 0)
                        {
                            StkId ra = RA(ref state);
                            L->top.p = ra;
                            savepc(ref state);
                            luaD_poscall(L, state.ci, 0); /* no hurry... */
                            state.trap = 1;
                        }
                        else
                        {
                            /* do the 'poscall' here */
                            int nres = get_nresults(state.ci->callstatus);
                            L->ci = state.ci->previous; /* back to caller */
                            L->top.p = state.@base - 1;
                            for (; nres > 0; nres--)
                            {
                                setnilvalue(s2v(L->top.p++)); /* all results are nil */
                            }
                        }

                        goto ret;
                    }

                case OpCode.OP_RETURN1:
                    if (L->hookmask != 0)
                    {
                        StkId ra = RA(ref state);
                        L->top.p = ra + 1;
                        savepc(ref state);
                        luaD_poscall(L, state.ci, 1); /* no hurry... */
                        state.trap = 1;
                    }
                    else
                    {
                        /* do the 'poscall' here */
                        int nres = get_nresults(state.ci->callstatus);
                        L->ci = state.ci->previous; /* back to caller */
                        if (nres == 0)
                        {
                            L->top.p = state.@base - 1; /* asked for no results */
                        }
                        else
                        {
                            StkId ra = RA(ref state);
                            setobjs2s(L, state.@base - 1, ra); /* at least this result */
                            L->top.p = state.@base;
                            for (; nres > 1; nres--)
                            {
                                setnilvalue(s2v(L->top.p++)); /* complete missing results */
                            }
                        }
                    }

                    goto ret;

                case OpCode.OP_FORLOOP:
                    {
                        StkId ra = RA(ref state);
                        if (ttisinteger(s2v(ra + 1)))
                        {
                            /* integer loop? */
                            ulong count = (ulong)ivalue(s2v(ra));
                            if (count > 0)
                            {
                                /* still more iterations? */
                                long step = ivalue(s2v(ra + 1));
                                long idx = ivalue(s2v(ra + 2)); /* control variable */
                                chgivalue(s2v(ra), (long)(count - 1)); /* update counter */
                                idx = idx + step; /* add step to index */
                                chgivalue(s2v(ra + 2), idx); /* update control variable */
                                state.pc -= GETARG_Bx(state.i); /* jump back */
                            }
                        }
                        else if (floatforloop(ra)) /* float loop */
                        {
                            state.pc -= GETARG_Bx(state.i); /* jump back */
                        }

                        updatetrap(ref state); /* allows a signal to break the loop */
                        break;
                    }

                case OpCode.OP_FORPREP:
                    {
                        StkId ra = RA(ref state);
                        savestate(ref state); /* in case of errors */
                        if (forprep(L, ra))
                        {
                            state.pc += GETARG_Bx(state.i) + 1; /* skip the loop */
                        }

                        break;
                    }

                case OpCode.OP_TFORPREP:
                    {
                        /* before: 'ra' has the iterator function, 'ra + 1' has the state,
                           'ra + 2' has the initial value for the control variable, and
                           'ra + 3' has the closing variable. This opcode then swaps the
                           control and the closing variables and marks the closing variable
                           as to-be-closed.
                        */
                        StkId ra = RA(ref state);
                        TValue temp; /* to swap control and closing variables */
                        setobj(L, &temp, s2v(ra + 3));
                        setobjs2s(L, ra + 3, ra + 2);
                        setobj2s(L, ra + 2, &temp);
                        /* create to-be-closed upvalue (if closing var. is not nil) */
                        halfProtect(ref state, () => luaF_newtbcupval(L, ra + 2));
                        state.pc += GETARG_Bx(state.i); /* go to end of the loop */
                        state.i = *(state.pc++); /* fetch next instruction */
                        Debug.Assert(GET_OPCODE(state.i) == OpCode.OP_TFORCALL && ra == RA(ref state));
                        goto l_tforcall;
                    }

                case OpCode.OP_TFORCALL:
                    goto l_tforcall;

                case OpCode.OP_TFORLOOP:
                    goto l_tforloop;

                case OpCode.OP_SETLIST:
                    {
                        StkId ra = RA(ref state);
                        uint n = (uint)GETARG_vB(state.i);
                        uint last = (uint)GETARG_vC(state.i);
                        Table* h = hvalue(s2v(ra));
                        if (n == 0)
                        {
                            n = (uint)(L->top.p - ra) - 1; /* get up to the top */
                        }
                        else
                        {
                            L->top.p = state.ci->top.p; /* correct top in case of emergency GC */
                        }

                        last += n;
                        if (TESTARG_k(state.i))
                        {
                            last += (uint)GETARG_Ax(*state.pc) * (MAXARG_vC + 1);
                            state.pc++;
                        }

                        /* when 'n' is known, table should have proper size */
                        if (last > h->asize)
                        {
                            /* needs more space? */
                            /* fixed-size sets should have space preallocated */
                            Debug.Assert(GETARG_vB(state.i) == 0);
                            luaH_resizearray(L, h, last); /* preallocate it at once */
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

                case OpCode.OP_CLOSURE:
                    {
                        StkId ra = RA(ref state);
                        Proto* p = state.cl->p->p[GETARG_Bx(state.i)];
                        halfProtect(ref state, (ref state) => pushclosure(L, p, &state.cl->upvals, state.@base, ra));
                        checkGC(ref state, ra + 1);
                        break;
                    }

                case OpCode.OP_VARARG:
                    {
                        StkId ra = RA(ref state);
                        int n = (int)(GETARG_C(state.i) - 1); /* required results (-1 means all) */
                        int vatab = GETARG_k(state.i) ? GETARG_B(state.i) : -1;
                        Protect(ref state, (ref state) => luaT_getvarargs(L, state.ci, ra, n, vatab));
                        break;
                    }

                case OpCode.OP_GETVARG:
                    {
                        StkId ra = RA(ref state);
                        TValue* rc = vRC(ref state);
                        luaT_getvararg(state.ci, ra, rc);
                        break;
                    }

                case OpCode.OP_ERRNNIL:
                    {
                        TValue* ra = vRA(ref state);
                        if (!ttisnil(ra))
                        {
                            halfProtect(ref state, (ref state) => luaG_errnnil(L, state.cl, GETARG_Bx(state.i)));
                        }

                        break;
                    }

                case OpCode.OP_VARARGPREP:
                    {
                        ProtectNT(ref state, (ref state) => luaT_adjustvarargs(L, state.ci, state.cl->p));
                        if (state.trap != 0)
                        {
                            /* previous "Protect" updated trap */
                            luaD_hookcall(L, state.ci);
                            L->oldpc = 1; /* next opcode will be seen as a "new" line */
                        }

                        updatebase(ref state); /* function has new base after adjustment */
                        break;
                    }

                case OpCode.OP_EXTRAARG:
                    throw new InvalidOperationException();
            }

            continue;

            ret: /* return from a Lua function */
            if ((state.ci->callstatus & CIST_FRESH) != 0)
            {
                return; /* end this frame */
            }

            state.ci = state.ci->previous;
            goto returning; /* continue running caller in this frame */
            
            l_tforcall:
            {
                /* 'ra' has the iterator function, 'ra + 1' has the state,
                   'ra + 2' has the closing variable, and 'ra + 3' has the control
                   variable. The call will use the stack starting at 'ra + 3',
                   so that it preserves the first three values, and the first
                   return will be the new value for the control variable.
                */
                StkId ra = RA(ref state);
                setobjs2s(L, ra + 5, ra + 3); /* copy the control variable */
                setobjs2s(L, ra + 4, ra + 1); /* copy state */
                setobjs2s(L, ra + 3, ra); /* copy function */
                L->top.p = ra + 3 + 3;
                ProtectNT(ref state, (ref state) => luaD_call(L, ra + 3, (int)GETARG_C(state.i))); /* do the call */
                updatestack(ref state, ref ra); /* stack may have changed */
                state.i = *(state.pc++); /* go to next instruction */
                Debug.Assert(GET_OPCODE(state.i) == OpCode.OP_TFORLOOP && ra == RA(ref state));
            }
            
            l_tforloop:
            {
                StkId ra = RA(ref state);
                if (!ttisnil(s2v(ra + 3))) /* continue loop? */
                {
                    state.pc -= GETARG_Bx(state.i); /* jump back */
                }
            }
        }
    }
}
