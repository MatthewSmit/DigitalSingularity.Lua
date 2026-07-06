namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

public static unsafe partial class Lua
{
    private static bool LuaClosure(Closure* f)
    {
        return f != null && f->c.tt == LUA_VLCL;
    }

    private const string strlocal = "local";
    private const string strupval = "upvalue";

    private static int currentpc(CallInfo* ci)
    {
        Debug.Assert(isLua(ci));
        return pcRel(ci->u.l.savedpc, ci_func(ci)->p);
    }

    /*
     ** Get a "base line" to find the line corresponding to an instruction.
     ** Base lines are regularly placed at MAXIWTHABS intervals, so usually
     ** an integer division gets the right place. When the source file has
     ** large sequences of empty/comment lines, it may need extra entries,
     ** so the original estimate needs a correction.
     ** If the original estimate is -1, the initial 'if' ensures that the
     ** 'while' will run at least once.
     ** The assertion that the estimate is a lower bound for the correct base
     ** is valid as long as the debug info has been generated with the same
     ** value for MAXIWTHABS or smaller. (Previous releases use a little
     ** smaller value.)
     */
    private static int getbaseline(Proto* f, int pc, out int basepc)
    {
        if (f->sizeabslineinfo == 0 || pc < f->abslineinfo[0].pc)
        {
            basepc = -1; /* start from the beginning */
            return f->linedefined;
        }

        int i = pc / MAXIWTHABS - 1; /* get an estimate */
        /* estimate must be a lower bound of the correct base */
        Debug.Assert(
            i < 0 ||
            (i < f->sizeabslineinfo && f->abslineinfo[i].pc <= pc));
        while (i + 1 < f->sizeabslineinfo && pc >= f->abslineinfo[i + 1].pc)
        {
            i++; /* low estimate; adjust it */
        }

        basepc = f->abslineinfo[i].pc;
        return f->abslineinfo[i].line;
    }

    /*
     ** Get the line corresponding to instruction 'pc' in function 'f';
     ** first gets a base line and from there does the increments until
     ** the desired instruction.
     */
    internal static partial int luaG_getfuncline(Proto* f, int pc)
    {
        if (f->lineinfo == null) /* no debug information? */
        {
            return -1;
        }

        int baseline = getbaseline(f, pc, out int basepc);
        while (basepc++ < pc)
        {
            /* walk until given instruction */
            Debug.Assert(f->lineinfo[basepc] != ABSLINEINFO);
            baseline += f->lineinfo[basepc]; /* correct line */
        }

        return baseline;
    }

    private static int getcurrentline(CallInfo* ci)
    {
        return luaG_getfuncline(ci_func(ci)->p, currentpc(ci));
    }

    /*
     ** Set 'trap' for all active Lua frames.
     ** This function can be called during a signal, under "reasonable"
     ** assumptions. A new 'ci' is completely linked in the list before it
     ** becomes part of the "active" list, and we assume that pointers are
     ** atomic; see comment in next function.
     ** (A compiler doing interprocedural optimizations could, theoretically,
     ** reorder memory writes in such a way that the list could be
     ** temporarily broken while inserting a new element. We simply assume it
     ** has no good reasons to do that.)
     */
    private static void settraps(CallInfo* ci)
    {
        for (; ci != null; ci = ci->previous)
        {
            if (isLua(ci))
            {
                ci->u.l.trap = 1;
            }
        }
    }

    /*
     ** This function can be called during a signal, under "reasonable"
     ** assumptions.
     ** Fields 'basehookcount' and 'hookcount' (set by 'resethookcount')
     ** are for debug only, and it is no problem if they get arbitrary
     ** values (causes at most one wrong hook call). 'hookmask' is an atomic
     ** value. We assume that pointers are atomic too (e.g. gcc ensures that
     ** for all platforms where it runs). Moreover, 'hook' is always checked
     ** before being called (see 'luaD_hook').
     */
    public static partial void lua_sethook(lua_State* L, lua_Hook func, byte mask, int count)
    {
        if (func == null || mask == 0)
        {
            /* turn off hooks? */
            mask = 0;
            func = null;
        }

        L->hook = func;
        L->basehookcount = count;
        resethookcount(L);
        L->hookmask = mask;
        if (mask != 0)
        {
            settraps(L->ci); /* to trace inside 'luaV_execute' */
        }
    }

    public static partial lua_Hook lua_gethook(lua_State* L)
    {
        return L->hook;
    }

    public static partial byte lua_gethookmask(lua_State* L)
    {
        return L->hookmask;
    }

    public static partial int lua_gethookcount(lua_State* L)
    {
        return L->basehookcount;
    }

    public static partial bool lua_getstack(lua_State* L, int level, ref lua_Debug ar)
    {
        if (level < 0)
        {
            return false; /* invalid (negative) level */
        }

        lua_lock(L);

        CallInfo* ci;
        for (ci = L->ci; level > 0 && ci != &L->base_ci; ci = ci->previous)
        {
            level--;
        }

        bool status;
        if (level == 0 && ci != &L->base_ci)
        {
            /* level found? */
            status = true;
            ar.i_ci = ci;
        }
        else
        {
            status = false; /* no such level */
        }

        lua_unlock(L);
        return status;
    }

    private static string upvalname(Proto* p, int uv)
    {
        Debug.Assert(uv < p->sizeupvalues);
        TString* s = p->upvalues[uv].name;
        if (s == null)
        {
            return "?";
        }

        return getnetstr(s);
    }

    private static string findvararg(CallInfo* ci, int n, StkId* pos)
    {
//   if (clLvalue(s2v(ci->func.p))->p->flag & PF_VAHID) {
//     int nextra = ci->u.l.nextraargs;
//     if (n >= -nextra) {  /* 'n' is negative */
//       *pos = ci->func.p - nextra - (n + 1);
//       return "(vararg)";  /* generic name for any vararg */
//     }
//   }
//   return null;  /* no such vararg */
        throw new NotImplementedException();
    }

    internal static partial string? luaG_findlocal(lua_State* L, CallInfo* ci, int n, StkId* pos)
    {
        StkId @base = ci->func.p + 1;
        string? name = null;
        if (isLua(ci))
        {
            if (n < 0) /* access to vararg values? */
            {
                return findvararg(ci, n, pos);
            }

            name = luaF_getlocalname(ci_func(ci)->p, n, currentpc(ci));
        }

        if (name == null)
        {
            /* no 'standard' name? */
            StkId limit = (ci == L->ci) ? L->top.p : ci->next->func.p;
            if (limit - @base >= n && n > 0)
            {
                /* is 'n' inside 'ci' stack? */
                /* generic name for any valid slot */
                name = isLua(ci) ? "(temporary)" : "(C temporary)";
            }
            else
            {
                return null; /* no name */
            }
        }

        if (pos != null)
        {
            *pos = @base + (n - 1);
        }

        return name;
    }

    public static partial string? lua_getlocal(lua_State* L, int n)
    {
        lua_lock(L);
        /* information about non-active function? */

        string? name;
        if (!isLfunction(s2v(L->top.p - 1))) /* not a Lua function? */
        {
            name = null;
        }
        else /* consider live variables at function start (parameters) */
        {
            name = luaF_getlocalname(clLvalue(s2v(L->top.p - 1))->p, n, 0);
        }

        lua_unlock(L);
        return name;
    }

    public static partial string? lua_getlocal(lua_State* L, ref lua_Debug ar, int n)
    {
        lua_lock(L);
        /* active function; get information through 'ar' */
        StkId pos = null; /* to avoid warnings */
        string? name = luaG_findlocal(L, ar.i_ci, n, &pos);
        if (name != null)
        {
            setobjs2s(L, L->top.p, pos!);
            api_incr_top(L);
        }

        lua_unlock(L);
        return name;
    }

    public static partial string? lua_setlocal(lua_State* L, ref lua_Debug ar, int n)
    {
        lua_lock(L);
        StkId pos = null; /* to avoid warnings */
        string? name = luaG_findlocal(L, ar.i_ci, n, &pos);
        if (name != null)
        {
            api_checkpop(L, 1);
            setobjs2s(L, pos!, L->top.p - 1);
            L->top.p--; /* pop value */
        }

        lua_unlock(L);
        return name;
    }

    private static void funcinfo(ref lua_Debug ar, Closure* cl)
    {
        if (!LuaClosure(cl))
        {
            ar.source = "=[C]";
            ar.linedefined = -1;
            ar.lastlinedefined = -1;
            ar.what = "C";
        }
        else
        {
            Proto* p = cl->l.p;
            ar.source = p->source != null ? getnetstr(p->source) : "=?";
            ar.linedefined = p->linedefined;
            ar.lastlinedefined = p->lastlinedefined;
            ar.what = ar.linedefined == 0 ? "main" : "Lua";
        }

        ar.short_src = luaO_chunkid(ar.source);
    }

    private static int nextline(Proto* p, int currentline, int pc)
    {
        if (p->lineinfo[pc] != ABSLINEINFO)
        {
            return currentline + p->lineinfo[pc];
        }

        return luaG_getfuncline(p, pc);
    }

    private static void collectvalidlines(lua_State* L, Closure* f)
    {
        if (!LuaClosure(f))
        {
            setnilvalue(s2v(L->top.p));
            api_incr_top(L);
        }
        else
        {
            Proto* p = f->l.p;
            int currentline = p->linedefined;
            Table* t = luaH_new(L); /* new table to store active lines */
            sethvalue2s(L, L->top.p, t); /* push it on stack */
            api_incr_top(L);
            if (p->lineinfo != null)
            {
                /* proto with debug information? */
                TValue v;
                setbtvalue(&v); /* boolean 'true' to be the value of all indices */

                int i;
                if (!(isvararg(p))) /* regular function? */
                {
                    i = 0; /* consider all instructions */
                }
                else
                {
                    /* vararg function */
                    Debug.Assert(GET_OPCODE(p->code[0]) == OpCode.OP_VARARGPREP);
                    currentline = nextline(p, currentline, 0);
                    i = 1; /* skip first instruction (OP_VARARGPREP) */
                }

                for (; i < p->sizelineinfo; i++)
                {
                    /* for each instruction */
                    currentline = nextline(p, currentline, i); /* get its line */
                    luaH_setint(L, t, currentline, &v); /* table[line] = true */
                }
            }
        }
    }

    private static string? getfuncname(lua_State* L, CallInfo* ci, out string? name)
    {
        // calling function is a known function? 
        if (ci != null && (ci->callstatus & CIST_TAIL) == 0)
        {
            return funcnamefromcall(L, ci->previous, out name);
        }

        name = null;
        return null; /* no way to find a name */
    }

    private static bool auxgetinfo(
        lua_State* L,
        ReadOnlySpan<char> what,
        ref lua_Debug ar,
        Closure* f,
        CallInfo* ci)
    {
        bool status = true;
        for (; !what.IsEmpty; what = what[1..])
        {
            switch (what[0])
            {
                case 'S':
                    funcinfo(ref ar, f);
                    break;

                case 'l':
                    ar.currentline = (ci != null && isLua(ci)) ? getcurrentline(ci) : -1;
                    break;

                case 'u':
                    ar.nups = f == null ? (byte)0 : f->c.nupvalues;
                    if (!LuaClosure(f))
                    {
                        ar.isvararg = true;
                        ar.nparams = 0;
                    }
                    else
                    {
                        ar.isvararg = isvararg(f->l.p);
                        ar.nparams = f->l.p->numparams;
                    }

                    break;

                case 't':
                    {
                        if (ci != null)
                        {
                            ar.istailcall = (ci->callstatus & CIST_TAIL) != 0;
                            ar.extraargs = (byte)((ci->callstatus & MAX_CCMT) >> CIST_CCMT);
                        }
                        else
                        {
                            ar.istailcall = false;
                            ar.extraargs = 0;
                        }

                        break;
                    }

                case 'n':
                    ar.namewhat = getfuncname(L, ci, out ar.name);
                    if (ar.namewhat == null)
                    {
                        ar.namewhat = ""; /* not found */
                        ar.name = null;
                    }

                    break;

                case 'r':
                    if (ci == null || (ci->callstatus & CIST_HOOKED) == 0)
                    {
                        ar.ftransfer = ar.ntransfer = 0;
                    }
                    else
                    {
//           ar->ftransfer = L->transferinfo.ftransfer;
//           ar->ntransfer = L->transferinfo.ntransfer;
                        throw new NotImplementedException();
                    }

                    break;

                case 'L':
                case 'f': /* handled by lua_getinfo */
                    break;

                default:
                    status = false; /* invalid option */
                    break;
            }
        }

        return status;
    }

    public static partial bool lua_getinfo(lua_State* L, string what, ref lua_Debug ar)
    {
        ReadOnlySpan<char> whatSpan = what;

        lua_lock(L);

        CallInfo* ci;
        TValue* func;
        if (whatSpan.StartsWith('>'))
        {
            ci = null;
            func = s2v(L->top.p - 1);
            Debug.Assert(ttisfunction(func), "function expected");
            whatSpan = whatSpan[1..]; /* skip the '>' */
            L->top.p--; /* pop function */
        }
        else
        {
            ci = ar.i_ci;
            func = s2v(ci->func.p);
            Debug.Assert(ttisfunction(func));
        }

        Closure* cl = ttisclosure(func) ? clvalue(func) : null;
        bool status = auxgetinfo(L, whatSpan, ref ar, cl, ci);
        if (whatSpan.Contains('f'))
        {
            setobj2s(L, L->top.p, func);
            api_incr_top(L);
        }

        if (whatSpan.Contains('L'))
        {
            collectvalidlines(L, cl);
        }

        lua_unlock(L);
        return status;
    }

    /*
    ** {======================================================
    ** Symbolic Execution
    ** =======================================================
    */

    private static int filterpc(int pc, int jmptarget)
    {
        if (pc < jmptarget) /* is code conditional (inside a jump)? */
        {
            return -1; /* cannot know who sets that register */
        }

        return pc; /* current position sets that register */
    }

    /*
     ** Try to find last instruction before 'lastpc' that modified register 'reg'.
     */
    private static int findsetreg(Proto* p, int lastpc, int reg)
    {
        int setreg = -1; /* keep last instruction that changed 'reg' */
        int jmptarget = 0; /* any code before this address is conditional */
        if (testMMMode((OpMode)GET_OPCODE(p->code[lastpc])))
        {
            lastpc--; /* previous instruction was not actually executed */
        }

        for (int pc = 0; pc < lastpc; pc++)
        {
            uint i = p->code[pc];
            OpCode op = GET_OPCODE(i);
            int a = GETARG_A(i);
            bool change; /* true if current instruction changed 'reg' */
            switch (op)
            {
                case OpCode.OP_LOADNIL:
                    {
                        /* set registers from 'a' to 'a+b' */
                        int b = GETARG_B(i);
                        change = a <= reg && reg <= a + b;
                        break;
                    }

                case OpCode.OP_TFORCALL:
                    /* affect all regs above its base */
                    change = reg >= a + 2;
                    break;

                case OpCode.OP_CALL:
                case OpCode.OP_TAILCALL:
                    /* affect all registers above base */
                    change = reg >= a;
                    break;

                case OpCode.OP_JMP:
                    {
                        /* doesn't change registers, but changes 'jmptarget' */
                        int b = GETARG_sJ(i);
                        int dest = pc + 1 + b;
                        /* jump does not skip 'lastpc' and is larger than current one? */
                        if (dest <= lastpc && dest > jmptarget)
                        {
                            jmptarget = dest; /* update 'jmptarget' */
                        }

                        change = false;
                        break;
                    }
                default: /* any instruction that sets A */
                    change = testAMode((OpMode)op) && reg == a;
                    break;
            }

            if (change)
            {
                setreg = filterpc(pc, jmptarget);
            }
        }

        return setreg;
    }

    /*
     ** Find a "name" for the constant 'c'.
     */
    private static string? kname(Proto* p, int index, out string name)
    {
        TValue* kvalue = &p->k[index];
        if (ttisstring(kvalue))
        {
            name = getnetstr(tsvalue(kvalue));
            return "constant";
        }

        name = "?";
        return null;
    }

    private static string? basicgetobjname(Proto* p, ref int pc, int reg, out string? name)
    {
        name = luaF_getlocalname(p, reg + 1, pc);
        if (name != null) /* is a local? */
        {
            return strlocal;
        }

        /* else try symbolic execution */
        pc = findsetreg(p, pc, reg);
        if (pc != -1)
        {
            /* could find instruction? */
            uint i = p->code[pc];
            OpCode op = GET_OPCODE(i);
            switch (op)
            {
                case OpCode.OP_MOVE:
                    {
                        int b = GETARG_B(i); /* move from 'b' to 'a' */
                        if (b < GETARG_A(i))
                        {
                            return basicgetobjname(p, ref pc, b, out name); /* get name for 'b' */
                        }

                        break;
                    }

                case OpCode.OP_GETUPVAL:
                    name = upvalname(p, GETARG_B(i));
                    return strupval;

                case OpCode.OP_LOADK:
                    return kname(p, GETARG_Bx(i), out name);
                
                case OpCode.OP_LOADKX:
                    return kname(p, GETARG_Ax(p->code[pc + 1]), out name);
            }
        }

        return null; /* could not find reasonable name */
    }

    /*
    ** Find a "name" for the register 'c'.
    */
    private static void rname(Proto* p, int pc, int c, out string? name)
    {
        string? what = basicgetobjname(p, ref pc, c, out name); /* search for 'c' */
        if (!(what != null && what.StartsWith('c'))) /* did not find a constant name? */
        {
            name = "?";
        }
    }

    /*
     ** Check whether table being indexed by instruction 'i' is the
     ** environment '_ENV'
     */
    private static string isEnv(Proto* p, int pc, uint i, bool isup)
    {
        int t = GETARG_B(i); /* table index */
        string? name; /* name of indexed variable */
        if (isup) /* is 't' an upvalue? */
        {
            name = upvalname(p, t);
        }
        else
        {
            /* 't' is a register */
            string? what = basicgetobjname(p, ref pc, t, out name);
            /* 'name' must be the name of a local variable (at the current
               level or an upvalue) */
            if (what != strlocal && what != strupval)
            {
                name = null; /* cannot be the variable _ENV */
            }
        }

        return string.Equals(name, LUA_ENV, StringComparison.Ordinal) ? "global" : "field";
    }

    /*
     ** Extend 'basicgetobjname' to handle table accesses
     */
    private static string? getobjname(Proto* p, int lastpc, int reg, out string? name)
    {
        string? kind = basicgetobjname(p, ref lastpc, reg, out name);
        if (kind != null)
        {
            return kind;
        }

        if (lastpc != -1)
        {
            /* could find instruction? */
            uint i = p->code[lastpc];
            OpCode op = GET_OPCODE(i);
            switch (op)
            {
                case OpCode.OP_GETTABUP:
                    {
                        int k = (int)GETARG_C(i); /* key index */
                        kname(p, k, out name);
                        return isEnv(p, lastpc, i, true);
                    }
                case OpCode.OP_GETTABLE:
                    {
                        int k = (int)GETARG_C(i); /* key index */
                        rname(p, lastpc, k, out name);
                        return isEnv(p, lastpc, i, false);
                    }

                case OpCode.OP_GETI:
                    name = "integer index";
                    return "field";

                case OpCode.OP_GETFIELD:
                    {
                        int k = (int)GETARG_C(i); /* key index */
                        kname(p, k, out name);
                        return isEnv(p, lastpc, i, false);
                    }

                case OpCode.OP_SELF:
                    {
                        int k = (int)GETARG_C(i); /* key index */
                        kname(p, k, out name);
                        return "method";
                    }

            }
        }

        return null; /* could not find reasonable name */
    }

    /*
     ** Try to find a name for a function based on the code that called it.
     ** (Only works when function was called by a Lua function.)
     ** Returns what the name is (e.g., "for iterator", "method",
     ** "metamethod") and sets '*name' to point to the name.
     */
    private static string? funcnamefromcode(lua_State* L, Proto* p, int pc, out string? name)
    {
        TMS tm; /* (initial value avoids warnings) */
        uint i = p->code[pc]; /* calling instruction */
        switch (GET_OPCODE(i))
        {
            case OpCode.OP_CALL:
            case OpCode.OP_TAILCALL:
                return getobjname(p, pc, GETARG_A(i), out name); /* get function name */

            case OpCode.OP_TFORCALL:
                /* for iterator */
                name = "for iterator";
                return "for iterator";

            /* other instructions can do calls through metamethods */
            case OpCode.OP_SELF:
            case OpCode.OP_GETTABUP:
            case OpCode.OP_GETTABLE:
            case OpCode.OP_GETI:
            case OpCode.OP_GETFIELD:
                tm = TMS.INDEX;
                break;

            case OpCode.OP_SETTABUP:
            case OpCode.OP_SETTABLE:
            case OpCode.OP_SETI:
            case OpCode.OP_SETFIELD:
                tm = TMS.NEWINDEX;
                break;

            case OpCode.OP_MMBIN:
            case OpCode.OP_MMBINI:
            case OpCode.OP_MMBINK:
                tm = (TMS)GETARG_C(i);
                break;

            case OpCode.OP_UNM:
                tm = TMS.UNM;
                break;

            case OpCode.OP_BNOT:
                tm = TMS.BNOT;
                break;

            case OpCode.OP_LEN:
                tm = TMS.LEN;
                break;

            case OpCode.OP_CONCAT:
                tm = TMS.CONCAT;
                break;

            case OpCode.OP_EQ:
                tm = TMS.EQ;
                break;

            /* no cases for OP_EQI and OP_EQK, as they don't call metamethods */
            case OpCode.OP_LT:
            case OpCode.OP_LTI:
            case OpCode.OP_GTI:
                tm = TMS.LT;
                break;

            case OpCode.OP_LE:
            case OpCode.OP_LEI:
            case OpCode.OP_GEI:
                tm = TMS.LE;
                break;

            case OpCode.OP_CLOSE:
            case OpCode.OP_RETURN:
                tm = TMS.CLOSE;
                break;

            default:
                name = null;
                return null; /* cannot find a reasonable name */
        }

        name = getnetstr(G(L)->tmname[(int)tm])[2..];
        return "metamethod";
    }

    /*
     ** Try to find a name for a function based on how it was called.
     */
    private static string? funcnamefromcall(lua_State* L, CallInfo* ci, out string? name)
    {
        if ((ci->callstatus & CIST_HOOKED) != 0)
        {
            // was it called inside a hook? 
            name = "?";
            return "hook";
        }

        if ((ci->callstatus & CIST_FIN) != 0)
        {
            // was it called as a finaliser? 
            name = "__gc";
            return "metamethod"; // report it as such
        }
        
        if (isLua(ci))
        {
            return funcnamefromcode(L, ci_func(ci)->p, currentpc(ci), out name);
        }

        name = null;
        return null;
    }
    
    /*
    ** Check whether pointer 'o' points to some value in the stack frame of
    ** the current function and, if so, returns its index.  Because 'o' may
    ** not point to a value in this stack, we cannot compare it with the
    ** region boundaries (undefined behaviour in ISO C).
    */
    private static int instack(CallInfo* ci, TValue* o)
    {
        StkId @base = ci->func.p + 1;
        for (int pos = 0; @base + pos < ci->top.p; pos++)
        {
            if (o == s2v(@base + pos))
            {
                return pos;
            }
        }

        return -1; /* not found */
    }

    /*
     ** Checks whether value 'o' came from an upvalue. (That can only happen
     ** with instructions OP_GETTABUP/OP_SETTABUP, which operate directly on
     ** upvalues.)
     */
    private static string? getupvalname(CallInfo* ci, TValue* o, out string? name)
    {
        LClosure* c = ci_func(ci);
        for (int i = 0; i < c->nupvalues; i++)
        {
            if ((&c->upvals)[i]->v.p == o)
            {
                name = upvalname(c->p, i);
                return strupval;
            }
        }

        name = null;
        return null;
    }

    private static string formatvarinfo(
        lua_State* L,
        string? kind,
        string? name)
    {
        if (kind == null)
        {
            return ""; /* no information */
        }

        return luaO_pushfstring(L, " (%s '%s')", kind, name ?? "");
    }

    /*
     ** Build a string with a "description" for the value 'o', such as
     ** "variable 'x'" or "upvalue 'y'".
     */
    private static string varinfo(lua_State* L, TValue* o)
    {
        CallInfo* ci = L->ci;
        string? name = null; /* to avoid warnings */
        string? kind = null;
        if (isLua(ci))
        {
            kind = getupvalname(ci, o, out name); /* check whether 'o' is an upvalue */
            if (kind == null)
            {
                /* not an upvalue? */
                int reg = instack(ci, o); /* try a register */
                if (reg >= 0) /* is 'o' a register? */
                {
                    kind = getobjname(ci_func(ci)->p, currentpc(ci), reg, out name);
                }
            }
        }

        return formatvarinfo(L, kind, name);
    }

    /*
     ** Raise a type error
     */
    [DoesNotReturn]
    private static void typeerror(
        lua_State* L,
        TValue* o,
        string op,
        string extra)
    {
        string t = luaT_objtypename(L, o);
        luaG_runerror(L, "attempt to %s a %s value%s", op, t, extra);
    }

    /*
     ** Raise a type error with "standard" information about the faulty
     ** object 'o' (using 'varinfo').
     */
    internal static partial void luaG_typeerror(lua_State* L, TValue* o, string op)
    {
        typeerror(L, o, op, varinfo(L, o));
    }

    /*
     ** Raise an error for calling a non-callable object. Try to find a name
     ** for the object based on how it was called ('funcnamefromcall'); if it
     ** cannot get a name there, try 'varinfo'.
     */
    internal static partial void luaG_callerror(lua_State* L, TValue* o)
    {
        CallInfo* ci = L->ci;
        string? kind = funcnamefromcall(L, ci, out string? name);
        string extra = kind != null ? formatvarinfo(L, kind, name) : varinfo(L, o);
        typeerror(L, o, "call", extra);
    }

    internal static partial void luaG_forerror(lua_State* L, TValue* o, string what)
    {
        luaG_runerror(L, "bad 'for' %s (number expected, got %s)", what, luaT_objtypename(L, o));
    }

    internal static partial void luaG_concaterror(lua_State* L, TValue* p1, TValue* p2)
    {
        if (ttisstring(p1) || cvt2str(p1))
        {
            p1 = p2;
        }

        luaG_typeerror(L, p1, "concatenate");
    }

    internal static partial void luaG_opinterror(lua_State* L, TValue* p1, TValue* p2, string msg)
    {
        if (!ttisnumber(p1)) /* first operand is wrong? */
        {
            p2 = p1; /* now second is wrong */
        }

        luaG_typeerror(L, p2, msg);
    }

    /*
     ** Error when both values are convertible to numbers, but not to integers
     */
    internal static partial void luaG_tointerror(lua_State* L, TValue* p1, TValue* p2)
    {
        if (!luaV_tointegerns(p1, out long _, LUA_FLOORN2I))
        {
            p2 = p1;
        }

        luaG_runerror(L, "number%s has no integer representation", varinfo(L, p2));
    }

    internal static partial void luaG_ordererror(lua_State* L, TValue* p1, TValue* p2)
    {
        string t1 = luaT_objtypename(L, p1);
        string t2 = luaT_objtypename(L, p2);
        if (string.Equals(t1, t2, StringComparison.Ordinal))
        {
            luaG_runerror(L, "attempt to compare two %s values", t1);
        }
        else
        {
            luaG_runerror(L, "attempt to compare %s with %s", t1, t2);
        }
    }

    private static partial void luaG_errnnil(lua_State* L, LClosure* cl, int k)
    {
        string globalname = "?"; /* default name if k == 0 */
        if (k > 0)
        {
            kname(cl->p, k - 1, out globalname);
        }

        luaG_runerror(L, "global '%s' already defined", globalname);
    }

    /* add src:line information to 'msg' */
    internal static partial string luaG_addinfo(lua_State* L, string msg, TString* src, int line)
    {
        if (src == null) /* no debug information? */
        {
            return luaO_pushfstring(L, "?:?: %s", msg);
        }

        string id = getnetstr(src);
        string buff = luaO_chunkid(id);
        return luaO_pushfstring(L, "%s:%d: %s", buff, line, msg);
    }

    internal static partial void luaG_errormsg(lua_State* L)
    {
        if (L->errfunc != 0)
        {
            // is there an error handling function? 
            StkId errfunc = restorestack(L, L->errfunc);
            Debug.Assert(ttisfunction(s2v(errfunc)));
            setobjs2s(L, L->top.p, L->top.p - 1); /* move argument */
            setobjs2s(L, L->top.p - 1, errfunc); /* push function */
            L->top.p++; /* assume EXTRA_STACK */
            luaD_callnoyield(L, L->top.p - 2, 1); /* call it */
        }

        if (ttisnil(s2v(L->top.p - 1)))
        {
            // error object is nil? 
            // change it to a proper message 
            setsvalue2s(L, L->top.p - 1, luaS_newliteral(L, "<no error object>"));
        }

        luaD_throw(L, LUA_ERRRUN);
    }

    internal static partial void luaG_runerror(lua_State* L, string fmt, params object[] args)
    {
        CallInfo* ci = L->ci;
        luaC_checkGC(L); /* error message uses memory */
        pushvfstring(L, args, fmt, out string msg);
        if (isLua(ci))
        {
            // Lua function?
            // add source:line information 
            luaG_addinfo(L, msg, ci_func(ci)->p->source, getcurrentline(ci));
            setobjs2s(L, L->top.p - 2, L->top.p - 1); /* remove 'msg' */
            L->top.p--;
        }

        luaG_errormsg(L);
    }

    /*
     ** Check whether new instruction 'newpc' is in a different line from
     ** previous instruction 'oldpc'. More often than not, 'newpc' is only
     ** one or a few instructions after 'oldpc' (it must be after, see
     ** caller), so try to avoid calling 'luaG_getfuncline'. If they are
     ** too far apart, there is a good chance of a ABSLINEINFO in the way,
     ** so it goes directly to 'luaG_getfuncline'.
     */
    private static bool changedline(Proto* p, int oldpc, int newpc)
    {
        if (p->lineinfo == null) /* no debug information? */
        {
            return false;
        }

        if (newpc - oldpc < MAXIWTHABS / 2)
        {
            /* not too far apart? */
            int delta = 0; /* line difference */
            int pc = oldpc;
            for (;;)
            {
                int lineinfo = p->lineinfo[++pc];
                if (lineinfo == ABSLINEINFO)
                {
                    break; /* cannot compute delta; fall through */
                }

                delta += lineinfo;
                if (pc == newpc)
                {
                    return delta != 0; /* delta computed successfully */
                }
            }
        }

        /* either instructions are too far apart or there is an absolute line
           info in the way; compute line difference explicitly */
        return luaG_getfuncline(p, oldpc) != luaG_getfuncline(p, newpc);
    }

    /*
     ** Traces Lua calls. If code is running the first instruction of a function,
     ** and function is not vararg, and it is not coming from an yield,
     ** calls 'luaD_hookcall'. (Vararg functions will call 'luaD_hookcall'
     ** after adjusting its variable arguments; otherwise, they could call
     ** a line/count hook before the call hook. Functions coming from
     ** an yield already called 'luaD_hookcall' before yielding.)
     */
    private static partial bool luaG_tracecall(lua_State* L)
    {
        CallInfo* ci = L->ci;
        Proto* p = ci_func(ci)->p;
        ci->u.l.trap = 1; /* ensure hooks will be checked */
        if (ci->u.l.savedpc == p->code)
        {
            /* first instruction (not resuming)? */
            if (isvararg(p))
            {
                return false; /* hooks will start at VARARGPREP instruction */
            }

            if ((ci->callstatus & CIST_HOOKYIELD) == 0) /* not yielded? */
            {
                luaD_hookcall(L, ci); /* check 'call' hook */
            }
        }

        return true; /* keep 'trap' on */
    }

    /*
     ** Traces the execution of a Lua function. Called before the execution
     ** of each opcode, when debug is on. 'L->oldpc' stores the last
     ** instruction traced, to detect line changes. When entering a new
     ** function, 'npci' will be zero and will test as a new line whatever
     ** the value of 'oldpc'.  Some exceptional conditions may return to
     ** a function without setting 'oldpc'. In that case, 'oldpc' may be
     ** invalid; if so, use zero as a valid value. (A wrong but valid 'oldpc'
     ** at most causes an extra call to a line hook.)
     ** This function is not "Protected" when called, so it should correct
     ** 'L->top.p' before calling anything that can run the GC.
     */
    private static partial bool luaG_traceexec(lua_State* L, uint* pc)
    {
        CallInfo* ci = L->ci;
        byte mask = L->hookmask;
        Proto* p = ci_func(ci)->p;
        if ((mask & (LUA_MASKLINE | LUA_MASKCOUNT)) == 0)
        {
            /* no hooks? */
            ci->u.l.trap = 0; /* don't need to stop again */
            return false; /* turn off 'trap' */
        }

        pc++; /* reference is always next instruction */
        ci->u.l.savedpc = pc; /* save 'pc' */
        bool counthook = (mask & LUA_MASKCOUNT) != 0 && --L->hookcount == 0;
        if (counthook)
        {
            resethookcount(L); /* reset count */
        }
        else if ((mask & LUA_MASKLINE) == 0)
        {
            return true; /* no line hook and count != 0; nothing to be done now */
        }

        if ((ci->callstatus & CIST_HOOKYIELD) != 0)
        {
            /* hook yielded last time? */
            ci->callstatus &= ~CIST_HOOKYIELD; /* erase mark */
            return true; /* do not call hook again (VM yielded, so it did not move) */
        }

        if (!luaP_isIT(*(ci->u.l.savedpc - 1))) /* top not being used? */
        {
            L->top.p = ci->top.p; /* correct top */
        }

        if (counthook)
        {
            luaD_hook(L, LUA_HOOKCOUNT, -1, 0, 0); /* call count hook */
        }

        if ((mask & LUA_MASKLINE) != 0)
        {
            /* 'L->oldpc' may be invalid; use zero in this case */
            int oldpc = L->oldpc < p->sizecode ? L->oldpc : 0;
            int npci = pcRel(pc, p);
            if (npci <= oldpc || /* call hook when jump back (loop), */
                changedline(p, oldpc, npci))
            {
                /* or when enter new line */
                int newline = luaG_getfuncline(p, npci);
                luaD_hook(L, LUA_HOOKLINE, newline, 0, 0); /* call line hook */
            }

            L->oldpc = npci; /* 'pc' of last call to line hook */
        }

        if (L->status == LUA_YIELD)
        {
            /* did hook yield? */
            if (counthook)
            {
                L->hookcount = 1; /* undo decrement to zero */
            }

            ci->callstatus |= CIST_HOOKYIELD; /* mark that it yielded */
            luaD_throw(L, LUA_YIELD);
        }

        return true; /* keep 'trap' on */
    }
}
