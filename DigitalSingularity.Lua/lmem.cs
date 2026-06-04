namespace DigitalSingularity.Lua;

using System.Diagnostics.CodeAnalysis;

public static unsafe partial class Lua
{
    public static void luaM_error(lua_State* L)
    {
        // luaD_throw(L, LUA_ERRMEM)
        throw new NotImplementedException();
    }

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

    /*
    ** Arrays of chars do not need any test
    */
    private static byte* luaM_reallocvchar(lua_State* L, byte* b, long on, long n)
    {
        return (byte*)luaM_saferealloc_(L, b, on, n);
    }

    private static void luaM_freemem(lua_State* L, void* b, long s)
    {
        luaM_free_(L, b, s);
    }

    private static void luaM_free<T>(lua_State* L, T* b)
        where T : unmanaged
    {
        luaM_free_(L, b, sizeof(T));
    }

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

    private static void luaM_newvectorchecked<T>(lua_State* L, int n)
        where T : unmanaged
    {
        luaM_newvector<T>(L, n);
    }

    private static void* luaM_newobject(lua_State* L, int tag, long s)
    {
        return luaM_malloc_(L, s, tag);
    }

    private static byte* luaM_newblock(lua_State* L, int size)
    {
        return luaM_newvector<byte>(L, size);
    }

    private static T* luaM_growvector<T>(lua_State* L, ref T* v, int nelems, ref int size, long limit, string e)
        where T : unmanaged
    {
        return v = (T*)luaM_growaux_(
            L,
            v,
            nelems,
            ref size,
            sizeof(T),
            checked((int)luaM_limitN<T>(limit)),
            e);
    }

    private static T** luaM_growvector2<T>(lua_State* L, ref T** v, int nelems, ref int size, long limit, string e)
        where T : unmanaged
    {
        return v = (T**)luaM_growaux_(
            L,
            v,
            nelems,
            ref size,
            sizeof(T*),
            checked((int)luaM_limitN<nint>(limit)),
            e);
    }

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

    private static void luaM_shrinkvector<T>(lua_State* L, ref T* v, ref int size, int fs)
        where T : unmanaged
    {
        v = (T*)luaM_shrinkvector_(L, v, ref size, fs, sizeof(T));
    }

    private static void luaM_shrinkvector<T>(lua_State* L, ref T** v, ref int size, int fs)
        where T : unmanaged
    {
        v = (T**)luaM_shrinkvector_(L, v, ref size, fs, sizeof(T*));
    }

    [DoesNotReturn]
    private static partial void luaM_toobig(lua_State* L);

    /* not to be called directly */
    private static partial void* luaM_realloc_(lua_State* L, void* block, long oldsize, long size);

    private static partial void* luaM_saferealloc_(lua_State* L, void* block, long oldsize, long size);

    private static partial void luaM_free_(lua_State* L, void* block, long osize);

    private static partial void* luaM_growaux_(
        lua_State* L,
        void* block,
        int nelems,
        ref int psize,
        int size_elem,
        int limit,
        string what);

    private static partial void* luaM_shrinkvector_(lua_State* L, void* block, ref int nelem, int final_n, int size_elem);
    
    private static partial void* luaM_malloc_(lua_State* L, long size, int tag);
}
