namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /*
    ** $Id: lgc.c $
    ** Garbage Collector
    ** See Copyright Notice in lua.h
    */

    /*
    ** Maximum number of elements to sweep in each single step.
    ** (Large enough to dissipate fixed overheads but small enough
    ** to allow small steps for the collector.)
    */
    private const int GCSWEEPMAX = 20;
    
    /*
    ** Cost (in work units) of running one finaliser.
    */
    private const int CWUFIN = 10;

    /* mask with all colour bits */
    private const byte maskcolours = (1 << BLACKBIT) | WHITEBITS;

    /* mask with all GC bits */
    private const byte maskgcbits = maskcolours | AGEBITS;

    /* macro to erase all colour bits then set only the current white bit */
    private static void makewhite(global_State* g, GCObject* x)
    {
        x->marked = (byte)(x->marked & ~maskcolours | luaC_white(g));
    }

    /* make an object grey (neither white nor black) */
    private static void set2grey(GCObject* x)
    {
        resetbits(ref x->marked, maskcolours);
    }

    /* make an object black (coming from any colour) */
    private static void set2black(GCObject* x)
    {
        x->marked = (byte)(x->marked & ~WHITEBITS | bitmask(BLACKBIT));
    }

    private static bool valiswhite(TValue* x)
    {
        return iscollectable(x) && iswhite(gcvalue(x));
    }

    private static bool keyiswhite(Node* n)
    {
        return keyiscollectable(n) && iswhite(gckey(n));
    }

    // /*
// ** Protected access to objects in values
// */
// #define gcvalueN(o)     (iscollectable(o) ? gcvalue(o) : null)

    /*
    ** Access to collectable objects in array part of tables
    */
    private static GCObject* gcvalarr(Table* t, ulong i)
    {
        return (*getArrTag(t, i) & BIT_ISCOLLECTABLE) != 0 ? getArrVal(t, i)->gc : null;
    }

    private static void markvalue(global_State* g, TValue* o)
    {
        checkliveness(mainthread(g), o);
        if (valiswhite(o))
        {
            reallymarkobject(g, gcvalue(o));
        }
    }

    private static void markkey(global_State* g, Node* n)
    {
        if (keyiswhite(n))
        {
            reallymarkobject(g, gckey(n));
        }
    }

    private static void markobject(global_State* g, GCObject* t)
    {
        if (iswhite(t))
        {
            reallymarkobject(g, obj2gco(t));
        }
    }

    /*
     ** mark an object that can be null (either because it is really optional,
     ** or it was stripped as debug info, or inside an uncompleted structure)
     */
    private static void markobjectN(global_State* g, GCObject* t)
    {
        if (t != null)
        {
            markobject(g, t);
        }
    }

    private static partial void reallymarkobject(global_State* g, GCObject* o);

    private static partial void atomic(lua_State* L);

    private static partial void entersweep(lua_State* L);

    /*
     ** {======================================================
     ** Generic functions
     ** =======================================================
     */

    /*
    ** one after last element in a hash array
    */
    private static Node* gnodelast(Table* h)
    {
        return gnode(h, sizenode(h));
    }

    private static long objsize(GCObject* o)
    {
        return o->tt switch
        {
            LUA_VTABLE => luaH_size(gco2t(o)),
            LUA_VLCL => sizeLclosure(gco2lcl(o)->nupvalues),
            LUA_VCCL => sizeCclosure(gco2ccl(o)->nupvalues),
            LUA_VUSERDATA => sizeudata(gco2u(o)->nuvalue, gco2u(o)->len),
            LUA_VPROTO => luaF_protosize(gco2p(o)),
            LUA_VTHREAD => luaE_threadsize(gco2th(o)),
            LUA_VSHRSTR => sizestrshr((uint)gco2ts(o)->shrlen),
            LUA_VLNGSTR => luaS_sizelngstr(gco2ts(o)->u.lnglen, gco2ts(o)->shrlen),
            LUA_VUPVAL => sizeof(UpVal),
            _ => throw new InvalidOperationException(),
        };
    }

    private static GCObject** getgclist(GCObject* o)
    {
        switch (o->tt)
        {
            case LUA_VTABLE: return &gco2t(o)->gclist;
            case LUA_VLCL: return &gco2lcl(o)->gclist;
            case LUA_VCCL: return &gco2ccl(o)->gclist;
            case LUA_VTHREAD: return &gco2th(o)->gclist;
            case LUA_VPROTO: return &gco2p(o)->gclist;
            case LUA_VUSERDATA:
                {
                    Udata* u = gco2u(o);
                    Debug.Assert(u->nuvalue > 0);
                    return &u->gclist;
                }
            default:
                throw new InvalidOperationException();
        }
    }

    /*
     ** Link a collectable object 'o' with a known type into the list 'p'.
     ** (Must be a macro to access the 'gclist' field in different types.)
     */
    private static void linkgclist(lua_State* o, ref GCObject* p)
    {
        linkgclist_(obj2gco(o), &o->gclist, ref p);
    }

    private static void linkgclist_(GCObject* o, GCObject** pnext, ref GCObject* list)
    {
        Debug.Assert(!isgrey(o)); /* cannot be in a grey list */
        *pnext = list;
        list = o;
        set2grey(o); /* now it is */
    }

    /*
     ** Link a generic collectable object 'o' into the list 'p'.
     */
    private static void linkobjgclist(GCObject* o, ref GCObject* p)
    {
        linkgclist_(obj2gco(o), getgclist(o), ref p);
    }

    /*
    ** Clear keys for empty entries in tables. If entry is empty, mark its
    ** entry as dead. This allows the collection of the key, but keeps its
    ** entry in the table: its removal could break a chain and could break
    ** a table traversal.  Other places never manipulate dead keys, because
    ** its associated empty value is enough to signal that the entry is
    ** logically empty.
    */
    private static void clearkey(Node* n)
    {
        Debug.Assert(isempty(gval(n)));
        if (keyiscollectable(n))
        {
            setdeadkey(n); /* unused key; remove it */
        }
    }

// /*
// ** tells whether a key or value can be cleared from a weak
// ** table. Non-collectable objects are never removed from weak
// ** tables. Strings behave as 'values', so are never removed too. for
// ** other objects: if really collected, cannot keep them; for objects
// ** being finalized, keep them in keys, but not in values
// */
// static int iscleared (global_State *g, const GCObject *o) {
//   if (o == null) return 0;  /* non-collectable value */
//   else if (novariant(o->tt) == LUA_TSTRING) {
//     markobject(g, o);  /* strings are 'values', so are never weak */
//     return 0;
//   }
//   else return iswhite(o);
// }

    /*
    ** Barrier that moves collector forward, that is, marks the white object
    ** 'v' being pointed by the black object 'o'.  In the generational
    ** mode, 'v' must also become old, if 'o' is old; however, it cannot
    ** be changed directly to OLD, because it may still point to non-old
    ** objects. So, it is marked as OLD0. In the next cycle it will become
    ** OLD1, and in the next it will finally become OLD (regular old). By
    ** then, any object it points to will also be old.  If called in the
    ** incremental sweep phase, it clears the black object to white (sweep
    ** it) to avoid other barrier calls for this same object. (That cannot
    ** be done is generational mode, as its sweep does not distinguish
    ** white from dead.)
    */
    internal static void luaC_barrier_(lua_State* L, GCObject* o, GCObject* v)
    {
        global_State* g = G(L);
        Debug.Assert(isblack(o) && iswhite(v) && !isdead(g, v) && !isdead(g, o));
        if (keepinvariant(g))
        {
            /* must keep invariant? */
            reallymarkobject(g, v); /* restore invariant */
            if (isold(o))
            {
                Debug.Assert(!isold(v)); /* white object could not be old */
                setage(v, G_OLD0); /* restore generational invariant */
            }
        }
        else
        {
            /* sweep phase */
            Debug.Assert(issweepphase(g));
            if (g->gckind != KGC_GENMINOR) /* incremental mode? */
            {
                makewhite(g, o); /* mark 'o' as white to avoid other barriers */
            }
        }
    }

    /*
     ** barrier that moves collector backward, that is, mark the black object
     ** pointing to a white object as gray again.
     */
    internal static void luaC_barrierback_(lua_State* L, GCObject* o)
    {
        global_State* g = G(L);
        Debug.Assert(isblack(o) && !isdead(g, o));
        Debug.Assert((g->gckind != KGC_GENMINOR) || (isold(o) && getage(o) != G_TOUCHED1));
        if (getage(o) == G_TOUCHED2) /* already in gray list? */
        {
            set2grey(o); /* make it gray to become touched1 */
        }
        else /* link it in 'grayagain' and paint it gray */
        {
            linkobjgclist(o, ref g->greyagain);
        }

        if (isold(o)) /* generational mode? */
        {
            setage(o, G_TOUCHED1); /* touched in current cycle */
        }
    }

    internal static void luaC_fix(lua_State* L, GCObject* o)
    {
        global_State* g = G(L);
        Debug.Assert(g->allgc == o); /* object must be 1st in 'allgc' list! */
        set2grey(o); /* they will be gray forever */
        setage(o, G_OLD); /* and old forever */
        g->allgc = o->next; /* remove object from 'allgc' list */
        o->next = g->fixedgc; /* link it to 'fixedgc' list */
        g->fixedgc = o;
    }

    /*
     ** create a new collectable object (with given type, size, and offset)
     ** and link it to 'allgc' list.
     */
    internal static GCObject* luaC_newobjdt(lua_State* L, byte tt, long sz, long offset)
    {
        global_State* g = G(L);
        byte* p = (byte*)luaM_newobject(L, novariant(tt), sz);
        GCObject* o = (GCObject*)(p + offset);
        o->marked = luaC_white(g);
        o->tt = tt;
        o->next = g->allgc;
        g->allgc = o;
        return o;
    }

    /// <summary>
    /// Create a new collectable object with no offset.
    /// </summary>
    internal static GCObject* luaC_newobj(lua_State* L, byte tt, long sz)
    {
        return luaC_newobjdt(L, tt, sz, 0);
    }

    /*
     ** {======================================================
     ** Mark functions
     ** =======================================================
     */
    
    /*
     ** Mark an object.  Userdata with no user values, strings, and closed
     ** upvalues are visited and turned black here.  Open upvalues are
     ** already indirectly linked through their respective threads in the
     ** 'twups' list, so they don't go to the grey list; nevertheless, they
     ** are kept grey to avoid barriers, as their values will be revisited
     ** by the thread or by 'remarkupvals'.  Other objects are added to the
     ** grey list to be visited (and turned black) later.  Both userdata and
     ** upvalues can call this function recursively, but this recursion goes
     ** for at most two levels: An upvalue cannot refer to another upvalue
     ** (only closures can), and a userdata's metatable must be a table.
     */
    private static partial void reallymarkobject(global_State* g, GCObject* o)
    {
        g->GCmarked += objsize(o);
        switch (o->tt)
        {
            case LUA_VSHRSTR:
            case LUA_VLNGSTR:
                set2black(o);  /* nothing to visit */
                break;

            case LUA_VUPVAL:
                {
                    UpVal* uv = gco2upv(o);
                    if (upisopen(uv))
                    {
                        set2grey((GCObject*)uv); /* open upvalues are kept gray */
                    }
                    else
                    {
                        set2black((GCObject*)uv); /* closed upvalues are visited here */
                    }

                    markvalue(g, uv->v.p); /* mark its content */
                    break;
                }

            case LUA_VUSERDATA:
                {
                    Udata* u = gco2u(o);
                    if (u->nuvalue == 0)
                    {
                        /* no user values? */
                        markobjectN(g, (GCObject*)u->metatable); /* mark its metatable */
                        set2black((GCObject*)u); /* nothing else to mark */
                        break;
                    }

                    /* else... */
                    goto case LUA_VLCL;
                } /* FALLTHROUGH */

            case LUA_VLCL:
            case LUA_VCCL:
            case LUA_VTABLE:
            case LUA_VTHREAD:
            case LUA_VPROTO:
                linkobjgclist(o, ref g->grey); /* to be visited later */
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    /*
     ** mark metamethods for basic types
     */
    private static void markmt(global_State* g)
    {
        for (int i = 0; i < LUA_NUMTYPES; i++)
        {
            markobjectN(g, (GCObject*)g->mt[i]);
        }
    }

    /*
     ** mark all objects in list of being-finalised
     */
    private static void markbeingfnz(global_State* g)
    {
        for (GCObject* o = g->tobefnz; o != null; o = o->next)
        {
            markobject(g, o);
        }
    }

    /*
     ** For each non-marked thread, simulates a barrier between each open
     ** upvalue and its value. (If the thread is collected, the value will be
     ** assigned to the upvalue, but then it can be too late for the barrier
     ** to act. The "barrier" does not need to check colors: A non-marked
     ** thread must be young; upvalues cannot be older than their threads; so
     ** any visited upvalue must be young too.) Also removes the thread from
     ** the list, as it was already visited. Removes also threads with no
     ** upvalues, as they have nothing to be checked. (If the thread gets an
     ** upvalue later, it will be linked in the list again.)
     */
    private static void remarkupvals(global_State* g)
    {
        lua_State** p = &g->twups;

        lua_State* thread;
        while ((thread = *p) != null)
        {
            if (!iswhite((GCObject*)thread) && thread->openupval != null)
            {
                p = &thread->twups; /* keep marked thread with upvalues in the list */
            }
            else
            {
                /* thread is not marked or without upvalues */
                Debug.Assert(!isold((GCObject*)thread) || thread->openupval == null);
                *p = thread->twups; /* remove thread from the list */
                thread->twups = thread; /* mark that it is out of list */
                for (UpVal* uv = thread->openupval; uv != null; uv = uv->u.open.next)
                {
                    Debug.Assert(getage((GCObject*)uv) <= getage((GCObject*)thread));
                    if (!iswhite((GCObject*)uv))
                    {
                        /* upvalue already visited? */
                        Debug.Assert(upisopen(uv) && isgrey((GCObject*)uv));
                        markvalue(g, uv->v.p); /* mark its value */
                    }
                }
            }
        }
    }

    private static void cleargreylists(global_State* g)
    {
        g->grey = g->greyagain = null;
        g->weak = g->allweak = g->ephemeron = null;
    }

    /*
     ** mark root set and reset all grey lists, to start a new collection.
     ** 'GCmarked' is initialised to count the total number of live bytes
     ** during a cycle.
     */
    private static void restartcollection(global_State* g)
    {
        cleargreylists(g);
        g->GCmarked = 0;
        markobject(g, (GCObject*)mainthread(g));
        markvalue(g, &g->l_registry);
        markmt(g);
        markbeingfnz(g); /* mark any finalising object left from previous cycle */
    }

    /* }====================================================== */

    /*
    ** {======================================================
    ** Traverse functions
    ** =======================================================
    */
    
    
    /*
    ** Check whether object 'o' should be kept in the 'grayagain' list for
    ** post-processing by 'correctgreylist'. (It could put all old objects
    ** in the list and leave all the work to 'correctgreylist', but it is
    ** more efficient to avoid adding elements that will be removed.) Only
    ** TOUCHED1 objects need to be in the list. TOUCHED2 doesn't need to go
    ** back to a gray list, but then it must become OLD. (That is what
    ** 'correctgreylist' does when it finds a TOUCHED2 object.)
    ** This function is a no-op in incremental mode, as objects cannot be
    ** marked as touched in that mode.
    */
    private static void genlink(global_State* g, GCObject* o)
    {
        Debug.Assert(isblack(o));
        if (getage(o) == G_TOUCHED1)
        {
            /* touched in this cycle? */
            linkobjgclist(o, ref g->greyagain); /* link it back in 'grayagain' */
        } /* everything else do not need to be linked back */
        else if (getage(o) == G_TOUCHED2)
        {
            setage(o, G_OLD); /* advance age */
        }
    }

// /*
// ** Traverse a table with weak values and link it to proper list. During
// ** propagate phase, keep it in 'grayagain' list, to be revisited in the
// ** atomic phase. In the atomic phase, if table has any white value,
// ** put it in 'weak' list, to be cleared; otherwise, call 'genlink'
// ** to check table age in generational mode.
// */
// static void traverseweakvalue (global_State *g, Table *h) {
//   Node *n, *limit = gnodelast(h);
//   /* if there is array part, assume it may have white values (it is not
//      worth traversing it now just to check) */
//   int hasclears = (h->asize > 0);
//   for (n = gnode(h, 0); n < limit; n++) {  /* traverse hash part */
//     if (isempty(gval(n)))  /* entry is empty? */
//       clearkey(n);  /* clear its key */
//     else {
//       Debug.Assert(!keyisnil(n));
//       markkey(g, n);
//       if (!hasclears && iscleared(g, gcvalueN(gval(n))))  /* a white value? */
//         hasclears = 1;  /* table will have to be cleared */
//     }
//   }
//   if (g->gcstate == GCSpropagate)
//     linkgclist(h, g->grayagain);  /* must retraverse it in atomic phase */
//   else if (hasclears)
//       linkgclist(h, g->weak);  /* has to be cleared later */
//   else
//     genlink(g, obj2gco(h));
// }

    /*
    ** Traverse the array part of a table.
    */
    private static int traversearray(global_State* g, Table* h)
    {
        uint asize = h->asize;
        int marked = 0; /* true if some object is marked in this traversal */
        for (uint i = 0; i < asize; i++)
        {
            GCObject* o = gcvalarr(h, i);
            if (o != null && iswhite(o))
            {
                marked = 1;
                reallymarkobject(g, o);
            }
        }

        return marked;
    }

    /*
     ** Traverse an ephemeron table and link it to proper list. Returns true
     ** iff any object was marked during this traversal (which implies that
     ** convergence has to continue). During propagation phase, keep table
     ** in 'greyagain' list, to be visited again in the atomic phase. In
     ** the atomic phase, if table has any white->white entry, it has to
     ** be revisited during ephemeron convergence (as that key may turn
     ** black). Otherwise, if it has any white key, table has to be cleared
     ** (in the atomic phase). In generational mode, some tables
     ** must be kept in some grey list for post-processing; this is done
     ** by 'genlink'.
     */
    private static bool traverseephemeron(global_State* g, Table* h, bool inv)
    {
//   int hasclears = 0;  /* true if table has white keys */
//   int hasww = 0;  /* true if table has entry "white-key -> white-value" */
//   unsigned int i;
//   unsigned int nsize = sizenode(h);
//   int marked = traversearray(g, h);  /* traverse array part */
//   /* traverse hash part; if 'inv', traverse descending
//      (see 'convergeephemerons') */
//   for (i = 0; i < nsize; i++) {
//     Node *n = inv ? gnode(h, nsize - 1 - i) : gnode(h, i);
//     if (isempty(gval(n)))  /* entry is empty? */
//       clearkey(n);  /* clear its key */
//     else if (iscleared(g, gckeyN(n))) {  /* key is not marked (yet)? */
//       hasclears = 1;  /* table must be cleared */
//       if (valiswhite(gval(n)))  /* value not marked yet? */
//         hasww = 1;  /* white-white entry */
//     }
//     else if (valiswhite(gval(n))) {  /* value not marked yet? */
//       marked = 1;
//       reallymarkobject(g, gcvalue(gval(n)));  /* mark it now */
//     }
//   }
//   /* link table into proper list */
//   if (g->gcstate == GCSpropagate)
//     linkgclist(h, g->grayagain);  /* must retraverse it in atomic phase */
//   else if (hasww)  /* table has white->white entries? */
//     linkgclist(h, g->ephemeron);  /* have to propagate again */
//   else if (hasclears)  /* table has white keys? */
//     linkgclist(h, g->allweak);  /* may have to clean white keys */
//   else
//     genlink(g, obj2gco(h));  /* check whether collector still needs to see it */
//   return marked;
        throw new NotImplementedException();
    }

    private static void traversestrongtable(global_State* g, Table* h)
    {
        Node* limit = gnodelast(h);
        traversearray(g, h);
        for (Node* n = gnode(h, 0); n < limit; n++)
        {
            /* traverse hash part */
            if (isempty(gval(n))) /* entry is empty? */
            {
                clearkey(n); /* clear its key */
            }
            else
            {
                Debug.Assert(!keyisnil(n));
                markkey(g, n);
                markvalue(g, gval(n));
            }
        }

        genlink(g, obj2gco(h));
    }

    /*
     ** (result & 1) iff weak values; (result & 2) iff weak keys.
     */
    private static int getmode(global_State* g, Table* h)
    {
        TValue* mode = gfasttm(g, h->metatable, TMS.MODE);
        if (mode == null || !ttisstring(mode))
        {
            return 0; /* ignore non-string modes */
        }

//     const char *smode = getstr(tsvalue(mode));
//     const char *weakkey = strchr(smode, 'k');
//     const char *weakvalue = strchr(smode, 'v');
//     return ((weakkey != null) << 1) | (weakvalue != null);
        throw new NotImplementedException();
    }

    private static long traversetable(global_State* g, Table* h)
    {
        markobjectN(g, (GCObject*)h->metatable);
        switch (getmode(g, h))
        {
            case 0: /* not weak */
                traversestrongtable(g, h);
                break;

            case 1: /* weak values */
//       traverseweakvalue(g, h);
//       break;
                throw new NotImplementedException();

            case 2: /* weak keys */
//       traverseephemeron(g, h, 0);
//       break;
                throw new NotImplementedException();

            case 3: /* all weak; nothing to traverse */
//       if (g->gcstate == GCSpropagate)
//         linkgclist(h, g->grayagain);  /* must visit again its metatable */
//       else
//         linkgclist(h, g->allweak);  /* must clear collected entries */
//       break;
                throw new NotImplementedException();
        }

        return 1 + 2 * sizenode(h) + h->asize;
    }

    private static long traverseudata(global_State* g, Udata* u)
    {
        markobjectN(g, (GCObject*)u->metatable); /* mark its metatable */
        for (int i = 0; i < u->nuvalue; i++)
        {
            markvalue(g, &((TValue*)u->uv)[i]);
        }

        genlink(g, obj2gco(u));
        return 1 + u->nuvalue;
    }

    /*
     ** Traverse a prototype. (While a prototype is being build, its
     ** arrays can be larger than needed; the extra slots are filled with
     ** null, so the use of 'markobjectN')
     */
    private static long traverseproto(global_State* g, Proto* f)
    {
        markobjectN(g, (GCObject*)f->source);
        for (int i = 0; i < f->sizek; i++) /* mark literals */
        {
            markvalue(g, &f->k[i]);
        }

        for (int i = 0; i < f->sizeupvalues; i++) /* mark upvalue names */
        {
            markobjectN(g, (GCObject*)f->upvalues[i].name);
        }

        for (int i = 0; i < f->sizep; i++) /* mark nested protos */
        {
            markobjectN(g, (GCObject*)f->p[i]);
        }

        for (int i = 0; i < f->sizelocvars; i++) /* mark local-variable names */
        {
            markobjectN(g, (GCObject*)f->locvars[i].varname);
        }

        return 1 + f->sizek + f->sizeupvalues + f->sizep + f->sizelocvars;
    }

    private static long traverseCclosure(global_State* g, CClosure* cl)
    {
        for (int i = 0; i < cl->nupvalues; i++) /* mark its upvalues */
        {
            markvalue(g, CClosure.GetUpValuePtr(cl, i));
        }

        return 1 + cl->nupvalues;
    }

    /*
     ** Traverse a Lua closure, marking its prototype and its upvalues.
     ** (Both can be null while closure is being created.)
     */
    private static long traverseLclosure(global_State* g, LClosure* cl)
    {
        markobjectN(g, (GCObject*)cl->p); /* mark its prototype */
        for (int i = 0; i < cl->nupvalues; i++)
        {
            /* visit its upvalues */
            UpVal* uv = LClosure.GetUpValue(cl, i);
            markobjectN(g, (GCObject*)uv); /* mark upvalue */
        }

        return 1 + cl->nupvalues;
    }

    /*
     ** Traverse a thread, marking the elements in the stack up to its top
     ** and cleaning the rest of the stack in the final traversal. That
     ** ensures that the entire stack have valid (non-dead) objects.
     ** Threads have no barriers. In gen. mode, old threads must be visited
     ** at every cycle, because they might point to young objects.  In inc.
     ** mode, the thread can still be modified before the end of the cycle,
     ** and therefore it must be visited again in the atomic phase. To ensure
     ** these visits, threads must return to a gray list if they are not new
     ** (which can only happen in generational mode) or if the traverse is in
     ** the propagate phase (which can only happen in incremental mode).
     */
    private static long traversethread(global_State* g, lua_State* th)
    {
        StkId o = th->stack.p;
        if (isold((GCObject*)th) || g->gcstate == GCSpropagate)
        {
            linkgclist(th, ref g->greyagain); /* insert into 'grayagain' list */
        }

        if (o == null!)
        {
            return 0; /* stack not completely built yet */
        }

        Debug.Assert(g->gcstate == GCSatomic || th->openupval == null || isintwups(th));
        for (; o < th->top.p; o++) /* mark live elements in the stack */
        {
            markvalue(g, s2v(o));
        }

        for (UpVal* uv = th->openupval; uv != null; uv = uv->u.open.next)
        {
            markobject(g, (GCObject*)uv); /* open upvalues cannot be collected */
        }

        if (g->gcstate == GCSatomic)
        {
            /* final traversal? */
            if (!g->gcemergency)
            {
                luaD_shrinkstack(th); /* do not change stack in emergency cycle */
            }

            for (o = th->top.p; o < th->stack_last.p + EXTRA_STACK; o++)
            {
                setnilvalue(s2v(o)); /* clear dead stack slice */
            }

            /* 'remarkupvals' may have removed thread from 'twups' list */
            if (!isintwups(th) && th->openupval != null)
            {
                th->twups = g->twups; /* link it back to the list */
                g->twups = th;
            }
        }

        return 1 + (th->top.p - th->stack.p);
    }

    /*
     ** traverse one gray object, turning it to black. Return an estimate
     ** of the number of slots traversed.
     */
    private static long propagatemark(global_State* g)
    {
        GCObject* o = g->grey;
        nw2black(o);
        g->grey = *getgclist(o); /* remove from 'grey' list */
        return o->tt switch
        {
            LUA_VTABLE => traversetable(g, gco2t(o)),
            LUA_VUSERDATA => traverseudata(g, gco2u(o)),
            LUA_VLCL => traverseLclosure(g, gco2lcl(o)),
            LUA_VCCL => traverseCclosure(g, gco2ccl(o)),
            LUA_VPROTO => traverseproto(g, gco2p(o)),
            LUA_VTHREAD => traversethread(g, gco2th(o)),
            _ => throw new InvalidOperationException(),
        };
    }

    private static void propagateall(global_State* g)
    {
        while (g->grey != null)
        {
            propagatemark(g);
        }
    }

    /*
     ** Traverse all ephemeron tables propagating marks from keys to values.
     ** Repeat until it converges, that is, nothing new is marked. 'dir'
     ** inverts the direction of the traversals, trying to speed up
     ** convergence on chains in the same table.
     */
    private static void convergeephemerons(global_State* g)
    {
        bool changed;
        bool dir = false;
        do
        {
            GCObject* next = g->ephemeron; /* get ephemeron list */
            g->ephemeron = null; /* tables may return to this list when traversed */
            changed = false;

            GCObject* w;
            while ((w = next) != null)
            {
                /* for each ephemeron table */
                Table* h = gco2t(w);
                next = h->gclist; /* list is rebuilt during loop */
                nw2black((GCObject*)h); /* out of the list (for now) */
                if (traverseephemeron(g, h, dir))
                {
                    /* marked some value? */
                    propagateall(g); /* propagate changes */
                    changed = true; /* will have to revisit all ephemeron tables */
                }
            }

            dir = !dir; /* invert direction next time */
        } while (changed); /* repeat until no more changes */
    }

    /* }====================================================== */


    /*
     ** {======================================================
     ** Sweep Functions
     ** =======================================================
     */

    /*
     ** clear entries with unmarked keys from all weaktables in list 'l'
     */
    private static void clearbykeys(global_State* g, GCObject* l)
    {
        for (; l != null; l = gco2t(l)->gclist)
        {
            Table* h = gco2t(l);
            Node* limit = gnodelast(h);
            for (Node* n = gnode(h, 0); n < limit; n++)
            {
//       if (iscleared(g, gckeyN(n)))  /* unmarked key? */
//         setempty(gval(n));  /* remove entry */
//       if (isempty(gval(n)))  /* is entry empty? */
//         clearkey(n);  /* clear its key */
                throw new NotImplementedException();
            }
        }
    }

    /*
     ** clear entries with unmarked values from all weaktables in list 'l' up
     ** to element 'f'
     */
    private static void clearbyvalues(global_State* g, GCObject* l, GCObject* f)
    {
        for (; l != f; l = gco2t(l)->gclist)
        {
            Table* h = gco2t(l);
            Node* limit = gnodelast(h);
            uint asize = h->asize;
            for (uint i = 0; i < asize; i++)
            {
//       GCObject *o = gcvalarr(h, i);
//       if (iscleared(g, o))  /* value was collected? */
//         *getArrTag(h, i) = LUA_VEMPTY;  /* remove entry */
                throw new NotImplementedException();
            }

            for (Node* n = gnode(h, 0); n < limit; n++)
            {
//       if (iscleared(g, gcvalueN(gval(n))))  /* unmarked value? */
//         setempty(gval(n));  /* remove entry */
//       if (isempty(gval(n)))  /* is entry empty? */
//         clearkey(n);  /* clear its key */
                throw new NotImplementedException();
            }
        }
    }

    private static void freeupval(lua_State* L, UpVal* uv)
    {
        if (upisopen(uv))
        {
            luaF_unlinkupval(uv);
        }

        luaM_free(L, uv);
    }

    private static void freeobj(lua_State* L, GCObject* o)
    {
#if DEBUG
        long newmem = gettotalbytes(G(L)) - objsize(o);
#endif
        switch (o->tt)
        {
            case LUA_VPROTO:
                luaF_freeproto(L, gco2p(o));
                break;

            case LUA_VUPVAL:
                freeupval(L, gco2upv(o));
                break;

            case LUA_VLCL:
                {
                    LClosure* cl = gco2lcl(o);
                    luaM_freemem(L, cl, sizeLclosure(cl->nupvalues));
                    break;
                }

            case LUA_VCCL:
                {
                    CClosure* cl = gco2ccl(o);
                    luaM_freemem(L, cl, sizeCclosure(cl->nupvalues));
                    break;
                }

            case LUA_VTABLE:
                luaH_free(L, gco2t(o));
                break;

            case LUA_VTHREAD:
                luaE_freethread(L, gco2th(o));
                break;

            case LUA_VUSERDATA:
                {
                    Udata* u = gco2u(o);
                    luaM_freemem(L, o, sizeudata(u->nuvalue, u->len));
                    break;
                }

            case LUA_VSHRSTR:
                {
                    TString* ts = gco2ts(o);
                    luaS_remove(L, ts); /* remove it from hash table */
                    luaM_freemem(L, ts, sizestrshr((uint)ts->shrlen));
                    break;
                }

            case LUA_VLNGSTR:
                {
                    TString* ts = gco2ts(o);
                    if (ts->shrlen == LSTRMEM) /* must free external string? */
                    {
                        ts->falloc(ts->ud, ts->contents, ts->u.lnglen + 1, 0);
                    }

                    luaM_freemem(L, ts, luaS_sizelngstr(ts->u.lnglen, ts->shrlen));
                    break;
                }

            default:
                throw new InvalidOperationException();
        }

        Debug.Assert(gettotalbytes(G(L)) == newmem);
    }

    /*
    ** sweep at most 'countin' elements from a list of GCObjects erasing dead
    ** objects, where a dead object is one marked with the old (non current)
    ** white; change all non-dead objects back to white (and new), preparing
    ** for next collection cycle. Return where to continue the traversal or
    ** null if list is finished.
    */
    private static GCObject** sweeplist(lua_State* L, GCObject** p, long countin)
    {
        global_State* g = G(L);
        byte ow = otherwhite(g);
        int white = luaC_white(g); /* current white */
        while (*p != null && countin-- > 0)
        {
            GCObject* curr = *p;
            byte marked = curr->marked;
            if (isdeadm(ow, marked))
            {
                /* is 'curr' dead? */
                *p = curr->next; /* remove 'curr' from list */
                freeobj(L, curr); /* erase 'curr' */
            }
            else
            {
                /* change mark to 'white' and age to 'new' */
                curr->marked = (byte)((marked & ~maskgcbits) | white | G_NEW);
                p = &curr->next; /* go to next element */
            }
        }

        return (*p == null) ? null : p;
    }

    /*
     ** sweep a list until a live object (or end of list)
     */
    private static GCObject** sweeptolive(lua_State* L, GCObject** p)
    {
        GCObject** old = p;
        do
        {
            p = sweeplist(L, p, 1);
        } while (p == old);

        return p;
    }

    /* }====================================================== */

    /*
     ** {======================================================
     ** Finalisation
     ** =======================================================
     */

    /*
     ** If possible, shrink string table.
     */
    private static void checkSizes(lua_State* L, global_State* g)
    {
        if (!g->gcemergency)
        {
            if (g->strt.nuse < g->strt.size / 4) /* string table too big? */
            {
                luaS_resize(L, g->strt.size / 2);
            }
        }
    }

    /*
    ** Get the next udata to be finalised from the 'tobefnz' list, and
    ** link it back into the 'allgc' list.
    */
    private static GCObject* udata2finalise(global_State* g)
    {
        GCObject* o = g->tobefnz; /* get first element */
        Debug.Assert(tofinalise(o));
        g->tobefnz = o->next; /* remove it from 'tobefnz' list */
        o->next = g->allgc; /* return it to 'allgc' list */
        g->allgc = o;
        resetbit(ref o->marked, FINALISEDBIT); /* object is "normal" again */
        if (issweepphase(g))
        {
            makewhite(g, o); /* "sweep" object */
        }
        else if (getage(o) == G_OLD1)
        {
            g->firstold1 = o; /* it is the first OLD1 object in the list */
        }

        return o;
    }

    private static void dothecall(lua_State* L, void* ud)
    {
        luaD_callnoyield(L, L->top.p - 2, 0);
    }

    private static void GCTM(lua_State* L)
    {
        global_State* g = G(L);
        Debug.Assert(!g->gcemergency);

        TValue v;
        setgcovalue(L, &v, udata2finalise(g));
        TValue* tm = luaT_gettmbyobj(L, &v, TMS.GC);
        if (!notm(tm))
        {
            // is there a finaliser?
            bool oldah = L->allowhook;
            byte oldgcstp = g->gcstp;
            g->gcstp |= GCSTPGC; /* avoid GC steps */
            L->allowhook = false; /* stop debug hooks during GC metamethod */
            setobj2s(L, L->top.p++, tm); /* push finaliser... */
            setobj2s(L, L->top.p++, &v); /* ... and its argument */
            L->ci->callstatus |= CIST_FIN; /* will run a finaliser */
            byte status = luaD_pcall(L, dothecall, null, savestack(L, L->top.p - 2), 0);
            L->ci->callstatus &= ~CIST_FIN; /* not running a finaliser anymore */
            L->allowhook = oldah; /* restore hooks */
            g->gcstp = oldgcstp; /* restore state */
            if (status != LUA_OK)
            {
                /* error while running __gc? */
                luaE_warnerror(L, "__gc");
                L->top.p--; /* pops error object */
            }
        }
    }

    /*
     ** call all pending finalisers
     */
    private static void callallpendingfinalisers(lua_State* L)
    {
        global_State* g = G(L);
        while (g->tobefnz != null)
        {
            GCTM(L);
        }
    }

    /*
     ** find last 'next' field in list 'p' list (to add elements in its end)
     */
    private static GCObject** findlast(GCObject** p)
    {
        while (*p != null)
        {
            p = &(*p)->next;
        }

        return p;
    }

    /*
     ** Move all unreachable objects (or 'all' objects) that need
     ** finalisation from list 'finobj' to list 'tobefnz' (to be finalised).
     ** (Note that objects after 'finobjold1' cannot be white, so they
     ** don't need to be traversed. In incremental mode, 'finobjold1' is null,
     ** so the whole list is traversed.)
     */
    private static void separatetobefnz(global_State* g, bool all)
    {
        GCObject** p = &g->finobj;
        GCObject** lastnext = findlast(&g->tobefnz);

        GCObject* curr;
        while ((curr = *p) != g->finobjold1)
        {
            // traverse all finalisable objects 
            Debug.Assert(tofinalise(curr));
            if (!(iswhite(curr) || all)) /* not being collected? */
            {
                p = &curr->next; /* don't bother with it */
            }
            else
            {
                if (curr == g->finobjsur) /* removing 'finobjsur'? */
                {
                    g->finobjsur = curr->next; /* correct it */
                }

                *p = curr->next; /* remove 'curr' from 'finobj' list */
                curr->next = *lastnext; /* link at the end of 'tobefnz' list */
                *lastnext = curr;
                lastnext = &curr->next;
            }
        }
    }

    /*
    ** If pointer 'p' points to 'o', move it to the next element.
    */
    private static void checkpointer(ref GCObject* p, GCObject* o)
    {
        if (o == p)
        {
            p = o->next;
        }
    }

    /*
     ** Correct pointers to objects inside 'allgc' list when
     ** object 'o' is being removed from the list.
     */
    private static void correctpointers(global_State* g, GCObject* o)
    {
        checkpointer(ref g->survival, o);
        checkpointer(ref g->old1, o);
        checkpointer(ref g->reallyold, o);
        checkpointer(ref g->firstold1, o);
    }

    /*
     ** if object 'o' has a finaliser, remove it from 'allgc' list (must
     ** search the list to find it) and link it in 'finobj' list.
     */
    internal static void luaC_checkfinaliser(lua_State* L, GCObject* o, Table* mt)
    {
        global_State* g = G(L);
        if (tofinalise(o) || /* obj. is already marked... */
            gfasttm(g, mt, TMS.GC) == null || /* or has no finaliser... */
            (g->gcstp & GCSTPCLS) != 0) /* or closing state? */
        {
            return; /* nothing to be done */
        }

        /* move 'o' to 'finobj' list */
        if (issweepphase(g))
        {
            makewhite(g, o); /* "sweep" object 'o' */
            if (g->sweepgc == &o->next) /* should not remove 'sweepgc' object */
            {
                g->sweepgc = sweeptolive(L, g->sweepgc); /* change 'sweepgc' */
            }
        }
        else
        {
            correctpointers(g, o);
        }

        // search for pointer pointing to 'o' 
        GCObject** p;
        for (p = &g->allgc; *p != o; p = &(*p)->next)
        {
            /* empty */
        }

        *p = o->next; /* remove 'o' from 'allgc' list */
        o->next = g->finobj; /* link it in 'finobj' list */
        g->finobj = o;
        l_setbit(ref o->marked, FINALISEDBIT); /* mark it as such */
    }

    /*
    ** {======================================================
    ** Generational Collector
    ** =======================================================
    */

    /*
    ** Fields 'GCmarked' and 'GCmajorminor' are used to control the pace and
    ** the mode of the collector. They play several roles, depending on the
    ** mode of the collector:
    ** * KGC_INC:
    **     GCmarked: number of marked bytes during a cycle.
    **     GCmajorminor: not used.
    ** * KGC_GENMINOR
    **     GCmarked: number of bytes that became old since last major collection.
    **     GCmajorminor: number of bytes marked in last major collection.
    ** * KGC_GENMAJOR
    **     GCmarked: number of bytes that became old since last major collection.
    **     GCmajorminor: number of bytes marked in last major collection.
    */
    
    /*
    ** Set the "time" to wait before starting a new incremental cycle;
    ** cycle will start when number of bytes in use hits the threshold of
    ** approximately (marked * pause / 100).
    */
    private static void setpause(global_State* g)
    {
        long threshold = applygcparam(g, LUA_GCPPAUSE, g->GCmarked);
        long debt = threshold - gettotalbytes(g);
        if (debt < 0)
        {
            debt = 0;
        }

        luaE_setdebt(g, debt);
    }

    /*
     ** Sweep a list of objects to enter generational mode.  Deletes dead
     ** objects and turns the non dead to old. All non-dead threads---which
     ** are now old---must be in a grey list. Everything else is not in a
     ** grey list. Open upvalues are also kept grey.
     */
    private static void sweep2old(lua_State* L, GCObject** p)
    {
        global_State* g = G(L);

        GCObject* curr;
        while ((curr = *p) != null)
        {
            if (iswhite(curr))
            {
                /* is 'curr' dead? */
                Debug.Assert(isdead(g, curr));
                *p = curr->next; /* remove 'curr' from list */
                freeobj(L, curr); /* erase 'curr' */
            }
            else
            {
                /* all surviving objects become old */
                setage(curr, G_OLD);
                if (curr->tt == LUA_VTHREAD)
                {
                    /* threads must be watched */
                    lua_State* th = gco2th(curr);
                    linkgclist(th, ref g->greyagain); /* insert into 'greyagain' list */
                }
                else if (curr->tt == LUA_VUPVAL && upisopen(gco2upv(curr)))
                {
                    set2grey(curr); /* open upvalues are always grey */
                }
                else /* everything else is black */
                {
                    nw2black(curr);
                }

                p = &curr->next; /* go to next element */
            }
        }
    }

    /*
    ** Sweep for generational mode. Delete dead objects. (Because the
    ** collection is not incremental, there are no "new white" objects
    ** during the sweep. So, any white object must be dead.) For
    ** non-dead objects, advance their ages and clear the colour of
    ** new objects. (Old objects keep their colours.)
    ** The ages of G_TOUCHED1 and G_TOUCHED2 objects cannot be advanced
    ** here, because these old-generation objects are usually not swept
    ** here.  They will all be advanced in 'correctgreylist'. That function
    ** will also remove objects turned white here from any grey list.
    */
    private static GCObject** sweepgen(
        lua_State* L,
        global_State* g,
        GCObject** p,
        GCObject* limit,
        GCObject** pfirstold1,
        long* paddedold)
    {
//   static const lu_byte nextage[] = {
//     G_SURVIVAL,  /* from G_NEW */
//     G_OLD1,      /* from G_SURVIVAL */
//     G_OLD1,      /* from G_OLD0 */
//     G_OLD,       /* from G_OLD1 */
//     G_OLD,       /* from G_OLD (do not change) */
//     G_TOUCHED1,  /* from G_TOUCHED1 (do not change) */
//     G_TOUCHED2   /* from G_TOUCHED2 (do not change) */
//   };

        long addedold = 0;
        int white = luaC_white(g);
        GCObject* curr;
        while ((curr = *p) != limit)
        {
            if (iswhite(curr))
            {
                /* is 'curr' dead? */
                Debug.Assert(!isold(curr) && isdead(g, curr));
                *p = curr->next; /* remove 'curr' from list */
                freeobj(L, curr); /* erase 'curr' */
            }
            else
            {
                /* correct mark and age */
                int age = getage(curr);
                if (age == G_NEW)
                {
                    /* new objects go back to white */
                    int marked = curr->marked & ~maskgcbits; /* erase GC bits */
                    curr->marked = (byte)(marked | G_SURVIVAL | white);
                }
                else
                {
                    /* all other objects will be old, and so keep their colour */
//         Debug.Assert(age != G_OLD1);  /* advanced in 'markold' */
//         setage(curr, nextage[age]);
//         if (getage(curr) == G_OLD1) {
//           addedold += objsize(curr);  /* bytes becoming old */
//           if (*pfirstold1 == null)
//             *pfirstold1 = curr;  /* first OLD1 object in the list */
//         }
                    throw new NotImplementedException();
                }

                p = &curr->next; /* go to next element */
            }
        }

        *paddedold += addedold;
        return p;
    }

    /*
     ** Correct a list of grey objects. Return a pointer to the last element
     ** left on the list, so that we can link another list to the end of
     ** this one.
     ** Because this correction is done after sweeping, young objects might
     ** be turned white and still be in the list. They are only removed.
     ** 'TOUCHED1' objects are advanced to 'TOUCHED2' and remain on the list;
     ** Non-white threads also remain on the list. 'TOUCHED2' objects and
     ** anything else become regular old, are marked black, and are removed
     ** from the list.
     */
    private static GCObject** correctgreylist(GCObject** p)
    {
        GCObject* curr;
        while ((curr = *p) != null)
        {
            GCObject** next = getgclist(curr);
            if (iswhite(curr))
            {
                goto remove; /* remove all white objects */
            }

            if (getage(curr) == G_TOUCHED1)
            {
                /* touched in this cycle? */
                Debug.Assert(isgrey(curr));
                nw2black(curr); /* make it black, for next barrier */
                setage(curr, G_TOUCHED2);
                goto remain; /* keep it in the list and go to next element */
            }

            if (curr->tt == LUA_VTHREAD)
            {
                Debug.Assert(isgrey(curr));
                goto remain; /* keep non-white threads on the list */
            }

            /* everything else is removed */
            Debug.Assert(isold(curr)); /* young objects should be white here */
            if (getage(curr) == G_TOUCHED2) /* advance from TOUCHED2... */
            {
                setage(curr, G_OLD); /* ... to OLD */
            }

            nw2black(curr); /* make object black (to be removed) */
            goto remove;

            remove:
            *p = *next;
            continue;

            remain:
            p = next;
        }

        return p;
    }

    /*
     ** Correct all grey lists, coalescing them into 'greyagain'.
     */
    private static void correctgreylists(global_State* g)
    {
        GCObject** list = correctgreylist(&g->greyagain);
        *list = g->weak;
        g->weak = null;
        list = correctgreylist(list);
        *list = g->allweak;
        g->allweak = null;
        list = correctgreylist(list);
        *list = g->ephemeron;
        g->ephemeron = null;
        correctgreylist(list);
    }

    /*
    ** Mark black 'OLD1' objects when starting a new young collection.
    ** Grey objects are already in some grey list, and so will be visited in
    ** the atomic step.
    */
    private static void markold(global_State* g, GCObject* from, GCObject* to)
    {
        for (GCObject* p = from; p != to; p = p->next)
        {
            if (getage(p) != G_OLD1)
            {
                continue;
            }

            Debug.Assert(!iswhite(p));
            setage(p, G_OLD); /* now they are old */
            if (isblack(p))
            {
                reallymarkobject(g, p);
            }
        }
    }

    /*
     ** Finish a young-generation collection.
     */
    private static void finishgencycle(lua_State* L, global_State* g)
    {
        correctgreylists(g);
        checkSizes(L, g);
        g->gcstate = GCSpropagate; /* skip restart */
        if (!g->gcemergency && luaD_checkminstack(L))
        {
            callallpendingfinalisers(L);
        }
    }

    /*
     ** Shifts from a minor collection to major collections. It starts in
     ** the "sweep all" state to clear all objects, which are mostly black
     ** in generational mode.
     */
    static void minor2inc(lua_State* L, global_State* g, byte kind)
    {
        g->GCmajorminor = g->GCmarked; /* number of live bytes */
        g->gckind = kind;
        g->reallyold = g->old1 = g->survival = null;
        g->finobjrold = g->finobjold1 = g->finobjsur = null;
        entersweep(L); /* continue as an incremental cycle */
        /* set a debt equal to the step size */
        luaE_setdebt(g, applygcparam(g, LUA_GCPSTEPSIZE, 100));
    }

    /*
    ** Decide whether to shift to major mode. It shifts if the accumulated
    ** number of added old bytes (counted in 'GCmarked') is larger than
    ** 'minormajor'% of the number of lived bytes after the last major
    ** collection. (This number is kept in 'GCmajorminor'.)
    */
    private static bool checkminormajor(global_State* g)
    {
        long limit = applygcparam(g, LUA_GCPMINORMAJOR, g->GCmajorminor);
        if (limit == 0)
        {
            return false; /* special case: 'minormajor' 0 stops major collections */
        }

        return g->GCmarked >= limit;
    }

    /*
     ** Does a young collection. First, mark 'OLD1' objects. Then does the
     ** atomic step. Then, check whether to continue in minor mode. If so,
     ** sweep all lists and advance pointers. Finally, finish the collection.
     */
    private static void youngcollection(lua_State* L, global_State* g)
    {
        long addedold1 = 0;
        long marked = g->GCmarked; /* preserve 'g->GCmarked' */
        Debug.Assert(g->gcstate == GCSpropagate);
        if (g->firstold1 != null)
        {
            /* are there regular OLD1 objects? */
            markold(g, g->firstold1, g->reallyold); /* mark them */
            g->firstold1 = null; /* no more OLD1 objects (for now) */
        }

        markold(g, g->finobj, g->finobjrold);
        markold(g, g->tobefnz, null);

        atomic(L); /* will lose 'g->marked' */

        /* sweep nursery and get a pointer to its last live element */
        g->gcstate = GCSswpallgc;
        GCObject** psurvival = sweepgen(L, g, &g->allgc, g->survival, &g->firstold1, &addedold1);
        /* sweep 'survival' */
        sweepgen(L, g, psurvival, g->old1, &g->firstold1, &addedold1);
        g->reallyold = g->old1;
        g->old1 = *psurvival; /* 'survival' survivals are old now */
        g->survival = g->allgc; /* all news are survivals */

        /* repeat for 'finobj' lists */
        GCObject* dummy = null; /* no 'firstold1' optimization for 'finobj' lists */
        psurvival = sweepgen(L, g, &g->finobj, g->finobjsur, &dummy, &addedold1);
        /* sweep 'survival' */
        sweepgen(L, g, psurvival, g->finobjold1, &dummy, &addedold1);
        g->finobjrold = g->finobjold1;
        g->finobjold1 = *psurvival; /* 'survival' survivals are old now */
        g->finobjsur = g->finobj; /* all news are survivals */

        sweepgen(L, g, &g->tobefnz, null, &dummy, &addedold1);

        /* keep total number of added old1 bytes */
        g->GCmarked = marked + addedold1;

        /* decide whether to shift to major mode */
        if (checkminormajor(g))
        {
            minor2inc(L, g, KGC_GENMAJOR); /* go to major mode */
            g->GCmarked = 0; /* avoid pause in first major cycle (see 'setpause') */
        }
        else
        {
            finishgencycle(L, g); /* still in minor mode; finish it */
        }
    }


    /*
     ** Clears all grey lists, sweeps objects, and prepare sublists to enter
     ** generational mode. The sweeps remove dead objects and turn all
     ** surviving objects to old. Threads go back to 'grayagain'; everything
     ** else is turned black (not in any grey list).
     */
    private static void atomic2gen(lua_State* L, global_State* g)
    {
        cleargreylists(g);
        /* sweep all elements making them old */
        g->gcstate = GCSswpallgc;
        sweep2old(L, &g->allgc);
        /* everything alive now is old */
        g->reallyold = g->old1 = g->survival = g->allgc;
        g->firstold1 = null; /* there are no OLD1 objects anywhere */

        /* repeat for 'finobj' lists */
        sweep2old(L, &g->finobj);
        g->finobjrold = g->finobjold1 = g->finobjsur = g->finobj;

        sweep2old(L, &g->tobefnz);

        g->gckind = KGC_GENMINOR;
        g->GCmajorminor = g->GCmarked; /* "base" for number of bytes */
        g->GCmarked = 0; /* to count the number of added old1 bytes */
        finishgencycle(L, g);
    }

    /*
     ** Set debt for the next minor collection, which will happen when
     ** total number of bytes grows 'genminormul'% in relation to
     ** the base, GCmajorminor, which is the number of bytes being used
     ** after the last major collection.
     */
    private static void setminordebt(global_State* g)
    {
        luaE_setdebt(g, applygcparam(g, LUA_GCPMINORMUL, g->GCmajorminor));
    }

    /*
     ** Enter generational mode. Must go until the end of an atomic cycle
     ** to ensure that all objects are correctly marked and weak tables
     ** are cleared. Then, turn all objects into old and finishes the
     ** collection.
     */
    private static void entergen(lua_State* L, global_State* g)
    {
        luaC_runtilstate(L, GCSpause, true); /* prepare to start a new cycle */
        luaC_runtilstate(L, GCSpropagate, true); /* start new cycle */
        atomic(L); /* propagates all and then do the atomic stuff */
        atomic2gen(L, g);
        setminordebt(g); /* set debt assuming next cycle will be minor */
    }

    /*
     ** Change collector mode to 'newmode'.
     */
    internal static void luaC_changemode(lua_State* L, int newmode)
    {
        global_State* g = G(L);
        if (g->gckind == KGC_GENMAJOR) /* doing major collections? */
        {
            g->gckind = KGC_INC; /* already incremental but in name */
        }

        if (newmode != g->gckind)
        {
            /* does it need to change? */
            if (newmode == KGC_INC) /* entering incremental mode? */
            {
                minor2inc(L, g, KGC_INC); /* entering incremental mode */
            }
            else
            {
                Debug.Assert(newmode == KGC_GENMINOR);
                entergen(L, g);
            }
        }
    }

    /*
    ** Does a full collection in generational mode.
    */
    private static void fullgen(lua_State* L, global_State* g)
    {
        minor2inc(L, g, KGC_INC);
        entergen(L, g);
    }

    /*
    ** After an atomic incremental step from a major collection,
    ** check whether collector could return to minor collections.
    ** It checks whether the number of bytes 'tobecollected'
    ** is greater than 'majorminor'% of the number of bytes added
    ** since the last collection ('addedbytes').
    */
    private static bool checkmajorminor(lua_State* L, global_State* g)
    {
        if (g->gckind == KGC_GENMAJOR)
        {
            /* generational mode? */
            long numbytes = gettotalbytes(g);
            long addedbytes = numbytes - g->GCmajorminor;
            long limit = applygcparam(g, LUA_GCPMAJORMINOR, addedbytes);
            long tobecollected = numbytes - g->GCmarked;
            if (tobecollected > limit)
            {
                atomic2gen(L, g); /* return to generational mode */
                setminordebt(g);
                return true; /* exit incremental collection */
            }
        }

        g->GCmajorminor = g->GCmarked; /* prepare for next collection */
        return false; /* stay doing incremental collections */
    }

    /*
     ** {======================================================
     ** GC control
     ** =======================================================
     */

    /*
    ** Enter first sweep phase.
    ** The call to 'sweeptolive' makes the pointer point to an object
    ** inside the list (instead of to the header), so that the real sweep do
    ** not need to skip objects created between "now" and the start of the
    ** real sweep.
    */
    private static partial void entersweep(lua_State* L)
    {
        global_State* g = G(L);
        g->gcstate = GCSswpallgc;
        Debug.Assert(g->sweepgc == null);
        g->sweepgc = sweeptolive(L, &g->allgc);
    }

    /*
    ** Delete all objects in list 'p' until (but not including) object
    ** 'limit'.
    */
    private static void deletelist(lua_State* L, GCObject* p, GCObject* limit)
    {
        while (p != limit)
        {
            GCObject* next = p->next;
            freeobj(L, p);
            p = next;
        }
    }

    /*
     ** Call all finalisers of the objects in the given Lua state, and
     ** then free all objects, except for the main thread.
     */
    internal static void luaC_freeallobjects(lua_State* L)
    {
        global_State* g = G(L);
        g->gcstp = GCSTPCLS; /* no extra finalisers after here */
        luaC_changemode(L, KGC_INC);
        separatetobefnz(g, true); /* separate all objects with finalisers */
        Debug.Assert(g->finobj == null);
        callallpendingfinalisers(L);
        deletelist(L, g->allgc, obj2gco(mainthread(g)));
        Debug.Assert(g->finobj == null); /* no new finalisers */
        deletelist(L, g->fixedgc, null); /* collect fixed objects */
        Debug.Assert(g->strt.nuse == 0);
    }

    private static partial void atomic(lua_State* L)
    {
        global_State* g = G(L);
        GCObject* greyagain = g->greyagain; /* save original list */
        g->greyagain = null;
        Debug.Assert(g->ephemeron == null && g->weak == null);
        Debug.Assert(!iswhite((GCObject*)mainthread(g)));
        g->gcstate = GCSatomic;
        markobject(g, (GCObject*)L); /* mark running thread */
        /* registry and global metatables may be changed by API */
        markvalue(g, &g->l_registry);
        markmt(g); /* mark global metatables */
        propagateall(g); /* empties 'grey' list */
        /* remark occasional upvalues of (maybe) dead threads */
        remarkupvals(g);
        propagateall(g); /* propagate changes */
        g->grey = greyagain;
        propagateall(g); /* traverse 'greyagain' list */
        convergeephemerons(g);
        /* at this point, all strongly accessible objects are marked. */
        /* Clear values from weak tables, before checking finalisers */
        clearbyvalues(g, g->weak, null);
        clearbyvalues(g, g->allweak, null);
        GCObject* origweak = g->weak;
        GCObject* origall = g->allweak;
        separatetobefnz(g, false); /* separate objects to be finalised */
        markbeingfnz(g); /* mark objects that will be finalised */
        propagateall(g); /* remark, to propagate 'resurrection' */
        convergeephemerons(g);
        /* at this point, all resurrected objects are marked. */
        /* remove dead objects from weak tables */
        clearbykeys(g, g->ephemeron); /* clear keys from all ephemeron */
        clearbykeys(g, g->allweak); /* clear keys from all 'allweak' */
        /* clear values from resurrected weak tables */
        clearbyvalues(g, g->weak, origweak);
        clearbyvalues(g, g->allweak, origall);
        luaS_clearcache(g);
        g->currentwhite = otherwhite(g); /* flip current white */
        Debug.Assert(g->grey == null);
    }

    /*
    ** Do a sweep step. The normal case (not fast) sweeps at most GCSWEEPMAX
    ** elements. The fast case sweeps the whole list.
    */
    private static void sweepstep(
        lua_State* L,
        global_State* g,
        byte nextstate,
        GCObject** nextlist,
        bool fast)
    {
        const long MAX_LMEM = 0x7FFFFFFFFFFFFFFFL;
        
        if (g->sweepgc != null)
        {
            g->sweepgc = sweeplist(L, g->sweepgc, fast ? MAX_LMEM : GCSWEEPMAX);
        }
        else
        {
            /* enter next state */
            g->gcstate = nextstate;
            g->sweepgc = nextlist;
        }
    }

    /*
    ** Performs one incremental "step" in an incremental garbage collection.
    ** For indivisible work, a step goes to the next state. When marking
    ** (propagating), a step traverses one object. When sweeping, a step
    ** sweeps GCSWEEPMAX objects, to avoid a big overhead for sweeping
    ** objects one by one. (Sweeping is inexpensive, no matter the
    ** object.) When 'fast' is true, 'singlestep' tries to finish a state
    ** "as fast as possible". In particular, it skips the propagation
    ** phase and leaves all objects to be traversed by the atomic phase:
    ** That avoids traversing twice some objects, such as threads and
    ** weak tables.
    */

    private const int step2pause = -3;  /* finished collection; entered pause state */
    private const int atomicstep = -2;  /* atomic step */
    private const int step2minor = -1;  /* moved to minor collections */

    private static long singlestep(lua_State* L, bool fast)
    {
        global_State* g = G(L);
        long stepresult;
        Debug.Assert(!g->gcstopem); /* collector is not reentrant */
        g->gcstopem = true; /* no emergency collections while collecting */
        switch (g->gcstate)
        {
            case GCSpause:
                restartcollection(g);
                g->gcstate = GCSpropagate;
                stepresult = 1;
                break;

            case GCSpropagate:
                if (fast || g->grey == null)
                {
                    g->gcstate = GCSenteratomic; /* finish propagate phase */
                    stepresult = 1;
                }
                else
                {
                    stepresult = propagatemark(g); /* traverse one gray object */
                }

                break;

            case GCSenteratomic:
                atomic(L);
                if (checkmajorminor(L, g))
                {
                    stepresult = step2minor;
                }
                else
                {
                    entersweep(L);
                    stepresult = atomicstep;
                }

                break;

            case GCSswpallgc:
                /* sweep "regular" objects */
                sweepstep(L, g, GCSswpfinobj, &g->finobj, fast);
                stepresult = GCSWEEPMAX;
                break;

            case GCSswpfinobj:
                /* sweep objects with finalisers */
                sweepstep(L, g, GCSswptobefnz, &g->tobefnz, fast);
                stepresult = GCSWEEPMAX;
                break;

            case GCSswptobefnz:
                /* sweep objects to be finalised */
                sweepstep(L, g, GCSswpend, null, fast);
                stepresult = GCSWEEPMAX;
                break;

            case GCSswpend:
                /* finish sweeps */
                checkSizes(L, g);
                g->gcstate = GCScallfin;
                stepresult = GCSWEEPMAX;
                break;

            case GCScallfin:
                /* call finalisers */
                if (g->tobefnz != null && !g->gcemergency && luaD_checkminstack(L))
                {
                    g->gcstopem = false; /* ok collections during finalisers */
                    GCTM(L); /* call one finaliser */
                    stepresult = CWUFIN;
                }
                else
                {
                    /* no more finalisers or emergency mode or no enough stack
                       to run finalisers */
                    g->gcstate = GCSpause; /* finish collection */
                    stepresult = step2pause;
                }

                break;

            default:
                throw new InvalidOperationException();
        }

        g->gcstopem = false;
        return stepresult;
    }

    /*
     ** Advances the garbage collector until it reaches the given state.
     ** (The option 'fast' is only for testing; in normal code, 'fast'
     ** here is always true.)
     */
    internal static void luaC_runtilstate(lua_State* L, int state, bool fast)
    {
        global_State* g = G(L);
        Debug.Assert(g->gckind == KGC_INC);
        while (state != g->gcstate)
        {
            singlestep(L, fast);
        }
    }

    /*
    ** Performs a basic incremental step. The step size is
    ** converted from bytes to "units of work"; then the function loops
    ** running single steps until adding that many units of work or
    ** finishing a cycle (pause state). Finally, it sets the debt that
    ** controls when next step will be performed.
    */
    private static void incstep(lua_State* L, global_State* g)
    {
        long stepsize = applygcparam(g, LUA_GCPSTEPSIZE, 100);
        long work2do = applygcparam(g, LUA_GCPSTEPMUL, stepsize / sizeof(void*));
        bool fast = work2do == 0; /* special case: do a full collection */
        do
        {
            // repeat until enough work 
            long stres = singlestep(L, fast); /* perform one single step */
            if (stres == step2minor) /* returned to minor collections? */
            {
                return; /* nothing else to be done here */
            }

            if (stres == step2pause || (stres == atomicstep && !fast))
            {
                break; /* end of cycle or atomic */
            }

            work2do -= stres;
        } while (fast || work2do > 0);

        if (g->gcstate == GCSpause)
        {
            setpause(g); /* pause until next cycle */
        }
        else
        {
            luaE_setdebt(g, stepsize);
        }
    }

// #if !defined(luai_tracegc)
// #define luai_tracegc(L,f)		((void)0)
// #endif

    /*
     ** Performs a basic GC step if collector is running. (If collector was
     ** stopped by the user, set a reasonable debt to avoid it being called
     ** at every single check.)
     */
    internal static void luaC_step(lua_State* L)
    {
        global_State* g = G(L);
        Debug.Assert(!g->gcemergency);
        if (!gcrunning(g))
        {
            /* not running? */
            if ((g->gcstp & GCSTPUSR) != 0) /* stopped by the user? */
            {
                luaE_setdebt(g, 20000);
            }
        }
        else
        {
            luai_tracegc(L, true); /* for internal debugging */
            switch (g->gckind)
            {
                case KGC_INC:
                case KGC_GENMAJOR:
                    incstep(L, g);
                    break;

                case KGC_GENMINOR:
                    youngcollection(L, g);
                    setminordebt(g);
                    break;
            }

            luai_tracegc(L, false); /* for internal debugging */
        }
    }

    /*
    ** Perform a full collection in incremental mode.
    ** Before running the collection, check 'keepinvariant'; if it is true,
    ** there may be some objects marked as black, so the collector has
    ** to sweep all objects to turn them back to white (as white has not
    ** changed, nothing will be collected).
    */
    private static void fullinc(lua_State* L, global_State* g)
    {
        if (keepinvariant(g)) /* black objects? */
        {
            entersweep(L); /* sweep everything to turn them back to white */
        }

        // finish any pending sweep phase to start a new cycle 
        luaC_runtilstate(L, GCSpause, true);
        luaC_runtilstate(L, GCScallfin, true); /* run up to finalisers */
        luaC_runtilstate(L, GCSpause, true); /* finish collection */
        setpause(g);
    }

    /*
     ** Performs a full GC cycle; if 'isemergency', set a flag to avoid
     ** some operations which could change the interpreter state in some
     ** unexpected ways (running finalisers and shrinking some structures).
     */
    internal static void luaC_fullgc(lua_State* L, bool isemergency)
    {
        global_State* g = G(L);
        Debug.Assert(!g->gcemergency);
        g->gcemergency = isemergency; /* set flag */
        switch (g->gckind)
        {
            case KGC_GENMINOR: fullgen(L, g); break;
            
            case KGC_INC: fullinc(L, g); break;
            
            case KGC_GENMAJOR:
                g->gckind = KGC_INC;
                fullinc(L, g);
                g->gckind = KGC_GENMAJOR;
                break;
        }

        g->gcemergency = false;
    }
}
