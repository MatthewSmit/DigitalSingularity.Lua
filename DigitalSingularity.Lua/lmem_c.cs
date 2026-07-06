namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
//     /*
// ** $Id: lmem.c $
// ** Interface to Memory Manager
// ** See Copyright Notice in lua.h
// */
//
// #define lmem_c
// #define LUA_CORE
//
// #include "lprefix.h"
//
//
// #include <stddef.h>
//
// #include "lua.h"
//
// #include "ldebug.h"
// #include "ldo.h"
// #include "lgc.h"
// #include "lmem.h"
// #include "lobject.h"
// #include "lstate.h"
//
//
//
// /*
// ** About the realloc function:
// ** void *frealloc (void *ud, void *ptr, size_t osize, size_t nsize);
// ** ('osize' is the old size, 'nsize' is the new size)
// **
// ** - frealloc(ud, p, x, 0) frees the block 'p' and returns null.
// ** Particularly, frealloc(ud, null, 0, 0) does nothing,
// ** which is equivalent to free(null) in ISO C.
// **
// ** - frealloc(ud, null, x, s) creates a new block of size 's'
// ** (no matter 'x'). Returns null if it cannot create the new block.
// **
// ** - otherwise, frealloc(ud, b, x, y) reallocates the block 'b' from
// ** size 'x' to size 'y'. Returns null if it cannot reallocate the
// ** block to the new size.
// */

    /// <summary>
    /// Macro to call the allocation function.
    /// </summary>
    private static void* callfrealloc(global_State* g, void* block, long os, long ns)
    {
        return g->frealloc(g->ud, block, os, ns);
    }

    /*
    ** When an allocation fails, it will try again after an emergency
    ** collection, except when it cannot run a collection.  The GC should
    ** not be called while the state is not fully built, as the collector
    ** is not yet fully initialised. Also, it should not be called when
    ** 'gcstopem' is true, because then the interpreter is in the middle of
    ** a collection step.
    */
    private static bool cantryagain(global_State* g)
    {
        return completestate(g) && !g->gcstopem;
    }

#if EMERGENCYGCTESTS
// /*
// ** First allocation will fail except when freeing a block (frees never
// ** fail) and when it cannot try again; this fail will trigger 'tryagain'
// ** and a full GC cycle at every allocation.
// */
// static void *firsttry (global_State *g, void *block, size_t os, size_t ns) {
//   if (ns > 0 && cantryagain(g))
//     return null;  /* fail */
//   else  /* normal allocation */
//     return callfrealloc(g, block, os, ns);
// }
#else
    private static void* firsttry(global_State* g, void* block, long os, long ns)
    {
        return callfrealloc(g, block, os, ns);
    }
#endif
    
    /*
    ** {==================================================================
    ** Functions to allocate/deallocate arrays for the Parser
    ** ===================================================================
    */

    /*
    ** Minimum size for arrays during parsing, to avoid overhead of
    ** reallocating to size 1, then 2, and then 4. All these arrays
    ** will be reallocated to exact sizes or erased when parsing ends.
    */
    private const int MINSIZEARRAY = 4;

    internal static partial void* luaM_growaux_(
        lua_State* L,
        void* block,
        int nelems,
        ref int psize,
        int size_elem,
        int limit,
        string what)
    {
        int size = psize;
        if (nelems + 1 <= size) /* does one extra element still fit? */
        {
            return block; /* nothing to be done */
        }

        if (size >= limit / 2)
        {
            /* cannot double it? */
            if (size >= limit) /* cannot grow even a little? */
            {
//       luaG_runerror(L, "too many %s (limit is %d)", what, limit);
                throw new NotImplementedException();
            }

            size = limit; /* still have at least one free place */
        }
        else
        {
            size *= 2;
            if (size < MINSIZEARRAY)
            {
                size = MINSIZEARRAY; /* minimum size */
            }
        }

        Debug.Assert(nelems + 1 <= size && size <= limit);
        /* 'limit' ensures that multiplication will not overflow */
        void* newblock = luaM_saferealloc_(
            L,
            block,
            (long)psize * size_elem,
            (long)size * size_elem);
        psize = size; /* update only when everything else is OK */
        return newblock;
    }

    /*
    ** In prototypes, the size of the array is also its number of
    ** elements (to save memory). So, if it cannot shrink an array
    ** to its number of elements, the only option is to raise an
    ** error.
    */
    internal static partial void* luaM_shrinkvector_(lua_State* L, void* block, ref int size, int final_n, int size_elem)
    {
        long oldsize = (long)size * size_elem;
        long newsize = (long)final_n * size_elem;
        Debug.Assert(newsize <= oldsize);
        void* newblock = luaM_saferealloc_(L, block, oldsize, newsize);
        size = final_n;
        return newblock;
    }

    /* }================================================================== */

    internal static partial void luaM_toobig(lua_State* L)
    {
        luaG_runerror(L, "memory allocation error: block too big");
    }
    
    /*
    ** Free memory
    */
    internal static partial void luaM_free_(lua_State* L, void* block, long osize)
    {
        global_State* g = G(L);
        Debug.Assert((osize == 0) == (block == null));
        callfrealloc(g, block, osize, 0);
        g->GCdebt += osize;
    }

    /*
     ** In case of allocation fail, this function will do an emergency
     ** collection to free some memory and then try the allocation again.
     */
    private static void* tryagain(
        lua_State* L,
        void* block,
        long osize,
        long nsize)
    {
        global_State* g = G(L);
        if (cantryagain(g))
        {
            luaC_fullgc(L, true); /* try to free some memory... */
            return callfrealloc(g, block, osize, nsize); /* try again */
        }

        return null; /* cannot run an emergency collection */
    }

    /*
    ** Generic allocation routine.
    */
    internal static partial void* luaM_realloc_(lua_State* L, void* block, long oldsize, long size)
    {
        global_State* g = G(L);
        Debug.Assert((oldsize == 0) == (block == null));
        void* newblock = firsttry(g, block, oldsize, size);
        if (newblock == null && size > 0)
        {
            newblock = tryagain(L, block, oldsize, size);
            if (newblock == null) /* still no memory? */
            {
                return null; /* do not update 'GCdebt' */
            }
        }

        Debug.Assert(size == 0 == (newblock == null));
        g->GCdebt -= size - oldsize;
        return newblock;
    }

    internal static partial void* luaM_saferealloc_(lua_State* L, void* block, long osize, long nsize)
    {
        void* newblock = luaM_realloc_(L, block, osize, nsize);
        if (newblock == null && nsize > 0) /* allocation failed? */
        {
            luaM_error(L);
        }

        return newblock;
    }

    internal static partial void* luaM_malloc_(lua_State* L, long size, int tag)
    {
        if (size == 0)
        {
            return null; /* that's all */
        }

        global_State* g = G(L);
        void* newblock = firsttry(g, null, tag, size);
        if (newblock == null)
        {
            newblock = tryagain(L, null, tag, size);
            if (newblock == null)
            {
                luaM_error(L);
            }
        }

        g->GCdebt -= size;
        return newblock;
    }
}
