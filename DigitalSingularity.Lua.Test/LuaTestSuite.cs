namespace DigitalSingularity.Lua.Test;

using DigitalSingularity.Lua.Cli;
using static Lua;

public unsafe class LuaTestSuite
{
    private static int pmain(lua_State* L)
    {
        string test = lua_tostring(L, 1);
        luaL_checkversion(L, LUA_VERSION_NUM, LUAL_NUMSIZES);
        luai_openlibs(L);

        lua_gc(L, LUA_GCRESTART); /* start GC... */
        lua_gc(L, LUA_GCGEN); /* ...in generational mode */

        int status = luaL_loadfile(L, test);
        if (status == LUA_OK)
        {
            status = Program.docall(L, 0, LUA_MULTRET);
        }

        if (status != LUA_OK)
        {
            return 0; /* interrupt in case of error */
        }

        lua_pushboolean(L, true); /* signal no errors */
        return 1;
    }

    private static void DumpStack(lua_State* L)
    {
        int stackSize = lua_gettop(L);
        
        for (int i = 1; i <= stackSize; i++)
        {
            int type = lua_type(L, i);
            Console.WriteLine($"Stack[{i}] = {type} {lua_typename(L, type)}");
        }
    }

    [Datapoints] public string[] values =
    [
        "lua/all.lua",
        "lua/api.lua",
        "lua/attrib.lua",
        "lua/big.lua",
        "lua/bitwise.lua",
        "lua/calls.lua",
        "lua/closure.lua",
        "lua/code.lua",
        "lua/constructs.lua",
        "lua/coroutine.lua",
        "lua/cstack.lua",
        "lua/db.lua",
        "lua/errors.lua",
        "lua/events.lua",
        "lua/files.lua",
        "lua/gc.lua",
        "lua/gengc.lua",
        "lua/goto.lua",
        "lua/heavy.lua",
        "lua/literals.lua",
        "lua/locals.lua",
        "lua/main.lua",
        "lua/math.lua",
        "lua/memerr.lua",
        "lua/nextvar.lua",
        "lua/pm.lua",
        "lua/sort.lua",
        "lua/strings.lua",
        "lua/tpack.lua",
        "lua/tracegc.lua",
        "lua/utf8.lua",
        "lua/vararg.lua",
        "lua/verybig.lua",
        
        "bench/array3d.lua",
        "bench/binary-trees.lua",
        "bench/chameneos.lua",
        "bench/coroutine-ring.lua",
        "bench/euler14-bit.lua",
        "bench/fannkuch.lua",
        "bench/fasta.lua",
        "bench/k-nucleotide.lua",
        "bench/life.lua",
        "bench/mandelbrot.lua",
        "bench/mandelbrot-bit.lua",
        "bench/md5.lua",
        "bench/meteor.lua",
        "bench/nbody.lua",
        "bench/nsieve.lua",
        "bench/nsieve-bit.lua",
        "bench/nsieve-bit-fp.lua",
        "bench/partialsums.lua",
        "bench/pidigits-nogmp.lua",
        "bench/ray.lua",
        "bench/recursive-ack.lua",
        "bench/recursive-fib.lua",
        "bench/revcomp.lua",
        "bench/scimark-2010-12-20.lua",
        "bench/scimark-fft.lua",
        "bench/scimark-lu.lua",
        "bench/scimark-sor.lua",
        "bench/scimark-sparse.lua",
        "bench/series.lua",
        "bench/spectral-norm.lua",
        "bench/sum-file.lua",
        
        // TODO luajit tests
    ];

    [Theory]
    public void Test(string test)
    {
        lua_State* L = luaL_newstate(); /* create state */
        Assert.That(L, Is.Not.Null);
        
        string testFile = Path.Join(Path.GetFullPath("../../../"), test);
        Console.WriteLine($"Runnint test: {testFile}");
        
        lua_gc(L, LUA_GCSTOP); /* stop GC while building state */
        lua_pushcfunction(L, &pmain); /* to call 'pmain' in protected mode */
        lua_pushstring(L, testFile);
        int status = lua_pcall(L, 1, 1, 0) /* do the call */;
        bool result = lua_toboolean(L, -1) /* get result */;
        if (result && status == LUA_OK)
        {
            
        }
        else
        {
            throw new NotImplementedException();
        }
        
        lua_close(L);
    }
}
