namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /*
     ** $Id: lmathlib.c $
     ** Standard mathematical library
     ** See Copyright Notice in lua.h
     */

    private static int math_abs(lua_State* L)
    {
        if (lua_isinteger(L, 1))
        {
            long n = lua_tointeger(L, 1);
            if (n < 0)
            {
                n = unchecked((long)(0u - (ulong)n));
            }

            lua_pushinteger(L, n);
        }
        else
        {
            lua_pushnumber(L, Math.Abs(luaL_checknumber(L, 1)));
        }

        return 1;
    }

    private static int math_sin(lua_State* L)
    {
        lua_pushnumber(L, Math.Sin(luaL_checknumber(L, 1)));
        return 1;
    }

    private static int math_cos(lua_State* L)
    {
        lua_pushnumber(L, Math.Cos(luaL_checknumber(L, 1)));
        return 1;
    }

    private static int math_tan(lua_State* L)
    {
        lua_pushnumber(L, Math.Tan(luaL_checknumber(L, 1)));
        return 1;
    }

    private static int math_asin(lua_State* L)
    {
        lua_pushnumber(L, Math.Asin(luaL_checknumber(L, 1)));
        return 1;
    }

    private static int math_acos(lua_State* L)
    {
        lua_pushnumber(L, Math.Acos(luaL_checknumber(L, 1)));
        return 1;
    }

    private static int math_atan(lua_State* L)
    {
        double y = luaL_checknumber(L, 1);
        double x = luaL_optnumber(L, 2, 1);
        lua_pushnumber(L, Math.Atan2(y, x));
        return 1;
    }

    private static int math_toint(lua_State* L)
    {
        long n = lua_tointegerx(L, 1, out bool valid);
        if (valid)
        {
            lua_pushinteger(L, n);
        }
        else
        {
            luaL_checkany(L, 1);
            luaL_pushfail(L); /* value is not convertible to integer */
        }

        return 1;
    }

    private static void pushnumint(lua_State* L, double d)
    {
        if (lua_numbertointeger(d, out long n)) /* does 'd' fit in an integer? */
        {
            lua_pushinteger(L, n); /* result is integer */
        }
        else
        {
            lua_pushnumber(L, d); /* result is float */
        }
    }

    private static int math_floor(lua_State* L)
    {
        if (lua_isinteger(L, 1))
        {
            lua_settop(L, 1); /* integer is its own floor */
        }
        else
        {
            double d = Math.Floor(luaL_checknumber(L, 1));
            pushnumint(L, d);
        }

        return 1;
    }

    private static int math_ceil(lua_State* L)
    {
        if (lua_isinteger(L, 1))
        {
            lua_settop(L, 1); /* integer is its own ceiling */
        }
        else
        {
            double d = Math.Ceiling(luaL_checknumber(L, 1));
            pushnumint(L, d);
        }

        return 1;
    }

    private static int math_fmod(lua_State* L)
    {
        if (lua_isinteger(L, 1) && lua_isinteger(L, 2))
        {
            long d = lua_tointeger(L, 2);
            if ((ulong)d + 1u <= 1u)
            {
                /* special cases: -1 or 0 */
                luaL_argcheck(L, d != 0, 2, "zero");
                lua_pushinteger(L, 0); /* avoid overflow with 0x80000... / -1 */
            }
            else
            {
                lua_pushinteger(L, lua_tointeger(L, 1) % d);
            }
        }
        else
        {
            lua_pushnumber(L, luaL_checknumber(L, 1) % luaL_checknumber(L, 2));
        }

        return 1;
    }

    /*
    ** next function does not use 'modf', avoiding problems with 'double*'
    ** (which is not compatible with 'float*') when double is not
    ** 'double'.
    */
    private static int math_modf(lua_State* L)
    {
        if (lua_isinteger(L, 1))
        {
            lua_settop(L, 1); /* number is its own integer part */
            lua_pushnumber(L, 0); /* no fractional part */
        }
        else
        {
            double n = luaL_checknumber(L, 1);
            /* integer part (rounds toward zero) */
            double ip = n < 0 ? Math.Ceiling(n) : Math.Floor(n);
            pushnumint(L, ip);
            /* fractional part (test needed for inf/-inf) */
            lua_pushnumber(L, n == ip ? 0.0 : n - ip);
        }

        return 2;
    }

    private static int math_sqrt(lua_State* L)
    {
        lua_pushnumber(L, Math.Sqrt(luaL_checknumber(L, 1)));
        return 1;
    }

    private static int math_ult(lua_State* L)
    {
        long a = luaL_checkinteger(L, 1);
        long b = luaL_checkinteger(L, 2);
        lua_pushboolean(L, (ulong)a < (ulong)b);
        return 1;
    }

    private static int math_log(lua_State* L)
    {
        double x = luaL_checknumber(L, 1);
        double res;
        if (lua_isnoneornil(L, 2))
        {
            res = Math.Log(x);
        }
        else
        {
            double @base = luaL_checknumber(L, 2);
            res = @base switch
            {
                2 => Math.Log2(x),
                10 => Math.Log10(x),
                _ => Math.Log(x, @base),
            };
        }

        lua_pushnumber(L, res);
        return 1;
    }

    private static int math_exp(lua_State* L)
    {
        lua_pushnumber(L, Math.Exp(luaL_checknumber(L, 1)));
        return 1;
    }

    private static int math_deg(lua_State* L)
    {
        lua_pushnumber(L, luaL_checknumber(L, 1) * (180.0 / Math.PI));
        return 1;
    }

    private static int math_rad(lua_State* L)
    {
        lua_pushnumber(L, luaL_checknumber(L, 1) * (Math.PI / 180.0));
        return 1;
    }

    private static int math_frexp(lua_State* L)
    {
        double x = luaL_checknumber(L, 1);
        lua_pushnumber(L, frexp(x, out int ep));
        lua_pushinteger(L, ep);
        return 2;
    }

    private static int math_ldexp(lua_State* L)
    {
        double x = luaL_checknumber(L, 1);
        int ep = (int)luaL_checkinteger(L, 2);
        lua_pushnumber(L, Math.ScaleB(x, ep));
        return 1;
    }

    private static double frexp(double x, out int exponent)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        int biasedExponent = (int)((bits >> 52) & 0x7FF);

        if (biasedExponent == 0)
        {
            if ((bits & 0x000F_FFFF_FFFF_FFFFL) == 0)
            {
                exponent = 0;
                return x;
            }

            double mantissa = frexp(Math.ScaleB(x, 64), out exponent);
            exponent -= 64;
            return mantissa;
        }

        if (biasedExponent == 0x7FF)
        {
            exponent = 0;
            return x;
        }

        exponent = biasedExponent - 1022;
        bits = (bits & unchecked((long)0x8000_0000_0000_0000UL))
               | (0x3FEL << 52)
               | (bits & 0x000F_FFFF_FFFF_FFFFL);
        return BitConverter.Int64BitsToDouble(bits);
    }

    private static int math_min(lua_State* L)
    {
        int n = lua_gettop(L); /* number of arguments */
        int imin = 1; /* index of current minimum value */
        int i;
        luaL_argcheck(L, n >= 1, 1, "value expected");
        for (i = 2; i <= n; i++)
        {
            if (lua_compare(L, i, imin, LUA_OPLT))
            {
                imin = i;
            }
        }

        lua_pushvalue(L, imin);
        return 1;
    }

    private static int math_max(lua_State* L)
    {
        int n = lua_gettop(L); /* number of arguments */
        int imax = 1; /* index of current maximum value */
        int i;
        luaL_argcheck(L, n >= 1, 1, "value expected");
        for (i = 2; i <= n; i++)
        {
            if (lua_compare(L, imax, i, LUA_OPLT))
            {
                imax = i;
            }
        }

        lua_pushvalue(L, imax);
        return 1;
    }

    private static int math_type(lua_State* L)
    {
        if (lua_type(L, 1) == LUA_TNUMBER)
        {
            lua_pushstring(L, lua_isinteger(L, 1) ? "integer" : "float");
        }
        else
        {
            luaL_checkany(L, 1);
            luaL_pushfail(L);
        }

        return 1;
    }

    /*
     ** {==================================================================
     ** Pseudo-Random Number Generator based on 'xoshiro256**'.
     ** ===================================================================
     */

    /*
     ** This code uses lots of shifts. ISO C does not allow shifts greater
     ** than or equal to the width of the type being shifted, so some shifts
     ** are written in convoluted ways to match that restriction. For
     ** preprocessor tests, it assumes a width of 32 bits, so the maximum
     ** shift there is 31 bits.
     */

    /* number of binary digits in the mantissa of a float */
    private const int FIGS = DBL_MANT_DIG;

    /*
     ** Standard implementation, using 64-bit integers.
     ** If 'Rand64' has more than 64 bits, the extra bits do not interfere
     ** with the 64 initial bits, except in a right shift. Moreover, the
     ** final result has to discard the extra bits.
     */

    /* rotate left 'x' by 'n' bits */
    private static ulong rotl(ulong x, int n)
    {
        return x << n | x >> 64 - n;
    }

    private static ulong nextrand(ulong* state)
    {
        ulong state0 = state[0];
        ulong state1 = state[1];
        ulong state2 = state[2] ^ state0;
        ulong state3 = state[3] ^ state1;
        ulong res = rotl(state1 * 5, 7) * 9;
        state[0] = state0 ^ state3;
        state[1] = state1 ^ state2;
        state[2] = state2 ^ state1 << 17;
        state[3] = rotl(state3, 45);
        return res;
    }

    /*
    ** Convert bits from a random integer into a float in the
    ** interval [0,1), getting the higher FIG bits from the
    ** random unsigned integer and converting that to a float.
    ** Some old Microsoft compilers cannot cast an unsigned long
    ** to a floating-point number, so we use a signed long as an
    ** intermediary. When double is float or double, the shift ensures
    ** that 'sx' is non negative; in that case, a good compiler will remove
    ** the correction.
    */

    /* must throw out the extra (64 - FIGS) bits */
    private const int shift64_FIG = 64 - FIGS;

    /* 2^(-FIGS) == 2^-1 / 2^(FIGS-1) */
    private const double scaleFIG = 0.5 / (1UL << FIGS - 1);

    private static double I2d(ulong x)
    {
        long sx = (long)(x >> shift64_FIG);
        double res = sx * scaleFIG;
        if (sx < 0)
        {
            res += 1.0; /* correct the two's complement if negative */
        }

        Debug.Assert(0 <= res && res < 1);
        return res;
    }

    /*
     ** A state uses four 'Rand64' values.
     */
    private struct RanState
    {
        public fixed ulong s[4];
    }

    /*
    ** Project the random integer 'ran' into the interval [0, n].
    ** Because 'ran' has 2^B possible values, the projection can only be
    ** uniform when the size of the interval is a power of 2 (exact
    ** division). So, to get a uniform projection into [0, n], we
    ** first compute 'lim', the smallest Mersenne number not smaller than
    ** 'n'. We then project 'ran' into the interval [0, lim].  If the result
    ** is inside [0, n], we are done. Otherwise, we try with another 'ran',
    ** until we have a result inside the interval.
    */
    private static ulong project(ulong ran, ulong n, RanState* state)
    {
        ulong lim = n; /* to compute the Mersenne number */
        /* spread '1' bits in 'lim' until it becomes a Mersenne number */
        for (int sh = 1; (lim & lim + 1) != 0; sh *= 2)
        {
            lim |= lim >> sh; /* spread '1's to the right */
        }

        while ((ran &= lim) > n) /* project 'ran' into [0..lim] and test */
        {
            ran = nextrand(state->s); /* not inside [0..n]? try again */
        }

        return ran;
    }

    private static int math_random(lua_State* L)
    {
        long low;
        long up;
        RanState* state = (RanState*)lua_touserdata(L, lua_upvalueindex(1));
        ulong rv = nextrand(state->s); /* next pseudo-random value */
        switch (lua_gettop(L))
        {
            /* check number of arguments */
            case 0:
                /* no arguments */
                lua_pushnumber(L, I2d(rv));  /* float between 0 and 1 */
                return 1;

            case 1:
                /* only upper limit */
                low = 1;
                up = luaL_checkinteger(L, 1);
                if (up == 0)
                {
                    /* single 0 as argument? */
                    lua_pushinteger(L, (long)rv); /* full random integer */
                    return 1;
                }

                break;

            case 2:
                /* lower and upper limits */
                low = luaL_checkinteger(L, 1);
                up = luaL_checkinteger(L, 2);
                break;

            default:
                return luaL_error(L, "wrong number of arguments");
        }

        /* random integer in the interval [low, up] */
        luaL_argcheck(L, low <= up, 1, "interval is empty");
        /* project random integer into the interval [0, up - low] */
        var p = project(rv, (ulong)up - (ulong)low, state);
        lua_pushinteger(L, (long)(p + (ulong)low));
        return 1;
    }

    private static void setseed(
        lua_State* L,
        ulong* state,
        ulong n1,
        ulong n2)
    {
        state[0] = n1;
        state[1] = 0xff; /* avoid a zero state */
        state[2] = n2;
        state[3] = 0;
        for (int i = 0; i < 16; i++)
        {
            nextrand(state); /* discard initial values to "spread" seed */
        }

        lua_pushinteger(L, (long)n1);
        lua_pushinteger(L, (long)n2);
    }

    private static int math_randomseed(lua_State* L)
    {
        RanState* state = (RanState*)lua_touserdata(L, lua_upvalueindex(1));
        ulong n1;
        ulong n2;
        if (lua_isnone(L, 1))
        {
            n1 = luaL_makeseed(L); /* "random" seed */
            n2 = nextrand(state->s); /* in case seed is not that random... */
        }
        else
        {
            n1 = (ulong)luaL_checkinteger(L, 1);
            n2 = (ulong)luaL_optinteger(L, 2, 0);
        }

        setseed(L, state->s, n1, n2);
        return 2; /* return seeds */
    }

    private static readonly luaL_Reg[] randfuncs =
    [
        new("random", &math_random),
        new("randomseed", &math_randomseed),
    ];

    /*
     ** Register the random functions and initialise their state.
     */
    private static void setrandfunc(lua_State* L)
    {
        RanState* state = (RanState*)lua_newuserdatauv(L, sizeof(RanState), 0);
        setseed(L, state->s, luaL_makeseed(L), 0); /* initialise with random seed */
        lua_pop(L, 2); /* remove pushed seeds */
        luaL_setfuncs(L, randfuncs, 1);
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Deprecated functions (for compatibility only)
     ** ===================================================================
     */
#if LUA_COMPAT_MATHLIB
    private static int math_cosh(lua_State* L)
    {
//   lua_pushnumber(L, (cosh)(luaL_checknumber(L, 1)));
//   return 1;
        throw new NotImplementedException();
    }

    private static int math_sinh(lua_State* L)
    {
//   lua_pushnumber(L, (sinh)(luaL_checknumber(L, 1)));
//   return 1;
        throw new NotImplementedException();
    }

    private static int math_tanh(lua_State* L)
    {
//   lua_pushnumber(L, (tanh)(luaL_checknumber(L, 1)));
//   return 1;
        throw new NotImplementedException();
    }

    private static int math_pow(lua_State* L)
    {
//   double x = luaL_checknumber(L, 1);
//   double y = luaL_checknumber(L, 2);
//   lua_pushnumber(L, (pow)(x, y));
//   return 1;
        throw new NotImplementedException();
    }

    private static int math_log10(lua_State* L)
    {
//   lua_pushnumber(L, (log10)(luaL_checknumber(L, 1)));
//   return 1;
        throw new NotImplementedException();
    }
#endif
    /* }================================================================== */

    private static readonly luaL_Reg[] mathlib =
    [
        new("abs", &math_abs),
        new("acos", &math_acos),
        new("asin", &math_asin),
        new("atan", &math_atan),
        new("ceil", &math_ceil),
        new("cos", &math_cos),
        new("deg", &math_deg),
        new("exp", &math_exp),
        new("tointeger", &math_toint),
        new("floor", &math_floor),
        new("fmod", &math_fmod),
        new("frexp", &math_frexp),
        new("ult", &math_ult),
        new("ldexp", &math_ldexp),
        new("log", &math_log),
        new("max", &math_max),
        new("min", &math_min),
        new("modf", &math_modf),
        new("rad", &math_rad),
        new("sin", &math_sin),
        new("sqrt", &math_sqrt),
        new("tan", &math_tan),
        new("type", &math_type),
#if LUA_COMPAT_MATHLIB
        new("atan2", &math_atan),
        new("cosh", &math_cosh),
        new("sinh", &math_sinh),
        new("tanh", &math_tanh),
        new("pow", &math_pow),
        new("log10", &math_log10),
#endif
        /* placeholders */
        new("random", null),
        new("randomseed", null),
        new("pi", null),
        new("huge", null),
        new("maxinteger", null),
        new("mininteger", null),
    ];

    /*
     ** Open math library
     */
    public static int luaopen_math(lua_State* L)
    {
        luaL_newlib(L, mathlib);
        lua_pushnumber(L, Math.PI);
        lua_setfield(L, -2, "pi");
        lua_pushnumber(L, double.PositiveInfinity);
        lua_setfield(L, -2, "huge");
        lua_pushinteger(L, long.MaxValue);
        lua_setfield(L, -2, "maxinteger");
        lua_pushinteger(L, long.MinValue);
        lua_setfield(L, -2, "mininteger");
        setrandfunc(L);
        return 1;
    }
}
