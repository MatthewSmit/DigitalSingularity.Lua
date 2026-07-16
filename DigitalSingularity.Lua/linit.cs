namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /// <summary>
    /// Standard Libraries. (Must be listed in the same ORDER of their
    /// respective constants LUA_&lt;libname&gt;K.)
    /// </summary>
    private static readonly luaL_Reg[] stdlibs =
    [
        new(LUA_GNAME, CFunction.FromFunction(&luaopen_base)),
        new(LUA_LOADLIBNAME, CFunction.FromFunction(&luaopen_package)),
        new(LUA_COLIBNAME, CFunction.FromFunction(&luaopen_coroutine)),
        new(LUA_DBLIBNAME, CFunction.FromFunction(&luaopen_debug)),
        new(LUA_IOLIBNAME, CFunction.FromFunction(&luaopen_io)),
        new(LUA_MATHLIBNAME, CFunction.FromFunction(&luaopen_math)),
        new(LUA_OSLIBNAME, CFunction.FromFunction(&luaopen_os)),
        new(LUA_STRLIBNAME, CFunction.FromFunction(&luaopen_string)),
        new(LUA_TABLIBNAME, CFunction.FromFunction(&luaopen_table)),
        new(LUA_UTF8LIBNAME, CFunction.FromFunction(&luaopen_utf8)),
    ];

    /// <summary>
    /// open all libraries
    /// </summary>
    public static void luaL_openlibs(lua_State* L)
    {
        luaL_openselectedlibs(L, ~0, 0);
    }

    /// <summary>
    /// require and preload selected standard libraries
    /// </summary>
    public static void luaL_openselectedlibs(lua_State* L, int load, int preload)
    {
        int mask;
        Span<luaL_Reg> lib;
        luaL_getsubtable(L, LUA_REGISTRYINDEX, LUA_PRELOAD_TABLE);
        for (lib = stdlibs.AsSpan(), mask = 1; !lib.IsEmpty; lib = lib[1..], mask <<= 1)
        {
            if ((load & mask) != 0)
            {
                // selected?
                luaL_requiref(L, lib[0].name, lib[0].func, true); // require library
                lua_pop(L, 1); // remove result from the stack
            }
            else if ((preload & mask) != 0)
            {
                // selected?
                lua_pushcfunction(L, lib[0].func);
                lua_setfield(L, -2, lib[0].name); // add library to PRELOAD table
            }
        }

        Debug.Assert(mask >> 1 == LUA_UTF8LIBK);
        lua_pop(L, 1); // remove PRELOAD table
    }
}
