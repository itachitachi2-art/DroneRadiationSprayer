# Build and package evidence

Use the repository Windows build script with a real 7DTD v3.3 installation and its 0_TFP_Harmony. The supplementary Mono build in current-build/verification.json records every production source hash, actual reference hash, command and compiler limitation; current-build/actual-v33.rsp records the precise invocation. These reference binaries are external inputs and are not redistributed.

The shipped DLL hash is recorded in package-verification.json. For user-game-tested mods, retain the supplied DLL; the supplementary rebuild establishes source/API compilation and is not substituted for the tested DLL. RoofBuilder1.1.0.5 ships the newly built repair candidate.

The dist ZIP uses Mods/<existing installed folder>/ and is an overlay: users copy it over their existing installation without compiling or deleting folders. Existing audio, animation, settings and unchanged roof icon remain installed. These update packages are not complete fresh installations for the media-dependent mods. Sprayer is complete for a new installation with the normal Harmony prerequisite.

No main merge or Release publication. No live game or Windows compiler execution in this session. Historical supplied verification records are retained, with user validation and current test results recorded separately.

Reproduce the supplementary compile using tools/verify_actual_v33.py with --managed, --harmony, --mono, --compiler, --mono-config, --output and --assembly-name. Set --source-dir to Scripts, src/DonChanZombieRadar or src/TelemetryProbe as appropriate; Probe also requires --additional-source-dir shared. Mono must use matching class libraries and configuration. Source and reference hashes in the saved verification are the accepted inputs.
