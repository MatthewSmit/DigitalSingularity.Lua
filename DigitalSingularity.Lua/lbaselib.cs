namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /*
     ** $Id: lbaselib.c $
     ** Basic library
     ** See Copyright Notice in lua.h
     */

    private static int luaB_print(lua_State* L)
    {
        int n = lua_gettop(L); /* number of arguments */
        for (int i = 1; i <= n; i++)
        {
            /* for each argument */
            string s = luaL_tonetstring(L, i); /* convert it to string */
            if (i > 1) /* not the first element? */
            {
                Console.Write("\t"); /* add a tab before it */
            }

            Console.Write(s); /* print it */
            lua_pop(L, 1); /* pop result */
        }

        Console.WriteLine();
        return 0;
    }

/*
 ** Creates a warning with all given arguments.
 ** Check first for errors; otherwise an error may interrupt
 ** the composition of a warning, leaving it unfinished.
 */
    private static int luaB_warn(lua_State* L)
    {
//   int n = lua_gettop(L);  /* number of arguments */
//   int i;
//   luaL_checkstring(L, 1);  /* at least one argument */
//   for (i = 2; i <= n; i++)
//     luaL_checkstring(L, i);  /* make sure all arguments are strings */
//   for (i = 1; i < n; i++)  /* compose warning */
//     lua_warning(L, lua_tostring(L, i), 1);
//   lua_warning(L, lua_tostring(L, n), 0);  /* close warning */
//   return 0;
        throw new NotImplementedException();
    }

// #define SPACECHARS	" \f\n\r\t\v"
//
// static const char *b_str2int (const char *s, unsigned base, long *pn) {
//   lua_Unsigned n = 0;
//   int neg = 0;
//   s += strspn(s, SPACECHARS);  /* skip initial spaces */
//   if (*s == '-') { s++; neg = 1; }  /* handle sign */
//   else if (*s == '+') s++;
//   if (!isalnum(cast_uchar(*s)))  /* no digit? */
//     return null;
//   do {
//     unsigned digit = cast_uint(isdigit(cast_uchar(*s))
//                                ? *s - '0'
//                                : (toupper(cast_uchar(*s)) - 'A') + 10);
//     if (digit >= base) return null;  /* invalid numeral */
//     n = n * base + digit;
//     s++;
//   } while (isalnum(cast_uchar(*s)));
//   s += strspn(s, SPACECHARS);  /* skip trailing spaces */
//   *pn = (long)((neg) ? (0u - n) : n);
//   return s;
// }

    private static int luaB_tonumber(lua_State* L)
    {
        if (lua_isnoneornil(L, 2))
        {
            /* standard conversion? */
            if (lua_type(L, 1) == LUA_TNUMBER)
            {
                /* already a number? */
                lua_settop(L, 1); /* yes; return it */
                return 1;
            }

            string? s = luaL_tonetstring(L, 1);
            if (s != null && lua_stringtonumber(L, s) == s.Length + 1)
            {
                return 1; /* successful conversion to number */
            }

            /* else not a number */
            luaL_checkany(L, 1); /* (but there must be some parameter) */
        }
        else
        {
//     size_t l;
//     const char *s;
//     long n = 0;  /* to avoid warnings */
//     long base = luaL_checkinteger(L, 2);
//     luaL_checktype(L, 1, LUA_TSTRING);  /* no numbers as strings */
//     s = lua_tolstring(L, 1, &l);
//     luaL_argcheck(L, 2 <= base && base <= 36, 2, "base out of range");
//     if (b_str2int(s, cast_uint(base), &n) == s + l) {
//       lua_pushinteger(L, n);
//       return 1;
//     }  /* else not a number */
            throw new NotImplementedException();
        }
        /* else not a number */

        luaL_pushfail(L); /* not a number */
        return 1;
    }

    private static int luaB_error(lua_State* L)
    {
        int level = (int)luaL_optinteger(L, 2, 1);
        lua_settop(L, 1);
        if (lua_type(L, 1) == LUA_TSTRING && level > 0)
        {
            luaL_where(L, level); /* add extra information */
            lua_pushvalue(L, 1);
            lua_concat(L, 2);
        }

        return lua_error(L);
    }

    private static int luaB_getmetatable(lua_State* L)
    {
        luaL_checkany(L, 1);
        if (!lua_getmetatable(L, 1))
        {
            lua_pushnil(L);
            return 1; /* no metatable */
        }

        luaL_getmetafield(L, 1, "__metatable");
        return 1; /* returns either __metatable field (if present) or metatable */
    }

    private static int luaB_setmetatable(lua_State* L)
    {
        int t = lua_type(L, 2);
        luaL_checktype(L, 1, LUA_TTABLE);
        luaL_argexpected(L, t is LUA_TNIL or LUA_TTABLE, 2, "nil or table");
        if (luaL_getmetafield(L, 1, "__metatable") != LUA_TNIL)
        {
            return luaL_error(L, "cannot change a protected metatable");
        }

        lua_settop(L, 2);
        lua_setmetatable(L, 1);
        return 1;
    }

    private static int luaB_rawequal(lua_State* L)
    {
//   luaL_checkany(L, 1);
//   luaL_checkany(L, 2);
//   lua_pushboolean(L, lua_rawequal(L, 1, 2));
//   return 1;
        throw new NotImplementedException();
    }

    private static int luaB_rawlen(lua_State* L)
    {
        int t = lua_type(L, 1);
        luaL_argexpected(
            L,
            t is LUA_TTABLE or LUA_TSTRING,
            1,
            "table or string");
        lua_pushinteger(L, (long)lua_rawlen(L, 1));
        return 1;
    }

    private static int luaB_rawget(lua_State* L)
    {
        luaL_checktype(L, 1, LUA_TTABLE);
        luaL_checkany(L, 2);
        lua_settop(L, 2);
        lua_rawget(L, 1);
        return 1;
    }

    private static int luaB_rawset(lua_State* L)
    {
        luaL_checktype(L, 1, LUA_TTABLE);
        luaL_checkany(L, 2);
        luaL_checkany(L, 3);
        lua_settop(L, 3);
        lua_rawset(L, 1);
        return 1;
    }

    private static int pushmode(lua_State* L, int oldmode)
    {
//   if (oldmode == -1)
//     luaL_pushfail(L);  /* invalid call to 'lua_gc' */
//   else
//     lua_pushstring(L, (oldmode == LUA_GCINC) ? "incremental"
//                                              : "generational");
//   return 1;
        throw new NotImplementedException();
    }

    private static string[] opts =
    [
        "stop", "restart", "collect",
        "count", "step", "isrunning", "generational", "incremental",
        "param",
    ];

    private static byte[] optsnum =
    [
        LUA_GCSTOP, LUA_GCRESTART, LUA_GCCOLLECT,
        LUA_GCCOUNT, LUA_GCSTEP, LUA_GCISRUNNING, LUA_GCGEN, LUA_GCINC,
        LUA_GCPARAM,
    ];

    private static int luaB_collectgarbage(lua_State* L)
    {
        int o = optsnum[luaL_checkoption(L, 1, "collect", opts)];
        switch (o)
        {
            case LUA_GCCOUNT:
                {
                    int k = lua_gc(L, o);
                    int b = lua_gc(L, LUA_GCCOUNTB);
                    if (k == -1)
                    {
                        break;
                    }

                    lua_pushnumber(L, k + ((double)b / 1024));
                    return 1;
                }

            case LUA_GCSTEP:
                {
                    long n = luaL_optinteger(L, 2, 0);
                    int res = lua_gc(L, o, n);
                    if (res == -1)
                    {
                        break;
                    }

                    lua_pushboolean(L, res > 0);
                    return 1;
                }

            case LUA_GCISRUNNING:
                {
                    int res = lua_gc(L, o);
                    if (res == -1)
                    {
                        break;
                    }

                    lua_pushboolean(L, res > 0);
                    return 1;
                }

            case LUA_GCGEN:
                return pushmode(L, lua_gc(L, o));

            case LUA_GCINC:
                return pushmode(L, lua_gc(L, o));

            case LUA_GCPARAM:
                {
//       static const char *const params[] = {
//         "minormul", "majorminor", "minormajor",
//         "pause", "stepmul", "stepsize", null};
//       static const char pnum[] = {
//         LUA_GCPMINORMUL, LUA_GCPMAJORMINOR, LUA_GCPMINORMAJOR,
//         LUA_GCPPAUSE, LUA_GCPSTEPMUL, LUA_GCPSTEPSIZE};
//       int p = pnum[luaL_checkoption(L, 2, null, params)];
//       long value = luaL_optinteger(L, 3, -1);
//       lua_pushinteger(L, lua_gc(L, o, p, (int)value));
//       return 1;
                    throw new NotImplementedException();
                }

            default:
                {
                    int res = lua_gc(L, o);
                    if (res == -1)
                    {
                        break;
                    }

                    lua_pushinteger(L, res);
                    return 1;
                }
        }

        luaL_pushfail(L); /* invalid call (inside a finaliser) */
        return 1;
    }

    private static int luaB_type(lua_State* L)
    {
        int t = lua_type(L, 1);
        luaL_argcheck(L, t != LUA_TNONE, 1, "value expected");
        lua_pushstring(L, lua_typename(L, t));
        return 1;
    }

    private static int luaB_next(lua_State* L)
    {
        luaL_checktype(L, 1, LUA_TTABLE);
        lua_settop(L, 2); /* create a 2nd argument if there isn't one */
        if (lua_next(L, 1))
        {
            return 2;
        }

        lua_pushnil(L);
        return 1;
    }

// static int pairscont (lua_State *L, int status, nint k) {
//   (void)L; (void)status; (void)k;  /* unused */
//   return 4;  /* __pairs did all the work, just return its results */
// }

    private static int luaB_pairs(lua_State* L)
    {
//   luaL_checkany(L, 1);
//   if (luaL_getmetafield(L, 1, "__pairs") == LUA_TNIL) {  /* no metamethod? */
//     lua_pushcfunction(L, luaB_next);  /* will return generator and */
//     lua_pushvalue(L, 1);  /* state */
//     lua_pushnil(L);  /* initial value */
//     lua_pushnil(L);  /* to-be-closed object */
//   }
//   else {
//     lua_pushvalue(L, 1);  /* argument 'self' to metamethod */
//     lua_callk(L, 1, 4, 0, pairscont);  /* get 4 values from metamethod */
//   }
//   return 4;
        throw new NotImplementedException();
    }

    /*
    ** Traversal function for 'ipairs'
    */
    private static int ipairsaux(lua_State* L)
    {
        long i = luaL_checkinteger(L, 2);
        i++;
        lua_pushinteger(L, i);
        return lua_geti(L, 1, i) == LUA_TNIL ? 1 : 2;
    }

/*
 ** 'ipairs' function. Returns 'ipairsaux', given "table", 0.
 ** (The given "table" may not be a table.)
 */
    private static int luaB_ipairs(lua_State* L)
    {
        luaL_checkany(L, 1);
        lua_pushcfunction(L, &ipairsaux); /* iteration function */
        lua_pushvalue(L, 1); /* state */
        lua_pushinteger(L, 0); /* initial value */
        return 3;
    }

    private static int load_aux(lua_State* L, int status, int envidx)
    {
//   if (l_likely(status == LUA_OK)) {
//     if (envidx != 0) {  /* 'env' parameter? */
//       lua_pushvalue(L, envidx);  /* environment for loaded function */
//       if (!lua_setupvalue(L, -2, 1))  /* set it as 1st upvalue */
//         lua_pop(L, 1);  /* remove 'env' if not used by previous call */
//     }
//     return 1;
//   }
//   else {  /* error (message is on top of the stack) */
//     luaL_pushfail(L);
//     lua_insert(L, -2);  /* put before error message */
//     return 2;  /* return fail plus error message */
//   }
        throw new NotImplementedException();
    }

// static const char *getMode (lua_State *L, int idx) {
//   const char *mode = luaL_optstring(L, idx, "bt");
//   if (strchr(mode, 'B') != null)  /* Lua code cannot use fixed buffers */
//     luaL_argerror(L, idx, "invalid mode");
//   return mode;
// }

    private static int luaB_loadfile(lua_State* L)
    {
//   const char *fname = luaL_optstring(L, 1, null);
//   const char *mode = getMode(L, 2);
//   int env = (!lua_isnone(L, 3) ? 3 : 0);  /* 'env' index or 0 if no 'env' */
//   int status = luaL_loadfilex(L, fname, mode);
//   return load_aux(L, status, env);
        throw new NotImplementedException();
    }

/*
 ** {======================================================
 ** Generic Read function
 ** =======================================================
 */

// /*
// ** reserved slot, above all arguments, to hold a copy of the returned
// ** string to avoid it being collected while parsed. 'load' has four
// ** optional arguments (chunk, source name, mode, and environment).
// */
// #define RESERVEDSLOT	5
//
//
// /*
// ** Reader for generic 'load' function: 'lua_load' uses the
// ** stack for internal stuff, so the reader cannot change the
// ** stack top. Instead, it keeps its resulting string in a
// ** reserved slot inside the stack.
// */
// static const char *generic_reader (lua_State *L, void *ud, size_t *size) {
//   (void)(ud);  /* not used */
//   luaL_checkstack(L, 2, "too many nested functions");
//   lua_pushvalue(L, 1);  /* get function */
//   lua_call(L, 0, 1);  /* call it */
//   if (lua_isnil(L, -1)) {
//     lua_pop(L, 1);  /* pop result */
//     *size = 0;
//     return null;
//   }
//   else if (l_unlikely(!lua_isstring(L, -1)))
//     luaL_error(L, "reader function must return a string");
//   lua_replace(L, RESERVEDSLOT);  /* save string in reserved slot */
//   return lua_tolstring(L, RESERVEDSLOT, size);
// }

    private static int luaB_load(lua_State* L)
    {
//   int status;
        byte* s = lua_tolstring(L, 1, out long l);
        // var mode = getMode(L, 3);
//   int env = (!lua_isnone(L, 4) ? 4 : 0);  /* 'env' index or 0 if no 'env' */
//   if (s != null) {  /* loading a string? */
//     const char *chunkname = luaL_optstring(L, 2, s);
//     status = luaL_loadbufferx(L, s, l, chunkname, mode);
//   }
//   else {  /* loading from a reader function */
//     const char *chunkname = luaL_optstring(L, 2, "=(load)");
//     luaL_checktype(L, 1, LUA_TFUNCTION);
//     lua_settop(L, RESERVEDSLOT);  /* create reserved slot */
//     status = lua_load(L, generic_reader, null, chunkname, mode);
//   }
//   return load_aux(L, status, env);
        throw new NotImplementedException();
    }

/* }====================================================== */

// static int dofilecont (lua_State *L, int d1, nint d2) {
//   (void)d1;  (void)d2;  /* only to match 'lua_Kfunction' prototype */
//   return lua_gettop(L) - 1;
// }

    private static int luaB_dofile(lua_State* L)
    {
//   const char *fname = luaL_optstring(L, 1, null);
//   lua_settop(L, 1);
//   if (l_unlikely(luaL_loadfile(L, fname) != LUA_OK))
//     return lua_error(L);
//   lua_callk(L, 0, LUA_MULTRET, 0, dofilecont);
//   return dofilecont(L, 0, 0);
        throw new NotImplementedException();
    }

    private static int luaB_assert(lua_State* L)
    {
        if (lua_toboolean(L, 1)) /* condition is true? */
        {
            return lua_gettop(L); /* return all arguments */
        }

        /* error */
        luaL_checkany(L, 1); /* there must be a condition */
        lua_remove(L, 1); /* remove it */
        lua_pushliteral(L, "assertion failed!"); /* default message */
        lua_settop(L, 1); /* leave only message (default if no other one) */
        return luaB_error(L); /* call 'error' */
    }

    private static int luaB_select(lua_State* L)
    {
//   int n = lua_gettop(L);
//   if (lua_type(L, 1) == LUA_TSTRING && *lua_tostring(L, 1) == '#') {
//     lua_pushinteger(L, n-1);
//     return 1;
//   }
//   else {
//     long i = luaL_checkinteger(L, 1);
//     if (i < 0) i = n + i;
//     else if (i > n) i = n;
//     luaL_argcheck(L, 1 <= i, 1, "index out of range");
//     return n - (int)i;
//   }
        throw new NotImplementedException();
    }

    /*
    ** Continuation function for 'pcall' and 'xpcall'. Both functions
    ** already pushed a 'true' before doing the call, so in case of success
    ** 'finishpcall' only has to return everything in the stack minus
    ** 'extra' values (where 'extra' is exactly the number of items to be
    ** ignored).
    */
    private static int finishpcall(lua_State* L, int status, void* extra)
    {
        if (status != LUA_OK && status != LUA_YIELD)
        {
            /* error? */
            lua_pushboolean(L, false); /* first result (false) */
            lua_pushvalue(L, -2); /* error message */
            return 2; /* return false, msg */
        }

        return lua_gettop(L) - (int)extra; /* return all results */
    }

    private static int luaB_pcall(lua_State* L)
    {
        luaL_checkany(L, 1);
        lua_pushboolean(L, true); /* first result if no errors */
        lua_insert(L, 1); /* put it in place */
        int status = lua_pcallk(L, lua_gettop(L) - 2, LUA_MULTRET, 0, 0, &finishpcall);
        return finishpcall(L, status, null);
    }

/*
 ** Do a protected call with error handling. After 'lua_rotate', the
 ** stack will have <f, err, true, f, [args...]>; so, the function passes
 ** 2 to 'finishpcall' to skip the 2 first values when returning results.
 */
    private static int luaB_xpcall(lua_State* L)
    {
//   int status;
//   int n = lua_gettop(L);
//   luaL_checktype(L, 2, LUA_TFUNCTION);  /* check error function */
//   lua_pushboolean(L, 1);  /* first result */
//   lua_pushvalue(L, 1);  /* function */
//   lua_rotate(L, 3, 2);  /* move them below function's arguments */
//   status = lua_pcallk(L, n - 2, LUA_MULTRET, 2, 2, finishpcall);
//   return finishpcall(L, status, 2);
        throw new NotImplementedException();
    }

    private static int luaB_tostring(lua_State* L)
    {
        luaL_checkany(L, 1);
        luaL_tolstring(L, 1, out _);
        return 1;
    }

    private static readonly luaL_Reg[] base_funcs =
    [
        new("assert", &luaB_assert),
        new("collectgarbage", &luaB_collectgarbage),
        new("dofile", &luaB_dofile),
        new("error", &luaB_error),
        new("getmetatable", &luaB_getmetatable),
        new("ipairs", &luaB_ipairs),
        new("loadfile", &luaB_loadfile),
        new("load", &luaB_load),
        new("next", &luaB_next),
        new("pairs", &luaB_pairs),
        new("pcall", &luaB_pcall),
        new("print", &luaB_print),
        new("warn", &luaB_warn),
        new("rawequal", &luaB_rawequal),
        new("rawlen", &luaB_rawlen),
        new("rawget", &luaB_rawget),
        new("rawset", &luaB_rawset),
        new("select", &luaB_select),
        new("setmetatable", &luaB_setmetatable),
        new("tonumber", &luaB_tonumber),
        new("tostring", &luaB_tostring),
        new("type", &luaB_type),
        new("xpcall", &luaB_xpcall),
        /* placeholders */
        new(LUA_GNAME, null),
        new("_VERSION", null),
    ];

    public static partial int luaopen_base(lua_State* L)
    {
        /* open lib into global table */
        lua_pushglobaltable(L);
        luaL_setfuncs(L, base_funcs, 0);
        /* set global _G */
        lua_pushvalue(L, -1);
        lua_setfield(L, -2, LUA_GNAME);
        /* set global _VERSION */
        lua_pushliteral(L, LUA_VERSION);
        lua_setfield(L, -2, "_VERSION");
        return 1;
    }
}
