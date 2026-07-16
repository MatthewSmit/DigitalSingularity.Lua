namespace DigitalSingularity.Lua;

public unsafe struct CFunction
{
    public delegate* managed<Lua.lua_State*, int> fm;
    public delegate* unmanaged[Cdecl]<Lua.lua_State*, int> fn;

    public int Call(Lua.lua_State* L)
    {
        if (this.fm != null)
        {
            return this.fm(L);
        }

        // throw new NotImplementedException();
        // TODO: exception handling
        return this.fn(L);
    }
    
    public void* ToPointer()
    {
        return this.fm != null ? (void*)(nint)this.fm : (void*)(nint)this.fn;
    }

    public static CFunction FromFunction(delegate* managed<Lua.lua_State*, int> f)
    {
        return new CFunction
        {
            fm = f,
        };
    }

    public static CFunction FromUnmanaged(delegate* unmanaged[Cdecl]<Lua.lua_State*, int> f)
    {
        return new CFunction
        {
            fn = f,
        };
    }

    public static bool operator ==(CFunction lhs, CFunction rhs)
    {
        return lhs.fn == null ? (lhs.fm == rhs.fm) : (lhs.fn == rhs.fn);
    }

    public static bool operator !=(CFunction lhs, CFunction rhs)
    {
        return !(lhs == rhs);
    }
}
