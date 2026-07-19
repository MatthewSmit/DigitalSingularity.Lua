namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    // $Id: ldump.c $
    // save precompiled Lua chunks
    // See Copyright Notice in lua.h

    private struct DumpState
    {
        public lua_State* L;
        public lua_Writer writer;
        public void* data;
        public long offset; // current position relative to beginning of dump
        public bool strip;
        public int status;
        public Table* h; // table to track saved strings
        public ulong nstr; // counter for counting saved strings
    }

    /// <summary>
    /// All high-level dumps go through dumpVector; you can change it to
    /// change the endianness of the result
    /// </summary>
    private static void dumpVector<T>(ref DumpState D, T* v, int n)
        where T : unmanaged
    {
        dumpBlock(ref D, v, n * sizeof(T));
    }
    
    private static void dumpVector<T>(ref DumpState D, ReadOnlySpan<T> v)
        where T : unmanaged
    {
        fixed(T* p = v)
        {
            dumpBlock(ref D, p, v.Length * sizeof(T));
        }
    }

    private static void dumpLiteral(ref DumpState D, ReadOnlySpan<byte> s)
    {
        fixed (byte* ptr = s)
        {
            dumpBlock(ref D, ptr, s.Length);
        }
    }

    /// <summary>
    /// Dump the block of memory pointed by 'b' with given 'size'.
    /// 'b' should not be null, except for the last call signalling the end
    /// of the dump.
    /// </summary>
    private static void dumpBlock(ref DumpState D, void* b, long size)
    {
        if (D.status != 0)
        {
            // do not write anything after an error
            return;
        }

        lua_unlock(D.L);
        D.status = D.writer(D.L, b, size, D.data);
        lua_lock(D.L);
        D.offset += size;
    }

    /// <summary>
    /// Dump enough zeros to ensure that current position is a multiple of
    /// 'align'.
    /// </summary>
    private static void dumpAlign(ref DumpState D, uint align)
    {
        uint padding = align - (uint)(D.offset % align);
        if (padding < align)
        {
            // padding == align means no padding
            long paddingContent = 0;
            Debug.Assert(align <= sizeof(long));
            dumpBlock(ref D, &paddingContent, padding);
        }

        Debug.Assert(D.offset % align == 0);
    }

    private static void dumpVar<T>(ref DumpState D, T x)
        where T : unmanaged
    {
        dumpVector(ref D, &x, 1);
    }

    private static void dumpByte(ref DumpState D, int y)
    {
        byte x = (byte)y;
        dumpVar(ref D, x);
    }

    /// <summary>
    /// size for 'dumpVarint' buffer: each byte can store up to 7 bits.
    /// (The "+6" rounds up the division.)
    /// </summary>
    private const int DIBS = (sizeof(ulong) * 8 + 6) / 7;

    /// <summary>
    /// Dumps an unsigned integer using the MSB Varint encoding
    /// </summary>
    private static void dumpVarint(ref DumpState D, ulong x)
    {
        byte* buff = stackalloc byte[DIBS];
        int n = 1;
        buff[DIBS - 1] = (byte)(x & 0x7f); // fill least-significant byte
        while ((x >>= 7) != 0) // fill other bytes in reverse order
        {
            buff[DIBS - ++n] = (byte)(x & 0x7f | 0x80);
        }

        dumpVector(ref D, buff + DIBS - n, n);
    }

    private static void dumpSize(ref DumpState D, long sz)
    {
        dumpVarint(ref D, (ulong)sz);
    }

    private static void dumpInt(ref DumpState D, int x)
    {
        Debug.Assert(x >= 0);
        dumpVarint(ref D, (uint)x);
    }

    private static void dumpNumber(ref DumpState D, double x)
    {
        dumpVar(ref D, x);
    }

    /// <summary>
    /// Signed integers are coded to keep small values small. (Coding -1 as
    /// 0xfff...fff would use too many bytes to save a quite common value.)
    /// A non-negative x is coded as 2x; a negative x is coded as -2x - 1.
    /// (0 =&gt; 0; -1 =&gt; 1; 1 =&gt; 2; -2 =&gt; 3; 2 =&gt; 4; ...)
    /// </summary>
    private static void dumpInteger(ref DumpState D, long x)
    {
        ulong cx = x >= 0
            ? 2u * (ulong)x
            : 2u * unchecked(~(ulong)x) + 1;
        dumpVarint(ref D, cx);
    }

    /// <summary>
    /// Dump a String. First dump its "size":
    /// size==0 is followed by an index and means "reuse saved string with
    /// that index"; index==0 means null.
    /// size&gt;=1 is followed by the string contents with real size==size-1 and
    /// means that string, which will be saved with the next available index.
    /// The real size does not include the ending '\0' (which is not dumped),
    /// so adding 1 to it cannot overflow a size_t.
    /// </summary>
    private static void dumpString(ref DumpState D, TString* ts)
    {
        if (ts == null)
        {
            dumpVarint(ref D, 0); // will "reuse" null
            dumpVarint(ref D, 0); // special index for null
        }
        else
        {
            TValue idx;
            byte tag = luaH_getstr(D.h, ts, &idx);
            if (!tagisempty(tag))
            {
                // string already saved?
                dumpVarint(ref D, 0); // reuse a saved string
                dumpVarint(ref D, (ulong)ivalue(&idx)); // index of saved string
            }
            else
            {
                // must write and save the string
                TValue key, value; // to save the string in the hash
                
                ReadOnlySpan<byte> s2 = getlstr(ts);
                byte* s = getlstr(ts, out _);
                dumpSize(ref D, s2.Length + 1);
                dumpVector(ref D, s, s2.Length);
                dumpByte(ref D, 0);  // include ending '\0'

                D.nstr++; // one more saved string
                setsvalue(D.L, &key, ts); // the string is the key
                setivalue(&value, (long)D.nstr); // its index is the value
                luaH_set(D.L, D.h, &key, &value); // h[ts] = nstr
                // integer value does not need barrier
            }
        }
    }

    private static void dumpCode(ref DumpState D, Proto* f)
    {
        dumpInt(ref D, f->sizecode);
        dumpAlign(ref D, sizeof(uint));
        Debug.Assert(f->code != null);
        dumpVector(ref D, f->code, f->sizecode);
    }

    private static void dumpConstants(ref DumpState D, Proto* f)
    {
        int n = f->sizek;
        dumpInt(ref D, n);
        for (int i = 0; i < n; i++)
        {
            TValue* o = &f->k[i];
            int tt = ttypetag(o);
            dumpByte(ref D, tt);
            switch (tt)
            {
                case LUA_VNUMFLT:
                    dumpNumber(ref D, fltvalue(o));
                    break;
                case LUA_VNUMINT:
                    dumpInteger(ref D, ivalue(o));
                    break;
                case LUA_VSHRSTR:
                case LUA_VLNGSTR:
                    dumpString(ref D, tsvalue(o));
                    break;
                default:
                    Debug.Assert(tt is LUA_VNIL or LUA_VFALSE or LUA_VTRUE);
                    break;
            }
        }
    }

    private static void dumpProtos(ref DumpState D, Proto* f)
    {
        int n = f->sizep;
        dumpInt(ref D, n);
        for (int i = 0; i < n; i++)
        {
            dumpFunction(ref D, f->p[i]);
        }
    }

    private static void dumpUpvalues(ref DumpState D, Proto* f)
    {
        int i, n = f->sizeupvalues;
        dumpInt(ref D, n);
        for (i = 0; i < n; i++)
        {
            dumpByte(ref D, f->upvalues[i].instack);
            dumpByte(ref D, f->upvalues[i].idx);
            dumpByte(ref D, f->upvalues[i].kind);
        }
    }

    private static void dumpDebug(ref DumpState D, Proto* f)
    {
        int n = D.strip ? 0 : f->sizelineinfo;
        dumpInt(ref D, n);
        if (f->lineinfo != null)
        {
            dumpVector(ref D, f->lineinfo, n);
        }

        n = D.strip ? 0 : f->sizeabslineinfo;
        dumpInt(ref D, n);
        if (n > 0)
        {
            // 'abslineinfo' is an array of structures of int's
            dumpAlign(ref D, sizeof(int));
            dumpVector(ref D, f->abslineinfo, n);
        }

        n = D.strip ? 0 : f->sizelocvars;
        dumpInt(ref D, n);
        for (int i = 0; i < n; i++)
        {
            dumpString(ref D, f->locvars[i].varname);
            dumpInt(ref D, f->locvars[i].startpc);
            dumpInt(ref D, f->locvars[i].endpc);
        }

        n = D.strip ? 0 : f->sizeupvalues;
        dumpInt(ref D, n);
        for (int i = 0; i < n; i++)
        {
            dumpString(ref D, f->upvalues[i].name);
        }
    }

    private static void dumpFunction(ref DumpState D, Proto* f)
    {
        dumpInt(ref D, f->linedefined);
        dumpInt(ref D, f->lastlinedefined);
        dumpByte(ref D, f->numparams);
        dumpByte(ref D, f->flag);
        dumpByte(ref D, f->maxstacksize);
        dumpCode(ref D, f);
        dumpConstants(ref D, f);
        dumpUpvalues(ref D, f);
        dumpProtos(ref D, f);
        dumpString(ref D, D.strip ? null : f->source);
        dumpDebug(ref D, f);
    }

    private static void dumpNumInfo<T>(ref DumpState D, T value)
        where T : unmanaged
    {
        dumpByte(ref D, sizeof(T));
        dumpVar(ref D, value);
    }

    private static void dumpHeader(ref DumpState D)
    {
        dumpLiteral(ref D, LUA_SIGNATURE);
        dumpByte(ref D, LUAC_VERSION);
        dumpByte(ref D, LUAC_FORMAT);
        dumpLiteral(ref D, LUAC_DATA);
        dumpNumInfo(ref D, LUAC_INT);
        dumpNumInfo(ref D, LUAC_INST);
        dumpNumInfo<long>(ref D, LUAC_INT);
        dumpNumInfo(ref D, LUAC_NUM);
    }

    /// <summary>
    /// dump Lua function as precompiled chunk
    /// </summary>
    private static int luaU_dump(lua_State* L, Proto* f, lua_Writer w, void* data, bool strip)
    {
        DumpState D = new()
        {
            h = luaH_new(L), // aux. table to keep strings already dumped
            L = L,
            writer = w,
            offset = 0,
            data = data,
            strip = strip,
            status = 0,
            nstr = 0,
        };
        sethvalue2s(L, L->top.p, D.h); // anchor it
        L->top.p++;
        dumpHeader(ref D);
        dumpByte(ref D, f->sizeupvalues);
        dumpFunction(ref D, f);
        dumpBlock(ref D, null, 0); // signal end of dump
        return D.status;
    }
}
