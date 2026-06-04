namespace DigitalSingularity.Lua;

public static unsafe partial class Lua
{
    private static Node* gnode(Table* t, int i)
    {
        return &t->node[i];
    }

    private static Node* gnode(Table* t, uint i)
    {
        return &t->node[i];
    }

    private static TValue* gval(Node* n)
    {
        return &n->i_val;
    }

    private static ref int gnext(Node* n)
    {
        return ref n->u.next;
    }

    /*
     ** Clear all bits of fast-access metamethods, which means that the table
     ** may have any of these metamethods. (First access that fails after the
     ** clearing will set the bit again.)
     */
    private static void invalidateTMcache(Table* t)
    {
        t->flags &= unchecked((byte)~maskflags);
    }

    /*
     ** Bit BITDUMMY set in 'flags' means the table is using the dummy node
     ** for its hash part.
     */

    private const byte BITDUMMY = 1 << 6;
    private const byte NOTBITDUMMY = unchecked((byte)~BITDUMMY);

    private static bool isdummy(Table* t)
    {
        return (t->flags & BITDUMMY) != 0;
    }

    private static void setnodummy(Table* t)
    {
        t->flags &= NOTBITDUMMY;
    }

    private static void setdummy(Table* t)
    {
        t->flags |= BITDUMMY;
    }

    /* allocated size for hash nodes */
    private static uint allocsizenode(Table* t)
    {
        return isdummy(t) ? 0 : sizenode(t);
    }

    /* returns the Node, given the value of a table entry */
    private static Node* nodefromval(TValue* v)
    {
        return (Node*)v;
    }

    private static void luaH_fastgeti(Table* t, long k, TValue* res, out byte tag)
    {
        ulong u = (ulong)k - 1u;
        if (u < t->asize)
        {
            tag = *getArrTag(t, u);
            if (!tagisempty(tag))
            {
                farr2val(t, u, tag, res);
            }
        }
        else
        {
            tag = luaH_getint(t, k, res);
        }
    }

    private static void luaH_fastseti(Table* t, long k, TValue* val, out int hres)
    {
        ulong u = (ulong)k - 1u;
        if (u < t->asize)
        {
            byte* tag = getArrTag(t, u);
            if (checknoTM(t->metatable, TMS.NEWINDEX) || !tagisempty(*tag))
            {
                fval2arr(t, u, tag, val);
                hres = HOK;
            }
            else
            {
                hres = ~(int)u;
            }
        }
        else
        {
            hres = luaH_psetint(t, k, val); 
        }
    }

    /* results from pset */
    private const int HOK = 0;
    private const int HNOTFOUND = 1;
    private const int HNOTATABLE = 2;
    private const int HFIRSTNODE = 3;

    /*
    ** 'luaH_get*' operations set 'res', unless the value is absent, and
    ** return the tag of the result.
    ** The 'luaH_pset*' (pre-set) operations set the given value and return
    ** HOK, unless the original value is absent. In that case, if the key
    ** is really absent, they return HNOTFOUND. Otherwise, if there is a
    ** slot with that key but with no value, 'luaH_pset*' return an encoding
    ** of where the key is (usually called 'hres'). (pset cannot set that
    ** value because there might be a metamethod.) If the slot is in the
    ** hash part, the encoding is (HFIRSTNODE + hash index); if the slot is
    ** in the array part, the encoding is (~array index), a negative value.
    ** The value HNOTATABLE is used by the fast macros to signal that the
    ** value being indexed is not a table.
    ** (The size for the array part is limited by the maximum power of two
    ** that fits in an unsigned integer; that is INT_MAX+1. So, the C-index
    ** ranges from 0, which encodes to -1, to INT_MAX, which encodes to
    ** INT_MIN. The size of the hash part is limited by the maximum power of
    ** two that fits in a signed integer; that is (INT_MAX+1)/2. So, it is
    ** safe to add HFIRSTNODE to any index there.)
    */

    /*
    ** The array part of a table is represented by an inverted array of
    ** values followed by an array of tags, to avoid wasting space with
    ** padding. In between them there is an unsigned int, explained later.
    ** The 'array' pointer points between the two arrays, so that values are
    ** indexed with negative indices and tags with non-negative indices.

                 Values                              Tags
      --------------------------------------------------------
      ...  |   Value 1     |   Value 0     |unsigned|0|1|...
      --------------------------------------------------------
                                           ^ t->array

    ** All accesses to 't->array' should be through the macros 'getArrTag'
    ** and 'getArrVal'.
    */

    /* Computes the address of the tag for the abstract C-index 'k' */
    private static byte* getArrTag(Table* t, ulong k)
    {
        return (byte*)t->array + sizeof(uint) + k;
    }

    /* Computes the address of the value for the abstract C-index 'k' */
    private static Value* getArrVal(Table* t, ulong k)
    {
        return t->array - 1 - k;
    }

    /*
     ** The unsigned between the two arrays is used as a hint for #t;
     ** see luaH_getn. It is stored there to avoid wasting space in
     ** the structure Table for tables with no array part.
     */
    private static uint* lenhint(Table* t)
    {
        return (uint*)t->array;
    }

    /*
    ** Move TValues to/from arrays, using C indices
    */
    private static void arr2obj(Table* h, ulong k, TValue* val)
    {
        val->tt_ = *getArrTag(h, k);
        val->value_ = *getArrVal(h, k);
    }

    private static void obj2arr(Table* h, uint k, TValue* val)
    {
        *getArrTag(h, k) = val->tt_;
        *getArrVal(h, k) = val->value_;
    }

    /*
     ** Often, we need to check the tag of a value before moving it. The
     ** following macros also move TValues to/from arrays, but receive the
     ** precomputed tag value or address as an extra argument.
     */
    private static void farr2val(Table* h, ulong k, byte tag, TValue* res)
    {
        res->tt_ = tag;
        res->value_ = *getArrVal(h, k);
    }

    private static void fval2arr(Table* h, ulong k, byte* tag, TValue* val)
    {
        *tag = val->tt_;
        *getArrVal(h, k) = val->value_;
    }

    private static partial byte luaH_get(Table* t, TValue* key, TValue* res);

    private static partial byte luaH_getshortstr(Table* t, TString* key, TValue* res);

    private static partial byte luaH_getstr(Table* t, TString* key, TValue* res);

    private static partial byte luaH_getint(Table* t, long key, TValue* res);

    /* Special get for metamethods */
    private static partial TValue* luaH_Hgetshortstr(Table* t, TString* key);

    private static partial int luaH_psetint(Table* t, long key, TValue* val);

    private static partial int luaH_psetshortstr(Table* t, TString* key, TValue* val);

    private static partial int luaH_psetstr(Table* t, TString* key, TValue* val);

    private static partial int luaH_pset(Table* t, TValue* key, TValue* val);

    private static partial void luaH_setint(lua_State* L, Table* t, long key, TValue* value);

    private static partial void luaH_set(lua_State* L, Table* t, TValue* key, TValue* value);

    private static partial void luaH_finishset(lua_State* L, Table* t, TValue* key, TValue* value, int hres);

    private static partial Table* luaH_new(lua_State* L);

    private static partial void luaH_resize(lua_State* L, Table* t, uint nasize, uint nhsize);

    private static partial void luaH_resizearray(lua_State* L, Table* t, uint nasize);

    private static partial long luaH_size(Table* t);

    private static partial void luaH_free(lua_State* L, Table* t);

    private static partial int luaH_next(lua_State* L, Table* t, StkId key);

    private static partial ulong luaH_getn(lua_State* L, Table* t);

#if LUA_DEBUG
    private static partial Node* luaH_mainposition(Table* t, TValue* key);
#endif
}
