namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Globalization;

public static unsafe partial class Lua
{
//
// {==================================================================
// List of valid conversion specifiers for the 'strftime' function;
// options are grouped by length; group of length 2 start with '||'.
// ===================================================================
//
// #if !defined(LUA_STRFTIMEOPTIONS) // {
//
// #if defined(LUA_USE_WINDOWS)
// #define LUA_STRFTIMEOPTIONS  "aAbBcdHIjmMpSUwWxXyYzZ%" \
// "||" "#c#x#d#H#I#j#m#M#S#U#w#W#y#Y" // two-char options
// #elif defined(LUA_USE_C89) // C89 (only 1-char options)
// #define LUA_STRFTIMEOPTIONS  "aAbBcdHIjmMpSUwWxXyYZ%"
// #else // C99 specification
// #define LUA_STRFTIMEOPTIONS  "aAbBcCdDeFgGhHIjmMnprRStTuUVwWxXyYzZ%" \
// "||" "EcECExEXEyEY" "OdOeOHOIOmOMOSOuOUOVOwOWOy" // two-char options
// #endif
//
// #endif // }
// }==================================================================
//
//
//
// {==================================================================
// Configuration for time-related stuff
// ===================================================================
//

// #define l_timet			long
// #define l_gettime(L,arg)	luaL_checkinteger(L, arg)
//
// #if !defined(l_gmtime) // {
//
// By default, Lua uses gmtime/localtime, except when POSIX is available,
// where it uses gmtime_r/localtime_r
//
//
// #if defined(LUA_USE_POSIX) // {
//
// #define l_gmtime(t,r)		gmtime_r(t,r)
// #define l_localtime(t,r)	localtime_r(t,r)
//
// #else // }{
//
// ISO C definitions
// #define l_gmtime(t,r)		((void)(r)->tm_sec, gmtime(t))
// #define l_localtime(t,r)	((void)(r)->tm_sec, localtime(t))
//
// #endif // }
//
// #endif // }

    private static int os_execute(lua_State* L)
    {
        string? cmd = luaL_optnetstring(L, 1, null);
        if (string.IsNullOrEmpty(cmd))
        {
            lua_pushboolean(L, true); // true if there is a shell
            return 1;
        }

        bool isWindows = OperatingSystem.IsWindows();

        Process? process = null;
        try
        {
            process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd.exe" : "/bin/sh",
                    Arguments = isWindows ? $"/c {cmd}" : $"-c \"{cmd}\"",
                    
                    UseShellExecute = true,
                });
            
            process!.WaitForExit();
            
            return luaL_execresult(L, process.ExitCode);
        }
        catch (Exception e)
        {
            return luaL_execresult(L, process?.ExitCode ?? -1, e);
        }
    }

    private static int os_remove(lua_State* L)
    {
        string filename = luaL_checknetstring(L, 1);
        try
        {
            File.Delete(filename);
        }
        catch (Exception e)
        {
            return luaL_fileresult(L, false, filename, e);
        }

        return luaL_fileresult(L, true, filename, null);
    }

    private static int os_rename(lua_State* L)
    {
        string fromname = luaL_checknetstring(L, 1);
        string toname = luaL_checknetstring(L, 2);

        try
        {
            File.Move(fromname, toname);
        }
        catch (Exception e)
        {
            return luaL_fileresult(L, false, null, e);
        }
        
        return luaL_fileresult(L, true, null, null);
    }

    private static int os_tmpname(lua_State* L)
    {
        string buff = Path.GetTempFileName();
        lua_pushstring(L, buff);
        return 1;
    }

    private static int os_getenv(lua_State* L)
    {
        string variableName = luaL_checknetstring(L, 1);
        string? value = Environment.GetEnvironmentVariable(variableName);
        lua_pushstring(L, value); // if null push nil
        return 1;
    }

    private static int os_clock(lua_State* L)
    {
        lua_pushnumber(L, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        return 1;
    }

//
// {======================================================
// Time/Date operations
// { year=%Y, month=%m, day=%d, hour=%H, min=%M, sec=%S,
// wday=%w+1, yday=%j, isdst=? }
// =======================================================
//
//
//
// About the overflow check: an overflow cannot occur when time
// is represented by a long, because either long is
// large enough to represent all int fields or it is not large enough
// to represent a time that cause a field to overflow.  However, if
// times are represented as doubles and long is int, then the
// time 0x1.e1853b0d184f6p+55 would cause an overflow when adding 1900
// to compute the year.
//
// static void setfield (lua_State *L, const char *key, int value, int delta) {
// #if (defined(LUA_NUMTIME) && LUA_MAXINTEGER <= INT_MAX)
// if (l_unlikely(value > LUA_MAXINTEGER - delta))
// luaL_error(L, "field '%s' is out-of-bound", key);
// #endif
// lua_pushinteger(L, (long)value + delta);
// lua_setfield(L, -2, key);
// }
//
//
// static void setboolfield (lua_State *L, const char *key, int value) {
// if (value < 0) // undefined?
// return; // does not set field
// lua_pushboolean(L, value);
// lua_setfield(L, -2, key);
// }
//
//
//
// Set all fields from structure 'tm' in the table on top of the stack
//
// static void setallfields (lua_State *L, struct tm *stm) {
// setfield(L, "year", stm->tm_year, 1900);
// setfield(L, "month", stm->tm_mon, 1);
// setfield(L, "day", stm->tm_mday, 0);
// setfield(L, "hour", stm->tm_hour, 0);
// setfield(L, "min", stm->tm_min, 0);
// setfield(L, "sec", stm->tm_sec, 0);
// setfield(L, "yday", stm->tm_yday, 1);
// setfield(L, "wday", stm->tm_wday, 1);
// setboolfield(L, "isdst", stm->tm_isdst);
// }
//
//
// static int getboolfield (lua_State *L, const char *key) {
// int res;
// res = (lua_getfield(L, -1, key) == LUA_TNIL) ? -1 : lua_toboolean(L, -1);
// lua_pop(L, 1);
// return res;
// }
//
//
// static int getfield (lua_State *L, const char *key, int d, int delta) {
// int isnum;
// int t = lua_getfield(L, -1, key); // get field and its type
// long res = lua_tointegerx(L, -1, &isnum);
// if (!isnum) { // field is not an integer?
// if (l_unlikely(t != LUA_TNIL)) // some other value?
// return luaL_error(L, "field '%s' is not an integer", key);
// else if (l_unlikely(d < 0)) // absent field; no default?
// return luaL_error(L, "field '%s' missing in date table", key);
// res = d;
// }
// else {
// if (!(res >= 0 ? res - delta <= INT_MAX : INT_MIN + delta <= res))
// return luaL_error(L, "field '%s' is out-of-bound", key);
// res -= delta;
// }
// lua_pop(L, 1);
// return (int)res;
// }
//
//
// static const char *checkoption (lua_State *L, const char *conv,
// size_t convlen, char *buff) {
// const char *option = LUA_STRFTIMEOPTIONS;
// unsigned oplen = 1; // length of options being checked
// for (; *option != '\0' && oplen <= convlen; option += oplen) {
// if (*option == '|') // next block?
// oplen++; // will check options with next length (+1)
// else if (memcmp(conv, option, oplen) == 0) { // match?
// memcpy(buff, conv, oplen); // copy valid option to buffer
// buff[oplen] = '\0';
// return conv + oplen; // return next item
// }
// }
// luaL_argerror(L, 1,
// lua_pushfstring(L, "invalid conversion specifier '%%%s'", conv));
// return conv; // to avoid warnings
// }
//
//
// static time_t l_checktime (lua_State *L, int arg) {
// l_timet t = l_gettime(L, arg);
// luaL_argcheck(L, (time_t)t == t, arg, "time out-of-bounds");
// return (time_t)t;
// }
//
//
// maximum size for an individual 'strftime' item
// #define SIZETIMEFMT	250

    private static int os_date(lua_State* L)
    {
// size_t slen;
// const char *s = luaL_optlstring(L, 1, "%c", &slen);
// time_t t = luaL_opt(L, l_checktime, 2, time(null));
// const char *se = s + slen; // 's' end
// struct tm tmr, *stm;
// if (*s == '!') { // UTC?
// stm = l_gmtime(&t, &tmr);
// s++; // skip '!'
// }
// else
// stm = l_localtime(&t, &tmr);
// if (stm == null) // invalid date?
// return luaL_error(L,
// "date result cannot be represented in this installation");
// if (strcmp(s, "*t") == 0) {
// lua_createtable(L, 0, 9); // 9 = number of fields
// setallfields(L, stm);
// }
// else {
// char cc[4]; // buffer for individual conversion specifiers
// luaL_Buffer b;
// cc[0] = '%';
// luaL_buffinit(L, &b);
// while (s < se) {
// if (*s != '%') // not a conversion specifier?
// luaL_addchar(&b, *s++);
// else {
// size_t reslen;
// char *buff = luaL_prepbuffsize(&b, SIZETIMEFMT);
// s++; // skip '%'
// copy specifier to 'cc'
// s = checkoption(L, s, ct_diff2sz(se - s), cc + 1);
// reslen = strftime(buff, SIZETIMEFMT, cc, stm);
// luaL_addsize(&b, reslen);
// }
// }
// luaL_pushresult(&b);
// }
// return 1;
        throw new NotImplementedException();
    }

    private static int os_time(lua_State* L)
    {
        long t;
        if (lua_isnoneornil(L, 1)) // called without args?
        {
            t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        else
        {
// struct tm ts;
// luaL_checktype(L, 1, LUA_TTABLE);
// lua_settop(L, 1); // make sure table is at the top
// ts.tm_year = getfield(L, "year", -1, 1900);
// ts.tm_mon = getfield(L, "month", -1, 1);
// ts.tm_mday = getfield(L, "day", -1, 0);
// ts.tm_hour = getfield(L, "hour", 12, 0);
// ts.tm_min = getfield(L, "min", 0, 0);
// ts.tm_sec = getfield(L, "sec", 0, 0);
// ts.tm_isdst = getboolfield(L, "isdst");
// t = mktime(&ts);
// setallfields(L, &ts); // update fields with normalised values

// if (t != (time_t)(l_timet)t || t == (time_t)(-1))
// return luaL_error(L,
// "time result cannot be represented in this installation");
            throw new NotImplementedException();
        }

        lua_pushinteger(L, t);
        return 1;
    }

    private static int os_difftime(lua_State* L)
    {
// time_t t1 = l_checktime(L, 1);
// time_t t2 = l_checktime(L, 2);
// lua_pushnumber(L, (double)difftime(t1, t2));
// return 1;
        throw new NotImplementedException();
    }

    // private static readonly string[] cat =
    // [
    // LC_ALL, LC_COLLATE, LC_CTYPE, LC_MONETARY,
    // LC_NUMERIC, LC_TIME,
    // ];

    private static readonly string[] catnames =
    [
        "all", "collate", "ctype", "monetary",
        "numeric", "time",
    ];
    
    private static int os_setlocale(lua_State* L)
    {
        string? l = luaL_optnetstring(L, 1, null);
        int op = luaL_checkoption(L, 2, "all", catnames);

        if (l is null or "C")
        {
            lua_pushstring(L, "C"u8);
            return 1;
        }

        // TODO: We disable culture support for now.
        lua_pushnil(L);
        return 1;

        // if (l == "")
        // {
        // l = savedCulture!.Name;
        // }
        //
        // CultureInfo culture;
        // try
        // {
        // culture = CultureInfo.GetCultureInfo(l);
        // }
        // catch (CultureNotFoundException)
        // {
        // lua_pushnil(L);
        // return 1;
        // }
        //
        // if (op != 0)
        // {
        // throw new NotImplementedException();
        // }
        //
        // CultureInfo.CurrentCulture = culture;
        // lua_pushstring(L, l);
        // return 1;
    }

    private static int os_exit(lua_State* L)
    {
// int status;
// if (lua_isboolean(L, 1))
// status = (lua_toboolean(L, 1) ? EXIT_SUCCESS : EXIT_FAILURE);
// else
// status = (int)luaL_optinteger(L, 1, EXIT_SUCCESS);
// if (lua_toboolean(L, 2))
// lua_close(L);
// if (L) exit(status); // 'if' to avoid warnings for unreachable 'return'
// return 0;
        throw new NotImplementedException();
    }

    private static readonly luaL_Reg[] syslib =
    [
        new("clock", &os_clock),
        new("date", &os_date),
        new("difftime", &os_difftime),
        new("execute", &os_execute),
        new("exit", &os_exit),
        new("getenv", &os_getenv),
        new("remove", &os_remove),
        new("rename", &os_rename),
        new("setlocale", &os_setlocale),
        new("time", &os_time),
        new("tmpname", &os_tmpname),
    ];

    // }======================================================

    public static int luaopen_os(lua_State* L)
    {
        luaL_newlib(L, syslib);
        return 1;
    }
}
