namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    static Lua()
    {
#if LUA_TEST
        System.Diagnostics.Debug.Assert(LUA_EXTRASPACE <= sizeof(L_EXTRA));

        l_memcontrol->countlimit = -1;
#endif

        dummynode->u.tt_ = LUA_VEMPTY;
        dummynode->u.key_tt = LUA_TDEADKEY;

        absentkey->tt_ = LUA_VABSTKEY;

        *invalidinstruction = ~0u;
    }
}
