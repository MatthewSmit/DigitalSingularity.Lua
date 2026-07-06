namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    /*
    ** $Id: lopcodes.h $
    ** Opcodes for Lua virtual machine
    ** See Copyright Notice in lua.h
    */

    /*===========================================================================
      We assume that instructions are unsigned 32-bit integers.
      All instructions have an opcode in the first 7 bits.
      Instructions can have the following formats:

            3 3 2 2 2 2 2 2 2 2 2 2 1 1 1 1 1 1 1 1 1 1 0 0 0 0 0 0 0 0 0 0
            1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0
    iABC          C(8)     |      B(8)     |k|     A(8)      |   Op(7)     |
    ivABC         vC(10)     |     vB(6)   |k|     A(8)      |   Op(7)     |
    iABx                Bx(17)               |     A(8)      |   Op(7)     |
    iAsBx              sBx (signed)(17)      |     A(8)      |   Op(7)     |
    iAx                           Ax(25)                     |   Op(7)     |
    isJ                           sJ (signed)(25)            |   Op(7)     |

      ('v' stands for "variant", 's' for "signed", 'x' for "extended".)
      A signed argument is represented in excess K: The represented value is
      the written unsigned value minus K, where K is half (rounded down) the
      maximum value for the corresponding unsigned argument.
    ===========================================================================*/

    /* basic instruction formats */
    private enum OpMode
    {
        iABC,
        ivABC,
        iABx,
        iAsBx,
        iAx,
        isJ,
    }

    /*
    ** size and position of opcode arguments.
    */
    private const int SIZE_C = 8;
    private const int SIZE_vC = 10;
    private const int SIZE_B = 8;
    private const int SIZE_vB = 6;
    private const int SIZE_Bx = SIZE_C + SIZE_B + 1;
    private const int SIZE_A = 8;
    private const int SIZE_Ax = SIZE_Bx + SIZE_A;
    private const int SIZE_sJ = SIZE_Bx + SIZE_A;

    private const int SIZE_OP = 7;

    private const int POS_OP = 0;

    private const int POS_A = POS_OP + SIZE_OP;
    private const int POS_k = POS_A + SIZE_A;
    private const int POS_B = POS_k + 1;
    private const int POS_vB = POS_k + 1;
    private const int POS_C = POS_B + SIZE_B;
    private const int POS_vC = POS_vB + SIZE_vB;

    private const int POS_Bx = POS_k;

    private const int POS_Ax = POS_A;

    private const int POS_sJ = POS_A;


    /*
    ** limits for opcode arguments.
    ** we use (signed) 'int' to manipulate most arguments,
    ** so they must fit in ints.
    */

    private const int MAXARG_Bx = (1 << SIZE_Bx) - 1;

    private const int OFFSET_sBx = MAXARG_Bx >> 1;         /* 'sBx' is signed */

    private const int MAXARG_Ax = (1 << SIZE_Ax) - 1;
    
    private const int MAXARG_sJ = (1 << SIZE_sJ) - 1;

    private const int OFFSET_sJ = MAXARG_sJ >> 1;

    private const int MAXARG_A = (1 << SIZE_A) - 1;
    private const int MAXARG_B = (1 << SIZE_B) - 1;
    private const int MAXARG_vB = (1 << SIZE_vB) - 1;
    private const int MAXARG_C = (1 << SIZE_C) - 1;
    internal const int MAXARG_vC = (1 << SIZE_vC) - 1;
    private const int OFFSET_sC = MAXARG_C >> 1;

    private static int int2sC(int i)
    {
        return i + OFFSET_sC;
    }

    private static uint int2sC(uint i)
    {
        return i + OFFSET_sC;
    }

    private static uint sC2int(uint i)
    {
        return i - OFFSET_sC;
    }

    /* creates a mask with 'n' 1 bits at position 'p' */
    private static uint MASK1(int n, int p)
    {
        return ~(~0u << n) << p;
    }

    /* creates a mask with 'n' 0 bits at position 'p' */
    private static uint MASK0(int n, int p)
    {
        return ~MASK1(n, p);
    }

    /*
    ** the following macros help to manipulate instructions
    */

    internal static OpCode GET_OPCODE(uint i)
    {
        return (OpCode)(i >> POS_OP & MASK1(SIZE_OP, 0));
    }

    private static void SET_OPCODE(ref uint i, OpCode o)
    {
        i = i & MASK0(SIZE_OP, POS_OP) |
            (uint)o << POS_OP & MASK1(SIZE_OP, POS_OP);
    }

    private static bool checkopm(uint i, OpMode m)
    {
        return getOpMode(GET_OPCODE(i)) == m;
    }

    private static int getarg(uint i, int pos, int size)
    {
        return (int)(i >> pos & MASK1(size, 0));
    }

    private static void setarg(ref uint i, int v, int pos, int size)
    {
        i = i & MASK0(size, pos) |
            (uint)v << pos &
            MASK1(size, pos);
    }

    private static int GETARG_A(uint i)
    {
        return getarg(i, POS_A, SIZE_A);
    }

    private static void SETARG_A(ref uint i, int v)
    {
        setarg(ref i, v, POS_A, SIZE_A);
    }

    private static int GETARG_B(uint i)
    {
        Debug.Assert(checkopm(i, OpMode.iABC));
        return getarg(i, POS_B, SIZE_B);
    }

    private static int GETARG_vB(uint i)
    {
        Debug.Assert(checkopm(i, OpMode.ivABC));
        return getarg(i, POS_vB, SIZE_vB);
    }

    private static uint GETARG_sB(uint i)
    {
        return sC2int((uint)GETARG_B(i));
    }

    private static void SETARG_B(ref uint i, int v)
    {
        setarg(ref i, v, POS_B, SIZE_B);
    }

    private static void SETARG_vB(ref uint i, int v)
    {
        setarg(ref i, v, POS_vB, SIZE_vB);
    }

    private static uint GETARG_C(uint i)
    {
        Debug.Assert(checkopm(i, OpMode.iABC));
        return (uint)getarg(i, POS_C, SIZE_C);
    }

    private static int GETARG_vC(uint i)
    {
        Debug.Assert(checkopm(i, OpMode.ivABC));
        return getarg(i, POS_vC, SIZE_vC);
    }

    private static uint GETARG_sC(uint i)
    {
        return sC2int(GETARG_C(i));
    }

    private static void SETARG_C(ref uint i, int v)
    {
        setarg(ref i, v, POS_C, SIZE_C);
    }

    private static void SETARG_vC(ref uint i, int v)
    {
        setarg(ref i, v, POS_vC, SIZE_vC);
    }

    private static bool TESTARG_k(uint i)
    {
        return (i & 1u << POS_k) != 0;
    }

    private static bool GETARG_k(uint i)
    {
        return getarg(i, POS_k, 1) != 0;
    }

    private static void SETARG_k(ref uint i, bool v)
    {
        setarg(ref i, v ? 1 : 0, POS_k, 1);
    }

    private static int GETARG_Bx(uint i)
    {
        Debug.Assert(checkopm(i, OpMode.iABx));
        return getarg(i, POS_Bx, SIZE_Bx);
    }

    private static void SETARG_Bx(ref uint i, int v)
    {
        setarg(ref i, v, POS_Bx, SIZE_Bx);
    }

    private static int GETARG_Ax(uint i)
    {
        Debug.Assert(checkopm(i, OpMode.iAx));
        return getarg(i, POS_Ax, SIZE_Ax);
    }

    private static void SETARG_Ax(ref uint i, int v)
    {
        setarg(ref i, v, POS_Ax, SIZE_Ax);
    }

    private static int GETARG_sBx(uint i)
    {
        Debug.Assert(checkopm(i, OpMode.iAsBx));
        return getarg(i, POS_Bx, SIZE_Bx) - OFFSET_sBx;
    }

    private static void SETARG_sBx(ref uint i, int b)
    {
        SETARG_Bx(ref i, b + OFFSET_sBx);
    }

    private static int GETARG_sJ(uint i)
    {
        Debug.Assert(checkopm(i, OpMode.isJ));
        return getarg(i, POS_sJ, SIZE_sJ) - OFFSET_sJ;
    }

    private static void SETARG_sJ(ref uint i, int j)
    {
        setarg(ref i, j + OFFSET_sJ, POS_sJ, SIZE_sJ);
    }

    internal static uint CREATE_ABCk(OpCode o, int a, int b, int c, bool k)
    {
        return (uint)o << POS_OP |
               (uint)a << POS_A |
               (uint)b << POS_B |
               (uint)c << POS_C |
               (uint)(k ? 1 : 0) << POS_k;
    }

    private static uint CREATE_vABCk(OpCode o, int a, int b, int c, bool k)
    {
        return (uint)o << POS_OP |
               (uint)a << POS_A |
               (uint)b << POS_vB |
               (uint)c << POS_vC |
               (uint)(k ? 1 : 0) << POS_k;
    }

    private static uint CREATE_ABx(OpCode o, int a, int bc)
    {
        return (uint)o << POS_OP | (uint)a << POS_A | (uint)bc << POS_Bx;
    }

    private static uint CREATE_Ax(OpCode o, int a)
    {
        return (uint)o << POS_OP | (uint)a << POS_Ax;
    }

    private static uint CREATE_sJ(OpCode o, int j, int k)
    {
        return (uint)o << POS_OP | (uint)j << POS_sJ | (uint)k << POS_k;
    }

    private const int MAXINDEXRK =
#if LUA_TEST
        1;
#else
        MAXARG_B;
#endif
    
    /*
    ** Maximum size for the stack of a Lua function. It must fit in 8 bits.
    ** The highest valid register is one less than this value.
    */
    private const int MAX_FSTACK = MAXARG_A;

    /*
    ** Invalid register (one more than last valid register).
    */
    private const int NO_REG = MAX_FSTACK;
    
    /*
    ** R[x] - register
    ** K[x] - constant (in constant table)
    ** RK(x) == if k(i) then K[x] else R[x]
    */

    /*
    ** Grep "ORDER OP" if you change this enum.
    ** See "Notes" below for more information about some instructions.
    */

    internal enum OpCode
    {
        /*----------------------------------------------------------------------
          name		args	description
        ------------------------------------------------------------------------*/
        OP_MOVE, /*	A B	R[A] := R[B]					*/
        OP_LOADI, /*	A sBx	R[A] := sBx					*/
        OP_LOADF, /*	A sBx	R[A] := (double)sBx				*/
        OP_LOADK, /*	A Bx	R[A] := K[Bx]					*/
        OP_LOADKX, /*	A	R[A] := K[extra arg]				*/
        OP_LOADFALSE, /*	A	R[A] := false					*/
        OP_LFALSESKIP, /*A	R[A] := false; pc++				*/
        OP_LOADTRUE, /*	A	R[A] := true					*/
        OP_LOADNIL, /*	A B	R[A], R[A+1], ..., R[A+B] := nil		*/
        OP_GETUPVAL, /*	A B	R[A] := UpValue[B]				*/
        OP_SETUPVAL, /*	A B	UpValue[B] := R[A]				*/

        OP_GETTABUP, /*	A B C	R[A] := UpValue[B][K[C]:shortstring]		*/
        OP_GETTABLE, /*	A B C	R[A] := R[B][R[C]]				*/
        OP_GETI, /*	A B C	R[A] := R[B][C]					*/
        OP_GETFIELD, /*	A B C	R[A] := R[B][K[C]:shortstring]			*/

        OP_SETTABUP, /*	A B C	UpValue[A][K[B]:shortstring] := RK(C)		*/
        OP_SETTABLE, /*	A B C	R[A][R[B]] := RK(C)				*/
        OP_SETI, /*	A B C	R[A][B] := RK(C)				*/
        OP_SETFIELD, /*	A B C	R[A][K[B]:shortstring] := RK(C)			*/

        OP_NEWTABLE, /*	A vB vC k	R[A] := {}				*/

        OP_SELF, /*	A B C	R[A+1] := R[B]; R[A] := R[B][K[C]:shortstring]	*/

        OP_ADDI, /*	A B sC	R[A] := R[B] + sC				*/

        OP_ADDK, /*	A B C	R[A] := R[B] + K[C]:number			*/
        OP_SUBK, /*	A B C	R[A] := R[B] - K[C]:number			*/
        OP_MULK, /*	A B C	R[A] := R[B] * K[C]:number			*/
        OP_MODK, /*	A B C	R[A] := R[B] % K[C]:number			*/
        OP_POWK, /*	A B C	R[A] := R[B] ^ K[C]:number			*/
        OP_DIVK, /*	A B C	R[A] := R[B] / K[C]:number			*/
        OP_IDIVK, /*	A B C	R[A] := R[B] // K[C]:number			*/

        OP_BANDK, /*	A B C	R[A] := R[B] & K[C]:integer			*/
        OP_BORK, /*	A B C	R[A] := R[B] | K[C]:integer			*/
        OP_BXORK, /*	A B C	R[A] := R[B] ~ K[C]:integer			*/

        OP_SHLI, /*	A B sC	R[A] := sC << R[B]				*/
        OP_SHRI, /*	A B sC	R[A] := R[B] >> sC				*/

        OP_ADD, /*	A B C	R[A] := R[B] + R[C]				*/
        OP_SUB, /*	A B C	R[A] := R[B] - R[C]				*/
        OP_MUL, /*	A B C	R[A] := R[B] * R[C]				*/
        OP_MOD, /*	A B C	R[A] := R[B] % R[C]				*/
        OP_POW, /*	A B C	R[A] := R[B] ^ R[C]				*/
        OP_DIV, /*	A B C	R[A] := R[B] / R[C]				*/
        OP_IDIV, /*	A B C	R[A] := R[B] // R[C]				*/

        OP_BAND, /*	A B C	R[A] := R[B] & R[C]				*/
        OP_BOR, /*	A B C	R[A] := R[B] | R[C]				*/
        OP_BXOR, /*	A B C	R[A] := R[B] ~ R[C]				*/
        OP_SHL, /*	A B C	R[A] := R[B] << R[C]				*/
        OP_SHR, /*	A B C	R[A] := R[B] >> R[C]				*/

        OP_MMBIN, /*	A B C	call C metamethod over R[A] and R[B]		*/
        OP_MMBINI, /*	A sB C k	call C metamethod over R[A] and sB	*/
        OP_MMBINK, /*	A B C k		call C metamethod over R[A] and K[B]	*/

        OP_UNM, /*	A B	R[A] := -R[B]					*/
        OP_BNOT, /*	A B	R[A] := ~R[B]					*/
        OP_NOT, /*	A B	R[A] := not R[B]				*/
        OP_LEN, /*	A B	R[A] := #R[B] (length operator)			*/

        OP_CONCAT, /*	A B	R[A] := R[A].. ... ..R[A + B - 1]		*/

        OP_CLOSE, /*	A	close all upvalues >= R[A]			*/
        OP_TBC, /*	A	mark variable A "to be closed"			*/
        OP_JMP, /*	sJ	pc += sJ					*/
        OP_EQ, /*	A B k	if ((R[A] == R[B]) ~= k) then pc++		*/
        OP_LT, /*	A B k	if ((R[A] <  R[B]) ~= k) then pc++		*/
        OP_LE, /*	A B k	if ((R[A] <= R[B]) ~= k) then pc++		*/

        OP_EQK, /*	A B k	if ((R[A] == K[B]) ~= k) then pc++		*/
        OP_EQI, /*	A sB k	if ((R[A] == sB) ~= k) then pc++		*/
        OP_LTI, /*	A sB k	if ((R[A] < sB) ~= k) then pc++			*/
        OP_LEI, /*	A sB k	if ((R[A] <= sB) ~= k) then pc++		*/
        OP_GTI, /*	A sB k	if ((R[A] > sB) ~= k) then pc++			*/
        OP_GEI, /*	A sB k	if ((R[A] >= sB) ~= k) then pc++		*/

        OP_TEST, /*	A k	if (not R[A] == k) then pc++			*/
        OP_TESTSET, /*	A B k	if (not R[B] == k) then pc++ else R[A] := R[B]  */

        OP_CALL, /*	A B C	R[A], ... ,R[A+C-2] := R[A](R[A+1], ... ,R[A+B-1]) */
        OP_TAILCALL, /*	A B C k	return R[A](R[A+1], ... ,R[A+B-1])		*/

        OP_RETURN, /*	A B C k	return R[A], ... ,R[A+B-2]			*/
        OP_RETURN0, /*		return						*/
        OP_RETURN1, /*	A	return R[A]					*/

        OP_FORLOOP, /*	A Bx	update counters; if loop continues then pc-=Bx; */
        OP_FORPREP, /*	A Bx	<check values and prepare counters>;
                                if not to run then pc+=Bx+1;			*/

        OP_TFORPREP, /*	A Bx	create upvalue for R[A + 3]; pc+=Bx		*/
        OP_TFORCALL, /*	A C	R[A+4], ... ,R[A+3+C] := R[A](R[A+1], R[A+2]);	*/
        OP_TFORLOOP, /*	A Bx	if R[A+2] ~= nil then { R[A]=R[A+2]; pc -= Bx }	*/

        OP_SETLIST, /*	A vB vC k	R[A][vC+i] := R[A+i], 1 <= i <= vB	*/

        OP_CLOSURE, /*	A Bx	R[A] := closure(KPROTO[Bx])			*/

        OP_VARARG, /*	A B C k	R[A], ..., R[A+C-2] = varargs  			*/

        OP_GETVARG, /* A B C	R[A] := R[B][R[C]], R[B] is vararg parameter    */

        OP_ERRNNIL, /*	A Bx	raise error if R[A] ~= nil (K[Bx - 1] is global name)*/

        OP_VARARGPREP, /* 	(adjust varargs)				*/

        OP_EXTRAARG, /*	Ax	extra (larger) argument for previous opcode	*/
    }

    private const int NUM_OPCODES = (int)OpCode.OP_EXTRAARG + 1;

    /*===========================================================================
      Notes:

      (*) Opcode OP_LFALSESKIP is used to convert a condition to a boolean
      value, in a code equivalent to (not cond ? false : true).  (It
      produces false and skips the next instruction producing true.)

      (*) Opcodes OP_MMBIN and variants follow each arithmetic and
      bitwise opcode. If the operation succeeds, it skips this next
      opcode. Otherwise, this opcode calls the corresponding metamethod.

      (*) Opcode OP_TESTSET is used in short-circuit expressions that need
      both to jump and to produce a value, such as (a = b or c).

      (*) In OP_CALL, if (B == 0) then B = top - A. If (C == 0), then
      'top' is set to last_result+1, so next open instruction (OP_CALL,
      OP_RETURN*, OP_SETLIST) may use 'top'.

      (*) In OP_VARARG, if (C == 0) then use actual number of varargs and
      set top (like in OP_CALL with C == 0). 'k' means function has a
      vararg table, which is in R[B].

      (*) In OP_RETURN, if (B == 0) then return up to 'top'.

      (*) In OP_LOADKX and OP_NEWTABLE, the next instruction is always
      OP_EXTRAARG.

      (*) In OP_SETLIST, if (B == 0) then real B = 'top'; if k, then
      real C = EXTRAARG _ C (the bits of EXTRAARG concatenated with the
      bits of C).

      (*) In OP_NEWTABLE, vB is log2 of the hash size (which is always a
      power of 2) plus 1, or zero for size zero. If not k, the array size
      is vC. Otherwise, the array size is EXTRAARG _ vC.

      (*) In OP_ERRNNIL, (Bx == 0) means index of global name doesn't
      fit in Bx. (So, that name is not available for the error message.)

      (*) For comparisons, k specifies what condition the test should accept
      (true or false).

      (*) In OP_MMBINI/OP_MMBINK, k means the arguments were flipped
      (the constant is the first operand).

      (*) All comparison and test instructions assume that the instruction
      being skipped (pc++) is a jump.

      (*) In instructions OP_RETURN/OP_TAILCALL, 'k' specifies that the
      function builds upvalues, which may need to be closed. C > 0 means
      the function has hidden vararg arguments, so that its 'func' must be
      corrected before returning; in this case, (C - 1) is its number of
      fixed parameters.

      (*) In comparisons with an immediate operand, C signals whether the
      original operand was a float. (It must be corrected in case of
      metamethods.)

    ===========================================================================*/

    /*
    ** masks for instruction properties. The format is:
    ** bits 0-2: op mode
    ** bit 3: instruction set register A
    ** bit 4: operator is a test (next instruction must be a jump)
    ** bit 5: instruction uses 'L->top' set by previous instruction (when B == 0)
    ** bit 6: instruction sets 'L->top' for next instruction (when C == 0)
    ** bit 7: instruction is an MM instruction (call a metamethod)
    */

    /* ORDER OP */

    private static readonly byte[] luaP_opmodes =
    [
        /*     MM OT IT T  A  mode		   opcode  */
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_MOVE */,
        opmode(false, false, false, false, true, OpMode.iAsBx) /* OP_LOADI */,
        opmode(false, false, false, false, true, OpMode.iAsBx) /* OP_LOADF */,
        opmode(false, false, false, false, true, OpMode.iABx) /* OP_LOADK */,
        opmode(false, false, false, false, true, OpMode.iABx) /* OP_LOADKX */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_LOADFALSE */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_LFALSESKIP */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_LOADTRUE */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_LOADNIL */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_GETUPVAL */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_SETUPVAL */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_GETTABUP */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_GETTABLE */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_GETI */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_GETFIELD */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_SETTABUP */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_SETTABLE */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_SETI */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_SETFIELD */,
        opmode(false, false, false, false, true, OpMode.ivABC) /* OP_NEWTABLE */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_SELF */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_ADDI */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_ADDK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_SUBK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_MULK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_MODK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_POWK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_DIVK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_IDIVK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_BANDK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_BORK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_BXORK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_SHLI */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_SHRI */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_ADD */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_SUB */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_MUL */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_MOD */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_POW */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_DIV */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_IDIV */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_BAND */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_BOR */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_BXOR */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_SHL */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_SHR */,
        opmode(true, false, false, false, false, OpMode.iABC) /* OP_MMBIN */,
        opmode(true, false, false, false, false, OpMode.iABC) /* OP_MMBINI */,
        opmode(true, false, false, false, false, OpMode.iABC) /* OP_MMBINK */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_UNM */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_BNOT */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_NOT */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_LEN */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_CONCAT */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_CLOSE */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_TBC */,
        opmode(false, false, false, false, false, OpMode.isJ) /* OP_JMP */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_EQ */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_LT */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_LE */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_EQK */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_EQI */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_LTI */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_LEI */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_GTI */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_GEI */,
        opmode(false, false, false, true, false, OpMode.iABC) /* OP_TEST */,
        opmode(false, false, false, true, true, OpMode.iABC) /* OP_TESTSET */,
        opmode(false, true, true, false, true, OpMode.iABC) /* OP_CALL */,
        opmode(false, true, true, false, true, OpMode.iABC) /* OP_TAILCALL */,
        opmode(false, false, true, false, false, OpMode.iABC) /* OP_RETURN */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_RETURN0 */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_RETURN1 */,
        opmode(false, false, false, false, true, OpMode.iABx) /* OP_FORLOOP */,
        opmode(false, false, false, false, true, OpMode.iABx) /* OP_FORPREP */,
        opmode(false, false, false, false, false, OpMode.iABx) /* OP_TFORPREP */,
        opmode(false, false, false, false, false, OpMode.iABC) /* OP_TFORCALL */,
        opmode(false, false, false, false, true, OpMode.iABx) /* OP_TFORLOOP */,
        opmode(false, false, true, false, false, OpMode.ivABC) /* OP_SETLIST */,
        opmode(false, false, false, false, true, OpMode.iABx) /* OP_CLOSURE */,
        opmode(false, true, false, false, true, OpMode.iABC) /* OP_VARARG */,
        opmode(false, false, false, false, true, OpMode.iABC) /* OP_GETVARG */,
        opmode(false, false, false, false, false, OpMode.iABx) /* OP_ERRNNIL */,
        opmode(false, false, true, false, true, OpMode.iABC) /* OP_VARARGPREP */,
        opmode(false, false, false, false, false, OpMode.iAx), /* OP_EXTRAARG */
    ];

    private static OpMode getOpMode(OpCode m)
    {
        return (OpMode)(luaP_opmodes[(int)m] & 7);
    }
    
    private static bool testAMode(OpMode m)
    {
        return (luaP_opmodes[(int)m] & (1 << 3)) != 0;
    }

    private static bool testTMode(OpMode m)
    {
        return (luaP_opmodes[(int)m] & (1 << 4)) != 0;
    }

    private static bool testITMode(OpCode m)
    {
        return (luaP_opmodes[(int)m] & (1 << 5)) != 0;
    }

    private static bool testOTMode(OpCode m)
    {
        return (luaP_opmodes[(int)m] & (1 << 6)) != 0;
    }

    private static bool testMMMode(OpMode m)
    {
        return (luaP_opmodes[(int)m] & (1 << 7)) != 0;
    }

    internal static partial bool luaP_isOT(uint i);

    internal static partial bool luaP_isIT(uint i);
}
