namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /* version suffix for environment variable names */
    private static readonly string LUA_VERSUFFIX = $"_{LUA_VERSION_MAJOR_N}_{LUA_VERSION_MINOR_N}";

    private const int LUA_GLIBK = 1;
    private static partial int luaopen_base(lua_State* L);

    private const string LUA_LOADLIBNAME = "package";
    private const int LUA_LOADLIBK = LUA_GLIBK << 1;
    private static partial int luaopen_package(lua_State* L);

    private const string LUA_COLIBNAME = "coroutine";
    private const int LUA_COLIBK = LUA_LOADLIBK << 1;
    private static partial int luaopen_coroutine(lua_State* L);

    private const string LUA_DBLIBNAME = "debug";
    private const int LUA_DBLIBK = LUA_COLIBK << 1;
    private static partial int luaopen_debug(lua_State* L);

    private const string LUA_IOLIBNAME = "io";
    private const int LUA_IOLIBK = LUA_DBLIBK << 1;
    private static partial int luaopen_io(lua_State* L);

    private const string LUA_MATHLIBNAME = "math";
    private const int LUA_MATHLIBK = LUA_IOLIBK << 1;
    private static partial int luaopen_math(lua_State* L);

    private const string LUA_OSLIBNAME = "os";
    private const int LUA_OSLIBK = LUA_MATHLIBK << 1;
    private static partial int luaopen_os(lua_State* L);

    private const string LUA_STRLIBNAME = "string";
    private const int LUA_STRLIBK = LUA_OSLIBK << 1;
    private static partial int luaopen_string(lua_State* L);

    private const string LUA_TABLIBNAME = "table";
    private const int LUA_TABLIBK = LUA_STRLIBK << 1;
    private static partial int luaopen_table(lua_State* L);

    private const string LUA_UTF8LIBNAME = "utf8";
    private const int LUA_UTF8LIBK = LUA_TABLIBK << 1;
    private static partial int luaopen_utf8(lua_State* L);

    /* open selected libraries */
    public static partial void luaL_openselectedlibs(lua_State* L, int load, int preload);

    /* open all libraries */
    public static void luaL_openlibs(lua_State* L)
    {
        luaL_openselectedlibs(L, ~0, 0);
    }
}
