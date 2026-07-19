namespace DigitalSingularity.Lua;

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    // $Id: lstrlib.c $
    // Standard library for string operations and pattern-matching
    // See Copyright Notice in lua.h

    /// <summary>
    /// maximum number of captures that a pattern can do during
    /// pattern-matching. This limit is arbitrary, but must fit in
    /// an unsigned char.
    /// </summary>
    private const int LUA_MAXCAPTURES = 32;

    private static int str_len(lua_State* L)
    {
        int l = luaL_checklstring(L, 1).Length;
        lua_pushinteger(L, l);
        return 1;
    }

    /// <summary>
    /// translate a relative initial string position
    /// (negative means back from end): clip result to [1, inf).
    /// The length of any string in Lua must fit in a long,
    /// so there are no overflows in the casts.
    /// The inverted comparison avoids a possible overflow
    /// computing '-pos'.
    /// </summary>
    private static long posrelatI(long pos, long len)
    {
        if (pos > 0)
        {
            return pos;
        }

        if (pos == 0)
        {
            return 1;
        }

        if (pos < -len) // inverted comparison
        {
            return 1; // clip to 1
        }

        return len + pos + 1;
    }

    /// <summary>
    /// Gets an optional ending string position from argument 'arg',
    /// with default value 'def'.
    /// Negative means back from end: clip result to [0, len]
    /// </summary>
    private static long getendpos(lua_State* L, int arg, long def, long len)
    {
        long pos = luaL_optinteger(L, arg, def);
        if (pos > len)
        {
            return len;
        }

        if (pos >= 0)
        {
            return pos;
        }

        if (pos < -len)
        {
            return 0;
        }

        return len + pos + 1;
    }

    private static int str_sub(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        int start = (int)posrelatI(luaL_checkinteger(L, 2), s.Length);
        int end = (int)getendpos(L, 3, -1, s.Length);
        if (start <= end)
        {
            lua_pushlstring(L, s.Slice(start - 1, (end - start) + 1));
        }
        else
        {
            lua_pushliteral(L, "");
        }

        return 1;
    }

    private static int str_reverse(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        luaL_Buffer b;
        byte* p = luaL_buffinitsize(L, &b, s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            p[i] = s[s.Length - i - 1];
        }

        luaL_pushresultsize(&b, s.Length);
        return 1;
    }

    private static int str_lower(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);

        luaL_Buffer b;
        byte* p = luaL_buffinitsize(L, &b, s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            p[i] = (byte)char.ToLowerInvariant((char)s[i]);
        }

        luaL_pushresultsize(&b, s.Length);
        return 1;
    }

    private static int str_upper(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);

        luaL_Buffer b;
        byte* p = luaL_buffinitsize(L, &b, s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            p[i] = (byte)char.ToUpperInvariant((char)s[i]);
        }

        luaL_pushresultsize(&b, s.Length);
        return 1;
    }

    /// <summary>
    /// MAX_SIZE is limited both by size_t and long.
    /// When x &lt;= MAX_SIZE, x can be safely cast to size_t or long.
    /// </summary>
    private static int str_rep(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        long n = luaL_checkinteger(L, 2);
        ReadOnlySpan<byte> sep = luaL_optlstring(L, 3);
        if (n <= 0)
        {
            lua_pushliteral(L, "");
            return 1;
        }

        if (s.Length + sep.Length > long.MaxValue / n)
        {
            return luaL_error(L, "resulting string too large");
        }

        long totallen = n * (s.Length + sep.Length) - sep.Length;
        luaL_Buffer b;
        byte* p = luaL_buffinitsize(L, &b, totallen);
        Span<byte> dest = new(p, checked((int)totallen));
        while (n-- > 1)
        {
            // first n-1 copies (followed by separator)
            s.CopyTo(dest);
            dest = dest[s.Length..];
            if (sep.Length > 0)
            {
                // empty 'memcpy' is not that cheap
                sep.CopyTo(dest);
                dest = dest[sep.Length..];
            }
        }

        s.CopyTo(dest);
        luaL_pushresultsize(&b, totallen);

        return 1;
    }

    private static int str_byte(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        long pi = luaL_optinteger(L, 2, 1);
        long posi = posrelatI(pi, s.Length);
        long pose = getendpos(L, 3, pi, s.Length);
        if (posi > pose)
        {
            return 0; // empty interval; return no values
        }

        if (pose - posi >= int.MaxValue) // arithmetic overflow?
        {
            return luaL_error(L, "string slice too long");
        }

        int n = (int)(pose - posi) + 1;
        luaL_checkstack(L, n, "string slice too long");
        for (int i = 0; i < n; i++)
        {
            lua_pushinteger(L, s[(int)(posi + i - 1)]);
        }

        return n;
    }

    private static int str_char(lua_State* L)
    {
        int n = lua_gettop(L); // number of arguments
        luaL_Buffer b;
        byte* p = luaL_buffinitsize(L, &b, n);
        for (int i = 1; i <= n; i++)
        {
            ulong c = (ulong)luaL_checkinteger(L, i);
            luaL_argcheck(L, c <= byte.MaxValue, i, "value out of range");
            p[i - 1] = (byte)c;
        }

        luaL_pushresultsize(&b, n);
        return 1;
    }

    /// <summary>
    /// Buffer to store the result of 'string.dump'. It must be initialised
    /// after the call to 'lua_dump', to ensure that the function is on the
    /// top of the stack when 'lua_dump' is called. ('luaL_buffinit' might
    /// push stuff.)
    /// </summary>
    private struct str_Writer
    {
        public bool init; // true iff buffer has been initialised
        public luaL_Buffer B;
    }

    private static int writer(lua_State* L, void* b, long size, void* ud)
    {
        str_Writer* state = (str_Writer*)ud;
        if (!state->init)
        {
            state->init = true;
            luaL_buffinit(L, &state->B);
        }

        if (b == null)
        {
            // finishing dump?
            luaL_pushresult(&state->B); // push result
            lua_replace(L, 1); // move it to reserved slot
        }
        else
        {
            luaL_addlstring(&state->B, new ReadOnlySpan<byte>((byte*)b, checked((int)size)));
        }

        return 0;
    }

    private static int str_dump(lua_State* L)
    {
        bool strip = lua_toboolean(L, 2);
        luaL_argcheck(
            L,
            lua_type(L, 1) == LUA_TFUNCTION && !lua_iscfunction(L, 1),
            1,
            "Lua function expected");
        // ensure function is on the top of the stack and vacate slot 1
        lua_pushvalue(L, 1);
        str_Writer state = new();
        lua_dump(L, &writer, &state, strip);
        lua_settop(L, 1); // leave final result on top
        return 1;
    }

    // {======================================================
    // METAMETHODS
    // =======================================================

#if LUA_NOCVTS2N
    // no coercion from strings to numbers

    private static readonly luaL_Reg[] stringmetamethods =
    [
        new("__index", null), // placeholder
    ];

#else
    private static bool tonum(lua_State* L, int arg)
    {
        if (lua_type(L, arg) == LUA_TNUMBER)
        {
            // already a number?
            lua_pushvalue(L, arg);
            return true;
        }

        // check whether it is a numerical string
        string? s = lua_tonetstring(L, arg);
        return s != null && lua_stringtonumber(L, s) == s.Length + 1;
    }

    /// <summary>
    /// To be here, either the first operand was a string or the first
    /// operand didn't have a corresponding metamethod. (Otherwise, that
    /// other metamethod would have been called.) So, if this metamethod
    /// doesn't work, the only other option would be for the second
    /// operand to have a different metamethod.
    /// </summary>
    private static void trymt(lua_State* L, string mtkey, string opname)
    {
        lua_settop(L, 2); // back to the original arguments
        if (lua_type(L, 2) == LUA_TSTRING ||
            luaL_getmetafield(L, 2, mtkey) == 0)
        {
            luaL_error(
                L,
                "attempt to %s a '%s' with a '%s'",
                opname,
                luaL_typename(L, -2),
                luaL_typename(L, -1));
        }

        lua_insert(L, -3); // put metamethod before arguments
        lua_call(L, 2, 1); // call metamethod
    }

    private static int arith(lua_State* L, int op, string mtname)
    {
        if (tonum(L, 1) && tonum(L, 2))
        {
            lua_arith(L, op); // result will be on the top
        }
        else
        {
            trymt(L, mtname, mtname[2..]);
        }

        return 1;
    }

    private static int arith_add(lua_State* L)
    {
        return arith(L, LUA_OPADD, "__add");
    }

    private static int arith_sub(lua_State* L)
    {
        return arith(L, LUA_OPSUB, "__sub");
    }

    private static int arith_mul(lua_State* L)
    {
        return arith(L, LUA_OPMUL, "__mul");
    }

    private static int arith_mod(lua_State* L)
    {
        return arith(L, LUA_OPMOD, "__mod");
    }

    private static int arith_pow(lua_State* L)
    {
        return arith(L, LUA_OPPOW, "__pow");
    }

    private static int arith_div(lua_State* L)
    {
        return arith(L, LUA_OPDIV, "__div");
    }

    private static int arith_idiv(lua_State* L)
    {
        return arith(L, LUA_OPIDIV, "__idiv");
    }

    private static int arith_unm(lua_State* L)
    {
        return arith(L, LUA_OPUNM, "__unm");
    }

    private static readonly luaL_Reg[] stringmetamethods =
    [
        new("__add", CFunction.FromFunction(&arith_add)),
        new("__sub", CFunction.FromFunction(&arith_sub)),
        new("__mul", CFunction.FromFunction(&arith_mul)),
        new("__mod", CFunction.FromFunction(&arith_mod)),
        new("__pow", CFunction.FromFunction(&arith_pow)),
        new("__div", CFunction.FromFunction(&arith_div)),
        new("__idiv", CFunction.FromFunction(&arith_idiv)),
        new("__unm", CFunction.FromFunction(&arith_unm)),
        new("__index", default), // placeholder
    ];
#endif

    // {======================================================
    // PATTERN MATCHING
    // =======================================================

    private const int CAP_UNFINISHED = -1;
    private const int CAP_POSITION = -2;

    private struct MatchState
    {
        public struct Capture
        {
            public byte* init;
            public long len; // length or special value (CAP_*)
        }

        public struct CaptureArray
        {
            public Capture capture0;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
            public Capture capture1;
            public Capture capture2;
            public Capture capture3;
            public Capture capture4;
            public Capture capture5;
            public Capture capture6;
            public Capture capture7;
            public Capture capture8;
            public Capture capture9;
            public Capture capture10;
            public Capture capture11;
            public Capture capture12;
            public Capture capture13;
            public Capture capture14;
            public Capture capture15;
            public Capture capture16;
            public Capture capture17;
            public Capture capture18;
            public Capture capture19;
            public Capture capture20;
            public Capture capture21;
            public Capture capture22;
            public Capture capture23;
            public Capture capture24;
            public Capture capture25;
            public Capture capture26;
            public Capture capture27;
            public Capture capture28;
            public Capture capture29;
            public Capture capture30;
            public Capture capture31;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value

            static CaptureArray()
            {
                Debug.Assert(LUA_MAXCAPTURES <= 32);
            }

            public ref Capture this[int index]
            {
                get => ref Unsafe.Add(ref this.capture0, index);
            }
        }

        public byte* src_init; // init of source string
        public byte* src_end; // end ('\0') of source string
        public byte* p_end; // end ('\0') of pattern
        public lua_State* L;
        public int matchdepth; // control for recursive depth (to avoid C stack overflow)
        public int level; // total number of captures (finished or unfinished)
        public CaptureArray capture;
    }

    /// <summary>
    /// maximum recursion depth for 'match'
    /// </summary>
    private const int MAXCCALLS = 200;

    private const char L_ESC = '%';
    private static readonly SearchValues<byte> specialsSearch = SearchValues.Create("^$*+?.([%-"u8);

    private static int check_capture(ref MatchState ms, int l)
    {
        l -= '1';
        if (l < 0 ||
            l >= ms.level ||
            ms.capture[l].len == CAP_UNFINISHED)
        {
            return luaL_error(ms.L, "invalid capture index %%%d", l + 1);
        }

        return l;
    }

    private static int capture_to_close(ref MatchState ms)
    {
        int level = ms.level;
        for (level--; level >= 0; level--)
        {
            if (ms.capture[level].len == CAP_UNFINISHED)
            {
                return level;
            }
        }

        return luaL_error(ms.L, "invalid pattern capture");
    }

    private static byte* classend(ref MatchState ms, byte* p)
    {
        switch (*p++)
        {
            case (byte)L_ESC:
                if (p == ms.p_end)
                {
                    luaL_error(ms.L, "malformed pattern (ends with '%%')");
                }

                return p + 1;
            
            case (byte)'[':
                if (*p == '^')
                {
                    p++;
                }

                do
                {
                    // look for a ']'
                    if (p == ms.p_end)
                    {
                        luaL_error(ms.L, "malformed pattern (missing ']')");
                    }

                    if (*p++ == L_ESC && p < ms.p_end)
                    {
                        p++; // skip escapes (e.g. '%]')
                    }
                } while (*p != ']');

                return p + 1;

            default:
                return p;
        }
    }

    private static bool match_class(char c, char cl)
    {
        bool res;
        switch (char.ToLowerInvariant(cl))
        {
            case 'a': res = char.IsLetter(c); break;
            case 'c': res = char.IsControl(c); break;
            case 'd': res = char.IsDigit(c); break;
            case 'g': res = c is >= '!' and <= '~'; break;
            case 'l': res = char.IsLower(c); break;
            case 'p': res = char.IsPunctuation(c) || char.IsSymbol(c); break;
            case 's': res = char.IsWhiteSpace(c); break;
            case 'u': res = char.IsUpper(c); break;
            case 'w': res = char.IsLetterOrDigit(c); break;
            case 'x': res = char.IsAsciiHexDigit(c); break;
            case 'z': res = c == 0; break; // deprecated option
            default: return cl == c;
        }

        return char.IsLower(cl) ? res : !res;
    }

    private static bool matchbracketclass(char c, byte* p, byte* ec)
    {
        bool sig = true;
        if (*(p + 1) == '^')
        {
            sig = false;
            p++; // skip the '^'
        }

        while (++p < ec)
        {
            if (*p == L_ESC)
            {
                p++;
                if (match_class(c, (char)*p))
                {
                    return sig;
                }
            }
            else if (*(p + 1) == '-' && p + 2 < ec)
            {
                p += 2;
                if ((char)*(p - 2) <= c && c <= (char)*p)
                {
                    return sig;
                }
            }
            else if ((char)*p == c)
            {
                return sig;
            }
        }

        return !sig;
    }

    private static bool singlematch(
        ref MatchState ms,
        byte* s,
        byte* p,
        byte* ep)
    {
        if (s >= ms.src_end)
        {
            return false;
        }

        char c = (char)*s;
        return (*p) switch
        {
            (byte)'.' => true , // matches any char
            (byte)L_ESC => match_class(c, (char)*(p + 1)),
            (byte)'[' => matchbracketclass(c, p, ep - 1),
            _ => (char)*p == c,
        };
    }

    private static byte* matchbalance(
        ref MatchState ms,
        byte* s,
        byte* p)
    {
        if (p >= ms.p_end - 1)
        {
            luaL_error(ms.L, "malformed pattern (missing arguments to '%%b')");
        }

        if (*s != *p)
        {
            return null;
        }

        int b = *p;
        int e = *(p + 1);
        int cont = 1;
        while (++s < ms.src_end)
        {
            if (*s == e)
            {
                if (--cont == 0)
                {
                    return s + 1;
                }
            }
            else if (*s == b)
            {
                cont++;
            }
        }

        return null; // string ends out of balance
    }

    private static byte* max_expand(
        ref MatchState ms,
        byte* s,
        byte* p,
        byte* ep)
    {
        long i = 0; // counts maximum expand for item
        while (singlematch(ref ms, s + i, p, ep))
        {
            i++;
        }

        // keeps trying to match with the maximum repetitions
        while (i >= 0)
        {
            byte* res = match(ref ms, s + i, ep + 1);
            if (res != null)
            {
                return res;
            }

            i--; // else didn't match; reduce 1 repetition to try again
        }

        return null;
    }

    private static byte* min_expand(
        ref MatchState ms,
        byte* s,
        byte* p,
        byte* ep)
    {
        for (;;)
        {
            byte* res = match(ref ms, s, ep + 1);
            if (res != null)
            {
                return res;
            }

            if (singlematch(ref ms, s, p, ep))
            {
                s++; // try with one more repetition
            }
            else
            {
                return null;
            }
        }
    }

    private static byte* start_capture(
        ref MatchState ms,
        byte* s,
        byte* p,
        int what)
    {
        int level = ms.level;
        if (level >= LUA_MAXCAPTURES)
        {
            luaL_error(ms.L, "too many captures");
        }

        ms.capture[level].init = s;
        ms.capture[level].len = what;
        ms.level = level + 1;
        
        byte* res;
        if ((res = match(ref ms, s, p)) == null) // match failed?
        {
            ms.level--; // undo capture
        }

        return res;
    }

    private static byte* end_capture(
        ref MatchState ms,
        byte* s,
        byte* p)
    {
        int l = capture_to_close(ref ms);
        ms.capture[l].len = s - ms.capture[l].init; // close capture

        byte* res;
        if ((res = match(ref ms, s, p)) == null) // match failed?
        {
            ms.capture[l].len = CAP_UNFINISHED; // undo capture
        }

        return res;
    }

    private static byte* match_capture(ref MatchState ms, byte* s, int l)
    {
        l = check_capture(ref ms, l);
        long len = ms.capture[l].len;
        if (ms.src_end - s >= len &&
            memcmp(ms.capture[l].init, s, len) == 0)
        {
            return s + len;
        }

        return null;
    }

    private static byte* match(ref MatchState ms, byte* s, byte* p)
    {
        if (ms.matchdepth-- == 0)
        {
            luaL_error(ms.L, "pattern too complex");
        }

        init: // using goto to optimise tail recursion
        if (p != ms.p_end)
        {
            // end of pattern?
            switch (*p)
            {
                case (byte)'(':
                    {
                        // start capture
                        if (*(p + 1) == ')') // position capture?
                        {
                            s = start_capture(ref ms, s, p + 2, CAP_POSITION);
                        }
                        else
                        {
                            s = start_capture(ref ms, s, p + 1, CAP_UNFINISHED);
                        }

                        break;
                    }

                case (byte)')':
                    // end capture
                    s = end_capture(ref ms, s, p + 1);
                    break;

                case (byte)'$':
                    if (p + 1 != ms.p_end) // is the '$' the last char in pattern?
                    {
                        goto dflt; // no; go to default
                    }

                    s = s == ms.src_end ? s : null; // check end of string
                    break;

                case (byte)L_ESC:
                    // escaped sequences not in the format class[*+?-]?
                    switch (*(p + 1))
                    {
                        case (byte)'b':
                            // balanced string?
                            s = matchbalance(ref ms, s, p + 2);
                            if (s != null)
                            {
                                p += 4;
                                goto init; // return match(ms, s, p + 4);
                            } // else fail (s == NULL)

                            break;

                        case (byte)'f':
                            {
                                // frontier?
                                p += 2;
                                if (*p != '[')
                                {
                                    luaL_error(ms.L, "missing '[' after '%%f' in pattern");
                                }

                                byte* ep = classend(ref ms, p); // points to what is next
                                byte previous = s == ms.src_init ? (byte)0 : *(s - 1);
                                if (!matchbracketclass((char)previous, p, ep - 1) &&
                                    matchbracketclass((char)*s, p, ep - 1))
                                {
                                    p = ep;
                                    goto init; // return match(ms, s, ep);
                                }

                                s = null; // match failed
                                break;
                            }

                        case (byte)'0':
                        case (byte)'1':
                        case (byte)'2':
                        case (byte)'3':
                        case (byte)'4':
                        case (byte)'5':
                        case (byte)'6':
                        case (byte)'7':
                        case (byte)'8':
                        case (byte)'9':
                            // capture results (%0-%9)?
                            s = match_capture(ref ms, s, *(p + 1));
                            if (s != null)
                            {
                                p += 2;
                                goto init; // return match(ms, s, p + 2)
                            }

                            break;

                        default:
                            goto dflt;
                    }

                    break;

                default:
                    dflt:
                {
                    // pattern class plus optional suffix
                    byte* ep = classend(ref ms, p); // points to optional suffix
                    // does not match at least once?
                    if (!singlematch(ref ms, s, p, ep))
                    {
                        if (*ep == '*' || *ep == '?' || *ep == '-')
                        {
                            // accept empty?
                            p = ep + 1;
                            goto init; // return match(ms, s, ep + 1);
                        }

                        // '+' or no suffix
                        s = null; // fail
                    }
                    else
                    {
                        // matched once
                        switch (*ep)
                        {
                            // handle optional suffix
                            case (byte)'?':
                                {
                                    // optional
                                    byte* res;
                                    if ((res = match(ref ms, s + 1, ep + 1)) != null)
                                    {
                                        s = res;
                                    }
                                    else
                                    {
                                        p = ep + 1;
                                        goto init; // else return match(ms, s, ep + 1);
                                    }

                                    break;
                                }

                            case (byte)'+': // 1 or more repetitions
                                s++; // 1 match already done
                                goto case (byte)'*';

                            case (byte)'*': // 0 or more repetitions
                                s = max_expand(ref ms, s, p, ep);
                                break;

                            case (byte)'-': // 0 or more repetitions (minimum)
                                s = min_expand(ref ms, s, p, ep);
                                break;

                            default: // no suffix
                                s++;
                                p = ep;
                                goto init; // return match(ms, s + 1, ep);
                        }
                    }

                    break;
                }
            }
        }

        ms.matchdepth++;
        return s;
    }

    private static ReadOnlySpan<byte> lmemfind(ReadOnlySpan<byte> s1, ReadOnlySpan<byte> s2)
    {
        int x = s1.IndexOf(s2);
        if (x >= 0)
        {
            return s1[x..];
        }

        return default;
    }

    /// <summary>
    /// get information about the i-th capture. If there are no captures
    /// and 'i==0', return information about the whole match, which
    /// is the range 's'..'e'. If the capture is a string, return
    /// its length and put its address in '*cap'. If it is an integer
    /// (a position), push it on the stack and return CAP_POSITION.
    /// </summary>
    private static long get_onecapture(
        ref MatchState ms,
        int i,
        byte* s,
        byte* e,
        byte** cap)
    {
        if (i >= ms.level)
        {
            if (i != 0)
            {
                luaL_error(ms.L, "invalid capture index %%%d", i + 1);
            }

            *cap = s;
            return e - s;
        }

        long capl = ms.capture[i].len;
        *cap = ms.capture[i].init;
        if (capl == CAP_UNFINISHED)
        {
            luaL_error(ms.L, "unfinished capture");
        }
        else if (capl == CAP_POSITION)
        {
            lua_pushinteger(
                ms.L,
                ms.capture[i].init - ms.src_init + 1);
        }

        return capl;
    }

    /// <summary>
    /// Push the i-th capture on the stack.
    /// </summary>
    private static void push_onecapture(
        ref MatchState ms,
        int i,
        byte* s,
        byte* e)
    {
        byte* cap;
        long l = get_onecapture(ref ms, i, s, e, &cap);
        if (l != CAP_POSITION)
        {
            lua_pushlstring(ms.L, new ReadOnlySpan<byte>(cap, (int)l));
        }
        // else position was already pushed
    }

    private static int push_captures(ref MatchState ms, byte* s, byte* e)
    {
        int nlevels = ms.level == 0 && s != null ? 1 : ms.level;
        luaL_checkstack(ms.L, nlevels, "too many captures");
        for (int i = 0; i < nlevels; i++)
        {
            push_onecapture(ref ms, i, s, e);
        }

        return nlevels; // number of strings pushed
    }

    /// <summary>
    /// check whether pattern has no special characters
    /// </summary>
    private static bool nospecials(ReadOnlySpan<byte> p)
    {
        return !p.ContainsAny(specialsSearch);
    }

    private static void prepstate(
        ref MatchState ms,
        lua_State* L,
        byte* s,
        int ls,
        byte* p,
        int lp)
    {
        ms.L = L;
        ms.matchdepth = MAXCCALLS;
        ms.src_init = s;
        ms.src_end = s + ls;
        ms.p_end = p + lp;
    }

    private static void reprepstate(ref MatchState ms)
    {
        ms.level = 0;
        Debug.Assert(ms.matchdepth == MAXCCALLS);
    }

    private static int str_find_aux(lua_State* L, bool find)
    {
        byte* s = luaL_checklstring(L, 1, out int ls);
        byte* p = luaL_checklstring(L, 2, out int lp);
        int sLength = ls;
        int pLength = lp;
        ReadOnlySpan<byte> sSpan = new(s, sLength);
        ReadOnlySpan<byte> pSpan = new(p, pLength);
        int init = checked((int)(posrelatI(luaL_optinteger(L, 3, 1), sLength) - 1));
        if (init > sLength)
        {
            // start after string's end?
            luaL_pushfail(L); // cannot find anything
            return 1;
        }

        // explicit request or no special characters?
        if (find && (lua_toboolean(L, 4) || nospecials(pSpan)))
        {
            if (pSpan.IsEmpty)
            {
                lua_pushinteger(L, init + 1);
                lua_pushinteger(L, init);
                return 2;
            }
            
            // do a plain search
            ReadOnlySpan<byte> s2 = lmemfind(sSpan[init..], pSpan);
            if (!s2.IsEmpty)
            {
                lua_pushinteger(L, sLength - s2.Length + 1);
                lua_pushinteger(L, sLength - s2.Length + pLength);
                return 2;
            }
        }
        else
        {
            MatchState ms = new();
            byte* s1p = s + init;
            bool anchor = *p == '^';
            if (anchor)
            {
                p++; // skip anchor character
                pLength--;
            }

            prepstate(ref ms, L, s, sLength, p, pLength);
            do
            {
                reprepstate(ref ms);

                byte* res;
                if ((res = match(ref ms, s1p, p)) != null)
                {
                    if (find)
                    {
                        lua_pushinteger(L, s1p - s + 1); // start
                        lua_pushinteger(L, res - s); // end
                        return push_captures(ref ms, null, null) + 2;
                    }

                    return push_captures(ref ms, s1p, res);
                }
            } while (s1p++ < ms.src_end && !anchor);
        }

        luaL_pushfail(L); // not found
        return 1;
    }

    private static int str_find(lua_State* L)
    {
        return str_find_aux(L, true);
    }

    private static int str_match(lua_State* L)
    {
        return str_find_aux(L, false);
    }

    /// <summary>
    /// state for 'gmatch'
    /// </summary>
    private struct GMatchState
    {
        public byte* src; // current position
        public byte* p; // pattern
        public byte* lastmatch; // end of last match
        public MatchState ms; // match state
    }

    private static int gmatch_aux(lua_State* L)
    {
        GMatchState* gm = (GMatchState*)lua_touserdata(L, lua_upvalueindex(3));
        gm->ms.L = L;
        for (byte* src = gm->src; src <= gm->ms.src_end; src++)
        {
            reprepstate(ref gm->ms);
            byte* e;
            if ((e = match(ref gm->ms, src, gm->p)) != null && e != gm->lastmatch)
            {
                gm->src = gm->lastmatch = e;
                return push_captures(ref gm->ms, src, e);
            }
        }

        return 0; // not found
    }

    private static int gmatch(lua_State* L)
    {
        byte* s = luaL_checklstring(L, 1, out int ls);
        byte* p = luaL_checklstring(L, 2, out int lp);
        long init = posrelatI(luaL_optinteger(L, 3, 1), ls) - 1;
        lua_settop(L, 2); // keep strings on closure to avoid being collected
        GMatchState* gm = (GMatchState*)lua_newuserdatauv(L, sizeof(GMatchState), 0);
        if (init > ls) // start after string's end?
        {
            init = ls + 1; // avoid overflows in 's + init'
        }

        prepstate(ref gm->ms, L, s, ls, p, lp);
        gm->src = s + init;
        gm->p = p;
        gm->lastmatch = null;
        lua_pushcclosure(L, CFunction.FromFunction(&gmatch_aux), 3);
        return 1;
    }

    private static void add_s(
        ref MatchState ms,
        luaL_Buffer* b,
        byte* s,
        byte* e)
    {
        lua_State* L = ms.L;
        byte* news = lua_tolstring(L, 3, out int l);
        byte* p;
        while ((p = memchr(news, (byte)L_ESC, l)) != null)
        {
            luaL_addlstring(b, new ReadOnlySpan<byte>(news, (int)(p - news)));
            p++; // skip ESC
            if (*p == L_ESC) // '%%'
            {
                luaL_addchar(b, *p);
            }
            else if (*p == '0') // '%0'
            {
                luaL_addlstring(b, new ReadOnlySpan<byte>(s, (int)(e - s)));
            }
            else if (char.IsAsciiDigit((char)*p))
            {
                // '%n'
                byte* cap;
                long resl = get_onecapture(ref ms, *p - '1', s, e, &cap);
                if (resl == CAP_POSITION)
                {
                    luaL_addvalue(b); // add position to accumulated result
                }
                else
                {
                    luaL_addlstring(b, new ReadOnlySpan<byte>(cap, (int)resl));
                }
            }
            else
            {
                luaL_error(L, "invalid use of '%c' in replacement string", L_ESC);
            }

            l -= (int)(p + 1 - news);
            news = p + 1;
        }

        luaL_addlstring(b, new ReadOnlySpan<byte>(news, (int)l));
    }

    /// <summary>
    /// Add the replacement value to the string buffer 'b'.
    /// Return true if the original string was changed. (Function calls and
    /// table indexing resulting in nil or false do not change the subject.)
    /// </summary>
    private static bool add_value(
        ref MatchState ms,
        luaL_Buffer* b,
        byte* s,
        byte* e,
        int tr)
    {
        lua_State* L = ms.L;
        switch (tr)
        {
            case LUA_TFUNCTION:
                {
                    // call the function
                    lua_pushvalue(L, 3); // push the function
                    int n = push_captures(ref ms, s, e); // all captures as arguments
                    lua_call(L, n, 1); // call it
                    break;
                }

            case LUA_TTABLE:
                // index the table
                push_onecapture(ref ms, 0, s, e); // first capture is the index
                lua_gettable(L, 3);
                break;

            default:
                // LUA_TNUMBER or LUA_TSTRING
                add_s(ref ms, b, s, e); // add value to the buffer
                return true; // something changed
        }

        if (!lua_toboolean(L, -1))
        {
            // nil or false?
            lua_pop(L, 1); // remove value
            luaL_addlstring(b, new Span<byte>(s, (int)(e - s))); // keep original text
            return false; // no changes
        }

        if (!lua_isstring(L, -1))
        {
            return luaL_error(L, "invalid replacement value (a %s)", luaL_typename(L, -1)) != 0;
        }

        luaL_addvalue(b); // add result to accumulator
        return true; // something changed
    }

    private static int str_gsub(lua_State* L)
    {
        ReadOnlySpan<byte> srcspan = luaL_checklstring(L, 1); // subject
        ReadOnlySpan<byte> pspan = luaL_checklstring(L, 2); // pattern
        byte* lastmatch = null; // end of last match
        int tr = lua_type(L, 3); // replacement type
        // max replacements
        long max_s = luaL_optinteger(L, 4, srcspan.Length + 1);
        bool anchor = !pspan.IsEmpty && pspan[0] == '^';
        long n = 0; // replacement count
        bool changed = false; // change flag
        luaL_argexpected(
            L,
            tr is LUA_TNUMBER or LUA_TSTRING or LUA_TFUNCTION or LUA_TTABLE,
            3,
            "string/function/table");
        luaL_Buffer b;
        luaL_buffinit(L, &b);
        if (anchor)
        {
            pspan = pspan[1..]; // skip anchor character
        }

        fixed (byte* srcp = srcspan)
        {
            byte empty = 0;
            byte* src = srcp == null ? &empty : srcp;
            
            fixed (byte* pp = pspan)
            {
                byte* p = pp == null ? &empty : pp;
                
                MatchState ms = new();
                prepstate(ref ms, L, src, srcspan.Length, p, pspan.Length);
                while (n < max_s)
                {
                    reprepstate(ref ms); // (re)prepare state for new match
                    byte* e;
                    if ((e = match(ref ms, src, p)) != null && e != lastmatch)
                    {
                        // match?
                        n++;
                        changed = add_value(ref ms, &b, src, e, tr) || changed;
                        src = lastmatch = e;
                    }
                    else if (src < ms.src_end) // otherwise, skip one character
                    {
                        luaL_addchar(&b, *src++);
                    }
                    else
                    {
                        break; // end of subject
                    }

                    if (anchor)
                    {
                        break;
                    }
                }

                if (!changed) // no changes?
                {
                    lua_pushvalue(L, 1); // return original string
                }
                else
                {
                    // something changed
                    luaL_addlstring(&b, new ReadOnlySpan<byte>(src, (int)(ms.src_end - src)));
                    luaL_pushresult(&b); // create and return new string
                }

                lua_pushinteger(L, n); // number of substitutions
                return 2;
            }
        }
    }

    // {======================================================
    // STRING FORMAT
    // =======================================================

    private const int HEX_FLOAT_PRECISION = (DBL_MANT_DIG - 1 + 3) / 4;
    private const int DBL_MAX_10_EXP = 308;

    /// <summary>
    /// Maximum size for items formatted with '%f'. This size is produced
    /// by format('%.99f', -maxfloat), and is equal to 99 + 3 ('-', '.',
    /// and '\0') + number of decimal digits to represent maxfloat (which
    /// is maximum exponent + 1). (99+3+1, adding some extra, 110)
    /// </summary>
    private const int MAX_ITEMF = 110 + DBL_MAX_10_EXP;

    /// <summary>
    /// All formats except '%f' do not need that large limit.  The other
    /// float formats use exponents, so that they fit in the 99 limit for
    /// significant digits; 's' for large strings and 'q' add items directly
    /// to the buffer; all integer formats also fit in the 99 limit.  The
    /// worst case are floats: they may need 99 significant digits, plus
    /// '0x', '-', '.', 'e+XXXX', and '\0'. Adding some extra, 120.
    /// </summary>
    private const int MAX_ITEM = 120;

    /// <summary>
    /// valid flags for a, A, e, E, f, F, g, and G conversions
    /// </summary>
    private static ReadOnlySpan<byte> L_FMTFLAGSF => "-+#0 "u8;

    /// <summary>
    /// valid flags for o, x, and X conversions
    /// </summary>
    private static ReadOnlySpan<byte> L_FMTFLAGSX => "-#0"u8;

    /// <summary>
    /// valid flags for d and i conversions
    /// </summary>
    private static ReadOnlySpan<byte> L_FMTFLAGSI => "-+0 "u8;

    /// <summary>
    /// valid flags for u conversions
    /// </summary>
    private static ReadOnlySpan<byte> L_FMTFLAGSU => "-0"u8;

    /// <summary>
    /// valid flags for c, p, and s conversions
    /// </summary>
    private static ReadOnlySpan<byte> L_FMTFLAGSC => "-"u8;

    /// <summary>
    /// Maximum size of each format specification (such as "%-099.99d"):
    /// Initial '%', flags (up to 5), width (2), period, precision (2),
    /// length modifier (8), conversion specifier, and final '\0', plus some
    /// extra.
    /// </summary>
    private const int MAX_FORMAT = 32;

    private static void addquoted(luaL_Buffer* b, ReadOnlySpan<byte> s)
    {
        Span<byte> buff = stackalloc byte[10];
        
        luaL_addchar(b, '"');
        while (!s.IsEmpty)
        {
            if (s[0] == '"' || s[0] == '\\' || s[0] == '\n')
            {
                luaL_addchar(b, '\\');
                luaL_addchar(b, s[0]);
            }
            else if (char.IsControl((char)s[0]))
            {
                luaL_addchar(b, '\\');
                buff.Clear();

                if (s.Length == 1 || !char.IsAsciiDigit((char)s[1]))
                {
                    int size = FormatInt(
                        s[0],
                        new FormatFlags(),
                        buff,
                        10);
                    luaL_addstring(b, buff[..size]);
                }
                else
                {
                    int size = FormatInt(
                        s[0],
                        new FormatFlags
                        {
                            LeftPadZero = true,
                            Width = 3,
                        },
                        buff,
                        10);
                    luaL_addstring(b, buff[..size]);
                }
            }
            else
            {
                luaL_addchar(b, s[0]);
            }

            s = s[1..];
        }

        luaL_addchar(b, '"');
    }

    /// <summary>
    /// Serialize a floating-point number in such a way that it can be
    /// scanned back by Lua. Use hexadecimal format for "common" numbers
    /// (to preserve precision); inf, -inf, and NaN are handled separately.
    /// (NaN cannot be expressed as a numeral, so we write '(0/0)' for it.)
    /// </summary>
    private static int quotefloat(lua_State* L, Span<byte> output, double n)
    {
        ReadOnlySpan<byte> s; // for the fixed representations
        if (double.IsPositiveInfinity(n))
        {
            s = "1e9999"u8;
        }
        else if (double.IsNegativeInfinity(n))
        {
            s = "-1e9999"u8;
        }
        else if (double.IsNaN(n))
        {
            s = "(0/0)"u8;
        }
        else
        {
            // format number as hexadecimal
            return FormatFloat(n, new FormatFlags(), output, FloatFormatType.Hexadecimal, false);
        }

        // for the fixed representations
        s.CopyTo(output);
        return s.Length;
    }

    private static void addliteral(lua_State* L, luaL_Buffer* b, int arg)
    {
        switch (lua_type(L, arg))
        {
            case LUA_TSTRING:
                {
                    byte* s = lua_tolstring(L, arg, out int len);
                    addquoted(b, new Span<byte>(s, len));
                    break;
                }

            case LUA_TNUMBER:
                {
                    byte* buff = luaL_prepbuffsize(b, MAX_ITEM);
                    int nb;
                    if (!lua_isinteger(L, arg)) // float?
                    {
                        nb = quotefloat(L, new Span<byte>(buff, MAX_ITEM), lua_tonumber(L, arg));
                    }
                    else
                    {
                        // integers
                        long n = lua_tointeger(L, arg);
                        if (n == long.MinValue)
                        {
                            // corner case?
                            // use hex
                            nb = FormatInt(
                                n,
                                new FormatFlags
                                {
                                    AlternateForm = true,
                                },
                                new Span<byte>(buff, MAX_ITEM),
                                16,
                                true);
                        }
                        else
                        {
                            nb = FormatInt(n, new FormatFlags(), new Span<byte>(buff, MAX_ITEM), 10);
                        }
                    }

                    luaL_addsize(b, (uint)nb);
                    break;
                }

            case LUA_TNIL:
            case LUA_TBOOLEAN:
                luaL_tolstring(L, arg, out _);
                luaL_addvalue(b);
                break;
            
            default:
                luaL_argerror(L, arg, "value has no literal form");
                break;
        }
    }

    private static ReadOnlySpan<byte> get2digits(ReadOnlySpan<byte> s)
    {
        if (char.IsAsciiDigit((char)s[0]))
        {
            s = s[1..];
            if (char.IsAsciiDigit((char)s[0]))
            {
                s = s[1..]; // (2 digits at most)
            }
        }

        return s;
    }

    private static ReadOnlySpan<byte> get2digits(ReadOnlySpan<byte> s, out ReadOnlySpan<byte> digits)
    {
        ReadOnlySpan<byte> ss = s;
        digits = default;
        if (char.IsAsciiDigit((char)s[0]))
        {
            s = s[1..];
            if (char.IsAsciiDigit((char)s[0]))
            {
                s = s[1..]; // (2 digits at most)
                digits = ss[..2];
            }
            else
            {
                digits = ss[..1];
            }
        }

        return s;
    }

    /// <summary>
    /// Check whether a conversion specification is valid. When called,
    /// first character in 'form' must be '%' and last character must
    /// be a valid conversion specifier. 'flags' are the accepted flags;
    /// 'precision' signals whether to accept a precision.
    /// </summary>
    private static void checkformat(lua_State* L, ReadOnlySpan<byte> form, ReadOnlySpan<byte> flags, bool precision)
    {
        ReadOnlySpan<byte> spec = form[1..]; // skip '%'
        spec = spec[strspn(spec, flags)..]; // skip flags
        if (spec[0] != '0')
        {
            // a width cannot start with '0'
            spec = get2digits(spec); // skip width
            if (spec[0] == '.' && precision)
            {
                spec = spec[1..];
                spec = get2digits(spec); // skip precision
            }
        }

        if (!char.IsAsciiLetter((char)spec[0])) // did not go to the end?
        {
            luaL_error(L, "invalid conversion specification: '%s'", Encoding.UTF8.GetString(form));
        }
    }

    /// <summary>
    /// Get a conversion specification and copy it to 'form'.
    /// Return the address of its last character.
    /// </summary>
    private static ReadOnlySpan<byte> getformat(lua_State* L, ReadOnlySpan<byte> strfrmt, Span<byte> form)
    {
        form.Clear();

        // spans flags, width, and precision ('0' is included as a flag)
        int len = strspn(strfrmt, "-+#0 123456789."u8);
        len++; // adds following character (should be the specifier)
        // still needs space for '%', '\0', plus a length modifier
        if (len >= MAX_FORMAT - 10)
        {
            luaL_error(L, "invalid format (too long)");
        }

        form[0] = (byte)'%';
        strfrmt[..len].CopyTo(form.Slice(1, len));
        form[len + 1] = 0;
        return strfrmt[len..];
    }

    private static int str_format(lua_State* L)
    {
        Span<byte> form = stackalloc byte[MAX_FORMAT]; // to store the format ('%...')

        int top = lua_gettop(L);
        int arg = 1;
        ReadOnlySpan<byte> strfrmt = luaL_checkstring(L, arg);
        luaL_Buffer b;
        luaL_buffinit(L, &b);
        while (!strfrmt.IsEmpty)
        {
            if (strfrmt[0] != L_ESC)
            {
                luaL_addchar(&b, strfrmt[0]);
                strfrmt = strfrmt[1..];
            }
            else if (strfrmt[1] == L_ESC)
            {
                luaL_addchar(&b, strfrmt[1]); // %%
                strfrmt = strfrmt[2..];
            }
            else
            {
                strfrmt = strfrmt[1..];
                // format item
                byte* buff = luaL_prepbuffsize(&b, MAX_ITEMF); // to put result
                int nb = 0; // number of bytes in result
                if (++arg > top)
                {
                    return luaL_argerror(L, arg, "no value");
                }

                strfrmt = getformat(L, strfrmt, form);
                FormatFlags format = ParseFormatFlags(L, form);
                switch (format.Type)
                {
                    case (byte)'c':
                        {
                            int c = (int)luaL_checkinteger(L, arg);
                            checkformat(L, form, L_FMTFLAGSC, false);

                            Span<byte> span = new(buff, MAX_ITEMF);
                            nb = 1;
                            if (format.Width.HasValue)
                            {
                                int width = format.Width.Value;
                                nb = width;
                                span[..width].Fill((byte)' ');
                                if (format.LeftJustified)
                                {
                                    span[0] = (byte)c;
                                }
                                else
                                {
                                    span[width - 1] = (byte)c;
                                }
                            }
                            else
                            {
                                span[0] = (byte)c;
                            }

                            break;
                        }

                    case (byte)'d':
                    case (byte)'i':
                        checkformat(L, form, L_FMTFLAGSI, true);
                        nb = FormatInt(luaL_checkinteger(L, arg), format, new Span<byte>(buff, MAX_ITEMF), 10);
                        break;

                    case (byte)'u':
                        checkformat(L, form, L_FMTFLAGSU, true);
                        nb = FormatInt(luaL_checkinteger(L, arg), format, new Span<byte>(buff, MAX_ITEMF), 10, true);
                        break;

                    case (byte)'o':
                        checkformat(L, form, L_FMTFLAGSX, true);
                        nb = FormatInt(luaL_checkinteger(L, arg), format, new Span<byte>(buff, MAX_ITEMF), 8, true);
                        break;

                    case (byte)'x':
                        checkformat(L, form, L_FMTFLAGSX, true);
                        nb = FormatInt(luaL_checkinteger(L, arg), format, new Span<byte>(buff, MAX_ITEMF), 16, true);
                        break;

                    case (byte)'X':
                        checkformat(L, form, L_FMTFLAGSX, true);
                        nb = FormatInt(
                            luaL_checkinteger(L, arg),
                            format,
                            new Span<byte>(buff, MAX_ITEMF),
                            16,
                            true,
                            true);
                        break;

                    case (byte)'a':
                    case (byte)'A':
                        checkformat(L, form, L_FMTFLAGSF, true);
                        nb = FormatFloat(
                            luaL_checknumber(L, arg),
                            format,
                            new Span<byte>(buff, MAX_ITEMF),
                            FloatFormatType.Hexadecimal,
                            format.Type == 'A');
                        break;

                    case (byte)'f':
                        checkformat(L, form, L_FMTFLAGSF, true);
                        nb = FormatFloat(
                            luaL_checknumber(L, arg),
                            format,
                            new Span<byte>(buff, MAX_ITEMF),
                            FloatFormatType.Standard,
                            false);
                        break;

                    case (byte)'e':
                    case (byte)'E':
                        checkformat(L, form, L_FMTFLAGSF, true);
                        nb = FormatFloat(
                            luaL_checknumber(L, arg),
                            format,
                            new Span<byte>(buff, MAX_ITEMF),
                            FloatFormatType.Scientific,
                            format.Type == 'E');
                        break;

                    case (byte)'g':
                    case (byte)'G':
                        checkformat(L, form, L_FMTFLAGSF, true);
                        nb = FormatFloat(
                            luaL_checknumber(L, arg),
                            format,
                            new Span<byte>(buff, MAX_ITEMF),
                            FloatFormatType.Shortest,
                            format.Type == 'G');
                        break;

                    case (byte)'p':
                        {
                            checkformat(L, form, L_FMTFLAGSC, false);
                            void* p = lua_topointer(L, arg);
                            if (p == null)
                            {
                                // avoid calling 'printf' with argument null
                                nb = FormatString(
                                    "(null)"u8,
                                    format,
                                    new Span<byte>(buff, MAX_ITEMF)); // format it as a string
                            }
                            else
                            {
                                nb = FormatInt(
                                    (long)p,
                                    format with
                                    {
                                        Precision = sizeof(nint) * 2,
                                        AlternateForm = true,
                                    },
                                    new Span<byte>(buff, MAX_ITEMF),
                                    16,
                                    true);
                            }

                            break;
                        }

                    case (byte)'q':
                        if (form[2] != '\0') // modifiers?
                        {
                            return luaL_error(L, "specifier '%%q' cannot have modifiers");
                        }

                        addliteral(L, &b, arg);
                        break;

                    case (byte)'s':
                        {
                            byte* s = luaL_tolstring(L, arg, out int l);
                            if (form[2] == '\0') // no modifiers?
                            {
                                luaL_addvalue(&b); // keep entire string
                            }
                            else
                            {
                                luaL_argcheck(L, l == strlen(s), arg, "string contains zeros");
                                checkformat(L, form, L_FMTFLAGSC, true);
                                if (format.Precision == null && l >= 100)
                                {
                                    // no precision and string is too long to be formatted
                                    luaL_addvalue(&b); // keep entire string
                                }
                                else
                                {
                                    // format the string into 'buff'
                                    nb = FormatString(
                                        new ReadOnlySpan<byte>(s, l),
                                        format,
                                        new Span<byte>(buff, MAX_ITEMF));
                                    lua_pop(L, 1); // remove result from 'luaL_tolstring'
                                }
                            }

                            break;
                        }

                    default:
                        // also treat cases 'pnLlh'
                        return luaL_error(L, "invalid conversion '%s' to 'format'", Encoding.UTF8.GetString(form));
                }

                Debug.Assert((uint)nb < MAX_ITEMF);
                luaL_addsize(&b, (uint)nb);
            }
        }

        luaL_pushresult(&b);
        return 1;
    }

    private static int FormatInt(
        long value,
        FormatFlags format,
        Span<byte> output,
        uint @base,
        bool isUnsigned = false,
        bool isUpperCase = false)
    {
        bool isZero = value == 0;

        if (format.LeftJustified || format.Precision.HasValue)
        {
            format.LeftPadZero = false;
        }

        Span<byte> buffer = stackalloc byte[100];
        int i = 0;

        // Extract the negative sign if needed. If unsigned (or long.MinValue), handle the first digit manually.
        bool isNegative = false;
        if (value < 0)
        {
            isNegative = !isUnsigned;
            if (isUnsigned || value == long.MinValue)
            {
                int c = (int)(unchecked((ulong)value) % @base);
                value = (long)(unchecked((ulong)value) / @base);
                AddDigit(buffer, c, ref i);
            }
            else
            {
                value = -value;
            }
        }

        // Output the remaining digits
        if (isZero)
        {
            if (format.Precision != 0)
            {
                AddDigit(buffer, 0, ref i);
            }
        }
        else
        {
            while (value > 0)
            {
                int c = (int)(value % @base);
                value /= @base;
                AddDigit(buffer, c, ref i);
            }
        }

        if (format.Precision.HasValue)
        {
            int precision = format.Precision.Value;
            while (i < precision)
            {
                buffer[^++i] = (byte)'0';
            }
        }

        // Output the sign (or preceeding character)
        byte sign = 0;
        if (isNegative)
        {
            sign = (byte)'-';
        }
        else if (format.ForcePreceedPlus)
        {
            sign = (byte)'+';
        }
        else if (format.ForcePreceedSpace)
        {
            sign = (byte)' ';
        }

        if (format is { Width: not null, LeftPadZero: true })
        {
            int width = format.Width.Value;
            if (sign != 0)
            {
                width -= 1;
            }

            if (format.AlternateForm && @base == 8 && !isZero)
            {
                width -= 1;
            }

            if (format.AlternateForm && @base is 2 or 16 && !isZero)
            {
                width -= 2;
            }

            while (i < width)
            {
                buffer[^++i] = (byte)'0';
            }
        }

        // Output the prefix
        if (format.AlternateForm && @base != 10 && !isZero)
        {
            switch (@base)
            {
                case 2:
                    buffer[^++i] = (byte)(isUpperCase ? 'B' : 'b');
                    break;
                
                case 16:
                    buffer[^++i] = (byte)(isUpperCase ? 'X' : 'x');
                    break;
            }

            buffer[^++i] = (byte)'0';
        }

        if (sign != 0)
        {
            buffer[^++i] = sign;
        }
        
        return FinishFormatFloat(buffer, i, output, format);

        void AddDigit(Span<byte> buffer, int c, ref int i)
        {
            if (c >= 10)
            {
                if (c >= 36)
                {
                    throw new InvalidOperationException();
                }

                c -= 10;
                buffer[^++i] = (byte)((isUpperCase ? 'A' : 'a') + c);
            }
            else
            {
                buffer[^++i] = (byte)('0' + c);
            }
        }
    }

    private enum FloatFormatType
    {
        Standard,
        Shortest,
        Scientific,
        Hexadecimal,
    }

    private static int FormatFloat(
        double value,
        in FormatFlags format,
        Span<byte> output,
        FloatFormatType type,
        bool isUppercase)
    {
        if (!double.IsFinite(value))
        {
            if (double.IsPositiveInfinity(value))
            {
                return FormatString(isUppercase ? "INF"u8 : "inf"u8, format, output);
            }

            if (double.IsNegativeInfinity(value))
            {
                return FormatString(isUppercase ? "-INF"u8 : "-inf"u8, format, output);
            }

            return FormatString(isUppercase ? "NAN"u8 : "nan"u8, format, output);
        }
        
        return type switch
        {
            FloatFormatType.Standard => FormatStandardFloat(value, format, output, false),
            FloatFormatType.Shortest => FormatShortestFloat(value, format, output, isUppercase),
            FloatFormatType.Scientific => FormatScientificFloat(value, format, output, isUppercase),
            FloatFormatType.Hexadecimal => FormatHexadecimalFloat(value, format, output, isUppercase),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private static int FormatStandardFloat(double value, in FormatFlags format, Span<byte> output, bool trimTrailingZero)
    {
        int precision = format.Precision ?? 6;

        bool isNegative = BitConverter.DoubleToInt64Bits(value) < 0;
        DecomposeAbsDouble(value, out BigInteger numerator, out BigInteger denominator);

        BigInteger scale = Pow10(precision);
        BigInteger rounded = RoundQuotientNearestEven(numerator * scale, denominator);
        BigInteger integer = BigInteger.DivRem(rounded, scale, out BigInteger fraction);

        Span<byte> buffer = stackalloc byte[512];
        int i = 0;

        if (precision > 0)
        {
            int startI = i;
            AppendNumber(buffer, ref i, fraction, precision, trimTrailingZero);
            if (!trimTrailingZero || i != startI)
            {
                buffer[^++i] = (byte)'.';
            }
        }
        else if (format.AlternateForm)
        {
            buffer[^++i] = (byte)'.';
        }

        AppendNumber(buffer, ref i, integer);

        // Output the sign (or preceeding character)
        byte sign = 0;
        if (isNegative)
        {
            sign = (byte)'-';
        }
        else if (format.ForcePreceedPlus)
        {
            sign = (byte)'+';
        }
        else if (format.ForcePreceedSpace)
        {
            sign = (byte)' ';
        }

        if (format is { Width: not null, LeftPadZero: true })
        {
            int width = format.Width.Value;
            if (sign != 0)
            {
                width -= 1;
            }

            while (i < width)
            {
                buffer[^++i] = (byte)'0';
            }
        }

        if (sign != 0)
        {
            buffer[^++i] = sign;
        }
        
        return FinishFormatFloat(buffer, i, output, format);
    }

    private static int FormatShortestFloat(double value, in FormatFlags format, Span<byte> output, bool isUppercase)
    {
        int precision = format.Precision ?? 6;
        
        int p = precision == 0 ? 1 : precision;
        int exponent = value == 0.0
            ? 0
            : GetDecimalExponent(value);

        bool useExponential = exponent < -4 || exponent >= p;
        return useExponential
            ? FormatScientificFloat(
                value,
                format with
                {
                    Precision = p - 1,
                },
                output,
                isUppercase)
            : FormatStandardFloat(
                value,
                format with
                {
                    Precision = p - (exponent + 1),
                },
                output,
                !format.AlternateForm);
    }

    private static int FormatScientificFloat(double value, in FormatFlags format, Span<byte> output, bool isUppercase)
    {
        int precision = format.Precision ?? 6;
        
        bool isNegative = BitConverter.DoubleToInt64Bits(value) < 0;
        GetScientificDigits(value, precision, out BigInteger significand, out int exponent);

        BigInteger scale = Pow10(precision);
        BigInteger integer = BigInteger.DivRem(significand, scale, out BigInteger fraction);

        Span<byte> buffer = stackalloc byte[200];
        int i = 0;
        
        AppendNumber(buffer, ref i, BigInteger.Abs(exponent));
        if (Math.Abs(exponent) < 10)
        {
            buffer[^++i] = (byte)'0';
        }
        buffer[^++i] = exponent < 0 ? (byte)'-' : (byte)'+';
        buffer[^++i] = isUppercase ? (byte)'E' : (byte)'e';

        if (precision > 0)
        {
            AppendNumber(buffer, ref i, fraction, precision);
            buffer[^++i] = (byte)'.';
        }
        else if (format.AlternateForm)
        {
            buffer[^++i] = (byte)'.';
        }

        AppendNumber(buffer, ref i, integer);

        // Output the sign (or preceeding character)
        byte sign = 0;
        if (isNegative)
        {
            sign = (byte)'-';
        }
        else if (format.ForcePreceedPlus)
        {
            sign = (byte)'+';
        }
        else if (format.ForcePreceedSpace)
        {
            sign = (byte)' ';
        }

        if (format is { Width: not null, LeftPadZero: true })
        {
            int width = format.Width.Value;
            if (sign != 0)
            {
                width -= 1;
            }

            while (i < width)
            {
                buffer[^++i] = (byte)'0';
            }
        }

        if (sign != 0)
        {
            buffer[^++i] = sign;
        }

        return FinishFormatFloat(buffer, i, output, format);
    }

    private static int FormatHexadecimalFloat(double value, in FormatFlags format, Span<byte> output, bool isUppercase)
    {
        int precision = format.Precision ?? HEX_FLOAT_PRECISION;

        bool isNegative = BitConverter.DoubleToInt64Bits(value) < 0;
        
        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value) & 0x7FFF_FFFF_FFFF_FFFFUL;
        int rawExponent = (int)((bits >> 52) & 0x7FFUL);
        ulong fractionBits = bits & 0x000F_FFFF_FFFF_FFFFUL;
        
        BigInteger significand;
        int exponent;

        if (rawExponent == 0)
        {
            exponent = fractionBits == 0 ? 0 : -1022;
            significand = fractionBits;
        }
        else
        {
            exponent = rawExponent - 1023;
            significand = (BigInteger.One << 52) | fractionBits;
        }
        
        BigInteger scaled;
        if (precision < HEX_FLOAT_PRECISION)
        {
            BigInteger divisor = BigInteger.One << (4 * (HEX_FLOAT_PRECISION - precision));
            scaled = RoundQuotientNearestEven(significand, divisor);
        }
        else
        {
            scaled = significand << (4 * (precision - HEX_FLOAT_PRECISION));
        }
        
        BigInteger integer = precision == 0 ? scaled : scaled >> (4 * precision);
        BigInteger fraction = precision == 0
            ? BigInteger.Zero
            : scaled & ((BigInteger.One << (4 * precision)) - 1);
        int fractionDigits = precision;
        
        if (!format.Precision.HasValue)
        {
            while (fractionDigits > 0 && (fraction & 0xF) == 0)
            {
                fraction >>= 4;
                fractionDigits--;
            }
        }

        Span<byte> buffer = stackalloc byte[200];
        int i = 0;
        
        AppendNumber(buffer, ref i, BigInteger.Abs(exponent));
        buffer[^++i] = exponent < 0 ? (byte)'-' : (byte)'+';
        buffer[^++i] = isUppercase ? (byte)'P' : (byte)'p';

        if (fractionDigits > 0)
        {
            AppendHexNumber(buffer, ref i, fraction, isUppercase, fractionDigits);
            buffer[^++i] = (byte)'.';
        }
        else if (format.AlternateForm)
        {
            buffer[^++i] = (byte)'.';
        }
        
        AppendHexNumber(buffer, ref i, integer, isUppercase);

        // Output the sign (or preceeding character)
        byte sign = 0;
        if (isNegative)
        {
            sign = (byte)'-';
        }
        else if (format.ForcePreceedPlus)
        {
            sign = (byte)'+';
        }
        else if (format.ForcePreceedSpace)
        {
            sign = (byte)' ';
        }

        if (format is { Width: not null, LeftPadZero: true })
        {
            int width = format.Width.Value - 2;
            if (sign != 0)
            {
                width -= 1;
            }

            while (i < width)
            {
                buffer[^++i] = (byte)'0';
            }
        }

        buffer[^++i] = isUppercase ? (byte)'X' : (byte)'x';
        buffer[^++i] = (byte)'0';

        if (sign != 0)
        {
            buffer[^++i] = sign;
        }

        return FinishFormatFloat(buffer, i, output, format);
    }

    private static int FinishFormatFloat(Span<byte> buffer, int i, Span<byte> output, in FormatFlags format)
    {
        if (format is { Width: not null, LeftJustified: false, LeftPadZero: false })
        {
            while (i < format.Width)
            {
                buffer[^++i] = (byte)' ';
            }
        }
        
        Span<byte> usedSpan = buffer[^i..];
        usedSpan.CopyTo(output);
        int usedLength = usedSpan.Length;

        if (format is { Width: not null, LeftJustified: true })
        {
            int toCopy = format.Width.Value - usedLength;
            if (toCopy > 0)
            {
                for (int j = 0; j < toCopy; j++)
                {
                    output[usedLength + j] = (byte)' ';
                }

                usedLength += toCopy;
            }
        }

        return usedLength;
    }

    private static void DecomposeAbsDouble(double value, out BigInteger numerator, out BigInteger denominator)
    {
        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value) & 0x7FFF_FFFF_FFFF_FFFFUL;
        int rawExponent = (int)((bits >> 52) & 0x7FFUL);
        ulong fraction = bits & 0x000F_FFFF_FFFF_FFFFUL;

        if (rawExponent == 0)
        {
            numerator = fraction;
            denominator = BigInteger.One << 1074;
            return;
        }

        numerator = (1UL << 52) | fraction;
        int binaryExponent = rawExponent - 1075;
        if (binaryExponent >= 0)
        {
            numerator <<= binaryExponent;
            denominator = BigInteger.One;
        }
        else
        {
            denominator = BigInteger.One << -binaryExponent;
        }
    }

    private static BigInteger Pow10(int exponent)
    {
        return BigInteger.Pow(10, exponent);
    }

    private static BigInteger RoundQuotientNearestEven(BigInteger numerator, BigInteger denominator)
    {
        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        int cmp = (remainder * 2).CompareTo(denominator);
        if (cmp > 0 || (cmp == 0 && !quotient.IsEven))
        {
            quotient++;
        }

        return quotient;
    }

    private static int GetDecimalExponent(double value)
    {
        DecomposeAbsDouble(value, out BigInteger numerator, out BigInteger denominator);
        if (numerator.IsZero)
        {
            return 0;
        }

        int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        while (CompareToPowerOf10(numerator, denominator, exponent + 1) >= 0)
        {
            exponent++;
        }

        while (CompareToPowerOf10(numerator, denominator, exponent) < 0)
        {
            exponent--;
        }

        return exponent;
    }

    private static int CompareToPowerOf10(BigInteger numerator, BigInteger denominator, int exponent)
    {
        return exponent >= 0
            ? numerator.CompareTo(denominator * Pow10(exponent))
            : (numerator * Pow10(-exponent)).CompareTo(denominator);
    }

    private static void GetScientificDigits(double value, int precision, out BigInteger significand, out int exponent)
    {
        DecomposeAbsDouble(value, out BigInteger numerator, out BigInteger denominator);
        if (numerator.IsZero)
        {
            significand = BigInteger.Zero;
            exponent = 0;
            return;
        }

        exponent = GetDecimalExponent(value);
        int totalDigits = precision + 1;
        int scalePower = totalDigits - 1 - exponent;
        significand = scalePower >= 0
            ? RoundQuotientNearestEven(numerator * Pow10(scalePower), denominator)
            : RoundQuotientNearestEven(numerator, denominator * Pow10(-scalePower));

        BigInteger limit = Pow10(totalDigits);
        if (significand >= limit)
        {
            significand /= 10;
            exponent++;
        }
    }

    private static void AppendNumber(
        Span<byte> buffer,
        ref int i,
        BigInteger integer,
        int? precision = null,
        bool trimTrailingZero = false)
    {
        if (precision.HasValue)
        {
            int digitsToWrite = precision.Value;
            if (trimTrailingZero)
            {
                while (digitsToWrite > 0 && integer > 0 && integer % 10 == 0)
                {
                    integer /= 10;
                    digitsToWrite--;
                }

                if (integer.IsZero)
                {
                    return;
                }
            }

            for (int j = 0; j < digitsToWrite; j++)
            {
                int c = (int)(integer % 10);
                integer /= 10;
                buffer[^++i] = (byte)('0' + c);
            }

            return;
        }

        if (integer.IsZero)
        {
            buffer[^++i] = (byte)'0';
            return;
        }

        while (integer > 0)
        {
            int c = (int)(integer % 10);
            integer /= 10;
            buffer[^++i] = (byte)('0' + c);
        }
    }

    private static void AppendHexNumber(
        Span<byte> buffer,
        ref int i,
        BigInteger integer,
        bool isUppercase,
        int? precision = null)
    {
        if (precision.HasValue)
        {
            int digitsToWrite = precision.Value;

            for (int j = 0; j < digitsToWrite; j++)
            {
                int c = (int)(integer & 0x0F);
                integer >>= 4;
                buffer[^++i] = c >= 10
                    ? isUppercase ? (byte)('A' + c - 10) : (byte)('a' + c - 10)
                    : (byte)('0' + c);
            }

            return;
        }

        if (integer.IsZero)
        {
            buffer[^++i] = (byte)'0';
            return;
        }

        while (integer > 0)
        {
            int c = (int)(integer & 0x0F);
            integer >>= 4;
            buffer[^++i] = c >= 10
                ? isUppercase ? (byte)('A' + c - 10) : (byte)('a' + c - 10)
                : (byte)('0' + c);
        }
    }

    private static int FormatString(ReadOnlySpan<byte> input, in FormatFlags format, Span<byte> output)
    {
        if (format.Precision.HasValue && format.Precision < input.Length)
        {
            input = input[..format.Precision.Value];
        }

        int toCopy = Math.Max((format.Width ?? 0) - input.Length, 0);
        if (toCopy > 0)
        {
            if (format.LeftJustified)
            {
                input.CopyTo(output);
                output.Slice(input.Length, toCopy).Fill((byte)' ');
            }
            else
            {
                output[..toCopy].Fill((byte)' ');
                input.CopyTo(output[toCopy..]);
            }
        }
        else
        {
            input.CopyTo(output);
        }

        return input.Length + toCopy;
    }

    private static FormatFlags ParseFormatFlags(lua_State* L, ReadOnlySpan<byte> form)
    {
        form = form.TrimEnd((byte)0);

        ReadOnlySpan<byte> originalForm = form;
        Debug.Assert(form.Length >= 2);
        Debug.Assert(form[0] == '%');

        form = form[1..];

        FormatFlags format = new();

        while (true)
        {
            if (form[0] == '-')
            {
                format.LeftJustified = true;
            }
            else if (form[0] == '+')
            {
                format.ForcePreceedPlus = true;
            }
            else if (form[0] == ' ')
            {
                format.ForcePreceedSpace = true;
            }
            else if (form[0] == '#')
            {
                format.AlternateForm = true;
            }
            else if (form[0] == '0')
            {
                format.LeftPadZero = true;
            }
            else
            {
                break;
            }

            form = form[1..];
            if (form.IsEmpty)
            {
                luaL_error(L, "invalid conversion specification: '%s'", Encoding.UTF8.GetString(originalForm));
            }
        }

        form = get2digits(form, out ReadOnlySpan<byte> width);
        if (!width.IsEmpty)
        {
            format.Width = int.Parse(width, CultureInfo.InvariantCulture);
        }

        if (form.IsEmpty)
        {
            luaL_error(L, "invalid conversion specification: '%s'", Encoding.UTF8.GetString(originalForm));
        }

        if (form[0] == '.')
        {
            form = get2digits(form[1..], out ReadOnlySpan<byte> precision);
            if (precision.IsEmpty)
            {
                format.Precision = 0;
            }
            else
            {
                format.Precision = int.Parse(precision, CultureInfo.InvariantCulture);
            }
        }

        if (form.Length != 1)
        {
            luaL_error(L, "invalid conversion specification: '%s'", Encoding.UTF8.GetString(originalForm));
        }

        format.Type = form[0];
        return format;
    }

    private struct FormatFlags
    {
        public bool LeftJustified;
        public bool ForcePreceedPlus;
        public bool ForcePreceedSpace;
        public bool AlternateForm;
        public bool LeftPadZero;
        public int? Width;
        public int? Precision;
        public byte Type;
    }

    // {======================================================
    // PACK/UNPACK
    // =======================================================

    /// <summary>
    /// value used for padding
    /// </summary>
    private const byte LUAL_PACKPADBYTE = 0x00;

    /// <summary>
    /// maximum size for the binary representation of an integer
    /// </summary>
    private const int MAXINTSIZE = 16;

    /// <summary>
    /// number of bits in a character
    /// </summary>
    private const int NB = 8;

    /// <summary>
    /// mask for one character (NB 1's)
    /// </summary>
    private const int MC = 0xFF;

// size of a long
// #define SZINT	((int)sizeof(long))

    /// <summary>
    /// information to pack/unpack stuff
    /// </summary>
    private struct Header
    {
        public lua_State* L;
        public bool islittle;
        public uint maxalign;
    }

    /// <summary>
    /// Options for pack/unpack.
    /// </summary>
    private enum KOption
    {
        Int, // signed integers
        UInt, // unsigned integers
        Float, // single-precision floating-point numbers
        Number, // Lua "native" floating-point numbers
        Double, // double-precision floating-point numbers
        Char, // fixed-length strings
        String, // strings with prefixed length
        ZeroString, // zero-terminated strings
        Padding, // padding
        PaddAlign, // padding for alignment
        Nop, // no-op (configuration or spaces)
    }

    /// <summary>
    /// Read an integer numeral from string 'fmt' or return 'df' if
    /// there is no numeral
    /// </summary>
    private static long getnum(ref ReadOnlySpan<char> fmt, long df)
    {
        if (fmt.IsEmpty || !char.IsAsciiDigit(fmt[0])) // no number?
        {
            return df; // return default value
        }

        long a = 0;
        do
        {
            a = a * 10 + (uint)(fmt[0] - '0');
            fmt = fmt[1..];
        } while (!fmt.IsEmpty && char.IsAsciiDigit(fmt[0]) && a <= (long.MaxValue - 9) / 10);

        return a;
    }

    /// <summary>
    /// Read an integer numeral and raises an error if it is larger
    /// than the maximum size of integers.
    /// </summary>
    private static uint getnumlimit(Header* h, ref ReadOnlySpan<char> fmt, int df)
    {
        long sz = getnum(ref fmt, df);
        if ((uint)sz - 1u >= MAXINTSIZE)
        {
            return (uint)luaL_error(h->L, "integral size (%d) out of limits [1,%d]", sz, MAXINTSIZE);
        }

        return (uint)sz;
    }

    /// <summary>
    /// Initialise Header.
    /// </summary>
    private static void initheader(lua_State* L, Header* h)
    {
        h->L = L;
        h->islittle = BitConverter.IsLittleEndian;
        h->maxalign = 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AlignProbe<T>
        where T : unmanaged
    {
        public byte Padding;
        public T Value;
    }

    private static readonly int nativeAlignment =
        Math.Max(
            Marshal.OffsetOf<AlignProbe<double>>(nameof(AlignProbe<>.Value)).ToInt32(),
            Marshal.OffsetOf<AlignProbe<nint>>(nameof(AlignProbe<>.Value)).ToInt32());

    /// <summary>
    /// Read and classify next option. 'size' is filled with option's size.
    /// </summary>
    private static KOption getoption(Header* h, ref ReadOnlySpan<char> fmt, out long size)
    {
        char opt = fmt[0];
        fmt = fmt[1..];
        size = 0; // default
        switch (opt)
        {
            case 'b':
                size = sizeof(byte);
                return KOption.Int;
            
            case 'B':
                size = sizeof(byte);
                return KOption.UInt;
            
            case 'h':
                size = sizeof(short);
                return KOption.Int;
            
            case 'H':
                size = sizeof(short);
                return KOption.UInt;
            
            case 'l':
                size = sizeof(long);
                return KOption.Int;
            
            case 'L':
                size = sizeof(long);
                return KOption.UInt;
            
            case 'j':
                size = sizeof(long);
                return KOption.Int;
            
            case 'J':
                size = sizeof(long);
                return KOption.UInt;
            
            case 'T':
                size = sizeof(long);
                return KOption.UInt;
            
            case 'f':
                size = sizeof(float);
                return KOption.Float;
            
            case 'n':
                size = sizeof(double);
                return KOption.Number;
            
            case 'd':
                size = sizeof(double);
                return KOption.Double;
            
            case 'i':
                size = getnumlimit(h, ref fmt, sizeof(int));
                return KOption.Int;
            
            case 'I':
                size = getnumlimit(h, ref fmt, sizeof(int));
                return KOption.UInt;
            
            case 's':
                size = getnumlimit(h, ref fmt, sizeof(long));
                return KOption.String;
            
            case 'c':
                size = getnum(ref fmt, -1);
                if (size == -1)
                {
                    luaL_error(h->L, "missing size for format option 'c'");
                }

                return KOption.Char;

            case 'z':
                return KOption.ZeroString;
            
            case 'x':
                size = 1;
                return KOption.Padding;
            
            case 'X':
                return KOption.PaddAlign;
            
            case ' ':
                break;
            
            case '<':
                h->islittle = true;
                break;
            
            case '>':
                h->islittle = false;
                break;
            
            case '=':
                h->islittle = BitConverter.IsLittleEndian;
                break;
            
            case '!':
                h->maxalign = getnumlimit(h, ref fmt, nativeAlignment);
                break;

            default:
                luaL_error(h->L, "invalid format option '%c'", opt);
                break;
        }

        return KOption.Nop;
    }

    private static bool ispow2(long x)
    {
        return (x & x - 1) == 0;
    }

    /// <summary>
    /// Read, classify, and fill other details about the next option.
    /// 'psize' is filled with option's size, 'notoalign' with its
    /// alignment requirements.
    /// Local variable 'size' gets the size to be aligned. (Kpadal option
    /// always gets its full alignment, other options are limited by
    /// the maximum alignment ('maxalign'). Kchar option needs no alignment
    /// despite its size.
    /// </summary>
    private static KOption getdetails(
        Header* h,
        long totalsize,
        ref ReadOnlySpan<char> fmt,
        out long psize,
        out uint ntoalign)
    {
        KOption opt = getoption(h, ref fmt, out psize);
        long align = psize; // usually, alignment follows size
        if (opt == KOption.PaddAlign)
        {
            // 'X' gets alignment from following option
            if (fmt.IsEmpty || getoption(h, ref fmt, out align) == KOption.Char || align == 0)
            {
                luaL_argerror(h->L, 1, "invalid next option for option 'X'");
            }
        }

        if (align <= 1 || opt == KOption.Char) // need no alignment?
        {
            ntoalign = 0;
        }
        else
        {
            if (align > h->maxalign) // enforce maximum alignment
            {
                align = h->maxalign;
            }

            if (!ispow2(align))
            {
                // not a power of 2?
                ntoalign = 0; // to avoid warnings
                luaL_argerror(h->L, 1, "format asks for alignment not power of 2");
            }
            else
            {
                // 'szmoda' = totalsize % align
                uint szmoda = (uint)(totalsize & align - 1);
                ntoalign = (uint)(align - szmoda & align - 1);
            }
        }

        return opt;
    }

    /// <summary>
    /// Pack integer 'n' with 'size' bytes and 'islittle' endianness.
    /// The final 'if' handles the case when 'size' is larger than
    /// the size of a Lua integer, correcting the extra sign-extension
    /// bytes if necessary (by default they would be zeros).
    /// </summary>
    private static void packint(
        luaL_Buffer* b,
        ulong n,
        bool islittle,
        uint size,
        bool neg)
    {
        byte* buff = luaL_prepbuffsize(b, size);
        buff[islittle ? 0 : size - 1] = (byte)(n & MC); // first byte
        for (int i = 1; i < size; i++)
        {
            n >>= NB;
            buff[islittle ? i : size - 1 - i] = (byte)(n & MC);
        }

        if (neg && size > sizeof(long))
        {
            // negative number need sign extension?
            for (long i = sizeof(long); i < size; i++) // correct extra bytes
            {
                buff[islittle ? i : size - 1 - i] = MC;
            }
        }

        luaL_addsize(b, size); // add result to buffer
    }

    /// <summary>
    /// Copy 'size' bytes from 'src' to 'dest', correcting endianness if
    /// given 'islittle' is different from native endianness.
    /// </summary>
    [Obsolete]
    private static void copywithendian(byte* dest, byte* src, int size, bool islittle)
    {
        if (islittle == BitConverter.IsLittleEndian)
        {
            memcpy(dest, src, size);
        }
        else
        {
            dest += size - 1;
            while (size-- != 0)
            {
                *dest-- = *src++;
            }
        }
    }

    /// <summary>
    /// Copy 'size' bytes from 'src' to 'dest', correcting endianness if
    /// given 'islittle' is different from native endianness.
    /// </summary>
    private static void copywithendian(Span<byte> dest, ReadOnlySpan<byte> src, int size, bool islittle)
    {
        if (islittle == BitConverter.IsLittleEndian)
        {
            src[..size].CopyTo(dest[..size]);
        }
        else
        {
            for (int i = 0; i < size; i++)
            {
                dest[^i] = src[i];
            }
        }
    }

    private static int str_pack(lua_State* L)
    {
        luaL_Buffer b;
        ReadOnlySpan<char> fmt = luaL_checknetstring(L, 1); // format string
        int arg = 1; // current argument to pack
        long totalsize = 0; // accumulate total size of result

        Header h;
        initheader(L, &h);
        lua_pushnil(L); // mark to separate arguments from string buffer
        luaL_buffinit(L, &b);
        while (!fmt.IsEmpty)
        {
            KOption opt = getdetails(&h, totalsize, ref fmt, out long size, out uint ntoalign);
            luaL_argcheck(
                L,
                size + ntoalign <= long.MaxValue - totalsize,
                arg,
                "result too long");
            totalsize += ntoalign + size;
            while (ntoalign-- > 0)
            {
                luaL_addchar(&b, LUAL_PACKPADBYTE); // fill alignment
            }

            arg++;
            switch (opt)
            {
                case KOption.Int:
                    {
                        // signed integers
                        long n = luaL_checkinteger(L, arg);
                        if (size < sizeof(long))
                        {
                            // need overflow check?
                            long lim = (long)1 << (int)(size * 8 - 1);
                            luaL_argcheck(L, -lim <= n && n < lim, arg, "integer overflow");
                        }

                        packint(&b, (ulong)n, h.islittle, (uint)size, n < 0);
                        break;
                    }

                case KOption.UInt:
                    {
                        // unsigned integers
                        long n = luaL_checkinteger(L, arg);
                        if (size < sizeof(long)) // need overflow check?
                        {
                            luaL_argcheck(
                                L,
                                (ulong)n < (1UL << (int)(size * NB)),
                                arg,
                                "unsigned overflow");
                        }

                        packint(&b, (ulong)n, h.islittle, (uint)size, false);
                        break;
                    }

                case KOption.Float: // C float
                    {
                        float f = (float)luaL_checknumber(L, arg); // get argument
                        byte* buff = luaL_prepbuffsize(&b, sizeof(float));
                        // move 'f' to final result, correcting endianness if needed
                        copywithendian(buff, (byte*)&f, sizeof(float), h.islittle);
                        luaL_addsize(&b, size);
                        break;
                    }

                case KOption.Number: // Lua float
                case KOption.Double: // C double
                    {
                        double f = luaL_checknumber(L, arg); // get argument
                        byte* buff = luaL_prepbuffsize(&b, sizeof(double));
                        // move 'f' to final result, correcting endianness if needed
                        copywithendian(buff, (byte*)&f, sizeof(double), h.islittle);
                        luaL_addsize(&b, size);
                        break;
                    }

                case KOption.Char:
                    {
                        // fixed-size string
                        ReadOnlySpan<byte> s = luaL_checklstring(L, arg);
                        luaL_argcheck(L, s.Length <= size, arg, "string longer than given size");
                        luaL_addlstring(&b, s); // add string
                        if (s.Length < size)
                        {
                            // does it need padding?
                            long psize = size - s.Length; // pad size
                            byte* buff = luaL_prepbuffsize(&b, psize);
                            new Span<byte>(buff, checked((int)psize)).Fill(LUAL_PACKPADBYTE);
                            luaL_addsize(&b, psize);
                        }

                        break;
                    }

                case KOption.String:
                    {
                        // strings with length count
                        ReadOnlySpan<byte> s = luaL_checklstring(L, arg);
                        luaL_argcheck(
                            L,
                            size >= sizeof(ulong) || s.Length < ((long)1 << (int)(size * NB)),
                            arg,
                            "string length does not fit in given size");
                        // pack length
                        packint(&b, (ulong)s.Length, h.islittle, (uint)size, false);
                        luaL_addlstring(&b, s);
                        totalsize += s.Length;
                        break;
                    }

                case KOption.ZeroString:
                    {
                        // zero-terminated string
                        ReadOnlySpan<byte> s = luaL_checklstring(L, arg);
                        luaL_argcheck(L, !s.Contains((byte)0), arg, "string contains zeros");
                        luaL_addlstring(&b, s);
                        luaL_addchar(&b, '\0'); // add zero at the end
                        totalsize += s.Length + 1;
                        break;
                    }

                case KOption.Padding:
                    luaL_addchar(&b, LUAL_PACKPADBYTE);
                    goto case KOption.PaddAlign;

                case KOption.PaddAlign:
                case KOption.Nop:
                    arg--; // undo increment
                    break;
            }
        }

        luaL_pushresult(&b);
        return 1;
    }

    private static int str_packsize(lua_State* L)
    {
        ReadOnlySpan<char> fmt = luaL_checknetstring(L, 1); // format string
        Header h;
        initheader(L, &h);

        long totalsize = 0; // accumulate total size of result
        while (!fmt.IsEmpty)
        {
            KOption opt = getdetails(&h, totalsize, ref fmt, out long size, out uint ntoalign);
            luaL_argcheck(
                L,
                opt != KOption.String && opt != KOption.ZeroString,
                1,
                "variable-length format");
            size += ntoalign; // total space used by option
            luaL_argcheck(
                L,
                totalsize <= long.MaxValue - size,
                1,
                "format result too large");
            totalsize += size;
        }

        lua_pushinteger(L, totalsize);
        return 1;
    }

    /// <summary>
    /// Unpack an integer with 'size' bytes and 'islittle' endianness.
    /// If size is smaller than the size of a Lua integer and integer
    /// is signed, must do sign extension (propagating the sign to the
    /// higher bits); if size is larger than the size of a Lua integer,
    /// it must check the unread bytes to see whether they do not cause an
    /// overflow.
    /// </summary>
    private static long unpackint(
        lua_State* L,
        ReadOnlySpan<byte> str,
        bool islittle,
        int size,
        bool issigned)
    {
        ulong res = 0;
        int limit = size <= sizeof(long) ? size : sizeof(long);
        for (int i = limit - 1; i >= 0; i--)
        {
            res <<= NB;
            res |= str[islittle ? i : size - 1 - i];
        }

        if (size < sizeof(long))
        {
            // real size smaller than long?
            if (issigned)
            {
                // needs sign extension?
                ulong mask = 1UL << size * NB - 1;
                res = (res ^ mask) - mask; // do sign extension
            }
        }
        else if (size > sizeof(long))
        {
            // must check unread bytes
            int mask = (!issigned || (long)res >= 0) ? 0 : MC;
            for (int i = limit; i < size; i++)
            {
                if (str[islittle ? i : size - 1 - i] != mask)
                {
                    luaL_error(L, "%d-byte integer does not fit into Lua Integer", size);
                }
            }
        }

        return (long)res;
    }

    private static int str_unpack(lua_State* L)
    {
        ReadOnlySpan<char> fmt = luaL_checknetstring(L, 1).AsSpan();
        ReadOnlySpan<byte> data = luaL_checkstring(L, 2);
        int pos = (int)(posrelatI(luaL_optinteger(L, 3, 1), data.Length) - 1);
        luaL_argcheck(L, pos <= data.Length, 3, "initial position out of string");

        int n = 0; // number of results
        Header h;
        initheader(L, &h);
        while (!fmt.IsEmpty)
        {
            KOption opt = getdetails(&h, pos, ref fmt, out long size, out uint ntoalign);
            luaL_argcheck(
                L,
                ntoalign + size <= data.Length - pos,
                2,
                "data string too short");
            pos += (int)ntoalign; // skip alignment
            // stack space for item + next position
            luaL_checkstack(L, 2, "too many results");
            n++;
            switch (opt)
            {
                case KOption.Int:
                case KOption.UInt:
                    {
                        long res = unpackint(
                            L,
                            data[pos..],
                            h.islittle,
                            (int)size,
                            opt == KOption.Int);
                        lua_pushinteger(L, res);
                        break;
                    }

                case KOption.Float:
                    {
                        float f;
                        fixed (byte* ptr = data[pos..])
                        {
                            copywithendian((byte*)&f, ptr, sizeof(float), h.islittle);
                        }

                        lua_pushnumber(L, f);
                        break;
                    }

                case KOption.Number:
                case KOption.Double:
                    {
                        double f;
                        fixed (byte* src = data[pos..])
                        {
                            copywithendian((byte*)&f, src, sizeof(double), h.islittle);
                        }
                        lua_pushnumber(L, f);
                        break;
                    }

                case KOption.Char:
                    lua_pushlstring(L, data.Slice(pos, (int)size));
                    break;

                case KOption.String:
                    {
                        ulong len = (ulong)unpackint(L, data[pos..], h.islittle, (int)size, false);
                        luaL_argcheck(L, (long)len <= data.Length - pos - size, 2, "data string too short");
                        lua_pushlstring(L, data.Slice(checked((int)(pos + size)), checked((int)len)));
                        pos += (int)len; // skip string
                        break;
                    }

                case KOption.ZeroString:
                    {
                        ReadOnlySpan<byte> tmp = data[pos..];
                        int len = tmp.IndexOf((byte)0);
                        if (len <= 0)
                        {
                            len = tmp.Length;
                        }

                        luaL_argcheck(L, pos + len < data.Length, 2, "unfinished string for format 'z'");
                        lua_pushlstring(L, data.Slice(pos, len));
                        pos += len + 1; // skip string plus final '\0'
                        break;
                    }

                case KOption.PaddAlign:
                case KOption.Padding:
                case KOption.Nop:
                    n--; // undo increment
                    break;
            }

            pos += (int)size;
        }

        lua_pushinteger(L, pos + 1); // next position
        return n + 1;
    }

    private static readonly luaL_Reg[] strlib =
    [
        new("byte", CFunction.FromFunction(&str_byte)),
        new("char", CFunction.FromFunction(&str_char)),
        new("dump", CFunction.FromFunction(&str_dump)),
        new("find", CFunction.FromFunction(&str_find)),
        new("format", CFunction.FromFunction(&str_format)),
        new("gmatch", CFunction.FromFunction(&gmatch)),
        new("gsub", CFunction.FromFunction(&str_gsub)),
        new("len", CFunction.FromFunction(&str_len)),
        new("lower", CFunction.FromFunction(&str_lower)),
        new("match", CFunction.FromFunction(&str_match)),
        new("rep", CFunction.FromFunction(&str_rep)),
        new("reverse", CFunction.FromFunction(&str_reverse)),
        new("sub", CFunction.FromFunction(&str_sub)),
        new("upper", CFunction.FromFunction(&str_upper)),
        new("pack", CFunction.FromFunction(&str_pack)),
        new("packsize", CFunction.FromFunction(&str_packsize)),
        new("unpack", CFunction.FromFunction(&str_unpack)),
    ];

    private static void createmetatable(lua_State* L)
    {
        // table to be metatable for strings
        luaL_newlibtable(L, stringmetamethods);
        luaL_setfuncs(L, stringmetamethods, 0);
        lua_pushliteral(L, ""); // dummy string
        lua_pushvalue(L, -2); // copy table
        lua_setmetatable(L, -2); // set table as metatable for strings
        lua_pop(L, 1); // pop dummy string
        lua_pushvalue(L, -2); // get string library
        lua_setfield(L, -2, "__index"); // metatable.__index = string
        lua_pop(L, 1); // pop metatable
    }

    /// <summary>
    /// Open string library
    /// </summary>
    public static int luaopen_string(lua_State* L)
    {
        luaL_newlib(L, strlib);
        createmetatable(L);
        return 1;
    }
}
