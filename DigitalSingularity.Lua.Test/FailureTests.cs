namespace DigitalSingularity.Lua.Test;

using static DigitalSingularity.Lua.Lua;

#pragma warning disable NUnit2045

public unsafe class FailureTests
{
    private void ResetAllocatorControls()
    {
        l_memcontrol->failnext = false;
        l_memcontrol->countlimit = -1;
    }

    private static string StackString(lua_State* L, int idx)
    {
        return lua_tonetstring(L, idx) ?? "";
    }

    private class RunResult
    {
        public required int status;
        public required string message;
        public required int stackTop;
    }

    private RunResult RunLuaCode(lua_State* L, string source)
    {
        lua_settop(L, 0);
        int status = luaL_loadstring(L, source);
        if (status == LUA_OK)
        {
            status = lua_pcall(L, 0, LUA_MULTRET, 0);
        }

        this.ResetAllocatorControls();

        RunResult result = new()
        {
            status = status,
            stackTop = lua_gettop(L),
            message = "",
        };
        if (status != LUA_OK && result.stackTop > 0)
        {
            result.message = StackString(L, -1);
        }

        lua_settop(L, 0);
        return result;
    }

    private static int ErroringMessageHandler(lua_State* L)
    {
        lua_pushliteral(L, "message handler failed");
        return lua_error(L);
    }

    private readonly LuaState state = new();

    [SetUp]
    public void SetUp()
    {
        this.ResetAllocatorControls();
        luai_openlibs(this.state);
    }

    [TearDown]
    public void TearDown()
    {
        this.ResetAllocatorControls();
        lua_settop(this.state, 0);
    }

    private lua_State* L()
    {
        return this.state.get(); 
    }

    [Test]
    public void SuccessfulChunkReturnsOk()
    {
        RunResult result = this.RunLuaCode(this.L(), "return 1 + 2");

        Assert.That(result.status, Is.EqualTo(LUA_OK));
        Assert.That(result.stackTop, Is.EqualTo(1));
    }

    [Test]
    public void AssertFailureReturnsRuntimeError()
    {
        RunResult result = this.RunLuaCode(
            this.L(),
            """
              assert(false, "assertion failed from Lua")
            """);

        Assert.That(result.status, Is.EqualTo(LUA_ERRRUN));
        Assert.That(result.message, Contains.Substring("assertion failed from Lua"));
    }

    [Test]
    public void AssertFunctionFailureReturnsRuntimeError()
    {
        RunResult result = this.RunLuaCode(
            this.L(),
            """
            local function f()
                assert(false, "assertion failed from Lua function")
            end
            f()
            """);

        Assert.That(result.status, Is.EqualTo(LUA_ERRRUN));
        Assert.That(result.message, Contains.Substring("assertion failed from Lua function"));
    }

    [Test]
    public void ErrorFailureReturnsRuntimeError()
    {
        RunResult result = this.RunLuaCode(
            this.L(),
            """
              error("explicit Lua error")
            """);

        Assert.That(result.status, Is.EqualTo(LUA_ERRRUN));
        Assert.That(result.message, Contains.Substring("explicit Lua error"));
    }

    [Test]
    public void TestCThrowReturnsRuntimeError()
    {
        RunResult result = this.RunLuaCode(
            this.L(),
            """
              T.testC("throw")
            """);

        Assert.That(result.status, Is.EqualTo(LUA_ERRRUN));
        Assert.That(result.message, Contains.Substring("C++"));
    }

    [Test]
    public void SyntaxErrorReturnsSyntaxStatusBeforeExecution()
    {
        RunResult result = this.RunLuaCode(this.L(), "return +");

        Assert.That(result.status, Is.EqualTo(LUA_ERRSYNTAX));
        Assert.That(result.message, Is.Not.Empty);
    }

    [Test]
    public void MemoryFailureReturnsMemoryStatus()
    {
        RunResult result = this.RunLuaCode(
            this.L(),
            """
              T.alloccount(0)
              return {}
            """);

        Assert.That(result.status, Is.EqualTo(LUA_ERRMEM));
        Assert.That(result.message, Contains.Substring("not enough memory"));
    }

    [Test]
    public void MessageHandlerFailureReturnsErrErrStatus()
    {
        lua_settop(this.L(), 0);
        lua_pushcfunction(this.L(), CFunction.FromFunction(&ErroringMessageHandler));
        int messageHandler = lua_gettop(this.L());
        Assert.That(
            luaL_loadstring(
                this.L(),
                "error(\"primary failure\")"),
            Is.EqualTo(LUA_OK),
            () => StackString(this.L(), -1));

        int status = lua_pcall(this.L(), 0, 0, messageHandler);
        this.ResetAllocatorControls();

        Assert.That(status, Is.EqualTo(LUA_ERRERR));
        Assert.That(lua_gettop(this.L()), Is.GreaterThan(0));
        Assert.That(StackString(this.L(), -1), Contains.Substring("error in error handling"));
    }

    [Test]
    public void MissingFileReturnsFileStatus()
    {
        lua_settop(this.L(), 0);

        int status = luaL_loadfilex(this.L(), "definitely_missing_lua_failure_state_test_file.lua", "t");

        Assert.That(status, Is.EqualTo(LUA_ERRFILE));
        Assert.That(lua_gettop(this.L()), Is.GreaterThan(0));
        Assert.That(StackString(this.L(), -1), Contains.Substring("definitely_missing"));
    }

    [Test]
    public void CoroutineYieldReturnsYieldStatus()
    {
        lua_State* thread = lua_newthread(this.L());
        Assert.That(thread, Is.Not.Null);
        Assert.That(
            luaL_loadstring(
                thread,
                """
                  coroutine.yield("paused")
                  return "finished"
                """),
            Is.EqualTo(LUA_OK),
            () => StackString(thread, -1));

        int nresults = 0;
        int status = lua_resume(thread, this.L(), 0, &nresults);
        Assert.That(status, Is.EqualTo(LUA_YIELD));
        Assert.That(nresults, Is.EqualTo(1));
        Assert.That(lua_tonetstring(thread, -1), Is.EqualTo("paused"));
        lua_pop(thread, 1);

        status = lua_resume(thread, this.L(), 0, &nresults);
        Assert.That(status, Is.EqualTo(LUA_OK));
        Assert.That(nresults, Is.EqualTo(1));
        Assert.That(lua_tonetstring(thread, -1), Is.EqualTo("finished"));
    }
}
