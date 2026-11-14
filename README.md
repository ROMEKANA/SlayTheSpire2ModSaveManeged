# UnifiedVanillaSave

Slay the Spire 2 のMODあり・なしでバニラ側のプロフィール保存先を使う、外部MODに依存しないC# MODです。標準ローダーとゲーム同梱Harmonyを使用します。保存データの移行・相互コピー・双方向同期は行いません。例外は復元用の初回バックアップだけです。

**現状は実機検証前の開発版です。** ゲームDLLなしで検証できるCoreと監査ツールを実装しています。MOD本体の実ゲームDLL参照ビルド、起動、現行版への適合、マルチ互換性は未確認です。`affects_gameplay:false` はutilityとしての暫定設定で、動作保証ではありません。[調査・パッチ候補・必須監査](docs/RESEARCH.md)を先に確認してください。

## 構成

- `src/UnifiedVanillaSave`: 標準initializer、対象を限定したHarmony prefix、競合検出、ログ。Coreのソースを含めて単一MOD DLLとしてビルド。
- `src/UnifiedVanillaSave.Core`: ゲーム非依存の監査チェックとバックアップ。
- `tools/SaveAudit`: ゲームDLLを実行せずメタデータ/ILハッシュを表示するツール、初回バックアップコマンド。
- `tests/UnifiedVanillaSave.Tests`: 外部テストパッケージ不要の独立したテストランナー。

`GetProfileDir` だけを置換し、`IsRunningModded` などのゲーム全体のMOD判定は変更しません。メソッドの契約が現行DLLと一致することはローカルで監査します。監査前・DLL更新後・バックアップ失敗・既知のHarmony競合時には有効化しません。initializerの失敗後にゲームが続行する可能性があるため、**Loaded表示だけを信用せず、ACTIVEログがなければ終了してください。**

検証済みの結果と未実施項目は [クラウド検証記録](docs/VALIDATION.md) に記載しています。

## Linux / Cloudでの検証

.NET 9 SDKを使用します。`global.json` は9.0.203以降の9.0 feature bandを許容します。ゲーム実体が別TFMの場合は `Directory.Build.props` とSDK設定を合わせて見直してください。

```bash
bash scripts/test.sh
# または
 dotnet run --project tests/UnifiedVanillaSave.Tests -c Release
 dotnet build tools/SaveAudit -c Release
```

制限環境では `DOTNET_CLI_HOME=/tmp/uvs-dotnet-home`、`NUGET_PACKAGES=/tmp/uvs-nuget` を設定できます。ゲームDLLなしのMOD本体ビルドは明示エラーで停止します。ゲーム型の代用品でビルド成功を装う処理はありません。CIもCore/監査ツールのみを対象にします。

## Windowsでビルド

.NET 9 SDK、PowerShell、ローカルのゲームを用意してください。ゲームを終了し、旧セーブ統合MODを `mods` の外へ移動します。名前変更だけで `mods` 配下に残すとmanifestが再検出される場合があります。

```powershell
$game = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2'
$data = Join-Path $game 'data_sts2_windows_x86_64'
Get-Content (Join-Path $data 'sts2.runtimeconfig.json')
dotnet --info
.\scripts\Build.ps1 -GameDataDir $data
```

標準以外の配置にも対応します。

```powershell
.\scripts\Build.ps1 -GameDataDir $data `
  -GameAssemblyPath 'D:\references\sts2.dll' `
  -HarmonyAssemblyPath 'D:\references\0Harmony.dll' `
  -GodotAssemblyPath 'D:\references\GodotSharp.dll'
```

`.csproj` にも同名のMSBuildプロパティ、`GameDataDir`、`GameDir` を指定可能です。参照は `Private=false`。成果物 `artifacts/mods/UnifiedVanillaSave/` は `UnifiedVanillaSave.dll` と `UnifiedVanillaSave.json` の2ファイルだけです。ゲームDLL・Harmony・Godot・PCKは配布もGitへのコミットもしません。

## ローカル監査・初回バックアップ・設定

1. [必須監査項目](docs/RESEARCH.md)に従ってILSpyで対象と呼び出し元を確認します。候補は `static string MegaCrit.Sts2.Core.Saves.UserDataPathProvider.GetProfileDir(int)`。シグネチャの一致だけで使用可能と判断しないでください。
2. 次の出力と、ゲーム画面のバージョン、監査結果を手元に保存します。SHA256は更新検知用で、ゲームの意味上の互換性を証明しません。

```powershell
dotnet run --project tools/SaveAudit -c Release -- inspect (Join-Path $data 'sts2.dll')
```

3. Godot `user://` の実際のルートを特定します。資料上は `%APPDATA%\SlayTheSpire2` ですが、実際のパスを確認してください。Steamアカウント配下や個別profileではなく、ゲームのuserデータ全体のルートを指定します。実行時にもGodotの解決結果と照合します。
4. **ゲームを閉じ、Steam Cloudの同期を完了/一時停止した状態**で初回バックアップを作成します。空き容量はuserデータ全体分が必要です。ルート内のバニラ側・modded側の両方を保護します。Cloudキャッシュは別途保全してください。

```powershell
$userRoot = Join-Path $env:APPDATA 'SlayTheSpire2'
$backup = 'D:\STS2-backups\UnifiedVanillaSave-first'
dotnet run --project tools/SaveAudit -c Release -- backup $userRoot $backup
if ($LASTEXITCODE -ne 0) { throw 'Backup failed' }
```

バックアップは指定先の `files/` とSHA256付き `receipt.json` に保存します。途中失敗は有効なバックアップとして公開しません。既存バックアップは検証だけ行い、以後の進行で上書きしません。ルートの内外関係、リンク/reparse point、同時バックアップ、コピー中の変更、破損、不完全な既存バックアップを拒否します。元セーブへは書き込みません。空のディレクトリは記録しません。Cloudや別プロセスを停止する機能はありません。

起動時にも初回バックアップを作成/検証しますが、起動中のログなどの変更で失敗する可能性があるため上記のオフライン作成を推奨します。バックアップを手動編集せず、別のuserルートに使い回さないでください。

5. [config.example](docs/config.example) を次の場所へコピーし、ハッシュ・絶対パス・実ゲームバージョンを入力します。呼び出し元/バニラレイアウト/初期化順序を実際に確認してから `CallersAndVanillaLayoutVerified` を `true` にします。configはMOD配下へ置きません。

```powershell
$configHome = Join-Path $env:LOCALAPPDATA 'UnifiedVanillaSave'
New-Item -ItemType Directory -Path $configHome -Force | Out-Null
Copy-Item .\docs\config.example (Join-Path $configHome 'config.json')
notepad (Join-Path $configHome 'config.json')
```

別の設定場所を使う場合は、ゲームプロセスに継承される環境変数 `UVS_CONFIG` にconfigの絶対パスを設定します。起動済みのSteamにはPowerShellの環境変数が伝わらない点に注意してください。通常は既定パスを推奨します。

## インストール・ログ・実機確認

```powershell
.\scripts\Install.ps1 -GameDir $game
# ゲームを通常どおり起動した後、別のPowerShellでログを確認
Get-Content (Join-Path $env:LOCALAPPDATA 'UnifiedVanillaSave\UnifiedVanillaSave.log') -Tail 80 -Wait
```

起動ごとの時刻と `Audit accepted`、`First backup verified`、`ACTIVE: only GetProfileDir patched` を確認します。`INACTIVE`、例外、競合owner表示があればプレイせず終了してください。ローダー自体の失敗では専用ログも出ないため、Godot/ゲームのログも `user://` のログフォルダで確認します。固定のファイル名は実機に合わせてください。

実機では次を確認し、ゲームバージョン・ハッシュ・結果を記録します。最初は退避済みのテスト用データで実施してください。

- MODなしで既存の進行/ランを確認→終了→本MODありで同じ進行を読めること。
- 本MODありで変更・保存→終了→MODなしで同じ変更を読めること。全profile、ラン中断再開、履歴、設定も確認。
- 保存時刻/ハッシュを比較し、バニラ側へ書き込み、`modded` 側を変更していないこと。コピーや同期による一致でないこと。
- グローバルMOD状態、ゲームプレイMOD一覧、実績等の元の判定が維持されること。
- 旧統合MODなどとの競合、壊したテスト用バックアップ、ゲーム更新時のハッシュ不一致でACTIVEにならないこと。
- [マルチ確認表](docs/RESEARCH.md)の接続・再接続・保存再開を両ホスト役で確認すること。

他MODの独自カード等を含むランをバニラ側に保存すると、そのMODを外したゲームで読めなくなる場合があります。本MODはセーブ形式や独自コンテンツの互換性を変換しません。プロフィール共有が適切なMOD構成で使用してください。

## 無効化・復元

ゲームとCloud同期を停止し、`<game>\mods\UnifiedVanillaSave` をmodsの外へ移動すれば無効化できます。セーブの逆移行は行いません。以後、他のゲームプレイMODがある場合は通常のmodded側に戻る可能性があります。

初回状態に戻す場合は現在のuserデータ全体を別の場所へ退避し、空の元ルートへバックアップの `files/` の**内容**を復元します。上書きマージは新しいファイルが残るので避けてください。復元は初回以降の進行を戻します。

```powershell
# ゲーム終了・Cloud同期停止後。$userRoot/$backupを再確認して実行。
$quarantine = $userRoot + '.before-restore-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
Move-Item -LiteralPath $userRoot -Destination $quarantine
New-Item -ItemType Directory -Path $userRoot | Out-Null
Get-ChildItem -LiteralPath (Join-Path $backup 'files') -Force | Copy-Item -Destination $userRoot -Recurse -Force
```

Cloudキャッシュ `<Steam>\userdata\<AccountId>\2868840\remote` は本MODのバックアップ対象外です。実ファイルを別途退避し、Steamが新しいコピーを再適用しないようCloudの復元競合を確認してから同期を再開します。自動同期・自動削除・自動復元は実装していません。
