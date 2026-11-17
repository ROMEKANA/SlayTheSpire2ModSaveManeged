# Save routing evidence and the 0.2.0 patch contract

## Evidence

The user supplied decompiled `UserDataPathProvider` source from v0.113.1. The game DLL itself is not available in Cloud.

- `GetProfileDir(int)` is a short wrapper calling `GetProfileDir(int, null)`.
- The two-argument overload returns `GetAccountDir(forceModState).PathJoin($"profile{profileId}")`.
- `GetAccountDir(bool?)` chooses an empty string or `modded` using the explicit argument, falling back to `IsRunningModded`.
- Normal profile paths are completed by two static methods returning string:
  - `GetProfileScopedPath(int profileId, string dataType, PlatformType? platformOverride = null, ulong? userIdOverride = null)`
  - `GetProfileScopedBasePath(int profileId, PlatformType? platformOverride = null, ulong? userIdOverride = null)`
- Both select platform/user via `PlatformUtil` and construct `user://{platform}/{id}/{GetProfileDir(profileId)}`, optionally followed by dataType.
- `GetAccountScopedBasePath` has no modded branch. Explicit legacy/profile-directory probes are separate methods.

The user's runtime logs identify game 0.113.1 and assembly SHA256 `62ECBE91093E1FA862DE4329AAC865209DCB297804CAF0F0520D92D8C0B4A9C3`. The wrapper patch installed, but both vanilla-to-modded and modded-to-vanilla acceptance tests failed.

Inlining or direct-overload callers could explain the failure. Neither has been confirmed by runtime/JIT tracing. Patch installation is not evidence of successful routing.

## 0.2.0 targets

Patch only the two final path builders above with postfixes. Preserve the original platform, user ID, profile and dataType; remove only the account-level `modded/` segment. Do not patch global mod state, models, RNG, achievements or networking. Leave both GetProfileDir overloads, GetAccountDir, explicit forceModState=true probes, account-scoped paths and legacy paths alone.

Patching the two-argument GetProfileDir to always return vanilla would also redirect explicit probes for existing modded data. Patching final normal paths avoids that behavior and the dependency on intercepting the short wrapper.

`ProfilePaths.Vanilla` validates `user://{steam|default|editor}/{ulong}/{modded/}profile{matching ID}` boundaries. It does not replace modded inside dataType or match a different profile number. Existing vanilla paths are unchanged. Unexpected layouts throw rather than receiving a guessed rewrite.

Resolve exact static/string/parameter contracts and both IL hashes before installing either patch. Reject legacy configs containing only the wrapper hash. `SaveAudit path-config` records new hashes using metadata only when the DLL matches the existing assembly audit. It preserves audit flags, roots and backup settings and retains the old config as a .bak file.

## Remaining runtime checks

1. Verify the installed game version and runtimeconfig match the audited DLL and .NET target.
2. Confirm both method bodies match the supplied source and relevant profile reads/writes/history/settings reach them. `SaveAudit paths` lists provider IL and direct references; reflection and indirect calls are not exhaustively covered.
3. Observe PROFILE_ROUTE before/after values and actual vanilla-side file writes. Repeat both acceptance directions with the mod removed/reinstalled between processes.
4. Check for paths cached before initialization or callers that inline/bypass the final builders. Missing route logs or failed acceptance still require further investigation.
5. Distinguish active user:// saves from Steam Cloud caches. Do not synchronize them. Preserve both vanilla/modded trees and the first backup.

No real-DLL MOD compilation, game launch, file I/O acceptance or multiplayer test has been performed in Cloud. Fixture/pure-function tests do not establish game compatibility.

## Conflicts

Check other Harmony owners on both final builders, both GetProfileDir overloads, GetAccountDir, and IsRunningModded accessors before/after installation and on each postfix invocation. On partial-install failure, remove only this owner's postfixes on the two targets.

This does not exhaustively detect patches on other classes, direct file writes, non-Harmony detours, later changes that suppress the postfix, or concurrent mutation. Remove previous save unifiers outside mods. A loader may still show Loaded after initializer failure; INACTIVE/errors require exiting the game.

## Manifest and multiplayer

Keep affects_gameplay=false: routing is a local utility and changes neither rules nor wire data. Persistent-data compatibility and the current loader/network interpretation still need real testing. min_game_version=0.113.1 reflects the supplied source, not a guarantee for later versions.

Test no mod on either peer, this mod on one peer, both peers, matching gameplay mods, and mismatched gameplay mods. Swap host/client and exercise create/join/reconnect/suspend/reload. Mismatched gameplay-mod acceptance must remain the same as without this utility; do not spoof global mod state to bypass checks.

## References

- [SLS2Mods e78c2e2](https://github.com/luojiesi/SLS2Mods/tree/e78c2e29efcddf60ce9fe30529312f7261102450): patches GetProfileDir and both IsRunningModded accessors. No explicit license was found; it was investigated, not copied. Global state spoofing was rejected.
- [Modding-Tutorial 99905ed](https://github.com/fresh-milkshake/Modding-Tutorial/tree/99905eda3b026e0baffc93a894038b178fd493ee): docs/03 describes .NET 9, initializer and manifest rules; docs/05 exact targets and re-audits; docs/08 profile layouts, separate Cloud caches and misleading Loaded status; docs/09 utility-only affects_gameplay=false. Its v0.107.1 scope does not override the user's v0.113.1 source.
