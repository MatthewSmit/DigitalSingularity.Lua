namespace DigitalSingularity.Lua;

using System.Runtime.InteropServices;

#if LUA_TEST
public static unsafe partial class Lua
{
// /* test Lua with compatibility code */
// #define LUA_COMPAT_MATHLIB
// #undef LUA_COMPAT_GLOBAL
//
//
// #define LUA_DEBUG
//
//
// /* turn on assertions */
// #define LUAI_ASSERT
//
//
// /* to avoid warnings, and to make sure value is really unused */
// #define UNUSED(x)       (x=0, (void)(x))
//
//
// /* test for sizes in 'l_sprintf' (make sure whole buffer is available) */
// #undef l_sprintf
// #if !defined(LUA_USE_C89)
// #define l_sprintf(s,sz,f,i)	(memset(s,0xAB,sz), snprintf(s,sz,f,i))
// #else
// #define l_sprintf(s,sz,f,i)	(memset(s,0xAB,sz), sprintf(s,f,i))
// #endif
//
//
// /* get a chance to test code without jump tables */
// #define LUA_USE_JUMPTABLE	0
//
//
// /* use 32-bit integers in random generator */
// #define LUA_RAND32
//
//
// /* test stack reallocation without strict address use */
// #define LUAI_STRICT_ADDRESS	0
//

    /* memory-allocator control variables */
    private struct Memcontrol
    {
        public int failnext;
        public long numblocks;
        public long total;
        public long maxmem;
        public long memlimit;
        public long countlimit;
        public fixed uint objcount[LUA_NUMTYPES];
    }

    private static Memcontrol* l_memcontrol = (Memcontrol*)NativeMemory.AllocZeroed((nuint)sizeof(Memcontrol));

    // #define luai_tracegc(L,f)		luai_tracegctest(L, f)
// extern void luai_tracegctest (lua_State *L, int first);
//
//
// /*
// ** generic variable for debug tricks
// */
// extern void *l_Trick;
//
//
// /*
// ** Function to traverse and check all memory used by Lua
// */
// extern int lua_checkmemory (lua_State *L);
//
// /*
// ** Function to print an object GC-friendly
// */
// struct GCObject;
// extern void lua_printobj (lua_State *L, struct GCObject *o);
//
//
// /*
// ** Function to print a value
// */
// struct TValue;
// extern void lua_printvalue (struct TValue *v);
//
// /*
// ** Function to print the stack
// */
// extern void lua_printstack (lua_State *L);
// extern int lua_printallstack (lua_State *L);

    /* test for lock/unlock */
    private struct L_EXTRA
    {
        public int @lock;
        public int* plock;
    }

    private static partial int luaB_opentests(lua_State* L);

    private static partial void* debug_realloc(void* ud, void* block, long osize, long nsize);

    public static void luai_openlibs(lua_State* L)
    {
        luaL_openlibs(L);
        luaL_requiref(L, "T", &luaB_opentests, true);
        lua_pop(L, 1);
    }

// /* change some sizes to give some bugs a chance */
//
// #undef LUAL_BUFFERSIZE
// #define LUAL_BUFFERSIZE		23
// #define MINSTRTABSIZE		2
// #define MAXIWTHABS		3

    private const int STRCACHE_N = 23;
    private const int STRCACHE_M = 5;

// #define MAXINDEXRK	1
//
// /* test mode uses more stack space */
// #undef LUAI_MAXCCALLS
// #define LUAI_MAXCCALLS	180
//
//
// /* force Lua to use its own implementations */
// #undef lua_strx2number
// #undef lua_number2strx
}
#endif
