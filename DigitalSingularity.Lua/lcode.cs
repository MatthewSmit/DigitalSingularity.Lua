namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

public static unsafe partial class Lua
{
    // $Id: lcode.c $
    // Code generator for Lua
    // See Copyright Notice in lua.h

    /// <summary>
    /// Marks the end of a patch list. It is an invalid value both as an absolute
    /// address, and as a list link (would link an element to itself).
    /// </summary>
    private const int NO_JUMP = -1;

    /// <summary>
    /// grep "ORDER OPR" if you change these enums  (ORDER OP)
    /// </summary>
    private enum BinOpr
    {
        // arithmetic operators
        OPR_ADD, OPR_SUB, OPR_MUL, OPR_MOD, OPR_POW,
        OPR_DIV, OPR_IDIV,

        // bitwise operators
        OPR_BAND, OPR_BOR, OPR_BXOR,
        OPR_SHL, OPR_SHR,

        // string operator
        OPR_CONCAT,

        // comparison operators
        OPR_EQ, OPR_LT, OPR_LE,
        OPR_NE, OPR_GT, OPR_GE,

        // logical operators
        OPR_AND, OPR_OR,
        OPR_NOBINOPR,
    }

    /// <summary>
    /// true if operation is foldable (that is, it is arithmetic or bitwise)
    /// </summary>
    private static bool foldbinop(BinOpr op)
    {
        return op <= BinOpr.OPR_SHR;
    }

    private static int luaK_codeABC(FuncState* fs, OpCode o, int a, int b, int c)
    {
        return luaK_codeABCk(fs, o, a, b, c, false);
    }

    private enum UnOpr
    {
        MINUS, 
        BNOT,
        NOT,
        LEN,
        NOUNOPR,
    }

    /// <summary>
    /// get (pointer to) instruction of given 'expdesc'
    /// </summary>
    private static ref uint getinstruction(FuncState* fs, expdesc* e)
    {
        return ref fs->f->code[e->u.info];
    }

    private static void luaK_setmultret(FuncState* fs, expdesc* e)
    {
        luaK_setreturns(fs, e, LUA_MULTRET);
    }

    private static void luaK_jumpto(FuncState* fs, int t)
    {
        luaK_patchlist(fs, luaK_jump(fs), t);
    }
    
    /// <summary>
    /// (note that expressions VJMP also have jumps.)
    /// </summary>
    private static bool hasjumps(expdesc* e)
    {
        return e->t != e->f;
    }

    /// <summary>
    /// semantic error
    /// </summary>
    [DoesNotReturn]
    private static void luaK_semerror(LexState* ls, string fmt, params object[] args)
    {
        pushvfstring(ls->L, args, fmt, out string msg);
        ls->t.token = 0; // remove "near <token>" from final message
        ls->linenumber = ls->lastline; // back to line of last used token
        luaX_syntaxerror(ls, msg);
    }

    /// <summary>
    /// If expression is a numeric constant, fills 'v' with its value
    /// and returns 1. Otherwise, returns 0.
    /// </summary>
    private static bool tonumeral(expdesc* e, TValue* v)
    {
        if (hasjumps(e))
        {
            return false; // not a numeral
        }

        switch (e->k)
        {
            case expkind.VKINT:
                if (v != null)
                {
                    setivalue(v, e->u.ival);
                }

                return true;

            case expkind.VKFLT:
                if (v != null)
                {
                    setfltvalue(v, e->u.nval);
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Get the constant value from a constant expression
    /// </summary>
    private static TValue* const2val(FuncState* fs, expdesc* e)
    {
        Debug.Assert(e->k == expkind.VCONST);
        return &fs->ls->dyd->actvar.arr[e->u.info].k;
    }

    /// <summary>
    /// If expression is a constant, fills 'v' with its value
    /// and returns 1. Otherwise, returns 0.
    /// </summary>
    private static bool luaK_exp2const(FuncState* fs, expdesc* e, TValue* v)
    {
        if (hasjumps(e))
        {
            return false; // not a constant
        }

        switch (e->k)
        {
            case expkind.VFALSE:
                setbfvalue(v);
                return true;

            case expkind.VTRUE:
                setbtvalue(v);
                return true;

            case expkind.VNIL:
                setnilvalue(v);
                return true;

            case expkind.VKSTR:
                setsvalue(fs->ls->L, v, e->u.strval);
                return true;

            case expkind.VCONST:
                setobj(fs->ls->L, v, const2val(fs, e));
                return true;

            default:
                return tonumeral(e, v);
        }
    }

    private static readonly uint* invalidinstruction = (uint*)NativeMemory.AllocZeroed(sizeof(uint));

    /// <summary>
    /// Return the previous instruction of the current code. If there
    /// may be a jump target between the current instruction and the
    /// previous one, return an invalid instruction (to avoid wrong
    /// optimisations).
    /// </summary>
    private static uint* previousinstruction(FuncState* fs)
    {
        // static const Instruction invalidinstruction = ~(Instruction)0;
        if (fs->pc > fs->lasttarget)
        {
            return &fs->f->code[fs->pc - 1]; // previous instruction
        }

        return invalidinstruction;
    }

    /// <summary>
    /// Create a OP_LOADNIL instruction, but try to optimise: if the previous
    /// instruction is also OP_LOADNIL and ranges are compatible, adjust
    /// range of previous instruction instead of emitting a new one. (For
    /// instance, 'local a; local b' will generate a single opcode.)
    /// </summary>
    private static void luaK_nil(FuncState* fs, int from, int n)
    {
        int l = from + n - 1; // last register to set nil
        uint* previous = previousinstruction(fs);
        if (GET_OPCODE(*previous) == OpCode.LoadNil)
        {
            // previous is LOADNIL?
            int pfrom = GETARG_A(*previous); // get previous range
            int pl = pfrom + GETARG_B(*previous);
            if (pfrom <= from && from <= pl + 1 ||
                from <= pfrom && pfrom <= l + 1)
            {
                // can connect both?
                if (pfrom < from)
                {
                    from = pfrom; // from = min(from, pfrom)
                }

                if (pl > l)
                {
                    l = pl; // l = max(l, pl)
                }

                SETARG_A(ref *previous, from);
                SETARG_B(ref *previous, l - from);
                return;
            } // else go through
        }

        luaK_codeABC(fs, OpCode.LoadNil, from, n - 1, 0); // else no optimisation
    }

    /// <summary>
    /// Gets the destination address of a jump instruction. Used to traverse
    /// a list of jumps.
    /// </summary>
    private static int getjump(FuncState* fs, int pc)
    {
        int offset = GETARG_sJ(fs->f->code[pc]);
        if (offset == NO_JUMP) // point to itself represents end of list
        {
            return NO_JUMP; // end of list
        }

        return pc + 1 + offset; // turn offset into absolute position
    }

    /// <summary>
    /// Fix jump instruction at position 'pc' to jump to 'dest'.
    /// (Jump addresses are relative in Lua)
    /// </summary>
    private static void fixjump(FuncState* fs, int pc, int dest)
    {
        uint* jmp = &fs->f->code[pc];
        int offset = dest - (pc + 1);
        Debug.Assert(dest != NO_JUMP);
        if (offset is < -OFFSET_sJ or > MAXARG_sJ - OFFSET_sJ)
        {
            luaX_syntaxerror(fs->ls, "control structure too long");
        }

        Debug.Assert(GET_OPCODE(*jmp) == OpCode.Jmp);
        SETARG_sJ(ref *jmp, offset);
    }

    /// <summary>
    /// Concatenate jump-list 'l2' into jump-list 'l1'
    /// </summary>
    private static void luaK_concat(FuncState* fs, int* l1, int l2)
    {
        if (l2 == NO_JUMP)
        {
            return; // nothing to concatenate?
        }

        if (*l1 == NO_JUMP) // no original list?
        {
            *l1 = l2; // 'l1' points to 'l2'
            return;
        }

        int list = *l1;
        int next;
        while ((next = getjump(fs, list)) != NO_JUMP) // find last element
        {
            list = next;
        }

        fixjump(fs, list, l2); // last element links to 'l2'
    }

    /// <summary>
    /// Create a jump instruction and return its position, so its destination
    /// can be fixed later (with 'fixjump').
    /// </summary>
    private static int luaK_jump(FuncState* fs)
    {
        return codesJ(fs, OpCode.Jmp, NO_JUMP, 0);
    }
    
    /// <summary>
    /// Code a 'return' instruction
    /// </summary>
    private static void luaK_ret(FuncState* fs, int first, int nret)
    {
        OpCode op = nret switch
        {
            0 => OpCode.Return0,
            1 => OpCode.Return1,
            _ => OpCode.Return,
        };

        luaY_checklimit(fs, nret + 1, MAXARG_B, "returns");
        luaK_codeABC(fs, op, first, nret + 1, 0);
    }

    /// <summary>
    /// Code a "conditional jump", that is, a test or comparison opcode
    /// followed by a jump. Return jump position.
    /// </summary>
    private static int condjump(FuncState* fs, OpCode op, int A, int B, int C, bool k)
    {
        luaK_codeABCk(fs, op, A, B, C, k);
        return luaK_jump(fs);
    }

    /// <summary>
    /// returns current 'pc' and marks it as a jump target (to avoid wrong
    /// optimisations with consecutive instructions not in the same basic block).
    /// </summary>
    private static int luaK_getlabel(FuncState* fs)
    {
        fs->lasttarget = fs->pc;
        return fs->pc;
    }
    
    /// <summary>
    /// Returns the position of the instruction "controlling" a given
    /// jump (that is, its condition), or the jump itself if it is
    /// unconditional.
    /// </summary>
    private static uint* getjumpcontrol(FuncState* fs, int pc)
    {
        uint* pi = &fs->f->code[pc];
        if (pc >= 1 && testTMode((OpMode)GET_OPCODE(*(pi - 1))))
        {
            return pi - 1;
        }

        return pi;
    }

    /// <summary>
    /// Patch destination register for a TESTSET instruction.
    /// If instruction in position 'node' is not a TESTSET, return 0 ("fails").
    /// Otherwise, if 'reg' is not 'NO_REG', set it as the destination
    /// register. Otherwise, change instruction to a simple 'TEST' (produces
    /// no register value)
    /// </summary>
    private static bool patchtestreg(FuncState* fs, int node, int reg)
    {
        uint* i = getjumpcontrol(fs, node);
        if (GET_OPCODE(*i) != OpCode.TestSet)
        {
            return false; // cannot patch other instructions
        }

        if (reg != NO_REG && reg != GETARG_B(*i))
        {
            SETARG_A(ref *i, reg);
        }
        else
        {
            // no register to put value or register already has the value;
            // change instruction to simple test
            *i = CREATE_ABCk(OpCode.Test, GETARG_B(*i), 0, 0, GETARG_k(*i));
        }

        return true;
    }

    /// <summary>
    /// Traverse a list of tests ensuring no one produces a value
    /// </summary>
    private static void removevalues(FuncState* fs, int list)
    {
        for (; list != NO_JUMP; list = getjump(fs, list))
        {
            patchtestreg(fs, list, NO_REG);
        }
    }

    /// <summary>
    /// Traverse a list of tests, patching their destination address and
    /// registers: tests producing values jump to 'vtarget' (and put their
    /// values in 'reg'), other tests jump to 'dtarget'.
    /// </summary>
    private static void patchlistaux(FuncState* fs, int list, int vtarget, int reg, int dtarget)
    {
        while (list != NO_JUMP)
        {
            int next = getjump(fs, list);
            fixjump(
                fs,
                list,
                patchtestreg(fs, list, reg) ? vtarget : dtarget ); // jump to default target

            list = next;
        }
    }

    /// <summary>
    /// Path all jumps in 'list' to jump to 'target'.
    /// (The assert means that we cannot fix a jump to a forward address
    /// because we only know addresses once code is generated.)
    /// </summary>
    private static void luaK_patchlist(FuncState* fs, int list, int target)
    {
        Debug.Assert(target <= fs->pc);
        patchlistaux(fs, list, target, NO_REG, target);
    }

    private static void luaK_patchtohere(FuncState* fs, int list)
    {
        int hr = luaK_getlabel(fs); // mark "here" as a jump target
        luaK_patchlist(fs, list, hr);
    }

    /// <summary>
    /// limit for difference between lines in relative line info.
    /// </summary>
    private static int LIMLINEDIFF = 0x80;

    /// <summary>
    /// Save line info for a new instruction. If difference from last line
    /// does not fit in a byte, of after that many instructions, save a new
    /// absolute line info; (in that case, the special value 'ABSLINEINFO'
    /// in 'lineinfo' signals the existence of this absolute information.)
    /// Otherwise, store the difference from last line in 'lineinfo'.
    /// </summary>
    private static void savelineinfo(FuncState* fs, Proto* f, int line)
    {
        int linedif = line - fs->previousline;
        int pc = fs->pc - 1; // last instruction coded
        if (Math.Abs(linedif) >= LIMLINEDIFF || fs->iwthabs++ >= MAXIWTHABS)
        {
            luaM_growvector(
                fs->ls->L,
                ref f->abslineinfo,
                fs->nabslineinfo,
                ref f->sizeabslineinfo,
                int.MaxValue,
                "lines");
            f->abslineinfo[fs->nabslineinfo].pc = pc;
            f->abslineinfo[fs->nabslineinfo++].line = line;
            linedif = ABSLINEINFO; // signal that there is absolute information
            fs->iwthabs = 1; // restart counter
        }

        luaM_growvector(
            fs->ls->L,
            ref f->lineinfo,
            pc,
            ref f->sizelineinfo,
            int.MaxValue,
            "opcodes");
        f->lineinfo[pc] = (sbyte)linedif;
        fs->previousline = line; // last line saved
    }

    /// <summary>
    /// Remove line information from the last instruction.
    /// If line information for that instruction is absolute, set 'iwthabs'
    /// above its max to force the new (replacing) instruction to have
    /// absolute line info, too.
    /// </summary>
    private static void removelastlineinfo(FuncState* fs)
    {
        Proto* f = fs->f;
        int pc = fs->pc - 1; // last instruction coded
        if (f->lineinfo[pc] != ABSLINEINFO)
        {
            // relative line info?
            fs->previousline -= f->lineinfo[pc]; // correct last line saved
            fs->iwthabs--; // undo previous increment
        }
        else
        {
            // absolute line information
            Debug.Assert(f->abslineinfo[fs->nabslineinfo - 1].pc == pc);
            fs->nabslineinfo--; // remove it
            fs->iwthabs = MAXIWTHABS + 1; // force next line info to be absolute
        }
    }

    /// <summary>
    /// Remove the last instruction created, correcting line information
    /// accordingly.
    /// </summary>
    private static void removelastinstruction(FuncState* fs)
    {
        removelastlineinfo(fs);
        fs->pc--;
    }

    /// <summary>
    /// Emit instruction 'i', checking for array sizes and saving also its
    /// line information. Return 'i' position.
    /// </summary>
    private static int luaK_code(FuncState* fs, uint i)
    {
        Proto* f = fs->f;
        // put new instruction in code array
        luaM_growvector(fs->ls->L, ref f->code, fs->pc, ref f->sizecode, int.MaxValue, "opcodes");
        f->code[fs->pc++] = i;
        savelineinfo(fs, f, fs->ls->lastline);
        return fs->pc - 1; // index of new instruction
    }

    /// <summary>
    /// Format and emit an 'iABC' instruction. (Assertions check consistency
    /// of parameters versus opcode.)
    /// </summary>
    private static int luaK_codeABCk(FuncState* fs, OpCode o, int A, int B, int C, bool k)
    {
        Debug.Assert(getOpMode(o) == OpMode.iABC);
        Debug.Assert(A <= MAXARG_A && B <= MAXARG_B && C <= MAXARG_C);
        return luaK_code(fs, CREATE_ABCk(o, A, B, C, k));
    }

    private static int luaK_codevABCk(FuncState* fs, OpCode o, int A, int B, int C, bool k)
    {
        Debug.Assert(getOpMode(o) == OpMode.ivABC);
        Debug.Assert(A <= MAXARG_A && B <= MAXARG_vB && C <= MAXARG_vC);
        return luaK_code(fs, CREATE_vABCk(o, A, B, C, k));
    }

    /// <summary>
    /// Format and emit an 'iABx' instruction.
    /// </summary>
    private static int luaK_codeABx(FuncState* fs, OpCode o, int A, int Bx)
    {
        Debug.Assert(getOpMode(o) == OpMode.iABx);
        Debug.Assert(A <= MAXARG_A && Bx <= MAXARG_Bx);
        return luaK_code(fs, CREATE_ABx(o, A, Bx));
    }

    /// <summary>
    /// Format and emit an 'iAsBx' instruction.
    /// </summary>
    private static int codeAsBx(FuncState* fs, OpCode o, int A, int Bc)
    {
        int b = Bc + OFFSET_sBx;
        Debug.Assert(getOpMode(o) == OpMode.iAsBx);
        Debug.Assert(A <= MAXARG_A && b <= MAXARG_Bx);
        return luaK_code(fs, CREATE_ABx(o, A, b));
    }

    /// <summary>
    /// Format and emit an 'isJ' instruction.
    /// </summary>
    private static int codesJ(FuncState* fs, OpCode o, int sj, int k)
    {
        int j = sj + OFFSET_sJ;
        Debug.Assert(getOpMode(o) == OpMode.isJ);
        Debug.Assert(j <= MAXARG_sJ && (k & ~1) == 0);
        return luaK_code(fs, CREATE_sJ(o, j, k));
    }

    /// <summary>
    /// Emit an "extra argument" instruction (format 'iAx')
    /// </summary>
    private static int codeextraarg(FuncState* fs, int A)
    {
        Debug.Assert(A <= MAXARG_Ax);
        return luaK_code(fs, CREATE_Ax(OpCode.ExtraArg, A));
    }

    /// <summary>
    /// Emit a "load constant" instruction, using either 'OP_LOADK'
    /// (if constant index 'k' fits in 18 bits) or an 'OP_LOADKX'
    /// instruction with "extra argument".
    /// </summary>
    private static int luaK_codek(FuncState* fs, int reg, int k)
    {
        if (k <= MAXARG_Bx)
        {
            return luaK_codeABx(fs, OpCode.LoadK, reg, k);
        }

        int p = luaK_codeABx(fs, OpCode.LoadKX, reg, 0);
        codeextraarg(fs, k);
        return p;
    }

    /// <summary>
    /// Check register-stack level, keeping track of its maximum size
    /// in field 'maxstacksize'
    /// </summary>
    private static void luaK_checkstack(FuncState* fs, int n)
    {
        int newstack = fs->freereg + n;
        if (newstack > fs->f->maxstacksize)
        {
            luaY_checklimit(fs, newstack, MAX_FSTACK, "registers");
            fs->f->maxstacksize = (byte)newstack;
        }
    }

    /// <summary>
    /// Reserve 'n' registers in register stack
    /// </summary>
    private static void luaK_reserveregs(FuncState* fs, int n)
    {
        luaK_checkstack(fs, n);
        fs->freereg = (byte)(fs->freereg + n);
    }

    /// <summary>
    /// Free register 'reg', if it is neither a constant index nor
    /// a local variable.
    /// )
    /// </summary>
    private static void freereg(FuncState* fs, int reg)
    {
        if (reg >= luaY_nvarstack(fs))
        {
            fs->freereg--;
            Debug.Assert(reg == fs->freereg);
        }
    }

    /// <summary>
    /// Free two registers in proper order
    /// </summary>
    private static void freeregs(FuncState* fs, int r1, int r2)
    {
        if (r1 > r2)
        {
            freereg(fs, r1);
            freereg(fs, r2);
        }
        else
        {
            freereg(fs, r2);
            freereg(fs, r1);
        }
    }

    /// <summary>
    /// Free register used by expression 'e' (if any)
    /// </summary>
    private static void freeexp(FuncState* fs, expdesc* e)
    {
        if (e->k == expkind.VNONRELOC)
        {
            freereg(fs, e->u.info);
        }
    }

    /// <summary>
    /// Free registers used by expressions 'e1' and 'e2' (if any) in proper
    /// order.
    /// </summary>
    private static void freeexps(FuncState* fs, expdesc* e1, expdesc* e2)
    {
        int r1 = e1->k == expkind.VNONRELOC ? e1->u.info : -1;
        int r2 = e2->k == expkind.VNONRELOC ? e2->u.info : -1;
        freeregs(fs, r1, r2);
    }

    /// <summary>
    /// Add constant 'v' to prototype's list of constants (field 'k').
    /// </summary>
    private static int addk(FuncState* fs, Proto* f, TValue* v)
    {
        lua_State* L = fs->ls->L;
        int oldsize = f->sizek;
        int k = fs->nk;
        luaM_growvector(L, ref f->k, k, ref f->sizek, MAXARG_Ax, "constants");
        while (oldsize < f->sizek)
        {
            setnilvalue(&f->k[oldsize++]);
        }

        setobj(L, &f->k[k], v);
        fs->nk++;
        luaC_barrier(L, (GCObject*)f, v);
        return k;
    }

    /// <summary>
    /// Use scanner's table to cache position of constants in constant list
    /// and try to reuse constants. Because some values should not be used
    /// as keys (nil cannot be a key, integer keys can collapse with float
    /// keys), the caller must provide a useful 'key' for indexing the cache.
    /// </summary>
    private static int k2proto(FuncState* fs, TValue* key, TValue* v)
    {
        Proto* f = fs->f;

        TValue val;
        byte tag = luaH_get(fs->kcache, key, &val); // query scanner table
        if (!tagisempty(tag))
        {
            // is there an index there?
            int k = (int)ivalue(&val);
            // collisions can happen only for float keys
            Debug.Assert(ttisfloat(key) || luaV_rawequalobj(&f->k[k], v));
            return k; // reuse index
        }
        else
        {
            // constant not found; create a new entry
            int k = addk(fs, f, v);
            // cache it for reuse; numerical value does not need GC barrier;
            // table is not a metatable, so it does not need to invalidate cache
            setivalue(&val, k);
            luaH_set(fs->ls->L, fs->kcache, key, &val);
            return k;
        }
    }

    /// <summary>
    /// Add a string to list of constants and return its index.
    /// </summary>
    private static int stringK(FuncState* fs, TString* s)
    {
        TValue o;
        setsvalue(fs->ls->L, &o, s);
        return k2proto(fs, &o, &o); // use string itself as key
    }

    /// <summary>
    /// Add an integer to list of constants and return its index.
    /// </summary>
    private static int luaK_intK(FuncState* fs, long n)
    {
        TValue o;
        setivalue(&o, n);
        return k2proto(fs, &o, &o); // use integer itself as key
    }

    /// <summary>
    /// Add a float to list of constants and return its index. Floats
    /// with integral values need a different key, to avoid collision
    /// with actual integers. To that end, we add to the number its smaller
    /// power-of-two fraction that is still significant in its scale.
    /// (For doubles, the fraction would be 2^-52).
    /// This method is not bulletproof: different numbers may generate the
    /// same key (e.g. very large numbers will overflow to 'inf') and for
    /// floats larger than 2^53 the result is still an integer. For those
    /// cases, just generate a new entry. At worst, this only wastes an entry
    /// with a duplicate.
    /// </summary>
    private static int luaK_numberK(FuncState* fs, double r)
    {
        TValue o;
        TValue kv;
        setfltvalue(&o, r); // value as a TValue
        if (r == 0)
        {
            // handle zero as a special case
            setpvalue(&kv, fs); // use FuncState as index
            return k2proto(fs, &kv, &o); // cannot collide
        }

        const int nbm = DBL_MANT_DIG;
        double q = Math.ScaleB(1.0, -nbm + 1);
        double k = r * (1 + q); // key
        setfltvalue(&kv, k); // key as a TValue
        if (!luaV_flttointeger(k, out long ik, F2Imod.F2Ieq))
        {
            // not an integer value?
            int n = k2proto(fs, &kv, &o); // use key
            if (luaV_rawequalobj(&fs->f->k[n], &o)) // correct value?
            {
                return n;
            }
        }

        // else, either key is still an integer or there was a collision;
        // anyway, do not try to reuse constant; instead, create a new one
        return addk(fs, fs->f, &o);
    }

    /// <summary>
    /// Add a false to list of constants and return its index.
    /// </summary>
    private static int boolF(FuncState* fs)
    {
        TValue o;
        setbfvalue(&o);
        return k2proto(fs, &o, &o); // use boolean itself as key
    }

    /// <summary>
    /// Add a true to list of constants and return its index.
    /// </summary>
    private static int boolT(FuncState* fs)
    {
        TValue o;
        setbtvalue(&o);
        return k2proto(fs, &o, &o); // use boolean itself as key
    }

    /// <summary>
    /// Add nil to list of constants and return its index.
    /// </summary>
    private static int nilK(FuncState* fs)
    {
        TValue k, v;
        setnilvalue(&v);
        // cannot use nil as key; instead use table itself
        sethvalue(fs->ls->L, &k, fs->kcache);
        return k2proto(fs, &k, &v);
    }

    /// <summary>
    /// Check whether 'i' can be stored in an 'sC' operand. Equivalent to
    /// (0 &lt;= int2sC(i) &amp;&amp; int2sC(i) &lt;= MAXARG_C) but without risk of
    /// overflows in the hidden addition inside 'int2sC'.
    /// </summary>
    private static bool fitsC(long i)
    {
        return (ulong)i + OFFSET_sC <= MAXARG_C;
    }

    /// <summary>
    /// Check whether 'i' can be stored in an 'sBx' operand.
    /// </summary>
    private static bool fitsBx(long i)
    {
        return i is >= -OFFSET_sBx and <= MAXARG_Bx - OFFSET_sBx;
    }

    private static void luaK_int(FuncState* fs, int reg, long n)
    {
        if (fitsBx(n))
        {
            codeAsBx(fs, OpCode.LoadI, reg, (int)n);
        }
        else
        {
            luaK_codek(fs, reg, luaK_intK(fs, n));
        }
    }

    private static void luaK_float(FuncState* fs, int reg, double f)
    {
        if (luaV_flttointeger(f, out long fi, F2Imod.F2Ieq) && fitsBx(fi))
        {
            codeAsBx(fs, OpCode.LoadF, reg, (int)fi);
        }
        else
        {
            luaK_codek(fs, reg, luaK_numberK(fs, f));
        }
    }

    /// <summary>
    /// Get the value of 'var' in a register and generate an opcode to check
    /// whether that register is nil. 'k' is the index of the variable name
    /// in the list of constants. If its value cannot be encoded in Bx, a 0
    /// will use '?' for the name.
    /// </summary>
    private static void luaK_codecheckglobal(FuncState* fs, expdesc* var, int k, int line)
    {
        luaK_exp2anyreg(fs, var);
        luaK_fixline(fs, line);
        k = k >= MAXARG_Bx ? 0 : k + 1;
        luaK_codeABx(fs, OpCode.ErrNNil, var->u.info, k);
        luaK_fixline(fs, line);
        freeexp(fs, var);
    }

   /// <summary>
   /// Convert a constant in 'v' into an expression description 'e'
   /// </summary>
   private static void const2exp(TValue* v, expdesc* e)
    {
        switch (ttypetag(v))
        {
            case LUA_VNUMINT:
                e->k = expkind.VKINT;
                e->u.ival = ivalue(v);
                break;
            
            case LUA_VNUMFLT:
                e->k = expkind.VKFLT;
                e->u.nval = fltvalue(v);
                break;
            
            case LUA_VFALSE:
                e->k = expkind.VFALSE;
                break;
            
            case LUA_VTRUE:
                e->k = expkind.VTRUE;
                break;
            
            case LUA_VNIL:
                e->k = expkind.VNIL;
                break;
            
            case LUA_VSHRSTR:
            case LUA_VLNGSTR:
                e->k = expkind.VKSTR;
                e->u.strval = tsvalue(v);
                break;
            
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Fix an expression to return the number of results 'nresults'.
    /// 'e' must be a multi-ret expression (function call or vararg).
    /// </summary>
    private static void luaK_setreturns(FuncState* fs, expdesc* e, int nresults)
    {
        ref uint pc = ref getinstruction(fs, e);
        luaY_checklimit(fs, nresults + 1, MAXARG_C, "multiple results");
        if (e->k == expkind.VCALL) // expression is an open function call?
        {
            SETARG_C(ref pc, nresults + 1);
        }
        else
        {
            Debug.Assert(e->k == expkind.VVARARG);
            SETARG_C(ref pc, nresults + 1);
            SETARG_A(ref pc, fs->freereg);
            luaK_reserveregs(fs, 1);
        }
    }

    /// <summary>
    /// Convert a VKSTR to a VK
    /// </summary>
    private static int str2K(FuncState* fs, expdesc* e)
    {
        Debug.Assert(e->k == expkind.VKSTR);
        e->u.info = stringK(fs, e->u.strval);
        e->k = expkind.VK;
        return e->u.info;
    }

    /// <summary>
    /// Fix an expression to return one result.
    /// If expression is not a multi-ret expression (function call or
    /// vararg), it already returns one result, so nothing needs to be done.
    /// Function calls become VNONRELOC expressions (as its result comes
    /// fixed in the base register of the call), while vararg expressions
    /// become VRELOC (as OP_VARARG puts its results where it wants).
    /// (Calls are created returning one result, so that does not need
    /// to be fixed.)
    /// </summary>
    private static void luaK_setoneret(FuncState* fs, expdesc* e)
    {
        if (e->k == expkind.VCALL)
        {
            // expression is an open function call?
            // already returns 1 value
            Debug.Assert(GETARG_C(getinstruction(fs, e)) == 2);
            e->k = expkind.VNONRELOC; // result has fixed position
            e->u.info = GETARG_A(getinstruction(fs, e));
        }
        else if (e->k == expkind.VVARARG)
        {
            SETARG_C(ref getinstruction(fs, e), 2);
            e->k = expkind.VRELOC; // can relocate its simple result
        }
    }

    /// <summary>
    /// Change a vararg parameter into a regular local variable
    /// </summary>
    private static void luaK_vapar2local(FuncState* fs, expdesc* var)
    {
        needvatab(fs->f); // function will need a vararg table
        // now a vararg parameter is equivalent to a regular local variable
        var->k = expkind.VLOCAL;
    }

    /// <summary>
    /// Ensure that expression 'e' is not a variable (nor a &lt;const&gt;).
    /// (Expression still may have jump lists.)
    /// </summary>
    private static void luaK_dischargevars(FuncState* fs, expdesc* e)
    {
        switch (e->k)
        {
            case expkind.VCONST:
                const2exp(const2val(fs, e), e);
                break;
            
            case expkind.VVARGVAR:
                luaK_vapar2local(fs, e); // turn it into a local variable
                goto case expkind.VLOCAL;

            case expkind.VLOCAL:
                {
                    // already in a register
                    int temp = e->u.var.ridx;
                    e->u.info = temp; // (can't do a direct assignment; values overlap)
                    e->k = expkind.VNONRELOC; // becomes a non-relocatable value
                    break;
                }

            case expkind.VUPVAL:
                // move value to some (pending) register
                e->u.info = luaK_codeABC(fs, OpCode.GetUpVal, 0, e->u.info, 0);
                e->k = expkind.VRELOC;
                break;
            
            case expkind.VINDEXUP:
                e->u.info = luaK_codeABC(fs, OpCode.GetTabUp, 0, e->u.ind.t, e->u.ind.idx);
                e->k = expkind.VRELOC;
                break;
            
            case expkind.VINDEXI:
                freereg(fs, e->u.ind.t);
                e->u.info = luaK_codeABC(fs, OpCode.GetI, 0, e->u.ind.t, e->u.ind.idx);
                e->k = expkind.VRELOC;
                break;
            
            case expkind.VINDEXSTR:
                freereg(fs, e->u.ind.t);
                e->u.info = luaK_codeABC(fs, OpCode.GetField, 0, e->u.ind.t, e->u.ind.idx);
                e->k = expkind.VRELOC;
                break;
            
            case expkind.VINDEXED:
                freeregs(fs, e->u.ind.t, e->u.ind.idx);
                e->u.info = luaK_codeABC(fs, OpCode.GetTable, 0, e->u.ind.t, e->u.ind.idx);
                e->k = expkind.VRELOC;
                break;

            case expkind.VVARGIND:
                freeregs(fs, e->u.ind.t, e->u.ind.idx);
                e->u.info = luaK_codeABC(fs, OpCode.GetVArg, 0, e->u.ind.t, e->u.ind.idx);
                e->k = expkind.VRELOC;
                break;
            
            case expkind.VVARARG:
            case expkind.VCALL:
                luaK_setoneret(fs, e);
                break;
        }
    }

    /// <summary>
    /// Ensure expression value is in register 'reg', making 'e' a
    /// non-relocatable expression.
    /// (Expression still may have jump lists.)
    /// </summary>
    private static void discharge2reg(FuncState* fs, expdesc* e, int reg)
    {
        luaK_dischargevars(fs, e);
        switch (e->k)
        {
            case expkind.VNIL:
                luaK_nil(fs, reg, 1);
                break;

            case expkind.VFALSE:
                luaK_codeABC(fs, OpCode.LoadFalse, reg, 0, 0);
                break;

            case expkind.VTRUE:
                luaK_codeABC(fs, OpCode.LoadTrue, reg, 0, 0);
                break;

            case expkind.VKSTR:
                str2K(fs, e);
                goto case expkind.VK;

            case expkind.VK:
                luaK_codek(fs, reg, e->u.info);
                break;

            case expkind.VKFLT:
                luaK_float(fs, reg, e->u.nval);
                break;

            case expkind.VKINT:
                luaK_int(fs, reg, e->u.ival);
                break;

            case expkind.VRELOC:
                {
                    ref uint pc = ref getinstruction(fs, e);
                    SETARG_A(ref pc, reg); // instruction will put result in 'reg'
                    break;
                }

            case expkind.VNONRELOC:
                if (reg != e->u.info)
                {
                    luaK_codeABC(fs, OpCode.Move, reg, e->u.info, 0);
                }

                break;
            
            case expkind.VJMP:
                return; // nothing to do...

            default:
                throw new InvalidOperationException();
        }

        e->u.info = reg;
        e->k = expkind.VNONRELOC;
    }

    /// <summary>
    /// Ensure expression value is in a register, making 'e' a
    /// non-relocatable expression.
    /// (Expression still may have jump lists.)
    /// </summary>
    private static void discharge2anyreg(FuncState* fs, expdesc* e)
    {
        if (e->k != expkind.VNONRELOC)
        {
            // no fixed register yet?
            luaK_reserveregs(fs, 1); // get a register
            discharge2reg(fs, e, fs->freereg - 1); // put value there
        }
    }

    private static int code_loadbool(FuncState* fs, int A, OpCode op)
    {
        luaK_getlabel(fs); // those instructions may be jump targets
        return luaK_codeABC(fs, op, A, 0, 0);
    }

    /// <summary>
    /// check whether list has any jump that do not produce a value
    /// or produce an inverted value
    /// </summary>
    private static bool need_value(FuncState* fs, int list)
    {
        for (; list != NO_JUMP; list = getjump(fs, list))
        {
            uint i = *getjumpcontrol(fs, list);
            if (GET_OPCODE(i) != OpCode.TestSet)
            {
                return true;
            }
        }

        return false; // not found
    }

    /// <summary>
    /// Ensures final expression result (which includes results from its
    /// jump lists) is in register 'reg'.
    /// If expression has jumps, need to patch these jumps either to
    /// its final position or to "load" instructions (for those tests
    /// that do not produce values).
    /// </summary>
    private static void exp2reg(FuncState* fs, expdesc* e, int reg)
    {
        discharge2reg(fs, e, reg);
        if (e->k == expkind.VJMP) // expression itself is a test?
        {
            luaK_concat(fs, &e->t, e->u.info); // put this jump in 't' list
        }

        if (hasjumps(e))
        {
            int p_f = NO_JUMP; // position of an eventual LOAD false
            int p_t = NO_JUMP; // position of an eventual LOAD true
            if (need_value(fs, e->t) || need_value(fs, e->f))
            {
                int fj = e->k == expkind.VJMP ? NO_JUMP : luaK_jump(fs);
                p_f = code_loadbool(fs, reg, OpCode.LFalseSkip); // skip next inst.
                p_t = code_loadbool(fs, reg, OpCode.LoadTrue);
                // jump around these booleans if 'e' is not a test
                luaK_patchtohere(fs, fj);
            }

            int final = luaK_getlabel(fs);
            patchlistaux(fs, e->f, final, reg, p_f);
            patchlistaux(fs, e->t, final, reg, p_t);
        }

        e->f = e->t = NO_JUMP;
        e->u.info = reg;
        e->k = expkind.VNONRELOC;
    }

    /// <summary>
    /// Ensures final expression result is in next available register.
    /// </summary>
    private static void luaK_exp2nextreg(FuncState* fs, expdesc* e)
    {
        luaK_dischargevars(fs, e);
        freeexp(fs, e);
        luaK_reserveregs(fs, 1);
        exp2reg(fs, e, fs->freereg - 1);
    }

    /// <summary>
    /// Ensures final expression result is in some (any) register
    /// and return that register.
    /// </summary>
    private static int luaK_exp2anyreg(FuncState* fs, expdesc* e)
    {
        luaK_dischargevars(fs, e);
        if (e->k == expkind.VNONRELOC)
        {
            // expression already has a register?
            if (!hasjumps(e)) // no jumps?
            {
                return e->u.info; // result is already in a register
            }

            if (e->u.info >= luaY_nvarstack(fs))
            {
                // reg. is not a local?
                exp2reg(fs, e, e->u.info); // put final result in it
                return e->u.info;
            }
            // else expression has jumps and cannot change its register
            // to hold the jump values, because it is a local variable.
            // Go through to the default case.
        }

        luaK_exp2nextreg(fs, e); // default: use next available register
        return e->u.info;
    }

    /// <summary>
    /// Ensures final expression result is either in a register,
    /// in an upvalue, or it is the vararg parameter.
    /// </summary>
    private static void luaK_exp2anyregup(FuncState* fs, expdesc* e)
    {
        if (e->k != expkind.VUPVAL && e->k != expkind.VVARGVAR || hasjumps(e))
        {
            luaK_exp2anyreg(fs, e);
        }
    }

    /// <summary>
    /// Ensures final expression result is either in a register
    /// or it is a constant.
    /// </summary>
    private static void luaK_exp2val(FuncState* fs, expdesc* e)
    {
        if (e->k == expkind.VJMP || hasjumps(e))
        {
            luaK_exp2anyreg(fs, e);
        }
        else
        {
            luaK_dischargevars(fs, e);
        }
    }

    /// <summary>
    /// Try to make 'e' a K expression with an index in the range of R/K
    /// indices. Return true iff succeeded.
    /// </summary>
    private static bool luaK_exp2K(FuncState* fs, expdesc* e)
    {
        if (!hasjumps(e))
        {
            int info;
            switch (e->k)
            {
                // move constants to 'k'
                case expkind.VTRUE: info = boolT(fs); break;
                case expkind.VFALSE: info = boolF(fs); break;
                case expkind.VNIL: info = nilK(fs); break;
                case expkind.VKINT: info = luaK_intK(fs, e->u.ival); break;
                case expkind.VKFLT: info = luaK_numberK(fs, e->u.nval); break;
                case expkind.VKSTR: info = stringK(fs, e->u.strval); break;
                case expkind.VK: info = e->u.info; break;
                default: return false; // not a constant
            }

            if (info <= MAXINDEXRK)
            {
                // does constant fit in 'argC'?
                e->k = expkind.VK; // make expression a 'K' expression
                e->u.info = info;
                return true;
            }
        }

        // expression doesn't fit; leave it unchanged
        return false;
    }

    /// <summary>
    /// Ensures final expression result is in a valid R/K index
    /// (that is, it is either in a register or in 'k' with an index
    /// in the range of R/K indices).
    /// Returns 1 iff expression is K.
    /// </summary>
    private static bool exp2RK(FuncState* fs, expdesc* e)
    {
        if (luaK_exp2K(fs, e))
        {
            return true;
        }

        // not a constant in the right range: put it in a register
        luaK_exp2anyreg(fs, e);
        return false;
    }

    private static void codeABRK(
        FuncState* fs,
        OpCode o,
        int A,
        int B,
        expdesc* ec)
    {
        bool k = exp2RK(fs, ec);
        luaK_codeABCk(fs, o, A, B, ec->u.info, k);
    }

    /// <summary>
    /// Generate code to store result of expression 'ex' into variable 'var'.
    /// </summary>
    private static void luaK_storevar(FuncState* fs, expdesc* var, expdesc* ex)
    {
        switch (var->k)
        {
            case expkind.VLOCAL:
                freeexp(fs, ex);
                exp2reg(fs, ex, var->u.var.ridx); // compute 'ex' into proper place
                return;

            case expkind.VUPVAL:
                {
                    int e = luaK_exp2anyreg(fs, ex);
                    luaK_codeABC(fs, OpCode.SetUpVal, e, var->u.info, 0);
                    break;
                }

            case expkind.VINDEXUP:
                codeABRK(fs, OpCode.SetTabUp, var->u.ind.t, var->u.ind.idx, ex);
                break;

            case expkind.VINDEXI:
                codeABRK(fs, OpCode.SetI, var->u.ind.t, var->u.ind.idx, ex);
                break;

            case expkind.VINDEXSTR:
                codeABRK(fs, OpCode.SetField, var->u.ind.t, var->u.ind.idx, ex);
                break;

            case expkind.VVARGIND:
                needvatab(fs->f); // function will need a vararg table
                // now, assignment is to a regular table
                goto case expkind.VINDEXED;
                
            case expkind.VINDEXED:
                codeABRK(fs, OpCode.SetTable, var->u.ind.t, var->u.ind.idx, ex);
                break;
            
            default:
                throw new InvalidOperationException();
        }

        freeexp(fs, ex);
    }

    /// <summary>
    /// Negate condition 'e' (where 'e' is a comparison).
    /// </summary>
    private static void negatecondition(FuncState* fs, expdesc* e)
    {
        uint* pc = getjumpcontrol(fs, e->u.info);
        Debug.Assert(
            testTMode((OpMode)GET_OPCODE(*pc)) &&
            GET_OPCODE(*pc) != OpCode.TestSet &&
            GET_OPCODE(*pc) != OpCode.Test);
        SETARG_k(ref *pc, !GETARG_k(*pc));
    }

    /// <summary>
    /// Emit instruction to jump if 'e' is 'cond' (that is, if 'cond'
    /// is true, code will jump if 'e' is true.) Return jump position.
    /// Optimise when 'e' is 'not' something, inverting the condition
    /// and removing the 'not'.
    /// </summary>
    private static int jumponcond(FuncState* fs, expdesc* e, bool cond)
    {
        if (e->k == expkind.VRELOC)
        {
            uint ie = getinstruction(fs, e);
            if (GET_OPCODE(ie) == OpCode.Not)
            {
                removelastinstruction(fs); // remove previous OP_NOT
                return condjump(fs, OpCode.Test, GETARG_B(ie), 0, 0, !cond);
            }
            // else go through
        }

        discharge2anyreg(fs, e);
        freeexp(fs, e);
        return condjump(fs, OpCode.TestSet, NO_REG, e->u.info, 0, cond);
    }

    /// <summary>
    /// Emit code to go through if 'e' is true, jump otherwise.
    /// </summary>
    private static void luaK_goiftrue(FuncState* fs, expdesc* e)
    {
        int pc; // pc of new jump
        luaK_dischargevars(fs, e);
        switch (e->k)
        {
            case expkind.VJMP:
                // condition?
                negatecondition(fs, e); // jump when it is false
                pc = e->u.info; // save jump position
                break;
            
            case expkind.VK:
            case expkind.VKFLT:
            case expkind.VKINT:
            case expkind.VKSTR:
            case expkind.VTRUE:
                pc = NO_JUMP; // always true; do nothing
                break;
            
            default:
                pc = jumponcond(fs, e, false); // jump when false
                break;
        }

        luaK_concat(fs, &e->f, pc); // insert new jump in false list
        luaK_patchtohere(fs, e->t); // true list jumps to here (to go through)
        e->t = NO_JUMP;
    }

    /// <summary>
    /// Emit code to go through if 'e' is false, jump otherwise.
    /// </summary>
    private static void luaK_goiffalse(FuncState* fs, expdesc* e)
    {
        luaK_dischargevars(fs, e);
        int pc = e->k switch
        {
            expkind.VJMP => e->u.info , // already jump if true
            expkind.VNIL or expkind.VFALSE => NO_JUMP , // always false; do nothing
            _ => jumponcond(fs, e, true),
        };

        luaK_concat(fs, &e->t, pc); // insert new jump in 't' list
        luaK_patchtohere(fs, e->f); // false list jumps to here (to go through)
        e->f = NO_JUMP;
    }

    /// <summary>
    /// Code 'not e', doing constant folding.
    /// </summary>
    private static void codenot(FuncState* fs, expdesc* e)
    {
        switch (e->k)
        {
            case expkind.VNIL:
            case expkind.VFALSE:
                e->k = expkind.VTRUE; // true == not nil == not false
                break;

            case expkind.VK:
            case expkind.VKFLT:
            case expkind.VKINT:
            case expkind.VKSTR:
            case expkind.VTRUE:
                e->k = expkind.VFALSE; // false == not "x" == not 0.5 == not 1 == not true
                break;

            case expkind.VJMP:
                negatecondition(fs, e);
                break;

            case expkind.VRELOC:
            case expkind.VNONRELOC:
                discharge2anyreg(fs, e);
                freeexp(fs, e);
                e->u.info = luaK_codeABC(fs, OpCode.Not, 0, e->u.info, 0);
                e->k = expkind.VRELOC;
                break;

            default:
                throw new InvalidOperationException();
        }

        // interchange true and false lists
        int temp = e->f;
        e->f = e->t;
        e->t = temp;
        removevalues(fs, e->f); // values are useless when negated
        removevalues(fs, e->t);
    }

    /// <summary>
    /// Check whether expression 'e' is a short literal string
    /// </summary>
    private static bool isKstr(FuncState* fs, expdesc* e)
    {
        return e->k == expkind.VK &&
               !hasjumps(e) &&
               e->u.info <= MAXINDEXRK &&
               ttisshrstring(&fs->f->k[e->u.info]);
    }

    /// <summary>
    /// Check whether expression 'e' is a literal integer.
    /// </summary>
    private static bool isKint(expdesc* e)
    {
        return e->k == expkind.VKINT && !hasjumps(e);
    }

    /// <summary>
    /// Check whether expression 'e' is a literal integer in
    /// proper range to fit in register C
    /// </summary>
    private static bool isCint(expdesc* e)
    {
        return isKint(e) && (ulong)e->u.ival <= MAXARG_C;
    }

    /// <summary>
    /// Check whether expression 'e' is a literal integer in
    /// proper range to fit in register sC
    /// </summary>
    private static bool isSCint(expdesc* e)
    {
        return isKint(e) && fitsC(e->u.ival);
    }

    /// <summary>
    /// Check whether expression 'e' is a literal integer or float in
    /// proper range to fit in a register (sB or sC).
    /// </summary>
    private static bool isSCnumber(expdesc* e, int* pi, bool* isfloat)
    {
        long i;
        if (e->k == expkind.VKINT)
        {
            i = e->u.ival;
        }
        else if (e->k == expkind.VKFLT && luaV_flttointeger(e->u.nval, out i, F2Imod.F2Ieq))
        {
            *isfloat = true;
        }
        else
        {
            return false; // not a number
        }

        if (!hasjumps(e) && fitsC(i))
        {
            *pi = int2sC((int)i);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Emit SELF instruction or equivalent: the code will convert
    /// expression 'e' into 'e.key(e,'.
    /// </summary>
    private static void luaK_self(FuncState* fs, expdesc* e, expdesc* key)
    {
        luaK_exp2anyreg(fs, e);
        int ereg = e->u.info; // register where 'e' (the receiver) was placed
        freeexp(fs, e);
        int @base = e->u.info = fs->freereg; // base register for op_self
        e->k = expkind.VNONRELOC; // self expression has a fixed register
        luaK_reserveregs(fs, 2); // method and 'self' produced by op_self
        Debug.Assert(key->k == expkind.VKSTR);
        // is method name a short string in a valid K index?
        if (strisshr(key->u.strval) && luaK_exp2K(fs, key))
        {
            // can use 'self' opcode
            luaK_codeABCk(fs, OpCode.Self, @base, ereg, key->u.info, false);
        }
        else
        {
            // cannot use 'self' opcode; use move+gettable
            luaK_exp2anyreg(fs, key); // put method name in a register
            luaK_codeABC(fs, OpCode.Move, @base + 1, ereg, 0); // copy self to base+1
            luaK_codeABC(fs, OpCode.GetTable, @base, ereg, key->u.info); // get method
        }

        freeexp(fs, key);
    }

    /// <summary>
    /// auxiliary function to define indexing expressions
    /// </summary>
    private static void fillidxk(expdesc* t, int idx, expkind k)
    {
        t->u.ind.idx = (byte)idx;
        t->k = k;
    }

    /// <summary>
    /// Create expression 't[k]'. 't' must have its final result already in a
    /// register or upvalue. Upvalues can only be indexed by literal strings.
    /// Keys can be literal strings in the constant table or arbitrary
    /// values in registers.
    /// </summary>
    private static void luaK_indexed(FuncState* fs, expdesc* t, expdesc* k)
    {
        int keystr = -1;
        if (k->k == expkind.VKSTR)
        {
            keystr = str2K(fs, k);
        }

        Debug.Assert(
            !hasjumps(t) &&
            (t->k == expkind.VLOCAL ||
             t->k == expkind.VVARGVAR ||
             t->k == expkind.VNONRELOC ||
             t->k == expkind.VUPVAL));
        if (t->k == expkind.VUPVAL && !isKstr(fs, k)) // upvalue indexed by non 'Kstr'?
        {
            luaK_exp2anyreg(fs, t); // put it in a register
        }

        if (t->k == expkind.VUPVAL)
        {
            byte temp = (byte)t->u.info; // upvalue index
            t->u.ind.t = temp; // (can't do a direct assignment; values overlap)
            Debug.Assert(isKstr(fs, k));
            fillidxk(t, k->u.info, expkind.VINDEXUP); // literal short string
        }
        else if (t->k == expkind.VVARGVAR)
        {
            // indexing the vararg parameter?
            int kreg = luaK_exp2anyreg(fs, k); // put key in some register
            byte vreg = t->u.var.ridx; // register with vararg param.
            Debug.Assert(vreg == fs->f->numparams);
            t->u.ind.t = vreg; // (avoid a direct assignment; values may overlap)
            fillidxk(t, kreg, expkind.VVARGIND); // 't' represents 'vararg[k]'
        }
        else
        {
            // register index of the table
            t->u.ind.t = (byte)(t->k == expkind.VLOCAL ? t->u.var.ridx : t->u.info);
            if (isKstr(fs, k))
            {
                fillidxk(t, k->u.info, expkind.VINDEXSTR); // literal short string
            }
            else if (isCint(k)) // int. constant in proper range?
            {
                fillidxk(t, (int)k->u.ival, expkind.VINDEXI);
            }
            else
            {
                fillidxk(t, luaK_exp2anyreg(fs, k), expkind.VINDEXED); // register
            }
        }

        t->u.ind.keystr = keystr; // string index in 'k'
        t->u.ind.ro = false; // by default, not read-only
    }

    /// <summary>
    /// Return false if folding can raise an error.
    /// Bitwise operations need operands convertible to integers; division
    /// operations cannot have 0 as divisor.
    /// </summary>
    private static bool validop(int op, TValue* v1, TValue* v2)
    {
        return op switch
        {
            // conversion errors
            LUA_OPBAND or LUA_OPBOR or LUA_OPBXOR or LUA_OPSHL or LUA_OPSHR or LUA_OPBNOT =>
                luaV_tointegerns(v1, out _, LUA_FLOORN2I) && luaV_tointegerns(v2, out _, LUA_FLOORN2I),

            // division by 0
            LUA_OPDIV or LUA_OPIDIV or LUA_OPMOD => nvalue(v2) != 0,

            _ => true,
        };
    }

    /// <summary>
    /// Try to "constant-fold" an operation; return 1 iff successful.
    /// (In this case, 'e1' has the final result.)
    /// </summary>
    private static bool constfolding(
        FuncState* fs,
        int op,
        expdesc* e1,
        expdesc* e2)
    {
        TValue v1, v2, res;
        if (!tonumeral(e1, &v1) || !tonumeral(e2, &v2) || !validop(op, &v1, &v2))
        {
            return false; // non-numeric operands or not safe to fold
        }

        luaO_rawarith(fs->ls->L, op, &v1, &v2, &res); // does operation
        if (ttisinteger(&res))
        {
            e1->k = expkind.VKINT;
            e1->u.ival = ivalue(&res);
        }
        else
        {
            // folds neither NaN nor 0.0 (to avoid problems with -0.0)
            double n = fltvalue(&res);
            if (double.IsNaN(n) || n == 0)
            {
                return false;
            }

            e1->k = expkind.VKFLT;
            e1->u.nval = n;
        }

        return true;
    }

    /// <summary>
    /// Convert a BinOpr to an OpCode  (ORDER OPR - ORDER OP)
    /// </summary>
    private static OpCode binopr2op(BinOpr opr, BinOpr baser, OpCode @base)
    {
        Debug.Assert(
            baser <= opr &&
            (baser == BinOpr.OPR_ADD && opr <= BinOpr.OPR_SHR ||
             baser == BinOpr.OPR_LT && opr <= BinOpr.OPR_LE));
        return (OpCode)((int)opr - (int)baser + (int)@base);
    }

    /// <summary>
    /// Convert a UnOpr to an OpCode  (ORDER OPR - ORDER OP)
    /// </summary>
    private static OpCode unopr2op(UnOpr opr)
    {
        return (OpCode)((int)opr - (int)UnOpr.MINUS + (int)OpCode.UNM);
    }

    /// <summary>
    /// Convert a BinOpr to a tag method  (ORDER OPR - ORDER TM)
    /// </summary>
    private static TMS binopr2TM(BinOpr opr)
    {
        Debug.Assert(opr is >= BinOpr.OPR_ADD and <= BinOpr.OPR_SHR);
        return (TMS)((int)opr - (int)BinOpr.OPR_ADD + (int)TMS.ADD);
    }

    /// <summary>
    /// Emit code for unary expressions that "produce values"
    /// (everything but 'not').
    /// Expression to produce final result will be encoded in 'e'.
    /// </summary>
    private static void codeunexpval(FuncState* fs, OpCode op, expdesc* e, int line)
    {
        int r = luaK_exp2anyreg(fs, e); // opcodes operate only on registers
        freeexp(fs, e);
        e->u.info = luaK_codeABC(fs, op, 0, r, 0); // generate opcode
        e->k = expkind.VRELOC; // all those operations are relocatable
        luaK_fixline(fs, line);
    }

    /// <summary>
    /// Emit code for binary expressions that "produce values"
    /// (everything but logical operators 'and'/'or' and comparison
    /// operators).
    /// Expression to produce final result will be encoded in 'e1'.
    /// </summary>
    private static void finishbinexpval(
        FuncState* fs,
        expdesc* e1,
        expdesc* e2,
        OpCode op,
        int v2,
        bool flip,
        int line,
        OpCode mmop,
        TMS @event)
    {
        int v1 = luaK_exp2anyreg(fs, e1);
        int pc = luaK_codeABCk(fs, op, 0, v1, v2, false);
        freeexps(fs, e1, e2);
        e1->u.info = pc;
        e1->k = expkind.VRELOC; // all those operations are relocatable
        luaK_fixline(fs, line);
        luaK_codeABCk(fs, mmop, v1, v2, (int)@event, flip); // metamethod
        luaK_fixline(fs, line);
    }

    /// <summary>
    /// Emit code for binary expressions that "produce values" over
    /// two registers.
    /// </summary>
    private static void codebinexpval(
        FuncState* fs,
        BinOpr opr,
        expdesc* e1,
        expdesc* e2,
        int line)
    {
        OpCode op = binopr2op(opr, BinOpr.OPR_ADD, OpCode.Add);
        int v2 = luaK_exp2anyreg(fs, e2); // make sure 'e2' is in a register
        // 'e1' must be already in a register or it is a constant
        Debug.Assert(
            expkind.VNIL <= e1->k && e1->k <= expkind.VKSTR ||
            e1->k == expkind.VNONRELOC ||
            e1->k == expkind.VRELOC);
        Debug.Assert(op is >= OpCode.Add and <= OpCode.Shr);
        finishbinexpval(fs, e1, e2, op, v2, false, line, OpCode.MMBin, binopr2TM(opr));
    }

    /// <summary>
    /// Code binary operators with immediate operands.
    /// </summary>
    private static void codebini(
        FuncState* fs,
        OpCode op,
        expdesc* e1,
        expdesc* e2,
        bool flip,
        int line,
        TMS @event)
    {
        int v2 = int2sC((int)e2->u.ival); // immediate operand
        Debug.Assert(e2->k == expkind.VKINT);
        finishbinexpval(fs, e1, e2, op, v2, flip, line, OpCode.MMBinI, @event);
    }

    /// <summary>
    /// Code binary operators with K operand.
    /// </summary>
    private static void codebinK(
        FuncState* fs,
        BinOpr opr,
        expdesc* e1,
        expdesc* e2,
        bool flip,
        int line)
    {
        TMS @event = binopr2TM(opr);
        int v2 = e2->u.info; // K index
        OpCode op = binopr2op(opr, BinOpr.OPR_ADD, OpCode.AddK);
        finishbinexpval(fs, e1, e2, op, v2, flip, line, OpCode.MMBinK, @event);
    }

    /// <summary>
    /// Try to code a binary operator negating its second operand.
    /// For the metamethod, 2nd operand must keep its original value.
    /// </summary>
    private static bool finishbinexpneg(
        FuncState* fs,
        expdesc* e1,
        expdesc* e2,
        OpCode op,
        int line,
        TMS @event)
    {
        if (!isKint(e2))
        {
            return false; // not an integer constant
        }

        long i2 = e2->u.ival;
        if (!(fitsC(i2) && fitsC(-i2)))
        {
            return false; // not in the proper range
        }

        // operating a small integer constant
        int v2 = (int)i2;
        finishbinexpval(fs, e1, e2, op, int2sC(-v2), false, line, OpCode.MMBinI, @event);
        // correct metamethod argument
        SETARG_B(ref fs->f->code[fs->pc - 1], int2sC(v2));
        return true; // successfully coded
    }

    private static void swapexps(expdesc* e1, expdesc* e2)
    {
        expdesc temp = *e1;
        *e1 = *e2;
        *e2 = temp; // swap 'e1' and 'e2'
    }

    /// <summary>
    /// Code binary operators with no constant operand.
    /// </summary>
    private static void codebinNoK(
        FuncState* fs,
        BinOpr opr,
        expdesc* e1,
        expdesc* e2,
        bool flip,
        int line)
    {
        if (flip)
        {
            swapexps(e1, e2); // back to original order
        }

        codebinexpval(fs, opr, e1, e2, line); // use standard operators
    }

    /// <summary>
    /// Code arithmetic operators ('+', '-', ...). If second operand is a
    /// constant in the proper range, use variant opcodes with K operands.
    /// </summary>
    private static void codearith(
        FuncState* fs,
        BinOpr opr,
        expdesc* e1,
        expdesc* e2,
        bool flip,
        int line)
    {
        if (tonumeral(e2, null) && luaK_exp2K(fs, e2)) // K operand?
        {
            codebinK(fs, opr, e1, e2, flip, line);
        }
        else // 'e2' is neither an immediate nor a K operand
        {
            codebinNoK(fs, opr, e1, e2, flip, line);
        }
    }

    /// <summary>
    /// Code commutative operators ('+', '*'). If first operand is a
    /// numeric constant, change order of operands to try to use an
    /// immediate or K operator.
    /// </summary>
    private static void codecommutative(
        FuncState* fs,
        BinOpr op,
        expdesc* e1,
        expdesc* e2,
        int line)
    {
        bool flip = false;
        if (tonumeral(e1, null))
        {
            // is first operand a numeric constant?
            swapexps(e1, e2); // change order
            flip = true;
        }

        if (op == BinOpr.OPR_ADD && isSCint(e2)) // immediate operand?
        {
            codebini(fs, OpCode.AddI, e1, e2, flip, line, TMS.ADD);
        }
        else
        {
            codearith(fs, op, e1, e2, flip, line);
        }
    }

    /// <summary>
    /// Code bitwise operations; they are all commutative, so the function
    /// tries to put an integer constant as the 2nd operand (a K operand).
    /// </summary>
    private static void codebitwise(
        FuncState* fs,
        BinOpr opr,
        expdesc* e1,
        expdesc* e2,
        int line)
    {
        bool flip = false;
        if (e1->k == expkind.VKINT)
        {
            swapexps(e1, e2); // 'e2' will be the constant operand
            flip = true;
        }

        if (e2->k == expkind.VKINT && luaK_exp2K(fs, e2)) // K operand?
        {
            codebinK(fs, opr, e1, e2, flip, line);
        }
        else // no constants
        {
            codebinNoK(fs, opr, e1, e2, flip, line);
        }
    }

    /// <summary>
    /// Emit code for order comparisons. When using an immediate operand,
    /// 'isfloat' tells whether the original value was a float.
    /// </summary>
    private static void codeorder(FuncState* fs, BinOpr opr, expdesc* e1, expdesc* e2)
    {
        int im;
        bool isfloat = false;
        int r1;
        int r2;
        OpCode op;
        if (isSCnumber(e2, &im, &isfloat))
        {
            // use immediate operand
            r1 = luaK_exp2anyreg(fs, e1);
            r2 = im;
            op = binopr2op(opr, BinOpr.OPR_LT, OpCode.LTI);
        }
        else if (isSCnumber(e1, &im, &isfloat))
        {
            // transform (A < B) to (B > A) and (A <= B) to (B >= A)
            r1 = luaK_exp2anyreg(fs, e2);
            r2 = im;
            op = binopr2op(opr, BinOpr.OPR_LT, OpCode.GTI);
        }
        else
        {
            // regular case, compare two registers
            r1 = luaK_exp2anyreg(fs, e1);
            r2 = luaK_exp2anyreg(fs, e2);
            op = binopr2op(opr, BinOpr.OPR_LT, OpCode.LT);
        }

        freeexps(fs, e1, e2);
        e1->u.info = condjump(fs, op, r1, r2, isfloat ? 1 : 0, true);
        e1->k = expkind.VJMP;
    }

    /// <summary>
    /// Emit code for equality comparisons ('==', '~=').
    /// 'e1' was already put as RK by 'luaK_infix'.
    /// </summary>
    private static void codeeq(FuncState* fs, BinOpr opr, expdesc* e1, expdesc* e2)
    {
        if (e1->k != expkind.VNONRELOC)
        {
            Debug.Assert(e1->k == expkind.VK || e1->k == expkind.VKINT || e1->k == expkind.VKFLT);
            swapexps(e1, e2);
        }

        int r1 = luaK_exp2anyreg(fs, e1); // 1st expression must be in register
        int im;
        bool isfloat = false; // not needed here, but kept for symmetry
        int r2;
        OpCode op;
        if (isSCnumber(e2, &im, &isfloat))
        {
            op = OpCode.EqI;
            r2 = im; // immediate operand
        }
        else if (exp2RK(fs, e2))
        {
            // 2nd expression is constant?
            op = OpCode.EqK;
            r2 = e2->u.info; // constant index
        }
        else
        {
            op = OpCode.Eq; // will compare two registers
            r2 = luaK_exp2anyreg(fs, e2);
        }

        freeexps(fs, e1, e2);
        e1->u.info = condjump(fs, op, r1, r2, isfloat ? 1 : 0, opr == BinOpr.OPR_EQ);
        e1->k = expkind.VJMP;
    }

    /// <summary>
    /// Apply prefix operation 'op' to expression 'e'.
    /// </summary>
    private static void luaK_prefix(FuncState* fs, UnOpr opr, expdesc* e, int line)
    {
        expdesc ef = new()
        {
            k = expkind.VKINT,
            u = default,
            t = NO_JUMP,
            f = NO_JUMP,
        };
        luaK_dischargevars(fs, e);
        switch (opr)
        {
            case UnOpr.MINUS:
            case UnOpr.BNOT: // use 'ef' as fake 2nd operand
                if (constfolding(fs, (int)(opr + LUA_OPUNM), e, &ef))
                {
                    break;
                }

                goto case UnOpr.LEN;

            case UnOpr.LEN:
                codeunexpval(fs, unopr2op(opr), e, line);
                break;

            case UnOpr.NOT:
                codenot(fs, e);
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Process 1st operand 'v' of binary operation 'op' before reading
    /// 2nd operand.
    /// </summary>
    private static void luaK_infix(FuncState* fs, BinOpr op, expdesc* v)
    {
        luaK_dischargevars(fs, v);
        switch (op)
        {
            case BinOpr.OPR_AND:
                luaK_goiftrue(fs, v); // go ahead only if 'v' is true
                break;
            
            case BinOpr.OPR_OR:
                luaK_goiffalse(fs, v); // go ahead only if 'v' is false
                break;
            
            case BinOpr.OPR_CONCAT:
                luaK_exp2nextreg(fs, v); // operand must be on the stack
                break;
            
            case BinOpr.OPR_ADD:
            case BinOpr.OPR_SUB:
            case BinOpr.OPR_MUL:
            case BinOpr.OPR_DIV:
            case BinOpr.OPR_IDIV:
            case BinOpr.OPR_MOD:
            case BinOpr.OPR_POW:
            case BinOpr.OPR_BAND:
            case BinOpr.OPR_BOR:
            case BinOpr.OPR_BXOR:
            case BinOpr.OPR_SHL:
            case BinOpr.OPR_SHR:
                if (!tonumeral(v, null))
                {
                    luaK_exp2anyreg(fs, v);
                }

                // else keep numeral, which may be folded or used as an immediate
                // operand
                break;
            
            case BinOpr.OPR_EQ:
            case BinOpr.OPR_NE:
                if (!tonumeral(v, null))
                {
                    exp2RK(fs, v);
                }

                // else keep numeral, which may be an immediate operand
                break;
            
            case BinOpr.OPR_LT:
            case BinOpr.OPR_LE:
            case BinOpr.OPR_GT:
            case BinOpr.OPR_GE:
                {
                    int dummy;
                    bool dummy2;
                    if (!isSCnumber(v, &dummy, &dummy2))
                    {
                        luaK_exp2anyreg(fs, v);
                    }

                    // else keep numeral, which may be an immediate operand
                    break;
                }
            
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Create code for '(e1 .. e2)'.
    /// For '(e1 .. e2.1 .. e2.2)' (which is '(e1 .. (e2.1 .. e2.2))',
    /// because concatenation is right associative), merge both CONCATs.
    /// </summary>
    private static void codeconcat(FuncState* fs, expdesc* e1, expdesc* e2, int line)
    {
        uint* ie2 = previousinstruction(fs);
        if (GET_OPCODE(*ie2) == OpCode.Concat)
        {
            // is 'e2' a concatenation?
            int n = GETARG_B(*ie2); // # of elements concatenated in 'e2'
            Debug.Assert(e1->u.info + 1 == GETARG_A(*ie2));
            freeexp(fs, e2);
            SETARG_A(ref *ie2, e1->u.info); // correct first element ('e1')
            SETARG_B(ref *ie2, n + 1); // will concatenate one more element
        }
        else
        {
            // 'e2' is not a concatenation
            luaK_codeABC(fs, OpCode.Concat, e1->u.info, 2, 0); // new concat opcode
            freeexp(fs, e2);
            luaK_fixline(fs, line);
        }
    }

    /// <summary>
    /// Finalise code for binary operation, after reading 2nd operand.
    /// </summary>
    private static void luaK_posfix(FuncState* fs, BinOpr opr, expdesc* e1, expdesc* e2, int line)
    {
        luaK_dischargevars(fs, e2);
        if (foldbinop(opr) && constfolding(fs, (int)(opr + LUA_OPADD), e1, e2))
        {
            return; // done by folding
        }

        switch (opr)
        {
            case BinOpr.OPR_AND:
                Debug.Assert(e1->t == NO_JUMP); // list closed by 'luaK_infix'
                luaK_concat(fs, &e2->f, e1->f);
                *e1 = *e2;
                break;

            case BinOpr.OPR_OR:
                Debug.Assert(e1->f == NO_JUMP); // list closed by 'luaK_infix'
                luaK_concat(fs, &e2->t, e1->t);
                *e1 = *e2;
                break;

            case BinOpr.OPR_CONCAT:
                // e1 .. e2
                luaK_exp2nextreg(fs, e2);
                codeconcat(fs, e1, e2, line);
                break;

            case BinOpr.OPR_ADD:
            case BinOpr.OPR_MUL:
                codecommutative(fs, opr, e1, e2, line);
                break;

            case BinOpr.OPR_SUB:
                if (finishbinexpneg(fs, e1, e2, OpCode.AddI, line, TMS.SUB))
                {
                    break; // coded as (r1 + -I)
                }

                goto case BinOpr.OPR_DIV;

            case BinOpr.OPR_DIV:
            case BinOpr.OPR_IDIV:
            case BinOpr.OPR_MOD:
            case BinOpr.OPR_POW:
                codearith(fs, opr, e1, e2, false, line);
                break;

            case BinOpr.OPR_BAND:
            case BinOpr.OPR_BOR:
            case BinOpr.OPR_BXOR:
                codebitwise(fs, opr, e1, e2, line);
                break;

            case BinOpr.OPR_SHL:
                if (isSCint(e1)) {
                    swapexps(e1, e2);
                    codebini(fs, OpCode.ShlI, e1, e2, true, line, TMS.SHL); // I << r2
                }
                else if (finishbinexpneg(fs, e1, e2, OpCode.ShrI, line, TMS.SHL)) {
                    ; // coded as (r1 >> -I)
                }
                else // regular case (two registers)
                {
                    codebinexpval(fs, opr, e1, e2, line);
                }

                break;
            
            case BinOpr.OPR_SHR:
                if (isSCint(e2))
                {
                    codebini(fs, OpCode.ShrI, e1, e2, false, line, TMS.SHR); // r1 >> I
                }
                else // regular case (two registers)
                {
                    codebinexpval(fs, opr, e1, e2, line);
                }

                break;
            
            case BinOpr.OPR_EQ:
            case BinOpr.OPR_NE:
                codeeq(fs, opr, e1, e2);
                break;

            case BinOpr.OPR_GT:
            case BinOpr.OPR_GE:
                // '(a > b)' <=> '(b < a)';  '(a >= b)' <=> '(b <= a)'
                swapexps(e1, e2);
                opr = opr - BinOpr.OPR_GT + BinOpr.OPR_LT;
                goto case BinOpr.OPR_LT;

            case BinOpr.OPR_LT:
            case BinOpr.OPR_LE:
                codeorder(fs, opr, e1, e2);
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Change line information associated with current position, by removing
    /// previous info and adding it again with new line.
    /// </summary>
    private static void luaK_fixline(FuncState* fs, int line)
    {
        removelastlineinfo(fs);
        savelineinfo(fs, fs->f, line);
    }

    private static void luaK_settablesize(FuncState* fs, int pc, int ra, int asize, int hsize)
    {
        uint* inst = &fs->f->code[pc];
        int extra = asize / (MAXARG_vC + 1); // higher bits of array size
        int rc = asize % (MAXARG_vC + 1); // lower bits of array size
        bool k = extra > 0; // true iff needs extra argument
        hsize = hsize != 0 ? luaO_ceillog2((uint)hsize) + 1 : 0;
        *inst = CREATE_vABCk(OpCode.NewTable, ra, hsize, rc, k);
        *(inst + 1) = CREATE_Ax(OpCode.ExtraArg, extra);
    }

    /// <summary>
    /// Emit a SETLIST instruction.
    /// 'base' is register that keeps table;
    /// 'nelems' is #table plus those to be stored now;
    /// 'tostore' is number of values (in registers 'base + 1',...) to add to
    /// table (or LUA_MULTRET to add up to stack top).
    /// </summary>
    private static void luaK_setlist(FuncState* fs, int @base, int nelems, int tostore)
    {
        Debug.Assert(tostore != 0);
        if (tostore == LUA_MULTRET)
        {
            tostore = 0;
        }

        if (nelems <= MAXARG_vC)
        {
            luaK_codevABCk(fs, OpCode.SetList, @base, tostore, nelems, false);
        }
        else
        {
            int extra = nelems / (MAXARG_vC + 1);
            nelems %= MAXARG_vC + 1;
            luaK_codevABCk(fs, OpCode.SetList, @base, tostore, nelems, true);
            codeextraarg(fs, extra);
        }

        fs->freereg = (byte)(@base + 1); // free registers with list values
    }

    /// <summary>
    /// return the final target of a jump (skipping jumps to jumps)
    /// </summary>
    private static int finaltarget(uint* code, int i)
    {
        for (int count = 0; count < 100; count++)
        {
            // avoid infinite loops
            uint pc = code[i];
            if (GET_OPCODE(pc) != OpCode.Jmp)
            {
                break;
            }

            i += GETARG_sJ(pc) + 1;
        }

        return i;
    }

    /// <summary>
    /// Do a final pass over the code of a function, doing small peephole
    /// optimisations and adjustments.
    /// </summary>
    private static void luaK_finish(FuncState* fs)
    {
        Proto* p = fs->f;
        if ((p->flag & PF_VATAB) != 0) // will it use a vararg table?
        {
            p->flag &= unchecked((byte)~PF_VAHID); // then it will not use hidden args.
        }

        for (int i = 0; i < fs->pc; i++)
        {
            uint* pc = &p->code[i];
            // avoid "not used" warnings when assert is off (for 'onelua.c')
            Debug.Assert(i == 0 || luaP_isOT(*(pc - 1)) == luaP_isIT(*pc));
            switch (GET_OPCODE(*pc))
            {
                case OpCode.Return0:
                case OpCode.Return1:
                    if (!(fs->needclose || (p->flag & PF_VAHID) != 0))
                    {
                        break; // no extra work
                    }

                    // else use OP_RETURN to do the extra work
                    SET_OPCODE(ref *pc, OpCode.Return);
                    goto case OpCode.Return;

                case OpCode.Return:
                case OpCode.TailCall:
                    if (fs->needclose)
                    {
                        SETARG_k(ref *pc, true); // signal that it needs to close
                    }

                    if ((p->flag & PF_VAHID) != 0) // does it use hidden arguments?
                    {
                        SETARG_C(ref *pc, p->numparams + 1); // signal that
                    }

                    break;

                case OpCode.GetVArg:
                    if ((p->flag & PF_VATAB) != 0) // function has a vararg table?
                    {
                        SET_OPCODE(ref *pc, OpCode.GetTable); // must get vararg there
                    }

                    break;

                case OpCode.VarArg:
                    if ((p->flag & PF_VATAB) != 0) // function has a vararg table?
                    {
                        SETARG_k(ref *pc, true); // must get vararg there
                    }

                    break;

                case OpCode.Jmp:
                    {
                        // to optimise jumps to jumps
                        int target = finaltarget(p->code, i);
                        fixjump(fs, i, target); // jump directly to final target
                        break;
                    }
            }
        }
    }
}
