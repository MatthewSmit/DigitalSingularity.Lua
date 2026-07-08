namespace DigitalSingularity.Lua.Test;

using System.Globalization;
using System.Text;
using static DigitalSingularity.Lua.Lua;

#pragma warning disable NUnit2045

public unsafe class GSubTests
{
    private static string StackString(lua_State* l, int idx)
    {
        return lua_tonetstring(l, idx) ?? "";
    }

    [SetUp]
    public void SetUp()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private void ExpectGsubResult(
        string source,
        string expected,
        long expectedCount)
    {
        using LuaState state = new();
        luaL_openlibs(state);

        lua_settop(state, 0);
        byte[] sourceUtf = Encoding.UTF8.GetBytes(source);
        Assert.That(
            luaL_loadbufferx(state, sourceUtf, "=(gsub test)", "t"),
            Is.EqualTo(LUA_OK),
            () => StackString(state, -1));
        Assert.That(lua_pcall(state, 0, 2, 0), Is.EqualTo(LUA_OK), () => StackString(state, -1));
        Assert.That(lua_gettop(state), Is.EqualTo(2));
        Assert.That(lua_type(state, -2), Is.EqualTo(LUA_TSTRING), () => lua_typename(state, lua_type(state, -2)));
        Assert.That(StackString(state, -2), Is.EqualTo(expected));

        long count = lua_tointegerx(state, -1, out bool isNumber);
        Assert.That(isNumber, Is.True);
        Assert.That(count, Is.EqualTo(expectedCount));
        lua_settop(state, 0);
    }

    private void ExpectBooleanIntegerResult(
        string source,
        bool expectedBoolean,
        long expectedInteger)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        
        lua_settop(state, 0);
        byte[] sourceUtf = Encoding.UTF8.GetBytes(source);
        Assert.That(
            luaL_loadbufferx(state, sourceUtf, "=(gsub test)", "t"),
            Is.EqualTo(LUA_OK),
            () => StackString(state, -1));
        Assert.That(lua_pcall(state, 0, 2, 0), Is.EqualTo(LUA_OK), () => StackString(state, -1));
        Assert.That(lua_gettop(state), Is.EqualTo(2));
        Assert.That(lua_type(state, -2), Is.EqualTo(LUA_TBOOLEAN), () => lua_typename(state, lua_type(state, -2)));
        Assert.That(lua_toboolean(state, -2), Is.EqualTo(expectedBoolean));

        long count = lua_tointegerx(state, -1, out bool isNumber);
        Assert.That(isNumber, Is.True);
        Assert.That(count, Is.EqualTo(expectedInteger));
        lua_settop(state, 0);
    }

    private void ExpectLuaRuntimeError(
        string source,
        string expectedMessage)
    {
        using LuaState state = new();
        luaL_openlibs(state);
        
        lua_settop(state, 0);
        byte[] sourceUtf = Encoding.UTF8.GetBytes(source);
        Assert.That(
            luaL_loadbufferx(state, sourceUtf, "=(gsub test)", "t"),
            Is.EqualTo(LUA_OK),
            () => StackString(state, -1));
        Assert.That(lua_pcall(state, 0, LUA_MULTRET, 0), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring(expectedMessage));
        lua_settop(state, 0);
    }

    [TestCase("return string.gsub('one two one', 'one', '1')", "1 two 1", 2)]
    [TestCase("return string.gsub('abc', 'z', 'x')", "abc", 0)]
    [TestCase("return string.gsub('aaa', 'a', 'x', 2)", "xxa", 2)]
    [TestCase("return string.gsub('aaa', 'a', 'x', 0)", "aaa", 0)]
    [TestCase("return string.gsub('aaa', 'a', 'x', -1)", "aaa", 0)]
    [TestCase("return string.gsub('abc abc', '^abc', 'X')", "X abc", 1)]
    [TestCase("return string.gsub('zabc', '^abc', 'X')", "zabc", 0)]
    [TestCase("return string.gsub('abc', '^', '<')", "<abc", 1)]
    [TestCase("return string.gsub('abc', '$', '!')", "abc!", 1)]
    [TestCase("return string.gsub('', '^', 'r')", "r", 1)]
    [TestCase("return string.gsub('', '$', 'r')", "r", 1)]
    public void MainLoopHandlesPlainAnchoredAndLimitedMatches(string source, string expected, long expectedCount)
    {
        this.ExpectGsubResult(source, expected, expectedCount);
    }

    [TestCase("return string.gsub('a' .. string.char(0) .. 'b', '.', 'x')", "xxx", 3)]
    [TestCase("return string.gsub('a.b.c', '%.', '/')", "a/b/c", 2)]
    [TestCase("return string.gsub('Az 09_!', '%a', 'x')", "xx 09_!", 2)]
    [TestCase("return string.gsub('Az 09_!', '%A', 'x')", "Azxxxxx", 5)]
    [TestCase("return string.gsub(string.char(1) .. 'A\\n', '%c', 'x')", "xAx", 2)]
    [TestCase("return string.gsub(' A', '%C', 'x')", "xx", 2)]
    [TestCase("return string.gsub('a09x', '%d', 'x')", "axxx", 2)]
    [TestCase("return string.gsub('a09x', '%D', 'x')", "x09x", 2)]
    [TestCase("return string.gsub(' A!', '%g', 'x')", " xx", 2)]
    [TestCase("return string.gsub('A ', '%G', 'x')", "Ax", 1)]
    [TestCase("return string.gsub('aA', '%l', 'x')", "xA", 1)]
    [TestCase("return string.gsub('aA', '%L', 'x')", "ax", 1)]
    [TestCase("return string.gsub('a.!', '%p', 'x')", "axx", 2)]
    [TestCase("return string.gsub('a.!', '%P', 'x')", "x.!", 1)]
    [TestCase(@"return string.gsub('a \n\tb', '%s', 'x')", "axxxb", 3)]
    [TestCase("return string.gsub('a b', '%S', 'x')", "x x", 2)]
    [TestCase("return string.gsub('aA', '%u', 'x')", "ax", 1)]
    [TestCase("return string.gsub('aA', '%U', 'x')", "xA", 1)]
    [TestCase("return string.gsub('a9_!', '%w', 'x')", "xx_!", 2)]
    [TestCase("return string.gsub('a9_!', '%W', 'x')", "a9xx", 2)]
    [TestCase("return string.gsub('0gF!', '%x', 'x')", "xgx!", 2)]
    [TestCase("return string.gsub('0gF!', '%X', 'x')", "0xFx", 2)]
    [TestCase("return string.gsub('a' .. string.char(0) .. 'b', '%z', 'x')", "axb", 1)]
    [TestCase("return string.gsub('a' .. string.char(0), '%Z', 'x')", "x\0", 1)]
    [TestCase("return string.gsub('abcd', '[a-c]', 'x')", "xxxd", 3)]
    [TestCase("return string.gsub('abcd', '[^a-c]', 'x')", "abcx", 1)]
    [TestCase("return string.gsub('a1_b', '[%d_]', 'x')", "axxb", 2)]
    [TestCase("return string.gsub('-[]^ab', '[%^%[%-a%]%-b]', 'x')", "xxxxxx", 6)]
    [TestCase(@"return string.gsub('a' .. string.char(0, 1, 2) .. 'b', '[\0-\2]', 'x')", "axxxb", 3)]
    public void CharacterClassesCoverNamedEscapedAndBracketForms(string source, string expected, long expectedCount)
    {
        this.ExpectGsubResult(source, expected, expectedCount);
    }

    [TestCase("return string.gsub('aaab', 'a*ab', 'X')", "X", 1)]
    [TestCase("return string.gsub('aaa', '^a*b', 'X')", "aaa", 0)]
    [TestCase("return string.gsub('aaab', 'a+b', 'X')", "X", 1)]
    [TestCase("return string.gsub('bbb', 'a+', 'X')", "bbb", 0)]
    [TestCase("return string.gsub('aaab', 'a-b', 'X')", "X", 1)]
    [TestCase("return string.gsub('aaa', '^a-b', 'X')", "aaa", 0)]
    [TestCase("return string.gsub('ab', 'a?b', 'X')", "X", 1)]
    [TestCase("return string.gsub('b', 'a?b', 'X')", "X", 1)]
    [TestCase("return string.gsub('ab', 'a?ab', 'X')", "X", 1)]
    [TestCase("return string.gsub('a b cd', ' *', '-')", "-a-b-c-d-", 5)]
    [TestCase("return string.gsub('key=value', '(%w+)=(%w+)', '%2:%1:%0')", "value:key:key=value", 1)]
    [TestCase("return string.gsub('abc', '()a()', '%1-%2')", "1-2bc", 1)]
    [TestCase("return string.gsub('bookkeeper', '(.)%1', '<%1>')", "b<o><k><e>per", 3)]
    [TestCase("return string.gsub('abab', '^(a)%1', 'X')", "abab", 0)]
    public void QuantifiersCapturesAndBackReferencesAreExercised(string source, string expected, long expectedCount)
    {
        this.ExpectGsubResult(source, expected, expectedCount);
    }

    [TestCase("return string.gsub('a (b (c)) d ()', '%b()', 'P')", "a P d P", 2)]
    [TestCase("return string.gsub('a (b', '%b()', 'P')", "a (b", 0)]
    [TestCase("return string.gsub('one two2 _x', '%f[%w]%w', 'X')", "Xne Xwo2 _X", 3)]
    [TestCase("return string.gsub('one two', '%f[%W]', '|')", "one| two|", 2)]
    [TestCase("return string.gsub('$HOME $PATH', '%$(%u+)', '<%1>')", "<HOME> <PATH>", 2)]
    [TestCase("return string.gsub('a$b$', '$b', 'X')", "aX$", 1)]
    [TestCase("return string.gsub('a^b', 'a^', 'X')", "Xb", 1)]
    public void BalancedFrontierAndLiteralAnchorCharactersWork(string source, string expected, long expectedCount)
    {
        this.ExpectGsubResult(source, expected, expectedCount);
    }

    [TestCase("return string.gsub('ab', '(%a)', '%%%0:%1')", "%a:a%b:b", 2)]
    [TestCase("return string.gsub('a1b2', '%d', 7)", "a7b7", 2)]
    [TestCase("return string.gsub('a1 b2', '%d', function (d) return tonumber(d) + 10 end)", "a11 b12", 2)]
    [TestCase(
        """
          return string.gsub('a=1 b=2 c=x', '(%a)=(%w)', function (k, v)
            if k == 'b' then return false end
            if v == 'x' then return nil end
            return k .. v
          end)
        """,
        "a1 b=2 c=x",
        3)]
    [TestCase(
        """
          return string.gsub('ab', '()(%a)', function (pos, c)
            return pos .. c
          end)
        """,
        "1a2b",
        2)]
    [TestCase("return string.gsub('cat dog eel', '%a+', {cat='CAT', dog=false})", "CAT dog eel", 3)]
    [TestCase(
        """
          local t = setmetatable({a='A'}, {
            __index = function (_, key) return '[' .. key .. ']' end
          })
          return string.gsub('a1 b2', '(%a)(%d)', t)
        """,
        "A [b]",
        2)]
    [TestCase("return string.gsub('abc', '().', {'x', 'yy', 'zzz'})", "xyyzzz", 3)]
    [TestCase("return string.gsub('aaa', '.', {})", "aaa", 3)]
    [TestCase("return string.gsub('aaa', '.', function () return nil end)", "aaa", 3)]
    public void ReplacementStringsFunctionsTablesAndNumbersWork(string source, string expected, long expectedCount)
    {
        this.ExpectGsubResult(source, expected, expectedCount);
    }

    [TestCase("return string.gsub('abc', '', '-')", "-a-b-c-", 4)]
    [TestCase("return string.gsub('ab', '()', '<%1>')", "<1>a<2>b<3>", 3)]
    [TestCase("return string.gsub('bc', 'a*', 'X')", "XbXcX", 3)]
    [TestCase("return string.gsub('a' .. string.char(0) .. 'b', string.char(0), '<0>')", "a<0>b", 1)]
    [TestCase("return string.gsub('ab', 'b', '\\0X')", "a\0X", 1)]
    public void EmptyAndBinaryPatternsDoNotRepeatStaleMatches(string source, string expected, long expectedCount)
    {
        this.ExpectGsubResult(source, expected, expectedCount);
    }

    [Test]
    public void NoChangePathReturnsOriginalSubject()
    {
        this.ExpectBooleanIntegerResult(
            """
              local s = string.rep('a', 100)
              local r, n = string.gsub(s, '.', {})
              return string.format('%p', s) == string.format('%p', r), n
            """,
            true,
            100);
    }

    [TestCase("return string.gsub('a', '[a', 'x')", "malformed pattern (missing ']')")]
    [TestCase("return string.gsub('a', '[a%', 'x')", "malformed pattern (missing ']')")]
    [TestCase("return string.gsub('a', '%', 'x')", "malformed pattern (ends with '%')")]
    [TestCase("return string.gsub('a', '%b', 'x')", "malformed pattern (missing arguments to '%b')")]
    [TestCase("return string.gsub('a', '%f%w', 'x')", "missing '[' after '%f' in pattern")]
    [TestCase("return string.gsub('a', ')', 'x')", "invalid pattern capture")]
    [TestCase("return string.gsub('a', '(.', '%1')", "unfinished capture")]
    [TestCase("return string.gsub('', string.rep('(', 33) .. string.rep(')', 33), 'x')", "too many captures")]
    [TestCase("return string.gsub(string.rep('a', 300), string.rep('.?', 300), 'x')", "pattern too complex")]
    [TestCase("return string.gsub('a', 'a', '%2')", "invalid capture index %2")]
    [TestCase("return string.gsub('a', '(%0)', 'x')", "invalid capture index %0")]
    [TestCase("return string.gsub('a', 'a', '%x')", "invalid use of '%' in replacement string")]
    [TestCase("return string.gsub('a', 'a', true)", "string/function/table expected")]
    [TestCase("return string.gsub('a', '.', {a = {}})", "invalid replacement value (a table)")]
    [TestCase("return string.gsub('a', '.', function () return {} end)", "invalid replacement value (a table)")]
    [TestCase("return string.gsub('a', 'a', 'x', 'bad')", "number expected")]
    public void InvalidPatternsAndReplacementValuesReportErrors(string source, string expectedMessage)
    {
        this.ExpectLuaRuntimeError(source, expectedMessage);
    }
}
