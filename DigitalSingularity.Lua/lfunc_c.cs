namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /*
    ** $Id: lfunc.c $
    ** Auxiliary functions to manipulate prototypes and closures
    ** See Copyright Notice in lua.h
    */

    internal static CClosure* luaF_newCclosure(lua_State* L, int nupvals)
    {
        GCObject* o = luaC_newobj(L, LUA_VCCL, sizeCclosure(nupvals));
        CClosure* c = gco2ccl(o);
        c->nupvalues = (byte)nupvals;
        return c;
    }

    internal static LClosure* luaF_newLclosure(lua_State* L, int nupvals)
    {
        GCObject* o = luaC_newobj(L, LUA_VLCL, sizeLclosure(nupvals));
        LClosure* c = gco2lcl(o);
        c->p = null;
        c->nupvalues = (byte)nupvals;
        while (nupvals-- > 0)
        {
            LClosure.GetUpValue(c, nupvals) = null;
        }

        return c;
    }

    /*
    ** fill a closure with new closed upvalues
    */
    internal static void luaF_initupvals(lua_State* L, LClosure* cl)
    {
        for (int i = 0; i < cl->nupvalues; i++)
        {
            GCObject* o = luaC_newobj(L, LUA_VUPVAL, sizeof(UpVal));
            UpVal* uv = gco2upv(o);
            uv->v.p = &uv->u.value; /* make it closed */
            setnilvalue(uv->v.p);
            LClosure.GetUpValue(cl, i) = uv;
            luaC_objbarrier(L, (GCObject*)cl, (GCObject*)uv);
        }
    }

    /*
     ** Create a new upvalue at the given level, and link it to the list of
     ** open upvalues of 'L' after entry 'prev'.
     **/
    private static UpVal* newupval(lua_State* L, StkId level, UpVal** prev)
    {
        GCObject* o = luaC_newobj(L, LUA_VUPVAL, sizeof(UpVal));
        UpVal* uv = gco2upv(o);
        UpVal* next = *prev;
        uv->v.p = s2v(level); /* current value lives in the stack */
        uv->u.open.next = next; /* link it to list of open upvalues */
        uv->u.open.previous = prev;
        if (next != null)
        {
            next->u.open.previous = &uv->u.open.next;
        }

        *prev = uv;
        if (!isintwups(L))
        {
            /* thread not in list of threads with upvalues? */
            L->twups = G(L)->twups; /* link it to the list */
            G(L)->twups = L;
        }

        return uv;
    }

    /*
     ** Find and reuse, or create if it does not exist, an upvalue
     ** at the given level.
     */
    internal static UpVal* luaF_findupval(lua_State* L, StkId level)
    {
        UpVal** pp = &L->openupval;
        Debug.Assert(isintwups(L) || L->openupval == null);

        UpVal* p;
        while ((p = *pp) != null && uplevel(p) >= level)
        {
            /* search for it */
            Debug.Assert(!isdead(G(L), (GCObject*)p));
            if (uplevel(p) == level) /* corresponding upvalue? */
            {
                return p; /* return it */
            }

            pp = &p->u.open.next;
        }

        /* not found: create a new upvalue after 'pp' */
        return newupval(L, level, pp);
    }

    /*
     ** Call closing method for object 'obj' with error object 'err'. The
     ** boolean 'yy' controls whether the call is yieldable.
     ** (This function assumes EXTRA_STACK.)
     */
    private static void callclosemethod(lua_State* L, TValue* obj, TValue* err, bool yy)
    {
        StkId top = L->top.p;
        StkId func = top;
        TValue* tm = luaT_gettmbyobj(L, obj, TMS.CLOSE);
        setobj2s(L, top++, tm); /* will call metamethod... */
        setobj2s(L, top++, obj); /* with 'self' as the 1st argument */
        if (err != null) /* if there was an error... */
        {
            setobj2s(L, top++, err); /* then error object will be 2nd argument */
        }

        L->top.p = top; /* add function and arguments */
        if (yy)
        {
            luaD_call(L, func, 0);
        }
        else
        {
            luaD_callnoyield(L, func, 0);
        }
    }

    /*
     ** Check whether object at given level has a close metamethod and raise
     ** an error if not.
     */
    private static void checkclosemth(lua_State* L, StkId level)
    {
        TValue* tm = luaT_gettmbyobj(L, s2v(level), TMS.CLOSE);
        if (ttisnil(tm))
        {
            /* no metamethod? */
            int idx = (int)(level - L->ci->func.p); /* variable index */
            string vname = luaG_findlocal(L, L->ci, idx, null) ?? "?";
            luaG_runerror(L, "variable '%s' got a non-closable value", vname);
        }
    }

    /*
     ** Prepare and call a closing method.
     ** If status is CLOSEKTOP, the call to the closing method will be pushed
     ** at the top of the stack. Otherwise, values can be pushed right after
     ** the 'level' of the upvalue being closed, as everything after that
     ** won't be used again.
     */
    private static void prepcallclosemth(
        lua_State* L,
        StkId level,
        byte status,
        bool yy)
    {
        TValue* uv = s2v(level); /* value being closed */
        TValue* errobj;
        switch (status)
        {
            case LUA_OK:
                L->top.p = level + 1; /* call will be at this level */
                goto case CLOSEKTOP;

            case CLOSEKTOP: /* don't need to change top */
                errobj = null; /* no error object */
                break;

            default: /* 'luaD_seterrorobj' will set top to level + 2 */
                errobj = s2v(level + 1); /* error object goes after 'uv' */
                luaD_seterrorobj(L, status, level + 1); /* set error object */
                break;
        }

        callclosemethod(L, uv, errobj, yy);
    }

    /* Maximum value for deltas in 'tbclist' */
    private const int MAXDELTA = ushort.MaxValue;

    /*
    ** Insert a variable in the list of to-be-closed variables.
    */
    internal static void luaF_newtbcupval(lua_State* L, StkId level)
    {
        Debug.Assert(level > L->tbclist.p);
        if (l_isfalse(s2v(level)))
        {
            return; /* false doesn't need to be closed */
        }

        checkclosemth(L, level); /* value must have a close method */
        while ((uint)(level - L->tbclist.p) > MAXDELTA)
        {
            L->tbclist.p += MAXDELTA; /* create a dummy node at maximum delta */
            L->tbclist.p->tbclist.delta = 0;
        }

        level->tbclist.delta = (ushort)(level - L->tbclist.p);
        L->tbclist.p = level;
    }

    internal static void luaF_unlinkupval(UpVal* uv)
    {
        Debug.Assert(upisopen(uv));
        *uv->u.open.previous = uv->u.open.next;
        if (uv->u.open.next != null)
        {
            uv->u.open.next->u.open.previous = uv->u.open.previous;
        }
    }

    /*
    ** Close all upvalues up to the given stack level.
    */
    internal static void luaF_closeupval(lua_State* L, StkId level)
    {
        UpVal* uv;
        while ((uv = L->openupval) != null && uplevel(uv) >= level)
        {
            TValue* slot = &uv->u.value; /* new position for value */
            Debug.Assert(uplevel(uv) < L->top.p);
            luaF_unlinkupval(uv); /* remove upvalue from 'openupval' list */
            setobj(L, slot, uv->v.p); /* move value to upvalue slot */
            uv->v.p = slot; /* now current value lives here */
            if (!iswhite((GCObject*)uv))
            {
                /* neither white nor dead? */
                nw2black((GCObject*)uv); /* closed upvalues cannot be grey */
                luaC_barrier(L, (GCObject*)uv, slot);
            }
        }
    }

    /*
     ** Remove first element from the tbclist plus its dummy nodes.
     */
    private static void poptbclist(lua_State* L)
    {
        StkId tbc = L->tbclist.p;
        Debug.Assert(tbc->tbclist.delta > 0); /* first element cannot be dummy */
        tbc -= tbc->tbclist.delta;
        while (tbc > L->stack.p && tbc->tbclist.delta == 0)
        {
            tbc -= MAXDELTA; /* remove dummy nodes */
        }

        L->tbclist.p = tbc;
    }

    /*
     ** Close all upvalues and to-be-closed variables up to the given stack
     ** level. Return restored 'level'.
     */
    internal static StkId luaF_close(lua_State* L, StkId level, byte status, bool yy)
    {
        nint levelrel = savestack(L, level);
        luaF_closeupval(L, level); /* first, close the upvalues */
        while (L->tbclist.p >= level)
        {
            /* traverse tbc's down to that level */
            StkId tbc = L->tbclist.p; /* get variable index */
            poptbclist(L); /* remove it from list */
            prepcallclosemth(L, tbc, status, yy); /* close variable */
            level = restorestack(L, levelrel);
        }

        return level;
    }

    internal static Proto* luaF_newproto(lua_State* L)
    {
        GCObject* o = luaC_newobj(L, LUA_VPROTO, sizeof(Proto));
        Proto* f = gco2p(o);
        f->k = null;
        f->sizek = 0;
        f->p = null;
        f->sizep = 0;
        f->code = null;
        f->sizecode = 0;
        f->lineinfo = null;
        f->sizelineinfo = 0;
        f->abslineinfo = null;
        f->sizeabslineinfo = 0;
        f->upvalues = null;
        f->sizeupvalues = 0;
        f->numparams = 0;
        f->flag = 0;
        f->maxstacksize = 0;
        f->locvars = null;
        f->sizelocvars = 0;
        f->linedefined = 0;
        f->lastlinedefined = 0;
        f->source = null;
        return f;
    }

    internal static long luaF_protosize(Proto* p)
    {
        long sz = sizeof(Proto) +
                  (uint)p->sizep * sizeof(Proto*) +
                  (uint)p->sizek * sizeof(TValue) +
                  (uint)p->sizelocvars * sizeof(LocVar) +
                  (uint)p->sizeupvalues * sizeof(Upvaldesc);
        if ((p->flag & PF_FIXED) == 0)
        {
            sz += (uint)p->sizecode * sizeof(uint);
            sz += (uint)p->sizelineinfo * sizeof(byte);
            sz += (uint)p->sizeabslineinfo * sizeof(AbsLineInfo);
        }

        return sz;
    }

    private static void luaF_freeproto(lua_State* L, Proto* f)
    {
        if ((f->flag & PF_FIXED) == 0)
        {
            luaM_freearray(L, f->code, f->sizecode);
            luaM_freearray(L, f->lineinfo, f->sizelineinfo);
            luaM_freearray(L, f->abslineinfo, f->sizeabslineinfo);
        }

        luaM_freearray(L, f->p, f->sizep);
        luaM_freearray(L, f->k, f->sizek);
        luaM_freearray(L, f->locvars, f->sizelocvars);
        luaM_freearray(L, f->upvalues, f->sizeupvalues);
        luaM_free(L, f);
    }

    /*
     ** Look for n-th local variable at line 'line' in function 'func'.
     ** Returns null if not found.
     */
    internal static string? luaF_getlocalname(Proto* func, int localNumber, int pc)
    {
        for (int i = 0; i < func->sizelocvars && func->locvars[i].startpc <= pc; i++)
        {
            if (pc < func->locvars[i].endpc)
            {
                // is variable active? 
                localNumber--;
                if (localNumber == 0)
                {
                    return getnetstr(func->locvars[i].varname);
                }
            }
        }

        return null; /* not found */
    }
}
