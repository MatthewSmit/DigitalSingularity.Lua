namespace DigitalSingularity.Lua;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe partial class Lua
{
    // $Id: loadlib.c $
    // Dynamic library loader for Lua
    // See Copyright Notice in lua.h

    // prefix for open functions in C libraries
    private const string LUA_POF = "luaopen_";

    // separator for open functions in C libraries
    private const string LUA_OFSEP = "_";

    /// <summary>
    /// key for table in the registry that keeps handles
    /// for all loaded C libraries
    /// </summary>
    private const string CLIBS = "_CLIBS";

    private const string LIB_FAIL = "open";
    
// Replace in the path (on the top of the stack) any occurrence
// of LUA_EXEC_DIR with the executable's path.
//
// static void setprogdir (lua_State *L) {
// char buff[MAX_PATH + 1];
// char *lb;
// DWORD nsize = sizeof(buff)/sizeof(char);
// DWORD n = GetModuleFileNameA(null, buff, nsize); // get exec. name
// if (n == 0 || n == nsize || (lb = strrchr(buff, '\\')) == null)
// luaL_error(L, "unable to get ModuleFileName");
// else {
// *lb = '\0'; // cut name on the last '\\' to get the path
// luaL_gsub(L, lua_tostring(L, -1), LUA_EXEC_DIR, buff);
// lua_remove(L, -2); // remove original string
// }
// }

    // {==================================================================
    // Set Paths
    // ===================================================================

    /// <summary>
    /// LUA_PATH_VAR and LUA_CPATH_VAR are the names of the environment
    /// variables that Lua check to set its paths.
    /// </summary>
    private const string LUA_PATH_VAR = "LUA_PATH";
    private const string LUA_CPATH_VAR = "LUA_CPATH";

    /// <summary>
    /// return registry.LUA_NOENV as a boolean
    /// </summary>
    private static bool noenv(lua_State* L)
    {
        lua_getfield(L, LUA_REGISTRYINDEX, "LUA_NOENV");
        bool b = lua_toboolean(L, -1);
        lua_pop(L, 1); // remove value
        return b;
    }

    /// <summary>
    /// Set a path. (If using the default path, assume it is a string
    /// literal in C and create it as an external string.)
    /// </summary>
    private static void setpath(
        lua_State* L,
        string fieldname,
        string envname,
        string dft)
    {
        ReadOnlySpan<char> dftmark;
        string nver = lua_pushfstring(L, "%s%s", envname, LUA_VERSUFFIX);
        string? path = Environment.GetEnvironmentVariable(nver); // try versioned name
        if (string.IsNullOrEmpty(path)) // no versioned environment variable?
        {
            path = Environment.GetEnvironmentVariable(envname); // try unversioned name
        }

        if (path == null || noenv(L)) // no environment variable?
        {
            lua_pushstring(L, dft); // use default
        }
        else if (!(dftmark = strstr(path, LUA_PATH_SEP + LUA_PATH_SEP)).IsEmpty)
        {
            // path contains a ";;": insert default path in its place
// size_t len = strlen(path);
// luaL_Buffer b;
// luaL_buffinit(L, &b);
// if (path < dftmark) { // is there a prefix before ';;'?
// luaL_addlstring(&b, path, ct_diff2sz(dftmark - path)); // add it
// luaL_addchar(&b, *LUA_PATH_SEP);
// }
// luaL_addstring(&b, dft); // add default
// if (dftmark < path + len - 2) { // is there a suffix after ';;'?
// luaL_addchar(&b, *LUA_PATH_SEP);
// luaL_addlstring(&b, dftmark + 2, ct_diff2sz((path + len - 2) - dftmark));
// }
// luaL_pushresult(&b);
            throw new NotImplementedException();
        }
        else
        {
            lua_pushstring(L, path); // nothing to change
        }

        // setprogdir(L); TODO?
        lua_setfield(L, -3, fieldname); // package[fieldname] = path value
        lua_pop(L, 1); // pop versioned variable name ('nver')
    }

    // External strings created by DLLs may need the DLL code to be
    // deallocated. This implies that a DLL can only be unloaded after all
    // its strings were deallocated. To ensure that, we create a 'library
    // string' to represent each DLL, and when this string is deallocated
    // it closes its corresponding DLL.
    // (The string itself is irrelevant; its userdata is the DLL pointer.)

    /// <summary>
    /// return registry.CLIBS[path]
    /// </summary>
    private static void* checkclib(lua_State* L, string path)
    {
        lua_getfield(L, LUA_REGISTRYINDEX, CLIBS);
        lua_getfield(L, -1, path);
        void* plib = lua_touserdata(L, -1); // plib = CLIBS[path]
        lua_pop(L, 2); // pop CLIBS table and 'plib'
        return plib;
    }

    /// <summary>
    /// Deallocate function for library strings.
    /// Unload the DLL associated with the string being deallocated.
    /// </summary>
    private static void* freelib(void* ud, void* ptr, long osize, long nsize)
    { 
        // string itself is irrelevant and static
        NativeLibrary.Free((nint)ud); // unload library represented by the string
        return null;
    }

    /// <summary>
    /// Create a library string that, when deallocated, will unload 'plib'
    /// </summary>
    private static void createlibstr(lua_State* L, void* plib)
    {
        // common content for all library strings
        lua_pushexternalstring(L, "01234567890"u8.ToPointer(), 11, AllocFunction.FromFunction(&freelib), plib);
    }

    /// <summary>
    /// registry.CLIBS[path] = plib          -- for queries.
    /// Also create a reference to strlib, so that the library string will
    /// only be collected when registry.CLIBS is collected.
    /// </summary>
    private static void addtoclib(lua_State* L, string path, void* plib)
    {
        lua_getfield(L, LUA_REGISTRYINDEX, CLIBS);
        lua_pushlightuserdata(L, plib);
        lua_setfield(L, -2, path); // CLIBS[path] = plib
        createlibstr(L, plib);
        luaL_ref(L, -2); // keep library string in CLIBS
        lua_pop(L, 1); // pop CLIBS table
    }

    // error codes for 'lookforfunc'
    private const int ERRLIB = 1;
    private const int ERRFUNC = 2;

    private static void* nativeDll;

    /// <summary>
    /// Look for a C function named 'sym' in a dynamically loaded library
    /// 'path'.
    /// First, check whether the library is already loaded; if not, try
    /// to load it.
    /// Then, if 'sym' is '*', return true (as library has been loaded).
    /// Otherwise, look for symbol 'sym' in the library and push a
    /// C function with that symbol.
    /// Return 0 with 'true' or a function in the stack; in case of
    /// errors, return an error code with an error message in the stack.
    /// </summary>
    private static int lookforfunc(lua_State* L, string path, string sym)
    {
        void* reg = checkclib(L, path); // check loaded C libraries
        if (reg == null)
        {
            if (nativeDll == null)
            {
                try
                {
                    nativeDll = (void*)NativeLibrary.Load(
                        "lua55",
                        Assembly.GetCallingAssembly(),
                        DllImportSearchPath.ApplicationDirectory |
                        DllImportSearchPath.AssemblyDirectory);
                }
                catch (Exception e)
                {
                    lua_pushstring(L, e.Message);
                    return ERRLIB;
                }
            }

            string s = Environment.CurrentDirectory;
            
            // must load library?
            try
            {
                reg = (void*)NativeLibrary.Load(
                    path,
                    Assembly.GetCallingAssembly(),
                    DllImportSearchPath.ApplicationDirectory |
                    DllImportSearchPath.AssemblyDirectory |
                    DllImportSearchPath.UserDirectories);
            }
            catch (Exception e)
            {
                lua_pushstring(L, e.Message);
                return ERRLIB;
            }

            addtoclib(L, path, reg);
        }

        if (sym.StartsWith('*'))
        {
            // loading only library (no function)?
            lua_pushboolean(L, true); // return 'true'
            return 0; // no errors
        }

        CFunction f;
        try
        {
            nint tmp = NativeLibrary.GetExport((nint)reg, sym);
            f = CFunction.FromUnmanaged((delegate* unmanaged[Cdecl]<lua_State*, int>)tmp);
        }
        catch (Exception e)
        {
            lua_pushstring(L, e.Message);
            return ERRFUNC;
        }

        lua_pushcfunction(L, f); // else create new function
        return 0; // no errors
    }

    private static int ll_loadlib(lua_State* L)
    {
        string path = luaL_checknetstring(L, 1);
        string init = luaL_checknetstring(L, 2);
        int stat = lookforfunc(L, path, init);
        if (stat == 0) // no errors?
        {
            return 1; // return the loaded function
        }

        // error; error message is on stack top
        luaL_pushfail(L);
        lua_insert(L, -2);
        lua_pushstring(L, stat == ERRLIB ? LIB_FAIL : "init");
        return 3; // return fail, error message, and where
    }

    // {======================================================
    // 'require' function
    // =======================================================

    private static bool readable(string filename)
    {
        try
        {
            // try to open file
            using (File.Open(filename, FileMode.Open))
            {
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Get the next name in '*path' = 'name1;name2;name3;...', changing
    /// the ending ';' to '\0' to create a zero-terminated string. Return
    /// null when list ends.
    /// </summary>
    private static byte* getnextfilename(byte** path, byte* end)
    {
        byte* name = *path;
        if (name == end)
        {
            return null; // no more names
        }

        if (*name == '\0')
        {
            // from previous iteration?
            *name = (byte)LUA_PATH_SEP[0]; // restore separator
            name++; // skip it
        }

        byte* sep = strchr(name, LUA_PATH_SEP[0]); // find next separator
        if (sep == null) // separator not found?
        {
            sep = end; // name goes until the end
        }

        *sep = 0; // finish file name
        *path = sep; // will start next search from here
        return name;
    }

    /// <summary>
    /// Given a path such as ";blabla.so;blublu.so", pushes the string
    ///
    /// no file 'blabla.so'
    /// no file 'blublu.so'
    /// </summary>
    private static void pusherrornotfound(lua_State* L, string path)
    {
        luaL_Buffer b;
        luaL_buffinit(L, &b);
        luaL_addstring(&b, "no file '");
        luaL_addgsub(&b, path, LUA_PATH_SEP, "'\n\tno file '");
        luaL_addstring(&b, "'");
        luaL_pushresult(&b);
    }

    private static string? searchpath(
        lua_State* L,
        string name,
        string path,
        string sep,
        string dirsep)
    {
        // separator is non-empty and appears in 'name'?
        if (sep.Length > 0 && name.Contains(sep[0]))
        {
            name = luaL_gsub(L, name, sep, dirsep); // replace it by 'dirsep'
        }

        luaL_Buffer buff;
        luaL_buffinit(L, &buff);
        // add path to the buffer, replacing marks ('?') with the file name
        luaL_addgsub(&buff, path, LUA_PATH_MARK, name);
        luaL_addchar(&buff, '\0');
        byte* pathname = luaL_buffaddr(&buff); // writable list of file names
        byte* endpathname = pathname + luaL_bufflen(&buff) - 1;
        byte* filename;
        while ((filename = getnextfilename(&pathname, endpathname)) != null)
        {
            string s = new((sbyte*)filename); // TODO: utf8
            if (readable(s)) // does file exist and is readable?
            {
                lua_pushstring(L, s); // save and return name
                return s;
            }
        }

        luaL_pushresult(&buff); // push path to create error message
        pusherrornotfound(L, lua_tonetstring(L, -1).TrimEnd('\0')); // create error message
        return null; // not found
    }

    private static int ll_searchpath(lua_State* L)
    {
        string? f = searchpath(
            L,
            luaL_checknetstring(L, 1),
            luaL_checknetstring(L, 2),
            luaL_optnetstring(L, 3, "."),
            luaL_optnetstring(L, 4, LUA_DIRSEP));
        if (f != null)
        {
            return 1;
        }

        // error message is on top of the stack
        luaL_pushfail(L);
        lua_insert(L, -2);
        return 2; // return fail + error message
    }


    private static string? findfile(
        lua_State* L,
        string name,
        string pname,
        string dirsep)
    {
        lua_getfield(L, lua_upvalueindex(1), pname);
        string? path = lua_tonetstring(L, -1);
        if (path == null)
        {
            luaL_error(L, "'package.%s' must be a string", pname);
        }

        return searchpath(L, name, path, ".", dirsep);
    }

    private static int checkload(lua_State* L, bool stat, string filename)
    {
        if (stat)
        {
            // module loaded successfully?
            lua_pushstring(L, filename); // will be 2nd argument to module
            return 2; // return open function and file name
        }

        return luaL_error(
            L,
            "error loading module '%s' from file '%s':\n\t%s",
            lua_tonetstring(L, 1) ?? "",
            filename,
            lua_tonetstring(L, -1) ?? "");
    }

    private static int searcher_Lua(lua_State* L)
    {
        string name = luaL_checknetstring(L, 1);
        string? filename = findfile(L, name, "path", LUA_LSUBSEP);
        if (filename == null)
        {
            return 1; // module not found in this path
        }

        return checkload(L, luaL_loadfile(L, filename) == LUA_OK, filename);
    }

    /// <summary>
    /// Try to find a load function for module 'modname' at file 'filename'.
    /// First, change '.' to '_' in 'modname'; then, if 'modname' has
    /// the form X-Y (that is, it has an "ignore mark"), build a function
    /// name "luaopen_X" and look for it. (For compatibility, if that
    /// fails, it also tries "luaopen_Y".) If there is no ignore mark,
    /// look for a function named "luaopen_modname".
    /// </summary>
    private static int loadfunc(lua_State* L, string filename, string modname)
    {
        modname = luaL_gsub(L, modname, ".", LUA_OFSEP);
        int mark = modname.IndexOf(LUA_IGMARK, StringComparison.InvariantCulture);
        string openfunc;
        if (mark >= 0)
        {
            openfunc = modname[..mark];
            lua_pushlstring(L, openfunc);
            openfunc = lua_pushfstring(L, LUA_POF + "%s", openfunc);
            int stat = lookforfunc(L, filename, openfunc);
            if (stat != ERRFUNC)
            {
                return stat;
            }

            modname = modname[(mark + 1)..]; // else go ahead and try old-style name
        }

        openfunc = lua_pushfstring(L, LUA_POF + "%s", modname);
        return lookforfunc(L, filename, openfunc);
    }

    private static int searcher_C(lua_State* L)
    {
        string name = luaL_checknetstring(L, 1);
        string? filename = findfile(L, name, "cpath", LUA_CSUBSEP);
        if (filename == null)
        {
            return 1; // module not found in this path
        }

        return checkload(L, loadfunc(L, filename, name) == 0, filename);
    }

    private static int searcher_Croot(lua_State* L)
    {
        ReadOnlySpan<byte> name = luaL_checkstring(L, 1);
        int p = name.IndexOf((byte)'.');
        if (p < 0)
        {
            return 0; // is root
        }

        lua_pushlstring(L, name[..p]);
        string? filename = findfile(L, lua_tonetstring(L, -1), "cpath", LUA_CSUBSEP);
        if (filename == null)
        {
            return 1; // root not found
        }

        string names = Encoding.UTF8.GetString(name);
        int stat = loadfunc(L, filename, names);
        if (stat != 0)
        {
            if (stat != ERRFUNC)
            {
                return checkload(L, false, filename); // real error
            }

            // open function not found
            lua_pushfstring(L, "no module '%s' in file '%s'", names, filename);
            return 1;
        }

        lua_pushstring(L, filename); // will be 2nd argument to module
        return 2;
    }

    private static int searcher_preload(lua_State* L)
    {
        string name = luaL_checknetstring(L, 1);
        lua_getfield(L, LUA_REGISTRYINDEX, LUA_PRELOAD_TABLE);
        if (lua_getfield(L, -1, name) == LUA_TNIL)
        {
            // not found?
            lua_pushfstring(L, "no field package.preload['%s']", name);
            return 1;
        }

        lua_pushliteral(L, ":preload:");
        return 2;
    }

    private static void findloader(lua_State* L, string name)
    {
        // push 'package.searchers' to index 3 in the stack
        if (lua_getfield(L, lua_upvalueindex(1), "searchers") != LUA_TTABLE)
        {
            luaL_error(L, "'package.searchers' must be a table");
        }

        luaL_Buffer msg; // to build error message
        luaL_buffinit(L, &msg);
        luaL_addstring(&msg, "\n\t"); // error-message prefix for first message
        // iterate over available searchers to find a loader
        for (int i = 1;; i++)
        {
            if (lua_rawgeti(L, 3, i) == LUA_TNIL)
            {
                // no more searchers?
                lua_pop(L, 1); // remove nil
                luaL_buffsub(&msg, 2); // remove last prefix
                luaL_pushresult(&msg); // create error message
                luaL_error(L, "module '%s' not found:%s", name, lua_tonetstring(L, -1) ?? "");
            }

            lua_pushstring(L, name);
            lua_call(L, 1, 2); // call it
            if (lua_isfunction(L, -2)) // did it find a loader?
            {
                return; // module loader found
            }

            if (lua_isstring(L, -2))
            {
                // searcher returned error message?
                lua_pop(L, 1); // remove extra return
                luaL_addvalue(&msg); // concatenate error message
                luaL_addstring(&msg, "\n\t"); // prefix for next message
            }
            else
            {
                // no error message
                lua_pop(L, 2); // remove both returns
            }
        }
    }

    private static int ll_require(lua_State* L)
    {
        string name = luaL_checknetstring(L, 1);
        lua_settop(L, 1); // LOADED table will be at index 2
        lua_getfield(L, LUA_REGISTRYINDEX, LUA_LOADED_TABLE);
        lua_getfield(L, 2, name); // LOADED[name]
        if (lua_toboolean(L, -1)) // is it there?
        {
            return 1; // package is already loaded
        }

        // else must load package
        lua_pop(L, 1); // remove 'getfield' result
        findloader(L, name);
        lua_rotate(L, -2, 1); // function <-> loader data
        lua_pushvalue(L, 1); // name is 1st argument to module loader
        lua_pushvalue(L, -3); // loader data is 2nd argument
        // stack: ...; loader data; loader function; mod. name; loader data
        lua_call(L, 2, 1); // run loader to load module
        // stack: ...; loader data; result from loader
        if (!lua_isnil(L, -1)) // non-nil return?
        {
            lua_setfield(L, 2, name); // LOADED[name] = returned value
        }
        else
        {
            lua_pop(L, 1); // pop nil
        }

        if (lua_getfield(L, 2, name) == LUA_TNIL)
        {
            // module set no value?
            lua_pushboolean(L, true); // use true as result
            lua_copy(L, -1, -2); // replace loader result
            lua_setfield(L, 2, name); // LOADED[name] = true
        }

        lua_rotate(L, -2, 1); // loader data <-> module result
        return 2; // return module result and loader data
    }

    private static readonly luaL_Reg[] pk_funcs =
    [
        new ("loadlib", CFunction.FromFunction(&ll_loadlib)),
        new ("searchpath", CFunction.FromFunction(&ll_searchpath)),
        // placeholders
        new ("preload", default),
        new ("cpath", default),
        new ("path", default),
        new ("searchers", default),
        new ("loaded", default),
    ];

    private static readonly luaL_Reg[] ll_funcs =
    [
        new("require", CFunction.FromFunction(&ll_require)),
    ];

    private static readonly CFunction[] searchers =
    [
        CFunction.FromFunction(&searcher_preload),
        CFunction.FromFunction(&searcher_Lua),
        CFunction.FromFunction(&searcher_C),
        CFunction.FromFunction(&searcher_Croot),
    ];

    private static void createsearcherstable(lua_State* L)
    {
        // create 'searchers' table
        lua_createtable(L, searchers.Length, 0);
        // fill it with predefined searchers
        for (int i = 0; i < searchers.Length; i++)
        {
            lua_pushvalue(L, -2); // set 'package' as upvalue for all searchers
            lua_pushcclosure(L, searchers[i], 1);
            lua_rawseti(L, -2, i + 1);
        }

        lua_setfield(L, -2, "searchers"); // put it in field 'searchers'
    }

    public static int luaopen_package(lua_State* L)
    {
        luaL_getsubtable(L, LUA_REGISTRYINDEX, CLIBS); // create CLIBS table
        lua_pop(L, 1); // will not use it now
        luaL_newlib(L, pk_funcs); // create 'package' table
        createsearcherstable(L);
        // set paths
        setpath(L, "path", LUA_PATH_VAR, LUA_PATH_DEFAULT);
        setpath(L, "cpath", LUA_CPATH_VAR, LUA_CPATH_DEFAULT);
        // store config information
        lua_pushliteral(
            L,
            LUA_DIRSEP + "\n" + LUA_PATH_SEP + "\n" + LUA_PATH_MARK + "\n" + LUA_EXEC_DIR + "\n" + LUA_IGMARK + "\n");
        lua_setfield(L, -2, "config");
        // set field 'loaded'
        luaL_getsubtable(L, LUA_REGISTRYINDEX, LUA_LOADED_TABLE);
        lua_setfield(L, -2, "loaded");
        // set field 'preload'
        luaL_getsubtable(L, LUA_REGISTRYINDEX, LUA_PRELOAD_TABLE);
        lua_setfield(L, -2, "preload");
        lua_pushglobaltable(L);
        lua_pushvalue(L, -2); // set 'package' as upvalue for next lib
        luaL_setfuncs(L, ll_funcs, 1); // open lib into global table
        lua_pop(L, 1); // pop global table
        return 1; // return 'package' table
    }
}
