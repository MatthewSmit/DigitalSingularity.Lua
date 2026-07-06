namespace DigitalSingularity.Lua.Test;

using System.Runtime.InteropServices;
using System.Text;
using static DigitalSingularity.Lua.Lua;

#pragma warning disable NUnit2045

public unsafe class PrivateTests
{
    private static string LuaString(TString* ts)
    {
        return getnetstr(ts);
    }

    private static string StackString(lua_State* L, int idx)
    {
        byte* value = lua_tolstring(L, idx, out long _);
        return value == null ? "" : new string((sbyte*)value);
    }

    private static Table* NewAnchoredTable(lua_State* L)
    {
        Table* table = luaH_new(L);
        sethvalue2s(L, L->top.p, table);
        L->top.p++;
        return table;
    }

    private static TString* NewAnchoredString(lua_State* L, string value)
    {
        TString* str = luaS_new(L, value);
        setsvalue2s(L, L->top.p, str);
        L->top.p++;
        return str;
    }

    private static Udata* NewAnchoredUdata(lua_State* L, int size, ushort user_value_count)
    {
        Udata* userdata = luaS_newudata(L, size, user_value_count);
        setuvalue(L, s2v(L->top.p), userdata);
        L->top.p++;
        return userdata;
    }

    private static void TableSetString(lua_State* L, Table* table, TString* key, TValue* value)
    {
        TValue keyValue;
        setsvalue(L, &keyValue, key);
        luaH_set(L, table, &keyValue, value);
    }

    private static void MarkObjectWhite(global_State* global, GCObject* obj)
    {
        resetbits(ref obj->marked, (byte)(WHITEBITS | bitmask(BLACKBIT)));
        setbits(ref obj->marked, luaC_white(global));
    }

    private static void MarkObjectBlack(GCObject* obj)
    {
        resetbits(ref obj->marked, (byte)(WHITEBITS | bitmask(BLACKBIT)));
        l_setbit(ref obj->marked, BLACKBIT);
    }

    private static void MarkObjectGrey(GCObject* obj)
    {
        resetbits(ref obj->marked, (byte)(WHITEBITS | bitmask(BLACKBIT)));
    }

    private static bool ListContains(GCObject* list, GCObject* obj)
    {
        for (GCObject* current = list; current != null; current = current->next)
        {
            if (current == obj)
            {
                return true;
            }
        }

        return false;
    }

    private static void DestroyStateAfterLuaCFreeallobjects(lua_State* L)
    {
        global_State* global = G(L);
        luaM_freearray(L, global->strt.hash, global->strt.size);
        luaM_freearray(L, L->stack.p, stacksize(L) + EXTRA_STACK);
        global->frealloc(global->ud, global, sizeof(global_State), 0);
    }

    private sealed class ReaderState
    {
        public List<nint> chunks = [];
        public int index;
    }

    private static byte* ChunkReader(lua_State* L, void* ud, long* size)
    {
        ReaderState state = GCHandle<ReaderState>.FromIntPtr((IntPtr)ud).Target;
        if (state.index >= state.chunks.Count)
        {
            *size = 0;
            return null;
        }

        byte* chunk = (byte*)state.chunks[state.index++];
        *size = strlen(chunk);
        return chunk;
    }

    private static void ProtectedLuaMToobig(lua_State* L, void* ud)
    {
        luaM_toobig(L);
    }

    private struct LuaOArithArgs
    {
        public int op;
        public TValue left;
        public TValue right;
    }

    private static void ProtectedLuaOArith(lua_State* L, void* ud)
    {
        LuaOArithArgs* args = (LuaOArithArgs*)ud;
        luaO_arith(L, args->op, &args->left, &args->right, L->top.p);
    }

    private struct DivArgs
    {
        public long left;
        public long right;
        public long result;
    }

    private static void ProtectedLuaVIdiv(lua_State* L, void* ud)
    {
        DivArgs* args = (DivArgs*)ud;
        args->result = luaV_idiv(L, args->left, args->right);
    }

    private static void ProtectedLuaVMod(lua_State* L, void* ud)
    {
        DivArgs* args = (DivArgs*)ud;
        args->result = luaV_mod(L, args->left, args->right);
    }

    private static void ProtectedLuaVObjlen(lua_State* L, void* ud)
    {
        TValue* value = (TValue*)ud;
        luaV_objlen(L, L->top.p, value);
    }

    private static void ProtectedLuaVFinishGet(lua_State* L, void* ud)
    {
        TValue* target = (TValue*)ud;
        TValue key;
        setsvalue(L, &key, luaS_new(L, "field"));
        luaV_finishget(L, target, &key, L->top.p, LUA_VNOTABLE);
    }

    private static void ProtectedLuaVFinishSet(lua_State* L, void* ud)
    {
        TValue* target = (TValue*)ud;
        TValue key;
        TValue value;
        setsvalue(L, &key, luaS_new(L, "field"));
        setivalue(&value, 1);
        luaV_finishset(L, target, &key, &value, HNOTATABLE);
    }

    private static void ProtectedLuaFNewTbcUpval(lua_State* L, void* ud)
    {
        setivalue(s2v(L->top.p), 7);
        StackValue* level = L->top.p;
        L->top.p++;
        luaF_newtbcupval(L, level);
    }

    private static void ProtectedLuaGRunerror(lua_State* L, void* ud)
    {
        luaG_runerror(L, "internal %s", "failure");
    }

    private static void ProtectedLuaGTypeerror(lua_State* L, void* ud)
    {
        TValue value;
        setnilvalue(&value);
        luaG_typeerror(L, &value, "index");
    }

    private static void ProtectedLuaGCallerror(lua_State* L, void* ud)
    {
        TValue value;
        setivalue(&value, 1);
        luaG_callerror(L, &value);
    }

    private static void ProtectedLuaGForerror(lua_State* L, void* ud)
    {
        TValue value;
        setsvalue(L, &value, luaS_new(L, "abc"));
        luaG_forerror(L, &value, "limit");
    }

    private static void ProtectedLuaGConcaterror(lua_State* L, void* ud)
    {
        TValue left;
        TValue right;
        setnilvalue(&left);
        setivalue(&right, 1);
        luaG_concaterror(L, &left, &right);
    }

    private static void ProtectedLuaGOpinterror(lua_State* L, void* ud)
    {
        TValue left;
        TValue right;
        setnilvalue(&left);
        setivalue(&right, 1);
        luaG_opinterror(L, &left, &right, "perform arithmetic on");
    }

    private static void ProtectedLuaGTointError(lua_State* L, void* ud)
    {
        TValue left;
        TValue right;
        setfltvalue(&left, 1.5);
        setivalue(&right, 2);
        luaG_tointerror(L, &left, &right);
    }

    private static void ProtectedLuaGOrdererror(lua_State* L, void* ud)
    {
        TValue left;
        TValue right;
        setnilvalue(&left);
        setivalue(&right, 1);
        luaG_ordererror(L, &left, &right);
    }

    private static void ProtectedLuaGErrormsg(lua_State* L, void* ud)
    {
        setsvalue2s(L, L->top.p, luaS_new(L, "direct error"));
        L->top.p++;
        luaG_errormsg(L);
    }

    private static void ProtectedLuaECheckCStack(lua_State* L, void* ud)
    {
        L->nCcalls = (L->nCcalls & 0xffff0000u) | LUAI_MAXCCALLS;
        luaE_checkcstack(L);
    }

    private static void ProtectedLuaEIncCStack(lua_State* L, void* ud)
    {
        L->nCcalls = (L->nCcalls & 0xffff0000u) | (LUAI_MAXCCALLS - 1);
        luaE_incCstack(L);
    }

    private sealed class WarningCapture
    {
        public List<(string msg, bool tocont)> messages = [];
    }

    private static void WarningHandler(void* ud, string msg, bool tocont)
    {
        WarningCapture warningCapture = GCHandle<WarningCapture>.FromIntPtr((nint)ud).Target;
        warningCapture.messages.Add((msg, tocont));
    }

    private static int ReturnBoolean(lua_State* L)
    {
        lua_pushboolean(L, true);
        return 1;
    }

    [Test]
    public void Function_luaP_isOT()
    {
        uint tailcall = CREATE_ABCk(OpCode.OP_TAILCALL, 0, 1, 1, false);
        uint open_call = CREATE_ABCk(OpCode.OP_CALL, 0, 1, 0, false);
        uint fixed_call = CREATE_ABCk(OpCode.OP_CALL, 0, 1, 2, false);

        Assert.That(luaP_isOT(tailcall));
        Assert.That(luaP_isOT(open_call));
        Assert.That(luaP_isOT(fixed_call), Is.False);
    }

    [Test]
    public void Function_luaP_isIT()
    {
        uint open_call = CREATE_ABCk(OpCode.OP_CALL, 0, 0, 1, false);
        uint fixed_call = CREATE_ABCk(OpCode.OP_CALL, 0, 1, 1, false);
        uint open_setlist = CREATE_ABCk(OpCode.OP_SETLIST, 0, 0, 1, false);

        Assert.That(luaP_isIT(open_call));
        Assert.That(luaP_isIT(fixed_call), Is.False);
        Assert.That(luaP_isIT(open_setlist));
    }

    [Test]
    public void Function_luaO_utf8esc()
    {
        Span<byte> buffer = stackalloc byte[UTF8BUFFSZ];
        Span<byte> result = luaO_utf8esc(buffer, 0x24);
        Assert.That(result.Length, Is.EqualTo(1));
        Assert.That(Encoding.UTF8.GetString(result), Is.EqualTo("$"));
        
        result = luaO_utf8esc(buffer, 0x20AC);
        Assert.That(result.Length, Is.EqualTo(3));
        Assert.That(Encoding.UTF8.GetString(result), Is.EqualTo("€"));
    }

    [Test]
    public void Function_luaO_ceillog2()
    {
        Assert.That(luaO_ceillog2(1), Is.EqualTo(0));
        Assert.That(luaO_ceillog2(2), Is.EqualTo(1));
        Assert.That(luaO_ceillog2(3), Is.EqualTo(2));
        Assert.That(luaO_ceillog2(8), Is.EqualTo(3));
        Assert.That(luaO_ceillog2(9), Is.EqualTo(4));
    }

    [Test]
    public void Function_luaO_codeparam()
    {
        Assert.That(luaO_codeparam(0), Is.EqualTo(0));
        Assert.That(luaO_codeparam(100), Is.Not.Zero);
        Assert.That(luaO_codeparam(uint.MaxValue), Is.EqualTo(0xFF));
    }

    [Test]
    public void Function_luaO_applyparam()
    {
        Assert.That(luaO_applyparam(0, 1000), Is.EqualTo(0));
        byte encoded = luaO_codeparam(100);
        Assert.That(luaO_applyparam(encoded, 1000), Is.EqualTo(1000).Within(80));
    }

    [Test]
    public void Function_luaO_rawarith()
    {
        using LuaState state = new();
        TValue left;
        TValue right;
        TValue result;
        TValue invalid;
        setivalue(&left, 20);
        setivalue(&right, 22);
        setnilvalue(&invalid);

        Assert.That(luaO_rawarith(state, LUA_OPADD, &left, &right, &result), Is.True);
        Assert.That(ttisinteger(&result), Is.True);
        Assert.That(ivalue(&result), Is.EqualTo(42));

        Assert.That(luaO_rawarith(state, LUA_OPADD, &invalid, &right, &result), Is.False);
    }

    [Test]
    public void Function_luaO_arith()
    {
        using LuaState state = new();
        TValue left;
        TValue right;
        setivalue(&left, 8);
        setivalue(&right, 5);

        luaO_arith(state, LUA_OPSUB, &left, &right, state.get()->top.p);
        Assert.That(ttisinteger(s2v(state.get()->top.p)), Is.True);
        Assert.That(ivalue(s2v(state.get()->top.p)), Is.EqualTo(3));

        LuaOArithArgs args;
        args.op = LUA_OPADD;
        setnilvalue(&args.left);
        setivalue(&args.right, 1);
        Assert.That(luaD_rawrunprotected(state, ProtectedLuaOArith, &args), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("arithmetic"));
    }

    [Test]
    public void Function_luaO_str2num()
    {
        TValue value;

        Assert.That(luaO_str2num("42"u8.ToPointer(), &value), Is.Not.Zero);
        Assert.That(ttisinteger(&value), Is.True);
        Assert.That(ivalue(&value), Is.EqualTo(42));
        
        Assert.That(luaO_str2num("3.5"u8.ToPointer(), &value), Is.Not.Zero);
        Assert.That(ttisfloat(&value), Is.True);
        Assert.That(fltvalue(&value), Is.EqualTo(3.5));
        
        Assert.That(luaO_str2num("not-a-number"u8.ToPointer(), &value), Is.Zero);
    }

    [Test]
    public void Function_luaO_tostringbuff()
    {
        Span<byte> buffer = stackalloc byte[LUA_N2SBUFFSZ];
        TValue value;
        setivalue(&value, -123);
        
        int len = luaO_tostringbuff(&value, buffer);
        Assert.That(Encoding.UTF8.GetString(buffer[..len]), Is.EqualTo("-123"));
    }

    [Test]
    public void Function_luaO_hexavalue()
    {
        Assert.That(luaO_hexavalue('0'), Is.EqualTo(0));
        Assert.That(luaO_hexavalue('9'), Is.EqualTo(9));
        Assert.That(luaO_hexavalue('a'), Is.EqualTo(10));
        Assert.That(luaO_hexavalue('F'), Is.EqualTo(15));
    }

    [Test]
    public void Function_luaO_tostring()
    {
        using LuaState state = new();
        TValue value;
        setivalue(&value, 456);

        luaO_tostring(state, &value);
        Assert.That(ttisstring(&value), Is.True);
        Assert.That(LuaString(tsvalue(&value)), Is.EqualTo("456"));
    }

    [Test]
    public void Function_luaO_pushvfstring()
    {
        using LuaState state = new();

        string result = luaO_pushfstring(
            state,
            "value=%I char=%c utf=%U",
            (long)37,
            'x',
            (ulong)0x24);
        
        Assert.That(result, Is.Not.Null);
        Assert.That(StackString(state, -1), Is.EqualTo("value=37 char=x utf=$"));
    }

    [Test]
    public void Function_luaO_pushfstring()
    {
        using LuaState state = new();

        string result = luaO_pushfstring(state, "name=%s percent=%%", "lua");

        Assert.That(result, Is.Not.Null);
        Assert.That(StackString(state, -1), Is.EqualTo("name=lua percent=%"));
    }

    [Test]
    public void Function_luaO_chunkid()
    {
        Assert.That(luaO_chunkid("=literal"), Is.EqualTo("literal"));
        Assert.That(luaO_chunkid("@file.lua"), Is.EqualTo("file.lua"));
        Assert.That(luaO_chunkid("return 1\nreturn 2"), Contains.Substring("[string \"return 1"));
    }

    [Test]
    public void Function_luaV_equalobj()
    {
        TValue left;
        TValue right;
        setivalue(&left, 42);
        setfltvalue(&right, 42.0);
        Assert.That(luaV_equalobj(null, &left, &right));

        setivalue(&right, 43);
        Assert.That(luaV_equalobj(null, &left, &right), Is.False);
    }

    [Test]
    public void Function_luaV_lessthan()
    {
        using LuaState state = new();
        TValue left;
        TValue right;
        setivalue(&left, 1);
        setivalue(&right, 2);

        Assert.That(luaV_lessthan(state, &left, &right));
        Assert.That(luaV_lessthan(state, &right, &left), Is.False);
    }

    [Test]
    public void Function_luaV_lessequal()
    {
        using LuaState state = new();
        TValue left;
        TValue right;
        setivalue(&left, 2);
        setivalue(&right, 2);

        Assert.That(luaV_lessequal(state, &left, &right));
        setivalue(&right, 1);
        Assert.That(luaV_lessequal(state, &left, &right), Is.False);
    }

    [Test]
    public void Function_luaV_tonumber_()
    {
        using LuaState state = new();
        TValue value;
        double number = 0;
        setsvalue(state, &value, luaS_new(state, "12.25"));

        Assert.That(luaV_tonumber_(&value, &number));
        Assert.That(number, Is.EqualTo(12.25));

        setsvalue(state, &value, luaS_new(state, "abc"));
        Assert.That(luaV_tonumber_(&value, &number), Is.False);
    }

    [Test]
    public void Function_luaV_tointeger()
    {
        using LuaState state = new();
        TValue value;
        long integer = 0;
        setsvalue(state, &value, luaS_new(state, "17"));

        Assert.That(luaV_tointeger(&value, out integer, F2Imod.F2Ieq));
        Assert.That(integer, Is.EqualTo(17));

        setsvalue(state, &value, luaS_new(state, "17.5"));
        Assert.That(luaV_tointeger(&value, out integer, F2Imod.F2Ieq), Is.False);
        Assert.That(luaV_tointeger(&value, out integer, F2Imod.F2Ifloor));
        Assert.That(integer, Is.EqualTo(17));
    }

    [Test]
    public void Function_luaV_tointegerns()
    {
        using LuaState state = new();
        TValue value;
        long integer = 0;
        setfltvalue(&value, 9.0);

        Assert.That(luaV_tointegerns(&value, out integer, F2Imod.F2Ieq));
        Assert.That(integer, Is.EqualTo(9));

        setsvalue(state, &value, luaS_new(state, "9"));
        Assert.That(luaV_tointegerns(&value, out integer, F2Imod.F2Ieq), Is.False);
    }

    [Test]
    public void Function_luaV_flttointeger()
    {
        Assert.That(luaV_flttointeger(3.0, out long integer, F2Imod.F2Ieq));
        Assert.That(integer, Is.EqualTo(3));
        Assert.That(luaV_flttointeger(3.2, out integer, F2Imod.F2Ieq), Is.False);
        Assert.That(luaV_flttointeger(3.2, out integer, F2Imod.F2Iceil));
        Assert.That(integer, Is.EqualTo(4));
    }

    [Test]
    public void Function_luaV_finishget()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue tableValue;
        TValue key;
        sethvalue(state, &tableValue, table);
        setsvalue(state, &key, luaS_new(state, "missing"));

        byte tag = luaV_finishget(state, &tableValue, &key, state.get()->top.p, LUA_VEMPTY);
        Assert.That(tag, Is.EqualTo(LUA_VNIL));
        Assert.That(ttisnil(s2v(state.get()->top.p)));

        TValue invalid;
        setnilvalue(&invalid);
        Assert.That(luaD_rawrunprotected(state, ProtectedLuaVFinishGet, &invalid), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaV_finishset()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue table_value;
        TValue key;
        TValue value;
        TValue result;
        sethvalue(state, &table_value, table);
        setsvalue(state, &key, luaS_new(state, "created"));
        setivalue(&value, 321);

        luaV_finishset(state, &table_value, &key, &value, HNOTFOUND);
        Assert.That(tagisempty(luaH_getstr(table, tsvalue(&key), &result)), Is.False);
        Assert.That(ivalue(&result), Is.EqualTo(321));

        TValue invalid;
        setnilvalue(&invalid);
        Assert.That(luaD_rawrunprotected(state, ProtectedLuaVFinishSet, &invalid), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaV_concat()
    {
        using LuaState state = new();
        lua_pushliteral(state, "hello ");
        lua_pushliteral(state, "lua");

        luaV_concat(state, 2);

        Assert.That(lua_gettop(state), Is.EqualTo(1));
        Assert.That(StackString(state, -1), Is.EqualTo("hello lua"));
    }

    [Test]
    public void Function_luaV_idiv()
    {
        using LuaState state = new();
        DivArgs args = new()
        {
            left = 7,
            right = 2,
        };
        
        Assert.That(luaV_idiv(state, args.left, args.right), Is.EqualTo(3));
        
        args.right = 0;
        Assert.That(luaD_rawrunprotected(state, ProtectedLuaVIdiv, &args), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("divide by zero"));
    }

    [Test]
    public void Function_luaV_mod()
    {
        using LuaState state = new();
        DivArgs args = new()
        {
            left = -7,
            right = 3,
        };
        
        Assert.That(luaV_mod(state, args.left, args.right), Is.EqualTo(2));
        
        args.right = 0;
        Assert.That(luaD_rawrunprotected(state, ProtectedLuaVMod, &args), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("'n%0'"));
    }

    [Test]
    public void Function_luaV_modf()
    {
        using LuaState state = new();

        Assert.That(luaV_modf(state, -7.0, 3.0), Is.EqualTo(2.0));
    }

    [Test]
    public void Function_luaV_shiftl()
    {
        Assert.That(luaV_shiftl(1, 3), Is.EqualTo(8));
        Assert.That(luaV_shiftl(8, -2), Is.EqualTo(2));
        Assert.That(luaV_shiftl(1, sizeof(long) * 8), Is.EqualTo(0));
    }

    [Test]
    public void Function_luaV_objlen()
    {
        using LuaState state = new();
        TValue value;
        setsvalue(state, &value, luaS_new(state, "abc"));

        luaV_objlen(state, state.get()->top.p, &value);
        Assert.That(ttisinteger(s2v(state.get()->top.p)));
        Assert.That(ivalue(s2v(state.get()->top.p)), Is.EqualTo(3));

        setnilvalue(&value);
        Assert.That(luaD_rawrunprotected(state, ProtectedLuaVObjlen, &value), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaS_hashlongstr()
    {
        using LuaState state = new();
        TString* longString = luaS_newlstr(
            state,
            "this string is deliberately longer than the short string limit"u8);
        
        Assert.That(strisshr(longString), Is.False);
        Assert.That(longString->extra, Is.Zero);
        uint firstHash = luaS_hashlongstr(longString);
        Assert.That(longString->extra, Is.EqualTo(1));
        Assert.That(luaS_hashlongstr(longString), Is.EqualTo(firstHash));
    }

    [Test]
    public void Function_luaS_eqstr()
    {
        using LuaState state = new();
        TString* left = luaS_new(state, "same");
        TString* right = luaS_newlstr(state, "same"u8);
        TString* different = luaS_new(state, "different");
        
        Assert.That(luaS_eqstr(left, right));
        Assert.That(luaS_eqstr(left, different), Is.False);
    }

    [Test]
    public void Function_luaS_resize()
    {
        using LuaState state = new();
        stringtable* table = &G(state.get())->strt;
        int oldSize = table->size;

        luaS_resize(state, oldSize * 2);

        Assert.That(table->size, Is.EqualTo(oldSize * 2));
    }

    [Test]
    public void Function_luaS_clearcache()
    {
        using LuaState state = new();
        global_State* g = G(state.get());
        TString* cached = luaS_new(state, "cache-clear");
        g->strcache[0, 0] = cached;
        
        luaS_clearcache(g);
        
        Assert.That(g->strcache[0, 0], Is.Not.Null);
        if (iswhite((GCObject*)cached))
        {
            Assert.That(g->strcache[0, 0], Is.EqualTo(g->memerrmsg));
        }
    }

    [Test]
    public void Function_luaS_newudata()
    {
        using LuaState state = new();

        Udata* userdata = luaS_newudata(state, 16, 2);

        Assert.That(userdata, Is.Not.Null);
        Assert.That(userdata->len, Is.EqualTo(16u));
        Assert.That(userdata->nuvalue, Is.EqualTo(2));
        Assert.That(ttisnil(&((TValue*)userdata->uv)[0]));
        Assert.That(ttisnil(&((TValue*)userdata->uv)[1]));
    }

    [Test]
    public void Function_luaS_newlstr()
    {
        using LuaState state = new();

        TString* shortString = luaS_newlstr(state, "short"u8);
        TString* longString = luaS_newlstr(
            state,
            "this string is deliberately longer than the short string limit"u8);
        
        Assert.That(strisshr(shortString));
        Assert.That(strisshr(longString), Is.False);
        Assert.That(LuaString(shortString), Is.EqualTo("short"));
    }

    [Test]
    public void Function_luaS_new()
    {
        using LuaState state = new();

        TString* first = luaS_new(state, "cached");
        TString* second = luaS_new(state, "cached");

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Function_luaS_createlngstrobj()
    {
        using LuaState state = new();

        TString* str = luaS_createlngstrobj(state, 12);
        memcpy(getlngstr(str), "hello world!"u8.ToPointer(), 12);
        
        Assert.That(strisshr(str), Is.False);
        Assert.That(LuaString(str), Is.EqualTo("hello world!"));
    }

    [Test]
    public void Function_luaS_newextlstr()
    {
        using LuaState state = new();
        byte* external = "external string data that stays outside the Lua allocation"u8.ToPointer();

        TString* str = luaS_newextlstr(state, external, strlen(external), null, null);
        
        Assert.That(strisshr(str), Is.False);
        Assert.That(str->shrlen, Is.EqualTo(LSTRFIX));
        Assert.That(tsslen(str), Is.EqualTo(strlen(external)));
        Assert.That(getstr(str), Is.EqualTo(external));
    }

    [Test]
    public void Function_luaS_sizelngstr()
    {
        Assert.That(luaS_sizelngstr(10, LSTRREG), Is.GreaterThan(10));
        Assert.That(luaS_sizelngstr(10, LSTRFIX), Is.EqualTo((long)TString_falloc_offset));
        Assert.That(luaS_sizelngstr(10, LSTRMEM), Is.EqualTo(sizeof(TString)));
    }

    [Test]
    public void Function_luaS_normstr()
    {
        using LuaState state = new();
        byte* external = "external-short"u8.ToPointer();

        TString* externalString = luaS_newextlstr(state, external, strlen(external), null, null);
        TString* normalized = luaS_normstr(state, externalString);

        Assert.That(strisshr(normalized));
        Assert.That(LuaString(normalized), Is.EqualTo("external-short"));
    }

    [Test]
    public void Function_luaH_get()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue key;
        TValue value;
        TValue result;
        setsvalue(state, &key, NewAnchoredString(state, "key"));
        setivalue(&value, 99);
        luaH_set(state, table, &key, &value);

        Assert.That(luaH_get(table, &key, &result), Is.EqualTo(LUA_VNUMINT));
        Assert.That(ivalue(&result), Is.EqualTo(99));

        setsvalue(state, &key, NewAnchoredString(state, "missing"));
        Assert.That(tagisempty(luaH_get(table, &key, &result)));
    }

    [Test]
    public void Function_luaH_getshortstr()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TString* key = NewAnchoredString(state, "short");
        TValue value;
        TValue result;
        setivalue(&value, 11);
        TableSetString(state, table, key, &value);

        Assert.That(luaH_getshortstr(table, key, &result), Is.EqualTo(LUA_VNUMINT));
        Assert.That(ivalue(&result), Is.EqualTo(11));
        Assert.That(tagisempty(luaH_getshortstr(table, NewAnchoredString(state, "nope"), &result)));
    }

    [Test]
    public void Function_luaH_getstr()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TString* key = luaS_newlstr(
            state,
            "this long key is longer than the short string threshold"u8);
        
        TValue keyValue;
        setsvalue(state, &keyValue, key);
        TValue value;
        setivalue(&value, 12);
        luaH_set(state, table, &keyValue, &value);
        
        TValue result;
        Assert.That(luaH_getstr(table, key, &result), Is.EqualTo(LUA_VNUMINT));
        Assert.That(ivalue(&result), Is.EqualTo(12));
    }

    [Test]
    public void Function_luaH_getint()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue value;
        TValue result;
        setivalue(&value, 123);
        luaH_setint(state, table, 7, &value);

        Assert.That(luaH_getint(table, 7, &result), Is.EqualTo(LUA_VNUMINT));
        Assert.That(ivalue(&result), Is.EqualTo(123));
        Assert.That(tagisempty(luaH_getint(table, 8, &result)));
    }

    [Test]
    public void Function_luaH_Hgetshortstr()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TString* key = NewAnchoredString(state, "meta");
        TValue value;
        setivalue(&value, 44);
        TableSetString(state, table, key, &value);

        TValue* found = luaH_Hgetshortstr(table, key);
        Assert.That(ttisnil(found), Is.False);
        Assert.That(ivalue(found), Is.EqualTo(44));
        Assert.That(ttisnil(luaH_Hgetshortstr(table, NewAnchoredString(state, "missing"))));
    }

    [Test]
    public void Function_luaH_psetint()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue value;
        TValue result;
        setivalue(&value, 1);
        luaH_setint(state, table, 100, &value);

        setivalue(&value, 2);
        Assert.That(luaH_psetint(table, 100, &value), Is.EqualTo(HOK));
        luaH_getint(table, 100, &result);
        Assert.That(ivalue(&result), Is.EqualTo(2));

        Assert.That(luaH_psetint(table, 101, &value), Is.EqualTo(HNOTFOUND));
    }

    [Test]
    public void Function_luaH_psetshortstr()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TString* key = NewAnchoredString(state, "field");
        TValue value;
        TValue result;
        setivalue(&value, 1);
        TableSetString(state, table, key, &value);

        setivalue(&value, 2);
        Assert.That(luaH_psetshortstr(table, key, &value), Is.EqualTo(HOK));
        luaH_getshortstr(table, key, &result);
        Assert.That(ivalue(&result), Is.EqualTo(2));

        Table* metatable = NewAnchoredTable(state);
        table->metatable = metatable;
        invalidateTMcache(metatable);
        setivalue(&value, 3);
        Assert.That(luaH_psetshortstr(table, NewAnchoredString(state, "new-field"), &value), Is.Not.EqualTo(HOK));
    }

    [Test]
    public void Function_luaH_psetstr()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TString* key = NewAnchoredString(state, "name");
        TValue value;
        TValue result;
        setivalue(&value, 1);
        TableSetString(state, table, key, &value);

        setivalue(&value, 5);
        Assert.That(luaH_psetstr(table, key, &value), Is.EqualTo(HOK));
        luaH_getstr(table, key, &result);
        Assert.That(ivalue(&result), Is.EqualTo(5));
    }

    [Test]
    public void Function_luaH_pset()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue key;
        TValue value;
        TValue result;
        setivalue(&key, 25);
        setivalue(&value, 10);
        luaH_set(state, table, &key, &value);

        setivalue(&value, 20);
        Assert.That(luaH_pset(table, &key, &value), Is.EqualTo(HOK));
        luaH_get(table, &key, &result);
        Assert.That(ivalue(&result), Is.EqualTo(20));

        setnilvalue(&key);
        Assert.That(luaH_pset(table, &key, &value), Is.EqualTo(HNOTFOUND));
    }

    [Test]
    public void Function_luaH_setint()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue value;
        TValue result;
        setivalue(&value, 88);

        luaH_setint(state, table, 1, &value);

        Assert.That(luaH_getint(table, 1, &result), Is.EqualTo(LUA_VNUMINT));
        Assert.That(ivalue(&result), Is.EqualTo(88));
    }

    [Test]
    public void Function_luaH_set()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue key;
        TValue value;
        TValue result;
        setsvalue(state, &key, NewAnchoredString(state, "x"));
        setivalue(&value, 77);

        luaH_set(state, table, &key, &value);

        Assert.That(luaH_get(table, &key, &result), Is.EqualTo(LUA_VNUMINT));
        Assert.That(ivalue(&result), Is.EqualTo(77));
    }

    [Test]
    public void Function_luaH_finishset()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue key;
        TValue value;
        TValue result;
        setsvalue(state, &key, NewAnchoredString(state, "new"));
        setivalue(&value, 66);

        luaH_finishset(state, table, &key, &value, HNOTFOUND);

        Assert.That(luaH_get(table, &key, &result), Is.EqualTo(LUA_VNUMINT));
        Assert.That(ivalue(&result), Is.EqualTo(66));
    }

    [Test]
    public void Function_luaH_new()
    {
        using LuaState state = new();

        Table* table = NewAnchoredTable(state);

        Assert.That(table, Is.Not.Null);
        Assert.That(table->asize, Is.EqualTo(0u));
        Assert.That(isdummy(table));
    }

    [Test]
    public void Function_luaH_resize()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);

        luaH_resize(state, table, 4, 4);

        Assert.That(table->asize, Is.EqualTo(4u));
        Assert.That(luaH_size(table), Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void Function_luaH_resizearray()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);

        luaH_resizearray(state, table, 3);

        Assert.That(table->asize, Is.EqualTo(3u));
    }

    [Test]
    public void Function_luaH_size()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);

        long emptySize = luaH_size(table);
        Assert.That(emptySize, Is.GreaterThanOrEqualTo(sizeof(Table)));
        luaH_resize(state, table, 2, 2);
        Assert.That(luaH_size(table), Is.GreaterThan(emptySize));
    }

    [Test]
    public void Function_luaH_next()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue value;
        setivalue(&value, 55);
        luaH_setint(state, table, 1, &value);
        setnilvalue(s2v(state.get()->top.p));
        StackValue* key = state.get()->top.p++;
        
        Assert.That(luaH_next(state, table, key), Is.True);
        Assert.That(ttisinteger(s2v(key)), Is.True);
        Assert.That(ttisinteger(s2v(key + 1)), Is.True);
    }

    [Test]
    public void Function_luaH_getn()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue value;
        setivalue(&value, 1);
        luaH_setint(state, table, 1, &value);
        luaH_setint(state, table, 2, &value);

        Assert.That(luaH_getn(state, table), Is.EqualTo(2u));
    }

    [Test]
    public void Function_luaM_realloc_()
    {
        using LuaState state = new();

        void* block = luaM_realloc_(state, null, 0, 16);
        Assert.That(block, Is.Not.Null);
        block = luaM_realloc_(state, block, 16, 32);
        Assert.That(block, Is.Not.Null);
        Assert.That(luaM_realloc_(state, block, 32, 0), Is.Null);
    }

    [Test]
    public void Function_luaM_saferealloc_()
    {
        using LuaState state = new();

        void* block = luaM_saferealloc_(state, null, 0, 8);
        Assert.That(block, Is.Not.Null);
        block = luaM_saferealloc_(state, block, 8, 4);
        Assert.That(block, Is.Not.Null);
        luaM_free_(state, block, 4);
    }

    [Test]
    public void Function_luaM_free_()
    {
        using LuaState state = new();
        void* block = luaM_malloc_(state, 8, 0);
        long before = G(state.get())->GCdebt;

        luaM_free_(state, block, 8);

        Assert.That(G(state.get())->GCdebt, Is.EqualTo(before + 8));
    }

    [Test]
    public void Function_luaM_growaux_()
    {
        using LuaState state = new();
        int size = 0;
        int* values = (int*)luaM_growaux_(state, null, 0, ref size, sizeof(int), 16, "ints");
        
        Assert.That(values, Is.Not.Null);
        Assert.That(size, Is.GreaterThanOrEqualTo(4));
        luaM_free_(state, values, size * sizeof(int));
    }

    [Test]
    public void Function_luaM_shrinkvector_()
    {
        using LuaState state = new();
        int size = 8;
        int* values = (int*)luaM_malloc_(state, size * sizeof(int), 0);
        
        values = (int*)luaM_shrinkvector_(state, values, ref size, 2, sizeof(int));
        
        Assert.That(values, Is.Not.Null);
        Assert.That(size, Is.EqualTo(2));
        luaM_free_(state, values, size * sizeof(int));
    }

    [Test]
    public void Function_luaM_malloc_()
    {
        using LuaState state = new();

        void* block = luaM_malloc_(state, 24, 0);

        Assert.That(block, Is.Not.Null);
        luaM_free_(state, block, 24);
        Assert.That(luaM_malloc_(state, 0, 0), Is.Null);
    }

    [Test]
    public void Function_luaM_toobig()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaMToobig, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("block too big"));
    }

    [Test]
    public void Function_luaZ_init()
    {
        using LuaState state = new();
        ReaderState reader = new()
        {
            chunks = [(nint)"abc"u8.ToPointer()],
        };
        using GCHandle<ReaderState> handle = new(reader);
        void* handlePtr = handle.ToPointer();
        
        Zio zio;
        luaZ_init(state, &zio, &ChunkReader, handlePtr);
        
        Assert.That(zio.n, Is.Zero);
        Assert.That(zio.p, Is.Null);
        Assert.That(zio.data, Is.EqualTo(handlePtr));
    }

    [Test]
    public void Function_luaZ_read()
    {
        using LuaState state = new();
        ReaderState reader = new()
        {
            chunks = [(nint)"ab"u8.ToPointer(), (nint)"cd"u8.ToPointer()],
        };
        using GCHandle<ReaderState> handle = new(reader);
        void* handlePtr = handle.ToPointer();
        
        Zio zio;
        luaZ_init(state, &zio, &ChunkReader, handlePtr);
        byte* buffer = stackalloc byte[5];
        
        Assert.That(luaZ_read(&zio, buffer, 4), Is.EqualTo(0u));
        Assert.That(new string((sbyte*)buffer), Is.EqualTo("abcd"));
        Assert.That(luaZ_read(&zio, buffer, 2), Is.EqualTo(2u));
    }

    [Test]
    public void Function_luaZ_getaddr()
    {
        using LuaState state = new();
        ReaderState reader = new()
        {
            chunks = [(nint)"abcdef"u8.ToPointer()],
        };
        using GCHandle<ReaderState> handle = new(reader);
        void* handlePtr = handle.ToPointer();
        
        Zio zio;
        luaZ_init(state, &zio, &ChunkReader, handlePtr);
        
        void* address = luaZ_getaddr(&zio, 3);

        Assert.That(address, Is.Not.Null);
        Assert.That(new string((sbyte*)address)[..3], Is.EqualTo("abc"));
        Assert.That(luaZ_getaddr(&zio, 10), Is.Null);
    }

    [Test]
    public void Function_luaZ_fill()
    {
        using LuaState state = new();
        ReaderState reader = new()
        {
            chunks = [(nint)"xy"u8.ToPointer()],
        };
        using GCHandle<ReaderState> handle = new(reader);
        void* handlePtr = handle.ToPointer();
        
        Zio zio;
        luaZ_init(state, &zio, &ChunkReader, handlePtr);
        
        Assert.That(luaZ_fill(&zio), Is.EqualTo('x'));
        Assert.That(zio.n, Is.EqualTo(1u));
        Assert.That(*zio.p, Is.EqualTo('y'));
    }

    [Test]
    public void Function_luaF_newproto()
    {
        using LuaState state = new();

        Proto* proto = luaF_newproto(state);

        Assert.That(proto, Is.Not.Null);
        Assert.That(proto->sizecode, Is.EqualTo(0));
        Assert.That(proto->code, Is.Null);
        Assert.That(proto->source, Is.Null);
    }

    [Test]
    public void Function_luaF_newCclosure()
    {
        using LuaState state = new();

        CClosure* closure = luaF_newCclosure(state, 2);

        Assert.That(closure, Is.Not.Null);
        Assert.That(closure->nupvalues, Is.EqualTo(2));
        Assert.That(closure->tt, Is.EqualTo(LUA_VCCL));
    }

    [Test]
    public void Function_luaF_newLclosure()
    {
        using LuaState state = new();

        LClosure* closure = luaF_newLclosure(state, 2);

        Assert.That(closure, Is.Not.Null);
        Assert.That(closure->nupvalues, Is.EqualTo(2));
        Assert.That(closure->p, Is.Null);
        Assert.That(LClosure.GetUpValue(closure, 0), Is.Null);
        Assert.That(LClosure.GetUpValue(closure, 1), Is.Null);
    }

    [Test]
    public void Function_luaF_initupvals()
    {
        using LuaState state = new();
        LClosure* closure = luaF_newLclosure(state, 2);

        luaF_initupvals(state, closure);

        Assert.That(LClosure.GetUpValue(closure, 0), Is.Not.Null);
        Assert.That(LClosure.GetUpValue(closure, 1), Is.Not.Null);
        Assert.That(upisopen(LClosure.GetUpValue(closure, 0)), Is.False);
        Assert.That(ttisnil(LClosure.GetUpValue(closure, 0)->v.p), Is.True);
    }

    [Test]
    public void Function_luaF_findupval()
    {
        using LuaState state = new();
        lua_pushinteger(state, 123);
        StackValue* level = state.get()->top.p - 1;

        UpVal* first = luaF_findupval(state, level);
        UpVal* second = luaF_findupval(state, level);

        Assert.That(second, Is.EqualTo(first));
        Assert.That(upisopen(first));
        Assert.That(ivalue(first->v.p), Is.EqualTo(123));
        luaF_closeupval(state, level);
    }

    [Test]
    public void Function_luaF_newtbcupval()
    {
        using LuaState state = new();
        setbfvalue(s2v(state.get()->top.p));
        StackValue* falseLevel = state.get()->top.p++;

        luaF_newtbcupval(state, falseLevel);
        Assert.That(state.get()->tbclist.p, Is.EqualTo(state.get()->stack.p));

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaFNewTbcUpval, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("non-closable"));
    }

    [Test]
    public void Function_luaF_closeupval()
    {
        using LuaState state = new();
        lua_pushinteger(state, 42);
        StackValue* level = state.get()->top.p - 1;
        UpVal* upvalue = luaF_findupval(state, level);

        luaF_closeupval(state, level);

        Assert.That(upisopen(upvalue), Is.False);
        Assert.That(ivalue(upvalue->v.p), Is.EqualTo(42));
    }

    [Test]
    public void Function_luaF_close()
    {
        using LuaState state = new();
        lua_pushinteger(state, 42);
        StackValue* level = state.get()->top.p - 1;
        UpVal* upvalue = luaF_findupval(state, level);

        StackValue* restored = luaF_close(state, level, LUA_OK, false);

        Assert.That(restored, Is.EqualTo(level));
        Assert.That(upisopen(upvalue), Is.False);
    }

    [Test]
    public void Function_luaF_unlinkupval()
    {
        using LuaState state = new();
        lua_pushinteger(state, 42);
        StackValue* level = state.get()->top.p - 1;
        UpVal* upvalue = luaF_findupval(state, level);

        luaF_unlinkupval(upvalue);

        Assert.That(state.get()->openupval, Is.Null);
        upvalue->v.p = &upvalue->u.value;
        setnilvalue(upvalue->v.p);
    }

    [Test]
    public void Function_luaF_protosize()
    {
        using LuaState state = new();
        Proto* proto = luaF_newproto(state);

        Assert.That(luaF_protosize(proto), Is.GreaterThanOrEqualTo(sizeof(Proto)));
    }

    [Test]
    public void Function_luaF_getlocalname()
    {
        using LuaState state = new();
        Proto* proto = luaF_newproto(state);
        proto->sizelocvars = 1;
        proto->locvars = luaM_newvector<LocVar>(state.get(), 1);
        proto->locvars[0].varname = luaS_new(state, "local_name");
        proto->locvars[0].startpc = 0;
        proto->locvars[0].endpc = 5;

        Assert.That(luaF_getlocalname(proto, 1, 3), Is.EqualTo("local_name"));
        Assert.That(luaF_getlocalname(proto, 1, 6), Is.Null);
    }

    [Test]
    public void Function_luaG_getfuncline()
    {
        using LuaState state = new();
        Proto* proto = luaF_newproto(state);
        Assert.That(luaG_getfuncline(proto, 0), Is.EqualTo(-1));

        proto->linedefined = 10;
        proto->sizelineinfo = 3;
        proto->lineinfo = luaM_newvector<sbyte>(state.get(), 3);
        proto->lineinfo[0] = 1;
        proto->lineinfo[1] = 2;
        proto->lineinfo[2] = -1;

        Assert.That(luaG_getfuncline(proto, 0), Is.EqualTo(11));
        Assert.That(luaG_getfuncline(proto, 1), Is.EqualTo(13));
        Assert.That(luaG_getfuncline(proto, 2), Is.EqualTo(12));
    }

    [Test]
    public void Function_luaG_findlocal()
    {
        using LuaState state = new();
        lua_pushinteger(state, 17);
        StackValue* position = null;

        string? name = luaG_findlocal(state, state.get()->ci, 1, &position);

        Assert.That(name, Is.Not.Null);
        Assert.That(name, Is.EqualTo("(C temporary)"));
        Assert.That(position, Is.Not.Null);
        Assert.That(ivalue(s2v(position)), Is.EqualTo(17));
    }

    [Test]
    public void Function_luaG_typeerror()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGTypeerror, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("nil value"));
    }

    [Test]
    public void Function_luaG_callerror()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGCallerror, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("call"));
    }

    [Test]
    public void Function_luaG_forerror()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGForerror, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("for"));
    }

    [Test]
    public void Function_luaG_concaterror()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGConcaterror, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("concatenate"));
    }

    [Test]
    public void Function_luaG_opinterror()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGOpinterror, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("arithmetic"));
    }

    [Test]
    public void Function_luaG_tointerror()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGTointError, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("integer representation"));
    }

    [Test]
    public void Function_luaG_ordererror()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGOrdererror, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("compare"));
    }

    [Test]
    public void Function_luaG_runerror()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGRunerror, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("internal failure"));
    }

    [Test]
    public void Function_luaG_addinfo()
    {
        using LuaState state = new();
        TString* source = luaS_new(state, "@file.lua");

        string message = luaG_addinfo(state, "message", source, 12);
        
        Assert.That(message, Is.Not.Null);
        Assert.That(message, Contains.Substring("file.lua:12: message"));
    }

    [Test]
    public void Function_luaG_errormsg()
    {
        using LuaState state = new();

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaGErrormsg, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Is.EqualTo("direct error"));
    }

    [Test]
    public void Function_luaE_setdebt()
    {
        using LuaState state = new();
        global_State* global = G(state.get());
        long total = gettotalbytes(global);

        luaE_setdebt(global, 1234);

        Assert.That(global->GCdebt, Is.EqualTo(1234));
        Assert.That(gettotalbytes(global), Is.EqualTo(total));
    }

    [Test]
    public void Function_luaE_threadsize()
    {
        using LuaState state = new();
        Assert.That(luaE_threadsize(state), Is.GreaterThanOrEqualTo(sizeof(lua_State)));
    }

    [Test]
    public void Function_luaE_extendCI()
    {
        using LuaState state = new();
        int old_count = state.get()->nci;

        CallInfo* ci = luaE_extendCI(state);

        Assert.That(ci, Is.Not.Null);
        Assert.That(state.get()->ci->next, Is.EqualTo(ci));
        Assert.That(state.get()->nci, Is.EqualTo(old_count + 1));
    }

    [Test]
    public void Function_luaE_shrinkCI()
    {
        using LuaState state = new();
        CallInfo* @base = state.get()->ci;
        CallInfo* first = luaE_extendCI(state);
        state.get()->ci = first;
        CallInfo* second = luaE_extendCI(state);
        state.get()->ci = second;
        luaE_extendCI(state);
        state.get()->ci = @base;
        int oldCount = state.get()->nci;

        luaE_shrinkCI(state);

        Assert.That(state.get()->nci, Is.LessThan(oldCount));
    }

    [Test]
    public void Function_luaE_checkcstack()
    {
        using LuaState state = new();

        luaE_checkcstack(state);
        Assert.That(luaD_rawrunprotected(state, ProtectedLuaECheckCStack, null), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(state, -1), Contains.Substring("C stack overflow"));
    }

    [Test]
    public void Function_luaE_incCstack()
    {
        using LuaState state = new();
        uint oldCalls = state.get()->nCcalls;

        luaE_incCstack(state);
        Assert.That(state.get()->nCcalls, Is.EqualTo(oldCalls + 1));
        state.get()->nCcalls = oldCalls;

        Assert.That(luaD_rawrunprotected(state, ProtectedLuaEIncCStack, null), Is.EqualTo(LUA_ERRRUN));
    }

    [Test]
    public void Function_luaE_warning()
    {
        using LuaState state = new();
        WarningCapture capture = new();
        using GCHandle<WarningCapture> handle = new(capture);
        lua_setwarnf(state, &WarningHandler, handle.ToPointer());
        
        luaE_warning(state, "hello", false);
        
        Assert.That(capture.messages, Has.Count.EqualTo(1));
        Assert.That(capture.messages[0].msg, Is.EqualTo("hello"));
        Assert.That(capture.messages[0].tocont, Is.False);
    }

    [Test]
    public void Function_luaE_warnerror()
    {
        using LuaState state = new();
        WarningCapture capture = new();
        using GCHandle<WarningCapture> handle = new(capture);
        lua_setwarnf(state, &WarningHandler, handle.ToPointer());
        lua_pushliteral(state, "boom");
        
        luaE_warnerror(state, "test");
        
        Assert.That(capture.messages, Has.Count.EqualTo(5));
        Assert.That(capture.messages[0].msg, Is.EqualTo("error in "));
        Assert.That(capture.messages[1].msg, Is.EqualTo("test"));
        Assert.That(capture.messages[3].msg, Is.EqualTo("boom"));
        Assert.That(capture.messages[^1].tocont, Is.False);
    }

    [Test]
    public void Function_luaE_resetthread()
    {
        using LuaState state = new();
        lua_State* thread = lua_newthread(state);
        Assert.That(thread, Is.Not.Null);
        lua_pushinteger(thread, 42);

        Assert.That(luaE_resetthread(thread, LUA_OK), Is.EqualTo(LUA_OK));
        Assert.That(lua_gettop(thread), Is.EqualTo(0));
    }

    [Test]
    public void Function_luaC_fix()
    {
        using LuaState state = new();
        global_State* global = G(state.get());
        GCObject* previous_allgc = global->allgc;
        TString* str = luaS_new(state, "fixed-object");
        GCObject * obj = obj2gco(str);

        Assert.That(global->allgc, Is.EqualTo(obj));

        luaC_fix(state, obj);

        Assert.That(global->fixedgc, Is.EqualTo(obj));
        Assert.That(global->allgc, Is.EqualTo(previous_allgc));
        Assert.That(isgrey(obj));
        Assert.That(getage(obj), Is.EqualTo(G_OLD));
    }

    [Test]
    public void Function_luaC_freeallobjects()
    {
        lua_State* L = luaL_newstate();
        Assert.That(L, Is.Not.Null);
        lua_pushliteral(L, "temporary");
        lua_newtable(L);
        L->top.p = L->stack.p + 1;
        global_State* global = G(L);

        luaC_freeallobjects(L);

        Assert.That(global->gcstp, Is.EqualTo(GCSTPCLS));
        Assert.That(global->finobj, Is.Null);
        Assert.That(global->strt.nuse, Is.EqualTo(0));
        DestroyStateAfterLuaCFreeallobjects(L);
    }

    [Test]
    public void Function_luaC_step()
    {
        using LuaState state = new();
        global_State* global = G(state.get());

        luaC_step(state);

        Assert.That(global->gcstate, Is.LessThanOrEqualTo(GCSpause));
        
        byte oldStop = global->gcstp;
        luaE_setdebt(global, 0);
        global->gcstp = GCSTPUSR;
        luaC_step(state);
        Assert.That(global->GCdebt, Is.EqualTo(20000));
        global->gcstp = oldStop;
    }

    [Test]
    public void Function_luaC_runtilstate()
    {
        using LuaState state = new();

        luaC_changemode(state, KGC_INC);
        luaC_runtilstate(state, GCSpropagate, true);
        Assert.That(G(state.get())->gcstate, Is.EqualTo(GCSpropagate));
        luaC_runtilstate(state, GCSenteratomic, true);
        Assert.That(G(state.get())->gcstate, Is.EqualTo(GCSenteratomic));
        luaC_runtilstate(state, GCScallfin, true);
        Assert.That(G(state.get())->gcstate, Is.EqualTo(GCScallfin));
        luaC_runtilstate(state, GCSpause, true);
        Assert.That(G(state.get())->gcstate, Is.EqualTo(GCSpause));
    }

    [Test]
    public void Function_luaC_fullgc()
    {
        using LuaState state = new();
        global_State* global = G(state.get());

        luaC_fullgc(state, false);

        Assert.That(global->gcstate, Is.EqualTo(GCSpause));
        Assert.That(global->gcemergency, Is.False);

        luaC_fullgc(state, true);
        Assert.That(global->gcstate, Is.EqualTo(GCSpause));
        Assert.That(global->gcemergency, Is.False);
    }

    [Test]
    public void Function_luaC_newobj()
    {
        using LuaState state = new();
        global_State* global = G(state.get());
        GCObject* previous_allgc = global->allgc;

        GCObject* @object = luaC_newobj(state, LUA_VCCL, sizeCclosure(0));
        CClosure* closure = gco2ccl(@object);
        closure->nupvalues = 0;
        closure->f = null;
        
        Assert.That(global->allgc, Is.EqualTo(@object));
        Assert.That(@object->next, Is.EqualTo(previous_allgc));
        Assert.That(@object->tt, Is.EqualTo(LUA_VCCL));
        Assert.That(@object->marked, Is.EqualTo(luaC_white(global)));
        Assert.That(iswhite(@object));
    }

    [Test]
    public void Function_luaC_barrier_()
    {
        using LuaState state = new();
        global_State* global = G(state.get());
        byte oldState = global->gcstate;
        byte oldKind = global->gckind;

        Table* owner = NewAnchoredTable(state);
        Table* value = NewAnchoredTable(state);
        GCObject* ownerObject = obj2gco(owner);
        GCObject* valueObject = obj2gco(value);
        MarkObjectBlack(ownerObject);
        setage(ownerObject, G_OLD);
        MarkObjectWhite(global, valueObject);
        global->gcstate = GCSpropagate;
        global->gckind = KGC_INC;
        
        luaC_barrier_(state, ownerObject, valueObject);
        
        Assert.That(isgrey(valueObject));
        Assert.That(getage(valueObject), Is.EqualTo(G_OLD0));
        Assert.That(global->grey, Is.EqualTo(valueObject));
        
        Table* sweepOwner = NewAnchoredTable(state);
        Table* sweepValue = NewAnchoredTable(state);
        GCObject* sweepOwnerObject = obj2gco(sweepOwner);
        GCObject* sweepValueObject = obj2gco(sweepValue);
        MarkObjectBlack(sweepOwnerObject);
        MarkObjectWhite(global, sweepValueObject);
        global->gcstate = GCSswpallgc;
        global->gckind = KGC_INC;
        
        luaC_barrier_(state, sweepOwnerObject, sweepValueObject);
        
        Assert.That(iswhite(sweepOwnerObject));
        Assert.That(isblack(sweepOwnerObject), Is.False);
        global->gcstate = oldState;
        global->gckind = oldKind;
    }

    [Test]
    public void Function_luaC_barrierback_()
    {
        using LuaState state = new();
        global_State* global = G(state.get());

        Table* owner = NewAnchoredTable(state);
        GCObject* ownerObject = obj2gco(owner);
        MarkObjectBlack(ownerObject);
        setage(ownerObject, G_OLD);
        
        luaC_barrierback_(state, ownerObject);
        
        Assert.That(isgrey(ownerObject));
        Assert.That(getage(ownerObject), Is.EqualTo(G_TOUCHED1));
        Assert.That(global->greyagain, Is.EqualTo(ownerObject));
        
        Table* touched = NewAnchoredTable(state);
        GCObject* touchedObject = obj2gco(touched);
        MarkObjectBlack(touchedObject);
        setage(touchedObject, G_TOUCHED2);
        
        luaC_barrierback_(state, touchedObject);
        
        Assert.That(isgrey(touchedObject));
        Assert.That(getage(touchedObject), Is.EqualTo(G_TOUCHED1));
    }

    [Test]
    public void Function_luaC_checkfinaliser()
    {
        using LuaState state = new();
        global_State* global = G(state.get());
        Udata* userdata = NewAnchoredUdata(state, 4, 0);
        GCObject* obj = obj2gco(userdata);
        Table* metatable = NewAnchoredTable(state);
        TValue finalizer;
        setfvalue(&finalizer, &ReturnBoolean);
        TableSetString(state, metatable, global->tmname[(int)TMS.GC], &finalizer);
        invalidateTMcache(metatable);
        
        Assert.That(ListContains(global->allgc, obj), Is.True);
        
        luaC_checkfinaliser(state, obj, metatable);
        
        Assert.That(tofinalise(obj));
        Assert.That(global->finobj, Is.EqualTo(obj));
        Assert.That(ListContains(global->allgc, obj), Is.False);
        
        GCObject* finobj = global->finobj;
        luaC_checkfinaliser(state, obj, metatable);
        Assert.That(global->finobj, Is.EqualTo(finobj));
        
        Udata* noFinaliserUserdata = NewAnchoredUdata(state, 4, 0);
        GCObject* noFinaliserObject = obj2gco(noFinaliserUserdata);
        Table* emptyMetatable = NewAnchoredTable(state);
        luaC_checkfinaliser(state, noFinaliserObject, emptyMetatable);
        Assert.That(tofinalise(noFinaliserObject), Is.False);
    }

    [Test]
    public void Function_luaC_changemode()
    {
        using LuaState state = new();
        global_State* global = G(state.get());

        luaC_changemode(state, KGC_GENMINOR);
        Assert.That(global->gckind, Is.EqualTo(KGC_GENMINOR));
        luaC_changemode(state, KGC_GENMINOR);
        Assert.That(global->gckind, Is.EqualTo(KGC_GENMINOR));
        luaC_changemode(state, KGC_INC);
        Assert.That(global->gckind, Is.EqualTo(KGC_INC));
        global->gckind = KGC_GENMAJOR;
        luaC_changemode(state, KGC_INC);
        Assert.That(global->gckind, Is.EqualTo(KGC_INC));
    }

    [Test]
    public void Macro_GCColorBits()
    {
        using LuaState state = new();
        global_State* global = G(state.get());
        Table* table = NewAnchoredTable(state);
        GCObject* obj = obj2gco(table);

        MarkObjectWhite(global, obj);
        Assert.That(iswhite(obj));
        Assert.That(isblack(obj), Is.False);
        Assert.That(isgrey(obj), Is.False);
        Assert.That(isdead(global, obj), Is.False);
        
        changewhite(obj);
        Assert.That(isdead(global, obj));
        changewhite(obj);
        Assert.That(isdead(global, obj), Is.False);
        
        MarkObjectBlack(obj);
        Assert.That(isblack(obj));
        Assert.That(iswhite(obj), Is.False);
        
        MarkObjectGrey(obj);
        Assert.That(isgrey(obj));
        Assert.That(iswhite(obj), Is.False);
        Assert.That(isblack(obj), Is.False);
    }

    [Test]
    public void Macro_GCAgeBits()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        GCObject* obj = obj2gco(table);

        setage(obj, G_NEW);
        Assert.That(getage(obj), Is.EqualTo(G_NEW));
        Assert.That(isold(obj), Is.False);

        setage(obj, G_SURVIVAL);
        Assert.That(getage(obj), Is.EqualTo(G_SURVIVAL));
        Assert.That(isold(obj), Is.False);

        setage(obj, G_OLD0);
        Assert.That(getage(obj), Is.EqualTo(G_OLD0));
        Assert.That(isold(obj));

        setage(obj, G_TOUCHED2);
        Assert.That(getage(obj), Is.EqualTo(G_TOUCHED2));
        Assert.That(isold(obj));
    }

    [Test]
    public void Macro_GCStatePredicates()
    {
        using LuaState state = new();
        global_State* global = G(state.get());
        byte old_state = global->gcstate;

        global->gcstate = GCSpropagate;
        Assert.That(keepinvariant(global));
        Assert.That(issweepphase(global), Is.False);

        global->gcstate = GCSswpallgc;
        Assert.That(keepinvariant(global), Is.False);
        Assert.That(issweepphase(global));

        global->gcstate = GCScallfin;
        Assert.That(keepinvariant(global), Is.False);
        Assert.That(issweepphase(global), Is.False);
        global->gcstate = old_state;
    }

    [Test]
    public void Macro_GCRunningFlags()
    {
        using LuaState state = new();
        global_State* global = G(state.get());
        byte oldStop = global->gcstp;

        global->gcstp = 0;
        Assert.That(gcrunning(global));

        global->gcstp = GCSTPUSR;
        Assert.That(gcrunning(global), Is.False);

        global->gcstp = GCSTPUSR | GCSTPGC;
        Assert.That(gcrunning(global), Is.False);
        global->gcstp = oldStop;
    }

    [Test]
    public void Function_luaT_objtypename()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        TValue tableValue;
        sethvalue(state, &tableValue, table);

        Assert.That(luaT_objtypename(state, &tableValue), Is.EqualTo("table"));

        Table* metatable = NewAnchoredTable(state);
        TValue key;
        TValue name;
        setsvalue(state, &key, NewAnchoredString(state, "__name"));
        setsvalue(state, &name, NewAnchoredString(state, "custom"));
        luaH_set(state, metatable, &key, &name);
        table->metatable = metatable;

        Assert.That(luaT_objtypename(state, &tableValue), Is.EqualTo("custom"));
    }

    [Test]
    public void Function_luaT_gettm()
    {
        using LuaState state = new();
        Table* metatable = NewAnchoredTable(state);
        TValue value;
        setfvalue(&value, &ReturnBoolean);
        TableSetString(state, metatable, G(state.get())->tmname[(int)TMS.EQ], &value);

        TValue* tm = luaT_gettm(metatable, TMS.EQ, G(state.get())->tmname[(int)TMS.EQ]);

        Assert.That(tm, Is.Not.Null);
        Assert.That(ttisfunction(tm));
        Assert.That(luaT_gettm(metatable, TMS.LEN, G(state.get())->tmname[(int)TMS.LEN]), Is.Null);
    }

    [Test]
    public void Function_luaT_gettmbyobj()
    {
        using LuaState state = new();
        Table* table = NewAnchoredTable(state);
        Table* metatable = NewAnchoredTable(state);
        TValue tableValue;
        sethvalue(state, &tableValue, table);
        TValue value;
        setfvalue(&value, &ReturnBoolean);
        TableSetString(state, metatable, G(state.get())->tmname[(int)TMS.LEN], &value);
        table->metatable = metatable;
        
        TValue* tm = luaT_gettmbyobj(state, &tableValue, TMS.LEN);
        
        Assert.That(ttisnil(tm), Is.False);
        Assert.That(ttisfunction(tm), Is.True);
    }
}
