# Conventions — Crystal Vanguard 固有コード規約（engine=unity）

> 正本は `.claude/docs/tech-stack-unity.md`「コード規約」（15項）と `.claude/rules/unity-code.md`。
> ここでは**このゲーム固有の追加則のみ**を書く（tech-stack の重複は書かない）。違反は CR-CODE で CONCERNS 以上。

## 1. 命名・名前空間

- ルート名前空間 `ForgeGame`。層ごとにサブ名前空間を付ける: `ForgeGame.Systems` / `ForgeGame.Systems.Meta` / `ForgeGame.Persistence` / `ForgeGame.Components` / `ForgeGame.InputLayer` / `ForgeGame.Ui` / `ForgeGame.EditorTools`。
  - 注: 入力層は `ForgeGame.Input` にしない（`UnityEngine.Input` と紛らわしいため `ForgeGame.InputLayer`）。
- Systems の型は「システム名＋役割」: `PlayerMovement` `DashSystem` `WaveSpawnSystem`。純粋計算は可能なら `static` クラスの純粋関数（例: `ScoreSystem`, `MetaProgression`）。状態を持つ System は `struct`/`class` で状態を明示的に受け渡す（MonoBehaviour 禁止）。
- Components は「対象＋Controller/Driver/Agent/Rig」: `PlayerController` `WaveSpawner` `EnemyAgent` `ArenaCameraRig`。

## 2. GameConfig への集約（マジックナンバー禁止の具体則）

- gdd 数値表の全定数は `GameConfig` の**入れ子 static クラス**に定数名（gdd の `SNAKE_CASE` を C# の `PascalCase` に）で置く。例: `SPAWN_INTERVAL_BASE` → `GameConfig.Wave.SpawnIntervalBase`。コメントに元の定数名と単位を残す（既に scaffold 済み・追加時も踏襲）。
- **初期値は必ず gdd 記載値**を写す（技術都合で変えない。変更が必要なら game-designer へ提案）。調整レンジは gdd 側が正本なので config には初期値のみ。
- ウェーブ導出（スポーン間隔＝`base + decay*wave` を `min` でクランプ、敵HP成長は wave4+ 等）は `WaveSpawnSystem` の純粋関数に集約し、係数は必ず `GameConfig.Wave.*` から引く。式のリテラル直書き禁止。

## 3. 単位と座標の規約

- 距離は**メートル**（1 unit = 1m）、速度は **m/s**、時間は**秒**、角度は**度**（`GameConfig.Camera.PitchDeg` 等）。混在禁止。
- アリーナは XZ 平面（Y は上）。プレイヤー/敵の移動は `Vector3` の XZ 成分のみ（Y 固定）。移動ベクトルは合成後 `PLAYER_MOVE_SPEED` を超えないよう正規化してから速度を掛ける（斜め移動高速化バグの禁止・gdd 操作仕様）。
- 前方軸 +Z / アップ軸 +Y（art-bible.json `scale` と一致）。モデル取込スケールは authoring 計測値基準。

## 4. 入力（gdd 操作仕様の写し込み）

- 入力は `InputLayer/GameInput` の1箇所でコード生成（`.inputactions` 資産は編集しない）。Game 用マップ（Move/Dash）と UI 用マップ（Navigate/Adjust/Submit/Cancel/TabPrev/TabNext）を分離し、シーンに応じて `EnableGameplay`/`EnableUi` を排他的に切り替える。
- キー割当は gdd「操作仕様」表と厳密一致: 移動=WASD/矢印、ダッシュ=Space、決定=Enter/Space、戻る/終了=Esc、タブ=Q/E、スライダー調整=A/D。**A/D をタブ切替に割り当てない**（設定タブのスライダー入力と衝突するため — gdd 決定）。
- ダッシュ方向は hero の facing を参照しない（移動ベースの優先順位(1)(2)(3)のみ・gdd 決定）。この規約に反する facing 連動実装は不可。

## 5. シーン遷移と状態受け渡し

- 遷移は `SceneManager.LoadScene(GameConfig.Scenes.<Name>)` を使い、シーン名は必ず `GameConfig.Scenes` 定数経由（文字列直書き禁止）。
- シーンをまたぐ状態（SaveData / 選択アップグレードLv / 直近 RunResult）は `DontDestroyOnLoad` の単一セッションホルダ（`Components/`）で保持。ホルダはデータ保持と Persistence 委譲のみ。Game/Menu/Result は起動時にホルダから読む。
- Game 初期化時、アップグレードLv→`effectiveAttackDamage`/`effectiveMoveSpeed`/`effectiveMaxHp` は `MetaProgression.Effective*` で算出する（式の再実装禁止）。

## 6. 永続化（破損プロトコルの徹底）

- セーブ書込は Result 到達時に**1回**（`ApplyRunResult`→`Save`）、Menu 購入成功時、設定スライダー変更時のみ。毎フレーム保存禁止。Result→リスタート連打で二重保存しないため、メモリ上の SaveData を使い回す。
- 破損時は必ず3点セット（`.bak` 退避 → `Debug.LogError("[SaveCorruption] reason=... backup=...")` 1回 → 既定値＋`recovered` フラグ伝播）。catch して既定値を返すだけ / フィールド単位で既定埋め は禁止（`FileSaveAdapter`/`MetaSchema` の既存実装を経由すること）。
- SaveData のフィールド名は gdd「セーブデータ方針」表と完全一致（`save_version` は先頭・その他 camelCase）。JsonUtility 互換のためフラットなプレーン型のみ（Dictionary/ネスト非対応）。

## 7. 表現・フィードバックの制約（アセット予算由来）

- hero アニメは idle/run/attack の3クリップ（ANM-01〜03）のみ。**死亡・被弾専用クリップを追加しない**。死亡はコード合成演出（マテリアルフェード＋回転チルト＋画面ディゾルブ）、被弾はマテリアルフラッシュ＋既存アニメ継続（gdd 決定）。
- 自動攻撃は瞬間ヒット＋VFX（弾道オブジェクトを持たない）。attack アニメ長が `AUTO_ATTACK_INTERVAL` を超える場合は再生速度でスケール（gdd 決定）。
- ウェーブ切替の演出は SFX（SFX-05）1回＋HUD数値の 0.3s パルス（`GameConfig.Fx.WavePulse*`）のみ。ゲーム一時停止・専用VFX/モデル追加禁止。
- 敵ヘヴィ変種（MDL-03）は MDL-02 プレハブ複製＋マテリアル差し替えのみ（新規ジオメトリ生成禁止・gdd/art-bible 決定）。実装余力が無ければ省略可。

## 8. UI（Canvas/描画の規約）

- 全 UI Canvas は `RenderMode.ScreenSpaceCamera` ＋ `worldCamera` にメインカメラを割当（tech-stack-unity 規約14。QA の RenderTexture 撮影に写すため）。PlayMode テストに `renderMode` のスモークチェックを置く。
- Menu の4タブ（はじめる/統計/アップグレード/設定）とフォーカス挙動は gdd「Menu 画面構成」と一致。フォーカスは項目リスト先頭/末尾で止まる（ラップしない）。統計タブは表示専用。
- クリスタル残高/コスト表示には IMG-03 アイコンを使用。色・発光は art-bible.json のパレット/輝度差方針に従う。

## 9. テスト規約（このゲームの検証観点）

- 新規 System（pure C#）を追加したら EditMode テストを同時に足す（回避無敵窓・スコア式・ウェーブ導出・アップグレード補正・セーブ検証はロジック中核なので必須）。
- PlayMode テストは `InputTestFixture` 継承で入力を擬似発行（batchmode の Game View フォーカス問題回避・rule 8）。永続化テストは `Application.temporaryCachePath` の一時ファイルを使い `[TearDown]` で削除（`persistentDataPath` 直使用禁止）。
