# Hoard

Bigger stacks, without turning the game into a sandbox.

Built against the installed game (0.221.12, Unity 6000.0.61, BepInEx 5.4.23.3, Harmony 2.9).

## The defaults are the point

The usual version of this mod ships x10 stacks and halved weight. That does not make
Valheim more convenient so much as remove a system from it. The mid game is largely a
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

## Each boss raises one kind of stack

A flat multiplier cannot tell the two halves of that argument apart. Meadows scarcity is the
game teaching you to plan; your ninth trip to the same copper deposit is not teaching you
anything. So stacks start at vanilla and each boss raises **one group**:

**Kill a biome's boss and the things that biome gives you stack better.**

| Boss | Raises |
| --- | --- |
| Eikthyr | Meadows |
| The Elder | Black Forest |
| Bonemass | Swamp |
| Moder | Mountain |
| Yagluth | Plains |
| The Queen | Mistlands |
| Fader | Ashlands |

So the copper you carry out of the Black Forest stops being a nuisance once the Black
Forest's boss is dead, and not before. Every boss matters, and each one eases exactly the
hauling you have just finished proving you can survive.

### Which biome an item belongs to is worked out, not listed

There is no table of items in this mod. Four routes, all reading the game's own data:

- **The world's vegetation table** places a copper deposit in the Black Forest, and the
  deposit says it drops copper ore. So copper ore is a Black Forest item.
- **The spawn tables** place Fulings in the Plains, and the Fuling's drop list says black
  metal scrap. So black metal is a Plains item.
- **Recipes and stations.** Anything made takes the biome of its **latest** ingredient,
  because that is when you could first make it. Barley is Plains, so flour is Plains, so
  dough is Plains, so bread is Plains, through a mill, a recipe and an oven in turn, none of
  it written down here. Every mead lands the same way, off the fermenter.
- **Spawners.** A greydwarf nest or a surtling geyser is placed in a biome, and it is the
  nest rather than the creature that the tables know about.
- **Bosses place their own drops.** A boss prefab knows the global key its death sets, and
  the tier table knows which biome that key belongs to, so Moder's trophy is a Mountain item
  without either side naming it.
- **`BiomeOverrides`** for the roots none of that reaches: ore deposits, dungeon loot, fish
  and the trophies of creatures that only appear inside a location. Roots only: the recipe
  pass turns each one into everything made from it.

That lands 665 of 671 stackable items. The six it does not reach are crafting oddities, left
unplaced rather than guessed at, because a wrong biome is worse than vanilla. Anything
unplaced stays at vanilla.

An item found in several biomes belongs to the **earliest**, because that is where you first
had to carry it home. A crafted one takes the **latest** of its ingredients. Those two rules
sound contradictory and are not: found is about where it turns up, made is about when you can
make it.

**The groups are read off the game's own systems, not a list in this mod.** Building material
is whatever appears as a build cost on the Hammer's piece table, so an item a content mod adds
lands in the right group by itself. Metal is whatever the game refuses to send through a
portal, the same flag the pacing rule already used.

Every row is one line of `ProgressionTiers`, so a different table is one edit. Any global key
works, not just a boss key.

**An earned metal tier lifts the portal rule.** That is on purpose and it is the point: the
haul is real pacing while you are doing your first copper runs, and by the boss that hands you
the iron age you have already paid it. `IncludeNonTeleportable` remains for anyone who
disagrees and does not want to wait.

### In multiplayer, it is the world's progress

Global keys are world state the server pushes to every client, so everyone computes the same
answer with no new networking. A fresh character joining a Mistlands-era world gets
Mistlands-era stacks, which is the deliberate trade: progress is the world's, not yours.

Keys only ever accumulate, so the multiplier only ever rises. Nothing here can leave a stack
sitting above its own limit.

### With Utangard

[Utangard](https://github.com/Ezomic/valheim-utangard) opens a biome only when **every member
of the group** was personally at that boss's death, which is not the same as the boss having
died in the world. Those answers part company the moment somebody is offline for a kill.

When Utangard is installed, Hoard asks it instead of reading the key, so stacks never arrive
for a biome Utangard still has fenced off. It is a soft dependency in both directions: neither
mod needs the other, and `DeferToUtangard = false` turns it off.

## What it will not do

**It never makes equipment stackable.** Only items with a vanilla stack size above 1 are
touched. An item that does not stack in vanilla is a weapon, a tool or a piece of armour,
and those carry per-item durability and quality, and collapsing several into a count silently
throws all but one of those away.

## Design notes

Why both `ObjectDB` entry points are patched, why every value is computed from the item's
captured original, why the biome routes stop where they do, and the release checklist:
[DESIGN.md](DESIGN.md).

## Core is optional

Hoard installs and runs on its own. [Core](https://github.com/Ezomic/valheim-core) is a
**soft** dependency: present, it is used; absent, nothing here is degraded. Installing
Hoard from Thunderstore no longer installs Core with it.

What Core adds is the **version gate**, a handshake that compares mod versions and build
ids on connect and refuses a client that does not match. Without it nothing reports two ends running different item data, which desyncs inventories. The `ObjectDB.CopyOtherDB` patch still puts a joining client on the server's numbers, which covers the common case on its own.

Solo, none of that applies and Core is not needed at all.

## Config

`BepInEx\config\ezomic.valheim.hoard.cfg`

| Key | Default | What it does |
| --- | --- | --- |
| `ScaleWithProgression` | `true` | Each boss raises one group. Off falls back to `StackMultiplier` |
| `ProgressionBase` | `1` | Multiplier for a group no boss has unlocked; 1 is vanilla |
| `ProgressionTiers` | see above | `boss:group:multiplier`, comma separated |
| `ProgressionStep` | `0` | Later bosses re-raise earlier groups by this fraction, compounding |
| `ProgressionOrder` | the seven bosses | What "later" means; idle while `ProgressionStep` is 0 |
| `DeferToUtangard` | `true` | Ask Utangard what the group has earned, when it is installed |
| `StackMultiplier` | `2` | Flat multiplier, used only when `ScaleWithProgression` is off |
| `StackCap` | `200` | Hard ceiling on the result |
| `WeightMultiplier` | `1` | Multiplies item weight; `1` leaves it alone |
| `IncludeNonTeleportable` | `false` | Also affect ore, bars and anything portal-blocked |
| `IncludeTrophies` | `true` | Also affect trophies |
| `ExcludeItems` | | Comma-separated prefab names to skip entirely |
| `WriteItemList` | `true` | Write the item list described below beside the `.cfg` |
| `Verbose` | `false` | Log every item that changed |

A value already written to the `.cfg` beats a new default in code. Change the `.cfg`.

## The item list

`BepInEx\config\ezomic.valheim.hoard.items.txt`, rewritten on every run.

`ExcludeItems` takes prefab names, and prefab names are not guessable: copper ore is
`CopperOre` but raspberries are `Raspberry` and a draugr's arrow is `draugr_arrow`. So the
mod writes down every item it saw, what it did to each one, and which rule stopped it when
it did nothing:

```
Prefab       Name          Type        Stack       Weight  Left alone because
Wood         Wood          Material    50 -> 100   2
CopperOre    Copper Ore    Material    30          10      portal-blocked
SwordIron    Iron Sword    OneHanded   1           0.8     equipment
```

An arrow means Hoard changed that value. A single number means it did not, and the last
column says why. The header carries the settings that pass ran under and a count of each
reason, so `equipment 677, portal-blocked 22` is the whole safety story at a glance.

This is also the answer to "why did this item not change", which is the only question a mod
like this ever gets asked. ## Building

```bash
dotnet build
```

Deploys to the repo-local `testprofile\`. Override with `-p:ProfileDir=...`, or build it
into the shared play profile with `valheim-own-profile\build-all.ps1`.

## Author

Hoard is an original mod by **Robbin Thijssen** (Thijssen Software).
Copyright (c) 2026 Robbin Thijssen. MIT licensed. See `LICENSE`.
