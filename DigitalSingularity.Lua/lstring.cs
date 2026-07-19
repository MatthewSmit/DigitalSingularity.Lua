namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    /// <summary>
    /// Memory-allocation error message must be preallocated (it cannot
    /// be created after memory is exhausted)
    /// </summary>
    private const string MEMERRMSG = "not enough memory";

    /// <summary>
    /// Maximum length for short strings, that is, strings that are
    /// internalised. (Cannot be smaller than reserved words or tags for
    /// metamethods, as these strings must be internalized;
    /// #("function") = 8, #("__newindex") = 10.)
    /// </summary>
    private const int LUAI_MAXSHORTLEN = 40;

    private static readonly int TStringContentsOffset = Marshal.OffsetOf<TString>(nameof(TString.contents)).ToInt32();

    /// <summary>
    /// Size of a short TString: Size of the header plus space for the string
    /// itself (including final '\0').
    /// </summary>
    private static long sizestrshr(long l)
    {
        return TStringContentsOffset + l + 1;
    }

    internal static TString* luaS_newliteral(lua_State* L, string s)
    {
        byte[] data = Encoding.UTF8.GetBytes(s);
        return luaS_newlstr(L, data);
    }

    /// <summary>
    /// test whether a string is a reserved word
    /// </summary>
    private static bool isreserved(TString* s)
    {
        return strisshr(s) && s->extra > 0;
    }

    /// <summary>
    /// equality for short strings, which are always internalised
    /// </summary>
    private static bool eqshrstr(TString* a, TString* b)
    {
        Debug.Assert(a->tt == LUA_VSHRSTR);
        return a == b;
    }
    
    /// <summary>
    /// Maximum size for string table.
    /// </summary>
    private static readonly int MAXSTRTB = (int)luaM_limitN<nint>(int.MaxValue);

    /// <summary>
    /// Initial size for the string table (must be power of 2).
    /// The Lua core alone registers ~50 strings (reserved words +
    /// metaevent keys + a few others). Libraries would typically add
    /// a few dozens more.
    /// </summary>
    private const int MINSTRTABSIZE =
        #if LUA_TEST
            2;
        #else
            128;
        #endif

    /// <summary>
    /// generic equality for strings
    /// </summary>
    internal static bool luaS_eqstr(TString* a, TString* b)
    {
        ReadOnlySpan<byte> s1 = getlstr(a);
        ReadOnlySpan<byte> s2 = getlstr(b);
        return s1.SequenceEqual(s2);
    }

    private static uint luaS_hash(ReadOnlySpan<byte> str, uint seed)
    {
        uint h = seed ^ (uint)str.Length;
        int l = str.Length;
        for (; l > 0; l--)
        {
            h ^= (h << 5) + (h >> 2) + str[l - 1];
        }

        return h;
    }

    internal static uint luaS_hashlongstr(TString* ts)
    {
        Debug.Assert(ts->tt == LUA_VLNGSTR);
        if (ts->extra == 0)
        {
            // no hash?
            ts->hash = luaS_hash(new ReadOnlySpan<byte>(getlngstr(ts), ts->u.lnglen), ts->hash);
            ts->extra = 1; // now it has its hash
        }

        return ts->hash;
    }

    private static void tablerehash(TString** vect, int osize, int nsize)
    {
        for (int i = osize; i < nsize; i++) // clear new elements
        {
            vect[i] = null;
        }

        for (int i = 0; i < osize; i++)
        {
            // rehash old part of the array
            TString* p = vect[i];
            vect[i] = null;
            while (p != null)
            {
                // for each string in the list
                TString* hnext = p->u.hnext; // save next
                uint h = lmod(p->hash, nsize); // new position
                p->u.hnext = vect[h]; // chain it into array
                vect[h] = p;
                p = hnext;
            }
        }
    }

    /// <summary>
    /// Resize the string table. If allocation fails, keep the current size.
    /// (This can degrade performance, but any non-zero size should work
    /// correctly.)
    /// </summary>
    internal static void luaS_resize(lua_State* L, int nsize)
    {
        stringtable* tb = &G(L)->strt;
        int osize = tb->size;
        if (nsize < osize) // shrinking table?
        {
            tablerehash(tb->hash, osize, nsize); // depopulate shrinking part
        }

        TString** newvect = luaM_reallocvector2<TString>(L, tb->hash, osize, nsize);
        if (newvect == null)
        {
            // reallocation failed?
            if (nsize < osize) // was it shrinking table?
            {
                tablerehash(tb->hash, nsize, osize); // restore to original size
            }

            // leave table as it was
        }
        else
        {
            // allocation succeeded
            tb->hash = newvect;
            tb->size = nsize;
            if (nsize > osize)
            {
                tablerehash(newvect, osize, nsize); // rehash for new size
            }
        }
    }

    /// <summary>
    /// Clear API string cache. (Entries cannot be empty, so fill them with
    /// a non-collectable string.)
    /// </summary>
    internal static void luaS_clearcache(global_State* g)
    {
        for (int i = 0; i < STRCACHE_N; i++)
        {
            for (int j = 0; j < STRCACHE_M; j++)
            {
                if (iswhite((GCObject*)g->strcache[i, j])) // will entry be collected?
                {
                    g->strcache[i, j] = g->memerrmsg; // replace it with something fixed
                }
            }
        }
    }

    /// <summary>
    /// Initialise the string table and the string cache
    /// </summary>
    private static void luaS_init(lua_State* L)
    {
        global_State* g = G(L);
        stringtable* tb = &G(L)->strt;
        tb->hash = luaM_newvector2<TString>(L, MINSTRTABSIZE);
        tablerehash(tb->hash, 0, MINSTRTABSIZE); // clear array
        tb->size = MINSTRTABSIZE;
        // pre-create memory-error message
        g->memerrmsg = luaS_newliteral(L, MEMERRMSG);
        luaC_fix(L, obj2gco(g->memerrmsg)); // it should never be collected
        for (int i = 0; i < STRCACHE_N; i++) // fill cache with valid strings
        {
            for (int j = 0; j < STRCACHE_M; j++)
            {
                g->strcache[i, j] = g->memerrmsg;
            }
        }
    }

    internal static int luaS_sizelngstr(int len, int kind)
    {
        return kind switch
        {
            // regular long string
            // don't need 'falloc'/'ud', but need space for content
            LSTRREG => TString_falloc_offset + len + 1,
            
            // fixed external long string
            // don't need 'falloc'/'ud'
            LSTRFIX => TString_falloc_offset,
            
             // external long string with deallocation
            LSTRMEM => sizeof(TString),
            
            _ => throw new InvalidOperationException("Invalid string kind"),
        };
    }

    /// <summary>
    /// creates a new string object
    /// </summary>
    private static TString* createstrobj(lua_State* L, long totalsize, byte tag, uint h)
    {
        GCObject* o = luaC_newobj(L, tag, totalsize);
        TString* ts = gco2ts(o);
        ts->hash = h;
        ts->extra = 0;
        return ts;
    }

    internal static TString* luaS_createlngstrobj(lua_State* L, int l)
    {
        int totalsize = luaS_sizelngstr(l, LSTRREG);
        TString* ts = createstrobj(L, totalsize, LUA_VLNGSTR, G(L)->seed);
        ts->u.lnglen = l;
        ts->shrlen = LSTRREG; // signals that it is a regular long string
        ts->contents = (byte*)ts + TString_falloc_offset;
        ts->contents[l] = 0; // ending 0
        return ts;
    }

    private static void luaS_remove(lua_State* L, TString* ts)
    {
        stringtable* tb = &G(L)->strt;
        TString** p = &tb->hash[lmod(ts->hash, tb->size)];
        while (*p != ts) // find previous element
        {
            p = &(*p)->u.hnext;
        }

        *p = (*p)->u.hnext; // remove element from its list
        tb->nuse--;
    }

    private static void growstrtab(lua_State* L, stringtable* tb)
    {
        if (tb->nuse == int.MaxValue)
        {
            // too many strings?
            luaC_fullgc(L, true); // try to free some...
            if (tb->nuse == int.MaxValue) // still too many?
            {
                luaM_error(L); // cannot even create a message...
            }
        }

        if (tb->size <= MAXSTRTB / 2) // can grow string table?
        {
            luaS_resize(L, tb->size * 2);
        }
    }

    /// <summary>
    /// Checks whether short string exists and reuses it or creates a new one.
    /// </summary>
    private static TString* internshrstr(lua_State* L, ReadOnlySpan<byte> str)
    {
        global_State* g = G(L);
        stringtable* tb = &g->strt;
        uint h = luaS_hash(str, g->seed);
        ref TString* list = ref tb->hash[lmod(h, tb->size)];

        TString* ts;
        for (ts = list; ts != null; ts = ts->u.hnext)
        {
            if (str.Length == (uint)ts->shrlen)
            {
                fixed (byte* ptr = str)
                {
                    if (memcmp(ptr, getshrstr(ts), str.Length) == 0)
                    {
                        // found!
                        if (isdead(g, obj2gco(ts))) // dead (but not collected yet)?
                        {
                            changewhite(obj2gco(ts)); // resurrect it
                        }

                        return ts;
                    }
                }
            }
        }

        // else must create a new string
        if (tb->nuse >= tb->size)
        {
            // need to grow string table?
            growstrtab(L, tb);
            list = ref tb->hash[lmod(h, tb->size)]; // rehash with new size
        }

        ts = createstrobj(L, sizestrshr(str.Length), LUA_VSHRSTR, h);
        ts->shrlen = (sbyte)str.Length;
        getshrstr(ts)[str.Length] = 0; // ending 0
        str.CopyTo(new Span<byte>(getshrstr(ts), str.Length));
        ts->u.hnext = list;
        list = ts;
        tb->nuse++;
        return ts;
    }

    /// <summary>
    /// New string (with explicit length)
    /// </summary>
    [Obsolete]
    internal static TString* luaS_newlstr(lua_State* L, byte* str, int l)
    {
        if (l <= LUAI_MAXSHORTLEN) // short string?
        {
            return internshrstr(L, new ReadOnlySpan<byte>(str, l));
        }

        if (l >= long.MaxValue - sizeof(TString))
        {
            luaM_toobig(L);
        }

        TString* ts = luaS_createlngstrobj(L, l);
        memcpy(getlngstr(ts), str, l);
        return ts;
    }

    internal static TString* luaS_newlstr(lua_State* L, ReadOnlySpan<byte> str)
    {
        fixed (byte* ptr = str)
        {
            return luaS_newlstr(L, ptr, str.Length);
        }
    }

    /// <summary>
    /// Create or reuse a zero-terminated string, first checking in the
    /// cache (using the string address as a key). The cache can contain
    /// only zero-terminated strings, so it is safe to use 'strcmp' to
    /// check hits.
    /// </summary>
    private static TString* luaS_new(lua_State* L, byte* str)
    {
        uint i = (uint)((nint)str % STRCACHE_N); // hash
        TString** p = (TString**)Unsafe.AsPointer(ref G(L)->strcache) + i;
        for (int j = 0; j < STRCACHE_M; j++)
        {
            if (strcmp(str, getstrptr(p[j])) == 0) // hit?
            {
                return p[j]; // that is it
            }
        }

        // normal route
        for (int j = STRCACHE_M - 1; j > 0; j--)
        {
            p[j] = p[j - 1]; // move out last element
        }
        
        // new element is first in the list
        p[0] = luaS_newlstr(L, str, strlen(str));
        return p[0];
    }

    internal static TString* luaS_new(lua_State* L, string str)
    {
        if (str.Length == 0)
        {
            byte tmp = 0;
            return luaS_new(L, &tmp);
        }

        return luaS_newlstr(L, Encoding.UTF8.GetBytes(str));
    }

    internal static Udata* luaS_newudata(lua_State* L, long s, ushort nuvalue)
    {
        if (s > long.MaxValue - udatamemoffset(nuvalue))
        {
            luaM_toobig(L);
        }

        GCObject* o = luaC_newobj(L, LUA_VUSERDATA, sizeudata(nuvalue, s));
        Udata* u = gco2u(o);
        u->len = s;
        u->nuvalue = nuvalue;
        u->metatable = null;
        for (int i = 0; i < nuvalue; i++)
        {
            setnilvalue(&((TValue*)u->uv)[i]);
        }

        return u;
    }

    private struct NewExt
    {
        public sbyte kind;
        /// <summary>
        /// const char *s;
        /// size_t len;
        /// </summary>
        public TString* ts; // output
    }

    private static void f_newext(lua_State* L, void* ud)
    {
        NewExt* ne = (NewExt*)ud;
        long size = luaS_sizelngstr(0, ne->kind);
        ne->ts = createstrobj(L, size, LUA_VLNGSTR, G(L)->seed);
    }

    internal static TString* luaS_newextlstr(lua_State* L, byte* s, int len, AllocFunction falloc, void* ud)
    {
        NewExt ne;
        if (falloc == default)
        {
            ne.kind = LSTRFIX;
            f_newext(L, &ne); // just create header
        }
        else
        {
            ne.kind = LSTRMEM;
            if (luaD_rawrunprotected(L, f_newext, &ne) != LUA_OK)
            {
                // mem. error?
                falloc.Call(ud, s, len + 1, 0); // free external string
                luaM_error(L); // re-raise memory error
            }

            ne.ts->falloc = falloc;
            ne.ts->ud = ud;
        }

        ne.ts->shrlen = ne.kind;
        ne.ts->u.lnglen = len;
        ne.ts->contents = s;
        return ne.ts;
    }

    /// <summary>
    /// Normalise an external string: If it is short, internalise it.
    /// </summary>
    internal static TString* luaS_normstr(lua_State* L, TString* ts)
    {
        int len = ts->u.lnglen;
        if (len > LUAI_MAXSHORTLEN)
        {
            return ts; // long string; keep the original
        }

        byte* str = getlngstr(ts);
        return internshrstr(L, new ReadOnlySpan<byte>(str, len));
    }
}
