# Architecture — Crystal Vanguard（engine=unity / 3D）

> 本書は「どう作るか」の技術的背骨。数値・面白さ・ルールの正本は `design/gdd.md` / `design/concept.md`（ピラー P-01〜P-04）。
> スタック・ディレクトリ・検証コマンドの正本は `.claude/docs/tech-stack-unity.md`、コード規約の追加則は `docs/conventions.md`。

## 1. スタックとプロジェクト

- Unity 6.3 LTS（6000.3.16f1）+ URP + C# + Input System + Unity Test Framework。テンプレートは `3d-cross-platform`（URP/InputSystem/TestFramework 同梱）から生成し、glTFast（`com.unity.cloud.gltfast`）を追加、`Packages/manifest.json` に `"testables": ["com.unity.inputsystem"]` を明記。
- プロジェクトマーカー: `game/ProjectSettings/ProjectVersion.txt`（contract §11）。
- 検証: EditMode テスト（typecheck 相当）と `ForgeGame.EditorTools.ForgeBuild.BuildMac`（build 相当）が exit 0。両者とも scaffold 時点で緑を確認済み。

## 2. シーン構成（contract §11 必須シーン集合・gdd ゲームフロー）

正準フロー: `Boot → Title → Menu → Game → Result → {Game|Menu}`。5シーンは `Assets/Scenes/` に実在し、`EditorBuildSettings` にこの順で登録済み（`ForgeScaffold.SetupScenes` が生成）。Boot が index 0。

| シーン | 責務 | 主な遷移トリガー（gdd 操作仕様と一致） | 主担当 |
|---|---|---|---|
| **Boot** | セーブロード（`FileSaveAdapter.Load`）→ 破損フラグを保持 → Title へ即遷移。ゲームロジックを持たない。 | ロード完了で自動 → Title | gameplay-engineer |
| **Title** | タイトル表示。決定キー(Enter/Space)で Menu、Esc でアプリ終了（`Application.Quit`、デスクトップのみ）。破損復旧フラグの通知表示（任意）。 | 決定→Menu / Esc→Quit | ui-engineer |
| **Menu** | アウトゲームのハブ。4タブ（はじめる/統計/アップグレード/設定）。プレイ開始・アウトゲーム表示・設定・終了導線の4必須要素を持つ。 | 「はじめる」+決定→Game / Esc→Title / Q/E タブ切替 / 購入・音量調整 | ui-engineer（購入/永続化ロジックは gameplay-engineer） |
| **Game** | コアループの実体。プレイヤー・敵・スポーン・自動攻撃・HP・スコア・HUD。HP≤0 で `MetaProgression.ApplyRunResult`→即セーブ→Result。 | HP≤0→Result | gameplay-engineer（HUD は ui-engineer） |
| **Result** | 最終スコア/ハイスコア更新表示。決定キーで即リスタート(Game)、Esc/「メニューへ」で Menu。 | 決定→Game / Esc→Menu | ui-engineer |

シーン間のランタイム状態（ロード済み SaveData・選択アップグレードLv・直近 RunResult）は、シーンをまたいで生存する単一の状態ホルダ（`DontDestroyOnLoad` な軽量 MonoBehaviour = `Components/` に置く「セッションホルダ」）で受け渡す。ホルダは値の保持と Persistence への委譲のみを行い、ロジックを持たない。

## 3. レイヤー境界（エンジン非依存コアの線引き — tech-stack-unity §将来のエンジン非依存化）

```
Assets/Scripts/
  GameConfig.cs      ← 全パラメータ + AssetKeys（唯一のチューニング/パス集約点）
  Types.cs           ← 共有プレーン型（EntityState / RunResult / EnumKind）
  Systems/           ← ★エンジン非依存コア（pure C#。UnityEngine のシーンAPI/MonoBehaviour/File I/O を import しない。値型・数学型は可）
    Meta/            ← メタ進行ロジック（MetaTypes=SaveData / MetaSchema=検証・移行 / MetaProgression=純粋 reducer）
  Persistence/       ← 永続化 I/O（File・persistentDataPath・JsonUtility の唯一の置き場）
  Components/        ← MonoBehaviour（ライフサイクル配線のみ。判定は Systems へ委譲）
  Input/             ← 入力集約（Input System・コードでアクション生成）
  Ui/                ← HUD/メニュー表示（uGUI。Canvas は ScreenSpaceCamera 固定）
  Editor/            ← ForgeBuild(BuildMac) / ForgeScaffold(SetupScenes)（Editor 専用 asmdef）
```

- **依存方向**: `Components/Ui/Input` → `Systems`（＋`Persistence`）→ `Types/GameConfig`。逆流禁止（Systems は Unity シーン層を知らない）。
- **アセンブリ**: 実行時は単一 asmdef `ForgeGame`（`Unity.InputSystem` 参照）。Editor は `ForgeGame.Editor`（Editor限定・`ForgeGame` 参照）。テストは EditMode/PlayMode の2 asmdef（後者は `Unity.InputSystem.TestFramework` を参照し `InputTestFixture` を使う）。
- **なぜこの線引きか**: P-01〜P-04 の判定ロジック（回避無敵窓・スコア式・ウェーブ算出・アップグレード補正・セーブ検証）を Unity 非依存に保つと、EditMode で高速に単体検証でき（実プレイ前にバグを潰せる）、将来のエンジン移植時もコアを流用できる。

## 4. システム→実装先マップ（gdd システム一覧と一致）

| システム | P-xx | Systems（pure） | Components/Ui（配線） |
|---|---|---|---|
| プレイヤー移動 | P-01,P-02 | `Systems/PlayerMovement.cs`（入力ベクトル→速度） | `Components/PlayerController.cs` |
| ダッシュ回避 | P-01 | `Systems/DashSystem.cs`（方向優先順位・無敵窓・CD） | PlayerController が駆動 |
| 自動攻撃 | P-02,P-03 | `Systems/AutoAttackSystem.cs`（最寄り索敵・間隔・瞬間ヒット） | `Components/AutoAttackDriver.cs`（VFX/アニメ再生） |
| 固定俯瞰カメラ | P-01 | （数学のみ Systems 補助可） | `Components/ArenaCameraRig.cs` |
| 敵接近AI | P-03 | `Systems/EnemyApproachSystem.cs`（直線ベクトル） | `Components/EnemyAgent.cs` |
| ウェーブスポーン | P-03 | `Systems/WaveSpawnSystem.cs`（間隔/数/倍率の導出） | `Components/WaveSpawner.cs`（Instantiate） |
| HP・被弾・死亡 | P-01,P-03 | `Systems/HealthSystem.cs`（`EntityState` 純粋関数） | `Components/HealthComponent.cs` |
| クリスタル | P-04 | `Systems/CrystalSystem.cs`（回収判定） | `Components/CrystalPickup.cs` |
| スコア | P-04 | `Systems/ScoreSystem.cs`（**scaffold 実装済**） | HUD が購読 |
| HUD | P-01,P-03 | — | `Ui/GameHud.cs` |
| メタ進行/永続化 | P-04 | `Systems/Meta/MetaProgression.cs`（**scaffold 実装済**） | `Persistence/FileSaveAdapter.cs`（**scaffold 実装済**）+ `Ui/MenuScreen.cs` |

`Systems/ScoreSystem.cs` `Systems/Meta/*` `Persistence/FileSaveAdapter.cs` `Input/GameInput.cs` `Components/BootLoader.cs` は scaffold で実装済み（engineer は残りの Systems/Components を追加し配線する）。

## 5. データフロー

### ラン内（Game）
入力(`Input/GameInput`) → `Components/PlayerController` が `PlayerMovement`/`DashSystem` を駆動 → Transform 反映。`WaveSpawner` が `WaveSpawnSystem` の導出値で敵を Instantiate → `EnemyAgent` が `EnemyApproachSystem` で移動 → 接触で `HealthSystem` にダメージ適用 → 敵撃破で `CrystalSystem`/`ScoreSystem` 更新 → `Ui/GameHud` が現在値を購読表示。プレイヤー HP≤0 で死亡演出（コード合成・gdd 決定）後 Result へ。

### メタ進行（Result/Menu の永続化）
Result 到達時: ラン集計を `RunResult` に詰め → `MetaProgression.ApplyRunResult(save, run)`（純粋）→ 新 `SaveData` → `FileSaveAdapter.Save`（アトミック `.tmp`→rename）を**1回だけ**。Menu 購入: `MetaProgression.TryPurchase`（純粋）→ 成功時のみ `Save`。起動時: `FileSaveAdapter.Load` → `MetaSchema.Normalize`（検証/移行）→ 破損なら `.bak` 退避＋`[SaveCorruption]` エラー1回＋既定値＋`recovered` フラグを UI へ伝播。

- **層の規律**: `Systems/Meta` は値を受けて値を返すのみ（I/O ゼロ）。File・`persistentDataPath` は `Persistence/` だけが触れる。テストは `Application.temporaryCachePath` の一時ファイルを使い実セーブを汚さない。

## 6. 3D 資産と表現

- リグ付きキャラは hero（MDL-01）のみ。FBX で取込み、`ModelImporter.animationType=Humanoid` で Avatar 生成（失敗時 Generic 縮退＋MANIFEST 注記）、ANM-01/02/03（attack/idle/run）を同一スケルトンで取込み hero AnimatorController を生成（S-18）。取込先 `Assets/Generated/`、raw と MANIFEST は `_generated/` に温存（contract §6/§11）。
- swarmer（MDL-02）はリグ縮退（assets.md must-replace・ANM-04 未生成）のため**静的メッシュとして取込み、接近表現は `Components/EnemyAgent` のコードモーション（前傾チルト＋上下バウンス）で代替する**（Checkpoint B 追認・2026-07-13。S-21）。スケルタルアニメ/Avatar/AnimatorController を持たない。Tripo クレジット補充時にリグ化して置換可能なよう Renderer と motion を分離しておく（任意フォローアップ）。
- クリスタル・アリーナ環境・ヘヴィ変種（MDL-03）は生成MDLを使わず Unity プリミティブ＋マテリアル/複製で表現（gdd 決定）。死亡・被弾はコード合成演出（新規アニメ追加なし）。
- 音声は OGG（Unity ネイティブ・M4A 不要）。動的ロードするクリップ/プレハブのキーは `GameConfig.AssetKeys` 経由。

## 7. 検証と QA の技術前提

- 単一インスタンスロック: Unity 起動工程（テスト/ビルド/取込）は直列化する。
- QA-PLAY は PlayMode テストで `Title→Menu→Game→Result→Menu` の1周＋各 acceptance を `InputTestFixture` で入力擬似発行し検証、`LogAssert.NoUnexpectedReceived()` でエラー0、RenderTexture 撮影で証跡（UI Canvas は ScreenSpaceCamera 固定なので同カメラに写る）。
- 永続化テスト（必須）: 保存→新規インスタンスで再ロード→一致、破損→`.bak`＋`[SaveCorruption]` を `LogAssert.Expect` で検知＋既定値復旧。
