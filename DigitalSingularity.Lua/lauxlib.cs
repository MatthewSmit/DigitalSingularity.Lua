namespace DigitalSingularity.Lua;

using System.Text;

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
    
    /* predefined references */
    public const int LUA_NOREF = -2;
    public const int LUA_REFNIL = -1;

    public static int luaL_loadfile(lua_State* L, string? f)
    {
        return luaL_loadfilex(L, f, null);
    }

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
        if (!cond)
        {
            luaL_argerror(L, arg, extramsg);
        }
    }

    public static void luaL_argexpected(lua_State* L, bool cond, int arg, string tname)
    {
        if (!cond)
        {
            luaL_typeerror(L, arg, tname);
        }
    }

    public static ReadOnlySpan<byte> luaL_checkstring(lua_State* L, int n)
    {
        return luaL_checklstring(L, n);
    }

    public static ReadOnlySpan<byte> luaL_optstring(lua_State* L, int n)
    {
        return luaL_optlstring(L, n);
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

    public struct luaL_Buffer
    {
        public byte* b; /* buffer address */
        public long size; /* buffer size */
        public long n; /* number of characters in buffer */
        public lua_State* L;
        public fixed byte init[LUAL_BUFFERSIZE]; /* initial buffer */
    }

    public static long luaL_bufflen(luaL_Buffer* bf)
    {
        return bf->n;
    }

    public static byte* luaL_buffaddr(luaL_Buffer* bf)
    {
        return bf->b;
    }

    public static void luaL_addchar(luaL_Buffer* B, byte c)
    {
        if (B->n < B->size || luaL_prepbuffsize(B, 1) != null)
        {
            B->b[B->n++] = c;
        }
    }

    public static void luaL_addchar(luaL_Buffer* B, char c)
    {
        Rune rune = (Rune)c;
        if (rune.IsAscii)
        {
            luaL_addchar(B, (byte)rune.Value);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public static void luaL_addsize(luaL_Buffer* B, long s)
    {
        B->n += s;
    }

    public static void luaL_buffsub(luaL_Buffer* B, long s)
    {
        B->n -= s;
    }

    public static byte* luaL_prepbuffer(luaL_Buffer* B)
    {
        return luaL_prepbuffsize(B, LUAL_BUFFERSIZE);
    }
    
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
        public void* f; /* stream (null for incompletely created streams) */
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
