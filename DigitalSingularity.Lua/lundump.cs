namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    // $Id: lundump.c $
    // load precompiled Lua chunks
    // See Copyright Notice in lua.h
    
    /// <summary>
    /// data to catch conversion errors
    /// </summary>
    private static ReadOnlySpan<byte> LUAC_DATA => [0x19, 0x93, 0x0D, 0x0A, 0x1A, 0x0A];

    private const int LUAC_INT = -0x5678;
    private const uint LUAC_INST = 0x12345678;
    private const double LUAC_NUM = -370.5;

    /// <summary>
    /// Encode major-minor version in one byte, one nibble for each
    /// </summary>
    private const int LUAC_VERSION = LUA_VERSION_MAJOR_N * 16 + LUA_VERSION_MINOR_N;

    private const int LUAC_FORMAT = 0; // this is the official format
    
    private static void luai_verifycode(lua_State* L, Proto* f)
    {
    }

    private struct LoadState
    {
        public lua_State* L;
        public Zio* Z;
        public string name;
        public Table* h; // list for string reuse
        public long offset; // current position relative to beginning of dump
        public ulong nstr; // number of strings in the list
        public bool @fixed; // dump is fixed in memory
    }

    [DoesNotReturn]
    private static void error(ref LoadState S, string why)
    {
        luaO_pushfstring(S.L, "%s: bad binary format (%s)", S.name, why);
        luaD_throw(S.L, LUA_ERRSYNTAX);
    }

    /// <summary>
    /// All high-level loads go through loadVector; you can change it to
    /// adapt to the endianness of the input
    /// </summary>
    private static void loadVector(ref LoadState S, ReadOnlySpan<byte> b)
    {
        loadBlock(ref S, b);
    }

    private static void loadBlock(ref LoadState S, ReadOnlySpan<byte> b)
    {
        fixed (void* p = b)
        {
            if (luaZ_read(S.Z, p, b.Length) != 0)
            {
                error(ref S, "truncated chunk");
            }

            S.offset += b.Length;
        }
    }

    private static void loadAlign(ref LoadState S, int align)
    {
        int padding = align - (int)(S.offset % align);
        if (padding < align)
        {
            // (padding == align) means no padding
            long paddingContent;
            Span<byte> paddingSpan = new(&paddingContent, padding);
            loadBlock(ref S, paddingSpan);
            Debug.Assert(S.offset % align == 0);
        }
    }

    private static T* getaddr<T>(ref LoadState S, int n)
        where T : unmanaged
    {
        return (T*)getaddr_(ref S, n * sizeof(T));
    }

    private static void* getaddr_(ref LoadState S, int size)
    {
        void* block = luaZ_getaddr(S.Z, size);
        S.offset += size;
        if (block == null)
        {
            error(ref S, "truncated fixed buffer");
        }

        return block;
    }

    private static void loadVar<T>(ref LoadState S, out T x)
        where T : unmanaged
    {
        x = default;
        Span<T> s = new(ref x);
        loadVector(ref S, MemoryMarshal.Cast<T, byte>(s));
    }

    private static byte loadByte(ref LoadState S)
    {
        int b = zgetc(S.Z);
        if (b < 0)
        {
            error(ref S, "truncated chunk");
        }

        S.offset++;
        return (byte)b;
    }

    private static ulong loadVarint(ref LoadState S, ulong limit)
    {
        ulong x = 0;
        limit >>= 7;

        int b;
        do
        {
            b = loadByte(ref S);
            if (x > limit)
            {
                error(ref S, "integer overflow");
            }

            x = x << 7 | (byte)(b & 0x7f);
        } while ((b & 0x80) != 0);

        return x;
    }

    private static long loadSize(ref LoadState S)
    {
        return (long)loadVarint(ref S, long.MaxValue);
    }

    private static int loadInt(ref LoadState S)
    {
        return (int)loadVarint(ref S, int.MaxValue);
    }

    private static double loadNumber(ref LoadState S)
    {
        loadVar(ref S, out double x);
        return x;
    }

    private static long loadInteger(ref LoadState S)
    {
        ulong cx = loadVarint(ref S, ulong.MaxValue);
        // decode unsigned to signed
        if ((cx & 1) != 0)
        {
            return (long)~(cx >> 1);
        }

        return (long)(cx >> 1);
    }

    /// <summary>
    /// Load a nullable string into slot 'sl' from prototype 'p'. The
    /// assignment to the slot and the barrier must be performed before any
    /// possible GC activity, to anchor the string. (Both 'loadVector' and
    /// 'luaH_setint' can call the GC.)
    /// </summary>
    private static void loadString(ref LoadState S, Proto* p, out TString* sl)
    {
        lua_State* L = S.L;
        TString* ts;
        int size = loadInt(ref S);
        if (size == 0)
        {
            // previously saved string?
            ulong idx = loadVarint(ref S, ulong.MaxValue); // get its index
            if (idx == 0)
            {
                // no string?
                sl = null;
                return;
            }

            TValue stv;
            if (novariant(luaH_getint(S.h, (long)idx, &stv)) != LUA_TSTRING)
            {
                error(ref S, "invalid string index");
            }

            sl = ts = tsvalue(&stv); // get its value
            luaC_objbarrier(L, (GCObject*)p, (GCObject*)ts);
            return; // do not save it again
        }

        if ((size -= 1) <= LUAI_MAXSHORTLEN)
        {
            // short string?
            Span<byte> buff = stackalloc byte[LUAI_MAXSHORTLEN + 1];
            loadVector(ref S, buff[..(size + 1)]); // load string into buffer
            sl = ts = luaS_newlstr(L, buff[..size]); // create string
            luaC_objbarrier(L, (GCObject*)p, (GCObject*)ts);
        }
        else if (S.@fixed)
        {
            // for a fixed buffer, use a fixed string
            byte* s = getaddr<byte>(ref S, size + 1); // get content address
            sl = ts = luaS_newextlstr(L, s, size, null, null);
            luaC_objbarrier(L, (GCObject*)p, (GCObject*)ts);
        }
        else
        {
            // create internal copy
            sl = ts = luaS_createlngstrobj(L, size); // create string
            luaC_objbarrier(L, (GCObject*)p, (GCObject*)ts);
            loadVector(ref S, new ReadOnlySpan<byte>(getlngstr(ts), size + 1)); // load directly in final place
        }

        // add string to list of saved strings
        S.nstr++;
        TValue sv;
        setsvalue(L, &sv, ts);
        luaH_setint(L, S.h, (long)S.nstr, &sv);
        luaC_objbarrierback(L, obj2gco(S.h), (GCObject*)ts);
    }

    private static void loadCode(ref LoadState S, Proto* f)
    {
        int n = loadInt(ref S);
        loadAlign(ref S, sizeof(uint));
        if (S.@fixed)
        {
            f->code = getaddr<uint>(ref S, n);
            f->sizecode = n;
        }
        else
        {
            f->code = luaM_newvectorchecked<uint>(S.L, n);
            f->sizecode = n;
            loadVector(ref S, MemoryMarshal.Cast<uint, byte>(new ReadOnlySpan<uint>(f->code, n)));
        }
    }

    private static void loadConstants(ref LoadState S, Proto* f)
    {
        int n = loadInt(ref S);
        f->k = luaM_newvectorchecked<TValue>(S.L, n);
        f->sizek = n;
        for (int i = 0; i < n; i++)
        {
            setnilvalue(&f->k[i]);
        }

        for (int i = 0; i < n; i++)
        {
            TValue* o = &f->k[i];
            int t = loadByte(ref S);
            switch (t)
            {
                case LUA_VNIL:
                    setnilvalue(o);
                    break;

                case LUA_VFALSE:
                    setbfvalue(o);
                    break;

                case LUA_VTRUE:
                    setbtvalue(o);
                    break;

                case LUA_VNUMFLT:
                    setfltvalue(o, loadNumber(ref S));
                    break;

                case LUA_VNUMINT:
                    setivalue(o, loadInteger(ref S));
                    break;

                case LUA_VSHRSTR:
                case LUA_VLNGSTR:
                    Debug.Assert(f->source == null);
                    loadString(ref S, f, out f->source); // use 'source' to anchor string
                    if (f->source == null)
                    {
                        error(ref S, "bad format for constant string");
                    }

                    setsvalue2n(S.L, o, f->source); // save it in the right place
                    f->source = null;
                    break;

                default:
                    error(ref S, "invalid constant");
                    break;
            }
        }
    }

    private static void loadProtos(ref LoadState S, Proto* f)
    {
        int n = loadInt(ref S);
        f->p = luaM_newvectorchecked2<Proto>(S.L, n);
        f->sizep = n;
        for (int i = 0; i < n; i++)
        {
            f->p[i] = null;
        }

        for (int i = 0; i < n; i++)
        {
            f->p[i] = luaF_newproto(S.L);
            luaC_objbarrier(S.L, (GCObject*)f, (GCObject*)f->p[i]);
            loadFunction(ref S, f->p[i]);
        }
    }

    /// <summary>
    /// Load the upvalues for a function. The names must be filled first,
    /// because the filling of the other fields can raise read errors and
    /// the creation of the error message can call an emergency collection;
    /// in that case all prototypes must be consistent for the GC.
    /// </summary>
    private static void loadUpvalues(ref LoadState S, Proto* f)
    {
        int n = loadInt(ref S);
        f->upvalues = luaM_newvectorchecked<Upvaldesc>(S.L, n);
        f->sizeupvalues = n;
        for (int i = 0; i < n; i++) // make array valid for GC
        {
            f->upvalues[i].name = null;
        }

        for (int i = 0; i < n; i++)
        {
            // following calls can raise errors
            f->upvalues[i].instack = loadByte(ref S);
            f->upvalues[i].idx = loadByte(ref S);
            f->upvalues[i].kind = loadByte(ref S);
        }
    }

    private static void loadDebug(ref LoadState S, Proto* f)
    {
        int n = loadInt(ref S);
        if (S.@fixed)
        {
            f->lineinfo = getaddr<sbyte>(ref S, n);
            f->sizelineinfo = n;
        }
        else
        {
            f->lineinfo = luaM_newvectorchecked<sbyte>(S.L, n);
            f->sizelineinfo = n;
            loadVector(ref S, new ReadOnlySpan<byte>(f->lineinfo, n));
        }

        n = loadInt(ref S);
        if (n > 0)
        {
            loadAlign(ref S, sizeof(int));
            if (S.@fixed)
            {
                f->abslineinfo = getaddr<AbsLineInfo>(ref S, n);
                f->sizeabslineinfo = n;
            }
            else
            {
                f->abslineinfo = luaM_newvectorchecked<AbsLineInfo>(S.L, n);
                f->sizeabslineinfo = n;
                loadVector(ref S, MemoryMarshal.Cast<nint, byte>(new ReadOnlySpan<nint>(f->abslineinfo, n)));
            }
        }

        n = loadInt(ref S);
        f->locvars = luaM_newvectorchecked<LocVar>(S.L, n);
        f->sizelocvars = n;
        for (int i = 0; i < n; i++)
        {
            f->locvars[i].varname = null;
        }

        for (int i = 0; i < n; i++)
        {
            loadString(ref S, f, out f->locvars[i].varname);
            f->locvars[i].startpc = loadInt(ref S);
            f->locvars[i].endpc = loadInt(ref S);
        }

        n = loadInt(ref S);
        if (n != 0) // does it have debug information?
        {
            n = f->sizeupvalues; // must be this many
        }

        for (int i = 0; i < n; i++)
        {
            loadString(ref S, f, out f->upvalues[i].name);
        }
    }

    private static void loadFunction(ref LoadState S, Proto* f)
    {
        f->linedefined = loadInt(ref S);
        f->lastlinedefined = loadInt(ref S);
        f->numparams = loadByte(ref S);
        // get only the meaningful flags
        f->flag = (byte)(loadByte(ref S) & ~PF_FIXED);
        if (S.@fixed)
        {
            f->flag |= PF_FIXED; // signal that code is fixed
        }

        f->maxstacksize = loadByte(ref S);
        loadCode(ref S, f);
        loadConstants(ref S, f);
        loadUpvalues(ref S, f);
        loadProtos(ref S, f);
        loadString(ref S, f, out f->source);
        loadDebug(ref S, f);
    }

    private static void checkliteral(ref LoadState S, ReadOnlySpan<byte> s, string msg)
    {
        Span<byte> buff = stackalloc byte[LUA_SIGNATURE.Length + LUAC_DATA.Length];
        loadVector(ref S, buff[..s.Length]);
        if (!s.SequenceEqual(buff[..s.Length]))
        {
            error(ref S, msg);
        }
    }

    [DoesNotReturn]
    private static void numerror(ref LoadState S, string what, string tname)
    {
        string msg = luaO_pushfstring(S.L, "%s %s mismatch", tname, what);
        error(ref S, msg);
    }

    private static void checknumsize(ref LoadState S, int size, string tname)
    {
        if (size != loadByte(ref S))
        {
            numerror(ref S, "size", tname);
        }
    }

    private static void checknumformat(ref LoadState S, bool eq, string tname)
    {
        if (!eq)
        {
            numerror(ref S, "format", tname);
        }
    }

    private static void checknum<T>(ref LoadState S, T value, string tname)
        where T : unmanaged, IEquatable<T>
    {
        checknumsize(ref S, sizeof(T), tname);
        loadVar(ref S, out T i);
        checknumformat(ref S, i.Equals(value), tname);
    }

    private static void checkHeader(ref LoadState S)
    {
        // skip 1st char (already read and checked)
        checkliteral(ref S, LUA_SIGNATURE[1..], "not a binary chunk");
        if (loadByte(ref S) != LUAC_VERSION)
        {
            error(ref S, "version mismatch");
        }

        if (loadByte(ref S) != LUAC_FORMAT)
        {
            error(ref S, "format mismatch");
        }

        checkliteral(ref S, LUAC_DATA, "corrupted chunk");
        checknum(ref S, LUAC_INT, "int");
        checknum(ref S, LUAC_INST, "instruction");
        checknum<long>(ref S, LUAC_INT, "Lua integer");
        checknum(ref S, LUAC_NUM, "Lua number");
    }

    /// <summary>
    /// Load precompiled chunk.
    /// </summary>
    private static LClosure* luaU_undump(lua_State* L, Zio* Z, string name, bool @fixed)
    {
        if (name.StartsWith('@') || name.StartsWith('='))
        {
            name = name[1..];
        }
        else if (name.StartsWith((char)LUA_SIGNATURE[0]))
        {
            name = "binary string";
        }

        LoadState S = new()
        {
            name = name,
            L = L,
            Z = Z,
            @fixed = @fixed,
            offset = 1, // fist byte was already read
        };
        checkHeader(ref S);
        LClosure* cl = luaF_newLclosure(L, loadByte(ref S));
        setclLvalue2s(L, L->top.p, cl);
        luaD_inctop(L);
        S.h = luaH_new(L); // create list of saved strings
        S.nstr = 0;
        sethvalue2s(L, L->top.p, S.h); // anchor it
        luaD_inctop(L);
        cl->p = luaF_newproto(L);
        luaC_objbarrier(L, (GCObject*)cl, (GCObject*)cl->p);
        loadFunction(ref S, cl->p);
        if (cl->nupvalues != cl->p->sizeupvalues)
        {
            error(ref S, "corrupted chunk");
        }

        luai_verifycode(L, cl->p);
        L->top.p--; // pop table
        return cl;
    }
}
