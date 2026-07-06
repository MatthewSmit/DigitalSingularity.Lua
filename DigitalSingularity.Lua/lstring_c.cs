namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    /*
    ** Maximum size for string table.
    */
    private static readonly int MAXSTRTB = (int)luaM_limitN<nint>(int.MaxValue);

    /*
    ** Initial size for the string table (must be power of 2).
    ** The Lua core alone registers ~50 strings (reserved words +
    ** metaevent keys + a few others). Libraries would typically add
    ** a few dozens more.
    */
    private const int MINSTRTABSIZE =
        #if LUA_TEST
            2;
        #else
            128;
        #endif

    /*
    ** generic equality for strings
    */
    internal static partial bool luaS_eqstr(TString* a, TString* b)
    {
        byte* s1 = getlstr(a, out long len1);
        byte* s2 = getlstr(b, out long len2);
        return len1 == len2 && /* equal length and ... */
               memcmp(s1, s2, len1) == 0; /* equal contents */
    }

    private static uint luaS_hash(byte* str, long l, uint seed)
    {
        uint h = seed ^ (uint)l;
        for (; l > 0; l--)
        {
            h ^= (h << 5) + (h >> 2) + str[l - 1];
        }

        return h;
    }

    internal static partial uint luaS_hashlongstr(TString* ts)
    {
        Debug.Assert(ts->tt == LUA_VLNGSTR);
        if (ts->extra == 0)
        {
            /* no hash? */
            long len = ts->u.lnglen;
            ts->hash = luaS_hash(getlngstr(ts), len, ts->hash);
            ts->extra = 1; /* now it has its hash */
        }

        return ts->hash;
    }

    private static void tablerehash(TString** vect, int osize, int nsize)
    {
        for (int i = osize; i < nsize; i++) /* clear new elements */
        {
            vect[i] = null;
        }

        for (int i = 0; i < osize; i++)
        {
            /* rehash old part of the array */
            TString* p = vect[i];
            vect[i] = null;
            while (p != null)
            {
                // /* for each string in the list */
                TString* hnext = p->u.hnext; /* save next */
                uint h = lmod(p->hash, nsize); /* new position */
                p->u.hnext = vect[h]; /* chain it into array */
                vect[h] = p;
                p = hnext;
            }
        }
    }

    /*
     ** Resize the string table. If allocation fails, keep the current size.
     ** (This can degrade performance, but any non-zero size should work
     ** correctly.)
     */
    internal static partial void luaS_resize(lua_State* L, int nsize)
    {
        stringtable* tb = &G(L)->strt;
        int osize = tb->size;
        if (nsize < osize) /* shrinking table? */
        {
            tablerehash(tb->hash, osize, nsize); /* depopulate shrinking part */
        }

        TString** newvect = luaM_reallocvector2<TString>(L, tb->hash, osize, nsize);
        if (newvect == null)
        {
            /* reallocation failed? */
            if (nsize < osize) /* was it shrinking table? */
            {
                tablerehash(tb->hash, nsize, osize); /* restore to original size */
            }

            /* leave table as it was */
        }
        else
        {
            /* allocation succeeded */
            tb->hash = newvect;
            tb->size = nsize;
            if (nsize > osize)
            {
                tablerehash(newvect, osize, nsize); /* rehash for new size */
            }
        }
    }

    /*
    ** Clear API string cache. (Entries cannot be empty, so fill them with
    ** a non-collectable string.)
    */
    internal static partial void luaS_clearcache(global_State* g)
    {
        for (int i = 0; i < STRCACHE_N; i++)
        {
            for (int j = 0; j < STRCACHE_M; j++)
            {
                if (iswhite((GCObject*)g->strcache[i, j])) /* will entry be collected? */
                {
                    g->strcache[i, j] = g->memerrmsg; /* replace it with something fixed */
                }
            }
        }
    }

    /*
     ** Initialise the string table and the string cache
     */
    private static partial void luaS_init(lua_State* L)
    {
        global_State* g = G(L);
        stringtable* tb = &G(L)->strt;
        tb->hash = luaM_newvector2<TString>(L, MINSTRTABSIZE);
        tablerehash(tb->hash, 0, MINSTRTABSIZE); /* clear array */
        tb->size = MINSTRTABSIZE;
        /* pre-create memory-error message */
        g->memerrmsg = luaS_newliteral(L, MEMERRMSG);
        luaC_fix(L, obj2gco(g->memerrmsg)); /* it should never be collected */
        for (int i = 0; i < STRCACHE_N; i++) /* fill cache with valid strings */
        {
            for (int j = 0; j < STRCACHE_M; j++)
            {
                g->strcache[i, j] = g->memerrmsg;
            }
        }
    }

    internal static partial long luaS_sizelngstr(long len, int kind)
    {
        switch (kind)
        {
            case LSTRREG: /* regular long string */
                /* don't need 'falloc'/'ud', but need space for content */
                return TString_falloc_offset + len + 1;
            
            case LSTRFIX: /* fixed external long string */
                /* don't need 'falloc'/'ud' */
                return TString_falloc_offset;
            
            case LSTRMEM: /* external long string with deallocation */
                return sizeof(TString);
            
            default:
                throw new InvalidOperationException("Invalid string kind");
        }
    }

    /*
     ** creates a new string object
     */
    private static TString* createstrobj(lua_State* L, long totalsize, byte tag, uint h)
    {
        GCObject* o = luaC_newobj(L, tag, totalsize);
        TString* ts = gco2ts(o);
        ts->hash = h;
        ts->extra = 0;
        return ts;
    }

    internal static partial TString* luaS_createlngstrobj(lua_State* L, long l)
    {
        long totalsize = luaS_sizelngstr(l, LSTRREG);
        TString* ts = createstrobj(L, totalsize, LUA_VLNGSTR, G(L)->seed);
        ts->u.lnglen = l;
        ts->shrlen = LSTRREG;  /* signals that it is a regular long string */
        ts->contents = (byte*)ts + TString_falloc_offset;
        ts->contents[l] = 0;  /* ending 0 */
        return ts;
    }

    private static partial void luaS_remove(lua_State* L, TString* ts)
    {
        stringtable* tb = &G(L)->strt;
        TString** p = &tb->hash[lmod(ts->hash, tb->size)];
        while (*p != ts) /* find previous element */
        {
            p = &(*p)->u.hnext;
        }

        *p = (*p)->u.hnext; /* remove element from its list */
        tb->nuse--;
    }

    private static void growstrtab(lua_State* L, stringtable* tb)
    {
        if (tb->nuse == int.MaxValue)
        {
            /* too many strings? */
            //     luaC_fullgc(L, 1);  /* try to free some... */
            //     if (tb->nuse == INT_MAX)  /* still too many? */
            //       luaM_error(L);  /* cannot even create a message... */
            throw new NotImplementedException();
        }

        if (tb->size <= MAXSTRTB / 2) /* can grow string table? */
        {
            luaS_resize(L, tb->size * 2);
        }
    }

    /*
     ** Checks whether short string exists and reuses it or creates a new one.
     */
    private static TString* internshrstr(lua_State* L, byte* str, long l)
    {
        global_State* g = G(L);
        stringtable* tb = &g->strt;
        uint h = luaS_hash(str, l, g->seed);
        TString** list = &tb->hash[lmod(h, tb->size)];
        Debug.Assert(str != null); /* otherwise 'memcmp'/'memcpy' are undefined */

        TString* ts;
        for (ts = *list; ts != null; ts = ts->u.hnext)
        {
            if (l == (uint)ts->shrlen &&
                memcmp(str, getshrstr(ts), l) == 0)
            {
                /* found! */
                if (isdead(g, obj2gco(ts))) /* dead (but not collected yet)? */
                {
                    changewhite(obj2gco(ts)); /* resurrect it */
                }

                return ts;
            }
        }

        /* else must create a new string */
        if (tb->nuse >= tb->size)
        {
            /* need to grow string table? */
            growstrtab(L, tb);
            list = &tb->hash[lmod(h, tb->size)]; /* rehash with new size */
        }

        ts = createstrobj(L, sizestrshr(l), LUA_VSHRSTR, h);
        ts->shrlen = (sbyte)l;
        getshrstr(ts)[l] = 0; /* ending 0 */
        memcpy(getshrstr(ts), str, l);
        ts->u.hnext = *list;
        *list = ts;
        tb->nuse++;
        return ts;
    }

    /*
     ** new string (with explicit length)
     */
    internal static partial TString* luaS_newlstr(lua_State* L, byte* str, int l)
    {
        if (l <= LUAI_MAXSHORTLEN) /* short string? */
        {
            return internshrstr(L, str, l);
        }

        if (l >= long.MaxValue - sizeof(TString))
        {
            luaM_toobig(L);
        }

        TString* ts = luaS_createlngstrobj(L, l);
        memcpy(getlngstr(ts), str, l);
        return ts;
    }

    /*
     ** Create or reuse a zero-terminated string, first checking in the
     ** cache (using the string address as a key). The cache can contain
     ** only zero-terminated strings, so it is safe to use 'strcmp' to
     ** check hits.
     */
    private static partial TString* luaS_new(lua_State* L, byte* str)
    {
        uint i = (uint)((nint)str % STRCACHE_N); /* hash */
        TString** p = (TString**)Unsafe.AsPointer(ref G(L)->strcache) + i;
        for (int j = 0; j < STRCACHE_M; j++)
        {
            if (strcmp(str, getstr(p[j])) == 0) /* hit? */
            {
                return p[j]; /* that is it */
            }
        }

        /* normal route */
        for (int j = STRCACHE_M - 1; j > 0; j--)
        {
            p[j] = p[j - 1]; /* move out last element */
        }
        
        /* new element is first in the list */
        p[0] = luaS_newlstr(L, str, strlen(str));
        return p[0];
    }

    internal static partial TString* luaS_new(lua_State* L, string str)
    {
        if (str.Length == 0)
        {
            byte tmp = 0;
            return luaS_new(L, &tmp);
        }

        byte[] data = Encoding.UTF8.GetBytes(str);
        fixed (byte* dataPtr = data)
        {
            return luaS_new(L, dataPtr);
        }
    }

    internal static partial Udata* luaS_newudata(lua_State* L, long s, ushort nuvalue)
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
//   const char *s;
//    size_t len;
        public TString* ts; /* output */
    }

    private static void f_newext(lua_State* L, void* ud)
    {
        NewExt* ne = (NewExt*)ud;
        long size = luaS_sizelngstr(0, ne->kind);
        ne->ts = createstrobj(L, size, LUA_VLNGSTR, G(L)->seed);
    }

    internal static partial TString* luaS_newextlstr(lua_State* L, byte* s, int len, lua_Alloc falloc, void* ud)
    {
        NewExt ne;
        if (falloc == null)
        {
            ne.kind = LSTRFIX;
            f_newext(L, &ne); /* just create header */
        }
        else
        {
            ne.kind = LSTRMEM;
            if (luaD_rawrunprotected(L, f_newext, &ne) != LUA_OK)
            {
                /* mem. error? */
                falloc(ud, s, len + 1, 0); /* free external string */
                luaM_error(L); /* re-raise memory error */
            }

            ne.ts->falloc = falloc;
            ne.ts->ud = ud;
        }

        ne.ts->shrlen = ne.kind;
        ne.ts->u.lnglen = len;
        ne.ts->contents = s;
        return ne.ts;
    }

    /*
    ** Normalise an external string: If it is short, internalise it.
    */
    internal static partial TString* luaS_normstr(lua_State* L, TString* ts)
    {
        long len = ts->u.lnglen;
        if (len > LUAI_MAXSHORTLEN)
        {
            return ts; /* long string; keep the original */
        }

        byte* str = getlngstr(ts);
        return internshrstr(L, str, len);
    }
}
