namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.InteropServices;

public static unsafe partial class Lua
{
    /*
    ** $Id: liolib.c $
    ** Standard I/O (and system) library
    ** See Copyright Notice in lua.h
    */

// /*
// ** Change this macro to accept other modes for 'fopen' besides
// ** the standard ones.
// */
// #if !defined(l_checkmode)
//
// /* accepted extensions to 'mode' in 'fopen' */
// #if !defined(L_MODEEXT)
// #define L_MODEEXT	"b"
// #endif
//
// /* Check whether 'mode' matches '[rwa]%+?[L_MODEEXT]*' */
// static int l_checkmode (const char *mode) {
//   return (*mode != '\0' && strchr("rwa", *(mode++)) != null &&
//          (*mode != '+' || ((void)(++mode), 1)) &&  /* skip if char is '+' */
//          (strspn(mode, L_MODEEXT) == strlen(mode)));  /* check extensions */
// }
//
// #endif
//
// /*
// ** {======================================================
// ** l_popen spawns a new process connected to the current
// ** one through the file streams.
// ** =======================================================
// */
//
// #if !defined(l_popen)		/* { */
//
// #if defined(LUA_USE_POSIX)	/* { */
//
// #define l_popen(L,c,m)		(fflush(null), popen(c,m))
// #define l_pclose(L,file)	(pclose(file))
//
// #elif defined(LUA_USE_WINDOWS)	/* }{ */
//
// #define l_popen(L,c,m)		(_popen(c,m))
// #define l_pclose(L,file)	(_pclose(file))
//
// #if !defined(l_checkmodep)
// /* Windows accepts "[rw][bt]?" as valid modes */
// #define l_checkmodep(m)	((m[0] == 'r' || m[0] == 'w') && \
//   (m[1] == '\0' || ((m[1] == 'b' || m[1] == 't') && m[2] == '\0')))
// #endif
//
// #else				/* }{ */
//
// /* ISO C definitions */
// #define l_popen(L,c,m)  \
// 	  ((void)c, (void)m, \
// 	  luaL_error(L, "'popen' not supported"), \
// 	  (FILE*)0)
// #define l_pclose(L,file)		((void)L, (void)file, -1)
//
// #endif				/* } */
//
// #endif				/* } */
//
//
// #if !defined(l_checkmodep)
// /* By default, Lua accepts only "r" or "w" as valid modes */
// #define l_checkmodep(m)        ((m[0] == 'r' || m[0] == 'w') && m[1] == '\0')
// #endif
//
// /* }====================================================== */
//
//
// #if !defined(l_getc)		/* { */
//
// #if defined(LUA_USE_POSIX)
// #define l_getc(f)		getc_unlocked(f)
// #define l_lockfile(f)		flockfile(f)
// #define l_unlockfile(f)		funlockfile(f)
// #else
// #define l_getc(f)		getc(f)
// #define l_lockfile(f)		((void)0)
// #define l_unlockfile(f)		((void)0)
// #endif
//
// #endif				/* } */

    private const string IO_PREFIX = "_IO_";
    private const string IO_INPUT =	IO_PREFIX + "input";
    private const string IO_OUTPUT =IO_PREFIX + "output";

    private static luaL_Stream* tolstream(lua_State* L)
    {
        return (luaL_Stream*)luaL_checkudata(L, 1, LUA_FILEHANDLE);
    }

    private static bool isclosed(luaL_Stream* p)
    {
        return p->closef == null;
    }

    private static int io_type(lua_State* L)
    {
//   LStream *p;
//   luaL_checkany(L, 1);
//   p = (LStream *)luaL_testudata(L, 1, LUA_FILEHANDLE);
//   if (p == null)
//     luaL_pushfail(L);  /* not a file */
//   else if (isclosed(p))
//     lua_pushliteral(L, "closed file");
//   else
//     lua_pushliteral(L, "file");
//   return 1;
        throw new NotImplementedException();
    }

    private static int f_tostring(lua_State* L)
    {
//   LStream *p = tolstream(L);
//   if (isclosed(p))
//     lua_pushliteral(L, "file (closed)");
//   else
//     lua_pushfstring(L, "file (%p)", p->f);
//   return 1;
        throw new NotImplementedException();
    }

    private static Stream tofile(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        if (isclosed(p))
        {
            luaL_error(L, "attempt to use a closed file");
        }
        
        Debug.Assert(p->f != null);
        return GCHandle<Stream>.FromIntPtr((nint)p->f).Target;
    }

    /*
     ** When creating file handles, always creates a 'closed' file handle
     ** before opening the actual file; so, if there is a memory error, the
     ** handle is in a consistent state.
     */
    private static luaL_Stream* newprefile(lua_State* L)
    {
        luaL_Stream* p = (luaL_Stream*)lua_newuserdatauv(L, sizeof(luaL_Stream), 0);
        p->closef = null; /* mark file handle as 'closed' */
        luaL_setmetatable(L, LUA_FILEHANDLE);
        return p;
    }

    /*
    ** Calls the 'close' function from a file handle.
    */
    private static int aux_close(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        lua_CFunction cf = p->closef;
        p->closef = null; /* mark stream as closed */
        return cf(L); /* close it */
    }

    private static int f_close(lua_State* L)
    {
//   tofile(L);  /* make sure argument is an open stream */
//   return aux_close(L);
        throw new NotImplementedException();
    }

    private static int io_close(lua_State* L)
    {
//   if (lua_isnone(L, 1))  /* no argument? */
//     lua_getfield(L, LUA_REGISTRYINDEX, IO_OUTPUT);  /* use default output */
//   return f_close(L);
        throw new NotImplementedException();
    }

    private static int f_gc(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        if (!isclosed(p) && p->f != null)
        {
            aux_close(L); /* ignore closed and incompletely open files */
        }

        return 0;
    }

    /*
     ** function to close regular files
     */
    private static int io_fclose(lua_State* L)
    {
//   LStream *p = tolstream(L);
//   errno = 0;
//   return luaL_fileresult(L, (fclose(p->f) == 0), null);
        throw new NotImplementedException();
    }

// static LStream *newfile (lua_State *L) {
//   LStream *p = newprefile(L);
//   p->f = null;
//   p->closef = &io_fclose;
//   return p;
// }
//
//
// static void opencheck (lua_State *L, const char *fname, const char *mode) {
//   LStream *p = newfile(L);
//   p->f = fopen(fname, mode);
//   if (l_unlikely(p->f == null))
//     luaL_error(L, "cannot open file '%s' (%s)", fname, strerror(errno));
// }

    private static int io_open(lua_State* L)
    {
//   const char *filename = luaL_checkstring(L, 1);
//   const char *mode = luaL_optstring(L, 2, "r");
//   LStream *p = newfile(L);
//   const char *md = mode;  /* to traverse/check mode */
//   luaL_argcheck(L, l_checkmode(md), 2, "invalid mode");
//   errno = 0;
//   p->f = fopen(filename, mode);
//   return (p->f == null) ? luaL_fileresult(L, 0, filename) : 1;
        throw new NotImplementedException();
    }

    /*
     ** function to close 'popen' files
     */
    private static int io_pclose(lua_State* L)
    {
//   LStream *p = tolstream(L);
//   errno = 0;
//   return luaL_execresult(L, l_pclose(L, p->f));
        throw new NotImplementedException();
    }

    private static int io_popen(lua_State* L)
    {
//   const char *filename = luaL_checkstring(L, 1);
//   const char *mode = luaL_optstring(L, 2, "r");
//   LStream *p = newprefile(L);
//   luaL_argcheck(L, l_checkmodep(mode), 2, "invalid mode");
//   errno = 0;
//   p->f = l_popen(L, filename, mode);
//   p->closef = &io_pclose;
//   return (p->f == null) ? luaL_fileresult(L, 0, filename) : 1;
        throw new NotImplementedException();
    }

    private static int io_tmpfile(lua_State* L)
    {
//   LStream *p = newfile(L);
//   errno = 0;
//   p->f = tmpfile();
//   return (p->f == null) ? luaL_fileresult(L, 0, null) : 1;
        throw new NotImplementedException();
    }

    private static Stream getiofile(lua_State* L, string findex)
    {
        lua_getfield(L, LUA_REGISTRYINDEX, findex);
        luaL_Stream* p = (luaL_Stream*)lua_touserdata(L, -1);
        if (isclosed(p))
        {
            luaL_error(L, "default %s file is closed", findex + IO_PREFIX.Length);
        }

        return GCHandle<Stream>.FromIntPtr((nint)p->f).Target;
    }

// static int g_iofile (lua_State *L, const char *f, const char *mode) {
//   if (!lua_isnoneornil(L, 1)) {
//     const char *filename = lua_tostring(L, 1);
//     if (filename)
//       opencheck(L, filename, mode);
//     else {
//       tofile(L);  /* check that it's a valid file handle */
//       lua_pushvalue(L, 1);
//     }
//     lua_setfield(L, LUA_REGISTRYINDEX, f);
//   }
//   /* return current value */
//   lua_getfield(L, LUA_REGISTRYINDEX, f);
//   return 1;
// }

    private static int io_input(lua_State* L)
    {
//   return g_iofile(L, IO_INPUT, "r");
        throw new NotImplementedException();
    }

    private static int io_output(lua_State* L)
    {
//   return g_iofile(L, IO_OUTPUT, "w");
        throw new NotImplementedException();
    }

    private static partial int io_readline(lua_State* L);


// /*
// ** maximum number of arguments to 'f:lines'/'io.lines' (it + 3 must fit
// ** in the limit for upvalues of a closure)
// */
// #define MAXARGLINE	250
//
// /*
// ** Auxiliary function to create the iteration function for 'lines'.
// ** The iteration function is a closure over 'io_readline', with
// ** the following upvalues:
// ** 1) The file being read (first value in the stack)
// ** 2) the number of arguments to read
// ** 3) a boolean, true iff file has to be closed when finished ('toclose')
// ** *) a variable number of format arguments (rest of the stack)
// */
// static void aux_lines (lua_State *L, int toclose) {
//   int n = lua_gettop(L) - 1;  /* number of arguments to read */
//   luaL_argcheck(L, n <= MAXARGLINE, MAXARGLINE + 2, "too many arguments");
//   lua_pushvalue(L, 1);  /* file */
//   lua_pushinteger(L, n);  /* number of arguments to read */
//   lua_pushboolean(L, toclose);  /* close/not close file when finished */
//   lua_rotate(L, 2, 3);  /* move the three values to their positions */
//   lua_pushcclosure(L, io_readline, 3 + n);
// }

    private static int f_lines(lua_State* L)
    {
//   tofile(L);  /* check that it's a valid file handle */
//   aux_lines(L, 0);
//   return 1;
        throw new NotImplementedException();
    }

    /*
     ** Return an iteration function for 'io.lines'. If file has to be
     ** closed, also returns the file itself as a second result (to be
     ** closed as the state at the exit of a generic for).
     */
    private static int io_lines(lua_State* L)
    {
//   int toclose;
        if (lua_isnone(L, 1)) lua_pushnil(L); /* at least one argument */
        if (lua_isnil(L, 1))
        {
            /* no file name? */
            lua_getfield(L, LUA_REGISTRYINDEX, IO_INPUT); /* get default input */
            lua_replace(L, 1); /* put it at index 1 */
//     tofile(L);  /* check that it's a valid file handle */
//     toclose = 0;  /* do not close it after iteration */
            throw new NotImplementedException();
        }
        else
        {
            /* open a new file */
            string filename = luaL_checknetstring(L, 1);
//     opencheck(L, filename, "r");
//     lua_replace(L, 1);  /* put file at index 1 */
//     toclose = 1;  /* close it after iteration */
            throw new NotImplementedException();
        }

//   aux_lines(L, toclose);  /* push iteration function */
//   if (toclose) {
//     lua_pushnil(L);  /* state */
//     lua_pushnil(L);  /* control */
//     lua_pushvalue(L, 1);  /* file is the to-be-closed variable (4th result) */
//     return 4;
//   }
//   else
//     return 1;
        throw new NotImplementedException();
    }

    /*
     ** {======================================================
     ** READ
     ** =======================================================
     */

// /* maximum length of a numeral */
// #if !defined (L_MAXLENNUM)
// #define L_MAXLENNUM     200
// #endif
//
//
// /* auxiliary structure used by 'read_number' */
// typedef struct {
//   FILE *f;  /* file being read */
//   int c;  /* current character (look ahead) */
//   int n;  /* number of elements in buffer 'buff' */
//   char buff[L_MAXLENNUM + 1];  /* +1 for ending '\0' */
// } RN;
//
//
// /*
// ** Add current char to buffer (if not out of space) and read next one
// */
// static int nextc (RN *rn) {
//   if (l_unlikely(rn->n >= L_MAXLENNUM)) {  /* buffer overflow? */
//     rn->buff[0] = '\0';  /* invalidate result */
//     return 0;  /* fail */
//   }
//   else {
//     rn->buff[rn->n++] = cast_char(rn->c);  /* save current char */
//     rn->c = l_getc(rn->f);  /* read next one */
//     return 1;
//   }
// }
//
//
// /*
// ** Accept current char if it is in 'set' (of size 2)
// */
// static int test2 (RN *rn, const char *set) {
//   if (rn->c == set[0] || rn->c == set[1])
//     return nextc(rn);
//   else return 0;
// }
//
//
// /*
// ** Read a sequence of (hex)digits
// */
// static int readdigits (RN *rn, int hex) {
//   int count = 0;
//   while ((hex ? isxdigit(rn->c) : isdigit(rn->c)) && nextc(rn))
//     count++;
//   return count;
// }
//
//
// /*
// ** Read a number: first reads a valid prefix of a numeral into a buffer.
// ** Then it calls 'lua_stringtonumber' to check whether the format is
// ** correct and to convert it to a Lua number.
// */
// static int read_number (lua_State *L, FILE *f) {
//   RN rn;
//   int count = 0;
//   int hex = 0;
//   char decp[2];
//   rn.f = f; rn.n = 0;
//   decp[0] = lua_getlocaledecpoint();  /* get decimal point from locale */
//   decp[1] = '.';  /* always accept a dot */
//   l_lockfile(rn.f);
//   do { rn.c = l_getc(rn.f); } while (isspace(rn.c));  /* skip spaces */
//   test2(&rn, "-+");  /* optional sign */
//   if (test2(&rn, "00")) {
//     if (test2(&rn, "xX")) hex = 1;  /* numeral is hexadecimal */
//     else count = 1;  /* count initial '0' as a valid digit */
//   }
//   count += readdigits(&rn, hex);  /* integral part */
//   if (test2(&rn, decp))  /* decimal point? */
//     count += readdigits(&rn, hex);  /* fractional part */
//   if (count > 0 && test2(&rn, (hex ? "pP" : "eE"))) {  /* exponent mark? */
//     test2(&rn, "-+");  /* exponent sign */
//     readdigits(&rn, 0);  /* exponent digits */
//   }
//   ungetc(rn.c, rn.f);  /* unread look-ahead char */
//   l_unlockfile(rn.f);
//   rn.buff[rn.n] = '\0';  /* finish string */
//   if (l_likely(lua_stringtonumber(L, rn.buff)))
//     return 1;  /* ok, it is a valid number */
//   else {  /* invalid format */
//    lua_pushnil(L);  /* "result" to be removed */
//    return 0;  /* read fails */
//   }
// }
//
//
// static int test_eof (lua_State *L, FILE *f) {
//   int c = getc(f);
//   ungetc(c, f);  /* no-op when c == EOF */
//   lua_pushliteral(L, "");
//   return (c != EOF);
// }
//
//
// static int read_line (lua_State *L, FILE *f, int chop) {
//   luaL_Buffer b;
//   int c;
//   luaL_buffinit(L, &b);
//   do {  /* may need to read several chunks to get whole line */
//     char *buff = luaL_prepbuffer(&b);  /* preallocate buffer space */
//     unsigned i = 0;
//     l_lockfile(f);  /* no memory errors can happen inside the lock */
//     while (i < LUAL_BUFFERSIZE && (c = l_getc(f)) != EOF && c != '\n')
//       buff[i++] = cast_char(c);  /* read up to end of line or buffer limit */
//     l_unlockfile(f);
//     luaL_addsize(&b, i);
//   } while (c != EOF && c != '\n');  /* repeat until end of line */
//   if (!chop && c == '\n')  /* want a newline and have one? */
//     luaL_addchar(&b, '\n');  /* add ending newline to result */
//   luaL_pushresult(&b);  /* close buffer */
//   /* return ok if read something (either a newline or something else) */
//   return (c == '\n' || lua_rawlen(L, -1) > 0);
// }
//
//
// static void read_all (lua_State *L, FILE *f) {
//   size_t nr;
//   luaL_Buffer b;
//   luaL_buffinit(L, &b);
//   do {  /* read file in chunks of LUAL_BUFFERSIZE bytes */
//     char *p = luaL_prepbuffer(&b);
//     nr = fread(p, sizeof(char), LUAL_BUFFERSIZE, f);
//     luaL_addsize(&b, nr);
//   } while (nr == LUAL_BUFFERSIZE);
//   luaL_pushresult(&b);  /* close buffer */
// }
//
//
// static int read_chars (lua_State *L, FILE *f, size_t n) {
//   size_t nr;  /* number of chars actually read */
//   char *p;
//   luaL_Buffer b;
//   luaL_buffinit(L, &b);
//   p = luaL_prepbuffsize(&b, n);  /* prepare buffer to read whole block */
//   nr = fread(p, sizeof(char), n, f);  /* try to read 'n' chars */
//   luaL_addsize(&b, nr);
//   luaL_pushresult(&b);  /* close buffer */
//   return (nr > 0);  /* true iff read something */
// }
//
//
// static int g_read (lua_State *L, FILE *f, int first) {
//   int nargs = lua_gettop(L) - 1;
//   int n, success;
//   clearerr(f);
//   errno = 0;
//   if (nargs == 0) {  /* no arguments? */
//     success = read_line(L, f, 1);
//     n = first + 1;  /* to return 1 result */
//   }
//   else {
//     /* ensure stack space for all results and for auxlib's buffer */
//     luaL_checkstack(L, nargs+LUA_MINSTACK, "too many arguments");
//     success = 1;
//     for (n = first; nargs-- && success; n++) {
//       if (lua_type(L, n) == LUA_TNUMBER) {
//         size_t l = (size_t)luaL_checkinteger(L, n);
//         success = (l == 0) ? test_eof(L, f) : read_chars(L, f, l);
//       }
//       else {
//         const char *p = luaL_checkstring(L, n);
//         if (*p == '*') p++;  /* skip optional '*' (for compatibility) */
//         switch (*p) {
//           case 'n':  /* number */
//             success = read_number(L, f);
//             break;
//           case 'l':  /* line */
//             success = read_line(L, f, 1);
//             break;
//           case 'L':  /* line with end-of-line */
//             success = read_line(L, f, 0);
//             break;
//           case 'a':  /* file */
//             read_all(L, f);  /* read entire file */
//             success = 1; /* always success */
//             break;
//           default:
//             return luaL_argerror(L, n, "invalid format");
//         }
//       }
//     }
//   }
//   if (ferror(f))
//     return luaL_fileresult(L, 0, null);
//   if (!success) {
//     lua_pop(L, 1);  /* remove last result */
//     luaL_pushfail(L);  /* push nil instead */
//   }
//   return n - first;
// }

    private static int io_read(lua_State* L)
    {
//   return g_read(L, getiofile(L, IO_INPUT), 1);
        throw new NotImplementedException();
    }

    private static int f_read(lua_State* L)
    {
//   return g_read(L, tofile(L), 2);
        throw new NotImplementedException();
    }

/*
 ** Iteration function for 'lines'.
 */
    private static partial int io_readline(lua_State* L)
    {
//   LStream *p = (LStream *)lua_touserdata(L, lua_upvalueindex(1));
//   int i;
//   int n = (int)lua_tointeger(L, lua_upvalueindex(2));
//   if (isclosed(p))  /* file is already closed? */
//     return luaL_error(L, "file is already closed");
//   lua_settop(L , 1);
//   luaL_checkstack(L, n, "too many arguments");
//   for (i = 1; i <= n; i++)  /* push arguments to 'g_read' */
//     lua_pushvalue(L, lua_upvalueindex(3 + i));
//   n = g_read(L, p->f, 2);  /* 'n' is number of results */
//   Debug.Assert(n > 0);  /* should return at least a nil */
//   if (lua_toboolean(L, -n))  /* read at least one value? */
//     return n;  /* return them */
//   else {  /* first result is false: EOF or error */
//     if (n > 1) {  /* is there error information? */
//       /* 2nd result is error message */
//       return luaL_error(L, "%s", lua_tostring(L, -n + 1));
//     }
//     if (lua_toboolean(L, lua_upvalueindex(3))) {  /* generator created file? */
//       lua_settop(L, 0);  /* clear stack */
//       lua_pushvalue(L, lua_upvalueindex(1));  /* push file at index 1 */
//       aux_close(L);  /* close it */
//     }
//     return 0;
//   }
        throw new NotImplementedException();
    }

    private static int g_write(lua_State* L, Stream f, int arg)
    {
        Span<byte> buff = stackalloc byte[LUA_N2SBUFFSZ];

        int nargs = lua_gettop(L) - arg;
        long totalbytes = 0; /* total number of bytes written */
        for (; nargs-- > 0; arg++)
        {
            /* for each argument */
//     const char *s;
//     size_t numbytes;  /* bytes written in one call to 'fwrite' */
            // int len = lua_numbertocstring(L, arg, buff);  /* try as a number */
//     if (len > 0) {  /* did conversion work (value was a number)? */
//       s = buff;
//       len--;
//     }
//     else  /* must be a string */
//       s = luaL_checklstring(L, arg, &len);
//     numbytes = fwrite(s, sizeof(char), len, f);
//     totalbytes += numbytes;
//     if (numbytes < len) {  /* write error? */
//       int n = luaL_fileresult(L, 0, null);
//       lua_pushinteger(L, cast_st2S(totalbytes));
//       return n + 1;  /* return fail, error msg., error code, and counter */
//     }
            throw new NotImplementedException();
        }

        return 1; /* no errors; file handle already on stack top */
    }

    private static int io_write(lua_State* L)
    {
        return g_write(L, getiofile(L, IO_OUTPUT), 1);
    }

    private static int f_write(lua_State* L)
    {
        Stream f = tofile(L);
        lua_pushvalue(L, 1);  /* push file at the stack top (to be returned) */
        return g_write(L, f, 2);
    }

    private static int f_seek(lua_State* L)
    {
//   static const int mode[] = {SEEK_SET, SEEK_CUR, SEEK_END};
//   static const char *const modenames[] = {"set", "cur", "end", null};
//   FILE *f = tofile(L);
//   int op = luaL_checkoption(L, 2, "cur", modenames);
//   long p3 = luaL_optinteger(L, 3, 0);
//   l_seeknum offset = (l_seeknum)p3;
//   luaL_argcheck(L, (long)offset == p3, 3,
//                   "not an integer in proper range");
//   errno = 0;
//   op = l_fseek(f, offset, mode[op]);
//   if (l_unlikely(op))
//     return luaL_fileresult(L, 0, null);  /* error */
//   else {
//     lua_pushinteger(L, (long)l_ftell(f));
//     return 1;
//   }
        throw new NotImplementedException();
    }

    private static int f_setvbuf(lua_State* L)
    {
//   static const int mode[] = {_IONBF, _IOFBF, _IOLBF};
//   static const char *const modenames[] = {"no", "full", "line", null};
//   FILE *f = tofile(L);
//   int op = luaL_checkoption(L, 2, null, modenames);
//   long sz = luaL_optinteger(L, 3, LUAL_BUFFERSIZE);
//   int res;
//   errno = 0;
//   res = setvbuf(f, null, mode[op], (size_t)sz);
//   return luaL_fileresult(L, res == 0, null);
        throw new NotImplementedException();
    }

// static int aux_flush (lua_State *L, FILE *f) {
//   errno = 0;
//   return luaL_fileresult(L, fflush(f) == 0, null);
// }

    private static int f_flush(lua_State* L)
    {
//   return aux_flush(L, tofile(L));
        throw new NotImplementedException();
    }

    private static int io_flush(lua_State* L)
    {
//   return aux_flush(L, getiofile(L, IO_OUTPUT));
        throw new NotImplementedException();
    }

    /*
     ** functions for 'io' library
     */
    private static readonly luaL_Reg[] iolib =
    [
        new("close", &io_close),
        new("flush", &io_flush),
        new("input", &io_input),
        new("lines", &io_lines),
        new("open", &io_open),
        new("output", &io_output),
        new("popen", &io_popen),
        new("read", &io_read),
        new("tmpfile", &io_tmpfile),
        new("type", &io_type),
        new("write", &io_write),
    ];

    /*
    ** methods for file handles
    */
    private static readonly luaL_Reg[] meth =
    [
        new("read", &f_read),
        new("write", &f_write),
        new("lines", &f_lines),
        new("flush", &f_flush),
        new("seek", &f_seek),
        new("close", &f_close),
        new("setvbuf", &f_setvbuf),
    ];

    /*
    ** metamethods for file handles
    */
    private static readonly luaL_Reg[] metameth =
    [
        new("__index", null), /* placeholder */
        new("__gc", &f_gc),
        new("__close", &f_gc),
        new("__tostring", &f_tostring),
    ];

    private static void createmeta(lua_State* L)
    {
        luaL_newmetatable(L, LUA_FILEHANDLE); /* metatable for file handles */
        luaL_setfuncs(L, metameth, 0); /* add metamethods to new metatable */
        luaL_newlibtable(L, meth); /* create method table */
        luaL_setfuncs(L, meth, 0); /* add file methods to method table */
        lua_setfield(L, -2, "__index"); /* metatable.__index = method table */
        lua_pop(L, 1); /* pop metatable */
    }

    /*
    ** function to (not) close the standard files stdin, stdout, and stderr
    */
    private static int io_noclose(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        p->closef = &io_noclose; /* keep file opened */
        luaL_pushfail(L);
        lua_pushliteral(L, "cannot close standard file");
        return 2;
    }

    private static void createstdfile(lua_State* L, Stream f, string? k, string fname)
    {
        luaL_Stream* p = newprefile(L);
        p->f = new GCHandle<Stream>(f).ToPointer();
        p->closef = &io_noclose;
        if (k != null)
        {
            lua_pushvalue(L, -1);
            lua_setfield(L, LUA_REGISTRYINDEX, k); /* add file to registry */
        }

        lua_setfield(L, -2, fname); /* add file to module */
    }

    public static partial int luaopen_io(lua_State* L)
    {
        luaL_newlib(L, iolib); /* new module */
        createmeta(L);
        /* create (and set) default files */
        createstdfile(L, Console.OpenStandardInput(), IO_INPUT, "stdin");
        createstdfile(L, Console.OpenStandardOutput(), IO_OUTPUT, "stdout");
        createstdfile(L, Console.OpenStandardError(), null, "stderr");
        return 1;
    }
}
