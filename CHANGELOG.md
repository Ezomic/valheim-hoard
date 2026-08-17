# Changelog

Notable changes to Hoard. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

Nothing here has been released yet. The numbers below are development builds, and **1.0.0 is
reserved for the first version that has actually been played and published** — a 1.0 asserts
a mod works in a game, not merely that it compiles and loads.

## [0.11.0] — 2026-08-17

### Every later boss adds ten percent

A boss now raises the group it is assigned **and** every group unlocked before it, by
`ProgressionStep` — ten percent, compounding. Building unlocked at Eikthyr is 2x, 2.2x once
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
here has had two keys down at once — the unlock path and the retune when a key arrives are
both verified live, the ten percent is verified only by reading it.

## [0.10.1] — 2026-08-17

### The table is shorter on purpose

Ammo, food and the catch-all group lost their tiers, and trophies moved to Bonemass. Three
entries remain: Eikthyr raises building material, The Elder the crops, Bonemass the metal
and the trophies.

A group with no entry stays at vanilla forever, and most of the game's stackables are meant
to. A mod that eventually doubles everything is a slower version of the one that doubles it
on day one; what is left here is the hauling that is actually repetitive.

- **The portal rule now says when it lifts.** Ore read `portal-blocked`, which is a
  half-truth once a metal tier exists — it now reads `portal-blocked until
  defeated_bonemass`. Tallies match on the rule, so a qualified note still counts under it.
- **"Earned so far" lists only groups a tier can reach.** Printing the others at 1x read as
  a promise that some boss would come for them, and with this table none will.

## [0.10.0] — 2026-08-17

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
different item databases — one drops a hundred wood in a shared chest, the other opens it
holding a slot over its own maximum, and the next move writes back through the smaller
rules. Global keys are world state pushed to every client, so everyone computes the same
answer with no new networking, and since keys only accumulate the multiplier only ever
rises.

Read through the **string** global-key lookup, not the `GlobalKeys` enum: that enum stops at
`defeated_goblinking` in this build, with no `defeated_queen` or `defeated_fader` member, so
the enum route would have silently capped the ramp at the Plains.

### Utangard decides what counts, when it is installed

Utangard opens a biome only when every member of the group was personally at that boss's
death, which is not the same as the boss having died in the world. Hoard now asks it rather
than reading the raw key, so stacks never arrive for a biome Utangard still has fenced off.
Soft dependency; `DeferToUtangard = false` turns it off, and neither mod needs the other.

### Changed

- The item list gained a Group column, sorts by group, and its header counts describe the
  **state** of the database rather than the last pass — so they no longer appear to
  contradict the `Retuned n` line in the log, which counts only what that pass moved.

## [0.9.2] — 2026-08-17

### Added

- **An item list, written beside the config on every run** —
  `ezomic.valheim.hoard.items.txt`. Every item in the game, its vanilla stack size and
  weight, what Hoard made them, and which rule left it alone when it did nothing. Off with
  `WriteItemList = false`.

  `ExcludeItems` takes prefab names and prefab names are not guessable — raspberries are
  `Raspberry` and a draugr's arrow is `draugr_arrow` — so a per-item setting without a list
  beside it is a setting nobody can spell. It doubles as the answer to "why did this item
  not change", which is the only question a mod like this ever gets, and it answers it
  without asking anyone to turn on `Verbose` and read a log.

  The rows are built by the tuning pass as it runs rather than by a second walk over the
  database. A separate walk would be a second copy of the eligibility rules, and the first
  thing it would do is disagree with the real one.

### Fixed

- **Weights are written with a decimal point on every machine.** `float.ToString` follows
  the machine's locale, so on a Dutch install resin came out as `0,3` — a comma in a file
  whose other numbers come from a `.cfg` that uses points, sitting next to a column of
  integers. Both the item list and the `Verbose` log now format invariantly.

### Changed

- The pass logs three counts rather than two: changed, already at those values, and left
  alone. They add up to the item count now. `Retuned 0` on the second setup of a session
  reads like a failure until you know the other items were already at the right numbers.

## [0.9.1] — 2026-08-17

### Core is optional

- **Core is now a soft dependency rather than a hard one.** Hoard installs and runs on its
  own, and the Thunderstore package no longer drags Core in with it. Present, Core is used;
  absent, nothing here is degraded.
- What is given up standing alone is the **version gate**, not the mod. Item data that
  differs between two ends desyncs inventories, and without Core nothing reports that. The
  `ObjectDB.CopyOtherDB` patch still puts a joining client on the server's numbers, which
  covers the common case on its own. Solo, none of it applies.
- The registration call sits in its own method that is **never inlined**, because the JIT
  resolves the assemblies a method needs when it first compiles that method. A Core call
  written directly into `Awake` would drag the assembly in before the installed-check could
  prevent it, and the missing-assembly exception would land during plugin load — which is
  precisely the failure the soft dependency exists to avoid.

## [0.9.0] — 2026-08-16

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
