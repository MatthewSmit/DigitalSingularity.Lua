namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    /*
     ** $Id: lzio.h $
     ** Buffered streams
     ** See Copyright Notice in lua.h
     */


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

    private static byte* luaZ_buffer(Mbuffer* buff)
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

    // #define luaZ_buffremove(buff,i)	((buff)->n -= cast_sizet(i))

    private static void luaZ_resetbuffer(Mbuffer* buff)
    {
        buff->n = 0;
    }

    private static void luaZ_resizebuffer(lua_State* L, Mbuffer* buff, int size)
    {
        buff->buffer = luaM_reallocvchar(
            L,
            buff->buffer,
            buff->buffsize,
            size);
        buff->buffsize = size;
    }

    // #define luaZ_freebuffer(L, buff)	luaZ_resizebuffer(L, buff, 0)

    private static partial void luaZ_init(lua_State* L, Zio* z, lua_Reader reader, void* data);
    
//     LUAI_FUNC size_t luaZ_read (ZIO* z, void *b, size_t n);	/* read next n bytes */
//
//     LUAI_FUNC const void *luaZ_getaddr (ZIO* z, size_t n);

    /* --------- Private Part ------------------ */

    private struct Zio
    {
        public long n; /* bytes still unread */
        public byte* p; /* current position in buffer */
        public lua_Reader reader; /* reader function */
        public void* data; /* additional data */
        public lua_State* L; /* Lua state (for reader) */
    }

    private static partial int luaZ_fill(Zio* z);
}
