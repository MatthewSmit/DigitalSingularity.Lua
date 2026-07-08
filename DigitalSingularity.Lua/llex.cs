namespace DigitalSingularity.Lua;

using System.Diagnostics;
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
    
    private static void next(LexState* ls)
    {
        ls->current = zgetc(ls->z);
    }

    /* minimum size for string buffer */
    private const int LUA_MINBUFFER = 32;

    private static bool currIsNewline(LexState* ls)
    {
        return ls->current == '\n' || ls->current == '\r';
    }

    /* ORDER RESERVED */
    private static readonly string[] luaX_tokens =
    [
        "and", "break", "do", "else", "elseif",
        "end", "false", "for", "function", "global", "goto", "if",
        "in", "local", "nil", "not", "or", "repeat",
        "return", "then", "true", "until", "while",
        "//", "..", "...", "==", ">=", "<=", "~=",
        "<<", ">>", "::", "<eof>",
        "<number>", "<integer>", "<name>", "<string>",
    ];

    private static void save_and_next(LexState* ls)
    {
        save(ls, ls->current);
        next(ls);
    }

    private static void save(LexState* ls, int c)
    {
        Mbuffer* b = ls->buff;
        if (luaZ_bufflen(b) + 1 > luaZ_sizebuffer(b))
        {
            long newsize = luaZ_sizebuffer(b); /* get old size */
            if (newsize >= nint.MaxValue / 3 * 2) /* larger than MAX_SIZE/1.5 ? */
            {
                lexerror(ls, "lexical element too long", 0);
            }

            newsize += newsize >> 1; /* new size is 1.5 times the old one */
            luaZ_resizebuffer(ls->L, b, newsize);
        }

        b->buffer[luaZ_bufflen(b)++] = (byte)c;
    }

    private static void luaX_init(lua_State* L)
    {
        TString* e = luaS_newliteral(L, LUA_ENV); /* create env name */
        luaC_fix(L, obj2gco(e)); /* never collect this name */
        for (int i = 0; i < NUM_RESERVED; i++)
        {
            TString* ts = luaS_new(L, luaX_tokens[i]);
            luaC_fix(L, obj2gco(ts)); /* reserved words are never collected */
            ts->extra = (byte)(i + 1); /* reserved word */
        }
    }

    private static string luaX_token2str(LexState* ls, int token)
    {
        if (token < FIRST_RESERVED)
        {
            /* single-byte symbols? */
            if (lisprint(token))
            {
                return luaO_pushfstring(ls->L, "'%c'", token);
            }

            /* control character */
            return luaO_pushfstring(ls->L, "'<\\%d>'", token);
        }

        string s = luaX_tokens[token - FIRST_RESERVED];
        if (token < (int)RESERVED.TK_EOS) /* fixed format (symbols and reserved words)? */
        {
            return luaO_pushfstring(ls->L, "'%s'", s);
        }

        /* names, strings, and numerals */
        return s;
    }

    private static string txtToken(LexState* ls, int token)
    {
        switch (token)
        {
            case (int)RESERVED.TK_NAME:
            case (int)RESERVED.TK_STRING:
            case (int)RESERVED.TK_FLT:
            case (int)RESERVED.TK_INT:
                save(ls, '\0');
                return luaO_pushfstring(ls->L, "'%s'", new string((sbyte*)luaZ_bufferptr(ls->buff)));
            
            default:
                return luaX_token2str(ls, token);
        }
    }

    [DoesNotReturn]
    private static void lexerror(LexState* ls, string msg, int token)
    {
        msg = luaG_addinfo(ls->L, msg, ls->source, ls->linenumber);
        if (token != 0)
        {
            luaO_pushfstring(ls->L, "%s near %s", msg, txtToken(ls, token));
        }

        luaD_throw(ls->L, LUA_ERRSYNTAX);
    }

    [DoesNotReturn]
    private static void luaX_syntaxerror(LexState* ls, string msg)
    {
        lexerror(ls, msg, ls->t.token);
    }

    /*
     ** Anchors a string in scanner's table so that it will not be collected
     ** until the end of the compilation; by that time it should be anchored
     ** somewhere. It also internalises long strings, ensuring there is only
     ** one copy of each unique string.
     */
    private static TString* anchorstr(LexState* ls, TString* ts)
    {
        lua_State* L = ls->L;
        TValue oldts;
        byte tag = luaH_getstr(ls->h, ts, &oldts);
        if (!tagisempty(tag)) /* string already present? */
        {
            return tsvalue(&oldts); /* use stored value */
        }

        /* create a new entry */
        TValue* stv = s2v(L->top.p++); /* reserve stack space for string */
        setsvalue(L, stv, ts); /* push (anchor) the string on the stack */
        luaH_set(L, ls->h, stv, stv); /* t[string] = string */
        /* table is not a metatable, so it does not need to invalidate cache */
        luaC_checkGC(L);
        L->top.p--; /* remove string from stack */
        return ts;
    }

    /*
     ** Creates a new string and anchors it in scanner's table.
     */
    private static TString* luaX_newstring(LexState* ls, byte* str, long l)
    {
        return anchorstr(ls, luaS_newlstr(ls->L, str, checked((int)l)));
    }

    private static TString* luaX_newstring(LexState* ls, string str)
    {
        return anchorstr(ls, luaS_new(ls->L, str));
    }

    /*
     ** increment line number and skips newline sequence (any of
     ** \n, \r, \n\r, or \r\n)
     */
    private static void inclinenumber(LexState* ls)
    {
        int old = ls->current;
        Debug.Assert(currIsNewline(ls));
        next(ls); /* skip '\n' or '\r' */
        if (currIsNewline(ls) && ls->current != old)
        {
            next(ls); /* skip '\n\r' or '\r\n' */
        }

        if (++ls->linenumber >= int.MaxValue)
        {
            lexerror(ls, "chunk has too many lines", 0);
        }
    }

    private static void luaX_setinput(lua_State* L, LexState* ls, Zio* z, TString* source, int firstchar)
    {
        ls->t.token = 0;
        ls->L = L;
        ls->current = firstchar;
        ls->lookahead.token = -1; /* no look-ahead token */
        ls->z = z;
        ls->fs = null;
        ls->linenumber = 1;
        ls->lastline = 1;
        ls->source = source;
        /* all three strings here ("_ENV", "break", "global") were fixed,
           so they cannot be collected */
        ls->envn = luaS_newliteral(L, LUA_ENV); /* get env string */
        ls->brkn = luaS_newliteral(L, "break"); /* get "break" string */
#if LUA_COMPAT_GLOBAL
        /* compatibility mode: "global" is not a reserved word */
        ls->glbn = luaS_newliteral(L, "global");  /* get "global" string */
        ls->glbn->extra = 0;  /* mark it as not reserved */
#endif
        luaZ_resizebuffer(ls->L, ls->buff, LUA_MINBUFFER); /* initialise buffer */
    }

    /*
     ** =======================================================
     ** LEXICAL ANALYZER
     ** =======================================================
     */

    private static bool check_next1(LexState* ls, int c)
    {
        if (ls->current == c)
        {
            next(ls);
            return true;
        }

        return false;
    }


    /*
     ** Check whether current char is in set 'set' (with two chars) and
     ** saves it
     */
    private static bool check_next2(LexState* ls, string set)
    {
        Debug.Assert(set.Length == 2);
        if (ls->current == set[0] || ls->current == set[1])
        {
            save_and_next(ls);
            return true;
        }

        return false;
    }

    /* LUA_NUMBER */
    /*
     ** This function is quite liberal in what it accepts, as 'luaO_str2num'
     ** will reject ill-formed numerals. Roughly, it accepts the following
     ** pattern:
     **
     **   %d(%x|%.|([Ee][+-]?))* | 0[Xx](%x|%.|([Pp][+-]?))*
     **
     ** The only tricky part is to accept [+-] only after a valid exponent
     ** mark, to avoid reading '3-4' or '0xe+1' as a single number.
     **
     ** The caller might have already read an initial dot.
     */
    private static int read_numeral(LexState* ls, SemInfo* seminfo)
    {
        string expo = "Ee";
        int first = ls->current;
        Debug.Assert(lisdigit(ls->current));
        save_and_next(ls);
        if (first == '0' && check_next2(ls, "xX")) /* hexadecimal? */
        {
            expo = "Pp";
        }

        while (true)
        {
            if (check_next2(ls, expo)) /* exponent mark? */
            {
                check_next2(ls, "-+"); /* optional exponent sign */
            }
            else if (lisxdigit(ls->current) || ls->current == '.') /* '%x|%.' */
            {
                save_and_next(ls);
            }
            else
            {
                break;
            }
        }

        if (lislalpha(ls->current)) /* is numeral touching a letter? */
        {
            save_and_next(ls); /* force an error */
        }

        save(ls, '\0');

        TValue obj;
        if (luaO_str2num(luaZ_buffer(ls->buff), &obj) == 0) /* format error? */
        {
            lexerror(ls, "malformed number", (int)RESERVED.TK_FLT);
        }

        if (ttisinteger(&obj))
        {
            seminfo->i = ivalue(&obj);
            return (int)RESERVED.TK_INT;
        }

        Debug.Assert(ttisfloat(&obj));
        seminfo->r = fltvalue(&obj);
        return (int)RESERVED.TK_FLT;
    }

    /*
     ** read a sequence '[=*[' or ']=*]', leaving the last bracket. If
     ** sequence is well formed, return its number of '='s + 2; otherwise,
     ** return 1 if it is a single bracket (no '='s and no 2nd bracket);
     ** otherwise (an unfinished '[==...') return 0.
     */
    private static long skip_sep(LexState* ls)
    {
        long count = 0;
        int s = ls->current;
        Debug.Assert(s is '[' or ']');
        save_and_next(ls);
        while (ls->current == '=')
        {
            save_and_next(ls);
            count++;
        }

        return ls->current == s ? count + 2
            : count == 0 ? 1
            : 0;
    }

    private static void read_long_string(LexState* ls, SemInfo* seminfo, long sep)
    {
        int line = ls->linenumber; /* initial line (for error message) */
        save_and_next(ls); /* skip 2nd '[' */
        if (currIsNewline(ls)) /* string starts with a newline? */
        {
            inclinenumber(ls); /* skip it */
        }

        while (true)
        {
            switch (ls->current)
            {
                case -1:
                    {
                        string what = seminfo != null ? "string" : "comment";
                        /* error */
                        string msg = luaO_pushfstring(
                            ls->L,
                            "unfinished long %s (starting at line %d)",
                            what,
                            line);
                        lexerror(ls, msg, (int)RESERVED.TK_EOS);
                        break; /* to avoid warnings */
                    }

                case ']':
                    if (skip_sep(ls) == sep)
                    {
                        save_and_next(ls); /* skip 2nd ']' */
                        goto endloop;
                    }

                    break;

                case '\n':
                case '\r':
                    save(ls, '\n');
                    inclinenumber(ls);
                    if (seminfo == null)
                    {
                        luaZ_resetbuffer(ls->buff); /* avoid wasting space */
                    }

                    break;

                default:
                    if (seminfo != null)
                    {
                        save_and_next(ls);
                    }
                    else
                    {
                        next(ls);
                    }

                    break;
            }
        }

        endloop:
        if (seminfo != null)
        {
            seminfo->ts = luaX_newstring(
                ls,
                luaZ_bufferptr(ls->buff) + sep,
                luaZ_bufflen(ls->buff) - 2 * sep);
        }
    }

    private static void esccheck(LexState* ls, bool c, string msg)
    {
        if (!c)
        {
            if (ls->current >= 0)
            {
                save_and_next(ls); /* add current to buffer for error message */
            }

            lexerror(ls, msg, (int)RESERVED.TK_STRING);
        }
    }

    private static int gethexa(LexState* ls)
    {
        save_and_next(ls);
        esccheck(ls, lisxdigit(ls->current), "hexadecimal digit expected");
        return luaO_hexavalue(ls->current);
    }

    private static int readhexaesc(LexState* ls)
    {
        int r = gethexa(ls);
        r = (r << 4) + gethexa(ls);
        luaZ_buffremove(ls->buff, 2); /* remove saved chars from buffer */
        return r;
    }

    /*
     ** When reading a UTF-8 escape sequence, save everything to the buffer
     ** for error reporting in case of errors; 'i' counts the number of
     ** saved characters, so that they can be removed if case of success.
     */
    private static uint readutf8esc(LexState* ls)
    {
        uint r;
        int i = 4; /* number of chars to be removed: start with #"\u{X" */
        save_and_next(ls); /* skip 'u' */
        esccheck(ls, ls->current == '{', "missing '{'");
        r = (uint)gethexa(ls); /* must have at least one digit */

        while (true)
        {
            save_and_next(ls);
            if (!lisxdigit(ls->current))
            {
                break;
            }

            i++;
            esccheck(ls, r <= 0x7FFFFFFFu >> 4, "UTF-8 value too large");
            r = (r << 4) + luaO_hexavalue(ls->current);
        }

        esccheck(ls, ls->current == '}', "missing '}'");
        next(ls); /* skip '}' */
        luaZ_buffremove(ls->buff, i); /* remove saved chars from buffer */
        return r;
    }

    private static void utf8esc(LexState* ls)
    {
        Span<byte> buff = stackalloc byte[UTF8BUFFSZ];
        Span<byte> result = luaO_utf8esc(buff, readutf8esc(ls));
        
        // add 'buff' to string 
        foreach (byte b in result)
        {
            save(ls, b);
        }
    }

    private static int readdecesc(LexState* ls)
    {
        int i;
        int r = 0; /* result accumulator */
        for (i = 0; i < 3 && lisdigit(ls->current); i++)
        {
            /* read up to 3 digits */
            r = 10 * r + ls->current - '0';
            save_and_next(ls);
        }

        esccheck(ls, r <= byte.MaxValue, "decimal escape too large");
        luaZ_buffremove(ls->buff, i); /* remove read digits from buffer */
        return r;
    }

    private static void read_string(LexState* ls, int del, SemInfo* seminfo)
    {
        save_and_next(ls); /* keep delimiter (for error messages) */
        while (ls->current != del)
        {
            switch (ls->current)
            {
                case -1:
                    lexerror(ls, "unfinished string", (int)RESERVED.TK_EOS);
                    break;

                case '\n':
                case '\r':
                    lexerror(ls, "unfinished string", (int)RESERVED.TK_STRING);
                    break;

                case '\\':
                    {
                        /* escape sequences */
                        int c; /* final character to be saved */
                        save_and_next(ls); /* keep '\\' for error messages */
                        switch (ls->current)
                        {
                            case 'a':
                                c = '\a';
                                goto read_save;

                            case 'b':
                                c = '\b';
                                goto read_save;

                            case 'f':
                                c = '\f';
                                goto read_save;

                            case 'n':
                                c = '\n';
                                goto read_save;

                            case 'r':
                                c = '\r';
                                goto read_save;

                            case 't':
                                c = '\t';
                                goto read_save;

                            case 'v':
                                c = '\v';
                                goto read_save;

                            case 'x':
                                c = readhexaesc(ls);
                                goto read_save;

                            case 'u':
                                utf8esc(ls);
                                goto no_save;

                            case '\n':
                            case '\r':
                                inclinenumber(ls);
                                c = '\n';
                                goto only_save;

                            case '\\':
                            case '\"':
                            case '\'':
                                c = ls->current;
                                goto read_save;

                            case -1:
                                goto no_save; /* will raise an error next loop */

                            case 'z':
                                /* zap following span of spaces */
                                luaZ_buffremove(ls->buff, 1); /* remove '\\' */
                                next(ls); /* skip the 'z' */
                                while (lisspace(ls->current))
                                {
                                    if (currIsNewline(ls))
                                    {
                                        inclinenumber(ls);
                                    }
                                    else
                                    {
                                        next(ls);
                                    }
                                }

                                goto no_save;

                            default:
                                esccheck(ls, lisdigit(ls->current), "invalid escape sequence");
                                c = readdecesc(ls); /* digital escape '\ddd' */
                                goto only_save;
                        }

                        read_save:
                        next(ls);
                        /* go through */
                        only_save:
                        luaZ_buffremove(ls->buff, 1); /* remove '\\' */
                        save(ls, c);
                        /* go through */
                        no_save:
                        break;
                    }

                default:
                    save_and_next(ls);
                    break;
            }
        }

        save_and_next(ls); /* skip delimiter */
        seminfo->ts = luaX_newstring(ls, luaZ_bufferptr(ls->buff) + 1, luaZ_bufflen(ls->buff) - 2);
    }

    private static int llex(LexState* ls, SemInfo* seminfo)
    {
        luaZ_resetbuffer(ls->buff);
        while (true)
        {
            switch (ls->current)
            {
                case '\n':
                case '\r':
                    /* line breaks */
                    inclinenumber(ls);
                    break;

                case ' ':
                case '\f':
                case '\t':
                case '\v':
                    /* spaces */
                    next(ls);
                    break;

                case '-':
                    /* '-' or '--' (comment) */
                    next(ls);
                    if (ls->current != '-')
                    {
                        return '-';
                    }

                    /* else is a comment */
                    next(ls);
                    if (ls->current == '[')
                    {
                        /* long comment? */
                        long sep = skip_sep(ls);
                        luaZ_resetbuffer(ls->buff); /* 'skip_sep' may dirty the buffer */
                        if (sep >= 2)
                        {
                            read_long_string(ls, null, sep); /* skip long comment */
                            luaZ_resetbuffer(ls->buff); /* previous call may dirty the buff. */
                            break;
                        }
                    }

                    /* else short comment */
                    while (!currIsNewline(ls) && ls->current >= 0)
                    {
                        next(ls); /* skip until end of line (or end of file) */
                    }

                    break;

                case '[':
                    {
                        /* long string or simply '[' */
                        long sep = skip_sep(ls);
                        if (sep >= 2)
                        {
                            read_long_string(ls, seminfo, sep);
                            return (int)RESERVED.TK_STRING;
                        }

                        if (sep == 0) /* '[=...' missing second bracket? */
                        {
                            lexerror(ls, "invalid long string delimiter", (int)RESERVED.TK_STRING);
                        }

                        return '[';
                    }

                case '=':
                    next(ls);
                    if (check_next1(ls, '='))
                    {
                        return (int)RESERVED.TK_EQ; /* '==' */
                    }

                    return '=';

                case '<':
                    next(ls);
                    if (check_next1(ls, '='))
                    {
                        return (int)RESERVED.TK_LE; /* '<=' */
                    }

                    if (check_next1(ls, '<'))
                    {
                        return (int)RESERVED.TK_SHL; /* '<<' */
                    }

                    return '<';

                case '>':
                    next(ls);
                    if (check_next1(ls, '='))
                    {
                        return (int)RESERVED.TK_GE; /* '>=' */
                    }

                    if (check_next1(ls, '>'))
                    {
                        return (int)RESERVED.TK_SHR; /* '>>' */
                    }

                    return '>';

                case '/':
                    next(ls);
                    if (check_next1(ls, '/'))
                    {
                        return (int)RESERVED.TK_IDIV; /* '//' */
                    }

                    return '/';

                case '~':
                    next(ls);
                    if (check_next1(ls, '='))
                    {
                        return (int)RESERVED.TK_NE; /* '~=' */
                    }

                    return '~';

                case ':':
                    next(ls);
                    if (check_next1(ls, ':'))
                    {
                        return (int)RESERVED.TK_DBCOLON; /* '::' */
                    }

                    return ':';

                case '"':
                case '\'':
                    /* short literal strings */
                    read_string(ls, ls->current, seminfo);
                    return (int)RESERVED.TK_STRING;

                case '.':
                    /* '.', '..', '...', or number */
                    save_and_next(ls);
                    if (check_next1(ls, '.'))
                    {
                        if (check_next1(ls, '.'))
                        {
                            return (int)RESERVED.TK_DOTS; /* '...' */
                        }

                        return (int)RESERVED.TK_CONCAT; /* '..' */
                    }

                    if (!lisdigit(ls->current))
                    {
                        return '.';
                    }

                    return read_numeral(ls, seminfo);

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return read_numeral(ls, seminfo);

                case -1:
                    return (int)RESERVED.TK_EOS;

                default:
                    if (lislalpha(ls->current))
                    {
                        /* identifier or reserved word? */
                        do
                        {
                            save_and_next(ls);
                        } while (lislalnum(ls->current));

                        /* find or create string */
                        TString* ts = luaS_newlstr(ls->L, luaZ_bufferptr(ls->buff), checked((int)luaZ_bufflen(ls->buff)));
                        if (isreserved(ts)) /* reserved word? */
                        {
                            return ts->extra - 1 + FIRST_RESERVED;
                        }

                        seminfo->ts = anchorstr(ls, ts);
                        return (int)RESERVED.TK_NAME;
                    }

                    /* single-char tokens ('+', '*', '%', '{', '}', ...) */
                    int c = ls->current;
                    next(ls);
                    return c;
            }
        }
    }

    private static void luaX_next(LexState* ls)
    {
        ls->lastline = ls->linenumber;
        if (ls->lookahead.token >= 0)
        {
            /* is there a look-ahead token? */
            ls->t = ls->lookahead; /* use this one */
            ls->lookahead.token = -1; /* and discharge it */
        }
        else
        {
            ls->t.token = llex(ls, &ls->t.seminfo); /* read next token */
        }
    }

    private static int luaX_lookahead(LexState* ls)
    {
        Debug.Assert(ls->lookahead.token < 0);
        ls->lookahead.token = llex(ls, &ls->lookahead.seminfo);
        return ls->lookahead.token;
    }
}
