namespace DigitalSingularity.Lua.Test;

using System.Runtime.InteropServices;
using System.Text;
using static DigitalSingularity.Lua.Lua;

#pragma warning disable NUnit2045

internal unsafe struct LuaState : IDisposable
{
    public LuaState()
    {
        this.l = luaL_newstate();
        Assert.That(this.l, Is.Not.Null);
    }

    public void Dispose()
    {
        if (this.l != null)
        {
            lua_close(this.l);
        }
    }
    
    public readonly lua_State* get() => this.l;

    public static implicit operator lua_State*(LuaState state)
    {
        return state.l;
    }

    private readonly lua_State* l;
}

public unsafe class PublicTests
{
    private struct AllocStats
    {
        public int alloc_calls;
        public int free_calls;
        public bool fail_alloc;
    }

    private static void* CountingAlloc(void* ud, void* ptr, long osize, long nsize)
    {
        AllocStats* stats = (AllocStats*)(ud);
        if (nsize == 0)
        {
            if (stats != null)
            {
                ++stats->free_calls;
            }

            NativeMemory.Free(ptr);
            return null;
        }

        if (stats != null)
        {
            ++stats->alloc_calls;
            if (stats->fail_alloc)
            {
                return null;
            }
        }

        try
        {
            return NativeMemory.Realloc(ptr, (nuint)nsize);
        }
        catch (OutOfMemoryException)
        {
            return null;
        }
    }

    private static int ProtectedCall(lua_State* L, delegate*<lua_State*, int> fn, int nresults = LUA_MULTRET)
    {
        lua_pushcfunction(L, fn);
        return lua_pcall(L, 0, nresults, 0);
    }

    private static string StackString(lua_State* L, int idx)
    {
        return lua_tonetstring(L, idx) ?? "";
    }

    private static int ReturnZero(lua_State* L)
    {
        return 0;
    }

    private static int ReturnOne(lua_State* L)
    {
        lua_pushinteger(L, 1);
        return 1;
    }

    private static int ReturnIntegerUpvalue(lua_State* L)
    {
        lua_pushvalue(L, lua_upvalueindex(1));
        return 1;
    }

    private static int AddTwoIntegers(lua_State* L)
    {
        long a = lua_tointeger(L, 1);
        long b = lua_tointeger(L, 2);
        lua_pushinteger(L, a + b);
        return 1;
    }

    private static int ErrorFunction(lua_State* L)
    {
        lua_pushliteral(L, "intentional failure");
        return lua_error(L);
    }

    private struct ReaderState
    {
        public byte* data;
        public long size;
        public bool done;
    }

    private static byte* StringReader(lua_State* L, void* ud, out long size)
    {
        ReaderState* state = (ReaderState*)ud;
        if (state->done)
        {
            size = 0;
            return null;
        }

        state->done = true;
        size = state->size;
        return state->data;
    }

    private static int LoadChunk(lua_State* L, string source, string name = "=(unit)")
    {
        byte[] sourceData = Encoding.UTF8.GetBytes(source);
        fixed (byte* sourcePtr = sourceData)
        {
            ReaderState reader = new()
            {
                data = sourcePtr,
                size = sourceData.Length,
                done = false,
            };
            return lua_load(L, &StringReader, &reader, name, "t");
        }
    }

    private sealed class DumpState
    {
        public List<byte> bytes = [];
        public bool fail;
    }

    private static int DumpWriter(lua_State* L, void* p, long sz, void* ud)
    {
        DumpState state = GCHandle<DumpState>.FromIntPtr((nint)ud).Target;
        if (state.fail)
        {
            return 1;
        }

        for (int i = 0; i < sz; i++)
        {
            state.bytes.Add(((byte*)p)[i]);
        }

        return 0;
    }

    private static string TempLuaFile(string name, string contents)
    {
        string path = Path.Join(Path.GetTempPath(), name);
        File.WriteAllText(path, contents);
        return path;
    }

    private static int CloseCounter(lua_State* L)
    {
        int* counter = (int*)lua_touserdata(L, lua_upvalueindex(1));
        ++*counter;
        return 0;
    }

    private static int CloseWithError(lua_State* L)
    {
        lua_pushliteral(L, "close failed");
        return lua_error(L);
    }

    private static void PushClosable(lua_State* L, int* counter)
    {
        PushClosable(L, counter, &CloseCounter);
    }

    private static void PushClosable(
        lua_State* L,
        int* counter,
        delegate* managed<lua_State*, int> closef)
    {
        lua_newuserdatauv(L, 1, 0);
        lua_newtable(L);
        lua_pushlightuserdata(L, counter);
        lua_pushcclosure(L, closef, 1);
        lua_setfield(L, -2, "__close");
        lua_setmetatable(L, -2);
    }

    private static int TocloseSuccess(lua_State* L)
    {
        int counter = 0;
        PushClosable(L, &counter);
        lua_toclose(L, -1);
        lua_pushlightuserdata(L, &counter);
        return 1;
    }

    private static int TocloseFailure(lua_State* L)
    {
        int counter = 0;
        PushClosable(L, &counter, &CloseWithError);
        lua_toclose(L, -1);
        return 0;
    }

    private static int CloseSlotSuccess(lua_State* L)
    {
        int counter = 0;
        PushClosable(L, &counter);
        lua_toclose(L, -1);
        lua_closeslot(L, -1);
        lua_pushinteger(L, counter);
        return 1;
    }

    private static int CloseSlotFailure(lua_State* L)
    {
        int counter = 0;
        PushClosable(L, &counter, &CloseWithError);
        lua_toclose(L, -1);
        lua_closeslot(L, -1);
        return 0;
    }

    private static int YieldKContinuation(lua_State* L, int status, nint ctx)
    {
        lua_pushinteger(L, status);
        lua_pushinteger(L, ctx);
        return 2;
    }

    private static int YieldKFunction(lua_State* L)
    {
        lua_pushinteger(L, 7);
        return lua_yieldk(L, 1, 123, &YieldKContinuation);
    }

    private static int YieldFunction(lua_State* L)
    {
        lua_pushinteger(L, 9);
        return lua_yield(L, 1);
    }

    private static int IsYieldableFunction(lua_State* L)
    {
        lua_pushboolean(L, lua_isyieldable(L));
        return 1;
    }

    private static int CallKSuccess(lua_State* L)
    {
        lua_pushcfunction(L, &AddTwoIntegers);
        lua_pushinteger(L, 20);
        lua_pushinteger(L, 22);
        lua_callk(L, 2, 1, 0, null);
        return 1;
    }

    private static int CallKFailure(lua_State* L)
    {
        lua_pushcfunction(L, &ErrorFunction);
        lua_callk(L, 0, 0, 0, null);
        return 0;
    }

    private static int CallMacroSuccess(lua_State* L)
    {
        lua_pushcfunction(L, &ReturnOne);
        lua_call(L, 0, 1);
        return 1;
    }

    private static int CallMacroFailure(lua_State* L)
    {
        lua_pushcfunction(L, &ErrorFunction);
        lua_call(L, 0, 0);
        return 0;
    }

    private static int ArithFailure(lua_State* L)
    {
        lua_newtable(L);
        lua_newtable(L);
        lua_arith(L, LUA_OPADD);
        return 1;
    }

    private static int LenFailure(lua_State* L)
    {
        lua_pushboolean(L, true);
        lua_len(L, -1);
        return 1;
    }

    private static int ConcatFailure(lua_State* L)
    {
        lua_newtable(L);
        lua_newtable(L);
        lua_concat(L, 2);
        return 1;
    }

    private static int SetTableFailure(lua_State* L)
    {
        lua_pushinteger(L, 1);
        lua_pushliteral(L, "key");
        lua_pushinteger(L, 2);
        lua_settable(L, 1);
        return 0;
    }

    private static int SetFieldFailure(lua_State* L)
    {
        lua_pushinteger(L, 1);
        lua_pushinteger(L, 2);
        lua_setfield(L, 1, "field");
        return 0;
    }

    private static int SetIFailure(lua_State* L)
    {
        lua_pushinteger(L, 1);
        lua_pushinteger(L, 2);
        lua_seti(L, 1, 1);
        return 0;
    }

    private static int GetTableFailure(lua_State* L)
    {
        lua_pushinteger(L, 1);
        lua_pushliteral(L, "key");
        lua_gettable(L, 1);
        return 1;
    }

    private static int GetFieldFailure(lua_State* L)
    {
        lua_pushinteger(L, 1);
        lua_getfield(L, 1, "field");
        return 1;
    }

    private static int GetIFailure(lua_State* L)
    {
        lua_pushinteger(L, 1);
        lua_geti(L, 1, 1);
        return 1;
    }

    private static void HookFunction(lua_State* L, ref lua_Debug D)
    {
    }

    private static int GetStackProbe(lua_State* L)
    {
        lua_Debug ar = new();
        lua_pushboolean(L, lua_getstack(L, 0, ref ar));
        lua_pushboolean(L, !lua_getstack(L, 1000, ref ar));
        return 2;
    }

    private static int GetLocalProbe(lua_State* L)
    {
        lua_Debug ar = new();
        lua_getstack(L, 0, ref ar);
        string? name = lua_getlocal(L, ref ar, 1);
        bool gotFirst = name != null && lua_tointeger(L, -1) == 123;
        if (name != null)
        {
            lua_pop(L, 1);
        }

        string? missing = lua_getlocal(L, ref ar, 1000);
        lua_pushboolean(L, gotFirst);
        lua_pushboolean(L, missing == null);
        return 2;
    }

    private static int SetLocalProbe(lua_State* L)
    {
        lua_Debug ar = new();
        lua_getstack(L, 0, ref ar);
        lua_pushinteger(L, 456);
        string? setName = lua_setlocal(L, ref ar, 1);
        string? getName = lua_getlocal(L, ref ar, 1);
        bool replaced = setName != null &&
                        getName != null &&
                        lua_tointeger(L, -1) == 456;
        if (getName != null)
        {
            lua_pop(L, 1);
        }

        lua_pushliteral(L, "unused");
        string? missing = lua_setlocal(L, ref ar, 1000);
        lua_pushboolean(L, replaced);
        lua_pushboolean(L, missing == null);
        return 2;
    }

    private static int MetaToString(lua_State* L)
    {
        lua_pushliteral(L, "metavalue");
        return 1;
    }

    private static int AuxCheckVersionOk(lua_State* L)
    {
        luaL_checkversion(L, LUA_VERSION_NUM, LUAL_NUMSIZES);
        return 0;
    }

    private static int AuxCheckVersionBad(lua_State* L)
    {
        luaL_checkversion(L, 0, 0);
        return 0;
    }

    private static int AuxArgError(lua_State* L)
    {
        return luaL_argerror(L, 1, "bad argument");
    }

    private static int AuxTypeError(lua_State* L)
    {
        return luaL_typeerror(L, 1, "table");
    }

// int AuxCheckLStringOk(lua_State* L) {
//   size_t len = 0;
//   const char* value = luaL_checklstring(L, 1, &len);
//   lua_pushboolean(L, std::strcmp(value, "abc") == 0 && len == 3);
//   return 1;
// }

    private static int AuxCheckLStringFail(lua_State* L)
    {
        lua_newtable(L);
        luaL_checklstring(L, 1);
        return 0;
    }

    private static int AuxOptLStringFail(lua_State* L)
    {
        lua_newtable(L);
        luaL_optnetstring(L, 1, "default");
        return 0;
    }

    private static int AuxCheckNumberFail(lua_State* L)
    {
        lua_pushliteral(L, "not-a-number");
        luaL_checknumber(L, 1);
        return 0;
    }

    private static int AuxOptNumberFail(lua_State* L)
    {
        lua_newtable(L);
        luaL_optnumber(L, 1, 1.0);
        return 0;
    }

    private static int AuxCheckIntegerFail(lua_State* L)
    {
        lua_pushnumber(L, 1.5);
        luaL_checkinteger(L, 1);
        return 0;
    }

    private static int AuxOptIntegerFail(lua_State* L)
    {
        lua_newtable(L);
        luaL_optinteger(L, 1, 1);
        return 0;
    }

    private static int AuxCheckStackFail(lua_State* L)
    {
        luaL_checkstack(L, int.MaxValue, "too much stack");
        return 0;
    }

    private static int AuxCheckTypeFail(lua_State* L)
    {
        lua_pushnil(L);
        luaL_checktype(L, 1, LUA_TTABLE);
        return 0;
    }

    private static int AuxCheckAnyFail(lua_State* L)
    {
        luaL_checkany(L, 1);
        return 0;
    }

    private static int AuxCheckUDataFail(lua_State* L)
    {
        lua_newuserdatauv(L, 1, 0);
        luaL_checkudata(L, 1, "missing.metatable");
        return 0;
    }

    private static int AuxError(lua_State* L)
    {
        return luaL_error(L, "formatted %s", "failure");
    }

    private static int AuxCheckOptionFail(lua_State* L)
    {
        string[] options = ["red", "green"];
        lua_pushliteral(L, "blue");
        luaL_checkoption(L, 1, null, options);
        return 0;
    }

    private static int AuxLenFailure(lua_State* L)
    {
        lua_pushboolean(L, true);
        luaL_len(L, 1);
        return 0;
    }

    private static int AuxArgCheckOk(lua_State* L)
    {
        luaL_argcheck(L, true, 1, "ok");
        return 0;
    }

    private static int AuxArgCheckFail(lua_State* L)
    {
        luaL_argcheck(L, false, 1, "not ok");
        return 0;
    }

    private static int AuxArgExpectedOk(lua_State* L)
    {
        luaL_argexpected(L, true, 1, "table");
        return 0;
    }

    private static int AuxArgExpectedFail(lua_State* L)
    {
        luaL_argexpected(L, false, 1, "table");
        return 0;
    }

    private static int TestOpenLib(lua_State* L)
    {
        lua_newtable(L);
        return 1;
    }

    private static int IncrementingOpenLib(lua_State* L)
    {
        int* counter = (int*)lua_touserdata(L, lua_upvalueindex(1));
        ++*counter;
        lua_newtable(L);
        return 1;
    }

    private static readonly luaL_Reg[] kOneFunctionLib =
    [
        new("one", &ReturnOne),
    ];

// #define EXPECT_INT_DEFINE_TEST(test_name, macro_name, expected_value) \
//   [Test] public void test_name() { Assert.That((macro_name), Is.EqualTo((expected_value))); }
//
// #define EXPECT_STR_DEFINE_TEST(test_name, macro_name, expected_value) \
//   [Test] public void test_name() { EXPECT_STREQ((expected_value), (macro_name)); }

// EXPECT_STR_DEFINE_TEST(Define_LUA_AUTHORS, LUA_AUTHORS,
//                        "R. Ierusalimschy, L. H. de Figueiredo, W. Celes")
// [Test] public void Define_LUA_COPYRIGHT() {
//   EXPECT_NE(std::string(LUA_COPYRIGHT).find(LUA_RELEASE), std::string::npos);
//   EXPECT_NE(std::string(LUA_COPYRIGHT).find("Lua.org"), std::string::npos);
// }
// EXPECT_INT_DEFINE_TEST(Define_LUA_VERSION_MAJOR_N, LUA_VERSION_MAJOR_N, 5)
// EXPECT_INT_DEFINE_TEST(Define_LUA_VERSION_MINOR_N, LUA_VERSION_MINOR_N, 5)
// EXPECT_INT_DEFINE_TEST(Define_LUA_VERSION_RELEASE_N, LUA_VERSION_RELEASE_N, 0)
// EXPECT_INT_DEFINE_TEST(Define_LUA_VERSION_NUM, LUA_VERSION_NUM, 505)
// EXPECT_INT_DEFINE_TEST(Define_LUA_VERSION_RELEASE_NUM, LUA_VERSION_RELEASE_NUM, 50500)
// EXPECT_STR_DEFINE_TEST(Define_LUA_SIGNATURE, LUA_SIGNATURE, "\x1bLua")
// EXPECT_INT_DEFINE_TEST(Define_LUA_MULTRET, LUA_MULTRET, -1)
// [Test] public void Define_LUA_REGISTRYINDEX() {
//   EXPECT_LT(LUA_REGISTRYINDEX, 0);
//   EXPECT_LT(lua_upvalueindex(1), LUA_REGISTRYINDEX);
// }
// [Test] public void Define_lua_upvalueindex() {
//   Assert.That(lua_upvalueindex(2), Is.EqualTo(LUA_REGISTRYINDEX - 2));
// }
// EXPECT_INT_DEFINE_TEST(Define_LUA_OK, LUA_OK, 0)
// EXPECT_INT_DEFINE_TEST(Define_LUA_YIELD, LUA_YIELD, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_ERRRUN, LUA_ERRRUN, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_ERRSYNTAX, LUA_ERRSYNTAX, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_ERRMEM, LUA_ERRMEM, 4)
// EXPECT_INT_DEFINE_TEST(Define_LUA_ERRERR, LUA_ERRERR, 5)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TNONE, LUA_TNONE, -1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TNIL, LUA_TNIL, 0)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TBOOLEAN, LUA_TBOOLEAN, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TLIGHTUSERDATA, LUA_TLIGHTUSERDATA, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TNUMBER, LUA_TNUMBER, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TSTRING, LUA_TSTRING, 4)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TTABLE, LUA_TTABLE, 5)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TFUNCTION, LUA_TFUNCTION, 6)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TUSERDATA, LUA_TUSERDATA, 7)
// EXPECT_INT_DEFINE_TEST(Define_LUA_TTHREAD, LUA_TTHREAD, 8)
// EXPECT_INT_DEFINE_TEST(Define_LUA_NUMTYPES, LUA_NUMTYPES, 9)
// EXPECT_INT_DEFINE_TEST(Define_LUA_MINSTACK, LUA_MINSTACK, 20)
// EXPECT_INT_DEFINE_TEST(Define_LUA_RIDX_GLOBALS, LUA_RIDX_GLOBALS, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_RIDX_MAINTHREAD, LUA_RIDX_MAINTHREAD, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_RIDX_LAST, LUA_RIDX_LAST, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPADD, LUA_OPADD, 0)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPSUB, LUA_OPSUB, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPMUL, LUA_OPMUL, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPMOD, LUA_OPMOD, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPPOW, LUA_OPPOW, 4)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPDIV, LUA_OPDIV, 5)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPIDIV, LUA_OPIDIV, 6)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPBAND, LUA_OPBAND, 7)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPBOR, LUA_OPBOR, 8)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPBXOR, LUA_OPBXOR, 9)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPSHL, LUA_OPSHL, 10)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPSHR, LUA_OPSHR, 11)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPUNM, LUA_OPUNM, 12)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPBNOT, LUA_OPBNOT, 13)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPEQ, LUA_OPEQ, 0)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPLT, LUA_OPLT, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_OPLE, LUA_OPLE, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCSTOP, LUA_GCSTOP, 0)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCRESTART, LUA_GCRESTART, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCCOLLECT, LUA_GCCOLLECT, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCCOUNT, LUA_GCCOUNT, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCCOUNTB, LUA_GCCOUNTB, 4)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCSTEP, LUA_GCSTEP, 5)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCISRUNNING, LUA_GCISRUNNING, 6)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCGEN, LUA_GCGEN, 7)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCINC, LUA_GCINC, 8)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCPARAM, LUA_GCPARAM, 9)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCPMINORMUL, LUA_GCPMINORMUL, 0)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCPMAJORMINOR, LUA_GCPMAJORMINOR, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCPMINORMAJOR, LUA_GCPMINORMAJOR, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCPPAUSE, LUA_GCPPAUSE, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCPSTEPMUL, LUA_GCPSTEPMUL, 4)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCPSTEPSIZE, LUA_GCPSTEPSIZE, 5)
// EXPECT_INT_DEFINE_TEST(Define_LUA_GCPN, LUA_GCPN, 6)
// EXPECT_INT_DEFINE_TEST(Define_LUA_N2SBUFFSZ, LUA_N2SBUFFSZ, 64)
// EXPECT_INT_DEFINE_TEST(Define_LUA_HOOKCALL, LUA_HOOKCALL, 0)
// EXPECT_INT_DEFINE_TEST(Define_LUA_HOOKRET, LUA_HOOKRET, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_HOOKLINE, LUA_HOOKLINE, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_HOOKCOUNT, LUA_HOOKCOUNT, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_HOOKTAILCALL, LUA_HOOKTAILCALL, 4)
// EXPECT_INT_DEFINE_TEST(Define_LUA_MASKCALL, LUA_MASKCALL, 1 << LUA_HOOKCALL)
// EXPECT_INT_DEFINE_TEST(Define_LUA_MASKRET, LUA_MASKRET, 1 << LUA_HOOKRET)
// EXPECT_INT_DEFINE_TEST(Define_LUA_MASKLINE, LUA_MASKLINE, 1 << LUA_HOOKLINE)
// EXPECT_INT_DEFINE_TEST(Define_LUA_MASKCOUNT, LUA_MASKCOUNT, 1 << LUA_HOOKCOUNT)
// EXPECT_STR_DEFINE_TEST(Define_LUA_VERSION_MAJOR, LUA_VERSION_MAJOR, "5")
// EXPECT_STR_DEFINE_TEST(Define_LUA_VERSION_MINOR, LUA_VERSION_MINOR, "5")
// EXPECT_STR_DEFINE_TEST(Define_LUA_VERSION_RELEASE, LUA_VERSION_RELEASE, "0")
// EXPECT_STR_DEFINE_TEST(Define_LUA_VERSION, LUA_VERSION, "Lua 5.5")
// EXPECT_STR_DEFINE_TEST(Define_LUA_RELEASE, LUA_RELEASE, "Lua 5.5.0")
//
// EXPECT_STR_DEFINE_TEST(Define_LUA_GNAME, LUA_GNAME, "_G")
// EXPECT_INT_DEFINE_TEST(Define_LUA_ERRFILE, LUA_ERRFILE, LUA_ERRERR + 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_LOADED_TABLE, LUA_LOADED_TABLE, "_LOADED")
// EXPECT_STR_DEFINE_TEST(Define_LUA_PRELOAD_TABLE, LUA_PRELOAD_TABLE, "_PRELOAD")
// [Test] public void Define_LUAL_NUMSIZES() {
//   Assert.That(LUAL_NUMSIZES, Is.EqualTo(sizeof(lua_Integer) * 16 + sizeof(lua_Number)));
// }
// EXPECT_INT_DEFINE_TEST(Define_LUA_NOREF, LUA_NOREF, -2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_REFNIL, LUA_REFNIL, -1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_FILEHANDLE, LUA_FILEHANDLE, "FILE*")
//
// EXPECT_STR_DEFINE_TEST(Define_LUA_VERSUFFIX, LUA_VERSUFFIX, "_5_5")
// EXPECT_INT_DEFINE_TEST(Define_LUA_GLIBK, LUA_GLIBK, 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_LOADLIBNAME, LUA_LOADLIBNAME, "package")
// EXPECT_INT_DEFINE_TEST(Define_LUA_LOADLIBK, LUA_LOADLIBK, LUA_GLIBK << 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_COLIBNAME, LUA_COLIBNAME, "coroutine")
// EXPECT_INT_DEFINE_TEST(Define_LUA_COLIBK, LUA_COLIBK, LUA_LOADLIBK << 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_DBLIBNAME, LUA_DBLIBNAME, "debug")
// EXPECT_INT_DEFINE_TEST(Define_LUA_DBLIBK, LUA_DBLIBK, LUA_COLIBK << 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_IOLIBNAME, LUA_IOLIBNAME, "io")
// EXPECT_INT_DEFINE_TEST(Define_LUA_IOLIBK, LUA_IOLIBK, LUA_DBLIBK << 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_MATHLIBNAME, LUA_MATHLIBNAME, "math")
// EXPECT_INT_DEFINE_TEST(Define_LUA_MATHLIBK, LUA_MATHLIBK, LUA_IOLIBK << 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_OSLIBNAME, LUA_OSLIBNAME, "os")
// EXPECT_INT_DEFINE_TEST(Define_LUA_OSLIBK, LUA_OSLIBK, LUA_MATHLIBK << 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_STRLIBNAME, LUA_STRLIBNAME, "string")
// EXPECT_INT_DEFINE_TEST(Define_LUA_STRLIBK, LUA_STRLIBK, LUA_OSLIBK << 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_TABLIBNAME, LUA_TABLIBNAME, "table")
// EXPECT_INT_DEFINE_TEST(Define_LUA_TABLIBK, LUA_TABLIBK, LUA_STRLIBK << 1)
// EXPECT_STR_DEFINE_TEST(Define_LUA_UTF8LIBNAME, LUA_UTF8LIBNAME, "utf8")
// EXPECT_INT_DEFINE_TEST(Define_LUA_UTF8LIBK, LUA_UTF8LIBK, LUA_TABLIBK << 1)
//
// EXPECT_INT_DEFINE_TEST(Define_LUA_INT_INT, LUA_INT_INT, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_INT_LONG, LUA_INT_LONG, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_INT_LONGLONG, LUA_INT_LONGLONG, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_FLOAT_FLOAT, LUA_FLOAT_FLOAT, 1)
// EXPECT_INT_DEFINE_TEST(Define_LUA_FLOAT_DOUBLE, LUA_FLOAT_DOUBLE, 2)
// EXPECT_INT_DEFINE_TEST(Define_LUA_FLOAT_LONGDOUBLE, LUA_FLOAT_LONGDOUBLE, 3)
// EXPECT_INT_DEFINE_TEST(Define_LUA_INT_DEFAULT, LUA_INT_DEFAULT, LUA_INT_LONGLONG)
// EXPECT_INT_DEFINE_TEST(Define_LUA_FLOAT_DEFAULT, LUA_FLOAT_DEFAULT, LUA_FLOAT_DOUBLE)
// [Test] public void Define_LUA_INT_TYPE() {
//   Assert.That(LUA_INT_TYPE == LUA_INT_INT || LUA_INT_TYPE == LUA_INT_LONG ||
//               LUA_INT_TYPE == LUA_INT_LONGLONG);
// }
// [Test] public void Define_LUA_FLOAT_TYPE() {
//   Assert.That(LUA_FLOAT_TYPE == LUA_FLOAT_FLOAT ||
//               LUA_FLOAT_TYPE == LUA_FLOAT_DOUBLE ||
//               LUA_FLOAT_TYPE == LUA_FLOAT_LONGDOUBLE);
// }
// EXPECT_STR_DEFINE_TEST(Define_LUA_PATH_SEP, LUA_PATH_SEP, ";")
// EXPECT_STR_DEFINE_TEST(Define_LUA_PATH_MARK, LUA_PATH_MARK, "?")
// EXPECT_STR_DEFINE_TEST(Define_LUA_EXEC_DIR, LUA_EXEC_DIR, "!")
// EXPECT_STR_DEFINE_TEST(Define_LUA_IGMARK, LUA_IGMARK, "-")
// EXPECT_INT_DEFINE_TEST(Define_LUA_IDSIZE, LUA_IDSIZE, 60)

    [Test]
    public void Function_lua_newstate()
    {
        AllocStats stats;
        lua_State* L = lua_newstate(&CountingAlloc, &stats, 123);
        Assert.That(L, Is.Not.Null);
        Assert.That(stats.alloc_calls, Is.GreaterThan(0));

        lua_close(L);

        AllocStats failStats;
        failStats.fail_alloc = true;
        L = lua_newstate(&CountingAlloc, &failStats, 123);
        Assert.That(L, Is.Null);
    }

    [Test]
    public void Function_lua_close()
    {
        AllocStats stats;
        lua_State* L = lua_newstate(&CountingAlloc, &stats, 123);
        Assert.That(L, Is.Not.Null);
        lua_close(L);
        Assert.That(stats.free_calls, Is.GreaterThan(0));
    }

    [Test]
    public void Function_lua_newthread()
    {
        using LuaState state = new();
        lua_State* co = lua_newthread(state);

        {
            Assert.That(co, Is.Not.Null);
            Assert.That(lua_type(state, -1), Is.EqualTo(LUA_TTHREAD));
            Assert.That(lua_tothread(state, -1), Is.EqualTo(co));
        }
    }

    [Test]
    public void Function_lua_closethread()
    {
        using LuaState state = new();
        lua_State* co = lua_newthread(state);

        {
            Assert.That(co, Is.Not.Null);
            Assert.That(lua_closethread(co, state), Is.EqualTo(LUA_OK));
        }

        lua_State* failing = lua_newthread(state);
        lua_pushcfunction(failing, &ErrorFunction);
        int nres = 0;

        {
            Assert.That(lua_resume(failing, state, 0, &nres), Is.EqualTo(LUA_ERRRUN));
            Assert.That(lua_closethread(failing, state), Is.EqualTo(LUA_ERRRUN));
        }
    }

    [Test]
    public void Function_lua_atpanic()
    {
        AllocStats stats;
        lua_State* L = lua_newstate(&CountingAlloc, &stats, 123);
        Assert.That(L, Is.Not.Null);
        delegate*<lua_State*, int> errorFunction = &ErrorFunction;
        delegate*<lua_State*, int> previous = lua_atpanic(L, errorFunction);

        {
            Assert.That(previous, Is.Null);
            Assert.That(lua_atpanic(L, &ReturnZero), Is.EqualTo(errorFunction));
        }
        lua_close(L);
    }

    [Test]
    public void Function_lua_version()
    {
        using LuaState state = new();
        Assert.That(lua_version(state), Is.EqualTo(LUA_VERSION_NUM));
    }

    [Test]
    public void Function_lua_absindex()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 2);

        {
            Assert.That(lua_absindex(state, -1), Is.EqualTo(2));
            Assert.That(lua_absindex(state, 1), Is.EqualTo(1));
            Assert.That(lua_absindex(state, LUA_REGISTRYINDEX), Is.EqualTo(LUA_REGISTRYINDEX));
        }
    }

    [Test]
    public void Function_lua_gettop()
    {
        using LuaState state = new();
        Assert.That(lua_gettop(state), Is.EqualTo(0));
        lua_pushnil(state);
        Assert.That(lua_gettop(state), Is.EqualTo(1));
    }

    [Test]
    public void Function_lua_settop()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_settop(state, 3);

        {
            Assert.That(lua_gettop(state), Is.EqualTo(3));
            Assert.That(lua_isnil(state, 2));
            Assert.That(lua_isnil(state, 3));
        }
        lua_settop(state, 1);
        Assert.That(lua_gettop(state), Is.EqualTo(1));
    }

    [Test]
    public void Function_lua_pushvalue()
    {
        using LuaState state = new();
        lua_pushliteral(state, "same");
        lua_pushvalue(state, 1);

        {
            Assert.That(lua_gettop(state), Is.EqualTo(2));
            Assert.That(lua_rawequal(state, 1, 2));
        }
    }

    [Test]
    public void Function_lua_rotate()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 2);
        lua_pushinteger(state, 3);
        lua_rotate(state, 1, 1);

        {
            Assert.That(lua_tointeger(state, 1), Is.EqualTo(3));
            Assert.That(lua_tointeger(state, 2), Is.EqualTo(1));
            Assert.That(lua_tointeger(state, 3), Is.EqualTo(2));
        }
    }

    [Test]
    public void Function_lua_copy()
    {
        using LuaState state = new();
        lua_pushinteger(state, 10);
        lua_pushinteger(state, 20);
        lua_copy(state, 1, 2);
        Assert.That(lua_tointeger(state, 2), Is.EqualTo(10));
    }

    [Test]
    public void Function_lua_checkstack()
    {
        using LuaState state = new();

        {
            Assert.That(lua_checkstack(state, LUA_MINSTACK));
            Assert.That(lua_checkstack(state, int.MaxValue), Is.False);
        }
    }

    [Test]
    public void Function_lua_xmove()
    {
        using LuaState state = new();
        lua_State* co = lua_newthread(state);
        lua_pushinteger(state, 77);
        lua_xmove(state, co, 1);

        {
            Assert.That(lua_gettop(state), Is.EqualTo(1));
            Assert.That(lua_gettop(co), Is.EqualTo(1));
            Assert.That(lua_tointeger(co, -1), Is.EqualTo(77));
        }
    }

    [Test]
    public void Function_lua_isnumber()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushliteral(state, "2");
        lua_pushliteral(state, "not-number");

        {
            Assert.That(lua_isnumber(state, 1));
            Assert.That(lua_isnumber(state, 2));
            Assert.That(lua_isnumber(state, 3), Is.False);
        }
    }

    [Test]
    public void Function_lua_isstring()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushliteral(state, "text");
        lua_newtable(state);

        {
            Assert.That(lua_isstring(state, 1));
            Assert.That(lua_isstring(state, 2));
            Assert.That(lua_isstring(state, 3), Is.False);
        }
    }

    [Test]
    public void Function_lua_iscfunction()
    {
        using LuaState state = new();
        lua_pushcfunction(state, &ReturnOne);
        lua_newtable(state);

        {
            Assert.That(lua_iscfunction(state, 1));
            Assert.That(lua_iscfunction(state, 2), Is.False);
        }
    }

    [Test]
    public void Function_lua_isinteger()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushnumber(state, 1.5);

        {
            Assert.That(lua_isinteger(state, 1));
            Assert.That(lua_isinteger(state, 2), Is.False);
        }
    }

    [Test]
    public void Function_lua_isuserdata()
    {
        using LuaState state = new();
        int value = 0;
        lua_newuserdatauv(state, 1, 0);
        lua_pushlightuserdata(state, &value);
        lua_pushnil(state);

        {
            Assert.That(lua_isuserdata(state, 1));
            Assert.That(lua_isuserdata(state, 2));
            Assert.That(lua_isuserdata(state, 3), Is.False);
        }
    }

    [Test]
    public void Function_lua_type()
    {
        using LuaState state = new();
        lua_pushliteral(state, "text");

        {
            Assert.That(lua_type(state, 1), Is.EqualTo(LUA_TSTRING));
            Assert.That(lua_type(state, 2), Is.EqualTo(LUA_TNONE));
        }
    }

    [Test]
    public void Function_lua_typename()
    {
        using LuaState state = new();

        {
            Assert.That(lua_typename(state, LUA_TNIL), Is.EqualTo("nil"));
            Assert.That(lua_typename(state, LUA_TNONE), Is.Not.Null);
        }
    }

    [Test]
    public void Function_lua_tonumberx()
    {
        using LuaState state = new();
        lua_pushliteral(state, "12.5");

        {
            Assert.That(lua_tonumberx(state, 1, out bool isnum), Is.EqualTo(12.5));
            Assert.That(isnum, Is.True);
        }
        lua_pushliteral(state, "bad");

        {
            Assert.That(lua_tonumberx(state, 2, out bool isnum), Is.EqualTo(0));
            Assert.That(isnum, Is.False);
        }
    }

    [Test]
    public void Function_lua_tointegerx()
    {
        using LuaState state = new();
        lua_pushinteger(state, 42);

        {
            Assert.That(lua_tointegerx(state, 1, out bool isnum), Is.EqualTo(42));
            Assert.That(isnum, Is.True);
        }
        lua_pushnumber(state, 1.5);

        {
            Assert.That(lua_tointegerx(state, 2, out bool isnum), Is.EqualTo(0));
            Assert.That(isnum, Is.False);
        }
    }

    [Test]
    public void Function_lua_toboolean()
    {
        using LuaState state = new();
        lua_pushnil(state);
        lua_pushboolean(state, false);
        lua_pushinteger(state, 0);

        {
            Assert.That(lua_toboolean(state, 1), Is.False);
            Assert.That(lua_toboolean(state, 2), Is.False);
            Assert.That(lua_toboolean(state, 3));
        }
    }

    [Test]
    public void Function_lua_tolstring()
    {
        using LuaState state = new();
        lua_pushlstring(state, "a\0b");
        byte* text = lua_tolstring(state, 1, out int len);

        Assert.That(text, Is.Not.Null);
        Assert.That(len, Is.EqualTo(3u));

        lua_newtable(state);
        Assert.That(lua_tolstring(state, 2, out len), Is.Null);
    }

    [Test]
    public void Function_lua_rawlen()
    {
        using LuaState state = new();
        lua_pushliteral(state, "abcd");
        Assert.That(lua_rawlen(state, 1), Is.EqualTo(4u));
        lua_newtable(state);
        lua_pushinteger(state, 1);
        lua_rawseti(state, 2, 1);
        lua_pushinteger(state, 2);
        lua_rawseti(state, 2, 2);
        Assert.That(lua_rawlen(state, 2), Is.EqualTo(2u));
        lua_pushnil(state);
        Assert.That(lua_rawlen(state, 3), Is.EqualTo(0u));
    }

    [Test]
    public void Function_lua_tocfunction()
    {
        using LuaState state = new();
        delegate*<lua_State*, int> returnOne = &ReturnOne;
        lua_pushcfunction(state, returnOne);
        lua_pushnil(state);
        Assert.That(lua_tocfunction(state, 1), Is.EqualTo(returnOne));
        Assert.That(lua_tocfunction(state, 2), Is.Null);
    }

    [Test]
    public void Function_lua_touserdata()
    {
        using LuaState state = new();
        int value = 0;
        lua_pushlightuserdata(state, &value);
        void* full = lua_newuserdatauv(state, 4, 0);
        lua_pushnil(state);
        Assert.That(lua_touserdata(state, 1), Is.EqualTo(&value));
        Assert.That(lua_touserdata(state, 2), Is.EqualTo(full));
        Assert.That(lua_touserdata(state, 3), Is.Null);
    }

    [Test]
    public void Function_lua_tothread()
    {
        using LuaState state = new();
        lua_pushthread(state);
        Assert.That(lua_tothread(state, -1), Is.EqualTo(state.get()));
        lua_pushnil(state);
        Assert.That(lua_tothread(state, -1), Is.Null);
    }

    [Test]
    public void Function_lua_topointer()
    {
        using LuaState state = new();
        lua_newtable(state);
        Assert.That(lua_topointer(state, 1), Is.Not.Null);
        lua_pushnil(state);
        Assert.That(lua_topointer(state, 2), Is.Null);
    }

    [Test]
    public void Function_lua_arith()
    {
        using LuaState state = new();
        lua_pushinteger(state, 20);
        lua_pushinteger(state, 22);
        lua_arith(state, LUA_OPADD);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(42));
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &ArithFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_rawequal()
    {
        using LuaState state = new();
        lua_pushinteger(state, 10);
        lua_pushinteger(state, 10);
        lua_pushinteger(state, 11);

        {
            Assert.That(lua_rawequal(state, 1, 2));
            Assert.That(lua_rawequal(state, 1, 3), Is.False);
            Assert.That(lua_rawequal(state, 1, 19), Is.False);
        }
    }

    [Test]
    public void Function_lua_compare()
    {
        using LuaState state = new();
        lua_pushinteger(state, 10);
        lua_pushinteger(state, 20);

        Assert.That(lua_compare(state, 1, 1, LUA_OPEQ), Is.True);
        Assert.That(lua_compare(state, 1, 2, LUA_OPLT), Is.True);
        Assert.That(lua_compare(state, 1, 2, LUA_OPLE), Is.True);
        Assert.That(lua_compare(state, 1, 19, LUA_OPEQ), Is.False);
    }

    [Test]
    public void Function_lua_pushnil()
    {
        using LuaState state = new();
        lua_pushnil(state);
        Assert.That(lua_isnil(state, -1));
    }

    [Test]
    public void Function_lua_pushnumber()
    {
        using LuaState state = new();
        lua_pushnumber(state, 2.5);
        Assert.That(lua_tonumber(state, -1), Is.EqualTo(2.5));
    }

    [Test]
    public void Function_lua_pushinteger()
    {
        using LuaState state = new();
        lua_pushinteger(state, 42);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(42));
    }

    [Test]
    public void Function_lua_pushlstring()
    {
        using LuaState state = new();
        lua_pushlstring(state, "a\0b");
        byte* actual = lua_tolstring(state, -1, out int len);
        Assert.That(actual, Is.Not.Null);

        {
            Assert.That(len, Is.EqualTo(3));
            Assert.That(actual[0], Is.EqualTo('a'));
            Assert.That(actual[1], Is.EqualTo('\0'));
            Assert.That(actual[2], Is.EqualTo('b'));
        }
    }

    [Test]
    public void Function_lua_pushexternalstring()
    {
        using LuaState state = new();
        ReadOnlySpan<byte> text = "this external string is long enough to avoid short-string interning"u8;
        lua_pushexternalstring(
            state,
            text.ToPointer(),
            text.Length,
            null,
            null);
        Assert.That(lua_tostring(state, -1), Is.EqualTo(text.ToPointer()));
    }

    [Test]
    public void Function_lua_pushstring()
    {
        using LuaState state = new();
        lua_pushstring(state, "abc");
        lua_pushstring(state, (string?)null);
        Assert.That(lua_isnil(state, -1));
    }

    [Test]
    public void Function_lua_pushvfstring()
    {
        using LuaState state = new();
        string pushed = lua_pushfstring(state, "%s %d", "value", 7);

        {
            Assert.That(pushed, Is.Not.Null);
            Assert.That(pushed, Is.EqualTo("value 7"));
        }
    }

    [Test]
    public void Function_lua_pushfstring()
    {
        using LuaState state = new();
        string pushed = lua_pushfstring(state, "%s %d", "value", 8);

        {
            Assert.That(pushed, Is.Not.Null);
            Assert.That(pushed, Is.EqualTo("value 8"));
        }
    }

    [Test]
    public void Function_lua_pushcclosure()
    {
        using LuaState state = new();
        lua_pushinteger(state, 42);
        lua_pushcclosure(state, &ReturnIntegerUpvalue, 1);

        {
            Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(42));
        }
    }

    [Test]
    public void Function_lua_pushboolean()
    {
        using LuaState state = new();
        lua_pushboolean(state, false);
        lua_pushboolean(state, true);

        {
            Assert.That(lua_isboolean(state, 1));
            Assert.That(lua_toboolean(state, 1), Is.False);
            Assert.That(lua_toboolean(state, 2), Is.True);
        }
    }

    [Test]
    public void Function_lua_pushlightuserdata()
    {
        using LuaState state = new();
        int value = 0;
        lua_pushlightuserdata(state, &value);
        Assert.That(lua_touserdata(state, -1), Is.EqualTo(&value));
    }

    [Test]
    public void Function_lua_pushthread()
    {
        using LuaState state = new();
        Assert.That(lua_pushthread(state));
        Assert.That(lua_tothread(state, -1), Is.EqualTo(state.get()));
        lua_State* co = lua_newthread(state);
        Assert.That(lua_pushthread(co), Is.False);
    }

    [Test]
    public void Function_lua_getglobal()
    {
        using LuaState state = new();
        lua_pushinteger(state, 42);
        lua_setglobal(state, "answer");

        {
            Assert.That(lua_getglobal(state, "answer"), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(42));
        }
        lua_pop(state, 1);
        Assert.That(lua_getglobal(state, "missing_global"), Is.EqualTo(LUA_TNIL));
    }

    [Test]
    public void Function_lua_gettable()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushliteral(state, "key");
        lua_pushinteger(state, 12);
        lua_settable(state, 1);
        lua_pushliteral(state, "key");

        {
            Assert.That(lua_gettable(state, 1), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(12));
        }
        lua_settop(state, 1);
        Assert.That(ProtectedCall(state, &GetTableFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_getfield()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushinteger(state, 33);
        lua_setfield(state, 1, "field");

        {
            Assert.That(lua_getfield(state, 1, "field"), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(33));
        }
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &GetFieldFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_geti()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushinteger(state, 44);
        lua_seti(state, 1, 2);

        {
            Assert.That(lua_geti(state, 1, 2), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(44));
        }
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &GetIFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_rawget()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushliteral(state, "key");
        lua_pushinteger(state, 55);
        lua_rawset(state, 1);
        lua_pushliteral(state, "key");

        {
            Assert.That(lua_rawget(state, 1), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(55));
        }
        lua_settop(state, 1);
        lua_pushliteral(state, "missing");
        Assert.That(lua_rawget(state, 1), Is.EqualTo(LUA_TNIL));
    }

    [Test]
    public void Function_lua_rawgeti()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushinteger(state, 66);
        lua_rawseti(state, 1, 3);

        {
            Assert.That(lua_rawgeti(state, 1, 3), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(66));
        }
        lua_settop(state, 1);
        Assert.That(lua_rawgeti(state, 1, 99), Is.EqualTo(LUA_TNIL));
    }

    [Test]
    public void Function_lua_rawgetp()
    {
        using LuaState state = new();
        int key = 0;
        int missing = 0;
        lua_newtable(state);
        lua_pushinteger(state, 77);
        lua_rawsetp(state, 1, &key);

        {
            Assert.That(lua_rawgetp(state, 1, &key), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(77));
        }
        lua_settop(state, 1);
        Assert.That(lua_rawgetp(state, 1, &missing), Is.EqualTo(LUA_TNIL));
    }

    [Test]
    public void Function_lua_createtable()
    {
        using LuaState state = new();
        lua_createtable(state, 2, 1);
        Assert.That(lua_istable(state, -1));
        lua_pushinteger(state, 1);
        lua_rawseti(state, -2, 1);
        Assert.That(lua_rawlen(state, -1), Is.EqualTo(1u));
    }

    [Test]
    public void Function_lua_newuserdatauv()
    {
        using LuaState state = new();
        void* userdata = lua_newuserdatauv(state, 8, 2);

        {
            Assert.That(userdata, Is.Not.Null);
            Assert.That(lua_isuserdata(state, -1));
            Assert.That(lua_getiuservalue(state, -1, 1), Is.EqualTo(LUA_TNIL));
        }
    }

    [Test]
    public void Function_lua_getmetatable()
    {
        using LuaState state = new();
        lua_newtable(state);
        Assert.That(lua_getmetatable(state, -1), Is.False);
        lua_newtable(state);
        lua_setmetatable(state, -2);

        {
            Assert.That(lua_getmetatable(state, -1));
            Assert.That(lua_istable(state, -1));
        }
    }

    [Test]
    public void Function_lua_getiuservalue()
    {
        using LuaState state = new();
        lua_newuserdatauv(state, 1, 1);
        lua_pushinteger(state, 88);

        {
            Assert.That(lua_setiuservalue(state, -2, 1), Is.True);
            Assert.That(lua_getiuservalue(state, -1, 1), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(88));
        }
        lua_pop(state, 1);

        {
            Assert.That(lua_getiuservalue(state, -1, 2), Is.EqualTo(LUA_TNONE));
            Assert.That(lua_isnil(state, -1));
        }
    }

    [Test]
    public void Function_lua_setglobal()
    {
        using LuaState state = new();
        lua_pushinteger(state, 99);
        lua_setglobal(state, "setglobal_value");
        lua_getglobal(state, "setglobal_value");
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(99));
        lua_pushnil(state);
        lua_setglobal(state, "setglobal_value");
        lua_getglobal(state, "setglobal_value");
        Assert.That(lua_isnil(state, -1));
    }

    [Test]
    public void Function_lua_settable()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushliteral(state, "key");
        lua_pushinteger(state, 123);
        lua_settable(state, 1);
        lua_getfield(state, 1, "key");
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(123));
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &SetTableFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_setfield()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushinteger(state, 234);
        lua_setfield(state, 1, "field");
        lua_getfield(state, 1, "field");
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(234));
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &SetFieldFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_seti()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushinteger(state, 345);
        lua_seti(state, 1, 4);
        lua_geti(state, 1, 4);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(345));
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &SetIFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_rawset()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushliteral(state, "key");
        lua_pushinteger(state, 456);
        lua_rawset(state, 1);
        lua_getfield(state, 1, "key");
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(456));
    }

    [Test]
    public void Function_lua_rawseti()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushinteger(state, 567);
        lua_rawseti(state, 1, 5);
        lua_rawgeti(state, 1, 5);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(567));
    }

    [Test]
    public void Function_lua_rawsetp()
    {
        using LuaState state = new();
        int key = 0;
        lua_newtable(state);
        lua_pushinteger(state, 678);
        lua_rawsetp(state, 1, &key);
        lua_rawgetp(state, 1, &key);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(678));
    }

    [Test]
    public void Function_lua_setmetatable()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_newtable(state);

        {
            Assert.That(lua_setmetatable(state, 1), Is.True);
            Assert.That(lua_getmetatable(state, 1), Is.True);
        }
        lua_pop(state, 1);
        lua_pushnil(state);

        {
            Assert.That(lua_setmetatable(state, 1), Is.True);
            Assert.That(lua_getmetatable(state, 1), Is.False);
        }
    }

    [Test]
    public void Function_lua_setiuservalue()
    {
        using LuaState state = new();
        lua_newuserdatauv(state, 1, 1);
        lua_pushinteger(state, 789);
        Assert.That(lua_setiuservalue(state, -2, 1));
        lua_getiuservalue(state, -1, 1);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(789));
        lua_pop(state, 1);
        lua_pushinteger(state, 1);
        Assert.That(lua_setiuservalue(state, -2, 2), Is.False);
    }

    [Test]
    public void Function_lua_callk()
    {
        using LuaState state = new();

        {
            Assert.That(ProtectedCall(state, &CallKSuccess, 1), Is.EqualTo(LUA_OK));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(42));
        }
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &CallKFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_pcallk()
    {
        using LuaState state = new();
        lua_pushcfunction(state, &ReturnOne);

        {
            Assert.That(lua_pcallk(state, 0, 1, 0, 0, null), Is.EqualTo(LUA_OK));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(1));
        }
        lua_settop(state, 0);
        lua_pushcfunction(state, &ErrorFunction);
        Assert.That(lua_pcallk(state, 0, 0, 0, 0, null), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_load()
    {
        using LuaState state = new();
        Assert.That(LoadChunk(state, "return 64"), Is.EqualTo(LUA_OK));

        {
            Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(64));
        }
        lua_settop(state, 0);
        Assert.That(LoadChunk(state, "return +", "=(bad)"), Is.EqualTo(LUA_ERRSYNTAX));
    }

    [Test]
    public void Function_lua_dump()
    {
        using LuaState state = new();
        Assert.That(LoadChunk(state, "return 71"), Is.EqualTo(LUA_OK));

        DumpState dump = new();
        using (GCHandle<DumpState> handle = new(dump))
        {
            int result = lua_dump(state, &DumpWriter, handle.ToPointer(), false);

            Assert.That(result, Is.EqualTo(0));
            Assert.That(dump.bytes, Is.Not.Empty);
        }

        DumpState fail = new()
        {
            fail = true,
        };
        using (GCHandle<DumpState> handle = new(fail))
        {
            int result = lua_dump(state, &DumpWriter, handle.ToPointer(), false);
            Assert.That(result, Is.Not.EqualTo(0));
        }
    }

    [Test]
    public void Function_lua_yieldk()
    {
        using LuaState state = new();
        lua_State* co = lua_newthread(state);
        lua_pushcfunction(co, &YieldKFunction);
        int nres = 0;

        {
            Assert.That(lua_resume(co, state, 0, &nres), Is.EqualTo(LUA_YIELD));
            Assert.That(nres, Is.EqualTo(1));
            Assert.That(lua_tointeger(co, -1), Is.EqualTo(7));
        }
        lua_pop(co, 1);

        {
            Assert.That(lua_resume(co, state, 0, &nres), Is.EqualTo(LUA_OK));
            Assert.That(nres, Is.EqualTo(2));
        }

        {
            Assert.That(lua_tointeger(co, -2), Is.EqualTo(LUA_YIELD));
            Assert.That(lua_tointeger(co, -1), Is.EqualTo(123));

            Assert.That(ProtectedCall(state, &YieldKFunction, 0), Is.EqualTo(LUA_ERRRUN));
        }
    }

    [Test]
    public void Function_lua_resume()
    {
        using LuaState state = new();
        lua_State* co = lua_newthread(state);
        lua_pushcfunction(co, &ReturnOne);
        int nres = 0;

        {
            Assert.That(lua_resume(co, state, 0, &nres), Is.EqualTo(LUA_OK));
            Assert.That(nres, Is.EqualTo(1));
            Assert.That(lua_tointeger(co, -1), Is.EqualTo(1));
        }

        lua_State* failing = lua_newthread(state);
        lua_pushcfunction(failing, &ErrorFunction);
        Assert.That(lua_resume(failing, state, 0, &nres), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_status()
    {
        using LuaState state = new();
        Assert.That(lua_status(state), Is.EqualTo(LUA_OK));
        lua_State* co = lua_newthread(state);
        lua_pushcfunction(co, &YieldFunction);
        int nres = 0;

        {
            Assert.That(lua_resume(co, state, 0, &nres), Is.EqualTo(LUA_YIELD));
            Assert.That(lua_status(co), Is.EqualTo(LUA_YIELD));
        }
    }

    [Test]
    public void Function_lua_isyieldable()
    {
        using LuaState state = new();
        Assert.That(lua_isyieldable(state), Is.False);
        lua_State* co = lua_newthread(state);
        lua_pushcfunction(co, &IsYieldableFunction);
        int nres = 0;

        {
            Assert.That(lua_resume(co, state, 0, &nres), Is.EqualTo(LUA_OK));
            Assert.That(nres, Is.EqualTo(1));
            Assert.That(lua_toboolean(co, -1));
        }
    }

    private sealed class WarningState
    {
        public int calls;
        public string? message;
    }

    [Test]
    public void Function_lua_setwarnf()
    {
        using LuaState state = new();
        WarningState warnings = new();
        using GCHandle<WarningState> handle = new(warnings);
        lua_setwarnf(state, &warnf, handle.ToPointer());
        lua_warning(state, "a", true);
        lua_warning(state, "b", false);

        Assert.That(warnings.calls, Is.EqualTo(2));
        Assert.That(warnings.message, Is.EqualTo("ab|"));

        static void warnf(void* ud, ReadOnlySpan<char> msg, bool tocont)
        {
            WarningState state = GCHandle<WarningState>.FromIntPtr((nint)ud).Target;
            ++state.calls;
            state.message += msg.ToString();
            if (!tocont)
            {
                state.message += "|";
            }
        }
    }

    [Test]
    public void Function_lua_warning()
    {
        using LuaState state = new();
        int calls = 0;
        lua_setwarnf(state, &warnf, &calls);
        lua_warning(state, "message", false);
        Assert.That(calls, Is.EqualTo(1));
        lua_setwarnf(state, null, null);
        lua_warning(state, "ignored", false);
        Assert.That(calls, Is.EqualTo(1));

        static void warnf(void* ud, ReadOnlySpan<char> msg, bool tocont)
        {
            ++*(int*)(ud);
        }
    }

    [Test]
    public void Function_lua_gc()
    {
        using LuaState state = new();

        {
            Assert.That(lua_gc(state, LUA_GCCOUNT), Is.GreaterThanOrEqualTo(0));
            Assert.That(lua_gc(state, LUA_GCCOUNTB), Is.GreaterThanOrEqualTo(0));
            Assert.That(lua_gc(state, LUA_GCSTOP), Is.EqualTo(0));
            Assert.That(lua_gc(state, LUA_GCRESTART), Is.EqualTo(0));
            Assert.That(lua_gc(state, LUA_GCCOLLECT), Is.EqualTo(0));
            Assert.That(lua_gc(state, LUA_GCISRUNNING), Is.GreaterThanOrEqualTo(0));
            Assert.That(lua_gc(state, 9999), Is.EqualTo(-1));
        }
    }

    [Test]
    public void Function_lua_error()
    {
        using LuaState state = new();

        {
            Assert.That(ProtectedCall(state, &ErrorFunction, 0), Is.EqualTo(LUA_ERRRUN));
            Assert.That(StackString(state, -1), Contains.Substring("intentional failure"));
        }
    }

    [Test]
    public void Function_lua_next()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushliteral(state, "key");
        lua_pushinteger(state, 1);
        lua_settable(state, 1);
        lua_pushnil(state);

        {
            Assert.That(lua_next(state, 1), Is.True);
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(1));
        }
        lua_pop(state, 1);
        Assert.That(lua_next(state, 1), Is.False);
    }

    [Test]
    public void Function_lua_concat()
    {
        using LuaState state = new();
        lua_pushliteral(state, "hello");
        lua_pushliteral(state, " ");
        lua_pushliteral(state, "lua");
        lua_concat(state, 3);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("hello lua"));
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &ConcatFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_len()
    {
        using LuaState state = new();
        lua_pushliteral(state, "abcd");
        lua_len(state, -1);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(4));
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &LenFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_numbertocstring()
    {
        using LuaState state = new();
        Span<byte> buffer = stackalloc byte[LUA_N2SBUFFSZ];
        
        lua_pushinteger(state, 123);

        int luaNumbertocstring = lua_numbertocstring(state, -1, buffer);
        Assert.That(luaNumbertocstring, Is.GreaterThan(0));
        Assert.That(Encoding.UTF8.GetString(buffer).TrimEnd('\0'), Is.EqualTo("123"));
        lua_newtable(state);
        Assert.That(lua_numbertocstring(state, -1, buffer), Is.Zero);
    }

    [Test]
    public void Function_lua_stringtonumber()
    {
        using LuaState state = new();

        {
            Assert.That(lua_stringtonumber(state, "123"), Is.EqualTo(4u));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(123));
        }
        int top = lua_gettop(state);

        {
            Assert.That(lua_stringtonumber(state, "abc"), Is.EqualTo(0u));
            Assert.That(lua_gettop(state), Is.EqualTo(top));
        }
    }

    [Test]
    public void Function_lua_getallocf()
    {
        using LuaState state = new();
        delegate*<void*, void*, long, long, void*> alloc = lua_getallocf(state, out void* _);
        Assert.That(alloc, Is.Not.Null);
    }

    [Test]
    public void Function_lua_setallocf()
    {
        using LuaState state = new();
        delegate*<void*, void*, long, long, void*> original = lua_getallocf(state, out void* originalUd);
        AllocStats stats;
        delegate*<void*, void*, long, long, void*> f = &CountingAlloc;
        lua_setallocf(state, f, &stats);
        Assert.That(lua_getallocf(state, out void* newUd), Is.EqualTo(f));
        Assert.That(newUd, Is.EqualTo(&stats));
        lua_setallocf(state, original, originalUd);
    }

    [Test]
    public void Function_lua_toclose()
    {
        using LuaState state = new();
        Assert.That(ProtectedCall(state, &TocloseSuccess, 0), Is.EqualTo(LUA_OK));
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &TocloseFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_closeslot()
    {
        using LuaState state = new();

        {
            Assert.That(ProtectedCall(state, &CloseSlotSuccess, 1), Is.EqualTo(LUA_OK));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(1));
        }
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &CloseSlotFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_lua_getstack()
    {
        using LuaState state = new();

        {
            Assert.That(ProtectedCall(state, &GetStackProbe, 2), Is.EqualTo(LUA_OK));
            Assert.That(lua_toboolean(state, -2));
            Assert.That(lua_toboolean(state, -1));
        }
    }

    [Test]
    public void Function_lua_getinfo()
    {
        using LuaState state = new();
        lua_Debug ar = new();
        lua_pushcfunction(state, &ReturnOne);

        Assert.That(lua_getinfo(state, ">Snu", ref ar), Is.True);
        Assert.That(ar.what, Is.EqualTo("C"));
        Assert.That(ar.nparams, Is.EqualTo(0));
    }

    [Test]
    public void Function_lua_getlocal()
    {
        using LuaState state = new();
        lua_pushcfunction(state, &GetLocalProbe);
        lua_pushinteger(state, 123);

        {
            Assert.That(lua_pcall(state, 1, 2, 0), Is.EqualTo(LUA_OK));
            Assert.That(lua_toboolean(state, -2));
            Assert.That(lua_toboolean(state, -1));
        }
    }

    [Test]
    public void Function_lua_setlocal()
    {
        using LuaState state = new();
        lua_pushcfunction(state, &SetLocalProbe);
        lua_pushinteger(state, 123);

        {
            Assert.That(lua_pcall(state, 1, 2, 0), Is.EqualTo(LUA_OK));
            Assert.That(lua_toboolean(state, -2));
            Assert.That(lua_toboolean(state, -1));
        }
    }

    [Test]
    public void Function_lua_getupvalue()
    {
        using LuaState state = new();
        lua_pushinteger(state, 321);
        lua_pushcclosure(state, &ReturnIntegerUpvalue, 1);
        string? name = lua_getupvalue(state, -1, 1);
        Assert.That(name, Is.Not.Null);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(321));
        lua_pop(state, 1);
        Assert.That(lua_getupvalue(state, -1, 2), Is.Null);
    }

    [Test]
    public void Function_lua_setupvalue()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushcclosure(state, &ReturnIntegerUpvalue, 1);
        lua_pushinteger(state, 654);
        string? name = lua_setupvalue(state, -2, 1);
        Assert.That(name, Is.Not.Null);
        lua_getupvalue(state, -1, 1);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(654));
        lua_pop(state, 1);
        lua_pushinteger(state, 1);
        Assert.That(lua_setupvalue(state, -2, 2), Is.Null);
        lua_pop(state, 1);
    }

    [Test]
    public void Function_lua_upvalueid()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushcclosure(state, &ReturnIntegerUpvalue, 1);
        Assert.That(lua_upvalueid(state, -1, 1), Is.Not.Null);
        Assert.That(lua_upvalueid(state, -1, 2), Is.Null);
    }

    [Test]
    public void Function_lua_upvaluejoin()
    {
        using LuaState state = new();
        lua_gc(state, LUA_GCSTOP);
        Assert.That(LoadChunk(state, "local x = 1; return function() return x end"), Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(LoadChunk(state, "local x = 2; return function() return x end"), Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        void* secondID = lua_upvalueid(state, -1, 1);
        Assert.That(secondID, Is.Not.Null);
        lua_upvaluejoin(state, -2, 1, -1, 1);
        Assert.That(lua_upvalueid(state, -2, 1), Is.EqualTo(secondID));
    }

    [Test]
    public void Function_lua_sethook()
    {
        using LuaState state = new();
        delegate*<lua_State*, ref lua_Debug, void> hookFunc = &HookFunction;
        lua_sethook(state, hookFunc, LUA_MASKCOUNT, 1);
        Assert.That(lua_gethook(state), Is.EqualTo(hookFunc));
        lua_sethook(state, null, 0, 0);
        Assert.That(lua_gethook(state), Is.Null);
    }

    [Test]
    public void Function_lua_gethook()
    {
        using LuaState state = new();
        Assert.That(lua_gethook(state), Is.Null);
        delegate*<lua_State*, ref lua_Debug, void> hookFunc = &HookFunction;
        lua_sethook(state, hookFunc, LUA_MASKLINE, 0);
        Assert.That(lua_gethook(state), Is.EqualTo(hookFunc));
    }

    [Test]
    public void Function_lua_gethookmask()
    {
        using LuaState state = new();
        Assert.That(lua_gethookmask(state), Is.EqualTo(0));
        lua_sethook(state, &HookFunction, LUA_MASKCALL | LUA_MASKRET, 0);
        Assert.That(lua_gethookmask(state), Is.EqualTo(LUA_MASKCALL | LUA_MASKRET));
    }

    [Test]
    public void Function_lua_gethookcount()
    {
        using LuaState state = new();
        Assert.That(lua_gethookcount(state), Is.EqualTo(0));
        lua_sethook(state, &HookFunction, LUA_MASKCOUNT, 5);
        Assert.That(lua_gethookcount(state), Is.EqualTo(5));
    }

    [Test]
    public void Macro_lua_call()
    {
        using LuaState state = new();

        {
            Assert.That(ProtectedCall(state, &CallMacroSuccess, 1), Is.EqualTo(LUA_OK));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(1));
        }
        lua_settop(state, 0);
        Assert.That(ProtectedCall(state, &CallMacroFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Macro_lua_pcall()
    {
        using LuaState state = new();
        lua_pushcfunction(state, &ReturnOne);

        {
            Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(1));
        }
        lua_settop(state, 0);
        lua_pushcfunction(state, &ErrorFunction);
        Assert.That(lua_pcall(state, 0, 0, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Macro_lua_yield()
    {
        using LuaState state = new();
        lua_State* co = lua_newthread(state);
        lua_pushcfunction(co, &YieldFunction);
        int nres = 0;

        {
            Assert.That(lua_resume(co, state, 0, &nres), Is.EqualTo(LUA_YIELD));
            Assert.That(nres, Is.EqualTo(1));
            Assert.That(lua_tointeger(co, -1), Is.EqualTo(9));
        }
    }

    [Test]
    public void Macro_lua_getextraspace()
    {
        using LuaState state = new();
        int marker = 0;
        *(void**)lua_getextraspace(state.get()) = &marker;
        lua_State* co = lua_newthread(state);
        Assert.That(*(void**)lua_getextraspace(co), Is.EqualTo(&marker));
    }

    [Test]
    public void Macro_lua_tonumber()
    {
        using LuaState state = new();
        lua_pushinteger(state, 13);
        lua_pushliteral(state, "bad");

        {
            Assert.That(lua_tonumber(state, 1), Is.EqualTo(13));
            Assert.That(lua_tonumber(state, 2), Is.EqualTo(0));
        }
    }

    [Test]
    public void Macro_lua_tointeger()
    {
        using LuaState state = new();
        lua_pushinteger(state, 14);
        lua_pushnumber(state, 1.5);

        {
            Assert.That(lua_tointeger(state, 1), Is.EqualTo(14));
            Assert.That(lua_tointeger(state, 2), Is.EqualTo(0));
        }
    }

    [Test]
    public void Macro_lua_pop()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 2);
        lua_pop(state, 1);
        Assert.That(lua_gettop(state), Is.EqualTo(1));
    }

    [Test]
    public void Macro_lua_newtable()
    {
        using LuaState state = new();
        lua_newtable(state);
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Macro_lua_register()
    {
        using LuaState state = new();
        lua_register(state, "registered", &ReturnOne);
        lua_getglobal(state, "registered");
        Assert.That(lua_iscfunction(state, -1), Is.True);
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(1));
    }

    [Test]
    public void Macro_lua_pushcfunction()
    {
        using LuaState state = new();
        lua_pushcfunction(state, &ReturnOne);
        Assert.That(lua_iscfunction(state, -1));
    }

    [Test]
    public void Macro_lua_isfunction()
    {
        using LuaState state = new();
        lua_pushcfunction(state, &ReturnOne);
        lua_pushnil(state);

        Assert.That(lua_isfunction(state, 1));
        Assert.That(lua_isfunction(state, 2), Is.False);
    }

    [Test]
    public void Macro_lua_istable()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushnil(state);

        {
            Assert.That(lua_istable(state, 1));
            Assert.That(lua_istable(state, 2), Is.False);
        }
    }

    [Test]
    public void Macro_lua_islightuserdata()
    {
        using LuaState state = new();
        int value = 0;
        lua_pushlightuserdata(state, &value);
        lua_newuserdatauv(state, 1, 0);

        {
            Assert.That(lua_islightuserdata(state, 1));
            Assert.That(lua_islightuserdata(state, 2), Is.False);
        }
    }

    [Test]
    public void Macro_lua_isnil()
    {
        using LuaState state = new();
        lua_pushnil(state);
        lua_pushinteger(state, 1);

        {
            Assert.That(lua_isnil(state, 1));
            Assert.That(lua_isnil(state, 2), Is.False);
        }
    }

    [Test]
    public void Macro_lua_isboolean()
    {
        using LuaState state = new();
        lua_pushboolean(state, true);
        lua_pushnil(state);

        Assert.That(lua_isboolean(state, 1));
        Assert.That(lua_isboolean(state, 2), Is.False);
    }

    [Test]
    public void Macro_lua_isthread()
    {
        using LuaState state = new();
        lua_newthread(state);
        lua_pushnil(state);

        Assert.That(lua_isthread(state, 1));
        Assert.That(lua_isthread(state, 2), Is.False);
    }

    [Test]
    public void Macro_lua_isnone()
    {
        using LuaState state = new();
        Assert.That(lua_isnone(state, 1));
        lua_pushnil(state);
        Assert.That(lua_isnone(state, 1), Is.False);
    }

    [Test]
    public void Macro_lua_isnoneornil()
    {
        using LuaState state = new();
        Assert.That(lua_isnoneornil(state, 1));
        lua_pushnil(state);
        Assert.That(lua_isnoneornil(state, 1));
        lua_pushinteger(state, 1);
        Assert.That(lua_isnoneornil(state, 2), Is.False);
    }

    [Test]
    public void Macro_lua_pushliteral()
    {
        using LuaState state = new();
        lua_pushliteral(state, "literal");
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("literal"));
    }

    [Test]
    public void Macro_lua_pushglobaltable()
    {
        using LuaState state = new();
        lua_pushglobaltable(state);
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Macro_lua_tostring()
    {
        using LuaState state = new();
        lua_pushinteger(state, 123);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("123"));
    }

    [Test]
    public void Macro_lua_insert()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 2);
        lua_insert(state, 1);

        {
            Assert.That(lua_tointeger(state, 1), Is.EqualTo(2));
            Assert.That(lua_tointeger(state, 2), Is.EqualTo(1));
        }
    }

    [Test]
    public void Macro_lua_remove()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 2);
        lua_remove(state, 1);

        {
            Assert.That(lua_gettop(state), Is.EqualTo(1));
            Assert.That(lua_tointeger(state, 1), Is.EqualTo(2));
        }
    }

    [Test]
    public void Macro_lua_replace()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 2);
        lua_replace(state, 1);

        Assert.That(lua_gettop(state), Is.EqualTo(1));
        Assert.That(lua_tointeger(state, 1), Is.EqualTo(2));
    }

    [Test]
    public void Macro_lua_newuserdata()
    {
        using LuaState state = new();
        void* userdata = lua_newuserdata(state, 4);
        Assert.That(userdata, Is.Not.Null);
        lua_pushinteger(state, 1);
        Assert.That(lua_setuservalue(state, -2));
    }

    [Test]
    public void Macro_lua_getuservalue()
    {
        using LuaState state = new();
        lua_newuserdatauv(state, 1, 1);
        lua_pushinteger(state, 2);
        Assert.That(lua_setiuservalue(state, -2, 1), Is.True);

        {
            Assert.That(lua_getuservalue(state, -1), Is.EqualTo(LUA_TNUMBER));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(2));
        }
    }

    [Test]
    public void Macro_lua_setuservalue()
    {
        using LuaState state = new();
        lua_newuserdatauv(state, 1, 1);
        lua_pushinteger(state, 3);
        Assert.That(lua_setuservalue(state, -2));
        lua_getiuservalue(state, -1, 1);
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(3));
    }

    [Test]
    public void Macro_lua_resetthread()
    {
        using LuaState state = new();
        lua_State* co = lua_newthread(state);
        lua_pushcfunction(co, &YieldFunction);
        int nres = 0;

        {
            Assert.That(lua_resume(co, state, 0, &nres), Is.EqualTo(LUA_YIELD));
            Assert.That(lua_resetthread(co), Is.EqualTo(LUA_OK));
            Assert.That(lua_status(co), Is.EqualTo(LUA_OK));
        }
    }

    private static int g_require_count;

    private static int CountingRequire(lua_State* L)
    {
        ++g_require_count;
        lua_newtable(L);
        lua_pushinteger(L, g_require_count);
        lua_setfield(L, -2, "count");
        return 1;
    }

    [Test]
    public void Function_luaL_checkversion_()
    {
        using LuaState state = new();
        Assert.That(ProtectedCall(state, &AuxCheckVersionOk, 0), Is.EqualTo(LUA_OK));
        Assert.That(ProtectedCall(state, &AuxCheckVersionBad, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_getmetafield()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_newtable(state);
        lua_pushliteral(state, "named");
        lua_setfield(state, -2, "__name");
        lua_setmetatable(state, -2);
        Assert.That(luaL_getmetafield(state, 1, "__name"), Is.EqualTo(LUA_TSTRING));
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("named"));
        lua_settop(state, 0);
        lua_newtable(state);
        Assert.That(luaL_getmetafield(state, 1, "__name"), Is.EqualTo(LUA_TNIL));
    }

    [Test]
    public void Function_luaL_callmeta()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_newtable(state);
        lua_pushcfunction(state, &MetaToString);
        lua_setfield(state, -2, "__tostring");
        lua_setmetatable(state, -2);

        Assert.That(luaL_callmeta(state, 1, "__tostring"));
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("metavalue"));
        lua_settop(state, 0);
        lua_newtable(state);
        Assert.That(luaL_callmeta(state, 1, "__tostring"), Is.False);
    }

    [Test]
    public void Function_luaL_tolstring()
    {
        using LuaState state = new();
        lua_pushinteger(state, 42);
        byte* value = luaL_tolstring(state, 1, out _);
        Assert.That(value, Is.Not.Null);
        Assert.That(new string((sbyte*)value), Is.EqualTo("42"));
        lua_newtable(state);
        value = luaL_tolstring(state, -1, out _);
        Assert.That(value, Is.Not.Null);
        Assert.That(new string((sbyte*)value), Contains.Substring("table"));
    }

    [Test]
    public void Function_luaL_argerror()
    {
        using LuaState state = new();
        Assert.That(ProtectedCall(state, &AuxArgError, 0), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("bad argument"));
    }

    [Test]
    public void Function_luaL_typeerror()
    {
        using LuaState state = new();
        Assert.That(ProtectedCall(state, &AuxTypeError, 0), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("table expected"));
    }

    [Test]
    public void Function_luaL_checklstring()
    {
        using LuaState state = new();
        lua_pushliteral(state, "abc");
        string s = luaL_checknetstring(state, 1);
        Assert.That(s, Is.EqualTo("abc"));
        Assert.That(s, Has.Length.EqualTo(3));
        Assert.That(ProtectedCall(state, &AuxCheckLStringFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_optlstring()
    {
        using LuaState state = new();
        string s = luaL_optnetstring(state, 1, "default");
        Assert.That(s, Is.EqualTo("default"));
        Assert.That(s, Has.Length.EqualTo(7));
        
        lua_pushliteral(state, "given");
        s = luaL_optnetstring(state, 1, "default");
        Assert.That(s, Is.EqualTo("given"));
        Assert.That(s, Has.Length.EqualTo(5));
        
        Assert.That(ProtectedCall(state, &AuxOptLStringFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_checknumber()
    {
        using LuaState state = new();
        lua_pushnumber(state, 3.5);
        Assert.That(luaL_checknumber(state, 1), Is.EqualTo(3.5));
        Assert.That(ProtectedCall(state, &AuxCheckNumberFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_optnumber()
    {
        using LuaState state = new();
        Assert.That(luaL_optnumber(state, 1, 4.5), Is.EqualTo(4.5));
        lua_pushnumber(state, 5.5);
        Assert.That(luaL_optnumber(state, 1, 4.5), Is.EqualTo(5.5));
        Assert.That(ProtectedCall(state, &AuxOptNumberFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_checkinteger()
    {
        using LuaState state = new();
        lua_pushinteger(state, 6);
        Assert.That(luaL_checkinteger(state, 1), Is.EqualTo(6));
        Assert.That(ProtectedCall(state, &AuxCheckIntegerFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_optinteger()
    {
        using LuaState state = new();
        Assert.That(luaL_optinteger(state, 1, 7), Is.EqualTo(7));
        lua_pushinteger(state, 8);
        Assert.That(luaL_optinteger(state, 1, 7), Is.EqualTo(8));
        Assert.That(ProtectedCall(state, &AuxOptIntegerFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_checkstack()
    {
        using LuaState state = new();
        luaL_checkstack(state, LUA_MINSTACK, "stack");
        Assert.That(ProtectedCall(state, &AuxCheckStackFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_checktype()
    {
        using LuaState state = new();
        lua_newtable(state);
        luaL_checktype(state, 1, LUA_TTABLE);
        Assert.That(ProtectedCall(state, &AuxCheckTypeFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_checkany()
    {
        using LuaState state = new();
        lua_pushnil(state);
        luaL_checkany(state, 1);
        Assert.That(ProtectedCall(state, &AuxCheckAnyFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_newmetatable()
    {
        using LuaState state = new();
        Assert.That(luaL_newmetatable(state, "unit.mt"));
        Assert.That(lua_istable(state, -1));
        Assert.That(luaL_newmetatable(state, "unit.mt"), Is.False);
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaL_setmetatable()
    {
        using LuaState state = new();
        Assert.That(luaL_newmetatable(state, "unit.setmt"), Is.True);
        lua_pop(state, 1);
        lua_newuserdatauv(state, 1, 0);
        luaL_setmetatable(state, "unit.setmt");
        Assert.That(lua_getmetatable(state, -1), Is.True);
        luaL_getmetatable(state, "unit.setmt");
        Assert.That(lua_rawequal(state, -1, -2), Is.True);
    }

    [Test]
    public void Function_luaL_testudata()
    {
        using LuaState state = new();
        Assert.That(luaL_newmetatable(state, "unit.ud"), Is.True);
        lua_pop(state, 1);
        void* userdata = lua_newuserdatauv(state, 1, 0);
        luaL_setmetatable(state, "unit.ud");
        Assert.That(luaL_testudata(state, 1, "unit.ud"), Is.EqualTo(userdata));
        Assert.That(luaL_testudata(state, 1, "unit.other"), Is.Null);
    }

    [Test]
    public void Function_luaL_checkudata()
    {
        using LuaState state = new();
        Assert.That(luaL_newmetatable(state, "unit.checkud"), Is.True);
        lua_pop(state, 1);
        void* userdata = lua_newuserdatauv(state, 1, 0);
        luaL_setmetatable(state, "unit.checkud");
        Assert.That(luaL_checkudata(state, 1, "unit.checkud"), Is.EqualTo(userdata));
        Assert.That(ProtectedCall(state, &AuxCheckUDataFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_where()
    {
        using LuaState state = new();
        luaL_where(state, 0);
        Assert.That(lua_isstring(state, -1));
    }

    [Test]
    public void Function_luaL_error()
    {
        using LuaState state = new();
        Assert.That(ProtectedCall(state, &AuxError, 0), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("formatted failure"));
    }

    [Test]
    public void Function_luaL_checkoption()
    {
        using LuaState state = new();
        string[] options =
        [
            "red", "green",
        ];
        Assert.That(luaL_checkoption(state, 1, "green", options), Is.EqualTo(1));
        lua_pushliteral(state, "red");
        Assert.That(luaL_checkoption(state, 1, null, options), Is.EqualTo(0));
        Assert.That(ProtectedCall(state, &AuxCheckOptionFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_fileresult()
    {
        using LuaState state = new();
        
        Assert.That(luaL_fileresult(state, true, null, null), Is.EqualTo(1));
        Assert.That(lua_toboolean(state, -1));
        lua_pop(state, 1);

        Assert.That(
            luaL_fileresult(state, false, "missing.file", new FileNotFoundException(null, "missing.file")),
            Is.EqualTo(3));
        Assert.That(lua_isnoneornil(state, -3));
        Assert.That(lua_isstring(state, -2));
    }

    [Test]
    public void Function_luaL_execresult()
    {
        using LuaState state = new();
        Assert.That(luaL_execresult(state, 0), Is.EqualTo(3));
        Assert.That(lua_toboolean(state, -3));
        Assert.That(lua_tonetstring(state, -2), Is.EqualTo("exit"));
        lua_settop(state, 0);
        
        Assert.That(luaL_execresult(state, 1), Is.EqualTo(3));
        Assert.That(lua_isnoneornil(state, -3));
    }

    [Test]
    public void Function_luaL_alloc()
    {
        void* ptr = luaL_alloc(null, null, 0, 16);
        Assert.That(ptr, Is.Not.Null);
        ptr = luaL_alloc(null, ptr, 16, 32);
        Assert.That(ptr, Is.Not.Null);
        Assert.That(luaL_alloc(null, ptr, 32, 0), Is.Null);
    }

    [Test]
    public void Function_luaL_ref()
    {
        using LuaState state = new();
        lua_pushliteral(state, "referenced");
        int @ref = luaL_ref(state, LUA_REGISTRYINDEX);
        Assert.That(@ref, Is.GreaterThan(0));
        lua_rawgeti(state, LUA_REGISTRYINDEX, @ref);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("referenced"));
        lua_pop(state, 1);
        lua_pushnil(state);
        Assert.That(luaL_ref(state, LUA_REGISTRYINDEX), Is.EqualTo(LUA_REFNIL));
    }

    [Test]
    public void Function_luaL_unref()
    {
        using LuaState state = new();
        lua_pushliteral(state, "referenced");
        int @ref = luaL_ref(state, LUA_REGISTRYINDEX);
        luaL_unref(state, LUA_REGISTRYINDEX, @ref);
        lua_pushliteral(state, "new reference");
        int reused_ref = luaL_ref(state, LUA_REGISTRYINDEX);
        Assert.That(reused_ref, Is.EqualTo(@ref));
        lua_rawgeti(state, LUA_REGISTRYINDEX, reused_ref);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("new reference"));
        luaL_unref(state, LUA_REGISTRYINDEX, LUA_NOREF);
        luaL_unref(state, LUA_REGISTRYINDEX, LUA_REFNIL);
    }

    [Test]
    public void Function_luaL_loadfilex()
    {
        using LuaState state = new();
        string path = TempLuaFile("lua_public_api_loadfilex.lua", "return 81");
        Assert.That(luaL_loadfilex(state, path, "t"), Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(81));
        lua_settop(state, 0);
        Assert.That(luaL_loadfilex(state, "definitely_missing_file.lua", "t"), Is.EqualTo(LUA_ERRFILE));
        File.Delete(path);
    }

    [Test]
    public void Function_luaL_loadbufferx()
    {
        using LuaState state = new();
        Assert.That(luaL_loadbufferx(state, "return 82"u8, "buffer", "t"), Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(82));
        lua_settop(state, 0);
        Assert.That(luaL_loadbufferx(state, "return +"u8, "bad", "t"), Is.EqualTo(LUA_ERRSYNTAX));
    }

    [Test]
    public void Function_luaL_loadstring()
    {
        using LuaState state = new();
        Assert.That(luaL_loadstring(state, "return 83"), Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(83));
        lua_settop(state, 0);
        Assert.That(luaL_loadstring(state, "return +"), Is.EqualTo(LUA_ERRSYNTAX));
    }

    [Test]
    public void Function_luaL_newstate()
    {
        lua_State* L = luaL_newstate();
        Assert.That(L, Is.Not.Null);
        lua_close(L);
    }

    [Test]
    public void Function_luaL_makeseed()
    {
        Assert.That(luaL_makeseed(null), Is.Not.Zero);
    }

    [Test]
    public void Function_luaL_len()
    {
        using LuaState state = new();
        lua_pushliteral(state, "abcd");
        Assert.That(luaL_len(state, 1), Is.EqualTo(4));
        Assert.That(ProtectedCall(state, &AuxLenFailure, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaL_addgsub()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addgsub(&buffer, "a-b-a", "-", "+");
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("a+b+a"));
    }

    [Test]
    public void Function_luaL_gsub()
    {
        using LuaState state = new();
        string result = luaL_gsub(state, "one two one", "one", "1");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.EqualTo("1 two 1"));
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("1 two 1"));
    }

    [Test]
    public void Function_luaL_setfuncs()
    {
        using LuaState state = new();
        lua_newtable(state);
        luaL_setfuncs(state, kOneFunctionLib, 0);
        lua_getfield(state, -1, "one");
        Assert.That(lua_iscfunction(state, -1), Is.True);
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(1));
    }

    [Test]
    public void Function_luaL_getsubtable()
    {
        using LuaState state = new();
        lua_newtable(state);
        Assert.That(luaL_getsubtable(state, 1, "child"), Is.False);
        Assert.That(lua_istable(state, -1));
        lua_pop(state, 1);
        Assert.That(luaL_getsubtable(state, 1, "child"));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaL_traceback()
    {
        using LuaState state = new();
        luaL_traceback(state, state, "trace message", 0);
        Assert.That(lua_isstring(state, -1));
        Assert.That(StackString(state, -1), Contains.Substring("trace message"));
    }

    [Test]
    public void Function_luaL_requiref()
    {
        using LuaState state = new();
        g_require_count = 0;
        luaL_requiref(state, "unit.require", &CountingRequire, true);
        Assert.That(g_require_count, Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
        lua_pop(state, 1);
        luaL_requiref(state, "unit.require", &CountingRequire, true);
        Assert.That(g_require_count, Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
        lua_getglobal(state, "unit.require");
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaL_buffinit()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addstring(&buffer, "buffer");
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("buffer"));
    }

    [Test]
    public void Function_luaL_prepbuffsize()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        byte* space = luaL_prepbuffsize(&buffer, 3);
        Assert.That(space, Is.Not.Null);
        memcpy(space, "abc"u8.ToPointer(), 3);
        luaL_addsize(&buffer, 3);
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("abc"));
    }

    [Test]
    public void Function_luaL_addlstring()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addlstring(&buffer, "a\0b");
        luaL_pushresult(&buffer);
        string? result = lua_tonetstring(state, -1);
        Assert.That(result, Is.EqualTo("a\0b"));
    }

    [Test]
    public void Function_luaL_addstring()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addstring(&buffer, "abc");
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("abc"));
    }

    [Test]
    public void Function_luaL_addvalue()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addstring(&buffer, "a");
        lua_pushliteral(state, "b");
        luaL_addvalue(&buffer);
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("ab"));
    }

    [Test]
    public void Function_luaL_pushresult()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addstring(&buffer, "done");
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("done"));
    }

    [Test]
    public void Function_luaL_pushresultsize()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        byte* space = luaL_buffinitsize(state, &buffer, 4);
        memcpy(space, "data"u8.ToPointer(), 4);
        luaL_pushresultsize(&buffer, 4);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("data"));
    }

    [Test]
    public void Function_luaL_buffinitsize()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        byte* space = luaL_buffinitsize(state, &buffer, 3);
        Assert.That(space, Is.Not.Null);
        memcpy(space, "xyz"u8.ToPointer(), 3);
        luaL_pushresultsize(&buffer, 3);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("xyz"));
    }

    [Test]
    public void Macro_luaL_checkversion()
    {
        using LuaState state = new();
        Assert.That(ProtectedCall(state, &AuxCheckVersionOk, 0), Is.EqualTo(LUA_OK));
    }

    [Test]
    public void Macro_luaL_loadfile()
    {
        using LuaState state = new();
        string path = TempLuaFile("lua_public_api_loadfile.lua", "return 84");
        Assert.That(luaL_loadfile(state, path), Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(84));
        lua_settop(state, 0);
        Assert.That(luaL_loadfile(state, "definitely_missing_file.lua"), Is.EqualTo(LUA_ERRFILE));
        File.Delete(path);
    }

    [Test]
    public void Macro_luaL_newlibtable()
    {
        using LuaState state = new();
        luaL_newlibtable(state, kOneFunctionLib);
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Macro_luaL_newlib()
    {
        using LuaState state = new();
        luaL_newlib(state, kOneFunctionLib);
        lua_getfield(state, -1, "one");
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(1));
    }

    [Test]
    public void Macro_luaL_argcheck()
    {
        using LuaState state = new();
        Assert.That(ProtectedCall(state, &AuxArgCheckOk, 0), Is.EqualTo(LUA_OK));
        Assert.That(ProtectedCall(state, &AuxArgCheckFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Macro_luaL_argexpected()
    {
        using LuaState state = new();
        Assert.That(ProtectedCall(state, &AuxArgExpectedOk, 0), Is.EqualTo(LUA_OK));
        Assert.That(ProtectedCall(state, &AuxArgExpectedFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Macro_luaL_checkstring()
    {
        using LuaState state = new();
        lua_pushliteral(state, "abc");
        Assert.That(luaL_checknetstring(state, 1), Is.EqualTo("abc"));
        Assert.That(ProtectedCall(state, &AuxCheckLStringFail, 0), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Macro_luaL_optstring()
    {
        using LuaState state = new();
        Assert.That(luaL_optnetstring(state, 1, "default"), Is.EqualTo("default"));
        lua_pushliteral(state, "given");
        Assert.That(luaL_optnetstring(state, 1, "default"), Is.EqualTo("given"));
    }

    [Test]
    public void Macro_luaL_typename()
    {
        using LuaState state = new();
        lua_newtable(state);
        Assert.That(luaL_typename(state, -1), Is.EqualTo("table"));
    }

    [Test]
    public void Macro_luaL_dofile()
    {
        using LuaState state = new();
        string path = TempLuaFile("lua_public_api_dofile.lua", "return 85");
        Assert.That(luaL_dofile(state, path), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(85));
        lua_settop(state, 0);
        Assert.That(luaL_dofile(state, "definitely_missing_file.lua"), Is.Not.EqualTo(LUA_OK));
        File.Delete(path);
    }

    [Test]
    public void Macro_luaL_dostring()
    {
        using LuaState state = new();

        {
            Assert.That(luaL_dostring(state, "return 86"), Is.EqualTo(LUA_OK));
            Assert.That(lua_tointeger(state, -1), Is.EqualTo(86));
        }
        lua_settop(state, 0);
        Assert.That(luaL_dostring(state, "error('failure')"), Is.Not.EqualTo(LUA_OK));
    }

    [Test]
    public void Macro_luaL_getmetatable()
    {
        using LuaState state = new();
        Assert.That(luaL_newmetatable(state, "unit.getmt"), Is.True);
        lua_pop(state, 1);
        Assert.That(luaL_getmetatable(state, "unit.getmt"), Is.EqualTo(LUA_TTABLE));
        lua_pop(state, 1);
        Assert.That(luaL_getmetatable(state, "unit.missingmt"), Is.EqualTo(LUA_TNIL));
    }

    [Test]
    public void Macro_luaL_loadbuffer()
    {
        using LuaState state = new();
        Assert.That(luaL_loadbuffer(state, "return 87"u8, "buffer"), Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        Assert.That(lua_tointeger(state, -1), Is.EqualTo(87));
    }

    [Test]
    public void Macro_luaL_pushfail()
    {
        using LuaState state = new();
        luaL_pushfail(state);
#if LUA_FAILISFALSE
        Assert.That(lua_isboolean(state, -1));
        Assert.That(lua_toboolean(state, -1), Is.False);
#else
        Assert.That(lua_isnil(state, -1));
#endif
    }

    [Test]
    public void Macro_luaL_bufflen()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addstring(&buffer, "abc");
        Assert.That(luaL_bufflen(&buffer), Is.EqualTo(3));
        luaL_pushresult(&buffer);
    }

    [Test]
    public void Macro_luaL_buffaddr()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addstring(&buffer, "abc");
        Assert.That(luaL_buffaddr(&buffer), Is.Not.Null);
        Assert.That(luaL_buffaddr(&buffer)[0], Is.EqualTo((byte)'a'));
        luaL_pushresult(&buffer);
    }

    [Test]
    public void Macro_luaL_addchar()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addchar(&buffer, 'x');
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("x"));
    }

    [Test]
    public void Macro_luaL_addsize()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        byte* space = luaL_prepbuffsize(&buffer, 2);
        memcpy(space, "xy"u8.ToPointer(), 2);
        luaL_addsize(&buffer, 2);
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("xy"));
    }

    [Test]
    public void Macro_luaL_buffsub()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        luaL_addstring(&buffer, "abcd");
        luaL_buffsub(&buffer, 2);
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("ab"));
    }

    [Test]
    public void Macro_luaL_prepbuffer()
    {
        using LuaState state = new();
        luaL_Buffer buffer;
        luaL_buffinit(state, &buffer);
        byte* space = luaL_prepbuffer(&buffer);
        Assert.That(space, Is.Not.Null);
        memcpy(space, "pq"u8.ToPointer(), 2);
        luaL_addsize(&buffer, 2);
        luaL_pushresult(&buffer);
        Assert.That(lua_tonetstring(state, -1), Is.EqualTo("pq"));
    }

    [Test]
    public void Macro_lua_strlen()
    {
        using LuaState state = new();
        lua_pushliteral(state, "length");
        Assert.That(lua_strlen(state, -1), Is.EqualTo(6));
    }

    [Test]
    public void Macro_lua_objlen()
    {
        using LuaState state = new();
        lua_newtable(state);
        lua_pushinteger(state, 1);
        lua_rawseti(state, -2, 1);
        Assert.That(lua_objlen(state, -1), Is.EqualTo(1));
    }

    [Test]
    public void Macro_lua_equal()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 2);
        Assert.That(lua_equal(state, 1, 2), Is.True);
        Assert.That(lua_equal(state, 1, 3), Is.False);
    }

    [Test]
    public void Macro_lua_lessthan()
    {
        using LuaState state = new();
        lua_pushinteger(state, 1);
        lua_pushinteger(state, 2);
        Assert.That(lua_lessthan(state, 1, 2), Is.True);
        Assert.That(lua_lessthan(state, 2, 1), Is.False);
    }

    [Test]
    public void Function_luaopen_base()
    {
        using LuaState state = new();

        Assert.That(luaopen_base(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_package()
    {
        using LuaState state = new();

        Assert.That(luaopen_package(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_coroutine()
    {
        using LuaState state = new();

        Assert.That(luaopen_coroutine(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_debug()
    {
        using LuaState state = new();

        Assert.That(luaopen_debug(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_io()
    {
        using LuaState state = new();

        Assert.That(luaopen_io(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_math()
    {
        using LuaState state = new();

        Assert.That(luaopen_math(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_os()
    {
        using LuaState state = new();

        Assert.That(luaopen_os(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_string()
    {
        using LuaState state = new();

        Assert.That(luaopen_string(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_table()
    {
        using LuaState state = new();

        Assert.That(luaopen_table(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaopen_utf8()
    {
        using LuaState state = new();

        Assert.That(luaopen_utf8(state), Is.EqualTo(1));
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void Function_luaL_openselectedlibs()
    {
        using LuaState state = new();
        luaL_openselectedlibs(state, LUA_MATHLIBK, LUA_STRLIBK);
        lua_getglobal(state, LUA_MATHLIBNAME);
        Assert.That(lua_istable(state, -1));
        lua_pop(state, 1);
        lua_getglobal(state, LUA_STRLIBNAME);
        Assert.That(lua_isnil(state, -1));
        lua_pop(state, 1);
        luaL_getsubtable(state, LUA_REGISTRYINDEX, LUA_PRELOAD_TABLE);
        lua_getfield(state, -1, LUA_STRLIBNAME);
        Assert.That(lua_isfunction(state, -1));
    }

    [Test]
    public void Macro_luaL_openlibs()
    {
        using LuaState state = new();
        luaL_openlibs(state);
        lua_getglobal(state, LUA_MATHLIBNAME);
        Assert.That(lua_istable(state, -1));
        lua_pop(state, 1);
        lua_getglobal(state, LUA_STRLIBNAME);
        Assert.That(lua_istable(state, -1));
        lua_pop(state, 1);
        lua_getglobal(state, LUA_GNAME);
        Assert.That(lua_istable(state, -1));
    }

    [Test]
    public void MultipleArguments()
    {
        using LuaState state = new();
        luaL_openlibs(state);
        
        Assert.That(
            LoadChunk(
                state,
                """
                local function test(a, b)
                    return b
                end

                assert(test"1" == nil)
                """),
            Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
        
        Assert.That(
            LoadChunk(
                state,
                """
                local function test(a, b)
                    return a
                end

                assert(test"1" == "1")
                """),
            Is.EqualTo(LUA_OK));
        Assert.That(lua_pcall(state, 0, 1, 0), Is.EqualTo(LUA_OK));
    }
}
