namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    // $Id: lzio.c $
    // Buffered streams
    // See Copyright Notice in lua.h

    private static int zgetc(Zio* z)
    {
        return z->n-- > 0 ? *z->p++ : luaZ_fill(z);
    }

    private struct Mbuffer
    {
        public byte* buffer;
        public long n;
        public long buffsize;
    }

    private static void luaZ_initbuffer(lua_State* L, Mbuffer* buff)
    {
        buff->buffer = null;
        buff->buffsize = 0;
    }

    [Obsolete]
    private static byte* luaZ_bufferptr(Mbuffer* buff)
    {
        return buff->buffer;
    }

    private static ref long luaZ_sizebuffer(Mbuffer* buff)
    {
        return ref buff->buffsize;
    }

    private static ref long luaZ_bufflen(Mbuffer* buff)
    {
        return ref buff->n;
    }
    
    private static Span<byte> luaZ_buffer(Mbuffer* buff)
    {
        return new Span<byte>(buff->buffer, checked((int)buff->buffsize));
    }

    private static void luaZ_buffremove(Mbuffer* buff, int i)
    {
        buff->n -= i;
    }

    private static void luaZ_resetbuffer(Mbuffer* buff)
    {
        buff->n = 0;
    }

    private static void luaZ_resizebuffer(lua_State* L, Mbuffer* buff, long size)
    {
        buff->buffer = luaM_reallocvchar(
            L,
            buff->buffer,
            buff->buffsize,
            size);
        buff->buffsize = size;
    }

    private static void luaZ_freebuffer(lua_State* L, Mbuffer* buff)
    {
        luaZ_resizebuffer(L, buff, 0);
    }

    // --------- Private Part ------------------

    internal struct Zio
    {
        public long n; // bytes still unread
        public byte* p; // current position in buffer
        public lua_Reader reader; // reader function
        public void* data; // additional data
        public lua_State* L; // Lua state (for reader)
    }
    
    internal static int luaZ_fill(Zio* z)
    {
        lua_State* L = z->L;
        lua_unlock(L);
        byte* buff = z->reader(L, z->data, out long size);
        lua_lock(L);
        if (buff == null || size == 0)
        {
            return -1;
        }

        z->n = size - 1; // discount char being returned
        z->p = buff;
        return *z->p++;
    }

    internal static void luaZ_init(lua_State* L, Zio* z, lua_Reader reader, void* data)
    {
        z->L = L;
        z->reader = reader;
        z->data = data;
        z->n = 0;
        z->p = null;
    }

    // --------------------------------------------------------------- read ---

    private static bool checkbuffer(Zio* z)
    {
        if (z->n == 0)
        {
            // no bytes in buffer?
            if (luaZ_fill(z) == -1) // try to read more
            {
                return false; // no more input
            }

            z->n++; // luaZ_fill consumed first byte; put it back
            z->p--;
        }

        return true; // now buffer has something
    }

    internal static long luaZ_read(Zio* z, void* b, long n)
    {
        while (n > 0)
        {
            if (!checkbuffer(z))
            {
                return n; // no more input; return number of missing bytes
            }

            long m = n <= z->n ? n : z->n; // min. between n and z->n
            memcpy(b, z->p, m);
            z->n -= m;
            z->p += m;
            b = (byte*)b + m;
            n -= m;
        }

        return 0;
    }

    internal static void* luaZ_getaddr(Zio* z, long n)
    {
        if (!checkbuffer(z))
        {
            return null; // no more input
        }

        if (z->n < n) // not enough bytes?
        {
            return null; // block not whole; cannot give an address
        }

        byte* res = z->p; // get block address
        z->n -= n; // consume these bytes
        z->p += n;
        return res;
    }
}
