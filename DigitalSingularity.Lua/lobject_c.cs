namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Text;

public static unsafe partial class Lua
{
    private static byte[] log_2 =
    [
        /* log_2[i - 1] = ceil(log2(i)) */
        0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
    ];

    /*
     ** Computes ceil(log2(x)), which is the smallest integer n such that
     ** x <= (1 << n).
     */
    private static partial byte luaO_ceillog2(uint x)
    {
        int l = 0;
        x--;
        while (x >= 256)
        {
            l += 8;
            x >>= 8;
        }

        return (byte)(l + log_2[x]);
    }

    /*
     ** Encodes 'p'% as a floating-point byte, represented as (eeeexxxx).
     ** The exponent is represented using excess-7. Mimicking IEEE 754, the
     ** representation normalizes the number when possible, assuming an extra
     ** 1 before the mantissa (xxxx) and adding one to the exponent (eeee)
     ** to signal that. So, the real value is (1xxxx) * 2^(eeee - 7 - 1) if
     ** eeee != 0, and (xxxx) * 2^-7 otherwise (subnormal numbers).
     */
    private static partial byte luaO_codeparam(uint p)
    {
        if (p >= ((long)0x1F << 0xF - 7 - 1) * 100u) /* overflow? */
        {
            return 0xFF; /* return maximum value */
        }

        p = (p * 128 + 99) / 100; /* round up the division */
        if (p < 0x10)
        {
            /* subnormal number? */
            /* exponent bits are already zero; nothing else to do */
            return (byte)p;
        }

        /* p >= 0x10 implies ceil(log2(p + 1)) >= 5 */
        /* preserve 5 bits in 'p' */
        uint log = luaO_ceillog2(p + 1) - 5u;
        return (byte)((p >> (int)log) - 0x10 | log + 1 << 4);
    }

    /*
     ** Computes 'p' times 'x', where 'p' is a floating-point byte. Roughly,
     ** we have to multiply 'x' by the mantissa and then shift accordingly to
     ** the exponent.  If the exponent is positive, both the multiplication
     ** and the shift increase 'x', so we have to care only about overflows.
     ** For negative exponents, however, multiplying before the shift keeps
     ** more significant bits, as long as the multiplication does not
     ** overflow, so we check which order is best.
     */
    private static partial long luaO_applyparam(byte p, long x)
    {
        const long MAX_LMEM = 0x7FFFFFFFFFFFFFFFL;

        int m = p & 0xF; /* mantissa */
        int e = (p >> 4); /* exponent */
        if (e > 0)
        {
            /* normalized? */
            e--; /* correct exponent */
            m += 0x10; /* correct mantissa; maximum value is 0x1F */
        }

        e -= 7; /* correct excess-7 */
        if (e >= 0)
        {
            if (x < (MAX_LMEM / 0x1F) >> e) /* no overflow? */
            {
                return (x * m) << e; /* order doesn't matter here */
            }

            /* real overflow */
            return MAX_LMEM;
        }

        /* negative exponent */
        e = -e;
        if (x < MAX_LMEM / 0x1F) /* multiplication cannot overflow? */
        {
            return (x * m) >> e; /* multiplying first gives more precision */
        }

        if ((x >> e) < MAX_LMEM / 0x1F) /* cannot overflow after shift? */
        {
            return (x >> e) * m;
        }

        /* real overflow */
        return MAX_LMEM;
    }

    private static long intarith(lua_State* L, int op, long v1, long v2)
    {
        return op switch
        {
            LUA_OPADD => v1 + v2,
            LUA_OPSUB => v1 - v2,
            LUA_OPMUL => (long)((ulong)v1 * (ulong)v2),
            LUA_OPMOD => luaV_mod(L, v1, v2),
            LUA_OPIDIV => luaV_idiv(L, v1, v2),
            LUA_OPBAND => v1 & v2,
            LUA_OPBOR => v1 | v2,
            LUA_OPBXOR => v1 ^ v2,
            LUA_OPSHL => luaV_shiftl(v1, v2),
            LUA_OPSHR => luaV_shiftr(v1, v2),
            LUA_OPUNM => -v1,
            LUA_OPBNOT => ~0L ^ v1,
            _ => throw new InvalidOperationException(),
        };
    }

    private static double numarith(lua_State* L, int op, double v1, double v2)
    {
        return op switch
        {
            LUA_OPADD => v1 + v2,
            LUA_OPSUB => v1 - v2,
            LUA_OPMUL => v1 * v2,
            LUA_OPDIV => v1 / v2,
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            LUA_OPPOW => v2 == 2 ? v1 * v1 : Math.Pow(v1, v2),
            LUA_OPIDIV => Math.Floor(v1 / v2),
            LUA_OPUNM => -v1,
            LUA_OPMOD => luaV_modf(L, v1, v2),
            _ => throw new InvalidOperationException(),
        };
    }

    private static partial bool luaO_rawarith(lua_State* L, int op, TValue* p1, TValue* p2, TValue* res)
    {
        switch (op)
        {
            case LUA_OPBAND:
            case LUA_OPBOR:
            case LUA_OPBXOR:
            case LUA_OPSHL:
            case LUA_OPSHR:
            case LUA_OPBNOT:
                {
                    /* operate only on integers */
                    if (tointegerns(p1, out long i1) && tointegerns(p2, out long i2))
                    {
                        setivalue(res, intarith(L, op, i1, i2));
                        return true;
                    }

                    return false; /* fail */
                }

            case LUA_OPDIV:
            case LUA_OPPOW:
                {
                    /* operate only on floats */
                    if (tonumberns(p1, out double n1) && tonumberns(p2, out double n2))
                    {
                        setfltvalue(res, numarith(L, op, n1, n2));
                        return true;
                    }

                    return false; /* fail */
                }

            default:
                {
                    /* other operations */
                    if (ttisinteger(p1) && ttisinteger(p2))
                    {
                        setivalue(res, intarith(L, op, ivalue(p1), ivalue(p2)));
                        return true;
                    }

                    if (tonumberns(p1, out double n1) && tonumberns(p2, out double n2))
                    {
                        setfltvalue(res, numarith(L, op, n1, n2));
                        return true;
                    }

                    return false; /* fail */
                }
        }
    }

    private static partial void luaO_arith(lua_State* L, int op, TValue* p1, TValue* p2, StkId res)
    {
//   if (!luaO_rawarith(L, op, p1, p2, s2v(res))) {
//     /* could not perform raw operation; try metamethod */
//     luaT_trybinTM(L, p1, p2, res, cast(TMS, (op - LUA_OPADD) + TM_ADD));
//   }
        throw new NotImplementedException();
    }

    private static partial byte luaO_hexavalue(int c)
    {
        Debug.Assert(lisxdigit(c));
        if (lisdigit(c))
        {
            return (byte)(c - '0');
        }

        return (byte)(ltolower(c) - 'a' + 10);
    }

    private static bool isneg(ref byte* s)
    {
        if (*s == '-')
        {
            s++;
            return true;
        }

        if (*s == '+')
        {
            s++;
        }

        return false;
    }

    /*
     ** {==================================================================
     ** Lua's implementation for 'lua_strx2number'
     ** ===================================================================
     */

// /* maximum number of significant digits to read (to avoid overflows
//    even with single floats) */
// #define MAXSIGDIG	30

    /*
    ** convert a hexadecimal numeric string to a number, following
    ** C99 specification for 'strtod'
    */
    private static double lua_strx2number(byte* s, byte** endptr)
    {
//   int dot = lua_getlocaledecpoint();
//   double r = (0.0);  /* result (accumulator) */
//   int sigdig = 0;  /* number of significant digits */
//   int nosigdig = 0;  /* number of non-significant digits */
//   int e = 0;  /* exponent correction */
//   int neg;  /* 1 if number is negative */
//   int hasdot = 0;  /* true after seen a dot */
//   *endptr = cast_charp(s);  /* nothing is valid yet */
//   while (lisspace(cast_uchar(*s))) s++;  /* skip initial spaces */
//   neg = isneg(&s);  /* check sign */
//   if (!(*s == '0' && (*(s + 1) == 'x' || *(s + 1) == 'X')))  /* check '0x' */
//     return (0.0);  /* invalid format (no '0x') */
//   for (s += 2; ; s++) {  /* skip '0x' and read numeral */
//     if (*s == dot) {
//       if (hasdot) break;  /* second dot? stop loop */
//       else hasdot = 1;
//     }
//     else if (lisxdigit(cast_uchar(*s))) {
//       if (sigdig == 0 && *s == '0')  /* non-significant digit (zero)? */
//         nosigdig++;
//       else if (++sigdig <= MAXSIGDIG)  /* can read it without overflow? */
//           r = (r * (16.0)) + luaO_hexavalue(*s);
//       else e++;  /* too many digits; ignore, but still count for exponent */
//       if (hasdot) e--;  /* decimal digit? correct exponent */
//     }
//     else break;  /* neither a dot nor a digit */
//   }
//   if (nosigdig + sigdig == 0)  /* no digits? */
//     return (0.0);  /* invalid format */
//   *endptr = cast_charp(s);  /* valid up to here */
//   e *= 4;  /* each digit multiplies/divides value by 2^4 */
//   if (*s == 'p' || *s == 'P') {  /* exponent part? */
//     int exp1 = 0;  /* exponent value */
//     int neg1;  /* exponent sign */
//     s++;  /* skip 'p' */
//     neg1 = isneg(&s);  /* sign */
//     if (!lisdigit(cast_uchar(*s)))
//       return (0.0);  /* invalid; must have at least one digit */
//     while (lisdigit(cast_uchar(*s)))  /* read exponent */
//       exp1 = exp1 * 10 + *(s++) - '0';
//     if (neg1) exp1 = -exp1;
//     e += exp1;
//     *endptr = cast_charp(s);  /* valid up to here */
//   }
//   if (neg) r = -r;
//   return (ldexp)(r, e);
        throw new NotImplementedException();
    }
    
    /* }====================================================== */
    
// /* maximum length of a numeral to be converted to a number */
// #if !defined (L_MAXLENNUM)
// #define L_MAXLENNUM	200
// #endif

    /*
    ** Convert string 's' to a Lua number (put in 'result'). Return null on
    ** fail or the address of the ending '\0' on success. ('mode' == 'x')
    ** means a hexadecimal numeral.
    */
    private static byte* l_str2dloc(byte* s, double* result, int mode)
    {
        byte* endptr;
        *result = strtod(s, &endptr);
        if (endptr == s)
        {
            return null; /* nothing recognised? */
        }

        while (lisspace(*endptr))
        {
            endptr++; /* skip trailing spaces */
        }

        return *endptr == '\0' ? endptr : null; /* OK iff no trailing chars */
    }

    /*
     ** Convert string 's' to a Lua number (put in 'result') handling the
     ** current locale.
     ** This function accepts both the current locale or a dot as the radix
     ** mark. If the conversion fails, it may mean number has a dot but
     ** locale accepts something else. In that case, the code copies 's'
     ** to a buffer (because 's' is read-only), changes the dot to the
     ** current locale radix mark, and tries to convert again.
     ** The variable 'mode' checks for special characters in the string:
     ** - 'n' means 'inf' or 'nan' (which should be rejected)
     ** - 'x' means a hexadecimal numeral
     ** - '.' just optimises the search for the common case (no special chars)
     */
    private static byte* l_str2d(byte* s, double* result)
    {
        byte* pmode = strpbrk(s, ".xXnN"); /* look for special chars */
        int mode = pmode != null ? ltolower(*pmode) : 0;
        if (mode == 'n') /* reject 'inf' and 'nan' */
        {
            return null;
        }

        byte* endptr = l_str2dloc(s, result, mode); /* try to convert */
        if (endptr == null)
        {
            /* failed? may be a different locale */
//     char buff[L_MAXLENNUM + 1];
//     const char *pdot = strchr(s, '.');
//     if (pdot == null || strlen(s) > L_MAXLENNUM)
//       return null;  /* string too long or no dot; fail */
//     strcpy(buff, s);  /* copy string to buffer */
//     buff[pdot - s] = lua_getlocaledecpoint();  /* correct decimal point */
//     endptr = l_str2dloc(buff, result, mode);  /* try again */
//     if (endptr != null)
//       endptr = s + (endptr - buff);  /* make relative to 's' */
            throw new NotImplementedException();
        }

        return endptr;
    }

    private const ulong MAXBY10 = long.MaxValue / 10;
    private const int MAXLASTD = (int)(long.MaxValue % 10);

    private static byte* l_str2int(byte* s, long* result)
    {
        while (lisspace(*s))
        {
            s++; /* skip initial spaces */
        }

        bool neg = isneg(ref s);
        bool empty = true;
        ulong a = 0;
        if (s[0] == '0' && (s[1] == 'x' || s[1] == 'X'))
        {
            /* hex? */
            s += 2; /* skip '0x' */
            for (; lisxdigit(*s); s++)
            {
                a = a * 16 + luaO_hexavalue(*s);
                empty = false;
            }
        }
        else
        {
            /* decimal */
            for (; lisdigit(*s); s++)
            {
                int d = *s - '0';
                if (a >= MAXBY10 && (a > MAXBY10 || d > MAXLASTD + (neg ? 1 : 0))) /* overflow? */
                {
                    return null; /* do not accept it (as integer) */
                }

                a = a * 10 + (uint)d;
                empty = false;
            }
        }

        while (lisspace(*s))
        {
            s++; /* skip trailing spaces */
        }

        if (empty || *s != '\0')
        {
            return null; /* something wrong in the numeral */
        }

        *result = (long)((neg) ? 0u - a : a);
        return s;
    }

    private static partial long luaO_str2num(byte* s, TValue* o)
    {
        long i;
        double n;
        byte* e;
        if ((e = l_str2int(s, &i)) != null)
        {
            /* try as an integer */
            setivalue(o, i);
        }
        else if ((e = l_str2d(s, &n)) != null)
        {
            /* else try as a float */
            setfltvalue(o, n);
        }
        else
        {
            return 0; /* conversion failed */
        }

        return e - s + 1;  /* success; return string size */
    }

    private static partial int luaO_utf8esc(byte* buff, uint x)
    {
        int n = 1; /* number of bytes put in buffer (backwards) */
        Debug.Assert(x <= 0x7FFFFFFFu);
        if (x < 0x80) /* ASCII? */
        {
            buff[UTF8BUFFSZ - 1] = (byte)x;
        }

        else
        {
            /* need continuation bytes */
            uint mfb = 0x3f; /* maximum that fits in first byte */
            do
            {
                /* add continuation bytes */
                buff[UTF8BUFFSZ - (n++)] = (byte)(0x80 | (x & 0x3f));
                x >>= 6; /* remove added bits */
                mfb >>= 1; /* now there is one less bit available in first byte */
            } while (x > mfb); /* still needs continuation byte? */

            buff[UTF8BUFFSZ - n] = (byte)(~mfb << 1 | x); /* add first byte */
        }

        return n;
    }

    /*
     ** The size of the buffer for the conversion of a number to a string
     ** 'LUA_N2SBUFFSZ' must be enough to accommodate both LUA_INTEGER_FMT
     ** and LUA_NUMBER_FMT.  For a long long int, this is 19 digits plus a
     ** sign and a final '\0', adding to 21. For a long double, it can go to
     ** a sign, the dot, an exponent letter, an exponent sign, 4 exponent
     ** digits, the final '\0', plus the significant digits, which are
     ** approximately the *_DIG attribute.
     */
// #if LUA_N2SBUFFSZ < (20 + l_floatatt(DIG))
// #error "invalid value for LUA_N2SBUFFSZ"
// #endif

// /*
// ** Convert a float to a string, adding it to a buffer. First try with
// ** a not too large number of digits, to avoid noise (for instance,
// ** 1.1 going to "1.1000000000000001"). If that lose precision, so
// ** that reading the result back gives a different number, then do the
// ** conversion again with extra precision. Moreover, if the numeral looks
// ** like an integer (without a decimal point or an exponent), add ".0" to
// ** its end.
// */
// static int tostringbuffFloat (double n, char *buff) {
//   /* first conversion */
//   int len = l_sprintf(buff, LUA_N2SBUFFSZ, LUA_NUMBER_FMT,
//                             (LUAI_UACNUMBER)n);
//   double check = lua_str2number(buff, null);  /* read it back */
//   if (check != n) {  /* not enough precision? */
//     /* convert again with more precision */
//     len = l_sprintf(buff, LUA_N2SBUFFSZ, LUA_NUMBER_FMT_N,
//                           (LUAI_UACNUMBER)n);
//   }
//   /* looks like an integer? */
//   if (buff[strspn(buff, "-0123456789")] == '\0') {
//     buff[len++] = lua_getlocaledecpoint();
//     buff[len++] = '0';  /* adds '.0' to result */
//   }
//   return len;
// }

    /*
    ** Convert a number object to a string, adding it to a buffer.
    */
    private static partial uint luaO_tostringbuff(TValue* obj, byte* buff)
    {
//   int len;
//   Debug.Assert(ttisnumber(obj));
//   if (ttisinteger(obj))
//     len = lua_integer2str(buff, LUA_N2SBUFFSZ, ivalue(obj));
//   else
//     len = tostringbuffFloat(fltvalue(obj), buff);
//   Debug.Assert(len < LUA_N2SBUFFSZ);
//   return cast_uint(len);
        throw new NotImplementedException();
    }
    
    /*
    ** Convert a number object to a Lua string, replacing the value at 'obj'
    */
    private static partial void luaO_tostring(lua_State* L, TValue* obj)
    {
//   char buff[LUA_N2SBUFFSZ];
//   unsigned len = luaO_tostringbuff(obj, buff);
//   setsvalue(L, obj, luaS_newlstr(L, buff, len));
        throw new NotImplementedException();
    }

    /*
     ** {==================================================================
     ** 'luaO_pushvfstring'
     ** ===================================================================
     */

    /*
     ** Size for buffer space used by 'luaO_pushvfstring'. It should be
     ** (LUA_IDSIZE + LUA_N2SBUFFSZ) + a minimal space for basic messages,
     ** so that 'luaG_addinfo' can work directly on the static buffer.
     */
    private const int BUFVFS = LUA_IDSIZE + LUA_N2SBUFFSZ + 95;

    /*
     ** Buffer used by 'luaO_pushvfstring'. 'err' signals an error while
     ** building result (memory error [1] or buffer overflow [2]).
     */
    private struct BuffFS
    {
        public lua_State* L;
        public byte* b;
        public int buffsize;
        public int blen; /* length of string in 'buff' */
        public int err;
        public fixed byte space[BUFVFS]; /* initial buffer */
    }

    private static void initbuff(lua_State* L, BuffFS* buff)
    {
        buff->L = L;
        buff->b = buff->space;
        buff->buffsize = BUFVFS;
        buff->blen = 0;
        buff->err = 0;
    }

    /*
     ** Push final result from 'luaO_pushvfstring'. This function may raise
     ** errors explicitly or through memory errors, so it must run protected.
     */
    private static void pushbuff(lua_State* L, void* ud)
    {
        BuffFS* buff = (BuffFS*)ud;
        switch (buff->err)
        {
            case 1: /* memory error */
                luaD_throw(L, LUA_ERRMEM);
                break;

            case 2: /* length overflow: Add "..." at the end of result */
//       if (buff->buffsize - buff->blen < 3)
//         strcpy(buff->b + buff->blen - 3, "...");  /* 'blen' must be > 3 */
//       else {  /* there is enough space left for the "..." */
//         strcpy(buff->b + buff->blen, "...");
//         buff->blen += 3;
//       }
                throw new NotImplementedException();
//       /* FALLTHROUGH */
            default:
                {
                    /* no errors, but it can raise one creating the new string */
                    TString* ts = luaS_newlstr(L, buff->b, buff->blen);
                    setsvalue2s(L, L->top.p, ts);
                    L->top.p++;
                    break;
                }
        }
    }

    private static string? clearbuff(BuffFS* buff)
    {
        string? res;
        if (luaD_rawrunprotected(buff->L, pushbuff, buff) != LUA_OK) /* errors? */
        {
            res = null; /* error message is on the top of the stack */
        }
        else
        {
            TString* ts = tsvalue(s2v(buff->L->top.p - 1));
            return getnetstr(ts);
        }

        if (buff->b != buff->space) /* using dynamic buffer? */
        {
            luaM_freearray(buff->L, buff->b, buff->buffsize); /* free it */
        }

        return res;
    }

    private static void addstr2buff(BuffFS* buff, string str)
    {
        byte[] data = Encoding.UTF8.GetBytes(str);
        addstr2buff(buff, data, data.Length);
    }

    private static void addstr2buff(BuffFS* buff, ReadOnlySpan<byte> str, int slen)
    {
        int left = buff->buffsize - buff->blen; /* space left in the buffer */
        if (buff->err != 0) /* do nothing else after an error */
        {
            return;
        }

        if (slen > left)
        {
            /* new string doesn't fit into current buffer? */
//     if (slen > ((MAX_SIZE/2) - buff->blen)) {  /* overflow? */
//       memcpy(buff->b + buff->blen, str, left);  /* copy what it can */
//       buff->blen = buff->buffsize;
//       buff->err = 2;  /* doesn't add anything else */
//       return;
//     }
//     else {
//       size_t newsize = buff->buffsize + slen;  /* limited to MAX_SIZE/2 */
//       char *newb =
//         (buff->b == buff->space)  /* still using static space? */
//         ? luaM_reallocvector(buff->L, null, 0, newsize, char)
//         : luaM_reallocvector(buff->L, buff->b, buff->buffsize, newsize,
//                                                                char);
//       if (newb == null) {  /* allocation error? */
//         buff->err = 1;  /* signal a memory error */
//         return;
//       }
//       if (buff->b == buff->space)  /* new buffer (not reallocated)? */
//         memcpy(newb, buff->b, buff->blen);  /* copy previous content */
//       buff->b = newb;  /* set new (larger) buffer... */
//       buff->buffsize = newsize;  /* ...and its new size */
//     }
            throw new NotImplementedException();
        }

        Span<byte> dest = new(buff->b + buff->blen, slen);
        str[..slen].CopyTo(dest); /* copy new content */
        buff->blen += slen;
    }

// /*
// ** Add a numeral to the buffer.
// */
// static void addnum2buff (BuffFS *buff, TValue *num) {
//   char numbuff[LUA_N2SBUFFSZ];
//   unsigned len = luaO_tostringbuff(num, numbuff);
//   addstr2buff(buff, numbuff, len);
// }

    /*
    ** this function handles only '%d', '%c', '%f', '%p', '%s', and '%%'
       conventional formats, plus Lua-specific '%I' and '%U'
    */
    private static partial string luaO_pushfstring(lua_State* L, string fmt, params object[] args)
    {
        byte[] tmp = Encoding.UTF8.GetBytes(fmt);
        ReadOnlySpan<byte> fmtSpan = tmp;
        BuffFS buff; /* holds last part of the result */
        initbuff(L, &buff);
        ReadOnlySpan<byte> e; /* points to next '%' */
        int i = 0;
        while (!(e = strchr(fmtSpan, '%')).IsEmpty)
        {
            addstr2buff(&buff, fmtSpan, fmt.Length - e.Length); /* add 'fmt' up to '%' */
            switch ((char)e[1])
            {
                /* conversion specifier */
                case 's':
                    {
                        /* zero-terminated string */
                        string s = args[i++].ToString() ?? "(null)";
                        addstr2buff(&buff, s);
                        break;
                    }

                case 'c':
                    {
                        /* an 'int' as a character */
//         char c = cast_char(va_arg(argp, int));
//         addstr2buff(&buff, &c, sizeof(char));
//         break;
                        throw new NotImplementedException();
                    }
                case 'd':
                    {
                        /* an 'int' */
                        TValue num;
                        setivalue(&num, (int)args[i++]);
//         addnum2buff(&buff, &num);
//         break;
                        throw new NotImplementedException();
                    }
                
                case 'I':
                    {
                        /* a 'long' */
//         TValue num;
//         setivalue(&num, cast_Integer(va_arg(argp, l_uacInt)));
//         addnum2buff(&buff, &num);
//         break;
                        throw new NotImplementedException();
                    }
                case 'f':
                    {
                        /* a 'double' */
//         TValue num;
//         setfltvalue(&num, cast_num(va_arg(argp, l_uacNumber)));
//         addnum2buff(&buff, &num);
//         break;
                        throw new NotImplementedException();
                    }
                case 'p':
                    {
                        /* a pointer */
//         char bf[LUA_N2SBUFFSZ];  /* enough space for '%p' */
//         void *p = va_arg(argp, void *);
//         int len = lua_pointer2str(bf, LUA_N2SBUFFSZ, p);
//         addstr2buff(&buff, bf, cast_uint(len));
//         break;
                        throw new NotImplementedException();
                    }
                case 'U':
                    {
                        /* an 'unsigned long' as a UTF-8 sequence */
//         char bf[UTF8BUFFSZ];
//         unsigned long arg = va_arg(argp, unsigned long);
//         int len = luaO_utf8esc(bf, cast(uint, arg));
//         addstr2buff(&buff, bf + UTF8BUFFSZ - len, cast_uint(len));
//         break;
                        throw new NotImplementedException();
                    }
                case '%':
                    {
//         addstr2buff(&buff, "%", 1);
//         break;
                        throw new NotImplementedException();
                    }
                default:
                    {
//         addstr2buff(&buff, e, 2);  /* keep unknown format in the result */
//         break;
                        throw new NotImplementedException();
                    }
            }

            fmtSpan = e[2..]; /* skip '%' and the specifier */
        }

        addstr2buff(&buff, fmtSpan, fmtSpan.Length); /* rest of 'fmt' */
        string? msg = clearbuff(&buff); /* empty buffer into a new string */

        if (msg == null) /* error? */
        {
            luaD_throw(L, LUA_ERRMEM);
        }

        return msg;
    }

    /* }================================================================== */

// #define RETS	"..."
// #define PRE	"[string \""
// #define POS	"\"]"
//
// #define addstr(a,b,l)	( memcpy(a,b,(l) * sizeof(char)), a += (l) )

    private static partial void luaO_chunkid(byte* @out, byte* source, long srclen)
    {
//   size_t bufflen = LUA_IDSIZE;  /* free space in buffer */
//   if (*source == '=') {  /* 'literal' source */
//     if (srclen <= bufflen)  /* small enough? */
//       memcpy(out, source + 1, srclen * sizeof(char));
//     else {  /* truncate it */
//       addstr(out, source + 1, bufflen - 1);
//       *out = '\0';
//     }
//   }
//   else if (*source == '@') {  /* file name */
//     if (srclen <= bufflen)  /* small enough? */
//       memcpy(out, source + 1, srclen * sizeof(char));
//     else {  /* add '...' before rest of name */
//       addstr(out, RETS, LL(RETS));
//       bufflen -= LL(RETS);
//       memcpy(out, source + 1 + srclen - bufflen, bufflen * sizeof(char));
//     }
//   }
//   else {  /* string; format as [string "source"] */
//     const char *nl = strchr(source, '\n');  /* find first new line (if any) */
//     addstr(out, PRE, LL(PRE));  /* add prefix */
//     bufflen -= LL(PRE RETS POS) + 1;  /* save space for prefix+suffix+'\0' */
//     if (srclen < bufflen && nl == null) {  /* small one-line source? */
//       addstr(out, source, srclen);  /* keep it */
//     }
//     else {
//       if (nl != null)
//         srclen = ct_diff2sz(nl - source);  /* stop at first newline */
//       if (srclen > bufflen) srclen = bufflen;
//       addstr(out, source, srclen);
//       addstr(out, RETS, LL(RETS));
//     }
//     memcpy(out, POS, (LL(POS) + 1) * sizeof(char));
//   }
        throw new NotImplementedException();
    }
}
