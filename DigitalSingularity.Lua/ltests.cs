namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
#if LUA_TEST
    /* memory-allocator control variables */
    internal struct Memcontrol
    {
        public bool failnext;
        public long numblocks;
        public long total;
        public long maxmem;
        public long memlimit;
        public long countlimit;
        public fixed uint objcount[LUA_NUMTYPES];
    }

    internal static Memcontrol* l_memcontrol = (Memcontrol*)NativeMemory.AllocZeroed((nuint)sizeof(Memcontrol));

    private static void luai_tracegc(lua_State* L, bool f)
    {
        luai_tracegctest(L, f);
    }

// /*
// ** generic variable for debug tricks TODO
// */
// extern void *l_Trick;

    /* test for lock/unlock */
    private struct L_EXTRA
    {
        public int @lock;
        public int* plock;
    }

    public static void luai_openlibs(lua_State* L)
    {
        luaL_openlibs(L);
        luaL_requiref(L, "T", &luaB_opentests, true);
        lua_pop(L, 1);
    }
    
// void *l_Trick = 0;

    private static TValue* obj_at(lua_State* L, int k)
    {
        return s2v(L->ci->func.p + k);
    }

    private static void setnameval(lua_State* L, string name, int val)
    {
        lua_pushinteger(L, val);
        lua_setfield(L, -2, name);
    }

    private static void pushobject(lua_State* L, TValue* o)
    {
        setobj2s(L, L->top.p, o);
        api_incr_top(L);
    }

    private static void badexit(string fmt, ReadOnlySpan<char> s1, ReadOnlySpan<char> s2)
    {
//   fprintf(stderr, fmt, s1);
//   if (s2)
//     fprintf(stderr, "extra info: %s\n", s2);
//   /* avoid assertion failures when exiting */
//   l_memcontrol.numblocks = l_memcontrol.total = 0;
//   exit(EXIT_FAILURE);
        throw new NotImplementedException();
    }

    private static int tpanic(lua_State* L)
    {
//   const char *msg = (lua_type(L, -1) == LUA_TSTRING)
//                   ? lua_tostring(L, -1)
//                   : "error object is not a string";
//   return (badexit("PANIC: unprotected error in call to Lua API (%s)\n",
//                    msg, null),
//           0);  /* do not return to Lua */
        throw new NotImplementedException();
    }

    private static string warnf_buff = "";
    private static bool warnf_onoff;
    private static int warnf_mode; /* start in normal mode */
    private static bool warnf_lasttocont;

    /*
     ** Warning function for tests. First, it concatenates all parts of
     ** a warning in buffer 'buff'. Then, it has three modes:
     ** - 0.normal: messages starting with '#' are shown on standard output;
     ** - other messages abort the tests (they represent real warning
     ** conditions; the standard tests should not generate these conditions
     ** unexpectedly);
     ** - 1.allow: all messages are shown;
     ** - 2.store: all warnings go to the global '_WARN';
     */
    private static void warnf(void* ud, ReadOnlySpan<char> msg, bool tocont)
    {
        if (!warnf_lasttocont && !tocont && msg.StartsWith('@'))
        {
            /* control message? */
            if (warnf_buff.Length > 0)
            {
                badexit("Control warning during warning: %s\naborting...\n", msg, warnf_buff);
            }

            if (msg is "@off")
            {
                warnf_onoff = false;
            }
            else if (msg is "@on")
            {
                warnf_onoff = true;
            }
            else if (msg is "@normal")
            {
                warnf_mode = 0;
            }
            else if (msg is "@allow")
            {
                warnf_mode = 1;
            }
            else if (msg is "@store")
            {
                warnf_mode = 2;
            }
            else
            {
                badexit(
                    "Invalid control warning in test mode: %s\naborting...\n",
                    msg,
                    null);
            }

            return;
        }

        lua_State* L = (lua_State*)ud;
        warnf_lasttocont = tocont;
        // if (strlen(msg) >= warnf_buff.Length - strlen(warnf_buff))
        // {
        //     badexit("warnf-buffer overflow (%s)\n", msg, warnf_buff);
        // }

        warnf_buff += new string(msg); /* add new message to current warning */
        if (!tocont)
        {
            /* message finished? */
            lua_unlock(L);
            luaL_checkstack(L, 1, "warn stack space");
            lua_getglobal(L, "_WARN");
            if (!lua_toboolean(L, -1))
            {
                lua_pop(L, 1); /* ok, no previous unexpected warning */
            }
            else
            {
                badexit(
                    "Unhandled warning in store mode: %s\naborting...\n",
                    lua_tonetstring(L, -1) ?? "",
                    warnf_buff);
            }

            lua_lock(L);
            switch (warnf_mode)
            {
                case 0:
                    /* normal */
                    if (warnf_buff[0] != '#' && warnf_onoff) /* unexpected warning? */
                    {
                        badexit(
                            "Unexpected warning in test mode: %s\naborting...\n",
                            warnf_buff,
                            null);
                    }

                    goto case 1;

                case 1:
                    /* allow */
                    if (warnf_onoff)
                    {
                        Console.Error.WriteLine("Lua warning: {0}", warnf_buff); /* print warning */
                    }

                    break;

                case 2:
                    /* store */
                    lua_unlock(L);
                    luaL_checkstack(L, 1, "warn stack space");
                    lua_pushstring(L, warnf_buff);
                    lua_setglobal(L, "_WARN"); /* assign message to global '_WARN' */
                    lua_lock(L);
                    break;
            }

            warnf_buff = ""; /* prepare buffer for next warning */
        }
    }

    /*
     ** {======================================================================
     ** Controlled version for realloc.
     ** =======================================================================
     */

    private const byte MARK = 0x55; // 01010101 (a nice pattern)

    private struct memHeader
    {
        public long size;
        public int type;
    }

    // full memory check
    private const int MARKSIZE = 16; // size of marks after each block

    private static void fillmem(void* mem, long size)
    {
        NativeMemory.Fill(mem, (nuint)size, unchecked((byte)-MARK));
    }

    private static void freeblock(Memcontrol* mc, memHeader* block)
    {
        if (block != null)
        {
            long size = block->size;
            for (int i = 0; i < MARKSIZE; i++) /* check marks after block */
            {
                Debug.Assert(*((byte*)(block + 1) + size + i) == MARK);
            }

            mc->objcount[block->type]--;
            fillmem(block, sizeof(memHeader) + size + MARKSIZE); /* erase block */
            NativeMemory.Free(block); /* actually free block */
            mc->numblocks--; /* update counts */
            mc->total -= size;
        }
    }

    private static void* debug_realloc(void* ud, void* b, long oldsize, long size)
    {
        Memcontrol* mc = (Memcontrol*)ud;
        memHeader* block = (memHeader*)b;
        if (mc->memlimit == 0)
        {
            // first time? 
            string? limit = Environment.GetEnvironmentVariable("MEMLIMIT"); // initialise memory limit
            mc->memlimit = string.IsNullOrEmpty(limit)
                ? long.MaxValue
                : long.Parse(limit, CultureInfo.InvariantCulture);
        }

        int type;
        if (block == null)
        {
            type = oldsize < LUA_NUMTYPES ? (int)oldsize : 0;
            oldsize = 0;
        }
        else
        {
            block--; /* go to real header */
            type = block->type;
            Debug.Assert(oldsize == block->size);
        }

        if (size == 0)
        {
            freeblock(mc, block);
            return null;
        }

        if (mc->failnext)
        {
            mc->failnext = false;
            return null; // fake a single memory allocation error
        }

        if (mc->countlimit >= 0 && size != oldsize)
        {
            /* count limit in use? */
            if (mc->countlimit == 0)
            {
                return null; /* fake a memory allocation error */
            }

            mc->countlimit--;
        }

        if (size > oldsize && mc->total + size - oldsize > mc->memlimit)
        {
            return null; // fake a memory allocation error
        }

        long commonsize = oldsize < size ? oldsize : size;
        long realsize = sizeof(memHeader) + size + MARKSIZE;
        if (realsize < size)
        {
            return null; // arithmetic overflow!
        }

        memHeader* newblock; // alloc a new block
        try
        {
            newblock = (memHeader*)NativeMemory.Alloc((nuint)realsize);
        }
        catch (OutOfMemoryException)
        {
            return null; // really out of memory?
        }

        if (block != null)
        {
            memcpy(newblock + 1, block + 1, commonsize); /* copy old contents */
            freeblock(mc, block); /* erase (and check) old copy */
        }

        // initialise new part of the block with something weird
        fillmem((byte*)(newblock + 1) + commonsize, size - commonsize);
        // initialise marks after block
        for (int i = 0; i < MARKSIZE; i++)
        {
            *((byte*)(newblock + 1) + size + i) = MARK;
        }

        newblock->size = size;
        newblock->type = type;
        mc->total += size;
        if (mc->total > mc->maxmem)
        {
            mc->maxmem = mc->total;
        }

        mc->numblocks++;
        mc->objcount[type]++;
        return newblock + 1;
    }

    /*
    ** {=====================================================================
    ** Functions to check memory consistency.
    ** Most of these checks are done through asserts, so this code does
    ** not make sense with asserts off. For this reason, it uses 'assert'
    ** directly, instead of 'Debug.Assert'.
    ** ======================================================================
    */
    
    /*
    ** Check GC invariants. For incremental mode, a black object cannot
    ** point to a white one. For generational mode, really old objects
    ** cannot point to young objects. Both old1 and touched2 objects
    ** cannot point to new objects (but can point to survivals).
    ** (Threads and open upvalues, despite being marked "really old",
    ** continue to be visited in all collections, and therefore can point to
    ** new objects. They, and only they, are old but gray.)
    */
    private static bool testobjref1(global_State* g, GCObject* f, GCObject* t)
    {
        if (isdead(g, t))
        {
            return false;
        }

        if (issweepphase(g))
        {
            return true; /* no invariants */
        }

        if (g->gckind != KGC_GENMINOR)
        {
            return !(isblack(f) && iswhite(t)); /* basic incremental invariant */
        }

        /* generational mode */
        if ((getage(f) == G_OLD && isblack(f)) && !isold(t))
        {
            return false;
        }

        if ((getage(f) == G_OLD1 || getage(f) == G_TOUCHED2) &&
            getage(t) == G_NEW)
        {
            return false;
        }

        return true;
    }

// static void printobj (global_State *g, GCObject *o) {
//   printf("||%s(%p)-%c%c(%02X)||",
//            ttypename(novariant(o->tt)), (void *)o,
//            isdead(g,o) ? 'd' : isblack(o) ? 'b' : iswhite(o) ? 'w' : 'g',
//            "ns01oTt"[getage(o)], o->marked);
//   if (o->tt == LUA_VSHRSTR || o->tt == LUA_VLNGSTR)
//     printf(" '%s'", getstr(gco2ts(o)));
// }

    /*
     ** Function to print an object GC-friendly
     */
    private static void lua_printobj(lua_State* L, GCObject* o)
    {
//   printobj(G(L), o);
        throw new NotImplementedException();
    }

    /*
     ** Function to print a value
     */
    private static void lua_printvalue(TValue* v)
    {
//   switch (ttypetag(v)) {
//     case LUA_VNUMINT: case LUA_VNUMFLT: {
//       char buff[LUA_N2SBUFFSZ];
//       unsigned len = luaO_tostringbuff(v, buff);
//       buff[len] = '\0';
//       printf("%s", buff);
//       break;
//     }
//     case LUA_VSHRSTR:
//       printf("'%s'", getstr(tsvalue(v))); break;
//     case LUA_VLNGSTR:
//       printf("'%.30s...'", getstr(tsvalue(v))); break;
//     case LUA_VFALSE:
//       printf("%s", "false"); break;
//     case LUA_VTRUE:
//       printf("%s", "true"); break;
//     case LUA_VLIGHTUSERDATA:
//       printf("light udata: %p", pvalue(v)); break;
//     case LUA_VUSERDATA:
//       printf("full udata: %p", uvalue(v)); break;
//     case LUA_VNIL:
//       printf("nil"); break;
//     case LUA_VLCF:
//       printf("light C function: %p", fvalue(v)); break;
//     case LUA_VCCL:
//       printf("C closure: %p", clCvalue(v)); break;
//     case LUA_VLCL:
//       printf("Lua function: %p", clLvalue(v)); break;
//     case LUA_VTHREAD:
//       printf("thread: %p", thvalue(v)); break;
//     case LUA_VTABLE:
//       printf("table: %p", hvalue(v)); break;
//     default:
//       Debug.Assert(0);
//   }
        throw new NotImplementedException();
    }

    private static bool testobjref(global_State* g, GCObject* f, GCObject* t)
    {
        bool r1 = testobjref1(g, f, t);
        if (!r1)
        {
//     printf("%d(%02X) - ", g->gcstate, g->currentwhite);
//     printobj(g, f);
//     printf("  ->  ");
//     printobj(g, t);
//     printf("\n");
            throw new NotImplementedException();
        }

        return r1;
    }

    private static void checkobjref(global_State* g, GCObject* f, GCObject* t)
    {
        Debug.Assert(testobjref(g, f, t));
    }

    /*
     ** Version where 't' can be null. In that case, it should not apply the
     ** macro 'obj2gco' over the object. ('t' may have several types, so this
     ** definition must be a macro.)  Most checks need this version, because
     ** the check may run while an object is still being created.
     */
    private static void checkobjrefN(global_State* g, GCObject* f, GCObject* t)
    {
        if (t != null)
        {
            checkobjref(g, f, obj2gco(t));
        }
    }

    private static void checkvalref(global_State* g, GCObject* f, TValue* t)
    {
        Debug.Assert(!iscollectable(t) || (righttt(t) && testobjref(g, f, gcvalue(t))));
    }

    private static void checktable(global_State* g, Table* h)
    {
        uint asize = h->asize;
        Node* limit = gnode(h, sizenode(h));
        GCObject* hgc = obj2gco(h);
        checkobjrefN(g, hgc, (GCObject*)h->metatable);
        for (uint i = 0; i < asize; i++)
        {
            TValue aux;
            arr2obj(h, i, &aux);
            checkvalref(g, hgc, &aux);
        }

        for (Node* n = gnode(h, 0); n < limit; n++)
        {
            if (!isempty(gval(n)))
            {
                TValue k;
                getnodekey(mainthread(g), &k, n);
                Debug.Assert(!keyisnil(n));
                checkvalref(g, hgc, &k);
                checkvalref(g, hgc, gval(n));
            }
        }
    }

    private static void checkudata(global_State* g, Udata* u)
    {
        GCObject* hgc = obj2gco(u);
        checkobjrefN(g, hgc, (GCObject*)u->metatable);
        for (int i = 0; i < u->nuvalue; i++)
        {
            checkvalref(g, hgc, &((TValue*)u->uv)[i]);
        }
    }

    private static void checkproto(global_State* g, Proto* f)
    {
        GCObject* fgc = obj2gco(f);
        checkobjrefN(g, fgc, (GCObject*)f->source);
        for (int i = 0; i < f->sizek; i++)
        {
            if (iscollectable(f->k + i))
            {
                checkobjref(g, fgc, gcvalue(f->k + i));
            }
        }

        for (int i = 0; i < f->sizeupvalues; i++)
        {
            checkobjrefN(g, fgc, (GCObject*)f->upvalues[i].name);
        }

        for (int i = 0; i < f->sizep; i++)
        {
            checkobjrefN(g, fgc, (GCObject*)f->p[i]);
        }

        for (int i = 0; i < f->sizelocvars; i++)
        {
            checkobjrefN(g, fgc, (GCObject*)f->locvars[i].varname);
        }
    }

    private static void checkCclosure(global_State* g, CClosure* cl)
    {
        GCObject* clgc = obj2gco(cl);
        for (int i = 0; i < cl->nupvalues; i++)
        {
            checkvalref(g, clgc, CClosure.GetUpValuePtr(cl, i));
        }
    }

    private static void checkLclosure(global_State* g, LClosure* cl)
    {
        GCObject* clgc = obj2gco(cl);
        checkobjrefN(g, clgc, (GCObject*)cl->p);
        for (int i = 0; i < cl->nupvalues; i++)
        {
            UpVal* uv = LClosure.GetUpValue(cl, i);
            if (uv != null)
            {
                checkobjrefN(g, clgc, (GCObject*)uv);
                if (!upisopen(uv))
                {
                    checkvalref(g, obj2gco(uv), uv->v.p);
                }
            }
        }
    }

    private static bool lua_checkpc(CallInfo* ci)
    {
        if (!isLua(ci))
        {
            return true;
        }

        StkId f = ci->func.p;
        Proto* p = clLvalue(s2v(f))->p;
        return p->code <= ci->u.l.savedpc &&
               ci->u.l.savedpc <= p->code + p->sizecode;
    }

    private static void check_stack(global_State* g, lua_State* L1)
    {
        Debug.Assert(!isdead(g, (GCObject*)L1));
        if (L1->stack.p == null!)
        {
            /* incomplete thread? */
            Debug.Assert(L1->openupval == null && L1->ci == null);
            return;
        }

        for (UpVal* uv = L1->openupval; uv != null; uv = uv->u.open.next)
        {
            Debug.Assert(upisopen(uv)); /* must be open */
        }

        Debug.Assert(L1->top.p <= L1->stack_last.p);
        Debug.Assert(L1->tbclist.p <= L1->top.p);
        for (CallInfo* ci = L1->ci; ci != null; ci = ci->previous)
        {
            Debug.Assert(ci->top.p <= L1->stack_last.p);
            Debug.Assert(lua_checkpc(ci));
        }

        for (StkId o = L1->stack.p; o < L1->stack_last.p; o++)
        {
            checkliveness(L1, s2v(o)); /* entire stack must have valid values */
        }
    }

    private static void checkrefs(global_State* g, GCObject* o)
    {
        switch (o->tt)
        {
            case LUA_VUSERDATA:
                checkudata(g, gco2u(o));
                break;
            
            case LUA_VUPVAL:
                checkvalref(g, o, gco2upv(o)->v.p);
                break;
            
            case LUA_VTABLE:
                checktable(g, gco2t(o));
                break;
            
            case LUA_VTHREAD:
                check_stack(g, gco2th(o));
                break;
            
            case LUA_VLCL:
                checkLclosure(g, gco2lcl(o));
                break;
            
            case LUA_VCCL:
                checkCclosure(g, gco2ccl(o));
                break;
            
            case LUA_VPROTO:
                checkproto(g, gco2p(o));
                break;
            
            case LUA_VSHRSTR:
            case LUA_VLNGSTR:
                Debug.Assert(!isgrey(o)); /* strings are never grey */
                break;
            
            default:
                throw new InvalidOperationException();
        }
    }

    /*
     ** Check consistency of an object:
     ** - Dead objects can only happen in the 'allgc' list during a sweep
     ** phase (controlled by the caller through 'maybedead').
     ** - During pause, all objects must be white.
     ** - In generational mode:
     **   * objects must be old enough for their lists ('listage').
     **   * old objects cannot be white.
     **   * old objects must be black, except for 'touched1', 'old0',
     **     threads, and open upvalues.
     **   * 'touched1' objects must be gray.
     */
    private static void checkobject(
        global_State* g,
        GCObject* o,
        bool maybedead,
        int listage)
    {
        if (isdead(g, o))
        {
            Debug.Assert(maybedead);
        }

        else
        {
            Debug.Assert(g->gcstate != GCSpause || iswhite(o));
            if (g->gckind == KGC_GENMINOR)
            {
                /* generational mode? */
                Debug.Assert(getage(o) >= listage);
                if (isold(o))
                {
                    Debug.Assert(!iswhite(o));
                    Debug.Assert(
                        isblack(o) ||
                        getage(o) == G_TOUCHED1 ||
                        getage(o) == G_OLD0 ||
                        o->tt == LUA_VTHREAD ||
                        (o->tt == LUA_VUPVAL && upisopen(gco2upv(o))));
                }

                Debug.Assert(getage(o) != G_TOUCHED1 || isgrey(o));
            }

            checkrefs(g, o);
        }
    }

    private static long checkgreylist(global_State* g, GCObject* o)
    {
        int total = 0; /* count number of elements in the list */
        while (o != null)
        {
            Debug.Assert(isgrey(o) ^ (getage(o) == G_TOUCHED2));
            Debug.Assert(!testbit(o->marked, TESTBIT));
            if (keepinvariant(g))
            {
                l_setbit(ref o->marked, TESTBIT); /* mark that object is in a grey list */
            }

            total++;
            switch (o->tt)
            {
                case LUA_VTABLE: o = gco2t(o)->gclist; break;
                case LUA_VLCL: o = gco2lcl(o)->gclist; break;
                case LUA_VCCL: o = gco2ccl(o)->gclist; break;
                case LUA_VTHREAD: o = gco2th(o)->gclist; break;
                case LUA_VPROTO: o = gco2p(o)->gclist; break;
                case LUA_VUSERDATA:
                    Debug.Assert(gco2u(o)->nuvalue > 0);
                    o = gco2u(o)->gclist;
                    break;
                default:
                    Debug.Fail("other objects cannot be in a grey list");
                    break; /* other objects cannot be in a grey list */
            }
        }

        return total;
    }

    /*
     ** Check objects in grey lists.
     */
    private static long checkgreys(global_State* g)
    {
        long total = 0; /* count number of elements in all lists */
        if (!keepinvariant(g))
        {
            return total;
        }

        total += checkgreylist(g, g->grey);
        total += checkgreylist(g, g->greyagain);
        total += checkgreylist(g, g->weak);
        total += checkgreylist(g, g->allweak);
        total += checkgreylist(g, g->ephemeron);
        return total;
    }

    /*
    ** Check whether 'o' should be in a grey list. If so, increment
    ** 'count' and check its TESTBIT. (It must have been previously set by
    ** 'checkgraylist'.)
    */
    private static void incifingrey(global_State* g, GCObject* o, ref long count)
    {
        if (!keepinvariant(g))
        {
            return; /* grey lists not being kept in these phases */
        }

        if (o->tt == LUA_VUPVAL)
        {
            /* only open upvalues can be grey */
            Debug.Assert(!isgrey(o) || upisopen(gco2upv(o)));
            return; /* upvalues are never in grey lists */
        }

        /* these are the ones that must be in grey lists */
        if (isgrey(o) || getage(o) == G_TOUCHED2)
        {
            count++;
            Debug.Assert(testbit(o->marked, TESTBIT));
            resetbit(ref o->marked, TESTBIT); /* prepare for next cycle */
        }
    }

    private static long checklist(
        global_State* g,
        bool maybedead,
        bool tof,
        GCObject* newl,
        GCObject* survival,
        GCObject* old,
        GCObject* reallyold)
    {
        long total = 0; /* number of object that should be in grey lists */
        for (GCObject* o = newl; o != survival; o = o->next)
        {
            checkobject(g, o, maybedead, G_NEW);
            incifingrey(g, o, ref total);
            Debug.Assert(!tof == !tofinalise(o));
        }

        for (GCObject* o = survival; o != old; o = o->next)
        {
            checkobject(g, o, false, G_SURVIVAL);
            incifingrey(g, o, ref total);
            Debug.Assert(!tof == !tofinalise(o));
        }

        for (GCObject* o = old; o != reallyold; o = o->next)
        {
            checkobject(g, o, false, G_OLD1);
            incifingrey(g, o, ref total);
            Debug.Assert(!tof == !tofinalise(o));
        }

        for (GCObject* o = reallyold; o != null; o = o->next)
        {
            checkobject(g, o, false, G_OLD);
            incifingrey(g, o, ref total);
            Debug.Assert(!tof == !tofinalise(o));
        }

        return total;
    }

    /*
     ** Function to traverse and check all memory used by Lua
     */
    private static int lua_checkmemory(lua_State* L)
    {
        global_State* g = G(L);
        if (keepinvariant(g))
        {
            Debug.Assert(!iswhite((GCObject*)mainthread(g)));
            Debug.Assert(!iswhite(gcvalue(&g->l_registry)));
        }

        Debug.Assert(!isdead(g, gcvalue(&g->l_registry)));
        Debug.Assert(g->sweepgc == null || issweepphase(g));
        long totalin = checkgreys(g); /* total of objects that are in grey lists */

        /* check 'fixedgc' list */
        for (GCObject* o = g->fixedgc; o != null; o = o->next)
        {
            Debug.Assert(o->tt == LUA_VSHRSTR && isgrey(o) && getage(o) == G_OLD);
        }

        /* check 'allgc' list */
        bool maybedead = GCSatomic < g->gcstate && g->gcstate <= GCSswpallgc;
        long totalshould = checklist(
            g,
            maybedead,
            false,
            g->allgc,
            g->survival,
            g->old1,
            g->reallyold); /* total of objects that should be in grey lists */

        /* check 'finobj' list */
        totalshould += checklist(
            g,
            false,
            true,
            g->finobj,
            g->finobjsur,
            g->finobjold1,
            g->finobjrold);

        /* check 'tobefnz' list */
        for (GCObject* o = g->tobefnz; o != null; o = o->next)
        {
            checkobject(g, o, false, G_NEW);
            incifingrey(g, o, ref totalshould);
            Debug.Assert(tofinalise(o));
            Debug.Assert(o->tt == LUA_VUSERDATA || o->tt == LUA_VTABLE);
        }

        if (keepinvariant(g))
        {
            Debug.Assert(totalin == totalshould);
        }

        return 0;
    }

    /*
     ** {======================================================
     ** Disassembler
     ** =======================================================
     */

    private static string buildop(Proto* p, int pc)
    {
        StringBuilder sb = new();
        uint i = p->code[pc];
        OpCode o = GET_OPCODE(i);
        string name = opnames[(int)o];
        int line = luaG_getfuncline(p, pc);
        int lineinfo = p->lineinfo != null ? p->lineinfo[pc] : 0;
        if (lineinfo == ABSLINEINFO)
        {
            sb.Append("(__");
        }
        else
        {
            sb.Append($"({lineinfo,2}");
        }

        sb.Append($" - {line,4}) {pc,4} - ");
        switch (getOpMode(o))
        {
            case OpMode.iABC:
                sb.Append($"{name,-12}{GETARG_A(i),4} {GETARG_B(i),4} {GETARG_C(i),4}{(GETARG_k(i) ? " (k)" : "")}");
                break;
            
            case OpMode.ivABC:
                sb.Append($"{name,-12}{GETARG_A(i),4} {GETARG_vB(i),4} {GETARG_vC(i),4}{(GETARG_k(i) ? " (k)" : "")}");
                break;
            
            case OpMode.iABx:
                sb.Append($"{name,-12}{GETARG_A(i),4} {GETARG_Bx(i),4}");
                break;
            
            case OpMode.iAsBx:
                sb.Append($"{name,-12}{GETARG_A(i),4} {GETARG_sBx(i),4}");
                break;
            
            case OpMode.iAx:
                sb.Append($"{name,-12}{GETARG_Ax(i),4}");
                break;
            
            case OpMode.isJ:
                sb.Append($"{name, 12}{GETARG_sJ(i),4}");
                break;
        }

        return sb.ToString();
    }

// #if 0
// void luaI_printcode (Proto *pt, int size) {
//   int pc;
//   for (pc=0; pc<size; pc++) {
//     char buff[100];
//     printf("%s\n", buildop(pt, pc, buff));
//   }
//   printf("-------\n");
// }
//
//
// void luaI_printinst (Proto *pt, int pc) {
//   char buff[100];
//   printf("%s\n", buildop(pt, pc, buff));
// }
// #endif

    private static int listcode(lua_State* L)
    {
        luaL_argcheck(
            L,
            lua_isfunction(L, 1) && !lua_iscfunction(L, 1),
            1,
            "Lua function expected");
        Proto* p = getproto(obj_at(L, 1));
        lua_newtable(L);
        setnameval(L, "maxstack", p->maxstacksize);
        setnameval(L, "numparams", p->numparams);
        for (int pc = 0; pc < p->sizecode; pc++)
        {
            lua_pushinteger(L, pc + 1);
            lua_pushstring(L, buildop(p, pc));
            lua_settable(L, -3);
        }

        return 1;
    }

    internal static int printcode(lua_State* L)
    {
        luaL_argcheck(
            L,
            lua_isfunction(L, 1) && !lua_iscfunction(L, 1),
            1,
            "Lua function expected");
        Proto* p = getproto(obj_at(L, 1));
        Console.WriteLine("maxstack: {0}", p->maxstacksize);
        Console.WriteLine("numparams: {0}", p->numparams);
        
        for (int pc = 0; pc < p->sizecode; pc++)
        {
            Console.WriteLine(buildop(p, pc));
        }

        return 0;
    }

    private static int listk(lua_State* L)
    {
        luaL_argcheck(
            L,
            lua_isfunction(L, 1) && !lua_iscfunction(L, 1),
            1,
            "Lua function expected");
        Proto* p = getproto(obj_at(L, 1));
        lua_createtable(L, p->sizek, 0);
        for (int i = 0; i < p->sizek; i++)
        {
            pushobject(L, p->k + i);
            lua_rawseti(L, -2, i + 1);
        }

        return 1;
    }

    private static int listabslineinfo(lua_State* L)
    {
//   Proto *p;
//   int i;
//   luaL_argcheck(L, lua_isfunction(L, 1) && !lua_iscfunction(L, 1),
//                  1, "Lua function expected");
//   p = getproto(obj_at(L, 1));
//   luaL_argcheck(L, p->abslineinfo != null, 1, "function has no debug info");
//   lua_createtable(L, 2 * p->sizeabslineinfo, 0);
//   for (i=0; i < p->sizeabslineinfo; i++) {
//     lua_pushinteger(L, p->abslineinfo[i].pc);
//     lua_rawseti(L, -2, 2 * i + 1);
//     lua_pushinteger(L, p->abslineinfo[i].line);
//     lua_rawseti(L, -2, 2 * i + 2);
//   }
//   return 1;
        throw new NotImplementedException();
    }

    private static int listlocals(lua_State* L)
    {
//   Proto *p;
//   int pc = cast_int(luaL_checkinteger(L, 2)) - 1;
//   int i = 0;
//   const char *name;
//   luaL_argcheck(L, lua_isfunction(L, 1) && !lua_iscfunction(L, 1),
//                  1, "Lua function expected");
//   p = getproto(obj_at(L, 1));
//   while ((name = luaF_getlocalname(p, ++i, pc)) != null)
//     lua_pushstring(L, name);
//   return i-1;
        throw new NotImplementedException();
    }

    /*
     ** Function to print the stack
     */
    private static void lua_printstack(lua_State* L)
    {
//   int i;
//   int n = lua_gettop(L);
//   printf("stack: >>\n");
//   for (i = 1; i <= n; i++) {
//     printf("%3d: ", i);
//     lua_printvalue(s2v(L->ci->func.p + i));
//     printf("\n");
//   }
//   printf("<<\n");
        throw new NotImplementedException();
    }

    private static int lua_printallstack(lua_State* L)
    {
//   StkId p;
//   int i = 1;
//   CallInfo *ci = &L->base_ci;
//   printf("stack: >>\n");
//   for (p = L->stack.p; p < L->top.p; p++) {
//     if (ci != null && p == ci->func.p) {
//       printf("  ---\n");
//       if (ci == L->ci)
//         ci = null;  /* printed last frame */
//       else
//         ci = ci->next;
//     }
//     printf("%3d: ", i++);
//     lua_printvalue(s2v(p));
//     printf("\n");
//   }
//   printf("<<\n");
//   return 0;
        throw new NotImplementedException();
    }

    private static int get_limits(lua_State* L)
    {
        lua_createtable(L, 0, 5);
        setnameval(L, "IS32INT", 1);
        //   setnameval(L, "MAXARG_Ax", MAXARG_Ax);
        //   setnameval(L, "MAXARG_Bx", MAXARG_Bx);
        //   setnameval(L, "OFFSET_sBx", OFFSET_sBx);
        //   setnameval(L, "NUM_OPCODES", NUM_OPCODES);
        //   return 1;
        throw new NotImplementedException();
    }

    private static int get_sizes(lua_State* L)
    {
//   lua_newtable(L);
//   setnameval(L, "Lua state", sizeof(lua_State));
//   setnameval(L, "global state", sizeof(global_State));
//   setnameval(L, "TValue", sizeof(TValue));
//   setnameval(L, "Node", sizeof(Node));
//   setnameval(L, "stack Value", sizeof(StackValue));
//   return 1;
        throw new NotImplementedException();
    }

    private static int mem_query(lua_State* L)
    {
        if (lua_isnone(L, 1))
        {
            lua_pushinteger(L, l_memcontrol->total);
            lua_pushinteger(L, l_memcontrol->numblocks);
            lua_pushinteger(L, l_memcontrol->maxmem);
            return 3;
        }

        if (lua_isnumber(L, 1))
        {
            long limit = luaL_checkinteger(L, 1);
            if (limit == 0)
            {
                limit = long.MaxValue;
            }

            l_memcontrol->memlimit = limit;
            return 0;
        }

        string t = luaL_checknetstring(L, 1);
        for (int i = LUA_NUMTYPES - 1; i >= 0; i--)
        {
            if (t == ttypename(i))
            {
                lua_pushinteger(L, l_memcontrol->objcount[i]);
                return 1;
            }
        }

        return luaL_error(L, "unknown type '%s'", t);
    }

    private static int alloc_count(lua_State* L)
    {
        if (lua_isnone(L, 1))
        {
            l_memcontrol->countlimit = ~0L;
        }
        else
        {
            l_memcontrol->countlimit = luaL_checkinteger(L, 1);
        }

        return 0;
    }

    private static int alloc_failnext(lua_State* L)
    {
        l_memcontrol->failnext = true;
        return 0;
    }

    private static int settrick(lua_State* L)
    {
//   if (ttisnil(obj_at(L, 1)))
//     l_Trick = null;
//   else
//     l_Trick = gcvalue(obj_at(L, 1));
//   return 0;
        throw new NotImplementedException();
    }

    private static int gc_color(lua_State* L)
    {
        luaL_checkany(L, 1);
        TValue* o = obj_at(L, 1);
        if (!iscollectable(o))
        {
            lua_pushstring(L, "no collectable");
        }
        else
        {
            GCObject* obj = gcvalue(o);
            lua_pushstring(
                L,
                isdead(G(L), obj) ? "dead" :
                iswhite(obj) ? "white" :
                isblack(obj) ? "black" : "gray");
        }

        return 1;
    }

    private static readonly string[] gennames =
    [
        "new", "survival", "old0", "old1",
        "old", "touched1", "touched2",
    ];

    private static int gc_age(lua_State* L)
    {
        luaL_checkany(L, 1);
        TValue* o = obj_at(L, 1);
        if (!iscollectable(o))
        {
            lua_pushstring(L, "no collectable");
        }
        else
        {
            GCObject* obj = gcvalue(o);
            lua_pushstring(L, gennames[getage(obj)]);
        }

        return 1;
    }

    private static int gc_printobj(lua_State* L)
    {
//   TValue *o;
//   luaL_checkany(L, 1);
//   o = obj_at(L, 1);
//   if (!iscollectable(o))
//     printf("no collectable\n");
//   else {
//     GCObject *obj = gcvalue(o);
//     printobj(G(L), obj);
//     printf("\n");
//   }
//   return 0;
        throw new NotImplementedException();
    }

    private static readonly string[] statenames =
    [
        "propagate", "enteratomic", "atomic", "sweepallgc", "sweepfinobj",
        "sweeptobefnz", "sweepend", "callfin", "pause", "",
    ];

    private static readonly int[] states =
    [
        GCSpropagate, GCSenteratomic, GCSatomic, GCSswpallgc, GCSswpfinobj,
        GCSswptobefnz, GCSswpend, GCScallfin, GCSpause, -1,
    ];

    private static int gc_state(lua_State* L)
    {
        int option = states[luaL_checkoption(L, 1, "", statenames)];
        global_State* g = G(L);
        if (option == -1)
        {
            lua_pushstring(L, statenames[g->gcstate]);
            return 1;
        }

        if (g->gckind != KGC_INC)
        {
            luaL_error(L, "cannot change states in generational mode");
        }

        lua_lock(L);
        if (option < g->gcstate)
        {
            /* must cross 'pause'? */
            luaC_runtilstate(L, GCSpause, true); /* run until pause */
        }

        luaC_runtilstate(L, option, false);  /* do not skip propagation state */
        Debug.Assert(g->gcstate == option);
        lua_unlock(L);
        return 0;
    }

    private static bool tracinggc;

    private static void luai_tracegctest(lua_State* L, bool first)
    {
        if (!tracinggc)
        {
            return;
        }

//     global_State *g = G(L);
//     lua_unlock(L);
//     g->gcstp = GCSTPGC;
//     lua_checkstack(L, 10);
//     lua_getfield(L, LUA_REGISTRYINDEX, "tracegc");
//     lua_pushboolean(L, first);
//     lua_call(L, 1, 0);
//     g->gcstp = 0;
//     lua_lock(L);
        throw new NotImplementedException();
    }

    private static int tracegc(lua_State* L)
    {
        if (lua_isnil(L, 1))
        {
            tracinggc = false;
        }
        else
        {
            tracinggc = true;
            lua_setfield(L, LUA_REGISTRYINDEX, "tracegc");
        }

        return 0;
    }

    private static int hash_query(lua_State* L)
    {
//   if (lua_isnone(L, 2)) {
//     TString *ts;
//     luaL_argcheck(L, lua_type(L, 1) == LUA_TSTRING, 1, "string expected");
//     ts = tsvalue(obj_at(L, 1));
//     if (ts->tt == LUA_VLNGSTR)
//       luaS_hashlongstr(ts);  /* make sure long string has a hash */
//     lua_pushinteger(L, cast_int(ts->hash));
//   }
//   else {
//     TValue *o = obj_at(L, 1);
//     Table *t;
//     luaL_checktype(L, 2, LUA_TTABLE);
//     t = hvalue(obj_at(L, 2));
//     lua_pushinteger(L, cast_Integer(luaH_mainposition(t, o) - t->node));
//   }
//   return 1;
        throw new NotImplementedException();
    }

    private static int stacklevel(lua_State* L)
    {
        int a = 0;
        lua_pushinteger(L, L->top.p - L->stack.p);
        lua_pushinteger(L, stacksize(L));
        lua_pushinteger(L, L->nCcalls);
        lua_pushinteger(L, L->nci);
        lua_pushinteger(L, (long)&a);
        return 5;
    }

    private static int table_query(lua_State* L)
    {
        int i = (int)luaL_optinteger(L, 2, -1);
        luaL_checktype(L, 1, LUA_TTABLE);
        Table* t = hvalue(obj_at(L, 1));
        uint asize = t->asize;
        if (i == -1)
        {
            lua_pushinteger(L, asize);
            lua_pushinteger(L, allocsizenode(t));
            lua_pushinteger(L, asize > 0 ? *lenhint(t) : 0);
        }
        else if ((uint)i < asize)
        {
            lua_pushinteger(L, i);
            if (!tagisempty(*getArrTag(t, (ulong)i)))
            {
                arr2obj(t, (uint)i, s2v(L->top.p));
            }
            else
            {
                setnilvalue(s2v(L->top.p));
            }

            api_incr_top(L);
            lua_pushnil(L);
        }
        else if ((uint)(i -= (int)asize) < sizenode(t))
        {
            TValue k;
            getnodekey(L, &k, gnode(t, i));
            if (!isempty(gval(gnode(t, i))) ||
                ttisnil(&k) ||
                ttisnumber(&k))
            {
                pushobject(L, &k);
            }
            else
            {
                lua_pushliteral(L, "<undef>");
            }

            if (!isempty(gval(gnode(t, i))))
            {
                pushobject(L, gval(gnode(t, i)));
            }
            else
            {
                lua_pushnil(L);
            }

            lua_pushinteger(L, gnext(&t->node[i]));
        }

        return 3;
    }

    private static int gc_query(lua_State* L)
    {
//   global_State *g = G(L);
//   lua_pushstring(L, g->gckind == KGC_INC ? "inc"
//                   : g->gckind == KGC_GENMAJOR ? "genmajor"
//                   : "genminor");
//   lua_pushstring(L, statenames[g->gcstate]);
//   lua_pushinteger(L, cast_st2S(gettotalbytes(g)));
//   lua_pushinteger(L, cast_st2S(g->GCdebt));
//   lua_pushinteger(L, cast_st2S(g->GCmarked));
//   lua_pushinteger(L, cast_st2S(g->GCmajorminor));
//   return 6;
        throw new NotImplementedException();
    }

    private static int test_codeparam(lua_State* L)
    {
        long p = luaL_checkinteger(L, 1);
        lua_pushinteger(L, luaO_codeparam((uint)p));
        return 1;
    }

    private static int test_applyparam(lua_State* L)
    {
        long p = luaL_checkinteger(L, 1);
        long x = luaL_checkinteger(L, 2);
        lua_pushinteger(L, luaO_applyparam((byte)p, x));
        return 1;
    }

    private static int string_query(lua_State* L)
    {
        stringtable* tb = &G(L)->strt;
        int s = (int)luaL_optinteger(L, 1, 0) - 1;
        if (s == -1)
        {
            lua_pushinteger(L, tb->size);
            lua_pushinteger(L, tb->nuse);
            return 2;
        }

        if (s < tb->size)
        {
            TString* ts;
            int n = 0;
            for (ts = tb->hash[s]; ts != null; ts = ts->u.hnext)
            {
                setsvalue2s(L, L->top.p, ts);
                api_incr_top(L);
                n++;
            }

            return n;
        }

        return 0;
    }

    private static int getreftable(lua_State* L)
    {
        if (lua_istable(L, 2)) /* is there a table as second argument? */
        {
            return 2; /* use it as the table */
        }

        return LUA_REGISTRYINDEX; /* default is to use the register */
    }

    private static int tref(lua_State* L)
    {
        int t = getreftable(L);
        int level = lua_gettop(L);
        luaL_checkany(L, 1);
        lua_pushvalue(L, 1);
        lua_pushinteger(L, luaL_ref(L, t));
        Debug.Assert(lua_gettop(L) == level + 1); /* +1 for result */
        return 1;
    }

    private static int getref(lua_State* L)
    {
        int t = getreftable(L);
        int level = lua_gettop(L);
        lua_rawgeti(L, t, luaL_checkinteger(L, 1));
        Debug.Assert(lua_gettop(L) == level + 1);
        return 1;
    }

    private static int unref(lua_State* L)
    {
        int t = getreftable(L);
        int level = lua_gettop(L);
        luaL_unref(L, t, (int)luaL_checkinteger(L, 1));
        Debug.Assert(lua_gettop(L) == level);
        return 0;
    }

    private static int upvalue(lua_State* L)
    {
        int n = (int)luaL_checkinteger(L, 2);
        luaL_checktype(L, 1, LUA_TFUNCTION);
        if (lua_isnone(L, 3))
        {
            string? name = lua_getupvalue(L, 1, n);
            if (name == null)
            {
                return 0;
            }

            lua_pushstring(L, name);
            return 2;
        }
        else
        {
            string? name = lua_setupvalue(L, 1, n);
            lua_pushstring(L, name);
            return 1;
        }
    }

    private static int newuserdata(lua_State* L)
    {
        long size = luaL_optinteger(L, 1, 0);
        int nuv = (int)luaL_optinteger(L, 2, 0);
        byte* p = (byte*)lua_newuserdatauv(L, size, nuv);
        while (size-- > 0)
        {
            *p++ = 0;
        }

        return 1;
    }

    private static int pushuserdata(lua_State* L)
    {
        long u = luaL_checkinteger(L, 1);
        lua_pushlightuserdata(L, (void*)u);
        return 1;
    }

    private static int udataval(lua_State* L)
    {
        lua_pushinteger(L, (long)lua_touserdata(L, 1));
        return 1;
    }

    private static int doonnewstack(lua_State* L)
    {
        lua_State* L1 = lua_newthread(L);
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        int status = luaL_loadbuffer(L1, s, Encoding.UTF8.GetString(s));
        if (status == LUA_OK)
        {
            status = lua_pcall(L1, 0, 0, 0);
        }

        lua_pushinteger(L, status);
        return 1;
    }

    private static int s2d(lua_State* L)
    {
        lua_pushnumber(L, MemoryMarshal.Cast<byte, double>(luaL_checkstring(L, 1))[0]);
        return 1;
    }

    private static int d2s(lua_State* L)
    {
        double d = luaL_checknumber(L, 1);
        double* dPtr = &d;
        ReadOnlySpan<double> dSpan = new(dPtr, 1);
        lua_pushlstring(L, MemoryMarshal.Cast<double, byte>(dSpan));
        return 1;
    }

    private static int num2int(lua_State* L)
    {
//   lua_pushinteger(L, lua_tointeger(L, 1));
//   return 1;
        throw new NotImplementedException();
    }

    private static int makeseed(lua_State* L)
    {
//   lua_pushinteger(L, cast_Integer(luaL_makeseed(L)));
//   return 1;
        throw new NotImplementedException();
    }

    private static int newstate(lua_State* L)
    {
        lua_Alloc f = lua_getallocf(L, out void* ud);
        lua_State* L1 = lua_newstate(f, ud, 0);
        if (L1 != null)
        {
            lua_atpanic(L1, &tpanic);
            lua_pushlightuserdata(L, L1);
        }
        else
        {
            lua_pushnil(L);
        }

        return 1;
    }

    private static lua_State* getstate(lua_State* L)
    {
        lua_State* L1 = (lua_State*)lua_touserdata(L, 1);
        luaL_argcheck(L, L1 != null, 1, "state expected");
        return L1;
    }

    private static int loadlib(lua_State* L)
    {
        lua_State* L1 = getstate(L);
        int load = (int)luaL_checkinteger(L, 2);
        int preload = (int)luaL_checkinteger(L, 3);
        luaL_openselectedlibs(L1, load, preload);
        luaL_requiref(L1, "T", &luaB_opentests, false);
        Debug.Assert(lua_type(L1, -1) == LUA_TTABLE);
        /* 'requiref' should not reload module already loaded... */
        luaL_requiref(L1, "T", null, true); /* seg. fault if it reloads */
        /* ...but should return the same module */
        Debug.Assert(lua_compare(L1, -1, -2, LUA_OPEQ));
        return 0;
    }

    private static int closestate(lua_State* L)
    {
        lua_State* L1 = getstate(L);
        lua_close(L1);
        return 0;
    }

    private static int doremote(lua_State* L)
    {
        lua_State* L1 = getstate(L);
        ReadOnlySpan<byte> code = luaL_checklstring(L, 2);
        lua_settop(L1, 0);
        int status = luaL_loadbuffer(L1, code, Encoding.UTF8.GetString(code));
        if (status == LUA_OK)
        {
            status = lua_pcall(L1, 0, LUA_MULTRET, 0);
        }

        if (status != LUA_OK)
        {
            lua_pushnil(L);
            lua_pushstring(L, lua_tostring(L1, -1));
            lua_pushinteger(L, status);
            return 3;
        }
        else
        {
            int i = 0;
            while (!lua_isnone(L1, ++i))
            {
                lua_pushstring(L, lua_tostring(L1, i));
            }

            lua_pop(L1, i - 1);
            return i - 1;
        }
    }

    private static int log2_aux(lua_State* L)
    {
//   unsigned int x = (unsigned int)luaL_checkinteger(L, 1);
//   lua_pushinteger(L, luaO_ceillog2(x));
//   return 1;
        throw new NotImplementedException();
    }

    private sealed class Aux
    {
        public string? paniccode;
        public lua_State* L;
    }

    /*
    ** does a long-jump back to "main program".
    */
    private static int panicback(lua_State* L)
    {
        lua_checkstack(L, 1);  /* open space for 'Aux' struct */
        lua_getfield(L, LUA_REGISTRYINDEX, "_jmpbuf");  /* get 'Aux' struct */
        Aux b = GCHandle<Aux>.FromIntPtr((nint)lua_touserdata(L, -1)).Target;
        lua_pop(L, 1);  /* remove 'Aux' struct */
        runC(b.L, L, b.paniccode);  /* run optional panic code */
        throw new lua_longjmp(null);
    }

    private static int checkpanic(lua_State* L)
    {
        string code = luaL_checknetstring(L, 1);
        lua_Alloc f = lua_getallocf(L, out void* ud);

        Aux b = new();
        using GCHandle<Aux> bhandle = new(b);

        b.paniccode = luaL_optnetstring(L, 2, "");
        b.L = L;
        lua_State* L1 = lua_newstate(f, ud, 0); /* create new state */
        if (L1 == null)
        {
            /* error? */
            lua_pushstring(L, MEMERRMSG);
            return 1;
        }

        lua_atpanic(L1, &panicback); /* set its panic function */
        lua_pushlightuserdata(L1, bhandle.ToPointer());
        lua_setfield(L1, LUA_REGISTRYINDEX, "_jmpbuf"); /* store 'Aux' struct */
        try
        {
            runC(L, L1, code); /* run code unprotected */
            lua_pushliteral(L, "no errors");
        }
        catch (lua_longjmp)
        {
            /* move error message to original state */
            lua_pushstring(L, lua_tonetstring(L1, -1));
        }

        lua_close(L1);
        return 1;
    }

    private static int externKstr(lua_State* L)
    {
        byte* s = luaL_checklstring(L, 1, out int len);
        lua_pushexternalstring(L, s, len, null, null);
        return 1;
    }

    /*
     ** Create a buffer with the content of a given string and then
     ** create an external string using that buffer. Use the allocation
     ** function from Lua to create and free the buffer.
     */
    private static int externstr(lua_State* L)
    {
        ReadOnlySpan<byte> s = luaL_checklstring(L, 1);
        lua_Alloc allocf = lua_getallocf(L, out void* ud); /* get allocation function */
        /* create the buffer */
        byte* buff = (byte*)allocf(ud, null, 0, s.Length + 1);
        if (buff == null)
        {
            /* memory error? */
            lua_pushliteral(L, "not enough memory");
            lua_error(L); /* raise a memory error */
        }

        /* copy string content to buffer, including ending 0 */
        Span<byte> buffSpan = new(buff, s.Length + 1);
        s.CopyTo(buffSpan);

        buff[s.Length] = 0;
        /* create external string */
        lua_pushexternalstring(L, buff, s.Length, allocf, ud);
        return 1;
    }

    /*
     ** {====================================================================
     ** function to test the API with C. It interprets a kind of assembler
     ** language with calls to the API, so the test can be driven by Lua code
     ** =====================================================================
     */

    private static string delimits = " \t\n,;";

    private static void skip(ref ReadOnlySpan<char> pc)
    {
        while(!pc.IsEmpty)
        {
            if (delimits.Contains(pc[0]))
            {
                pc = pc[1..];
            }
            else if (pc[0] == '#')
            {
                /* comment? */
                while (!pc.IsEmpty && pc[0] != '\n')
                {
                    pc = pc[1..]; /* until end-of-line */
                }
            }
            else
            {
                break;
            }
        }
    }

    private static int getnum_aux(lua_State* L, lua_State* L1, ref ReadOnlySpan<char> pc)
    {
        skip(ref pc);
        if (pc[0] == '.')
        {
            int res = (int)lua_tointeger(L1, -1);
            lua_pop(L1, 1);
            pc = pc[1..];
            return res;
        }

        if (pc[0] == '*')
        {
            int res = lua_gettop(L1);
            pc = pc[1..];
            return res;
        }

        if (pc[0] == '!')
        {
            int res = pc[1] switch
            {
                'G' => LUA_RIDX_GLOBALS,
                'M' => LUA_RIDX_MAINTHREAD,
                _ => throw new InvalidOperationException(),
            };

            pc = pc[2..];
            return res;
        }

        int sig = 1;
        if (pc[0] == '-')
        {
            sig = -1;
            pc = pc[1..];
        }

        if (!lisdigit(pc[0]))
        {
            luaL_error(L, "number expected (%s)", pc.ToString());
        }

        int result = 0;
        while (!pc.IsEmpty && lisdigit(pc[0]))
        {
            result = result * 10 + pc[0] - '0';
            pc = pc[1..];
        }

        return sig * result;
    }

    private static ReadOnlySpan<char> getstring_aux(lua_State* L, ref ReadOnlySpan<char> pc)
    {
        skip(ref pc);
        if (!pc.IsEmpty && (pc[0] == '"' || pc[0] == '\''))
        {
            // quoted string?
            char quote = pc[0];
            pc = pc[1..];
            ReadOnlySpan<char> start = pc;
            while (pc[0] != quote)
            {
                if (pc.IsEmpty || pc[0] == '\0')
                {
                    luaL_error(L, "unfinished string in C script");
                }
                
                pc = pc[1..];
            }
            
            ReadOnlySpan<char> result = start[..^pc.Length];
            pc = pc[1..];
            return result;
        }

        int index = pc.IndexOfAny(delimits);
        if (index >= 0)
        {
            ReadOnlySpan<char> result = pc[..index];
            pc = pc[index..];
            return result;
        }
        else
        {
            ReadOnlySpan<char> result = pc;
            pc = ReadOnlySpan<char>.Empty;
            return result;
        }
    }

    private static int getindex_aux(lua_State* L, lua_State* L1, ref ReadOnlySpan<char> pc)
    {
        skip(ref pc);
        switch (pc[0])
        {
            case 'R':
                pc = pc[1..];
                return LUA_REGISTRYINDEX;

            case 'U':
                pc = pc[1..];
                return lua_upvalueindex(getnum_aux(L, L1, ref pc));

            default:
                int n = getnum_aux(L, L1, ref pc);
                if (n == 0)
                {
                    return 0;
                }

                return lua_absindex(L1, n);
        }
    }

    private static readonly string[] statcodes =
    [
        "OK", "YIELD", "ERRRUN",
        "ERRSYNTAX", MEMERRMSG, "ERRERR",
    ];

    /*
     ** Avoid these stat codes from being collected, to avoid possible
     ** memory error when pushing them.
     */
    private static void regcodes(lua_State* L)
    {
        for (int i = 0; i < statcodes.Length; i++)
        {
            lua_pushboolean(L, true);
            lua_setfield(L, LUA_REGISTRYINDEX, statcodes[i]);
        }
    }

    /*
    ** arithmetic operation encoding for 'arith' instruction
    ** LUA_OPIDIV  -> \
    ** LUA_OPSHL   -> <
    ** LUA_OPSHR   -> >
    ** LUA_OPUNM   -> _
    ** LUA_OPBNOT  -> !
    */
    private const string ops = "+-*%^/\\&|~<>_!";

    private static int runC(lua_State* L, lua_State* L1, string? ppc)
    {
        if (ppc == null)
        {
            return luaL_error(L, "attempt to runC null script");
        }

        ReadOnlySpan<char> pc = ppc;

        int status = 0;
        while (true)
        {
            ReadOnlySpan<char> inst = getstring_aux(L, ref pc);
            switch (inst)
            {
                case "":
                    return 0;

                case "absindex":
                    lua_pushinteger(L1, getindex_aux(L, L1, ref pc));
                    break;

                case "append":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        int i = (int)lua_rawlen(L1, t);
                        lua_rawseti(L1, t, i + 1);
                        break;
                    }

                case "arith":
                    {
                        skip(ref pc);
                        int op = ops.IndexOf(pc[0]);
                        pc = pc[1..];
                        lua_arith(L1, op);
                        break;
                    }

                case "call":
                    {
                        int narg = getnum_aux(L, L1, ref pc);
                        int nres = getnum_aux(L, L1, ref pc);
                        lua_call(L1, narg, nres);
                        break;
                    }

                case "callk":
                    {
                        int narg = getnum_aux(L, L1, ref pc);
                        int nres = getnum_aux(L, L1, ref pc);
                        int i = getindex_aux(L, L1, ref pc);
                        lua_callk(L1, narg, nres, i, &Cfunck);
                        break;
                    }

                case "checkstack":
                    {
                        int sz = getnum_aux(L, L1, ref pc);
                        ReadOnlySpan<char> msg = getstring_aux(L, ref pc);
                        if (msg.IsEmpty || msg[0] == '\0')
                        {
                            msg = null; /* to test 'luaL_checkstack' with no message */
                        }

                        luaL_checkstack(L1, sz, msg.ToString());
                        break;
                    }

                case "rawcheckstack":
                    {
                        int sz = getnum_aux(L, L1, ref pc);
                        lua_pushboolean(L1, lua_checkstack(L1, sz));
                        break;
                    }

                case "compare":
                    {
                        ReadOnlySpan<char> opt = getstring_aux(L, ref pc); /* EQ, LT, or LE */
                        int op = opt[0] == 'E' ? LUA_OPEQ
                            : opt[1] == 'T' ? LUA_OPLT : LUA_OPLE;
                        int a = getindex_aux(L, L1, ref pc);
                        int b = getindex_aux(L, L1, ref pc);
                        lua_pushboolean(L1, lua_compare(L1, a, b, op));
                        break;
                    }

                case "concat":
                    lua_concat(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "copy":
                    {
                        int f = getindex_aux(L, L1, ref pc);
                        lua_copy(L1, f, getindex_aux(L, L1, ref pc));
                        break;
                    }

                case "func2num":
                    lua_CFunction func = lua_tocfunction(L1, getindex_aux(L, L1, ref pc));
                    lua_pushinteger(L1, (nint)(func));
                    break;

                case "getfield":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        int tp = lua_getfield(L1, t, getstring_aux(L, ref pc).ToString());
                        Debug.Assert(tp == lua_type(L1, -1));
                        break;
                    }

                case "getglobal":
                    lua_getglobal(L1, getstring_aux(L, ref pc).ToString());
                    break;

                case "getmetatable":
                    if (!lua_getmetatable(L1, getindex_aux(L, L1, ref pc)))
                    {
                        lua_pushnil(L1);
                    }

                    break;

                case "gettable":
                    {
                        int tp = lua_gettable(L1, getindex_aux(L, L1, ref pc));
                        Debug.Assert(tp == lua_type(L1, -1));
                        break;
                    }

                case "gettop":
                    lua_pushinteger(L1, lua_gettop(L1));
                    break;

                case "gsub":
                    {
                        int a = getnum_aux(L, L1, ref pc);
                        int b = getnum_aux(L, L1, ref pc);
                        int c = getnum_aux(L, L1, ref pc);
                        luaL_gsub(
                            L1,
                            lua_tonetstring(L1, a),
                            lua_tonetstring(L1, b),
                            lua_tonetstring(L1, c));
                        break;
                    }

                case "insert":
                    lua_insert(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "iscfunction":
                    lua_pushboolean(L1, lua_iscfunction(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "isfunction":
                    lua_pushboolean(L1, lua_isfunction(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "isnil":
                    lua_pushboolean(L1, lua_isnil(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "isnull":
                    lua_pushboolean(L1, lua_isnone(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "isnumber":
                    lua_pushboolean(L1, lua_isnumber(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "isstring":
                    lua_pushboolean(L1, lua_isstring(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "istable":
                    lua_pushboolean(L1, lua_istable(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "isudataval":
                    lua_pushboolean(L1, lua_islightuserdata(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "isuserdata":
                    lua_pushboolean(L1, lua_isuserdata(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "len":
                    lua_len(L1, getindex_aux(L, L1, ref pc));
                    break;

                case "Llen":
                    lua_pushinteger(L1, luaL_len(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "loadfile":
                    luaL_loadfile(L1, Encoding.UTF8.GetString(luaL_checkstring(L1, getnum_aux(L, L1, ref pc))));
                    break;

                case "loadstring":
                    {
                        byte* s = luaL_checklstring(L1, getnum_aux(L, L1, ref pc), out int slen);
                        ReadOnlySpan<char> name = getstring_aux(L, ref pc);
                        ReadOnlySpan<char> mode = getstring_aux(L, ref pc);
                        luaL_loadbufferx(L1, new ReadOnlySpan<byte>(s, slen), name.ToString(), mode.ToString());
                        break;
                    }

                case "newmetatable":
                    lua_pushboolean(L1, luaL_newmetatable(L1, getstring_aux(L, ref pc).ToString()));
                    break;

                case "newtable":
                    lua_newtable(L1);
                    break;

                case "newthread":
                    lua_newthread(L1);
                    break;

                case "resetthread":
                    lua_pushinteger(L1, lua_resetthread(L1)); /* deprecated */
                    break;

                case "newuserdata":
                    lua_newuserdata(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "next":
                    lua_next(L1, -2);
                    break;

                case "objsize":
                    lua_pushinteger(L1, (long)lua_rawlen(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "pcall":
                    {
                        int narg = getnum_aux(L, L1, ref pc);
                        int nres = getnum_aux(L, L1, ref pc);
                        status = lua_pcall(L1, narg, nres, getnum_aux(L, L1, ref pc));
                        break;
                    }

                case "pcallk":
                    {
                        int narg = getnum_aux(L, L1, ref pc);
                        int nres = getnum_aux(L, L1, ref pc);
                        int i = getindex_aux(L, L1, ref pc);
                        status = lua_pcallk(L1, narg, nres, 0, i, &Cfunck);
                        break;
                    }

                case "pop":
                    lua_pop(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "printstack":
//       int n = getnum_aux(L, L1, ref pc);
//       if (n != 0) {
//         lua_printvalue(s2v(L->ci->func.p + n));
//         printf("\n");
//       }
//       else lua_printstack(L1);
                    throw new NotImplementedException();
                    break;

                case "print":
//       const char *msg = getstring_aux(L, ref pc);
//       printf("%s\n", msg);
                    throw new NotImplementedException();
                    break;

                case "warningC":
                    {
                        ReadOnlySpan<char> msg = getstring_aux(L, ref pc);
                        lua_warning(L1, new string(msg), true);
                        break;
                    }

                case "warning":
                    {
                        ReadOnlySpan<char> msg = getstring_aux(L, ref pc);
                        lua_warning(L1, msg, false);
                        break;
                    }

                case "pushbool":
                    lua_pushboolean(L1, getnum_aux(L, L1, ref pc) != 0);
                    break;

                case "pushcclosure":
                    lua_pushcclosure(L1, &testC, getnum_aux(L, L1, ref pc));
                    break;

                case "pushint":
                    lua_pushinteger(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "pushnil":
                    lua_pushnil(L1);
                    break;

                case "pushnum":
                    lua_pushnumber(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "pushstatus":
                    lua_pushstring(L1, statcodes[status]);
                    break;

                case "pushstring":
                    lua_pushstring(L1, getstring_aux(L, ref pc).ToString());
                    break;

                case "pushupvalueindex":
                    lua_pushinteger(L1, lua_upvalueindex(getnum_aux(L, L1, ref pc)));
                    break;

                case "pushvalue":
                    lua_pushvalue(L1, getindex_aux(L, L1, ref pc));
                    break;

                case "pushfstringI":
                    lua_pushfstring(L1, lua_tonetstring(L, -2), (int)lua_tointeger(L, -1));
                    break;

                case "pushfstringS":
                    lua_pushfstring(L1, lua_tonetstring(L, -2), lua_tonetstring(L, -1));
                    break;

                case "pushfstringP":
                    lua_pushfstring(L1, lua_tonetstring(L, -2), (nint)lua_topointer(L, -1));
                    break;

                case "rawget":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        lua_rawget(L1, t);
                        break;
                    }

                case "rawgeti":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        lua_rawgeti(L1, t, getnum_aux(L, L1, ref pc));
                        break;
                    }

                case "rawgetp":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        lua_rawgetp(L1, t, (void*)getnum_aux(L, L1, ref pc));
                        break;
                    }

                case "rawset":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        lua_rawset(L1, t);
                        break;
                    }

                case "rawseti":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        lua_rawseti(L1, t, getnum_aux(L, L1, ref pc));
                        break;
                    }

                case "rawsetp":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        lua_rawsetp(L1, t, (void*)getnum_aux(L, L1, ref pc));
                        break;
                    }

                case "remove":
                    lua_remove(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "replace":
                    lua_replace(L1, getindex_aux(L, L1, ref pc));
                    break;

                case "resume":
                    {
                        int i = getindex_aux(L, L1, ref pc);
                        int nres;
                        status = lua_resume(lua_tothread(L1, i), L, getnum_aux(L, L1, ref pc), &nres);
                        break;
                    }

                case "traceback":
                    {
                        ReadOnlySpan<char> msg = getstring_aux(L, ref pc);
                        int level = getnum_aux(L, L1, ref pc);
                        luaL_traceback(L1, L1, msg.ToString(), level);
                        break;
                    }

                case "threadstatus":
                    lua_pushstring(L1, statcodes[lua_status(L1)]);
                    break;

                case "alloccount":
                    l_memcontrol->countlimit = getnum_aux(L, L1, ref pc);
                    break;

                case "return":
                    {
                        int n = getnum_aux(L, L1, ref pc);
                        if (L1 != L)
                        {
                            for (int i = 0; i < n; i++)
                            {
                                int idx = -(n - i);
                                switch (lua_type(L1, idx))
                                {
                                    case LUA_TBOOLEAN:
                                        lua_pushboolean(L, lua_toboolean(L1, idx));
                                        break;
                                    default:
                                        lua_pushstring(L, lua_tostring(L1, idx));
                                        break;
                                }
                            }
                        }

                        return n;
                    }

                case "rotate":
                    {
                        int i = getindex_aux(L, L1, ref pc);
                        lua_rotate(L1, i, getnum_aux(L, L1, ref pc));
                        break;
                    }

                case "setfield":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        ReadOnlySpan<char> s = getstring_aux(L, ref pc);
                        lua_setfield(L1, t, s.ToString());
                        break;
                    }

                case "seti":
                    {
                        int t = getindex_aux(L, L1, ref pc);
                        lua_seti(L1, t, getnum_aux(L, L1, ref pc));
                        break;
                    }

                case "setglobal":
                    {
                        ReadOnlySpan<char> s = getstring_aux(L, ref pc);
                        lua_setglobal(L1, s.ToString());
                        break;
                    }

                case "sethook":
                    {
                        int mask = getnum_aux(L, L1, ref pc);
                        int count = getnum_aux(L, L1, ref pc);
                        ReadOnlySpan<char> s = getstring_aux(L, ref pc);
                        sethookaux(L1, (byte)mask, count, s.ToString());
                        break;
                    }

                case "setmetatable":
                    lua_setmetatable(L1, getindex_aux(L, L1, ref pc));
                    break;

                case "settable":
                    lua_settable(L1, getindex_aux(L, L1, ref pc));
                    break;

                case "settop":
                    lua_settop(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "testudata":
                    {
                        int i = getindex_aux(L, L1, ref pc);
                        lua_pushboolean(L1, luaL_testudata(L1, i, getstring_aux(L, ref pc).ToString()) != null);
                        break;
                    }

                case "error":
                    lua_error(L1);
                    break;

                case "abort":
                    Environment.Exit(-1);
                    break;

                case "throw":
                    luaL_error(L1, "C++");
                    break;

                case "tobool":
                    lua_pushboolean(L1, lua_toboolean(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "tocfunction":
                    lua_pushcfunction(L1, lua_tocfunction(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "tointeger":
                    lua_pushinteger(L1, lua_tointeger(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "tonumber":
                    lua_pushnumber(L1, lua_tonumber(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "topointer":
                    lua_pushlightuserdata(L1, lua_topointer(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "touserdata":
                    lua_pushlightuserdata(L1, lua_touserdata(L1, getindex_aux(L, L1, ref pc)));
                    break;

                case "tostring":
                    {
                        string? s = lua_tonetstring(L1, getindex_aux(L, L1, ref pc));
                        lua_pushstring(L1, s);
                        break;
                    }

                case "Ltolstring":
                    luaL_tolstring(L1, getindex_aux(L, L1, ref pc), out _);
                    break;

                case "type":
                    lua_pushstring(L1, luaL_typename(L1, getnum_aux(L, L1, ref pc)));
                    break;

                case "xmove":
                    {
                        int f = getindex_aux(L, L1, ref pc);
                        int t = getindex_aux(L, L1, ref pc);
                        lua_State* fs = f == 0 ? L1 : lua_tothread(L1, f);
                        lua_State* ts = t == 0 ? L1 : lua_tothread(L1, t);
                        int n = getnum_aux(L, L1, ref pc);
                        if (n == 0)
                        {
                            n = lua_gettop(fs);
                        }

                        lua_xmove(fs, ts, n);
                        break;
                    }

                case "isyieldable":
                    lua_pushboolean(L1, lua_isyieldable(lua_tothread(L1, getindex_aux(L, L1, ref pc))));
                    break;

                case "yield":
                    return lua_yield(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "yieldk":
                    {
                        int nres = getnum_aux(L, L1, ref pc);
                        int i = getindex_aux(L, L1, ref pc);
                        return lua_yieldk(L1, nres, i, &Cfunck);
                        break;
                    }

                case "toclose":
                    lua_toclose(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "closeslot":
                    lua_closeslot(L1, getnum_aux(L, L1, ref pc));
                    break;

                case "argerror":
                    {
                        int arg = getnum_aux(L, L1, ref pc);
                        luaL_argerror(L1, arg, getstring_aux(L, ref pc).ToString());
                        break;
                    }

                default:
                    luaL_error(L, "unknown instruction %s", inst.ToString());
                    break;
            }
        }

        return 0;
    }

    private static int testC(lua_State* L)
    {
        lua_State* L1;
        string pc;
        if (lua_isuserdata(L, 1))
        {
            L1 = getstate(L);
            pc = luaL_checknetstring(L, 2);
        }
        else if (lua_isthread(L, 1))
        {
            L1 = lua_tothread(L, 1);
            pc = luaL_checknetstring(L, 2);
        }
        else
        {
            L1 = L;
            pc = luaL_checknetstring(L, 1);
        }

        return runC(L, L1, pc);
    }

    private static int Cfunc(lua_State* L)
    {
        return runC(L, L, lua_tonetstring(L, lua_upvalueindex(1)));
    }

    private static int Cfunck(lua_State* L, int status, nint ctx)
    {
        lua_pushstring(L, statcodes[status]);
        lua_setglobal(L, "status");
        lua_pushinteger(L, ctx);
        lua_setglobal(L, "ctx");
        return runC(L, L, lua_tonetstring(L, (int)ctx));
    }

    private static int makeCfunc(lua_State* L)
    {
        luaL_checkstring(L, 1);
        lua_pushcclosure(L, &Cfunc, lua_gettop(L));
        return 1;
    }

    /*
    ** {======================================================
    ** tests for C hooks
    ** =======================================================
    */

    private static readonly string[] events = ["call", "ret", "line", "count", "tailcall"];

    /*
    ** C hook that runs the C script stored in registry.C_HOOK[L]
    */
    private static void Chook(lua_State* L, ref lua_Debug ar)
    {
        lua_getfield(L, LUA_REGISTRYINDEX, "C_HOOK");
        lua_pushlightuserdata(L, L);
        lua_gettable(L, -2); /* get C_HOOK[L] (script saved by sethookaux) */
        string? scpt = lua_tonetstring(L, -1); /* not very religious (string will be popped) */
        lua_pop(L, 2); /* remove C_HOOK and script */
        lua_pushstring(L, events[ar.@event]); /* may be used by script */
        lua_pushinteger(L, ar.currentline); /* may be used by script */
        runC(L, L, scpt); /* run script from C_HOOK[L] */
    }

    /*
     ** sets 'registry.C_HOOK[L] = scpt' and sets 'Chook' as a hook
     */
    private static void sethookaux(lua_State* L, byte mask, int count, string script)
    {
        if (string.IsNullOrEmpty(script))
        {
            /* no script? */
            lua_sethook(L, null, 0, 0); /* turn off hooks */
            return;
        }

        lua_getfield(L, LUA_REGISTRYINDEX, "C_HOOK"); /* get C_HOOK table */
        if (!lua_istable(L, -1))
        {
            /* no hook table? */
            lua_pop(L, 1); /* remove previous value */
            lua_newtable(L); /* create new C_HOOK table */
            lua_pushvalue(L, -1);
            lua_setfield(L, LUA_REGISTRYINDEX, "C_HOOK"); /* register it */
        }

        lua_pushlightuserdata(L, L);
        lua_pushstring(L, script);
        lua_settable(L, -3); /* C_HOOK[L] = script */
        lua_sethook(L, &Chook, mask, count);
    }

    private static int sethook(lua_State* L)
    {
        if (lua_isnoneornil(L, 1))
        {
            lua_sethook(L, null, 0, 0); /* turn off hooks */
        }
        else
        {
            string scpt = luaL_checknetstring(L, 1);
            ReadOnlySpan<byte> smask = luaL_checkstring(L, 2);
            int count = (int)luaL_optinteger(L, 3, 0);
            byte mask = 0;
            if (!strchr(smask, 'c').IsEmpty)
            {
                mask |= LUA_MASKCALL;
            }

            if (!strchr(smask, 'r').IsEmpty)
            {
                mask |= LUA_MASKRET;
            }

            if (!strchr(smask, 'l').IsEmpty)
            {
                mask |= LUA_MASKLINE;
            }

            if (count > 0)
            {
                mask |= LUA_MASKCOUNT;
            }

            sethookaux(L, mask, count, scpt);
        }

        return 0;
    }

    private static int coresume(lua_State* L)
    {
        lua_State* co = lua_tothread(L, 1);
        luaL_argcheck(L, co != null, 1, "coroutine expected");
        int nres;
        int status = lua_resume(co, L, 0, &nres);
        if (status != LUA_OK && status != LUA_YIELD)
        {
            lua_pushboolean(L, false);
            lua_insert(L, -2);
            return 2; /* return false + error message */
        }

        lua_pushboolean(L, true);
        return 1;
    }

    private static int nonblock(lua_State* L)
    {
// #if !defined(LUA_USE_POSIX)
//
// #define nonblock	null
//
// #else

//   FILE *f = cast(luaL_Stream*, luaL_checkudata(L, 1, LUA_FILEHANDLE))->f;
//   int fd = fileno(f);
//   int flags = fcntl(fd, F_GETFL, 0);
//   flags |= O_NONBLOCK;
//   fcntl(fd, F_SETFL, flags);
//   return 0;
        throw new NotImplementedException();
// #endif
    }

    private static readonly luaL_Reg[] tests_funcs =
    [
        new("checkmemory", &lua_checkmemory),
        new("closestate", &closestate),
        new("d2s", &d2s),
        new("doonnewstack", &doonnewstack),
        new("doremote", &doremote),
        new("gccolor", &gc_color),
        new("gcage", &gc_age),
        new("gcstate", &gc_state),
        new("tracegc", &tracegc),
        new("pobj", &gc_printobj),
        new("getref", &getref),
        new("hash", &hash_query),
        new("log2", &log2_aux),
        new("limits", &get_limits),
        new("listcode", &listcode),
        new("printcode", &printcode),
        new("printallstack", &lua_printallstack),
        new("listk", &listk),
        new("listabslineinfo", &listabslineinfo),
        new("listlocals", &listlocals),
        new("loadlib", &loadlib),
        new("checkpanic", &checkpanic),
        new("newstate", &newstate),
        new("newuserdata", &newuserdata),
        new("num2int", &num2int),
        new("makeseed", &makeseed),
        new("pushuserdata", &pushuserdata),
        new("gcquery", &gc_query),
        new("querystr", &string_query),
        new("querytab", &table_query),
        new("codeparam", &test_codeparam),
        new("applyparam", &test_applyparam),
        new("ref", &tref),
        new("resume", &coresume),
        new("s2d", &s2d),
        new("sethook", &sethook),
        new("stacklevel", &stacklevel),
        new("sizes", &get_sizes),
        new("testC", &testC),
        new("makeCfunc", &makeCfunc),
        new("totalmem", &mem_query),
        new("alloccount", &alloc_count),
        new("allocfailnext", &alloc_failnext),
        new("trick", &settrick),
        new("udataval", &udataval),
        new("unref", &unref),
        new("upvalue", &upvalue),
        new("externKstr", &externKstr),
        new("externstr", &externstr),
        new("nonblock", &nonblock),
    ];

    private static void checkfinalmem()
    {
        Debug.Assert(l_memcontrol->numblocks == 0);
        Debug.Assert(l_memcontrol->total == 0);
    }

    private static int luaB_opentests(lua_State* L)
    {
        lua_Alloc f = lua_getallocf(L, out void* ud);
        lua_atpanic(L, &tpanic);
        lua_setwarnf(L, &warnf, L);
        lua_pushboolean(L, false);
        lua_setglobal(L, "_WARN"); /* _WARN = false */
        regcodes(L);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => checkfinalmem();
        Debug.Assert(f == (lua_Alloc)(&debug_realloc) && ud == l_memcontrol);
        lua_setallocf(L, f, ud); /* exercise this function */
        luaL_newlib(L, tests_funcs);
        return 1;
    }
#else
    private static void luai_tracegc(lua_State* L, bool f)
    {
    }

    public static void luai_openlibs(lua_State* L)
    {
        luaL_openlibs(L);
    }
#endif
}
