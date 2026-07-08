namespace DigitalSingularity.Lua.Test;

using System.Text;

public class LuaHelperTests
{
    [TestCase("12.345", 12.345)]
    [TestCase("12.345e19", 12.345e19)]
    [TestCase("-.1e+9", -.1e+9)]
    [TestCase(".125", .125)]
    [TestCase("1e20", 1e20)]
    [TestCase("0e-19", 0)]
    [TestCase("4\00012", 4.0)]
    [TestCase("5.9e-76", 5.9e-76)]
    [TestCase("0x1.4p+3", 10.0)]
    [TestCase("0xAp0", 10.0)]
    [TestCase("0x0Ap0", 10.0)]
    [TestCase("0x0A", 10.0)]
    [TestCase("0xA0", 160.0)]
    [TestCase("0x0.A0p8", 160.0)]
    [TestCase("0x0.50p9", 160.0)]
    [TestCase("0x0.28p10", 160.0)]
    [TestCase("0x0.14p11", 160.0)]
    [TestCase("0x0.0A0p12", 160.0)]
    [TestCase("0x0.050p13", 160.0)]
    [TestCase("0x0.028p14", 160.0)]
    [TestCase("0x0.014p15", 160.0)]
    [TestCase("0x00.00A0p16", 160.0)]
    [TestCase("0x00.0050p17", 160.0)]
    [TestCase("0x00.0028p18", 160.0)]
    [TestCase("0x00.0014p19", 160.0)]
    [TestCase("0x1p-1023", 1.11253692925360069154511635866620203210960799023116591527666e-308)]
    [TestCase("0x0.8p-1022", 1.11253692925360069154511635866620203210960799023116591527666e-308)]
    [TestCase("Inf", double.PositiveInfinity)]
    [TestCase("-Inf", double.NegativeInfinity)]
    [TestCase("+InFiNiTy", double.PositiveInfinity)]
    [TestCase("0x80000Ap-23", 1.0000011920928955)]
    [TestCase("1e-324", 0)]
    [TestCase("0x100000000000008p0", 72057594037927936.000000)]
    [TestCase("0x100000000000008.p0", 72057594037927936.000000)]
    [TestCase("0x100000000000008.00p0", 72057594037927936.000000)]
    [TestCase("0x10000000000000800p0", 18446744073709551616.000000)]
    [TestCase("0x10000000000000801p0", 18446744073709555712.000000)]
    [TestCase("0x10000000000000801p0", 18446744073709555712.000000)]
    [TestCase("2.2250738585072014e-308", 2.2250738585072014e-308)]
    public unsafe void TestStrtod(string source, double expected)
    {
        byte[] data = Encoding.UTF8.GetBytes(source);
        fixed (byte* ptr = data)
        {
            byte* endPtr = ptr;
            double result = Lua.strtod(ptr, &endPtr);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(*endPtr, Is.EqualTo(0));
            }
        }
    }
}
