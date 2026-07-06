namespace DigitalSingularity.Lua.Test;

using System.Text;
using static DigitalSingularity.Lua.Lua;

#pragma warning disable NUnit2045

public unsafe class OpcodeTests
{
    private static string StackString(lua_State* L, int idx)
    {
        return lua_tonetstring(L, idx) ?? "";
    }

    private static Proto* StackProto(lua_State* L, int index)
    {
        int absolute = lua_absindex(L, index);
        TValue* value = s2v(L->ci->func.p + absolute);
        return clLvalue(value)->p;
    }

    private bool HasOpcode(Proto* proto, OpCode opcode)
    {
        for (int pc = 0; pc < proto->sizecode; ++pc)
        {
            if (GET_OPCODE(proto->code[pc]) == opcode)
            {
                return true;
            }
        }

        for (int i = 0; i < proto->sizep; ++i)
        {
            if (this.HasOpcode(proto->p[i], opcode))
            {
                return true;
            }
        }

        return false;
    }

    private string DumpOpcodes(Proto* proto, int depth = 0)
    {
        StringBuilder sb = new();

        for (int pc = 0; pc < proto->sizecode; ++pc)
        {
            for (int i = 0; i < depth; ++i)
            {
                sb.Append("  ");
            }

            sb.Append(pc)
                .Append(": ")
                .Append(opnames[(int)GET_OPCODE(proto->code[pc])])
                .AppendLine();
        }

        for (int i = 0; i < proto->sizep; ++i)
        {
            for (int j = 0; j < depth; ++j)
            {
                sb.Append("  ");
            }

            sb.Append("proto ")
                .Append(i)
                .AppendLine(":");

            sb.Append(this.DumpOpcodes(proto->p[i], depth + 1));
        }

        return sb.ToString();
    }

    private string RepeatedListConstructor(int count)
    {
        string source = "local t = {";
        for (int i = 0; i < count; ++i)
        {
            source += "1,";
        }

        source += "}\nassert(#t == ";
        source += count;
        source += " and t[1] == 1 and t[";
        source += count;
        source += "] == 1)\n";
        return source;
    }

    private readonly LuaState state = new();

    [SetUp]
    public void SetUp()
    {
        luaL_openlibs(this.state);
    }

    private void ExpectRunsWithOpcode(OpCode opcode, string source)
    {
        lua_settop(this.state, 0);
        Assert.That(luaL_loadstring(this.state, source), Is.EqualTo(LUA_OK), () => StackString(this.state, -1));
        Proto* proto = StackProto(this.state, -1);
        Assert.That(this.HasOpcode(proto, opcode), Is.True, () => this.DumpOpcodes(proto));
        Console.WriteLine("Starting function call");
        Assert.That(lua_pcall(this.state, 0, 0, 0), Is.EqualTo(LUA_OK), () => StackString(this.state, -1));
        lua_settop(this.state, 0);
    }

    private void ExpectRuntimeErrorWithOpcode(
        OpCode opcode,
        string source,
        string expectedMessage)
    {
        lua_settop(this.state, 0);
        Assert.That(luaL_loadstring(this.state, source), Is.EqualTo(LUA_OK), () => StackString(this.state, -1));
        Proto* proto = StackProto(this.state, -1);
        Assert.That(this.HasOpcode(proto, opcode), Is.True, () => this.DumpOpcodes(proto));
        Console.WriteLine("Starting function call");
        Assert.That(lua_pcall(this.state, 0, 0, 0), Is.EqualTo(LUA_ERRRUN));
        Assert.That(StackString(this.state, -1), Contains.Substring(expectedMessage));
        lua_settop(this.state, 0);
    }

    private void ExpectPatchedLoadKxString(string expected)
    {
        lua_settop(this.state, 0);
        Assert.That(luaL_loadstring(this.state, "return nil"), Is.EqualTo(LUA_OK), () => StackString(this.state, -1));
        Proto* proto = StackProto(this.state, -1);
        this.ReplaceWithLoadKx(proto);
        TString* value = luaS_new(this.state, expected);
        setsvalue(this.state.get(), &proto->k[MAXARG_Bx + 1], value);
        luaC_objbarrier(this.state.get(), (GCObject*)proto, (GCObject*)value);

        Assert.That(this.HasOpcode(proto, OpCode.OP_LOADKX), Is.True, () => this.DumpOpcodes(proto));
        Assert.That(this.HasOpcode(proto, OpCode.OP_EXTRAARG), Is.True, () => this.DumpOpcodes(proto));
        Assert.That(lua_pcall(this.state, 0, 1, 0), Is.EqualTo(LUA_OK), () => StackString(this.state, -1));
        Assert.That(lua_tostring(this.state, -1), Is.EqualTo(expected));
        lua_settop(this.state, 0);
    }

    private void ExpectPatchedLoadKxInteger(long expected)
    {
        lua_settop(this.state, 0);
        Assert.That(luaL_loadstring(this.state, "return nil"), Is.EqualTo(LUA_OK), () => StackString(this.state, -1));
        Proto* proto = StackProto(this.state, -1);
        this.ReplaceWithLoadKx(proto);
        setivalue(&proto->k[MAXARG_Bx + 1], expected);

        Assert.That(this.HasOpcode(proto, OpCode.OP_LOADKX), Is.True, () => this.DumpOpcodes(proto));
        Assert.That(this.HasOpcode(proto, OpCode.OP_EXTRAARG), Is.True, () => this.DumpOpcodes(proto));
        Assert.That(lua_pcall(this.state, 0, 1, 0), Is.EqualTo(LUA_OK), () => StackString(this.state, -1));
        Assert.That(lua_tointeger(this.state, -1), Is.EqualTo(expected));
        lua_settop(this.state, 0);
    }

    private lua_State* L()
    {
        return this.state.get(); 
    }

    private void ReplaceWithLoadKx(Proto* proto)
    {
        luaM_freearray(this.state.get(), proto->code, proto->sizecode);
        luaM_freearray(
            this.state.get(),
            proto->lineinfo,
            proto->sizelineinfo);
        luaM_freearray(
            this.state.get(),
            proto->abslineinfo,
            proto->sizeabslineinfo);
        luaM_freearray(this.state.get(), proto->k, proto->sizek);

        proto->flag = 0;
        proto->numparams = 0;
        proto->maxstacksize = 2;
        proto->sizelineinfo = 0;
        proto->lineinfo = null;
        proto->sizeabslineinfo = 0;
        proto->abslineinfo = null;

        proto->sizecode = 3;
        proto->code = luaM_newvector<uint>(this.state.get(), proto->sizecode);
        proto->code[0] = CREATE_ABx(OpCode.OP_LOADKX, 0, 0);
        proto->code[1] = CREATE_Ax(OpCode.OP_EXTRAARG, MAXARG_Bx + 1);
        proto->code[2] = CREATE_ABCk(OpCode.OP_RETURN1, 0, 2, 0, false);

        proto->sizek = MAXARG_Bx + 2;
        proto->k = luaM_newvector<TValue>(this.state.get(), proto->sizek);
        for (int i = 0; i < proto->sizek; ++i)
        {
            setnilvalue(&proto->k[i]);
        }
    }

    [Test]
    public void MOVE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_MOVE,
            """
              local a, b, c = 1, 2, 3
              b, c = a, b
              assert(a == 1 and b == 1 and c == 2)
            """);
    }

    [Test]
    public void LOADI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADI,
            """
              local a = 42
              local b = -7
              assert(a == 42 and b == -7 and math.type(a) == "integer")
            """);
    }

    [Test]
    public void LOADF()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADF,
            """
              local a = 3.0
              local b = -4.0
              assert(a == 3.0 and b == -4.0 and math.type(a) == "float")
            """);
    }

    [Test]
    public void LOADK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADK,
            """
              local s = "a non-immediate constant"
              assert(s == "a non-immediate constant")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADK,
            """
              local n = 1000000
              assert(n == 1000000)
            """);
    }

    [Test]
    public void LOADKX()
    {
        this.ExpectPatchedLoadKxString("loaded through OP_LOADKX");
        this.ExpectPatchedLoadKxInteger(123456789);
    }

    [Test]
    public void LOADFALSE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADFALSE,
            """
              local f = false
              assert(f == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADFALSE,
            """
              local f = false
              assert((not not f) == false)
            """);
    }

    [Test]
    public void LFALSESKIP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LFALSESKIP,
            """
              local function is_one(x)
                local b = (x == 1)
                return b
              end
              assert(is_one(1) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LFALSESKIP,
            """
              local function is_one(x)
                local b = (x == 1)
                return b
              end
              assert(is_one(2) == false)
            """);
    }

    [Test]
    public void LOADTRUE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADTRUE,
            """
              local t = true
              assert(t == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADTRUE,
            """
              local t = true
              assert((not not t) == true)
            """);
    }

    [Test]
    public void LOADNIL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADNIL,
            """
              local a, b, c
              assert(a == nil and b == nil and c == nil)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LOADNIL,
            """
              local a, b, c = 1, 2, 3
              a, b, c = nil, nil, nil
              assert(a == nil and b == nil and c == nil)
            """);
    }

    [Test]
    public void GETUPVAL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETUPVAL,
            """
              local x = 41
              local function f()
                return x + 1
              end
              assert(f() == 42)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETUPVAL,
            """
              local x = 41
              local function f()
                return x + 1
              end
              x = 99
              assert(f() == 100)
            """);
    }

    [Test]
    public void SETUPVAL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETUPVAL,
            """
              local x = 0
              local function setx(v)
                x = v
              end
              setx(12)
              assert(x == 12)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETUPVAL,
            """
              local x = 0
              local function setx(v)
                x = v
              end
              setx(nil)
              assert(x == nil)
            """);
    }

    [Test]
    public void GETTABUP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETTABUP,
            """
              assert(type(assert) == "function")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETTABUP,
            """
              assert(math.type(1) == "integer")
            """);
    }

    [Test]
    public void GETTABLE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETTABLE,
            """
              local key = "dynamic"
              local t = {[key] = 11}
              assert(t[key] == 11)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETTABLE,
            """
              local key = "dynamic"
              local proxy = setmetatable({}, {__index = function(_, k)
                return k == key and 22 or nil
              end})
              assert(proxy[key] == 22)
            """);
    }

    [Test]
    public void GETI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETI,
            """
              local t = {"a", "b", "c"}
              assert(t[1] == "a")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETI,
            """
              local t = {"a", "b", "c"}
              assert(t[3] == "c")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETI,
            """
              local proxy = setmetatable({}, {__index = function(_, k)
                return k * 10
              end})
              assert(proxy[2] == 20)
            """);
    }

    [Test]
    public void GETFIELD()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETFIELD,
            """
              local t = {field = 17}
              assert(t.field == 17)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETFIELD,
            """
              local function f(t)
                return t.field
              end
              local proxy = setmetatable({}, {__index = {field = 23}})
              assert(f(proxy) == 23)
            """);
    }

    [Test]
    public void SETTABUP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETTABUP,
            """
              __opcode_settabup_value = 41
              assert(__opcode_settabup_value == 41)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETTABUP,
            """
              __opcode_settabup_value = nil
              assert(__opcode_settabup_value == nil)
            """);
    }

    [Test]
    public void SETTABLE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETTABLE,
            """
              local key = "dynamic"
              local t = {}
              t[key] = 31
              assert(t[key] == 31)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETTABLE,
            """
              local key = "dynamic"
              local writes = {}
              local proxy = setmetatable({}, {__newindex = function(_, k, v)
                writes[k] = v
              end})
              proxy[key] = 32
              assert(writes[key] == 32)
            """);
    }

    [Test]
    public void SETI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETI,
            """
              local t = {}
              t[1] = "first"
              t[2] = "second"
              assert(t[1] == "first" and t[2] == "second")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETI,
            """
              local writes = {}
              local proxy = setmetatable({}, {__newindex = function(_, k, v)
                writes[k] = v
              end})
              proxy[3] = "third"
              assert(writes[3] == "third")
            """);
    }

    [Test]
    public void SETFIELD()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETFIELD,
            """
              local t = {}
              t.field = 44
              assert(t.field == 44)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETFIELD,
            """
              local writes = {}
              local proxy = setmetatable({}, {__newindex = function(_, k, v)
                writes[k] = v
              end})
              proxy.field = 45
              assert(writes.field == 45)
            """);
    }

    [Test]
    public void NEWTABLE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_NEWTABLE,
            """
              local empty = {}
              assert(next(empty) == nil)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_NEWTABLE,
            """
              local array = {1, 2, 3}
              assert(#array == 3 and array[2] == 2)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_NEWTABLE,
            """
              local hash = {a = 4, b = 5}
              assert(hash.a + hash.b == 9)
            """);
    }

    [Test]
    public void SELF()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SELF,
            """
              local function call_add(t, x)
                return t:add(x)
              end

              local t = {base = 10}
              function t:add(x)
                return self.base + x
              end

              assert(call_add(t, 5) == 15)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SELF,
            """
              local function call_add(t, x)
                return t:add(x)
              end

              local t = {base = 10}
              function t:add(x)
                return self.base + x
              end

              t.base = -2
              assert(call_add(t, 5) == 3)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SELF,
            """
              local function call_add(t, x)
                return t:add(x)
              end

              local t = {base = 10}
              function t:add(x)
                return self.base + x
              end

              local proxy = setmetatable({base = 20}, {__index = t})
              assert(call_add(proxy, 1) == 21)
            """);
    }

    [Test]
    public void ADDI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADDI,
            """
              local function f(x)
                return x + 1
              end
              assert(f(10) == 11)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADDI,
            """
              local function f(x)
                return x + 1
              end
              assert(f(1.5) == 2.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADDI,
            """
              local mt = {__add = function(a, b)
                return (type(a) == "table" and a.v or a) + b
              end}
              local function f(x)
                return x + 1
              end
              assert(f(setmetatable({v = 4}, mt)) == 5)
            """);
    }

    [Test]
    public void ADDK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADDK,
            """
              local function f(x)
                return x + 0.0
              end
              assert(f(10) == 10.0 and math.type(f(10)) == "float")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADDK,
            """
              local function f(x)
                return x + 0.0
              end
              assert(f(1.5) == 1.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADDK,
            """
              local mt = {__add = function(a, b)
                return (type(a) == "table" and a.v or a) + b
              end}
              local function f(x)
                return x + 0.0
              end
              assert(f(setmetatable({v = 4}, mt)) == 4)
            """);
    }

    [Test]
    public void SUBK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SUBK,
            """
              local function f(x)
                return x - 10000
              end
              assert(f(10005) == 5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SUBK,
            """
              local function f(x)
                return x - 10000
              end
              assert(f(10005.5) == 5.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SUBK,
            """
              local mt = {__sub = function(a, b)
                return (type(a) == "table" and a.v or a) - b
              end}
              local function f(x)
                return x - 10000
              end
              assert(f(setmetatable({v = 10003}, mt)) == 3)
            """);
    }

    [Test]
    public void MULK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_MULK,
            """
              local function f(x)
                return x * -10000
              end
              assert(f(2) == -20000)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MULK,
            """
              local function f(x)
                return x * -10000
              end
              assert(f(2.5) == -25000)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MULK,
            """
              local mt = {__mul = function(a, b)
                return (type(a) == "table" and a.v or a) * b
              end}
              local function f(x)
                return x * -10000
              end
              assert(f(setmetatable({v = 3}, mt)) == -30000)
            """);
    }

    [Test]
    public void MODK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_MODK,
            """
              local function f(x)
                return x % 90.0
              end
              assert(f(91) == 1.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MODK,
            """
              local function f(x)
                return x % 90.0
              end
              assert(f(92.5) == 2.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MODK,
            """
              local mt = {__mod = function(a, b)
                return (type(a) == "table" and a.v or a) % b
              end}
              local function f(x)
                return x % 90.0
              end
              assert(f(setmetatable({v = 95}, mt)) == 5.0)
            """);
    }

    [Test]
    public void POWK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_POWK,
            """
              local function f(x)
                return x ^ 0.5
              end
              assert(f(4) == 2.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_POWK,
            """
              local function f(x)
                return x ^ 0.5
              end
              assert(f(6.25) == 2.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_POWK,
            """
              local mt = {__pow = function(a, b)
                return (type(a) == "table" and a.v or a) ^ b
              end}
              local function f(x)
                return x ^ 0.5
              end
              assert(f(setmetatable({v = 9}, mt)) == 3.0)
            """);
    }

    [Test]
    public void DIVK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_DIVK,
            """
              local function f(x)
                return x / 2.0
              end
              assert(f(9) == 4.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_DIVK,
            """
              local function f(x)
                return x / 2.0
              end
              assert(f(5.0) == 2.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_DIVK,
            """
              local mt = {__div = function(a, b)
                return (type(a) == "table" and a.v or a) / b
              end}
              local function f(x)
                return x / 2.0
              end
              assert(f(setmetatable({v = 8}, mt)) == 4.0)
            """);
    }

    [Test]
    public void IDIVK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_IDIVK,
            """
              local function f(x)
                return x // 10000
              end
              assert(f(25000) == 2)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_IDIVK,
            """
              local function f(x)
                return x // 10000
              end
              assert(f(25000.0) == 2.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_IDIVK,
            """
              local mt = {__idiv = function(a, b)
                return (type(a) == "table" and a.v or a) // b
              end}
              local function f(x)
                return x // 10000
              end
              assert(f(setmetatable({v = 39000}, mt)) == 3)
            """);
    }

    [Test]
    public void BANDK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_BANDK,
            """
              local function f(x)
                return x & 3
              end
              assert(f(7) == 3)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BANDK,
            """
              local function f(x)
                return x & 3
              end
              assert(f(6.0) == 2)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BANDK,
            """
              local mt = {__band = function(a, b)
                return (type(a) == "table" and a.v or a) & b
              end}
              local function f(x)
                return x & 3
              end
              assert(f(setmetatable({v = 5}, mt)) == 1)
            """);
    }

    [Test]
    public void BORK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_BORK,
            """
              local function f(x)
                return x | 8
              end
              assert(f(1) == 9)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BORK,
            """
              local function f(x)
                return x | 8
              end
              assert(f(2.0) == 10)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BORK,
            """
              local mt = {__bor = function(a, b)
                return (type(a) == "table" and a.v or a) | b
              end}
              local function f(x)
                return x | 8
              end
              assert(f(setmetatable({v = 3}, mt)) == 11)
            """);
    }

    [Test]
    public void BXORK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_BXORK,
            """
              local function f(x)
                return x ~ 6
              end
              assert(f(3) == 5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BXORK,
            """
              local function f(x)
                return x ~ 6
              end
              assert(f(3.0) == 5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BXORK,
            """
              local mt = {__bxor = function(a, b)
                return (type(a) == "table" and a.v or a) ~ b
              end}
              local function f(x)
                return x ~ 6
              end
              assert(f(setmetatable({v = 10}, mt)) == 12)
            """);
    }

    [Test]
    public void SHLI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHLI,
            """
              local function f(x)
                return 1 << x
              end
              assert(f(3) == 8)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHLI,
            """
              local function f(x)
                return 1 << x
              end
              assert(f(0.0) == 1)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHLI,
            """
              local mt = {__shl = function(a, b)
                local av = type(a) == "table" and a.v or a
                local bv = type(b) == "table" and b.v or b
                return av << bv
              end}
              local function f(x)
                return 1 << x
              end
              assert(f(setmetatable({v = 4}, mt)) == 16)
            """);
    }

    [Test]
    public void SHRI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHRI,
            """
              local function f(x)
                return x >> 1
              end
              assert(f(8) == 4)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHRI,
            """
              local function f(x)
                return x >> 1
              end
              assert(f(8.0) == 4)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHRI,
            """
              local mt = {__shr = function(a, b)
                return (type(a) == "table" and a.v or a) >> b
              end}
              local function f(x)
                return x >> 1
              end
              assert(f(setmetatable({v = 16}, mt)) == 8)
            """);
    }

    [Test]
    public void ADD()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADD,
            """
              local function f(a, b)
                return a + b
              end
              assert(f(2, 3) == 5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADD,
            """
              local function f(a, b)
                return a + b
              end
              assert(f(1.5, 2.25) == 3.75)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_ADD,
            """
              local mt = {__add = function(a, b) return a.v + b.v end}
              local function f(a, b)
                return a + b
              end
              assert(f(setmetatable({v = 4}, mt), setmetatable({v = 5}, mt)) == 9)
            """);
    }

    [Test]
    public void SUB()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SUB,
            """
              local function f(a, b)
                return a - b
              end
              assert(f(7, 2) == 5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SUB,
            """
              local function f(a, b)
                return a - b
              end
              assert(f(7.5, 2.25) == 5.25)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SUB,
            """
              local mt = {__sub = function(a, b) return a.v - b.v end}
              local function f(a, b)
                return a - b
              end
              assert(f(setmetatable({v = 9}, mt), setmetatable({v = 4}, mt)) == 5)
            """);
    }

    [Test]
    public void MUL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_MUL,
            """
              local function f(a, b)
                return a * b
              end
              assert(f(6, 7) == 42)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MUL,
            """
              local function f(a, b)
                return a * b
              end
              assert(f(1.5, 2.0) == 3.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MUL,
            """
              local mt = {__mul = function(a, b) return a.v * b.v end}
              local function f(a, b)
                return a * b
              end
              assert(f(setmetatable({v = 3}, mt), setmetatable({v = 5}, mt)) == 15)
            """);
    }

    [Test]
    public void MOD()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_MOD,
            """
              local function f(a, b)
                return a % b
              end
              assert(f(17, 5) == 2)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MOD,
            """
              local function f(a, b)
                return a % b
              end
              assert(f(17.5, 5.0) == 2.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MOD,
            """
              local mt = {__mod = function(a, b) return a.v % b.v end}
              local function f(a, b)
                return a % b
              end
              assert(f(setmetatable({v = 20}, mt), setmetatable({v = 6}, mt)) == 2)
            """);
        this.ExpectRuntimeErrorWithOpcode(
            OpCode.OP_MOD,
            """
              local function f(a, b)
                return a % b
              end
              f(1, 0)
            """,
            "'n%0'");
    }

    [Test]
    public void POW()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_POW,
            """
              local function f(a, b)
                return a ^ b
              end
              assert(f(2, 5) == 32.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_POW,
            """
              local function f(a, b)
                return a ^ b
              end
              assert(f(4.0, 0.5) == 2.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_POW,
            """
              local mt = {__pow = function(a, b) return a.v ^ b.v end}
              local function f(a, b)
                return a ^ b
              end
              assert(f(setmetatable({v = 3}, mt), setmetatable({v = 2}, mt)) == 9.0)
            """);
    }

    [Test]
    public void DIV()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_DIV,
            """
              local function f(a, b)
                return a / b
              end
              assert(f(7, 2) == 3.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_DIV,
            """
              local function f(a, b)
                return a / b
              end
              assert(f(7.5, 2.5) == 3.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_DIV,
            """
              local mt = {__div = function(a, b) return a.v / b.v end}
              local function f(a, b)
                return a / b
              end
              assert(f(setmetatable({v = 9}, mt), setmetatable({v = 2}, mt)) == 4.5)
            """);
    }

    [Test]
    public void IDIV()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_IDIV,
            """
              local function f(a, b)
                return a // b
              end
              assert(f(7, 2) == 3)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_IDIV,
            """
              local function f(a, b)
                return a // b
              end
              assert(f(7.5, 2.0) == 3.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_IDIV,
            """
              local mt = {__idiv = function(a, b) return a.v // b.v end}
              local function f(a, b)
                return a // b
              end
              assert(f(setmetatable({v = 9}, mt), setmetatable({v = 2}, mt)) == 4)
            """);
        this.ExpectRuntimeErrorWithOpcode(
            OpCode.OP_IDIV,
            """
              local function f(a, b)
                return a // b
              end
              f(1, 0)
            """,
            "divide by zero");
    }

    [Test]
    public void BAND()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_BAND,
            """
              local function f(a, b)
                return a & b
              end
              assert(f(6, 3) == 2)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BAND,
            """
              local function f(a, b)
                return a & b
              end
              assert(f(6.0, 3.0) == 2)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BAND,
            """
              local mt = {__band = function(a, b) return a.v & b.v end}
              local function f(a, b)
                return a & b
              end
              assert(f(setmetatable({v = 12}, mt), setmetatable({v = 10}, mt)) == 8)
            """);
    }

    [Test]
    public void BOR()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_BOR,
            """
              local function f(a, b)
                return a | b
              end
              assert(f(4, 1) == 5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BOR,
            """
              local function f(a, b)
                return a | b
              end
              assert(f(4.0, 2.0) == 6)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BOR,
            """
              local mt = {__bor = function(a, b) return a.v | b.v end}
              local function f(a, b)
                return a | b
              end
              assert(f(setmetatable({v = 8}, mt), setmetatable({v = 3}, mt)) == 11)
            """);
    }

    [Test]
    public void BXOR()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_BXOR,
            """
              local function f(a, b)
                return a ~ b
              end
              assert(f(12, 10) == 6)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BXOR,
            """
              local function f(a, b)
                return a ~ b
              end
              assert(f(12.0, 10.0) == 6)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BXOR,
            """
              local mt = {__bxor = function(a, b) return a.v ~ b.v end}
              local function f(a, b)
                return a ~ b
              end
              assert(f(setmetatable({v = 15}, mt), setmetatable({v = 5}, mt)) == 10)
            """);
    }

    [Test]
    public void SHL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHL,
            """
              local function f(a, b)
                return a << b
              end
              assert(f(3, 2) == 12)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHL,
            """
              local function f(a, b)
                return a << b
              end
              assert(f(3.0, 1.0) == 6)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHL,
            """
              local mt = {__shl = function(a, b) return a.v << b.v end}
              local function f(a, b)
                return a << b
              end
              assert(f(setmetatable({v = 2}, mt), setmetatable({v = 4}, mt)) == 32)
            """);
    }

    [Test]
    public void SHR()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHR,
            """
              local function f(a, b)
                return a >> b
              end
              assert(f(16, 2) == 4)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHR,
            """
              local function f(a, b)
                return a >> b
              end
              assert(f(16.0, 1.0) == 8)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SHR,
            """
              local mt = {__shr = function(a, b) return a.v >> b.v end}
              local function f(a, b)
                return a >> b
              end
              assert(f(setmetatable({v = 32}, mt), setmetatable({v = 3}, mt)) == 4)
            """);
    }

    [Test]
    public void MMBIN()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_MMBIN,
            """
              local mt = {__add = function(a, b) return a.v + b.v end}
              local function f(a, b)
                return a + b
              end
              assert(f(setmetatable({v = 7}, mt), setmetatable({v = 8}, mt)) == 15)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MMBIN,
            """
              local mt = {__add = function(a, b) return a.v + b.v end}
              local function f(a, b)
                return a + b
              end
              assert(f(setmetatable({v = -1}, mt), setmetatable({v = 3}, mt)) == 2)
            """);
    }

    [Test]
    public void MMBINI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_MMBINI,
            """
              local mt = {__add = function(a, b)
                return (type(a) == "table" and a.v or a) + (type(b) == "table" and b.v or b)
              end}
              local function f(x)
                return x + 5
              end
              assert(f(setmetatable({v = 7}, mt)) == 12)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MMBINI,
            """
              local mt = {__add = function(a, b)
                return (type(a) == "table" and a.v or a) + (type(b) == "table" and b.v or b)
              end}
              local function g(x)
                return 5 + x
              end
              assert(g(setmetatable({v = 8}, mt)) == 13)
            """);
    }

    [Test]
    public void MMBINK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_MMBINK,
            """
              local mt = {__add = function(a, b)
                return (type(a) == "table" and a.v or a) + (type(b) == "table" and b.v or b)
              end}
              local function f(x)
                return x + 0.0
              end
              assert(f(setmetatable({v = 12}, mt)) == 12.0)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_MMBINK,
            """
              local mt = {__add = function(a, b)
                return (type(a) == "table" and a.v or a) + (type(b) == "table" and b.v or b)
              end}
              local function f(x)
                return x + 0.0
              end
              assert(f(setmetatable({v = -2}, mt)) == -2.0)
            """);
    }

    [Test]
    public void UNM()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_UNM,
            """
              local function f(x)
                return -x
              end
              assert(f(12) == -12)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_UNM,
            """
              local function f(x)
                return -x
              end
              assert(f(1.5) == -1.5)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_UNM,
            """
              local mt = {__unm = function(a) return -a.v end}
              local function f(x)
                return -x
              end
              assert(f(setmetatable({v = 6}, mt)) == -6)
            """);
    }

    [Test]
    public void BNOT()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_BNOT,
            """
              local function f(x)
                return ~x
              end
              assert(f(0) == -1)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BNOT,
            """
              local function f(x)
                return ~x
              end
              assert(f(3.0) == -4)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_BNOT,
            """
              local mt = {__bnot = function(a) return ~a.v end}
              local function f(x)
                return ~x
              end
              assert(f(setmetatable({v = 5}, mt)) == ~5)
            """);
    }

    [Test]
    public void NOT()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_NOT,
            """
              local function f(x)
                return not x
              end
              assert(f(nil) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_NOT,
            """
              local function f(x)
                return not x
              end
              assert(f(false) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_NOT,
            """
              local function f(x)
                return not x
              end
              assert(f(0) == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_NOT,
            """
              local function f(x)
                return not x
              end
              assert(f({}) == false)
            """);
    }

    [Test]
    public void LEN()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LEN,
            """
              local function f(x)
                return #x
              end
              assert(f("abc") == 3)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LEN,
            """
              local function f(x)
                return #x
              end
              assert(f({1, 2, 3, 4}) == 4)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LEN,
            """
              local mt = {__len = function(t) return t.n end}
              local function f(x)
                return #x
              end
              assert(f(setmetatable({n = 99}, mt)) == 99)
            """);
    }

    [Test]
    public void CONCAT()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_CONCAT,
            """
              local function f(a, b, c)
                return a .. b .. c
              end
              assert(f("a", "b", "c") == "abc")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_CONCAT,
            """
              local function f(a, b, c)
                return a .. b .. c
              end
              assert(f("n", 1, 2) == "n12")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_CONCAT,
            """
              local mt = {__concat = function(a, b)
                local av = type(a) == "table" and a.v or a
                local bv = type(b) == "table" and b.v or b
                return av .. bv
              end}
              local function f(a, b, c)
                return a .. b .. c
              end
              assert(f(setmetatable({v = "x"}, mt), "y", "z") == "xyz")
            """);
    }

    [Test]
    public void CLOSE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_CLOSE,
            """
              local closed = 0
              local mt = {__close = function() closed = closed + 1 end}
              do
                local x <close> = setmetatable({}, mt)
                assert(closed == 0)
              end
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_CLOSE,
            """
              local closed = 0
              local mt = {__close = function() closed = closed + 1 end}
              do
                local x <close> = setmetatable({}, mt)
              end
              assert(closed == 1)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_CLOSE,
            """
              local closed = 0
              local mt = {__close = function() closed = closed + 1 end}
              do
                local x <close> = setmetatable({}, mt)
              end
              local function f()
                local y <close> = setmetatable({}, mt)
                return 7
              end
              assert(f() == 7 and closed == 2)
            """);
    }

    [Test]
    public void TBC()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_TBC,
            """
              local closed = {}
              local mt = {__close = function(x) closed[#closed + 1] = x.name end}
              do
                local a <close> = setmetatable({name = "a"}, mt)
                local b <close> = setmetatable({name = "b"}, mt)
              end
              assert(closed[1] == "b" and closed[2] == "a")
            """);
    }

    [Test]
    public void JMP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_JMP,
            """
              local n = 0
              while n < 3 do
                n = n + 1
              end
              goto done
              n = 99
              ::done::
              assert(n == 3)
            """);
    }

    [Test]
    public void EQ()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQ,
            """
              local function f(a, b)
                return a == b
              end
              assert(f(4, 4) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQ,
            """
              local function f(a, b)
                return a == b
              end
              assert(f(4, 5) == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQ,
            """
              local mt = {__eq = function(a, b) return a.v == b.v end}
              local function f(a, b)
                return a == b
              end
              assert(f(setmetatable({v = 9}, mt), setmetatable({v = 9}, mt)) == true)
            """);
    }

    [Test]
    public void LT()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LT,
            """
              local function f(a, b)
                return a < b
              end
              assert(f(1, 2) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LT,
            """
              local function f(a, b)
                return a < b
              end
              assert(f("b", "a") == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LT,
            """
              local mt = {__lt = function(a, b) return a.v < b.v end}
              local function f(a, b)
                return a < b
              end
              assert(f(setmetatable({v = 1}, mt), setmetatable({v = 3}, mt)) == true)
            """);
    }

    [Test]
    public void LE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LE,
            """
              local function f(a, b)
                return a <= b
              end
              assert(f(2, 2) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LE,
            """
              local function f(a, b)
                return a <= b
              end
              assert(f("b", "a") == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LE,
            """
              local mt = {__le = function(a, b) return a.v <= b.v end}
              local function f(a, b)
                return a <= b
              end
              assert(f(setmetatable({v = 3}, mt), setmetatable({v = 3}, mt)) == true)
            """);
    }

    [Test]
    public void EQK()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQK,
            """
              local function f(x)
                return x == "target"
              end
              assert(f("target") == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQK,
            """
              local function f(x)
                return x == "target"
              end
              assert(f("other") == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQK,
            """
              local function f(x)
                return x == "target"
              end
              assert(f(1) == false)
            """);
    }

    [Test]
    public void EQI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQI,
            """
              local function f(x)
                return x == 5
              end
              assert(f(5) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQI,
            """
              local function f(x)
                return x == 5
              end
              assert(f(5.0) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQI,
            """
              local function f(x)
                return x == 5
              end
              assert(f(6) == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_EQI,
            """
              local function f(x)
                return x == 5
              end
              assert(f({}) == false)
            """);
    }

    [Test]
    public void LTI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LTI,
            """
              local function f(x)
                return x < 5
              end
              assert(f(4) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LTI,
            """
              local function f(x)
                return x < 5
              end
              assert(f(5.0) == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LTI,
            """
              local mt = {__lt = function(a, b) return a.v < b end}
              local function f(x)
                return x < 5
              end
              assert(f(setmetatable({v = 3}, mt)) == true)
            """);
    }

    [Test]
    public void LEI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_LEI,
            """
              local function f(x)
                return x <= 5
              end
              assert(f(5) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LEI,
            """
              local function f(x)
                return x <= 5
              end
              assert(f(6.0) == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_LEI,
            """
              local mt = {__le = function(a, b) return a.v <= b end}
              local function f(x)
                return x <= 5
              end
              assert(f(setmetatable({v = 5}, mt)) == true)
            """);
    }

    [Test]
    public void GTI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_GTI,
            """
              local function f(x)
                return 5 < x
              end
              assert(f(6) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GTI,
            """
              local function f(x)
                return 5 < x
              end
              assert(f(5.0) == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GTI,
            """
              local mt = {__lt = function(a, b) return a < b.v end}
              local function f(x)
                return 5 < x
              end
              assert(f(setmetatable({v = 7}, mt)) == true)
            """);
    }

    [Test]
    public void GEI()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_GEI,
            """
              local function f(x)
                return 5 <= x
              end
              assert(f(5) == true)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GEI,
            """
              local function f(x)
                return 5 <= x
              end
              assert(f(4.0) == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GEI,
            """
              local mt = {__le = function(a, b) return a <= b.v end}
              local function f(x)
                return 5 <= x
              end
              assert(f(setmetatable({v = 6}, mt)) == true)
            """);
    }

    [Test]
    public void TEST()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_TEST,
            """
              local function f(x)
                if x then
                  return "truthy"
                else
                  return "falsey"
                end
              end
              assert(f(nil) == "falsey")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_TEST,
            """
              local function f(x)
                if x then
                  return "truthy"
                else
                  return "falsey"
                end
              end
              assert(f(false) == "falsey")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_TEST,
            """
              local function f(x)
                if x then
                  return "truthy"
                else
                  return "falsey"
                end
              end
              assert(f(0) == "truthy")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_TEST,
            """
              local function f(x)
                if x then
                  return "truthy"
                else
                  return "falsey"
                end
              end
              assert(f({}) == "truthy")
            """);
    }

    [Test]
    public void TESTSET()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_TESTSET,
            """
              local function first_truthy(a, b)
                local c = a or b
                return c
              end
              assert(first_truthy(false, "fallback") == "fallback")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_TESTSET,
            """
              local function first_truthy(a, b)
                local c = a or b
                return c
              end
              assert(first_truthy("primary", "fallback") == "primary")
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_TESTSET,
            """
              local function first_falsey(a, b)
                local c = a and b
                return c
              end
              assert(first_falsey(false, "value") == false)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_TESTSET,
            """
              local function first_falsey(a, b)
                local c = a and b
                return c
              end
              assert(first_falsey(true, "value") == "value")
            """);
    }

    [Test]
    public void CALL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_CALL,
            """
              local function add(a, b)
                return a + b
              end
              assert(add(20, 22) == 42)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_CALL,
            """
              local function no_results()
              end
              assert(no_results() == nil)
            """);
    }

    [Test]
    public void TAILCALL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_TAILCALL,
            """
              local function id(...)
                return ...
              end
              local function f(x)
                return id(x)
              end
              assert(f(42) == 42)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_TAILCALL,
            """
              local function id(...)
                return ...
              end
              local function g(a, b)
                return id(a, b)
              end
              local a, b = g("a", "b")
              assert(a == "a" and b == "b")
            """);
    }

    [Test]
    public void RETURN()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_RETURN,
            """
              local function f(a, b)
                return a, b
              end
              local x, y = f(1, 2)
              assert(x == 1 and y == 2)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_RETURN,
            """
              local closed = 0
              local mt = {__close = function() closed = closed + 1 end}
              local function g()
                local c <close> = setmetatable({}, mt)
                return "done"
              end
              assert(g() == "done" and closed == 1)
            """);
    }

    [Test]
    public void RETURN0()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_RETURN0,
            """
              local function f()
                local x = 1
              end
              assert(f() == nil)
            """);
    }

    [Test]
    public void RETURN1()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_RETURN1,
            """
              local function f(x)
                return x
              end
              assert(f(12) == 12)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_RETURN1,
            """
              local function f(x)
                return x
              end
              assert(f(nil) == nil)
            """);
    }

    [Test]
    public void FORLOOP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_FORLOOP,
            """
              local sum = 0
              for i = 1, 5 do
                sum = sum + i
              end
              assert(sum == 15)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_FORLOOP,
            """
              local fsum = 0
              for i = 1.0, 2.0, 0.5 do
                fsum = fsum + i
              end
              assert(fsum == 4.5)
            """);
    }

    [Test]
    public void FORPREP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_FORPREP,
            """
              local count = 0
              for i = 3, 1, -1 do
                count = count + i
              end
              assert(count == 6)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_FORPREP,
            """
              local count = 0
              for i = 1, 0 do
                count = 100
              end
              assert(count == 0)
            """);
    }

    [Test]
    public void TFORPREP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_TFORPREP,
            """
              local t = {a = 1, b = 2}
              local sum = 0
              for _, v in pairs(t) do
                sum = sum + v
              end
              assert(sum == 3)
            """);
    }

    [Test]
    public void TFORCALL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_TFORCALL,
            """
              local state = {10, 20, 30}
              local sum = 0
              for _, v in ipairs(state) do
                sum = sum + v
              end
              assert(sum == 60)
            """);
    }

    [Test]
    public void TFORLOOP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_TFORLOOP,
            """
              local seen = {}
              for k, v in pairs({x = 1, y = 2}) do
                seen[k] = v
              end
              assert(seen.x == 1 and seen.y == 2)
            """);
    }

    [Test]
    public void SETLIST()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETLIST,
            """
              local t = {1, 2, 3, 4}
              assert(#t == 4 and t[1] == 1 and t[4] == 4)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_SETLIST,
            """
              local function pack(...)
                return {...}
              end
              local p = pack("a", "b", "c")
              assert(#p == 3 and p[2] == "b")
            """);
    }

    [Test]
    public void CLOSURE()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_CLOSURE,
            """
              local function plain()
                return 1
              end
              assert(plain() == 1)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_CLOSURE,
            """
              local x = 41
              local function captures()
                return x + 1
              end
              assert(captures() == 42)
            """);
    }

    [Test]
    public void VARARG()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_VARARG,
            """
              local function f(...)
                return ...
              end
              local a, b, c = f(1, "two", nil)
              assert(a == 1 and b == "two" and c == nil)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_VARARG,
            """
              local function first(...)
                local x = ...
                return x
              end
              assert(first(9, 8) == 9)
            """);
    }

    [Test]
    public void GETVARG()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETVARG,
            """
              local function f(... va)
                return va[1], va[2], va.n
              end
              local a, b, n = f("x", "y")
              assert(a == "x" and b == "y" and n == 2)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_GETVARG,
            """
              local function f(... va)
                return va[1], va[2], va.n
              end
              local c, d, n2 = f()
              assert(c == nil and d == nil and n2 == 0)
            """);
    }

    [Test]
    public void ERRNNIL()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_ERRNNIL,
            """
              local assert = assert
              global __opcode_errnnil_success = 123
              assert(__opcode_errnnil_success == 123)
            """);
        lua_getglobal(this.L(), "__opcode_errnnil_success");
        Assert.That(lua_tointeger(this.L(), -1), Is.EqualTo(123));
        lua_settop(this.L(), 0);

        this.ExpectRuntimeErrorWithOpcode(
            OpCode.OP_ERRNNIL,
            "global print = 1",
            "global 'print'");
    }

    [Test]
    public void VARARGPREP()
    {
        this.ExpectRunsWithOpcode(
            OpCode.OP_VARARGPREP,
            """
              local function count(a, ... va)
                return a, va.n
              end
              local a, n = count("fixed", 1, 2, 3)
              assert(a == "fixed" and n == 3)
            """);
        this.ExpectRunsWithOpcode(
            OpCode.OP_VARARGPREP,
            """
              local function count(a, ... va)
                return a, va.n
              end
              local b, n2 = count("only")
              assert(b == "only" and n2 == 0)
            """);
    }

    [Test]
    public void EXTRAARG()
    {
        this.ExpectRunsWithOpcode(OpCode.OP_EXTRAARG, this.RepeatedListConstructor(MAXARG_vC + 2));
        this.ExpectPatchedLoadKxString("extra argument for LOADKX");
    }
}
