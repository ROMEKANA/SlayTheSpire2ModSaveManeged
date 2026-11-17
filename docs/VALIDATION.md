# クラウド検証記録

2026-11-14 / Codex Cloud Linux。実ゲームDLLは未導入。

使用SDK: .NET 9.0.321（global.jsonの9.0.203 + latestFeature規則で選択）。ゲーム実体のTFMは未確認。

実施結果:

- `bash scripts/test.sh`: 11/11テスト成功。CoreとSaveAuditのビルド成功、警告0・エラー0。
- バックアップ内のバニラ/modded両ツリーの保持、初回バックアップの非上書き、元セーブ不変更、破損・未完了・異なるルート・入れ子・欠落ソース・symlink拒否を確認。
- 監査未承認、DLLハッシュ不一致、IL不一致の拒否を確認。対象完全一致の受理はテストfixtureのみで確認。これは実ゲーム型の存在を証明しない。
- `SaveAudit inspect <テストfixture DLL>`: メタデータの候補シグネチャ `00010E08`、DLLとILのSHA256出力を確認。ゲームDLLを読み込んだ結果ではない。
- `dotnet build src/UnifiedVanillaSave -c Release`: `Missing sts2.dll` の明示エラーで停止、警告0。**MOD本体のビルド成功ではない。**
- `git diff --check`: 問題なし。

未実施:

- 実STS2 DLL/同梱Harmony/Godot参照でのMOD本体ビルド。
- Windows PowerShellでのスクリプト実行。スクリプトはソース確認のみ。
- 実ゲームの対象シグネチャ・ILの意味・呼び出し元・初期化順序/インライン化の監査。
- 実ローダーによる初期化・競合検出・バックアップ・読み書き先の検証。
- `affects_gameplay:false` の現行ゲームでの動作、ホスト/クライアント互換性。

これらはREADMEとRESEARCH.mdのローカル手順で確認する。現行バージョンへの適合性および動作成功は未確定。

## Windows導入時のエラーへの修正

.NET 10 SDKのみがある環境でのSDK解決エラーと、`Install.ps1` の `Unexpected GameDir` 報告に対応。

- global.jsonの.NET 9指定は維持し、READMEに9 SDKの並行インストールと `Build.ps1` 利用を明記。
- Build.ps1にSDK解決失敗時の導入案内を追加。
- Install.ps1に確認した絶対パスと修正方法を表示し、別のゲーム内DLLフォルダを `-GameDataDir` で指定可能にした。指定先がゲームルート外なら拒否し、コピー対象は宣言した2ファイルに限定。
- Linux / PowerShell 7.5.2で両スクリプトの構文解析に成功。一時フォルダとテキストのダミーファイルで、標準配置/別配置のコピー、存在しないルート/DLL欠落/重複インストール/ゲーム外データフォルダ/成果物欠落の拒否を確認。
- SDK解決コマンドの失敗を模擬し、.NET 9の導入案内が出ることと作業ディレクトリが復帰することを確認。

これらはインストール処理と案内の検証であり、Windowsでの実行や実ゲームDLLでのビルド成功を示すものではない。


## 実DLL参照ビルドの報告への修正

Windowsでユーザーが .NET SDK 9.0.321 にて独立テスト11/11通過と実DLLのメタデータ取得を実施。その後、MOD本体のコンパイルで `Environment` のGodot/System間の名前衝突4件と、同梱Harmonyの `UnpatchSelf` 欠如1件を報告。

- `using Godot` を `Godot.ProjectSettings` だけの型エイリアスに変更し、System.Environmentとの名前衝突を除去。
- ロールバックは `Harmony.Unpatch(target, HarmonyPatchType.Prefix, Owner)` に変更。対象メソッドの自ownerのprefixだけを解除し、他MODのパッチは解除しない。
- GetProfileDirが複数オーバーロードを持つメタデータが得られたため、監査ツールに候補/別オーバーロードの区別とconfigへ使うハッシュの明示を追加。fixtureにもオーバーロードを追加して正確な引数型による選択を検証。

ユーザー報告で確認された候補のメタデータ:

- Assembly SHA256: `A6CC282C52B96AD2E01D5692B1144B33AA8E3BA4E969582F648ECB13B2F751D7`
- `static string GetProfileDir(int32)` のIL SHA256: `5B3EAA045B8EB89BBF692C2468B1FA749090D8706173227691C7775248808687`
- 別オーバーロードの署名blob: `00020E08151181950102`。このメソッドには現実装のパッチを適用しない。

これらのハッシュはユーザーのDLLを識別する記録であり、互換性の承認や初期設定値ではない。単一引数版のILには別メソッド呼び出しがあり、calleeと全callerの監査が必要。二引数版を直接使う保存先処理があれば現在のパッチでは共通化を保証できない。ゲームDLL自体はクラウドにないため、修正後の実DLL参照ビルド・起動成功はユーザー側での再確認が必要。


## 初期化後も統合されない報告への診断追加

ユーザーから、MODなし→ありの既存進行読み込みと、あり→なしの変更反映の両方に失敗したとの報告。初期化成功と保存先統合成功は区別する。この時点では実DLLコードも実際の呼び出しログも未取得のため、原因は未確定でありパッチ対象を推測で拡張していない。

- 0.1.1では `ACTIVE` を `PATCH_INSTALLED` に改め、適用成功だけであると明示。単一引数版の最初の呼び出しをprofileごとに最大16種類まで `PROFILE_ROUTE` として観測。MOD DLLのパス/ハッシュ、ゲームDLLのパス/MVIDも記録。
- `SaveAudit paths <sts2.dll>` を追加。ゲームをロードせず、保存先プロバイダ全メソッドの名前付きIL/文字列/メタデータトークン、外部の型からの直接参照を表示。セーブにはアクセスしない。反射や間接呼び出し等の網羅性は保証しない。
- Linux / .NET 9で13/13独立テスト成功、Core/SaveAuditビルド成功。両オーバーロードへの直接参照と文字列の表示、型がないDLLの拒否をfixtureで確認。
- 最新ILSpyCmdのツール設定形式が.NET 9 SDKで拒否された報告に対し、固定版9.1.0.7988 + --allow-roll-forwardでのインストール、起動、fixture逆コンパイルをLinux / .NET 9で確認。

この変更は原因調査用。実ゲームDLL参照での0.1.1本体ビルド、実機の呼び出し観測、保存先統合成功は未確認。実動作を直したという検証結果はない。

## 0.2.0 correction based on supplied source

The user provided the v0.113.1 provider source and runtime logs. The single-argument method is a short wrapper; two final builders complete normal profile paths.

- Replace the wrapper prefix with postfixes on GetProfileScopedPath and GetProfileScopedBasePath. Remove only the account-level modded component from the completed result.
- Keep explicit GetProfileDir(id,true) probes, global mod state, platform/user/profile/dataType unchanged.
- Validate both targets before patching. Reject wrapper-only audits. path-config retains the prior config and records new IL hashes only for the same audited DLL.
- Linux/.NET 9 independent tests: 16/16 passed. Core/SaveAudit builds: zero warnings/errors. Cases cover nullable state inputs, existing vanilla paths, three platforms, multiple profiles, ulong max ID, modded inside suffixes, unexpected layouts, either target's IL mismatch, and preservation of config/audit flags/backup settings.

The game DLL has not been supplied. MOD compilation against actual assemblies, real Harmony routing, initializer order/caches/inlining, and successful bidirectional progress sharing remain unverified. Passing independent tests is not runtime acceptance.


## Automatic setup and three-line paths file

- Added Setup.ps1: creates local/paths.txt from Windows defaults, builds, generates/migrates config, verifies/creates the first backup, preserves the previous MOD outside mods and installs. Failed installation restores the previous directory and preserves partial files outside mods.
- The paths file has exactly three lines: game root, save root, config directory. Runtime files and resolved personal paths remain Git-ignored. Unknown DLLs require a matching prior audit or the fingerprint paired with the supplied/reviewed v0.113.1 source. No manual config/audit-flag edits are required for that build.
- Linux/.NET 9 independent checks: 19/19 passed; Core/SaveAudit builds passed without warnings/errors. Added policy rejection, fingerprint recognition, preservation and repeatability checks.
- Linux/PowerShell fixture checks cover syntax, automatic three-line generation, config migration and backup destination preservation, repeated installation, unknown-DLL rejection, rollback and malformed path files. These use text installation payloads and a test metadata assembly; they are not real-game builds or Windows acceptance tests.


## Manual feedback before 0.3.0: setup accepted, feature still failing

The owner reported that the automatic installation instructions were clear and should be retained for this and other MOD repositories. They also reported another run with no load-time error but no save unification. Record setup usability as accepted feedback; feature acceptance remains failed. No new runtime cause or successful save routing has been established by this report.

The installation pattern was originally captured in a separate playbook; its universal guidance is now consolidated into root AGENTS.md, with STS2-specific notes in root STS2_MODDING.md. This documentation change does not alter or claim to fix runtime behavior. The next investigation requires current-build routing/initialization logs and actual save behavior, not another installation redesign.

The subsequently supplied 0.2.0 log confirms the final-builder DLL was loaded, both targets passed their audits, and GetProfileScopedBasePath returned a corrected vanilla path for profile1. It contains no GetProfileScopedPath routing observation. This narrows the evidence to a corrected base-path return, not actual save reads/writes. Alternate callers, inlining, pre-initialization caches and profile selection remain hypotheses. Raw account IDs and user paths are intentionally omitted from this record.


## Static caller report identifies the missing save routes

The supplied caller report confirms that progress, run, history and preferences managers call GetProfileDir(int,bool?) directly. ProfileSaveManager calls GetAccountDir(bool?) and SaveManager has additional direct GetProfileDir(int) callers. Provider GetProfileScopedPath is used by profile UI/debug code; the base helper also serves initialization/migration/deletion. The two 0.2.0 targets are not sufficient to cover save construction. No raw user paths/account IDs are retained here.

Added Export-SaveSources.ps1 to collect the seven relevant implementations using the existing three-line paths file and pinned ILSpyCmd, without executing the game or changing saves/config/installations. Runtime patch behavior is unchanged pending source evidence.


Linux/PowerShell 7.5.2 checks for the source exporter passed: script syntax, a real pinned ILSpyCmd export against the test fixture DLL via the three-line paths file, previous-report preservation, and missing-type failure without replacing a valid report. Fixture DLL/save/config markers and installation state were unchanged. This is not a decompilation of the actual game DLL or a functional patch test.


## 0.3.0: route the shared relative selectors

The newly supplied seven-type source report has the same reviewed v0.113.1 assembly fingerprint. It establishes that SaveManager creates an account-root backend, while the five managers supply relative keys. ProfileSaveManager.LoadProfile reads profile.save before InitProfileId logs the absolute base. The 0.2.0 absolute-path correction therefore did not cover actual account/profile file keys.

The implementation now patches only GetAccountDir(bool?) and GetProfileDir(int,bool?). It changes null forceModState arguments to false and runs the original body; explicit true/false and IsRunningModded are unchanged. Old absolute-helper audits are rejected and Setup automatically replaces their hash map. Source files supplied by the user are not committed.

Verification performed in Cloud on Linux/.NET 9:

- Core/policy/metadata/backup tests: 16/16 passed; obsolete absolute-string-rewrite tests were removed with that implementation.
- Real Lib.Harmony 2.4.2 checks sharing the production patch code: 8/8 passed under default JIT and 8/8 with tiered compilation disabled. The model warms factories before patching, reads vanilla last-profile state, exercises all relative key categories, explicit overrides, a vanilla/modded round trip, preservation of old modded keys/global state, late conflicts and owner-scoped cleanup.
- Core/SaveAudit builds completed with zero warnings/errors.
- The PowerShell setup fixture suite passed with the new selector hash map; setup command and three-line path format are unchanged.
- CI is configured to run the independent suites on Linux and Windows. Do not infer Windows results until the corresponding run completes.

The test-only NuGet Harmony does not replace the game-owned assembly in production. The entire MOD has not been compiled here against the actual game DLL, and real startup/I/O/Steam Cloud/multiplayer acceptance remains unverified. These are regression tests for the supplied contracts, not a report of game success.


## Owner-reported 0.3.0 success and portable instruction consolidation

On 2026-11-18 (Asia/Tokyo), the owner reported that the MOD worked after the 0.3.0 update. Record this as user-reported real-game success, not an agent-run game test. The exact coverage of all profiles, multiplayer, Cloud states and future versions was not enumerated. Previous failed attempts remain historical evidence.

Universal automation/research/reporting/permission-troubleshooting guidance is consolidated into root AGENTS.md. Root STS2_MODDING.md holds the game-specific contracts and lessons. The old docs/MOD_INSTALLATION_PLAYBOOK.md was removed to avoid conflicting portable guidance. README now identifies the two transfer files, documents scoped PowerShell permission remedies and gives text-report commands. No runtime or installation implementation changed in this documentation task.

Documentation checks passed: all local Markdown links and code fences, both root transfer files, absence of the old duplicated playbook, and single-command formatting/syntax for all 28 README PowerShell blocks. Permission remedies were parsed, not executed; no Windows execution policy or ACL was changed by the agent.
