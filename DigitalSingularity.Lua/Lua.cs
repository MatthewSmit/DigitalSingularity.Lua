global using unsafe lua_Alloc = delegate* managed<void*, void*, long, long, void*>;
global using unsafe lua_Hook = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, DigitalSingularity.Lua.Lua.lua_Debug*, void>;
global using unsafe lua_WarnFunction = delegate* managed<void*, byte*, int, void>;
global using unsafe lua_CFunction = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, int>;
global using unsafe lua_KFunction = delegate* managed<DigitalSingularity.Lua.Lua.lua_State*, int, void*, int>;
global using unsafe StkId = DigitalSingularity.Lua.Lua.StackValue*;

namespace DigitalSingularity.Lua;

using System.Globalization;

public static unsafe partial class Lua
{
    public delegate void Pfunc(lua_State* L, void* ud);

    public const int LUA_VERSION_MAJOR_N = 5;
    public const int LUA_VERSION_MINOR_N = 5;
    public const int LUA_VERSION_RELEASE_N = 0;

    public const int LUA_VERSION_NUM = LUA_VERSION_MAJOR_N * 100 + LUA_VERSION_MINOR_N;
    public const int LUA_VERSION_RELEASE_NUM = LUA_VERSION_NUM * 100 + LUA_VERSION_RELEASE_N;

// /* mark for precompiled code ('<esc>Lua') */
// #define LUA_SIGNATURE	"\x1bLua"

    /* option for multiple returns in 'lua_pcall' and 'lua_call' */
    private const int LUA_MULTRET = -1;

    /*
    ** Pseudo-indices
    ** (The stack size is limited to INT_MAX/2; we keep some free empty
    ** space after that to help overflow detection.)
    */
    public const int LUA_REGISTRYINDEX = -(int.MaxValue / 2 + 1000);
// #define lua_upvalueindex(i)	(LUA_REGISTRYINDEX - (i))

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

// /* type of numbers in Lua */
// typedef LUA_NUMBER lua_Number;
//
//
// /* type for integer functions */
// typedef LUA_INTEGER lua_Integer;
//
// /* unsigned integer type */
// typedef LUA_UNSIGNED lua_Unsigned;
//
// /* type for continuation-function contexts */
// typedef LUA_KCONTEXT lua_KContext;
//
// /*
// ** Type for continuation functions
// */
// typedef int (*lua_KFunction) (lua_State *L, int status, lua_KContext ctx);
//
//
// /*
// ** Type for functions that read/write blocks when loading/dumping Lua chunks
// */
// typedef const char * (*lua_Reader) (lua_State *L, void *ud, size_t *sz);
//
// typedef int (*lua_Writer) (lua_State *L, const void *p, size_t sz, void *ud);

// /*
// ** Type used by the debug API to collect debug information
// */
// typedef struct lua_Debug lua_Debug;
//
//
//
// /*
// ** generic extra include file
// */
// #if defined(LUA_USER_H)
// #include LUA_USER_H
// #endif
//
//
// /*
// ** RCS ident string
// */
// extern const char lua_ident[];
//

    // state manipulation
    
    public static partial lua_State* lua_newstate(delegate* managed<void*, void*, long, long, void*> f, void* ud, uint seed);

    public static partial void lua_close(lua_State* L);
    
// LUA_API lua_State *(lua_newthread) (lua_State *L);
// LUA_API int        (lua_closethread) (lua_State *L, lua_State *from);

    public static partial lua_CFunction lua_atpanic(lua_State* L, lua_CFunction panicf);

    public static partial long lua_version(lua_State* L);

    /*
    ** basic stack manipulation
    */
    public static partial int lua_absindex(lua_State* L, int idx);
    
// LUA_API int   (lua_gettop) (lua_State *L);

    public static partial void lua_settop(lua_State* L, int idx);

    public static partial void lua_pushvalue(lua_State* L, int idx);

    public static partial void lua_rotate(lua_State* L, int idx, int n);
    
// LUA_API void  (lua_copy) (lua_State *L, int fromidx, int toidx);

    public static partial bool lua_checkstack(lua_State* L, int n);

// LUA_API void  (lua_xmove) (lua_State *from, lua_State *to, int n);
//
//
// /*
// ** access functions (stack -> C)
// */
//
// LUA_API int             (lua_isnumber) (lua_State *L, int idx);
// LUA_API int             (lua_isstring) (lua_State *L, int idx);
// LUA_API int             (lua_iscfunction) (lua_State *L, int idx);
// LUA_API int             (lua_isinteger) (lua_State *L, int idx);
// LUA_API int             (lua_isuserdata) (lua_State *L, int idx);

    public static partial int lua_type(lua_State* L, int idx);

// LUA_API const char     *(lua_typename) (lua_State *L, int tp);

    public static partial double lua_tonumberx(lua_State* L, int idx, out bool isnum);

    public static partial long lua_tointegerx(lua_State* L, int idx, out bool isnum);

    public static partial bool lua_toboolean(lua_State* L, int idx);
    
// LUA_API const char     *(lua_tolstring) (lua_State *L, int idx, size_t *len);
// LUA_API lua_Unsigned    (lua_rawlen) (lua_State *L, int idx);
// LUA_API lua_CFunction   (lua_tocfunction) (lua_State *L, int idx);

    public static partial void* lua_touserdata(lua_State* L, int idx);

// LUA_API lua_State      *(lua_tothread) (lua_State *L, int idx);
// LUA_API const void     *(lua_topointer) (lua_State *L, int idx);
//
//
// /*
// ** Comparison and arithmetic functions
// */
//
// #define LUA_OPADD	0	/* ORDER TM, ORDER OP */
// #define LUA_OPSUB	1
// #define LUA_OPMUL	2
// #define LUA_OPMOD	3
// #define LUA_OPPOW	4
// #define LUA_OPDIV	5
// #define LUA_OPIDIV	6
// #define LUA_OPBAND	7
// #define LUA_OPBOR	8
// #define LUA_OPBXOR	9
// #define LUA_OPSHL	10
// #define LUA_OPSHR	11
// #define LUA_OPUNM	12
// #define LUA_OPBNOT	13
//
// LUA_API void  (lua_arith) (lua_State *L, int op);
//
// #define LUA_OPEQ	0
// #define LUA_OPLT	1
// #define LUA_OPLE	2
//
// LUA_API int   (lua_rawequal) (lua_State *L, int idx1, int idx2);
// LUA_API int   (lua_compare) (lua_State *L, int idx1, int idx2, int op);

    /*
    ** push functions (C -> stack)
    */
    public static partial void lua_pushnil(lua_State* L);
    
    public static partial void lua_pushnumber(lua_State* L, double n);

    public static partial void lua_pushinteger(lua_State* L, long n);

// LUA_API const char *(lua_pushlstring) (lua_State *L, const char *s, size_t len);

    public static partial void lua_pushexternalstring(lua_State* L, byte* s, int len, lua_Alloc falloc, void* ud);

    public static partial void lua_pushstring(lua_State* L, string? s);
    
    public static partial string lua_pushfstring(lua_State* L, string fmt, params object[] args);

    public static partial void lua_pushcclosure(lua_State* L, lua_CFunction fn, int n);

    public static partial void lua_pushboolean(lua_State* L, bool b);

    public static partial void lua_pushlightuserdata(lua_State* L, void* p);

// LUA_API int   (lua_pushthread) (lua_State *L);
//
//
// /*
// ** get functions (Lua -> stack)
// */
// LUA_API int (lua_getglobal) (lua_State *L, const char *name);
// LUA_API int (lua_gettable) (lua_State *L, int idx);

    public static partial int lua_getfield(lua_State* L, int idx, string k);
    
// LUA_API int (lua_geti) (lua_State *L, int idx, lua_Integer n);
// LUA_API int (lua_rawget) (lua_State *L, int idx);

    public static partial int lua_rawgeti(lua_State* L, int idx, long n);

// LUA_API int (lua_rawgetp) (lua_State *L, int idx, const void *p);

    public static partial void lua_createtable(lua_State* L, int narr, int nrec);

    public static partial void* lua_newuserdatauv(lua_State* L, long sz, int nuvalue);
    
// LUA_API int   (lua_getmetatable) (lua_State *L, int objindex);
// LUA_API int  (lua_getiuservalue) (lua_State *L, int idx, int n);

    /*
    ** set functions (stack -> Lua)
    */
    public static partial void lua_setglobal(lua_State* L, string name);
// LUA_API void  (lua_settable) (lua_State *L, int idx);

    public static partial void lua_setfield(lua_State* L, int idx, string k);
    
// LUA_API void  (lua_seti) (lua_State *L, int idx, lua_Integer n);
// LUA_API void  (lua_rawset) (lua_State *L, int idx);

    public static partial void lua_rawseti(lua_State* L, int idx, long n);

// LUA_API void  (lua_rawsetp) (lua_State *L, int idx, const void *p);

    public static partial int lua_setmetatable(lua_State* L, int objindex);

// LUA_API int   (lua_setiuservalue) (lua_State *L, int idx, int n);

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

    // LUA_API int   (lua_load) (lua_State *L, lua_Reader reader, void *dt,
//                           const char *chunkname, const char *mode);
//
// LUA_API int (lua_dump) (lua_State *L, lua_Writer writer, void *data, int strip);
//
//
// /*
// ** coroutine functions
// */
// LUA_API int  (lua_yieldk)     (lua_State *L, int nresults, lua_KContext ctx,
//                                lua_KFunction k);
// LUA_API int  (lua_resume)     (lua_State *L, lua_State *from, int narg,
//                                int *nres);
// LUA_API int  (lua_status)     (lua_State *L);
// LUA_API int (lua_isyieldable) (lua_State *L);
//
// #define lua_yield(L,n)		lua_yieldk(L, (n), 0, NULL)


    /*
    ** Warning-related functions
    */
    public static partial void lua_setwarnf(lua_State* L, lua_WarnFunction f, void* ud);
    
// LUA_API void (lua_warning)  (lua_State *L, const char *msg, int tocont);

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

// /*
// ** miscellaneous functions
// */
//
// LUA_API int   (lua_error) (lua_State *L);
//
// LUA_API int   (lua_next) (lua_State *L, int idx);
//
// LUA_API void  (lua_concat) (lua_State *L, int n);
// LUA_API void  (lua_len)    (lua_State *L, int idx);

    public const int LUA_N2SBUFFSZ = 64;
// LUA_API unsigned  (lua_numbertocstring) (lua_State *L, int idx, char *buff);
// LUA_API size_t  (lua_stringtonumber) (lua_State *L, const char *s);
//
// LUA_API lua_Alloc (lua_getallocf) (lua_State *L, void **ud);
// LUA_API void      (lua_setallocf) (lua_State *L, lua_Alloc f, void *ud);
//
// LUA_API void (lua_toclose) (lua_State *L, int idx);
// LUA_API void (lua_closeslot) (lua_State *L, int idx);

/*
** {==============================================================
** some useful macros
** ===============================================================
*/

    public static void* lua_getextraspace(lua_State* L)
    {
        return (byte*)L - LUA_EXTRASPACE;
    }

    // #define lua_tonumber(L,i)	lua_tonumberx(L,(i),NULL)
    
    public static long lua_tointeger(lua_State* L, int i)
    {
        return lua_tointegerx(L, (i), out _);
    }

    public static void lua_pop(lua_State* L, int n)
    {
        lua_settop(L, -n - 1);
    }

    public static void lua_newtable(lua_State* L)
    {
        lua_createtable(L, 0, 0);
    }

    // #define lua_register(L,n,f) (lua_pushcfunction(L, (f)), lua_setglobal(L, (n)))

    public static void lua_pushcfunction(lua_State* L, lua_CFunction f)
    {
        lua_pushcclosure(L, f, 0);
    }

    // #define lua_isfunction(L,n)	(lua_type(L, (n)) == LUA_TFUNCTION)
// #define lua_istable(L,n)	(lua_type(L, (n)) == LUA_TTABLE)
// #define lua_islightuserdata(L,n)	(lua_type(L, (n)) == LUA_TLIGHTUSERDATA)
// #define lua_isnil(L,n)		(lua_type(L, (n)) == LUA_TNIL)
// #define lua_isboolean(L,n)	(lua_type(L, (n)) == LUA_TBOOLEAN)
// #define lua_isthread(L,n)	(lua_type(L, (n)) == LUA_TTHREAD)
// #define lua_isnone(L,n)		(lua_type(L, (n)) == LUA_TNONE)
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

    // #define lua_tostring(L,i)	lua_tolstring(L, (i), NULL)
//
//
// #define lua_insert(L,idx)	lua_rotate(L, (idx), 1)

    public static void lua_remove(lua_State* L, int idx)
    {
        lua_rotate(L, idx, -1);
        lua_pop(L, 1);
    }

    // #define lua_replace(L,idx)	(lua_copy(L, -1, (idx)), lua_pop(L, 1))
//
// /* }============================================================== */
//
//
// /*
// ** {==============================================================
// ** compatibility macros
// ** ===============================================================
// */
//
// #define lua_newuserdata(L,s)	lua_newuserdatauv(L,s,1)
// #define lua_getuservalue(L,idx)	lua_getiuservalue(L,idx,1)
// #define lua_setuservalue(L,idx)	lua_setiuservalue(L,idx,1)
//
// #define lua_resetthread(L)	lua_closethread(L,NULL)
//
// /* }============================================================== */
//
// /*
// ** {======================================================================
// ** Debug API
// ** =======================================================================
// */

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

// LUA_API int (lua_getstack) (lua_State *L, int level, lua_Debug *ar);
// LUA_API int (lua_getinfo) (lua_State *L, const char *what, lua_Debug *ar);
// LUA_API const char *(lua_getlocal) (lua_State *L, const lua_Debug *ar, int n);
// LUA_API const char *(lua_setlocal) (lua_State *L, const lua_Debug *ar, int n);
// LUA_API const char *(lua_getupvalue) (lua_State *L, int funcindex, int n);
// LUA_API const char *(lua_setupvalue) (lua_State *L, int funcindex, int n);
//
// LUA_API void *(lua_upvalueid) (lua_State *L, int fidx, int n);
// LUA_API void  (lua_upvaluejoin) (lua_State *L, int fidx1, int n1,
//                                                int fidx2, int n2);
//
// LUA_API void (lua_sethook) (lua_State *L, lua_Hook func, int mask, int count);
// LUA_API lua_Hook (lua_gethook) (lua_State *L);
// LUA_API int (lua_gethookmask) (lua_State *L);
// LUA_API int (lua_gethookcount) (lua_State *L);

    public struct lua_Debug
    {
//   int event;
//   const char *name;	/* (n) */
//   const char *namewhat;	/* (n) 'global', 'local', 'field', 'method' */
//   const char *what;	/* (S) 'Lua', 'C', 'main', 'tail' */
//   const char *source;	/* (S) */
//   size_t srclen;	/* (S) */
//   int currentline;	/* (l) */
//   int linedefined;	/* (S) */
//   int lastlinedefined;	/* (S) */
//   unsigned char nups;	/* (u) number of upvalues */
//   unsigned char nparams;/* (u) number of parameters */
//   char isvararg;        /* (u) */
//   unsigned char extraargs;  /* (t) number of extra arguments */
//   char istailcall;	/* (t) */
//   int ftransfer;   /* (r) index of first value transferred */
//   int ntransfer;   /* (r) number of transferred values */
//   char short_src[LUA_IDSIZE]; /* (S) */
//   /* private part */
//   struct CallInfo *i_ci;  /* active function */
    }
    
    /* }====================================================================== */

    public static readonly string LUA_VERSION_MAJOR = LUA_VERSION_MAJOR_N.ToString(CultureInfo.InvariantCulture);
    public static readonly string LUA_VERSION_MINOR = LUA_VERSION_MINOR_N.ToString(CultureInfo.InvariantCulture);
    public static readonly string LUA_VERSION_RELEASE = LUA_VERSION_RELEASE_N.ToString(CultureInfo.InvariantCulture);

    public static readonly string LUA_VERSION = "Lua " + LUA_VERSION_MAJOR + "." + LUA_VERSION_MINOR;
    public static readonly string LUA_RELEASE = LUA_VERSION + "." + LUA_VERSION_RELEASE;

    public static readonly string LUA_COPYRIGHT = LUA_RELEASE + "  Copyright (C) 1994-2025 Lua.org, PUC-Rio";
    public const string LUA_AUTHORS = "R. Ierusalimschy, L. H. de Figueiredo, W. Celes";


    // /******************************************************************************
// * Copyright (C) 1994-2025 Lua.org, PUC-Rio.
// *
// * Permission is hereby granted, free of charge, to any person obtaining
// * a copy of this software and associated documentation files (the
// * "Software"), to deal in the Software without restriction, including
// * without limitation the rights to use, copy, modify, merge, publish,
// * distribute, sublicense, and/or sell copies of the Software, and to
// * permit persons to whom the Software is furnished to do so, subject to
// * the following conditions:
// *
// * The above copyright notice and this permission notice shall be
// * included in all copies or substantial portions of the Software.
// *
// * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
// * CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ******************************************************************************/
//
//
// #endif

}
