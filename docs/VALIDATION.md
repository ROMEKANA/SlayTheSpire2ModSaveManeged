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
