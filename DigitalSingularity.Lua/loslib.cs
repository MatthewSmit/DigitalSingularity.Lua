namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Globalization;
using System.Text;

public static unsafe partial class Lua
{
    // {==================================================================
    // Configuration for time-related stuff
    // ===================================================================

    private static long l_gettime(lua_State* L, int arg)
    {
        return luaL_checkinteger(L, arg);
    }

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
            if (!File.Exists(filename))
            {
                return luaL_fileresult(L, false, filename, null);
            }
            
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
        string tempFileName = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        lua_pushstring(L, tempFileName);
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

    // {======================================================
    // Time/Date operations
    // { year=%Y, month=%m, day=%d, hour=%H, min=%M, sec=%S,
    // wday=%w+1, yday=%j, isdst=? }
    // =======================================================

    // About the overflow check: an overflow cannot occur when time
    // is represented by a long, because either long is
    // large enough to represent all int fields or it is not large enough
    // to represent a time that cause a field to overflow.  However, if
    // times are represented as doubles and long is int, then the
    // time 0x1.e1853b0d184f6p+55 would cause an overflow when adding 1900
    // to compute the year.

    private static void setfield(lua_State* L, string key, int value)
    {
        lua_pushinteger(L, value);
        lua_setfield(L, -2, key);
    }

    private static void setboolfield(lua_State* L, string key, bool value)
    {
        lua_pushboolean(L, value);
        lua_setfield(L, -2, key);
    }

    /// <summary>
    /// Set all fields from structure 'tm' in the table on top of the stack.
    /// </summary>
    private static void setallfields(lua_State* L, DateTimeOffset stm, bool isUtc)
    {
        setfield(L, "year", stm.Year);
        setfield(L, "month", stm.Month);
        setfield(L, "day", stm.Day);
        setfield(L, "hour", stm.Hour);
        setfield(L, "min", stm.Minute);
        setfield(L, "sec", stm.Second);
        setfield(L, "yday", stm.DayOfYear);
        setfield(L, "wday", (int)stm.DayOfWeek + 1);

        TimeZoneInfo zone = isUtc ? TimeZoneInfo.Utc : TimeZoneInfo.Local;
        setboolfield(L, "isdst", zone.IsDaylightSavingTime(stm));
    }

    private static int getboolfield(lua_State* L, string key)
    {
        int res = lua_getfield(L, -1, key) == LUA_TNIL
            ? -1
            : lua_toboolean(L, -1)
                ? 1
                : 0;
        lua_pop(L, 1);
        return res;
    }

    private static int getfield(lua_State* L, string key, int d)
    {
        int t = lua_getfield(L, -1, key); // get field and its type
        long res = lua_tointegerx(L, -1, out bool isnum);
        if (!isnum)
        {
            // field is not an integer?
            if (t != LUA_TNIL) // some other value?
            {
                return luaL_error(L, "field '%s' is not an integer", key);
            }

            if (d < 0) // absent field; no default?
            {
                return luaL_error(L, "field '%s' missing in date table", key);
            }

            res = d;
        }

        if (res is < int.MinValue or > int.MaxValue)
        {
            return luaL_error(L, "field '%s' is out-of-bound", key);
        }

        lua_pop(L, 1);
        return (int)res;
    }

    private static DateTimeOffset l_checktime(lua_State* L, int arg)
    {
        long t = l_gettime(L, arg);
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(t);
        }
        catch (ArgumentOutOfRangeException)
        {
            luaL_argcheck(L, false, arg, "time out-of-bounds");
            throw;
        }
    }

    private static int os_date(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_optlstring(L, 1, "%c"u8);
        DateTimeOffset t = lua_isnoneornil(L, 2) ? DateTimeOffset.UtcNow : l_checktime(L, 2);

        bool isUtc = !s.IsEmpty && s[0] == '!';
        DateTimeOffset stm;
        if (isUtc)
        {
            // UTC?
            stm = t.ToUniversalTime();
            s = s[1..];
        }
        else
        {
            stm = t.ToLocalTime();
        }

        if (s.StartsWith("*t"u8))
        {
            lua_createtable(L, 0, 9); // 9 = number of fields
            setallfields(L, stm, isUtc);
        }
        else
        {
            luaL_Buffer b;
            luaL_buffinit(L, &b);
            while (!s.IsEmpty)
            {
                if (!s.StartsWith((byte)'%')) // not a conversion specifier?
                {
                    luaL_addchar(&b, s[0]);
                    s = s[1..];
                }
                else
                {
                    Span<byte> buff = new(luaL_prepbuffsize(&b, 250), 250);
                    int reslen = strftime(L, buff, ref s, stm, isUtc);
                    luaL_addsize(&b, reslen);
                }
            }

            luaL_pushresult(&b);
        }

        return 1;
    }

    internal static int strftime(
        lua_State* L,
        Span<byte> output,
        ref ReadOnlySpan<byte> input,
        DateTimeOffset time,
        bool isUtc = true)
    {
        if (input.Length < 2 || input[0] != '%')
        {
            luaL_argerror(L, 1, lua_pushfstring(L, "invalid conversion specifier '%%%s'", Encoding.UTF8.GetString(input)));
        }

        string c = ((char)input[1]).ToString();
        input = input[2..];

        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo alternateCulture = culture;
        DateTimeFormatInfo format = culture.DateTimeFormat;
        if (format.Calendar is not GregorianCalendar)
        {
            format = (DateTimeFormatInfo)format.Clone();
            format.Calendar = new GregorianCalendar();
            culture = (CultureInfo)culture.Clone();
            culture.DateTimeFormat = format;
        }
        else
        {
            Calendar? otherCalendar = culture
                .OptionalCalendars
                .FirstOrDefault(e => e is not GregorianCalendar);
            if (otherCalendar != null)
            {
                DateTimeFormatInfo alternateFormat = (DateTimeFormatInfo)format.Clone();
                alternateFormat.Calendar = otherCalendar;
                alternateCulture = (CultureInfo)alternateCulture.Clone();
                alternateCulture.DateTimeFormat = alternateFormat;
            }
        }

        if (!input.IsEmpty && c is "O" or "E")
        {
            c += (char)input[0];
            input = input[1..];
        }

        string? outStr = c switch
        {
            "%" => "%",
            "n" => "\n",
            "t" => "\t",

            // Year
            "Y" => time.ToString("yyyy", culture),
            "EY" => time.ToString(
                "yyyy",
                alternateCulture), // TODO: this isn't technically correct, it should include the era too
            "y" => time.ToString("yy", culture),
            "Oy" => AlternateDigits(time.ToString("yy", culture)),
            "Ey" => time.ToString("yy", alternateCulture),
            "C" => (time.Year / 100).ToString("D2", culture),
            "EC" => time.ToString("gg", alternateCulture),
            "G" => ISOWeek.GetYear(time.Date).ToString("D4", culture),
            "g" => (ISOWeek.GetYear(time.Date) % 100).ToString("D2", culture),

            // Month
            "b" or "h" => format.AbbreviatedMonthNames[time.Month - 1],
            "Ob" => format.AbbreviatedMonthGenitiveNames[time.Month - 1],
            "B" => format.MonthNames[time.Month - 1],
            "OB" => format.MonthGenitiveNames[time.Month - 1],
            "m" => time.ToString("MM", culture),
            "Om" => AlternateDigits(time.ToString("MM", culture)),

            // Week
            "U" => ((time.DayOfYear + 6 - (int)time.DayOfWeek) / 7).ToString("D2", culture),
            "OU" => AlternateDigits(((time.DayOfYear + 6 - (int)time.DayOfWeek) / 7).ToString("D2", culture)),
            "W" => ((time.DayOfYear + 6 - ((int)time.DayOfWeek + 6) % 7) / 7).ToString("D2", culture),
            "OW" => AlternateDigits(((time.DayOfYear + 6 - ((int)time.DayOfWeek + 6) % 7) / 7).ToString("D2", culture)),
            "V" => ISOWeek.GetWeekOfYear(time.Date).ToString("D2", culture),
            "OV" => AlternateDigits(ISOWeek.GetWeekOfYear(time.Date).ToString("D2", culture)),
            "j" => time.DayOfYear.ToString("D3", culture),
            "d" => time.ToString("dd", culture),
            "Od" => AlternateDigits(time.ToString("dd", culture)),
            "e" => time.Day.ToString(culture).PadLeft(2, ' '),
            "Oe" => AlternateDigits(time.Day.ToString(culture).PadLeft(2, ' ')),

            // Day of the week
            "a" => time.ToString("ddd", culture),
            "A" => time.ToString("dddd", culture),
            "w" => ((int)time.DayOfWeek).ToString(culture),
            "Ow" => AlternateDigits(((int)time.DayOfWeek).ToString(culture)),
            "u" => (time.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)time.DayOfWeek).ToString(culture),
            "Ou" => AlternateDigits((time.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)time.DayOfWeek).ToString(culture)),

            // Hour, minute, second
            "H" => time.ToString("HH", culture),
            "OH" => AlternateDigits(time.ToString("HH", culture)),
            "I" => time.ToString("hh", culture),
            "OI" => AlternateDigits(time.ToString("hh", culture)),
            "M" => time.ToString("mm", culture),
            "OM" => AlternateDigits(time.ToString("mm", culture)),
            "S" => time.ToString("ss", culture),
            "OS" => AlternateDigits(time.ToString("ss", culture)),

            // Other
            "c" => time.ToString("F", culture),
            "Ec" => time.ToString("F", alternateCulture),
            "x" => time.ToString("d", culture),
            "Ex" => time.ToString("d", alternateCulture),
            "X" => time.ToString("T", culture),
            "EX" => time.ToString("T", alternateCulture),
            "D" => time.ToString("MM'/'dd'/'yy", CultureInfo.InvariantCulture), // Whoever uses this is just wrong
            "F" => time.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture),
            "r" => time.ToString("T", culture),
            "R" => time.ToString("HH':'mm", culture),
            "T" => time.ToString("HH':'mm':'ss", culture),
            "p" => time.ToString("tt", culture),
            "z" => OffsetHHmm(time),
            "Z" => isUtc ? "UTC" : TimeZoneInfo.Local.DisplayName,
            
            _ => null,
        };

        if (outStr == null)
        {
            luaL_argerror(L, 1, lua_pushfstring(L, "invalid conversion specifier '%%%s'", c));
        }

        return Encoding.UTF8.GetBytes(outStr, output);
        
        string AlternateDigits(string value)
        {
            string[] digits = culture.NumberFormat.NativeDigits;
            if (culture.NumberFormat.NativeDigits[1] == "1")
            {
                return value;
            }
            
            StringBuilder result = new(value.Length);

            foreach (char ch in value)
            {
                if (ch is >= '0' and <= '9')
                {
                    result.Append(digits[ch - '0']);
                }
                else
                {
                    result.Append(ch);
                }
            }

            return result.ToString();
        }
        
        string OffsetHHmm(DateTimeOffset value)
        {
            int totalMinutes = checked((int)value.Offset.TotalMinutes);
            int absolute = Math.Abs(totalMinutes);

            return string.Concat(
                totalMinutes < 0 ? "-" : "+",
                (absolute / 60).ToString("D2", culture),
                (absolute % 60).ToString("D2", culture));
        }
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
            luaL_checktype(L, 1, LUA_TTABLE);
            lua_settop(L, 1); // make sure table is at the top
            int year = getfield(L, "year", -1);
            int month = getfield(L, "month", -1);
            int day = getfield(L, "day", -1);
            int hour = getfield(L, "hour", 12);
            int minute = getfield(L, "min", 0);
            int second = getfield(L, "sec", 0);
            int isdst = getboolfield(L, "isdst");

            DateTime ts;
            DateTimeOffset tso;
            try
            {
                ts = TryCreateDateTime(year, month, day, hour, minute, second);
            }
            catch (ArgumentOutOfRangeException)
            {
                return luaL_error(L, "time result cannot be represented in this installation");
            }

            TimeZoneInfo timeZone = TimeZoneInfo.Local;
            if (isdst < 1 || !timeZone.IsAmbiguousTime(ts))
            {
                tso = new DateTimeOffset(ts, timeZone.GetUtcOffset(ts));
            }
            else
            {
                TimeSpan[] offsets = timeZone.GetAmbiguousTimeOffsets(ts);
                TimeSpan otherOffset = offsets
                    .First(o => o != timeZone.BaseUtcOffset);
                tso = new DateTimeOffset(ts, otherOffset);
            }

            setallfields(L, tso, false); // update fields with normalised values

            t = tso.ToUnixTimeSeconds();
        }

        lua_pushinteger(L, t);
        return 1;
    }

    private static DateTime TryCreateDateTime(int year, int month, int day, int hour, int minute, int second)
    {
        DateTime dt = new(year, 1, 1);
        dt = dt.AddMonths(month - 1);
        dt = dt.AddDays(day - 1);
        dt = dt.AddHours(hour);
        dt = dt.AddMinutes(minute);
        dt = dt.AddSeconds(second);
        return dt;
    }

    private static int os_difftime(lua_State* L)
    {
        DateTimeOffset t1 = l_checktime(L, 1);
        DateTimeOffset t2 = l_checktime(L, 2);
        lua_pushnumber(L, (t1 - t2).TotalSeconds);
        return 1;
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
        int status;
        if (lua_isboolean(L, 1))
        {
            status = lua_toboolean(L, 1) ? 0 : 1;
        }
        else
        {
            status = (int)luaL_optinteger(L, 1, 0);
        }

        if (lua_toboolean(L, 2))
        {
            lua_close(L);
        }

        Environment.Exit(status);
        return 0;
    }

    private static readonly luaL_Reg[] syslib =
    [
        new("clock", CFunction.FromFunction(&os_clock)),
        new("date", CFunction.FromFunction(&os_date)),
        new("difftime", CFunction.FromFunction(&os_difftime)),
        new("execute", CFunction.FromFunction(&os_execute)),
        new("exit", CFunction.FromFunction(&os_exit)),
        new("getenv", CFunction.FromFunction(&os_getenv)),
        new("remove", CFunction.FromFunction(&os_remove)),
        new("rename", CFunction.FromFunction(&os_rename)),
        new("setlocale", CFunction.FromFunction(&os_setlocale)),
        new("time", CFunction.FromFunction(&os_time)),
        new("tmpname", CFunction.FromFunction(&os_tmpname)),
    ];

    // }======================================================

    public static int luaopen_os(lua_State* L)
    {
        luaL_newlib(L, syslib);
        return 1;
    }
}
