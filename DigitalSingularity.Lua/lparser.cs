namespace DigitalSingularity.Lua;

using System.Runtime.InteropServices;

public static unsafe partial class Lua
{
    /*
     ** $Id: lparser.h $
     ** Lua Parser
     ** See Copyright Notice in lua.h
     */

    /*
    ** Expression and variable descriptor.
    ** Code generation for variables and expressions can be delayed to allow
    ** optimisations; An 'expdesc' structure describes a potentially-delayed
    ** variable/expression. It has a description of its "main" value plus a
    ** list of conditional jumps that can also produce its value (generated
    ** by short-circuit operators 'and'/'or').
    */

    /* kinds of variables/expressions */
    private enum expkind
    {
        VVOID, /* when 'expdesc' describes the last expression of a list,
                   this kind means an empty list (so, no expression) */
        VNIL, /* constant nil */
        VTRUE, /* constant true */
        VFALSE, /* constant false */
        VK, /* constant in 'k'; info = index of constant in 'k' */
        VKFLT, /* floating constant; nval = numerical float value */
        VKINT, /* integer constant; ival = numerical integer value */
        VKSTR, /* string constant; strval = TString address;
                   (string is fixed by the scanner) */
        VNONRELOC, /* expression has its value in a fixed register;
                       info = result register */
        VLOCAL, /* local variable; var.ridx = register index;
                    var.vidx = relative index in 'actvar.arr'  */
        VVARGVAR, /* vararg parameter; var.ridx = register index;
                    var.vidx = relative index in 'actvar.arr'  */
        VGLOBAL, /* global variable;
                     info = relative index in 'actvar.arr' (or -1 for
                            implicit declaration) */
        VUPVAL, /* upvalue variable; info = index of upvalue in 'upvalues' */
        VCONST, /* compile-time <const> variable;
                    info = absolute index in 'actvar.arr'  */
        VINDEXED, /* indexed variable;
                      ind.t = table register;
                      ind.idx = key's R index;
                      ind.ro = true if it represents a read-only global;
                      ind.keystr = if key is a string, index in 'k' of that string;
                                   -1 if key is not a string */
        VVARGIND, /* indexed vararg parameter;
                      ind.* as in VINDEXED */
        VINDEXUP, /* indexed upvalue;
                      ind.idx = key's K index;
                      ind.* as in VINDEXED */
        VINDEXI, /* indexed variable with constant integer;
                      ind.t = table register;
                      ind.idx = key's value */
        VINDEXSTR, /* indexed variable with literal string;
                      ind.idx = key's K index;
                      ind.* as in VINDEXED */
        VJMP, /* expression is a test/comparison;
                  info = pc of corresponding jump instruction */
        VRELOC, /* expression can put result in any register;
                    info = instruction pc */
        VCALL, /* expression is a function call; info = instruction pc */
        VVARARG, /* vararg expression; info = instruction pc */
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
            public short idx; /* index (R or "long" K) */
            public byte t; /* table (register or upvalue) */
            public bool ro; /* true if variable is read-only */
            public int keystr; /* index in 'k' of string key, or -1 if not a string */
        }

        public struct Var
        {
            public byte ridx; /* register holding the variable */
            public short vidx; /* index in 'actvar.arr' */
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct U
        {
            [FieldOffset(0)] public long ival; /* for VKINT */
            [FieldOffset(0)] public double nval; /* for VKFLT */
            [FieldOffset(0)] public TString *strval;  /* for VKSTR */
            [FieldOffset(0)] public int info;  /* for generic use */
            [FieldOffset(0)] public Ind ind; /* for indexed variables */
            [FieldOffset(0)] public Var var; /* for local variables */
        }

        public expkind k;
        public U u;
        public int t; /* patch list of 'exit when true' */
        public int f; /* patch list of 'exit when false' */
    }

    /* kinds of variables */
    private const int VDKREG = 0;   /* regular local */
    private const int RDKCONST = 1;   /* local constant */
    private const int RDKVAVAR = 2;   /* vararg parameter */
    private const int RDKTOCLOSE = 3;   /* to-be-closed */
    private const int RDKCTC = 4;   /* local compile-time constant */
    private const int GDKREG = 5;   /* regular global */
    private const int GDKCONST = 6;   /* global constant */

    /* variables that live in registers */
    private static bool varinreg(Vardesc* v)
    {
        return v->vd.kind <= RDKTOCLOSE;
    }

    /* test for global variables */
    private static bool varglobal(Vardesc* v)
    {
        return v->vd.kind >= GDKREG;
    }

    /* description of an active variable */
    [StructLayout(LayoutKind.Explicit)]
    private struct Vardesc
    {
        public struct Vd
        {
            public Value value_;
            public byte tt_;
            public byte kind;
            public byte ridx; /* register holding the variable */
            public short pidx; /* index of the variable in the Proto's 'locvars' array */
            public TString* name; /* variable name */
        }

        [FieldOffset(0)] public Vd vd;
        [FieldOffset(0)] public TValue k; /* constant value (if any) */
    }

    /* description of pending goto statements and label statements */
    private struct Labeldesc
    {
        public TString* name; /* label identifier */
        public int pc; /* position in code */
        public int line; /* line where it appeared */
        public short nactvar; /* number of active variables in that position */
        public bool close; /* true for goto that escapes upvalues */
    }

    /* list of labels or gotos */
    private struct Labellist
    {
        public Labeldesc* arr; /* array */
        public int n; /* number of entries in use */
        public int size; /* array size */
    }

    /* dynamic structures used by the parser */
    private struct Dyndata
    {
        public struct ActVar
        {
            /* list of all active local variables */
            public Vardesc* arr;
            public int n;
            public int size;
        }

        public ActVar actvar;
        public Labellist gt; /* list of pending gotos */
        public Labellist label; /* list of active labels */
    }

    /* state needed to generate code for a given function */
    private struct FuncState
    {
        public Proto* f; /* current function header */
        public FuncState* prev; /* enclosing function */
        public LexState* ls; /* lexical state */
        public BlockCnt* bl;  /* chain of current blocks */
        public Table* kcache; /* cache for reusing constants */
        public int pc; /* next position to code (equivalent to 'ncode') */
        public int lasttarget; /* 'label' of last 'jump label' */
        public int previousline; /* last line that was saved in 'lineinfo' */
        public int nk; /* number of elements in 'k' */
        public int np; /* number of elements in 'p' */
        public int nabslineinfo; /* number of elements in 'abslineinfo' */
        public int firstlocal; /* index of first local var (in Dyndata array) */
        public int firstlabel; /* index of first label (in 'dyd->label->arr') */
        public short ndebugvars; /* number of elements in 'f->locvars' */
        public short nactvar; /* number of active variable declarations */
        public byte nups; /* number of upvalues */
        public byte freereg; /* first free register */
        public byte iwthabs; /* instructions issued since last absolute line info */
        public bool needclose; /* function needs to close upvalues when returning */
    }

    private static partial byte luaY_nvarstack(FuncState* fs);

    private static partial void luaY_checklimit(FuncState* fs, int v, int l, string what);

    private static partial LClosure* luaY_parser(
        lua_State* L,
        Zio* z,
        Mbuffer* buff,
        Dyndata* dyd,
        string name,
        int firstchar);
}
