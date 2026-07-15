namespace DigitalSingularity.Lua.Test;

using System.Runtime.ExceptionServices;
using DigitalSingularity.Lua.Cli;
using static Lua;

public unsafe class LuaTestSuite
{
    private static int pmain(lua_State* L)
    {
        string? test = lua_tonetstring(L, 1);
        luaL_checkversion(L, LUA_VERSION_NUM, LUAL_NUMSIZES);
        luai_openlibs(L);

        lua_createtable(L, 0, 1);
        string programLocation = typeof(Program).Assembly.Location;
        if (programLocation.EndsWith(".dll"))
        {
            programLocation = programLocation[..^4] + ".exe";
        }

        if (File.Exists(programLocation))
        {
            lua_pushstring(L, programLocation);
        }
        else
        {
            lua_pushstring(L, "<UNIT TEST>");
        }
        
        lua_rawseti(L, -2, 0);
        lua_setglobal(L, "arg"); 
        
        lua_gc(L, LUA_GCRESTART); // start GC...
        lua_gc(L, LUA_GCGEN); // ...in generational mode
        
        int status = luaL_loadfile(L, test);
        if (status == LUA_OK)
        {
            status = Program.docall(L, 0, LUA_MULTRET);
        }

        if (status != LUA_OK)
        {
            string msg = lua_tonetstring(L, -1) ?? "(error message not a string)";
            Assert.Fail(msg);
            lua_pop(L, 1); // remove message
        }

        lua_pushboolean(L, true); // signal no errors
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
    
    [Test]
    public void Bench_Array3d()
    {
        RunFile("bench/array3d.lua");
    }
    
    [Test]
    public void Bench_BinaryTrees()
    {
        RunFile("bench/binary-trees.lua");
    }
    
    [Test]
    public void Bench_Chameneos()
    {
        RunFile("bench/chameneos.lua");
    }
    
    [Test]
    public void Bench_CoroutineRing()
    {
        RunFile("bench/coroutine-ring.lua");
    }
    
    [Test]
    public void Bench_Euler14Bit()
    {
        RunFile("bench/euler14-bit.lua");
    }
    
    [Test]
    public void Bench_Fannkuch()
    {
        RunFile("bench/fannkuch.lua");
    }
    
    [Test]
    public void Bench_Fasta()
    {
        RunFile("bench/fasta.lua");
    }
    
    [Test]
    public void Bench_KNucleotide()
    {
        RunFile("bench/k-nucleotide.lua");
    }
    
    [Test]
    public void Bench_Life()
    {
        RunFile("bench/life.lua");
    }
    
    [Test]
    public void Bench_Mandelbrot()
    {
        RunFile("bench/mandelbrot.lua");
    }
    
    [Test]
    public void Bench_MandelbrotBit()
    {
        RunFile("bench/mandelbrot-bit.lua");
    }
    
    [Test]
    public void Bench_Md5()
    {
        RunFile("bench/md5.lua");
    }
    
    [Test]
    public void Bench_Meteor()
    {
        RunFile("bench/meteor.lua");
    }
    
    [Test]
    public void Bench_Nbody()
    {
        RunFile("bench/nbody.lua");
    }
    
    [Test]
    public void Bench_Nsieve()
    {
        RunFile("bench/nsieve.lua");
    }
    
    [Test]
    public void Bench_NsieveBit()
    {
        RunFile("bench/nsieve-bit.lua");
    }
    
    [Test]
    public void Bench_NsieveBitFp()
    {
        RunFile("bench/nsieve-bit-fp.lua");
    }
    
    [Test]
    public void Bench_Partialsums()
    {
        RunFile("bench/partialsums.lua");
    }
    
    [Test]
    public void Bench_PidigitsNogmp()
    {
        RunFile("bench/pidigits-nogmp.lua");
    }
    
    [Test]
    public void Bench_Ray()
    {
        RunFile("bench/ray.lua");
    }
    
    [Test]
    public void Bench_RecursiveAck()
    {
        RunFile("bench/recursive-ack.lua");
    }
    
    [Test]
    public void Bench_RecursiveFib()
    {
        RunFile("bench/recursive-fib.lua");
    }
    
    [Test]
    public void Bench_Revcomp()
    {
        RunFile("bench/revcomp.lua");
    }
    
    [Test]
    public void Bench_Scimark20101220()
    {
        RunFile("bench/scimark-2010-12-20.lua");
    }
    
    [Test]
    public void Bench_ScimarkFft()
    {
        RunFile("bench/scimark-fft.lua");
    }
    
    [Test]
    public void Bench_ScimarkLu()
    {
        RunFile("bench/scimark-lu.lua");
    }
    
    [Test]
    public void Bench_ScimarkSor()
    {
        RunFile("bench/scimark-sor.lua");
    }
    
    [Test]
    public void Bench_ScimarkSparse()
    {
        RunFile("bench/scimark-sparse.lua");
    }
    
    [Test]
    public void Bench_Series()
    {
        RunFile("bench/series.lua");
    }
    
    [Test]
    public void Bench_SpectralNorm()
    {
        RunFile("bench/spectral-norm.lua");
    }
    
    [Test]
    public void Bench_SumFile()
    {
        RunFile("bench/sum-file.lua");
    }
    
    [Test]
    public void Lua_All()
    {
        RunFile("lua/all.lua");
    }
    
    [Test]
    public void Lua_Api()
    {
        RunFile("lua/api.lua");
    }
    
    [Test]
    public void Lua_Attrib()
    {
        RunFile("lua/attrib.lua");
    }
    
    [Test]
    public void Lua_Big()
    {
        RunFile("lua/big_runner.lua");
    }
    
    [Test]
    public void Lua_Bitwise()
    {
        RunFile("lua/bitwise.lua");
    }
    
    [Test]
    public void Lua_Calls()
    {
        RunFile("lua/calls.lua");
    }
    
    [Test]
    public void Lua_Closure()
    {
        RunFile("lua/closure.lua");
    }
    
    [Test]
    public void Lua_Code()
    {
        RunFile("lua/code.lua");
    }
    
    [Test]
    public void Lua_Constructs()
    {
        RunFile("lua/constructs.lua");
    }
    
    [Test]
    public void Lua_Coroutine()
    {
        RunFile("lua/coroutine.lua");
    }
    
    [Test]
    public void Lua_Cstack()
    {
        RunFile("lua/cstack.lua");
    }
    
    [Test]
    public void Lua_Db()
    {
        RunFile("lua/db.lua");
    }
    
    [Test]
    public void Lua_Errors()
    {
        RunFile("lua/errors.lua");
    }
    
    [Test]
    public void Lua_Events()
    {
        RunFile("lua/events.lua");
    }
    
    [Test]
    public void Lua_Files()
    {
        RunFile("lua/files.lua");
    }
    
    [Test]
    public void Lua_Gc()
    {
        RunFile("lua/gc.lua");
    }
    
    [Test]
    public void Lua_Gengc()
    {
        RunFile("lua/gengc.lua");
    }
    
    [Test]
    public void Lua_Goto()
    {
        RunFile("lua/goto.lua");
    }
    
    [Test]
    public void Lua_Heavy()
    {
        RunFile("lua/heavy.lua");
    }
    
    [Test]
    public void Lua_Literals()
    {
        RunFile("lua/literals.lua");
    }
    
    [Test]
    public void Lua_Locals()
    {
        RunFile("lua/locals.lua");
    }
    
    [Test]
    public void Lua_Main()
    {
        RunFile("lua/main.lua");
    }
    
    [Test]
    public void Lua_Math()
    {
        RunFile("lua/math.lua");
    }
    
    [Test]
    public void Lua_Memerr()
    {
        RunFile("lua/memerr.lua");
    }
    
    [Test]
    public void Lua_Nextvar()
    {
        RunFile("lua/nextvar.lua");
    }
    
    [Test]
    public void Lua_Pm()
    {
        RunFile("lua/pm.lua");
    }
    
    [Test]
    public void Lua_Sort()
    {
        RunFile("lua/sort.lua");
    }
    
    [Test]
    public void Lua_Strings()
    {
        RunFile("lua/strings.lua");
    }
    
    [Test]
    public void Lua_Tpack()
    {
        RunFile("lua/tpack.lua");
    }
    
    [Test]
    public void Lua_Utf8()
    {
        RunFile("lua/utf8.lua");
    }
    
    [Test]
    public void Lua_Vararg()
    {
        RunFile("lua/vararg.lua");
    }
    
    [Test]
    public void Lua_Verybig()
    {
        RunFile("lua/verybig.lua");
    }
    
    // TODO luajit tests

    private static void RunFile(string test)
    {
        FileInfo file = new(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "../../..", test));
        Environment.CurrentDirectory = file.DirectoryName ?? throw new InvalidOperationException();
        Console.WriteLine($"Running test: {Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, file.FullName)}");
        
        lua_State* L = luaL_newstate(); // create state
        Assert.That(L, Is.Not.Null);
        
        lua_gc(L, LUA_GCSTOP); // stop GC while building state
        lua_pushcfunction(L, &pmain); // to call 'pmain' in protected mode
        lua_pushstring(L, file.Name);

        ExceptionDispatchInfo? capturedException = null;
        
        Thread thread = new(
            () =>
            {
                try
                {
                    int status = lua_pcall(L, 1, 1, 0) ; // do the call
                    if (status != LUA_OK)
                    {
                        string msg = lua_tonetstring(L, -1) ?? "(error message not a string)";
                        Assert.Fail($"STATUS: {status}; MESSAGE: {msg}");
                        lua_pop(L, 1);
                    }
                }
                catch (Exception ex)
                {
                    capturedException = ExceptionDispatchInfo.Capture(ex);
                }
            }, 1024 * 1024 * 8);
        thread.Start();
        thread.Join();
        
        lua_close(L);
        
        capturedException?.Throw();
    }

    [Test]
    public void __Test2()
    {
        RunFile("test.lua");
    }
}
