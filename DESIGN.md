# Hoard design notes

Why it works the way it does, and how it is built. None of this is needed to play; for that
see the [README](README.md).

## Where the biome routes stop

That lands **665 of 671 stackable items**. The six left over are crafting oddities —
`AxeHead1`, `AxeHead2`, `BarrelRings`, `FireworksRocket_White`, `Ironpit`, `ScytheHandle` —
left unplaced rather than guessed at, because a wrong biome is worse than vanilla.

An item found in several biomes belongs to the **earliest**, because that is where you first
had to carry it home. A crafted one takes the **latest** of its ingredients. Those two rules
sound contradictory and are not: found is about where it turns up, made is about when you can
make it.

That last route exists because some things are inside **locations** — copper and silver
deposits, iron scrap in Sunken Crypts — and a location keeps its prefab as a soft reference
that is not loaded until the game wants it. Forcing dungeon interiors to load on the way into
a world, to learn something that fits on one config line, is not a trade worth making.

**The item list prints every item that ended up with no biome**, so that line gets filled in
from what is actually missing rather than from guesswork. Anything still unplaced simply
stays at vanilla, which is the same thing that happens to an item no tier names.

## Why progress is the world's and not yours

Stack size lives on the item prefab, so "different players, different stacks" would mean two
clients with different item databases. One drops a hundred wood in the shared chest, the other
opens it holding a slot bigger than its own maximum, and the next move writes back through the
smaller rules — silent loss out of a shared chest, the worst bug a storage mod can have.

## Why the item list is built by the tuning pass

The rows are built by the tuning pass itself rather than by a
second walk over the database — a separate walk would be a second copy of the eligibility
rules, and the first thing it would do is disagree with the real one.

## Implementation notes

**Both `ObjectDB.Awake` and `ObjectDB.CopyOtherDB` are patched.** `Awake` builds the
database at startup; `CopyOtherDB` rebuilds it when a world loads. Patching only `Awake`
means the untouched values quietly replace yours the moment you join a game.

**Every value is computed from the item's original**, captured the first time it is seen
and never overwritten. Since the database is set up more than once, a mod that multiplies
whatever it currently finds ends up squaring its own multiplier on the second pass. It also
means changing the multiplier and reloading gives the right answer instead of compounding.

One caveat that follows from that: if another mod changes an item before this one first
sees it, its value is what gets recorded as "original".

## What to check

1. On a world with Eikthyr down, wood should cap at 100 — and on a fresh world it should
   still be 50, with the item list saying `awaiting defeated_eikthyr`.
2. **Copper ore should still cap at 30** until Bonemass, because it cannot be teleported.
3. Weight per item should be exactly vanilla.
4. A sword or axe should still not stack.
5. Open `ezomic.valheim.hoard.items.txt` if a specific item looks wrong — it names every
   item and the rule that left it alone. `Verbose = true` is for watching a single pass
   happen in the log; the file is the better answer to a question about one item.
