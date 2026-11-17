# UnifiedVanillaSave

Slay the Spire 2のMODあり・なしでバニラ側のプロフィール保存先を使用する、独立したC# MODです。標準ローダーとゲーム同梱Harmonyを使い、BaseLib/RitsuLibには依存しません。セーブの移行・双方向同期は行わず、初回バックアップだけを作成します。

0.2.0では `GetProfileScopedPath` と `GetProfileScopedBasePath` の完成した戻り値から、アカウント直下の `modded/` 成分だけを除去します。グローバルMOD判定と明示的な旧保存先の照会は変更しません。`affects_gameplay:false` はローカルutilityとして採用していますが、マルチ互換性は未確認です。

**独立テストの成功と実機での進行共有成功は別です。** 実ゲームDLLでのビルド・起動・MODあり/なしの進行共有はローカルPCで確認します。[調査とパッチ契約](docs/RESEARCH.md)、[検証記録](docs/VALIDATION.md)も参照してください。

## Windows: 1コマンドのセットアップ

.NET 9 SDKが必要です。.NET 10 SDKだけではglobal.jsonの要件を満たしません。9と10は並行インストールできます。

.NET 9 SDKを追加する（未導入の場合）。
```powershell
winget install --id Microsoft.DotNet.SDK.9 --exact
```

PowerShellを開き直し、リポジトリ内で9.0系が選ばれることを確認する。
```powershell
dotnet --version
```

リポジトリを更新する。
```powershell
git pull --ff-only origin main
```

ゲームを終了し、セットアップする。
```powershell
.\scripts\Setup.ps1
```

初回は `local/paths.txt` を自動作成し、ビルド、設定の生成/更新、初回バックアップの作成/検証、旧MODの退避、新MODのインストールまで行います。既存configのバックアップ先（例: Dドライブ）は維持します。未設定なら設定ディレクトリの `backups/first` を使います。設定ファイルの手編集は不要です。

現在のソース確認済みゲームDLLのフィンガープリント、または同一DLLに対する既存の確認済み監査記録を使用します。未知のDLLへ更新されていた場合は、configや旧MODを置き換えず停止します。型名/メソッドの意味を推測して有効化したり、監査済みフラグを手でtrueにする手順にはしません。

旧MODは設定ディレクトリの `previous-mod-*` へ保存します。コピーに失敗したら旧MODを戻し、不完全な新ファイルも `failed-install-*` に保全します。ゲームDLLは同梱せず、実ゲームDLLによるビルドが失敗した場合はインストールしません。

## 3つの場所は別ファイルに保存

`local/paths.txt` は上から **ゲーム本体、セーブのルート、MOD設定ディレクトリ** の順で、3行だけです。引用符や見出しは不要です。初回自動作成時は標準のSteam配置と、現在のWindowsユーザーのAPPDATA/LOCALAPPDATAを実際の絶対パスに展開して保存します。

設定する場所の例（環境変数表記も読み込めます）:
```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2
%APPDATA%\SlayTheSpire2
%LOCALAPPDATA%\UnifiedVanillaSave
```

3行の内容を確認する。
```powershell
Get-Content .\local\paths.txt
```

場所を変更する場合だけ3行ファイルを開く。
```powershell
notepad .\local\paths.txt
```

別の3行ファイルを使う。
```powershell
.\scripts\Setup.ps1 -PathsFile 'D:\STS2-settings\paths.txt'
```

`local/` はGit対象外です。個人名を含む実パス・生成config・バックアップ・ゲームDLLはコミットしません。[配布用テンプレート](scripts/paths.example.txt)も3行です。

`UVS_CONFIG` が別の設定ファイルを指している場合、Setupは取り違えを避けて停止します。使用するconfigの親ディレクトリを3行目と一致させてください。

## ログと実機確認

セットアップ後、ゲームを起動してMODのバージョン0.2.0を確認します。

既定の場所の最新ログを表示する。
```powershell
Get-Content (Join-Path $env:LOCALAPPDATA 'UnifiedVanillaSave\UnifiedVanillaSave.log') -Tail 80
```

`PATCH_INSTALLED` はパッチ適用、`PROFILE_ROUTE` はビルダーの戻り値の観測です。例:
```text
original='user://steam/123/modded/profile1/saves'; vanilla='user://steam/123/profile1/saves'
```

Loaded表示・これらのログだけで保存完了とは判断しません。INACTIVE/例外/競合ownerが出たらゲームを終了します。最初は保全済みのデータで以下を確認してください。

- MODなしで進行/ランを確認して終了し、本MODありで同じ進行を読めること。
- 本MODありで変更・保存して終了し、MODなしで同じ変更を読めること。全profile、ラン中断再開、履歴、設定も確認すること。
- 実ファイルの時刻/ハッシュを確認し、バニラ側へ保存しmodded側を変更していないこと。
- 実績やグローバルMOD状態、ゲームプレイMOD一覧が元の判定を保つこと。
- マルチは両者なし/一方のみ/両者あり/ゲームプレイMOD併用をホスト役を交代して確認すること。

独自カード等を含むランは、提供MODを外すと読めない場合があります。本MODはセーブ形式を変換しません。Steam Cloudのキャッシュも操作・同期しません。

## 開発・診断用コマンド

ゲームDLLなしの独立テストと監査ツールをビルドする（Bash）。
```bash
bash scripts/test.sh
```

セットアップ処理を一時フォルダ/fixtureで検証する（PowerShell、上記ビルド後）。
```powershell
.\tests\Setup.Tests.ps1
```

本体だけをビルドする（PowerShell、実ゲームDLLが必要）。
```powershell
.\scripts\Build.ps1 -GameDataDir 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64'
```

`.csproj` は `GameDir`、`GameDataDir` または `GameAssemblyPath`/`HarmonyAssemblyPath`/`GodotAssemblyPath` を指定できます。参照はPrivate=false。成果物は `artifacts/mods/UnifiedVanillaSave/` のMOD DLLとmanifestの2ファイルだけです。ルート直下のdotnet build/publishではなく対象プロジェクトかスクリプトを指定します。ゲームのruntimeconfigが.NET 9以外になったらSDK/TFMも見直します。

DLLを実行せず、保存先ビルダーと直接呼び出し元のILを表示する（PowerShell）。
```powershell
dotnet run --project .\tools\SaveAudit -c Release -- paths 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll'
```

C#逆コンパイルが必要なときだけ、.NET 9で導入確認済みのILSpyCmdを入れる。
```powershell
dotnet tool install --tool-path .\local\ilspy9 ilspycmd --version 9.1.0.7988 --allow-roll-forward
```

プロバイダを `local/UserDataPathProvider.cs` に出力する（自動生成されるファイルではありません）。
```powershell
.\local\ilspy9\ilspycmd.exe -t MegaCrit.Sts2.Core.Saves.UserDataPathProvider 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll' | Set-Content -Encoding UTF8 .\local\UserDataPathProvider.cs
```

`SaveAudit paths` はセーブを読み書きせず、反射/間接呼び出し/キャッシュを網羅しません。CloudではゲームDLLなしの独立テストまでを検証し、実機結果は別に記録します。

## 無効化・復元

ゲームとCloud同期を停止し、`<game>\mods\UnifiedVanillaSave` をmodsの外へ移動すれば無効化できます。セーブの逆移行は行いません。以後、他のゲームプレイMODがある場合は通常のmodded側に戻る可能性があります。

初回状態に戻す場合は現在のuserデータ全体を別の場所へ退避し、空の元ルートへバックアップの `files/` の**内容**を復元します。上書きマージは新しいファイルが残るので避けてください。復元は初回以降の進行を戻します。

3行ファイルから設定ディレクトリを取得する。
```powershell
$configDir = [Environment]::ExpandEnvironmentVariables([IO.File]::ReadAllLines((Join-Path (Get-Location) 'local/paths.txt'))[2])
```

既存configから復元元と保存ルートを読み込む。
```powershell
$restore = Get-Content (Join-Path $configDir 'config.json') -Raw | ConvertFrom-Json
```

ゲームとCloud同期を停止した状態で、バックアップの整合性を確認する。
```powershell
dotnet run --project .\tools\SaveAudit -c Release -- backup $restore.UserDataRoot $restore.BackupDirectory
```

上のコマンドが成功したことを確認して、現在のデータを別の名前へ退避する。
```powershell
Move-Item -LiteralPath $restore.UserDataRoot -Destination ($restore.UserDataRoot + '.before-restore-' + [guid]::NewGuid().ToString('N'))
```

空の保存先を作る。
```powershell
New-Item -ItemType Directory -Path $restore.UserDataRoot | Out-Null
```

初回バックアップの内容を復元する。
```powershell
Get-ChildItem -LiteralPath (Join-Path $restore.BackupDirectory 'files') -Force | Copy-Item -Destination $restore.UserDataRoot -Recurse -Force
```

Cloudキャッシュ `<Steam>\userdata\<AccountId>\2868840\remote` は本MODのバックアップ対象外です。実ファイルを別途退避し、Steamが新しいコピーを再適用しないようCloudの復元競合を確認してから同期を再開します。自動同期・自動削除・自動復元は実装していません。
