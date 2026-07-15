namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    // $Id: ldblib.c $
    // Interface from Lua to its debug API
    // See Copyright Notice in lua.h

    /// <summary>
    /// The hook table at registry[HOOKKEY] maps threads to their current
    /// hook function.
    /// </summary>
    private const string HOOKKEY = "_HOOKKEY";

    /// <summary>
    /// If L1 != L, L1 can be in any state, and therefore there are no
    /// guarantees about its stack space; any push in L1 must be
    /// checked.
    /// </summary>
    private static void checkstack(lua_State* L, lua_State* L1, int n)
    {
        if (L != L1 && !lua_checkstack(L1, n))
        {
            luaL_error(L, "stack overflow");
        }
    }

    private static int db_getregistry(lua_State* L)
    {
        lua_pushvalue(L, LUA_REGISTRYINDEX);
        return 1;
    }

    private static int db_getmetatable(lua_State* L)
    {
        luaL_checkany(L, 1);
        if (!lua_getmetatable(L, 1))
        {
            lua_pushnil(L); // no metatable
        }

        return 1;
    }

    private static int db_setmetatable(lua_State* L)
    {
        int t = lua_type(L, 2);
        luaL_argexpected(L, t is LUA_TNIL or LUA_TTABLE, 2, "nil or table");
        lua_settop(L, 2);
        lua_setmetatable(L, 1);
        return 1; // return 1st argument
    }

    private static int db_getuservalue(lua_State* L)
    {
        int n = (int)luaL_optinteger(L, 2, 1);
        if (lua_type(L, 1) != LUA_TUSERDATA)
        {
            luaL_pushfail(L);
        }
        else if (lua_getiuservalue(L, 1, n) != LUA_TNONE)
        {
            lua_pushboolean(L, true);
            return 2;
        }

        return 1;
    }

    private static int db_setuservalue(lua_State* L)
    {
        int n = (int)luaL_optinteger(L, 3, 1);
        luaL_checktype(L, 1, LUA_TUSERDATA);
        luaL_checkany(L, 2);
        lua_settop(L, 2);
        if (!lua_setiuservalue(L, 1, n))
        {
            luaL_pushfail(L);
        }

        return 1;
    }

    /// <summary>
    /// Auxiliary function used by several library functions: check for
    /// an optional thread as function's first argument and set 'arg' with
    /// 1 if this argument is present (so that functions can skip it to
    /// access their other arguments)
    /// </summary>
    private static lua_State* getthread(lua_State* L, out int arg)
    {
        if (lua_isthread(L, 1))
        {
            arg = 1;
            return lua_tothread(L, 1);
        }

        arg = 0;
        return L; // function will operate over current thread
    }

    /// <summary>
    /// Variations of 'lua_settable', used by 'db_getinfo' to put results
    /// from 'lua_getinfo' into result table. Key is always a string;
    /// value can be a string, an int, or a boolean.
    /// </summary>
    private static void settabss(lua_State* L, string k, string? v)
    {
        lua_pushstring(L, v);
        lua_setfield(L, -2, k);
    }

    private static void settabsi(lua_State* L, string k, int v)
    {
        lua_pushinteger(L, v);
        lua_setfield(L, -2, k);
    }

    private static void settabsb(lua_State* L, string k, bool v)
    {
        lua_pushboolean(L, v);
        lua_setfield(L, -2, k);
    }

    /// <summary>
    /// In function 'db_getinfo', the call to 'lua_getinfo' may push
    /// results on the stack; later it creates the result table to put
    /// these objects. Function 'treatstackoption' puts the result from
    /// 'lua_getinfo' on top of the result table so that it can call
    /// 'lua_setfield'.
    /// </summary>
    private static void treatstackoption(lua_State* L, lua_State* L1, string fname)
    {
        if (L == L1)
        {
            lua_rotate(L, -2, 1); // exchange object and table
        }
        else
        {
            lua_xmove(L1, L, 1); // move object to the "main" stack
        }

        lua_setfield(L, -2, fname); // put object into table
    }

    /// <summary>
    /// Calls 'lua_getinfo' and collects all results in a new table.
    /// L1 needs stack space for an optional input (function) plus
    /// two optional outputs (function and line table) from function
    /// 'lua_getinfo'.
    /// </summary>
    private static int db_getinfo(lua_State* L)
    {
        lua_Debug ar = new();

        lua_State* L1 = getthread(L, out int arg);
        string options = luaL_optnetstring(L, arg + 2, "flnSrtu");
        checkstack(L, L1, 3);
        luaL_argcheck(L, options[0] != '>', arg + 2, "invalid option '>'");

        if (lua_isfunction(L, arg + 1))
        {
            // info about a function?
            options = lua_pushfstring(L, ">%s", options); // add '>' to 'options'
            lua_pushvalue(L, arg + 1); // move function to 'L1' stack
            lua_xmove(L, L1, 1);
        }
        else
        {
            // stack level
            if (!lua_getstack(L1, (int)luaL_checkinteger(L, arg + 1), ref ar))
            {
                luaL_pushfail(L); // level out of range
                return 1;
            }
        }

        if (!lua_getinfo(L1, options, ref ar))
        {
            return luaL_argerror(L, arg + 2, "invalid option");
        }

        lua_newtable(L); // table to collect results
        if (options.Contains('S'))
        {
            lua_pushlstring(L, ar.source);
            lua_setfield(L, -2, "source");
            settabss(L, "short_src", ar.short_src);
            settabsi(L, "linedefined", ar.linedefined);
            settabsi(L, "lastlinedefined", ar.lastlinedefined);
            settabss(L, "what", ar.what);
        }

        if (options.Contains('l'))
        {
            settabsi(L, "currentline", ar.currentline);
        }

        if (options.Contains('u'))
        {
            settabsi(L, "nups", ar.nups);
            settabsi(L, "nparams", ar.nparams);
            settabsb(L, "isvararg", ar.isvararg);
        }

        if (options.Contains('n'))
        {
            settabss(L, "name", ar.name);
            settabss(L, "namewhat", ar.namewhat);
        }

        if (options.Contains('r'))
        {
            settabsi(L, "ftransfer", ar.ftransfer);
            settabsi(L, "ntransfer", ar.ntransfer);
        }

        if (options.Contains('t'))
        {
            settabsb(L, "istailcall", ar.istailcall);
            settabsi(L, "extraargs", ar.extraargs);
        }

        if (options.Contains('L'))
        {
            treatstackoption(L, L1, "activelines");
        }

        if (options.Contains('f'))
        {
            treatstackoption(L, L1, "func");
        }

        return 1; // return table
    }

    private static int db_getlocal(lua_State* L)
    {
        lua_State* L1 = getthread(L, out int arg);
        int nvar = (int)luaL_checkinteger(L, arg + 2); // local-variable index
        if (lua_isfunction(L, arg + 1))
        {
            // function argument?
            lua_pushvalue(L, arg + 1); // push function
            lua_pushstring(L, lua_getlocal(L, nvar)); // push local name
            return 1; // return only name (there is no value)
        }

        // stack-level argument
        lua_Debug ar = new();
        int level = (int)luaL_checkinteger(L, arg + 1);
        if (!lua_getstack(L1, level, ref ar)) // out of range?
        {
            return luaL_argerror(L, arg + 1, "level out of range");
        }

        checkstack(L, L1, 1);
        string? name = lua_getlocal(L1, ref ar, nvar);
        if (name != null)
        {
            lua_xmove(L1, L, 1); // move local value
            lua_pushstring(L, name); // push name
            lua_rotate(L, -2, 1); // re-order
            return 2;
        }

        luaL_pushfail(L); // no name (nor value)
        return 1;
    }

    private static int db_setlocal(lua_State* L)
    {
        lua_State* L1 = getthread(L, out int arg);
        lua_Debug ar = new();
        int level = (int)luaL_checkinteger(L, arg + 1);
        int nvar = (int)luaL_checkinteger(L, arg + 2);
        if (!lua_getstack(L1, level, ref ar)) // out of range?
        {
            return luaL_argerror(L, arg + 1, "level out of range");
        }

        luaL_checkany(L, arg + 3);
        lua_settop(L, arg + 3);
        checkstack(L, L1, 1);
        lua_xmove(L, L1, 1);
        string? name = lua_setlocal(L1, ref ar, nvar);
        if (name == null)
        {
            lua_pop(L1, 1); // pop value (if not popped by 'lua_setlocal')
        }

        lua_pushstring(L, name);
        return 1;
    }

    /// <summary>
    /// get (if 'get' is true) or set an upvalue from a closure
    /// </summary>
    private static int auxupvalue(lua_State* L, bool get)
    {
        int n = (int)luaL_checkinteger(L, 2); // upvalue index
        luaL_checktype(L, 1, LUA_TFUNCTION); // closure
        string? name = get ? lua_getupvalue(L, 1, n) : lua_setupvalue(L, 1, n);
        if (name == null)
        {
            return 0;
        }

        lua_pushstring(L, name);
        lua_insert(L, -(get ? 2 : 1)); // no-op if get is false
        return get ? 2 : 1;
    }

    private static int db_getupvalue(lua_State* L)
    {
        return auxupvalue(L, true);
    }

    private static int db_setupvalue(lua_State* L)
    {
        luaL_checkany(L, 3);
        return auxupvalue(L, false);
    }

    /// <summary>
    /// Check whether a given upvalue from a given closure exists and
    /// returns its index
    /// </summary>
    private static void* checkupval(lua_State* L, int argf, int argnup, int* pnup)
    {
        int nup = (int)luaL_checkinteger(L, argnup); // upvalue index
        luaL_checktype(L, argf, LUA_TFUNCTION); // closure
        void* id = lua_upvalueid(L, argf, nup);
        if (pnup != null)
        {
            luaL_argcheck(L, id != null, argnup, "invalid upvalue index");
            *pnup = nup;
        }

        return id;
    }

    private static int db_upvalueid(lua_State* L)
    {
        void* id = checkupval(L, 1, 2, null);
        if (id != null)
        {
            lua_pushlightuserdata(L, id);
        }
        else
        {
            luaL_pushfail(L);
        }

        return 1;
    }

    private static int db_upvaluejoin(lua_State* L)
    {
        int n1, n2;
        checkupval(L, 1, 2, &n1);
        checkupval(L, 3, 4, &n2);
        luaL_argcheck(L, !lua_iscfunction(L, 1), 1, "Lua function expected");
        luaL_argcheck(L, !lua_iscfunction(L, 3), 3, "Lua function expected");
        lua_upvaluejoin(L, 1, n1, 3, n2);
        return 0;
    }

    private static readonly string[] hooknames = ["call", "return", "line", "count", "tail call"];

    /// <summary>
    /// Call hook function registered at hook table for the current
    /// thread (if there is one)
    /// </summary>
    private static void hookf(lua_State* L, ref lua_Debug ar)
    {
        lua_getfield(L, LUA_REGISTRYINDEX, HOOKKEY);
        lua_pushthread(L);
        if (lua_rawget(L, -2) == LUA_TFUNCTION)
        {
            // is there a hook function?
            lua_pushstring(L, hooknames[ar.@event]); // push event name
            if (ar.currentline >= 0)
            {
                lua_pushinteger(L, ar.currentline); // push current line
            }
            else
            {
                lua_pushnil(L);
            }

            Debug.Assert(lua_getinfo(L, "lS", ref ar));
            lua_call(L, 2, 0); // call hook function
        }
    }

    /// <summary>
    /// Convert a string mask (for 'sethook') into a bit mask
    /// </summary>
    private static byte makemask(ReadOnlySpan<byte> smask, int count)
    {
        byte mask = 0;
        if (smask.Contains((byte)'c'))
        {
            mask |= LUA_MASKCALL;
        }

        if (smask.Contains((byte)'r'))
        {
            mask |= LUA_MASKRET;
        }

        if (smask.Contains((byte)'l'))
        {
            mask |= LUA_MASKLINE;
        }

        if (count > 0)
        {
            mask |= LUA_MASKCOUNT;
        }

        return mask;
    }

    /// <summary>
    /// Convert a bit mask (for 'gethook') into a string mask
    /// </summary>
    private static ReadOnlySpan<byte> unmakemask(int mask, Span<byte> smask)
    {
        int i = 0;
        if ((mask & LUA_MASKCALL) != 0)
        {
            smask[i++] = (byte)'c';
        }

        if ((mask & LUA_MASKRET) != 0)
        {
            smask[i++] = (byte)'r';
        }

        if ((mask & LUA_MASKLINE) != 0)
        {
            smask[i++] = (byte)'l';
        }

        return smask[..i];
    }

    private static int db_sethook(lua_State* L)
    {
        lua_State* L1 = getthread(L, out int arg);
        lua_Hook func;
        byte mask;
        int count;
        if (lua_isnoneornil(L, arg + 1))
        {
            // no hook?
            lua_settop(L, arg + 1);
            func = null;
            mask = 0;
            count = 0; // turn off hooks
        }
        else
        {
            ReadOnlySpan<byte> smask = luaL_checkstring(L, arg + 2);
            luaL_checktype(L, arg + 1, LUA_TFUNCTION);
            count = (int)luaL_optinteger(L, arg + 3, 0);
            func = &hookf;
            mask = makemask(smask, count);
        }

        if (!luaL_getsubtable(L, LUA_REGISTRYINDEX, HOOKKEY))
        {
            // table just created; initialise it
            lua_pushliteral(L, "k");
            lua_setfield(L, -2, "__mode"); // hooktable.__mode = "k"
            lua_pushvalue(L, -1);
            lua_setmetatable(L, -2); // metatable(hooktable) = hooktable
        }

        checkstack(L, L1, 1);
        lua_pushthread(L1);
        lua_xmove(L1, L, 1); // key (thread)
        lua_pushvalue(L, arg + 1); // value (hook function)
        lua_rawset(L, -3); // hooktable[L1] = new Lua hook
        lua_sethook(L1, func!, mask, count);
        return 0;
    }

    private static int db_gethook(lua_State* L)
    {
        lua_State* L1 = getthread(L, out int arg);
        int mask = lua_gethookmask(L1);
        lua_Hook hook = lua_gethook(L1);
        if (hook == null!)
        {
            // no hook?
            luaL_pushfail(L);
            return 1;
        }

        lua_Hook hookfPtr = &hookf;
        if (hook != hookfPtr) // external hook?
        {
            lua_pushliteral(L, "external hook");
        }
        else
        {
            // hook table must exist
            lua_getfield(L, LUA_REGISTRYINDEX, HOOKKEY);
            checkstack(L, L1, 1);
            lua_pushthread(L1);
            lua_xmove(L1, L, 1);
            lua_rawget(L, -2); // 1st result = hooktable[L1]
            lua_remove(L, -2); // remove hook table
        }

        Span<byte> buff = stackalloc byte[5];
        lua_pushlstring(L, unmakemask(mask, buff)); // 2nd result = mask
        lua_pushinteger(L, lua_gethookcount(L1)); // 3rd result = count
        return 3;
    }

    private static int db_debug(lua_State* L)
    {
// for (;;) {
// char buffer[250];
// lua_writestringerror("%s", "lua_debug> ");
// if (fgets(buffer, sizeof(buffer), stdin) == null ||
// strcmp(buffer, "cont\n") == 0)
// return 0;
// if (luaL_loadbuffer(L, buffer, strlen(buffer), "=(debug command)") ||
// lua_pcall(L, 0, 0, 0))
// lua_writestringerror("%s\n", luaL_tolstring(L, -1, null));
// lua_settop(L, 0); // remove eventual returns
// }
        throw new NotImplementedException();
    }

    private static int db_traceback(lua_State* L)
    {
        lua_State* L1 = getthread(L, out int arg);
        string? msg = lua_tonetstring(L, arg + 1);
        if (msg == null && !lua_isnoneornil(L, arg + 1)) // non-string 'msg'?
        {
            lua_pushvalue(L, arg + 1); // return it untouched
        }
        else
        {
            int level = (int)luaL_optinteger(L, arg + 2, L == L1 ? 1 : 0);
            luaL_traceback(L, L1, msg, level);
        }

        return 1;
    }

    private static readonly luaL_Reg[] dblib =
    [
        new("debug", &db_debug),
        new("getuservalue", &db_getuservalue),
        new("gethook", &db_gethook),
        new("getinfo", &db_getinfo),
        new("getlocal", &db_getlocal),
        new("getregistry", &db_getregistry),
        new("getmetatable", &db_getmetatable),
        new("getupvalue", &db_getupvalue),
        new("upvaluejoin", &db_upvaluejoin),
        new("upvalueid", &db_upvalueid),
        new("setuservalue", &db_setuservalue),
        new("sethook", &db_sethook),
        new("setlocal", &db_setlocal),
        new("setmetatable", &db_setmetatable),
        new("setupvalue", &db_setupvalue),
        new("traceback", &db_traceback),
    ];

    public static int luaopen_debug(lua_State* L)
    {
        luaL_newlib(L, dblib);
        return 1;
    }
}
