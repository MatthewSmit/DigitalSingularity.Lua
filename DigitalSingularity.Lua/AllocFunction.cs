namespace DigitalSingularity.Lua;

public unsafe struct AllocFunction
{
    public delegate* managed<void*, void*, long, long, void*> fm;
    public delegate* unmanaged[Cdecl]<void*, void*, long, long, void*> fn;

    public void* Call(void* ud, void* ptr, long osize, long nsize)
    {
        if (this.fm != null)
        {
            return this.fm(ud, ptr, osize, nsize);
        }

        // throw new NotImplementedException();
        // TODO: exception handling
        return this.fn(ud, ptr, osize, nsize);
    }
    
    public void* ToPointer()
    {
        return this.fm != null ? (void*)(nint)this.fm : (void*)(nint)this.fn;
    }

    public static AllocFunction FromFunction(delegate* managed<void*, void*, long, long, void*> f)
    {
        return new AllocFunction
        {
            fm = f,
        };
    }

    public static AllocFunction FromUnmanaged(delegate* unmanaged[Cdecl]<void*, void*, long, long, void*> f)
    {
        return new AllocFunction
        {
            fn = f,
        };
    }

    public static bool operator ==(AllocFunction lhs, AllocFunction rhs)
    {
        return lhs.fn == null ? (lhs.fm == rhs.fm) : (lhs.fn == rhs.fn);
    }

    public static bool operator !=(AllocFunction lhs, AllocFunction rhs)
    {
        return !(lhs == rhs);
    }
}
