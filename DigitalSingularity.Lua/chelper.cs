[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DigitalSingularity.Lua.Test")]

namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.CompilerServices;

public static unsafe partial class Lua
{
    private const int DBL_MANT_DIG = 53;
    
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
    
    private static int strcmp(byte* s1, string? s2)
    {
        // If both are the same pointer or both null
        if (s1 == null && s2 == null)
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

        int i = 0;

        while (*s1 != 0 && *s1 == (s2.Length < i ? s2[i] : 0))
        {
            s1++;
            i++;
        }

        // Return the difference of the unsigned character values
        return *s1 - s2[i];
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

    private static byte* strpbrk(byte* str, string toFind)
    {
        if (str == null || string.IsNullOrEmpty(toFind))
        {
            return null;
        }
        
        bool* lut = stackalloc bool[256];
        foreach (char c in  toFind)
        {
            if (c <= 0xFF)
            {
                lut[c] = true;
            }
        }
        
        byte* p = str;
        while (*p != 0)
        {
            if (lut[*p])
            {
                return p;
            }

            p++;
        }

        return null;
    }

    internal static double strtod(byte* str, byte** endptr)
    {
        byte* p = str;
        while (char.IsWhiteSpace((char)*p))
        {
            p++;
        }

        bool isNegative = false;
        if (*p == '+')
        {
            p++;
        }
        else if (*p == '-')
        {
            isNegative = true;
            p++;
        }

        byte* start = p;

        if (MatchString(p, "infinity"))
        {
            if (endptr != null)
            {
                *endptr = p + 8;
            }

            return isNegative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (MatchString(p, "inf"))
        {
            if (endptr != null)
            {
                *endptr = p + 3;
            }

            return isNegative ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (MatchString(p, "nan"))
        {
            p += 3;

            // Don't bother with NANsequence

            if (endptr != null)
            {
                *endptr = p;
            }

            return double.NaN;
        }

        // - A sequence of digits, optionally containing a decimal-point character (.), optionally followed by an exponent part (an e or E character followed by an optional sign and a sequence of digits).
        // - A 0x or 0X prefix, then a sequence of hexadecimal digits (as in isxdigit) optionally containing a period which separates the whole and fractional number parts. Optionally followed by a power of 2 exponent (a p or P character followed by an optional sign and a sequence of hexadecimal digits).

        double val = 0.0;
        bool hasDigits = false;
        int expAdj = 0;
        int exp = 0;

        if (*p == '0' && (*(p + 1) == 'x' || *(p + 1) == 'X'))
        {
            p += 2;
            ulong mantissa = 0;
            bool sticky = false;
            bool foundNonZero = false;

            while (char.IsAsciiHexDigit((char)*p))
            {
                int h = HexValue(*p);
                if (h != 0 || foundNonZero)
                {
                    foundNonZero = true;
                    // Only accumulate if shifting won't drop the top 4 bits of the 64-bit ulong
                    if (mantissa <= 0x0FFFFFFFFFFFFFFF)
                    {
                        mantissa = (mantissa << 4) | (uint)h;
                    }
                    else
                    {
                        if (h != 0)
                        {
                            sticky = true;
                        }

                        expAdj += 4;
                    }
                }

                p++;
                hasDigits = true;
            }

            if (*p == '.')
            {
                p++;
                while (char.IsAsciiHexDigit((char)*p))
                {
                    int h = HexValue(*p);
                    if (h != 0 || foundNonZero)
                    {
                        foundNonZero = true;
                        if (mantissa <= 0x0FFFFFFFFFFFFFFF)
                        {
                            mantissa = (mantissa << 4) | (uint)h;
                            expAdj -= 4; // Shifted into mantissa, decrease place value
                        }
                        else
                        {
                            if (h != 0)
                            {
                                sticky = true;
                            }
                            // Do not adjust expAdj here; place value is preserved by not shifting
                        }
                    }
                    else
                    {
                        // Leading zeros in the fraction skip accumulation but adjust the exponent
                        expAdj -= 4;
                    }

                    p++;
                    hasDigits = true;
                }
            }

            if (!hasDigits)
            {
                p = start + 1;
                if (endptr != null)
                {
                    *endptr = p;
                }

                return 0.0;
            }

            if (*p == 'p' || *p == 'P')
            {
                byte* pStart = p;
                p++;
                bool expNeg = false;
                if (*p == '+')
                {
                    p++;
                }
                else if (*p == '-')
                {
                    expNeg = true;
                    p++;
                }

                bool hasExpDigits = false;
                while (char.IsAsciiDigit((char)*p))
                {
                    exp = exp * 10 + (*p - '0');
                    if (exp > 100000)
                    {
                        exp = 100000; // Cap to prevent integer overflow
                    }

                    p++;
                    hasExpDigits = true;
                }

                if (!hasExpDigits)
                {
                    p = pStart; // Rollback to before 'p'
                }
                else if (expNeg)
                {
                    exp = -exp;
                }
            }

            exp += expAdj;
            if (endptr != null)
            {
                *endptr = p;
            }

            if (sticky)
            {
                mantissa |= 1;
            }

            val = mantissa;
            exp = Math.Clamp(exp, -2000, 2000);
            if (exp < -1000)
            {
                val *= Math.Pow(2.0, -1000);
                exp += 1000;
            }
            else if (exp > 1000)
            {
                val *= Math.Pow(2.0, 1000);
                exp -= 1000;
            }

            val *= Math.Pow(2.0, exp);
            return isNegative ? -val : val;
        }

        while (char.IsAsciiDigit((char)*p))
        {
            val = val * 10.0 + (*p - '0');
            p++;
            hasDigits = true;
        }

        if (*p == '.')
        {
            p++;
            while (char.IsAsciiDigit((char)*p))
            {
                val = val * 10.0 + (*p - '0');
                expAdj--;
                p++;
                hasDigits = true;
            }
        }

        // If no digits parsed, return 0 and set endptr to original string
        if (!hasDigits)
        {
            if (endptr != null)
            {
                *endptr = str;
            }

            return 0.0;
        }

        if (*p == 'e' || *p == 'E')
        {
            byte* eStart = p;
            p++;
            bool expNeg = false;
            if (*p == '+')
            {
                p++;
            }
            else if (*p == '-')
            {
                expNeg = true;
                p++;
            }

            bool hasExpDigits = false;
            while (char.IsAsciiDigit((char)*p))
            {
                exp = exp * 10 + (*p - '0');
                p++;
                hasExpDigits = true;
            }

            if (!hasExpDigits)
            {
                p = eStart; // Rollback to before 'e'
            }
            else if (expNeg)
            {
                exp = -exp;
            }
        }

        exp += expAdj;
        if (endptr != null)
        {
            *endptr = p;
        }

        val *= Math.Pow(10.0, exp);
        return isNegative ? -val : val;

        static bool MatchString(byte* p, string target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                if (char.ToLowerInvariant((char)p[i]) != target[i])
                {
                    return false;
                }
            }

            return true;
        }

        static int HexValue(byte c)
        {
            return (char)c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => 0,
            };
        }
    }

    private static void memcpy(void* dest, void* src, long n)
    {
        Debug.Assert(n <= uint.MaxValue);
        Unsafe.CopyBlock(dest, src, (uint)n);
    }

    private static int memcmp(void* ptr1, void* ptr2, long count)
    {
        ReadOnlySpan<byte> view1 = new(ptr1, checked((int)count));
        ReadOnlySpan<byte> view2 = new(ptr2, checked((int)count));

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
    
    /*
    ** lua_numbertointeger converts a float number with an integral value
    ** to an integer, or returns 0 if the float is not within the range of
    ** a long.  (The range comparisons are tricky because of
    ** rounding. The tests here assume a two-complement representation,
    ** where MININTEGER always has an exact representation as a float;
    ** MAXINTEGER may not have one, and therefore its conversion to float
    ** may have an ill-defined value.)
    */
    private static bool lua_numbertointeger(double n, out long p)
    {
        if (n < long.MinValue)
        {
            p = 0;
            return false;
        }
        
        if (n >= -(double)long.MinValue)
        {
            p = 0;
            return false;
        }

        p = (long)n;
        return true;
    }
}
