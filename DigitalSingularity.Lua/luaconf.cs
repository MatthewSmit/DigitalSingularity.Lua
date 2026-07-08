namespace DigitalSingularity.Lua;

using System.Runtime.InteropServices;

public static unsafe partial class Lua
{
    // TODO: remaining file
    
    /*
    ** ===================================================================
    ** General Configuration File for Lua
    **
    ** Some definitions here can be changed externally, through the compiler
    ** (e.g., with '-D' options): They are commented out or protected
    ** by '#if !defined' guards. However, several other definitions
    ** should be changed directly here, either because they affect the
    ** Lua ABI (by making the changes here, you ensure that all software
    ** connected to Lua, such as C libraries, will be compiled with the same
    ** configuration); or because they are seldom changed.
    **
    ** Search for "@@" to find all configurable definitions.
    ** ===================================================================
    */
    
    /*
    ** {====================================================================
    ** System Configuration: macros to adapt (if needed) Lua to some
    ** particular platform, for instance restricting it to C89.
    ** =====================================================================
    */

    // #if defined(LUA_USE_WINDOWS)
    // #define LUA_DL_DLL	/* enable support for DLL */
    // #define LUA_USE_C89	/* broadly, Windows is C89 */
    // #endif

    // /*
    // ** When POSIX DLL ('LUA_USE_DLOPEN') is enabled, the Lua stand-alone
    // ** application will try to dynamically link a 'readline' facility
    // ** for its REPL.  In that case, LUA_READLINELIB is the name of the
    // ** library it will look for those facilities.  If lua.c cannot open
    // ** the specified library, it will generate a warning and then run
    // ** without 'readline'.  If that macro is not defined, lua.c will not
    // ** use 'readline'.
    // */
    // #if defined(LUA_USE_LINUX)
    // #define LUA_USE_POSIX
    // #define LUA_USE_DLOPEN		/* needs an extra library: -ldl */
    // #define LUA_READLINELIB		"libreadline.so"
    // #endif

    // #if defined(LUA_USE_MACOSX)
    // #define LUA_USE_POSIX
    // #define LUA_USE_DLOPEN		/* macOS does not need -ldl */
    // #define LUA_READLINELIB		"libedit.dylib"
    // #endif

    // #if defined(LUA_USE_IOS)
    // #define LUA_USE_POSIX
    // #define LUA_USE_DLOPEN
    // #endif

    /* }================================================================== */
    
// /*
// ** {==================================================================
// ** Configuration for Number types. These options should not be
// ** set externally, because any other code connected to Lua must
// ** use the same configuration.
// ** ===================================================================
// */
//
// /*
// @@ LUA_INT_TYPE defines the type for Lua integers.
// @@ LUA_FLOAT_TYPE defines the type for Lua floats.
// ** Lua should work fine with any mix of these options supported
// ** by your C compiler. The usual configurations are 64-bit integers
// ** and 'double' (the default), 32-bit integers and 'float' (for
// ** restricted platforms), and 'long'/'double' (for C compilers not
// ** compliant with C99, which may not have support for 'long long').
// */
//
// /* predefined options for LUA_INT_TYPE */
// #define LUA_INT_INT		1
// #define LUA_INT_LONG		2
// #define LUA_INT_LONGLONG	3
//
// /* predefined options for LUA_FLOAT_TYPE */
// #define LUA_FLOAT_FLOAT		1
// #define LUA_FLOAT_DOUBLE	2
// #define LUA_FLOAT_LONGDOUBLE	3
//
//
// /* Default configuration ('long long' and 'double', for 64-bit Lua) */
// #define LUA_INT_DEFAULT		LUA_INT_LONGLONG
// #define LUA_FLOAT_DEFAULT	LUA_FLOAT_DOUBLE

    /*
    ** {==================================================================
    ** Configuration for Paths.
    ** ===================================================================
    */

    /*
    ** LUA_PATH_SEP is the character that separates templates in a path.
    ** LUA_PATH_MARK is the string that marks the substitution points in a
    ** template.
    ** LUA_EXEC_DIR in a Windows path is replaced by the executable's
    ** directory.
    */
    private const string LUA_PATH_SEP = ";";
    private const string LUA_PATH_MARK = "?";
    private const string LUA_EXEC_DIR = "!";

    /*
    @@ LUA_PATH_DEFAULT is the default path that Lua uses to look for
    ** Lua libraries.
    @@ LUA_CPATH_DEFAULT is the default path that Lua uses to look for
    ** C libraries.
    ** CHANGE them if your machine has a non-conventional directory
    ** hierarchy or if you want to install your libraries in
    ** non-conventional directories.
    */

    private static readonly string LUA_VDIR = LUA_VERSION_MAJOR_N + "." + LUA_VERSION_MINOR_N;

    private const string LUA_ROOT = "/usr/local/";
    
    /*
    ** In Windows, any exclamation mark ('!') in the path is replaced by the
    ** path of the directory of the executable file of the current process.
    */
    private static readonly string LUA_LDIR = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? @"!\lua\"
        : LUA_ROOT + "share/lua/" + LUA_VDIR + "/";
    
    private static readonly string LUA_CDIR = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "!\\"
        : LUA_ROOT + "lib/lua/" + LUA_VDIR + "/";

    private static readonly string LUA_SHRDIR = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? @"!\..\share\lua\" + LUA_VDIR + "\\"
        : "";

    private static readonly string LUA_PATH_DEFAULT = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? LUA_LDIR +
          "?.lua;" +
          LUA_LDIR +
          "?\\init.lua;" +
          LUA_CDIR +
          "?.lua;" +
          LUA_CDIR +
          "?\\init.lua;" +
          LUA_SHRDIR +
          "?.lua;" +
          LUA_SHRDIR +
          "?\\init.lua;" +
          ".\\?.lua;" +
          @".\?\init.lua"
        : LUA_LDIR +
          "?.lua;" +
          LUA_LDIR +
          "?/init.lua;" +
          LUA_CDIR +
          "?.lua;" +
          LUA_CDIR +
          "?/init.lua;" +
          "./?.lua;" +
          "./?/init.lua";

    private static readonly string LUA_CPATH_DEFAULT = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? LUA_CDIR +
          "?.dll;" +
          LUA_CDIR +
          @"..\lib\lua\" +
          LUA_VDIR +
          "\\?.dll;" +
          LUA_CDIR +
          "loadall.dll;" +
          ".\\?.dll"
        : LUA_CDIR + "?.so;" + LUA_CDIR + "loadall.so;" + "./?.so";

    /*
    @@ LUA_DIRSEP is the directory separator (for submodules).
    ** CHANGE it if your machine does not use "/" as the directory separator
    ** and is not Windows. (On Windows Lua automatically uses "\".)
    */
    private static readonly string LUA_DIRSEP = Path.DirectorySeparatorChar.ToString();

    /*
    ** LUA_CSUBSEP is the character that replaces dots in submodule names
    ** when searching for a C loader.
    ** LUA_LSUBSEP is the character that replaces dots in submodule names
    ** when searching for a Lua loader.
    */
    private static readonly string LUA_CSUBSEP = LUA_DIRSEP;

    private static readonly string LUA_LSUBSEP = LUA_DIRSEP;
    
    /*
    ** LUA_IGMARK is a mark to ignore all after it when building the
    ** module name (e.g., used to build the luaopen_ function name).
    ** Typically, the suffix after the mark is the module version,
    ** as in "mod-v1.2.so".
    */
    private const string LUA_IGMARK = "-";

    /*
    @@ The following macros supply trivial compatibility for some
    ** changes in the API. The macros themselves document how to
    ** change your code to avoid using them.
    ** (Once more, these macros were officially removed in 5.3, but they are
    ** still available here.)
    */
    public static ulong lua_strlen(lua_State* L, int i)
    {
        return lua_rawlen(L, i);
    }
    
    public static ulong lua_objlen(lua_State* L, int i)
    {
        return lua_rawlen(L, i);
    }

    public static bool lua_equal(lua_State* L, int idx1, int idx2)
    {
        return lua_compare(L, idx1, idx2, LUA_OPEQ);
    }

    public static bool lua_lessthan(lua_State* L, int idx1, int idx2)
    {
        return lua_compare(L, idx1, idx2, LUA_OPLT);
    }

    // /*
// ** {==================================================================
// ** Configuration for Numbers (low-level part).
// ** Change these definitions if no predefined LUA_FLOAT_* / LUA_INT_*
// ** satisfy your needs.
// ** ===================================================================
// */
//
// /*
// @@ LUAI_UACNUMBER is the result of a 'default argument promotion'
// @@ over a floating number.
// @@ l_floatatt(x) corrects float attribute 'x' to the proper float type
// ** by prefixing it with one of FLT/DBL/LDBL.
// @@ LUA_NUMBER_FRMLEN is the length modifier for writing floats.
// @@ LUA_NUMBER_FMT is the format for writing floats with the maximum
// ** number of digits that respects tostring(tonumber(numeral)) == numeral.
// ** (That would be floor(log10(2^n)), where n is the number of bits in
// ** the float mantissa.)
// @@ LUA_NUMBER_FMT_N is the format for writing floats with the minimum
// ** number of digits that ensures tonumber(tostring(number)) == number.
// ** (That would be LUA_NUMBER_FMT+2.)
// @@  allows the addition of an 'l' or 'f' to all math operations.
// @@ l_floor takes the floor of a float.
// @@ lua_str2number converts a decimal numeral to a number.
// */
//
//
// /* The following definition is good for most cases here */
//
// #define l_floor(x)		((floor)(x))
//
//
// /* now the variable definitions */

// #define LUA_NUMBER	double
//
// #define l_floatatt(n)		(DBL_##n)
//
// #define LUAI_UACNUMBER	double

// #define LUA_NUMBER_FRMLEN	""
// #define LUA_NUMBER_FMT		"%.15g"
// #define LUA_NUMBER_FMT_N	"%.17g"
//
// #define lua_str2number(s,p)	strtod((s), (p))

    /*
    @@ LUA_UNSIGNED is the unsigned version of LUA_INTEGER.
    @@ LUAI_UACINT is the result of a 'default argument promotion'
    @@ over a LUA_INTEGER.
    @@ LUA_INTEGER_FRMLEN is the length modifier for reading/writing integers.
    @@ LUA_INTEGER_FMT is the format for writing integers.
    @@ LUA_MAXINTEGER is the maximum value for a LUA_INTEGER.
    @@ LUA_MININTEGER is the minimum value for a LUA_INTEGER.
    @@ LUA_MAXUNSIGNED is the maximum value for a LUA_UNSIGNED.
    @@ lua_integer2str converts an integer to a string.
    */

    /* The following definitions are good for most cases here */

    // private const string LUA_INTEGER_FMT = "%" + LUA_INTEGER_FRMLEN + "d";

// #define LUAI_UACINT		LUA_INTEGER
//
// /*
// ** use LUAI_UACINT here to avoid problems with promotions (which
// ** can turn a comparison between unsigneds into a signed comparison)
// */
// #define LUA_UNSIGNED		unsigned LUAI_UACINT

// #elif LUA_INT_TYPE == LUA_INT_LONG	/* }{ long */
// #elif LUA_INT_TYPE == LUA_INT_LONGLONG	/* }{ long long */
//
// #define LUA_INTEGER		long long
// #define LUA_INTEGER_FRMLEN	"ll"
//
// #define LUA_MAXINTEGER		LLONG_MAX
// #define LUA_MININTEGER		LLONG_MIN
//
// #define LUA_MAXUNSIGNED		ULLONG_MAX

// /*
// @@ lua_getlocaledecpoint gets the locale "radix character" (decimal point).
// ** Change that if you do not want to use C locales. (Code using this
// ** macro must include the header 'locale.h'.)
// */
// #if !defined(lua_getlocaledecpoint)
// #define lua_getlocaledecpoint()		(localeconv()->decimal_point[0])
// #endif

    /* }================================================================== */

    /*
    ** {==================================================================
    ** Language Variations
    ** =====================================================================
    */

    /*
    @@ LUA_NOCVTN2S/LUA_NOCVTS2N control how Lua performs some
    ** coercions. Define LUA_NOCVTN2S to turn off automatic coercion from
    ** numbers to strings. Define LUA_NOCVTS2N to turn off automatic
    ** coercion from strings to numbers.
    */
    /* #define LUA_NOCVTN2S */
    /* #define LUA_NOCVTS2N */

    /* }================================================================== */


    /*
    ** {==================================================================
    ** Macros that affect the API and must be stable (that is, must be the
    ** same when you compile Lua and when you compile code that links to
    ** Lua).
    ** =====================================================================
    */

    /*
    @@ LUA_EXTRASPACE defines the size of a raw memory area associated with
    ** a Lua state with very fast access.
    ** CHANGE it if you need a different size.
    */
    private const int LUA_EXTRASPACE =
#if LUA_TEST
        sizeof(ulong) * 2;
#else
        sizeof(ulong);
#endif

    /*
    @@ LUA_IDSIZE gives the maximum size for the description of the source
    ** of a function in debug information.
    ** CHANGE it if you want a different size.
    */
    internal const int LUA_IDSIZE = 60;

    /*
    @@ LUAL_BUFFERSIZE is the initial buffer size used by the lauxlib
    ** buffer system.
    */
    private const int LUAL_BUFFERSIZE =
#if LUA_TEST
        23;
#else
        16 * sizeof(long) * sizeof(double);
#endif
}
