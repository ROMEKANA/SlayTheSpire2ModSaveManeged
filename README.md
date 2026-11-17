# UnifiedVanillaSave

Slay the Spire 2のMODあり・なしでバニラ側のプロフィール保存先を使用する、独立したC# MODです。標準ローダーとゲーム同梱Harmonyを使い、BaseLib/RitsuLibには依存しません。セーブの移行・双方向同期は行わず、初回バックアップだけを作成します。

0.3.0では通常の保存で使われる共通の相対パス選択処理 `GetAccountDir(bool?)` と `GetProfileDir(int, bool?)` の2つを対象にします。未指定のMOD状態引数だけをfalseへ補正し、元の処理をそのまま実行します。グローバルMOD判定と明示的な旧保存先の照会は変更しません。`affects_gameplay:false` はローカルutilityとして採用していますが、マルチ互換性は未確認です。

**独立テストの成功と実機での進行共有成功は別です。** 実ゲームDLLでのビルド・起動・MODあり/なしの進行共有はローカルPCで確認します。[調査とパッチ契約](docs/RESEARCH.md)、[検証記録](docs/VALIDATION.md)も参照してください。

**実機確認状況:** 0.3.0について、ユーザーから動作成功の報告を受けています。Cloudからゲームを実行した結果ではなく、全profile・マルチ・Cloud同期の全組み合わせを確認したという意味ではありません。

## 他のリポジトリへ持っていくAI向け資料

資料はいずれもリポジトリのルート直下にあります。

| 持っていく先 | コピーするファイル |
| --- | --- |
| ゲームを問わない制作・開発 | [AGENTS.md](AGENTS.md) |
| Slay the Spire 2のMOD制作 | [AGENTS.md](AGENTS.md) と [STS2_MODDING.md](STS2_MODDING.md) |
| 同じ・似た保存先統合MODの再製作 | 上記に加えて [DESIGN_AND_LESSONS.md](DESIGN_AND_LESSONS.md) |

DESIGN_AND_LESSONS.mdはこのMODの目的・保存経路・0.3.0の仕組み・失敗した方法・解決根拠をまとめた設計記録です。一般仕様だけのspec.mdとは区別しています。

移動先にも指示ファイルがある場合は、既存ルールを保って統合してください。これらはCodex向けの資料です。localの実パス・生成config・診断レポート・セーブ・ゲームDLL・バックアップを一緒に持っていく必要はありません。

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

## PowerShellの権限エラーへの対処

エラーメッセージによって原因が異なります。信頼できるリポジトリのスクリプトであることを確認してから、該当する対処だけを行ってください。

### 「このシステムではスクリプトの実行が無効」

現在の実行ポリシーを確認する。
```powershell
Get-ExecutionPolicy -List
```

組織のポリシーで制限されていない場合、このPowerShellだけをRemoteSignedにする。
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
```

同じウィンドウでセットアップする。
```powershell
.\scripts\Setup.ps1
```

Processの設定はウィンドウを閉じると戻ります。MachinePolicy/UserPolicyで管理されている場合は、管理者へ確認してください。全体のポリシーをUnrestricted/Bypassへ変更する手順にはしません。

### 「デジタル署名されていません」

取得元と内容を確認したうえで、エラーに出たファイルだけダウンロードブロックを解除する（Setup.ps1の場合）。
```powershell
Unblock-File -LiteralPath .\scripts\Setup.ps1
```

別のスクリプト名が示された場合はそのファイルを対象にします。無関係なファイルまで一括解除しないでください。

### インストール先への「アクセスが拒否されました」

これはProgram Filesへの書き込み権限などの問題です。実行ポリシーの変更だけでは解決しません。ゲームを終了し、local/paths.txtが普段のWindowsユーザーの場所を指すことを確認します。

実際に書き込み拒否が出る場合に限り、同じWindowsアカウントで管理者PowerShellを開く。
```powershell
Start-Process powershell -Verb RunAs
```

新しいウィンドウで元の作業フォルダへ戻り、Setup.ps1を再実行します。別の管理者アカウントへ切り替えるとAPPDATA等が変わるため、既存の3行ファイルを使ってください。フォルダ全体のACLを緩める対処は行いません。

「別のプロセスが使用中」の場合は、ゲームを通常終了して再実行してください。未保存データのあるプロセスを強制終了する手順にはしません。

## 問題の報告はテキストファイルで

スクリプトのエラーや調査に必要な出力は、できるだけファイルに保存して送ってください。ゲームソースが必要な場合は後述のExport-SaveSources.ps1がlocal/save-sources.txtを作成します。

診断ファイル用のフォルダを用意する。
```powershell
New-Item -ItemType Directory -Path .\local -Force | Out-Null
```

スクリプト実行前のポリシー情報を保存する。
```powershell
Get-ExecutionPolicy -List | Out-File -Encoding utf8 .\local\execution-policy.txt
```

3行ファイルが指す設定場所から、最新のMODログを保存する。
```powershell
Get-Content (Join-Path ([Environment]::ExpandEnvironmentVariables((Get-Content .\local\paths.txt)[2])) 'UnifiedVanillaSave.log') -Tail 160 | Set-Content -Encoding UTF8 .\local\runtime-log.txt
```

必要に応じてlocal/execution-policy.txt、local/runtime-log.txt、local/save-sources.txtの指定されたファイルを送ってください。生のレポートはGitへコミットしません。

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

セットアップ後、ゲームを起動してMODのバージョン0.3.0を確認します。

既定の場所の最新ログを表示する。
```powershell
Get-Content (Join-Path $env:LOCALAPPDATA 'UnifiedVanillaSave\UnifiedVanillaSave.log') -Tail 80
```

`PATCH_INSTALLED` はパッチ適用、`RELATIVE_ROUTE` は通常の相対パス選択がバニラ向けになったことの観測です。例:
```text
RELATIVE_ROUTE: GetAccountDir; requested=default; effective=vanilla; relative=''
RELATIVE_ROUTE: GetProfileDir; requested=default; effective=vanilla; relative='profile1'
```

Loaded表示・これらのログだけで保存完了とは判断しません。INACTIVE/例外/競合ownerが出たらゲームを終了します。最初は保全済みのデータで以下を確認してください。

- MODなしで進行/ランを確認して終了し、本MODありで同じ進行を読めること。
- 本MODありで変更・保存して終了し、MODなしで同じ変更を読めること。全profile、ラン中断再開、履歴、設定も確認すること。
- 実ファイルの時刻/ハッシュを確認し、バニラ側へ保存しmodded側を変更していないこと。
- 実績やグローバルMOD状態、ゲームプレイMOD一覧が元の判定を保つこと。
- マルチは両者なし/一方のみ/両者あり/ゲームプレイMOD併用をホスト役を交代して確認すること。

独自カード等を含むランは、提供MODを外すと読めない場合があります。本MODはセーブ形式を変換しません。Steam Cloudのキャッシュも操作・同期しません。

## 開発・診断用コマンド

提供された実装では、進行・ラン・履歴・設定・profile選択の読み書きが共通の相対パス選択処理を使うことを確認しています。0.3.0はこの2箇所へ対象を変更しました。次のコマンドは調査用で、MODやセーブを変更しません。

3行ファイルのゲームDLLから、保存関連7型のソースを一括取得する。
```powershell
.\scripts\Export-SaveSources.ps1
```

出力は `local/save-sources.txt` です。SDK互換性を確認済みのILSpyCmd 9.1.0.7988を使用し、なければ自動導入します。レポートにはゲームDLLのハッシュと対象ソースを含み、セーブ内容・config・生の個人パスは含めません。生成コードはGit対象外に置き、MODへ同梱しません。


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

開発用のHarmony結合テストに限りNuGetのLib.Harmony 2.4.2を使用します。本番MODは従来どおりゲーム同梱Harmonyを参照し、NuGetのHarmony DLLはインストールしません。結合テストも実ゲームの受け入れ試験とは別です。

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
