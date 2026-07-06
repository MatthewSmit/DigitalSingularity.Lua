global using unsafe lua_Alloc = delegate* managed<void*, void*, long, long, void*>;
global using unsafe lua_Hook = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, ref DigitalSingularity.Lua.Lua.lua_Debug, void>;
global using unsafe lua_WarnFunction = delegate* managed<void*, string, bool, void>;
global using unsafe lua_CFunction = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, int>;
global using unsafe lua_KFunction = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, int, void*, int>;
global using unsafe StkId = DigitalSingularity.Lua.Lua.StackValue*;
global using unsafe lua_Reader = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, void*, long*, byte*>;
global using unsafe lua_Writer = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, void*, long, void*, int>;

namespace DigitalSingularity.Lua;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;

public static unsafe partial class Lua
{
    public delegate void Pfunc(lua_State* L, void* ud);

    public const int LUA_VERSION_MAJOR_N = 5;
    public const int LUA_VERSION_MINOR_N = 5;
    public const int LUA_VERSION_RELEASE_N = 0;

    public const int LUA_VERSION_NUM = LUA_VERSION_MAJOR_N * 100 + LUA_VERSION_MINOR_N;
    public const int LUA_VERSION_RELEASE_NUM = LUA_VERSION_NUM * 100 + LUA_VERSION_RELEASE_N;

    /* mark for precompiled code ('<esc>Lua') */
    public const string LUA_SIGNATURE = "\e" + "Lua";

    /* option for multiple returns in 'lua_pcall' and 'lua_call' */
    public const int LUA_MULTRET = -1;

    /*
    ** Pseudo-indices
    ** (The stack size is limited to INT_MAX/2; we keep some free empty
    ** space after that to help overflow detection.)
    */
    public const int LUA_REGISTRYINDEX = -(int.MaxValue / 2 + 1000);

    public static int lua_upvalueindex(int i)
    {
        return LUA_REGISTRYINDEX - i;
    }

    /* thread status */
    public const byte LUA_OK = 0;
    public const byte LUA_YIELD = 1;
    public const byte LUA_ERRRUN = 2;
    public const byte LUA_ERRSYNTAX = 3;
    public const byte LUA_ERRMEM = 4;
    public const byte LUA_ERRERR = 5;

    /*
    ** basic types
    */
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

    /* minimum Lua stack available to a C function */
    public const int LUA_MINSTACK = 20;

    /* predefined values in the registry */
    /* index 1 is reserved for the reference mechanism */
    private static byte LUA_RIDX_GLOBALS = 2;
    private static byte LUA_RIDX_MAINTHREAD = 3;
    private static byte LUA_RIDX_LAST = 3;

// /* type of numbers in Lua */ TODO
// typedef LUA_NUMBER double;
//
//
// /* type for integer functions */
// typedef LUA_INTEGER long;
//
// /* unsigned integer type */
// typedef LUA_UNSIGNED lua_Unsigned;

// /*
// ** Type for continuation functions TODO
// */
// typedef int (*lua_KFunction) (lua_State *L, int status, nint ctx);

    /*
    ** Comparison and arithmetic functions
    */

    public const int LUA_OPADD = 0;	/* ORDER TM, ORDER OP */
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

    public static partial void lua_arith(lua_State* L, int op);

    public const int LUA_OPEQ = 0;
    public const int LUA_OPLT = 1;
    public const int LUA_OPLE = 2;

    public static partial bool lua_rawequal(lua_State* L, int index1, int index2);

    public static partial bool lua_compare(lua_State* L, int idx1, int idx2, int op);

    /*
    ** push functions (C -> stack)
    */
    public static partial void lua_pushnil(lua_State* L);
    
    public static partial void lua_pushnumber(lua_State* L, double n);

    public static partial void lua_pushinteger(lua_State* L, long n);
    
    public static partial void lua_pushlstring(lua_State* L, ReadOnlySpan<byte> s);
    public static partial void lua_pushlstring(lua_State* L, ReadOnlySpan<char> s);

    public static partial void lua_pushexternalstring(lua_State* L, byte* s, int len, lua_Alloc falloc, void* ud);
    
    public static partial string lua_pushfstring(lua_State* L, string fmt, params object[] args);

    public static partial void lua_pushcclosure(lua_State* L, lua_CFunction fn, int n);

    public static partial void lua_pushboolean(lua_State* L, bool b);

    public static partial void lua_pushlightuserdata(lua_State* L, void* p);

    public static partial bool lua_pushthread(lua_State* L);

    /*
    ** get functions (Lua -> stack)
    */
    public static partial int lua_getglobal(lua_State* L, string name);

    public static partial int lua_gettable(lua_State* L, int idx);

    public static partial int lua_getfield(lua_State* L, int idx, string k);

    public static partial int lua_geti(lua_State* L, int idx, long n);

    public static partial int lua_rawget(lua_State* L, int idx);

    public static partial int lua_rawgeti(lua_State* L, int idx, long n);

    public static partial int lua_rawgetp(lua_State* L, int idx, void* p);

    public static partial void lua_createtable(lua_State* L, int narr, int nrec);

    public static partial void* lua_newuserdatauv(lua_State* L, long sz, int nuvalue);

    public static partial bool lua_getmetatable(lua_State* L, int objindex);

    public static partial int lua_getiuservalue(lua_State* L, int idx, int n);

    /*
    ** set functions (stack -> Lua)
    */
    public static partial void lua_setglobal(lua_State* L, string name);

    public static partial void lua_settable(lua_State* L, int idx);

    public static partial void lua_setfield(lua_State* L, int idx, string k);

    public static partial void lua_seti(lua_State* L, int idx, long n);

    public static partial void lua_rawset(lua_State* L, int idx);

    public static partial void lua_rawseti(lua_State* L, int idx, long n);

    public static partial void lua_rawsetp(lua_State* L, int idx, void* p);

    public static partial bool lua_setmetatable(lua_State* L, int objindex);

    public static partial bool lua_setiuservalue(lua_State* L, int idx, int n);

    /*
    ** 'load' and 'call' functions (load and run Lua code)
    */
    public static partial void lua_callk(
        lua_State* L,
        int nargs,
        int nresults,
        nuint ctx,
        lua_KFunction k);
    public static void lua_call(lua_State* L, int n, int r)
    {
        lua_callk(L, n, r, 0, null);
    }

    public static partial int lua_pcallk(
        lua_State* L,
        int nargs,
        int nresults,
        int errfunc,
        nint ctx,
        lua_KFunction k);
    
    public static int lua_pcall(lua_State* L, int n, int r, int f)
    {
        return lua_pcallk(L, n, r, f, 0, null);
    }

    public static partial int lua_load(lua_State* L, lua_Reader reader, void* dt, string? chunkname, string? mode);

    public static partial int lua_dump(lua_State* L, lua_Writer writer, void* data, bool strip);

    /*
    ** coroutine functions
    */
    public static partial int lua_yieldk(lua_State* L, int nresults, nint ctx, lua_KFunction k);

    public static partial int lua_resume(lua_State* L, lua_State* from, int narg, int* nres);

    public static partial int lua_status(lua_State* L);

    public static partial bool lua_isyieldable(lua_State* L);

    public static int lua_yield(lua_State* L, int n)
    {
        return lua_yieldk(L, n, 0, null);
    }

    /*
     ** Warning-related functions
     */
    public static partial void lua_setwarnf(lua_State* L, lua_WarnFunction f, void* ud);

    public static partial void lua_warning(lua_State* L, string msg, bool tocont);

    /*
    ** garbage-collection options
    */

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

    /*
    ** garbage-collection parameters
    */
    /* parameters for generational mode */
    private const byte LUA_GCPMINORMUL = 0;  /* control minor collections */
    private const byte LUA_GCPMAJORMINOR = 1;  /* control shift major->minor */
    private const byte LUA_GCPMINORMAJOR = 2;  /* control shift minor->major */

    /* parameters for incremental mode */
    private const byte LUA_GCPPAUSE = 3;  /* size of pause between successive GCs */
    private const byte LUA_GCPSTEPMUL = 4;  /* GC "speed" */
    private const byte LUA_GCPSTEPSIZE = 5;  /* GC granularity */

    /* number of parameters */
    private const byte LUA_GCPN = 6;

    public static partial int lua_gc(lua_State* L, int what, params object[] args);

    public const int LUA_N2SBUFFSZ = 64;

    /*
    ** {==============================================================
    ** some useful macros
    ** ===============================================================
    */

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

    public static void lua_register(lua_State* L, string n, lua_CFunction f)
    {
        lua_pushcfunction(L, f);
        lua_setglobal(L, n);
    }

    public static void lua_pushcfunction(lua_State* L, lua_CFunction f)
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

    public static byte* lua_tostringp(lua_State* L, int i)
    {
        return lua_tolstring(L, i, out _);
    }

    public static string? lua_tostring(lua_State* L, int i)
    {
        byte* tmp = lua_tolstring(L, i, out long size);
        if (tmp == null)
        {
            return null;
        }
        
        ReadOnlySpan<byte> span = new(tmp, checked((int)size));
        return Encoding.UTF8.GetString(span);
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
    
    /*
    ** {==============================================================
    ** compatibility macros
    ** ===============================================================
    */

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

    /* }============================================================== */

    /*
    ** {======================================================================
    ** Debug API
    ** =======================================================================
    */

    /*
    ** Event codes
    */
    public const byte LUA_HOOKCALL = 0;
    public const byte LUA_HOOKRET = 1;
    public const byte LUA_HOOKLINE = 2;
    public const byte LUA_HOOKCOUNT = 3;
    public const byte LUA_HOOKTAILCALL = 4;

    /*
    ** Event masks
    */
    public const int LUA_MASKCALL = 1 << LUA_HOOKCALL;
    public const int LUA_MASKRET = 1 << LUA_HOOKRET;
    public const int LUA_MASKLINE = 1 << LUA_HOOKLINE;
    public const int LUA_MASKCOUNT = 1 << LUA_HOOKCOUNT;

    public static partial bool lua_getstack(lua_State* L, int level, ref lua_Debug ar);

    public static partial bool lua_getinfo(lua_State* L, string what, ref lua_Debug ar);

    public static partial string? lua_getlocal(lua_State* L, int n);
    
    public static partial string? lua_getlocal(lua_State* L, ref lua_Debug ar, int n);

    public static partial string? lua_setlocal(lua_State* L, ref lua_Debug ar, int n);

    public static partial string? lua_getupvalue(lua_State* L, int funcindex, int n);

    public static partial string? lua_setupvalue(lua_State* L, int funcindex, int n);

    public static partial void* lua_upvalueid(lua_State* L, int fidx, int n);
    
    public static partial void lua_upvaluejoin(lua_State* L, int fidx1, int n1, int fidx2, int n2);

    public static partial void lua_sethook(lua_State* L, lua_Hook func, byte mask, int count);

    public static partial lua_Hook lua_gethook(lua_State* L);

    public static partial byte lua_gethookmask(lua_State* L);

    public static partial int lua_gethookcount(lua_State* L);

    public struct lua_Debug
    {
        public int @event;
        public string? name; /* (n) */
        public string? namewhat; /* (n) 'global', 'local', 'field', 'method' */
        public string what; /* (S) 'Lua', 'C', 'main', 'tail' */
        public string source; /* (S) */
        public int currentline; /* (l) */
        public int linedefined; /* (S) */
        public int lastlinedefined; /* (S) */
        public byte nups;	/* (u) number of upvalues */
        public byte nparams; /* (u) number of parameters */
        public bool isvararg;        /* (u) */
        public byte extraargs;  /* (t) number of extra arguments */
        public bool istailcall;	/* (t) */
        public int ftransfer;   /* (r) index of first value transferred */
        public int ntransfer;   /* (r) number of transferred values */
        public string short_src; /* (S) */

        /* private part */
        internal CallInfo* i_ci; /* active function */
    }

    public static readonly string LUA_VERSION_MAJOR = LUA_VERSION_MAJOR_N.ToString(CultureInfo.InvariantCulture);
    public static readonly string LUA_VERSION_MINOR = LUA_VERSION_MINOR_N.ToString(CultureInfo.InvariantCulture);
    public static readonly string LUA_VERSION_RELEASE = LUA_VERSION_RELEASE_N.ToString(CultureInfo.InvariantCulture);

    public static readonly string LUA_VERSION = "Lua " + LUA_VERSION_MAJOR + "." + LUA_VERSION_MINOR;
    public static readonly string LUA_RELEASE = LUA_VERSION + "." + LUA_VERSION_RELEASE;

    public static readonly string LUA_COPYRIGHT = LUA_RELEASE + "  Copyright (C) 1994-2025 Lua.org, PUC-Rio";
    public const string LUA_AUTHORS = "R. Ierusalimschy, L. H. de Figueiredo, W. Celes";
    
    /*
     ** RCS ident string
     */
    public static readonly ImmutableArray<string> lua_ident =
    [
        $"$LuaVersion: {LUA_COPYRIGHT} $",
        $"$LuaAuthors: {LUA_AUTHORS} $",
    ];


    /******************************************************************************
    * Copyright (C) 1994-2025 Lua.org, PUC-Rio.
    *
    * Permission is hereby granted, free of charge, to any person obtaining
    * a copy of this software and associated documentation files (the
    * "Software"), to deal in the Software without restriction, including
    * without limitation the rights to use, copy, modify, merge, publish,
    * distribute, sublicense, and/or sell copies of the Software, and to
    * permit persons to whom the Software is furnished to do so, subject to
    * the following conditions:
    *
    * The above copyright notice and this permission notice shall be
    * included in all copies or substantial portions of the Software.
    *
    * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
    * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
    * CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
    * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
    * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
    ******************************************************************************/
}
