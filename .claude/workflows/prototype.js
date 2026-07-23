// ArcadeRelay Phase 2 — prototype.js
// 起動元: /forge-prototype（contract.md §4）。args = { reviewMode, engine?, checkpointAFeedbackPath? }（engine は contract §11 の3値。省略時 phaser）
// 遊べる縦串（コアループが1周する最小プロトタイプ）を自律制作し、Checkpoint B 素材を返す。
// 命名・ID・パスはすべて .claude/docs/contract.md に従う。ループ仕様は .claude/docs/review-loops.md。
// 注意: Workflow ランナーはこのスクリプト本体をトップレベル実行する（default export は使わない）。

export const meta = {
  name: 'prototype',
  description: 'Phase 2: gdd をストーリー分解し、コアループ縦串の実装と資産生成を並走、QA を経て Checkpoint B 素材を返す',
  phases: [
    { title: 'Setup', detail: 'tech-director が game/ スキャフォールド・docs/architecture.md・docs/conventions.md・state/stories.yaml を生成' },
    { title: 'Build', detail: 'phase:prototype ストーリーを assignee レーン（gameplay/ui）で並走実装（レーン内順次・レーン中はエンジン検証なし）し、story ごとに CR-CODE レビューループ（MAX 2）を通す。レーン合流後にバッチ検証を直列実行' },
    { title: 'AssetGen', detail: 'art-director がコアループ必須画像（engine=unity/unreal では 3D モデル MDL/ANM も）、audio-designer がコア SFX を生成し AR-ASSET ループ（MAX 3 + fallback 1）を通す' },
    { title: 'Integrate', detail: '担当 engineer が生成資産を資産キー正本（phaser: ASSET_KEYS / unity: GameConfig.AssetKeys / unreal: GameConfig.h 定数）経由で game/ に組み込み typecheck/build 検証（直列区間・エンジン取込込み）' },
    { title: 'QA', detail: 'qa-lead が QA-PLAY（実起動・実操作・証跡）を実施し、重大バグを修正ループ（MAX 2）' },
    { title: 'Final', detail: 'creative-director が CD-CHECKPOINT 判定し Checkpoint B 素材を整形して返す' },
  ],
};

// ---------------------------------------------------------------------------
// 共通定数（contract.md §6/§7 のパス。発明禁止）
// ---------------------------------------------------------------------------
const DOCS = {
  contract: '.claude/docs/contract.md',
  gates: '.claude/docs/gates.md',
  reviewLoops: '.claude/docs/review-loops.md',
  techStack: '.claude/docs/tech-stack.md', // engine 別 — 下の EP 確定後に engine 対応値へ差し替わる
  assetsConfig: '.claude/docs/assets-config.md',
};
const ART = {
  brief: 'design/brief.md',
  concept: 'design/concept.md',
  gdd: 'design/gdd.md',
  artBible: 'design/art-bible.md',
  artBibleJson: 'design/art-bible.json',
  assetsManifest: 'design/assets.md',
  architecture: 'docs/architecture.md',
  conventions: 'docs/conventions.md',
  manifest: 'game/assets/MANIFEST.jsonl', // engine 別 — 下の EP 確定後に engine 対応値へ差し替わる
  qaReport: 'qa/report.md',
  qaEvidence: 'qa/evidence/',
};
const STATE = {
  stories: 'state/stories.yaml',
  active: 'state/active.md',
  budget: 'state/budget.txt',
  assetRouting: 'state/asset-routing.json',
  reviewsDir: 'state/reviews',
};

// ---------------------------------------------------------------------------
// エンジンプロファイル（contract.md §11。値は各 tech-stack 文書の「検証コマンド」「規約」と一致させること）
// phaser の値は従来のプロンプト文字列と一字一句同一に保つ（後方互換）。
// ---------------------------------------------------------------------------
// args 正規化: 起動側/ランナーが JSON 文字列で渡してくるケースの防御（E2 で実測。
// パース不能な文字列は明示エラーに倒す — 黙って既定値に落とさない）
const ARGS = (typeof args === 'string') ? JSON.parse(args) : (args || {});
// engine 未指定のみ phaser 既定。空文字・不正値は下の throw に倒す（無言フォールバック禁止）
const engine = (ARGS.engine !== undefined && ARGS.engine !== null) ? ARGS.engine : 'phaser';
const ENGINE_PROFILES = {
  phaser: {
    stack: 'Vite + TypeScript(strict) + Phaser 3（最新安定版）',
    techStackDoc: '.claude/docs/tech-stack.md',
    manifestPath: 'game/assets/MANIFEST.jsonl',
    rawAssetDir: 'game/assets/',
    assets3d: false,
    verifyCmd: 'cd game && npm run typecheck && npm run build',
    scaffoldTask:
      '1. game/ を tech-stack.md 厳守でスキャフォールドする: Vite + TypeScript(strict) + Phaser 3（最新安定版）。\n' +
      '   必須 scripts(dev/build/typecheck/preview)、規定ディレクトリ構造（src/main.ts, src/config.ts, src/scenes/{Boot,Title,Menu,Game,Result}Scene（contract §11 必須シーン集合）, src/systems/, src/systems/meta/, src/persistence/, src/ui/, src/types.ts, assets/）。\n' +
      '   src/config.ts に ASSET_KEYS 定数の器を用意。game/assets/MANIFEST.jsonl を空ファイルで作成。\n' +
      '   `cd game && npm install && npm run typecheck && npm run build` が exit 0 になるまで自己修正せよ。',
    codeRulesLine: '規約厳守: マジックナンバーは src/config.ts へ / delta-time 必須 / Scene は薄く systems/ にロジック / 入力抽象化 / 資産参照は ASSET_KEYS 経由 / 永続化 I/O は src/persistence/ のみ・メタ進行は systems/meta/（セーブ破損の黙示初期化禁止 — rules/gameplay-code.md）。',
    placeholderNote: '画像・音声はまだ生成中のため、Phaser の Graphics/generateTexture 等によるプレースホルダで良い（ASSET_KEYS のキーだけ先に定義し、差し替え可能にしておく）。',
    codeRulesFile: '.claude/rules/gameplay-code.md',
    codeAddExample: '`git add game/src game/package.json state/stories.yaml`',
    configPath: 'game/src/config.ts',
    laneVerifyLine: '`cd game && npm run typecheck` を実行し、**自分の編集ファイル起因のエラーのみ** 0 にする（他レーンの書きかけ WIP・他レーンが提供予定の API 参照に起因するエラーは無視してよい — レーン合流後のバッチ検証が最終確認する。**並走レーン中は `npm run build` を実行しない** — dist/ が他レーンと衝突する — tech-stack.md「検証コマンド」節）',
    qaTarget: 'game/ を実際にビルド・起動し、headless ブラウザで実操作してプレイテストせよ（机上確認は不可。証跡必須）。',
    qaBuildLine: '`cd game && npm run build` 成功、起動時 console エラー 0。',
    playInstructions: 'cd game && npm install && npm run dev でローカル起動（操作方法は design/gdd.md を参照）',
    integrateSteps:
      '1. game/assets/ の全資産を src/config.ts の ASSET_KEYS に登録（パスのハードコード禁止。参照は必ず ASSET_KEYS 経由）。\n' +
      '2. BootScene で preload し、Build フェーズのプレースホルダ（Graphics/generateTexture）を実資産に差し替える。\n' +
      '3. 音声はユーザー操作後に再生開始（初回入力で AudioContext resume。autoplay 制限対応）。\n' +
      '4. UI 系資産（HUD・ボタン等）の差し替えが大きい場合はその部分を丁寧に（ui/ 配下の規約に従う）。\n' +
      '5. `cd game && npm run typecheck && npm run build` が exit 0 になるまで自己修正。'
  },
  unity: {
    stack: 'Unity 6 LTS + C#（URP・3D。state/engine-info.json のエディタを使用）',
    techStackDoc: '.claude/docs/tech-stack-unity.md',
    manifestPath: 'game/_generated/MANIFEST.jsonl',
    rawAssetDir: 'game/_generated/',
    assets3d: true,
    verifyCmd: 'tech-stack-unity.md「検証コマンド」の typecheck 相当（EditMode テスト。合格 = exit 0 かつ結果 XML で failed 0 — exit code 単独判定禁止）と build 相当（ForgeBuild.BuildMac）',
    scaffoldTask:
      '1. game/ を tech-stack-unity.md 厳守でスキャフォールドする（「プロジェクト生成（scaffold）」節のコマンドを使用。エディタは state/engine-info.json の binary）。\n' +
      '   必須パッケージ（URP / Input System / glTFast / Test Framework）を Packages/manifest.json に明記し、規定ディレクトリ構造（Assets/Scenes/{Boot,Title,Menu,Game,Result}（contract §11 必須シーン集合・EditorBuildSettings も5シーン）, Assets/Scripts/{GameConfig.cs,Types.cs,Systems/,Systems/Meta/,Persistence/,Components/,Input/,Ui/,Editor/ForgeBuild.cs}, Assets/Tests/{EditMode,PlayMode}, Assets/Resources/Generated/, _generated/）を作る。\n' +
      '   GameConfig.cs に定数と AssetKeys の器、Editor/ForgeBuild.cs に BuildMac メソッド、EditMode に最小テスト1本を用意。game/_generated/MANIFEST.jsonl を空ファイルで作成。\n' +
      '   tech-stack-unity.md「検証コマンド」の typecheck 相当（EditMode テスト）と build 相当（ForgeBuild.BuildMac）が exit 0 になるまで自己修正せよ。',
    codeRulesLine: '規約厳守: マジックナンバーは Assets/Scripts/GameConfig.cs へ / Time.deltaTime 必須 / Components は薄く Systems/（pure C#・MonoBehaviour禁止）にロジック / Input System 集約（コード生成方式）/ 資産参照は GameConfig.cs の AssetKeys 経由 / 永続化 I/O は Persistence/ のみ・メタ進行は Systems/Meta/（セーブ破損の黙示初期化禁止 — rules/unity-code.md）/ UI Canvas は RenderMode.ScreenSpaceCamera 固定（tech-stack-unity.md 規約14）。',
    placeholderNote: '3Dモデル・音声はまだ生成中のため、Unity プリミティブ（Cube/Capsule 等）＋単色マテリアルのプレースホルダで良い（AssetKeys のキーだけ先に定義し、差し替え可能にしておく）。',
    codeRulesFile: '.claude/rules/unity-code.md',
    codeAddExample: '`git add game/Assets game/Packages game/ProjectSettings state/stories.yaml`',
    configPath: 'game/Assets/Scripts/GameConfig.cs',
    laneVerifyLine: '**Unity をここでは起動しない**（単一インスタンスロック — 並走レーン・資産レーンと衝突する。EditMode/ビルド検証はレーン合流後のバッチ検証区間で一括実行される — tech-stack-unity.md「検証コマンド」節）。代わりに参照する型・メンバ・アセットキー・シリアライズ対象の実在を Read/Grep で静的確認し、コンパイルを通らない参照を残さない',
    qaTarget: 'game/ を tech-stack-unity.md「QA-PLAY の実行方法」に従い、batchmode ビルドと PlayMode テスト（入力擬似発行・LogAssert・ScreenCapture）で実プレイ検証せよ（机上確認は不可。テスト結果XMLとスクリーンショット証跡必須）。',
    qaBuildLine: 'tech-stack-unity.md の build 相当（ForgeBuild.BuildMac batchmode）が exit 0、PlayMode テストで LogAssert.NoUnexpectedReceived() 通過（エラー0）。',
    playInstructions: 'open game/Build/ForgeGame.app で起動（または Unity エディタで game/ を開いて Play。操作方法は design/gdd.md を参照）',
    integrateSteps:
      '1. game/_generated/ の合格資産（MDL/ANM/画像/音声）を game/Assets/Resources/Generated/{models,textures,audio}/ にコピーして Unity にインポートさせ（Resources.Load 方式 — contract §11。AssetKeys の値は Resources 相対パス）、GameConfig.cs の AssetKeys に登録（パスのハードコード禁止）。\n' +
      '2. リグ付き FBX は ModelImporter の animationType（Humanoid なら Human）をエディタスクリプトで設定し Avatar 生成を確認。プレースホルダ（プリミティブ）を実資産に差し替える。\n' +
      '3. 取込後にバウンディングボックスでスケール検証（tech-stack-unity.md「資産の取り扱い」）。\n' +
      '4. 音声は AudioSource で配線（OGG）。\n' +
      '5. tech-stack-unity.md「検証コマンド」の typecheck 相当（EditMode テスト）と build 相当（ForgeBuild.BuildMac）が exit 0 になるまで自己修正。'
  },
  unreal: {
    stack: 'Unreal Engine 5.x + C++（3D。state/engine-info.json のエンジンを使用。Blueprint ロジック禁止）',
    techStackDoc: '.claude/docs/tech-stack-unreal.md',
    manifestPath: 'game/_generated/MANIFEST.jsonl',
    rawAssetDir: 'game/_generated/',
    assets3d: true,
    verifyCmd: 'tech-stack-unreal.md「検証コマンド」の typecheck/build 相当（BuildCookRun -build。テスト実行時の合格 = exit 0 かつレポート JSON で failed 0）',
    scaffoldTask:
      '1. game/ を tech-stack-unreal.md 厳守でスキャフォールドする（「プロジェクト生成（scaffold）」節: テンプレートコピー方式。プロジェクト名 ForgeGame 固定）。\n' +
      '   規定ディレクトリ構造（Source/ForgeGame/{GameConfig.h,Types.h,Systems/,Systems/Meta/,Persistence/,Actors/,Input/,Ui/}, Source/ForgeGameTests/, Content/{Generated/,Maps/}, Config/, _generated/）を作る。Maps/ は Boot/Title/Menu/Game/Result の5状態（contract §11。レベル分割 or 状態遷移）。\n' +
      '   GameConfig.h に定数の器、ForgeGameTests に最小 Automation Test 1本を用意。game/_generated/MANIFEST.jsonl を空ファイルで作成。\n' +
      '   tech-stack-unreal.md「検証コマンド」の typecheck/build 相当（BuildCookRun -build）が exit 0 になるまで自己修正せよ。',
    codeRulesLine: '規約厳守: マジックナンバーは Source/ForgeGame/GameConfig.h へ / DeltaSeconds 必須 / Actors は薄く Systems/（pure C++・UObject禁止）にロジック / Enhanced Input 集約 / 資産パスは GameConfig.h の定数経由。Blueprint にロジックを置かない / 永続化（USaveGame）は Persistence/ のみ・メタ進行は Systems/Meta/（セーブ破損の黙示初期化禁止 — rules/unreal-code.md）。',
    placeholderNote: '3Dモデル・音声はまだ生成中のため、UE BasicShapes（Cube/Capsule 等）＋単色マテリアルのプレースホルダで良い（GameConfig.h の資産定数だけ先に定義し、差し替え可能にしておく）。',
    codeRulesFile: '.claude/rules/unreal-code.md',
    codeAddExample: '`git add game/Source game/Config game/ForgeGame.uproject state/stories.yaml`',
    configPath: 'game/Source/ForgeGame/GameConfig.h',
    laneVerifyLine: '**UE/UBT をここでは起動しない**（単一インスタンスロック — 並走レーン・資産レーンと衝突する。BuildCookRun 検証はレーン合流後のバッチ検証区間で一括実行される — tech-stack-unreal.md「検証コマンド」節）。代わりに参照する型・メンバ・ヘッダ include の実在を Read/Grep で静的確認し、コンパイルを通らない参照を残さない',
    qaTarget: 'game/ を tech-stack-unreal.md「QA-PLAY の実行方法」に従い、BuildCookRun と Automation RunTests（レポートJSON・スクリーンショット）で実プレイ検証せよ（机上確認は不可。証跡必須）。',
    qaBuildLine: 'tech-stack-unreal.md の package 相当（BuildCookRun）が exit 0、Automation レポート JSON で failed 0。',
    playInstructions: 'open game/Build/Mac/ForgeGame.app で起動（操作方法は design/gdd.md を参照）',
    integrateSteps:
      '1. game/_generated/ の合格資産（MDL/ANM/画像/音声）を Interchange（Python: unreal.InterchangeManager）で game/Content/Generated/ にインポートし、GameConfig.h の資産定数（FSoftObjectPath）に登録（パス文字列の実装直書き禁止）。\n' +
      '2. リグ付き FBX はスケルトン取込を確認し、必要なら IK Rig / IK Retargeter（Python API）でリターゲット。プレースホルダ（BasicShapes）を実資産に差し替える。\n' +
      '3. 取込後にバウンディングボックスでスケール検証（UE は 1 unit = 1cm。tech-stack-unreal.md「資産の取り扱い」）。\n' +
      '4. 音声は WAV を SoundWave として配線。\n' +
      '5. tech-stack-unreal.md「検証コマンド」の typecheck/build 相当（BuildCookRun -build）が exit 0 になるまで自己修正。'
  }
};
const EP = ENGINE_PROFILES[engine];
if (!EP) throw new Error('args.engine が不正: ' + engine + '（contract §11: phaser|unity|unreal）');
// エンジン別の正本パスを反映（const オブジェクトのプロパティ差し替え。phaser は従来値のまま）
DOCS.techStack = EP.techStackDoc;
ART.manifest = EP.manifestPath;

// ---------------------------------------------------------------------------
// 実行時状態
// ---------------------------------------------------------------------------
const reviewMode = ARGS.reviewMode || 'lean';
const checkpointAFeedbackPath = ARGS.checkpointAFeedbackPath || null;
const unresolvedFindings = [];
const knownIssues = [];
// 全 verdict 履歴（contract.md §9 / review-loops.md: full モードではスキルが完了後の
// Checkpoint 提示でこの全件を人間に提示する。実行中の都度提示は行わない）
const verdictHistory = [];

function recordVerdict(gateId, artifactName, iteration, verdict, findingsSummary) {
  verdictHistory.push({
    gate: gateId,
    artifact: artifactName,
    iteration: iteration,
    verdict: verdict,
    findings: findingsSummary || [],
  });
}

// review-mode 変調（contract.md §9 / review-loops.md）。reviewer プロンプトに前置する。
function reviewModeNote(mode) {
  if (mode === 'full') {
    return '【review-mode: full】ループは自動。verdict は workflow が履歴として蓄積し、完了後に Checkpoint でまとめて人間に提示される（実行中の人間への提示は行わない）。';
  }
  return '【review-mode: ' + mode + '】ループは自動。人間への提示は不要（未解決指摘は Checkpoint でまとめて提示される）。';
}

// verdict の重い方を返す（レビューペアのマージ用）
function worseVerdict(a, b) {
  const rank = { APPROVE: 0, CONCERNS: 1, REJECT: 2 };
  return (rank[b] || 0) > (rank[a] || 0) ? b : a;
}

// ---------------------------------------------------------------------------
// transient エラー（safety classifier 一時失敗等）への1回だけの自動リトライ（retro-e3 指摘5）。
// label に -retry を付けて opts を変える = キャッシュキーが変わり、失敗結果の replay を避ける。
// リトライ後も null なら従来どおり呼び出し側がエスカレーションする
// ---------------------------------------------------------------------------
async function agentR(prompt, opts) {
  let r = await agent(prompt, opts);
  if (r === null) {
    log('agent null（transient の可能性）→ 1回リトライ: ' + ((opts && opts.label) || ''));
    // 盲目再実行の禁止: 初回呼び出しが「作業完了後に構造化応答だけ喪失」した可能性があるため、
    // 完了済み作業（コミット・資産生成・課金 API 呼び出し）の重複実行を防ぐ resume ガードを前置する
    const guarded = '【リトライ実行】直前の同一タスク呼び出しが構造化応答を失って中断した可能性がある。作業開始前に既存の成果（git log の直近コミット・生成済みファイル・MANIFEST 追記）を確認し、完了済みの操作（コミット・資産生成・課金 API 呼び出し）は繰り返すな。未完了分のみ実行し、全て完了済みなら再実行せず結果の構造化返却のみを行え。\n\n' + prompt;
    r = await agent(guarded, Object.assign({}, opts, { label: (((opts && opts.label) || 'agent') + '-retry') }));
  }
  return r;
}

// ---------------------------------------------------------------------------
// reviewLoop ヘルパー（review-loops.md の共通形。concept-design.js と同形・自己完結）
//   cfg = {
//     gateId, artifactName, maxIter, reviewMode,
//     produce: async () => any|null,                    // 初回制作。null = 失敗
//     review:  async (iteration) => {verdict, findings[]}|null,
//     revise:  async (findings, iteration) => any|null, // 指摘反映
//   }
// 戻り値: { ok, verdict, unresolved: string[] }
// MAX_ITER 到達かつ非 APPROVE → エスカレーション（パイプラインは止めず未解決指摘を持ち帰る）
// ---------------------------------------------------------------------------
async function reviewLoop(cfg) {
  const produced = await cfg.produce();
  if (produced === null || produced === undefined) {
    log('[' + cfg.gateId + '] produce 失敗: ' + cfg.artifactName);
    return { ok: false, verdict: null, unresolved: ['[' + cfg.gateId + '] ' + cfg.artifactName + ': produce フェーズが失敗（agent が結果を返さなかった）'] };
  }
  let unresolved = [];
  const loopFailures = []; // review/revise の実行失敗マーカー（findings の再代入で失わないよう別配列に蓄積 — red-team 指摘）
  let lastVerdict = 'CONCERNS';
  for (let i = 1; i <= cfg.maxIter; i++) {
    const result = await cfg.review(i);
    if (!result || !result.verdict) {
      log('[' + cfg.gateId + '] iteration ' + i + ': review 失敗（結果なし）');
      loopFailures.push('[' + cfg.gateId + '] ' + cfg.artifactName + ': iteration ' + i + ' の review が結果を返さなかった');
      continue;
    }
    log('[' + cfg.gateId + '] ' + cfg.artifactName + ' iteration ' + i + ': ' + result.verdict);
    recordVerdict(cfg.gateId, cfg.artifactName, i, result.verdict, result.findings || []);
    if (result.verdict === 'APPROVE') {
      // 途中 iteration の実行失敗は APPROVE でも人間に届ける（隠さない）
      return { ok: true, verdict: 'APPROVE', unresolved: loopFailures.slice() };
    }
    lastVerdict = result.verdict;
    unresolved = (result.findings || []).map(function (f) {
      return '[' + cfg.gateId + '][' + cfg.artifactName + '] ' + f;
    });
    const revised = await cfg.revise(result.findings || [], i);
    if (revised === null || revised === undefined) {
      log('[' + cfg.gateId + '] iteration ' + i + ': revise 失敗');
      loopFailures.push('[' + cfg.gateId + '] ' + cfg.artifactName + ': iteration ' + i + ' の revise が失敗（指摘未対応の可能性）');
    }
  }
  log('[' + cfg.gateId + '] ' + cfg.artifactName + ': MAX_ITER(' + cfg.maxIter + ') 到達・非APPROVE → エスカレーション');
  // REJECT 級（設計欠陥相当）で終わった場合は [BLOCKER] を前置し、CD-CHECKPOINT が冒頭で個別警告する
  if (lastVerdict === 'REJECT') {
    unresolved = unresolved.map(function (u) { return '[BLOCKER] ' + u; });
  }
  return { ok: false, verdict: lastVerdict, unresolved: loopFailures.concat(unresolved) };
}

// verdict + findings の共通レビュースキーマ
const VERDICT_SCHEMA = {
  type: 'object',
  required: ['verdict', 'findings'],
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
    findings: { type: 'array', items: { type: 'string' } },
  },
};

// 実装/修正 agent の返却スキーマ（コミット hash 必須 — CR-CODE のレビュー対象固定に使う）
const COMMIT_RESULT_SCHEMA = {
  type: 'object',
  required: ['commitHash'],
  properties: {
    commitHash: { type: 'string' },
    summary: { type: 'string' },
    changedFiles: { type: 'array', items: { type: 'string' } },
  },
};

// =========================================================================
// Phase: Setup — tech-director がスキャフォールド + 設計docs + stories.yaml
// =========================================================================
phase('Setup');

const setupPrompt = [
  'あなたは ArcadeRelay Phase 2 の Setup を担当する（engine: ' + engine + ' — contract §11）。以下を必ず読んでから作業せよ:',
  '- ' + ART.brief + ' / ' + ART.concept + '（ピラー P-xx）/ ' + ART.gdd + ' / ' + ART.assetsManifest + ' / ' + ART.artBibleJson,
  '- ' + DOCS.techStack + '（game/ の規約。厳守）',
  '- ' + DOCS.contract + '（§7 stories.yaml スキーマ / §2 agent名 / §11 エンジン）',
  checkpointAFeedbackPath
    ? '- ' + checkpointAFeedbackPath + '（Checkpoint A の人間フィードバック。設計・ストーリー分解に必ず反映し、反映内容を ' + STATE.active + ' に明記すること）'
    : '（Checkpoint A フィードバックファイルは無し）',
  '',
  'タスク（すべて完了させること）:',
  EP.scaffoldTask,
  '2. ' + ART.architecture + ' を書く（シーン/レベル構成・システム境界・データフロー。エンジン非依存コア層（Systems）の線引きを ' + DOCS.techStack + ' に従い明記）。',
  '3. ' + ART.conventions + ' を書く（このゲーム固有のコード規約。' + DOCS.techStack + ' の規約に上乗せする具体則）。',
  '4. ' + STATE.stories + ' を contract §7 スキーマ通りに書く: ' + ART.gdd + ' を分解し、',
  '   - phase: prototype = コアループが1周する縦串（開始→挑戦→報酬→再挑戦）+ 必須シーン遷移（Title→Menu→Game→Result→Menu — contract §11）に必要な最小ストーリー群。実装順に並べる。',
  '   - phase: build = 残り全部。',
  '   - 各 story: 安定ID S-01〜 / pillar は concept.md の P-xx を必ず参照 / assignee は gameplay-engineer か ui-engineer / status: todo / acceptance は実操作で検証可能な文。',
  '   - **必須（欠けたら分解不合格 — contract §11）**: (a) Title シーンの story（assignee: ui-engineer / phase: prototype）、(b) Menu シーンの story（assignee: ui-engineer / phase: prototype。acceptance に必須要素 = プレイ開始・アウトゲーム表示・設定・終了導線 の実在検証を含める）、(c) メタ進行の永続化 story（assignee: gameplay-engineer。acceptance に「保存→再起動相当→復元一致」と「破損時 .bak+[SaveCorruption] 明示エラー」を含める）、(d) 環境の最低限ビジュアル story（assignee: gameplay-engineer / phase: prototype。acceptance に unity/unreal は「可視の地面/背景・ライト・カメラ構図の確定」、phaser は「背景の可視化・画面レイアウトの確定」を含める — contract §11）。',
  '5. ' + STATE.active + ' を更新し（日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）、パス限定で add してコミットする: `git add game docs state design && git commit -m "phase2: scaffold + stories"`（`git add -A` 禁止 — .claude/ 等の作業対象外の変更を巻き込まない）。',
  '',
  '最後に phase:prototype のストーリー一覧を stories.yaml の記載順で構造化して返せ（titleStoryId / menuStoryId / metaPersistenceStoryId / environmentStoryId に該当 story の ID を明示すること）。',
].filter(Boolean).join('\n');

const SETUP_SCHEMA = {
  type: 'object',
  required: ['prototypeStories', 'titleStoryId', 'menuStoryId', 'metaPersistenceStoryId', 'environmentStoryId'],
  properties: {
    prototypeStories: {
      type: 'array',
      minItems: 1,
      items: {
        type: 'object',
        required: ['id', 'title', 'assignee', 'acceptance'],
        properties: {
          id: { type: 'string' },
          title: { type: 'string' },
          pillar: { type: 'string' },
          assignee: { type: 'string', enum: ['gameplay-engineer', 'ui-engineer'] },
          acceptance: { type: 'string' },
        },
      },
    },
    titleStoryId: { type: 'string', description: 'Title シーン story の S-xx（contract §11 必須）' },
    menuStoryId: { type: 'string', description: 'Menu シーン story の S-xx（contract §11 必須）' },
    metaPersistenceStoryId: { type: 'string', description: 'メタ進行永続化 story の S-xx（contract §11 必須）' },
    environmentStoryId: { type: 'string', description: '環境の最低限ビジュアル（unity/unreal: 可視の地面/背景+ライト+カメラ構図確定）story の S-xx（contract §11 必須）' },
    notes: { type: 'string' },
  },
};

// contract §11: Title/Menu/メタ進行 story の存在を機械検証する（tech-director の自己申告 ID が
// 実在し、Title/Menu は assignee: ui-engineer であること）。不合格なら1回だけ差し戻す。
// contract §11 の engine 別必須環境要素（validateSetup / Setup 差し戻しプロンプト / stories.yaml 独立突合で共用 —
// 検証と修正指示が同一セットを参照しないと、差し戻しに従った修正が validator に落ちて Setup が中断する）
const ENV_REQUIRED_ELEMENTS = engine === 'phaser'
  ? [['背景', /背景|background/i], ['画面レイアウト', /レイアウト|layout|画面構成/i]]
  : [['地面/背景', /地面|背景|ground|background|terrain|床|floor/i], ['ライト', /ライト|照明|light/i], ['カメラ', /カメラ|camera/i]];
const ENV_REQUIRED_TEXT = ENV_REQUIRED_ELEMENTS.map(function (r) { return r[0]; }).join('・');
function envAcceptanceMissing(acc) {
  return ENV_REQUIRED_ELEMENTS.filter(function (r) { return !r[1].test(acc || ''); }).map(function (r) { return r[0]; });
}

function validateSetup(s) {
  if (!s || !Array.isArray(s.prototypeStories) || s.prototypeStories.length === 0) return ['prototypeStories が空'];
  const byId = {};
  for (const st of s.prototypeStories) byId[st.id] = st;
  const problems = [];
  const title = byId[s.titleStoryId];
  const menu = byId[s.menuStoryId];
  const meta = byId[s.metaPersistenceStoryId];
  const env = byId[s.environmentStoryId];
  if (!title) problems.push('titleStoryId=' + s.titleStoryId + ' が prototypeStories に実在しない');
  else if (title.assignee !== 'ui-engineer') problems.push('Title story ' + title.id + ' の assignee が ui-engineer でない');
  if (!menu) problems.push('menuStoryId=' + s.menuStoryId + ' が prototypeStories に実在しない');
  else if (menu.assignee !== 'ui-engineer') problems.push('Menu story ' + menu.id + ' の assignee が ui-engineer でない');
  if (!meta) problems.push('metaPersistenceStoryId=' + s.metaPersistenceStoryId + ' が prototypeStories に実在しない');
  else if (meta.assignee !== 'gameplay-engineer') problems.push('メタ進行永続化 story ' + meta.id + ' の assignee が gameplay-engineer でない（Systems/Meta + Persistence 層の実装 — tech-director.md）');
  if (!env) problems.push('environmentStoryId=' + s.environmentStoryId + ' が prototypeStories に実在しない');
  else if (env.assignee !== 'gameplay-engineer') problems.push('環境ビジュアル story ' + env.id + ' の assignee が gameplay-engineer でない（可視の地面/背景・ライト・カメラ構図の実装 — contract §11）');
  else {
    // ID の自己申告だけでは無関係 story の流用を検出できない — acceptance（title ではなく）が
    // engine 別の必須環境要素を全てカバーしていることを機械検証する（contract §11:
    // phaser=背景の可視化+画面レイアウト確定 / unity・unreal=可視の地面+ライト+カメラ構図）
    const missing = envAcceptanceMissing(env.acceptance);
    if (missing.length > 0) {
      problems.push('環境ビジュアル story ' + env.id + ' の acceptance が必須環境要素を欠く: ' + missing.join('・') +
        '（contract §11 の engine=' + engine + ' 要件。無関係 story の申告か acceptance の記述不足 — acceptance に検証可能な形で明記せよ）');
    }
  }
  return problems;
}

let setup = await agentR(setupPrompt, {
  label: 'setup-scaffold-stories',
  phase: 'Setup',
  agentType: 'tech-director',
  effort: 'high',
  schema: SETUP_SCHEMA,
});

{
  const problems = setup ? validateSetup(setup) : ['Setup agent が結果を返さなかった'];
  if (problems.length > 0 && setup) {
    log('Setup 差し戻し（contract §11 必須ストーリー欠落）: ' + problems.join(' / '));
    setup = await agentR(
      [
        'ストーリー分解が contract §11 の必須要件を満たしていない。以下を修正して ' + STATE.stories + ' を更新し、修正後の phase:prototype ストーリー一覧を再返却せよ:',
        problems.map(function (p, i) { return (i + 1) + '. ' + p; }).join('\n'),
        '必須: Title story と Menu story（いずれも assignee: ui-engineer / phase: prototype）と メタ進行永続化 story と 環境の最低限ビジュアル story（assignee: gameplay-engineer / phase: prototype。acceptance に必須環境要素「' + ENV_REQUIRED_TEXT + '」（contract §11 の engine=' + engine + ' 要件）を検証可能な形で全て含める）。既存 S-xx の振り直しは禁止（続番で追加）。',
        '修正後 git add ' + STATE.stories + ' && git commit（メッセージ: "phase2: fix required stories"）。',
      ].join('\n'),
      { label: 'setup-fix-required-stories', phase: 'Setup', agentType: 'tech-director', effort: 'high', schema: SETUP_SCHEMA }
    );
    const problems2 = setup ? validateSetup(setup) : ['差し戻し後も Setup agent が結果を返さなかった'];
    if (problems2.length > 0) {
      unresolvedFindings.push('[Setup] contract §11 必須ストーリー（Title/Menu/メタ進行）の検証が差し戻し後も不合格: ' + problems2.join(' / '));
      setup = null; // 下の失敗経路に倒す（必須シーン欠落のまま実装に進まない）
    }
  }
}

// 独立突合: tech-director の自己申告（構造化返却）ではなく state/stories.yaml の実体を
// 読み取り専用 agent で確認する（QA 証跡の独立検証と同じ規律 — 自己申告を唯一の関門にしない）
if (setup) {
  const crosscheck = await agentR(
    [
      '読み取り専用の検証タスク。' + STATE.stories + ' を読み、以下の story ID それぞれについて実在・assignee・phase・acceptance（原文そのまま）を返せ。ファイルの変更は禁止。',
      '対象 ID(JSON): ' + JSON.stringify([setup.titleStoryId, setup.menuStoryId, setup.metaPersistenceStoryId, setup.environmentStoryId]),
    ].join('\n'),
    {
      label: 'setup-crosscheck-stories', phase: 'Setup', effort: 'low',
      schema: {
        type: 'object', required: ['found'],
        properties: {
          found: {
            type: 'array',
            items: {
              type: 'object', required: ['id', 'exists'],
              properties: { id: { type: 'string' }, exists: { type: 'boolean' }, assignee: { type: 'string' }, phase: { type: 'string' }, acceptance: { type: 'string' } },
            },
          },
        },
      },
    }
  );
  const ccProblems = [];
  if (!crosscheck) {
    ccProblems.push('stories.yaml 独立突合 agent が結果を返さなかった');
  } else {
    const ccById = {};
    for (const f of (crosscheck.found || [])) ccById[f.id] = f;
    const expect = [
      { id: setup.titleStoryId, assignee: 'ui-engineer', name: 'Title' },
      { id: setup.menuStoryId, assignee: 'ui-engineer', name: 'Menu' },
      { id: setup.metaPersistenceStoryId, assignee: 'gameplay-engineer', name: 'メタ進行永続化' },
      { id: setup.environmentStoryId, assignee: 'gameplay-engineer', name: '環境ビジュアル' },
    ];
    for (const e of expect) {
      const f = ccById[e.id];
      if (!f || !f.exists) { ccProblems.push(e.name + ' story ' + e.id + ' が ' + STATE.stories + ' 実体に存在しない（自己申告と不一致）'); continue; }
      // フィールド欠落は検証スキップではなく不合格（optional フィールドの省略で突合を素通りさせない）
      if (!f.assignee) ccProblems.push(e.name + ' story ' + e.id + ' の実体 assignee を突合 agent が返さなかった（検証不能）');
      else if (f.assignee !== e.assignee) ccProblems.push(e.name + ' story ' + e.id + ' の実体 assignee が ' + f.assignee + '（期待: ' + e.assignee + '）');
      if (!f.phase) ccProblems.push(e.name + ' story ' + e.id + ' の実体 phase を突合 agent が返さなかった（検証不能）');
      else if (f.phase !== 'prototype') ccProblems.push(e.name + ' story ' + e.id + ' の実体 phase が ' + f.phase + '（期待: prototype — phase: build に置かれた必須 story は Phase 2 の実装・QA 対象から漏れる）');
      if (e.id === setup.environmentStoryId) {
        // stories.yaml 実体の acceptance も engine 別必須環境要素で突合（自己申告のみの検証にしない）
        if (!f.acceptance) ccProblems.push(e.name + ' story ' + e.id + ' の実体 acceptance を突合 agent が返さなかった（検証不能）');
        else {
          const ccMissing = envAcceptanceMissing(f.acceptance);
          if (ccMissing.length > 0) ccProblems.push(e.name + ' story ' + e.id + ' の実体 acceptance が必須環境要素を欠く: ' + ccMissing.join('・') + '（contract §11 の engine=' + engine + ' 要件）');
        }
      }
    }
  }
  if (ccProblems.length > 0) {
    unresolvedFindings.push('[Setup] stories.yaml 独立突合が不合格: ' + ccProblems.join(' / '));
    setup = null; // 必須シーン欠落・不一致のまま実装に進まない
  }
}

if (!setup || !setup.prototypeStories || setup.prototypeStories.length === 0) {
  // 実際の失敗理由（validateSetup の不合格詳細を含む蓄積済み unresolvedFindings）をそのまま返す（固定文言で上書きしない）
  return {
    summary: 'Phase 2 中断: Setup（スキャフォールド + stories.yaml 生成）が不合格。' +
      (unresolvedFindings.length > 0 ? ' 理由: ' + unresolvedFindings.join(' / ') : ' Setup agent が結果を返さなかった。'),
    playInstructions: 'なし（game/ が成立していない可能性が高い。' + STATE.active + ' と ' + STATE.stories + ' を確認のこと）',
    evidencePaths: [],
    knownIssues: unresolvedFindings.length > 0 ? unresolvedFindings.slice() : ['Setup agent が結果を返さなかった'],
    unresolvedFindings: unresolvedFindings.concat(['Setup 失敗のため以降のフェーズは未実行']),
    verdictHistory: verdictHistory,
    verdict: 'REJECT',
  };
}
const stories = setup.prototypeStories;
log('Setup 完了: phase:prototype ストーリー ' + stories.length + ' 件 — ' + stories.map(function (s) { return s.id; }).join(', '));

// =========================================================================
// Phase: Build ∥ AssetGen — コード実装と資産生成を並走
// =========================================================================

// ---- Build 側: ストーリーを「順次」実装（並列実装禁止 = コンフリクト回避） ----
// コミット規律: 実装/修正のたびに触ったパス限定で add してコミットし hash を報告させ、
// CR-CODE には `git show <hash>` でレビュー対象を固定して渡す（並走する AssetGen の
// 生成物や未コミット変更が紛れ込むのを防ぐ）。
const GIT_ADD_RULE =
  'git add は自分が編集した**個別ファイルパス**のみ（ディレクトリ指定・`git add -A` は禁止 — 単一 index を共有する並走レーン/資産トラックの staged 変更や未コミット WIP を巻き込む）。' +
  'commit は必ずパス指定形 `git commit -m "<msg>" -- <自分の編集ファイル...>` を使う（他パスの staged 巻き込みを防ぐ。**同一ファイル内の他レーン WIP は除外できない**ため、共有ファイル — config/types/stories.yaml — への自分の追記は編集後すぐ**その1ファイルだけ**を単独コミットして確定させる）。' +
  'コミット hash は `git rev-parse HEAD` ではなく `git log --format="%H %s" -20` から**自分のコミットメッセージに一致する最上（最新）の行**の hash を取り、`git show --stat <hash>` に**自分の編集ファイルが含まれることを確認**する（rev-parse HEAD は並走レーンの直後コミットを拾い得る。一致行が窓に無ければ -50 で再取得。含まれない・commit 自体が失敗した場合は古い同名コミットの hash を返さず**失敗を正直に報告**する）。' +
  'commit が index.lock で失敗したら 1〜2 秒待って 1 回だけリトライせよ。';

// 並走レーン規律（retro-e2 案A: assignee レーン並列。コード編集と review agent のみ並列 —
// エンジン起動を伴う検証はレーン合流後のバッチ検証区間に集約（案B）。tech-stack 文書「検証」節が正本）
const LANE_RULE =
  '並走レーン規律: あなたの assignee の担当領域以外のコードを書き換えない（gameplay-engineer=ゲーム機構・システム・永続化層 / ui-engineer=UI・シーン表示層。境界は ' + ART.architecture + '）。' +
  '共有ファイル（' + EP.configPath + '・共有型定義）は**自 story に必要な定数/型の追記のみ**（既存行の変更・削除は禁止 — 並走レーンと衝突する。例外: story の acceptance/指示が明示するバランス調整はその対象定数の**値変更のみ**許可）。' +
  'やむを得ず他レーン担当領域の既存シーン/配線ファイルに触れる場合（例: Result 到達時の persist 配線）は**ピンポイント Edit のみ・ファイル全面 Write 禁止・Edit 直前に必ず再 Read**（並走レーンのコミット済み変更を巻き戻さない）。' +
  STATE.stories + ' は自 story のブロック内のみ（status 行・注記）をピンポイント Edit（ファイル全面書き直し禁止 — 並走レーンの更新を消す）。' +
  STATE.active + ' には触らない（並走レーンと衝突する — 現在地更新はレーン合流後の直列区間の責務）。' +
  '他レーンの story が提供予定の API に依存する場合は ' + ART.architecture + ' の設計に合わせた呼び出しで実装してよい（コンパイル整合はレーン合流後のバッチ検証が最終確認する）。';

// ---------- バッチ検証（レーン合流後の直列区間。retro-e2 案B） ----------

const BATCH_VERIFY_SCHEMA = {
  type: 'object',
  required: ['ok'],
  properties: {
    ok: { type: 'boolean', description: '検証コマンド一式が最終的に合格（exit 0。unity/unreal はテスト結果 failed 0 込み）に到達したか' },
    fixedNotes: { type: 'array', items: { type: 'string' }, description: '修正した問題の一覧（原因 story 帰属付き）。無ければ空配列' },
    unresolved: { type: 'array', items: { type: 'string' }, description: '解消できなかった問題。無ければ空配列' }
  }
};

async function batchVerify(phaseName, contextNote) {
  const bv = await agentR(
    'バッチ検証（直列区間 — 並走レーンは合流済み。エンジン検証をここで一括実行する。engine=' + engine + '）。\n' +
    contextNote + '\n' +
    '手順:\n' +
    '1) ' + EP.verifyCmd + ' を実行\n' +
    '2) 失敗があれば、エラーのファイルパスと `git log --oneline -- <該当パス>` で原因 story を特定する（切り分け困難ならレーン中の story コミット単位で二分探索）\n' +
    '3) 最小修正で合格に到達させる（他 story の設計を作り替えない。チューニング値の変更は ' + EP.configPath + ' のみ。**直列区間の例外として、バッチ検証の最小修正に限り担当領域外のファイル — ui 層含む — も編集してよい**。**機能の削除・呼び出しの除去・無効化による回避は最小修正ではない** — コンパイル整合を保ったまま意図を維持し、やむを得ず挙動を変えた場合は fixedNotes に明記せよ。修正原因がエンジン/テストランナー起因の一般則（環境の落とし穴）だった場合は、tech-stack 文書の「既知の落とし穴」節へ即時追記せよ（無ければ新設 — gates.md QA-PLAY）。）\n' +
    '4) 修正した場合は ' + STATE.reviewsDir + '/batch-verify.md に「phase / 原因 story / 修正内容 / ISO8601 日時」を追記し（日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）、コミット規律のパス指定形で git commit（メッセージ: "batch-verify fix (' + phaseName + ')"）。' + GIT_ADD_RULE + '\n' +
    '構造化返却: ok（最終合格で true。到達できなければ false を正直に）/ fixedNotes / unresolved。',
    { label: 'batch-verify-' + phaseName.toLowerCase(), phase: phaseName, agentType: 'gameplay-engineer', schema: BATCH_VERIFY_SCHEMA, effort: 'high' }
  );
  if (bv === null) {
    unresolvedFindings.push('[BLOCKER] ' + phaseName + ': バッチ検証 agent が結果を返さなかった（ビルド健全性未確認 — 後段 QA が検出する）');
    return false;
  }
  // 修正内容は人間可視チャネルへ載せる（CR-CODE を通らない直接コミットのため、log() だけでは
  // 全履歴提示から漏れる — adversarial H-3）
  for (const n of (bv.fixedNotes || [])) {
    knownIssues.push('[' + phaseName + '][batch-verify修正・CR-CODE非経由] ' + n);
  }
  for (const u of (bv.unresolved || [])) {
    unresolvedFindings.push('[BLOCKER] ' + phaseName + '[batch-verify] ' + u);
  }
  if (bv.ok !== true || (bv.unresolved || []).length > 0) {
    if (bv.ok !== true && (bv.unresolved || []).length === 0) {
      unresolvedFindings.push('[BLOCKER] ' + phaseName + ': バッチ検証が合格に未到達（詳細は ' + STATE.reviewsDir + '/batch-verify.md）');
    }
    log('batch-verify(' + phaseName + '): 不合格または未解決あり（エスカレーション）');
    return false;
  }
  log('batch-verify(' + phaseName + '): 合格');
  return true;
}

async function buildStoryLane(laneStories) {
  for (const story of laneStories) {
    const reviewLogPath = STATE.reviewsDir + '/' + story.id.toLowerCase() + '.md';
    const storyHeader =
      'story: ' + story.id + ' "' + story.title + '"（pillar: ' + (story.pillar || '未指定') + ' / acceptance: ' + story.acceptance + '）';
    let lastCommitHash = null;

    const loopResult = await reviewLoop({
      gateId: 'CR-CODE',
      artifactName: story.id,
      maxIter: 2, // review-loops.md: CR-CODE MAX_ITER 2
      reviewMode: reviewMode,

      produce: async function () {
        const r = await agentR(
          [
            'あなたは ArcadeRelay の実装 engineer。次のストーリーを実装せよ。',
            storyHeader,
            '',
            '必読: ' + ART.architecture + ' / ' + ART.conventions + ' / ' + ART.gdd + ' / ' + DOCS.techStack + ' / ' + STATE.stories,
            '',
            '手順:',
            '1. ' + STATE.stories + ' で ' + story.id + ' の status を in-progress に更新。',
            '2. 既存コードの上に積む形で実装（前ストーリーの成果を壊さない）。' + EP.codeRulesLine,
            '   ' + EP.placeholderNote,
            '   ' + LANE_RULE,
            '3. ' + EP.laneVerifyLine + '。',
            '4. ' + STATE.stories + ' で status を review に更新し、コミットする。' + GIT_ADD_RULE,
            '   コミットメッセージ: "' + story.id + ': ' + story.title + '"。コミット hash は上記コミット規律の方法（`git log --format="%H %s" -20` の自メッセージ一致・最新行）で取得せよ。',
            '',
            '構造化返却: commitHash（今回のコミット hash。必須）/ changedFiles（変更ファイル一覧）/ summary（実装要点）。',
          ].join('\n'),
          { label: 'implement-' + story.id, phase: 'Build', agentType: story.assignee, effort: 'high', schema: COMMIT_RESULT_SCHEMA }
        );
        if (r && r.commitHash) {
          lastCommitHash = r.commitHash;
          return r;
        }
        return null;
      },

      review: async function (iteration) {
        // CR-CODE は code-reviewer + silent-failure-hunter のペア（gates.md CR-CODE 節）
        const reviewCommon = [
          reviewModeNote(reviewMode),
          'GATE: CR-CODE（' + DOCS.gates + ' の CR-CODE 節を読んで従うこと）。',
          'レビュー対象はコミット ' + lastCommitHash + ' に固定する（`git show ' + lastCommitHash + '` で取得。作業ツリーの未コミット変更や他のコミットの diff は対象外）。',
          storyHeader,
          '',
          '判定の読み替え: findings 0件 = APPROVE / 修正可能な指摘 = CONCERNS / 設計欠陥 = REJECT。',
          '前提（並走レーン設計）: 他レーンの story が提供予定の API への参照は、' + ART.architecture + ' の設計に合致していれば「実体未実装」だけを理由に REJECT/blocker としない（コンパイル整合はレーン合流後のバッチ検証が保証する。設計との不一致・誤用は通常どおり指摘してよい）。**このレビューは読み取り専用 — エンジン起動・ビルド/テストコマンドの実行禁止**（並走レーン中の単一インスタンスロック/dist 競合）。',
          '応答の1行目は「CR-CODE: APPROVE|CONCERNS|REJECT」（contract.md §5）とし、構造化返却の verdict / findings にも同じ判定と指摘を入れよ。',
          'findings は指摘の配列（ファイル・行・修正方針を含む具体文）。',
        ];
        const pair = await parallel([
          function () {
            return agentR(
              reviewCommon.concat([
                '',
                '観点: 通常のコードレビューに加え、' + EP.codeRulesFile + '（存在しない場合は ' + DOCS.techStack + ' のコード規約）への違反 — 特にマジックナンバー混入と delta-time 非依存実装 — を確認せよ。',
                'acceptance がこの diff で満たせる実装になっているかも確認せよ。',
                'レビュー結果を ' + reviewLogPath + ' に追記せよ（review-loops.md の追記形式: iteration ' + iteration + '・verdict・指摘要約・日時。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。',
              ]).join('\n'),
              {
                label: 'cr-code-' + story.id + '-iter' + iteration,
                phase: 'Build',
                agentType: 'pr-review-toolkit:code-reviewer',
                schema: VERDICT_SCHEMA,
              }
            );
          },
          function () {
            return agentR(
              reviewCommon.concat([
                '',
                '観点: silent failure に絞って検査せよ（握りつぶされた例外・空 catch・失敗を隠すフォールバック・エラーの黙殺・失敗時に成功を装う戻り値）。',
                STATE.reviewsDir + '/ への追記は不要（追記は code-reviewer 側が行う。あなたは構造化返却のみでよい）。',
              ]).join('\n'),
              {
                label: 'cr-silent-' + story.id + '-iter' + iteration,
                phase: 'Build',
                agentType: 'pr-review-toolkit:silent-failure-hunter',
                schema: VERDICT_SCHEMA,
              }
            );
          },
        ]);
        const valid = (pair || []).filter(function (r) { return r && r.verdict; });
        if (valid.length === 0) {
          return null;
        }
        if (valid.length < 2) {
          knownIssues.push('[CR-CODE][' + story.id + '] iteration ' + iteration + ': レビューペアの片方が結果を返さなかった（片側判定で続行）');
        }
        let verdict = 'APPROVE';
        let findings = [];
        for (const r of valid) {
          verdict = worseVerdict(verdict, r.verdict);
          findings = findings.concat(r.findings || []);
        }
        return { verdict: verdict, findings: findings };
      },

      revise: async function (findings, iteration) {
        const r = await agentR(
          [
            'あなたは ArcadeRelay の実装 engineer。CR-CODE レビュー指摘（code-reviewer + silent-failure-hunter の合算）を修正せよ。',
            storyHeader,
            '',
            '指摘一覧:',
            findings.map(function (f, idx) { return (idx + 1) + '. ' + f; }).join('\n'),
            '',
            '手順:',
            '1. 各指摘に対応（対応しない場合は正当理由を明記。黙殺禁止）。規約は ' + ART.conventions + ' / ' + DOCS.techStack + '。' + LANE_RULE,
            '2. 修正後の検証: ' + EP.laneVerifyLine + '。',
            '3. ' + reviewLogPath + ' の iteration ' + iteration + ' の「対応:」欄に対応済み/見送り＋理由を追記。',
            '4. コミットする。' + GIT_ADD_RULE,
            '   コミットメッセージ: "' + story.id + ': fix CR-CODE iter ' + iteration + '"。コミット hash は上記コミット規律の方法（`git log --format="%H %s" -20` の自メッセージ一致・最新行）で取得せよ。',
            '構造化返却: commitHash（今回のコミット hash。必須）/ summary（対応要約）。',
          ].join('\n'),
          { label: 'fix-' + story.id + '-iter' + iteration, phase: 'Build', agentType: story.assignee, effort: 'high', schema: COMMIT_RESULT_SCHEMA }
        );
        if (r && r.commitHash) {
          lastCommitHash = r.commitHash;
          return r;
        }
        return null;
      },
    });

    // ok:true でも loop 中の review/revise 実行失敗マーカーは届ける（APPROVE で隠さない — adversarial W-1）
    if (loopResult.unresolved && loopResult.unresolved.length > 0) {
      unresolvedFindings.push(...loopResult.unresolved);
    }

    // ステータス確定（done。未解決指摘があれば注記）— state はファイルが真実
    const bookkeep = await agentR(
      [
        STATE.stories + ' で ' + story.id + ' の status を done に更新せよ。',
        loopResult.ok
          ? '（CR-CODE APPROVE 済み）'
          : '（CR-CODE 未APPROVE のままエスカレーション。story の acceptance 行の下に「# note: CR-CODE unresolved — ' + STATE.reviewsDir + '/' + story.id.toLowerCase() + '.md 参照」とコメント注記を追加すること）',
        STATE.active + ' には触らない（並走レーンと衝突する — 現在地の更新はレーン合流後の Integrate が行う）。' +
        STATE.stories + ' は該当 story の行のみをピンポイント Edit（ファイル全面書き直し禁止）。',
        'コミットする: `git add ' + STATE.stories + ' && git commit -m "' + story.id + ': status done" -- ' + STATE.stories + '`（素の git commit 禁止 — パス指定形で並走レーンの staged 変更を巻き込まない）。' + GIT_ADD_RULE,
      ].join('\n'),
      { label: 'bookkeep-' + story.id, phase: 'Build', agentType: story.assignee, effort: 'low' }
    );
    if (bookkeep === null) {
      knownIssues.push(story.id + ' の stories.yaml status 更新が未確認（agent 失敗）');
    }
  }
  return true;
}

// assignee レーン分割（retro-e2 案A）: gameplay と ui を並走、レーン内は Setup の返却順（依存順）を維持。
// エンジン検証はレーン中に行わず（EP.laneVerifyLine）、合流後の batchVerify が一括保証する（案B）
async function buildStories() {
  const gameplayLane = stories.filter(function (s) { return s.assignee !== 'ui-engineer'; });
  const uiLane = stories.filter(function (s) { return s.assignee === 'ui-engineer'; });
  log('Build レーン分割: gameplay ' + gameplayLane.length + '件 / ui ' + uiLane.length + '件');
  await parallel([
    function () { return buildStoryLane(gameplayLane); },
    function () { return buildStoryLane(uiLane); },
  ]);
  return true;
}

// ---- AssetGen 側: 画像（art-director）と SFX（audio-designer）----
// AR-ASSET ループ: 資産ごと MAX 3 + fallback プロバイダ切替後さらに 1 回（review-loops.md）
const GEN_SCHEMA = {
  type: 'object',
  required: ['generated', 'budgetExceeded', 'remainingPlanned', 'degradedRoutes'], // degradedRoutes 省略で fallback 記録が消えるのを防ぐ（無ければ空配列を明示）
  properties: {
    generated: { type: 'array', items: { type: 'string' }, description: '生成して MANIFEST に追記した資産パス一覧' },
    budgetExceeded: { type: 'boolean', description: '予算超過見込みで生成を停止した場合 true' },
    remainingPlanned: { type: 'number', description: '対象範囲のうちまだ生成できていない資産の件数（0 = 対象を全て生成済み）' },
    notes: { type: 'string', description: '開示事項（shippable:false ルート使用・Meshy 403→fal 切替・quota 制約等）。無ければ空文字' },
    degradedRoutes: { type: 'array', items: { type: 'string' }, description: '縮退・fallback 試行の全記録（ルート名+HTTPコード必須。例: "model_character: meshy:direct→422 / fal:meshy-v6→429 / tripo:direct→403 → local縮退"）。無ければ空配列' },
  },
};

// 生成 agent の構造化返却から開示事項を機械回収する（自由文で捨てない — contract §10）
function collectGenDisclosures(batchName, gen) {
  if (!gen) return;
  for (const d of (gen.degradedRoutes || [])) {
    unresolvedFindings.push('[AssetGen][' + batchName + '][縮退] ' + d);
  }
  if (gen.notes && String(gen.notes).trim().length > 0) {
    unresolvedFindings.push('[AssetGen][' + batchName + '][開示] ' + gen.notes);
  }
}

async function assetBatchLoop(cfg) {
  // cfg = { batchName, producerType, generatePrompt, regeneratePrompt(failed), fallbackPrompt(failed), reviewSubject }
  const reviewLogPath = STATE.reviewsDir + '/' + cfg.batchName + '.md';
  const reviewSchema = {
    type: 'object',
    required: ['verdict', 'failedAssets', 'disclosures'],
    properties: {
      verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
      failedAssets: {
        type: 'array',
        items: {
          type: 'object',
          required: ['file', 'reason'],
          properties: {
            file: { type: 'string' },
            reason: { type: 'string' },
            retryInstruction: { type: 'string' },
          },
        },
      },
      disclosures: {
        type: 'array',
        items: { type: 'string' },
        description: '再生成不要だが人間開示が必要な事項（shippable:false ルート由来・fal 経由 Meshy のライセンス継承未検証・cost_estimated:true・must_replace 等 — gates.md AR-ASSET 観点6）。無ければ空配列',
      },
    },
  };

  async function reviewBatch(iteration, extraNote) {
    const review = await agentR(
      [
        reviewModeNote(reviewMode),
        'GATE: AR-ASSET（' + DOCS.gates + ' の AR-ASSET 節に従う）。対象: ' + cfg.reviewSubject,
        '基準: ' + ART.artBibleJson + '（スタイルロック）と ' + ART.assetsManifest + '（生成仕様）。' + ART.manifest + ' で対象一覧を確認せよ。',
        extraNote || '',
        '不合格資産には理由と再生成指示（プロンプト修正案）を必ず付けよ。',
        '**再生成では直らない開示事項**（gates.md AR-ASSET 観点6: shippable:false ルート由来 / fal 経由 Meshy のライセンス継承未検証 / cost_estimated:true / provenance 記録漏れ以外の注記）は failedAssets ではなく disclosures に入れよ（failedAssets に入れると無意味な再生成ループが走る）。',
        'レビュー結果を ' + reviewLogPath + ' に追記（review-loops.md の追記形式・iteration ' + iteration + '。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。',
        '応答の1行目は「AR-ASSET: APPROVE|CONCERNS|REJECT」（contract.md §5）とし、構造化返却にも同じ判定を入れよ。',
        '構造化返却: verdict（品質は全資産合格なら APPROVE — 開示事項があっても再生成不要なら APPROVE + disclosures）/ failedAssets（file / reason / retryInstruction）/ disclosures。',
      ].filter(Boolean).join('\n'),
      { label: 'ar-asset-' + cfg.batchName + '-iter' + iteration, phase: 'AssetGen', agentType: 'art-reviewer', schema: reviewSchema }
    );
    if (review) {
      recordVerdict('AR-ASSET', cfg.batchName, iteration, review.verdict,
        (review.failedAssets || []).map(function (f) { return f.file + ': ' + f.reason; })
          .concat((review.disclosures || []).map(function (d) { return '[開示] ' + d; })));
      for (const d of (review.disclosures || [])) {
        unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '][開示] ' + d);
      }
    }
    return review;
  }

  const generated = await agentR(cfg.generatePrompt + '\n構造化返却: generated（MANIFEST 追記済みパス一覧）/ budgetExceeded / remainingPlanned（対象範囲の未生成残数）/ notes（開示事項）/ degradedRoutes（Primary からの縮退一覧）。', {
    label: 'generate-' + cfg.batchName,
    phase: 'AssetGen',
    agentType: cfg.producerType,
    effort: 'high',
    schema: GEN_SCHEMA,
  });
  if (generated === null) {
    unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '] 生成 agent が失敗。資産バッチ未生成');
    return false;
  }
  collectGenDisclosures(cfg.batchName, generated);
  if (generated.budgetExceeded) {
    unresolvedFindings.push('[AssetGen][' + cfg.batchName + '] 予算超過見込みで生成停止（未生成 ' + (typeof generated.remainingPlanned === 'number' ? generated.remainingPlanned : '不明') + ' 件。state/budget.txt 参照）');
    log('[AssetGen] ' + cfg.batchName + ': 予算超過見込みで停止');
    return false;
  }
  if ((generated.generated || []).length === 0 && (typeof generated.remainingPlanned === 'number' && generated.remainingPlanned > 0)) {
    unresolvedFindings.push('[AssetGen][' + cfg.batchName + '] 生成 0 件だが未生成対象が ' + generated.remainingPlanned + ' 件残存（API 全滅の疑い。notes: ' + (generated.notes || 'なし') + '）');
    return false;
  }

  let failed = null; // null = 未レビュー
  for (let i = 1; i <= 3; i++) {
    const review = await reviewBatch(i, null);
    if (!review) {
      unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '] iteration ' + i + ' の review が失敗');
      continue;
    }
    log('[AR-ASSET] ' + cfg.batchName + ' iteration ' + i + ': ' + review.verdict + '（不合格 ' + (review.failedAssets || []).length + ' 件）');
    if (review.verdict === 'APPROVE') {
      return true;
    }
    if ((review.failedAssets || []).length === 0) {
      // 非APPROVE + failedAssets 空 = バッチ全体指摘 or reviewer プロトコル不整合。合格扱いにしない（red-team 指摘）
      unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '] iteration ' + i + ' が ' + review.verdict + ' だが failedAssets が空（バッチ全体指摘の可能性 — 人間確認が必要）');
      return false;
    }
    failed = review.failedAssets;
    if (i < 3) {
      const regen = await agentR(cfg.regeneratePrompt(failed) + '\n構造化返却: generated / budgetExceeded / remainingPlanned / notes / degradedRoutes。', {
        label: 'regen-' + cfg.batchName + '-iter' + i,
        phase: 'AssetGen',
        agentType: cfg.producerType,
        effort: 'high',
        schema: GEN_SCHEMA,
      });
      if (regen === null) {
        unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '] iteration ' + i + ' の再生成が失敗');
      } else {
        collectGenDisclosures(cfg.batchName, regen);
        if (regen.budgetExceeded) {
          unresolvedFindings.push('[AssetGen][' + cfg.batchName + '] 再生成中に予算超過見込みで停止（不合格 ' + failed.length + ' 件が残存）');
          return false;
        }
      }
    }
  }

  // 3回不合格 → fallback プロバイダへ切替後さらに1回（review-loops.md）
  if (failed && failed.length > 0) {
    log('[AR-ASSET] ' + cfg.batchName + ': 3回不合格 → fallback プロバイダ切替（' + failed.length + ' 件）');
    const fb = await agentR(cfg.fallbackPrompt(failed) + '\n構造化返却: generated / budgetExceeded / remainingPlanned / notes / degradedRoutes。', {
      label: 'fallback-' + cfg.batchName,
      phase: 'AssetGen',
      agentType: cfg.producerType,
      effort: 'high',
      schema: GEN_SCHEMA,
    });
    if (fb !== null) collectGenDisclosures(cfg.batchName + ':fallback', fb);
    // 兄弟分岐（初回生成・regen）と同じ予算ガード（fallback 中の予算停止を品質不合格と誤報告しない）
    if (fb && fb.budgetExceeded) {
      unresolvedFindings.push('[AssetGen][' + cfg.batchName + '] fallback 中に予算超過見込みで停止（不合格 ' + failed.length + ' 件が残存）');
      return false;
    }
    if (fb !== null) {
      const finalReview = await reviewBatch(4, '（fallback プロバイダ切替後の最終判定。これが最後の反復）');
      if (finalReview && finalReview.verdict === 'APPROVE') {
        return true;
      }
      if (finalReview) {
        if ((finalReview.failedAssets || []).length === 0) {
          unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '] fallback 後の最終判定が ' + finalReview.verdict + ' だが failedAssets が空（バッチ全体指摘の可能性 — 人間確認が必要）');
        }
        for (const f of (finalReview.failedAssets || [])) {
          unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '] ' + f.file + ': ' + f.reason + '（fallback 後も不合格）');
        }
      } else {
        unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '] fallback 後の最終 review が失敗');
      }
    } else {
      unresolvedFindings.push('[AR-ASSET][' + cfg.batchName + '] fallback 生成が失敗（不合格 ' + failed.length + ' 件が残存）');
    }
  }
  return false;
}

const assetCommonRules = [
  '必読: ' + ART.assetsManifest + ' / ' + ART.artBibleJson + ' / ' + DOCS.assetsConfig + ' / ' + STATE.assetRouting + '（ルーティング表が真実。生成中の再判定禁止。shippable:false ルートで生成した資産は必ず未解決事項として報告）。',
  '**Primary が API 失敗（4xx/5xx/timeout）の場合、fallback を 1 段も試さずにローカル縮退/プレースホルダ/must-replace 化することを禁止**（品質不合格による再生成は従来どおり Primary 固定 — この規則は API 失敗時のルート切替の話）。' + STATE.assetRouting + ' の fallbacks を上から順に全段試行し、各試行の『ルート名 + HTTP ステータス（または失敗理由）』を degradedRoutes に必ず列挙する（例: "model_character: meshy:direct→422 / fal:meshy-v6→429 / tripo:direct→403 → local縮退"）。全段失敗の場合のみローカル縮退可（retro-e3 指摘7）。',
  'API キー: **API を呼ぶ Bash に限り**冒頭で `set -a; source .env 2>/dev/null; set +a` を実行してから curl する（検証・後処理 — ffmpeg/npx 等 — の Bash では source しない: サードパーティ子プロセスへのキー継承を避ける。キー値の echo・ログ出力禁止 — contract §10）。API エラー（401/403/429/5xx）は握り潰さず HTTP ステータスと共に報告。',
  '対象はコアループ縦串に必須の資産のみ（' + STATE.stories + ' の phase:prototype の acceptance と ' + ART.assetsManifest + ' から特定）。残りは Phase 3 に回す。',
  '予算: 生成のたびに ' + STATE.budget + '（既定 $20）と ' + ART.manifest + ' の cost_usd 合算を照合し、超過見込みなら生成を停止して残件を報告せよ。',
  '全生成を ' + ART.manifest + ' に 1行1資産で追記（provider/model/prompt/seed/cost_usd/plan_tier/sha256/license/generated_at。3D資産は kind/polycount/bone_count/rigged/format/units/bbox_authoring_m/validator も必須。クレジット換算見積は cost_estimated:true）。',
  '保存先は ' + EP.rawAssetDir + ' 配下。完了後はパス限定で add し、**commit も必ずパス指定形**: `git add ' + EP.rawAssetDir.replace(/\/$/, '') + ' design docs state/reviews && git commit -m "<msg>" -- ' + EP.rawAssetDir.replace(/\/$/, '') + ' design docs state/reviews`（素の git commit・state ディレクトリ丸ごと指定は禁止 — 並走コードレーンの stories.yaml / active.md の WIP を巻き取らない）。',
  '`git add -A` は禁止（並走する実装トラックのコード変更を巻き込まない）。commit が index.lock で失敗したら 1〜2 秒待って 1 回だけリトライせよ。',
].join('\n');

async function generateImages() {
  return assetBatchLoop({
    batchName: 'assets-images-prototype',
    producerType: 'art-director',
    reviewSubject: 'Phase 2 で生成した画像資産バッチ（' + EP.rawAssetDir + ' 配下、' + ART.manifest + ' の今回追記分）',
    generatePrompt: [
      'あなたは ArcadeRelay の art-director。コアループ必須の画像資産を生成せよ。',
      assetCommonRules,
      'スタイル一貫性: ' + ART.artBibleJson + ' の style_block を全プロンプトに機械的に前置し、seed を記録。hero 系は character_reference を共用。',
      '生成後パイプライン（' + DOCS.assetsConfig + ' 記載）を全数実施: 即時DL → アルファ検証（白背景PNG出荷禁止）→ 必要なら背景除去 → トリム。',
      '最後に生成した資産の一覧（file / ASSET_KEYS 用キー案）と、予算都合で見送った資産を報告せよ。',
    ].join('\n'),
    regeneratePrompt: function (failed) {
      return [
        'あなたは ArcadeRelay の art-director。AR-ASSET 不合格の画像を再生成せよ（ルーティングは ' + STATE.assetRouting + ' の Primary のまま）。',
        '不合格一覧（reason と retryInstruction を反映してプロンプトを修正すること）:',
        failed.map(function (f) { return '- ' + f.file + ': ' + f.reason + (f.retryInstruction ? '（再生成指示: ' + f.retryInstruction + '）' : ''); }).join('\n'),
        assetCommonRules,
        '再生成分も ' + ART.manifest + ' に追記し、旧ファイルを置換せよ。',
      ].join('\n');
    },
    fallbackPrompt: function (failed) {
      return [
        'あなたは ArcadeRelay の art-director。3回不合格の画像について、' + DOCS.assetsConfig + ' のルーティング表の Fallback プロバイダへ切替えて 1 回だけ再生成せよ。',
        '対象:',
        failed.map(function (f) { return '- ' + f.file + ': ' + f.reason; }).join('\n'),
        assetCommonRules,
      ].join('\n');
    },
  });
}

async function generateAudio() {
  return assetBatchLoop({
    batchName: 'assets-audio-prototype',
    producerType: 'audio-designer',
    reviewSubject: 'Phase 2 で生成した SFX バッチ（' + EP.rawAssetDir + ' 配下、' + ART.manifest + ' の今回追記分）。音声は仕様一致（長さ・フォーマット・ラウドネス・' + ART.assetsManifest + ' との整合）で採点せよ',
    generatePrompt: [
      'あなたは ArcadeRelay の audio-designer。コアループ必須の SFX を生成せよ（BGM は Phase 3。今回は SFX のみ）。',
      assetCommonRules,
      'ルート: ElevenLabs SFX v2 の REST 直（公式MCP禁止・duration_seconds 明示・ループ素材は loop:true）。Free プランでの出荷用生成は禁止。',
      '生成後パイプライン: ffmpeg loudnorm（-16 LUFS）+ 無音トリム → OGG Vorbis 128-160kbps + M4A/AAC の両出力。',
      'SFX は seed 無しのため共通語彙で 4 変種生成 → ベスト選別し、選別理由も ' + ART.manifest + ' に追記。',
      '最後に生成 SFX 一覧（file / ASSET_KEYS 用キー案）を報告せよ。',
    ].join('\n'),
    regeneratePrompt: function (failed) {
      return [
        'あなたは ArcadeRelay の audio-designer。AR-ASSET 不合格の SFX を再生成せよ（同一ルート・プロンプト修正）。',
        '不合格一覧:',
        failed.map(function (f) { return '- ' + f.file + ': ' + f.reason + (f.retryInstruction ? '（再生成指示: ' + f.retryInstruction + '）' : ''); }).join('\n'),
        assetCommonRules,
      ].join('\n');
    },
    fallbackPrompt: function (failed) {
      return [
        'あなたは ArcadeRelay の audio-designer。3回不合格の SFX について、' + DOCS.assetsConfig + ' のローカル縮退ルート（jsfxr。パブリックドメイン・決定的・出荷可）へ切替えて 1 回だけ生成せよ。',
        '対象:',
        failed.map(function (f) { return '- ' + f.file + ': ' + f.reason; }).join('\n'),
        assetCommonRules,
      ].join('\n');
    },
  });
}

// 3D エンジン時のみ: コアループ必須の 3D モデル/アニメ（MDL/ANM）バッチ
async function generateModels() {
  return assetBatchLoop({
    batchName: 'assets-models-prototype',
    producerType: 'art-director',
    reviewSubject: 'Phase 2 で生成した 3D モデル/アニメ資産バッチ（' + EP.rawAssetDir + ' 配下、' + ART.manifest + ' の今回追記分）。' + DOCS.gates + ' の AR-ASSET「3D資産」観点（gltf-validator / ポリ数・ボーン数 / スケール / リグ / スタイル一致）で機械検査せよ',
    generatePrompt: [
      'あなたは ArcadeRelay の art-director。コアループ必須の 3D モデル資産（MDL/ANM）を生成せよ。',
      assetCommonRules,
      'ルーティング: ' + STATE.assetRouting + ' の model_character / model_prop / model_environment / anim ルートに従う（Primary: Meshy 直API（キー有効時）→ 第二候補: fal 経由 fal-ai/meshy/*。Meshy 直の rigging/animation が 403 の場合は当該資産種別のみ fal 経由へ切替えて必ず報告。キー無し縮退: Blender プロシージャル+Rigify またはエンジン内プリミティブ — その場合は全て must_replace: true）。',
      'スタイル一貫性: ' + ART.artBibleJson + ' のコンセプト画（reference_images / character_reference）を image-to-3D の入力に使い、art-bible の 3D スタイル方針（ポリ予算・リグ方針）に従う。',
      '生成後パイプライン（' + DOCS.assetsConfig + ' の 3D 節）のうち **Unity/UE を起動しない段まで**を全段実施: スキーマ検証（GLB: gltf-transform validate / FBX: Blender headless で GLB 変換して同 validate）→ ポリ数/ボーン数/非多様体検査 → authoring-time 寸法計測を MANIFEST の bbox_authoring_m に記録 → スタイル確認用に Blender headless レンダリングのプレビュー画像を ' + EP.rawAssetDir + 'previews/ に出力。',
      '**エンジン取込は行わない**（Integrate フェーズが直列区間で実施 — ' + (engine === 'unity' ? 'Unity は同一プロジェクト単一インスタンスロックのため、並走レーンから Unity を起動してはならない' : 'UE エディタ起動は Integrate に集約する') + '。tech-stack 文書参照）。',
      '最後に生成した資産の一覧（file / kind / rigged / 登録キー案）と、予算都合で見送った資産を報告せよ。',
    ].join('\n'),
    regeneratePrompt: function (failed) {
      return [
        'あなたは ArcadeRelay の art-director。AR-ASSET 不合格の 3D 資産を再生成せよ（ルーティングは ' + STATE.assetRouting + ' の Primary のまま）。',
        '不合格一覧（reason と retryInstruction を反映してコンセプト画/プロンプトを修正すること）:',
        failed.map(function (f) { return '- ' + f.file + ': ' + f.reason + (f.retryInstruction ? '（再生成指示: ' + f.retryInstruction + '）' : ''); }).join('\n'),
        assetCommonRules,
        '再生成分も ' + ART.manifest + ' に追記し、raw の旧ファイルを置換せよ（エンジン取込先の更新は Integrate フェーズに委ねる — 並走レーンからエンジンを起動しない）。',
      ].join('\n');
    },
    fallbackPrompt: function (failed) {
      return [
        'あなたは ArcadeRelay の art-director。3回不合格の 3D 資産について、' + STATE.assetRouting + ' の fallbacks（直API またはローカル縮退）へ切替えて 1 回だけ再生成せよ（' + DOCS.assetsConfig + ' の 3D ルーティング表参照。縮退生成は must_replace: true）。',
        '対象:',
        failed.map(function (f) { return '- ' + f.file + ': ' + f.reason; }).join('\n'),
        assetCommonRules,
      ].join('\n');
    },
  });
}

// Build と AssetGen をバリア並走（コードは順次、資産は画像/音声（+3Dモデル）を並走）
// 並走区間は開始前に代表フェーズを1回だけ宣言する（thunk 内での phase() 呼び出しは
// マーカー遷移が非決定的になるため禁止。細分は agent opts の phase ラベルに任せる）。
phase('Build');
const assetThunks = [
  function () { return generateImages(); },
  function () { return generateAudio(); },
];
if (EP.assets3d) assetThunks.push(function () { return generateModels(); });
const parallelResults = await parallel([
  function () { return buildStories(); },
  function () { return parallel(assetThunks); },
]);
log('Build/AssetGen 並走完了: ' + JSON.stringify(parallelResults));

// バッチ検証（レーン合流後の直列区間 — retro-e2 案B。エンジン起動はここから直列。
// この後の Integrate/QA が「初回コンパイル」にならないよう、全 story のコミット済みコードを一括保証する）
let buildVerifyOk = true;
if (stories.length > 0) {
  buildVerifyOk = await batchVerify('Build',
    'ここまで Build レーン（gameplay/ui 並走）が phase:prototype の全コード story を実装しており、レーン中はエンジン検証を実行していない' +
    '（' + DOCS.techStack + '「検証」節の検証バッチ化）。全 story のコミット済みコードを一括検証・修正せよ。');
}
// 後段プロンプトへの警告注入（integrate の縮退注入と同じパターン — レビュー指摘 F3）
const BUILD_VERIFY_WARN = buildVerifyOk ? '' : '【警告: Build バッチ検証が不合格のまま — ' + STATE.reviewsDir + '/batch-verify.md 参照。ビルドが壊れている前提で作業せよ】\n';

// =========================================================================
// Phase: Integrate — 生成資産を ASSET_KEYS 経由で組み込み
// =========================================================================
phase('Integrate');

const INTEGRATE_SCHEMA = {
  type: 'object',
  required: ['ok', 'degradations'],
  properties: {
    ok: { type: 'boolean', description: 'エンジン取込後検証（gates.md AR-ASSET ※節）がすべて合格したか' },
    degradations: {
      type: 'array',
      items: { type: 'string' },
      description: '縮退・警告の一覧（例: Humanoid→Generic 縮退、スケール補正適用、取込警告、欠落資産のプレースホルダ残存）。無ければ空配列'
    },
    summary: { type: 'string' },
  },
};

const integrate = await agentR(
  [
    BUILD_VERIFY_WARN +
    'あなたは ArcadeRelay の gameplay-engineer。生成済み資産を game/ に組み込め（engine: ' + engine + '。直列区間 — エンジン起動はこの工程が専有）。',
    '必読: ' + ART.manifest + '（生成資産一覧）/ ' + ART.architecture + ' / ' + ART.conventions + ' / ' + DOCS.techStack + '。',
    '',
    '手順:',
    EP.integrateSteps,
    '6. **エンジン取込後検証**（gates.md AR-ASSET の※節）: FBX 取込成功・取込後バウンディングボックス・リグ資産のアニメ再生可否（unity: Avatar.isValid / unreal: リターゲット成功）を機械検証し、結果を ' + ART.manifest + ' の validator に記録。失敗・縮退は degradations として返す（MANIFEST 注記だけで済ませない）。',
    '7. 未生成・不合格で欠けている資産はプレースホルダのまま残し、欠落一覧を degradations に含める。',
    '8. ' + STATE.active + ' を更新し（日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）、パス限定で add してコミットする: `git add game docs state design && git commit -m "phase2: integrate assets"`（`git add -A` 禁止 — .claude/ 等の作業対象外の変更を巻き込まない）。',
    '構造化返却: ok / degradations / summary（組み込んだ資産キー一覧と欠落一覧を含む）。',
  ].join('\n'),
  { label: 'integrate-assets', phase: 'Integrate', agentType: 'gameplay-engineer', effort: 'high', schema: INTEGRATE_SCHEMA }
);
if (integrate === null) {
  unresolvedFindings.push('[Integrate] 資産組み込み agent が失敗。プレースホルダのままの可能性');
  knownIssues.push('資産組み込みが未完了の可能性（Integrate agent 失敗）');
} else {
  for (const d of (integrate.degradations || [])) {
    unresolvedFindings.push('[Integrate] ' + d);
  }
  if (integrate.ok === false) {
    unresolvedFindings.push('[Integrate] エンジン取込後検証が不合格（詳細は degradations と ' + ART.manifest + ' の validator）');
  }
}

// =========================================================================
// Phase: QA — QA-PLAY（実起動・実操作・証跡）。重大バグは修正して再QA（MAX 2）
// =========================================================================
phase('QA');

const qaSchema = {
  type: 'object',
  required: ['verdict', 'criticalBugs', 'failedAcceptance', 'summary', 'evidencePaths', 'screenshotsVisuallyConfirmed'],
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
    summary: { type: 'string' },
    criticalBugs: {
      type: 'array',
      items: {
        type: 'object',
        required: ['title', 'detail', 'assignee'],
        properties: {
          title: { type: 'string' },
          detail: { type: 'string' },
          storyId: { type: 'string' },
          assignee: { type: 'string', enum: ['gameplay-engineer', 'ui-engineer'] },
        },
      },
    },
    failedAcceptance: { type: 'array', items: { type: 'string' } },
    evidencePaths: { type: 'array', items: { type: 'string' } },
    screenshotsVisuallyConfirmed: {
      type: 'boolean',
      description: '撮影した全スクリーンショットを Read で目視し、対象（モデル・UI文字）が写っていることを確認したか（gates.md QA-PLAY 視覚証跡の目視義務。false/未実施の APPROVE は無効）',
    },
  },
};

// 証跡実在の独立検証スキーマ（qa-lead の自己申告を workflow 側で機械確認する）
const EVIDENCE_CHECK_SCHEMA = {
  type: 'object',
  required: ['checks'],
  properties: {
    checks: {
      type: 'array',
      items: {
        type: 'object',
        required: ['path', 'exists', 'nonEmpty'],
        properties: {
          path: { type: 'string' },
          exists: { type: 'boolean' },
          nonEmpty: { type: 'boolean' },
          bytes: { type: 'number' },
        },
      },
    },
    extraFilesInEvidenceDir: { type: 'array', items: { type: 'string' } },
  },
};

let qaResult = null;
const QA_MAX = 2; // review-loops.md: QA-PLAY MAX_ITER 2
for (let round = 1; round <= QA_MAX; round++) {
  qaResult = await agentR(
    [
      BUILD_VERIFY_WARN + reviewModeNote(reviewMode),
      'GATE: QA-PLAY（' + DOCS.gates + ' の QA-PLAY 節の engine=' + engine + ' の実行手段に従う）。iteration ' + round + '/' + QA_MAX + '。',
      '対象: ' + EP.qaTarget,
      (integrate && (integrate.degradations || []).length > 0
        ? '【Integrate からの縮退報告あり — 該当箇所は重点検証（特にリグ縮退時はアニメ再生の目視確認必須）】: ' + integrate.degradations.join(' / ')
        : ''),
      '',
      '検証項目:',
      '1. ' + EP.qaBuildLine,
      '2. ' + ART.gdd + ' 記載の操作でコアループが1周できる（開始→挑戦→結果→リスタート）。加えて必須シーン遷移 Title→Menu→Game→Result→Menu の1周（contract §11。Menu の必須要素 = プレイ開始・アウトゲーム表示・設定・終了導線 の実在込み — gates.md QA-PLAY 観点2）。Title/Menu/Game/Result 各画面のスクリーンショットを撮る（Game は開始直後の空盤面不可 — コアループの主要オブジェクトが写るフレームで撮る。gates.md 視覚証跡）。',
      '3. ' + STATE.stories + ' の phase:prototype 全ストーリーの acceptance を1つずつ実操作で検証。',
      '4. 実プレイ感が ' + ART.concept + ' のピラー P-xx を裏切っていないか。',
      '5. メタ進行の永続化（gates.md QA-PLAY 観点5）: 保存→再起動相当→復元一致、破損セーブ→.bak 退避＋[SaveCorruption] 明示エラー1回＋既定値復旧、を自動テストで検証。',
      '6. 視覚証跡の機械検知＋目視（gates.md QA-PLAY 視覚証跡の目視義務）: 全スクリーンショットに magick の mean 検査（<0.02 / >0.98 = SUSPECT_BLANK → 撮影方式を切替えて再撮影）を行い、必ず Read で目視して「何が写っているか」を ' + ART.qaReport + ' の目視所見表に記録。',
      '',
      '証跡（スクリーンショット/録画）を ' + ART.qaEvidence + ' に保存し、結果を ' + ART.qaReport + ' に書け。',
      'レビュー履歴を ' + STATE.reviewsDir + '/qa.md に追記（review-loops.md の追記形式・iteration ' + round + '。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。',
      '判定: 重大バグ 0 かつ acceptance 全通過 = APPROVE。',
      '応答の1行目は「QA-PLAY: APPROVE|CONCERNS|REJECT」（contract.md §5）とし、構造化返却にも同じ判定を入れよ。',
      '構造化返却: verdict / summary / criticalBugs（title・detail・storyId・修正担当 assignee。重大バグのみ。軽微な指摘は qa/report.md に記載）/ failedAcceptance（未通過の acceptance 一覧。story ID と何が満たせなかったかを1行ずつ。全通過なら空配列）/ evidencePaths（保存した証跡の相対パス）/ screenshotsVisuallyConfirmed（全スクリーンショットを Read で目視済みか。未実施なら false を正直に返す）。',
    ].filter(Boolean).join('\n'),
    { label: 'qa-play-round' + round, phase: 'QA', agentType: 'qa-lead', effort: 'high', schema: qaSchema }
  );

  if (!qaResult) {
    unresolvedFindings.push('[QA-PLAY] round ' + round + ' の QA agent が失敗');
    continue;
  }

  // 証跡実在＋目視宣言の独立機械検証（qa-lead の自己申告を workflow が別 agent で確認 — E1 教訓: 自己申告が唯一の関門にならないこと）
  {
    const evCheck = await agentR(
      [
        '読み取り専用の検証タスク。以下の証跡パス一覧について、各ファイルの実在と非0サイズを Bash（`test -s`・`stat`）で機械検証せよ。ファイルの作成・変更・削除は禁止。',
        '証跡パス(JSON): ' + JSON.stringify(qaResult.evidencePaths || []),
        '加えて ' + ART.qaEvidence + ' 直下の実ファイル一覧を ls で確認し extraFilesInEvidenceDir に返せ。',
      ].join('\n'),
      { label: 'verify-evidence-round' + round, phase: 'QA', effort: 'low', schema: EVIDENCE_CHECK_SCHEMA }
    );
    const missing = [];
    if (!evCheck) {
      missing.push('証跡検証 agent が結果を返さなかった');
    } else {
      // 網羅性突合: evidencePaths の各パスが checks に exists && nonEmpty で現れることを要求
      // （検証 agent が checks:[] や部分回答を返した場合に合格擬装させない）
      const byPath = {};
      for (const c of (evCheck.checks || [])) byPath[c.path] = c;
      for (const p of (qaResult.evidencePaths || [])) {
        const c = byPath[p];
        if (!c) missing.push(p + '（検証結果に現れず — 未検証）');
        else if (!c.exists || !c.nonEmpty) missing.push(p + '（' + (!c.exists ? '不存在' : '0バイト') + '）');
      }
    }
    if ((qaResult.evidencePaths || []).length === 0) missing.push('evidencePaths が空（証跡なしの判定は無効 — qa-lead.md）');
    if (qaResult.screenshotsVisuallyConfirmed !== true) missing.push('スクリーンショットの Read 目視が未実施（screenshotsVisuallyConfirmed=false）');
    if (missing.length > 0) {
      if (qaResult.verdict === 'APPROVE') {
        qaResult.verdict = 'CONCERNS';
        log('[QA-PLAY] round ' + round + ': 証跡/目視の機械検証不合格 → APPROVE を CONCERNS に降格');
      }
      unresolvedFindings.push('[QA-PLAY] round ' + round + ' 証跡/目視の機械検証不合格: ' + missing.join(' / '));
    }
  }

  log('[QA-PLAY] round ' + round + ': ' + qaResult.verdict + '（重大バグ ' + qaResult.criticalBugs.length + ' 件 / acceptance 未通過 ' + (qaResult.failedAcceptance || []).length + ' 件）');
  recordVerdict('QA-PLAY', 'qa', round, qaResult.verdict,
    qaResult.criticalBugs.map(function (b) { return b.title; }).concat(qaResult.failedAcceptance || []));
  if (qaResult.verdict === 'APPROVE') {
    break; // 合格は verdict === APPROVE のみ（criticalBugs 0 件でのショートカット禁止）
  }

  if (round < QA_MAX) {
    // 重大バグをassigneeが修正（同一コードベースのため順次。コンフリクト回避）
    for (const bug of qaResult.criticalBugs) {
      const fixed = await agentR(
        [
          'あなたは ArcadeRelay の実装 engineer。QA-PLAY で検出された重大バグを修正せよ。',
          'バグ: ' + bug.title,
          '詳細: ' + bug.detail,
          bug.storyId ? '関連 story: ' + bug.storyId : '',
          '参照: ' + ART.qaReport + '（QA 所見全文）/ ' + ART.conventions + ' / ' + DOCS.techStack + '。',
          '修正後 ' + EP.verifyCmd + ' が exit 0 を確認し、パス限定で add してコミット: `git add game state ' + DOCS.techStack + ' && git commit -m "phase2: fix QA — ' + bug.title + '"`（`git add -A`・`.claude/docs` ディレクトリ丸ごと指定は禁止。' + DOCS.techStack + ' は下記の落とし穴昇格を同一コミットに含めるため — 追記した場合のみ stage される）。',
          '修正原因がエンジン/テストランナー起因の一般則（環境の落とし穴）だった場合は、tech-stack 文書の「既知の落とし穴」節へ即時追記せよ（無ければ新設 — gates.md QA-PLAY）。',
          '修正内容を簡潔に返せ。',
        ].filter(Boolean).join('\n'),
        // round を label に含める: 同一バグが round を跨いで残存した場合に (prompt, opts) キャッシュが
        // 前 round の修正結果を replay して再修正を黙ってスキップしない（resume 安全 — adversarial M-8a）
        { label: 'fix-qa-r' + round + '-' + bug.assignee, phase: 'QA', agentType: bug.assignee, effort: 'high' }
      );
      if (fixed === null) {
        unresolvedFindings.push('[QA-PLAY] 重大バグ「' + bug.title + '」の修正 agent が失敗');
      }
    }
    // acceptance 未通過も修正対象（非APPROVEの原因を残したまま再QAしない）
    if ((qaResult.failedAcceptance || []).length > 0) {
      const faFixed = await agentR(
        [
          'あなたは ArcadeRelay の実装 engineer。QA-PLAY で未通過となった acceptance を満たすよう修正せよ。',
          '未通過一覧:',
          qaResult.failedAcceptance.map(function (fa, idx) { return (idx + 1) + '. ' + fa; }).join('\n'),
          '参照: ' + ART.qaReport + '（QA 所見全文）/ ' + STATE.stories + '（acceptance 原文）/ ' + ART.conventions + ' / ' + DOCS.techStack + '。',
          '修正後 ' + EP.verifyCmd + ' が exit 0 を確認し、パス限定で add してコミット: `git add game state ' + DOCS.techStack + ' && git commit -m "phase2: fix QA — failed acceptance"`（`git add -A`・`.claude/docs` ディレクトリ丸ごと指定は禁止。' + DOCS.techStack + ' は落とし穴昇格を同一コミットに含めるため）。',
          '修正原因がエンジン/テストランナー起因の一般則（環境の落とし穴）だった場合は、tech-stack 文書の「既知の落とし穴」節へ即時追記せよ（無ければ新設 — gates.md QA-PLAY）。',
          '修正内容を簡潔に返せ。',
        ].join('\n'),
        { label: 'fix-qa-acceptance-r' + round, phase: 'QA', agentType: 'gameplay-engineer', effort: 'high' }
      );
      if (faFixed === null) {
        unresolvedFindings.push('[QA-PLAY] acceptance 未通過の修正 agent が失敗');
      }
    }
  } else {
    for (const bug of qaResult.criticalBugs) {
      unresolvedFindings.push('[QA-PLAY] 未解決の重大バグ: ' + bug.title + ' — ' + bug.detail);
    }
  }
}
if (qaResult && qaResult.verdict !== 'APPROVE') {
  knownIssues.push('QA-PLAY が MAX ' + QA_MAX + ' 周で APPROVE に到達せず（詳細: ' + ART.qaReport + '）');
  for (const fa of qaResult.failedAcceptance || []) {
    unresolvedFindings.push('[QA-PLAY] acceptance 未通過: ' + fa);
  }
  if (qaResult.summary) {
    unresolvedFindings.push('[QA-PLAY] 非APPROVE要約: ' + qaResult.summary);
  }
}

// =========================================================================
// Phase: Final — CD-CHECKPOINT → Checkpoint B 素材を返す
// =========================================================================
phase('Final');

const cdSchema = {
  type: 'object',
  required: ['verdict', 'summary', 'playInstructions', 'evidencePaths', 'knownIssues'],
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
    summary: { type: 'string' },
    playInstructions: { type: 'string' },
    evidencePaths: { type: 'array', items: { type: 'string' } },
    knownIssues: { type: 'array', items: { type: 'string' } },
    rejectInstructions: { type: 'array', items: { type: 'string' } },
  },
};

function cdPrompt(attemptNote) {
  return [
    'GATE: CD-CHECKPOINT（' + DOCS.gates + ' の CD-CHECKPOINT 節に従う）。Checkpoint B（遊べる縦串）を人間に提示する直前の最終判定。',
    attemptNote || '',
    '確認対象: ' + ART.brief + ' / ' + ART.concept + '（ピラー）/ ' + ART.gdd + ' / ' + STATE.stories + ' / ' + ART.qaReport + ' / ' + ART.manifest + ' / ' + STATE.reviewsDir + '/ 配下のレビュー履歴。',
    '',
    'ループで持ち越された未解決指摘（正直に提示物へ含めること。隠蔽禁止。**[BLOCKER] 始まりの項目と縮退（Humanoid→Generic / プレースホルダ / Fallback / shippable:false / [開示]）は summary の冒頭で個別に警告し、箇条書きに埋没させない** — gates.md CD-CHECKPOINT 観点3）:',
    unresolvedFindings.length > 0 ? unresolvedFindings.map(function (f) { return '- ' + f; }).join('\n') : '- なし',
    '',
    '観点: 1) ビジョン一貫性（brief・P-xx から逸脱していないか） 2) 提示品質（人間が5分で判断できる要約か） 3) 正直さ（未達・妥協点が列挙されているか）。',
    '併せて ' + STATE.active + ' を「Phase 2 完了・Checkpoint B 待ち」に更新せよ（日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。',
    '',
    '応答の1行目は「CD-CHECKPOINT: APPROVE|CONCERNS|REJECT」（contract.md §5）とし、構造化返却の verdict にも同じ判定を入れよ。',
    '構造化返却:',
    '- verdict: APPROVE/CONCERNS/REJECT',
    '- summary: 何を作ったか・何を判断してほしいか・既知の課題（人間向け5分要約）',
    '- playInstructions: 人間が遊ぶ手順（「' + EP.playInstructions + '」起点の具体手順と操作方法）',
    '- evidencePaths: ' + ART.qaEvidence + ' 配下の提示すべき証跡パス',
    '- knownIssues: 未達・妥協点の列挙',
    '- rejectInstructions: REJECT の場合のみ、人間に見せる前に直すべき点の指示リスト',
  ].filter(Boolean).join('\n');
}

let cd = await agentR(cdPrompt(null), {
  label: 'cd-checkpoint-b',
  phase: 'Final',
  agentType: 'creative-director',
  effort: 'high',
  schema: cdSchema,
});
if (cd) {
  recordVerdict('CD-CHECKPOINT', 'checkpoint-b', 1, cd.verdict, cd.knownIssues || []);
}

// REJECT なら指示に従い修正後 1 回だけ再判定（review-loops.md: CD-CHECKPOINT MAX_ITER 1）
if (cd && cd.verdict === 'REJECT' && cd.rejectInstructions && cd.rejectInstructions.length > 0) {
  log('[CD-CHECKPOINT] REJECT → 指示に従い修正して1回だけ再判定');
  const cdFix = await agentR(
    [
      'あなたは ArcadeRelay の tech-director。CD-CHECKPOINT が REJECT した。以下の指示に従い、Checkpoint B 提示物を修正せよ（必要なら各担当の成果物を直接修正・再ビルド・再コミット）。',
      '指示一覧:',
      cd.rejectInstructions.map(function (r, idx) { return (idx + 1) + '. ' + r; }).join('\n'),
      '検証: ' + EP.verifyCmd + ' が exit 0。対応内容を ' + STATE.reviewsDir + '/checkpoint-b.md の「対応:」欄に追記し、パス限定で add してコミット: `git add game docs state design qa && git commit`（`git add -A` 禁止）。',
    ].join('\n'),
    { label: 'cd-reject-fix', phase: 'Final', agentType: 'tech-director', effort: 'high' }
  );
  if (cdFix !== null) {
    const cdRetry = await agentR(cdPrompt('（REJECT 指示への修正後の再判定。これが最後の判定）'), {
      label: 'cd-checkpoint-b-rejudge',
      phase: 'Final',
      agentType: 'creative-director',
      effort: 'high',
      schema: cdSchema,
    });
    if (cdRetry) {
      recordVerdict('CD-CHECKPOINT', 'checkpoint-b', 2, cdRetry.verdict, cdRetry.knownIssues || []);
      cd = cdRetry;
    }
  } else {
    unresolvedFindings.push('[CD-CHECKPOINT] REJECT 指示への修正 agent が失敗');
  }
}

if (!cd) {
  return {
    summary: 'Phase 2 完走したが CD-CHECKPOINT 判定 agent が失敗。成果物は ' + STATE.stories + ' / ' + ART.qaReport + ' / game/ を直接確認のこと。',
    playInstructions: EP.playInstructions,
    evidencePaths: (qaResult && qaResult.evidencePaths) || [],
    knownIssues: knownIssues.concat(['CD-CHECKPOINT 判定が取得できなかった']),
    unresolvedFindings: unresolvedFindings,
    verdictHistory: verdictHistory,
    verdict: 'CONCERNS',
  };
}

return {
  summary: cd.summary,
  playInstructions: cd.playInstructions,
  evidencePaths: (cd.evidencePaths && cd.evidencePaths.length > 0)
    ? cd.evidencePaths
    : ((qaResult && qaResult.evidencePaths) || []),
  knownIssues: knownIssues.concat(cd.knownIssues || []),
  unresolvedFindings: unresolvedFindings,
  verdictHistory: verdictHistory,
  verdict: cd.verdict,
};
