namespace DigitalSingularity.Lua;

using System.Diagnostics;

public static unsafe partial class Lua
{
    static Lua()
    {
        Debug.Assert(LUA_EXTRASPACE <= sizeof(L_EXTRA));

        l_memcontrol->countlimit = -1;

        dummynode->u.tt_ = LUA_VEMPTY;
        dummynode->u.key_tt = LUA_TDEADKEY;
        
        absentkey->tt_ = LUA_VABSTKEY;
    }
}
