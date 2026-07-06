namespace DigitalSingularity.Lua;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class Extensions
{
    extension(ReadOnlySpan<byte> ptr)
    {
        public unsafe byte* ToPointer()
        {
            ref byte r0 = ref MemoryMarshal.GetReference(ptr);
            return (byte*)Unsafe.AsPointer(ref r0);
        }
    }
    
    extension<T>(GCHandle<T> handle)
        where T : class
    {
        public unsafe void* ToPointer()
        {
            return (void*)GCHandle<T>.ToIntPtr(handle);
        }
    }
}
