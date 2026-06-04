namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    /*
     ** Extra types for collectable non-values
     */
    private const int LUA_TUPVAL = LUA_NUMTYPES; /* upvalues */
    private const int LUA_TPROTO = LUA_NUMTYPES + 1; /* function prototypes */
    private const int LUA_TDEADKEY = LUA_NUMTYPES + 2; /* removed keys in tables */

    /*
     ** tags for Tagged Values have the following use of bits:
     ** bits 0-3: actual tag (a LUA_T* constant)
     ** bits 4-5: variant bits
     ** bit 6: whether value is collectable
     */

    /* add variant bits to a type */
    private static byte makevariant(int t, int v)
    {
        return (byte)(t | v << 4);
    }

    /*
     ** Union of all Lua values
     */
    [StructLayout(LayoutKind.Explicit)]
    internal struct Value
    {
        [FieldOffset(0)] public GCObject* gc; /* collectable objects */
        [FieldOffset(0)] public void* p; /* light userdata */
        [FieldOffset(0)] public lua_CFunction f; /* light C functions */
        [FieldOffset(0)] public long i; /* integer numbers */
        [FieldOffset(0)] public double n; /* float numbers */
    }

    internal struct TValue
    {
        public Value value_;
        public byte tt_;
    }

    private static ref Value val_(TValue* o)
    {
        return ref o->value_;
    }

    /* raw type tag of a TValue */
    private static byte rawtt(TValue* o)
    {
        return o->tt_;
    }

    /* tag with no variants (bits 0-3) */
    private static byte novariant(byte t)
    {
        return (byte)(t & 0x0F);
    }

    /* type tag of a TValue (bits 0-3 for tags + variant bits 4-5) */

    private static byte withvariant(byte t)
    {
        return (byte)(t & 0x3F);
    }

    private static byte ttypetag(TValue* o)
    {
        return withvariant(rawtt(o));
    }

    /* type of a TValue */
    private static byte ttype(TValue* o)
    {
        return novariant(rawtt(o));
    }

    /* Macros to test type */
    private static bool checktag(TValue* o, byte t)
    {
        return rawtt(o) == t;
    }

    private static bool checktype(TValue* o, byte t)
    {
        return ttype(o) == t;
    }

    /* Macros for internal tests */

    /* collectable object has the same tag as the original value */
    private static bool righttt(TValue* obj)
    {
        return ttypetag(obj) == gcvalue(obj)->tt;
    }

    /*
     ** Any value being manipulated by the program either is non
     ** collectable, or the collectable object has the right tag
     ** and it is not dead. The option 'L == null' allows other
     ** macros using this one to be used where L is not available.
     */
    private static void checkliveness(lua_State* L, TValue* obj)
    {
        Debug.Assert(!iscollectable(obj) || righttt(obj) && (L == null || !isdead(G(L), gcvalue(obj))));
    }

    /* Macros to set values */

    /* set a value's tag */
    private static void settt_(TValue* o, byte t)
    {
        o->tt_ = t;
    }

    /* main macro to copy values (from 'obj2' to 'obj1') */
    private static void setobj(lua_State* L, TValue* obj1, TValue* obj2)
    {
        obj1->value_ = obj2->value_;
        settt_(obj1, obj2->tt_);
        checkliveness(L, obj1);
        Debug.Assert(!isnonstrictnil(obj1));
    }

    /*
     ** Different types of assignments, according to source and destination.
     ** (They are mostly equal now, but may be different in the future.)
     */

    /* from stack to stack */
    private static void setobjs2s(lua_State* L, StkId o1, StkId o2)
    {
        setobj(L, s2v(o1), s2v(o2));
    }

    /* to stack (not from same stack) */
    private static void setobj2s(lua_State* L, StkId o1, TValue* o2)
    {
        setobj(L, s2v(o1), o2);
    }

    /* from table to same table */
    private static void setobjt2t(lua_State* L, TValue* obj1, TValue* obj2)
    {
        setobj(L, obj1, obj2);
    }

    /* to new object */
    private static void setobj2n(lua_State* L, TValue* obj1, TValue* obj2)
    {
        setobj(L, obj1, obj2);
    }

    /* to table */
    private static void setobj2t(lua_State* L, TValue* obj1, TValue* obj2)
    {
        setobj(L, obj1, obj2);
    }

    /*
     ** Entries in a Lua stack. Field 'tbclist' forms a list of all
     ** to-be-closed variables active in this stack. Dummy entries are
     ** used when the distance between two tbc variables does not fit
     ** in an unsigned short. They are represented by delta==0, and
     ** their real delta is always the maximum value that fits in
     ** that field.
     */
    [StructLayout(LayoutKind.Explicit)]
    internal struct StackValue
    {
        public struct TbcList
        {
            public Value value_;
            public byte tt_;
            public ushort delta;
        }

        [FieldOffset(0)] public TValue val;
        [FieldOffset(0)] public TbcList tbclist;
    }

    /*
     ** When reallocating the stack, change all pointers to the stack into
     ** proper offsets.
     */
    internal struct StkIdRel
    {
        public StkId p; /* actual pointer */
        public nint offset; /* used while the stack is being reallocated */
    }

    /* convert a 'StackValue' to a 'TValue' */
    private static TValue* s2v(StkId o)
    {
        return &o->val;
    }

    /*
     ** {==================================================================
     ** Nil
     ** ===================================================================
     */

    /* Standard nil */
    private const byte LUA_VNIL = LUA_TNIL;

    /* Empty slot (which might be different from a slot containing nil) */
    private const byte LUA_VEMPTY = LUA_TNIL | 1 << 4;

    /* Value returned for a key not found in a table (absent key) */
    private const byte LUA_VABSTKEY = LUA_TNIL | 2 << 4;

    /* Special variant to signal that a fast get is accessing a non-table */
    private const byte LUA_VNOTABLE = LUA_TNIL | 3 << 4;

    /* macro to test for (any kind of) nil */
    private static bool ttisnil(TValue* v)
    {
        return checktype(v, LUA_TNIL);
    }

    /*
     ** Macro to test the result of a table access. Formally, it should
     ** distinguish between LUA_VEMPTY/LUA_VABSTKEY/LUA_VNOTABLE and
     ** other tags. As currently nil is equivalent to LUA_VEMPTY, it is
     ** simpler to just test whether the value is nil.
     */
    private static bool tagisempty(byte tag)
    {
        return novariant(tag) == LUA_TNIL;
    }

    /* macro to test for a standard nil */
    private static bool ttisstrictnil(TValue* o)
    {
        return checktag(o, LUA_VNIL);
    }

    private static void setnilvalue(TValue* obj)
    {
        settt_(obj, LUA_VNIL);
    }

    private static bool isabstkey(TValue* v)
    {
        return checktag(v, LUA_VABSTKEY);
    }

    /*
     ** macro to detect non-standard nils (used only in assertions)
     */
    private static bool isnonstrictnil(TValue* v)
    {
        return ttisnil(v) && !ttisstrictnil(v);
    }

    /*
     ** By default, entries with any kind of nil are considered empty.
     ** (In any definition, values associated with absent keys must also
     ** be accepted as empty.)
     */
    private static bool isempty(TValue* v)
    {
        return ttisnil(v);
    }

    /* mark an entry as empty */
    private static void setempty(TValue* v)
    {
        settt_(v, LUA_VEMPTY);
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Booleans
     ** ===================================================================
     */

    private const byte LUA_VFALSE = LUA_TBOOLEAN;
    private const byte LUA_VTRUE = LUA_TBOOLEAN | 1 << 4;

    private static bool ttisboolean(TValue* o)
    {
        return checktype(o, LUA_TBOOLEAN);
    }

    private static bool ttisfalse(TValue* o)
    {
        return checktag(o, LUA_VFALSE);
    }

    private static bool ttistrue(TValue* o)
    {
        return checktag(o, LUA_VTRUE);
    }

    private static bool l_isfalse(TValue* o)
    {
        return ttisfalse(o) || ttisnil(o);
    }

    private static bool tagisfalse(byte t)
    {
        return t == LUA_VFALSE || novariant(t) == LUA_TNIL;
    }

    private static void setbfvalue(TValue* obj)
    {
        settt_(obj, LUA_VFALSE);
    }

    private static void setbtvalue(TValue* obj)
    {
        settt_(obj, LUA_VTRUE);
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Threads
     ** ===================================================================
     */

    private const byte LUA_VTHREAD = LUA_TTHREAD;

    private static bool ttisthread(TValue* o)
    {
        return checktag(o, ctb(LUA_VTHREAD));
    }

    private static lua_State* thvalue(TValue* o)
    {
        Debug.Assert(ttisthread(o));
        return gco2th(val_(o).gc);
    }

    private static void setthvalue(lua_State* L, TValue* obj, lua_State* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(LUA_VTHREAD));
        checkliveness(L, obj);
    }

    private static void setthvalue2s(lua_State* L, StkId o, lua_State* t)
    {
        setthvalue(L, s2v(o), t);
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Collectable Objects
     ** ===================================================================
     */

    /* Common type for all collectable objects */
    internal struct GCObject
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
    }

    /* Bit mark for collectable types */
    private const byte BIT_ISCOLLECTABLE = 1 << 6;

    private static bool iscollectable(TValue* o)
    {
        return (rawtt(o) & BIT_ISCOLLECTABLE) != 0;
    }

    /* mark a tag as collectable */
    private static byte ctb(byte t)
    {
        return (byte)(t | BIT_ISCOLLECTABLE);
    }

    private static GCObject* gcvalue(TValue* o)
    {
        Debug.Assert(iscollectable(o));
        return val_(o).gc;
    }

    private static GCObject* gcvalueraw(in Value v)
    {
        return v.gc;
    }

    private static void setgcovalue(lua_State* L, TValue* obj, GCObject* x)
    {
        val_(obj).gc = x;
        settt_(obj, ctb(x->tt));
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Numbers
     ** ===================================================================
     */

    /* Variant tags for numbers */
    private const byte LUA_VNUMINT = LUA_TNUMBER;
    private const byte LUA_VNUMFLT = LUA_TNUMBER | 1 << 4;

    private static bool ttisnumber(TValue* o)
    {
        return checktype(o, LUA_TNUMBER);
    }

    private static bool ttisfloat(TValue* o)
    {
        return checktag(o, LUA_VNUMFLT);
    }

    private static bool ttisinteger(TValue* o)
    {
        return checktag(o, LUA_VNUMINT);
    }

    private static double nvalue(TValue* o)
    {
        Debug.Assert(ttisnumber(o));
        return ttisinteger(o) ? ivalue(o) : fltvalue(o);
    }

    private static double fltvalue(TValue* o)
    {
        Debug.Assert(ttisfloat(o));
        return val_(o).n;
    }

    private static long ivalue(TValue* o)
    {
        Debug.Assert(ttisinteger(o));
        return val_(o).i;
    }

    private static double fltvalueraw(in Value v)
    {
        return v.n;
    }

    private static long ivalueraw(in Value v)
    {
        return v.i;
    }

    private static void setfltvalue(TValue* obj, double x)
    {
        val_(obj).n = x;
        settt_(obj, LUA_VNUMFLT);
    }

    private static void chgfltvalue(TValue* obj, double x)
    {
        Debug.Assert(ttisfloat(obj));
        val_(obj).n = x;
    }

    private static void setivalue(TValue* obj, long x)
    {
        val_(obj).i = x;
        settt_(obj, LUA_VNUMINT);
    }

    private static void chgivalue(TValue* obj, long x)
    {
        Debug.Assert(ttisinteger(obj));
        val_(obj).i = x;
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Strings
     ** ===================================================================
     */

    /* Variant tags for strings */
    private const byte LUA_VSHRSTR = LUA_TSTRING; /* short strings */
    private const byte LUA_VLNGSTR = LUA_TSTRING | 1 << 4; /* long strings */
    private const byte LUA_VLNGSTR_C = LUA_VLNGSTR | BIT_ISCOLLECTABLE;

    private static bool ttisstring(TValue* o)
    {
        return checktype(o, LUA_TSTRING);
    }

    private static bool ttisshrstring(TValue* o)
    {
        return checktag(o, ctb(LUA_VSHRSTR));
    }

    private static bool ttislngstring(TValue* o)
    {
        return checktag(o, ctb(LUA_VLNGSTR));
    }

    private static TString* tsvalueraw(in Value v)
    {
        return gco2ts(v.gc);
    }

    private static TString* tsvalue(TValue* o)
    {
        Debug.Assert(ttisstring(o));
        return gco2ts(val_(o).gc);
    }

    private static void setsvalue(lua_State* L, TValue* obj, TString* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(x->tt));
        checkliveness(L, obj);
    }

    /* set a string to the stack */
    private static void setsvalue2s(lua_State* L, StkId o, TString* s)
    {
        setsvalue(L, s2v(o), s);
    }

    /* set a string to a new object */
    private static void setsvalue2n(lua_State* L, TValue* obj, TString* x)
    {
        setsvalue(L, obj, x);
    }

    /* Kinds of long strings (stored in 'shrlen') */
    private const sbyte LSTRREG = -1; /* regular long string */
    private const sbyte LSTRFIX = -2; /* fixed external long string */
    private const sbyte LSTRMEM = -3; /* external long string with deallocation */

    /*
     ** Header for a string value.
     */
    internal struct TString
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct U
        {
            [FieldOffset(0)] public long lnglen; /* length for long strings */
            [FieldOffset(0)] public TString* hnext; /* linked list for hash table */
        }

        public GCObject* next;
        public byte tt;
        public byte marked;
        public byte extra; /* reserved words for short strings; "has hash" for longs */
        public sbyte shrlen; /* length for short strings, negative for long strings */
        public uint hash;
        public U u;
        public byte* contents; /* pointer to content in long strings */
        public lua_Alloc falloc; /* deallocation function for external strings */
        public void* ud; /* user data for external strings */
    }

    private static bool strisshr(TString* ts)
    {
        return ts->shrlen >= 0;
    }

    private static bool isextstr(TValue* ts)
    {
        return ttislngstring(ts) && tsvalue(ts)->shrlen != LSTRREG;
    }

    /*
     ** Get the actual string (array of bytes) from a 'TString'. (Generic
     ** version and specialised versions for long and short strings.)
     */
    private static byte* rawgetshrstr(TString* ts)
    {
        return (byte*)&ts->contents;
    }

    private static byte* getshrstr(TString* ts)
    {
        Debug.Assert(strisshr(ts));
        return rawgetshrstr(ts);
    }

    private static byte* getlngstr(TString* ts)
    {
        Debug.Assert(!strisshr(ts));
        return ts->contents;
    }

    private static byte* getstr(TString* ts)
    {
        return strisshr(ts) ? rawgetshrstr(ts) : ts->contents;
    }

    private static string getnetstr(TString* ts)
    {
        ReadOnlySpan<byte> tmp = new(getstr(ts), checked((int)tsslen(ts)));
        return Encoding.UTF8.GetString(tmp);
    }

    /* get string length from 'TString *ts' */
    private static long tsslen(TString* ts)
    {
        return strisshr(ts) ? ts->shrlen : ts->u.lnglen;
    }

    /*
     ** Get string and length */
    private static byte* getlstr(TString* ts, out long len)
    {
        if (strisshr(ts))
        {
            len = ts->shrlen;
            return rawgetshrstr(ts);
        }

        len = ts->u.lnglen;
        return ts->contents;
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Userdata
     ** ===================================================================
     */

    /*
     ** Light userdata should be a variant of userdata, but for compatibility
     ** reasons they are also different types.
     */
    private const byte LUA_VLIGHTUSERDATA = LUA_TLIGHTUSERDATA;

    private const byte LUA_VUSERDATA = LUA_TUSERDATA;

    private static bool ttislightuserdata(TValue* o)
    {
        return checktag(o, LUA_VLIGHTUSERDATA);
    }

    private static bool ttisfulluserdata(TValue* o)
    {
        return checktag(o, ctb(LUA_VUSERDATA));
    }

    private static void* pvalue(TValue* o)
    {
        Debug.Assert(ttislightuserdata(o));
        return val_(o).p;
    }

    private static Udata* uvalue(TValue* o)
    {
        Debug.Assert(ttisfulluserdata(o));
        return gco2u(val_(o).gc);
    }

    private static void* pvalueraw(in Value v)
    {
        return v.p;
    }

    private static void setpvalue(TValue* obj, void* x)
    {
        val_(obj).p = x;
        settt_(obj, LUA_VLIGHTUSERDATA);
    }

    private static void setuvalue(lua_State* L, TValue* obj, Udata* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(LUA_VUSERDATA));
        checkliveness(L, obj);
    }

    /*
     ** Header for userdata with user values;
     ** memory area follows the end of this structure.
     */
    private struct Udata
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
        public ushort nuvalue; /* number of user values */
        public long len; /* number of bytes */
        public Table* metatable;
        public GCObject* gclist;
        public fixed byte uv[1]; /* user values */
    }

    /*
     ** Header for userdata with no user values. These userdata do not need
     ** to be gray during GC, and therefore do not need a 'gclist' field.
     ** To simplify, the code always use 'Udata' for both kinds of userdata,
     ** making sure it never accesses 'gclist' on userdata with no user values.
     ** This structure here is used only to compute the correct size for
     ** this representation. (The 'bindata' field in its end ensures correct
     ** alignment for binary data following this header.)
     */
    private struct Udata0
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
        public ushort nuvalue; /* number of user values */
        public long len; /* number of bytes */
        public Table* metatable;
        public long bindata;
    }

    private static readonly nint Udata_uv_offset = Marshal.OffsetOf<Udata>(nameof(Udata.uv));
    private static readonly nint Udata0_bindata_offset = Marshal.OffsetOf<Udata0>(nameof(Udata0.bindata));

    /* compute the offset of the memory area of a userdata */
    private static nint udatamemoffset(ushort nuv)
    {
        return nuv == 0 ? Udata0_bindata_offset : Udata_uv_offset + sizeof(TValue) * nuv;
    }

    /* get the address of the memory block inside 'Udata' */
    private static void* getudatamem(Udata* u)
    {
        return (byte*)u + udatamemoffset(u->nuvalue);
    }

    /* compute the size of a userdata */
    private static long sizeudata(ushort nuv, long nb)
    {
        return udatamemoffset(nuv) + nb;
    }

    /* }================================================================== */


    /*
     ** {==================================================================
     ** Prototypes
     ** ===================================================================
     */

    private const byte LUA_VPROTO = LUA_TPROTO;

    /*
     ** Description of an upvalue for function prototypes
     */
    private struct Upvaldesc
    {
        public TString* name; /* upvalue name (for debug information) */
        public byte instack; /* whether it is in stack (register) */
        public byte idx; /* index of upvalue (in stack or in outer function's list) */
        public byte kind; /* kind of corresponding variable */
    }

    /*
     ** Description of a local variable for function prototypes
     ** (used for debug information)
     */
    private struct LocVar
    {
        public TString* varname;
        public int startpc; /* first point where variable is active */
        public int endpc; /* first point where variable is dead */
    }

    /*
     ** Associates the absolute line source for a given instruction ('pc').
     ** The array 'lineinfo' gives, for each instruction, the difference in
     ** lines from the previous instruction. When that difference does not
     ** fit into a byte, Lua saves the absolute line for that instruction.
     ** (Lua also saves the absolute line periodically, to speed up the
     ** computation of a line number: we can use binary search in the
     ** absolute-line array, but we must traverse the 'lineinfo' array
     ** linearly to compute a line.)
     */
    private struct AbsLineInfo
    {
        public int pc;
        public int line;
    }

    /*
     ** Flags in Prototypes
     */
    private const int PF_VAHID = 1; /* function has hidden vararg arguments */
    private const int PF_VATAB = 2; /* function has vararg table */
    private const int PF_FIXED = 4; /* prototype has parts in fixed memory */

    /* a vararg function either has hidden args. or a vararg table */
    private static bool isvararg(Proto* p)
    {
        return (p->flag & (PF_VAHID | PF_VATAB)) != 0;
    }

    /*
     ** mark that a function needs a vararg table. (The flag PF_VAHID will
     ** be cleared later.)
     */
    private static void needvatab(Proto* p)
    {
        p->flag |= PF_VATAB;
    }

    /*
     ** Function Prototypes
     */
    private struct Proto
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
        public byte numparams; /* number of fixed (named) parameters */
        public byte flag;
        public byte maxstacksize; /* number of registers needed by this function */
        public int sizeupvalues; /* size of 'upvalues' */
        public int sizek; /* size of 'k' */
        public int sizecode;
        public int sizelineinfo;
        public int sizep; /* size of 'p' */
        public int sizelocvars;
        public int sizeabslineinfo; /* size of 'abslineinfo' */
        public int linedefined; /* debug information  */
        public int lastlinedefined; /* debug information  */
        public TValue* k; /* constants used by the function */
        public uint* code; /* opcodes */
        public Proto** p; /* functions defined inside the function */
        public Upvaldesc* upvalues; /* upvalue information */
        public sbyte* lineinfo; /* information about source lines (debug information) */
        public AbsLineInfo* abslineinfo; /* idem */
        public LocVar* locvars; /* information about local variables (debug information) */
        public TString* source; /* used for debug information */
        public GCObject* gclist;
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Functions
     ** ===================================================================
     */

    private const byte LUA_VUPVAL = LUA_TUPVAL;

    /* Variant tags for functions */
    private const byte LUA_VLCL = LUA_TFUNCTION; /* Lua closure */
    private const byte LUA_VLCF = LUA_TFUNCTION | 1 << 4; /* light C function */
    private const byte LUA_VCCL = LUA_TFUNCTION | 2 << 4; /* C closure */

    private static bool ttisfunction(TValue* o)
    {
        return checktype(o, LUA_TFUNCTION);
    }

    private static bool ttisLclosure(TValue* o)
    {
        return checktag(o, ctb(LUA_VLCL));
    }

    private static bool ttislcf(TValue* o)
    {
        return checktag(o, LUA_VLCF);
    }

    private static bool ttisCclosure(TValue* o)
    {
        return checktag(o, ctb(LUA_VCCL));
    }

    private static bool ttisclosure(TValue* o)
    {
        return ttisLclosure(o) || ttisCclosure(o);
    }

    private static bool isLfunction(TValue* o)
    {
        return ttisLclosure(o);
    }

    private static Closure* clvalue(TValue* o)
    {
        Debug.Assert(ttisclosure(o));
        return gco2cl(val_(o).gc);
    }

    private static LClosure* clLvalue(TValue* o)
    {
        Debug.Assert(ttisLclosure(o));
        return gco2lcl(val_(o).gc);
    }

    private static lua_CFunction fvalue(TValue* o)
    {
        Debug.Assert(ttislcf(o));
        return val_(o).f;
    }

    private static CClosure* clCvalue(TValue* o)
    {
        Debug.Assert(ttisCclosure(o));
        return gco2ccl(val_(o).gc);
    }

    private static lua_CFunction fvalueraw(in Value v)
    {
        return v.f;
    }

    private static void setclLvalue(lua_State* L, TValue* obj, LClosure* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(LUA_VLCL));
        checkliveness(L, obj);
    }

    private static void setclLvalue2s(lua_State* L, StkId o, LClosure* cl)
    {
        setclLvalue(L, s2v(o), cl);
    }

    private static void setfvalue(TValue* obj, lua_CFunction x)
    {
        val_(obj).f = x;
        settt_(obj, LUA_VLCF);
    }

    private static void setclCvalue(lua_State* L, TValue* obj, CClosure* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(LUA_VCCL));
        checkliveness(L, obj);
    }

    /*
     ** Upvalues for Lua closures
     */
    internal struct UpVal
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct V
        {
            [FieldOffset(0)] public TValue* p; /* points to stack or to its own value */
            [FieldOffset(0)] public nint offset; /* used while the stack is being reallocated */
        }

        public struct UOpen
        {
            public UpVal* next; /* linked list */
            public UpVal** previous;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct U
        {
            [FieldOffset(0)] public UOpen open; /* (when open) */
            [FieldOffset(0)] public TValue value; /* the value (when closed) */
        }

        public GCObject* next;
        public byte tt;
        public byte marked;
        public V v;
        public U u;
    }

    [InlineArray(1)]
    private struct TValueArray
    {
        private TValue element0;
    }

    private struct CClosure
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
        public byte nupvalues;
        public GCObject* gclist;
        public lua_CFunction f;
        public TValueArray upvalue; /* list of upvalues */
    }

    private struct LClosure
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
        public byte nupvalues;
        public GCObject* gclist;
        public Proto* p;
        public UpVal* upvals; /* list of upvalues */
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct Closure
    {
        [FieldOffset(0)] public CClosure c;
        [FieldOffset(0)] public LClosure l;
    }

    private static Proto* getproto(TValue* o)
    {
        return clLvalue(o)->p;
    }

    /* }================================================================== */

    /*
     ** {==================================================================
     ** Tables
     ** ===================================================================
     */

    private const byte LUA_VTABLE = LUA_TTABLE;

    private static bool ttistable(TValue* o)
    {
        return checktag(o, ctb(LUA_VTABLE));
    }

    private static Table* hvalue(TValue* o)
    {
        Debug.Assert(ttistable(o));
        return gco2t(val_(o).gc);
    }

    private static void sethvalue(lua_State* L, TValue* obj, Table* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(LUA_VTABLE));
        checkliveness(L, obj);
    }

    private static void sethvalue2s(lua_State* L, StkId o, Table* h)
    {
        sethvalue(L, s2v(o), h);
    }

    /*
     ** Nodes for Hash tables: A pack of two TValue's (key-value pairs)
     ** plus a 'next' field to link colliding entries. The distribution
     ** of the key's fields ('key_tt' and 'key_val') not forming a proper
     ** 'TValue' allows for a smaller size for 'Node' both in 4-byte
     ** and 8-byte alignments.
     */
    [StructLayout(LayoutKind.Explicit)]
    internal struct Node
    {
        public struct NodeKey
        {
            public Value value_;
            public byte tt_; /* fields for value */
            public byte key_tt; /* key type */
            public int next; /* for chaining */
            public Value key_val; /* key value */
        }

        [FieldOffset(0)] public NodeKey u;
        [FieldOffset(0)] public TValue i_val; /* direct access to node's value as a proper 'TValue' */
    }

    /* copy a value into a key */
    private static void setnodekey(Node* node, TValue* obj)
    {
        node->u.key_val = obj->value_;
        node->u.key_tt = obj->tt_;
    }

    /* copy a value from a key */
    private static void getnodekey(lua_State* L, TValue* obj, Node* node)
    {
        obj->value_ = node->u.key_val;
        obj->tt_ = node->u.key_tt;
        checkliveness(L, obj);
    }

    internal struct Table
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
        public byte flags; /* 1<<p means tagmethod(p) is not present */
        public byte lsizenode; /* log2 of number of slots of 'node' array */
        public uint asize; /* number of slots in 'array' array */
        public Value* array; /* array part */
        public Node* node;
        public Table* metatable;
        public GCObject* gclist;
    }

    /*
     ** Macros to manipulate keys inserted in nodes
     */
    private static ref byte keytt(Node* node)
    {
        return ref node->u.key_tt;
    }

    private static ref Value keyval(Node* node)
    {
        return ref node->u.key_val;
    }

    private static bool keyisnil(Node* node)
    {
        return keytt(node) == LUA_TNIL;
    }

    private static bool keyisinteger(Node* node)
    {
        return keytt(node) == LUA_VNUMINT;
    }

    private static long keyival(Node* node)
    {
        return keyval(node).i;
    }

    private static bool keyisshrstr(Node* node)
    {
        return keytt(node) == ctb(LUA_VSHRSTR);
    }

    private static TString* keystrval(Node* node)
    {
        return gco2ts(keyval(node).gc);
    }

    private static void setnilkey(Node* node)
    {
        keytt(node) = LUA_TNIL;
    }

    private static bool keyiscollectable(Node* n)
    {
        return (keytt(n) & BIT_ISCOLLECTABLE) != 0;
    }

    private static GCObject* gckey(Node* n)
    {
        return keyval(n).gc;
    }

    private static GCObject* gckeyN(Node* n)
    {
        return keyiscollectable(n) ? gckey(n) : null;
    }

    /*
     ** Dead keys in tables have the tag DEADKEY but keep their original
     ** gcvalue. This distinguishes them from regular keys but allows them to
     ** be found when searched in a special way. ('next' needs that to find
     ** keys removed from a table during a traversal.)
     */
    private static void setdeadkey(Node* node)
    {
        keytt(node) = LUA_TDEADKEY;
    }

    private static bool keyisdead(Node* node)
    {
        return keytt(node) == LUA_TDEADKEY;
    }

    /* }================================================================== */

    /*
     ** 'module' operation for hashing (size is always a power of 2)
     */
    private static uint lmod(uint s, int size)
    {
        Debug.Assert((size & size - 1) == 0);
        return s & (uint)(size - 1);
    }

    private static uint lmod(uint s, uint size)
    {
        Debug.Assert((size & size - 1) == 0);
        return s & size - 1;
    }

    private static uint twoto(byte x)
    {
        return 1u << x;
    }

    private static uint sizenode(Table* t)
    {
        return twoto(t->lsizenode);
    }

    /* size of buffer for 'luaO_utf8esc' function */
    private const int UTF8BUFFSZ = 8;

    /* macro to call 'luaO_pushvfstring' correctly */
    private static void pushvfstring(lua_State* L, object[] argp, string fmt, out string msg)
    {
        msg = luaO_pushfstring(L, fmt, argp);
        if (msg == null!)
        {
            luaD_throw(L, LUA_ERRMEM); /* only after 'va_end' */
        }
    }

    private static partial int luaO_utf8esc(byte* buff, uint x);

    private static partial byte luaO_ceillog2(uint x);

    private static partial byte luaO_codeparam(uint p);

    private static partial long luaO_applyparam(byte p, long x);

    private static partial bool luaO_rawarith(lua_State* L, int op, TValue* p1, TValue* p2, TValue* res);

    private static partial void luaO_arith(lua_State* L, int op, TValue* p1, TValue* p2, StkId res);

    private static partial long luaO_str2num(byte* s, TValue* o);

    private static partial uint luaO_tostringbuff(TValue* obj, byte* buff);

    private static partial byte luaO_hexavalue(int c);

    private static partial void luaO_tostring(lua_State* L, TValue* obj);

    private static partial string luaO_pushfstring(lua_State* L, string fmt, params object[] args);

    private static partial void luaO_chunkid(byte* @out, byte* source, long srclen);
}
