namespace DigitalSingularity.Lua.Cli;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using static Lua;

public static unsafe class Program
{
    private const string LUA_PROGNAME = "lua";

    private const string LUA_INIT_VAR = "LUA_INIT";

    private static readonly string LUA_INITVARVERSION = LUA_INIT_VAR + LUA_VERSUFFIX;

    private static lua_State* globalL;

    private static string progname = LUA_PROGNAME;

// #if defined(LUA_USE_POSIX)   /* { */
//
// /*
// ** Use 'sigaction' when available.
// */
// static void setsignal (int sig, void (*handler)(int)) {
//   struct sigaction sa;
//   sa.sa_handler = handler;
//   sa.sa_flags = 0;
//   sigemptyset(&sa.sa_mask);  /* do not mask any signal */
//   sigaction(sig, &sa, null);
// }
//
// #else           /* }{ */
//
// #define setsignal            signal
//
// #endif                               /* } */

    /*
    ** Hook set by signal function to stop the interpreter.
    */
    private static void lstop(lua_State* L, ref lua_Debug ar)
    {
        lua_sethook(L, null, 0, 0); /* reset hook */
        luaL_error(L, "interrupted!");
    }

    private static bool inSignal;

    /*
     ** Function to be called at a C signal. Because a C signal cannot
     ** just change a Lua state (as there is no proper synchronisation),
     ** this function only sets a hook that, when called, will stop the
     ** interpreter.
     */
    private static void laction(PosixSignalContext obj)
    {
        if (inSignal)
        {
            Environment.Exit(1);
        }

        inSignal = true;
        try
        {
            const int flag = LUA_MASKCALL | LUA_MASKRET | LUA_MASKLINE | LUA_MASKCOUNT;
            lua_sethook(globalL, &lstop, flag, 1);
        }
        finally
        {
            inSignal = false;
        }
    }

    private static void print_usage(string badoption)
    {
//   lua_writestringerror("%s: ", progname);
//   if (badoption[1] == 'e' || badoption[1] == 'l')
//     lua_writestringerror("'%s' needs argument\n", badoption);
//   else
//     lua_writestringerror("unrecognized option '%s'\n", badoption);
//   lua_writestringerror(
//   "usage: %s [options] [script [args]]\n"
//   "Available options are:\n"
//   "  -e stat   execute string 'stat'\n"
//   "  -i        enter interactive mode after executing 'script'\n"
//   "  -l mod    require library 'mod' into global 'mod'\n"
//   "  -l g=mod  require library 'mod' into global 'g'\n"
//   "  -v        show version information\n"
//   "  -E        ignore environment variables\n"
//   "  -W        turn warnings on\n"
//   "  --        stop handling options\n"
//   "  -         stop handling options and execute stdin\n"
//   ,
//   progname);
        throw new NotImplementedException();
    }

// /*
// ** Prints an error message, adding the program name in front of it
// ** (if present)
// */
// static void l_message (const char *pname, const char *msg) {
//   if (pname) lua_writestringerror("%s: ", pname);
//   lua_writestringerror("%s\n", msg);
// }

    /*
    ** Check whether 'status' is not OK and, if so, prints the error
    ** message on the top of the stack.
    */
    private static int report(lua_State* L, int status)
    {
        if (status != LUA_OK)
        {
            // const char *msg = lua_tostring(L, -1);
            // if (msg == null)
            //   msg = "(error message not a string)";
            // l_message(progname, msg);
            // lua_pop(L, 1);  /* remove message */
            throw new NotImplementedException();
        }

        return status;
    }

    /*
    ** Message handler used to run all chunks
    */
    private static int msghandler(lua_State* L)
    {
        string? msg = lua_tostring(L, 1);
        if (msg == null)
        {
            /* is error object not a string? */
//     if (luaL_callmeta(L, 1, "__tostring") &&  /* does it have a metamethod */
//         lua_type(L, -1) == LUA_TSTRING)  /* that produces a string? */
//       return 1;  /* that is the message */
//     else
//       msg = lua_pushfstring(L, "(error object is a %s value)",
//                                luaL_typename(L, 1));
            throw new NotImplementedException();
        }

        luaL_traceback(L, L, msg, 1); /* append a standard traceback */
        return 1; /* return the traceback */
    }

    /*
     ** Interface to 'lua_pcall', which sets appropriate message function
     ** and C-signal handler. Used to run all chunks.
     */
    public static int docall(lua_State* L, int narg, int nres)
    {
        int @base = lua_gettop(L) - narg; /* function index */
        lua_pushcfunction(L, &msghandler); /* push message handler */
        lua_insert(L, @base); /* put it under function and args */
        globalL = L; /* to be available to 'laction' */

        int status;
        
        using (PosixSignalRegistration.Create(PosixSignal.SIGINT, laction))
        {
            status = lua_pcall(L, narg, nres, @base);
        }
        
        lua_remove(L, @base); /* remove message handler from the stack */
        return status;
    }

    private static void print_version()
    {
        Console.WriteLine(LUA_COPYRIGHT);
    }

    /*
    ** Create the 'arg' table, which stores all arguments from the
    ** command line ('argv'). It should be aligned so that, at index 0,
    ** it has 'argv[script]', which is the script name. The arguments
    ** to the script (everything after 'script') go to positive indices;
    ** other arguments (before the script name) go to negative indices.
    ** If there is no script name, assume interpreter's name as base.
    ** (If there is no interpreter's name either, 'script' is -1, so
    ** table sizes are zero.)
    */
    private static void createargtable(lua_State* L, ReadOnlySpan<string> argv, int script)
    {
        int narg = argv.Length - (script + 1); /* number of positive indices */
        lua_createtable(L, narg, script + 1);
        for (int i = 0; i < argv.Length; i++)
        {
            lua_pushstring(L, argv[i]);
            lua_rawseti(L, -2, i - script);
        }

        lua_setglobal(L, "arg");
    }

// static int dochunk (lua_State *L, int status) {
//   if (status == LUA_OK) status = docall(L, 0, 0);
//   return report(L, status);
// }

    private static int dofile(lua_State* L, string? name)
    {
//   return dochunk(L, luaL_loadfile(L, name));
        throw new NotImplementedException();
    }

    private static int dostring(lua_State* L, string s, string name)
    {
        // return dochunk(L, luaL_loadbuffer(L, s, strlen(s), name));
        throw new NotImplementedException();
    }

// /*
// ** Receives 'globname[=modname]' and runs 'globname = require(modname)'.
// ** If there is no explicit modname and globname contains a '-', cut
// ** the suffix after '-' (the "version") to make the global name.
// */
// static int dolibrary (lua_State *L, char *globname) {
//   int status;
//   char *suffix = null;
//   char *modname = strchr(globname, '=');
//   if (modname == null) {  /* no explicit name? */
//     modname = globname;  /* module name is equal to global name */
//     suffix = strchr(modname, *LUA_IGMARK);  /* look for a suffix mark */
//   }
//   else {
//     *modname = '\0';  /* global name ends here */
//     modname++;  /* module name starts after the '=' */
//   }
//   lua_getglobal(L, "require");
//   lua_pushstring(L, modname);
//   status = docall(L, 1, 1);  /* call 'require(modname)' */
//   if (status == LUA_OK) {
//     if (suffix != null)  /* is there a suffix mark? */
//       *suffix = '\0';  /* remove suffix from global name */
//     lua_setglobal(L, globname);  /* globname = require(modname) */
//   }
//   return report(L, status);
// }
//
//
// /*
// ** Push on the stack the contents of table 'arg' from 1 to #arg
// */
// static int pushargs (lua_State *L) {
//   int i, n;
//   if (lua_getglobal(L, "arg") != LUA_TTABLE)
//     luaL_error(L, "'arg' is not a table");
//   n = (int)luaL_len(L, -1);
//   luaL_checkstack(L, n + 3, "too many arguments to script");
//   for (i = 1; i <= n; i++)
//     lua_rawgeti(L, -i, i);
//   lua_remove(L, -i);  /* remove table from the stack */
//   return n;
// }

    private static int handle_script(lua_State* L, ReadOnlySpan<string> argv, string s)
    {
        string? fname = argv[0];
        if (fname == "-" && s != "--")
        {
            fname = null; /* stdin */
        }

        int status = luaL_loadfile(L, fname);
        if (status == LUA_OK)
        {
//     int n = pushargs(L);  /* push arguments to script */
//     status = docall(L, n, LUA_MULTRET);v
            throw new NotImplementedException();
        }

        return report(L, status);
    }


    /* bits of various argument indicators in 'args' */
    private const int has_error = 1;	/* bad option */
    private const int has_i = 2;	/* -i */
    private const int has_v = 4;	/* -v */
    private const int has_e = 8;	/* -e */
    private const int has_E = 16;	/* -E */

    /*
    ** Traverses all arguments from 'argv', returning a mask with those
    ** needed before running any Lua code or an error code if it finds any
    ** invalid argument. In case of error, 'first' is the index of the bad
    ** argument.  Otherwise, 'first' is -1 if there is no program name,
    ** 0 if there is no script name, or the index of the script name.
    */
    private static int collectargs(string[] argv, int* first)
    {
        if (argv.Length > 0)
        {
            /* is there a program name? */
            if (argv[0][0] != '\0') /* not empty? */
            {
                progname = argv[0]; /* save it */
            }
        }
        else
        {
            /* no program name */
            *first = -1;
            return 0;
        }

        int args = 0;

        for (int i = 1; i < argv.Length; i++)
        {
            /* handle arguments */
            *first = i;
            if (argv[i][0] != '-') /* not an option? */
            {
                return args; /* stop handling options */
            }

            switch (argv[i][1])
            {
                /* else check option */
                case '-': /* '--' */
//         if (argv[i][2] != '\0')  /* extra characters after '--'? */
//           return has_error;  /* invalid option */
//         /* if there is a script name, it comes after '--' */
//         *first = (argv[i + 1] != null) ? i + 1 : 0;
//         return args;
                    throw new NotImplementedException();

                case '\0': /* '-' */
                    return args; /* script "name" is '-' */

                case 'E':
//         if (argv[i][2] != '\0')  /* extra characters? */
//           return has_error;  /* invalid option */
//         args |= has_E;
//         break;
                    throw new NotImplementedException();

                case 'W':
//         if (argv[i][2] != '\0')  /* extra characters? */
//           return has_error;  /* invalid option */
//         break;
                    throw new NotImplementedException();

                case 'i':
//         args |= has_i;  /* (-i implies -v) *//* FALLTHROUGH */
                    throw new NotImplementedException();

                case 'v':
//         if (argv[i][2] != '\0')  /* extra characters? */
//           return has_error;  /* invalid option */
//         args |= has_v;
//         break;
                    throw new NotImplementedException();

                case 'e':
//         args |= has_e;  /* FALLTHROUGH */
                    throw new NotImplementedException();

                case 'l': /* both options need an argument */
//         if (argv[i][2] == '\0') {  /* no concatenated argument? */
//           i++;  /* try next 'argv' */
//           if (argv[i] == null || argv[i][0] == '-')
//             return has_error;  /* no next argument or it is another option */
//         }
//         break;
                    throw new NotImplementedException();

                default: /* invalid option */
                    return has_error;
            }
        }

        *first = 0; /* no script name */
        return args;
    }

    /*
    ** Processes options 'e' and 'l', which involve running Lua code, and
    ** 'W', which also affects the state.
    ** Returns 0 if some code raises an error.
    */
    private static bool runargs(lua_State* L, ReadOnlySpan<string> argv, int n)
    {
        lua_warning(L, "@off", false); /* by default, Lua stand-alone has warnings off */
        for (int i = 1; i < n; i++)
        {
            int option = argv[i][1];
            Debug.Assert(argv[i][0] == '-');  /* already checked */
//     switch (option) {
//       case 'e':  case 'l': {
//         int status;
//         char *extra = argv[i] + 2;  /* both options need an argument */
//         if (*extra == '\0') extra = argv[++i];
//         Debug.Assert(extra != null);
//         status = (option == 'e')
//                  ? dostring(L, extra, "=(command line)")
//                  : dolibrary(L, extra);
//         if (status != LUA_OK) return 0;
//         break;
//       }
//       case 'W':
//         lua_warning(L, "@on", 0);  /* warnings on */
//         break;
//     }
            throw new NotImplementedException();
        }

        return true;
    }

    private static int handle_luainit(lua_State* L)
    {
        string name = "=" + LUA_INITVARVERSION;
        string? init = Environment.GetEnvironmentVariable(name[1..]);
        if (init == null)
        {
            name = "=" + LUA_INIT_VAR;
            init = Environment.GetEnvironmentVariable(name[1..]); /* try alternative name */
        }

        if (init == null)
        {
            return LUA_OK;
        }

        if (init[0] == '@')
        {
            return dofile(L, init + 1);
        }

        return dostring(L, init, name);
    }

// /*
// ** {==================================================================
// ** Read-Eval-Print Loop (REPL)
// ** ===================================================================
// */
//
// #if !defined(LUA_PROMPT)
// #define LUA_PROMPT		"> "
// #define LUA_PROMPT2		">> "
// #endif
//
// #if !defined(LUA_MAXINPUT)
// #define LUA_MAXINPUT		512
// #endif
//
//
// /*
// ** lua_stdin_is_tty detects whether the standard input is a 'tty' (that
// ** is, whether we're running lua interactively).
// */
// #if !defined(lua_stdin_is_tty)	/* { */
//
// #if defined(LUA_USE_POSIX)	/* { */
//
// #include <unistd.h>
// #define lua_stdin_is_tty()	isatty(0)
//
// #elif defined(LUA_USE_WINDOWS)	/* }{ */
//
// #include <io.h>
// #include <windows.h>
//
// #define lua_stdin_is_tty()	_isatty(_fileno(stdin))
//
// #else				/* }{ */
//
// /* ISO C definition */
// #define lua_stdin_is_tty()	1  /* assume stdin is a tty */
//
// #endif				/* } */
//
// #endif				/* } */
//
//
// /*
// ** * lua_initreadline initializes the readline system.
// ** * lua_readline defines how to show a prompt and then read a line from
// **   the standard input.
// ** * lua_saveline defines how to "save" a read line in a "history".
// ** * lua_freeline defines how to free a line read by lua_readline.
// */
//
// #if !defined(lua_readline)	/* { */
// /* Otherwise, all previously listed functions should be defined. */
//
// #if defined(LUA_USE_READLINE)	/* { */
// /* Lua will be linked with '-lreadline' */
//
// #include <readline/readline.h>
// #include <readline/history.h>
//
// #define lua_initreadline(L)	((void)L, rl_readline_name="lua")
// #define lua_readline(buff,prompt)	((void)buff, readline(prompt))
// #define lua_saveline(line)	add_history(line)
// #define lua_freeline(line)	free(line)
//
// #else		/* }{ */
// /* use dynamically loaded readline (or nothing) */
//
// /* pointer to 'readline' function (if any) */
// typedef char *(*l_readlineT) (const char *prompt);
// static l_readlineT l_readline = null;
//
// /* pointer to 'add_history' function (if any) */
// typedef void (*l_addhistT) (const char *string);
// static l_addhistT l_addhist = null;
//
//
// static char *lua_readline (char *buff, const char *prompt) {
//   if (l_readline != null)  /* is there a 'readline'? */
//     return (*l_readline)(prompt);  /* use it */
//   else {  /* emulate 'readline' over 'buff' */
//     fputs(prompt, stdout);
//     fflush(stdout);  /* show prompt */
//     return fgets(buff, LUA_MAXINPUT, stdin);  /* read line */
//   }
// }
//
//
// static void lua_saveline (const char *line) {
//   if (l_addhist != null)  /* is there an 'add_history'? */
//     (*l_addhist)(line);  /* use it */
//   /* else nothing to be done */
// }
//
//
// static void lua_freeline (char *line) {
//   if (l_readline != null)  /* is there a 'readline'? */
//     free(line);  /* free line created by it */
//   /* else 'lua_readline' used an automatic buffer; nothing to free */
// }
//
//
// #if defined(LUA_USE_DLOPEN) && defined(LUA_READLINELIB)		/* { */
// /* try to load 'readline' dynamically */
//
// #include <dlfcn.h>
//
// static void lua_initreadline (lua_State *L) {
//   void *lib = dlopen(LUA_READLINELIB, RTLD_NOW | RTLD_LOCAL);
//   if (lib == null)
//     lua_warning(L, "library '" LUA_READLINELIB "' not found", 0);
//   else {
//     const char **name = cast(const char**, dlsym(lib, "rl_readline_name"));
//     if (name != null)
//       *name = "lua";
//     l_readline = cast(l_readlineT, cast_func(dlsym(lib, "readline")));
//     l_addhist = cast(l_addhistT, cast_func(dlsym(lib, "add_history")));
//     if (l_readline == null)
//       lua_warning(L, "unable to load 'readline'", 0);
//   }
// }
//
// #else		/* }{ */
// /* no dlopen or LUA_READLINELIB undefined */
//
// /* Leave pointers with null */
// #define lua_initreadline(L)	((void)L)
//
// #endif		/* } */
//
// #endif				/* } */
//
// #endif				/* } */
//
//
// /*
// ** Return the string to be used as a prompt by the interpreter. Leave
// ** the string (or nil, if using the default value) on the stack, to keep
// ** it anchored.
// */
// static const char *get_prompt (lua_State *L, int firstline) {
//   if (lua_getglobal(L, firstline ? "_PROMPT" : "_PROMPT2") == LUA_TNIL)
//     return (firstline ? LUA_PROMPT : LUA_PROMPT2);  /* use the default */
//   else {  /* apply 'tostring' over the value */
//     const char *p = luaL_tolstring(L, -1, null);
//     lua_remove(L, -2);  /* remove original value */
//     return p;
//   }
// }
//
// /* mark in error messages for incomplete statements */
// #define EOFMARK		"<eof>"
// #define marklen		(sizeof(EOFMARK)/sizeof(char) - 1)
//
//
// /*
// ** Check whether 'status' signals a syntax error and the error
// ** message at the top of the stack ends with the above mark for
// ** incomplete statements.
// */
// static int incomplete (lua_State *L, int status) {
//   if (status == LUA_ERRSYNTAX) {
//     size_t lmsg;
//     const char *msg = lua_tolstring(L, -1, &lmsg);
//     if (lmsg >= marklen && strcmp(msg + lmsg - marklen, EOFMARK) == 0)
//       return 1;
//   }
//   return 0;  /* else... */
// }
//
//
// /*
// ** Prompt the user, read a line, and push it into the Lua stack.
// */
// static int pushline (lua_State *L, int firstline) {
//   char buffer[LUA_MAXINPUT];
//   size_t l;
//   const char *prmt = get_prompt(L, firstline);
//   char *b = lua_readline(buffer, prmt);
//   lua_pop(L, 1);  /* remove prompt */
//   if (b == null)
//     return 0;  /* no input */
//   l = strlen(b);
//   if (l > 0 && b[l-1] == '\n')  /* line ends with newline? */
//     b[--l] = '\0';  /* remove it */
//   lua_pushlstring(L, b, l);
//   lua_freeline(b);
//   return 1;
// }
//
//
// /*
// ** Try to compile line on the stack as 'return <line>;'; on return, stack
// ** has either compiled chunk or original line (if compilation failed).
// */
// static int addreturn (lua_State *L) {
//   const char *line = lua_tostring(L, -1);  /* original line */
//   const char *retline = lua_pushfstring(L, "return %s;", line);
//   int status = luaL_loadbuffer(L, retline, strlen(retline), "=stdin");
//   if (status == LUA_OK)
//     lua_remove(L, -2);  /* remove modified line */
//   else
//     lua_pop(L, 2);  /* pop result from 'luaL_loadbuffer' and modified line */
//   return status;
// }
//
//
// static void checklocal (const char *line) {
//   static const size_t szloc = sizeof("local") - 1;
//   static const char space[] = " \t";
//   line += strspn(line, space);  /* skip spaces */
//   if (strncmp(line, "local", szloc) == 0 &&  /* "local"? */
//       strchr(space, *(line + szloc)) != null) {  /* followed by a space? */
//     lua_writestringerror("%s\n",
//       "warning: locals do not survive across lines in interactive mode");
//   }
// }
//
//
// /*
// ** Read multiple lines until a complete Lua statement or an error not
// ** for an incomplete statement. Start with first line already read in
// ** the stack.
// */
// static int multiline (lua_State *L) {
//   size_t len;
//   const char *line = lua_tolstring(L, 1, &len);  /* get first line */
//   checklocal(line);
//   for (;;) {  /* repeat until gets a complete statement */
//     int status = luaL_loadbuffer(L, line, len, "=stdin");  /* try it */
//     if (!incomplete(L, status) || !pushline(L, 0))
//       return status;  /* should not or cannot try to add continuation line */
//     lua_remove(L, -2);  /* remove error message (from incomplete line) */
//     lua_pushliteral(L, "\n");  /* add newline... */
//     lua_insert(L, -2);  /* ...between the two lines */
//     lua_concat(L, 3);  /* join them */
//     line = lua_tolstring(L, 1, &len);  /* get what is has */
//   }
// }
//
//
// /*
// ** Read a line and try to load (compile) it first as an expression (by
// ** adding "return " in front of it) and second as a statement. Return
// ** the final status of load/call with the resulting function (if any)
// ** in the top of the stack.
// */
// static int loadline (lua_State *L) {
//   const char *line;
//   int status;
//   lua_settop(L, 0);
//   if (!pushline(L, 1))
//     return -1;  /* no input */
//   if ((status = addreturn(L)) != LUA_OK)  /* 'return ...' did not work? */
//     status = multiline(L);  /* try as command, maybe with continuation lines */
//   line = lua_tostring(L, 1);
//   if (line[0] != '\0')  /* non empty? */
//     lua_saveline(line);  /* keep history */
//   lua_remove(L, 1);  /* remove line from the stack */
//   Debug.Assert(lua_gettop(L) == 1);
//   return status;
// }
//
//
// /*
// ** Prints (calling the Lua 'print' function) any values on the stack
// */
// static void l_print (lua_State *L) {
//   int n = lua_gettop(L);
//   if (n > 0) {  /* any result to be printed? */
//     luaL_checkstack(L, LUA_MINSTACK, "too many results to print");
//     lua_getglobal(L, "print");
//     lua_insert(L, 1);
//     if (lua_pcall(L, n, 0, 0) != LUA_OK)
//       l_message(progname, lua_pushfstring(L, "error calling 'print' (%s)",
//                                              lua_tostring(L, -1)));
//   }
// }

    /*
    ** Do the REPL: repeatedly read (load) a line, evaluate (call) it, and
    ** print any results.
    */
    private static void doREPL(lua_State* L)
    {
//   int status;
//   const char *oldprogname = progname;
//   progname = null;  /* no 'progname' on errors in interactive mode */
//   lua_initreadline(L);
//   while ((status = loadline(L)) != -1) {
//     if (status == LUA_OK)
//       status = docall(L, 0, LUA_MULTRET);
//     if (status == LUA_OK) l_print(L);
//     else report(L, status);
//   }
//   lua_settop(L, 0);  /* clear stack */
//   lua_writeline();
//   progname = oldprogname;
        throw new NotImplementedException();
    }

    /* }================================================================== */

    /*
    ** Main body of stand-alone interpreter (to be called in protected mode).
    ** Reads the options and handles them all.
    */
    private static int pmain(lua_State* L)
    {
        int argc = (int)lua_tointeger(L, 1);
        string[] argv = (string[])(GCHandle.FromIntPtr((nint)lua_touserdata(L, 2)).Target ??
                                   throw new InvalidOperationException());
        int script;
        int args = collectargs(argv, &script);
        int optlim = (script > 0) ? script : argc; /* first argv not an option */
        luaL_checkversion(L, LUA_VERSION_NUM, LUAL_NUMSIZES); /* check that interpreter has correct version */
        if (args == has_error)
        {
            /* bad arg? */
            print_usage(argv[script]); /* 'script' has index of bad arg. */
            return 0;
        }

        if ((args & has_v) != 0) /* option '-v'? */
        {
            print_version();
        }

        if ((args & has_E) != 0)
        {
            /* option '-E'? */
            lua_pushboolean(L, true); /* signal for libraries to ignore env. vars. */
            lua_setfield(L, LUA_REGISTRYINDEX, "LUA_NOENV");
        }

        MethodInfo? openlibs = typeof(Lua).GetMethod(
            "luai_openlibs",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (openlibs == null)
        {
            luaL_openselectedlibs(L, ~0, 0); /* open standard libraries */
        }
        else
        {
            openlibs.Invoke(null, [(nint)L]); /* open standard libraries */
        }

        createargtable(L, argv.AsSpan(), script); /* create table 'arg' */
        lua_gc(L, LUA_GCRESTART); /* start GC... */
        lua_gc(L, LUA_GCGEN); /* ...in generational mode */
        if ((args & has_E) == 0)
        {
            /* no option '-E'? */
            if (handle_luainit(L) != LUA_OK) /* run LUA_INIT */
            {
                return 0; /* error running LUA_INIT */
            }
        }

        if (!runargs(L, argv, optlim)) /* execute arguments -e, -l, and -W */
        {
            return 0; /* something failed */
        }

        if (script > 0)
        {
            /* execute main script (if there is one) */
            if (handle_script(L, argv.AsSpan(script), argv[script - 1]) != LUA_OK)
            {
                return 0; /* interrupt in case of error */
            }
        }

        if ((args & has_i) != 0) /* -i option? */
        {
            doREPL(L); /* do read-eval-print loop */
        }
        else if (script < 1 && (args & (has_e | has_v)) == 0)
        {
            /* no active option? */
            if (!Console.IsInputRedirected)
            {
                //     /* running in interactive mode? */
                //     print_version();
                //     doREPL(L); /* do read-eval-print loop */
                throw new NotImplementedException();
            }
            else
            {
                dofile(L, null); /* executes stdin as a file */
            }
        }

        lua_pushboolean(L, true); /* signal no errors */
        return 1;
    }

    public static int Main(string[] args)
    {
        lua_State* L = luaL_newstate(); /* create state */
        if (L == null)
        {
            Console.WriteLine("cannot create state: not enough memory");
            return -1;
        }

        string[] xargs = [Environment.GetCommandLineArgs()[0], ..args];

        GCHandle argPtr = GCHandle.Alloc(xargs);

        lua_gc(L, LUA_GCSTOP); /* stop GC while building state */
        lua_pushcfunction(L, &pmain); /* to call 'pmain' in protected mode */
        lua_pushinteger(L, xargs.Length); /* 1st argument */
        lua_pushlightuserdata(L, (void*)GCHandle.ToIntPtr(argPtr)); /* 2nd argument */
        int status = lua_pcall(L, 2, 1, 0) /* do the call */;
        bool result = lua_toboolean(L, -1) /* get result */;
        report(L, status);
        lua_close(L);
        
        argPtr.Free();
        
        return result && status == LUA_OK ? 0 : -1;
    }
}
