namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /* global table */
    public const string LUA_GNAME = "_G";

    /* extra error code for 'luaL_loadfilex' */
    public const byte LUA_ERRFILE = LUA_ERRERR + 1;
    
    /* key, in the registry, for table of loaded modules */
    public const string LUA_LOADED_TABLE = "_LOADED";
    
    /* key, in the registry, for table of preloaded loaders */
    public const string LUA_PRELOAD_TABLE = "_PRELOAD";

    public struct luaL_Reg(string name, lua_CFunction func)
    {
        public string name = name;
        public lua_CFunction func = func;
    }

    public const int LUAL_NUMSIZES = sizeof(long) * 16 + sizeof(double);

    public static partial void luaL_checkversion(lua_State* L, int version, int sizes);

    public static partial int luaL_getmetafield(lua_State* L, int obj, string e);

    public static partial bool luaL_callmeta(lua_State* L, int obj, string e);

    public static partial byte* luaL_tolstring(lua_State* L, int idx, out long len);
    
    public static partial string luaL_tonetstring(lua_State* L, int idx);

    public static partial int luaL_argerror(lua_State* L, int arg, string extramsg);

    public static partial int luaL_typeerror(lua_State* L, int arg, string tname);

    public static partial byte* luaL_checklstring(lua_State* L, int arg, long* l);

    public static partial byte* luaL_optlstring(lua_State* L, int arg, string def, long* l);

    public static partial double luaL_checknumber(lua_State* L, int arg);

    public static partial double luaL_optnumber(lua_State* L, int arg, double def);

    public static partial long luaL_checkinteger(lua_State* L, int arg);

    public static partial long luaL_optinteger(lua_State* L, int arg, long def);

    public static partial void luaL_checkstack(lua_State* L, int sz, string msg);

    public static partial void luaL_checktype(lua_State* L, int arg, int t);

    public static partial void luaL_checkany(lua_State* L, int arg);

    public static partial int luaL_newmetatable(lua_State* L, string tname);

    public static partial void luaL_setmetatable(lua_State* L, string tname);

    public static partial void* luaL_testudata(lua_State* L, int ud, string tname);

    public static partial void* luaL_checkudata(lua_State* L, int ud, string tname);

    public static partial void luaL_where(lua_State* L, int lvl);

    public static partial int luaL_error(lua_State* L, string fmt, params object[] args);

    public static partial int luaL_checkoption(lua_State* L, int arg, string def, string[] lst);

    public static partial int luaL_fileresult(lua_State* L, int stat, string fname);

    public static partial int luaL_execresult(lua_State* L, int stat);

    public static partial void* luaL_alloc(void* ud, void* ptr, long osize, long nsize);
    
    /* predefined references */
    public const int LUA_NOREF = -2;
    public const int LUA_REFNIL = -1;

    public static partial int luaL_ref(lua_State* L, int t);

    public static partial void luaL_unref(lua_State* L, int t, int @ref);

    public static partial int luaL_loadfilex(lua_State* L, string? filename, string? mode);

    public static int luaL_loadfile(lua_State* L, string? f)
    {
        return luaL_loadfilex(L, f, null);
    }

    public static partial int luaL_loadbufferx(lua_State* L, ReadOnlySpan<byte> buff, string? name, string? mode);

    public static partial int luaL_loadstring(lua_State* L, string s);
    
    public static partial lua_State* luaL_newstate();

    public static partial uint luaL_makeseed(lua_State* L);

    public static partial long luaL_len(lua_State* L, int idx);
    
    // public static partial void luaL_addgsub (luaL_Buffer *b, const char *s, const char *p, const char *r); TODO
    
    // LUALIB_API const char *(luaL_gsub) (lua_State *L, const char *s, TODO
    //                                     const char *p, const char *r);

    public static partial void luaL_setfuncs(lua_State* L, ReadOnlySpan<luaL_Reg> l, int nup);

    public static partial bool luaL_getsubtable(lua_State* L, int idx, string fname);

    public static partial void luaL_traceback(lua_State* L, lua_State* L1, string msg, int level);

    public static partial void luaL_requiref(lua_State* L, string modname, lua_CFunction openf, bool glb);

    /*
    ** ===============================================================
    ** some useful macros
    ** ===============================================================
    */

    public static void luaL_newlibtable<T>(lua_State* L, ReadOnlySpan<T> l)
    {
        lua_createtable(L, 0, l.Length);
    }

    public static void luaL_newlib(lua_State* L, ReadOnlySpan<luaL_Reg> l)
    {
        luaL_checkversion(L, LUA_VERSION_NUM, LUAL_NUMSIZES);
        luaL_newlibtable(L, l);
        luaL_setfuncs(L, l, 0);
    }

    public static void luaL_argcheck(lua_State* L, bool cond, int arg, string extramsg)
    {
        if (cond)
        {
            luaL_argerror(L, arg, extramsg);
        }
    }

    public static void luaL_argexpected(lua_State* L, bool cond, int arg, string tname)
    {
        if (cond)
        {
            luaL_typeerror(L, arg, tname);
        }
    }

    public static byte* luaL_checkstring(lua_State* L, int n)
    {
        return luaL_checklstring(L, n, null);
    }

    public static byte* luaL_optstring(lua_State* L, int n, string d)
    {
        return luaL_optlstring(L, n, d, null);
    }

    public static string luaL_typename(lua_State* L, int i)
    {
        return lua_typename(L, lua_type(L, i));
    }

    public static int luaL_dofile(lua_State* L, string? fn)
    {
        int result = luaL_loadfile(L, fn);
        if (result == LUA_OK)
        {
            result = lua_pcall(L, 0, LUA_MULTRET, 0);
        }

        return result;
    }

    public static int luaL_dostring(lua_State* L, string s)
    {
        int result = luaL_loadstring(L, s);
        if (result == LUA_OK)
        {
            result = lua_pcall(L, 0, LUA_MULTRET, 0);
        }

        return result;
    }

    public static int luaL_getmetatable(lua_State* L, string n)
    {
        return lua_getfield(L, LUA_REGISTRYINDEX, n);
    }

    // private static T luaL_opt<T>(lua_State* L, Func<int, T> f, int n, T d) TODO
    // {
    //     return lua_isnoneornil(L, n) ? d : f(L, n);
    // }

    public static int luaL_loadbuffer(lua_State* L, ReadOnlySpan<byte> s, string? n)
    {
        return luaL_loadbufferx(L, s, n, null);
    }

    // /*
    // ** Perform arithmetic operations on long values with wrap-around
    // ** semantics, as the Lua core does.
    // */
    // public static void luaL_intop(void op, void v1, void v2) TODO
    // {
    //     return (long)(ulong)(v1)op(lua_Unsigned)(v2);
    // }
    
    /* push the value used to represent failure/error */
    public static void luaL_pushfail(lua_State* L)
    {
#if LUA_FAILISFALSE
        lua_pushboolean(L, false);
#else
        lua_pushnil(L);
#endif
    }
    
    /*
    ** {======================================================
    ** Generic Buffer manipulation
    ** =======================================================
    */
    
    // struct luaL_Buffer { TODO
    //   char *b;  /* buffer address */
    //   size_t size;  /* buffer size */
    //   size_t n;  /* number of characters in buffer */
    //   lua_State *L;
    //   union {
    //     LUAI_MAXALIGN;  /* ensure maximum alignment for buffer */
    //     char b[LUAL_BUFFERSIZE];  /* initial buffer */
    //   } init;
    // };
    //
    //
    // #define luaL_bufflen(bf)	((bf)->n) TODO
    // #define luaL_buffaddr(bf)	((bf)->b) TODO
    //
    //
    // #define luaL_addchar(B,c) \ TODO
    //   ((void)((B)->n < (B)->size || luaL_prepbuffsize((B), 1)), \
    //    ((B)->b[(B)->n++] = (c)))
    //
    // #define luaL_addsize(B,s)	((B)->n += (s)) TODO
    //
    // #define luaL_buffsub(B,s)	((B)->n -= (s)) TODO
    //
    // LUALIB_API void (luaL_buffinit) (lua_State *L, luaL_Buffer *B); TODO
    // LUALIB_API char *(luaL_prepbuffsize) (luaL_Buffer *B, size_t sz); TODO
    // LUALIB_API void (luaL_addlstring) (luaL_Buffer *B, const char *s, size_t l); TODO
    // LUALIB_API void (luaL_addstring) (luaL_Buffer *B, const char *s); TODO
    // LUALIB_API void (luaL_addvalue) (luaL_Buffer *B); TODO
    // LUALIB_API void (luaL_pushresult) (luaL_Buffer *B); TODO
    // LUALIB_API void (luaL_pushresultsize) (luaL_Buffer *B, size_t sz); TODO
    // LUALIB_API char *(luaL_buffinitsize) (lua_State *L, luaL_Buffer *B, size_t sz); TODO
    //
    // #define luaL_prepbuffer(B)	luaL_prepbuffsize(B, LUAL_BUFFERSIZE) TODO
    
    /* }====================================================== */
    
    /*
    ** {======================================================
    ** File handles for IO library
    ** =======================================================
    */
    
    /*
    ** A file handle is a userdata with metatable 'LUA_FILEHANDLE' and
    ** initial structure 'luaL_Stream' (it may contain other fields
    ** after that initial structure).
    */

    public const string LUA_FILEHANDLE = "FILE*";

    private struct luaL_Stream
    {
        public nint f; /* stream (null for incompletely created streams) */
        public lua_CFunction closef; /* to close stream (null for closed streams) */
    }

    /* }====================================================== */
    
    // /* TODO
    // ** {============================================================
    // ** Compatibility with deprecated conversions
    // ** =============================================================
    // */
    // #if defined(LUA_COMPAT_APIINTCASTS)
    //
    // #define luaL_checkunsigned(L,a)	((lua_Unsigned)luaL_checkinteger(L,a))
    // #define luaL_optunsigned(L,a,d)	\
    // 	((lua_Unsigned)luaL_optinteger(L,a,(long)(d)))
    //
    // #define luaL_checkint(L,n)	((int)luaL_checkinteger(L, (n)))
    // #define luaL_optint(L,n,d)	((int)luaL_optinteger(L, (n), (d)))
    //
    // #define luaL_checklong(L,n)	((long)luaL_checkinteger(L, (n)))
    // #define luaL_optlong(L,n,d)	((long)luaL_optinteger(L, (n), (d)))
    //
    // #endif
}
