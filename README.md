# Hoard

Bigger stacks, without turning the game into a sandbox.

Built against the installed game (0.221.12, Unity 6000.0.61, BepInEx 5.4.23.3, Harmony 2.9).

## The defaults are the point

The usual version of this mod ships x10 stacks and halved weight. That does not make
Valheim more convenient so much as remove a system from it — the mid game is largely a
logistics problem, and the tedium and the difficulty are the same mechanic seen from two
sides.

So the defaults here fix the annoying half and leave the interesting half alone:

| Default | Effect |
| --- | --- |
| `StackMultiplier = 2` | Stacks double. Wood 50 → 100 |
| `WeightMultiplier = 1` | **Weight untouched.** Carry limit still decides what comes home |
| `IncludeNonTeleportable = false` | **Ore and metal bars are not modified at all** |
| `StackCap = 200` | Ceiling regardless of multiplier |

That third one is the important one. Metal cannot go through a portal, so hauling it by
cart and boat is a deliberate part of the game's pacing rather than an oversight. Leaving
ore alone means your inventory stops being a nuisance for berries and wood while the trip
home from a copper mine is still a trip.

Everything is a knob. If you decide you want the easy version, it is one edit away.

## What it will not do

**It never makes equipment stackable.** Only items with a vanilla stack size above 1 are
touched. An item that does not stack in vanilla is a weapon, a tool or a piece of armour,
and those carry per-item durability and quality — collapsing several into a count silently
throws all but one of those away.

## Design notes

**Both `ObjectDB.Awake` and `ObjectDB.CopyOtherDB` are patched.** `Awake` builds the
database at startup; `CopyOtherDB` rebuilds it when a world loads. Patching only `Awake`
means the untouched values quietly replace yours the moment you join a game.

**Every value is computed from the item's original**, captured the first time it is seen
and never overwritten. Since the database is set up more than once, a mod that multiplies
whatever it currently finds ends up squaring its own multiplier on the second pass. It also
means changing the multiplier and reloading gives the right answer instead of compounding.

One caveat that follows from that: if another mod changes an item before this one first
sees it, its value is what gets recorded as "original".

## Config

`BepInEx\config\ezomic.valheim.hoard.cfg`

| Key | Default | What it does |
| --- | --- | --- |
| `StackMultiplier` | `2` | Multiplies vanilla stack size of anything that already stacks |
| `StackCap` | `200` | Hard ceiling on the result |
| `WeightMultiplier` | `1` | Multiplies item weight; `1` leaves it alone |
| `IncludeNonTeleportable` | `false` | Also affect ore, bars and anything portal-blocked |
| `IncludeTrophies` | `true` | Also affect trophies |
| `ExcludeItems` | | Comma-separated prefab names to skip entirely |
| `Verbose` | `false` | Log every item that changed |

A value already written to the `.cfg` beats a new default in code — change the `.cfg`.

## Building

```bash
dotnet build
```

Deploys to the repo-local `testprofile\`. Override with `-p:ProfileDir=...`, or build it
into the shared play profile with `valheim-own-profile\build-all.ps1`.

## What to check

1. Wood should cap at 100 rather than 50.
2. **Copper ore should still cap at 30** — unchanged, because it cannot be teleported.
3. Weight per item should be exactly vanilla.
4. A sword or axe should still not stack.
5. Set `Verbose = true` once and read the log if a specific item looks wrong.

## Author

Hoard is an original mod by **Robbin Thijssen** (Thijssen Software).
Copyright (c) 2026 Robbin Thijssen. MIT licensed — see `LICENSE`.
