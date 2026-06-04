namespace DigitalSingularity.Lua;

using System.Runtime.InteropServices;

#if LUA_TEST
public static unsafe partial class Lua
{
// /* test for sizes in 'l_sprintf' (make sure whole buffer is available) */ TODO
// #undef l_sprintf
// #if !defined(LUA_USE_C89)
// #define l_sprintf(s,sz,f,i)	(memset(s,0xAB,sz), snprintf(s,sz,f,i))
// #else
// #define l_sprintf(s,sz,f,i)	(memset(s,0xAB,sz), sprintf(s,f,i))
// #endif

// /* get a chance to test code without jump tables */
// #define LUA_USE_JUMPTABLE	0 TODO

// /* test stack reallocation without strict address use */ TODO
// #define LUAI_STRICT_ADDRESS	0

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

    private static void luai_tracegc(lua_State* L, bool f)
    {
        luai_tracegctest(L, f);
    }

    private static partial void luai_tracegctest(lua_State* L, bool first);

// /*
// ** generic variable for debug tricks TODO
// */
// extern void *l_Trick;

    /*
    ** Function to traverse and check all memory used by Lua
    */
    private static partial int lua_checkmemory(lua_State* L);
    
    /*
    ** Function to print an object GC-friendly
    */
    private static partial void lua_printobj(lua_State* L, GCObject* o);
    
    /*
    ** Function to print a value
    */
    private static partial void lua_printvalue(TValue* v);

    /*
    ** Function to print the stack
    */
    private static partial void lua_printstack(lua_State* L);
    private static partial int lua_printallstack(lua_State* L);

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
}
#endif
