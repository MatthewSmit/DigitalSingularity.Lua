namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static partial class Lua
{
    // $Id: lctype.h $
    // 'ctype' functions for Lua
    // See Copyright Notice in lua.h

    // WARNING: the functions defined here do not necessarily correspond
    // to the similar functions in the standard C ctype.h. They are
    // optimised for the specific needs of Lua.

    private const int ALPHABIT = 0;
    private const int DIGITBIT = 1;
    private const int PRINTBIT = 2;
    private const int SPACEBIT = 3;
    private const int XDIGITBIT = 4;

    /// <summary>
    /// add 1 to char to allow index -1 (EOZ)
    /// </summary>
    private static bool testprop(int c, int p)
    {
        return (luai_ctype_[c + 1] & p) != 0;
    }

    /// <summary>
    /// 'lalpha' (Lua alphabetic) and 'lalnum' (Lua alphanumeric) both include '_'
    /// </summary>
    private static bool lislalpha(int c)
    {
        return testprop(c, 1 << ALPHABIT);
    }

    private static bool lislalnum(int c)
    {
        return testprop(c, 1 << ALPHABIT | 1 << DIGITBIT);
    }

    private static bool lisdigit(int c)
    {
        return testprop(c, 1 << DIGITBIT);
    }

    private static bool lisspace(int c)
    {
        return testprop(c, 1 << SPACEBIT);
    }

    private static bool lisprint(int c)
    {
        return testprop(c, 1 << PRINTBIT);
    }

    private static bool lisxdigit(int c)
    {
        return testprop(c, 1 << XDIGITBIT);
    }

    /// <summary>
    /// In ASCII, this 'ltolower' is correct for alphabetic characters and
    /// for '.'. That is enough for Lua needs. ('check_exp' ensures that
    /// the character either is an upper-case letter or is unchanged by
    /// the transformation, which holds for lower-case letters and '.'.)
    /// </summary>
    private static char ltolower(int c)
    {
        Debug.Assert(c is >= 'A' and <= 'Z' || c == (c | 'A' ^ 'a'));
        return (char)(c | 'A' ^ 'a');
    }

#if LUA_UCID
    /// <summary>
    /// accept UniCode IDentifiers?
    /// consider all non-ASCII codepoints to be alphabetic
    /// </summary>
    private const int NONA = 0x01;
#else
    private const int NONA = 0x00;
#endif

    /// <summary>
    /// one entry for each character and for -1 (EOZ)
    /// </summary>
    private static readonly byte[] luai_ctype_ =
    [
        0x00, // EOZ
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 0.
        0x00, 0x08, 0x08, 0x08, 0x08, 0x08, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 1.
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x0c, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, // 2.
        0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04,
        0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, // 3.
        0x16, 0x16, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04,
        0x04, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x05, // 4.
        0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05,
        0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, // 5.
        0x05, 0x05, 0x05, 0x04, 0x04, 0x04, 0x04, 0x05,
        0x04, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x05, // 6.
        0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05,
        0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, // 7.
        0x05, 0x05, 0x05, 0x04, 0x04, 0x04, 0x04, 0x00,
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA, // 8.
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA,
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA, // 9.
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA,
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA, // a.
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA,
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA, // b.
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA,
        0x00, 0x00, NONA, NONA, NONA, NONA, NONA, NONA, // c.
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA,
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA, // d.
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA,
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA, // e.
        NONA, NONA, NONA, NONA, NONA, NONA, NONA, NONA,
        NONA, NONA, NONA, NONA, NONA, 0x00, 0x00, 0x00, // f.
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];
}
