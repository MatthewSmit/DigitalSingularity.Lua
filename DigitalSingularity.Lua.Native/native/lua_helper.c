#include <stddef.h>
#include <stdint.h>
#include <stdarg.h>
#include <dnne.h>

typedef struct
{
    va_list args;
} va_state;

typedef struct
{
    va_state* state;
    
    int32_t (DNNE_CALLTYPE_CDECL* next_int32)(struct va_state* state);
    int64_t (DNNE_CALLTYPE_CDECL* next_int64)(struct va_state* state);
    double (DNNE_CALLTYPE_CDECL* next_double)(struct va_state* state);
    void* (DNNE_CALLTYPE_CDECL* next_pointer)(struct va_state* state);
} va_reader;

static int32_t DNNE_CALLTYPE_CDECL next_int64(va_state* state)
{
    return va_arg(state->args, int32_t);
}

static int64_t DNNE_CALLTYPE_CDECL next_int32(va_state* state)
{
    return va_arg(state->args, int64_t);
}

static double DNNE_CALLTYPE_CDECL next_double(va_state* state)
{
    return va_arg(state->args, double);
}

static void* DNNE_CALLTYPE_CDECL next_pointer(va_state* state)
{
    return va_arg(state->args, void*);
}

DNNE_EXTERN_C DNNE_API int32_t DNNE_CALLTYPE_CDECL intl_luaL_error(struct lua_State* L, uint8_t* fmt, struct va_reader* va);

DNNE_API int luaL_error(struct lua_State* L, char* fmt, ...)
{
    va_state state;
    va_start(state.args, fmt);
    
    va_reader reader;
    reader.state = &state;
    reader.next_int32 = next_int32;
    reader.next_int64 = next_int64;
    reader.next_double = next_double;
    reader.next_pointer = next_pointer;
    
    int result = intl_luaL_error(L, fmt, &reader);
    va_end(state.args);
    
    return result;
}

DNNE_EXTERN_C DNNE_API uint8_t* DNNE_CALLTYPE_CDECL intl_lua_pushfstring(struct lua_State* L, uint8_t* fmt, struct va_reader* argp);

DNNE_API const char* lua_pushvfstring(struct lua_State *L, const char *fmt, va_list argp)
{
    va_state state;
    state.args = argp;
    
    va_reader reader;
    reader.state = &state;
    reader.next_int32 = next_int32;
    reader.next_int64 = next_int64;
    reader.next_double = next_double;
    reader.next_pointer = next_pointer;
    
    const char* result = intl_lua_pushfstring(L, fmt, &reader);
    
    return result;
}

DNNE_API const char* lua_pushfstring(struct lua_State *L, const char *fmt, ...)
{
    va_state state;
    va_start(state.args, fmt);
    
    va_reader reader;
    reader.state = &state;
    reader.next_int32 = next_int32;
    reader.next_int64 = next_int64;
    reader.next_double = next_double;
    reader.next_pointer = next_pointer;
    
    const char* result = intl_lua_pushfstring(L, fmt, &reader);
    va_end(state.args);
    
    return result;
}

DNNE_EXTERN_C DNNE_API int32_t DNNE_CALLTYPE_CDECL impl_lua_gc(struct lua_State* L, int32_t what, struct va_reader* va);

DNNE_API int lua_gc(struct lua_State *L, int what, ...)
{
    va_state state;
    va_start(state.args, what);
    
    va_reader reader;
    reader.state = &state;
    reader.next_int32 = next_int32;
    reader.next_int64 = next_int64;
    reader.next_double = next_double;
    reader.next_pointer = next_pointer;
    
    const char* result = impl_lua_gc(L, what, &reader);
    va_end(state.args);
    
    return result;
}