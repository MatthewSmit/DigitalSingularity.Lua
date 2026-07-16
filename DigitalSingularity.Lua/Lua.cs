global using unsafe lua_Alloc = delegate* managed<void*, void*, long, long, void*>;
global using unsafe lua_Hook = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, ref DigitalSingularity.Lua.Lua.lua_Debug, void>;
global using unsafe lua_WarnFunction = delegate* managed<void*, System.ReadOnlySpan<char>, bool, void>;
global using unsafe lua_KFunction = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, int, nint, int>;
global using unsafe StkId = DigitalSingularity.Lua.Lua.StackValue*;
global using unsafe lua_Reader = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, void*, out long, byte*>;
global using unsafe lua_Writer = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, void*, long, void*, int>;

namespace DigitalSingularity.Lua;

using System.Collections.Immutable;
using System.Globalization;

public static unsafe partial class Lua
{
    public delegate void Pfunc(lua_State* L, void* ud);

    public const int LUA_VERSION_MAJOR_N = 5;
    public const int LUA_VERSION_MINOR_N = 5;
    public const int LUA_VERSION_RELEASE_N = 0;

    public const int LUA_VERSION_NUM = LUA_VERSION_MAJOR_N * 100 + LUA_VERSION_MINOR_N;
    public const int LUA_VERSION_RELEASE_NUM = LUA_VERSION_NUM * 100 + LUA_VERSION_RELEASE_N;

    /// <summary>
    /// mark for precompiled code ('&lt;esc&gt;Lua')
    /// </summary>
    public static ReadOnlySpan<byte> LUA_SIGNATURE => "\eLua"u8;

    /// <summary>
    /// option for multiple returns in 'lua_pcall' and 'lua_call'
    /// </summary>
    public const int LUA_MULTRET = -1;

    /// <summary>
    /// Pseudo-indices
    /// (The stack size is limited to INT_MAX/2; we keep some free empty
    /// space after that to help overflow detection.)
    /// </summary>
    public const int LUA_REGISTRYINDEX = -(int.MaxValue / 2 + 1000);

    public static int lua_upvalueindex(int i)
    {
        return LUA_REGISTRYINDEX - i;
    }

    /// <summary>
    /// thread status
    /// </summary>
    public const byte LUA_OK = 0;
    public const byte LUA_YIELD = 1;
    public const byte LUA_ERRRUN = 2;
    public const byte LUA_ERRSYNTAX = 3;
    public const byte LUA_ERRMEM = 4;
    public const byte LUA_ERRERR = 5;

    /// <summary>
    /// basic types
    /// </summary>
    public const int LUA_TNONE = -1;

    public const int LUA_TNIL = 0;
    public const int LUA_TBOOLEAN = 1;
    public const int LUA_TLIGHTUSERDATA = 2;
    public const int LUA_TNUMBER = 3;
    public const int LUA_TSTRING = 4;
    public const int LUA_TTABLE = 5;
    public const int LUA_TFUNCTION = 6;
    public const int LUA_TUSERDATA = 7;
    public const int LUA_TTHREAD = 8;

    public const int LUA_NUMTYPES = 9;

    /// <summary>
    /// minimum Lua stack available to a C function
    /// </summary>
    public const int LUA_MINSTACK = 20;

    /// <summary>
    /// predefined values in the registry
    /// index 1 is reserved for the reference mechanism
    /// </summary>
    private static byte LUA_RIDX_GLOBALS = 2;
    private static byte LUA_RIDX_MAINTHREAD = 3;
    private static byte LUA_RIDX_LAST = 3;

// type for integer functions
// typedef LUA_INTEGER long;
//
// unsigned integer type
// typedef LUA_UNSIGNED lua_Unsigned;

    // Comparison and arithmetic functions

    public const int LUA_OPADD = 0; // ORDER TM, ORDER OP
    public const int LUA_OPSUB = 1;
    public const int LUA_OPMUL = 2;
    public const int LUA_OPMOD = 3;
    public const int LUA_OPPOW = 4;
    public const int LUA_OPDIV = 5;
    public const int LUA_OPIDIV = 6;
    public const int LUA_OPBAND = 7;
    public const int LUA_OPBOR = 8;
    public const int LUA_OPBXOR = 9;
    public const int LUA_OPSHL = 10;
    public const int LUA_OPSHR = 11;
    public const int LUA_OPUNM = 12;
    public const int LUA_OPBNOT = 13;

    public const int LUA_OPEQ = 0;
    public const int LUA_OPLT = 1;
    public const int LUA_OPLE = 2;
    
    public static void lua_call(lua_State* L, int n, int r)
    {
        lua_callk(L, n, r, 0, null);
    }
    
    public static int lua_pcall(lua_State* L, int n, int r, int f)
    {
        return lua_pcallk(L, n, r, f, 0, null);
    }

    public static int lua_yield(lua_State* L, int n)
    {
        return lua_yieldk(L, n, 0, null);
    }

    // garbage-collection options

    public const byte LUA_GCSTOP = 0;
    public const byte LUA_GCRESTART = 1;
    public const byte LUA_GCCOLLECT = 2;
    public const byte LUA_GCCOUNT = 3;
    public const byte LUA_GCCOUNTB = 4;
    public const byte LUA_GCSTEP = 5;
    public const byte LUA_GCISRUNNING = 6;
    public const byte LUA_GCGEN = 7;
    public const byte LUA_GCINC = 8;
    public const byte LUA_GCPARAM = 9;

    /// <summary>
    /// garbage-collection parameters
    /// parameters for generational mode
    /// </summary>
    private const byte LUA_GCPMINORMUL = 0; // control minor collections
    private const byte LUA_GCPMAJORMINOR = 1; // control shift major->minor
    private const byte LUA_GCPMINORMAJOR = 2; // control shift minor->major

    /// <summary>
    /// parameters for incremental mode
    /// </summary>
    private const byte LUA_GCPPAUSE = 3; // size of pause between successive GCs
    private const byte LUA_GCPSTEPMUL = 4; // GC "speed"
    private const byte LUA_GCPSTEPSIZE = 5; // GC granularity

    /// <summary>
    /// number of parameters
    /// </summary>
    private const byte LUA_GCPN = 6;

    public const int LUA_N2SBUFFSZ = 64;

    // {==============================================================
    // some useful macros
    // ===============================================================

    public static void* lua_getextraspace(lua_State* L)
    {
        return (byte*)L - LUA_EXTRASPACE;
    }

    public static double lua_tonumber(lua_State* L, int i)
    {
        return lua_tonumberx(L, i, out _);
    }

    public static long lua_tointeger(lua_State* L, int i)
    {
        return lua_tointegerx(L, i, out _);
    }

    public static void lua_pop(lua_State* L, int n)
    {
        lua_settop(L, -n - 1);
    }

    public static void lua_newtable(lua_State* L)
    {
        lua_createtable(L, 0, 0);
    }

    public static void lua_register(lua_State* L, string n, CFunction f)
    {
        lua_pushcfunction(L, f);
        lua_setglobal(L, n);
    }

    public static void lua_pushcfunction(lua_State* L, CFunction f)
    {
        lua_pushcclosure(L, f, 0);
    }

    public static bool lua_isfunction(lua_State* L, int n)
    {
        return lua_type(L, n) == LUA_TFUNCTION;
    }

    public static bool lua_istable(lua_State* L, int n)
    {
        return lua_type(L, n) == LUA_TTABLE;
    }

    public static bool lua_islightuserdata(lua_State* L, int n)
    {
        return lua_type(L, n) == LUA_TLIGHTUSERDATA;
    }

    public static bool lua_isnil(lua_State* L, int n)
    {
        return lua_type(L, n) == LUA_TNIL;
    }

    public static bool lua_isboolean(lua_State* L, int n)
    {
        return lua_type(L, n) == LUA_TBOOLEAN;
    }

    public static bool lua_isthread(lua_State* L, int n)
    {
        return lua_type(L, n) == LUA_TTHREAD;
    }

    public static bool lua_isnone(lua_State* L, int n)
    {
        return lua_type(L, n) == LUA_TNONE;
    }

    public static bool lua_isnoneornil(lua_State* L, int n)
    {
        return lua_type(L, n) <= 0;
    }

    public static void lua_pushliteral(lua_State* L, string? s)
    {
        lua_pushstring(L, s);
    }

    public static void lua_pushglobaltable(lua_State* L)
    {
        lua_rawgeti(L, LUA_REGISTRYINDEX, LUA_RIDX_GLOBALS);
    }

    public static byte* lua_tostring(lua_State* L, int i)
    {
        return lua_tolstring(L, i, out _);
    }

    public static void lua_insert(lua_State* L, int idx)
    {
        lua_rotate(L, idx, 1);
    }

    public static void lua_remove(lua_State* L, int idx)
    {
        lua_rotate(L, idx, -1);
        lua_pop(L, 1);
    }

    public static void lua_replace(lua_State* L, int idx)
    {
        lua_copy(L, -1, idx);
        lua_pop(L, 1);
    }
    
    // {==============================================================
    // compatibility macros
    // ===============================================================

    public static void* lua_newuserdata(lua_State* L, long s)
    {
        return lua_newuserdatauv(L, s, 1);
    }

    public static int lua_getuservalue(lua_State* L, int idx)
    {
        return lua_getiuservalue(L, idx, 1);
    }

    public static bool lua_setuservalue(lua_State* L, int idx)
    {
        return lua_setiuservalue(L, idx, 1);
    }

    public static int lua_resetthread(lua_State* L)
    {
        return lua_closethread(L, null);
    }

    // }==============================================================

    // {======================================================================
    // Debug API
    // =======================================================================

    /// <summary>
    /// Event codes
    /// </summary>
    public const byte LUA_HOOKCALL = 0;
    public const byte LUA_HOOKRET = 1;
    public const byte LUA_HOOKLINE = 2;
    public const byte LUA_HOOKCOUNT = 3;
    public const byte LUA_HOOKTAILCALL = 4;

    /// <summary>
    /// Event masks
    /// </summary>
    public const int LUA_MASKCALL = 1 << LUA_HOOKCALL;
    public const int LUA_MASKRET = 1 << LUA_HOOKRET;
    public const int LUA_MASKLINE = 1 << LUA_HOOKLINE;
    public const int LUA_MASKCOUNT = 1 << LUA_HOOKCOUNT;

    public struct lua_Debug
    {
        public int @event;
        public string? name; // (n)
        public string? namewhat; // (n) 'global', 'local', 'field', 'method'
        public string what; // (S) 'Lua', 'C', 'main', 'tail'
        public string source; // (S)
        public int currentline; // (l)
        public int linedefined; // (S)
        public int lastlinedefined; // (S)
        public byte nups; // (u) number of upvalues
        public byte nparams; // (u) number of parameters
        public bool isvararg; // (u)
        public byte extraargs; // (t) number of extra arguments
        public bool istailcall; // (t)
        public int ftransfer; // (r) index of first value transferred
        public int ntransfer; // (r) number of transferred values
        public string short_src; // (S)

        /// <summary>
        /// private part
        /// </summary>
        internal CallInfo* i_ci; // active function
    }

    public static readonly string LUA_VERSION_MAJOR = LUA_VERSION_MAJOR_N.ToString(CultureInfo.InvariantCulture);
    public static readonly string LUA_VERSION_MINOR = LUA_VERSION_MINOR_N.ToString(CultureInfo.InvariantCulture);
    public static readonly string LUA_VERSION_RELEASE = LUA_VERSION_RELEASE_N.ToString(CultureInfo.InvariantCulture);

    public static readonly string LUA_VERSION = "Lua " + LUA_VERSION_MAJOR + "." + LUA_VERSION_MINOR;
    public static readonly string LUA_RELEASE = LUA_VERSION + "." + LUA_VERSION_RELEASE;

    public static readonly string LUA_COPYRIGHT = LUA_RELEASE + "  Copyright (C) 1994-2025 Lua.org, PUC-Rio";
    public const string LUA_AUTHORS = "R. Ierusalimschy, L. H. de Figueiredo, W. Celes";
    
    /// <summary>
    /// RCS ident string
    /// </summary>
    public static readonly ImmutableArray<string> lua_ident =
    [
        $"$LuaVersion: {LUA_COPYRIGHT} $",
        $"$LuaAuthors: {LUA_AUTHORS} $",
    ];


    // Copyright (C) 1994-2025 Lua.org, PUC-Rio.
    //
    // Permission is hereby granted, free of charge, to any person obtaining
    // a copy of this software and associated documentation files (the
    // "Software"), to deal in the Software without restriction, including
    // without limitation the rights to use, copy, modify, merge, publish,
    // distribute, sublicense, and/or sell copies of the Software, and to
    // permit persons to whom the Software is furnished to do so, subject to
    // the following conditions:
    //
    // The above copyright notice and this permission notice shall be
    // included in all copies or substantial portions of the Software.
    //
    // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    // EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    // MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
    // IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
    // CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
    // TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
    // SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
}
