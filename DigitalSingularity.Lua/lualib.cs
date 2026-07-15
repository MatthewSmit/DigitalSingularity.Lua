namespace DigitalSingularity.Lua;

public static partial class Lua
{
    /// <summary>
    /// version suffix for environment variable names
    /// </summary>
    public static readonly string LUA_VERSUFFIX = $"_{LUA_VERSION_MAJOR_N}_{LUA_VERSION_MINOR_N}";

    private const int LUA_GLIBK = 1;

    private const string LUA_LOADLIBNAME = "package";
    private const int LUA_LOADLIBK = LUA_GLIBK << 1;

    private const string LUA_COLIBNAME = "coroutine";
    private const int LUA_COLIBK = LUA_LOADLIBK << 1;

    private const string LUA_DBLIBNAME = "debug";
    private const int LUA_DBLIBK = LUA_COLIBK << 1;

    private const string LUA_IOLIBNAME = "io";
    private const int LUA_IOLIBK = LUA_DBLIBK << 1;

    public const string LUA_MATHLIBNAME = "math";
    public const int LUA_MATHLIBK = LUA_IOLIBK << 1;

    private const string LUA_OSLIBNAME = "os";
    private const int LUA_OSLIBK = LUA_MATHLIBK << 1;

    public const string LUA_STRLIBNAME = "string";
    public const int LUA_STRLIBK = LUA_OSLIBK << 1;

    private const string LUA_TABLIBNAME = "table";
    private const int LUA_TABLIBK = LUA_STRLIBK << 1;

    private const string LUA_UTF8LIBNAME = "utf8";
    private const int LUA_UTF8LIBK = LUA_TABLIBK << 1;
}
