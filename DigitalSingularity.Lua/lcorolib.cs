namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /*
     ** $Id: lcorolib.c $
     ** Coroutine Library
     ** See Copyright Notice in lua.h
     */

    private static lua_State* getco(lua_State* L)
    {
        lua_State* co = lua_tothread(L, 1);
        luaL_argexpected(L, co != null, 1, "thread");
        return co;
    }

    /*
     ** Resumes a coroutine. Returns the number of results for non-error
     ** cases or -1 for errors.
     */
    private static int auxresume(lua_State* L, lua_State* co, int narg)
    {
        if (!lua_checkstack(co, narg))
        {
            lua_pushliteral(L, "too many arguments to resume");
            return -1; /* error flag */
        }

        lua_xmove(L, co, narg);
        int nres;
        int status = lua_resume(co, L, narg, &nres);
        if (status is LUA_OK or LUA_YIELD)
        {
            if (!lua_checkstack(L, nres + 1))
            {
                lua_pop(co, nres); /* remove results anyway */
                lua_pushliteral(L, "too many results to resume");
                return -1; /* error flag */
            }

            lua_xmove(co, L, nres); /* move yielded values */
            return nres;
        }

        lua_xmove(co, L, 1); /* move error message */
        return -1; /* error flag */
    }

    private static int luaB_coresume(lua_State* L)
    {
        lua_State* co = getco(L);
        int r = auxresume(L, co, lua_gettop(L) - 1);
        if (r < 0)
        {
            lua_pushboolean(L, false);
            lua_insert(L, -2);
            return 2; /* return false + error message */
        }

        lua_pushboolean(L, true);
        lua_insert(L, -(r + 1));
        return r + 1; /* return true + 'resume' returns */
    }

    private static int luaB_auxwrap(lua_State* L)
    {
        lua_State* co = lua_tothread(L, lua_upvalueindex(1));
        int r = auxresume(L, co, lua_gettop(L));
        if (r < 0)
        {
            /* error? */
            int stat = lua_status(co);
            if (stat != LUA_OK && stat != LUA_YIELD)
            {
                /* error in the coroutine? */
                stat = lua_closethread(co, L); /* close its tbc variables */
                Debug.Assert(stat != LUA_OK);
                lua_xmove(co, L, 1); /* move error message to the caller */
            }

            if (stat != LUA_ERRMEM && /* not a memory error and ... */
                lua_type(L, -1) == LUA_TSTRING)
            {
                /* ... error object is a string? */
                luaL_where(L, 1); /* add extra info, if available */
                lua_insert(L, -2);
                lua_concat(L, 2);
            }

            return lua_error(L); /* propagate error */
        }

        return r;
    }

    private static int luaB_cocreate(lua_State* L)
    {
        luaL_checktype(L, 1, LUA_TFUNCTION);
        lua_State* NL = lua_newthread(L);
        lua_pushvalue(L, 1); /* move function to top */
        lua_xmove(L, NL, 1); /* move function from L to NL */
        return 1;
    }

    private static int luaB_cowrap(lua_State* L)
    {
        luaB_cocreate(L);
        lua_pushcclosure(L, &luaB_auxwrap, 1);
        return 1;
    }

    private static int luaB_yield(lua_State* L)
    {
        return lua_yield(L, lua_gettop(L));
    }

    private const int COS_RUN = 0;
    private const int COS_DEAD = 1;
    private const int COS_YIELD = 2;
    private const int COS_NORM = 3;

    private static readonly string[] statname = ["running", "dead", "suspended", "normal"];

    private static int auxstatus(lua_State* L, lua_State* co)
    {
        if (L == co)
        {
            return COS_RUN;
        }

        switch (lua_status(co))
        {
            case LUA_YIELD:
                return COS_YIELD;

            case LUA_OK:
                lua_Debug ar = new();
                if (lua_getstack(co, 0, ref ar))  /* does it have frames? */
                {
                    return COS_NORM;  /* it is running */
                }

                if (lua_gettop(co) == 0)
                {
                    return COS_DEAD;
                }

                return COS_YIELD;  /* initial state */
                
            default: /* some error occurred */
                return COS_DEAD;
        }
    }

    private static int luaB_costatus(lua_State* L)
    {
        lua_State* co = getco(L);
        lua_pushstring(L, statname[auxstatus(L, co)]);
        return 1;
    }

    private static lua_State* getoptco(lua_State* L)
    {
        return lua_isnone(L, 1) ? L : getco(L);
    }

    private static int luaB_yieldable(lua_State* L)
    {
        lua_State* co = getoptco(L);
        lua_pushboolean(L, lua_isyieldable(co));
        return 1;
    }

    private static int luaB_corunning(lua_State* L)
    {
        bool ismain = lua_pushthread(L);
        lua_pushboolean(L, ismain);
        return 2;
    }

    private static int luaB_close(lua_State* L)
    {
        lua_State* co = getoptco(L);
        int status = auxstatus(L, co);
        switch (status)
        {
            case COS_DEAD:
            case COS_YIELD:
                status = lua_closethread(co, L);
                if (status == LUA_OK)
                {
                    lua_pushboolean(L, true);
                    return 1;
                }

                lua_pushboolean(L, false);
                lua_xmove(co, L, 1); /* move error message */
                return 2;

            case COS_NORM:
                return luaL_error(L, "cannot close a %s coroutine", statname[status]);

            case COS_RUN:
                lua_geti(L, LUA_REGISTRYINDEX, LUA_RIDX_MAINTHREAD); /* get main */
                if (lua_tothread(L, -1) == co)
                {
                    return luaL_error(L, "cannot close main thread");
                }

                lua_closethread(co, L); /* close itself */
                /* previous call does not return */
                throw new InvalidOperationException();

            default:
                throw new InvalidOperationException();
        }
    }

    private static readonly luaL_Reg[] co_funcs =
    [
        new("create", &luaB_cocreate),
        new("resume", &luaB_coresume),
        new("running", &luaB_corunning),
        new("status", &luaB_costatus),
        new("wrap", &luaB_cowrap),
        new("yield", &luaB_yield),
        new("isyieldable", &luaB_yieldable),
        new("close", &luaB_close),
    ];

    public static int luaopen_coroutine(lua_State* L)
    {
        luaL_newlib(L, co_funcs);
        return 1;
    }
}
