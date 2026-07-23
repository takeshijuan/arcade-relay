# ArcadeRelay 技術スタック規約（game/ 配下・engine=unreal の正本）

> エンジン選択は contract.md §11（`state/engine.txt`）。このファイルは **engine=`unreal`（3D）** の正本。
> phaser は tech-stack.md、unity は tech-stack-unity.md。共通思想（マジックナンバー禁止 / delta-time / エンジン非依存コア / 入力抽象化 / 資産キー集約）は全エンジン同一で、ここでは UE C++ イディオムに翻訳する。

## スタック（固定）

- **Unreal Engine 5.x（5.8 以降推奨）** + **C++** + **Enhanced Input** + **Automation Test**
- **Blueprint はロジック格納禁止**（.uasset バイナリでテキストエージェントが読み書き・差分レビュー不能なため）。UI ウィジェットの配線など最小限のみ許可し、ロジックは必ず C++ に置く
- macOS では Xcode 15.2 以上が必須（C++ コンパイル）
- `game/` は自己完結 UE プロジェクト。プロジェクト名は **`ForgeGame` 固定**（contract §11。`game/ForgeGame.uproject` がマーカー）
- エンジン実体は preflight が `state/engine-info.json` に解決したパス（`UE_ROOT`）を使う。**実行中の再解決禁止**

## エンジン導入（インストール状況の前提）

- エンジン取得はいずれの経路でも **Epic アカウントのブラウザログインが1回必須**（完全無人化は不可）:
  1. **Offline Installer（推奨）**: dev.epicgames.com/portal にログインし macOS 用 `.pkg` をダウンロード → 以降は CLI: `sudo installer -pkg FullInstall_OnMac.pkg -target /` → `sudo chown -R $USER "/Users/Shared/Epic Games/UE_"*`
  2. GitHub ソースビルド: Epic⇔GitHub 連携（GUI＋招待メール承諾）が必要な上、ディスク 150GB 超を要するため**この環境区分では非推奨**
- ディスク要件: エンジン本体で最小 30〜40GB、インストール一時展開を含め 100GB 超になる場合あり（公式の確定値なし）。**preflight は空き容量を検査し、不足時は人間へエスカレーションする**
- 導入済み確認: `ls "/Users/Shared/Epic Games/UE_"*/Engine/Build/BatchFiles/RunUAT.sh`

## プロジェクト生成（scaffold）

公式の CLI プロジェクト生成コマンドは存在しない。**テンプレートディレクトリのコピー方式**を使う（公式のテンプレート機構と整合）:

1. `$UE_ROOT/Templates/TP_ThirdPerson`（三人称 3D。C++ 版）を `game/` にコピー
2. `.uproject` を `ForgeGame.uproject` にリネームし、内部の module 名・`Config/DefaultGame.ini` の `ProjectName` を `ForgeGame` に更新
3. `Source/` 配下のモジュール名・クラス接頭辞を ForgeGame に統一
4. Python Editor Script Plugin を `.uproject` の Plugins 依存に追加（headless 自動化用）

## ディレクトリ構造

```
game/
  ForgeGame.uproject
  Source/
    ForgeGame/
      ForgeGame.Build.cs
      GameConfig.h            # ★全ゲームパラメータ（namespace GameConfig / constexpr）+ 資産パス定数
      Types.h                 # 共有型（FEntityState 等）
      Systems/                # エンジン非依存ロジック（pure C++。UObject/AActor 禁止・FVector等コア型は可）
        Meta/                 # メタ進行ロジック（MetaTypes.h / MetaSchema / MetaProgression — pure C++・contract §11）
      Persistence/            # 永続化 I/O 層（USaveGame 派生・SaveGameToSlot/LoadGameFromSlot の唯一の置き場。UObject 可）
      Actors/                 # AActor/UObject 派生（ライフサイクルと配線のみ）
      Input/                  # Enhanced Input の集約（PlayerController / 入力コンポーネント）
      Ui/                     # HUD（可能な限り C++。Widget BP は配線のみ）
    ForgeGameTests/           # Automation Test モジュール（IMPLEMENT_SIMPLE_AUTOMATION_TEST）
  Content/
    Generated/                # AI生成資産の取込先（Interchange でインポートされた .uasset）
    Maps/                     # Boot/Title/Menu/Game/Result の5状態（contract §11 必須シーン集合。レベル分割 or 状態遷移はどちらでも可 — ただし5状態すべての実在と遷移を Automation テストで検証可能にすること。「単一レベルだから Title/Menu 省略」は不可）
  Config/
    DefaultEngine.ini / DefaultGame.ini / DefaultInput.ini
  _generated/                 # raw 生成資産 + MANIFEST.jsonl（Content/ 外 = UE はインポートしない）
```

バージョン管理除外（.gitignore 済み）: `Binaries/ Intermediate/ Saved/ DerivedDataCache/`。`Source/ Config/ Content/ *.uproject` はコミット対象（Content/ の .uasset はバイナリ）。

## 検証コマンド

`UE_ROOT` は `state/engine-info.json` の `ue_root` フィールド（`/Users/Shared/Epic Games/UE_5.x` 形式。preflight が書き出す）。`binary` は `RunUAT.sh` のフルパスであり、`ue_root` が無い旧ファイルでは binary から 3 階層（`Engine/Build/BatchFiles`）を遡って導出する。

| 目的（phaser 対応） | コマンド | 合格条件 |
|---|---|---|
| typecheck/build 相当（コンパイル） | `"$UE_ROOT/Engine/Build/BatchFiles/RunUAT.sh" BuildCookRun -project="$PWD/game/ForgeGame.uproject" -platform=Mac -architecture=arm64 -clientconfig=Development -build` | exit 0 |
| test（QA用） | `"$UE_ROOT/Engine/Binaries/Mac/UnrealEditor-Cmd" "$PWD/game/ForgeGame.uproject" -ExecCmds="Automation RunTests ForgeGame;Quit" -ReportExportPath="$PWD/qa/evidence/automation" -unattended -nopause -nullrhi -stdout` | exit 0 かつレポート JSON で failed 0（`-unattended -nullrhi` 系フラグは公式文書外・実機検証で確定させること） |
| package 相当（フルビルド） | `"$UE_ROOT/Engine/Build/BatchFiles/RunUAT.sh" BuildCookRun -project="$PWD/game/ForgeGame.uproject" -platform=Mac -architecture=arm64 -clientconfig=Development -build -cook -stage -pak -archive -archivedirectory="$PWD/game/Build"` | exit 0（`BUILD SUCCESSFUL` ログ） |
| dev/preview 相当（人間向け） | `open "game/Build/Mac/ForgeGame.app"`（パッケージ済み）または UE エディタでプロジェクトを開く | — |

エディタ自動化スクリプトは Python（`unreal` モジュール）を使い、`UnrealEditor-Cmd <uproject> -run=pythonscript -script="script.py"` で headless 実行する。

**直列化と検証バッチ化（Build/Polish 並走レーン規約 — retro-e2 案A+B）**: UE のビルド（UBT/UAT）とエディタ起動を伴う工程は同一プロジェクトで並走させない（ビルド中間物・エディタロックが衝突する）。コード story の実装は assignee レーン（gameplay/ui）で並走するため、**レーン中の agent は UE/UBT を一切起動しない**。story ごとの検証は「参照する型・メンバ・ヘッダ include の実在を Read/Grep で静的確認」までとし、BuildCookRun の一括検証は**レーン合流後のバッチ検証区間（直列）**で行う。失敗時はエラーのファイルパスと `git log --oneline -- <path>` で原因 story を特定（困難なら story コミット単位の二分探索）し、最小修正と原因 story を `state/reviews/batch-verify.md` に記録する（正本実装は workflow の batchVerify）。

## コード規約（rules/unreal-code.md が編集時に強制する内容の正本）

1. **マジックナンバー禁止** — 全ゲームパラメータは `Source/ForgeGame/GameConfig.h` の `namespace GameConfig` 内 `constexpr` 定数に集約。チューニングは GameConfig.h だけで完結させる
2. **delta-time 必須** — `Tick(float DeltaSeconds)` の `DeltaSeconds` でスケール。固定フレームレート前提の実装禁止
3. **Actors は薄く** — AActor/UObject 派生はライフサイクルと配線のみ。ロジックは `Systems/` の pure C++（UObject/AActor/UWorld 禁止。`FVector`/`FMath` 等コア型は可）
4. **Blueprint ロジック禁止** — ゲームルール・状態遷移・数値計算を Blueprint に置かない（バイナリでレビュー不能）。Widget BP は表示配線のみ
5. **入力抽象化** — Enhanced Input（`UInputAction`/`UInputMappingContext`）に一元化。旧 `BindAxis("文字列")` 禁止
6. **資産参照はキー集約** — 動的ロードは `GameConfig.h` に集約した `FSoftObjectPath`/`TSoftObjectPtr` 定数経由。実装本文へのパス文字列直書き禁止
7. **テスト必須** — `IMPLEMENT_SIMPLE_AUTOMATION_TEST` でコアループ相当（Systems 層の状態遷移1周）を検証するテストを最低1本置く
8. **シーン/状態集合固定** — Boot / Title / Menu / Game / Result の5状態（contract §11 必須シーン集合。正準フロー: Boot→Title→Menu→Game→Result→{Game|Menu}）。Menu の必須要素: プレイ開始・アウトゲーム表示（アンロック/実績/統計）・設定（音量・操作表示）・終了導線。5状態の実在と遷移を Automation テストで検証する
9. **永続化 I/O は `Source/ForgeGame/Persistence/` に集約** — `USaveGame` 派生クラス・`UGameplayStatics::SaveGameToSlot/LoadGameFromSlot` はこの層のみ（UObject 派生のため Systems/ には置けない）。メタ進行ロジック（`Systems/Meta/` の pure C++・`FMetaSaveData` 等の純粋 struct）は値を受けて値を返すのみで、`FMetaSaveData` ⇔ `UForgeSaveGame` の UPROPERTY 変換は Persistence 層の責務（「セーブ / 永続化」節参照）

## 資産の取り扱い

- raw 生成物と MANIFEST.jsonl は `game/_generated/`（contract §6/§11）。取込は Interchange Framework（UE5 は glTF/FBX ネイティブ対応）を Python で自動化: `unreal.InterchangeManager` でインポートし `Content/Generated/` に格納
- **スケール単位が最大の罠**: UE は 1 unit = 1cm、glTF は m 基準。取込後にバウンディングボックスを検査し（ヒト型 ≈ 160–200 units）、外れていたら Import Uniform Scale=100 を適用するか raw 側で焼き込み修正
- リグ付きキャラ: FBX（2020 系フォーマット）推奨。リターゲットは IK Rig / IK Retargeter を Python API（`unreal.IKRetargeterController` / `auto_map_chains(FUZZY)`）で自動化
- 音声: WAV で取込（UE ネイティブ）。OGG/M4A の二重化は不要（phaser 専用要件）

## QA-PLAY の実行方法（gates.md QA-PLAY の unreal 節から参照される）

1. package 相当コマンドが exit 0
2. Automation Test でコアループ1周＋**必須シーン遷移 `Title → Menu → Game → Result → Menu` の1周**（Systems 層＋Functional Test）を検証。`-ReportExportPath` の JSON で failed 0 を機械検証
3. スクリーンショット証跡: Automation の screenshot 機能または `HighResShot` コンソールコマンドを ExecCmds で発行し `qa/evidence/` に保存（`-nullrhi` 使用時は描画不可のため、スクリーンショット取得時は必ず nullrhi を外す）。撮影の成否は機械判定を先行させる: `magick identify -format "%[fx:mean]" <shot>.png` が 0.02 未満/0.98 超なら SUSPECT_BLANK として撮影条件を直して再撮影。**撮影した画像は必ず Read で目視し、対象（モデル・UI 文字）が実際に写っていることを確認**（黒画面・描画欠落は不合格。値の内部整合性テストだけではレンダリング欠陥を検知できない — unity 節と同一の規律）
4. acceptance は stories.yaml の各項目を Automation Test として実装・実行
5. **メタ進行の永続化テスト（必須）**: gates.md QA-PLAY 観点5 のとおり、保存→新規ロードで復元一致・破損 `.sav` で `.bak` 退避＋`[SaveCorruption]` エラー（`AddExpectedError(TEXT("SaveCorruption"))` でホワイトリスト検知）＋既定値復旧を Automation テストで検証。テストは専用スロット名（例 `ForgeGameSave_Test`）を使い末尾で削除する（「セーブ / 永続化」節）

## ライセンス注意（EULA）

- ロイヤリティ: 製品あたり生涯総収益 $1,000,000 USD 超過分に 5%（Epic Games Store 同時ローンチで 3.5%）
- **エンジンコード・コンテンツを生成AIの学習・プロンプト入力に使うことは EULA で禁止**（Licensed Technology を Generative AI Program への入力にしない義務）。ハーネスのエージェントは **UE エンジンソース（`/Users/Shared/Epic Games/` 配下）を読み込んでプロンプトに含めてはならない**。自プロジェクト（game/ 配下の自作コード）は対象外
- Engine コードを含むリポジトリの一般公開は不可（Engine Licensee 限定配布のみ）

## セーブ / 永続化（contract §6 のセーブ規約の unreal 実装正本）

- **保存先**: `USaveGame` 派生 `UForgeSaveGame`（スロット名 `ForgeGameSave` 固定。実体は `Saved/SaveGames/ForgeGameSave.sav`）。`UPROPERTY() int32 SaveVersion` を必須先頭フィールドとする
- **層の分離**（contract §11）: メタ進行ロジック = `Source/ForgeGame/Systems/Meta/`（pure C++。`FMetaSaveData` 等の純粋 struct + RunResult を受けて新 SaveData を返す純粋関数群）。I/O = `Source/ForgeGame/Persistence/`（`UForgeSaveGame` と `MetaSaveService`: `TryLoad(FMetaSaveData&)` / `Save(const FMetaSaveData&)` / `BackupCorruptSlot()`。`FMetaSaveData` ⇔ UPROPERTY の 1:1 変換のみ）
- **マイグレーション**: `SaveVersion` が古ければ v(n)→v(n+1) を順に適用。現行より新しい版は変換せず破損相当（暗黙ダウングレード禁止）
- **破損時プロトコル（黙示初期化禁止 — rules/unreal-code.md が強制）**: ロード失敗・`SaveVersion` 不正・スキーマ検証失敗（必須フィールド欠落・型不正）のいずれも、(1) `IFileManager` で `.sav` を `.bak.sav` へ退避 → (2) `UE_LOG(LogForgeGame, Error, TEXT("[SaveCorruption] reason=... backup=..."))` を1回 → (3) 既定値で再生成し `bRecovered` を UI 層（Title/Menu）に伝播
- **保存タイミング**: Result 到達時に `ApplyRunResult` → 即 Save を1回。`UGameInstance` が `MetaSaveService` の呼び出し元として適任（レベル間で自然に永続）
- **テスト規約**: Automation テストは専用スロット名（`ForgeGameSave_Test` 等）を使い、`IFileManager::Get().Delete` で末尾クリーンアップ。本番スロット `ForgeGameSave` をテストで使わない

## 将来のエンジン非依存化に向けた線引き

- `Source/ForgeGame/Systems/` は UObject/AActor/UWorld を include しない（コア値型のみ可）— ここがエンジン非依存層（`Systems/Meta/` も同様）
- UE 依存は `Actors/` `Ui/` `Input/` `Content/` `Persistence/` に閉じ込める
