namespace DigitalSingularity.Lua;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

public static unsafe partial class Lua
{
    /*
    ** Single-char tokens (terminal symbols) are represented by their own
    ** numeric code. Other tokens start at the following value.
    */
    private const int FIRST_RESERVED = byte.MaxValue + 1;

    private const string LUA_ENV = "_ENV";

    /*
    * WARNING: if you change the order of this enumeration,
    * grep "ORDER RESERVED"
    */
    private enum RESERVED
    {
        /* terminal symbols denoted by reserved words */
        TK_AND = FIRST_RESERVED, TK_BREAK,
        TK_DO, TK_ELSE, TK_ELSEIF, TK_END, TK_FALSE, TK_FOR, TK_FUNCTION,
        TK_GLOBAL, TK_GOTO, TK_IF, TK_IN, TK_LOCAL, TK_NIL, TK_NOT, TK_OR,
        TK_REPEAT, TK_RETURN, TK_THEN, TK_TRUE, TK_UNTIL, TK_WHILE,

        /* other terminal symbols */
        TK_IDIV, TK_CONCAT, TK_DOTS, TK_EQ, TK_GE, TK_LE, TK_NE,
        TK_SHL, TK_SHR,
        TK_DBCOLON, TK_EOS,
        TK_FLT, TK_INT, TK_NAME, TK_STRING,
    }

    /* number of reserved words */
    private static readonly int NUM_RESERVED = (int)(RESERVED.TK_WHILE - FIRST_RESERVED + 1);

    /* semantics information */
    [StructLayout(LayoutKind.Explicit)]
    private struct SemInfo
    {
        [FieldOffset(0)] public double r;
        [FieldOffset(0)] public long i;
        [FieldOffset(0)] public TString* ts;
    }

    private struct Token
    {
        public int token;
        public SemInfo seminfo;
    }

    /* state of the scanner plus state of the parser when shared by all
       functions */
    private struct LexState
    {
        public int current; /* current character (charint) */
        public int linenumber; /* input line counter */
        public int lastline; /* line of last token 'consumed' */
        public Token t; /* current token */
        public Token lookahead; /* look ahead token */
        public FuncState* fs; /* current function (parser) */
        public lua_State* L;
        public Zio* z; /* input stream */
        public Mbuffer* buff; /* buffer for tokens */
        public Table* h; /* to avoid collection/reuse strings */
        public Dyndata* dyd; /* dynamic structures used by the parser */
        public TString* source; /* current source name */
        public TString* envn; /* environment variable name */
        public TString* brkn; /* "break" name (used as a label) */
        public TString* glbn; /* "global" name (when not a reserved word) */
    }

    private static partial void luaX_init(lua_State* L);

    private static partial void luaX_setinput(
        lua_State* L,
        LexState* ls,
        Zio* z,
        TString* source,
        int firstchar);

    private static partial TString* luaX_newstring(LexState* ls, byte* str, long l);

    private static partial void luaX_next(LexState* ls);
    
// LUAI_FUNC int luaX_lookahead (LexState *ls);

    [DoesNotReturn]
    private static partial void luaX_syntaxerror(LexState* ls, string msg);

    private static partial string luaX_token2str(LexState* ls, int token);
}
