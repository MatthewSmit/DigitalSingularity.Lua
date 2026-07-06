namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /*
    ** $Id: lzio.c $
    ** Buffered streams
    ** See Copyright Notice in lua.h
    */

    internal static partial int luaZ_fill(Zio* z)
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

    internal static partial void luaZ_init(lua_State* L, Zio* z, lua_Reader reader, void* data)
    {
        z->L = L;
        z->reader = reader;
        z->data = data;
        z->n = 0;
        z->p = null;
    }

    /* --------------------------------------------------------------- read --- */

    private static bool checkbuffer(Zio* z)
    {
        if (z->n == 0)
        {
            /* no bytes in buffer? */
            if (luaZ_fill(z) == -1) /* try to read more */
            {
                return false; /* no more input */
            }

            z->n++; /* luaZ_fill consumed first byte; put it back */
            z->p--;
        }

        return true; /* now buffer has something */
    }

    internal static partial long luaZ_read(Zio* z, void* b, long n)
    {
        while (n > 0)
        {
            if (!checkbuffer(z))
            {
                return n; /* no more input; return number of missing bytes */
            }

            long m = (n <= z->n) ? n : z->n; /* min. between n and z->n */
            memcpy(b, z->p, m);
            z->n -= m;
            z->p += m;
            b = (byte*)b + m;
            n -= m;
        }

        return 0;
    }

    internal static partial void* luaZ_getaddr(Zio* z, long n)
    {
        if (!checkbuffer(z))
        {
            return null; /* no more input */
        }

        if (z->n < n) /* not enough bytes? */
        {
            return null; /* block not whole; cannot give an address */
        }

        byte* res = z->p; /* get block address */
        z->n -= n; /* consume these bytes */
        z->p += n;
        return res;
    }
}
