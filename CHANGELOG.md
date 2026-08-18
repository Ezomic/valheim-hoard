# Changelog

Notable changes to Yoke. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

1.0.0 is the first published version. The numbers below it were development builds; the rule
was that 1.0 is spent on the build that actually ships, and it was.

## [1.0.0] - 2026-08-18

First release.

Yoke raises the stacks of a biome's own goods when you kill that biome's boss. Meadows
timber doubles when Eikthyr falls, Black Forest ore when The Elder does, and so on to Fader
and the Ashlands. Nothing else changes: weight is untouched, equipment never stacks, and
until the Swamp is earned a portal still refuses ore.

What makes it different from the rest of its category is that **nothing is listed**. Which
biome an item belongs to is worked out from the game's own tables, so an item a content mod
adds lands in the right place by itself:

- the world's vegetation table, where a copper deposit is Black Forest and the deposit says
  it drops copper ore;
- the spawn tables and spawners, down to the nest that makes the creature rather than the
  creature itself;
- recipes, smelters, cooking stations and fermenters, where a made thing takes the biome of
  its latest ingredient, so bread is Plains because barley is;
- each boss's own drops, matched through the global key its death sets;
- and a short override list for the roots that only exist inside locations.

That places 665 of 671 stackable items. The six it does not are crafting oddities, left
alone rather than guessed at.

The generated item list beside the config names every item, what was done to it, and which
rule or which boss it is waiting on.

### Verified in play

Wood at 100 and blueberries at 50 on a world with Eikthyr down, with copper ore still
portal-blocked. The biome index, the tier ramp, the portal rule and the retune when a world's
keys arrive were all confirmed on a real client.

**Not yet seen:** a boss dying mid-session and the stacks moving without a restart. The path
is exercised on every world load; what is unproven is that one arrival.

## [0.13.0] - 2026-08-18

### Hoard is now Yoke

A yoke is the bar across the shoulders that turns two loads into one carry, which is what
this mod does. "Hoard" named the pile; it never named the carrying, and the carrying is the
half the progression is about.

Nothing about the behaviour changed. What the rename touches:

- The plugin GUID is now `ezomic.valheim.yoke`, so the config file is renamed with it.
  **Hoard's config is adopted automatically** on the first run under the new name, before
  anything binds, so settings carry over unchanged. That matters more than it sounds:
  `BiomeOverrides` runs to about a hundred entries, and a silent reset would drop the mod
  back to derived-only placement with nothing in the log to explain why the ore stopped
  stacking.
- The assembly, namespace and plugin class follow the name.
- The icon is redrawn: two stacks under a notched bar, rendered through the suite's shared
  icon script so it sits correctly beside the others.

No world data is at risk. This mod registers no prefabs, so nothing saved is keyed to a name
that would stop resolving.

## [0.12.1] - 2026-08-17

### Everything is placed

**665 items placed, six left.** Two more derivations and a filled-in override list took the
unplaced count from 122 to 6.

- **Spawners.** A greydwarf nest, a surtling geyser, the skeleton piles in a crypt: the
  creature is not in the world's spawn table, the thing that makes it is. Walking `SpawnArea`
  finds them, which is where most of the missing trophies were.
- **Bosses place their own drops.** A boss stands in a location so nothing else can see it,
  but the boss prefab knows the global key its death sets and the tier table knows which
  biome that key belongs to. Moder's trophy is a Mountain item because Moder's key is the
  Mountain's key, since neither side names an item, and a modded boss added to `ProgressionTiers`
  brings its drops with it.
- **`BiomeOverrides` now ships the roots** that live inside locations: ore deposits, dungeon
  loot, dvergr and charred material, fish, and the trophies of creatures that only appear in
  a location. Roots, not a full list: the recipe pass turns each one into everything made
  from it, so cooked fish, chicken, spices and meads all placed themselves from these.

The six that remain are `AxeHead1`, `AxeHead2`, `BarrelRings`, `FireworksRocket_White`,
`Ironpit` and `ScytheHandle`, crafting oddities nobody hauls. They are unplaced rather than
guessed at, because a wrong biome is worse than vanilla, and they stay at vanilla.

## [0.12.0] - 2026-08-17

### A boss raises the biome it guards

The four hand-made categories are gone. Each boss now raises the stacks of **everything that
comes from its own biome**: Eikthyr the Meadows, The Elder the Black Forest, Bonemass the
Swamp, and so on through Fader and the Ashlands. Copper stops being a nuisance once the
Black Forest's boss is dead, and not before.

This also settles what the last three versions kept circling: with a biome each, all seven
bosses matter without inventing categories for the late ones or adding a percentage drift.

**Nothing is listed.** Which biome an item belongs to is worked out from four sources:

- `ZoneSystem.m_vegetation`: a copper deposit is Black Forest and the deposit says it drops
  copper ore, so copper ore is Black Forest.
- The spawn tables plus `CharacterDrop`: Fulings are Plains, so black metal scrap is Plains.
- Recipes, smelters, cooking stations and fermenters, where a made thing takes the biome of
  its **latest** ingredient. Barley is Plains, so flour is, so dough is, so bread is, across
  a mill, a recipe and an oven. Every mead lands off the fermenter the same way.
- `BiomeOverrides`, for what none of that reaches.

Found items take the **earliest** biome they appear in, made items the **latest** of their
ingredients. Not a contradiction: found is about where it turns up, made is about when you
could make it. That rule also repairs a wrong answer for free: Charred warriors drop bronze,
so the spawn tables call bronze an Ashlands item, and the recipe calling it Black Forest wins.

On this machine it places **543 items**, with 122 stackables left over, mostly fish,
trophies and chest loot, all of which stay at vanilla. The item list prints them so
`BiomeOverrides` is filled in from what is really missing.

The overrides shipped by default are the ones no table can see: iron scrap and the copper and
silver deposits live inside locations, whose prefabs are soft references that are not loaded
until the game wants them, and forcing dungeon interiors to load on the way into a world is
not worth what it buys.

`LiftPortalRuleAt` replaces the old metal group: the Swamp tier now lifts the portal rule,
because Bonemass is where iron begins and the copper runs are behind you by then.

### Fixed

- The index rebuilt **once per item** while incomplete, walking every prefab in the scene a
  thousand times per pass. It is built once per pass now.
- `SpawnSystem.Awake` fires more than once in a loaded world; the rebuild is guarded so it
  happens on the first one only.
- Overrides were applied after the conversions, so copper bars came out unplaced. The
  smelter pass went looking for copper ore before the override had put it on the map. They
  are applied before the derivation as well as after.
- Conversions and recipes ran in sequence, which left bread unplaced whatever the pass count,
  because the step it waited on lived in the other list. They interleave until it settles.

## [0.11.1] - 2026-08-17

### Progression finishes at Bonemass

`ProgressionStep` now defaults to **0**, so nothing after Bonemass changes a stack size.
The mechanism from 0.11.0 stays as a knob; it is simply off.

Three reasons it should not be the default, and none of them is that it did not work:

- **Ten percent is imperceptible.** Wood going 100 to 110 is not a reward anyone feels, and
  it lands at the moment a boss dies, when there is a great deal else to notice.
- **The numbers read as a bug.** Vanilla stacks are round: 20, 30, 50, 100. Compounding
  produces 121, 133, 146, 177, which look like a fault rather than a design.
- **It drifts past the honest amount.** Building ended at 3.54x, well beyond the flat 2x
  this mod ships and defends. A narrow table that quietly widens forever is the x10 mod
  taking the long way round.

Ending at Bonemass is a complete design and not an unfinished one: by then you have
portals, a cart and a longship, which is the game solving hauling on its own.

## [0.11.0] - 2026-08-17

### Every later boss adds ten percent (off by default since 0.11.1)

A boss now raises the group it is assigned **and** every group unlocked before it, by
`ProgressionStep`, ten percent, compounding. Building unlocked at Eikthyr is 2x, 2.2x once
The Elder is down, 2.42x after Bonemass, and 3.54x with everything dead.

Only three of the seven bosses unlock a group, so without this the whole feature was over
by Bonemass and the back half of the game got a reward table that had stopped paying. It
also gives the four bosses that unlock nothing something to do, without inventing a
category for them or reaching for a second axis like weight.

Ten percent is small enough that it never turns into the x10 mod by accident: six of them
on a doubled stack is 3.5x, and `StackCap` still caps all of it. `ProgressionStep = 0`
restores flat tiers.

- **`ProgressionOrder` is new**, because "later" needs an order and the tier table never had
  one. A key that is not listed still unlocks its own group; it simply never counts as
  before or after anything, which is the honest answer for a modded boss key somebody added
  to the tiers and not the order.
- A group still at the base multiplier does not compound. Untouched is untouched, not
  "unlocked at zero percent".

**Untested:** the compounding itself. The dev world has only `defeated_eikthyr`, so no run
here has had two keys down at once, so the unlock path and the retune when a key arrives are
both verified live, the ten percent is verified only by reading it.

## [0.10.1] - 2026-08-17

### The table is shorter on purpose

Ammo, food and the catch-all group lost their tiers, and trophies moved to Bonemass. Three
entries remain: Eikthyr raises building material, The Elder the crops, Bonemass the metal
and the trophies.

A group with no entry stays at vanilla forever, and most of the game's stackables are meant
to. A mod that eventually doubles everything is a slower version of the one that doubles it
on day one; what is left here is the hauling that is actually repetitive.

- **The portal rule now says when it lifts.** Ore read `portal-blocked`, which is a
  half-truth once a metal tier exists. It now reads `portal-blocked until
  defeated_bonemass`. Tallies match on the rule, so a qualified note still counts under it.
- **"Earned so far" lists only groups a tier can reach.** Printing the others at 1x read as
  a promise that some boss would come for them, and with this table none will.

## [0.10.0] - 2026-08-17

### Each boss raises one kind of stack

Stacks now start at vanilla and each boss unlocks one group. `ScaleWithProgression = false`
restores the old flat multiplier.

A flat multiplier cannot tell the two halves of this mod's own argument apart. Meadows
scarcity is the game teaching you to plan; the ninth trip to the same copper deposit is not
teaching you anything.

- **The groups are read off the game's own systems.** Building material is whatever appears
  as a build cost on the Hammer's piece table, crops are the Cultivator's, metal is whatever
  a portal refuses. So an item a content mod adds lands in the right group by itself, and
  there is no list here to go stale.
- **An earned metal tier lifts the portal rule.** The haul is real pacing during the first
  copper runs and paid off by the boss that hands you the iron age.
  `IncludeNonTeleportable` stays for anyone who wants it sooner.
- **The item list names the boss each group is waiting for**, so "why is my wood still 50"
  is answered in the file rather than by reading the tier table.

### It hangs on the world's progress, not each player's

Stack size lives on the item prefab, so per-player stacks would mean two clients holding
different item databases. One drops a hundred wood in a shared chest, the other opens it
holding a slot over its own maximum, and the next move writes back through the smaller
rules. Global keys are world state pushed to every client, so everyone computes the same
answer with no new networking, and since keys only accumulate the multiplier only ever
rises.

Read through the **string** global-key lookup, not the `GlobalKeys` enum: that enum stops at
`defeated_goblinking` in this build, with no `defeated_queen` or `defeated_fader` member, so
the enum route would have silently capped the ramp at the Plains.

### Utangard decides what counts, when it is installed

Utangard opens a biome only when every member of the group was personally at that boss's
death, which is not the same as the boss having died in the world. Yoke now asks it rather
than reading the raw key, so stacks never arrive for a biome Utangard still has fenced off.
Soft dependency; `DeferToUtangard = false` turns it off, and neither mod needs the other.

### Changed

- The item list gained a Group column, sorts by group, and its header counts describe the
  **state** of the database rather than the last pass, so they no longer appear to
  contradict the `Retuned n` line in the log, which counts only what that pass moved.

## [0.9.2] - 2026-08-17

### Added

- **An item list, written beside the config on every run**.
  `ezomic.valheim.yoke.items.txt`. Every item in the game, its vanilla stack size and
  weight, what Yoke made them, and which rule left it alone when it did nothing. Off with
  `WriteItemList = false`.

  `ExcludeItems` takes prefab names and prefab names are not guessable: raspberries are
  `Raspberry` and a draugr's arrow is `draugr_arrow`, so a per-item setting without a list
  beside it is a setting nobody can spell. It doubles as the answer to "why did this item
  not change", which is the only question a mod like this ever gets, and it answers it
  without asking anyone to turn on `Verbose` and read a log.

  The rows are built by the tuning pass as it runs rather than by a second walk over the
  database. A separate walk would be a second copy of the eligibility rules, and the first
  thing it would do is disagree with the real one.

### Fixed

- **Weights are written with a decimal point on every machine.** `float.ToString` follows
  the machine's locale, so on a Dutch install resin came out as `0,3`, and a comma in a file
  whose other numbers come from a `.cfg` that uses points, sitting next to a column of
  integers. Both the item list and the `Verbose` log now format invariantly.

### Changed

- The pass logs three counts rather than two: changed, already at those values, and left
  alone. They add up to the item count now. `Retuned 0` on the second setup of a session
  reads like a failure until you know the other items were already at the right numbers.

## [0.9.1] - 2026-08-17

### Core is optional

- **Core is now a soft dependency rather than a hard one.** Yoke installs and runs on its
  own, and the Thunderstore package no longer drags Core in with it. Present, Core is used;
  absent, nothing here is degraded.
- What is given up standing alone is the **version gate**, not the mod. Item data that
  differs between two ends desyncs inventories, and without Core nothing reports that. The
  `ObjectDB.CopyOtherDB` patch still puts a joining client on the server's numbers, which
  covers the common case on its own. Solo, none of it applies.
- The registration call sits in its own method that is **never inlined**, because the JIT
  resolves the assemblies a method needs when it first compiles that method. A Core call
  written directly into `Awake` would drag the assembly in before the installed-check could
  prevent it, and the missing-assembly exception would land during plugin load, which is
  precisely the failure the soft dependency exists to avoid.

## [0.9.0] - 2026-08-16

First complete build. Carried the number 1.0.0 until the release policy above was settled;
it was never published under it.

### Stacks

- **Stacks double.** Wood 50 becomes 100, with a hard ceiling of 200 whatever the multiplier
  says.
- **Weight is untouched by default.** The carry limit still decides what comes home, which is
  the half of the logistics problem worth keeping.
- **Ore and metal bars are not modified at all.** Metal cannot go through a portal, so
  hauling it by cart and boat is deliberate pacing rather than an oversight. One config line
  turns it on if you disagree.
- **Equipment is never made stackable.** Only items with a vanilla stack size above 1 are
  touched, because anything that does not stack in vanilla carries per-item durability and
  quality, and collapsing several into a count throws all but one of those away silently.

### Why the defaults are low

The usual version of this mod ships x10 stacks and halved weight, which does not make
Valheim more convenient so much as remove a system from it. The tedium and the difficulty
are the same mechanic seen from two sides, so the defaults here fix the annoying half and
leave the interesting half alone. Everything is a knob if you want the easy version.

### Correctness

- **Both `ObjectDB.Awake` and `ObjectDB.CopyOtherDB` are patched.** `Awake` builds the
  database at startup and `CopyOtherDB` rebuilds it when a world loads, so patching only the
  first means untouched values quietly replace yours the moment you join a game.
- **Every value is computed from the item's original**, captured the first time it is seen
  and never overwritten. The database is set up more than once, and a mod that multiplies
  whatever it currently finds squares its own multiplier on the second pass.
- Loads on dedicated servers, and declares to Core's version gate so a host and a client
  cannot disagree about stack sizes.

### Known limits

- If another mod changes an item before this one first sees it, that changed value is what
  gets recorded as the original.
