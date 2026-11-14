# 調査結果・互換性の境界

調査日: 2026-11-14。ゲーム本体はこの環境に存在しない。以下は公開ソースで確認した事実と、ローカルDLLで確認が必要な事項を区別した記録。

## 参考ソース

- [SLS2Mods 固定コミット e78c2e2](https://github.com/luojiesi/SLS2Mods/tree/e78c2e29efcddf60ce9fe30529312f7261102450)
  - `UnifiedSavePath/UnifiedSavePathMod.cs`: 標準 `ModInitializer` と Harmony を使用。
  - `MegaCrit.Sts2.Core.Saves.UserDataPathProvider.GetProfileDir` のprefixは `int profileId` を受け取り、`profile{profileId}` を返して元メソッドをスキップする。
  - さらに `IsRunningModded` をfalseに代入し、getterとsetterにもprefixを適用する。この変更は採用しない。
  - このソースだけでは `GetProfileDir` のstatic宣言・元の戻り値・全呼び出し元・現行ゲームへの適合性は証明できない。
  - `UnifiedSavePath.json` は `affects_gameplay:false`、`has_pck:true`。旧形式の `mod_manifest.json` も残っている。今回PCKも旧manifestも不要。
- [Modding-Tutorial 固定コミット 99905ed](https://github.com/fresh-milkshake/Modding-Tutorial/tree/99905eda3b026e0baffc93a894038b178fd493ee)
  - [03](https://github.com/fresh-milkshake/Modding-Tutorial/blob/99905eda3b026e0baffc93a894038b178fd493ee/docs/03-mod-loading-and-manifests.md): v0.107.1 の .NET 9、外部 `<id>.json`、`has_dll`、`has_pck`、`min_game_version`、`affects_gameplay` とDLL名の規約。
  - [05](https://github.com/fresh-milkshake/Modding-Tutorial/blob/99905eda3b026e0baffc93a894038b178fd493ee/docs/05-entrypoints-harmony-and-hooks.md): 明示initializer、正確なパッチシグネチャ、更新時の再逆コンパイル。
  - [08](https://github.com/fresh-milkshake/Modding-Tutorial/blob/99905eda3b026e0baffc93a894038b178fd493ee/docs/08-debugging-and-fast-iteration.md): `user://steam/<SteamId>/modded/profile<ProfileId>/saves/` と非MOD側の `modded/` 省略。Steam Cloudキャッシュは別の場所。initializer失敗後もLoaded表示になる場合がある。
  - [09](https://github.com/fresh-milkshake/Modding-Tutorial/blob/99905eda3b026e0baffc93a894038b178fd493ee/docs/09-packaging-installing-and-verifying.md): `affects_gameplay:false` はゲームプレイを変更しないutilityの場合に限る。

参考MODには明示ライセンスが見当たらないため、コードをコピーせず動作の調査資料として使用し、新規実装した。

## 採用するパッチ候補

`MegaCrit.Sts2.Core.Saves.UserDataPathProvider` に宣言された **static string GetProfileDir(int)** のみ。これはローカルで検証する契約候補であり、現行の実シグネチャだと確定したものではない。リフレクションで完全一致し、DLLとILのSHA256が手動監査記録と一致するときのみprefixを適用する。相違時の別名探索や推測によるフォールバックはしない。

prefixは引数位置 `__0` を使い、ローカルで監査したバニラ相対ディレクトリ `profileN` を返す。グローバルなMOD状態・実績・ネットワーク判定・シリアライザにはパッチを当てない。保存先だけの処理のため、ゲームモデルのhookはこの用途に適合しない。

実機監査で、バニラのパスが別形式、メソッドがinstance、引数が異なる等が分かった場合はconfigで迂回せず実装を修正する。設定のtrueだけでは監査を代替できない。

## ローカル監査の必須項目

1. `sts2.runtimeconfig.json` とゲーム表示バージョンを記録し、TFMが.NET 9か確認。異なる場合はSDK/TFM/global.jsonを合わせて再ビルド。
2. ILSpy等で対象メソッドを逆コンパイル。戻り値が `profileN` / `modded/profileN` の選択だけで、失うべきでない副作用・引数検証がないか確認。
3. Analyze / Used By を再帰的に確認。進行・ラン・履歴・設定・全プロフィールの読み込みと保存がこの処理を経由し、起動時に既にキャッシュされたパスや別分岐がないことを確認。インライン化によりprefixが迂回されないか実際の書き込み先も検証。
4. 初期化順序を確認。バックアップ・パッチより前の保存/読込/ディレクトリキャッシュがあるなら、このinitializerだけでは不十分。この版を有効にせず設計を見直す。
5. `IsRunningModded` の呼び出し元を確認し、他の保存先分岐が残らないことを確認。getter/setterにはパッチしない。
6. `affects_gameplay` の参照先、`ModManager` のゲームプレイMOD一覧とマルチ接続照合コードを確認。falseが保存先自体を共通化する保証にはならない。

このクラウド環境では上記の実DLL監査、MOD本体の実DLL参照ビルド、ゲーム起動、マルチ接続は未実施。

## affects_gameplay とマルチ

falseを暫定採用する。カード・乱数・ゲームルール・通信内容を変更せず、ローカルの保存先を選ぶutilityであることが理由。資料の定義上は妥当だが、セーブ共有によって進行データへ影響するため、実際のローダー/ネットワークでの扱いは確認が必要。`min_game_version:0.107.1` は資料の対象バージョンを示す下限で、現行版や以後の互換性の保証ではない。ローカル監査時にこの値も見直す。

両者MODなし、一方のみ本MOD、両者本MOD、同じゲームプレイMOD併用、異なるゲームプレイMOD併用の組み合わせで、部屋作成・参加・再接続・中断保存/ロードを確認する。ホスト/クライアントも入れ替える。最後の組み合わせは本MODなしの場合と同じ拒否/許可結果であるべき。接続制限を避けるためにグローバルMOD状態を偽装しない。

## 競合検出と限界

対象メソッドおよび同じ型の `IsRunningModded` getter/setterに他ownerのHarmonyパッチがある場合、適用前に拒否する。適用直後と対象prefixの各呼び出し時にも再確認し、後からロードされた競合を検知する。例外時に自分のownerのみunpatchする。ゲーム/ローダーが例外を捕捉して続行する可能性があるので、INACTIVE/競合ログ時はプレイを中止する。

別の保存メソッドへのパッチ、直接のファイル書き込み、Harmonyを介さない差し替え、競合MODの後続prefixによる当prefixのスキップ、同時パッチの競合は完全検出できない。旧統合MODは外してから使用する。Steam Cloudを操作せず、Cloudとの同期も行わない。
