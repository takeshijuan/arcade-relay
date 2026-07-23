# ArcadeRelay ゲート・プロンプト・ライブラリ

> スキル/workflowはゲートを**IDで参照**する（プロンプト本文を各所にコピーしない＝ドリフト防止）。
> 判定者agentは応答の**1行目**に必ず `<GATE-ID>: APPROVE|CONCERNS|REJECT` を出す（contract.md §5）。
> APPROVE=合格 / CONCERNS=指摘付き（revise対象リスト必須）/ REJECT=根本要修正（理由必須）。

## DR-CONCEPT（design-reviewer → design/concept.md）

以下の観点で design/concept.md を批評せよ:
1. **面白さの仮説が反証可能か** — 「何が楽しいのか」が1文で言え、プロトタイプで検証できる形か
2. **ピラー（P-xx）の質** — 3〜5個。互いに独立し、意思決定の裁定に使える具体性があるか（「楽しい」等の無内容ピラーは不可）
3. **コアループ** — 開始→挑戦→報酬→再挑戦が30秒で説明でき、1画面で成立するか
4. **スコープ** — 数時間の自律実装で到達可能か。過大ならカット候補を挙げよ
5. **MDA整合** — Mechanics が意図した Aesthetics に繋がる Dynamics を生むか
CONCERNSの場合、修正すべき箇所を優先度順の箇条書きで示せ。

## DR-GDD（design-reviewer → design/gdd.md）

以下の観点で design/gdd.md を批評せよ:
1. **concept.md との整合** — 全システムがいずれかのピラー P-xx を参照しているか。ピラーに寄与しないシステムは削除提案
2. **実装可能性** — 各システムが選択エンジン（`state/engine.txt`。engine別 tech-stack 文書のスタックと規約 — contract.md §11）で数時間で実装できる粒度に分解されているか
3. **数値の具体性** — 速度・HP・スコア等が「後で決める」でなく初期値＋調整レンジで書かれているか
4. **完結性** — 勝利/敗北条件、リスタート、ゲームフロー（必須シーン集合 `Boot→Title→Menu→Game→Result→{Game|Menu}` — contract §11。Menu の必須要素: プレイ開始・アウトゲーム表示・設定・終了導線）が定義されているか
5. **矛盾スキャン** — セクション間の食い違い
6. **アウトゲーム完結性** — 「メタ進行（アウトゲーム）」節（templates/gdd.md）が存在し、(a) ハイスコア/ベストタイム+統計が定義済み、(b) 選択要素（通貨/アンロック/実績/ラン間アップグレード）から2つ以上採用（2未満は CONCERNS。通貨は消費先が無い場合単独カウント不可。スコープ制約〔観点2〕と衝突しても下限2は削らず「実装コスト最小の組 — 例: アンロック=解放フラグのみ+実績=判定式のみ — で満たせ」と指摘する）、(c) 各要素がピラー P-xx に紐づく、(d) 数値パラメータを持つ要素（通貨・アップグレード、および解放条件/実績条件の式に含まれる閾値）が初期値+調整レンジで書かれ「後で決める」が無い、(e) セーブ対象キーと初回起動時の挙動（セーブ無し時の初期状態）が定義済み、(f) ID が contract §8 の `ACH-xx`/`UNL-xx`/`UPG-xx` 形式か

## AR-BIBLE（art-reviewer → design/art-bible.md + key image）

以下の観点で批評せよ:
1. **スタイルロックの機械可読性** — art-bible.json に固定スタイル記述ブロック・hexパレット・style_codes が揃い、全プロンプトに前置可能な形か
2. **ゲーム内可読性** — このスタイルでプレイヤー/敵/背景のシルエットが即座に区別できるか（ゲームは1画面で秒単位の判断）
3. **生成再現性** — 30資産を数時間かけて生成してもブレない指定か（曖昧形容詞のみの指定は不可）
4. **技術整合** — 解像度・タイルサイズ・透過方針が assets.md / 選択エンジンの tech-stack 文書（contract §11）と一致するか。engine=unity/unreal では「3D スタイル方針」節（ポリゴン予算・テクスチャ/PBR・リグ方針・スケール規約）が存在し assets-config.md の 3D 既定と矛盾しないか

## AR-ASSET（art-reviewer → 生成資産バッチ）

各資産を design/art-bible.json に照らして採点せよ:
1. スタイル一致（パレット逸脱・画風ブレ）
2. シルエット可読性（ゲーム内サイズに縮小して判別可能か）
3. アルファ縁品質（白フチ・ジャギ・背景残り）
4. 仕様一致（design/assets.md のサイズ・向き・フレーム数）

音声資産（SFX/BGM）の場合は 1〜4 に代えて以下を ffmpeg/ffprobe で機械検査せよ:
1. **ラウドネス** — 実測 -16 LUFS ±1 に収まっているか（`ffmpeg loudnorm` 実測値）
2. **ループ品質（BGM/ループ素材）** — 2連結してシームのクリック/RMS段差をスキャンし段差が無いか
3. **仕様一致** — duration が design/assets.md の指定と一致し、エンジン既定形式（phaser: OGG+M4A の両方 / unity: OGG / unreal: WAV — 各 tech-stack 文書）で存在するか
4. **音要件突合** — design/assets.md の音要件（ジャンル/BPM/キー/ループ可否）と一致するか

3D資産（MDL/ANM。engine=unity/unreal）の場合は 1〜4 に代えて以下を検査せよ（assets-config.md「生成後パイプライン」の 3D 節を機械実行）:
1. **仕様準拠** — GLB は `npx @gltf-transform/cli validate` でエラー0。**FBX は Blender headless で import → GLB export → 同じ validate を通す**（変換不能・エラーは不合格。FBX を素通りさせない）。加えて構造検査（Blender headless 再インポートでトポロジ・ボーン名・クリップ有無）
2. **予算・構造** — polycount が design/assets.md の指定内（既定: hero ≤ 50k / prop ≤ 10k / 環境 ≤ 100k tri）、非多様体・浮遊ジオメトリ・法線反転が無いか、マテリアル数が仕様内か
3. **スケール・向き** — MANIFEST の `bbox_authoring_m`（authoring-time 計測。記録漏れは不合格）が想定サイズ（ヒト型 1.6–2.0m 相当。UE は cm 換算）に収まり、前方軸・アップ軸が正しいか
4. **リグ（rigged 資産のみ）** — ボーン数が仕様内、バインドポーズ正常、指定アニメクリップが全て存在するか
5. **スタイル一致** — レンダリングプレビュー（Blender headless レンダリング。取込済みならエンジン内スクリーンショットでも可）を design/art-bible.json のコンセプト画・パレットに照らして画風ブレが無いか
6. **provenance/plan_tier** — MANIFEST に `plan_tier` 実測値と `license` があるか。`shippable: false` ルート（state/asset-routing.json）由来・`cost_estimated: true`・fal 経由 Meshy（ライセンス継承未検証）は指摘として明示する
不合格資産は理由と再生成指示（プロンプト修正案）を付けよ。

**※ エンジン取込後検証（AR-ASSET の後段・Integrate 実施者の責務）**: FBX のエンジン取込成功・取込後バウンディングボックス・アニメ再生（unity: Humanoid Avatar 生成成功=`Avatar.isValid` / unreal: IK Retargeter マッピング成功）は Unity/UE の起動を要するため、AssetGen 並走レーンの AR-ASSET では判定対象外（単一インスタンスロック — 各 tech-stack 文書）。これらは **Integrate（直列区間）実施者が機械検証し、構造化返却で workflow に報告する**。失敗・縮退（Humanoid→Generic 等）は workflow が未解決事項として蓄積し Checkpoint で必ず人間に提示する（MANIFEST 注記だけで済ませない）。

## CR-CODE（既存コードレビューを利用 → game/ のコード変更。対象パスは contract.md §11）

新規agentは使わない。`/code-review` スキル、または `pr-review-toolkit:code-reviewer` + `pr-review-toolkit:silent-failure-hunter` を story 単位の diff に対して起動する。
判定の読み替え: findings 0件 = APPROVE / 修正可能な指摘 = CONCERNS / 設計欠陥 = REJECT。
加えてエンジン別コード規約 rule（contract.md §11 の表: phaser=`rules/gameplay-code.md`+`rules/ui-code.md` / unity=`rules/unity-code.md` / unreal=`rules/unreal-code.md`。共通してマジックナンバー禁止・delta-time・エンジン非依存コア・永続化 I/O の Persistence 層集約）への違反を確認する。
**並走レーン中の前提**（Build/Polish の assignee レーン並走 — 各 tech-stack 文書「検証バッチ化」節）: 他レーンの story が提供予定の API への参照は、docs/architecture.md の設計に合致していれば「実体未実装」だけを理由に blocker/REJECT としない（コンパイル整合はレーン合流後のバッチ検証が保証する。設計との不一致・誤用は通常どおり指摘してよい）。
特に確認する silent-failure パターン:
- diff 内で `LogError`/`console.error`/`UE_LOG(Error)` が Warning 級へ降格されている場合、直前のヘッダコメントで縮退の正当性が文書化されていなければ CONCERNS 以上（Warning は QA のエラー0検査を素通りするため、バグ隠しの抜け道になる）。**新規コードが回復不能条件を最初から Warning 以下で記録している場合も同様**（「降格」の形を取らない同種の抜け道）
- batchmode ツール（`-executeMethod` 等）の回復不能エラーを LogError+return で握り潰していないか（throw か Exit(1) で非0終了が必須 — 各 tech-stack 文書）
- セーブ破損時に `.bak` 退避＋明示エラーログ無しで黙って初期化していないか（contract §6）

## QA-PLAY（qa-lead → 動く game/）

game/ を実際にビルド・起動・操作して判定せよ。**実行手段はエンジン別**（`state/engine.txt` を読み、該当節に従う）:

- **phaser**: headless ブラウザ（Playwright 等）で `npm run build` → preview を開き実操作
- **unity**: tech-stack-unity.md「QA-PLAY の実行方法」— batchmode ビルド exit 0 + PlayMode テストで入力擬似発行によるコアループ検証 + `LogAssert.NoUnexpectedReceived()` でエラー0 + RenderTexture 方式込みのスクリーンショット証跡（-nographics 不使用）+ 視覚サニティテスト（NaN 座標・カメラ向き・マテリアル欠落・Animator 進行）
- **unreal**: tech-stack-unreal.md「QA-PLAY の実行方法」— BuildCookRun exit 0 + Automation RunTests（レポート JSON で failed 0）+ スクリーンショット証跡（`-nullrhi` は描画不可のため撮影時は必ず外す）

共通判定観点:
1. **起動** — build成功、エンジン相当の console/ログエラー0で起動するか
2. **コアループ** — design/gdd.md 記載の操作でコアループが1周できるか（開始→挑戦→結果→リスタート）。加えて**必須シーン遷移 `Title → Menu → Game → Result → Menu` が実操作（unity/unreal は自動テストの入力擬似発行）で1周できること**（contract §11 の正準フロー。Menu のプレイ開始・アウトゲーム表示・設定・終了導線の実在も確認）
3. **受け入れ条件** — 対象ストーリー（state/stories.yaml）の acceptance を1つずつ実操作（unity/unreal は自動テストによる入力発行）で検証
4. **ピラー検証** — 実プレイ感が P-xx を裏切っていないか（例: 「爽快感」ピラーなのに操作遅延がある等）
5. **メタ進行の永続化** — (a) ラン結果の保存 → プロセス再起動相当（新規インスタンス生成＋ディスク/ストレージからの再ロード）→ 値が復元されることを自動テストで検証、(b) 破損セーブデータを与えた場合に黙って初期化せず `.bak` 退避＋`[SaveCorruption]` 明示エラーログ1回＋既定値復旧＋`recovered` フラグが UI 層に伝播されることを検証（contract §6。破損ケースには「パース不能データ」に加え**「valid JSON/ロード可能だがスキーマ不正（必須フィールド欠落・型不正）」を最低1件含める**。テストは実ユーザーのセーブ先を汚さない一時パスを使う — 各 tech-stack 文書「セーブ / 永続化」）

証跡（スクリーンショット/録画/テスト結果XML・JSON）を qa/evidence/ に保存し、qa/report.md に結果を書け。
**視覚証跡の目視義務（全エンジン共通）**: 撮影した各スクリーンショットは必ず Read で目視し、対象（モデル・UI 文字）が実際に写っていることを確認する。黒画面・文字欠落・ピンク（マテリアル欠落）は不合格。目視の前に機械検知を先行させる: `magick identify -format "%[fx:mean]" <shot>.png` が 0.02 未満または 0.98 超なら黒/白飛びの疑い（SUSPECT_BLANK）として撮影方式を切替えて再撮影する。**証跡ファイルが実在しない・0バイト・目視未実施の PASS は判定無効**。

## CD-CHECKPOINT（creative-director → Checkpoint A/B/C 提示前）

人間に見せる直前の最終判定。以下を確認せよ:
1. **ビジョン一貫性** — 成果物一式が brief と ピラー P-xx から逸脱していないか
2. **提示品質** — 人間が5分で判断できる要約（何を作ったか/何を判断してほしいか/既知の課題）が用意されているか
3. **正直さ** — 未達・妥協点が隠されず列挙されているか。特に `[BLOCKER]` プレフィクス付き未解決事項・縮退（Humanoid→Generic / 実資産→プレースホルダ / Primary→Fallback / `shippable: false` ルート使用）は要約の冒頭で個別に警告されているか（箇条書きの中に埋没させない）
REJECTの場合、人間に見せる前に直すべき点を指示せよ。
