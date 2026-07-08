namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

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
    internal static TValue* s2v(StkId o)
    {
        return &o->val;
    }

    /*
     ** {==================================================================
     ** Nil
     ** ===================================================================
     */

    /* Standard nil */
    internal const byte LUA_VNIL = LUA_TNIL;

    /* Empty slot (which might be different from a slot containing nil) */
    internal const byte LUA_VEMPTY = LUA_TNIL | 1 << 4;

    /* Value returned for a key not found in a table (absent key) */
    private const byte LUA_VABSTKEY = LUA_TNIL | 2 << 4;

    /* Special variant to signal that a fast get is accessing a non-table */
    internal const byte LUA_VNOTABLE = LUA_TNIL | 3 << 4;

    /* macro to test for (any kind of) nil */
    internal static bool ttisnil(TValue* v)
    {
        return checktype(v, LUA_TNIL);
    }

    /*
     ** Macro to test the result of a table access. Formally, it should
     ** distinguish between LUA_VEMPTY/LUA_VABSTKEY/LUA_VNOTABLE and
     ** other tags. As currently nil is equivalent to LUA_VEMPTY, it is
     ** simpler to just test whether the value is nil.
     */
    internal static bool tagisempty(byte tag)
    {
        return novariant(tag) == LUA_TNIL;
    }

    /* macro to test for a standard nil */
    private static bool ttisstrictnil(TValue* o)
    {
        return checktag(o, LUA_VNIL);
    }

    internal static void setnilvalue(TValue* obj)
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

    internal static void setbfvalue(TValue* obj)
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
    internal const byte LUA_VNUMINT = LUA_TNUMBER;
    private const byte LUA_VNUMFLT = LUA_TNUMBER | 1 << 4;

    private static bool ttisnumber(TValue* o)
    {
        return checktype(o, LUA_TNUMBER);
    }

    internal static bool ttisfloat(TValue* o)
    {
        return checktag(o, LUA_VNUMFLT);
    }

    internal static bool ttisinteger(TValue* o)
    {
        return checktag(o, LUA_VNUMINT);
    }

    private static double nvalue(TValue* o)
    {
        Debug.Assert(ttisnumber(o));
        return ttisinteger(o) ? ivalue(o) : fltvalue(o);
    }

    internal static double fltvalue(TValue* o)
    {
        Debug.Assert(ttisfloat(o));
        return val_(o).n;
    }

    internal static long ivalue(TValue* o)
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

    internal static void setfltvalue(TValue* obj, double x)
    {
        val_(obj).n = x;
        settt_(obj, LUA_VNUMFLT);
    }

    private static void chgfltvalue(TValue* obj, double x)
    {
        Debug.Assert(ttisfloat(obj));
        val_(obj).n = x;
    }

    internal static void setivalue(TValue* obj, long x)
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

    internal static bool ttisstring(TValue* o)
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

    internal static TString* tsvalue(TValue* o)
    {
        Debug.Assert(ttisstring(o));
        return gco2ts(val_(o).gc);
    }

    internal static void setsvalue(lua_State* L, TValue* obj, TString* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(x->tt));
        checkliveness(L, obj);
    }

    /* set a string to the stack */
    internal static void setsvalue2s(lua_State* L, StkId o, TString* s)
    {
        setsvalue(L, s2v(o), s);
    }

    /* set a string to a new object */
    private static void setsvalue2n(lua_State* L, TValue* obj, TString* x)
    {
        setsvalue(L, obj, x);
    }

    /* Kinds of long strings (stored in 'shrlen') */
    internal const sbyte LSTRREG = -1; /* regular long string */
    internal const sbyte LSTRFIX = -2; /* fixed external long string */
    internal const sbyte LSTRMEM = -3; /* external long string with deallocation */

    /*
     ** Header for a string value.
     */
    internal struct TString
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct U
        {
            [FieldOffset(0)] public int lnglen; /* length for long strings */
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
    
    internal static readonly int TString_falloc_offset = Marshal.OffsetOf<TString>(nameof(TString.falloc)).ToInt32();

    internal static bool strisshr(TString* ts)
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

    internal static byte* getlngstr(TString* ts)
    {
        Debug.Assert(!strisshr(ts));
        return ts->contents;
    }

    internal static byte* getstr(TString* ts)
    {
        return strisshr(ts) ? rawgetshrstr(ts) : ts->contents;
    }

    internal static string getnetstr(TString* ts)
    {
        return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(getstr(ts), tsslen(ts)));
    }

    /* get string length from 'TString *ts' */
    internal static int tsslen(TString* ts)
    {
        return strisshr(ts) ? ts->shrlen : ts->u.lnglen;
    }
    
    /// <summary>
    /// Get string and length.
    /// </summary>
    [Obsolete]
    private static byte* getlstr(TString* ts, out int len)
    {
        if (strisshr(ts))
        {
            len = ts->shrlen;
            return rawgetshrstr(ts);
        }

        len = ts->u.lnglen;
        return ts->contents;
    }
    
    /// <summary>
    /// Get string.
    /// </summary>
    private static ReadOnlySpan<byte> getlstr(TString* ts)
    {
        return strisshr(ts)
            ? new ReadOnlySpan<byte>(rawgetshrstr(ts), ts->shrlen)
            : new ReadOnlySpan<byte>(ts->contents, ts->u.lnglen);
    }

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

    internal static void setuvalue(lua_State* L, TValue* obj, Udata* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(LUA_VUSERDATA));
        checkliveness(L, obj);
    }

    /*
     ** Header for userdata with user values;
     ** memory area follows the end of this structure.
     */
    internal struct Udata
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
    internal struct Upvaldesc
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
    internal struct LocVar
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
    internal struct AbsLineInfo
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
    internal struct Proto
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
    internal const byte LUA_VCCL = LUA_TFUNCTION | 2 << 4; /* C closure */

    internal static bool ttisfunction(TValue* o)
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

    internal static LClosure* clLvalue(TValue* o)
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

    internal static void setfvalue(TValue* obj, lua_CFunction x)
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

    internal struct CClosure
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
        public byte nupvalues;
        public GCObject* gclist;
        public lua_CFunction f;

        public static ref TValue GetUpValue(CClosure* closure, int index)
        {
            Debug.Assert(index >= 0 && index < closure->nupvalues);
            TValue* upvals = (TValue*)(closure + 1);
            return ref upvals[index];
        }

        public static TValue* GetUpValuePtr(CClosure* closure, int index)
        {
            Debug.Assert(index >= 0 && index < closure->nupvalues);
            TValue* upvals = (TValue*)(closure + 1);
            return &upvals[index];
        }
    }

    internal struct LClosure
    {
        public GCObject* next;
        public byte tt;
        public byte marked;
        public byte nupvalues;
        public GCObject* gclist;
        public Proto* p;
        
        public static ref UpVal* GetUpValue(LClosure* closure, int index)
        {
            Debug.Assert(index >= 0 && index < closure->nupvalues);
            UpVal** upvals = (UpVal**)(closure + 1);
            return ref upvals[index];
        }
        
        public static UpVal** GetUpValuePtr(LClosure* closure, int index)
        {
            Debug.Assert(index >= 0 && index < closure->nupvalues);
            UpVal** upvals = (UpVal**)(closure + 1);
            return &upvals[index];
        }
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

    internal static void sethvalue(lua_State* L, TValue* obj, Table* x)
    {
        val_(obj).gc = obj2gco(x);
        settt_(obj, ctb(LUA_VTABLE));
        checkliveness(L, obj);
    }

    internal static void sethvalue2s(lua_State* L, StkId o, Table* h)
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
    internal const int UTF8BUFFSZ = 8;

    /* macro to call 'luaO_pushvfstring' correctly */
    private static void pushvfstring(lua_State* L, object[] argp, string fmt, out string msg)
    {
        msg = luaO_pushfstring(L, fmt, argp);
        if (msg == null!)
        {
            luaD_throw(L, LUA_ERRMEM); /* only after 'va_end' */
        }
    }
    
    private static byte[] log_2 =
    [
        /* log_2[i - 1] = ceil(log2(i)) */
        0, 1, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
        6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
    ];

    /*
     ** Computes ceil(log2(x)), which is the smallest integer n such that
     ** x <= (1 << n).
     */
    internal static byte luaO_ceillog2(uint x)
    {
        int l = 0;
        x--;
        while (x >= 256)
        {
            l += 8;
            x >>= 8;
        }

        return (byte)(l + log_2[x]);
    }

    /*
     ** Encodes 'p'% as a floating-point byte, represented as (eeeexxxx).
     ** The exponent is represented using excess-7. Mimicking IEEE 754, the
     ** representation normalises the number when possible, assuming an extra
     ** 1 before the mantissa (xxxx) and adding one to the exponent (eeee)
     ** to signal that. So, the real value is (1xxxx) * 2^(eeee - 7 - 1) if
     ** eeee != 0, and (xxxx) * 2^-7 otherwise (subnormal numbers).
     */
    internal static byte luaO_codeparam(uint p)
    {
        if (p >= ((long)0x1F << 0xF - 7 - 1) * 100u) /* overflow? */
        {
            return 0xFF; /* return maximum value */
        }

        p = (p * 128 + 99) / 100; /* round up the division */
        if (p < 0x10)
        {
            /* subnormal number? */
            /* exponent bits are already zero; nothing else to do */
            return (byte)p;
        }

        /* p >= 0x10 implies ceil(log2(p + 1)) >= 5 */
        /* preserve 5 bits in 'p' */
        uint log = luaO_ceillog2(p + 1) - 5u;
        return (byte)((p >> (int)log) - 0x10 | log + 1 << 4);
    }

    /*
     ** Computes 'p' times 'x', where 'p' is a floating-point byte. Roughly,
     ** we have to multiply 'x' by the mantissa and then shift accordingly to
     ** the exponent.  If the exponent is positive, both the multiplication
     ** and the shift increase 'x', so we have to care only about overflows.
     ** For negative exponents, however, multiplying before the shift keeps
     ** more significant bits, as long as the multiplication does not
     ** overflow, so we check which order is best.
     */
    internal static long luaO_applyparam(byte p, long x)
    {
        const long MAX_LMEM = 0x7FFFFFFFFFFFFFFFL;

        int m = p & 0xF; /* mantissa */
        int e = p >> 4; /* exponent */
        if (e > 0)
        {
            /* normalized? */
            e--; /* correct exponent */
            m += 0x10; /* correct mantissa; maximum value is 0x1F */
        }

        e -= 7; /* correct excess-7 */
        if (e >= 0)
        {
            if (x < MAX_LMEM / 0x1F >> e) /* no overflow? */
            {
                return x * m << e; /* order doesn't matter here */
            }

            /* real overflow */
            return MAX_LMEM;
        }

        /* negative exponent */
        e = -e;
        if (x < MAX_LMEM / 0x1F) /* multiplication cannot overflow? */
        {
            return x * m >> e; /* multiplying first gives more precision */
        }

        if (x >> e < MAX_LMEM / 0x1F) /* cannot overflow after shift? */
        {
            return (x >> e) * m;
        }

        /* real overflow */
        return MAX_LMEM;
    }

    private static long intarith(lua_State* L, int op, long v1, long v2)
    {
        return op switch
        {
            LUA_OPADD => v1 + v2,
            LUA_OPSUB => v1 - v2,
            LUA_OPMUL => (long)((ulong)v1 * (ulong)v2),
            LUA_OPMOD => luaV_mod(L, v1, v2),
            LUA_OPIDIV => luaV_idiv(L, v1, v2),
            LUA_OPBAND => v1 & v2,
            LUA_OPBOR => v1 | v2,
            LUA_OPBXOR => v1 ^ v2,
            LUA_OPSHL => luaV_shiftl(v1, v2),
            LUA_OPSHR => luaV_shiftr(v1, v2),
            LUA_OPUNM => -v1,
            LUA_OPBNOT => ~0L ^ v1,
            _ => throw new InvalidOperationException(),
        };
    }

    private static double numarith(lua_State* L, int op, double v1, double v2)
    {
        return op switch
        {
            LUA_OPADD => v1 + v2,
            LUA_OPSUB => v1 - v2,
            LUA_OPMUL => v1 * v2,
            LUA_OPDIV => v1 / v2,
            LUA_OPPOW => v2 == 2 ? v1 * v1 : Math.Pow(v1, v2),
            LUA_OPIDIV => Math.Floor(v1 / v2),
            LUA_OPUNM => -v1,
            LUA_OPMOD => luaV_modf(L, v1, v2),
            _ => throw new InvalidOperationException(),
        };
    }

    internal static bool luaO_rawarith(lua_State* L, int op, TValue* p1, TValue* p2, TValue* res)
    {
        switch (op)
        {
            case LUA_OPBAND:
            case LUA_OPBOR:
            case LUA_OPBXOR:
            case LUA_OPSHL:
            case LUA_OPSHR:
            case LUA_OPBNOT:
                {
                    /* operate only on integers */
                    if (tointegerns(p1, out long i1) && tointegerns(p2, out long i2))
                    {
                        setivalue(res, intarith(L, op, i1, i2));
                        return true;
                    }

                    return false; /* fail */
                }

            case LUA_OPDIV:
            case LUA_OPPOW:
                {
                    /* operate only on floats */
                    if (tonumberns(p1, out double n1) && tonumberns(p2, out double n2))
                    {
                        setfltvalue(res, numarith(L, op, n1, n2));
                        return true;
                    }

                    return false; /* fail */
                }

            default:
                {
                    /* other operations */
                    if (ttisinteger(p1) && ttisinteger(p2))
                    {
                        setivalue(res, intarith(L, op, ivalue(p1), ivalue(p2)));
                        return true;
                    }

                    if (tonumberns(p1, out double n1) && tonumberns(p2, out double n2))
                    {
                        setfltvalue(res, numarith(L, op, n1, n2));
                        return true;
                    }

                    return false; /* fail */
                }
        }
    }

    internal static void luaO_arith(lua_State* L, int op, TValue* p1, TValue* p2, StkId res)
    {
        if (!luaO_rawarith(L, op, p1, p2, s2v(res)))
        {
            // could not perform raw operation; try metamethod 
            luaT_trybinTM(L, p1, p2, res, op - LUA_OPADD + TMS.ADD);
        }
    }

    internal static byte luaO_hexavalue(int c)
    {
        Debug.Assert(lisxdigit(c));
        if (lisdigit(c))
        {
            return (byte)(c - '0');
        }

        return (byte)(ltolower(c) - 'a' + 10);
    }

    private static bool isneg(ref byte* s)
    {
        if (*s == '-')
        {
            s++;
            return true;
        }

        if (*s == '+')
        {
            s++;
        }

        return false;
    }

    /*
     ** {==================================================================
     ** Lua's implementation for 'lua_strx2number'
     ** ===================================================================
     */

// /* maximum number of significant digits to read (to avoid overflows
//    even with single floats) */
// #define MAXSIGDIG	30

    /*
    ** convert a hexadecimal numeric string to a number, following
    ** C99 specification for 'strtod'
    */
    private static double lua_strx2number(byte* s, byte** endptr)
    {
//   int dot = lua_getlocaledecpoint();
//   double r = (0.0);  /* result (accumulator) */
//   int sigdig = 0;  /* number of significant digits */
//   int nosigdig = 0;  /* number of non-significant digits */
//   int e = 0;  /* exponent correction */
//   int neg;  /* 1 if number is negative */
//   int hasdot = 0;  /* true after seen a dot */
//   *endptr = cast_charp(s);  /* nothing is valid yet */
//   while (lisspace(cast_uchar(*s))) s++;  /* skip initial spaces */
//   neg = isneg(&s);  /* check sign */
//   if (!(*s == '0' && (*(s + 1) == 'x' || *(s + 1) == 'X')))  /* check '0x' */
//     return (0.0);  /* invalid format (no '0x') */
//   for (s += 2; ; s++) {  /* skip '0x' and read numeral */
//     if (*s == dot) {
//       if (hasdot) break;  /* second dot? stop loop */
//       else hasdot = 1;
//     }
//     else if (lisxdigit(cast_uchar(*s))) {
//       if (sigdig == 0 && *s == '0')  /* non-significant digit (zero)? */
//         nosigdig++;
//       else if (++sigdig <= MAXSIGDIG)  /* can read it without overflow? */
//           r = (r * (16.0)) + luaO_hexavalue(*s);
//       else e++;  /* too many digits; ignore, but still count for exponent */
//       if (hasdot) e--;  /* decimal digit? correct exponent */
//     }
//     else break;  /* neither a dot nor a digit */
//   }
//   if (nosigdig + sigdig == 0)  /* no digits? */
//     return (0.0);  /* invalid format */
//   *endptr = cast_charp(s);  /* valid up to here */
//   e *= 4;  /* each digit multiplies/divides value by 2^4 */
//   if (*s == 'p' || *s == 'P') {  /* exponent part? */
//     int exp1 = 0;  /* exponent value */
//     int neg1;  /* exponent sign */
//     s++;  /* skip 'p' */
//     neg1 = isneg(&s);  /* sign */
//     if (!lisdigit(cast_uchar(*s)))
//       return (0.0);  /* invalid; must have at least one digit */
//     while (lisdigit(cast_uchar(*s)))  /* read exponent */
//       exp1 = exp1 * 10 + *(s++) - '0';
//     if (neg1) exp1 = -exp1;
//     e += exp1;
//     *endptr = cast_charp(s);  /* valid up to here */
//   }
//   if (neg) r = -r;
//   return (ldexp)(r, e);
        throw new NotImplementedException();
    }
    
    /* maximum length of a numeral */
    private const int L_MAXLENNUM = 200;

    /*
    ** Convert string 's' to a Lua number (put in 'result'). Return null on
    ** fail or the address of the ending '\0' on success. ('mode' == 'x')
    ** means a hexadecimal numeral.
    */
    private static byte* l_str2dloc(byte* s, double* result, int mode)
    {
        byte* endptr;
        *result = strtod(s, &endptr);
        if (endptr == s)
        {
            return null; /* nothing recognised? */
        }

        while (lisspace(*endptr))
        {
            endptr++; /* skip trailing spaces */
        }

        return *endptr == '\0' ? endptr : null; /* OK iff no trailing chars */
    }

    /*
     ** Convert string 's' to a Lua number (put in 'result') handling the
     ** current locale.
     ** This function accepts both the current locale or a dot as the radix
     ** mark. If the conversion fails, it may mean number has a dot but
     ** locale accepts something else. In that case, the code copies 's'
     ** to a buffer (because 's' is read-only), changes the dot to the
     ** current locale radix mark, and tries to convert again.
     ** The variable 'mode' checks for special characters in the string:
     ** - 'n' means 'inf' or 'nan' (which should be rejected)
     ** - 'x' means a hexadecimal numeral
     ** - '.' just optimises the search for the common case (no special chars)
     */
    private static byte* l_str2d(byte* s, double* result)
    {
        byte* pmode = strpbrk(s, ".xXnN"); /* look for special chars */
        int mode = pmode != null ? ltolower(*pmode) : 0;
        if (mode == 'n') /* reject 'inf' and 'nan' */
        {
            return null;
        }

        return l_str2dloc(s, result, mode); /* try to convert */
    }

    private const ulong MAXBY10 = long.MaxValue / 10;
    private const int MAXLASTD = (int)(long.MaxValue % 10);

    private static byte* l_str2int(byte* s, long* result)
    {
        while (lisspace(*s))
        {
            s++; /* skip initial spaces */
        }

        bool neg = isneg(ref s);
        bool empty = true;
        ulong a = 0;
        if (s[0] == '0' && (s[1] == 'x' || s[1] == 'X'))
        {
            /* hex? */
            s += 2; /* skip '0x' */
            for (; lisxdigit(*s); s++)
            {
                a = a * 16 + luaO_hexavalue(*s);
                empty = false;
            }
        }
        else
        {
            /* decimal */
            for (; lisdigit(*s); s++)
            {
                int d = *s - '0';
                if (a >= MAXBY10 && (a > MAXBY10 || d > MAXLASTD + (neg ? 1 : 0))) /* overflow? */
                {
                    return null; /* do not accept it (as integer) */
                }

                a = a * 10 + (uint)d;
                empty = false;
            }
        }

        while (lisspace(*s))
        {
            s++; /* skip trailing spaces */
        }

        if (empty || *s != '\0')
        {
            return null; /* something wrong in the numeral */
        }

        *result = (long)(neg ? 0u - a : a);
        return s;
    }

    internal static long luaO_str2num(ReadOnlySpan<byte> s, TValue* o)
    {
        if (s.IsEmpty)
        {
            return 0;
        }

        fixed (byte* ptr = s)
        {
            long i;
            double n;
            byte* e;
            if ((e = l_str2int(ptr, &i)) != null)
            {
                /* try as an integer */
                setivalue(o, i);
            }
            else if ((e = l_str2d(ptr, &n)) != null)
            {
                /* else try as a float */
                setfltvalue(o, n);
            }
            else
            {
                return 0; /* conversion failed */
            }
            
            return e - ptr + 1;  /* success; return string size */
        }
    }

    internal static Span<byte> luaO_utf8esc(Span<byte> buff, uint x)
    {
        int n = 1; /* number of bytes put in buffer (backwards) */
        Debug.Assert(x <= 0x7FFFFFFFu);
        if (x < 0x80) /* ASCII? */
        {
            buff[UTF8BUFFSZ - 1] = (byte)x;
        }
        else
        {
            /* need continuation bytes */
            uint mfb = 0x3f; /* maximum that fits in first byte */
            do
            {
                /* add continuation bytes */
                buff[UTF8BUFFSZ - n++] = (byte)(0x80 | x & 0x3f);
                x >>= 6; /* remove added bits */
                mfb >>= 1; /* now there is one less bit available in first byte */
            } while (x > mfb); /* still needs continuation byte? */

            buff[UTF8BUFFSZ - n] = (byte)(~mfb << 1 | x); /* add first byte */
        }

        return buff[(UTF8BUFFSZ - n)..];
    }

    /*
     ** The size of the buffer for the conversion of a number to a string
     ** 'LUA_N2SBUFFSZ' must be enough to accommodate both LUA_INTEGER_FMT
     ** and LUA_NUMBER_FMT.  For a long long int, this is 19 digits plus a
     ** sign and a final '\0', adding to 21. For a long double, it can go to
     ** a sign, the dot, an exponent letter, an exponent sign, 4 exponent
     ** digits, the final '\0', plus the significant digits, which are
     ** approximately the *_DIG attribute.
     */
// #if LUA_N2SBUFFSZ < (20 + l_floatatt(DIG))
// #error "invalid value for LUA_N2SBUFFSZ"
// #endif

    /*
    ** Convert a float to a string, adding it to a buffer. First try with
    ** a not too large number of digits, to avoid noise (for instance,
    ** 1.1 going to "1.1000000000000001"). If that lose precision, so
    ** that reading the result back gives a different number, then do the
    ** conversion again with extra precision. Moreover, if the numeral looks
    ** like an integer (without a decimal point or an exponent), add ".0" to
    ** its end.
    */
    private static int tostringbuffFloat(double n, Span<byte> buff)
    {
        /* first conversion */
        int len = FormatFloat(
            n,
            new FormatFlags
            {
                Precision = 15,
            },
            buff,
            FloatFormatType.Shortest,
            false);
        double check = double.Parse(buff, CultureInfo.InvariantCulture); /* read it back */
        if (check != n)
        {
            /* not enough precision? */
            /* convert again with more precision */
            len = FormatFloat(
                n,
                new FormatFlags
                {
                    Precision = 17,
                },
                buff,
                FloatFormatType.Shortest,
                false);
        }

        /* looks like an integer? */
        if (!buff.ContainsAnyExcept("-0123456789\0"u8))
        {
            buff[len++] = (byte)'.';
            buff[len++] = (byte)'0'; /* adds '.0' to result */
        }

        return len;
    }

    /// <summary>
    /// Convert a number object to a string, adding it to a buffer.
    /// </summary>
    internal static int luaO_tostringbuff(TValue* obj, Span<byte> buff)
    {
        int len;
        Debug.Assert(ttisnumber(obj));
        if (ttisinteger(obj))
        {
            string result = ivalue(obj).ToString(CultureInfo.InvariantCulture);
            len = result.Length;
            for (int i = 0; i < len; i++)
            {
                buff[i] = (byte)result[i];
            }
        }
        else
        {
            len = tostringbuffFloat(fltvalue(obj), buff);
        }

        Debug.Assert(len < LUA_N2SBUFFSZ);
        return len;
    }

    /*
     ** Convert a number object to a Lua string, replacing the value at 'obj'
     */
    internal static void luaO_tostring(lua_State* L, TValue* obj)
    {
        Span<byte> buff = stackalloc byte[LUA_N2SBUFFSZ];
        int len = luaO_tostringbuff(obj, buff);
        setsvalue(L, obj, luaS_newlstr(L, buff[..len]));
    }

    /*
     ** {==================================================================
     ** 'luaO_pushvfstring'
     ** ===================================================================
     */

    /*
     ** Size for buffer space used by 'luaO_pushvfstring'. It should be
     ** (LUA_IDSIZE + LUA_N2SBUFFSZ) + a minimal space for basic messages,
     ** so that 'luaG_addinfo' can work directly on the static buffer.
     */
    private const int BUFVFS = LUA_IDSIZE + LUA_N2SBUFFSZ + 95;

    /*
     ** Buffer used by 'luaO_pushvfstring'. 'err' signals an error while
     ** building result (memory error [1] or buffer overflow [2]).
     */
    private struct BuffFS
    {
        public lua_State* L;
        public byte* b;
        public int buffsize;
        public int blen; /* length of string in 'buff' */
        public int err;
        public fixed byte space[BUFVFS]; /* initial buffer */
    }

    private static void initbuff(lua_State* L, BuffFS* buff)
    {
        buff->L = L;
        buff->b = buff->space;
        buff->buffsize = BUFVFS;
        buff->blen = 0;
        buff->err = 0;
    }

    /*
     ** Push final result from 'luaO_pushvfstring'. This function may raise
     ** errors explicitly or through memory errors, so it must run protected.
     */
    private static void pushbuff(lua_State* L, void* ud)
    {
        BuffFS* buff = (BuffFS*)ud;
        switch (buff->err)
        {
            case 1: /* memory error */
                luaD_throw(L, LUA_ERRMEM);
                break;

            case 2: /* length overflow: Add "..." at the end of result */
//       if (buff->buffsize - buff->blen < 3)
//         strcpy(buff->b + buff->blen - 3, "...");  /* 'blen' must be > 3 */
//       else {  /* there is enough space left for the "..." */
//         strcpy(buff->b + buff->blen, "...");
//         buff->blen += 3;
//       }
                throw new NotImplementedException();
//       /* FALLTHROUGH */
            default:
                {
                    /* no errors, but it can raise one creating the new string */
                    TString* ts = luaS_newlstr(L, buff->b, buff->blen);
                    setsvalue2s(L, L->top.p, ts);
                    L->top.p++;
                    break;
                }
        }
    }

    private static string? clearbuff(BuffFS* buff)
    {
        string? res;
        if (luaD_rawrunprotected(buff->L, pushbuff, buff) != LUA_OK) /* errors? */
        {
            res = null; /* error message is on the top of the stack */
        }
        else
        {
            TString* ts = tsvalue(s2v(buff->L->top.p - 1));
            res = getnetstr(ts);
        }

        if (buff->b != buff->space) /* using dynamic buffer? */
        {
            luaM_freearray(buff->L, buff->b, buff->buffsize); /* free it */
        }

        return res;
    }

    private static void addstr2buff(BuffFS* buff, string str)
    {
        byte[] data = Encoding.UTF8.GetBytes(str);
        addstr2buff(buff, data);
    }

    private static void addstr2buff(BuffFS* buff, ReadOnlySpan<byte> str)
    {
        int left = buff->buffsize - buff->blen; /* space left in the buffer */
        if (buff->err != 0) /* do nothing else after an error */
        {
            return;
        }

        if (str.Length > left)
        {
            /* new string doesn't fit into current buffer? */
            if (str.Length > (int.MaxValue / 2 - buff->blen))
            {
                /* overflow? */
                fixed (byte* ptr = str)
                {
                    memcpy(buff->b + buff->blen, ptr, left); /* copy what it can */
                }

                buff->blen = buff->buffsize;
                buff->err = 2; /* doesn't add anything else */
                return;
            }

            int newsize = buff->buffsize + str.Length; /* limited to MAX_SIZE/2 */
            byte* newb = buff->b == buff->space /* still using static space? */
                ? luaM_reallocvector<byte>(buff->L, null, 0, newsize)
                : luaM_reallocvector<byte>(buff->L, buff->b, buff->buffsize, newsize);
            if (newb == null)
            {
                /* allocation error? */
                buff->err = 1; /* signal a memory error */
                return;
            }

            if (buff->b == buff->space) /* new buffer (not reallocated)? */
            {
                memcpy(newb, buff->b, buff->blen); /* copy previous content */
            }

            buff->b = newb; /* set new (larger) buffer... */
            buff->buffsize = newsize; /* ...and its new size */
        }

        Span<byte> dest = new(buff->b + buff->blen, str.Length);
        str.CopyTo(dest); /* copy new content */
        buff->blen += str.Length;
    }

    /*
    ** Add a numeral to the buffer.
    */
    private static void addnum2buff(BuffFS* buff, TValue* num)
    {
        Span<byte> numbuff = stackalloc byte[LUA_N2SBUFFSZ];
        int len = luaO_tostringbuff(num, numbuff);
        addstr2buff(buff, numbuff[..len]);
    }

    /*
    ** this function handles only '%d', '%c', '%f', '%p', '%s', and '%%'
       conventional formats, plus Lua-specific '%I' and '%U'
    */
    internal static string luaO_pushfstring(lua_State* L, string fmt, params object[] args)
    {
        byte[] fmtBytes = Encoding.UTF8.GetBytes(fmt);
        ReadOnlySpan<byte> fmtSpan = fmtBytes;
        BuffFS buff; /* holds last part of the result */
        initbuff(L, &buff);
        
        Span<byte> bf = stackalloc byte[UTF8BUFFSZ];
        
        ReadOnlySpan<byte> e; /* points to next '%' */
        int i = 0;
        while (!(e = strchr(fmtSpan, '%')).IsEmpty)
        {
            addstr2buff(&buff, fmtSpan[..^e.Length]); /* add 'fmt' up to '%' */
            switch ((char)e[1])
            {
                /* conversion specifier */
                case 's':
                    {
                        /* zero-terminated string */
                        string s = args[i++].ToString() ?? "(null)";
                        addstr2buff(&buff, s);
                        break;
                    }

                case 'c':
                    {
                        /* an 'int' as a character */
                        byte c = Convert.ToByte(args[i++], CultureInfo.InvariantCulture);
                        addstr2buff(&buff, [c]);
                        break;
                    }
                
                case 'd':
                    {
                        /* an 'int' */
                        TValue num;
                        setivalue(&num, Convert.ToInt32(args[i++], CultureInfo.InvariantCulture));
                        addnum2buff(&buff, &num);
                        break;
                    }

                case 'I':
                    {
                        /* a 'long' */
                        TValue num;
                        setivalue(&num, Convert.ToInt64(args[i++], CultureInfo.InvariantCulture));
                        addnum2buff(&buff, &num);
                        break;
                    }

                case 'f':
                    {
                        /* a 'double' */
                        TValue num;
                        setfltvalue(&num, (double)args[i++]);
                        addnum2buff(&buff, &num);
                        break;
                    }

                case 'p':
                    {
                        /* a pointer */
                        nint p = (nint)args[i++];
                        addstr2buff(&buff, "0x"u8);
                        addstr2buff(&buff, ((nuint)p).ToString($"x{nint.Size * 2}", CultureInfo.InvariantCulture));
                        break;
                    }
                
                case 'U':
                    {
                        /* an 'unsigned long' as a UTF-8 sequence */
                        ulong arg = (ulong)args[i++];
                        Span<byte> result = luaO_utf8esc(bf, (uint)arg);
                        addstr2buff(&buff, result);
                        break;
                    }
                
                case '%':
                    addstr2buff(&buff, "%"u8);
                    break;
                
                default:
                    addstr2buff(&buff, e[..2]);  /* keep unknown format in the result */
                    break;
            }

            fmtSpan = e[2..]; /* skip '%' and the specifier */
        }

        addstr2buff(&buff, fmtSpan); /* rest of 'fmt' */
        string? msg = clearbuff(&buff); /* empty buffer into a new string */

        if (msg == null) /* error? */
        {
            luaD_throw(L, LUA_ERRMEM);
        }

        return msg;
    }

    private const string RETS = "...";
    private const string PRE = "[string \"";
    private const string POS = "\"]";

    internal static string luaO_chunkid(string source)
    {
        const int bufflen = LUA_IDSIZE; /* free space in buffer */
        if (source.StartsWith('='))
        {
            /* 'literal' source */
            if (source.Length <= bufflen) /* small enough? */
            {
                return source[1..];
            }

            return source[1..bufflen];
        }

        if (source.StartsWith('@'))
        {
            /* file name */
            if (source.Length <= bufflen) /* small enough? */
            {
                return source[1..];
            }
            
            /* add '...' before rest of name */
            return RETS + source[(1 + source.Length - (bufflen - RETS.Length))..];
        }

        /* string; format as [string "source"] */
        int nl = source.IndexOf('\n'); /* find first new line (if any) */
        string result = PRE; /* add prefix */
        int len2 = bufflen - (PRE.Length + RETS.Length + POS.Length + 1);
        if (source.Length < len2 && nl < 0)
        {
            /* small one-line source? */
            result += source; /* keep it */
        }
        else
        {
            int srclen = source.Length;
            if (nl >= 0)
            {
                srclen = nl; /* stop at first newline */
            }

            if (srclen > len2)
            {
                srclen = len2;
            }

            result += source[..srclen];
            result += RETS;
        }

        return result + POS;
    }
}
