# Changelog

Notable changes to Hoard. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

Nothing here has been released yet. The numbers below are development builds, and **1.0.0 is
reserved for the first version that has actually been played and published** — a 1.0 asserts
a mod works in a game, not merely that it compiles and loads.

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
