namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /*
    ** $Id: lzio.c $
    ** Buffered streams
    ** See Copyright Notice in lua.h
    */

    private static partial int luaZ_fill(Zio* z)
    {
        lua_State* L = z->L;
        lua_unlock(L);
        long size;
        byte* buff = z->reader(L, z->data, &size);
        lua_lock(L);
        if (buff == null || size == 0)
        {
            return -1;
        }

        z->n = size - 1; /* discount char being returned */
        z->p = buff;
        return *z->p++;
    }

    private static partial void luaZ_init(lua_State* L, Zio* z, lua_Reader reader, void* data)
    {
        z->L = L;
        z->reader = reader;
        z->data = data;
        z->n = 0;
        z->p = null;
    }

// /* --------------------------------------------------------------- read --- */
//
//     static int checkbuffer (ZIO *z) {
//         if (z->n == 0) {  /* no bytes in buffer? */
//             if (luaZ_fill(z) == EOZ)  /* try to read more */
//                 return 0;  /* no more input */
//             else {
//                 z->n++;  /* luaZ_fill consumed first byte; put it back */
//                 z->p--;
//             }
//         }
//         return 1;  /* now buffer has something */
//     }
//
//
//     size_t luaZ_read (ZIO *z, void *b, size_t n) {
//         while (n) {
//             size_t m;
//             if (!checkbuffer(z))
//                 return n;  /* no more input; return number of missing bytes */
//             m = (n <= z->n) ? n : z->n;  /* min. between n and z->n */
//             memcpy(b, z->p, m);
//             z->n -= m;
//             z->p += m;
//             b = (char *)b + m;
//             n -= m;
//         }
//         return 0;
//     }
//
//
//     const void *luaZ_getaddr (ZIO* z, size_t n) {
//         const void *res;
//         if (!checkbuffer(z))
//             return null;  /* no more input */
//         if (z->n < n)  /* not enough bytes? */
//             return null;  /* block not whole; cannot give an address */
//         res = z->p;  /* get block address */
//         z->n -= n;  /* consume these bytes */
//         z->p += n;
//         return res;
//     }
}
