namespace DigitalSingularity.Lua;

using System.Numerics;

public static unsafe partial class Lua
{
    // $Id: lutf8lib.c $
    // Standard library for UTF-8 manipulation
    // See Copyright Notice in lua.h

    private const uint MAXUNICODE = 0x10FFFFu;

    private const uint MAXUTF = 0x7FFFFFFFu;

    private const string MSGInvalid = "invalid UTF-8 code";

    private static bool iscont(byte c)
    {
        return (c & 0xC0) == 0x80;
    }

    private static bool iscontp(ReadOnlySpan<byte> p)
    {
        return !p.IsEmpty && iscont(p[0]);
    }

    /// <summary>
    /// from strlib
    /// translate a relative string position: negative means back from end
    /// </summary>
    private static T u_posrelat<T>(T pos, T len)
        where T : IBinaryInteger<T>
    {
        if (pos >= T.Zero)
        {
            return pos;
        }

        if (T.Zero - pos > len)
        {
            return T.Zero;
        }

        return len + pos + T.One;
    }

    private static readonly uint[] limits = [~(uint)0, 0x80, 0x800, 0x10000u, 0x200000u, 0x4000000u];

    /// <summary>
    /// Decode one UTF-8 sequence, returning null if byte sequence is
    /// invalid.  The array 'limits' stores the minimum value for each
    /// sequence length, to check for overlong representations. Its first
    /// entry forces an error for non-ASCII bytes with no continuation
    /// bytes (count == 0).
    /// </summary>
    private static bool utf8_decode(ReadOnlySpan<byte> s, out uint val, bool strict, out int length)
    {
        byte c = s[0];
        val = 0; // final result
        if (c < 0x80) // ASCII?
        {
            val = c;
            length = 1;
            return true;
        }

        int count = 0; // to count number of continuation bytes
        for (; (c & 0x40) != 0; c <<= 1)
        {
            // while it needs continuation bytes...
            ++count;
            byte cc = s.Length <= count ? (byte)0 : s[count]; // read next byte
            if (!iscont(cc)) // not a continuation byte?
            {
                length = 0;
                return false; // invalid byte sequence
            }

            val = val << 6 | (uint)(cc & 0x3F); // add lower 6 bits from cont. byte
        }

        val |= ((uint)(c & 0x7F) << (count * 5)); // add first byte
        if (count > 5 || val > MAXUTF || val < limits[count])
        {
            length = 0;
            return false; // invalid byte sequence
        }

        if (strict)
        {
            // check for invalid code points; too large or surrogates
            if (val is > MAXUNICODE or >= 0xD800u and <= 0xDFFFu)
            {
                length = 0;
                return false;
            }
        }

        length = count + 1;
        return true;
    }

    /// <summary>
    /// utf8len(s [, i [, j [, lax]]]) --&gt; number of characters that
    /// start in the range [i,j], or nil + current position if 's' is not
    /// well formed in that interval
    /// </summary>
    private static int utflen(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        int posi = u_posrelat((int)luaL_optinteger(L, 2, 1), s.Length);
        int posj = u_posrelat((int)luaL_optinteger(L, 3, -1), s.Length);
        bool lax = lua_toboolean(L, 4);
        luaL_argcheck(
            L,
            1 <= posi && --posi <= (long)s.Length,
            2,
            "initial position out of bounds");
        luaL_argcheck(
            L,
            --posj < (long)s.Length,
            3,
            "final position out of bounds");

        long n = 0; // counter for the number of characters
        while (posi <= posj)
        {
            bool success = utf8_decode(s[posi..], out _, !lax, out int length);
            if (!success)
            {
                // conversion error?
                luaL_pushfail(L); // return fail ...
                lua_pushinteger(L, posi + 1); // ... and current position
                return 2;
            }

            posi += length;
            n++;
        }

        lua_pushinteger(L, n);
        return 1;
    }

    /// <summary>
    /// codepoint(s, [i, [j [, lax]]]) -&gt; returns codepoints for all
    /// characters that start in the range [i,j]
    /// </summary>
    private static int codepoint(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        long posi = u_posrelat(luaL_optinteger(L, 2, 1), s.Length);
        long pose = u_posrelat(luaL_optinteger(L, 3, posi), s.Length);
        bool lax = lua_toboolean(L, 4);
        luaL_argcheck(L, posi >= 1, 2, "out of bounds");
        luaL_argcheck(L, pose <= s.Length, 3, "out of bounds");
        if (posi > pose)
        {
            return 0; // empty interval; return no values
        }

        if (pose - posi >= int.MaxValue) // (long -> int) overflow?
        {
            return luaL_error(L, "string slice too long");
        }

        int n = (int)(pose - posi) + 1; // upper bound for number of returns
        luaL_checkstack(L, n, "string slice too long");
        n = 0; // count the number of returns
        int length = (int)(pose - posi + 1);
        s = s[(int)(posi - 1)..];
        for (int i = 0; i < length;)
        {
            bool success = utf8_decode(s[i..], out uint code, !lax, out int written);
            if (!success)
            {
                return luaL_error(L, MSGInvalid);
            }

            i += written;

            lua_pushinteger(L, code);
            n++;
        }

        return n;
    }

    private static void pushutfchar(lua_State* L, int arg)
    {
        ulong code = (ulong)luaL_checkinteger(L, arg);
        luaL_argcheck(L, code <= MAXUTF, arg, "value out of range");
        lua_pushfstring(L, "%U", code);
    }

    /// <summary>
    /// utfchar(n1, n2, ...)  -&gt; char(n1)..char(n2)...
    /// </summary>
    private static int utfchar(lua_State* L)
    {
        int n = lua_gettop(L); // number of arguments
        if (n == 1) // optimise common case of single char
        {
            pushutfchar(L, 1);
        }
        else
        {
            luaL_Buffer b;
            luaL_buffinit(L, &b);
            for (int i = 1; i <= n; i++)
            {
                pushutfchar(L, i);
                luaL_addvalue(&b);
            }

            luaL_pushresult(&b);
        }

        return 1;
    }

    /// <summary>
    /// offset(s, n, [i])  -&gt; indices where n-th character counting from
    ///   position 'i' starts and ends; 0 means character at 'i'.
    /// </summary>
    private static int byteoffset(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        long n = luaL_checkinteger(L, 2);
        int posi = n >= 0 ? 1 : s.Length + 1;
        posi = u_posrelat((int)luaL_optinteger(L, 3, posi), s.Length);
        luaL_argcheck(
            L,
            1 <= posi && --posi <= s.Length,
            3,
            "position out of bounds");
        if (n == 0)
        {
            // find beginning of current byte sequence
            while (posi > 0 && iscontp(s[posi..]))
            {
                posi--;
            }
        }
        else
        {
            if (iscontp(s[posi..]))
            {
                return luaL_error(L, "initial position is a continuation byte");
            }

            if (n < 0)
            {
                while (n < 0 && posi > 0)
                {
                    // move back
                    do
                    {
                        // find beginning of previous character
                        posi--;
                    } while (posi > 0 && iscontp(s[posi..]));

                    n++;
                }
            }
            else
            {
                n--; // do not move for 1st character
                while (n > 0 && posi < (long)s.Length)
                {
                    do
                    {
                        // find beginning of next character
                        posi++;
                    } while (iscontp(s[posi..])); // (cannot pass final '\0')

                    n--;
                }
            }
        }

        if (n != 0)
        {
            // did not find given character?
            luaL_pushfail(L);
            return 1;
        }

        lua_pushinteger(L, posi + 1); // initial position
        if (posi < s.Length && (s[posi] & 0x80) != 0)
        {
            // multi-byte character?
            if (iscont(s[posi]))
            {
                return luaL_error(L, "initial position is a continuation byte");
            }

            while (iscontp(s[(posi + 1)..]))
            {
                posi++; // skip to last continuation byte
            }
        }

        // else one-byte character: final position is the initial one
        lua_pushinteger(L, posi + 1); // 'posi' now is the final position
        return 2;
    }

    private static int iter_aux(lua_State* L, bool strict)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        int n = (int)lua_tointeger(L, 2);
        if (n < 0)
        {
            return 0;
        }
        
        while (n < s.Length && iscontp(s[n..]))
        {
            n++; // go to next character
        }

        if (n >= s.Length || n < 0) // (also handles original 'n' being negative)
        {
            return 0; // no more codepoints
        }

        bool success = utf8_decode(s[n..], out uint code, strict, out int length);
        if (!success || iscontp(s[(n + length)..]))
        {
            return luaL_error(L, MSGInvalid);
        }

        lua_pushinteger(L, n + 1);
        lua_pushinteger(L, code);
        return 2;
    }

    private static int iter_auxstrict(lua_State* L)
    {
        return iter_aux(L, true);
    }

    private static int iter_auxlax(lua_State* L)
    {
        return iter_aux(L, false);
    }

    private static int iter_codes(lua_State* L)
    {
        bool lax = lua_toboolean(L, 2);
        ReadOnlySpan<byte> s = luaL_checkstring(L, 1);
        luaL_argcheck(L, !iscontp(s), 1, MSGInvalid);
        lua_pushcfunction(L, lax ? &iter_auxlax : &iter_auxstrict);
        lua_pushvalue(L, 1);
        lua_pushinteger(L, 0);
        return 3;
    }

    private static luaL_Reg[] funcs =
    [
        new("offset", &byteoffset),
        new("codepoint", &codepoint),
        new("char", &utfchar),
        new("len", &utflen),
        new("codes", &iter_codes),
        // placeholders
        new("charpattern", null),
    ];

    public static int luaopen_utf8(lua_State* L)
    {
        luaL_newlib(L, funcs);
        // pattern to match a single UTF-8 character
        
        // "[\0-\x7F\xC2-\xFD][\x80-\xBF]*"
        ReadOnlySpan<byte> charpattern =
        [
            (byte)'[', 0x00, (byte)'-', 0x7F, 0xC2, (byte)'-', 0xFD, (byte)']',
            (byte)'[', 0x80, (byte)'-', 0xBF, (byte)']', (byte)'*',
        ];
        lua_pushlstring(L, charpattern);
        lua_setfield(L, -2, "charpattern");
        return 1;
    }
}
