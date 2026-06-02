namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.CompilerServices;

public static unsafe partial class Lua
{
    private static int strcmp(byte* s1, byte* s2)
    {
        // If both are the same pointer or both null
        if (s1 == s2)
        {
            return 0;
        }

        if (s1 == null)
        {
            return -1;
        }

        if (s2 == null)
        {
            return 1;
        }

        while (*s1 != 0 && *s1 == *s2)
        {
            s1++;
            s2++;
        }

        // Return the difference of the unsigned character values
        return *s1 - *s2;
    }

    private static int strlen(byte* str)
    {
        if (str == null)
        {
            return 0;
        }

        byte* start = str;
        while (*str != 0)
        {
            str++;
        }

        return (int)(str - start); // Pointer subtraction gives the length
    }

    private static void memcpy(void* dest, void* src, long n)
    {
        Debug.Assert(n <= uint.MaxValue);
        Unsafe.CopyBlock(dest, src, (uint)n);
    }

    private static int memcmp(void* ptr1, void* ptr2, int count)
    {
        ReadOnlySpan<byte> view1 = new ReadOnlySpan<byte>(ptr1, count);
        ReadOnlySpan<byte> view2 = new ReadOnlySpan<byte>(ptr2, count);

        return view1.SequenceCompareTo(view2);
    }

    private static ReadOnlySpan<byte> strchr(ReadOnlySpan<byte> s, char c)
    {
        int i = s.IndexOf((byte)c);
        if (i >= 0)
        {
            return s[i..];
        }
        
        return new ReadOnlySpan<byte>();
    }
    
    private static string? strstr(string s, string sub)
    {
        int i = s.IndexOf(sub, StringComparison.Ordinal);
        if (i >= 0)
        {
            return sub[i..];
        }

        return null;
    }
}
