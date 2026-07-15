namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public static unsafe partial class Lua
{
    // $Id: liolib.c $
    // Standard I/O (and system) library
    // See Copyright Notice in lua.h

    /// <summary>
    /// accepted extensions to 'mode' in 'fopen'
    /// </summary>
    private const string L_MODEEXT = "b";

    /// <summary>
    /// Check whether 'mode' matches '[rwa]%+?[L_MODEEXT]*'
    /// </summary>
    private static bool l_checkmode(ReadOnlySpan<char> mode)
    {
        if (!mode.IsEmpty && mode[0] is 'r' or 'w' or 'a')
        {
            mode = mode[1..];
            if (!mode.IsEmpty && mode[0] == '+')
            {
                mode = mode[1..]; // skip if char is '+'
            }

            int hasOther = mode.IndexOfAnyExcept(L_MODEEXT);
            return hasOther == -1;
        }
        
        return false;
    }

    // {======================================================
    // l_popen spawns a new process connected to the current
    // one through the file streams.
    // =======================================================

// #if !defined(l_popen) // {
//
// #if defined(LUA_USE_POSIX) // {
//
// #define l_popen(L,c,m)		(fflush(null), popen(c,m))
// #define l_pclose(L,file)	(pclose(file))
//
// #elif defined(LUA_USE_WINDOWS) // }{
//
// #define l_popen(L,c,m)		(_popen(c,m))
// #define l_pclose(L,file)	(_pclose(file))
//
// #if !defined(l_checkmodep)
// Windows accepts "[rw][bt]?" as valid modes
// #define l_checkmodep(m)	((m[0] == 'r' || m[0] == 'w') && \
// (m[1] == '\0' || ((m[1] == 'b' || m[1] == 't') && m[2] == '\0')))
// #endif
//
// #else // }{
//
// ISO C definitions
// #define l_popen(L,c,m)  \
// ((void)c, (void)m, \
// luaL_error(L, "'popen' not supported"), \
// (FILE*)0)
// #define l_pclose(L,file)		((void)L, (void)file, -1)
//
// #endif // }
//
// #endif // }
//
//
// #if !defined(l_checkmodep)
// By default, Lua accepts only "r" or "w" as valid modes
// #define l_checkmodep(m)        ((m[0] == 'r' || m[0] == 'w') && m[1] == '\0')
// #endif

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
        luaL_checkany(L, 1);
        luaL_Stream* p = (luaL_Stream*)luaL_testudata(L, 1, LUA_FILEHANDLE);
        if (p == null)
        {
            luaL_pushfail(L); // not a file
        }
        else if (isclosed(p))
        {
            lua_pushliteral(L, "closed file");
        }
        else
        {
            lua_pushliteral(L, "file");
        }

        return 1;
    }

    private static int f_tostring(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        if (isclosed(p))
        {
            lua_pushliteral(L, "file (closed)");
        }
        else
        {
            lua_pushfstring(L, "file (%p)", (nint)p->f);
        }

        return 1;
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

    /// <summary>
    /// When creating file handles, always creates a 'closed' file handle
    /// before opening the actual file; so, if there is a memory error, the
    /// handle is in a consistent state.
    /// </summary>
    private static luaL_Stream* newprefile(lua_State* L)
    {
        luaL_Stream* p = (luaL_Stream*)lua_newuserdatauv(L, sizeof(luaL_Stream), 0);
        p->closef = null; // mark file handle as 'closed'
        luaL_setmetatable(L, LUA_FILEHANDLE);
        return p;
    }

    /// <summary>
    /// Calls the 'close' function from a file handle.
    /// </summary>
    private static int aux_close(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        lua_CFunction cf = p->closef;
        p->closef = null; // mark stream as closed
        return cf(L); // close it
    }

    private static int f_close(lua_State* L)
    {
        tofile(L); // make sure argument is an open stream
        return aux_close(L);
    }

    private static int io_close(lua_State* L)
    {
        if (lua_isnone(L, 1)) // no argument?
        {
            lua_getfield(L, LUA_REGISTRYINDEX, IO_OUTPUT); // use default output
        }

        return f_close(L);
    }

    private static int f_gc(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        if (!isclosed(p) && p->f != null)
        {
            aux_close(L); // ignore closed and incompletely open files
        }

        return 0;
    }

    /// <summary>
    /// function to close regular files
    /// </summary>
    private static int io_fclose(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        using GCHandle<Stream> handle = GCHandle<Stream>.FromIntPtr((nint)p->f);
        try
        {
            handle.Target.Dispose();
            return luaL_fileresult(L, true, null, null);
        }
        catch (Exception e)
        {
            return luaL_fileresult(L, false, null, e);
        }
    }

    private static luaL_Stream* newfile(lua_State* L)
    {
        luaL_Stream* p = newprefile(L);
        p->f = null;
        p->closef = &io_fclose;
        return p;
    }

    private static void opencheck(lua_State* L, string fname, string mode)
    {
        luaL_Stream* p = newfile(L);
        
        (FileMode fm, FileAccess fa) = mode switch
        {
            "r" => (FileMode.Open, FileAccess.Read),
            "w" => (FileMode.OpenOrCreate, FileAccess.Write),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
        
        Stream f;
        try
        {
            f = File.Open(fname, fm, fa, FileShare.Read);
        }
        catch (Exception e)
        {
            luaL_error(L, "cannot open file '%s' (%s)", fname, e.Message);
            return;
        }
        
        p->f = new GCHandle<Stream>(f).ToPointer();
    }

    private static int io_open(lua_State* L)
    {
        string filename = luaL_checknetstring(L, 1);
        string mode = luaL_optnetstring(L, 2, "r");
        luaL_Stream* p = newfile(L);
        luaL_argcheck(L, l_checkmode(mode), 2, "invalid mode");

        (FileMode fileMode, FileAccess fileAccess) = mode[0] switch
        {
            'r' => (FileMode.Open, FileAccess.Read),
            'w' => (FileMode.Create, FileAccess.Write),
            'a' => (FileMode.Append, FileAccess.Write),
            _ => throw new ArgumentOutOfRangeException(),
        };
        
        if (mode.Length > 1 && mode[1] == '+')
        {
            fileAccess = FileAccess.ReadWrite;
        }

        FileStream f;
        try
        {
            f = File.Open(filename, fileMode, fileAccess, FileShare.Read);
        }
        catch (Exception e)
        {
            return luaL_fileresult(L, false, filename, e);
        }
        p->f = new GCHandle<Stream>(f).ToPointer();
        return 1;
    }

    /// <summary>
    /// function to close 'popen' files
    /// </summary>
    private static int io_pclose(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
// errno = 0;
// return luaL_execresult(L, l_pclose(L, p->f));
        throw new NotImplementedException();
    }

    private static int io_popen(lua_State* L)
    {
// const char *filename = luaL_checkstring(L, 1);
// const char *mode = luaL_optstring(L, 2, "r");
// LStream *p = newprefile(L);
// luaL_argcheck(L, l_checkmodep(mode), 2, "invalid mode");
// errno = 0;
// p->f = l_popen(L, filename, mode);
// p->closef = &io_pclose;
// return (p->f == null) ? luaL_fileresult(L, 0, filename) : 1;
        throw new NotImplementedException();
    }

    private static int io_tmpfile(lua_State* L)
    {
// LStream *p = newfile(L);
// errno = 0;
// p->f = tmpfile();
// return (p->f == null) ? luaL_fileresult(L, 0, null) : 1;
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

    private static int g_iofile(lua_State* L, string f, string mode)
    {
        if (!lua_isnoneornil(L, 1))
        {
            string? filename = lua_tonetstring(L, 1);
            if (filename != null)
            {
                opencheck(L, filename, mode);
            }
            else
            {
                tofile(L); // check that it's a valid file handle
                lua_pushvalue(L, 1);
            }

            lua_setfield(L, LUA_REGISTRYINDEX, f);
        }

        // return current value
        lua_getfield(L, LUA_REGISTRYINDEX, f);
        return 1;
    }

    private static int io_input(lua_State* L)
    {
        return g_iofile(L, IO_INPUT, "r");
    }

    private static int io_output(lua_State* L)
    {
        return g_iofile(L, IO_OUTPUT, "w");
    }

    /// <summary>
    /// maximum number of arguments to 'f:lines'/'io.lines' (it + 3 must fit
    /// in the limit for upvalues of a closure)
    /// </summary>
    private const int MAXARGLINE = 250;

    /// <summary>
    /// Auxiliary function to create the iteration function for 'lines'.
    /// The iteration function is a closure over 'io_readline', with
    /// the following upvalues:
    /// 1) The file being read (first value in the stack)
    /// 2) the number of arguments to read
    /// 3) a boolean, true iff file has to be closed when finished ('toclose')
    /// *) a variable number of format arguments (rest of the stack)
    /// </summary>
    private static void aux_lines(lua_State* L, bool toclose)
    {
        int n = lua_gettop(L) - 1; // number of arguments to read
        luaL_argcheck(L, n <= MAXARGLINE, MAXARGLINE + 2, "too many arguments");
        lua_pushvalue(L, 1); // file
        lua_pushinteger(L, n); // number of arguments to read
        lua_pushboolean(L, toclose); // close/not close file when finished
        lua_rotate(L, 2, 3); // move the three values to their positions
        lua_pushcclosure(L, &io_readline, 3 + n);
    }

    private static int f_lines(lua_State* L)
    {
        tofile(L); // check that it's a valid file handle
        aux_lines(L, false);
        return 1;
    }

    /// <summary>
    /// Return an iteration function for 'io.lines'. If file has to be
    /// closed, also returns the file itself as a second result (to be
    /// closed as the state at the exit of a generic for).
    /// </summary>
    private static int io_lines(lua_State* L)
    {
        bool toclose;
        if (lua_isnone(L, 1)) lua_pushnil(L); // at least one argument
        if (lua_isnil(L, 1))
        {
            // no file name?
            lua_getfield(L, LUA_REGISTRYINDEX, IO_INPUT); // get default input
            lua_replace(L, 1); // put it at index 1
            tofile(L); // check that it's a valid file handle
            toclose = false; // do not close it after iteration
        }
        else
        {
            // open a new file
            string filename = luaL_checknetstring(L, 1);
            opencheck(L, filename, "r");
            lua_replace(L, 1); // put file at index 1
            toclose = true; // close it after iteration
        }

        aux_lines(L, toclose); // push iteration function
        if (toclose)
        {
            lua_pushnil(L); // state
            lua_pushnil(L); // control
            lua_pushvalue(L, 1); // file is the to-be-closed variable (4th result)
            return 4;
        }

        return 1;
    }

    // {======================================================
    // READ
    // =======================================================

    /// <summary>
    /// auxiliary structure used by 'read_number'
    /// </summary>
    private struct RN
    {
        public Stream f; // file being read
        public int c; // current character (look ahead)
        public int n; // number of elements in buffer 'buff'
        public fixed byte buff[L_MAXLENNUM + 1]; // +1 for ending '\0'
    }

    /// <summary>
    /// Add current char to buffer (if not out of space) and read next one
    /// </summary>
    private static bool nextc(ref RN rn)
    {
        if (rn.n >= L_MAXLENNUM)
        {
            // buffer overflow?
            rn.buff[0] = 0; // invalidate result
            return false; // fail
        }

        rn.buff[rn.n++] = (byte)rn.c; // save current char
        rn.c = rn.f.ReadByte(); // read next one
        return true;
    }

    /// <summary>
    /// Accept current char if it is in 'set' (of size 2)
    /// </summary>
    private static bool test2(ref RN rn, ReadOnlySpan<byte> set)
    {
        if (rn.c == set[0] || rn.c == set[1])
        {
            return nextc(ref rn);
        }

        return false;
    }

    /// <summary>
    /// Read a sequence of (hex)digits
    /// </summary>
    private static int readdigits(ref RN rn, bool hex)
    {
        int count = 0;
        while ((hex ? char.IsAsciiHexDigit((char)rn.c) : char.IsAsciiDigit((char)rn.c)) && nextc(ref rn))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Read a number: first reads a valid prefix of a numeral into a buffer.
    /// Then it calls 'lua_stringtonumber' to check whether the format is
    /// correct and to convert it to a Lua number.
    /// </summary>
    private static bool read_number(lua_State* L, Stream f)
    {
        RN rn;
        rn.f = f;
        rn.n = 0;
        lock (rn.f)
        {
            do
            {
                rn.c = rn.f.ReadByte();
            } while (char.IsWhiteSpace((char)rn.c)); // skip spaces

            bool hex = false;
            int count = 0;

            test2(ref rn, "-+"u8); // optional sign
            if (test2(ref rn, "00"u8))
            {
                if (test2(ref rn, "xX"u8))
                {
                    hex = true; // numeral is hexadecimal
                }
                else
                {
                    count = 1; // count initial '0' as a valid digit
                }
            }

            count += readdigits(ref rn, hex); // integral part
            if (test2(ref rn, ".."u8)) // decimal point?
            {
                count += readdigits(ref rn, hex); // fractional part
            }

            if (count > 0 && test2(ref rn, hex ? "pP"u8 : "eE"u8))
            {
                // exponent mark?
                test2(ref rn, "-+"u8); // exponent sign
                readdigits(ref rn, false); // exponent digits
            }

            if (rn.c >= 0)
            {
                // unread look-ahead char
                rn.f.Seek(-1, SeekOrigin.Current);
            }
        }

        rn.buff[rn.n] = 0; // finish string
        if (lua_stringtonumber(L, new Span<byte>(rn.buff, rn.n)) != 0)
        {
            return true; // ok, it is a valid number
        }

        // invalid format
        lua_pushnil(L); // "result" to be removed
        return false; // read fails
    }

    private static bool test_eof(lua_State* L, Stream f)
    {
        lua_pushliteral(L, "");
        return f.Position < f.Length;
    }

    private static bool read_line(lua_State* L, Stream f, bool chop)
    {
        luaL_Buffer b;
        luaL_buffinit(L, &b);

        int c = 0;
        do
        {
            // may need to read several chunks to get whole line
            byte* buff = luaL_prepbuffer(&b); // preallocate buffer space
            uint i = 0;
            lock (f)
            {
                while (i < LUAL_BUFFERSIZE && (c = f.ReadByte()) >= 0 && c != '\n')
                {
                    buff[i++] = (byte)c; // read up to end of line or buffer limit
                }
            }

            luaL_addsize(&b, i);
        } while (c >= 0 && c != '\n'); // repeat until end of line

        if (!chop && c == '\n') // want a newline and have one?
        {
            luaL_addchar(&b, '\n'); // add ending newline to result
        }

        luaL_pushresult(&b); // close buffer
        // return ok if read something (either a newline or something else)
        return c == '\n' || lua_rawlen(L, -1) > 0;
    }

    private static void read_all(lua_State* L, Stream f)
    {
        luaL_Buffer b;
        luaL_buffinit(L, &b);

        int nr;
        do
        {
            // read file in chunks of LUAL_BUFFERSIZE bytes
            byte* p = luaL_prepbuffer(&b);
            nr = f.Read(new Span<byte>(p, LUAL_BUFFERSIZE));
            luaL_addsize(&b, nr);
        } while (nr == LUAL_BUFFERSIZE);

        luaL_pushresult(&b); // close buffer
    }

    private static bool read_chars(lua_State* L, Stream f, long n)
    {
        luaL_Buffer b;
        luaL_buffinit(L, &b);
        byte* p = luaL_prepbuffsize(&b, n); // prepare buffer to read whole block
        int nr = f.Read(new Span<byte>(p, checked((int)n))); // try to read 'n' chars
        luaL_addsize(&b, nr);
        luaL_pushresult(&b); // close buffer
        return nr > 0; // true iff read something
    }

    private static int g_read(lua_State* L, Stream f, int first)
    {
        int nargs = lua_gettop(L) - 1;

        int n;
        bool success;
        try
        {
            if (nargs == 0)
            {
                // no arguments?
                success = read_line(L, f, true);
                n = first + 1; // to return 1 result
            }
            else
            {
                // ensure stack space for all results and for auxlib's buffer
                luaL_checkstack(L, nargs + LUA_MINSTACK, "too many arguments");
                success = true;
                for (n = first; nargs-- > 0 && success; n++)
                {
                    if (lua_type(L, n) == LUA_TNUMBER)
                    {
                        long l = luaL_checkinteger(L, n);
                        success = l == 0 ? test_eof(L, f) : read_chars(L, f, l);
                    }
                    else
                    {
                        ReadOnlySpan<byte> p = luaL_checkstring(L, n);
                        if (!p.IsEmpty && p[0] == '*')
                        {
                            p = p[1..]; // skip optional '*' (for compatibility)
                        }

                        switch (p.IsEmpty ? 0 : p[0])
                        {
                            case 'n': // number
                                success = read_number(L, f);
                                break;

                            case 'l': // line
                                success = read_line(L, f, true);
                                break;

                            case 'L': // line with end-of-line
                                success = read_line(L, f, false);
                                break;

                            case 'a': // file
                                read_all(L, f); // read entire file
                                success = true; // always success
                                break;

                            default:
                                return luaL_argerror(L, n, "invalid format");
                        }
                    }
                }
            }
        }
        catch (IOException e)
        {
            return luaL_fileresult(L, false, null, e);
        }
        catch (NotSupportedException e)
        {
            return luaL_fileresult(L, false, null, e);
        }

        if (!success)
        {
            lua_pop(L, 1); // remove last result
            luaL_pushfail(L); // push nil instead
        }

        return n - first;
    }

    private static int io_read(lua_State* L)
    {
        return g_read(L, getiofile(L, IO_INPUT), 1);
    }

    private static int f_read(lua_State* L)
    {
        return g_read(L, tofile(L), 2);
    }

    /// <summary>
    /// Iteration function for 'lines'.
    /// </summary>
    private static int io_readline(lua_State* L)
    {
        luaL_Stream* p = (luaL_Stream*)lua_touserdata(L, lua_upvalueindex(1));
        int n = (int)lua_tointeger(L, lua_upvalueindex(2));
        if (isclosed(p)) // file is already closed?
        {
            return luaL_error(L, "file is already closed");
        }

        lua_settop(L, 1);
        luaL_checkstack(L, n, "too many arguments");
        for (int i = 1; i <= n; i++) // push arguments to 'g_read'
        {
            lua_pushvalue(L, lua_upvalueindex(3 + i));
        }

        n = g_read(L, GCHandle<Stream>.FromIntPtr((nint)p->f).Target, 2); // 'n' is number of results
        Debug.Assert(n > 0); // should return at least a nil
        if (lua_toboolean(L, -n)) // read at least one value?
        {
            return n; // return them
        }

        // first result is false: EOF or error
        if (n > 1)
        {
            // is there error information?
            // 2nd result is error message
            return luaL_error(L, "%s", lua_tonetstring(L, -n + 1));
        }

        if (lua_toboolean(L, lua_upvalueindex(3)))
        {
            // generator created file?
            lua_settop(L, 0); // clear stack
            lua_pushvalue(L, lua_upvalueindex(1)); // push file at index 1
            aux_close(L); // close it
        }

        return 0;
    }

    private static int g_write(lua_State* L, Stream f, int arg)
    {
        Span<byte> buff = stackalloc byte[LUA_N2SBUFFSZ];

        int nargs = lua_gettop(L) - arg;
        long totalbytes = 0; // total number of bytes written
        for (; nargs-- > 0; arg++)
        {
            // for each argument
            int len = lua_numbertocstring(L, arg, buff); // try as a number
            ReadOnlySpan<byte> s;
            if (len > 0)
            {
                // did conversion work (value was a number)?
                s = buff[..(len - 1)];
            }
            else
            {
                // must be a string
                s = luaL_checklstring(L, arg);
            }

            // bytes written in one call to 'fwrite'
            try
            {
                f.Write(s);
            }
            catch (Exception e)
            {
                int n = luaL_fileresult(L, false, null, e);
                lua_pushinteger(L, totalbytes);
                return n + 1; // return fail, error msg., error code, and counter
            }

            totalbytes += s.Length;
        }

        return 1; // no errors; file handle already on stack top
    }

    private static int io_write(lua_State* L)
    {
        return g_write(L, getiofile(L, IO_OUTPUT), 1);
    }

    private static int f_write(lua_State* L)
    {
        Stream f = tofile(L);
        lua_pushvalue(L, 1); // push file at the stack top (to be returned)
        return g_write(L, f, 2);
    }

    private static readonly SeekOrigin[] mode = [SeekOrigin.Begin, SeekOrigin.Current, SeekOrigin.End];
    private static readonly string[] modenames = ["set", "cur", "end"];

    private static int f_seek(lua_State* L)
    {
        Stream f = tofile(L);
        long op = luaL_checkoption(L, 2, "cur", modenames);
        long offset = luaL_optinteger(L, 3, 0);
        try
        {
            op = f.Seek(offset, mode[op]);
        }
        catch (Exception e)
        {
            return luaL_fileresult(L, false, null, e); // error
        }

        lua_pushinteger(L, op);
        return 1;
    }

    private static int f_setvbuf(lua_State* L)
    {
// static const int mode[] = {_IONBF, _IOFBF, _IOLBF};
// static const char *const modenames[] = {"no", "full", "line", null};
// FILE *f = tofile(L);
// int op = luaL_checkoption(L, 2, null, modenames);
// long sz = luaL_optinteger(L, 3, LUAL_BUFFERSIZE);
// int res;
// errno = 0;
// res = setvbuf(f, null, mode[op], (size_t)sz);
// return luaL_fileresult(L, res == 0, null);
        throw new NotImplementedException();
    }

// static int aux_flush (lua_State *L, FILE *f) {
// errno = 0;
// return luaL_fileresult(L, fflush(f) == 0, null);
// }

    private static int f_flush(lua_State* L)
    {
// return aux_flush(L, tofile(L));
        throw new NotImplementedException();
    }

    private static int io_flush(lua_State* L)
    {
// return aux_flush(L, getiofile(L, IO_OUTPUT));
        throw new NotImplementedException();
    }

    /// <summary>
    /// functions for 'io' library
    /// </summary>
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

    /// <summary>
    /// methods for file handles
    /// </summary>
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

    /// <summary>
    /// metamethods for file handles
    /// </summary>
    private static readonly luaL_Reg[] metameth =
    [
        new("__index", null), // placeholder
        new("__gc", &f_gc),
        new("__close", &f_gc),
        new("__tostring", &f_tostring),
    ];

    private static void createmeta(lua_State* L)
    {
        luaL_newmetatable(L, LUA_FILEHANDLE); // metatable for file handles
        luaL_setfuncs(L, metameth, 0); // add metamethods to new metatable
        luaL_newlibtable(L, meth); // create method table
        luaL_setfuncs(L, meth, 0); // add file methods to method table
        lua_setfield(L, -2, "__index"); // metatable.__index = method table
        lua_pop(L, 1); // pop metatable
    }

    /// <summary>
    /// function to (not) close the standard files stdin, stdout, and stderr
    /// </summary>
    private static int io_noclose(lua_State* L)
    {
        luaL_Stream* p = tolstream(L);
        p->closef = &io_noclose; // keep file opened
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
            lua_setfield(L, LUA_REGISTRYINDEX, k); // add file to registry
        }

        lua_setfield(L, -2, fname); // add file to module
    }

    public static int luaopen_io(lua_State* L)
    {
        luaL_newlib(L, iolib); // new module
        createmeta(L);
        // create (and set) default files
        createstdfile(L, new InputConsoleStreamWrapper(Console.In), IO_INPUT, "stdin");
        createstdfile(L, new OutputConsoleStreamWrapper(Console.Out), IO_OUTPUT, "stdout");
        createstdfile(L, new OutputConsoleStreamWrapper(Console.Error), null, "stderr");
        return 1;
    }
}
