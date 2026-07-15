namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

public static unsafe partial class Lua
{
    // $Id: lparser.c $
    // Lua Parser
    // See Copyright Notice in lua.h

    // Expression and variable descriptor.
    // Code generation for variables and expressions can be delayed to allow
    // optimisations; An 'expdesc' structure describes a potentially-delayed
    // variable/expression. It has a description of its "main" value plus a
    // list of conditional jumps that can also produce its value (generated
    // by short-circuit operators 'and'/'or').

    /// <summary>
    /// kinds of variables/expressions
    /// </summary>
    private enum expkind
    {
        VVOID, // when 'expdesc' describes the last expression of a list,
        // this kind means an empty list (so, no expression)
        VNIL, // constant nil
        VTRUE, // constant true
        VFALSE, // constant false
        VK, // constant in 'k'; info = index of constant in 'k'
        VKFLT, // floating constant; nval = numerical float value
        VKINT, // integer constant; ival = numerical integer value
        VKSTR, // string constant; strval = TString address;
        // (string is fixed by the scanner)
        VNONRELOC, // expression has its value in a fixed register;
        // info = result register
        VLOCAL, // local variable; var.ridx = register index;
        // var.vidx = relative index in 'actvar.arr'
        VVARGVAR, // vararg parameter; var.ridx = register index;
        // var.vidx = relative index in 'actvar.arr'
        VGLOBAL, // global variable;
        // info = relative index in 'actvar.arr' (or -1 for
        // implicit declaration)
        VUPVAL, // upvalue variable; info = index of upvalue in 'upvalues'
        VCONST, // compile-time <const> variable;
        // info = absolute index in 'actvar.arr'
        VINDEXED, // indexed variable;
        // ind.t = table register;
        // ind.idx = key's R index;
        // ind.ro = true if it represents a read-only global;
        // ind.keystr = if key is a string, index in 'k' of that string;
        // -1 if key is not a string
        VVARGIND, // indexed vararg parameter;
        // ind.* as in VINDEXED
        VINDEXUP, // indexed upvalue;
        // ind.idx = key's K index;
        // ind.* as in VINDEXED
        VINDEXI, // indexed variable with constant integer;
        // ind.t = table register;
        // ind.idx = key's value
        VINDEXSTR, // indexed variable with literal string;
        // ind.idx = key's K index;
        // ind.* as in VINDEXED
        VJMP, // expression is a test/comparison;
        // info = pc of corresponding jump instruction
        VRELOC, // expression can put result in any register;
        // info = instruction pc
        VCALL, // expression is a function call; info = instruction pc
        VVARARG, // vararg expression; info = instruction pc
    }

    private static bool vkisvar(expkind k)
    {
        return k is >= expkind.VLOCAL and <= expkind.VINDEXSTR;
    }

    private static bool vkisindexed(expkind k)
    {
        return k is >= expkind.VINDEXED and <= expkind.VINDEXSTR;
    }

    private struct expdesc
    {
        public struct Ind
        {
            public short idx; // index (R or "long" K)
            public byte t; // table (register or upvalue)
            public bool ro; // true if variable is read-only
            public int keystr; // index in 'k' of string key, or -1 if not a string
        }

        public struct Var
        {
            public byte ridx; // register holding the variable
            public short vidx; // index in 'actvar.arr'
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct U
        {
            [FieldOffset(0)] public long ival; // for VKINT
            [FieldOffset(0)] public double nval; // for VKFLT
            [FieldOffset(0)] public TString *strval; // for VKSTR
            [FieldOffset(0)] public int info; // for generic use
            [FieldOffset(0)] public Ind ind; // for indexed variables
            [FieldOffset(0)] public Var var; // for local variables
        }

        public expkind k;
        public U u;
        public int t; // patch list of 'exit when true'
        public int f; // patch list of 'exit when false'
    }

    /// <summary>
    /// kinds of variables
    /// </summary>
    private const int VDKREG = 0; // regular local
    private const int RDKCONST = 1; // local constant
    private const int RDKVAVAR = 2; // vararg parameter
    private const int RDKTOCLOSE = 3; // to-be-closed
    private const int RDKCTC = 4; // local compile-time constant
    private const int GDKREG = 5; // regular global
    private const int GDKCONST = 6; // global constant

    /// <summary>
    /// variables that live in registers
    /// </summary>
    private static bool varinreg(Vardesc* v)
    {
        return v->vd.kind <= RDKTOCLOSE;
    }

    /// <summary>
    /// test for global variables
    /// </summary>
    private static bool varglobal(Vardesc* v)
    {
        return v->vd.kind >= GDKREG;
    }

    /// <summary>
    /// description of an active variable
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct Vardesc
    {
        public struct Vd
        {
            public Value value_;
            public byte tt_;
            public byte kind;
            public byte ridx; // register holding the variable
            public short pidx; // index of the variable in the Proto's 'locvars' array
            public TString* name; // variable name
        }

        [FieldOffset(0)] public Vd vd;
        [FieldOffset(0)] public TValue k; // constant value (if any)
    }

    /// <summary>
    /// description of pending goto statements and label statements
    /// </summary>
    private struct Labeldesc
    {
        public TString* name; // label identifier
        public int pc; // position in code
        public int line; // line where it appeared
        public short nactvar; // number of active variables in that position
        public bool close; // true for goto that escapes upvalues
    }

    /// <summary>
    /// list of labels or gotos
    /// </summary>
    private struct Labellist
    {
        public Labeldesc* arr; // array
        public int n; // number of entries in use
        public int size; // array size
    }

    /// <summary>
    /// dynamic structures used by the parser
    /// </summary>
    private struct Dyndata
    {
        public struct ActVar
        {
            /// <summary>
            /// list of all active local variables
            /// </summary>
            public Vardesc* arr;
            public int n;
            public int size;
        }

        public ActVar actvar;
        public Labellist gt; // list of pending gotos
        public Labellist label; // list of active labels
    }

    /// <summary>
    /// state needed to generate code for a given function
    /// </summary>
    private struct FuncState
    {
        public Proto* f; // current function header
        public FuncState* prev; // enclosing function
        public LexState* ls; // lexical state
        public BlockCnt* bl; // chain of current blocks
        public Table* kcache; // cache for reusing constants
        public int pc; // next position to code (equivalent to 'ncode')
        public int lasttarget; // 'label' of last 'jump label'
        public int previousline; // last line that was saved in 'lineinfo'
        public int nk; // number of elements in 'k'
        public int np; // number of elements in 'p'
        public int nabslineinfo; // number of elements in 'abslineinfo'
        public int firstlocal; // index of first local var (in Dyndata array)
        public int firstlabel; // index of first label (in 'dyd->label->arr')
        public short ndebugvars; // number of elements in 'f->locvars'
        public short nactvar; // number of active variable declarations
        public byte nups; // number of upvalues
        public byte freereg; // first free register
        public byte iwthabs; // instructions issued since last absolute line info
        public bool needclose; // function needs to close upvalues when returning
    }
    
    /// <summary>
    /// maximum number of variable declarations per function (must be
    /// smaller than 250, due to the bytecode format)
    /// </summary>
    private const int MAXVARS = 200;

    private static bool hasmultret(expkind k)
    {
        return k is expkind.VCALL or expkind.VVARARG;
    }

    /// <summary>
    /// because all strings are unified by the scanner, the parser
    /// can use pointer equality for string equality
    /// </summary>
    private static bool eqstr(TString* a, TString* b)
    {
        return a == b;
    }

    /// <summary>
    /// nodes for block list (list of active blocks)
    /// </summary>
    private struct BlockCnt
    {
        public BlockCnt* previous; // chain
        public int firstlabel; // index of first label in this block
        public int firstgoto; // index of first pending goto in this block
        public short nactvar; // number of active declarations at block entry
        public bool upval; // true if some variable in the block is an upvalue
        public byte isloop; // 1 if 'block' is a loop; 2 if it has pending breaks
        public bool insidetbc; // true if inside the scope of a to-be-closed var.
    }

    [DoesNotReturn]
    private static void error_expected(LexState* ls, int token)
    {
        luaX_syntaxerror(
            ls,
            luaO_pushfstring(ls->L, "%s expected", luaX_token2str(ls, token)));
    }

    [DoesNotReturn]
    private static void errorlimit(FuncState* fs, int limit, string what)
    {
        lua_State* L = fs->ls->L;
        int line = fs->f->linedefined;
        string where = line == 0
            ? "main function"
            : luaO_pushfstring(L, "function at line %d", line);
        string msg = luaO_pushfstring(
            L,
            "too many %s (limit is %d) in %s",
            what,
            limit,
            where);
        luaX_syntaxerror(fs->ls, msg);
    }

    private static void luaY_checklimit(FuncState* fs, int v, int l, string what)
    {
        if (v > l)
        {
            errorlimit(fs, l, what);
        }
    }

    /// <summary>
    /// Test whether next token is 'c'; if so, skip it.
    /// </summary>
    private static bool testnext(LexState* ls, int c)
    {
        if (ls->t.token == c)
        {
            luaX_next(ls);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check that next token is 'c'.
    /// </summary>
    private static void check(LexState* ls, int c)
    {
        if (ls->t.token != c)
        {
            error_expected(ls, c);
        }
    }

    /// <summary>
    /// Check that next token is 'c' and skip it.
    /// </summary>
    private static void checknext(LexState* ls, int c)
    {
        check(ls, c);
        luaX_next(ls);
    }

    private static void check_condition(LexState* ls, bool c, string msg)
    {
        if (!c)
        {
            luaX_syntaxerror(ls, msg);
        }
    }

    /// <summary>
    /// Check that next token is 'what' and skip it. In case of error,
    /// raise an error that the expected 'what' should match a 'who'
    /// in line 'where' (if that is not the current line).
    /// </summary>
    private static void check_match(LexState* ls, int what, int who, int where)
    {
        if (!testnext(ls, what))
        {
            if (where == ls->linenumber) // all in the same line?
            {
                error_expected(ls, what); // do not need a complex message
            }
            else
            {
                luaX_syntaxerror(
                    ls,
                    luaO_pushfstring(
                        ls->L,
                        "%s expected (to close %s at line %d)",
                        luaX_token2str(ls, what),
                        luaX_token2str(ls, who),
                        where));
            }
        }
    }

    private static TString* str_checkname(LexState* ls)
    {
        check(ls, (int)RESERVED.TK_NAME);
        TString* ts = ls->t.seminfo.ts;
        luaX_next(ls);
        return ts;
    }

    private static void init_exp(expdesc* e, expkind k, int i)
    {
        e->f = e->t = NO_JUMP;
        e->k = k;
        e->u.info = i;
    }

    private static void codestring(expdesc* e, TString* s)
    {
        e->f = e->t = NO_JUMP;
        e->k = expkind.VKSTR;
        e->u.strval = s;
    }

    private static void codename(LexState* ls, expdesc* e)
    {
        codestring(e, str_checkname(ls));
    }

    /// <summary>
    /// Register a new local variable in the active 'Proto' (for debug
    /// information).
    /// </summary>
    private static short registerlocalvar(
        LexState* ls,
        FuncState* fs,
        TString* varname)
    {
        Proto* f = fs->f;
        int oldsize = f->sizelocvars;
        luaM_growvector(
            ls->L,
            ref f->locvars,
            fs->ndebugvars,
            ref f->sizelocvars,
            short.MaxValue,
            "local variables");
        while (oldsize < f->sizelocvars)
        {
            f->locvars[oldsize++].varname = null;
        }

        f->locvars[fs->ndebugvars].varname = varname;
        f->locvars[fs->ndebugvars].startpc = fs->pc;
        luaC_objbarrier(ls->L, (GCObject*)f, (GCObject*)varname);
        return fs->ndebugvars++;
    }

    /// <summary>
    /// Create a new variable with the given 'name' and given 'kind'.
    /// Return its index in the function.
    /// </summary>
    private static int new_varkind(LexState* ls, TString* name, byte kind)
    {
        lua_State* L = ls->L;
        FuncState* fs = ls->fs;
        Dyndata* dyd = ls->dyd;
        luaM_growvector(
            L,
            ref dyd->actvar.arr,
            dyd->actvar.n + 1,
            ref dyd->actvar.size,
            short.MaxValue,
            "variable declarations");
        Vardesc* var = &dyd->actvar.arr[dyd->actvar.n++];
        var->vd.kind = kind; // default
        var->vd.name = name;
        return dyd->actvar.n - 1 - fs->firstlocal;
    }

    /// <summary>
    /// Create a new local variable with the given 'name' and regular kind.
    /// </summary>
    private static int new_localvar(LexState* ls, TString* name)
    {
        return new_varkind(ls, name, VDKREG);
    }

    private static int new_localvarliteral(LexState* ls, string v)
    {
        return new_localvar(ls, luaX_newstring(ls, v));
    }

    /// <summary>
    /// Return the "variable description" (Vardesc) of a given variable.
    /// (Unless noted otherwise, all variables are referred to by their
    /// compiler indices.)
    /// </summary>
    private static Vardesc* getlocalvardesc(FuncState* fs, int vidx)
    {
        return &fs->ls->dyd->actvar.arr[fs->firstlocal + vidx];
    }

    /// <summary>
    /// Convert 'nvar', a compiler index level, to its corresponding
    /// register. For that, search for the highest variable below that level
    /// that is in a register and uses its register index ('ridx') plus one.
    /// </summary>
    private static byte reglevel(FuncState* fs, int nvar)
    {
        while (nvar-- > 0)
        {
            Vardesc* vd = getlocalvardesc(fs, nvar); // get previous variable
            if (varinreg(vd)) // is in a register?
            {
                return (byte)(vd->vd.ridx + 1);
            }
        }

        return 0; // no variables in registers
    }

    /// <summary>
    /// Return the number of variables in the register stack for the given
    /// function.
    /// </summary>
    private static byte luaY_nvarstack(FuncState* fs)
    {
        return reglevel(fs, fs->nactvar);
    }

    /// <summary>
    /// Get the debug-information entry for current variable 'vidx'.
    /// </summary>
    private static LocVar* localdebuginfo(FuncState* fs, int vidx)
    {
        Vardesc* vd = getlocalvardesc(fs, vidx);
        if (!varinreg(vd))
        {
            return null; // no debug info. for constants
        }

        int idx = vd->vd.pidx;
        Debug.Assert(idx < fs->ndebugvars);
        return &fs->f->locvars[idx];
    }

    /// <summary>
    /// Create an expression representing variable 'vidx'
    /// </summary>
    private static void init_var(FuncState* fs, expdesc* e, int vidx)
    {
        e->f = e->t = NO_JUMP;
        e->k = expkind.VLOCAL;
        e->u.var.vidx = (short)vidx;
        e->u.var.ridx = getlocalvardesc(fs, vidx)->vd.ridx;
    }

    /// <summary>
    /// Raises an error if variable described by 'e' is read only; moreover,
    /// if 'e' is t[exp] where t is the vararg parameter, change it to index
    /// a real table. (Virtual vararg tables cannot be changed.)
    /// </summary>
    private static void check_readonly(LexState* ls, expdesc* e)
    {
        FuncState* fs = ls->fs;
        TString* varname = null; // to be set if variable is const
        switch (e->k)
        {
            case expkind.VCONST:
                varname = ls->dyd->actvar.arr[e->u.info].vd.name;
                break;

            case expkind.VLOCAL:
            case expkind.VVARGVAR:
                {
                    Vardesc* vardesc = getlocalvardesc(fs, e->u.var.vidx);
                    if (vardesc->vd.kind != VDKREG) // not a regular variable?
                    {
                        varname = vardesc->vd.name;
                    }

                    break;
                }

            case expkind.VUPVAL:
                {
                    Upvaldesc* up = &fs->f->upvalues[e->u.info];
                    if (up->kind != VDKREG)
                    {
                        varname = up->name;
                    }

                    break;
                }

            case expkind.VVARGIND:
                needvatab(fs->f); // function will need a vararg table
                e->k = expkind.VINDEXED;
                goto case expkind.VINDEXUP;

            case expkind.VINDEXUP:
            case expkind.VINDEXSTR:
            case expkind.VINDEXED:
                // global variable
                if (e->u.ind.ro) // read-only?
                {
                    varname = tsvalue(&fs->f->k[e->u.ind.keystr]);
                }

                break;

            default:
                Debug.Assert(e->k == expkind.VINDEXI); // this one doesn't need any check
                return; // integer index cannot be read-only
        }

        if (varname != null)
        {
            luaK_semerror(
                ls,
                "attempt to assign to const variable '%s'",
                getnetstr(varname));
        }
    }

    /// <summary>
    /// Start the scope for the last 'nvars' created variables.
    /// </summary>
    private static void adjustlocalvars(LexState* ls, int nvars)
    {
        FuncState* fs = ls->fs;
        int reglevel = luaY_nvarstack(fs);
        for (int i = 0; i < nvars; i++)
        {
            int vidx = fs->nactvar++;
            Vardesc* var = getlocalvardesc(fs, vidx);
            var->vd.ridx = (byte)reglevel++;
            var->vd.pidx = registerlocalvar(ls, fs, var->vd.name);
            luaY_checklimit(fs, reglevel, MAXVARS, "local variables");
        }
    }

    /// <summary>
    /// Close the scope for all variables up to level 'tolevel'.
    /// (debug info.)
    /// </summary>
    private static void removevars(FuncState* fs, int tolevel)
    {
        fs->ls->dyd->actvar.n -= fs->nactvar - tolevel;
        while (fs->nactvar > tolevel)
        {
            LocVar* var = localdebuginfo(fs, --fs->nactvar);
            if (var != null) // does it have debug information?
            {
                var->endpc = fs->pc;
            }
        }
    }

    /// <summary>
    /// Search the upvalues of the function 'fs' for one
    /// with the given 'name'.
    /// </summary>
    private static int searchupvalue(FuncState* fs, TString* name)
    {
        Upvaldesc* up = fs->f->upvalues;
        for (int i = 0; i < fs->nups; i++)
        {
            if (eqstr(up[i].name, name))
            {
                return i;
            }
        }

        return -1; // not found
    }

    private static Upvaldesc* allocupvalue(FuncState* fs)
    {
        Proto* f = fs->f;
        int oldsize = f->sizeupvalues;
        luaY_checklimit(fs, fs->nups + 1, MAXUPVAL, "upvalues");
        luaM_growvector(
            fs->ls->L,
            ref f->upvalues,
            fs->nups,
            ref f->sizeupvalues,
            MAXUPVAL,
            "upvalues");
        while (oldsize < f->sizeupvalues)
        {
            f->upvalues[oldsize++].name = null;
        }

        return &f->upvalues[fs->nups++];
    }

    private static int newupvalue(FuncState* fs, TString* name, expdesc* v)
    {
        Upvaldesc* up = allocupvalue(fs);
        FuncState* prev = fs->prev;
        if (v->k == expkind.VLOCAL)
        {
            up->instack = 1;
            up->idx = v->u.var.ridx;
            up->kind = getlocalvardesc(prev, v->u.var.vidx)->vd.kind;
            Debug.Assert(eqstr(name, getlocalvardesc(prev, v->u.var.vidx)->vd.name));
        }
        else
        {
            up->instack = 0;
            up->idx = (byte)v->u.info;
            up->kind = prev->f->upvalues[v->u.info].kind;
            Debug.Assert(eqstr(name, prev->f->upvalues[v->u.info].name));
        }

        up->name = name;
        luaC_objbarrier(fs->ls->L, (GCObject*)fs->f, (GCObject*)name);
        return fs->nups - 1;
    }

    /// <summary>
    /// Look for an active variable with the name 'n' in the
    /// function 'fs'. If found, initialise 'var' with it and return
    /// its expression kind; otherwise return -1. While searching,
    /// var-&gt;u.info==-1 means that the preambular global declaration is
    /// active (the default while there is no other global declaration);
    /// var-&gt;u.info==-2 means there is no active collective declaration
    /// (some previous global declaration but no collective declaration);
    /// and var-&gt;u.info&gt;=0 points to the inner-most (the first one found)
    /// collective declaration, if there is one.
    /// </summary>
    private static int searchvar(FuncState* fs, TString* n, expdesc* var)
    {
        for (int i = fs->nactvar - 1; i >= 0; i--)
        {
            Vardesc* vd = getlocalvardesc(fs, i);
            if (varglobal(vd))
            {
                // global declaration?
                if (vd->vd.name == null)
                {
                    // collective declaration?
                    if (var->u.info < 0) // no previous collective declaration?
                    {
                        var->u.info = fs->firstlocal + i; // this is the first one
                    }
                }
                else
                {
                    // global name
                    if (eqstr(n, vd->vd.name))
                    {
                        // found?
                        init_exp(var, expkind.VGLOBAL, fs->firstlocal + i);
                        return (int)expkind.VGLOBAL;
                    }

                    if (var->u.info == -1) // active preambular declaration?
                    {
                        var->u.info = -2; // invalidate preambular declaration
                    }
                }
            }
            else if (eqstr(n, vd->vd.name))
            {
                // found?
                if (vd->vd.kind == RDKCTC) // compile-time constant?
                {
                    init_exp(var, expkind.VCONST, fs->firstlocal + i);
                }
                else
                {
                    // local variable
                    init_var(fs, var, i);
                    if (vd->vd.kind == RDKVAVAR) // vararg parameter?
                    {
                        var->k = expkind.VVARGVAR;
                    }
                }

                return (int)var->k;
            }
        }

        return -1; // not found
    }

    /// <summary>
    /// Mark block where variable at given level was defined
    /// (to emit close instructions later).
    /// </summary>
    private static void markupval(FuncState* fs, int level)
    {
        BlockCnt* bl = fs->bl;
        while (bl->nactvar > level)
        {
            bl = bl->previous;
        }

        bl->upval = true;
        fs->needclose = true;
    }

    /// <summary>
    /// Mark that current block has a to-be-closed variable.
    /// </summary>
    private static void marktobeclosed(FuncState* fs)
    {
        BlockCnt* bl = fs->bl;
        bl->upval = true;
        bl->insidetbc = true;
        fs->needclose = true;
    }

    /// <summary>
    /// Find a variable with the given name 'n'. If it is an upvalue, add
    /// this upvalue into all intermediate functions. If it is a global, set
    /// 'var' as 'void' as a flag.
    /// </summary>
    private static void singlevaraux(FuncState* fs, TString* n, expdesc* var, bool @base)
    {
        int v = searchvar(fs, n, var); // look up variables at current level
        if (v >= 0)
        {
            // found?
            if (!@base)
            {
                if (var->k == expkind.VVARGVAR) // vararg parameter?
                {
                    luaK_vapar2local(fs, var); // change it to a regular local
                }

                if (var->k == expkind.VLOCAL)
                {
                    markupval(fs, var->u.var.vidx); // will be used as an upvalue
                }
            }
            // else nothing else to be done
        }
        else
        {
            // not found at current level; try upvalues
            int idx = searchupvalue(fs, n); // try existing upvalues
            if (idx < 0)
            {
                // not found?
                if (fs->prev != null) // more levels?
                {
                    singlevaraux(fs->prev, n, var, false); // try upper levels
                }

                if (var->k == expkind.VLOCAL || var->k == expkind.VUPVAL) // local or upvalue?
                {
                    idx = newupvalue(fs, n, var); // will be a new upvalue
                }
                else // it is a global or a constant
                {
                    return; // don't need to do anything at this level
                }
            }

            init_exp(var, expkind.VUPVAL, idx); // new or old upvalue
        }
    }

    private static void buildglobal(LexState* ls, TString* varname, expdesc* var)
    {
        FuncState* fs = ls->fs;
        init_exp(var, expkind.VGLOBAL, -1); // global by default
        singlevaraux(fs, ls->envn, var, true); // get environment variable
        if (var->k == expkind.VGLOBAL)
        {
            luaK_semerror(
                ls,
                "%s is global when accessing variable '%s'",
                LUA_ENV,
                getnetstr(varname));
        }

        luaK_exp2anyregup(fs, var); // _ENV could be a constant
        expdesc key;
        codestring(&key, varname); // key is variable name
        luaK_indexed(fs, var, &key); // 'var' represents _ENV[varname]
    }

    /// <summary>
    /// Find a variable with the given name 'n', handling global variables
    /// too.
    /// </summary>
    private static void buildvar(LexState* ls, TString* varname, expdesc* var)
    {
        FuncState* fs = ls->fs;
        init_exp(var, expkind.VGLOBAL, -1); // global by default
        singlevaraux(fs, varname, var, true);
        if (var->k == expkind.VGLOBAL)
        {
            // global name?
            int info = var->u.info;
            // global by default in the scope of a global declaration?
            if (info == -2)
            {
                luaK_semerror(ls, "variable '%s' not declared", getnetstr(varname));
            }

            buildglobal(ls, varname, var);
            if (info != -1 && ls->dyd->actvar.arr[info].vd.kind == GDKCONST)
            {
                var->u.ind.ro = true; // mark variable as read-only
            }
            else // anyway must be a global
            {
                Debug.Assert(info == -1 || ls->dyd->actvar.arr[info].vd.kind == GDKREG);
            }
        }
    }

    private static void singlevar(LexState* ls, expdesc* var)
    {
        buildvar(ls, str_checkname(ls), var);
    }

    /// <summary>
    /// Adjust the number of results from an expression list 'e' with 'nexps'
    /// expressions to 'nvars' values.
    /// </summary>
    private static void adjust_assign(LexState* ls, int nvars, int nexps, expdesc* e)
    {
        FuncState* fs = ls->fs;
        int needed = nvars - nexps; // extra values needed
        luaK_checkstack(fs, needed);
        if (hasmultret(e->k))
        {
            // last expression has multiple returns?
            int extra = needed + 1; // discount last expression itself
            if (extra < 0)
            {
                extra = 0;
            }

            luaK_setreturns(fs, e, extra); // last exp. provides the difference
        }
        else
        {
            if (e->k != expkind.VVOID) // at least one expression?
            {
                luaK_exp2nextreg(fs, e); // close last expression
            }

            if (needed > 0) // missing values?
            {
                luaK_nil(fs, fs->freereg, needed); // complete with nils
            }
        }

        if (needed > 0)
        {
            luaK_reserveregs(fs, needed); // registers for extra values
        }
        else // adding 'needed' is actually a subtraction
        {
            fs->freereg = (byte)(fs->freereg + needed); // remove extra values
        }
    }

    private static void enterlevel(LexState* ls)
    {
        luaE_incCstack(ls->L);
    }

    private static void leavelevel(LexState* ls)
    {
        ls->L->nCcalls--;
    }

    /// <summary>
    /// Generates an error that a goto jumps into the scope of some
    /// variable declaration.
    /// </summary>
    [DoesNotReturn]
    private static void jumpscopeerror(LexState* ls, Labeldesc* gt)
    {
        TString* tsname = getlocalvardesc(ls->fs, gt->nactvar)->vd.name;
        string varname = tsname != null ? getnetstr(tsname) : "*";
        luaK_semerror(
            ls,
            "<goto %s> at line %d jumps into the scope of '%s'",
            getnetstr(gt->name),
            gt->line,
            varname); // raise the error
    }

    /// <summary>
    /// Closes the goto at index 'g' to given 'label' and removes it
    /// from the list of pending gotos.
    /// If it jumps into the scope of some variable, raises an error.
    /// The goto needs a CLOSE if it jumps out of a block with upvalues,
    /// or out of the scope of some variable and the block has upvalues
    /// (signalled by parameter 'bup').
    /// </summary>
    private static void closegoto(LexState* ls, int g, Labeldesc* label, bool bup)
    {
        int i;
        FuncState* fs = ls->fs;
        Labellist* gl = &ls->dyd->gt; // list of gotos
        Labeldesc* gt = &gl->arr[g]; // goto to be resolved
        Debug.Assert(eqstr(gt->name, label->name));
        if (gt->nactvar < label->nactvar) // enter some scope?
        {
            jumpscopeerror(ls, gt);
        }

        if (gt->close ||
            label->nactvar < gt->nactvar && bup)
        {
            // needs close?
            byte stklevel = reglevel(fs, label->nactvar);
            // move jump to CLOSE position
            fs->f->code[gt->pc + 1] = fs->f->code[gt->pc];
            // put CLOSE instruction at original position
            fs->f->code[gt->pc] = CREATE_ABCk(OpCode.Close, stklevel, 0, 0, false);
            gt->pc++; // must point to jump instruction
        }

        luaK_patchlist(ls->fs, gt->pc, label->pc); // goto jumps to label
        for (i = g; i < gl->n - 1; i++) // remove goto from pending list
        {
            gl->arr[i] = gl->arr[i + 1];
        }

        gl->n--;
    }

    /// <summary>
    /// Search for an active label with the given name, starting at
    /// index 'ilb' (so that it can search for all labels in current block
    /// or all labels in current function).
    /// </summary>
    private static Labeldesc* findlabel(LexState* ls, TString* name, int ilb)
    {
        Dyndata* dyd = ls->dyd;
        for (; ilb < dyd->label.n; ilb++)
        {
            Labeldesc* lb = &dyd->label.arr[ilb];
            if (eqstr(lb->name, name)) // correct label?
            {
                return lb;
            }
        }

        return null; // label not found
    }

    /// <summary>
    /// Adds a new label/goto in the corresponding list.
    /// </summary>
    private static int newlabelentry(
        LexState* ls,
        Labellist* l,
        TString* name,
        int line,
        int pc)
    {
        int n = l->n;
        luaM_growvector(ls->L, ref l->arr, n, ref l->size, short.MaxValue, "labels/gotos");
        l->arr[n].name = name;
        l->arr[n].line = line;
        l->arr[n].nactvar = ls->fs->nactvar;
        l->arr[n].close = false;
        l->arr[n].pc = pc;
        l->n = n + 1;
        return n;
    }

    /// <summary>
    /// Create an entry for the goto and the code for it. As it is not known
    /// at this point whether the goto may need a CLOSE, the code has a jump
    /// followed by an CLOSE. (As the CLOSE comes after the jump, it is a
    /// dead instruction; it works as a placeholder.) When the goto is closed
    /// against a label, if it needs a CLOSE, the two instructions swap
    /// positions, so that the CLOSE comes before the jump.
    /// </summary>
    private static int newgotoentry(LexState* ls, TString* name, int line)
    {
        FuncState* fs = ls->fs;
        int pc = luaK_jump(fs); // create jump
        luaK_codeABC(fs, OpCode.Close, 0, 1, 0); // spaceholder, marked as dead
        return newlabelentry(ls, &ls->dyd->gt, name, line, pc);
    }

    /// <summary>
    /// Create a new label with the given 'name' at the given 'line'.
    /// 'last' tells whether label is the last non-op statement in its
    /// block. Solves all pending gotos to this new label and adds
    /// a close instruction if necessary.
    /// Returns true iff it added a close instruction.
    /// </summary>
    private static void createlabel(LexState* ls, TString* name, int line, bool last)
    {
        FuncState* fs = ls->fs;
        Labellist* ll = &ls->dyd->label;
        int l = newlabelentry(ls, ll, name, line, luaK_getlabel(fs));
        if (last)
        {
            // label is last no-op statement in the block?
            // assume that locals are already out of scope
            ll->arr[l].nactvar = fs->bl->nactvar;
        }
    }

    /// <summary>
    /// Traverse the pending gotos of the finishing block checking whether
    /// each match some label of that block. Those that do not match are
    /// "exported" to the outer block, to be solved there. In particular,
    /// its 'nactvar' is updated with the level of the inner block,
    /// as the variables of the inner block are now out of scope.
    /// </summary>
    private static void solvegotos(FuncState* fs, BlockCnt* bl)
    {
        LexState* ls = fs->ls;
        Labellist* gl = &ls->dyd->gt;
        int outlevel = reglevel(fs, bl->nactvar); // level outside the block
        int igt = bl->firstgoto; // first goto in the finishing block
        while (igt < gl->n)
        {
            // for each pending goto
            Labeldesc* gt = &gl->arr[igt];
            // search for a matching label in the current block
            Labeldesc* lb = findlabel(ls, gt->name, bl->firstlabel);
            if (lb != null) // found a match?
            {
                closegoto(ls, igt, lb, bl->upval); // close and remove goto
            }
            else
            {
                // adjust 'goto' for outer block
                // block has variables to be closed and goto escapes the scope of
                // some variable?
                if (bl->upval && reglevel(fs, gt->nactvar) > outlevel)
                {
                    gt->close = true; // jump may need a close
                }

                gt->nactvar = bl->nactvar; // correct level for outer block
                igt++; // go to next goto
            }
        }

        ls->dyd->label.n = bl->firstlabel; // remove local labels
    }

    private static void enterblock(FuncState* fs, BlockCnt* bl, byte isloop)
    {
        bl->isloop = isloop;
        bl->nactvar = fs->nactvar;
        bl->firstlabel = fs->ls->dyd->label.n;
        bl->firstgoto = fs->ls->dyd->gt.n;
        bl->upval = false;
        // inherit 'insidetbc' from enclosing block
        bl->insidetbc = fs->bl != null && fs->bl->insidetbc;
        bl->previous = fs->bl; // link block in function's block list
        fs->bl = bl;
        Debug.Assert(fs->freereg == luaY_nvarstack(fs));
    }

    /// <summary>
    /// generates an error for an undefined 'goto'.
    /// </summary>
    [DoesNotReturn]
    private static void undefgoto(LexState* ls, Labeldesc* gt)
    {
        // breaks are checked when created, cannot be undefined
        Debug.Assert(!eqstr(gt->name, ls->brkn));
        luaK_semerror(
            ls,
            "no visible label '%s' for <goto> at line %d",
            getnetstr(gt->name),
            gt->line);
    }

    private static void leaveblock(FuncState* fs)
    {
        BlockCnt* bl = fs->bl;
        LexState* ls = fs->ls;
        byte stklevel = reglevel(fs, bl->nactvar); // level outside block
        if (bl->previous != null && bl->upval) // need a 'close'?
        {
            luaK_codeABC(fs, OpCode.Close, stklevel, 0, 0);
        }

        fs->freereg = stklevel; // free registers
        removevars(fs, bl->nactvar); // remove block locals
        Debug.Assert(bl->nactvar == fs->nactvar); // back to level on entry
        if (bl->isloop == 2) // has to fix pending breaks?
        {
            createlabel(ls, ls->brkn, 0, false);
        }

        solvegotos(fs, bl);
        if (bl->previous == null)
        {
            // was it the last block?
            if (bl->firstgoto < ls->dyd->gt.n) // still pending gotos?
            {
                undefgoto(ls, &ls->dyd->gt.arr[bl->firstgoto]); // error
            }
        }

        fs->bl = bl->previous; // current block now is previous one
    }

    /// <summary>
    /// adds a new prototype into list of prototypes
    /// </summary>
    private static Proto* addprototype(LexState* ls)
    {
        lua_State* L = ls->L;
        FuncState* fs = ls->fs;
        Proto* f = fs->f; // prototype of current function
        if (fs->np >= f->sizep)
        {
            int oldsize = f->sizep;
            luaM_growvector2(L, ref f->p, fs->np, ref f->sizep, MAXARG_Bx, "functions");
            while (oldsize < f->sizep)
            {
                f->p[oldsize++] = null;
            }
        }

        Proto* clp = f->p[fs->np++] = luaF_newproto(L);
        luaC_objbarrier(L, (GCObject*)f, (GCObject*)clp);
        return clp;
    }

    /// <summary>
    /// codes instruction to create new closure in parent function.
    /// The OP_CLOSURE instruction uses the last available register,
    /// so that, if it invokes the GC, the GC knows which registers
    /// are in use at that time.
    ///
    /// </summary>
    private static void codeclosure(LexState* ls, expdesc* v)
    {
        FuncState* fs = ls->fs->prev;
        init_exp(v, expkind.VRELOC, luaK_codeABx(fs, OpCode.Closure, 0, fs->np - 1));
        luaK_exp2nextreg(fs, v); // fix it at the last register
    }

    private static void open_func(LexState* ls, FuncState* fs, BlockCnt* bl)
    {
        lua_State* L = ls->L;
        Proto* f = fs->f;
        fs->prev = ls->fs; // linked list of funcstates
        fs->ls = ls;
        ls->fs = fs;
        fs->pc = 0;
        fs->previousline = f->linedefined;
        fs->iwthabs = 0;
        fs->lasttarget = 0;
        fs->freereg = 0;
        fs->nk = 0;
        fs->nabslineinfo = 0;
        fs->np = 0;
        fs->nups = 0;
        fs->ndebugvars = 0;
        fs->nactvar = 0;
        fs->needclose = false;
        fs->firstlocal = ls->dyd->actvar.n;
        fs->firstlabel = ls->dyd->label.n;
        fs->bl = null;
        f->source = ls->source;
        luaC_objbarrier(L, (GCObject*)f, (GCObject*)f->source);
        f->maxstacksize = 2; // registers 0/1 are always valid
        fs->kcache = luaH_new(L); // create table for function
        sethvalue2s(L, L->top.p, fs->kcache); // anchor it
        luaD_inctop(L);
        enterblock(fs, bl, 0);
    }

    private static void close_func(LexState* ls)
    {
        lua_State* L = ls->L;
        FuncState* fs = ls->fs;
        Proto* f = fs->f;
        luaK_ret(fs, luaY_nvarstack(fs), 0); // final return
        leaveblock(fs);
        Debug.Assert(fs->bl == null);
        luaK_finish(fs);
        luaM_shrinkvector(L, ref f->code, ref f->sizecode, fs->pc);
        luaM_shrinkvector(L, ref f->lineinfo, ref f->sizelineinfo, fs->pc);
        luaM_shrinkvector(L, ref f->abslineinfo, ref f->sizeabslineinfo, fs->nabslineinfo);
        luaM_shrinkvector(L, ref f->k, ref f->sizek, fs->nk);
        luaM_shrinkvector(L, ref f->p, ref f->sizep, fs->np);
        luaM_shrinkvector(L, ref f->locvars, ref f->sizelocvars, fs->ndebugvars);
        luaM_shrinkvector(L, ref f->upvalues, ref f->sizeupvalues, fs->nups);
        ls->fs = fs->prev;
        L->top.p--; // pop kcache table
        luaC_checkGC(L);
    }

    // {======================================================================
    // GRAMMAR RULES
    // =======================================================================

    /// <summary>
    /// check whether current token is in the follow set of a block.
    /// 'until' closes syntactical blocks, but do not close scope,
    /// so it is handled in separate.
    /// </summary>
    private static bool block_follow(LexState* ls, bool withuntil)
    {
        return ls->t.token switch
        {
            (int)RESERVED.TK_ELSE or (int)RESERVED.TK_ELSEIF or (int)RESERVED.TK_END or (int)RESERVED.TK_EOS => true,
            (int)RESERVED.TK_UNTIL => withuntil,
            _ => false,
        };
    }

    private static void statlist(LexState* ls)
    {
        // statlist -> { stat [';'] }
        while (!block_follow(ls, true))
        {
            if (ls->t.token == (int)RESERVED.TK_RETURN)
            {
                statement(ls);
                return; // 'return' must be last statement
            }

            statement(ls);
        }
    }

    private static void fieldsel(LexState* ls, expdesc* v)
    {
        // fieldsel -> ['.' | ':'] NAME
        FuncState* fs = ls->fs;
        luaK_exp2anyregup(fs, v);
        luaX_next(ls); // skip the dot or colon
        expdesc key;
        codename(ls, &key);
        luaK_indexed(fs, v, &key);
    }

    private static void yindex(LexState* ls, expdesc* v)
    {
        // index -> '[' expr ']'
        luaX_next(ls); // skip the '['
        expr(ls, v);
        luaK_exp2val(ls->fs, v);
        checknext(ls, ']');
    }

    // {======================================================================
    // Rules for Constructors
    // =======================================================================

    private struct ConsControl
    {
        public expdesc v; // last list item read
        public expdesc* t; // table descriptor
        public int nh; // total number of 'record' elements
        public int na; // number of array elements already stored
        public int tostore; // number of array elements pending to be stored
        public int maxtostore; // maximum number of pending elements
    }

    /// <summary>
    ///
    /// Maximum number of elements in a constructor, to control the following:
    /// * counter overflows;
    /// * overflows in 'extra' for OP_NEWTABLE and OP_SETLIST;
    /// * overflows when adding multiple returns in OP_SETLIST.
    ///
    /// </summary>
    private const int MAX_CNST = int.MaxValue / 2;

    private static void recfield(LexState* ls, ConsControl* cc)
    {
        // recfield -> (NAME | '['exp']') = exp
        FuncState* fs = ls->fs;
        byte reg = ls->fs->freereg;
        expdesc key;
        if (ls->t.token == (int)RESERVED.TK_NAME)
        {
            codename(ls, &key);
        }
        else // ls->t.token == '['
        {
            yindex(ls, &key);
        }

        cc->nh++;
        checknext(ls, '=');
        expdesc tab = *cc->t;
        luaK_indexed(fs, &tab, &key);
        expdesc val;
        expr(ls, &val);
        luaK_storevar(fs, &tab, &val);
        fs->freereg = reg; // free registers
    }

    private static void closelistfield(FuncState* fs, ConsControl* cc)
    {
        Debug.Assert(cc->tostore > 0);
        luaK_exp2nextreg(fs, &cc->v);
        cc->v.k = expkind.VVOID;
        if (cc->tostore >= cc->maxtostore)
        {
            luaK_setlist(fs, cc->t->u.info, cc->na, cc->tostore); // flush
            cc->na += cc->tostore;
            cc->tostore = 0; // no more items pending
        }
    }

    private static void lastlistfield(FuncState* fs, ConsControl* cc)
    {
        if (cc->tostore == 0)
        {
            return;
        }

        if (hasmultret(cc->v.k))
        {
            luaK_setmultret(fs, &cc->v);
            luaK_setlist(fs, cc->t->u.info, cc->na, LUA_MULTRET);
            cc->na--; // do not count last expression (unknown number of elements)
        }
        else
        {
            if (cc->v.k != expkind.VVOID)
            {
                luaK_exp2nextreg(fs, &cc->v);
            }

            luaK_setlist(fs, cc->t->u.info, cc->na, cc->tostore);
        }

        cc->na += cc->tostore;
    }

    private static void listfield(LexState* ls, ConsControl* cc)
    {
        // listfield -> exp
        expr(ls, &cc->v);
        cc->tostore++;
    }

    private static void field(LexState* ls, ConsControl* cc)
    {
        // field -> listfield | recfield
        switch (ls->t.token)
        {
            case (int)RESERVED.TK_NAME:
                // may be 'listfield' or 'recfield'
                if (luaX_lookahead(ls) != '=') // expression?
                {
                    listfield(ls, cc);
                }
                else
                {
                    recfield(ls, cc);
                }

                break;

            case '[':
                recfield(ls, cc);
                break;

            default:
                listfield(ls, cc);
                break;
        }
    }

    /// <summary>
    /// Compute a limit for how many registers a constructor can use before
    /// emitting a 'SETLIST' instruction, based on how many registers are
    /// available.
    /// </summary>
    private static int maxtostore(FuncState* fs)
    {
        int numfreeregs = MAX_FSTACK - fs->freereg;
        if (numfreeregs >= 160) // "lots" of registers?
        {
            return numfreeregs / 5; // use up to 1/5 of them
        }

        if (numfreeregs >= 80) // still "enough" registers?
        {
            return 10; // one 'SETLIST' instruction for each 10 values
        }

        // save registers for potential more nesting
        return 1;
    }

    private static void constructor(LexState* ls, expdesc* t)
    {
        // constructor -> '{' [ field { sep field } [sep] ] '}'
        // sep -> ',' | ';'
        FuncState* fs = ls->fs;
        int line = ls->linenumber;
        int pc = luaK_codevABCk(fs, OpCode.NewTable, 0, 0, 0, false);
        luaK_code(fs, 0); // space for extra arg.
        ConsControl cc;
        cc.na = cc.nh = cc.tostore = 0;
        cc.t = t;
        init_exp(t, expkind.VNONRELOC, fs->freereg); // table will be at stack top
        luaK_reserveregs(fs, 1);
        init_exp(&cc.v, expkind.VVOID, 0); // no value (yet)
        checknext(ls, '{' ); // }
        cc.maxtostore = maxtostore(fs);
        do
        {
            if (ls->t.token ==  '}') // {
            {
                break;
            }

            if (cc.v.k != expkind.VVOID) // is there a previous list item?
            {
                closelistfield(fs, &cc); // close it
            }

            field(ls, &cc);
            luaY_checklimit(fs, cc.tostore + cc.na + cc.nh, MAX_CNST, "items in a constructor");
        } while (testnext(ls, ',') || testnext(ls, ';'));

        check_match(ls,  '}', '{' , line); // { // }
        lastlistfield(fs, &cc);
        luaK_settablesize(fs, pc, t->u.info, cc.na, cc.nh);
    }

    // }======================================================================

    private static void setvararg(FuncState* fs)
    {
        fs->f->flag |= PF_VAHID; // by default, use hidden vararg arguments
        luaK_codeABC(fs, OpCode.VarArgPrep, 0, 0, 0);
    }

    private static void parlist(LexState* ls)
    {
        // parlist -> [ {NAME ','} (NAME | '...') ]
        FuncState* fs = ls->fs;
        Proto* f = fs->f;
        int nparams = 0;
        bool varargk = false;
        if (ls->t.token != ')')
        {
            // is 'parlist' not empty?
            do
            {
                switch (ls->t.token)
                {
                    case (int)RESERVED.TK_NAME:
                        new_localvar(ls, str_checkname(ls));
                        nparams++;
                        break;

                    case (int)RESERVED.TK_DOTS:
                        varargk = true;
                        luaX_next(ls); // skip '...'
                        if (ls->t.token == (int)RESERVED.TK_NAME)
                        {
                            new_varkind(ls, str_checkname(ls), RDKVAVAR);
                        }
                        else
                        {
                            new_localvarliteral(ls, "(vararg table)");
                        }

                        break;

                    default:
                        luaX_syntaxerror(ls, "<name> or '...' expected");
                        break;
                }
            } while (!varargk && testnext(ls, ','));
        }

        adjustlocalvars(ls, nparams);
        f->numparams = (byte)fs->nactvar;
        if (varargk)
        {
            setvararg(fs); // declared vararg
            adjustlocalvars(ls, 1); // vararg parameter
        }

        // reserve registers for parameters (plus vararg parameter, if present)
        luaK_reserveregs(fs, fs->nactvar);
    }

    private static void body(LexState* ls, expdesc* e, bool ismethod, int line)
    {
        // body ->  '(' parlist ')' block END
        FuncState new_fs;
        new_fs.f = addprototype(ls);
        new_fs.f->linedefined = line;
        BlockCnt bl;
        open_func(ls, &new_fs, &bl);
        checknext(ls, '(');
        if (ismethod)
        {
            new_localvarliteral(ls, "self"); // create 'self' parameter
            adjustlocalvars(ls, 1);
        }

        parlist(ls);
        checknext(ls, ')');
        statlist(ls);
        new_fs.f->lastlinedefined = ls->linenumber;
        check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_FUNCTION, line);
        codeclosure(ls, e);
        close_func(ls);
    }

    private static int explist(LexState* ls, expdesc* v)
    {
        // explist -> expr { ',' expr }
        int n = 1; // at least one expression
        expr(ls, v);
        while (testnext(ls, ','))
        {
            luaK_exp2nextreg(ls->fs, v);
            expr(ls, v);
            n++;
        }

        return n;
    }

    private static void funcargs(LexState* ls, expdesc* f)
    {
        FuncState* fs = ls->fs;
        int line = ls->linenumber;

        expdesc args = default;
        switch (ls->t.token)
        {
            case '(':
                // funcargs -> '(' [ explist ] ')'
                luaX_next(ls);
                if (ls->t.token == ')') // arg list is empty?
                {
                    args.k = expkind.VVOID;
                }
                else
                {
                    explist(ls, &args);
                    if (hasmultret(args.k))
                    {
                        luaK_setmultret(fs, &args);
                    }
                }

                check_match(ls, ')', '(', line);
                break;

            case '{' : // }
                // funcargs -> constructor
                constructor(ls, &args);
                break;

            case (int)RESERVED.TK_STRING:
                // funcargs -> STRING
                codestring(&args, ls->t.seminfo.ts);
                luaX_next(ls); // must use 'seminfo' before 'next'
                break;

            default:
                luaX_syntaxerror(ls, "function arguments expected");
                break;
        }

        Debug.Assert(f->k == expkind.VNONRELOC);
        int @base = f->u.info; // base register for call
        int nparams;
        if (hasmultret(args.k))
        {
            nparams = LUA_MULTRET; // open call
        }
        else
        {
            if (args.k != expkind.VVOID)
            {
                luaK_exp2nextreg(fs, &args); // close last argument
            }

            nparams = fs->freereg - (@base + 1);
        }

        init_exp(f, expkind.VCALL, luaK_codeABC(fs, OpCode.Call, @base, nparams + 1, 2));
        luaK_fixline(fs, line);
        // call removes function and arguments and leaves one result (unless
        // changed later)
        fs->freereg = (byte)(@base + 1);
    }

    // {======================================================================
    // Expression parsing
    // =======================================================================

    private static void primaryexp(LexState* ls, expdesc* v)
    {
        // primaryexp -> NAME | '(' expr ')'
        switch (ls->t.token)
        {
            case '(':
                {
                    int line = ls->linenumber;
                    luaX_next(ls);
                    expr(ls, v);
                    check_match(ls, ')', '(', line);
                    luaK_dischargevars(ls->fs, v);
                    return;
                }

            case (int)RESERVED.TK_NAME:
                singlevar(ls, v);
                return;

            default:
                luaX_syntaxerror(ls, "unexpected symbol");
                break;
        }
    }

    private static void suffixedexp(LexState* ls, expdesc* v)
    {
        // suffixedexp ->
        // primaryexp { '.' NAME | '[' exp ']' | ':' NAME funcargs | funcargs }
        FuncState* fs = ls->fs;
        primaryexp(ls, v);
        while (true)
        {
            switch (ls->t.token)
            {
                case '.':
                    // fieldsel
                    fieldsel(ls, v);
                    break;

                case '[':
                    {
                        // '[' exp ']'
                        expdesc key;
                        luaK_exp2anyregup(fs, v);
                        yindex(ls, &key);
                        luaK_indexed(fs, v, &key);
                        break;
                    }

                case ':':
                    {
                        // ':' NAME funcargs
                        expdesc key;
                        luaX_next(ls);
                        codename(ls, &key);
                        luaK_self(fs, v, &key);
                        funcargs(ls, v);
                        break;
                    }

                case '(':
                case (int)RESERVED.TK_STRING:
                case '{' : // }
                    // funcargs
                    luaK_exp2nextreg(fs, v);
                    funcargs(ls, v);
                    break;

                default: return;
            }
        }
    }

    private static void simpleexp(LexState* ls, expdesc* v)
    {
        // simpleexp -> FLT | INT | STRING | NIL | TRUE | FALSE | ... |
        // constructor | FUNCTION body | suffixedexp
        switch (ls->t.token)
        {
            case (int)RESERVED.TK_FLT:
                init_exp(v, expkind.VKFLT, 0);
                v->u.nval = ls->t.seminfo.r;
                break;

            case (int)RESERVED.TK_INT:
                init_exp(v, expkind.VKINT, 0);
                v->u.ival = ls->t.seminfo.i;
                break;

            case (int)RESERVED.TK_STRING:
                codestring(v, ls->t.seminfo.ts);
                break;

            case (int)RESERVED.TK_NIL:
                init_exp(v, expkind.VNIL, 0);
                break;

            case (int)RESERVED.TK_TRUE:
                init_exp(v, expkind.VTRUE, 0);
                break;

            case (int)RESERVED.TK_FALSE:
                init_exp(v, expkind.VFALSE, 0);
                break;

            case (int)RESERVED.TK_DOTS:
                {
                    // vararg
                    FuncState* fs = ls->fs;
                    check_condition(
                        ls,
                        isvararg(fs->f),
                        "cannot use '...' outside a vararg function");
                    init_exp(v, expkind.VVARARG, luaK_codeABC(fs, OpCode.VarArg, 0, fs->f->numparams, 1));
                    break;
                }

            case '{' : // }
                // constructor
                constructor(ls, v);
                return;

            case (int)RESERVED.TK_FUNCTION:
                luaX_next(ls);
                body(ls, v, false, ls->linenumber);
                return;

            default:
                suffixedexp(ls, v);
                return;
        }

        luaX_next(ls);
    }

    private static UnOpr getunopr(int op)
    {
        return op switch
        {
            (int)RESERVED.TK_NOT => UnOpr.NOT,
            '-' => UnOpr.MINUS,
            '~' => UnOpr.BNOT,
            '#' => UnOpr.LEN,
            _ => UnOpr.NOUNOPR,
        };
    }

    private static BinOpr getbinopr(int op)
    {
        return op switch
        {
            '+' => BinOpr.OPR_ADD,
            '-' => BinOpr.OPR_SUB,
            '*' => BinOpr.OPR_MUL,
            '%' => BinOpr.OPR_MOD,
            '^' => BinOpr.OPR_POW,
            '/' => BinOpr.OPR_DIV,
            (int)RESERVED.TK_IDIV => BinOpr.OPR_IDIV,
            '&' => BinOpr.OPR_BAND,
            '|' => BinOpr.OPR_BOR,
            '~' => BinOpr.OPR_BXOR,
            (int)RESERVED.TK_SHL => BinOpr.OPR_SHL,
            (int)RESERVED.TK_SHR => BinOpr.OPR_SHR,
            (int)RESERVED.TK_CONCAT => BinOpr.OPR_CONCAT,
            (int)RESERVED.TK_NE => BinOpr.OPR_NE,
            (int)RESERVED.TK_EQ => BinOpr.OPR_EQ,
            '<' => BinOpr.OPR_LT,
            (int)RESERVED.TK_LE => BinOpr.OPR_LE,
            '>' => BinOpr.OPR_GT,
            (int)RESERVED.TK_GE => BinOpr.OPR_GE,
            (int)RESERVED.TK_AND => BinOpr.OPR_AND,
            (int)RESERVED.TK_OR => BinOpr.OPR_OR,
            _ => BinOpr.OPR_NOBINOPR,
        };
    }

    private struct Priority(byte left, byte right)
    {
        public byte left = left; // left priority for each binary operator
        public byte right = right; // right priority
    }

    /// <summary>
    /// Priority table for binary operators.
    /// </summary>
    private static readonly Priority[] priority =
    [
        // ORDER OPR
        new(10, 10), new(10, 10), // '+' '-'
        new(11, 11), new(11, 11), // '*' '%'
        new(14, 13), // '^' (right associative)
        new(11, 11), new(11, 11), // '/' '//'
        new(6, 6), new(4, 4), new(5, 5), // '&' '|' '~'
        new(7, 7), new(7, 7), // '<<' '>>'
        new(9, 8), // '..' (right associative)
        new(3, 3), new(3, 3), new(3, 3), // ==, <, <=
        new(3, 3), new(3, 3), new(3, 3), // ~=, >, >=
        new(2, 2), new(1, 1) // and, or
    ];

    private const int UNARY_PRIORITY = 12; // priority for unary operators

    /// <summary>
    /// subexpr -&gt; (simpleexp | unop subexpr) { binop subexpr }
    /// where 'binop' is any binary operator with a priority higher than 'limit'
    /// </summary>
    private static BinOpr subexpr(LexState* ls, expdesc* v, int limit)
    {
        enterlevel(ls);
        UnOpr uop = getunopr(ls->t.token);
        if (uop != UnOpr.NOUNOPR)
        {
            // prefix (unary) operator?
            int line = ls->linenumber;
            luaX_next(ls); // skip operator
            subexpr(ls, v, UNARY_PRIORITY);
            luaK_prefix(ls->fs, uop, v, line);
        }
        else
        {
            simpleexp(ls, v);
        }

        // expand while operators have priorities higher than 'limit'
        BinOpr op = getbinopr(ls->t.token);
        while (op != BinOpr.OPR_NOBINOPR && priority[(int)op].left > limit)
        {
            int line = ls->linenumber;
            luaX_next(ls); // skip operator
            luaK_infix(ls->fs, op, v);
            // read sub-expression with higher priority
            expdesc v2;
            BinOpr nextop = subexpr(ls, &v2, priority[(int)op].right);
            luaK_posfix(ls->fs, op, v, &v2, line);
            op = nextop;
        }

        leavelevel(ls);
        return op; // return first untreated operator
    }

    private static void expr(LexState* ls, expdesc* v)
    {
        subexpr(ls, v, 0);
    }

    // }====================================================================

    // {======================================================================
    // Rules for Statements
    // =======================================================================

    private static void block(LexState* ls)
    {
        // block -> statlist
        FuncState* fs = ls->fs;
        BlockCnt bl;
        enterblock(fs, &bl, 0);
        statlist(ls);
        leaveblock(fs);
    }

    /// <summary>
    /// structure to chain all variables in the left-hand side of an
    /// assignment
    /// </summary>
    private struct LHS_assign
    {
        public LHS_assign* prev;
        public expdesc v; // variable (global, local, upvalue, or indexed)
    }

    /// <summary>
    /// check whether, in an assignment to an upvalue/local variable, the
    /// upvalue/local variable is begin used in a previous assignment to a
    /// table. If so, save original upvalue/local value in a safe place and
    /// use this safe copy in the previous assignment.
    /// </summary>
    private static void check_conflict(LexState* ls, LHS_assign* lh, expdesc* v)
    {
        FuncState* fs = ls->fs;
        byte extra = fs->freereg; // eventual position to save local variable
        bool conflict = false;
        for (; lh != null; lh = lh->prev)
        {
            // check all previous assignments
            if (vkisindexed(lh->v.k))
            {
                // assignment to table field?
                if (lh->v.k == expkind.VINDEXUP)
                {
                    // is table an upvalue?
                    if (v->k == expkind.VUPVAL && lh->v.u.ind.t == v->u.info)
                    {
                        conflict = true; // table is the upvalue being assigned now
                        lh->v.k = expkind.VINDEXSTR;
                        lh->v.u.ind.t = extra; // assignment will use safe copy
                    }
                }
                else
                {
                    // table is a register
                    if (v->k == expkind.VLOCAL && lh->v.u.ind.t == v->u.var.ridx)
                    {
                        conflict = true; // table is the local being assigned now
                        lh->v.u.ind.t = extra; // assignment will use safe copy
                    }

                    // is index the local being assigned?
                    if (lh->v.k == expkind.VINDEXED &&
                        v->k == expkind.VLOCAL &&
                        lh->v.u.ind.idx == v->u.var.ridx)
                    {
                        conflict = true;
                        lh->v.u.ind.idx = extra; // previous assignment will use safe copy
                    }
                }
            }
        }

        if (conflict)
        {
            // copy upvalue/local value to a temporary (in position 'extra')
            if (v->k == expkind.VLOCAL)
            {
                luaK_codeABC(fs, OpCode.Move, extra, v->u.var.ridx, 0);
            }
            else
            {
                luaK_codeABC(fs, OpCode.GetUpVal, extra, v->u.info, 0);
            }

            luaK_reserveregs(fs, 1);
        }
    }

    /// <summary>
    /// Create code to store the "top" register in 'var'
    /// </summary>
    private static void storevartop(FuncState* fs, expdesc* var)
    {
        expdesc e;
        init_exp(&e, expkind.VNONRELOC, fs->freereg - 1);
        luaK_storevar(fs, var, &e); // will also free the top register
    }

    /// <summary>
    /// Parse and compile a multiple assignment. The first "variable"
    /// (a 'suffixedexp') was already read by the caller.
    ///
    /// assignment -&gt; suffixedexp restassign
    /// restassign -&gt; ',' suffixedexp restassign | '=' explist
    /// </summary>
    private static void restassign(LexState* ls, LHS_assign* lh, int nvars)
    {
        check_condition(ls, vkisvar(lh->v.k), "syntax error");
        check_readonly(ls, &lh->v);
        if (testnext(ls, ','))
        {
            // restassign -> ',' suffixedexp restassign
            LHS_assign nv;
            nv.prev = lh;
            suffixedexp(ls, &nv.v);
            if (!vkisindexed(nv.v.k))
            {
                check_conflict(ls, lh, &nv.v);
            }

            enterlevel(ls); // control recursion depth
            restassign(ls, &nv, nvars + 1);
            leavelevel(ls);
        }
        else
        {
            // restassign -> '=' explist
            checknext(ls, '=');
            expdesc e;
            int nexps = explist(ls, &e);
            if (nexps != nvars)
            {
                adjust_assign(ls, nvars, nexps, &e);
            }
            else
            {
                luaK_setoneret(ls->fs, &e); // close last expression
                luaK_storevar(ls->fs, &lh->v, &e);
                return; // avoid default
            }
        }

        storevartop(ls->fs, &lh->v); // default assignment
    }

    private static int cond(LexState* ls)
    {
        // cond -> exp
        expdesc v;
        expr(ls, &v); // read condition
        if (v.k == expkind.VNIL)
        {
            v.k = expkind.VFALSE; // 'falses' are all equal here
        }

        luaK_goiftrue(ls->fs, &v);
        return v.f;
    }

    private static void gotostat(LexState* ls, int line)
    {
        TString* name = str_checkname(ls); // label's name
        newgotoentry(ls, name, line);
    }

    /// <summary>
    /// Break statement. Semantically equivalent to "goto break".
    /// </summary>
    private static void breakstat(LexState* ls, int line)
    {
        BlockCnt* bl; // to look for an enclosing loop
        for (bl = ls->fs->bl; bl != null; bl = bl->previous)
        {
            if (bl->isloop != 0) // found one?
            {
                goto ok;
            }
        }

        luaX_syntaxerror(ls, "break outside loop");
        ok:
        bl->isloop = 2; // signal that block has pending breaks
        luaX_next(ls); // skip break
        newgotoentry(ls, ls->brkn, line);
    }

    /// <summary>
    /// Check whether there is already a label with the given 'name' at
    /// current function.
    /// </summary>
    private static void checkrepeated(LexState* ls, TString* name)
    {
        Labeldesc* lb = findlabel(ls, name, ls->fs->firstlabel);
        if (lb != null) // already defined?
        {
            luaK_semerror(
                ls,
                "label '%s' already defined on line %d",
                getnetstr(name),
                lb->line); // error
        }
    }

    private static void labelstat(LexState* ls, TString* name, int line)
    {
        // label -> '::' NAME '::'
        checknext(ls, (int)RESERVED.TK_DBCOLON); // skip double colon
        while (ls->t.token == ';' || ls->t.token == (int)RESERVED.TK_DBCOLON)
        {
            statement(ls); // skip other no-op statements
        }

        checkrepeated(ls, name); // check for repeated labels
        createlabel(ls, name, line, block_follow(ls, false));
    }

    private static void whilestat(LexState* ls, int line)
    {
        // whilestat -> WHILE cond DO block END
        FuncState* fs = ls->fs;
        luaX_next(ls); // skip WHILE
        int whileinit = luaK_getlabel(fs);
        int condexit = cond(ls);
        BlockCnt bl;
        enterblock(fs, &bl, 1);
        checknext(ls, (int)RESERVED.TK_DO);
        block(ls);
        luaK_jumpto(fs, whileinit);
        check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_WHILE, line);
        leaveblock(fs);
        luaK_patchtohere(fs, condexit); // false conditions finish the loop
    }

    private static void repeatstat(LexState* ls, int line)
    {
        // repeatstat -> REPEAT block UNTIL cond
        FuncState* fs = ls->fs;
        int repeatInit = luaK_getlabel(fs);
        BlockCnt bl1, bl2;
        enterblock(fs, &bl1, 1); // loop block
        enterblock(fs, &bl2, 0); // scope block
        luaX_next(ls); // skip REPEAT
        statlist(ls);
        check_match(ls, (int)RESERVED.TK_UNTIL, (int)RESERVED.TK_REPEAT, line);
        int condexit = cond(ls); // read condition (inside scope block)
        leaveblock(fs); // finish scope
        if (bl2.upval)
        {
            // upvalues?
            int exit = luaK_jump(fs); // normal exit must jump over fix
            luaK_patchtohere(fs, condexit); // repetition must close upvalues
            luaK_codeABC(fs, OpCode.Close, reglevel(fs, bl2.nactvar), 0, 0);
            condexit = luaK_jump(fs); // repeat after closing upvalues
            luaK_patchtohere(fs, exit); // normal exit comes to here
        }

        luaK_patchlist(fs, condexit, repeatInit); // close the loop
        leaveblock(fs); // finish loop
    }

    /// <summary>
    /// Read an expression and generate code to put its results in next
    /// stack slot.
    ///
    /// </summary>
    private static void exp1(LexState* ls)
    {
        expdesc e;
        expr(ls, &e);
        luaK_exp2nextreg(ls->fs, &e);
        Debug.Assert(e.k == expkind.VNONRELOC);
    }

    /// <summary>
    /// Fix for instruction at position 'pc' to jump to 'dest'.
    /// (Jump addresses are relative in Lua). 'back' true means
    /// a back jump.
    /// </summary>
    private static void fixforjump(FuncState* fs, int pc, int dest, bool back)
    {
        uint* jmp = &fs->f->code[pc];
        int offset = dest - (pc + 1);
        if (back)
        {
            offset = -offset;
        }

        if (offset > MAXARG_Bx)
        {
            luaX_syntaxerror(fs->ls, "control structure too long");
        }

        SETARG_Bx(ref *jmp, offset);
    }

    /// <summary>
    /// Generate code for a 'for' loop.
    /// </summary>
    private static void forbody(LexState* ls, int @base, int line, int nvars, bool isgen)
    {
        // forbody -> DO block
        BlockCnt bl;
        FuncState* fs = ls->fs;
        checknext(ls, (int)RESERVED.TK_DO);
        int prep = luaK_codeABx(fs, isgen ? OpCode.TForPrep : OpCode.ForPrep, @base, 0);
        fs->freereg--; // both 'forprep' remove one register from the stack
        enterblock(fs, &bl, 0); // scope for declared variables
        adjustlocalvars(ls, nvars);
        luaK_reserveregs(fs, nvars);
        block(ls);
        leaveblock(fs); // end of scope for declared variables
        fixforjump(fs, prep, luaK_getlabel(fs), false);
        if (isgen)
        {
            // generic for?
            luaK_codeABC(fs, OpCode.TForCall, @base, 0, nvars);
            luaK_fixline(fs, line);
        }

        int endfor = luaK_codeABx(fs, isgen ? OpCode.TForLoop : OpCode.ForLoop, @base, 0);
        fixforjump(fs, endfor, prep + 1, true);
        luaK_fixline(fs, line);
    }

    private static void fornum(LexState* ls, TString* varname, int line)
    {
        // fornum -> NAME = exp,exp[,exp] forbody
        FuncState* fs = ls->fs;
        int @base = fs->freereg;
        new_localvarliteral(ls, "(for state)");
        new_localvarliteral(ls, "(for state)");
        new_varkind(ls, varname, RDKCONST); // control variable
        checknext(ls, '=');
        exp1(ls); // initial value
        checknext(ls, ',');
        exp1(ls); // limit
        if (testnext(ls, ','))
        {
            exp1(ls); // optional step
        }
        else
        {
            // default step = 1
            luaK_int(fs, fs->freereg, 1);
            luaK_reserveregs(fs, 1);
        }

        adjustlocalvars(ls, 2); // start scope for internal variables
        forbody(ls, @base, line, 1, false);
    }

    private static void forlist(LexState* ls, TString* indexname)
    {
        // forlist -> NAME {,NAME} IN explist forbody
        FuncState* fs = ls->fs;
        expdesc e;
        int nvars = 4; // function, state, closing, control
        int @base = fs->freereg;
        // create internal variables
        new_localvarliteral(ls, "(for state)"); // iterator function
        new_localvarliteral(ls, "(for state)"); // state
        new_localvarliteral(ls, "(for state)"); // closing var. (after swap)
        new_varkind(ls, indexname, RDKCONST); // control variable
        // other declared variables
        while (testnext(ls, ','))
        {
            new_localvar(ls, str_checkname(ls));
            nvars++;
        }

        checknext(ls, (int)RESERVED.TK_IN);
        int line = ls->linenumber;
        adjust_assign(ls, 4, explist(ls, &e), &e);
        adjustlocalvars(ls, 3); // start scope for internal variables
        marktobeclosed(fs); // last internal var. must be closed
        luaK_checkstack(fs, 2); // extra space to call iterator
        forbody(ls, @base, line, nvars - 3, true);
    }

    private static void forstat(LexState* ls, int line)
    {
        // forstat -> FOR (fornum | forlist) END
        FuncState* fs = ls->fs;
        BlockCnt bl;
        enterblock(fs, &bl, 1); // scope for loop and control variables
        luaX_next(ls); // skip 'for'
        TString* varname = str_checkname(ls) ; // first variable name
        switch (ls->t.token)
        {
            case '=':
                fornum(ls, varname, line);
                break;

            case ',':
            case (int)RESERVED.TK_IN:
                forlist(ls, varname);
                break;

            default:
                luaX_syntaxerror(ls, "'=' or 'in' expected");
                break;
        }

        check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_FOR, line);
        leaveblock(fs); // loop scope ('break' jumps to this point)
    }

    private static void test_then_block(LexState* ls, int* escapelist)
    {
        // test_then_block -> [IF | ELSEIF] cond THEN block
        FuncState* fs = ls->fs;
        luaX_next(ls); // skip IF or ELSEIF
        int condtrue = cond(ls) ; // read condition
        checknext(ls, (int)RESERVED.TK_THEN);
        block(ls); // 'then' part
        if (ls->t.token == (int)RESERVED.TK_ELSE ||
            ls->t.token == (int)RESERVED.TK_ELSEIF) // followed by 'else'/'elseif'?
        {
            luaK_concat(fs, escapelist, luaK_jump(fs)); // must jump over it
        }

        luaK_patchtohere(fs, condtrue);
    }

    private static void ifstat(LexState* ls, int line)
    {
        // ifstat -> IF cond THEN block {ELSEIF cond THEN block} [ELSE block] END
        FuncState* fs = ls->fs;
        int escapelist = NO_JUMP; // exit list for finished parts
        test_then_block(ls, &escapelist); // IF cond THEN block
        while (ls->t.token == (int)RESERVED.TK_ELSEIF)
        {
            test_then_block(ls, &escapelist); // ELSEIF cond THEN block
        }

        if (testnext(ls, (int)RESERVED.TK_ELSE))
        {
            block(ls); // 'else' part
        }

        check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_IF, line);
        luaK_patchtohere(fs, escapelist); // patch escape list to 'if' end
    }

    private static void localfunc(LexState* ls)
    {
        FuncState* fs = ls->fs;
        int fvar = fs->nactvar; // function's variable index
        new_localvar(ls, str_checkname(ls)); // new local variable
        adjustlocalvars(ls, 1); // enter its scope

        expdesc b;
        body(ls, &b, false, ls->linenumber); // function created in next register
        // debug information will only see the variable after this point!
        localdebuginfo(fs, fvar)->startpc = fs->pc;
    }

    private static byte getvarattribute(LexState* ls, byte df)
    {
        // attrib -> ['<' NAME '>']
        if (testnext(ls, '<'))
        {
            TString* ts = str_checkname(ls);
            string attr = getnetstr(ts);
            checknext(ls, '>');
            if (string.Equals(attr, "const", StringComparison.Ordinal))
            {
                return RDKCONST; // read-only variable
            }

            if (string.Equals(attr, "close", StringComparison.Ordinal))
            {
                return RDKTOCLOSE; // to-be-closed variable
            }

            luaK_semerror(ls, "unknown attribute '%s'", attr);
        }

        return df; // return default value
    }

    private static void checktoclose(FuncState* fs, int level)
    {
        if (level != -1)
        {
            // is there a to-be-closed variable?
            marktobeclosed(fs);
            luaK_codeABC(fs, OpCode.TBC, reglevel(fs, level), 0, 0);
        }
    }

    private static void localstat(LexState* ls)
    {
        // stat -> LOCAL NAME attrib { ',' NAME attrib } ['=' explist]
        FuncState* fs = ls->fs;
        int toclose = -1; // index of to-be-closed variable (if any)

        int nvars = 0;
        int vidx; // index of last variable

        // get prefixed attribute (if any); default is regular local variable
        byte defkind = getvarattribute(ls, VDKREG);
        do
        {
            // for each variable
            TString* vname = str_checkname(ls); // get its name
            byte kind = getvarattribute(ls, defkind); // postfixed attribute
            vidx = new_varkind(ls, vname, kind); // predeclare it
            if (kind == RDKTOCLOSE)
            {
                // to-be-closed?
                if (toclose != -1) // one already present?
                {
                    luaK_semerror(ls, "multiple to-be-closed variables in local list");
                }

                toclose = fs->nactvar + nvars;
            }

            nvars++;
        } while (testnext(ls, ','));

        int nexps;
        expdesc e;
        if (testnext(ls, '=')) // initialisation?
        {
            nexps = explist(ls, &e);
        }
        else
        {
            e.k = expkind.VVOID;
            nexps = 0;
        }

        Vardesc* var = getlocalvardesc(fs, vidx); // retrieve last variable
        if (nvars == nexps && // no adjustments?
            var->vd.kind == RDKCONST && // last variable is const?
            luaK_exp2const(fs, &e, &var->k))
        {
            // compile-time constant?
            var->vd.kind = RDKCTC; // variable is a compile-time constant
            adjustlocalvars(ls, nvars - 1); // exclude last variable
            fs->nactvar++; // but count it
        }
        else
        {
            adjust_assign(ls, nvars, nexps, &e);
            adjustlocalvars(ls, nvars);
        }

        checktoclose(fs, toclose);
    }

    private static byte getglobalattribute(LexState* ls, byte df)
    {
        byte kind = getvarattribute(ls, df);
        switch (kind)
        {
            case RDKTOCLOSE:
                luaK_semerror(ls, "global variables cannot be to-be-closed");
                return kind; // to avoid warnings

            case RDKCONST:
                return GDKCONST; // adjust kind for global variable

            default:
                return kind;
        }
    }

    private static void checkglobal(LexState* ls, TString* varname, int line)
    {
        FuncState* fs = ls->fs;
        expdesc var;
        buildglobal(ls, varname, &var); // create global variable in 'var'
        int k = var.u.ind.keystr ; // index of global name in 'k'
        luaK_codecheckglobal(fs, &var, k, line);
    }

    /// <summary>
    /// Recursively traverse list of globals to be initalized. When
    /// going, generate table description for the global. In the end,
    /// after all indices have been generated, read list of initialising
    /// expressions. When returning, generate the assignment of the value on
    /// the stack to the corresponding table description. 'n' is the variable
    /// being handled, range [0, nvars - 1].
    /// </summary>
    private static void initglobal(
        LexState* ls,
        int nvars,
        int firstidx,
        int n,
        int line)
    {
        if (n == nvars)
        {
            // traversed all variables?
            expdesc e;
            int nexps = explist(ls, &e); // read list of expressions
            adjust_assign(ls, nvars, nexps, &e);
        }
        else
        {
            // handle variable 'n'
            FuncState* fs = ls->fs;
            expdesc var;
            TString* varname = getlocalvardesc(fs, firstidx + n)->vd.name;
            buildglobal(ls, varname, &var); // create global variable in 'var'
            enterlevel(ls); // control recursion depth
            initglobal(ls, nvars, firstidx, n + 1, line);
            leavelevel(ls);
            checkglobal(ls, varname, line);
            storevartop(fs, &var);
        }
    }

    private static void globalnames(LexState* ls, byte defkind)
    {
        FuncState* fs = ls->fs;
        int nvars = 0;
        int lastidx; // index of last registered variable
        do
        {
            // for each name
            TString* vname = str_checkname(ls);
            byte kind = getglobalattribute(ls, defkind);
            lastidx = new_varkind(ls, vname, kind);
            nvars++;
        } while (testnext(ls, ','));

        if (testnext(ls, '=')) // initialisation?
        {
            initglobal(ls, nvars, lastidx - nvars + 1, 0, ls->linenumber);
        }

        fs->nactvar = (short)(fs->nactvar + nvars); // activate declaration
    }

    private static void globalstat(LexState* ls)
    {
        // globalstat -> (GLOBAL) attrib '*'
        // globalstat -> (GLOBAL) attrib NAME attrib {',' NAME attrib}
        FuncState* fs = ls->fs;
        // get prefixed attribute (if any); default is regular global variable
        byte defkind = getglobalattribute(ls, GDKREG);
        if (!testnext(ls, '*'))
        {
            globalnames(ls, defkind);
        }
        else
        {
            // use null as name to represent '*' entries
            new_varkind(ls, null, defkind);
            fs->nactvar++; // activate declaration
        }
    }

    private static void globalfunc(LexState* ls, int line)
    {
        // globalfunc -> (GLOBAL FUNCTION) NAME body
        expdesc var, b;
        FuncState* fs = ls->fs;
        TString* fname = str_checkname(ls);
        new_varkind(ls, fname, GDKREG); // declare global variable
        fs->nactvar++; // enter its scope
        buildglobal(ls, fname, &var);
        body(ls, &b, false, ls->linenumber); // compile and return closure in 'b'
        checkglobal(ls, fname, line);
        luaK_storevar(fs, &var, &b);
        luaK_fixline(fs, line); // definition "happens" in the first line
    }

    private static void globalstatfunc(LexState* ls, int line)
    {
        // stat -> GLOBAL globalfunc | GLOBAL globalstat
        luaX_next(ls); // skip 'global'
        if (testnext(ls, (int)RESERVED.TK_FUNCTION))
        {
            globalfunc(ls, line);
        }
        else
        {
            globalstat(ls);
        }
    }

    private static bool funcname(LexState* ls, expdesc* v)
    {
        // funcname -> NAME {fieldsel} [':' NAME]
        bool ismethod = false;
        singlevar(ls, v);
        while (ls->t.token == '.')
        {
            fieldsel(ls, v);
        }

        if (ls->t.token == ':')
        {
            ismethod = true;
            fieldsel(ls, v);
        }

        return ismethod;
    }

    private static void funcstat(LexState* ls, int line)
    {
        // funcstat -> FUNCTION funcname body
        expdesc v, b;
        luaX_next(ls); // skip FUNCTION
        bool ismethod = funcname(ls, &v);
        check_readonly(ls, &v);
        body(ls, &b, ismethod, line);
        luaK_storevar(ls->fs, &v, &b);
        luaK_fixline(ls->fs, line); // definition "happens" in the first line
    }

    private static void exprstat(LexState* ls)
    {
        // stat -> func | assignment
        FuncState* fs = ls->fs;
        LHS_assign v;
        suffixedexp(ls, &v.v);
        if (ls->t.token == '=' || ls->t.token == ',')
        {
            // stat -> assignment ?
            v.prev = null;
            restassign(ls, &v, 1);
        }
        else
        {
            // stat -> func
            check_condition(ls, v.v.k == expkind.VCALL, "syntax error");
            ref uint inst = ref getinstruction(fs, &v.v);
            SETARG_C(ref inst, 1); // call statement uses no results
        }
    }

    private static void retstat(LexState* ls)
    {
        // stat -> RETURN [explist] [';']
        FuncState* fs = ls->fs;
        int nret; // number of values being returned
        int first = luaY_nvarstack(fs); // first slot to be returned
        if (block_follow(ls, true) || ls->t.token == ';')
        {
            nret = 0; // return no values
        }
        else
        {
            expdesc e;
            nret = explist(ls, &e); // optional return values
            if (hasmultret(e.k))
            {
                luaK_setmultret(fs, &e);
                if (e.k == expkind.VCALL && nret == 1 && !fs->bl->insidetbc)
                {
                    // tail call?
                    SET_OPCODE(ref getinstruction(fs, &e), OpCode.TailCall);
                    Debug.Assert(GETARG_A(getinstruction(fs, &e)) == luaY_nvarstack(fs));
                }

                nret = LUA_MULTRET; // return all values
            }
            else
            {
                if (nret == 1) // only one single value?
                {
                    first = luaK_exp2anyreg(fs, &e); // can use original slot
                }
                else
                {
                    // values must go to the top of the stack
                    luaK_exp2nextreg(fs, &e);
                    Debug.Assert(nret == fs->freereg - first);
                }
            }
        }

        luaK_ret(fs, first, nret);
        testnext(ls, ';'); // skip optional semicolon
    }

    /// <summary>
    /// prototypes for recursive non-terminal functions
    /// </summary>
    private static void statement(LexState* ls)
    {
        int line = ls->linenumber; // may be needed for error messages
        enterlevel(ls);
        switch (ls->t.token)
        {
            case ';':
                // stat -> ';' (empty statement)
                luaX_next(ls); // skip ';'
                break;

            case (int)RESERVED.TK_IF:
                // stat -> ifstat
                ifstat(ls, line);
                break;

            case (int)RESERVED.TK_WHILE:
                // stat -> whilestat
                whilestat(ls, line);
                break;

            case (int)RESERVED.TK_DO:
                // stat -> DO block END
                luaX_next(ls); // skip DO
                block(ls);
                check_match(ls, (int)RESERVED.TK_END, (int)RESERVED.TK_DO, line);
                break;

            case (int)RESERVED.TK_FOR:
                // stat -> forstat
                forstat(ls, line);
                break;

            case (int)RESERVED.TK_REPEAT:
                // stat -> repeatstat
                repeatstat(ls, line);
                break;

            case (int)RESERVED.TK_FUNCTION:
                // stat -> funcstat
                funcstat(ls, line);
                break;

            case (int)RESERVED.TK_LOCAL:
                // stat -> localstat
                luaX_next(ls); // skip LOCAL
                if (testnext(ls, (int)RESERVED.TK_FUNCTION)) // local function?
                {
                    localfunc(ls);
                }
                else
                {
                    localstat(ls);
                }

                break;

            case (int)RESERVED.TK_GLOBAL:
                // stat -> globalstatfunc
                globalstatfunc(ls, line);
                break;

            case (int)RESERVED.TK_DBCOLON:
                // stat -> label
                luaX_next(ls); // skip double colon
                labelstat(ls, str_checkname(ls), line);
                break;

            case (int)RESERVED.TK_RETURN:
                // stat -> retstat
                luaX_next(ls); // skip RETURN
                retstat(ls);
                break;

            case (int)RESERVED.TK_BREAK:
                // stat -> breakstat
                breakstat(ls, line);
                break;

            case (int)RESERVED.TK_GOTO:
                // stat -> 'goto' NAME
                luaX_next(ls); // skip 'goto'
                gotostat(ls, line);
                break;

#if LUA_COMPAT_GLOBAL
            case (int)RESERVED.TK_NAME:
                // compatibility code to parse global keyword when "global"
                // is not reserved
                if (ls->t.seminfo.ts == ls->glbn)
                {
                    // current = "global"?
                    int lk = luaX_lookahead(ls);
                    if (lk == '<' || lk == (int)RESERVED.TK_NAME || lk == '*' || lk == (int)RESERVED.TK_FUNCTION)
                    {
                        // 'global <attrib>' or 'global name' or 'global *' or
                        // 'global function'
                        globalstatfunc(ls, line);
                        break;
                    }
                } // else...
#endif
                goto default;

            default:
                // stat -> func | assignment
                exprstat(ls);
                break;
        }

        Debug.Assert(
            ls->fs->f->maxstacksize >= ls->fs->freereg &&
            ls->fs->freereg >= luaY_nvarstack(ls->fs));
        ls->fs->freereg = luaY_nvarstack(ls->fs); // free registers
        leavelevel(ls);
    }

    // }======================================================================

    // }======================================================================

    /// <summary>
    /// compiles the main function, which is a regular vararg function with an
    /// upvalue named LUA_ENV
    /// </summary>
    private static void mainfunc(LexState* ls, FuncState* fs)
    {
        BlockCnt bl;
        open_func(ls, fs, &bl);
        setvararg(fs); // main function is always vararg
        Upvaldesc* env = allocupvalue(fs); // ...set environment upvalue
        env->instack = 1;
        env->idx = 0;
        env->kind = VDKREG;
        env->name = ls->envn;
        luaC_objbarrier(ls->L, (GCObject*)fs->f, (GCObject*)env->name);
        luaX_next(ls); // read first token
        statlist(ls); // parse main body
        check(ls, (int)RESERVED.TK_EOS);
        close_func(ls);
    }

    private static LClosure* luaY_parser(
        lua_State* L,
        Zio* z,
        Mbuffer* buff,
        Dyndata* dyd,
        string name,
        int firstchar)
    {
        LClosure* cl = luaF_newLclosure(L, 1); // create main closure
        setclLvalue2s(L, L->top.p, cl); // anchor it (to avoid being collected)
        luaD_inctop(L);

        FuncState funcstate;
        LexState lexstate;
        lexstate.h = luaH_new(L); // create table for scanner
        sethvalue2s(L, L->top.p, lexstate.h); // anchor it
        luaD_inctop(L);
        funcstate.f = cl->p = luaF_newproto(L);
        luaC_objbarrier(L, (GCObject*)cl, (GCObject*)cl->p);
        funcstate.f->source = luaS_new(L, name); // create and anchor TString
        luaC_objbarrier(L, (GCObject*)funcstate.f, (GCObject*)funcstate.f->source);
        lexstate.buff = buff;
        lexstate.dyd = dyd;
        dyd->actvar.n = dyd->gt.n = dyd->label.n = 0;
        luaX_setinput(L, &lexstate, z, funcstate.f->source, firstchar);
        mainfunc(&lexstate, &funcstate);
        Debug.Assert(funcstate.prev == null && funcstate.nups == 1 && lexstate.fs == null);
        // all scopes should be correctly finished
        Debug.Assert(dyd->actvar.n == 0 && dyd->gt.n == 0 && dyd->label.n == 0);
        L->top.p--; // remove scanner's table
        return cl; // closure is on the stack, too
    }
}
