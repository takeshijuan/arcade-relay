# Conventions — Crystal Bastion（仮）ゲーム固有コード規約

正本は `.claude/docs/tech-stack-unity.md`「コード規約」節（15項目）と `rules/unity-code.md`。ここには**重複を避け、このゲーム固有の追加則のみ**を書く。tech-stack の規約（マジックナンバー禁止・delta-time・薄い Components・入力抽象化・AssetKeys・シーン構成固定・ScreenSpaceCamera・永続化 I/O 集約・batchmode 失敗の exit 昇格・InputTestFixture 等）は前提として全て適用される。

## 1. 命名規約

- **名前空間**: ルート `ForgeGame`。層ごとに `ForgeGame.Systems` / `ForgeGame.Systems.Meta` / `ForgeGame.Persistence` / `ForgeGame.Components` / `ForgeGame.Input` / `ForgeGame.Ui` / `ForgeGame.EditorTools` / テストは `ForgeGame.Tests.EditMode` / `ForgeGame.Tests.PlayMode`。
- **System クラス**: `<機能>System`（例 `BuildSpotSystem`・`TowerCombatSystem`）。`static` クラス or 純粋インスタンスクラスとし、状態は引数と戻り値で受け渡す（内部可変状態を持つ場合もシーン API に触れない）。
- **Component（MonoBehaviour）**: `<エンティティ><役割>`（例 `TowerView`・`EnemyView`・`CoreView`・`BuildSpotView`）。「View/Controller」でロジックでなく配線層であることを示す。
- **UI クラス**: `<シーン/要素>Screen` または `<要素>Panel`（例 `TitleScreen`・`MenuScreen`・`ResultScreen`・`HudPanel`・`TowerSelectPanel`・`OutgamePanel`・`SettingsPanel`・`PausePanel`）。
- **enum 値**: `design/gdd.md` の固有名詞に一致（`TowerType.BastionCannon`/`.ArcEmitter`、`EnemyType.Marauder`/`.Warbeast`）。表示名を勝手に変えない。

## 2. GameConfig 集約の徹底（このゲーム固有）

- 全数値は `GameConfig` の対応する入れ子クラス（`Core`/`Economy`/`Build`/`Wave`/`BastionCannon`/`ArcEmitter`/`Tower`/`Marauder`/`Warbeast`/`Score`/`Meta`/`Save`/`Presentation`）に**既に定義済み**。実装 story は**新しい数値リテラルを足す前に GameConfig を探す**。無ければ GameConfig に追加してから使う（story 内での直書き禁止）。
- config の初期値は **gdd 数値表の初期値そのまま**。技術都合で変えたい場合は「提案＋根拠」を game-designer へ返す。実装で勝手に変えない（例: 「WAVE_COUNT を減らすとテストが速い」で 8→4 は禁止）。
- 装飾専用の値（演出のボブ振幅・フラッシュ時間等）は `GameConfig.Presentation` に置く。gdd で調整レンジ不要と明記されたもののみここに入れてよい。
- 資産パスは `GameConfig.AssetKeys`、シーン名は `GameConfig.Scenes` 経由。`"Generated/..."` や `"Game"` のような文字列直書きを Components/Ui/Systems に書かない。

## 3. 撃破帰属ルールの実装契約（ACH-04 の基盤・gdd 撃破の帰属ルール節）

- `TowerCombatSystem` の全ダメージ適用イベントは**発生源タワー種別（`TowerType`）を必須フィールド**として持つ。省略した実装は CR-CODE 差し戻し対象。
- 撃破帰属は **final-hit 方式**（HP を 0 以下にした最終ダメージの発生源）。貢献度按分は実装しない。
- 同フレーム複数ヒットの決定は「シーン内タワーコンポーネントの登録順（ヒエラルキー順）」に従う決定論的順序。狙って操作可能な要素にしない。
- `EnemyHealthSystem` は撃破時に **総撃破数**（`killCount`・種別問わず）と **AoE撃破数**（Arc Emitter 帰属のみ・単一ラン内）を**分離集計**する。AoE撃破数はラン開始/リスタートで 0 リセット。両方を `RunResult` に載せる。

## 4. メタ進行の純粋性（P-04 の実装契約）

- `MetaProgression.ApplyRunResult` は**純粋 reducer**: 引数 `SaveData` を破壊せず（`Clone()` 経由）新インスタンスを返す。`Debug.*`・時刻取得・乱数・I/O を書かない。
- 実績解放は**単調**（一度 true になったフラグを false に戻さない）: `next.achX = prev.achX || <条件>`。
- essence・統計は**勝敗に関わらず確定加算**（P-04）。「敗北時は加算しない」実装は P-04 違反で REJECT。
- UPG 効果は**ラン初期条件のみ**を変える（初期資金・設置/強化コスト割引・essence 獲得率）。敵側パラメータ（HP/速度/出現数）を弱化する UPG 効果を実装しない（concept 設計判断・gdd 明記）。

## 5. セーブ SaveData の命名例外（このゲーム固有の明示）

- `SaveData.save_version` の**このフィールドだけ** C# の PascalCase/camelCase を外れて snake_case とする。理由: contract §6 が JSON 先頭キーを `save_version` と規定し、`JsonUtility` はフィールド名をキーへそのまま写す（remap 不可）ため。他フィールドは gdd「セーブデータ方針」表の名称（`highScore`・`bestClearTimeSec` 等）に一致させる。
- `SaveData` は `[System.Serializable]`・フラット・全プリミティブ（int/float/bool）を維持する。`Dictionary`・ネスト・配列を足さない（`JsonUtility` 制約）。フィールド追加時は `SaveData.CreateDefault()`・`MetaSchema.Validate()`・migration を同時に更新する。
- `recovered` は SaveData に永続化しない（`LoadResult` のロード時フラグ）。

## 6. Game シーンの一時停止（gdd 操作仕様）

- Esc の一時停止は **Game シーンでのみ**有効。`Time.timeScale = 0` で全停止（タワー攻撃・敵移動・タイマー）。Systems 側は delta-time 駆動なので `timeScale=0` で自動停止する（フレーム固定加算を書かない前提が効く）。
- Title/Menu/Result 中の Esc は無反応。

## 7. UI Canvas（tech-stack 規約14 の徹底）

- 全 UI Canvas は `UiCanvasHelper.ConfigureScreenSpaceCamera(canvas, mainCamera)` で構成する（`RenderMode.ScreenSpaceCamera` + `worldCamera`）。`ScreenSpaceOverlay` 禁止（QA の RenderTexture 撮影に写らない）。
- 各 UI シーンの PlayMode テストに `Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode)` のスモークチェックを置く。

## 8. テスト配置

- **EditMode**（`Assets/Tests/EditMode/`）: Systems/Meta の純粋ロジック（設置判定・戦闘解決・スコア・reducer・スキーマ検証）を Unity 起動なしで検証。System を1つ足したらそのテストをここに追加する。
- **PlayMode**（`Assets/Tests/PlayMode/`）: コアループ1周・必須シーン遷移（Title→Menu→Game→Result→Menu）・永続化（保存→再ロード→一致 / 破損→`.bak`+`[SaveCorruption]`）・視覚サニティ。入力擬似発行は `InputTestFixture` 継承必須（batchmode でのフォーカス握り潰し回避）。
- 破損ログの検知は `LogAssert.Expect(LogType.Error, new Regex("^\\[SaveCorruption\\]"))` のホワイトリスト方式。

## 9. Editor スクリプトの exit 規律（tech-stack 規約11 の徹底）

- `Editor/` の `-executeMethod` 入口（`ForgeBuild.BuildMac`・`ForgeScaffold.GenerateScenes`・将来の資産取込）は、回復不能エラーを `Debug.LogError` + `return` で済ませず、`EditorApplication.Exit(1)` か例外 throw で**非0終了**させる。壊れた状態でシーン/アセットを保存しない。
- `-executeMethod` は完全修飾名で呼ぶ（`ForgeGame.EditorTools.ForgeBuild.BuildMac`）。
