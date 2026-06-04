namespace DigitalSingularity.Lua;

public static partial class Lua
{
    /*
    ** $Id: lopcodes.c $
    ** Opcodes for Lua virtual machine
    ** See Copyright Notice in lua.h
    */

    private static byte opmode(bool mm, bool ot, bool it, bool t, bool a, OpMode m)
    {
        return (byte)((mm ? 1 : 0) << 7 |
                      (ot ? 1 : 0) << 6 |
                      (it ? 1 : 0) << 5 |
                      (t ? 1 : 0) << 4 |
                      (a ? 1 : 0) << 3 |
                      (int)m);
    }

    /*
    ** Check whether instruction sets top for next instruction, that is,
    ** it results in multiple values.
    */
    private static partial bool luaP_isOT(uint i)
    {
        OpCode op = GET_OPCODE(i);

        return op switch
        {
            OpCode.OP_TAILCALL => true,
            _ => testOTMode(op) && GETARG_C(i) == 0,
        };
    }

    /*
    ** Check whether instruction uses top from previous instruction, that is,
    ** it accepts multiple results.
    */
    private static partial bool luaP_isIT(uint i)
    {
        OpCode op = GET_OPCODE(i);
        return op switch
        {
            OpCode.OP_SETLIST => testITMode(GET_OPCODE(i)) && GETARG_vB(i) == 0,
            _ => testITMode(GET_OPCODE(i)) && GETARG_B(i) == 0,
        };
    }
}
