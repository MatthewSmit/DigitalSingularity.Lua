namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using lua_State = Lua.lua_State;

using unsafe lua_Alloc = delegate* unmanaged[Cdecl]<void*, void*, long, long, void*>;
using unsafe lua_Hook = delegate* unmanaged[Cdecl]<DigitalSingularity.Lua.Lua.lua_State*, NativeLua.lua_Debug*, void>;
using unsafe lua_WarnFunction = delegate* unmanaged[Cdecl]<void*, byte*, int, void>;
using unsafe lua_KFunction = delegate* unmanaged[Cdecl]<DigitalSingularity.Lua.Lua.lua_State*, int, nint, int>;
using unsafe lua_Reader = delegate* unmanaged[Cdecl]<DigitalSingularity.Lua.Lua.lua_State*, void*, long*, byte*>;
using unsafe lua_Writer = delegate* unmanaged[Cdecl]<DigitalSingularity.Lua.Lua.lua_State*, void*, long, void*, int>;

public static unsafe class NativeLua
{
    [StructLayout(LayoutKind.Sequential)]
    public struct lua_Debug
    {
        // int event;
        // const char *name;	/* (n) */
        // const char *namewhat;	/* (n) 'global', 'local', 'field', 'method' */
        // const char *what;	/* (S) 'Lua', 'C', 'main', 'tail' */
        // const char *source;	/* (S) */
        // size_t srclen;	/* (S) */
        // int currentline;	/* (l) */
        // int linedefined;	/* (S) */
        // int lastlinedefined;	/* (S) */
        // unsigned char nups;	/* (u) number of upvalues */
        // unsigned char nparams;/* (u) number of parameters */
        // char isvararg;        /* (u) */
        // unsigned char extraargs;  /* (t) number of extra arguments */
        // char istailcall;	/* (t) */
        // int ftransfer;   /* (r) index of first value transferred */
        // int ntransfer;   /* (r) number of transferred values */
        // char short_src[LUA_IDSIZE]; /* (S) */
        // /* private part */
        // struct CallInfo *i_ci;  /* active function */
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct luaL_Buffer
    {
        // char *b;  /* buffer address */
        // size_t size;  /* buffer size */
        // size_t n;  /* number of characters in buffer */
        // lua_State *L;
        // union {
        //     LUAI_MAXALIGN;  /* ensure maximum alignment for buffer */
        //     char b[LUAL_BUFFERSIZE];  /* initial buffer */
        // } init;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct luaL_Reg
    {
        public byte* name;
        public delegate* unmanaged[Cdecl]<lua_State*, int> func;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct va_reader
    {
        public void* args;

        public delegate* unmanaged[Cdecl]<void*, int> NextInt32;
        public delegate* unmanaged[Cdecl]<void*, long> NextInt64;
        public delegate* unmanaged[Cdecl]<void*, double> NextDouble;
        public delegate* unmanaged[Cdecl]<void*, void*> NextPointer;
    }

    private struct VaReader(va_reader* reader) : IVarArgReader
    {
        public string NextString()
        {
            byte* str = (byte*)reader->NextPointer(reader->args);
            Debugger.Break();
            throw new NotImplementedException();
        }

        public byte NextByte()
        {
            return (byte)reader->NextInt32(reader->args);
        }

        public int NextInt()
        {
            return reader->NextInt32(reader->args);
        }

        public long NextLong()
        {
            return reader->NextInt64(reader->args);
        }

        public double NextDouble()
        {
            return reader->NextDouble(reader->args);
        }

        public void* NextPointer()
        {
            return reader->NextPointer(reader->args);
        }
    }

    private sealed class KFunctionHelper(lua_KFunction k, IntPtr ctx)
    {
        private readonly lua_KFunction k = k;
        private readonly IntPtr ctx = ctx;
    }

    [DoesNotReturn]
    private static void __fail(Exception e, [CallerMemberName] string caller = "")
    {
        string message = $"{caller} has an uncaught exception:\n{e}";
        Console.WriteLine(message);
        Console.Error.WriteLine(message);
        
        Debugger.Break();
        Environment.Exit(-1);
    }

    private static int KFunctionWrapper(lua_State* L, int nargs, nint ctx)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_newstate", CallConvs = [typeof(CallConvCdecl)])]
    [return: DNNE.C99Type("struct lua_State*")]
    public static lua_State* lua_newstate([DNNE.C99Type("void*")] lua_Alloc f, void* ud, uint seed)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_close", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_close([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Lua.lua_close(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_newthread", CallConvs = [typeof(CallConvCdecl)])]
    [return: DNNE.C99Type("struct lua_State*")]
    public static lua_State* lua_newthread([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_newthread(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_closethread", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_closethread(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("struct lua_State*")] lua_State* from)
    {
        try
        {
            return Lua.lua_closethread(L, from);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_atpanic", CallConvs = [typeof(CallConvCdecl)])]
    [return: DNNE.C99Type("void*")]
    public static delegate* unmanaged[Cdecl]<lua_State*, int> lua_atpanic(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("void*")] delegate* unmanaged[Cdecl]<lua_State*, int> panicf)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_version", CallConvs = [typeof(CallConvCdecl)])]
    public static double lua_version([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_version(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_absindex", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_absindex([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_absindex(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_gettop", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_gettop([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_gettop(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_settop", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_settop([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            Lua.lua_settop(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushvalue", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_pushvalue([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            Lua.lua_pushvalue(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rotate", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_rotate([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, int n)
    {
        try
        {
            Lua.lua_rotate(L, idx, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_copy", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_copy([DNNE.C99Type("struct lua_State*")] lua_State* L, int fromidx, int toidx)
    {
        try
        {
            Lua.lua_copy(L, fromidx, toidx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_checkstack", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_checkstack([DNNE.C99Type("struct lua_State*")] lua_State* L, int n)
    {
        try
        {
            return Lua.lua_checkstack(L, n) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_xmove", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_xmove(
        [DNNE.C99Type("struct lua_State*")] lua_State* from,
        [DNNE.C99Type("struct lua_State*")] lua_State* to,
        int n)
    {
        try
        {
            Lua.lua_xmove(from, to, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_isnumber", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_isnumber([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_isnumber(L, idx) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_isstring", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_isstring([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_isstring(L, idx) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_iscfunction", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_iscfunction([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_iscfunction(L, idx) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_isinteger", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_isinteger([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_isinteger(L, idx) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_isuserdata", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_isuserdata([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_isuserdata(L, idx) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_type", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_type([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_type(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_typename", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_typename([DNNE.C99Type("struct lua_State*")] lua_State* L, int tp)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_tonumberx", CallConvs = [typeof(CallConvCdecl)])]
    public static double lua_tonumberx([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, int* isnum)
    {
        try
        {
            double result = Lua.lua_tonumberx(L, idx, out bool tmp);
            if (isnum != null)
            {
                *isnum = tmp ? 1 : 0;
            }
            
            return result;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_tointegerx", CallConvs = [typeof(CallConvCdecl)])]
    public static long lua_tointegerx([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, int* isnum)
    {
        try
        {
            long result = Lua.lua_tointegerx(L, idx, out bool tmp);
            if (isnum != null)
            {
                *isnum = tmp ? 1 : 0;
            }

            return result;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_toboolean", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_toboolean([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_toboolean(L, idx) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_tolstring", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_tolstring([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, nuint* len)
    {
        try
        {
            byte* result = Lua.lua_tolstring(L, idx, out int tmp);
            if (len != null)
            {
                *len = (nuint)tmp;
            }

            return result;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rawlen", CallConvs = [typeof(CallConvCdecl)])]
    public static ulong lua_rawlen([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_rawlen(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_tocfunction", CallConvs = [typeof(CallConvCdecl)])]
    [return: DNNE.C99Type("void*")]
    public static delegate* unmanaged[Cdecl]<lua_State*, int> lua_tocfunction(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        int idx)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_touserdata", CallConvs = [typeof(CallConvCdecl)])]
    public static void* lua_touserdata([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_touserdata(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_tothread", CallConvs = [typeof(CallConvCdecl)])]
    [return: DNNE.C99Type("struct lua_State*")]
    public static lua_State* lua_tothread([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_tothread(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_topointer", CallConvs = [typeof(CallConvCdecl)])]
    public static void* lua_topointer([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_topointer(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_arith", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_arith([DNNE.C99Type("struct lua_State*")] lua_State* L, int op)
    {
        try
        {
            Lua.lua_arith(L, op);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rawequal", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_rawequal([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx1, int idx2)
    {
        try
        {
            return Lua.lua_rawequal(L, idx1, idx2) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_compare", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_compare([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx1, int idx2, int op)
    {
        try
        {
            return Lua.lua_compare(L, idx1, idx2, op) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushnil", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_pushnil([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Lua.lua_pushnil(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushnumber", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_pushnumber([DNNE.C99Type("struct lua_State*")] lua_State* L, double n)
    {
        try
        {
            Lua.lua_pushnumber(L, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushinteger", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_pushinteger([DNNE.C99Type("struct lua_State*")] lua_State* L, long n)
    {
        try
        {
            Lua.lua_pushinteger(L, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushlstring", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_pushlstring([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* s, nuint len)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushexternalstring", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_pushexternalstring(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        byte* s,
        nuint len,
        lua_Alloc falloc,
        void* ud)
    {
        try
        {
            return Lua.lua_pushexternalstring(L, s, (int)len, AllocFunction.FromUnmanaged(falloc), ud);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushstring", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_pushstring([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* s)
    {
        try
        {
            return Lua.lua_pushstring(L, s);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "intl_lua_pushfstring", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_pushfstring(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        byte* fmt,
        [DNNE.C99Type("struct va_reader*")] va_reader* argp)
    {
        try
        {
            return Lua.lua_pushfstring(L, fmt, new VaReader(argp));
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushcclosure", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_pushcclosure(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("void*")] delegate* unmanaged[Cdecl]<lua_State*, int> fn,
        int n)
    {
        try
        {
            CFunction function = CFunction.FromUnmanaged(fn);
            // Lua.lua_pushstring(L, "Test");
            // function.Call(L);
            Lua.lua_pushcclosure(L, function, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushboolean", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_pushboolean([DNNE.C99Type("struct lua_State*")] lua_State* L, int b)
    {
        try
        {
            Lua.lua_pushboolean(L, b != 0);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushlightuserdata", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_pushlightuserdata([DNNE.C99Type("struct lua_State*")] lua_State* L, void* p)
    {
        try
        {
            Lua.lua_pushlightuserdata(L, p);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pushthread", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_pushthread([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_pushthread(L) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getglobal", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_getglobal([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* name)
    {
        try
        {
            ReadOnlySpan<byte> span = new(name, Lua.strlen(name));
            return Lua.lua_getglobal(L, Encoding.UTF8.GetString(span));
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_gettable", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_gettable([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_gettable(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getfield", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_getfield([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, byte* k)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_geti", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_geti([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, long n)
    {
        try
        {
            return Lua.lua_geti(L, idx, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rawget", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_rawget([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_rawget(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rawgeti", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_rawgeti([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, long n)
    {
        try
        {
            return Lua.lua_rawgeti(L, idx, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rawgetp", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_rawgetp([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, void* p)
    {
        try
        {
            return Lua.lua_rawgetp(L, idx, p);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_createtable", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_createtable([DNNE.C99Type("struct lua_State*")] lua_State* L, int narr, int nrec)
    {
        try
        {
            Lua.lua_createtable(L, narr, nrec);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_newuserdatauv", CallConvs = [typeof(CallConvCdecl)])]
    public static void* lua_newuserdatauv([DNNE.C99Type("struct lua_State*")] lua_State* L, nuint sz, int nuvalue)
    {
        try
        {
            return Lua.lua_newuserdatauv(L, (long)sz, nuvalue);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getmetatable", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_getmetatable([DNNE.C99Type("struct lua_State*")] lua_State* L, int objindex)
    {
        try
        {
            return Lua.lua_getmetatable(L, objindex) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getiuservalue", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_getiuservalue([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, int n)
    {
        try
        {
            return Lua.lua_getiuservalue(L, idx, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_setglobal", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_setglobal([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* name)
    {
        try
        {
            ReadOnlySpan<byte> str = new(name, Lua.strlen(name));
            Lua.lua_setglobal(L, Encoding.UTF8.GetString(str));
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_settable", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_settable([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            Lua.lua_settable(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_setfield", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_setfield([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, byte* k)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_seti", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_seti([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, long n)
    {
        try
        {
            Lua.lua_seti(L, idx, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rawset", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_rawset([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            Lua.lua_rawset(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rawseti", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_rawseti([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, long n)
    {
        try
        {
            Lua.lua_rawseti(L, idx, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_rawsetp", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_rawsetp([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, void* p)
    {
        try
        {
            Lua.lua_rawsetp(L, idx, p);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_setmetatable", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_setmetatable([DNNE.C99Type("struct lua_State*")] lua_State* L, int objindex)
    {
        try
        {
            return Lua.lua_setmetatable(L, objindex) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_setiuservalue", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_setiuservalue([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, int n)
    {
        try
        {
            return Lua.lua_setiuservalue(L, idx, n) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_callk", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_callk(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        int nargs,
        int nresults,
        nint ctx,
        [DNNE.C99Type("void*")] lua_KFunction k)
    {
        try
        {
            using GCHandle<KFunctionHelper> handle = new(new KFunctionHelper(k, ctx));
            Lua.lua_callk(L, nargs, nresults, (nint)handle.ToPointer(), &KFunctionWrapper);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_pcallk", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_pcallk(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        int nargs,
        int nresults,
        int errfunc,
        nint ctx,
        [DNNE.C99Type("void*")] lua_KFunction k)
    {
        try
        {
            using GCHandle<KFunctionHelper> handle = new(new KFunctionHelper(k, ctx));
            return Lua.lua_pcallk(L, nargs, nresults, errfunc, (nint)handle.ToPointer(), &KFunctionWrapper);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_load", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_load(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("void*")] lua_Reader reader,
        void* dt,
        byte* chunkname,
        byte* mode)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_dump", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_dump(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("void*")] lua_Writer writer,
        void* data,
        int strip)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_yieldk", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_yieldk(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        int nresults,
        nint ctx,
        [DNNE.C99Type("void*")] lua_KFunction k)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_resume", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_resume(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("struct lua_State*")] lua_State* from,
        int narg,
        int* nres)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_status", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_status([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_status(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_isyieldable", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_isyieldable([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_isyieldable(L) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_setwarnf", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_setwarnf([DNNE.C99Type("struct lua_State*")] lua_State* L, lua_WarnFunction f, void* ud)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_warning", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_warning([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* msg, int tocont)
    {
        try
        {
            ReadOnlySpan<byte> span = new(msg, Lua.strlen(msg));
            Lua.lua_warning(L, Encoding.UTF8.GetString(span), tocont != 0);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "impl_lua_gc", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_gc(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        int what,
        [DNNE.C99Type("struct va_reader*")] va_reader* va)
    {
        // TODO: args
        try
        {
            return Lua.lua_gc(L, what);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_error", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_error([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_error(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_next", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_next([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.lua_next(L, idx) ? 1 : 0;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_concat", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_concat([DNNE.C99Type("struct lua_State*")] lua_State* L, int n)
    {
        try
        {
            Lua.lua_concat(L, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_len", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_len([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            Lua.lua_len(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_numbertocstring", CallConvs = [typeof(CallConvCdecl)])]
    public static uint lua_numbertocstring([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, byte* buff)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_stringtonumber", CallConvs = [typeof(CallConvCdecl)])]
    public static nuint lua_stringtonumber([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* s)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void* allocf_wrapper(void* ud, void* ptr, long osize, long nsize)
    {
        lua_State* L = (lua_State*)ud;
        AllocFunction result = Lua.lua_getallocf(L, out void* tmp);
        return result.Call(tmp, ptr, osize, nsize);
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getallocf", CallConvs = [typeof(CallConvCdecl)])]
    public static lua_Alloc lua_getallocf([DNNE.C99Type("struct lua_State*")] lua_State* L, void** ud)
    {
        try
        {
            AllocFunction falloc = Lua.lua_getallocf(L, out void* tmp);
            if (falloc.fn != null)
            {
                *ud = tmp;
                return falloc.fn;
            }
            
            *ud = L;
            lua_Alloc wrappedResult = &allocf_wrapper;
            return wrappedResult;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_setallocf", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_setallocf([DNNE.C99Type("struct lua_State*")] lua_State* L, lua_Alloc f, void* ud)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e) { __fail(e); }
        //return Lua.lua_setallocf();
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_toclose", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_toclose([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            Lua.lua_toclose(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_closeslot", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_closeslot([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            Lua.lua_closeslot(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getstack", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_getstack(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        int level,
        [DNNE.C99Type("struct lua_Debug*")] lua_Debug* ar)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getinfo", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_getinfo(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        byte* what,
        [DNNE.C99Type("struct lua_Debug*")] lua_Debug* ar)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getlocal", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_getlocal(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("struct lua_Debug*")] lua_Debug* ar,
        int n)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_setlocal", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_setlocal(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("struct lua_Debug*")] lua_Debug* ar,
        int n)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_getupvalue", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_getupvalue([DNNE.C99Type("struct lua_State*")] lua_State* L, int funcindex, int n)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_setupvalue", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* lua_setupvalue([DNNE.C99Type("struct lua_State*")] lua_State* L, int funcindex, int n)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_upvalueid", CallConvs = [typeof(CallConvCdecl)])]
    public static void* lua_upvalueid([DNNE.C99Type("struct lua_State*")] lua_State* L, int fidx, int n)
    {
        try
        {
            return Lua.lua_upvalueid(L, fidx, n);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_upvaluejoin", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_upvaluejoin(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        int fidx1,
        int n1,
        int fidx2,
        int n2)
    {
        try
        {
            Lua.lua_upvaluejoin(L, fidx1, n1, fidx2, n2);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_sethook", CallConvs = [typeof(CallConvCdecl)])]
    public static void lua_sethook(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("void*")] lua_Hook func,
        int mask,
        int count)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_gethook", CallConvs = [typeof(CallConvCdecl)])]
    [return: DNNE.C99Type("void*")]
    public static lua_Hook lua_gethook([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_gethookmask", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_gethookmask([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_gethookmask(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_gethookcount", CallConvs = [typeof(CallConvCdecl)])]
    public static int lua_gethookcount([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.lua_gethookcount(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checkversion_", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_checkversion_([DNNE.C99Type("struct lua_State*")] lua_State* L, double ver, nuint sz)
    {
        try
        {
            Lua.luaL_checkversion(L, (int)ver, (int)sz);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_getmetafield", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_getmetafield([DNNE.C99Type("struct lua_State*")] lua_State* L, int obj, byte* e)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            __fail(ex);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_callmeta", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_callmeta([DNNE.C99Type("struct lua_State*")] lua_State* L, int obj, byte* e)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            __fail(ex);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_tolstring", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* luaL_tolstring([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, nuint* len)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_argerror", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_argerror([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg, byte* extramsg)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_typeerror", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_typeerror([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg, byte* tname)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checklstring", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* luaL_checklstring([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg, nuint* l)
    {
        try
        {
            byte* result = Lua.luaL_checklstring(L, arg, out int tmp);
            if (l != null)
            {
                *l = (nuint)tmp;
            }

            return result;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_optlstring", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* luaL_optlstring([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg, byte* def, nuint* l)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checknumber", CallConvs = [typeof(CallConvCdecl)])]
    public static double luaL_checknumber([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg)
    {
        try
        {
            return Lua.luaL_checknumber(L, arg);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_optnumber", CallConvs = [typeof(CallConvCdecl)])]
    public static double luaL_optnumber([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg, double def)
    {
        try
        {
            return Lua.luaL_optnumber(L, arg, def);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checkinteger", CallConvs = [typeof(CallConvCdecl)])]
    public static long luaL_checkinteger([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg)
    {
        try
        {
            return Lua.luaL_checkinteger(L, arg);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_optinteger", CallConvs = [typeof(CallConvCdecl)])]
    public static long luaL_optinteger([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg, long def)
    {
        try
        {
            return Lua.luaL_optinteger(L, arg, def);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checkstack", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_checkstack([DNNE.C99Type("struct lua_State*")] lua_State* L, int sz, byte* msg)
    {
        try
        {
            ReadOnlySpan<byte> msgS = new(msg, Lua.strlen(msg));
            Lua.luaL_checkstack(L, sz, msg == null ? null : Encoding.UTF8.GetString(msgS));
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checktype", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_checktype([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg, int t)
    {
        try
        {
            Lua.luaL_checktype(L, arg, t);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checkany", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_checkany([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg)
    {
        try
        {
            Lua.luaL_checkany(L, arg);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_newmetatable", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_newmetatable([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* tname)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_setmetatable", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_setmetatable([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* tname)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_testudata", CallConvs = [typeof(CallConvCdecl)])]
    public static void* luaL_testudata([DNNE.C99Type("struct lua_State*")] lua_State* L, int ud, byte* tname)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checkudata", CallConvs = [typeof(CallConvCdecl)])]
    public static void* luaL_checkudata([DNNE.C99Type("struct lua_State*")] lua_State* L, int ud, byte* tname)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_where", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_where([DNNE.C99Type("struct lua_State*")] lua_State* L, int lvl)
    {
        try
        {
            Lua.luaL_where(L, lvl);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "intl_luaL_error", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_error(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        byte* fmt,
        [DNNE.C99Type("struct va_reader*")] va_reader* va)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_checkoption", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_checkoption([DNNE.C99Type("struct lua_State*")] lua_State* L, int arg, byte* def, byte** lst)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_fileresult", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_fileresult([DNNE.C99Type("struct lua_State*")] lua_State* L, int stat, byte* fname)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_execresult", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_execresult([DNNE.C99Type("struct lua_State*")] lua_State* L, int stat)
    {
        try
        {
            return Lua.luaL_execresult(L, stat);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_alloc", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_alloc(void* ud, void* ptr, nuint osize, nuint nsize)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_ref", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_ref([DNNE.C99Type("struct lua_State*")] lua_State* L, int t)
    {
        try
        {
            return Lua.luaL_ref(L, t);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_unref", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_unref([DNNE.C99Type("struct lua_State*")] lua_State* L, int t, int @ref)
    {
        try
        {
            Lua.luaL_unref(L, t, @ref);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_loadfilex", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_loadfilex([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* filename, byte* mode)
    {
        try
        {
            ReadOnlySpan<byte> filenameS = new(filename, Lua.strlen(filename));
            ReadOnlySpan<byte> modeS = new(mode, Lua.strlen(mode));
            return Lua.luaL_loadfilex(
                L,
                filename == null ? null : Encoding.UTF8.GetString(filenameS),
                mode == null ? null : Encoding.UTF8.GetString(modeS));
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_loadbufferx", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_loadbufferx(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        byte* buff,
        nuint sz,
        byte* name,
        byte* mode)
    {
        try
        {
            ReadOnlySpan<byte> buffS = new(buff, checked((int)sz));
            ReadOnlySpan<byte> nameS = new(name, Lua.strlen(name));
            ReadOnlySpan<byte> modeS = new(mode, Lua.strlen(mode));
            return Lua.luaL_loadbufferx(
                L,
                buffS,
                name == null ? null : Encoding.UTF8.GetString(nameS),
                mode == null ? null : Encoding.UTF8.GetString(modeS));
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_loadstring", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_loadstring([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* s)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_newstate", CallConvs = [typeof(CallConvCdecl)])]
    [return: DNNE.C99Type("struct lua_State*")]
    public static lua_State* luaL_newstate()
    {
        try
        {
            lua_State* result = Lua.luaL_newstate();
            return result;
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "lua_State", CallConvs = [typeof(CallConvCdecl)])]
    public static uint luaL_makeseed([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            return Lua.luaL_makeseed(L);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_len", CallConvs = [typeof(CallConvCdecl)])]
    public static long luaL_len([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx)
    {
        try
        {
            return Lua.luaL_len(L, idx);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_addgsub", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_addgsub([DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* b, byte* s, byte* p, byte* r)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_gsub", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* luaL_gsub([DNNE.C99Type("struct lua_State*")] lua_State* L, byte* s, byte* p, byte* r)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_setfuncs", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_setfuncs(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("struct luaL_Reg*")] luaL_Reg* l,
        int nup)
    {
        try
        {
            int i = 0;
            while (l[i].name != null)
            {
                i++;
            }

            Lua.luaL_Reg[] funcs = new Lua.luaL_Reg[i];
            for (int j = 0; j < i; j++)
            {
                funcs[j].name = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(l[j].name, Lua.strlen(l[j].name)));
                funcs[j].func = CFunction.FromUnmanaged(l[j].func);
            }
            
            Lua.luaL_setfuncs(L, funcs, nup);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_getsubtable", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaL_getsubtable([DNNE.C99Type("struct lua_State*")] lua_State* L, int idx, byte* fname)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_traceback", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_traceback(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("struct lua_State*")] lua_State* L1,
        byte* msg,
        int level)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_requiref", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_requiref(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        byte* modname,
        [DNNE.C99Type("void*")] delegate* unmanaged[Cdecl]<lua_State*, int> openf,
        int glb)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_buffinit", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_buffinit(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* B)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_prepbuffsize", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* luaL_prepbuffsize([DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* B, nuint sz)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_addlstring", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_addlstring([DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* B, byte* s, nuint l)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_addstring", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_addstring([DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* B, byte* s)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_addvalue", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_addvalue([DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* B)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_pushresult", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_pushresult([DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* B)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_pushresultsize", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_pushresultsize([DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* B, nuint sz)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_buffinitsize", CallConvs = [typeof(CallConvCdecl)])]
    public static byte* luaL_buffinitsize(
        [DNNE.C99Type("struct lua_State*")] lua_State* L,
        [DNNE.C99Type("struct luaL_Buffer*")] luaL_Buffer* B,
        nuint sz)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_base", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_base([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_package", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_package([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_coroutine", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_coroutine([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_debug", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_debug([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_io", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_io([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_math", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_math([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_os", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_os([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_string", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_string([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_table", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_table([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaopen_utf8", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaopen_utf8([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaL_openselectedlibs", CallConvs = [typeof(CallConvCdecl)])]
    public static void luaL_openselectedlibs([DNNE.C99Type("struct lua_State*")] lua_State* L, int load, int preload)
    {
        try
        {
            Lua.luaL_openselectedlibs(L, load, preload);
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "luaB_opentests", CallConvs = [typeof(CallConvCdecl)])]
    public static int luaB_opentests([DNNE.C99Type("struct lua_State*")] lua_State* L)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "debug_realloc", CallConvs = [typeof(CallConvCdecl)])]
    public static void* debug_realloc(void* ud, void* block, nuint osize, nuint nsize)
    {
        try
        {
            Debugger.Break();
            throw new NotImplementedException();
        }
        catch (Exception e)
        {
            __fail(e);
            throw;
        }
    }
}
