namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    /*
    ** Memory-allocation error message must be preallocated (it cannot
    ** be created after memory is exhausted)
    */
    private const string MEMERRMSG = "not enough memory";

    /*
    ** Maximum length for short strings, that is, strings that are
    ** internalised. (Cannot be smaller than reserved words or tags for
    ** metamethods, as these strings must be internalized;
    ** #("function") = 8, #("__newindex") = 10.)
    */
    private const int LUAI_MAXSHORTLEN = 40;

    private static readonly int TStringContentsOffset = (int)Marshal.OffsetOf<TString>(nameof(TString.contents));

    /*
    ** Size of a short TString: Size of the header plus space for the string
    ** itself (including final '\0').
    */
    private static long sizestrshr(long l)
    {
        return TStringContentsOffset + l + 1;
    }

    internal static TString* luaS_newliteral(lua_State* L, string s)
    {
        byte[] data = Encoding.UTF8.GetBytes(s);
        fixed (byte* dataPtr = data)
        {
            return luaS_newlstr(L, dataPtr, data.Length);
        }
    }

    /*
    ** test whether a string is a reserved word
    */
    private static bool isreserved(TString* s)
    {
        return strisshr(s) && s->extra > 0;
    }

    /*
     ** equality for short strings, which are always internalised
     */
    private static bool eqshrstr(TString* a, TString* b)
    {
        Debug.Assert(a->tt == LUA_VSHRSTR);
        return a == b;
    }
}
