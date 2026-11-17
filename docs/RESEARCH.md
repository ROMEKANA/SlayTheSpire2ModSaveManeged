# Save routing contract for 0.3.0

For the consolidated project specification, mechanism and failed approaches, start with [DESIGN_AND_LESSONS.md](../DESIGN_AND_LESSONS.md).

## Evidence and the previous failure

The user provided the v0.113.1 provider, SaveManager, and Profile/Prefs/Progress/Run/RunHistory manager source exported from assembly SHA256 `62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3`. The game DLL itself is not available in Cloud. Generated game source and raw user logs are not committed.

SaveManager constructs its local backend at `GetAccountScopedBasePath(null)` and may wrap it in the game's CloudSaveStore. This base has no modded component. The actual read/write operations pass relative keys to that backend:

| Data | Path factory | Shared selector |
| --- | --- | --- |
| Last selected profile | ProfileSaveManager.GetProfileSavePath(bool?) | GetAccountDir(bool?) |
| Preferences | PrefsSaveManager.GetPrefsPath(int,bool?) | GetProfileDir(int,bool?) |
| Progress | ProgressSaveManager.GetProgressPathForProfile(int,bool?) | GetProfileDir(int,bool?) |
| Current solo/multiplayer run | RunSaveManager.GetRunSavePath(int,string,bool?) | GetProfileDir(int,bool?) |
| Run history | RunHistorySaveManager.GetHistoryPath(int,bool?) | GetProfileDir(int,bool?) |
| SaveManager path/deletion/temporary cleanup | GetProfileDir(int) wrapper | GetProfileDir(int,bool?) |

Normal reads and writes omit forceModState. Explicit overrides exist for probing a particular save namespace. In InitProfileId, the profile.save record determines LastProfileId before the absolute base path is logged. Correcting that later log path in 0.2.0 did not correct the profile.save read or the other relative read/write keys. There is no stored per-manager profile path in the supplied constructors; factories evaluate the current profile ID at use time.

The old single-argument wrapper missed direct calls to the two-argument overload. The 0.2.0 absolute helpers missed the actual manager factories. Neither loader success nor the observed absolute-base correction established successful I/O.

## Two routing targets

Patch exactly these source-confirmed static string methods on UserDataPathProvider:

- GetAccountDir(bool? forceModState)
- GetProfileDir(int profileId, bool? forceModState)

Prefixes change only a null forceModState argument to false, then run the original method. Explicit false and explicit true are unchanged. Do not write IsRunningModded or patch its accessors. Do not replace save contents, serializer methods, backend objects, Cloud synchronization, or gameplay/network detection.

The account selector covers profile.save, which is necessary for matching the selected vanilla slot. The two-argument profile selector covers direct manager calls and the one-argument wrapper. Applying both avoids depending solely on interception of a tiny inner helper. The observed factory contracts make these two common path decisions sufficient for the supplied normal routes; arbitrary unrelated MOD backends are not covered.

A read-only postfix logs the first bounded set of default requests and their returned relative paths. Empty account component means profile.save is directly under the account base; profileN means saves/history/run keys use the vanilla profile component. This is still not a file-I/O completion log.

## Validation and installation

Resolve exact static/string/parameter contracts and both IL hashes before installing either patch. Old wrapper/absolute-helper-only hash maps cannot enable these targets. Existing one-command Setup regenerates the selector hashes automatically, retaining first backups, config backups, directories and explicit user settings. Unknown DLLs still require a matching prior audit or the fingerprint paired with the supplied source.

The patch implementation is shared with a separate test executable using NuGet Lib.Harmony 2.4.2. The production project continues to reference game-owned 0Harmony.dll and stages no NuGet Harmony payload. The independent tests use small contract fixtures and a key-value store, not proprietary game code or a substitute production game assembly. They exercise warmed callers, profile selection, all manager path categories, explicit overrides, round trips, global state preservation, late conflicts and owner-scoped cleanup under default and optimized JIT.

These checks do not prove compatibility with the exact bundled Harmony, actual startup ordering, all JIT inlining/cached callers, Steam Cloud, real serializers or multiplayer. Compile against the real assemblies and repeat both vanilla/modded acceptance directions, for all profiles, on the local PC. If the new relative-route logs never appear or acceptance still fails, inspect the actual callers/initialization rather than spoofing global mod state.

## Conflicts and failure handling

Check other Harmony owners on GetAccountDir, both GetProfileDir overloads, both old absolute helpers and IsRunningModded accessors before/after install and on each prefix. Remove only this owner's hooks on failure. Rollback errors are logged and require game exit. Other classes, non-Harmony detours and changes suppressing these hooks are not exhaustively detected. Remove previous unifiers outside mods.

## Manifest and multiplayer

Keep affects_gameplay=false because the MOD changes only local save-namespace selection, not rules/wire data. Persistence compatibility and the current loader/network interpretation still require real acceptance. min_game_version=0.113.1 describes the reviewed source, not a guarantee for future builds.

Test neither/one/both peers with the utility and matching/mismatched gameplay mods. Swap host/client and exercise join/reconnect/suspend/reload. Do not bypass original gameplay-mod mismatch checks. The MOD invokes no extra Cloud synchronization or copy/migration process; existing game storage behavior continues with the selected relative keys.

## References

- [SLS2Mods e78c2e2](https://github.com/luojiesi/SLS2Mods/tree/e78c2e29efcddf60ce9fe30529312f7261102450): investigated, not copied. Its global IsRunningModded spoofing is not adopted.
- [Modding-Tutorial 99905ed](https://github.com/fresh-milkshake/Modding-Tutorial/tree/99905eda3b026e0baffc93a894038b178fd493ee): .NET 9, standard initializer, manifest conventions, exact patch contracts, Cloud-cache distinction and misleading Loaded status. Its v0.107.1 scope does not override the supplied v0.113.1 source.


## Subsequent owner acceptance

After 0.3.0, the owner reported successful operation. This is manual acceptance reported by the owner; the agent did not run the game and no complete multiplayer/Cloud/profile matrix was claimed. Portable game lessons now live in root STS2_MODDING.md, while this file retains project-specific research evidence.
