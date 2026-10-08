# Candidate publication verification (2026-10-08)

Prepared for `feature/gun-sound-parts` (not yet committed or pushed) from `feature/display-fix1` commit `175e35f1dc62d054bd1f99935869b7e38a2c2c97`. This remains an in-game-test candidate, not an accepted release. No main merge or Release publication.

The original candidate artifacts were recovered byte-for-byte. Their 19 source/document/runtime-pack additions and updates are preserved without deletion. A separately named path-normalized buildpack is included in `dist/`; the original buildpack is excluded because historical logs contain internal absolute build paths. Only five historical evidence files have their build-directory prefix normalized, and the original artwork is restored. Source code, configuration, DLL and runtime PNG bytes are unchanged. The original artwork remains in the repository although it was omitted from that historical buildpack. Production C# sources, adopted MOD DLL, runtime icon, ModInfo, and build scripts remain identical to the baseline. No game, Harmony, compiler, or reference binaries are added.

## Artifact SHA-256

- Update ZIP: `4d90d337b640fe9e8f5a55a46813265b2b122bed44223a4336f07ad3786448d1`
- Original buildpack (excluded): `e53ea153c9373c394dd424d8e9451b09e3f3a12f26911522620582109656a87d`
- Path-normalized buildpack: `b9ac9f66c02c72c92e956b6c96575444be5ad51a340d140c8e8bb33bcad3b3f9`

Both archives passed ZIP CRC checks. Every runtime package member matched its recorded SHA-256 and the corresponding repository file.

## Checks rerun for publication

The supplied v3.3 reference configuration matched all four hashes in `reference-hashes.json`. The following existing checks were rerun successfully against those exact inputs:

- `tests/check-config.py`: 21 configuration/source contract checks
- `tests/check-display.py`: 21 display checks
- `tests/check-gun-sounds.py`: 134 static checks
- `git diff --check`: clean

The reference game configuration is used locally for validation and is not redistributed. Earlier recorded compile/harness results describe the baseline and do not establish runtime validity of the new sound attachments.

## Still untested

In-game sound playback and loops, suppressor versus sound-attachment priority for different installation/slot orders, installation UI, save/reload behavior, and multiplayer behavior remain untested. Follow `GUN-SOUND-1.md` for the manual test matrix. No new DLL compile is necessary for these XML/localization-only additions, and none was performed for publication.
