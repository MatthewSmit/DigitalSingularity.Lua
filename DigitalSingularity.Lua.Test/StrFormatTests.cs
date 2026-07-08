namespace DigitalSingularity.Lua.Test;

using System.Globalization;
using System.Text;
using static DigitalSingularity.Lua.Lua;

#pragma warning disable NUnit2045

public unsafe class StrFormatTests
{
    private static string StackString(lua_State* l, int idx, Encoding encoding)
    {
        byte* x = lua_tolstring(l, idx, out int len);
        Span<byte> s = new(x, len);
        return encoding.GetString(s);
    }

    private static string LuaFormatSource(string format, string argument)
    {
        return $"return string.format('{format}', {argument})";
    }

    private static string MakeFormat(string flag, string width, string precision, char specifier)
    {
        return "%" + flag + width + precision + specifier;
    }

    [SetUp]
    public void SetUp()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private static void ExpectLuaStringResult(lua_State* L, string source, string expected)
    {
        lua_settop(L, 0);
        Assert.That(luaL_loadstring(L, source), Is.EqualTo(LUA_OK), () => StackString(L, -1, Encoding.UTF8));
        Assert.That(lua_pcall(L, 0, 1, 0), Is.EqualTo(LUA_OK), () => StackString(L, -1, Encoding.UTF8));
        Assert.That(
            lua_type(L, -1),
            Is.EqualTo(LUA_TSTRING),
            () => lua_typename(L, lua_type(L, -1)));
        Assert.That(StackString(L, -1, Encoding.Latin1), Is.EqualTo(expected));
        lua_settop(L, 0);
    }

    private static void ExpectLuaBoolResult(lua_State* L, string source, bool expected)
    {
        lua_settop(L, 0);
        Assert.That(luaL_loadstring(L, source), Is.EqualTo(LUA_OK), () => StackString(L, -1, Encoding.UTF8));
        Assert.That(lua_pcall(L, 0, 1, 0), Is.EqualTo(LUA_OK), () => StackString(L, -1, Encoding.UTF8));
        Assert.That(lua_toboolean(L, -1), Is.EqualTo(expected));
        lua_settop(L, 0);
    }

    private static void ExpectLuaRuntimeError(lua_State* L, string source, string expectedMessage)
    {
        lua_settop(L, 0);
        Assert.That(luaL_loadstring(L, source), Is.EqualTo(LUA_OK), () => StackString(L, -1, Encoding.UTF8));
        Assert.That(lua_pcall(L, 0, 1, 0), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(L, -1, Encoding.UTF8), Contains.Substring(expectedMessage));
        lua_settop(L, 0);
    }

    [TestCase("%d", "42", "42")]
    [TestCase("%6d", "42", "    42")]
    [TestCase("%12.0d", "42", "          42")]
    [TestCase("%6.3d", "42", "   042")]
    [TestCase("%-d", "42", "42")]
    [TestCase("%-6d", "42", "42    ")]
    [TestCase("%-12.0d", "42", "42          ")]
    [TestCase("%-6.3d", "42", "042   ")]
    [TestCase("%d", "-1", "-1")]
    [TestCase("%.3d", "1", "001")]
    [TestCase("%.3d", "-1", "-001")]
    [TestCase("%6.3d", "-1", "  -001")]
    [TestCase("%+d", "42", "+42")]
    [TestCase("%+6d", "42", "   +42")]
    [TestCase("%+12.0d", "42", "         +42")]
    [TestCase("%+6.3d", "42", "  +042")]
    [TestCase("%0d", "42", "42")]
    [TestCase("%d", "-17.0","-17")]
    [TestCase("%d", "-0.0", "0")]
    [TestCase("%06d", "42", "000042")]
    [TestCase("%012.0d", "42", "          42")]
    [TestCase("%06.3d", "42", "   042")]
    [TestCase("% d", "42", " 42")]
    [TestCase("% 6d", "42", "    42")]
    [TestCase("% 12.0d", "42", "          42")]
    [TestCase("% 6.3d", "42", "   042")]
    [TestCase("%+08d", "-30927", "-0030927")]
    [TestCase("%2.5d", "-100", "-00100")]
    [TestCase("%.0d", "0", "")]
    [TestCase("%d", "-42", "-42")]
    [TestCase("%+d", "0", "+0")]
    [TestCase("% d", "0", " 0")]
    [TestCase("%8.5d", "-42", "  -00042")]
    [TestCase("%020d", "-42","-0000000000000000042")]
    [TestCase("%d", "math.maxinteger","9223372036854775807")]
    [TestCase("%d", "math.mininteger","-9223372036854775808")]
    [TestCase("%.20d", "math.mininteger","-09223372036854775808")]
    [TestCase("%020d", "math.maxinteger","09223372036854775807")]
    [TestCase("%21d", "math.mininteger"," -9223372036854775808")]
    [TestCase("%+21d", "math.maxinteger"," +9223372036854775807")]
    [TestCase("%+.0d", "0", "+")]
    [TestCase("% .0d", "0", " ")]
    public void ValidCases_d(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%i", "42", "42")]
    [TestCase("%6i", "42", "    42")]
    [TestCase("%12.0i", "42", "          42")]
    [TestCase("%6.3i", "42", "   042")]
    [TestCase("%-i", "42", "42")]
    [TestCase("%-6i", "42", "42    ")]
    [TestCase("%-12.0i", "42", "42          ")]
    [TestCase("%-6.3i", "42", "042   ")]
    [TestCase("%+i", "42", "+42")]
    [TestCase("%+6i", "42", "   +42")]
    [TestCase("%+12.0i", "42", "         +42")]
    [TestCase("%+6.3i", "42", "  +042")]
    [TestCase("%0i", "42", "42")]
    [TestCase("%06i", "42", "000042")]
    [TestCase("%012.0i", "42", "          42")]
    [TestCase("%06.3i", "42", "   042")]
    [TestCase("% i", "42", " 42")]
    [TestCase("% 6i", "42", "    42")]
    [TestCase("% 12.0i", "42", "          42")]
    [TestCase("% 6.3i", "42", "   042")]
    [TestCase("%i", "math.mininteger","-9223372036854775808")]
    [TestCase("%+i", "math.maxinteger","+9223372036854775807")]
    [TestCase("%.20i", "math.maxinteger","09223372036854775807")]
    public void ValidCases_i(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%u", "42", "42")]
    [TestCase("%6u", "42", "    42")]
    [TestCase("%12.0u", "42", "          42")]
    [TestCase("%6.3u", "42", "   042")]
    [TestCase("%-u", "42", "42")]
    [TestCase("%-6u", "42", "42    ")]
    [TestCase("%-12.0u", "42", "42          ")]
    [TestCase("%-6.3u", "42", "042   ")]
    [TestCase("%0u", "42", "42")]
    [TestCase("%06u", "42", "000042")]
    [TestCase("%012.0u", "42", "          42")]
    [TestCase("%06.3u", "42", "   042")]
    [TestCase("%.u", "0", "")]
    [TestCase("%020u", "0","00000000000000000000")]
    [TestCase("%u", "-2","18446744073709551614")]
    [TestCase("%u", "-1.0","18446744073709551615")]
    [TestCase("%u", "-1","18446744073709551615")]
    [TestCase("%u", "math.mininteger","9223372036854775808")]
    [TestCase("%21u", "-1"," 18446744073709551615")]
    [TestCase("%5.0u", "0", "     ")]
    [TestCase("%21u", "math.maxinteger","  9223372036854775807")]
    [TestCase("%.20u", "math.maxinteger","09223372036854775807")]
    [TestCase("%.21u", "-1","018446744073709551615")]
    public void ValidCases_u(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%o", "100", "144")]
    [TestCase("%6o", "100", "   144")]
    [TestCase("%12.0o", "100", "         144")]
    [TestCase("%6.3o", "100", "   144")]
    [TestCase("%-o", "100", "144")]
    [TestCase("%-6o", "100", "144   ")]
    [TestCase("%-12.0o", "100", "144         ")]
    [TestCase("%-6.3o", "100", "144   ")]
    [TestCase("%#o", "100", "0144")]
    [TestCase("%#6o", "100", "  0144")]
    [TestCase("%#12.0o", "100", "        0144")]
    [TestCase("%#6.3o", "100", "  0144")]
    [TestCase("%0o", "100", "144")]
    [TestCase("%06o", "100", "000144")]
    [TestCase("%012.0o", "100", "         144")]
    [TestCase("%06.3o", "100", "   144")]
    [TestCase("%#.0o", "0", "")]
    [TestCase("%#o", "0", "0")]
    [TestCase("%#o", "math.maxinteger","0777777777777777777777")]
    [TestCase("%#o", "math.mininteger","01000000000000000000000")]
    [TestCase("%o", "-1","1777777777777777777777")]
    [TestCase("%6.0o", "0", "      ")]
    [TestCase("%#o", "255", "0377")]
    [TestCase("%.22o", "-1","1777777777777777777777")]
    public void ValidCases_o(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%x", "100", "64")]
    [TestCase("%6x", "100", "    64")]
    [TestCase("%12.0x", "100", "          64")]
    [TestCase("%6.3x", "100", "   064")]
    [TestCase("%-x", "100", "64")]
    [TestCase("%-6x", "100", "64    ")]
    [TestCase("%-12.0x", "100", "64          ")]
    [TestCase("%-6.3x", "100", "064   ")]
    [TestCase("%#x", "100", "0x64")]
    [TestCase("%#6x", "100", "  0x64")]
    [TestCase("%#12.0x", "100", "        0x64")]
    [TestCase("%#6.3x", "100", " 0x064")]
    [TestCase("%0x", "100", "64")]
    [TestCase("%06x", "100", "000064")]
    [TestCase("%012.0x", "100", "          64")]
    [TestCase("%06.3x", "100", "   064")]
    [TestCase("%#.0x", "0", "")]
    [TestCase("%02x", "0.0", "00")]
    [TestCase("%#x", "0", "0")]
    [TestCase("%#x", "math.maxinteger","0x7fffffffffffffff")]
    [TestCase("%#x", "math.mininteger","0x8000000000000000")]
    [TestCase("%x", "-1", "ffffffffffffffff")]
    [TestCase("%6.0x", "0", "      ")]
    [TestCase("%#018x", "255","0x00000000000000ff")]
    [TestCase("%.18x", "math.maxinteger","007fffffffffffffff")]
    public void ValidCases_x(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%X", "100", "64")]
    [TestCase("%6X", "100", "    64")]
    [TestCase("%12.0X", "100", "          64")]
    [TestCase("%6.3X", "100", "   064")]
    [TestCase("%-X", "100", "64")]
    [TestCase("%-6X", "100", "64    ")]
    [TestCase("%-12.0X", "100", "64          ")]
    [TestCase("%-6.3X", "100", "064   ")]
    [TestCase("%#X", "100", "0X64")]
    [TestCase("%#6X", "100", "  0X64")]
    [TestCase("%#12.0X", "100", "        0X64")]
    [TestCase("%#6.3X", "100", " 0X064")]
    [TestCase("%0X", "100", "64")]
    [TestCase("%06X", "100", "000064")]
    [TestCase("%012.0X", "100", "          64")]
    [TestCase("%06.3X", "100", "   064")]
    [TestCase("%#X", "0", "0")]
    [TestCase("%#X", "math.maxinteger","0X7FFFFFFFFFFFFFFF")]
    [TestCase("%#X", "math.mininteger","0X8000000000000000")]
    [TestCase("%X", "-1", "FFFFFFFFFFFFFFFF")]
    [TestCase("%.18X", "math.maxinteger","007FFFFFFFFFFFFFFF")]
    [TestCase("%#22X", "-1","    0XFFFFFFFFFFFFFFFF")]
    [TestCase("%.18X", "math.maxinteger","007FFFFFFFFFFFFFFF")]
    public void ValidCases_X(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%e", "123.5", "1.235000e+02")]
    [TestCase("%8e", "123.5", "1.235000e+02")]
    [TestCase("%12.0e", "123.5", "       1e+02")]
    [TestCase("%8.3e", "123.5", "1.235e+02")]
    [TestCase("%-e", "123.5", "1.235000e+02")]
    [TestCase("%-8e", "123.5", "1.235000e+02")]
    [TestCase("%-12.0e", "123.5", "1e+02       ")]
    [TestCase("%-8.3e", "123.5", "1.235e+02")]
    [TestCase("%+e", "123.5", "+1.235000e+02")]
    [TestCase("%+8e", "123.5", "+1.235000e+02")]
    [TestCase("%+12.0e", "123.5", "      +1e+02")]
    [TestCase("%+8.3e", "123.5", "+1.235e+02")]
    [TestCase("%#e", "123.5", "1.235000e+02")]
    [TestCase("%#8e", "123.5", "1.235000e+02")]
    [TestCase("%#12.0e", "123.5", "      1.e+02")]
    [TestCase("%#8.3e", "123.5", "1.235e+02")]
    [TestCase("%0e", "123.5", "1.235000e+02")]
    [TestCase("%08e", "123.5", "1.235000e+02")]
    [TestCase("%012.0e", "123.5", "00000001e+02")]
    [TestCase("%08.3e", "123.5", "1.235e+02")]
    [TestCase("% e", "123.5", " 1.235000e+02")]
    [TestCase("% 8e", "123.5", " 1.235000e+02")]
    [TestCase("% 12.0e", "123.5", "       1e+02")]
    [TestCase("% 8.3e", "123.5", " 1.235e+02")]
    [TestCase("%e", "0.0", "0.000000e+00")]
    [TestCase("%e", "-0.0", "-0.000000e+00")]
    [TestCase("%e", "1.7976931348623157e308","1.797693e+308")]
    [TestCase("%e", "2.2250738585072014e-308","2.225074e-308")]
    [TestCase( "%e", "-123.5", "-1.235000e+02")]
    [TestCase( "%e", "-1.7976931348623157e308","-1.797693e+308")]
    [TestCase( "%e","-2.2250738585072014e-308", "-2.225074e-308")]
    [TestCase( "%e", "-math.huge", "-inf")]
    public void ValidCases_e(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%E", "123.5", "1.235000E+02")]
    [TestCase("%8E", "123.5", "1.235000E+02")]
    [TestCase("%12.0E", "123.5", "       1E+02")]
    [TestCase("%8.3E", "123.5", "1.235E+02")]
    [TestCase("%-E", "123.5", "1.235000E+02")]
    [TestCase("%-8E", "123.5", "1.235000E+02")]
    [TestCase("%-12.0E", "123.5", "1E+02       ")]
    [TestCase("%-8.3E", "123.5", "1.235E+02")]
    [TestCase("%+E", "123.5", "+1.235000E+02")]
    [TestCase("%+8E", "123.5", "+1.235000E+02")]
    [TestCase("%+12.0E", "123.5", "      +1E+02")]
    [TestCase("%+8.3E", "123.5", "+1.235E+02")]
    [TestCase("%#E", "123.5", "1.235000E+02")]
    [TestCase("%#8E", "123.5", "1.235000E+02")]
    [TestCase("%#12.0E", "123.5", "      1.E+02")]
    [TestCase("%#8.3E", "123.5", "1.235E+02")]
    [TestCase("%0E", "123.5", "1.235000E+02")]
    [TestCase("%08E", "123.5", "1.235000E+02")]
    [TestCase("%012.0E", "123.5", "00000001E+02")]
    [TestCase("%08.3E", "123.5", "1.235E+02")]
    [TestCase("% E", "123.5", " 1.235000E+02")]
    [TestCase("% 8E", "123.5", " 1.235000E+02")]
    [TestCase("% 12.0E", "123.5", "       1E+02")]
    [TestCase("% 8.3E", "123.5", " 1.235E+02")]
    [TestCase( "%E", "-123.5","-1.235000E+02")]
    [TestCase( "%E", "math.huge", "INF")]
    public void ValidCases_E(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%f", "123.5", "123.500000")]
    [TestCase("%8f", "123.5", "123.500000")]
    [TestCase("%12.0f", "123.5", "         124")]
    [TestCase("%8.3f", "123.5", " 123.500")]
    [TestCase("%-f", "123.5", "123.500000")]
    [TestCase("%-8f", "123.5", "123.500000")]
    [TestCase("%-12.0f", "123.5", "124         ")]
    [TestCase("%-8.3f", "123.5", "123.500 ")]
    [TestCase("%+f", "123.5", "+123.500000")]
    [TestCase("%+8f", "123.5", "+123.500000")]
    [TestCase("%+12.0f", "123.5", "        +124")]
    [TestCase("%+8.3f", "123.5", "+123.500")]
    [TestCase("%#f", "123.5", "123.500000")]
    [TestCase("%#8f", "123.5", "123.500000")]
    [TestCase("%#12.0f", "123.5", "        124.")]
    [TestCase("%#8.3f", "123.5", " 123.500")]
    [TestCase("%0f", "123.5", "123.500000")]
    [TestCase("%08f", "123.5", "123.500000")]
    [TestCase("%012.0f", "123.5", "000000000124")]
    [TestCase("%08.3f", "123.5", "0123.500")]
    [TestCase("% f", "123.5", " 123.500000")]
    [TestCase("% 8f", "123.5", " 123.500000")]
    [TestCase("% 12.0f", "123.5", "         124")]
    [TestCase("% 8.3f", "123.5", " 123.500")]
    [TestCase("%f", "0.0", "0.000000")]
    [TestCase("%f", "-0.0", "-0.000000")]
    [TestCase("%+f", "0.0", "+0.000000")]
    [TestCase("%+f", "-0.0", "-0.000000")]
    [TestCase("% f", "0.0", " 0.000000")]
    [TestCase("% f", "-0.0", "-0.000000")]
    [TestCase("%12.4f", "-123.5","   -123.5000")]
    [TestCase("%012.4f", "-123.5","-000123.5000")]
    [TestCase( "%f", "-123.5", "-123.500000")]
    [TestCase( "%.0f", "-0.5", "-0")]
    public void ValidCases_f(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%g", "123.5", "123.5")]
    [TestCase("%8g", "123.5", "   123.5")]
    [TestCase("%12.0g", "123.5", "       1e+02")]
    [TestCase("%8.3g", "123.5", "     124")]
    [TestCase("%-g", "123.5", "123.5")]
    [TestCase("%-8g", "123.5", "123.5   ")]
    [TestCase("%-12.0g", "123.5", "1e+02       ")]
    [TestCase("%-8.3g", "123.5", "124     ")]
    [TestCase("%+g", "123.5", "+123.5")]
    [TestCase("%+8g", "123.5", "  +123.5")]
    [TestCase("%+12.0g", "123.5", "      +1e+02")]
    [TestCase("%+8.3g", "123.5", "    +124")]
    [TestCase("%#g", "123.5", "123.500")]
    [TestCase("%#8g", "123.5", " 123.500")]
    [TestCase("%#12.0g", "123.5", "      1.e+02")]
    [TestCase("%#8.3g", "123.5", "    124.")]
    [TestCase("%0g", "123.5", "123.5")]
    [TestCase("%08g", "123.5", "000123.5")]
    [TestCase("%012.0g", "123.5", "00000001e+02")]
    [TestCase("%08.3g", "123.5", "00000124")]
    [TestCase("% g", "123.5", " 123.5")]
    [TestCase("% 8g", "123.5", "   123.5")]
    [TestCase("% 12.0g", "123.5", "       1e+02")]
    [TestCase("% 8.3g", "123.5", "     124")]
    [TestCase("%g", "0.0", "0")]
    [TestCase("%g", "-0.0", "-0")]
    [TestCase("%.17g", "1.7976931348623157e308","1.7976931348623157e+308")]
    [TestCase("%.17g","2.2250738585072014e-308", "2.2250738585072014e-308")]
    [TestCase("%.17g", "5e-324","4.9406564584124654e-324")]
    [TestCase("%g", "math.huge", "inf")]
    [TestCase("%g", "-math.huge", "-inf")]
    [TestCase("%g", "0/0", "nan")]
    [TestCase( "%g", "-123.5", "-123.5")]
    [TestCase( "%.3g", "0.00012345", "0.000123")]
    [TestCase( "%.3g", "-0.00012345","-0.000123")]
    [TestCase( "%.4g", "123456789", "1.235e+08")]
    [TestCase( "%.4G", "-123456789","-1.235E+08")]
    [TestCase( "%.17g","-1.7976931348623157e308", "-1.7976931348623157e+308")]
    [TestCase( "%.17g", "-5e-324","-4.9406564584124654e-324")]
    public void ValidCases_g(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%G", "123.5", "123.5")]
    [TestCase("%8G", "123.5", "   123.5")]
    [TestCase("%12.0G", "123.5", "       1E+02")]
    [TestCase("%8.3G", "123.5", "     124")]
    [TestCase("%-G", "123.5", "123.5")]
    [TestCase("%-8G", "123.5", "123.5   ")]
    [TestCase("%-12.0G", "123.5", "1E+02       ")]
    [TestCase("%-8.3G", "123.5", "124     ")]
    [TestCase("%+G", "123.5", "+123.5")]
    [TestCase("%+8G", "123.5", "  +123.5")]
    [TestCase("%+12.0G", "123.5", "      +1E+02")]
    [TestCase("%+8.3G", "123.5", "    +124")]
    [TestCase("%#G", "123.5", "123.500")]
    [TestCase("%#8G", "123.5", " 123.500")]
    [TestCase("%#12.0G", "123.5", "      1.E+02")]
    [TestCase("%#8.3G", "123.5", "    124.")]
    [TestCase("%0G", "123.5", "123.5")]
    [TestCase("%08G", "123.5", "000123.5")]
    [TestCase("%012.0G", "123.5", "00000001E+02")]
    [TestCase("%08.3G", "123.5", "00000124")]
    [TestCase("% G", "123.5", " 123.5")]
    [TestCase("% 8G", "123.5", "   123.5")]
    [TestCase("% 12.0G", "123.5", "       1E+02")]
    [TestCase("% 8.3G", "123.5", "     124")]
    [TestCase( "%G", "-123.5", "-123.5")]
    [TestCase( "%G", "0/0", "NAN")]
    public void ValidCases_G(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%a", "12", "0x1.8p+3")]
    [TestCase("%8a", "12", "0x1.8p+3")]
    [TestCase("%12.0a", "12", "      0x2p+3")]
    [TestCase("%8.3a", "12", "0x1.800p+3")]
    [TestCase("%-a", "12", "0x1.8p+3")]
    [TestCase("%-8a", "12", "0x1.8p+3")]
    [TestCase("%-12.0a", "12", "0x2p+3      ")]
    [TestCase("%-8.3a", "12", "0x1.800p+3")]
    [TestCase("%+a", "12", "+0x1.8p+3")]
    [TestCase("%+8a", "12", "+0x1.8p+3")]
    [TestCase("%+12.0a", "12", "     +0x2p+3")]
    [TestCase("%+8.3a", "12", "+0x1.800p+3")]
    [TestCase("%#a", "12", "0x1.8p+3")]
    [TestCase("%#8a", "12", "0x1.8p+3")]
    [TestCase("%#12.0a", "12", "     0x2.p+3")]
    [TestCase("%#8.3a", "12", "0x1.800p+3")]
    [TestCase("%0a", "12", "0x1.8p+3")]
    [TestCase("%08a", "12", "0x1.8p+3")]
    [TestCase("%012.0a", "12", "0x0000002p+3")]
    [TestCase("%08.3a", "12", "0x1.800p+3")]
    [TestCase("% a", "12", " 0x1.8p+3")]
    [TestCase("% 8a", "12", " 0x1.8p+3")]
    [TestCase("% 12.0a", "12", "      0x2p+3")]
    [TestCase("% 8.3a", "12", " 0x1.800p+3")]
    [TestCase("%a", "0.0", "0x0p+0")]
    [TestCase("%a", "-0.0", "-0x0p+0")]
    [TestCase("%+a", "0.0", "+0x0p+0")]
    [TestCase("%+a", "-0.0", "-0x0p+0")]
    [TestCase("%a", "1.7976931348623157e308","0x1.fffffffffffffp+1023")]
    [TestCase("%a", "2.2250738585072014e-308","0x1p-1022")]
    [TestCase("%a", "5e-324","0x0.0000000000001p-1022")]
    [TestCase( "%#.0a", "0.0","0x0.p+0")]
    [TestCase( "%a", "-12", "-0x1.8p+3")]
    [TestCase( "%12a", "-12", "   -0x1.8p+3")]
    [TestCase( "%.4a", "-12", "-0x1.8000p+3")]
    [TestCase( "%14.4a", "-12","  -0x1.8000p+3")]
    [TestCase( "%-14.4a", "-12","-0x1.8000p+3  ")]
    [TestCase( "%014.4a", "-12","-0x001.8000p+3")]
    [TestCase( "%#12.0a", "-12","    -0x2.p+3")]
    [TestCase( "% 14.4a", "12","   0x1.8000p+3")]
    [TestCase( "%+014.0a", "12","+0x00000002p+3")]
    [TestCase( "%a", "-1.7976931348623157e308","-0x1.fffffffffffffp+1023")]
    [TestCase( "%a","-2.2250738585072014e-308", "-0x1p-1022")]
    [TestCase( "%.4a", "5e-324","0x0.0000p-1022")]
    [TestCase( "%#.0a","5e-324", "0x0.p-1022")]
    [TestCase( "%28a", "5e-324","     0x0.0000000000001p-1022")]
    [TestCase( "%a", "-5e-324","-0x0.0000000000001p-1022")]
    [TestCase( "%a", "math.huge", "inf")]
    [TestCase( "%10a", "math.huge", "       inf")]
    [TestCase( "%10a", "0/0", "       nan")]
    public void ValidCases_a(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%A", "12", "0X1.8P+3")]
    [TestCase("%8A", "12", "0X1.8P+3")]
    [TestCase("%12.0A", "12", "      0X2P+3")]
    [TestCase("%8.3A", "12", "0X1.800P+3")]
    [TestCase("%-A", "12", "0X1.8P+3")]
    [TestCase("%-8A", "12", "0X1.8P+3")]
    [TestCase("%-12.0A", "12", "0X2P+3      ")]
    [TestCase("%-8.3A", "12", "0X1.800P+3")]
    [TestCase("%+A", "12", "+0X1.8P+3")]
    [TestCase("%+8A", "12", "+0X1.8P+3")]
    [TestCase("%+12.0A", "12", "     +0X2P+3")]
    [TestCase("%+8.3A", "12", "+0X1.800P+3")]
    [TestCase("%#A", "12", "0X1.8P+3")]
    [TestCase("%#8A", "12", "0X1.8P+3")]
    [TestCase("%#12.0A", "12", "     0X2.P+3")]
    [TestCase("%#8.3A", "12", "0X1.800P+3")]
    [TestCase("%0A", "12", "0X1.8P+3")]
    [TestCase("%08A", "12", "0X1.8P+3")]
    [TestCase("%012.0A", "12", "0X0000002P+3")]
    [TestCase("%08.3A", "12", "0X1.800P+3")]
    [TestCase("% A", "12", " 0X1.8P+3")]
    [TestCase("% 8A", "12", " 0X1.8P+3")]
    [TestCase("% 12.0A", "12", "      0X2P+3")]
    [TestCase("% 8.3A", "12", " 0X1.800P+3")]
    [TestCase("%A", "1.7976931348623157e308","0X1.FFFFFFFFFFFFFP+1023")]
    [TestCase( "%#.0A", "-0.0","-0X0.P+0")]
    [TestCase( "%A", "-12", "-0X1.8P+3")]
    [TestCase( "%12A", "-12", "   -0X1.8P+3")]
    [TestCase( "%.4A", "-12","-0X1.8000P+3")]
    [TestCase( "%14.4A", "-12","  -0X1.8000P+3")]
    [TestCase("%-14.4A", "-12", "-0X1.8000P+3  ")]
    [TestCase( "%014.4A","-12", "-0X001.8000P+3")]
    [TestCase( "%#12.0A", "-12","    -0X2.P+3")]
    [TestCase( "%+014.0A", "12","+0X00000002P+3")]
    [TestCase( "%A", "5e-324","0X0.0000000000001P-1022")]
    [TestCase( "%A", "-5e-324","-0X0.0000000000001P-1022")]
    [TestCase( "%29A","-5e-324", "     -0X0.0000000000001P-1022")]
    [TestCase( "%A", "-math.huge", "-INF")]
    [TestCase( "%10A", "-math.huge","      -INF")]
    [TestCase( "%A", "0/0", "NAN")]
    [TestCase( "%10A", "0/0", "       NAN")]
    public void ValidCases_A(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }
    
    [TestCase("%s", "'abcdef'", "abcdef")]
    [TestCase("%10s", "'abcdef'", "    abcdef")]
    [TestCase("%14.0s", "'abcdef'", "              ")]
    [TestCase("%10.3s", "'abcdef'", "       abc")]
    [TestCase("%-s", "'abcdef'", "abcdef")]
    [TestCase("%-10s", "'abcdef'", "abcdef    ")]
    [TestCase("%-14.0s", "'abcdef'", "              ")]
    [TestCase("%-10.3s", "'abcdef'", "abc       ")]
    [TestCase("%s", "-42", "-42")]
    [TestCase("%s", "math.mininteger","-9223372036854775808")]
    [TestCase("%s", "-0.0", "-0.0")]
    [TestCase("%12.4s", "-123.5","        -123")]
    public void ValidCases_s(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }

    [TestCase("%c", "97", "a")]
    [TestCase("%10c", "97", "         a")]
    [TestCase("%14c", "97", "             a")]
    [TestCase("%-c", "97", "a")]
    [TestCase("%-10c", "97", "a         ")]
    [TestCase("%-14c", "97", "a             ")]
    [TestCase("%c", "0", "\0")]
    [TestCase("%c", "10", "\n")]
    [TestCase("%c", "255", "\xFF")]
    [TestCase("%3c", "0", "  \0")]
    [TestCase("%-3c", "0", "\0  ")]
    public void ValidCases_c(string format, string argument, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }

    [TestCase("%p", "nil", "(null)")]
    [TestCase("%10p", "nil", "    (null)")]
    [TestCase("%14p", "nil", "        (null)")]
    [TestCase("%-p", "nil", "(null)")]
    [TestCase("%-10p", "nil", "(null)    ")]
    [TestCase("%-14p", "nil", "(null)        ")]
    [TestCase("%p", "-1", "(null)")]
    [TestCase("%10p", "-1.5", "    (null)")]
    [TestCase("%p", "T.pushuserdata(0x0badbeef)", "0x000000000badbeef")]
    [TestCase("%10p", "T.pushuserdata(0x0badbeef)", "0x000000000badbeef")]
    [TestCase("%20p", "T.pushuserdata(0x0badbeef)", "  0x000000000badbeef")]
    [TestCase("%-p", "T.pushuserdata(0x0badbeef)", "0x000000000badbeef")]
    [TestCase("%-10p", "T.pushuserdata(0x0badbeef)", "0x000000000badbeef")]
    [TestCase("%-20p", "T.pushuserdata(0x0badbeef)", "0x000000000badbeef  ")]
    public void ValidCases_p(string format, string argument, string expected)
    {
        using LuaState state = new();
        luai_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }

    [TestCase("%q", "''", "\"\"")]
    [TestCase("%q", "'plain text'", "\"plain text\"")]
    [TestCase("%q", "'a \"quote\" \\\\ slash'", "\"a \\\"quote\\\" \\\\ slash\"")]
    [TestCase("%q", "'line\\nbreak'", "\"line\\\nbreak\"")]
    [TestCase("%q", "'\\0'", "\"\\0\"")]
    [TestCase("%q", "'\\1' .. '2'", "\"\\0012\"")]
    [TestCase("%q", "string.char(0, 1, 2, 3, 5, 0) .. '9'", "\"\\0\\1\\2\\3\\5\\0009\"")]
    [TestCase("%q", "true", "true")]
    [TestCase("%q", "false", "false")]
    [TestCase("%q", "nil", "nil")]
    [TestCase("%q", "42", "42")]
    [TestCase("%q", "math.maxinteger", "9223372036854775807")]
    [TestCase("%q", "math.mininteger", "0x8000000000000000")]
    [TestCase("%q", "-42", "-42")]
    [TestCase("%q", "0", "0")]
    [TestCase("%q", "1.5", "0x1.8p+0")]
    [TestCase("%q", "-0.0", "-0x0p+0")]
    [TestCase("%q", "-1.25", "-0x1.4p+0")]
    [TestCase("%q", "0.1", "0x1.999999999999ap-4")]
    [TestCase("%q", "5e-324","0x0.0000000000001p-1022")]
    [TestCase("%q", "-5e-324","-0x0.0000000000001p-1022")]
    [TestCase("%q", "math.huge", "1e9999")]
    [TestCase("%q", "-math.huge", "-1e9999")]
    [TestCase("%q", "0/0", "(0/0)")]
    public void ValidCases_q(string format, string argument, string expected)
    {
        using LuaState state = new();
        luai_openlibs(state);
        ExpectLuaStringResult(
            state,
            LuaFormatSource(format, argument),
            expected);
    }

    [TestCase("return string.format('%+08d', 31501)", "+0031501")]
    [TestCase("return string.format('%#-17X', 100)", "0X64             ")]
    [TestCase("return string.format('%#08x', 100)", "0x000064")]
    [TestCase("return string.format('%+#014.0f', 100)", "+000000000100.")]
    [TestCase("return string.format('%+.3G', 1.5)", "+1.5")]
    [TestCase("return string.format('')", "")]
    [TestCase(
        "return string.format('%%%d %010d', 10, 23)",
        "%10 0000000023")]
    [TestCase("return string.format('%1c%-c%-1c%c', 34, 48, 90, 100)", "\"0Zd")]
    [TestCase("return string.format('%-16c', 97)", "a               ")]
    [TestCase("return string.format('%10.3s', 'abcdef')", "       abc")]
    [TestCase("return string.format('%-10.3s', 'abcdef')", "abc       ")]
    [TestCase("return string.format('%.0s', 'alo')", "")]
    [TestCase("return string.format('%.s', 'alo')", "")]
    [TestCase("return string.format('%s %.4s', false, true)", "false true")]
    [TestCase(
        @"return string.format('%s\0 is not \0%s', 'not be', 'be')",
        "not be\0 is not \0be")]
    [TestCase("return string.format('%q', '\\0')", "\"\\0\"")]
    [TestCase("return string.format('%p', nil)", "(null)")]
    [TestCase("return string.format('%10p', false)", "    (null)")]
    [TestCase("return string.format('%-12p', 1.5)", "(null)      ")]
    public void StringCases(string source, string expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaStringResult(
            state,
            source,
            expected);
    }

    [TestCase("return string.find(\"attempt to perform 'n%0'\", '%%0')", true)]
    [TestCase("return string.find(\"other error\", '%%0')", false)]
    [TestCase("return string.find(\"alo\", '')", true)]
    public void StringMatchCases(string source, bool expected)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        ExpectLuaBoolResult(
            state,
            source,
            expected);
    }

    [Test]
    public void RejectsUnsupportedFlagsForEachSpecifierFamily()
    {
        using LuaState state = new();
        luaL_openlibs(state);
        
        List<string> allFlags = ["-", "+", "#", "0", " "];
        List<(char, string)> cases =
        [
            ('d', "-+0 "), ('i', "-+0 "), ('u', "-0"), ('o', "-#0"),
            ('x', "-#0"), ('X', "-#0"), ('e', "-+#0 "), ('E', "-+#0 "),
            ('f', "-+#0 "), ('g', "-+#0 "), ('G', "-+#0 "), ('s', "-"),
            ('c', "-"), ('p', "-"),
        ];

        foreach ((char, string) test in cases)
        {
            foreach (string flag in allFlags)
            {
                if (test.Item2.Contains(flag))
                {
                    continue;
                }

                string format = MakeFormat(flag, "8", "", test.Item1);
                string argument = test.Item1 == 's' ? "'abc'" : "10";
                ExpectLuaRuntimeError(state, LuaFormatSource(format, argument), "invalid conversion specification");
            }
        }
    }

    [TestCase(
        "return string.format('%100.3d', 10)",
        "invalid conversion specification")]
    [TestCase(
        "return string.format('%1.100d', 10)",
        "invalid conversion specification")]
    [TestCase("return string.format('%.3c', 97)", "invalid conversion specification")]
    [TestCase("return string.format('%.3p', nil)", "invalid conversion specification")]
    [TestCase(
        "return string.format('%.3q', 'abc')",
        "specifier '%q' cannot have modifiers")]
    [TestCase("return string.format('%10s', '\\0')", "string contains zeros")]
    [TestCase("return string.format('%d %d', 1)", "no value")]
    public void RejectsInvalidWidthPrecisionAndLiteralModifiers(string source, string expectedMessage)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        
        ExpectLuaRuntimeError(
            state,
            source,
            expectedMessage);
    }

    [Test]
    public void QuotedLiteralRejectsEveryFlagWidthAndPrecisionModifier()
    {
        using LuaState state = new();
        luaL_openlibs(state);
        
        List<string> widths = ["", "8", "12"];
        List<string> precisions = ["", ".0", ".3"];

        foreach (string flag in (List<string>)["-", "+", "#", "0", " "])
        {
            string format = MakeFormat(flag, "", "", 'q');
            ExpectLuaRuntimeError(state, LuaFormatSource(format, "'abc'"), "specifier '%q' cannot have modifiers");
        }

        foreach (string width in widths)
        {
            foreach (string precision in precisions)
            {
                if (width.Length == 0 && precision.Length == 0)
                {
                    continue;
                }

                string format = MakeFormat("", width, precision, 'q');
                ExpectLuaRuntimeError(state, LuaFormatSource(format, "'abc'"), "specifier '%q' cannot have modifiers");
            }
        }
    }
}
