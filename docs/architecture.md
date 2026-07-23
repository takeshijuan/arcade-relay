# Architecture — Crystal Bastion（仮）

エンジン: **unity**（Unity 6.3 LTS / URP / C# / Input System）。正本規約は `.claude/docs/tech-stack-unity.md`、命名/ID/パスは `.claude/docs/contract.md`。
このドキュメントは「どう作るか（層の境界・シーン構成・データフロー）」を定義する。数値・面白さは `design/gdd.md` / `design/concept.md` が正本。

## 1. レイヤ構成（エンジン非依存境界の線引き）

Crystal Bastion は「純粋ロジック層」と「Unity 依存層」を厳密に分離する。依存の向きは常に **Unity 依存層 → 純粋ロジック層**（逆流禁止）。

```
┌─────────────────────────────────────────────────────────────┐
│ Unity 依存層（UnityEngine / MonoBehaviour / シーン API を使ってよい）│
│                                                               │
│  Scenes/(*.unity)   Components/     Ui/          Input/        │
│  5シーンの器          MonoBehaviour   uGUI/HUD/    InputSystem  │
│                       ライフサイクル   メニュー      アクション    │
│                       と配線のみ                    集約         │
│                                                               │
│  Persistence/  ← File I/O・persistentDataPath の唯一の置き場    │
└───────────────┬───────────────────────────────────────────────┘
                │ 値の受け渡しのみ（Unity 型は Vector3/Mathf 等の値型に限る）
                ▼
┌─────────────────────────────────────────────────────────────┐
│ エンジン非依存コア層（pure C#・MonoBehaviour/シーン API/File I/O 禁止）│
│                                                               │
│  Systems/         ゲームロジック（設置判定・戦闘解決・経済・スコア）  │
│    Systems/Meta/  メタ進行 reducer（RunResult → 新 SaveData）    │
│  GameConfig.cs    全パラメータ + AssetKeys + Scenes（唯一の定数源）  │
│  Types.cs         共有値型（enum・RunResult 等）                  │
└─────────────────────────────────────────────────────────────┘
```

- **Systems/ の禁止事項**（rules/unity-code.md が強制）: `MonoBehaviour` 継承・`GameObject.Find`/`Instantiate`/`GetComponent`・`Application.persistentDataPath`・`File` I/O を書かない。許されるのは `Vector3`/`Mathf`/`System.*` 等の値型・数学型のみ。
- **Persistence/ が唯一の I/O 層**: `persistentDataPath`・`File`・`PlayerPrefs` はここだけ。`Systems/Meta/` の reducer は値を受けて値を返すのみ（保存・読込を知らない）。
- この線引きにより Systems/ + Systems/Meta/ は EditMode テストで Unity 起動なしに検証でき（実際 `ScaffoldSmokeTests` が reducer/score を純粋テスト）、将来のエンジン移植でも再利用可能。

### ディレクトリ → 責務対応（実装先は gdd「システム一覧」の目安列に一致）

| ディレクトリ | 層 | 中身 |
|---|---|---|
| `Assets/Scripts/GameConfig.cs` | コア | 全ゲームパラメータ・`AssetKeys`・`Scenes` 名（マジックナンバー/パス文字列の唯一の置き場） |
| `Assets/Scripts/Types.cs` | コア | `TowerType`/`EnemyType`/`RunOutcome`/`RunResult` |
| `Assets/Scripts/Systems/` | コア | `BuildSpotSystem`/`TowerCombatSystem`/`TowerUpgradeSystem`/`WaveSpawnSystem`/`EnemyHealthSystem`/`CoreDefenseSystem`/`EconomySystem`/`ScoreSystem`/`FeedbackCueSystem` |
| `Assets/Scripts/Systems/Meta/` | コア | `MetaTypes`(SaveData)/`MetaSchema`(検証・migration)/`MetaProgression`(reducer) |
| `Assets/Scripts/Persistence/` | I/O | `ISaveStore`/`InMemorySaveStore`/`FileSaveStore`(story で追加) |
| `Assets/Scripts/Components/` | Unity 依存 | `GameBootstrap` ほか各エンティティ MonoBehaviour（Tower/Enemy/Core/BuildSpot の配線・Transform 反映） |
| `Assets/Scripts/Input/` | Unity 依存 | `InputReader`（マウス左/右クリック・Esc をコード生成アクションで抽象化） |
| `Assets/Scripts/Ui/` | Unity 依存 | HUD・Title/Menu/Result 画面・`UiCanvasHelper`（ScreenSpaceCamera 構成） |
| `Assets/Scripts/Editor/` | Editor | `ForgeBuild`(build 入口)・`ForgeScaffold`(シーン生成) |

## 2. シーン構成（contract §11 必須シーン集合）

`Assets/Scenes/` に **Boot / Title / Menu / Game / Result** の5シーン（EditorBuildSettings にこの順で登録済み）。正準フロー:

```
Boot → Title → Menu → Game → Result → { Game(リスタート) | Menu }
```

| シーン | 責務 | 主な遷移トリガー |
|---|---|---|
| **Boot** | セーブロード（Persistence）→ `recovered` フラグ保持 → Title へ。`GameBootstrap` が入口 | ロード完了で自動遷移 |
| **Title** | タイトル表示。任意クリック/キー入力で Menu へ。破損復旧時は通知表示（recovered） | クリック/キー → Menu |
| **Menu** | アウトゲームハブ。必須4要素（下表）を持つ | 「プレイ開始」→ Game / 「タイトルへ」→ Title / パネル開閉は同一シーン内 |
| **Game** | コアループ本体（設置・戦闘・ウェーブ・防衛・スコア）。Esc で一時停止オーバーレイ | 勝敗成立 → RunResult 確定 → ApplyRunResult → 即セーブ → Result |
| **Result** | 今回スコア/勝敗・新規実績・ハイスコア比較を表示 | 「もう一度」→ Game / 「メニューへ」→ Menu |

### Menu 必須要素（contract §11・gdd ゲームフロー節と一致）

| 必須要素 | 実装 |
|---|---|
| プレイ開始 | 「プレイ開始」ボタン → Game |
| アウトゲーム表示 | 実績一覧（ACH-01〜05・進捗バー）/ 統計（総ラン/勝利/撃破）/ 所持 essence + UPG-01〜03 の Lv・購入 UI |
| 設定 | 音量スライダー（BGM/SFX）・操作説明（左/右クリック・Esc） |
| 終了導線 | 「タイトルへ戻る」ボタン → Title |

- シーン遷移は `GameConfig.Scenes` の名前定数経由（`SceneManager.LoadScene`）。文字列直書き禁止。
- シーン間のデータ受け渡し（RunResult・SaveData・recovered）は永続 SaveData（Persistence 経由）と、プロセス内メモリ上の共有状態（DontDestroyOnLoad な軽量ホルダ or 直近 RunResult のキャリー）で行う。実装方式は S-01（GameFlow）で確定する。

## 3. データフロー（コアループ1周）

```
[Game 開始]
  Persistence.Load() → SaveData（UPG Lv 反映）
  EconomySystem 初期資金 = STARTING_GOLD + UPG-01 効果
        │
  ┌─────▼──────────────────────────────────────────────┐
  │ ウェーブループ（WaveSpawnSystem）                      │
  │  予告(SFX-05) → 敵スポーン → 敵が経路を等速前進         │
  │       │                                              │
  │  TowerCombatSystem: 射程内の敵へダメージ適用イベント    │
  │   （発生源タワー種別込み）→ EnemyHealthSystem          │
  │       │                                              │
  │  撃破: final-hit 帰属で総撃破数 / AoE撃破数を分離集計    │
  │        → 撃破報酬 EconomySystem / 撃破 VFX+SFX(SFX-03) │
  │  ゴール到達: CoreDefenseSystem がコアHP減算(SFX-04)     │
  └───────┬──────────────────────────────────────────────┘
          │ 全 WAVE_COUNT 消化 & coreHp>0 → 勝利 / coreHp<=0 → 敗北
          ▼
  RunResult 確定（IsWin/CoreHpRemaining/KillCount/AoeKillCount/UsedBuildSpots/ClearTimeSec）
  ScoreSystem.ComputeFinalScore(RunResult)
  MetaProgression.ApplyRunResult(SaveData, RunResult) → 新 SaveData（純粋・I/O なし）
  Persistence.Save(新 SaveData)  ← ここで1回だけ永続化（リスタート連打で二重保存しない）
          ▼
  [Result] 表示 → もう一度 / メニューへ
```

- **入力**: 全て `Input/InputReader` 経由。Components 層がポインタスクリーン座標をカメラでワールド変換し、`BuildSpotSystem` にビルドスポット選択を渡す。
- **演出**: `FeedbackCueSystem`（コア層）は「どの VFX/SFX を鳴らすか」の選択のみを返す。実際の再生（パーティクル・AudioSource・Transform 演出）は `Components/`。
- **モーション方式**（gdd 確定判断）: MDL-01〜05 は全て `rig_type: none`（スケルタルアニメ無し）。動きはコード駆動 transform（敵の移動/向き/ボブ、タワーの発射回転/リコイル、コアの被弾フラッシュ/パルス）。AnimatorController・Avatar は使わない。

## 4. 永続化アーキテクチャ（contract §6 / tech-stack-unity.md セーブ節）

- **保存先**: `Application.persistentDataPath/save.json`（`GameConfig.Save.FileName`）。`.tmp` 経由のアトミック書込。
- **形式**: JSON（`JsonUtility`）。先頭キー `save_version`（`SaveData.save_version`）。フラット・ネストなし・全プリミティブ。
- **層**:
  - `Systems/Meta/MetaTypes.cs` = `SaveData`（バージョン別プレーン型）+ `LoadResult`（recovered 同伴）
  - `Systems/Meta/MetaSchema.cs` = `Validate`（欠落/未来版/スキーマ不正の破損判定）+ `Migrate`（v(n)→v(n+1)・追加のみ）
  - `Systems/Meta/MetaProgression.cs` = `ApplyRunResult`（純粋 reducer）
  - `Persistence/ISaveStore.cs` + `FileSaveStore`（story で追加）= I/O
- **破損時プロトコル**（黙示初期化禁止）: パース失敗・`save_version` 欠落/未来版・スキーマ検証失敗のいずれも → (1) 生データを `save.json.bak.<UTC>` へ退避 → (2) `Debug.LogError("[SaveCorruption] reason=... backup=...")` を1回 → (3) 既定値再生成し `recovered=true` を Title/Menu へ伝播。
- **テスト**: `Application.temporaryCachePath` の一時ファイルを使い実ユーザーセーブを汚さない。

## 5. ビルド・検証パイプライン

- **typecheck 相当**: `-runTests -testPlatform EditMode`（コンパイルエラーはテスト起動失敗で検知）。scaffold 時点で 6/6 passed。
- **build 相当**: `-executeMethod ForgeGame.EditorTools.ForgeBuild.BuildMac`（StandaloneOSX・Apple silicon → `game/Build/ForgeGame.app`）。失敗は `EditorApplication.Exit(1)` で非0昇格。
- **単一インスタンスロック**: Unity は同一プロジェクトを同時1プロセスのみ。テスト/ビルド/資産取込は直列化。Build/Polish 並走レーンの agent は Unity を起動せず、検証は合流後のバッチ区間で行う（tech-stack-unity.md「検証バッチ化」）。

## 6. パッケージ依存（Packages/manifest.json）

必須4種を明記済み: `com.unity.render-pipelines.universal`（URP）/ `com.unity.inputsystem` / `com.unity.cloud.gltfast`（GLB 取込）/ `com.unity.test-framework`。加えて PlayMode の入力擬似発行のため `"testables": ["com.unity.inputsystem"]` を宣言（`InputTestFixture` を PlayMode asmdef から参照）。
