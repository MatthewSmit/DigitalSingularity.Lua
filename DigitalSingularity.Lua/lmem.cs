namespace DigitalSingularity.Lua;

using System.Diagnostics.CodeAnalysis;

public static unsafe partial class Lua
{
    public static void luaM_error(lua_State* L)
    {
        // luaD_throw(L, LUA_ERRMEM)
        throw new NotImplementedException();
    }

// /*
// ** This macro tests whether it is safe to multiply 'n' by the size of
// ** type 't' without overflows. Because 'e' is always constant, it avoids
// ** the runtime division MAX_SIZET/(e).
// ** (The macro is somewhat complex to avoid warnings:  The 'sizeof'
// ** comparison avoids a runtime comparison when overflow cannot occur.
// ** The compiler should be able to optimize the real test by itself, but
// ** when it does it, it may give a warning about "comparison is always
// ** false due to limited range of data type"; the +1 tricks the compiler,
// ** avoiding this warning but also this optimization.)
// */
// #define luaM_testsize(n,e)  \
// 	(sizeof(n) >= sizeof(size_t) && cast_sizet((n)) + 1 > MAX_SIZET/(e))
//
// #define luaM_checksize(L,n,e)  \
// 	(luaM_testsize(n,e) ? luaM_toobig(L) : cast_void(0))

    /*
    ** Computes the minimum between 'n' and 'MAX_SIZET/sizeof(t)', so that
    ** the result is not larger than 'n' and cannot overflow a 'size_t'
    ** when multiplied by the size of type 't'. (Assumes that 'n' is an
    ** 'int' and that 'int' is not larger than 'size_t'.)
    */
    private static long luaM_limitN<T>(long n)
        where T : unmanaged
    {
        return n <= long.MaxValue / sizeof(T) ? n : (int)(long.MaxValue / sizeof(T));
    }

    // /*
// ** Arrays of chars do not need any test
// */
// #define luaM_reallocvchar(L,b,on,n)  \
//   cast_charp(luaM_saferealloc_(L, (b), (on)*sizeof(char), (n)*sizeof(char)))
//
// #define luaM_freemem(L, b, s)	luaM_free_(L, (b), (s))
// #define luaM_free(L, b)		luaM_free_(L, (b), sizeof(*(b)))

    private static void luaM_freearray<T>(lua_State* L, T* b, long n)
        where T : unmanaged
    {
        luaM_free_(L, b, n * sizeof(T));
    }

    private static T* luaM_new<T>(lua_State* L)
        where T : unmanaged
    {
        return (T*)luaM_malloc_(L, sizeof(T), 0);
    }

    private static T* luaM_newvector<T>(lua_State* L, int n)
        where T : unmanaged
    {
        return (T*)luaM_malloc_(L, (long)n * sizeof(T), 0);
    }

    private static T** luaM_newvector2<T>(lua_State* L, int n)
        where T : unmanaged
    {
        return (T**)luaM_malloc_(L, (long)n * sizeof(T*), 0);
    }

    // #define luaM_newvectorchecked(L,n,t) \
//   (luaM_checksize(L,n,sizeof(t)), luaM_newvector(L,n,t))

    private static void* luaM_newobject(lua_State* L, int tag, long s)
    {
        return luaM_malloc_(L, s, tag);
    }

    private static byte* luaM_newblock(lua_State* L, int size)
    {
        return luaM_newvector<byte>(L, size);
    }

    // #define luaM_growvector(L,v,nelems,size,t,limit,e) \
// 	((v)=cast(t *, luaM_growaux_(L,v,nelems,&(size),sizeof(t), \
//                          luaM_limitN(limit,t),e)))

    private static T* luaM_reallocvector<T>(lua_State* L, void* v, long oldn, long n)
        where T : unmanaged
    {
        return (T*)luaM_realloc_(L, v, oldn * sizeof(T), n * sizeof(T));
    }

    private static T** luaM_reallocvector2<T>(lua_State* L, void* v, long oldn, long n)
        where T : unmanaged
    {
        return (T**)luaM_realloc_(L, v, oldn * sizeof(T*), n * sizeof(T*));
    }

// #define luaM_shrinkvector(L,v,size,fs,t) \
//    ((v)=cast(t *, luaM_shrinkvector_(L, v, &(size), fs, sizeof(t))))

    [DoesNotReturn]
    private static partial void luaM_toobig(lua_State* L);

    /* not to be called directly */
    private static partial void* luaM_realloc_(lua_State* L, void* block, long oldsize, long size);
    
// LUAI_FUNC void *luaM_saferealloc_ (lua_State *L, void *block, size_t oldsize,
//                                                               size_t size);

    private static partial void luaM_free_(lua_State* L, void* block, long osize);

// LUAI_FUNC void *luaM_growaux_ (lua_State *L, void *block, int nelems,
//                                int *size, unsigned size_elem, int limit,
//                                const char *what);
// LUAI_FUNC void *luaM_shrinkvector_ (lua_State *L, void *block, int *nelem,
//                                     int final_n, unsigned size_elem);
    private static partial void* luaM_malloc_(lua_State* L, long size, int tag);
}
