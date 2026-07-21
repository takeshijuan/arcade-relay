# ArcadeRelay 技術スタック規約（game/ 配下・engine=unity の正本）

> エンジン選択は contract.md §11（`state/engine.txt`）。このファイルは **engine=`unity`（3D）** の正本。
> phaser は tech-stack.md、unreal は tech-stack-unreal.md。共通思想（マジックナンバー禁止 / delta-time / エンジン非依存コア / 入力抽象化 / 資産キー集約）は全エンジン同一で、ここでは Unity イディオムに翻訳する。

## スタック（固定）

- **Unity 6.3 LTS（6000.3.x）** + **URP** + **C#** + **Input System**（`com.unity.inputsystem`）+ **Unity Test Framework**（`com.unity.test-framework` 1.6）
- レンダリングは **URP 固定**（Built-in は Unity 6 で非推奨。HDRP は macOS/Metal でレイトレーシング不可・設定分散のため不採用。URP は設定が URP Asset 1つに集約されコード管理しやすい）
- `game/` は自己完結 Unity プロジェクト。使用エディタは preflight が `state/engine-info.json` に解決した `binary`（以下 `$UNITY`）を使う。**実行中のバージョン再解決禁止**
- 3D 資産インポート: 静的モデルは **GLB（`com.unity.cloud.gltfast`）**、リグ・アニメーション付きは **FBX（ネイティブ ModelImporter）**。glTFast は Humanoid Avatar 非対応のため、ヒューマノイドは FBX 経由で `ModelImporter.animationType = Human` を設定する

## プロジェクト生成（scaffold）

```bash
UNITY="$(jq -r .binary state/engine-info.json)"
# 1) URP 系テンプレートを探す（優先順: Hub 管理下 → エディタバンドル内）
TPL="$(ls "$HOME/Library/Application Support/UnityHub/Templates"/com.unity.template.urp-blank-*.tgz \
        "$(dirname "$(dirname "$UNITY")")/Resources/PackageManager/ProjectTemplates"/com.unity.template.3d-cross-platform-*.tgz \
        2>/dev/null | sort -V | tail -1)"
[ -n "$TPL" ] && "$UNITY" -batchmode -quit -createProject "$PWD/game" -cloneFromTemplate "$TPL" -logFile -
# 2) 無ければ空プロジェクト生成 → Packages/manifest.json に依存を明記 → 再起動でインポート
[ -z "$TPL" ] && "$UNITY" -batchmode -quit -createProject "$PWD/game" -logFile -
```

テンプレート適用時の後処理: `SampleScene` は削除し Boot/Title/Menu/Game/Result の5シーンに正規化、EditorBuildSettings を5シーンで再構成する（contract §11 必須シーン集合）。`3d-cross-platform` テンプレートは URP / Input System / Test Framework 同梱（追加が必要なのは通常 glTFast のみ）で、`activeInputHandler` は既定で Input System（new）。

必須パッケージ（`game/Packages/manifest.json` の dependencies に明記。バージョンはエディタ推奨解決に任せてよい）:
`com.unity.render-pipelines.universal` / `com.unity.inputsystem` / `com.unity.cloud.gltfast` / `com.unity.test-framework`

テンプレ無しで生成した場合は、エディタスクリプトで URP Asset を生成し Graphics Settings に割り当てるまでを scaffold の完了条件とする。

## ディレクトリ構造

```
game/
  Assets/
    Scenes/                 # Boot / Title / Menu / Game / Result の5シーン（contract §11 必須シーン集合。gdd のゲームフローと一致）
    Scripts/
      GameConfig.cs         # ★全ゲームパラメータ + AssetKeys（パス文字列の唯一の置き場）
      Types.cs              # 共有型（EntityState 等）
      Systems/              # エンジン非依存ロジック（pure C#。MonoBehaviour/シーンAPI/File I/O 禁止）
        Meta/               # メタ進行ロジック（MetaTypes.cs / MetaSchema.cs / MetaProgression.cs — pure C#・contract §11）
      Persistence/          # 永続化 I/O 層（File I/O・persistentDataPath の唯一の置き場。Systems/ から直接 I/O 禁止）
      Components/           # MonoBehaviour（ライフサイクルと配線のみ）
      Input/                # 入力集約（Input System。アクションはコードで生成）
      Ui/                   # HUD・メニュー（uGUI/UI Toolkit）
      Editor/
        ForgeBuild.cs       # ビルド・検証用 static メソッド（-executeMethod の入口）
    Generated/              # AI生成資産の取込先（raw からコピーして Unity にインポートさせる）
    Tests/
      EditMode/             # コンパイル検証を兼ねる最小テスト（必ず1本以上置く）
      PlayMode/             # コアループ検証・永続化検証・スクリーンショット取得テスト
  Packages/manifest.json
  ProjectSettings/          # ProjectVersion.txt がプロジェクトマーカー（contract §11）
  _generated/               # raw 生成資産 + MANIFEST.jsonl（Assets/ 外 = Unity はインポートしない）
```

バージョン管理除外（.gitignore 済み）: `Library/ Temp/ Logs/ UserSettings/ obj/ Build/`。`Assets/ Packages/ ProjectSettings/` はコミット対象。`.meta` はコミットする（Visible Meta Files / Force Text が既定）。

## 検証コマンド

`$UNITY` は `state/engine-info.json` の `binary`。**`-runTests` と `-quit` は併用禁止**（テスト完了前にエディタが終了する既知問題）。

| 目的（phaser 対応） | コマンド | 合格条件 |
|---|---|---|
| typecheck 相当 | `"$UNITY" -batchmode -projectPath game -runTests -testPlatform EditMode -testResults "$PWD/qa/evidence/editmode-results.xml" -logFile -` | exit 0 かつ結果XMLに failed 0（コンパイルエラーでもテスト起動が失敗するため typecheck を兼ねる） |
| build 相当 | `"$UNITY" -batchmode -projectPath game -executeMethod ForgeBuild.BuildMac -quit -logFile game/Logs/build.log` | exit 0 かつログに `Build succeeded`。成果物 `game/Build/ForgeGame.app` |
| test（QA用） | `"$UNITY" -batchmode -projectPath game -runTests -testPlatform PlayMode -testResults "$PWD/qa/evidence/playmode-results.xml" -logFile -` | exit 0 かつ failed 0 |
| dev/preview 相当（人間向け） | `open -a "$UNITY_APP" --args -projectPath "$PWD/game"`（エディタで開く）または `open game/Build/ForgeGame.app`（ビルド済みを起動） | — |

`ForgeBuild.BuildMac` は `Assets/Scripts/Editor/ForgeBuild.cs` の static メソッドで、`BuildPipeline.BuildPlayer`（`BuildTarget.StandaloneOSX`・Apple silicon）を呼び、失敗時は `EditorApplication.Exit(1)` で非0終了させる。**`-executeMethod` は名前空間込みの完全修飾名で指定する**（例: `ForgeGame.EditorTools.ForgeBuild.BuildMac`。namespace に置いた場合、裸名では解決されない）。

**単一インスタンスロック（重要）**: Unity は同一プロジェクトを同時に1エディタプロセスしか開けない。**Unity を起動する工程（テスト実行・ビルド・エディタスクリプトによる資産取込）は全て直列化すること**。並走レーン設計（Build∥AssetGen）では、AssetGen 側は生成と Unity 外の機械検証（gltf validate / Blender 検査）までに留め、エンジン取込は Integrate フェーズ（直列区間）で行う。

**検証バッチ化（Build/Polish 並走レーン規約 — retro-e2 案A+B）**: コード story の実装は assignee レーン（gameplay/ui）で並走するため、**レーン中の agent は Unity を一切起動しない**（上記ロックと衝突する）。story ごとの検証は「参照する型・メンバ・アセットキー・シリアライズ対象の実在を Read/Grep で静的確認」までとし、EditMode+build の一括検証は**レーン合流後のバッチ検証区間（直列）**で行う。バッチ検証で失敗した場合はエラーのファイルパスと `git log --oneline -- <path>` で原因 story を特定（困難なら story コミット単位の二分探索）し、最小修正と原因 story を `state/reviews/batch-verify.md` に記録する。検証粒度が粗くなるトレードオフはこの切り分け規約で緩和する（正本実装は workflow の batchVerify）。

## コード規約（rules/unity-code.md が編集時に強制する内容の正本）

1. **マジックナンバー禁止** — 全ゲームパラメータは `Assets/Scripts/GameConfig.cs` の静的定数クラスに集約。チューニングは GameConfig.cs だけで完結させる
2. **delta-time 必須** — `Update()` は `Time.deltaTime`、物理は `FixedUpdate()` + `Time.fixedDeltaTime`。フレームレート依存の実装禁止
3. **Components は薄く** — MonoBehaviour はライフサイクルと配線のみ。ロジックは `Systems/` の純粋 C#（MonoBehaviour 継承・`GameObject.Find`/`Instantiate`/`GetComponent` 禁止。`Vector3`/`Mathf` 等の値型は可）
4. **入力抽象化** — Input System を使い `Scripts/Input/` に集約。旧 `Input.GetKey` 禁止。アクションは **コードで生成**（`new InputActionMap(...)` + `AddAction`/`AddBinding`。`.inputactions` JSON の直接編集はスキーマ非公開のため禁止）
5. **資産参照はキー集約** — 動的ロードのパス/アドレスは `GameConfig.cs` の `AssetKeys` 経由。`Resources.Load("文字列直書き")` 禁止。インスペクタ直参照（SerializeField）は可
6. **シーン構成固定** — Boot / Title / Menu / Game / Result の5シーン（contract §11 必須シーン集合。正準フロー: Boot→Title→Menu→Game→Result→{Game|Menu}）。遷移トリガーは gdd のゲームフローに一致させる。Menu の必須要素: プレイ開始・アウトゲーム表示（アンロック/実績/統計）・設定（音量・操作表示）・終了導線
7. **テスト必須** — EditMode に最低1本（コンパイル検証を兼ねる）、PlayMode にコアループ1周（開始→挑戦→結果→リスタート）を検証するテストを置く
8. **PlayMode の入力擬似発行は `InputTestFixture` 必須** — batchmode は Game View がフォーカスを持たないため、生の `InputSystem.QueueStateEvent` は既定設定（PointersAndKeyboardsRespectGameViewFocus）で握り潰され、`InputAction` 側が反応しない。scaffold 時点で `Packages/manifest.json` に `"testables": ["com.unity.inputsystem"]` を追加し、PlayMode の asmdef から `Unity.InputSystem.TestFramework` を参照して `InputTestFixture` を使うこと
9. **Components の Awake 配線罠** — 配線フィールドを `Awake()` で読むコンポーネントをテストから組む時は、GameObject を非アクティブで生成→フィールド注入→アクティブ化（`Awake()` を遅延させる）
10. **プレースホルダ⇔実モデル両対応の Renderer 参照** — `[RequireComponent(typeof(Renderer))]` 禁止（抽象型は自動付与不可・リグ付き FBX では SkinnedMeshRenderer が子に付く）。`GetComponentInChildren<Renderer>()` + null チェックで参照する
11. **batchmode ツールの失敗は exit code に昇格必須** — `-executeMethod` で呼ぶエディタスクリプト（ビルド・取込・シーン配線）は、回復不能エラーを `Debug.LogError` + `return` で済ませない（batchmode は exit 0 のまま = ハーネスの成否判定が失敗を成功と誤認する）。例外を throw するか `EditorApplication.Exit(1)` で必ず非0終了させ、壊れた状態でシーン/アセットを保存しない
12. **配線破損は Start で1回 LogError** — Components 層の「Editor ツールが注入する前提」のフィールドが null の場合、毎フレーム無言 return で隠さず `Start()` で1回 `Debug.LogError` を出す（縮退が正当なケースはヘッダコメントで文書化した上で LogWarning）
13. **アニメ切替はコードでなく AnimatorController 資産** — MDL のアニメ切替（idle/run 等）は `UnityEditor.Animations` API でエディタスクリプトから AnimatorController を生成し（states/transitions/threshold 条件。追加 asmdef 参照不要）、コード側は `animator.SetFloat` でパラメータを流すだけにする。**Unity 自動生成の `__preview__` クリップを選ばないこと**（クリップ検索で名前 `__preview__` を除外）
14. **HUD/メニューの Canvas は `RenderMode.ScreenSpaceCamera` 固定** — QA-PLAY の RenderTexture 撮影は Screen Space - Overlay の Canvas を構造的に写せない（Overlay は特定カメラに紐付かず、単一カメラの RenderTexture に一切描画されない）。全 UI Canvas は ScreenSpaceCamera とし `worldCamera` にメインカメラを割り当てる。PlayMode テストに `Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode)` のスモークチェックを置く
15. **永続化 I/O は `Assets/Scripts/Persistence/` に集約** — `Application.persistentDataPath`・`File` I/O・`PlayerPrefs` を Systems/・Components/・Ui/ から直接呼ばない。メタ進行ロジック（`Systems/Meta/` の pure C#）は値を受けて値を返すのみで、保存・読込は Persistence 層が仲介する（「セーブ / 永続化」節参照）

## 資産の取り扱い

- raw 生成物と MANIFEST.jsonl は `game/_generated/`（contract §6/§11）。取込は `game/Assets/Generated/` にコピーし Unity のインポートに任せる
- リグ付きキャラ: FBX を取込後、エディタスクリプトで `ModelImporter.animationType`（Humanoid なら `Human`、クリーチャー等は `Generic`）を設定し Avatar を生成。アニメーション FBX も同一スケルトンでインポートしリターゲット。**取込（Integrate）の必須検証**: `Avatar.isValid`（Humanoid 化成功）とアニメクリップの存在をエディタスクリプトで機械確認し、失敗時は `Generic` へ縮退した上で MANIFEST に注記する
- 静的プロップ/環境: GLB を `Assets/Generated/` に置き glTFast の ScriptedImporter に処理させる
- 音声: Unity は Ogg Vorbis / WAV をネイティブ対応。**OGG のみで良い**（Safari 用 M4A は不要 — phaser 専用要件）
- スケール検証必須: Unity は 1 unit = 1m。取込後にバウンディングボックスを検査し、想定サイズ（ヒト型 ≈ 1.6–2.0m）から外れていたら ModelImporter の scaleFactor で補正

## QA-PLAY の実行方法（gates.md QA-PLAY の unity 節から参照される）

1. build 相当コマンドが exit 0
2. PlayMode テストでコアループ1周＋**必須シーン遷移 `Title → Menu → Game → Result → Menu` の1周**を実操作相当（`InputTestFixture` による入力の擬似発行）で検証。`LogAssert.NoUnexpectedReceived()` で console エラー 0 を機械検証
3. スクリーンショット証跡: PlayMode テスト内から `qa/evidence/` に保存（**-nographics は使わない**）。`ScreenCapture.CaptureScreenshot()` は batchmode で機能しないことがある（バックバッファ無し）— その場合はカメラを `RenderTexture` にレンダリングして `Texture2D.ReadPixels` → `EncodeToPNG` で保存する方式に切替える（UI はコード規約14の ScreenSpaceCamera Canvas なら同カメラに写る。Overlay のままの撮影は「UI キャプチャ不能」として不合格）。撮影方式の成否は機械判定を先行させる: `magick identify -format "%[fx:mean]" <shot>.png` が 0.02 未満/0.98 超なら SUSPECT_BLANK として方式を切替えて再撮影。**撮影した画像は必ず Read で目視し、対象（モデル・UI 文字）が実際に写っていることを確認**（黒画面・文字欠落は不合格。値の内部整合性テストだけではレンダリング欠陥を検知できない）
4. acceptance は stories.yaml の各項目を PlayMode テストとして実装・実行
5. **視覚サニティテスト（必須・LogAssert では検知できない欠陥クラス）**: PlayMode テストに以下を置く —
   - NaN 座標検査: `Assert.IsFalse(float.IsNaN(player.position.x) || float.IsNaN(player.position.y) || float.IsNaN(player.position.z))`
   - カメラ向き検査: `Assert.Greater(Vector3.Dot(cam.transform.forward, (target.position - cam.transform.position).normalized), 0.2f)`（主要被写体を向いているか）
   - マテリアル欠落検査: 全 Renderer の sharedMaterials に null / `InternalErrorShader`（ピンク）が無いこと
   - Animator 進行検査（MDL 使用時）: 現在 state が `__preview__` でないこと、および `GetCurrentAnimatorStateInfo(0).normalizedTime` が 0.2 秒待機の前後で進んでいること（固着検知）
6. **メタ進行の永続化テスト（必須）**: gates.md QA-PLAY 観点5 のとおり、(a) 保存→新規インスタンスで再ロード→復元一致、(b) 破損データ→`.bak` 退避＋`[SaveCorruption]` エラー1回（`LogAssert.Expect(LogType.Error, new Regex("^\\[SaveCorruption\\]"))` でホワイトリスト検知）＋既定値復旧、を PlayMode テストで検証。テストのセーブ先は「セーブ / 永続化」節の一時パス規約に従う

## セーブ / 永続化（contract §6 のセーブ規約の unity 実装正本）

- **保存先**: `Application.persistentDataPath/save.json`。書込は `.tmp` に書いてからコピー/リネームするアトミック方式（書込中クラッシュで本体を壊さない）
- **形式**: JSON。先頭フィールド `save_version`（int・必須）。シリアライザは Unity 公式の `JsonUtility` を第一候補とする（追加依存なし。Dictionary 非対応のため SaveData はフラットな配列+プレーンクラスで設計する）。`System.Text.Json` は Unity ランタイムでの同梱状況が構成依存のため、使う場合は EditMode テストでシリアライズ往復を先に検証してから採用する
- **層の分離**（contract §11）: メタ進行ロジック = `Assets/Scripts/Systems/Meta/`（pure C#。`MetaTypes.cs`=バージョン別プレーン型 / `MetaSchema.cs`=マイグレーション関数チェーン+検証 / `MetaProgression.cs`=RunResult を受けて新 SaveData を返す純粋 reducer）。I/O = `Assets/Scripts/Persistence/`（`FileSaveAdapter` 等。`persistentDataPath` 文字列はここだけ）
- **マイグレーション**: `save_version` が古ければ v(n)→v(n+1) の関数を順に適用。**現行より新しい版は変換せず破損相当として扱う**（暗黙ダウングレード禁止）。マイグレーション関数は追加のみ・書き換え禁止
- **破損時プロトコル（黙示初期化禁止 — rules/unity-code.md が強制）**: パース失敗・`save_version` 欠落・未来版・チェックサム不一致・スキーマ検証失敗（必須フィールド欠落・型不正）のいずれも、(1) 生データを `save.json.bak.<UTC時刻>` へ退避 → (2) `Debug.LogError("[SaveCorruption] reason=... backup=...")` を1回 → (3) 既定値で再生成し `recovered: true` を UI 層（Title/Menu）に伝播（トースト等の表示は任意だが、フラグの伝播は必須）
- **保存タイミング**: Result 到達時に `MetaProgression.ApplyRunResult` → 即 persist を1回（Result→リスタート連打で二重保存しない。メモリ上の SaveData を使い回す）
- **テスト規約**: PlayMode テストは実ユーザーのセーブを汚さないため `Application.temporaryCachePath` 配下の一時ファイル（テストごとに一意名）を使い、`[TearDown]` で削除する。`persistentDataPath` を直接使うテスト禁止

## 将来のエンジン非依存化に向けた線引き

- `Assets/Scripts/Systems/` は UnityEngine のシーン API・MonoBehaviour・File I/O を import しない（値型・数学型のみ可）— ここがエンジン非依存層（`Systems/Meta/` も同様）
- Unity 依存は `Components/` `Ui/` `Input/` `Scenes/` `Persistence/` に閉じ込める
