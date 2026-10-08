# Current gun-sound candidate

See GUN-SOUND-1.md and docs/gun-sound/validation.json for current checks and limits. The records below describe the earlier drone implementation, not runtime validation of the new sounds.

# 0.1.0.1 candidate validation

Date: 2026-10-07 UTC. Target: the user's supplied 7 Days to Die v3.3 references.

## Passed

- All production C# sources compiled as C# 5 against the supplied game's actual Managed assemblies and `0Harmony.dll`.
- The build used `/nostdlib+`, command-line `/noconfig`, and the same reference list as the Windows build script.
- 21 configuration/source-contract checks passed: additive XML, installed-part category, vanilla icon name, localization, native 15-second replace buff, no global entity/buff/stat patches, and matching versions.
- 106 controlled behavioral cases passed with the exact production C# sources. The game and Unity types in these cases are test doubles.
- Real Harmony 2.13.0.0 patched a controlled `EntityDrone.OnUpdateEntity` method. The original update continued to execute, the production postfix ran, and final shutdown state was observed.
- Independent source/API review found no unresolved static blocker within the documented zombie-only scope.
- Emitted assembly identity is `DroneRadiationSprayer`, version `0.1.0.1`, runtime `v4.0.30319`.
- Emitted member references include the exact native `AddBuff(string, int, bool, bool, float)` overload and `ModificationCount` / `GetModification(int)` accessors. No direct health setter, damage or revenge-target reference is emitted.

## Controlled coverage

25m boundary/epsilon/negative coordinates/vertical/diagonal/box corners; installed versus cargo parts; null and sparse mod slots; removal/reinstallation; separate drones; server and client worlds; remotely owned server drones; absent, disconnected, dead or stale owners; shutdown, pickup, unloaded and transitional drones; HP2 temporary-revival safety; all three native target tags; excluded players, animals and vultures; sleeping-target state preserved by the test double; per-second query limits; overlapping-drone deduplication; non-stacking duration; rollback of the test clock; reused entity IDs; candidate-list cleanup; failed query/buff/tag operations; rate-limited errors; real Harmony postfix execution.

## Not tested

- Launching Unity or 7 Days to Die, applying Harmony to the actual running game, or loading a real save.
- Real display, installation UI, sound/particles, sleeping-zombie reaction, targeting, and interactions with other mods.
- Actual game execution of the buff XML or multiplayer replication, including Dedicated Server.
- Excuse Me, Drone F10 integration in the running game.
- Windows Framework csc / PowerShell execution.
- Real FPS, latency and long-duration performance. Controlled query counts are not a live performance benchmark.

The native API inspection establishes the intended server-authoritative path: `World.IsRemote()` reflects server authority, and `AddBuff(..., true, ..., -1f)` uses native buff replication without changing the shared duration. It does not substitute for a multiplayer playtest.

## Reproduce the supplemental tests

Run Python 3 `tests/run-tests.py` with `--mono`, `--mcs`, `--managed`, `--harmony`, `--config`, and `--output` pointing to the relevant local paths. Use the actual v3.3 Managed and Config folders and the game's Harmony assembly. The harness references a separate Mono runtime for controlled tests. No reference DLLs are distributed here.

Detailed hashes and outputs are in `tests/validation.json`. Remaining game checks are listed in `TEST-JA.txt`.
