namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    /// <summary>
    /// Global Table
    /// </summary>
    public const string LUA_GNAME = "_G";

    /// <summary>
    /// Extra error code for 'luaL_loadfilex'
    /// </summary>
    public const byte LUA_ERRFILE = LUA_ERRERR + 1;
    
    /// <summary>
    /// key, in the registry, for table of loaded modules
    /// </summary>
    public const string LUA_LOADED_TABLE = "_LOADED";
    
    /// <summary>
    /// key, in the registry, for table of preloaded loaders
    /// </summary>
    public const string LUA_PRELOAD_TABLE = "_PRELOAD";

    public struct luaL_Reg(string name, lua_CFunction func)
    {
        public string name = name;
        public lua_CFunction func = func;
    }

    public const int LUAL_NUMSIZES = sizeof(long) * 16 + sizeof(double);
    
    /// <summary>
    /// predefined references
    /// </summary>
    public const int LUA_NOREF = -2;
    public const int LUA_REFNIL = -1;

    public static int luaL_loadfile(lua_State* L, string? f)
    {
        return luaL_loadfilex(L, f, null);
    }

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
    // return lua_isnoneornil(L, n) ? d : f(L, n);
    // }

    public static int luaL_loadbuffer(lua_State* L, ReadOnlySpan<byte> s, string? n)
    {
        return luaL_loadbufferx(L, s, n, null);
    }
    
    /// <summary>
    /// push the value used to represent failure/error
    /// </summary>
    public static void luaL_pushfail(lua_State* L)
    {
#if LUA_FAILISFALSE
        lua_pushboolean(L, false);
#else
        lua_pushnil(L);
#endif
    }
    
    // {======================================================
    // Generic Buffer manipulation
    // =======================================================

    public struct luaL_Buffer
    {
        public byte* b; // buffer address
        public long size; // buffer size
        public long n; // number of characters in buffer
        public lua_State* L;
        public fixed byte init[LUAL_BUFFERSIZE]; // initial buffer
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
    
    // {======================================================
    // File handles for IO library
    // =======================================================
    
    // A file handle is a userdata with metatable 'LUA_FILEHANDLE' and
    // initial structure 'luaL_Stream' (it may contain other fields
    // after that initial structure).

    public const string LUA_FILEHANDLE = "FILE*";

    private struct luaL_Stream
    {
        public void* f; // stream (null for incompletely created streams)
        public lua_CFunction closef; // to close stream (null for closed streams)
    }

    // }======================================================
    
    // TODO
    // {============================================================
    // Compatibility with deprecated conversions
    // =============================================================
    //
    // #if defined(LUA_COMPAT_APIINTCASTS)
    //
    // #define luaL_checkunsigned(L,a)	((lua_Unsigned)luaL_checkinteger(L,a))
    // #define luaL_optunsigned(L,a,d)	\
    // ((lua_Unsigned)luaL_optinteger(L,a,(long)(d)))
    //
    // #define luaL_checkint(L,n)	((int)luaL_checkinteger(L, (n)))
    // #define luaL_optint(L,n,d)	((int)luaL_optinteger(L, (n), (d)))
    //
    // #define luaL_checklong(L,n)	((long)luaL_checkinteger(L, (n)))
    // #define luaL_optlong(L,n,d)	((long)luaL_optinteger(L, (n), (d)))
    //
    // #endif
    
    // {======================================================
    // Traceback
    // =======================================================

    private const int LEVELS1 = 10; // size of the first part of the stack
    private const int LEVELS2 = 11; // size of the second part of the stack

    /// <summary>
    /// Search for 'objidx' in table at index -1. ('objidx' must be an
    /// absolute index.) Return 1 + string at top if it found a good name.
    /// </summary>
    private static bool findfield(lua_State* L, int objidx, int level)
    {
        if (level == 0 || !lua_istable(L, -1))
        {
            return false; // not found
        }

        lua_pushnil(L); // start 'next' loop
        while (lua_next(L, -2))
        {
            // for each pair in table
            if (lua_type(L, -2) == LUA_TSTRING)
            {
                // ignore non-string keys
                if (lua_rawequal(L, objidx, -1))
                {
                    // found object?
                    lua_pop(L, 1); // remove value (but keep name)
                    return true;
                }

                if (findfield(L, objidx, level - 1))
                {
                    // try recursively
                    // stack: lib_name, lib_table, field_name (top)
                    lua_pushliteral(L, "."); // place '.' between the two names
                    lua_replace(L, -3); // (in the slot occupied by table)
                    lua_concat(L, 3); // lib_name.field_name
                    return true;
                }
            }

            lua_pop(L, 1); // remove value
        }

        return false; // not found
    }

    /// <summary>
    /// Search for a name for a function in all loaded modules
    /// </summary>
    private static bool pushglobalfuncname(lua_State* L, ref lua_Debug ar)
    {
        int top = lua_gettop(L);
        lua_getinfo(L, "f", ref ar); // push function
        lua_getfield(L, LUA_REGISTRYINDEX, LUA_LOADED_TABLE);
        luaL_checkstack(L, 6, "not enough stack"); // slots for 'findfield'
        if (findfield(L, top + 1, 2))
        {
            string? name = lua_tonetstring(L, -1);
            if (name != null && name.StartsWith(LUA_GNAME + ".", StringComparison.InvariantCulture))
            {
                // name start with '_G.'?
                lua_pushstring(L, name[3..]); // push name without prefix
                lua_remove(L, -2); // remove original name
            }

            lua_copy(L, -1, top + 1); // copy name to proper place
            lua_settop(L, top + 1); // remove table "loaded" and name copy
            return true;
        }

        lua_settop(L, top); // remove function and global table
        return false;
    }

    private static void pushfuncname(lua_State* L, ref lua_Debug ar)
    {
        if (ar.namewhat?.Length > 0) // is there a name from code?
        {
            lua_pushfstring(L, "%s '%s'", ar.namewhat, ar.name ?? ""); // use it
        }
        else if (ar.what[0] == 'm')
        {
            // main?
            lua_pushliteral(L, "main chunk");
        }
        else if (pushglobalfuncname(L, ref ar))
        {
            // try a global name
            lua_pushfstring(L, "function '%s'", lua_tonetstring(L, -1));
            lua_remove(L, -2); // remove name
        }
        else if (ar.what[0] != 'C') // for Lua functions, use <file:line>
        {
            lua_pushfstring(L, "function <%s:%d>", ar.short_src, ar.linedefined);
        }
        else
        {
            // nothing left...
            lua_pushliteral(L, "?");
        }
    }

    private static int lastlevel(lua_State* L)
    {
        lua_Debug ar = new();
        int li = 1;
        int le = 1;
        
        // find an upper bound
        while (lua_getstack(L, le, ref ar))
        {
            li = le;
            le *= 2;
        }

        // do a binary search
        while (li < le)
        {
            int m = (li + le) / 2;
            if (lua_getstack(L, m, ref ar))
            {
                li = m + 1;
            }
            else
            {
                le = m;
            }
        }

        return le - 1;
    }

    public static void luaL_traceback(lua_State* L, lua_State* L1, string? msg, int level)
    {
        int last = lastlevel(L1);
        int limit2show = last - level > LEVELS1 + LEVELS2 ? LEVELS1 : -1;

        luaL_Buffer b;
        luaL_buffinit(L, &b);
        if (msg != null)
        {
            luaL_addstring(&b, msg);
            luaL_addchar(&b, '\n');
        }

        lua_Debug ar = new();

        luaL_addstring(&b, "stack traceback:");
        while (lua_getstack(L1, level++, ref ar))
        {
            if (limit2show-- == 0)
            {
                // too many levels?
                int n = last - level - LEVELS2 + 1; // number of levels to skip
                lua_pushfstring(L, "\n\t...\t(skipping %d levels)", n);
                luaL_addvalue(&b); // add warning about skip
                level += n; // and skip to last levels
            }
            else
            {
                lua_getinfo(L1, "Slnt", ref ar);
                if (ar.currentline <= 0)
                {
                    lua_pushfstring(L, "\n\t%s: in ", ar.short_src);
                }
                else
                {
                    lua_pushfstring(L, "\n\t%s:%d: in ", ar.short_src, ar.currentline);
                }

                luaL_addvalue(&b);
                pushfuncname(L, ref ar);
                luaL_addvalue(&b);
                if (ar.istailcall)
                {
                    luaL_addstring(&b, "\n\t(...tail calls...)");
                }
            }
        }

        luaL_pushresult(&b);
    }

    // {======================================================
    // Error-report functions
    // =======================================================

    public static int luaL_argerror(lua_State* L, int arg, string extramsg)
    {
        lua_Debug ar = new();
        if (!lua_getstack(L, 0, ref ar)) // no stack frame?
        {
            return luaL_error(L, "bad argument #%d (%s)", arg, extramsg);
        }

        lua_getinfo(L, "nt", ref ar);
        string argword;
        if (arg <= ar.extraargs) // error in an extra argument?
        {
            argword = "extra argument";
        }
        else
        {
            arg -= ar.extraargs; // do not count extra arguments
            if (ar.namewhat == "method")
            {
                // colon syntax?
                arg--; // do not count (extra) self argument
                if (arg == 0) // error in self argument?
                {
                    return luaL_error(
                        L,
                        "calling '%s' on bad self (%s)",
                        ar.name ?? "",
                        extramsg);
                }
                // else go through; error in a regular argument
            }

            argword = "argument";
        }

        ar.name ??= pushglobalfuncname(L, ref ar) ? lua_tonetstring(L, -1) : "?";

        return luaL_error(
            L,
            "bad %s #%d to '%s' (%s)",
            argword,
            arg,
            ar.name,
            extramsg);
    }

    public static int luaL_typeerror(lua_State* L, int arg, string tname)
    {
        string typearg; // name for the type of the actual argument
        if (luaL_getmetafield(L, arg, "__name") == LUA_TSTRING)
        {
            typearg = lua_tonetstring(L, -1); // use the given type name
        }
        else if (lua_type(L, arg) == LUA_TLIGHTUSERDATA)
        {
            typearg = "light userdata"; // special name for messages
        }
        else
        {
            typearg = luaL_typename(L, arg); // standard name
        }

        string msg = lua_pushfstring(L, "%s expected, got %s", tname, typearg);
        return luaL_argerror(L, arg, msg);
    }

    [DoesNotReturn]
    private static void tag_error(lua_State* L, int arg, int tag)
    {
        luaL_typeerror(L, arg, lua_typename(L, tag));
    }

    /// <summary>
    /// The use of 'lua_pushfstring' ensures this function does not
    /// need reserved stack space when called.
    /// </summary>
    public static void luaL_where(lua_State* L, int level)
    {
        lua_Debug ar = new();
        if (lua_getstack(L, level, ref ar))
        {
            // check function at level
            lua_getinfo(L, "Sl", ref ar); // get info about it
            if (ar.currentline > 0)
            {
                // is there info?
                lua_pushfstring(L, "%s:%d: ", ar.short_src, ar.currentline);
                return;
            }
        }

        lua_pushfstring(L, ""); // else, no information available...
    }

    /// <summary>
    /// Again, the use of 'lua_pushvfstring' ensures this function does
    /// not need reserved stack space when called. (At worst, it generates
    /// a memory error instead of the given message.)
    /// </summary>
    [DoesNotReturn]
    public static int luaL_error(lua_State* L, string fmt, params object[] args)
    {
        luaL_where(L, 1);
        lua_pushfstring(L, fmt, args);
        lua_concat(L, 2);
        return lua_error(L);
    }

    public static int luaL_fileresult(lua_State* L, bool success, string? fname, Exception? e)
    {
        if (success)
        {
            lua_pushboolean(L, true);
            return 1;
        }

        luaL_pushfail(L);
        string msg = e?.Message ?? "(no extra info)";
        if (fname != null)
        {
            lua_pushfstring(L, "%s: %s", fname, msg);
        }
        else
        {
            lua_pushstring(L, msg);
        }

        lua_pushinteger(L, e?.HResult ?? 0);
        return 3;
    }

    public static int luaL_execresult(lua_State* L, int stat, Exception? e = null)
    {
        if (stat != 0 && e is IOException io) // error with an 'errno'?
        {
            return luaL_fileresult(L, false, null, io);
        }

        string what = e?.Message ?? "exit"; // type of termination
        if (what == "exit" && stat == 0) // successful termination?
        {
            lua_pushboolean(L, true);
        }
        else
        {
            luaL_pushfail(L);
        }

        lua_pushstring(L, what);
        lua_pushinteger(L, stat);
        return 3; // return true/fail,what,code
    }

    // }======================================================

    // {======================================================
    // Userdata's metatable manipulation
    // =======================================================

    public static bool luaL_newmetatable(lua_State* L, string tname)
    {
        if (luaL_getmetatable(L, tname) != LUA_TNIL) // name already in use?
        {
            return false; // leave previous value on top, but return 0
        }

        lua_pop(L, 1);
        lua_createtable(L, 0, 2); // create metatable
        lua_pushstring(L, tname);
        lua_setfield(L, -2, "__name"); // metatable.__name = tname
        lua_pushvalue(L, -1);
        lua_setfield(L, LUA_REGISTRYINDEX, tname); // registry.name = metatable
        return true;
    }

    public static void luaL_setmetatable(lua_State* L, string tname)
    {
        luaL_getmetatable(L, tname);
        lua_setmetatable(L, -2);
    }

    public static void* luaL_testudata(lua_State* L, int ud, string tname)
    {
        void* p = lua_touserdata(L, ud);
        if (p != null)
        {
            // value is a userdata?
            if (lua_getmetatable(L, ud))
            {
                // does it have a metatable?
                luaL_getmetatable(L, tname); // get correct metatable
                if (!lua_rawequal(L, -1, -2)) // not the same?
                {
                    p = null; // value is a userdata with wrong metatable
                }

                lua_pop(L, 2); // remove both metatables
                return p;
            }
        }

        return null; // value is not a userdata with a metatable
    }

    public static void* luaL_checkudata(lua_State* L, int ud, string tname)
    {
        void* p = luaL_testudata(L, ud, tname);
        luaL_argexpected(L, p != null, ud, tname);
        return p;
    }
    
    // }======================================================
    
    // {======================================================
    // Argument check functions
    // =======================================================

    public static int luaL_checkoption(lua_State* L, int arg, string? def, string[] lst)
    {
        string name = def != null ? luaL_optnetstring(L, arg, def) : luaL_checknetstring(L, arg);
        for (int i = 0; i < lst.Length; i++)
        {
            if (string.Equals(lst[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return luaL_argerror(
            L,
            arg,
            lua_pushfstring(L, "invalid option '%s'", name));
    }

    /// <summary>
    /// Ensures the stack has at least 'space' extra slots, raising an error
    /// if it cannot fulfill the request. (The error handling needs a few
    /// extra slots to format the error message. In case of an error without
    /// this extra space, Lua will generate the same 'stack overflow' error,
    /// but without 'msg'.)
    /// </summary>
    public static void luaL_checkstack(lua_State* L, int sz, string msg)
    {
        if (!lua_checkstack(L, sz))
        {
            if (!string.IsNullOrEmpty(msg))
            {
                luaL_error(L, "stack overflow (%s)", msg);
            }
            else
            {
                luaL_error(L, "stack overflow");
            }
        }
    }

    public static void luaL_checktype(lua_State* L, int arg, int t)
    {
        if (lua_type(L, arg) != t)
        {
            tag_error(L, arg, t);
        }
    }

    public static void luaL_checkany(lua_State* L, int arg)
    {
        if (lua_type(L, arg) == LUA_TNONE)
        {
            luaL_argerror(L, arg, "value expected");
        }
    }
    
    public static ReadOnlySpan<byte> luaL_checklstring(lua_State* L, int arg)
    {
        byte* s = lua_tolstring(L, arg, out int length);
        if (s == null)
        {
            tag_error(L, arg, LUA_TSTRING);
        }

        return new ReadOnlySpan<byte>(s, length);
    }
    
    public static byte* luaL_checklstring(lua_State* L, int arg, out int length)
    {
        byte* s = lua_tolstring(L, arg, out length);
        if (s == null)
        {
            tag_error(L, arg, LUA_TSTRING);
        }

        return s;
    }

    public static string luaL_checknetstring(lua_State* L, int arg)
    {
        string? s = lua_tonetstring(L, arg);
        if (s == null!)
        {
            tag_error(L, arg, LUA_TSTRING);
        }

        return s;
    }

    public static ReadOnlySpan<byte> luaL_optlstring(lua_State* L, int arg, ReadOnlySpan<byte> def = default)
    {
        if (lua_isnoneornil(L, arg))
        {
            return def;
        }

        return luaL_checklstring(L, arg);
    }

    [return: NotNullIfNotNull(nameof(def))]
    public static string? luaL_optnetstring(lua_State* L, int arg, string? def)
    {
        if (lua_isnoneornil(L, arg))
        {
            return def;
        }

        return luaL_checknetstring(L, arg);
    }

    public static double luaL_checknumber(lua_State* L, int arg)
    {
        double d = lua_tonumberx(L, arg, out bool isnum);
        if (!isnum)
        {
            tag_error(L, arg, LUA_TNUMBER);
        }

        return d;
    }

    public static double luaL_optnumber(lua_State* L, int arg, double def)
    {
        return lua_isnoneornil(L, arg) ? def : luaL_checknumber(L, arg);
    }

    private static void interror(lua_State* L, int arg)
    {
        if (lua_isnumber(L, arg))
        {
            luaL_argerror(L, arg, "number has no integer representation");
        }
        else
        {
            tag_error(L, arg, LUA_TNUMBER);
        }
    }

    public static long luaL_checkinteger(lua_State* L, int arg)
    {
        long d = lua_tointegerx(L, arg, out bool isnum);
        if (!isnum)
        {
            interror(L, arg);
        }

        return d;
    }

    public static long luaL_optinteger(lua_State* L, int arg, long def)
    {
        return lua_isnoneornil(L, arg) ? def : luaL_checkinteger(L, arg);
    }
    
    // }======================================================

    // {======================================================
    // Generic Buffer manipulation
    // =======================================================

    /// <summary>
    /// userdata to box arbitrary data
    /// </summary>
    private struct UBox
    {
        public void* box;
        public long bsize;
    }

    /// <summary>
    /// Resize the buffer used by a box. Optimise for the common case of
    /// resizing to the old size. (For instance, __gc will resize the box
    /// to 0 even after it was closed. 'pushresult' may also resize it to a
    /// final size that is equal to the one set when the buffer was created.)
    /// </summary>
    private static void* resizebox(lua_State* L, int idx, long newsize)
    {
        UBox* box = (UBox*)lua_touserdata(L, idx);
        if (box->bsize == newsize) // not changing size?
        {
            return box->box; // keep the buffer
        }

        lua_Alloc allocf = lua_getallocf(L, out void* ud);
        void* temp = allocf(ud, box->box, box->bsize, newsize);
        if (temp == null && newsize > 0)
        {
            // allocation error?
            lua_pushliteral(L, "not enough memory");
            lua_error(L); // raise a memory error
        }

        box->box = temp;
        box->bsize = newsize;
        return temp;
    }

    private static int boxgc(lua_State* L)
    {
        resizebox(L, 1, 0);
        return 0;
    }

    private static readonly luaL_Reg[] boxmt =
    [
        // box metamethods
        new("__gc", &boxgc),
        new("__close", &boxgc),
    ];

    private static void newbox(lua_State* L)
    {
        UBox* box = (UBox*)lua_newuserdatauv(L, sizeof(UBox), 0);
        box->box = null;
        box->bsize = 0;
        if (luaL_newmetatable(L, "_UBOX*")) // creating metatable?
        {
            luaL_setfuncs(L, boxmt, 0); // set its metamethods
        }

        lua_setmetatable(L, -2);
    }

    /// <summary>
    /// check whether buffer is using a userdata on the stack as a temporary
    /// buffer
    /// </summary>
    private static bool buffonstack(luaL_Buffer* B)
    {
        return B->b != B->init;
    }

    /// <summary>
    /// Whenever buffer is accessed, slot 'idx' must either be a box (which
    /// cannot be null) or it is a placeholder for the buffer.
    /// </summary>
    private static void checkbufferlevel(luaL_Buffer* B, int idx)
    {
        Debug.Assert(buffonstack(B) ? lua_touserdata(B->L, idx) != null : lua_touserdata(B->L, idx) == B);
    }
    
    /// <summary>
    /// Compute new size for buffer 'B', enough to accommodate extra 'sz'
    /// bytes plus one for a terminating zero.
    /// </summary>
    private static long newbuffsize(luaL_Buffer* B, long sz)
    {
        long newsize = B->size;
        if (sz >= long.MaxValue - B->n)
        {
            return luaL_error(B->L, "resulting string too large");
        }

        // else  B->n + sz + 1 <= MAX_SIZE
        if (newsize <= long.MaxValue / 3 * 2) // no overflow?
        {
            newsize += newsize >> 1; // new size *= 1.5
        }

        if (newsize < B->n + sz + 1) // not big enough?
        {
            newsize = B->n + sz + 1;
        }

        return newsize;
    }

    /// <summary>
    /// Returns a pointer to a free area with at least 'sz' bytes in buffer
    /// 'B'. 'boxidx' is the relative position in the stack where is the
    /// buffer's box or its placeholder.
    /// </summary>
    private static byte* prepbuffsize(luaL_Buffer* B, long sz, int boxidx)
    {
        checkbufferlevel(B, boxidx);
        if (B->size - B->n >= sz) // enough space?
        {
            return B->b + B->n;
        }

        lua_State* L = B->L;
        long newsize = newbuffsize(B, sz);

        // create larger buffer
        byte* newbuff;
        if (buffonstack(B)) // buffer already has a box?
        {
            newbuff = (byte*)resizebox(L, boxidx, newsize); // resize it
        }
        else
        {
            // no box yet
            lua_remove(L, boxidx); // remove placeholder
            newbox(L); // create a new box
            lua_insert(L, boxidx); // move box to its intended position
            lua_toclose(L, boxidx);
            newbuff = (byte*)resizebox(L, boxidx, newsize);
            memcpy(newbuff, B->b, B->n); // copy original content
        }

        B->b = newbuff;
        B->size = newsize;
        return newbuff + B->n;
    }

    /// <summary>
    /// returns a pointer to a free area with at least 'sz' bytes
    /// </summary>
    public static byte* luaL_prepbuffsize(luaL_Buffer* B, long sz)
    {
        return prepbuffsize(B, sz, -1);
    }

    public static void luaL_addlstring(luaL_Buffer* B, ReadOnlySpan<byte> s)
    {
        if (s.Length > 0)
        {
            // avoid 'memcpy' when 's' can be null
            byte* b = prepbuffsize(B, s.Length, -1);
            s.CopyTo(new Span<byte>(b, s.Length));
            luaL_addsize(B, s.Length);
        }
    }

    [Obsolete]
    public static void luaL_addlstring(luaL_Buffer* B, ReadOnlySpan<char> s)
    {
        if (s.Length > 0)
        {
            // avoid 'memcpy' when 's' can be null
            int length = Encoding.UTF8.GetByteCount(s);
            byte[] data = new byte[length];
            Encoding.UTF8.GetBytes(s, data);
            byte* b = prepbuffsize(B, length, -1);
            data.CopyTo(new Span<byte>(b, length));
            luaL_addsize(B, length);
        }
    }

    public static void luaL_addstring(luaL_Buffer* B, ReadOnlySpan<char> s)
    {
        luaL_addlstring(B, s);
    }

    public static void luaL_addstring(luaL_Buffer* B, ReadOnlySpan<byte> s)
    {
        luaL_addlstring(B, s);
    }

    public static void luaL_pushresult(luaL_Buffer* B)
    {
        lua_State* L = B->L;
        checkbufferlevel(B, -1);
        if (!buffonstack(B)) // using static buffer?
        {
            Span<byte> s = new(B->b, checked((int)B->n));
            lua_pushlstring(L, s); // save result as regular string
        }
        else
        {
            // reuse buffer already allocated
            UBox* box = (UBox*)lua_touserdata(L, -1);
            lua_Alloc allocf = lua_getallocf(L, out void* ud); // function to free buffer
            long len = B->n; // final string length
            resizebox(L, -1, len + 1); // adjust box size to content size
            byte* s = (byte*)box->box; // final buffer address
            s[len] = 0; // add ending zero
            // clear box, as Lua will take control of the buffer
            box->bsize = 0;
            box->box = null;
            lua_pushexternalstring(L, s, checked((int)len), allocf, ud);
            lua_closeslot(L, -2); // close the box
            lua_gc(L, LUA_GCSTEP, len);
        }

        lua_remove(L, -2); // remove box or placeholder from the stack
    }

    public static void luaL_pushresultsize(luaL_Buffer* B, long sz)
    {
        luaL_addsize(B, sz);
        luaL_pushresult(B);
    }

    /// <summary>
    /// 'luaL_addvalue' is the only function in the Buffer system where the
    /// box (if existent) is not on the top of the stack. So, instead of
    /// calling 'luaL_addlstring', it replicates the code using -2 as the
    /// last argument to 'prepbuffsize', signalling that the box is (or will
    /// be) below the string being added to the buffer. (Box creation can
    /// trigger an emergency GC, so we should not remove the string from the
    /// stack before we have the space guaranteed.)
    /// </summary>
    public static void luaL_addvalue(luaL_Buffer* B)
    {
        lua_State* L = B->L;
        byte* s = lua_tolstring(L, -1, out int len);
        byte* b = prepbuffsize(B, len, -2);
        memcpy(b, s, len);
        luaL_addsize(B, len);
        lua_pop(L, 1); // pop string
    }

    public static void luaL_buffinit(lua_State* L, luaL_Buffer* B)
    {
        B->L = L;
        B->b = B->init;
        B->n = 0;
        B->size = LUAL_BUFFERSIZE;
        lua_pushlightuserdata(L, B); // push placeholder
    }

    public static byte* luaL_buffinitsize(lua_State* L, luaL_Buffer* B, long sz)
    {
        luaL_buffinit(L, B);
        return prepbuffsize(B, sz, -1);
    }

    // {======================================================
    // Reference system
    // =======================================================

    /// <summary>
    /// The previously freed references form a linked list: t[1] is the index
    /// of a first free index, t[t[1]] is the index of the second element,
    /// etc. A zero signals the end of the list.
    /// </summary>
    public static int luaL_ref(lua_State* L, int t)
    {
        if (lua_isnil(L, -1))
        {
            lua_pop(L, 1); // remove from stack
            return LUA_REFNIL; // 'nil' has a unique fixed reference
        }

        t = lua_absindex(L, t);

        int @ref;
        if (lua_rawgeti(L, t, 1) == LUA_TNUMBER) // already initialised?
        {
            @ref = (int)lua_tointeger(L, -1); // ref = t[1]
        }
        else
        {
            // first access
            Debug.Assert(!lua_toboolean(L, -1)); // must be nil or false
            @ref = 0; // list is empty
            lua_pushinteger(L, 0); // initialise as an empty list
            lua_rawseti(L, t, 1); // ref = t[1] = 0
        }

        lua_pop(L, 1); // remove element from stack
        if (@ref != 0)
        {
            // any free element?
            lua_rawgeti(L, t, @ref); // remove it from list
            lua_rawseti(L, t, 1); // (t[1] = t[ref])
        }
        else // no free elements
        {
            @ref = (int)lua_rawlen(L, t) + 1; // get a new reference
        }

        lua_rawseti(L, t, @ref);
        return @ref;
    }

    public static void luaL_unref(lua_State* L, int t, int @ref)
    {
        if (@ref >= 0)
        {
            t = lua_absindex(L, t);
            lua_rawgeti(L, t, 1);
            Debug.Assert(lua_isinteger(L, -1));
            lua_rawseti(L, t, @ref); // t[ref] = t[1]
            lua_pushinteger(L, @ref);
            lua_rawseti(L, t, 1); // t[1] = ref
        }
    }

    // }======================================================

    // {======================================================
    // Load functions
    // =======================================================

    private const int BUFF_SIZE = 512;

    private struct LoadF
    {
        public uint n; // number of pre-read characters
        public nint f; // file being read
        public fixed byte buff[BUFF_SIZE]; // area for reading file
    }

    private static byte* getF(lua_State* L, void* ud, out long size)
    {
        LoadF* lf = (LoadF*)ud;
        if (lf->n > 0)
        {
            // are there pre-read characters to be read?
            size = lf->n; // return them (chars already in buffer)
            lf->n = 0; // no more pre-read characters
        }
        else
        {
            // read a block from file
            // 'fread' can return > 0 *and* set the EOF flag. If next call to
            // 'getF' called 'fread', it might still wait for user input.
            // The next check avoids this problem.
            Stream f = GCHandle<Stream>.FromIntPtr(lf->f).Target;
            Span<byte> span = new(lf->buff, BUFF_SIZE);
            int tmp = f.Read(span);
            if (tmp <= 0)
            {
                size = 0;
                return null;
            }

            size = tmp;
        }

        return lf->buff;
    }

    private static int errfile(lua_State* L, string what, int fnameindex, Exception exception)
    {
        string filename = lua_tonetstring(L, fnameindex)[1..];
        lua_pushfstring(L, "cannot %s %s: %s", what, filename, exception.Message);
        lua_remove(L, fnameindex);
        return LUA_ERRFILE;
    }

    /// <summary>
    /// Skip an optional BOM at the start of a stream. If there is an
    /// incomplete BOM (the first character is correct but the rest is
    /// not), returns the first character anyway to force an error
    /// (as no chunk can start with 0xEF).
    /// </summary>
    private static int skipBOM(Stream f)
    {
        int c = f.ReadByte(); // read first character
        if (c == 0xEF && f.ReadByte() == 0xBB && f.ReadByte() == 0xBF) // correct BOM?
        {
            return f.ReadByte(); // ignore BOM and return next char
        }

        // no (valid) BOM
        return c; // return first character
    }

    /// <summary>
    /// reads the first character of file 'f' and skips an optional BOM mark
    /// in its beginning plus its first line if it starts with '#'. Returns
    /// true if it skipped the first line.  In any case, '*cp' has the
    /// first "valid" character of the file (after the optional BOM and
    /// a first-line comment).
    /// </summary>
    private static bool skipcomment(Stream f, int* cp)
    {
        int c = *cp = skipBOM(f);
        if (c == '#')
        {
            // first line is a comment (Unix exec. file)?
            do
            {
                // skip first line
                c = f.ReadByte();
            } while (c >= 0 && c != '\n');

            *cp = f.ReadByte(); // next character after comment, if present
            return true; // there was a comment
        }

        return false; // no comment
    }

    public static int luaL_loadfilex(lua_State* L, string? filename, string? mode)
    {
        LoadF lf;
        Stream f;
        int fnameindex = lua_gettop(L) + 1; // index of filename on the stack
        if (filename == null)
        {
            lua_pushliteral(L, "=stdin");
            f = new InputConsoleStreamWrapper(Console.In);
        }
        else
        {
            lua_pushfstring(L, "@%s", filename);
            try
            {
                f = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception e)
            {
                return errfile(L, "open", fnameindex, e);
            }
        }
        
        lf.f = (nint)new GCHandle<Stream>(f).ToPointer();

        lf.n = 0;
        int c;
        if (skipcomment(f, &c)) // read initial portion
        {
            lf.buff[lf.n++] = (byte)'\n'; // add newline to correct line numbers
        }

        if (c == LUA_SIGNATURE[0])
        {
            // binary file?
            lf.n = 0; // remove possible newline
        }

        if (c >= 0)
        {
            lf.buff[lf.n++] = (byte)c; // 'c' is the first character
        }

        int status;
        try
        {
            status = lua_load(L, &getF, &lf, lua_tonetstring(L, -1), mode);
        }
        catch (IOException e)
        {
            lua_settop(L, fnameindex); // ignore results from 'lua_load'
            return errfile(L, "read", fnameindex, e);
        }

        f.Close();
        GCHandle<Stream>.FromIntPtr(lf.f).Dispose();
        lua_remove(L, fnameindex);
        return status;
    }

    private struct LoadS
    {
        public byte* s;
        public int size;
    }

    private static byte* getS(lua_State* L, void* ud, out long size)
    {
        LoadS* ls = (LoadS*)ud;
        if (ls->size == 0)
        {
            size = 0;
            return null;
        }

        size = ls->size;
        ls->size = 0;
        return ls->s;
    }

    public static int luaL_loadbufferx(lua_State* L, ReadOnlySpan<byte> buff, string? name, string? mode)
    {
        fixed (byte* ptr = buff)
        {
            LoadS ls;
            ls.s = ptr;
            ls.size = buff.Length;
            return lua_load(L, &getS, &ls, name, mode);
        }
    }

    public static int luaL_loadstring(lua_State* L, string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        return luaL_loadbuffer(L, bytes, s);
    }

    // }======================================================

    public static int luaL_getmetafield(lua_State* L, int obj, string e)
    {
        if (!lua_getmetatable(L, obj)) // no metatable?
        {
            return LUA_TNIL;
        }

        lua_pushstring(L, e);
        int tt = lua_rawget(L, -2);
        if (tt == LUA_TNIL) // is metafield nil?
        {
            lua_pop(L, 2); // remove metatable and metafield
        }
        else
        {
            lua_remove(L, -2); // remove only metatable
        }

        return tt; // return metafield type
    }

    public static bool luaL_callmeta(lua_State* L, int obj, string @event)
    {
        obj = lua_absindex(L, obj);
        if (luaL_getmetafield(L, obj, @event) == LUA_TNIL) // no metafield?
        {
            return false;
        }

        lua_pushvalue(L, obj);
        lua_call(L, 1, 1);
        return true;
    }

    public static long luaL_len(lua_State* L, int idx)
    {
        lua_len(L, idx);
        long l = lua_tointegerx(L, -1, out bool isnum);
        if (!isnum)
        {
            luaL_error(L, "object length is not an integer");
        }

        lua_pop(L, 1); // remove object
        return l;
    }

    public static byte* luaL_tolstring(lua_State* L, int idx, out int len)
    {
        Span<byte> buff = stackalloc byte[LUA_N2SBUFFSZ];

        idx = lua_absindex(L, idx);
        if (luaL_callmeta(L, idx, "__tostring"))
        {
            // metafield?
            if (!lua_isstring(L, -1))
            {
                luaL_error(L, "'__tostring' must return a string");
            }
        }
        else
        {
            switch (lua_type(L, idx))
            {
                case LUA_TNUMBER:
                    lua_numbertocstring(L, idx, buff);
                    lua_pushstring(L, buff);
                    break;

                case LUA_TSTRING:
                    lua_pushvalue(L, idx);
                    break;

                case LUA_TBOOLEAN:
                    lua_pushstring(L, lua_toboolean(L, idx) ? "true" : "false");
                    break;

                case LUA_TNIL:
                    lua_pushliteral(L, "nil");
                    break;

                default:
                    {
                        int tt = luaL_getmetafield(L, idx, "__name"); // try name
                        string? kind = tt == LUA_TSTRING ? lua_tonetstring(L, -1) : luaL_typename(L, idx);
                        lua_pushfstring(L, "%s: %p", kind, (nint)lua_topointer(L, idx));
                        if (tt != LUA_TNIL)
                        {
                            lua_remove(L, -2); // remove '__name'
                        }

                        break;
                    }
            }
        }

        return lua_tolstring(L, -1, out len);
    }

    public static string? luaL_tonetstring(lua_State* L, int idx)
    {
        byte* ptr = luaL_tolstring(L, idx, out int len);
        if (ptr == null)
        {
            return null;
        }
        
        ReadOnlySpan<byte> span = new(ptr, len);
        return Encoding.UTF8.GetString(span);
    }

    /// <summary>
    /// set functions from list 'l' into table at top - 'nup'; each
    /// function gets the 'nup' elements at the top as upvalues.
    /// Returns with only the table at the stack.
    /// </summary>
    public static void luaL_setfuncs(lua_State* L, ReadOnlySpan<luaL_Reg> l, int nup)
    {
        luaL_checkstack(L, nup, "too many upvalues");
        for (; !l.IsEmpty; l = l[1..])
        {
            // fill the table with given functions
            if (l[0].func == null!) // placeholder?
            {
                lua_pushboolean(L, false);
            }
            else
            {
                for (int i = 0; i < nup; i++) // copy upvalues to the top
                {
                    lua_pushvalue(L, -nup);
                }

                lua_pushcclosure(L, l[0].func, nup); // closure with those upvalues
            }

            lua_setfield(L, -(nup + 2), l[0].name);
        }

        lua_pop(L, nup); // remove upvalues
    }

    /// <summary>
    /// ensure that stack[idx][fname] has a table and push that table
    /// into the stack
    /// </summary>
    public static bool luaL_getsubtable(lua_State* L, int idx, string fname)
    {
        if (lua_getfield(L, idx, fname) == LUA_TTABLE)
        {
            return true; // table already there
        }

        lua_pop(L, 1); // remove previous result
        idx = lua_absindex(L, idx);
        lua_newtable(L);
        lua_pushvalue(L, -1); // copy to be left at top
        lua_setfield(L, idx, fname); // assign new table to field
        return false; // false, because did not find table there
    }

    /// <summary>
    /// Stripped-down 'require': After checking "loaded" table, calls 'openf'
    /// to open a module, registers the result in 'package.loaded' table and,
    /// if 'glb' is true, also registers the result in the global table.
    /// Leaves resulting module on the top.
    /// </summary>
    public static void luaL_requiref(lua_State* L, string modname, lua_CFunction openf, bool glb)
    {
        luaL_getsubtable(L, LUA_REGISTRYINDEX, LUA_LOADED_TABLE);
        lua_getfield(L, -1, modname); // LOADED[modname]
        if (!lua_toboolean(L, -1))
        {
            // package not already loaded?
            lua_pop(L, 1); // remove field
            lua_pushcfunction(L, openf);
            lua_pushstring(L, modname); // argument to open function
            lua_call(L, 1, 1); // call 'openf' to open module
            lua_pushvalue(L, -1); // make copy of module (call result)
            lua_setfield(L, -3, modname); // LOADED[modname] = module
        }

        lua_remove(L, -2); // remove LOADED table
        if (glb)
        {
            lua_pushvalue(L, -1); // copy of module
            lua_setglobal(L, modname); // _G[modname] = module
        }
    }

    public static void luaL_addgsub(luaL_Buffer* b, string s, string p, string r)
    {
        ReadOnlySpan<char> ss = s;

        ReadOnlySpan<char> wild;
        while (!(wild = strstr(ss, p)).IsEmpty)
        {
            luaL_addlstring(b, ss[..^wild.Length]); // push prefix
            luaL_addstring(b, r); // push replacement in place of pattern
            ss = wild[p.Length..]; // continue after 'p'
        }

        luaL_addstring(b, ss); // push last suffix
    }

    public static string luaL_gsub(lua_State* L, string s, string p, string r)
    {
        luaL_Buffer b;
        luaL_buffinit(L, &b);
        luaL_addgsub(&b, s, p, r);
        luaL_pushresult(&b);
        return lua_tonetstring(L, -1);
    }

    public static void* luaL_alloc(void* ud, void* ptr, long osize, long nsize)
    {
        if (nsize == 0)
        {
            NativeMemory.Free(ptr);
            return null;
        }

        try
        {
            return NativeMemory.Realloc(ptr, (nuint)nsize);
        }
        catch (OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>
    /// Standard panic function just prints an error message. The test
    /// with 'lua_type' avoids possible memory errors in 'lua_tostring'.
    /// </summary>
    private static int panic(lua_State* L)
    {
        string? msg = lua_type(L, -1) == LUA_TSTRING
            ? lua_tonetstring(L, -1)
            : "error object is not a string";
        Console.WriteLine(
            "PANIC: unprotected error in call to Lua API ({0})",
            msg);
        return 0; // return to Lua to abort
    }

    /// <summary>
    /// Check whether message is a control message. If so, execute the
    /// control or ignore it if unknown.
    /// </summary>
    private static bool checkcontrol(lua_State* L, ReadOnlySpan<char> message, bool tocont)
    {
        if (tocont || !message.StartsWith('@')) // not a control message?
        {
            return false;
        }

        if (message is "@off")
        {
            lua_setwarnf(L, &warnfoff, L); // turn warnings off
        }
        else if (message is "@on")
        {
            lua_setwarnf(L, &warnfon, L); // turn warnings on
        }

        return true; // it was a control message
    }

    private static void warnfoff(void* ud, ReadOnlySpan<char> message, bool tocont)
    {
        checkcontrol((lua_State*)ud, message, tocont);
    }

    /// <summary>
    /// Writes the message and handle 'tocont', finishing the message
    /// if needed and setting the next warn function.
    /// </summary>
    private static void warnfcont(void* ud, ReadOnlySpan<char> message, bool tocont)
    {
        lua_State* L = (lua_State*)ud;
        Console.Error.Write(message); // write message
        if (tocont) // not the last part?
        {
            lua_setwarnf(L, &warnfcont, L); // to be continued
        }
        else
        {
            // last part
            Console.Error.WriteLine(); // finish message with end-of-line
            lua_setwarnf(L, &warnfon, L); // next call is a new message
        }
    }

    private static void warnfon(void* ud, ReadOnlySpan<char> message, bool tocont)
    {
        if (checkcontrol((lua_State*)ud, message, tocont)) // control message?
        {
            return; // nothing else to be done
        }

        Console.Error.Write("Lua warning: "); // start a new warning
        warnfcont(ud, message, tocont); // finish processing
    }

//
// A function to compute an unsigned int with some level of
// randomness. Rely on Address Space Layout Randomisation (if present)
// and the current time.
//
// #if !defined(luai_makeseed)
//
// #include <time.h>
//
//
// Size for the buffer, in bytes
// #define BUFSEEDB	(sizeof(void*) + sizeof(time_t))
//
// Size for the buffer in int's, rounded up
// #define BUFSEED		((BUFSEEDB + sizeof(int) - 1) / sizeof(int))
//
//
// Copy the contents of variable 'v' into the buffer pointed by 'b'.
// (The '&b[0]' disguises 'b' to fix an absurd warning from clang.)
//
// #define addbuff(b,v)	(memcpy(&b[0], &(v), sizeof(v)), b += sizeof(v))

    private static uint luai_makeseed()
    {
        return (uint)new Random().NextInt64();
// unsigned int buff[BUFSEED];
// unsigned int res;
// unsigned int i;
// time_t t = time(null);
// char *b = (char*)buff;
// addbuff(b, b); // local variable's address
// addbuff(b, t); // time
// fill (rare but possible) remain of the buffer with zeros
// memset(b, 0, sizeof(buff) - BUFSEEDB);
// res = buff[0];
// for (i = 1; i < BUFSEED; i++)
// res ^= (res >> 3) + (res << 7) + buff[i];
// return res;
        throw new NotImplementedException();
    }

    public static uint luaL_makeseed(lua_State* L)
    {
        return luai_makeseed();
    }

    /// <summary>
    /// Use the name with parentheses so that headers can redefine it as a macro.
    /// </summary>
    public static lua_State* luaL_newstate()
    {
#if LUA_TEST
        lua_State* L = lua_newstate(&debug_realloc, l_memcontrol, luaL_makeseed(null));
#else
        lua_State* L = lua_newstate(&luaL_alloc, null, luaL_makeseed(null));
#endif
        if (L != null)
        {
            lua_atpanic(L, &panic);
            lua_setwarnf(L, &warnfon, L);
        }

        return L;
    }

    public static void luaL_checkversion(lua_State* L, int version, int sizes)
    {
        long v = lua_version(L);
        if (sizes != LUAL_NUMSIZES) // check numeric types
        {
            luaL_error(L, "core and library have incompatible numeric types");
        }
        else if (v != version)
        {
            luaL_error(
                L,
                "version mismatch: app. needs %f, Lua core provides %f",
                (double)sizes,
                (double)v);
        }
    }
}
