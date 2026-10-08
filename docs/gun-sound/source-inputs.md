# Source inputs and packaging

Base: itachitachi2-art/DroneRadiationSprayer commit 175e35f1dc62d054bd1f99935869b7e38a2c2c97, feature/display-fix1, adopted 2026-10-08.

All fetched base files used in this candidate were checked against GitHub's Git blob SHA-1 values. The adopted DLL's SHA-256 is 4d2da710330e7d2c3fc7784127713a2f5da9da4367df26297e4caa0f02a55f9f. All production C# sources, build.cmd, build.ps1 and runtime drone icon are byte-identical to that base. No rebuild was needed or performed.

The original 1.31 MB Artwork/modDroneRadiationSprayer-source.png could not be downloaded by available binary fetch tools and is omitted from the buildpack. This archival art source is not required by the runtime or C# build. It remains in the original Git commit; the runtime 160x160 PNG is included and unchanged. Do not remove the original artwork from a later repository commit. The buildpack contains every runtime/build source dependency owned by this mod; game/Harmony/compilers remain external and are not included.

The supplied local v3.3 Config XML was used as reference. Config/items.xml, Config/item_modifiers.xml, Config/sounds.xml and Config/Localization.csv are not redistributed. See reference-hashes.json for verification hashes.

The update ZIP is rooted at Mods/DroneRadiationSprayer/. It includes the unchanged adopted DLL and runtime icon plus current Config, ModInfo.xml and current usage notes. The buildpack is rooted at DroneRadiationSprayer/, includes the current update ZIP in dist, all mod production sources, existing Windows build scripts, static tests, current reports and historical adoption evidence. Earlier reports are history, not tests of the new gun-sound behavior.
