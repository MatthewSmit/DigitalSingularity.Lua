local f = coroutine.wrap(assert(loadfile('big.lua')))
assert(f() == 'b')
assert(f() == 'a')