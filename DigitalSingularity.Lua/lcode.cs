namespace DigitalSingularity.Lua;

using System.Diagnostics.CodeAnalysis;

public static unsafe partial class Lua
{
    /*
     ** $Id: lcode.h $
     ** Code generator for Lua
     ** See Copyright Notice in lua.h
     */

    /*
     ** Marks the end of a patch list. It is an invalid value both as an absolute
     ** address, and as a list link (would link an element to itself).
     */
    private const int NO_JUMP = -1;

    /*
    ** grep "ORDER OPR" if you change these enums  (ORDER OP)
    */
    private enum BinOpr
    {
        /* arithmetic operators */
        OPR_ADD, OPR_SUB, OPR_MUL, OPR_MOD, OPR_POW,
        OPR_DIV, OPR_IDIV,

        /* bitwise operators */
        OPR_BAND, OPR_BOR, OPR_BXOR,
        OPR_SHL, OPR_SHR,

        /* string operator */
        OPR_CONCAT,

        /* comparison operators */
        OPR_EQ, OPR_LT, OPR_LE,
        OPR_NE, OPR_GT, OPR_GE,

        /* logical operators */
        OPR_AND, OPR_OR,
        OPR_NOBINOPR,
    }

    /* true if operation is foldable (that is, it is arithmetic or bitwise) */
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

    /* get (pointer to) instruction of given 'expdesc' */
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

    private static partial int luaK_code(FuncState* fs, uint i);

    private static partial int luaK_codeABx(FuncState* fs, OpCode o, int A, int Bx);

    private static partial int luaK_codeABCk(FuncState* fs, OpCode o, int A, int B, int C, bool k);

    private static partial int luaK_codevABCk(FuncState* fs, OpCode o, int A, int B, int C, bool k);

    private static partial bool luaK_exp2const(FuncState* fs, expdesc* e, TValue* v);

    private static partial void luaK_fixline(FuncState* fs, int line);

    private static partial void luaK_nil(FuncState* fs, int from, int n);

    private static partial void luaK_codecheckglobal(FuncState* fs, expdesc* var, int k, int line);

    private static partial void luaK_reserveregs(FuncState* fs, int n);

    private static partial void luaK_checkstack(FuncState* fs, int n);

    private static partial void luaK_int(FuncState* fs, int reg, long n);

    private static partial void luaK_vapar2local(FuncState* fs, expdesc* var);

    private static partial void luaK_dischargevars(FuncState* fs, expdesc* e);

    private static partial int luaK_exp2anyreg(FuncState* fs, expdesc* e);

    private static partial void luaK_exp2anyregup(FuncState* fs, expdesc* e);

    private static partial void luaK_exp2nextreg(FuncState* fs, expdesc* e);

    private static partial void luaK_exp2val(FuncState* fs, expdesc* e);

    private static partial void luaK_self(FuncState* fs, expdesc* e, expdesc* key);

    private static partial void luaK_indexed(FuncState* fs, expdesc* t, expdesc* k);

    private static partial void luaK_goiftrue(FuncState* fs, expdesc* e);

    private static partial void luaK_storevar(FuncState* fs, expdesc* var, expdesc* e);

    private static partial void luaK_setreturns(FuncState* fs, expdesc* e, int nresults);

    private static partial void luaK_setoneret(FuncState* fs, expdesc* e);

    private static partial int luaK_jump(FuncState* fs);

    private static partial void luaK_ret(FuncState* fs, int first, int nret);

    private static partial void luaK_patchlist(FuncState* fs, int list, int target);

    private static partial void luaK_patchtohere(FuncState* fs, int list);

    private static partial void luaK_concat(FuncState* fs, int* l1, int l2);

    private static partial int luaK_getlabel(FuncState* fs);

    private static partial void luaK_prefix(FuncState* fs, UnOpr op, expdesc* v, int line);

    private static partial void luaK_infix(FuncState* fs, BinOpr op, expdesc* v);

    private static partial void luaK_posfix(FuncState* fs, BinOpr op, expdesc* v1, expdesc* v2, int line);

    private static partial void luaK_settablesize(FuncState* fs, int pc, int ra, int asize, int hsize);

    private static partial void luaK_setlist(FuncState* fs, int @base, int nelems, int tostore);

    private static partial void luaK_finish(FuncState* fs);

    [DoesNotReturn]
    private static partial void luaK_semerror(LexState* ls, string fmt, params object[] args);
}
