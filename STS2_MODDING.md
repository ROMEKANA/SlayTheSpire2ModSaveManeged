# Slay the Spire 2 MOD engineering notes for Codex

Read this together with the root AGENTS.md. This file contains STS2-specific evidence and constraints, not a universal requirement to add save routing to every MOD. Human setup/troubleshooting commands belong in README.md.

## Scope and confidence

These notes come from UnifiedVanillaSave development against user-supplied v0.113.1 source and metadata. After the 0.3.0 correction, the owner reported that the MOD worked. The agent did not run the game. The owner did not enumerate every profile, multiplayer combination, Cloud scenario or future-version condition, so those are not certified by that report.

Re-check the installed game's runtimeconfig, version and target methods after updates. Older tutorials and this case study are evidence for their versions, not stable API guarantees.

## Runtime, dependencies and loader

- The investigated installation uses .NET 9, sts2.dll, game-bundled 0Harmony.dll and GodotSharp.dll under data_sts2_windows_x86_64. Confirm actual locations and target framework rather than assuming the folder/framework will never change.
- global.json may select a 9.x SDK even when 10.x is installed. The SDK major must match the project's selection policy. Installing the supported SDK alongside a newer one is valid.
- Reference game assemblies through configurable paths and Private=false. Do not commit or ship them. Keep the full production MOD dependent on actual game references; do not make fake game types pass as a production build.
- This utility uses the standard ModInitializer and bundled Harmony, without BaseLib/RitsuLib. Other MODs may legitimately need libraries; decide from their requirements rather than copying a universal ban.
- Use explicit initialization for compatibility checks, backups and logging. The loader's Loaded/enabled display can coexist with initializer failure. Record explicit initialized/inactive state, then verify the feature separately.
- Keep the external manifest identity and payload basename aligned. Set has_dll/has_pck to actual shipped payloads. A code-only utility needs no dummy PCK. Do not leave old manifests in a renamed folder under mods: discovery may still find them.
- Use affects_gameplay=false only for an appropriate local utility. Content, balance or deterministic gameplay changes require a separate decision and multiplayer testing. Never spoof global MOD state to bypass compatibility checks.
- Avoid a blanket using Godot when System.Environment is used; alias Godot.ProjectSettings or qualify System.Environment to avoid the observed CS0104 collision.
- Validate APIs against the bundled Harmony. UnpatchSelf was absent in the investigated DLL. Owner-scoped Unpatch on exact targets was compatible; do not globally remove other MODs' patches.

## Local workflow and diagnostics

Apply the generic setup/reporting contract from AGENTS.md. For STS2, the standard path roles are:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2
%APPDATA%\SlayTheSpire2
%LOCALAPPDATA%\<ModId>
```

Generate actual values in ignored local/paths.txt; do not hardcode a real username. Keep config, prior releases and backup payloads outside the loader's mods tree. Let the installer report protected-directory write errors specifically, rather than assuming all users need elevation.

The investigated .NET 9 environment could not install the latest ILSpyCmd tool-package format. ILSpyCmd 9.1.0.7988 with --allow-roll-forward was installed and exercised. Pin a compatible version, use --disable-updatecheck in repeated exports, and pass the game dependency directory as referencepath. Re-evaluate the pin when the toolchain changes.

Prefer one source-export script driven by the paths file. Include the actual game DLL hash, target implementations and direct callers in an ignored UTF-8 report; do not ask for a game DLL or a screen of wrapped console output when text will suffice. Check the DLL fingerprint before/after the export. Do not commit the generated proprietary source.

Useful local artifacts in this repository include save-sources.txt and routing logs. The source exporter is read-only; installation remains a separate Setup.ps1 action. Other MODs should collect their own relevant types rather than inherit this utility's seven-type list blindly.

## Save-routing case study: follow the actual storage keys

The initial wrapper patch and the later absolute-path patches loaded but did not unify progress. Supplied caller/source reports established why:

- UserDataPathProvider.GetProfileDir(int) is a short wrapper around GetProfileDir(int,bool?). Several save managers call the two-argument overload directly.
- SaveManager.ConstructDefault creates the local backend at GetAccountScopedBasePath(null), then may wrap it in CloudSaveStore. That account root itself has no modded component.
- Managers pass relative keys to ISaveStore. ProfileSaveManager uses GetAccountDir(bool?) for profile.save. Progress, prefs, run and history factories use GetProfileDir(int,bool?).
- InitProfileId reads profile.save and LastProfileId before logging GetProfileScopedBasePath. Correcting that log path does not correct an earlier profile selection or the managers' independent read/write keys.
- Therefore a correct-looking absolute-path log was not evidence of save I/O using that path. Inspect factories, actual consumers, constructors and current-profile selection before patching.

For the reviewed implementation, 0.3.0 patches exactly GetAccountDir(bool?) and GetProfileDir(int,bool?). Only null forceModState arguments become false, and the original methods run. Explicit false/true probes keep their meaning. IsRunningModded is neither assigned nor patched. This covers the supplied normal relative routes without copying data or constructing replacement filenames.

Do not copy these patches into an unrelated MOD. If implementing a similar utility, re-audit the current source and contracts, preserve explicit legacy probes, and verify the real routing after initialization. Tiny wrappers/inlining, cached early paths and overload bypass are separate failure hypotheses; do not claim one is proven just from a method name.

## Persistence and multiplayer boundaries

- Back up active user:// data before changing routing; preserve both vanilla and modded namespaces. Keep the first snapshot immutable and validate its receipt/hash before reuse.
- Do not silently migrate, merge or synchronize saves. Existing game Cloud behavior is distinct from MOD-added synchronization.
- Steam's userdata/<account>/2868840/remote cache is separate from the active game user-data root. Do not infer which is authoritative just from directory names or timestamps.
- Redirecting saves does not make custom-model data readable after its providing MOD is removed. Preserve serialization/model IDs and test content compatibility independently.
- Test vanilla-to-MOD loading and MOD-to-vanilla progress persistence in fresh processes, including profile selection, suspended solo/multiplayer runs, preferences and history. Confirm real file keys/locations, not just hook logs.
- Multiplayer needs both host roles, one/both/no utility users, and matching/mismatched gameplay MODs. Preserve original mismatch detection. The owner's general success report is not a multiplayer certification.

## Tests and transfer

Keep pure metadata/policy tests and shared patch-code tests independent of the game when Cloud has no installation. This project exercised its production patch engine with test-only Lib.Harmony 2.4.2, fixture path factories and a key-value store under default/optimized JIT, plus Linux/Windows CI. The production MOD still uses the game's Harmony; never stage the test package as a game dependency.

Test default and explicit nullable-state inputs, warm callers, last-profile selection, all affected path categories, real read/write round trips where available, global-state preservation, old-namespace retention, conflicts and owner-scoped cleanup. Fixture success, actual-game compilation and owner acceptance are different outcomes.

For a new STS2 repository, copy AGENTS.md and this file to its root, merge with existing rules, and have Codex create project-specific scripts/contracts. Do not transfer this MOD's identity, reviewed DLL fingerprints, audit config, local paths, user data or backup payloads as ready-made approvals. This repository's detailed RESEARCH/VALIDATION notes are historical project evidence, not additional portable instruction files.
