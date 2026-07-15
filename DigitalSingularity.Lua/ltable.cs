namespace DigitalSingularity.Lua;

using System.Diagnostics;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Clear all bits of fast-access metamethods, which means that the table
    /// may have any of these metamethods. (First access that fails after the
    /// clearing will set the bit again.)
    /// </summary>
    internal static void invalidateTMcache(Table* t)
    {
        t->flags &= unchecked((byte)~maskflags);
    }

    // Bit BITDUMMY set in 'flags' means the table is using the dummy node
    // for its hash part.

    private const byte BITDUMMY = 1 << 6;
    private const byte NOTBITDUMMY = unchecked((byte)~BITDUMMY);

    internal static bool isdummy(Table* t)
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

    /// <summary>
    /// allocated size for hash nodes
    /// </summary>
    private static uint allocsizenode(Table* t)
    {
        return isdummy(t) ? 0 : sizenode(t);
    }

    /// <summary>
    /// returns the Node, given the value of a table entry
    /// </summary>
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

    /// <summary>
    /// results from pset
    /// </summary>
    internal const int HOK = 0;
    internal const int HNOTFOUND = 1;
    internal const int HNOTATABLE = 2;
    private const int HFIRSTNODE = 3;

    // 'luaH_get*' operations set 'res', unless the value is absent, and
    // return the tag of the result.
    // The 'luaH_pset*' (pre-set) operations set the given value and return
    // HOK, unless the original value is absent. In that case, if the key
    // is really absent, they return HNOTFOUND. Otherwise, if there is a
    // slot with that key but with no value, 'luaH_pset*' return an encoding
    // of where the key is (usually called 'hres'). (pset cannot set that
    // value because there might be a metamethod.) If the slot is in the
    // hash part, the encoding is (HFIRSTNODE + hash index); if the slot is
    // in the array part, the encoding is (~array index), a negative value.
    // The value HNOTATABLE is used by the fast macros to signal that the
    // value being indexed is not a table.
    // (The size for the array part is limited by the maximum power of two
    // that fits in an unsigned integer; that is INT_MAX+1. So, the C-index
    // ranges from 0, which encodes to -1, to INT_MAX, which encodes to
    // INT_MIN. The size of the hash part is limited by the maximum power of
    // two that fits in a signed integer; that is (INT_MAX+1)/2. So, it is
    // safe to add HFIRSTNODE to any index there.)

    // The array part of a table is represented by an inverted array of
    // values followed by an array of tags, to avoid wasting space with
    // padding. In between them there is an unsigned int, explained later.
    // The 'array' pointer points between the two arrays, so that values are
    // indexed with negative indices and tags with non-negative indices.
    //
    // Values                              Tags
    // --------------------------------------------------------
    // ...  |   Value 1     |   Value 0     |unsigned|0|1|...
    // --------------------------------------------------------
    // ^ t->array
    //
    // All accesses to 't->array' should be through the macros 'getArrTag'
    // and 'getArrVal'.

    /// <summary>
    /// Computes the address of the tag for the abstract C-index 'k'
    /// </summary>
    private static byte* getArrTag(Table* t, ulong k)
    {
        return (byte*)t->array + sizeof(uint) + k;
    }

    /// <summary>
    /// Computes the address of the value for the abstract C-index 'k'
    /// </summary>
    private static Value* getArrVal(Table* t, ulong k)
    {
        return t->array - 1 - k;
    }

    /// <summary>
    /// The unsigned between the two arrays is used as a hint for #t;
    /// see luaH_getn. It is stored there to avoid wasting space in
    /// the structure Table for tables with no array part.
    /// </summary>
    private static uint* lenhint(Table* t)
    {
        return (uint*)t->array;
    }

    /// <summary>
    /// Move TValues to/from arrays, using C indices
    /// </summary>
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

    /// <summary>
    /// Often, we need to check the tag of a value before moving it. The
    /// following macros also move TValues to/from arrays, but receive the
    /// precomputed tag value or address as an extra argument.
    /// </summary>
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
    
    // Implementation of tables (aka arrays, objects, or hash tables).
    // Tables keep its elements in two parts: an array part and a hash part.
    // Non-negative integer keys are all candidates to be kept in the array
    // part. The actual size of the array is the largest 'n' such that
    // more than half the slots between 1 and n are in use.
    // Hash uses a mix of chained scatter table with Brent's variation.
    // A main invariant of these tables is that, if an element is not
    // in its main position (i.e. the 'original' position that its hash gives
    // to it), then the colliding element is in its own main position.
    // Hence even when the load factor reaches 100%, performance remains good.

    /// <summary>
    /// Only hash parts with at least 2^LIMFORLAST have a 'lastfree' field
    /// that optimises finding a free slot. That field is stored just before
    /// the array of nodes, in the same block. Smaller tables do a complete
    /// search when looking for a free slot.
    /// </summary>
    private const int LIMFORLAST = 3; // log2 of real limit (8)

//
// The union 'Limbox' stores 'lastfree' and ensures that what follows it
// is properly aligned to store a Node.
//
// typedef struct { Node *dummy; Node follows_pNode; } Limbox_aux;

    [StructLayout(LayoutKind.Explicit)]
    private struct Limbox
    {
        [FieldOffset(0)] public Node* lastfree;
// char padding[offsetof(Limbox_aux, follows_pNode)];
    }

    private static bool haslastfree(Table* t)
    {
        return t->lsizenode >= LIMFORLAST;
    }

    private static ref Node* getlastfree(Table* t)
    {
        return ref ((Limbox*)t->node - 1)->lastfree;
    }

    /// <summary>
    /// MAXABITS is the largest integer such that 2^MAXABITS fits in an
    /// unsigned int.
    /// </summary>
    private const int MAXABITS = sizeof(int) * 8 - 1;

    /// <summary>
    /// MAXASIZEB is the maximum number of elements in the array part such
    /// that the size of the array fits in 'size_t'.
    /// </summary>
    private static readonly long MAXASIZEB = long.MaxValue / (sizeof(Value) + 1);

    /// <summary>
    /// MAXASIZE is the maximum size of the array part. It is the minimum
    /// between 2^MAXABITS and MAXASIZEB.
    /// </summary>
    private static readonly uint MAXASIZE = (uint)Math.Min(1u << MAXABITS, MAXASIZEB);

    /// <summary>
    /// MAXHBITS is the largest integer such that 2^MAXHBITS fits in a
    /// signed int.
    /// </summary>
    private const int MAXHBITS = MAXABITS - 1;

    /// <summary>
    /// MAXHSIZE is the maximum size of the hash part. It is the minimum
    /// between 2^MAXHBITS and the maximum size such that, measured in bytes,
    /// it fits in a 'size_t'.
    /// </summary>
    private static readonly long MAXHSIZE = luaM_limitN<Node>(1 << MAXHBITS);

    /// <summary>
    /// When the original hash value is good, hashing by a power of 2
    /// avoids the cost of '%'.
    /// </summary>
    private static Node* hashpow2(Table* t, uint n)
    {
        return gnode(t, lmod(n, sizenode(t)));
    }

    /// <summary>
    /// for other types, it is better to avoid modulo by power of 2, as
    /// they can have many 2 factors.
    /// </summary>
    private static Node* hashmod(Table* t, ulong n)
    {
        return gnode(t, (uint)(n % (sizenode(t) - 1u | 1u)));
    }

    private static Node* hashstr(Table* t, TString* str)
    {
        return hashpow2(t, str->hash);
    }

    private static Node* hashboolean(Table* t, bool p)
    {
        return hashpow2(t, p ? 1u : 0);
    }

    private static Node* hashpointer(Table* t, void* p)
    {
        return hashmod(t, (uint)p);
    }

    /// <summary>
    /// Common hash part for tables with empty hash parts. That allows all
    /// tables to have a hash part, avoiding an extra check ("is there a hash
    /// part?") when indexing. Its sole node has an empty value and a key
    /// (DEADKEY, null) that is different from any valid TValue.
    /// </summary>
    private static readonly Node* dummynode = (Node*)NativeMemory.AllocZeroed((nuint)sizeof(Node));
    
    private static readonly TValue* absentkey = (TValue*)NativeMemory.AllocZeroed((nuint)sizeof(TValue));
        
    /// <summary>
    /// Hash for integers. To allow a good hash, use the remainder operator
    /// ('%'). If integer fits as a non-negative int, compute an int
    /// remainder, which is faster. Otherwise, use an unsigned-integer
    /// remainder, which uses all bits and ensures a non-negative result.
    /// </summary>
    private static Node* hashint(Table* t, long i)
    {
        ulong ui = (ulong)i;
        if (ui <= int.MaxValue)
        {
            return gnode(t, (int)ui % (int)(sizenode(t) - 1 | 1));
        }

        return hashmod(t, ui);
    }

    /// <summary>
    /// Hash for floating-point numbers.
    /// The main computation should be just
    ///     n = frexp(n, &amp;i); return (n * INT_MAX) + i
    /// but there are some numerical subtleties.
    /// In a two-complement representation, INT_MAX may not have an exact
    /// representation as a float, but INT_MIN does; because the absolute
    /// value of 'frexp' is smaller than 1 (unless 'n' is inf/NaN), the
    /// absolute value of the product 'frexp * -INT_MIN' is smaller or equal
    /// to INT_MAX. Next, the use of 'unsigned int' avoids overflows when
    /// adding 'i'; the use of '~u' (instead of '-u') avoids problems with
    /// INT_MIN.
    /// </summary>
    private static uint l_hashfloat(double n)
    {
        return (uint)n.GetHashCode();
        // TODO:?
// int i;
// long ni;
// n = (frexp)(n, &i) * -cast_num(INT_MIN);
// if (!lua_numbertointeger(n, &ni)) { // is 'n' inf/-inf/NaN?
// Debug.Assert(luai_numisnan(n) || (fabs)(n) == cast_num(HUGE_VAL));
// return 0;
// }
// else { // normal case
// unsigned int u = cast_uint(i) + cast_uint(ni);
// return (u <= cast_uint(INT_MAX) ? u : ~u);
// }
        throw new NotImplementedException();
    }

    /// <summary>
    /// returns the 'main' position of an element in a table (that is,
    /// the index of its hash value).
    /// </summary>
    private static Node* mainpositionTV(Table* t, TValue* key)
    {
        return ttypetag(key) switch
        {
            LUA_VNUMINT => hashint(t, ivalue(key)),
            LUA_VNUMFLT => hashmod(t, l_hashfloat(fltvalue(key))),
            LUA_VSHRSTR => hashstr(t, tsvalue(key)),
            LUA_VLNGSTR => hashpow2(t, luaS_hashlongstr(tsvalue(key))),
            LUA_VFALSE => hashboolean(t, false),
            LUA_VTRUE => hashboolean(t, true),
            LUA_VLIGHTUSERDATA => hashpointer(t, pvalue(key)),
            LUA_VLCF => hashpointer(t, fvalue(key)),
            _ => hashpointer(t, gcvalue(key)),
        };
    }

    private static Node* mainpositionfromnode(Table* t, Node* nd)
    {
        TValue key;
        getnodekey(null, &key, nd);
        return mainpositionTV(t, &key);
    }

    /// <summary>
    /// Check whether key 'k1' is equal to the key in node 'n2'. This
    /// equality is raw, so there are no metamethods. Floats with integer
    /// values have been normalized, so integers cannot be equal to
    /// floats. It is assumed that 'eqshrstr' is simply pointer equality,
    /// so that short strings are handled in the default case.  The flag
    /// 'deadok' means to accept dead keys as equal to their original values.
    /// (Only collectable objects can produce dead keys.) Note that dead
    /// long strings are also compared by identity.  Once a key is dead,
    /// its corresponding value may be collected, and then another value
    /// can be created with the same address. If this other value is given
    /// to 'next', 'equalkey' will signal a false positive. In a regular
    /// traversal, this situation should never happen, as all keys given to
    /// 'next' came from the table itself, and therefore could not have been
    /// collected. Outside a regular traversal, we have garbage in, garbage
    /// out. What is relevant is that this false positive does not break
    /// anything.  (In particular, 'next' will return some other valid item
    /// on the table or nil.)
    /// </summary>
    private static bool equalkey(TValue* k1, Node* n2, bool deadok)
    {
        if (rawtt(k1) != keytt(n2))
        {
            // not the same variants?
            if (keyisshrstr(n2) && ttislngstring(k1))
            {
                // an external string can be equal to a short-string key
                return luaS_eqstr(tsvalue(k1), keystrval(n2));
            }

            if (deadok && keyisdead(n2) && iscollectable(k1))
            {
                // a collectable value can be equal to a dead key
                return gcvalue(k1) == gcvalueraw(keyval(n2));
            }

            return false; // otherwise, different variants cannot be equal
        }

        // equal variants
        return keytt(n2) switch
        {
            LUA_VNIL or LUA_VFALSE or LUA_VTRUE => true,
            LUA_VNUMINT => ivalue(k1) == keyival(n2),
            LUA_VNUMFLT => fltvalue(k1) == fltvalueraw(keyval(n2)),
            LUA_VLIGHTUSERDATA => pvalue(k1) == pvalueraw(keyval(n2)),
            LUA_VLCF => fvalue(k1) == fvalueraw(keyval(n2)),
            LUA_VLNGSTR_C => luaS_eqstr(tsvalue(k1), keystrval(n2)),
            _ => gcvalue(k1) == gcvalueraw(keyval(n2)),
        };
    }

    /// <summary>
    /// "Generic" get version. (Not that generic: not valid for integers,
    /// which may be in array part, nor for floats with integral values.)
    /// See explanation about 'deadok' in function 'equalkey'.
    /// </summary>
    private static TValue* getgeneric(Table* t, TValue* key, bool deadok)
    {
        Node* n = mainpositionTV(t, key);
        while (true)
        {
            // check whether 'key' is somewhere in the chain
            if (equalkey(key, n, deadok))
            {
                return gval(n); // that's it
            }

            int nx = gnext(n);
            if (nx == 0)
            {
                return absentkey; // not found
            }

            n += nx;
        }
    }

    /// <summary>
    /// Return the index 'k' (converted to an unsigned) if it is inside
    /// the range [1, limit].
    /// </summary>
    private static uint checkrange(long k, uint limit)
    {
        return (ulong)k - 1u < limit ? (uint)k : 0;
    }

    /// <summary>
    /// Return the index 'k' if 'k' is an appropriate key to live in the
    /// array part of a table, 0 otherwise.
    /// </summary>
    private static uint arrayindex(long k)
    {
        return checkrange(k, MAXASIZE);
    }

    /// <summary>
    /// Check whether an integer key is in the array part of a table and
    /// return its index there, or zero.
    /// </summary>
    private static uint ikeyinarray(Table* t, long k)
    {
        return checkrange(k, t->asize);
    }

    /// <summary>
    /// Check whether a key is in the array part of a table and return its
    /// index there, or zero.
    /// </summary>
    private static uint keyinarray(Table* t, TValue* key)
    {
        return ttisinteger(key) ? ikeyinarray(t, ivalue(key)) : 0;
    }

    /// <summary>
    /// returns the index of a 'key' for table traversals. First goes all
    /// elements in the array part, then elements in the hash part. The
    /// beginning of a traversal is signalled by 0.
    /// </summary>
    private static uint findindex(
        lua_State* L,
        Table* t,
        TValue* key,
        uint asize)
    {
        if (ttisnil(key))
        {
            return 0; // first iteration
        }

        uint i = keyinarray(t, key);
        if (i != 0) // is 'key' inside array part?
        {
            return i; // yes; that's the index
        }

        TValue* n = getgeneric(t, key, true);
        if (isabstkey(n))
        {
            luaG_runerror(L, "invalid key to 'next'"); // key not found
        }

        i = (uint)(nodefromval(n) - gnode(t, 0)); // key index in hash table
        // hash elements are numbered after array ones
        return i + 1 + asize;
    }

    internal static bool luaH_next(lua_State* L, Table* t, StkId key)
    {
        uint asize = t->asize;
        uint i = findindex(L, t, s2v(key), asize); // find original key
        for (; i < asize; i++)
        {
            // try first array part
            byte tag = *getArrTag(t, i);
            if (!tagisempty(tag))
            {
                // a non-empty entry?
                setivalue(s2v(key), (int)i + 1);
                farr2val(t, i, tag, s2v(key + 1));
                return true;
            }
        }

        for (i -= asize; i < sizenode(t); i++)
        {
            // hash part
            if (!isempty(gval(gnode(t, i))))
            {
                // a non-empty entry?
                Node* n = gnode(t, i);
                getnodekey(L, s2v(key), n);
                setobj2s(L, key + 1, gval(n));
                return true;
            }
        }

        return false; // no more elements
    }

    /// <summary>
    /// Extra space in Node array if it has a lastfree entry
    /// </summary>
    private static int extraLastfree(Table* t)
    {
        return haslastfree(t) ? sizeof(Limbox) : 0;
    }

    /// <summary>
    /// 'node' size in bytes
    /// </summary>
    private static long sizehash(Table* t)
    {
        return sizenode(t) * sizeof(Node) + extraLastfree(t);
    }

    private static void freehash(lua_State* L, Table* t)
    {
        if (!isdummy(t))
        {
            // get pointer to the beginning of Node array
            byte* arr = (byte*)t->node - extraLastfree(t);
            luaM_freearray(L, arr, sizehash(t));
        }
    }

    // {=============================================================
    // Rehash
    // ==============================================================

    /// <summary>
    /// Structure to count the keys in a table.
    /// 'total' is the total number of keys in the table.
    /// 'na' is the number of *array indices* in the table (see 'arrayindex').
    /// 'deleted' is true if there are deleted nodes in the hash part.
    /// 'nums' is a "count array" where 'nums[i]' is the number of integer
    /// keys between 2^(i - 1) + 1 and 2^i. Note that 'na' is the summation
    /// of 'nums'.
    /// </summary>
    private struct Counters
    {
        public uint total;
        public uint na;
        public bool deleted;
        public fixed uint nums[MAXABITS + 1];
    }

    /// <summary>
    /// Check whether it is worth to use 'na' array entries instead of 'nh'
    /// hash nodes. (A hash node uses ~3 times more memory than an array
    /// entry: Two values plus 'next' versus one value.) Evaluate with size_t
    /// to avoid overflows.
    /// </summary>
    private static bool arrayXhash(uint na, uint nh)
    {
        return na <= (long)nh * 3;
    }

    /// <summary>
    /// Compute the optimal size for the array part of table 't'.
    /// This size maximises the number of elements going to the array part
    /// while satisfying the condition 'arrayXhash' with the use of memory if
    /// all those elements went to the hash part.
    /// 'ct-&gt;na' enters with the total number of array indices in the table
    /// and leaves with the number of keys that will go to the array part;
    /// return the optimal size for the array part.
    /// </summary>
    private static uint computesizes(Counters* ct)
    {
        int i;
        uint twotoi; // 2^i (candidate for optimal size)
        uint a = 0; // number of elements smaller than 2^i
        uint na = 0; // number of elements to go to array part
        uint optimal = 0; // optimal size for array part
        // traverse slices while 'twotoi' does not overflow and total of array
        // indices still can satisfy 'arrayXhash' against the array size
        for (i = 0, twotoi = 1;
             twotoi > 0 && arrayXhash(twotoi, ct->na);
             i++, twotoi *= 2)
        {
            uint nums = ct->nums[i];
            a += nums;
            if (nums > 0 && // grows array only if it gets more elements...
                arrayXhash(twotoi, a))
            {
                // ...while using "less memory"
                optimal = twotoi; // optimal size (till now)
                na = a; // all elements up to 'optimal' will go to array part
            }
        }

        ct->na = na;
        return optimal;
    }

    private static void countint(long key, Counters* ct)
    {
        uint k = arrayindex(key);
        if (k != 0)
        {
            // is 'key' an array index?
            ct->nums[luaO_ceillog2(k)]++; // count as such
            ct->na++;
        }
    }

    private static bool arraykeyisempty(Table* t, uint key)
    {
        byte tag = *getArrTag(t, key - 1);
        return tagisempty(tag);
    }

    /// <summary>
    /// Count keys in array part of table 't'.
    /// </summary>
    private static void numusearray(Table* t, Counters* ct)
    {
        int lg;
        uint ttlg; // 2^lg
        uint ause = 0; // summation of 'nums'
        uint i = 1; // index to traverse all array keys
        uint asize = t->asize;
        // traverse each slice
        for (lg = 0, ttlg = 1; lg <= MAXABITS; lg++, ttlg *= 2)
        {
            uint lim = ttlg;
            if (lim > asize)
            {
                lim = asize; // adjust upper limit
                if (i > lim)
                {
                    break; // no more elements to count
                }
            }

            uint lc = 0; // counter
            // count elements in range (2^(lg - 1), 2^lg]
            for (; i <= lim; i++)
            {
                if (!arraykeyisempty(t, i))
                {
                    lc++;
                }
            }

            ct->nums[lg] += lc;
            ause += lc;
        }

        ct->total += ause;
        ct->na += ause;
    }

    /// <summary>
    /// Count keys in hash part of table 't'. As this only happens during
    /// a rehash, all nodes have been used. A node can have a nil value only
    /// if it was deleted after being created.
    /// </summary>
    private static void numusehash(Table* t, Counters* ct)
    {
        uint i = sizenode(t);
        uint total = 0;
        while (i-- > 0)
        {
            Node* n = &t->node[i];
            if (isempty(gval(n)))
            {
                Debug.Assert(!keyisnil(n)); // entry was deleted; key cannot be nil
                ct->deleted = true;
            }
            else
            {
                total++;
                if (keyisinteger(n))
                {
                    countint(keyival(n), ct);
                }
            }
        }

        ct->total += total;
    }

    /// <summary>
    /// Convert an "abstract size" (number of slots in an array) to
    /// "concrete size" (number of bytes in the array).
    /// </summary>
    private static long concretesize(uint size)
    {
        if (size == 0)
        {
            return 0;
        }

        // space for the two arrays plus an unsigned in between
        return size * (sizeof(Value) + 1) + sizeof(uint);
    }

    /// <summary>
    /// Resize the array part of a table. If new size is equal to the old,
    /// do nothing. Else, if new size is zero, free the old array. (It must
    /// be present, as the sizes are different.) Otherwise, allocate a new
    /// array, move the common elements to new proper position, and then
    /// frees the old array.
    /// We could reallocate the array, but we still would need to move the
    /// elements to their new position, so the copy implicit in realloc is a
    /// waste. Moreover, most allocators will move the array anyway when the
    /// new size is double the old one (the most common case).
    /// </summary>
    private static Value* resizearray(lua_State* L, Table* t, uint oldasize, uint newasize)
    {
        if (oldasize == newasize)
        {
            return t->array; // nothing to be done
        }

        if (newasize == 0)
        {
            // erasing array?
            Value* op = t->array - oldasize; // original array's real address
            luaM_freemem(L, op, concretesize(oldasize)); // free it
            return null;
        }

        long newasizeb = concretesize(newasize);
        Value* np = (Value*)luaM_reallocvector<byte>(L, null, 0, newasizeb);
        if (np == null) // allocation error?
        {
            return null;
        }

        np += newasize; // shift pointer to the end of value segment
        if (oldasize > 0)
        {
            // move common elements to new position
            long oldasizeb = concretesize(oldasize);
            Value* op = t->array; // original array
            uint tomove = oldasize < newasize ? oldasize : newasize;
            long tomoveb = oldasize < newasize ? oldasizeb : newasizeb;
            Debug.Assert(tomoveb > 0);
            memcpy(np - tomove, op - tomove, tomoveb);
            luaM_freemem(L, op - oldasize, oldasizeb); // free old block
        }

        return np;
    }

    /// <summary>
    /// Creates an array for the hash part of a table with the given
    /// size, or reuses the dummy node if size is zero.
    /// The computation for size overflow is in two steps: the first
    /// comparison ensures that the shift in the second one does not
    /// overflow.
    /// </summary>
    private static void setnodevector(lua_State* L, Table* t, uint size)
    {
        if (size == 0)
        {
            // no elements to hash part?
            t->node = dummynode; // use common 'dummynode'
            t->lsizenode = 0;
            setdummy(t); // signal that it is using dummy node
        }
        else
        {
            int lsize = luaO_ceillog2(size);
            if (lsize > MAXHBITS || 1 << lsize > MAXHSIZE)
            {
                luaG_runerror(L, "table overflow");
            }

            size = twoto((byte)lsize);
            if (lsize < LIMFORLAST) // no 'lastfree' field?
            {
                t->node = luaM_newvector<Node>(L, checked((int)size));
            }
            else
            {
                long bsize = size * sizeof(Node) + sizeof(Limbox);
                byte* node = luaM_newblock(L, checked((int)bsize));
                t->node = (Node*)(node + sizeof(Limbox));
                getlastfree(t) = gnode(t, size); // all positions are free
            }

            t->lsizenode = (byte)lsize;
            setnodummy(t);
            for (int i = 0; i < size; i++)
            {
                Node* n = gnode(t, i);
                gnext(n) = 0;
                setnilkey(n);
                setempty(gval(n));
            }
        }
    }

    /// <summary>
    /// (Re)insert all elements from the hash part of 'ot' into table 't'.
    /// </summary>
    private static void reinserthash(lua_State* L, Table* ot, Table* t)
    {
        uint size = sizenode(ot);
        for (int j = 0; j < size; j++)
        {
            Node* old = gnode(ot, j);
            if (!isempty(gval(old)))
            {
                // doesn't need barrier/invalidate cache, as entry was
                // already present in the table
                TValue k;
                getnodekey(L, &k, old);
                newcheckedkey(t, &k, gval(old));
            }
        }
    }

    /// <summary>
    /// Exchange the hash part of 't1' and 't2'. (In 'flags', only the
    /// dummy bit must be exchanged: The 'isrealasize' is not related
    /// to the hash part, and the metamethod bits do not change during
    /// a resize, so the "real" table can keep their values.)
    /// </summary>
    private static void exchangehashpart(Table* t1, Table* t2)
    {
        byte lsizenode = t1->lsizenode;
        Node* node = t1->node;
        int bitdummy1 = t1->flags & BITDUMMY;
        t1->lsizenode = t2->lsizenode;
        t1->node = t2->node;
        t1->flags = (byte)(t1->flags & NOTBITDUMMY | t2->flags & BITDUMMY);
        t2->lsizenode = lsizenode;
        t2->node = node;
        t2->flags = (byte)(t2->flags & NOTBITDUMMY | bitdummy1);
    }

    /// <summary>
    /// Re-insert into the new hash part of a table the elements from the
    /// vanishing slice of the array part.
    /// </summary>
    private static void reinsertOldSlice(Table* t, uint oldasize, uint newasize)
    {
        for (uint i = newasize; i < oldasize; i++)
        {
            // traverse vanishing slice
            byte tag = *getArrTag(t, i);
            if (!tagisempty(tag))
            {
                // a non-empty entry?
                TValue key;
                setivalue(&key, (long)i + 1); // make the key
                TValue aux;
                farr2val(t, i, tag, &aux); // copy value into 'aux'
                insertkey(t, &key, &aux); // insert entry into the hash part
            }
        }
    }

    /// <summary>
    /// Clear new slice of the array.
    /// </summary>
    private static void clearNewSlice(Table* t, uint oldasize, uint newasize)
    {
        for (; oldasize < newasize; oldasize++)
        {
            *getArrTag(t, oldasize) = LUA_VEMPTY;
        }
    }

    /// <summary>
    /// Resize table 't' for the new given sizes. Both allocations (for
    /// the hash part and for the array part) can fail, which creates some
    /// subtleties. If the first allocation, for the hash part, fails, an
    /// error is raised and that is it. Otherwise, it copies the elements from
    /// the shrinking part of the array (if it is shrinking) into the new
    /// hash. Then it reallocates the array part.  If that fails, the table
    /// is in its original state; the function frees the new hash part and then
    /// raises the allocation error. Otherwise, it sets the new hash part
    /// into the table, initializes the new part of the array (if any) with
    /// nils and reinserts the elements of the old hash back into the new
    /// parts of the table.
    /// Note that if the new size for the array part ('newasize') is equal to
    /// the old one ('oldasize'), this function will do nothing with that
    /// part.
    /// </summary>
    internal static void luaH_resize(lua_State* L, Table* t, uint nasize, uint nhsize)
    {
        Table newt; // to keep the new hash part
        uint oldasize = t->asize;
        Value* newarray;
        if (nasize > MAXASIZE)
        {
            luaG_runerror(L, "table overflow");
        }

        // create new hash part with appropriate size into 'newt'
        newt.flags = 0;
        setnodevector(L, &newt, nhsize);
        if (nasize < oldasize)
        {
            // will array shrink?
            // re-insert into the new hash the elements from vanishing slice
            exchangehashpart(t, &newt); // pretend table has new hash
            reinsertOldSlice(t, oldasize, nasize);
            exchangehashpart(t, &newt); // restore old hash (in case of errors)
        }

        // allocate new array
        newarray = resizearray(L, t, oldasize, nasize);
        if (newarray == null && nasize > 0)
        {
            // allocation failed?
            freehash(L, &newt); // release new hash part
            luaM_error(L); // raise error (with array unchanged)
        }

        // allocation ok; initialise new part of the array
        exchangehashpart(t, &newt); // 't' has the new hash ('newt' has the old)
        t->array = newarray; // set new array part
        t->asize = nasize;
        if (newarray != null)
        {
            *lenhint(t) = nasize / 2u; // set an initial hint
        }

        clearNewSlice(t, oldasize, nasize);
        // re-insert elements from old hash part into new parts
        reinserthash(L, &newt, t); // 'newt' now has the old hash
        freehash(L, &newt); // free old hash part
    }

    internal static void luaH_resizearray(lua_State* L, Table* t, uint nasize)
    {
        uint nsize = allocsizenode(t);
        luaH_resize(L, t, nasize, nsize);
    }

    /// <summary>
    /// Rehash a table. First, count its keys. If there are array indices
    /// outside the array part, compute the new best size for that part.
    /// Then, resize the table.
    /// </summary>
    private static void rehash(lua_State* L, Table* t, TValue* ek)
    {
        uint asize; // optimal size for array part
        uint nsize; // size for the hash part

        Counters ct;

        // reset counts
        for (int i = 0; i <= MAXABITS; i++)
        {
            ct.nums[i] = 0;
        }

        ct.na = 0;
        ct.deleted = false;
        ct.total = 1; // count extra key
        if (ttisinteger(ek))
        {
            countint(ivalue(ek), &ct); // extra key may go to array
        }

        numusehash(t, &ct); // count keys in hash part
        if (ct.na == 0)
        {
            // no new keys to enter array part; keep it with the same size
            asize = t->asize;
        }
        else
        {
            // compute best size for array part
            numusearray(t, &ct); // count keys in array part
            asize = computesizes(&ct); // compute new size for array part
        }

        // all keys not in the array part go to the hash part
        nsize = ct.total - ct.na;
        if (ct.deleted)
        {
            // table has deleted entries?
            // insertion-deletion-insertion: give hash some extra size to
            // avoid repeated resizings
            nsize += nsize >> 2;
        }

        // resize the table to new computed sizes
        luaH_resize(L, t, asize, nsize);
    }

    internal static Table* luaH_new(lua_State* L)
    {
        GCObject* o = luaC_newobj(L, LUA_VTABLE, sizeof(Table));
        Table* t = gco2t(o);
        t->metatable = null;
        t->flags = maskflags; // table has no metamethod fields
        t->array = null;
        t->asize = 0;
        setnodevector(L, t, 0);
        return t;
    }

    internal static long luaH_size(Table* t)
    {
        long sz = sizeof(Table) + concretesize(t->asize);
        if (!isdummy(t))
        {
            sz += sizehash(t);
        }

        return sz;
    }

    /// <summary>
    /// Frees a table.
    /// </summary>
    private static void luaH_free(lua_State* L, Table* t)
    {
        freehash(L, t);
        resizearray(L, t, t->asize, 0);
        luaM_free(L, t);
    }

    private static Node* getfreepos(Table* t)
    {
        if (haslastfree(t))
        {
            // does it have 'lastfree' information?
            // look for a spot before 'lastfree', updating 'lastfree'
            while (getlastfree(t) > t->node)
            {
                Node* free = --getlastfree(t);
                if (keyisnil(free))
                {
                    return free;
                }
            }
        }
        else
        {
            // no 'lastfree' information
            uint i = sizenode(t);
            while (i-- > 0)
            {
                // do a linear search
                Node* free = gnode(t, i);
                if (keyisnil(free))
                {
                    return free;
                }
            }
        }

        return null; // could not find a free place
    }

    /// <summary>
    /// Inserts a new key into a hash table; first, check whether key's main
    /// position is free. If not, check whether colliding node is in its main
    /// position or not: if it is not, move colliding node to an empty place
    /// and put new key in its main position; otherwise (colliding node is in
    /// its main position), new key goes to an empty position. Return 0 if
    /// could not insert key (could not find a free space).
    /// </summary>
    private static bool insertkey(Table* t, TValue* key, TValue* value)
    {
        Node* mp = mainpositionTV(t, key);
        // table cannot already contain the key
        Debug.Assert(isabstkey(getgeneric(t, key, false)));
        if (!isempty(gval(mp)) || isdummy(t))
        {
            // main position is taken?
            Node* f = getfreepos(t); // get a free place
            if (f == null) // cannot find a free place?
            {
                return false;
            }

            Debug.Assert(!isdummy(t));
            Node* othern = mainpositionfromnode(t, mp);
            if (othern != mp)
            {
                // is colliding node out of its main position?
                // yes; move colliding node into free position
                while (othern + gnext(othern) != mp) // find previous
                {
                    othern += gnext(othern);
                }

                gnext(othern) = (int)(f - othern); // rechain to point to 'f'
                *f = *mp; // copy colliding node into free pos. (mp->next also goes)
                if (gnext(mp) != 0)
                {
                    gnext(f) += (int)(mp - f); // correct 'next'
                    gnext(mp) = 0; // now 'mp' is free
                }

                setempty(gval(mp));
            }
            else
            {
                // colliding node is in its own main position
                // new node will go into free position
                if (gnext(mp) != 0)
                {
                    gnext(f) = (int)(mp + gnext(mp) - f); // chain new position
                }
                else
                {
                    Debug.Assert(gnext(f) == 0);
                }

                gnext(mp) = (int)(f - mp);
                mp = f;
            }
        }

        setnodekey(mp, key);
        Debug.Assert(isempty(gval(mp)));
        setobj2t(null, gval(mp), value);
        return true;
    }

    /// <summary>
    /// Insert a key in a table where there is space for that key, the
    /// key is valid, and the value is not nil.
    /// </summary>
    private static void newcheckedkey(Table* t, TValue* key, TValue* value)
    {
        uint i = keyinarray(t, key);
        if (i > 0) // is key in the array part?
        {
            obj2arr(t, i - 1, value); // set value in the array
        }
        else
        {
            bool done = insertkey(t, key, value); // insert key in the hash part
            Debug.Assert(done); // it cannot fail
        }
    }

    private static void luaH_newkey(lua_State* L, Table* t, TValue* key, TValue* value)
    {
        if (!ttisnil(value))
        {
            // do not insert nil values
            bool done = insertkey(t, key, value);
            if (!done)
            {
                // could not find a free place?
                rehash(L, t, key); // grow table
                newcheckedkey(t, key, value); // insert key in grown table
            }

            luaC_barrierback(L, obj2gco(t), key);
            // for debugging only: any new key may force an emergency collection
            
#if HARDMEMTESTS
            if (gcrunning(G(L)))
            {
                luaC_fullgc(L, true);
            }
#endif
        }
    }

    private static TValue* getintfromhash(Table* t, long key)
    {
        Node* n = hashint(t, key);
        Debug.Assert(ikeyinarray(t, key) == 0);
        while (true)
        {
            // check whether 'key' is somewhere in the chain
            if (keyisinteger(n) && keyival(n) == key)
            {
                return gval(n); // that's it
            }

            int nx = gnext(n);
            if (nx == 0)
            {
                break;
            }

            n += nx;
        }

        return absentkey;
    }

    private static bool hashkeyisempty(Table* t, ulong key)
    {
        TValue* val = getintfromhash(t, (long)key);
        return isempty(val);
    }

    private static byte finishnodeget(TValue* val, TValue* res)
    {
        if (!ttisnil(val))
        {
            setobj(null, res, val);
        }

        return ttypetag(val);
    }

    internal static byte luaH_getint(Table* t, long key, TValue* res)
    {
        uint k = ikeyinarray(t, key);
        if (k > 0)
        {
            byte tag = *getArrTag(t, k - 1);
            if (!tagisempty(tag))
            {
                farr2val(t, k - 1, tag, res);
            }

            return tag;
        }

        return finishnodeget(getintfromhash(t, key), res);
    }

    /// <summary>
    /// search function for short strings
    /// </summary>
    internal static TValue* luaH_Hgetshortstr(Table* t, TString* key)
    {
        Node* n = hashstr(t, key);
        Debug.Assert(strisshr(key));
        while (true)
        {
            // check whether 'key' is somewhere in the chain
            if (keyisshrstr(n) && eqshrstr(keystrval(n), key))
            {
                return gval(n); // that's it
            }

            int nx = gnext(n);
            if (nx == 0)
            {
                return absentkey; // not found
            }

            n += nx;
        }
    }

    internal static byte luaH_getshortstr(Table* t, TString* key, TValue* res)
    {
        return finishnodeget(luaH_Hgetshortstr(t, key), res);
    }

    private static TValue* Hgetlongstr(Table* t, TString* key)
    {
        Debug.Assert(!strisshr(key));
        TValue ko;
        setsvalue(null, &ko, key);
        return getgeneric(t, &ko, false); // for long strings, use generic case
    }

    private static TValue* Hgetstr(Table* t, TString* key)
    {
        if (strisshr(key))
        {
            return luaH_Hgetshortstr(t, key);
        }

        return Hgetlongstr(t, key);
    }

    internal static byte luaH_getstr(Table* t, TString* key, TValue* res)
    {
        return finishnodeget(Hgetstr(t, key), res);
    }
    
    /// <summary>
    /// main search function
    /// </summary>
    internal static byte luaH_get(Table* t, TValue* key, TValue* res)
    {
        TValue* slot;
        switch (ttypetag(key))
        {
            case LUA_VSHRSTR:
                slot = luaH_Hgetshortstr(t, tsvalue(key));
                break;
            
            case LUA_VNUMINT:
                return luaH_getint(t, ivalue(key), res);
            
            case LUA_VNIL:
                slot = absentkey;
                break;
            
            case LUA_VNUMFLT:
                if (luaV_flttointeger(fltvalue(key), out long k, F2Imod.F2Ieq)) // integral index?
                {
                    return luaH_getint(t, k, res); // use specialised version
                }

                goto default;

            default:
                slot = getgeneric(t, key, false);
                break;
        }

        return finishnodeget(slot, res);
    }

    /// <summary>
    /// When a 'pset' cannot be completed, this function returns an encoding
    /// of its result, to be used by 'luaH_finishset'.
    /// </summary>
    private static int retpsetcode(Table* t, TValue* slot)
    {
        if (isabstkey(slot))
        {
            return HNOTFOUND; // no slot with that key
        }

        // return node encoded
        return (int)((Node*)slot - t->node) + HFIRSTNODE;
    }

    private static int finishnodeset(Table* t, TValue* slot, TValue* val)
    {
        if (!ttisnil(slot))
        {
            setobj(null, slot, val);
            return HOK; // success
        }

        return retpsetcode(t, slot);
    }

    private static bool rawfinishnodeset(TValue* slot, TValue* val)
    {
        if (isabstkey(slot))
        {
            return false; // no slot with that key
        }

        setobj(null, slot, val);
        return true; // success
    }

    internal static int luaH_psetint(Table* t, long key, TValue* val)
    {
        Debug.Assert(ikeyinarray(t, key) == 0);
        return finishnodeset(t, getintfromhash(t, key), val);
    }

    private static int psetint(Table* t, long key, TValue* val)
    {
        luaH_fastseti(t, key, val, out int hres);
        return hres;
    }

    /// <summary>
    /// This function could be just this:
    ///    return finishnodeset(t, luaH_Hgetshortstr(t, key), val);
    /// However, it optimises the common case created by constructors (e.g.,
    /// {x=1, y=2}), which creates a key in a table that has no metatable,
    /// it is not old/black, and it already has space for the key.
    /// </summary>
    internal static int luaH_psetshortstr(Table* t, TString* key, TValue* val)
    {
        TValue* slot = luaH_Hgetshortstr(t, key);
        if (!ttisnil(slot))
        {
            // key already has a value? (all too common)
            setobj(null, slot, val); // update it
            return HOK; // done
        }

        if (checknoTM(t->metatable, TMS.NEWINDEX))
        {
            // no metamethod?
            if (ttisnil(val)) // new value is nil?
            {
                return HOK; // done (value is already nil/absent)
            }

            if (isabstkey(slot) && // key is absent?
                !(isblack((GCObject*)t) && iswhite((GCObject*)key)))
            {
                // and don't need barrier?
                TValue tk; // key as a TValue
                setsvalue(null, &tk, key);
                if (insertkey(t, &tk, val))
                {
                    // insert key, if there is space
                    invalidateTMcache(t);
                    return HOK;
                }
            }
        }

        // Else, either table has new-index metamethod, or it needs barrier,
        // or it needs to rehash for the new key. In any of these cases, the
        // operation cannot be completed here. Return a code for the caller.
        return retpsetcode(t, slot);
    }

    internal static int luaH_psetstr(Table* t, TString* key, TValue* val)
    {
        if (strisshr(key))
        {
            return luaH_psetshortstr(t, key, val);
        }

        return finishnodeset(t, Hgetlongstr(t, key), val);
    }

    internal static int luaH_pset(Table* t, TValue* key, TValue* val)
    {
        switch (ttypetag(key))
        {
            case LUA_VSHRSTR:
                return luaH_psetshortstr(t, tsvalue(key), val);
            case LUA_VNUMINT:
                return psetint(t, ivalue(key), val);
            
            case LUA_VNIL:
                return HNOTFOUND;
            
            case LUA_VNUMFLT:
                if (luaV_flttointeger(fltvalue(key), out long k, F2Imod.F2Ieq)) // integral index?
                {
                    return psetint(t, k, val); // use specialized version
                }

                goto default;

            default:
                return finishnodeset(t, getgeneric(t, key, false), val);
        }
    }

    /// <summary>
    /// Finish a raw "set table" operation, where 'hres' encodes where the
    /// value should have been (the result of a previous 'pset' operation).
    /// Beware: when using this function the caller probably need to check a
    /// GC barrier and invalidate the TM cache.
    /// </summary>
    internal static void luaH_finishset(lua_State* L, Table* t, TValue* key, TValue* value, int hres)
    {
        Debug.Assert(hres != HOK);
        if (hres == HNOTFOUND)
        {
            if (ttisnil(key))
            {
                luaG_runerror(L, "table index is nil");
            }
            else if (ttisfloat(key))
            {
                double f = fltvalue(key);
                if (luaV_flttointeger(f, out long k, F2Imod.F2Ieq))
                {
                    TValue aux;
                    setivalue(&aux, k); // key is equal to an integer
                    key = &aux; // insert it as an integer
                }
                else if (double.IsNaN(f))
                {
                    luaG_runerror(L, "table index is NaN");
                }
            }
            else if (isextstr(key))
            {
                // external string?
                // If string is short, must internalise it to be used as table key
                TString* ts = luaS_normstr(L, tsvalue(key));
                setsvalue2s(L, L->top.p++, ts); // anchor 'ts' (EXTRA_STACK)
                luaH_newkey(L, t, s2v(L->top.p - 1), value);
                L->top.p--;
                return;
            }

            luaH_newkey(L, t, key, value);
        }
        else if (hres > 0)
        {
            // regular Node?
            setobj2t(L, gval(gnode(t, hres - HFIRSTNODE)), value);
        }
        else
        {
            // array entry
            hres = ~hres; // real index
            obj2arr(t, (uint)hres, value);
        }
    }

    /// <summary>
    /// beware: when using this function, you probably need to check a GC
    /// barrier and invalidate the TM cache.
    /// </summary>
    internal static void luaH_set(lua_State* L, Table* t, TValue* key, TValue* value)
    {
        int hres = luaH_pset(t, key, value);
        if (hres != HOK)
        {
            luaH_finishset(L, t, key, value, hres);
        }
    }

    /// <summary>
    /// Ditto for a GC barrier. (No need to invalidate the TM cache, as
    /// integers cannot be keys to metamethods.)
    /// </summary>
    internal static void luaH_setint(lua_State* L, Table* t, long key, TValue* value)
    {
        uint ik = ikeyinarray(t, key);
        if (ik > 0)
        {
            obj2arr(t, ik - 1, value);
        }
        else
        {
            bool ok = rawfinishnodeset(getintfromhash(t, key), value);
            if (!ok)
            {
                TValue k;
                setivalue(&k, key);
                luaH_newkey(L, t, &k, value);
            }
        }
    }

    /// <summary>
    /// Try to find a boundary in the hash part of table 't'. From the
    /// caller, we know that 'asize + 1' is present. We want to find a larger
    /// key that is absent from the table, so that we can do a binary search
    /// between the two keys to find a boundary. We keep doubling 'j' until
    /// we get an absent index.  If the doubling would overflow, we try
    /// LUA_MAXINTEGER. If it is absent, we are ready for the binary search.
    /// ('j', being max integer, is larger or equal to 'i', but it cannot be
    /// equal because it is absent while 'i' is present.) Otherwise, 'j' is a
    /// boundary. ('j + 1' cannot be a present integer key because it is not
    /// a valid integer in Lua.)
    /// About 'rnd': If we used a fixed algorithm, a bad actor could fill
    /// a table with only the keys that would be probed, in such a way that
    /// a small table could result in a huge length. To avoid that, we use
    /// the state's seed as a source of randomness. For the first probe,
    /// we "randomly double" 'i' by adding to it a random number roughly its
    /// width.
    /// </summary>
    private static ulong hash_search(lua_State* L, Table* t, uint asize)
    {
        ulong i = asize + 1; // caller ensures t[i] is present
        uint rnd = G(L)->seed;
        int n = asize > 0 ? luaO_ceillog2(asize) : 0; // width of 'asize'
        uint mask = (1u << n) - 1; // 11...111 with the width of 'asize'
        uint incr = (rnd & mask) + 1; // first increment (at least 1)
        ulong j = incr <= long.MaxValue - i ? i + incr : i + 1;
        rnd >>= n; // used 'n' bits from 'rnd'
        while (!hashkeyisempty(t, j))
        {
            // repeat until an absent t[j]
            i = j; // 'i' is a present index
            if (j <= long.MaxValue / 2 - 1)
            {
                j = j * 2 + (rnd & 1); // try again with 2j or 2j+1
                rnd >>= 1;
            }
            else
            {
                j = long.MaxValue;
                if (hashkeyisempty(t, j)) // t[j] not present?
                {
                    break; // 'j' now is an absent index
                }
                else // weird case
                {
                    return j; // well, max integer is a boundary...
                }
            }
        }

        // i < j  &&  t[i] present  &&  t[j] absent
        while (j - i > 1u)
        {
            // do a binary search between them
            ulong m = (i + j) / 2;
            if (hashkeyisempty(t, m))
            {
                j = m;
            }
            else
            {
                i = m;
            }
        }

        return i;
    }

    private static uint binsearch(Table* array, uint i, uint j)
    {
        Debug.Assert(i <= j);
        while (j - i > 1u)
        {
            // binary search
            uint m = (i + j) / 2;
            if (arraykeyisempty(array, m))
            {
                j = m;
            }
            else
            {
                i = m;
            }
        }

        return i;
    }

    /// <summary>
    /// return a border, saving it as a hint for next call
    /// </summary>
    private static ulong newhint(Table* t, uint hint)
    {
        Debug.Assert(hint <= t->asize);
        *lenhint(t) = hint;
        return hint;
    }

    /// <summary>
    /// Try to find a border in table 't'. (A 'border' is an integer index
    /// such that t[i] is present and t[i+1] is absent, or 0 if t[1] is absent,
    /// or 'maxinteger' if t[maxinteger] is present.)
    /// If there is an array part, try to find a border there. First try
    /// to find it in the vicinity of the previous result (hint), to handle
    /// cases like 't[#t + 1] = val' or 't[#t] = nil', that move the border
    /// by one entry. Otherwise, do a binary search to find the border.
    /// If there is no array part, or its last element is non empty, the
    /// border may be in the hash part.
    /// </summary>
    internal static ulong luaH_getn(lua_State* L, Table* t)
    {
        uint asize = t->asize;
        if (asize > 0)
        {
            /// <summary>
            /// is there an array part?
            /// </summary>
            const uint maxvicinity = 4;
            uint limit = *lenhint(t); // start with the hint
            if (limit == 0)
            {
                limit = 1; // make limit a valid index in the array
            }

            if (arraykeyisempty(t, limit))
            {
                // t[limit] empty?
                // there must be a border before 'limit'
                // look for a border in the vicinity of the hint
                for (uint i = 0; i < maxvicinity && limit > 1; i++)
                {
                    limit--;
                    if (!arraykeyisempty(t, limit))
                    {
                        return newhint(t, limit); // 'limit' is a border
                    }
                }

                // t[limit] still empty; search for a border in [0, limit)
                return newhint(t, binsearch(t, 0, limit));
            }

            // 'limit' is present in table; look for a border after it
            // look for a border in the vicinity of the hint
            for (uint i = 0; i < maxvicinity && limit < asize; i++)
            {
                limit++;
                if (arraykeyisempty(t, limit))
                {
                    return newhint(t, limit - 1); // 'limit - 1' is a border
                }
            }

            if (arraykeyisempty(t, asize))
            {
                // last element empty?
                // t[limit] not empty; search for a border in [limit, asize)
                return newhint(t, binsearch(t, limit, asize));
            }

            // last element non empty; set a hint to speed up finding that again
            // (keys in the hash part cannot be hints)
            *lenhint(t) = asize;
        }

        // no array part or t[asize] is not empty; check the hash part
        Debug.Assert(asize == 0 || !arraykeyisempty(t, asize));
        if (isdummy(t) || hashkeyisempty(t, asize + 1))
        {
            return asize; // 'asize + 1' is empty
        }

        // 'asize + 1' is also non empty
        return hash_search(L, t, asize);
    }

#if LUA_TEST
    // export this function for the test library

    private static Node* luaH_mainposition(Table* t, TValue* key)
    {
        return mainpositionTV(t, key);
    }
#endif
}
