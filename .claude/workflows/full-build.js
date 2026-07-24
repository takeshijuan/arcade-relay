// .claude/workflows/full-build.js — Phase 3（/forge-build から起動、contract.md §4）
// args: { reviewMode, engine?, checkpointBFeedbackPath }（engine は contract §11 の3値。省略時 phaser）
// 流れ: Replan → (Build ∥ AssetGen) → Polish → FullQA → Final(CD-CHECKPOINT) → Checkpoint C 素材を return
// 規律の正本: .claude/docs/contract.md / review-loops.md / gates.md / tech-stack.md / assets-config.md
// 注: review-mode=full は「完了後の全verdict履歴提示」（contract §9 / review-loops.md）。
//     本workflowは全ゲート verdict を verdictHistory に蓄積して戻り値に含め、
//     起動元スキル(/forge-build)が完了後に人間へ全件提示する（workflow内からAskUserQuestionは不可）。

export const meta = {
  name: 'full-build',
  description: 'ArcadeRelay Phase 3: checkpoint-b-feedback反映の再計画、本実装と全資産生成の並走、polish、フルQA、CD-CHECKPOINTを経てCheckpoint C素材を返す',
  phases: [
    { title: 'Replan', detail: 'tech-directorがcheckpoint-b-feedbackを反映してstories.yamlを更新（資産系feedbackはdesign/assets.mdへ反映）し、game-designerが必要ならgdd該当節を改訂する（ピラー変更不可）' },
    { title: 'Build', detail: 'phase:buildのコードstoryをassigneeレーン（gameplay/ui）で並走実装（レーン内は順次・story毎にパス限定addでcommit・hash報告・レーン中はエンジン検証なし）し、各storyにCR-CODEレビューループ（MAX 2、対象はgit show <hash>で固定）を適用。レーン合流後にバッチ検証（エンジン検証一括+失敗はstory単位切り分け）を直列実行する' },
    { title: 'AssetGen', detail: '残り全資産（画像/SFX/BGM。engine=unity/unreal では 3D モデル MDL/ANM も）をルーティング表と予算チェックの下で生成し、AR-ASSETループ（MAX 3+fallback 1）とバッチ一貫性チェック・エンジン取込（直列区間）を行う' },
    { title: 'Polish', detail: 'game-designerがconfig.tsベースのバランス確認とピラーに沿うjuice/手触りpolishストーリーを起案し、engineerがassigneeレーン並走で実装（合流後にバッチ検証）する' },
    { title: 'FullQA', detail: 'qa-leadが全stories.yaml acceptanceの回帰とQA-PLAY（review 2回上限: QA→修正→QA）、資産監査（MANIFESTコスト合算・予算比較・ライセンスフラグ抽出→state/reviews/assets-audit.md）を行う' },
    { title: 'Final', detail: 'creative-directorのCD-CHECKPOINT判定（REJECT時は修正後1回のみ再判定）を経てCheckpoint C素材を返す' }
  ]
};

const DOCS = '.claude/docs';
const ENGINEERS = ['gameplay-engineer', 'ui-engineer'];

// ---------- コミット規律（D-05: story毎commit + パス限定add + index.lockリトライ） ----------
// CODE_COMMIT_RULE / ASSET_COMMIT_RULE はエンジン別パスを含むため、
// エンジンプロファイル確定後（args セクション末尾）で定義する。

const GIT_RETRY_NOTE = 'git commit が index.lock で失敗したら1〜2秒待って1回だけリトライせよ。';

// ---------- agentR: agent() null の1回自動リトライ ----------
// transient エラー（safety classifier 一時失敗等）への1回だけの自動リトライ（retro-e3 指摘5）。
// label に -retry を付けて opts を変える = キャッシュキーが変わり、失敗結果の replay を避ける。
// リトライ後も null なら従来どおり呼び出し側がエスカレーションする
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

// ---------- schemas ----------

const STORY_LIST_SCHEMA = {
  type: 'object',
  required: ['stories'],
  properties: {
    stories: {
      type: 'array',
      items: {
        type: 'object',
        required: ['id', 'title', 'assignee', 'acceptance'],
        properties: {
          id: { type: 'string', description: 'S-xx 形式の安定ID' },
          title: { type: 'string' },
          assignee: { type: 'string', enum: ['gameplay-engineer', 'ui-engineer', 'art-director', 'audio-designer'] },
          pillar: { type: 'string', description: 'P-xx 形式' },
          acceptance: { type: 'string' }
        }
      }
    },
    notes: { type: 'string' }
  }
};

const IMPL_SCHEMA = {
  type: 'object',
  required: ['commitHash'],
  properties: {
    commitHash: { type: 'string', description: '今回の変更をコミットした git hash（git log --format="%H %s" -20 の自メッセージ一致・最新行から取得 — rev-parse HEAD は並走レーンのコミットを拾い得る）' },
    notes: { type: 'string' }
  }
};

const CODE_REVIEW_SCHEMA = {
  type: 'object',
  required: ['findings'],
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        required: ['summary', 'severity'],
        properties: {
          summary: { type: 'string' },
          file: { type: 'string' },
          severity: { type: 'string', enum: ['blocker', 'major', 'minor'] }
        }
      }
    }
  }
};

const ASSET_GEN_SCHEMA = {
  type: 'object',
  required: ['generated', 'budgetExceeded', 'remainingPlanned', 'degradedRoutes'], // degradedRoutes 省略で fallback 記録が消えるのを防ぐ（無ければ空配列を明示）
  properties: {
    generated: { type: 'array', items: { type: 'string' }, description: '生成してMANIFESTに追記した資産パス一覧' },
    budgetExceeded: { type: 'boolean', description: '予算超過見込みで生成を停止した場合 true' },
    remainingPlanned: { type: 'number', description: '対象範囲（design/assets.md のうち MANIFEST 未記載）でまだ生成できていない資産の件数（0 = 全て生成済み）' },
    notes: { type: 'string', description: '開示事項（shippable:false ルート使用・Meshy 403→fal 切替・quota 制約等）。無ければ空文字' },
    degradedRoutes: { type: 'array', items: { type: 'string' }, description: '縮退・fallback 試行の全記録（ルート名+HTTPコード必須。例: "model_character: meshy:direct→422 / fal:meshy-v6→429 / tripo:direct→403 → local縮退"）。無ければ空配列' }
  }
};

const ASSET_REVIEW_SCHEMA = {
  type: 'object',
  required: ['verdict', 'failedAssets', 'disclosures'],
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
    failedAssets: {
      type: 'array',
      items: {
        type: 'object',
        required: ['file', 'reason', 'retryInstruction'],
        properties: {
          file: { type: 'string' },
          reason: { type: 'string' },
          retryInstruction: { type: 'string', description: 'プロンプト修正案を含む再生成指示' }
        }
      }
    },
    disclosures: {
      type: 'array',
      items: { type: 'string' },
      description: '再生成では直らないが人間開示が必要な事項（shippable:false ルート由来 / fal 経由 Meshy のライセンス継承未検証 / cost_estimated:true / must_replace 等 — gates.md AR-ASSET 観点6）。無ければ空配列'
    }
  }
};

const QA_SCHEMA = {
  type: 'object',
  required: ['verdict', 'bugs', 'evidencePaths', 'screenshotsVisuallyConfirmed'],
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
    summary: { type: 'string', description: '非APPROVE時の判定理由の要約' },
    bugs: {
      type: 'array',
      items: {
        type: 'object',
        required: ['summary', 'severity', 'assignee'],
        properties: {
          summary: { type: 'string' },
          severity: { type: 'string', enum: ['blocker', 'major', 'minor'] },
          assignee: { type: 'string', enum: ['gameplay-engineer', 'ui-engineer'] },
          storyId: { type: 'string' }
        }
      }
    },
    failedAcceptance: { type: 'array', items: { type: 'string' }, description: '不合格だった story ID 一覧' },
    evidencePaths: { type: 'array', items: { type: 'string' }, description: '保存した証跡ファイルの相対パス一覧' },
    screenshotsVisuallyConfirmed: {
      type: 'boolean',
      description: '撮影した全スクリーンショットを Read で目視し、対象（モデル・UI文字）が写っていることを確認したか（gates.md QA-PLAY 視覚証跡の目視義務。false/未実施の APPROVE は無効）'
    }
  }
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
          bytes: { type: 'number' }
        }
      }
    },
    extraFilesInEvidenceDir: { type: 'array', items: { type: 'string' } }
  }
};

const AUDIT_SCHEMA = {
  type: 'object',
  required: ['totalAssetCost', 'budgetUsd', 'overBudget', 'licenseFlags'],
  properties: {
    totalAssetCost: { type: 'number', description: 'MANIFEST.jsonl の cost_usd 合算（USD）' },
    budgetUsd: { type: 'number', description: 'state/budget.txt の値' },
    overBudget: { type: 'boolean' },
    licenseFlags: { type: 'array', items: { type: 'string' } },
    mustReplaceAssets: { type: 'array', items: { type: 'string' }, description: 'license=placeholder-nc / must_replace=true の残存資産' },
    provenanceGaps: { type: 'array', items: { type: 'string' }, description: 'MANIFEST 必須フィールド（plan_tier / bbox_authoring_m / validator / license）の記録漏れ・shippable:false ルート由来・cost_estimated:true の資産一覧。無ければ空配列' }
  }
};

const CD_SCHEMA = {
  type: 'object',
  required: ['verdict', 'summary', 'playInstructions'],
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
    summary: { type: 'string', description: '人間が5分で判断できる要約（何を作ったか/何を判断してほしいか/既知の課題）' },
    playInstructions: { type: 'string', description: '起動と操作の手順' },
    mustFix: { type: 'array', items: { type: 'string' }, description: 'REJECT時、人間に見せる前に直すべき点' }
  }
};

// ---------- args / 共有状態 ----------

// args 正規化: 起動側/ランナーが JSON 文字列で渡してくるケースの防御（E2 で実測。
// パース不能な文字列は明示エラーに倒す — 黙って既定値に落とさない）
const ARGS = (typeof args === 'string') ? JSON.parse(args) : (args || {});
const reviewMode = ARGS.reviewMode ? String(ARGS.reviewMode) : 'lean';
const feedbackPath = ARGS.checkpointBFeedbackPath ? String(ARGS.checkpointBFeedbackPath) : 'state/checkpoint-b-feedback.md';
const unresolvedFindings = [];
const verdictHistory = []; // 全ゲート verdict の蓄積（review-mode=full: スキルが完了後に全件を人間へ提示する素材）
let laneContextWarn = ''; // レーン実装プロンプト冒頭に注入する警告（Polish 開始時に Build バッチ検証不合格を伝える — adversarial L-11）

// ---------- エンジンプロファイル（contract.md §11。値は各 tech-stack 文書と一致させること）----------
// phaser の値は従来のプロンプト文字列と同一に保つ（後方互換）。
// engine 未指定のみ phaser 既定。空文字・不正値は下の throw に倒す（無言フォールバック禁止）
const engine = (ARGS.engine !== undefined && ARGS.engine !== null) ? ARGS.engine : 'phaser';
const ENGINE_PROFILES = {
  phaser: {
    techStackDoc: DOCS + '/tech-stack.md',
    manifestPath: 'game/assets/MANIFEST.jsonl',
    rawAssetDir: 'game/assets/',
    assets3d: false,
    verifyCmd: 'cd game && npm run typecheck && npm run build',
    laneVerifyLine: '`cd game && npm run typecheck` を実行し、**自分の編集ファイル起因のエラーのみ** 0 にする（他レーンの書きかけ WIP・他レーンが提供予定の API 参照に起因するエラーは無視してよい — レーン合流後のバッチ検証が最終確認する。**並走レーン中は `npm run build` を実行しない** — dist/ が他レーンと衝突する — tech-stack.md「検証コマンド」節）',
    implRulesLine: '2) game/src を tech-stack.md 規約で実装（マジックナンバーは config.ts に集約 / delta-time必須 / Sceneは薄くロジックはsystems/ / systems/はPhaserをimportしない / 資産参照はASSET_KEYS経由）',
    reviewRulesLine: 'コード規約違反（マジックナンバー・delta-time未使用・Scene肥大・systems/のPhaser依存・パスのハードコード）',
    codeAddExample: 'git add game/src state/stories.yaml state/reviews',
    configPath: 'game/src/config.ts',
    audioFormatLine: '全音声: ffmpeg loudnorm(-16 LUFS)＋無音トリム → OGG Vorbis 128-160kbps + M4A/AAC の両方を出力。',
    qaTarget: 'headlessブラウザで game/ を実際に起動・操作して判定せよ',
    playInstructions: 'cd game && npm install && npm run dev'
  },
  unity: {
    techStackDoc: DOCS + '/tech-stack-unity.md',
    manifestPath: 'game/_generated/MANIFEST.jsonl',
    rawAssetDir: 'game/_generated/',
    assets3d: true,
    verifyCmd: 'tech-stack-unity.md「検証コマンド」の typecheck 相当（EditMode テスト。合格 = exit 0 かつ結果 XML で failed 0 — exit code 単独判定禁止）と build 相当（ForgeBuild.BuildMac batchmode）',
    laneVerifyLine: '**Unity をここでは起動しない**（単一インスタンスロック — 並走レーン・資産レーンと衝突する。EditMode/ビルド検証はレーン合流後のバッチ検証区間で一括実行される — tech-stack-unity.md「検証コマンド」節）。代わりに参照する型・メンバ・アセットキー・シリアライズ対象の実在を Read/Grep で静的確認し、コンパイルを通らない参照を残さない',
    implRulesLine: '2) game/Assets/Scripts を tech-stack-unity.md 規約で実装（マジックナンバーは GameConfig.cs に集約 / Time.deltaTime必須 / Componentsは薄くロジックはSystems/（pure C#・MonoBehaviour禁止）/ Input System 集約 / 資産参照は AssetKeys 経由）',
    reviewRulesLine: 'コード規約違反（マジックナンバー・deltaTime未使用・Components肥大・Systems/のMonoBehaviour依存・パスのハードコード。rules/unity-code.md）',
    codeAddExample: 'git add game/Assets game/Packages game/ProjectSettings state/stories.yaml state/reviews',
    configPath: 'game/Assets/Scripts/GameConfig.cs',
    audioFormatLine: '全音声: ffmpeg loudnorm(-16 LUFS)＋無音トリム → OGG Vorbis 128-160kbps を出力（Unity ネイティブ対応。M4A 不要）。',
    qaTarget: 'tech-stack-unity.md「QA-PLAY の実行方法」に従い、batchmode ビルドと PlayMode テスト（入力擬似発行・LogAssert・ScreenCapture）で game/ を実プレイ検証せよ',
    playInstructions: 'open game/Build/ForgeGame.app（または Unity エディタで game/ を開いて Play）'
  },
  unreal: {
    techStackDoc: DOCS + '/tech-stack-unreal.md',
    manifestPath: 'game/_generated/MANIFEST.jsonl',
    rawAssetDir: 'game/_generated/',
    assets3d: true,
    verifyCmd: 'tech-stack-unreal.md「検証コマンド」の typecheck/build 相当（BuildCookRun -build。テスト実行時の合格 = exit 0 かつレポート JSON で failed 0）',
    laneVerifyLine: '**UE/UBT をここでは起動しない**（単一インスタンスロック — 並走レーン・資産レーンと衝突する。BuildCookRun 検証はレーン合流後のバッチ検証区間で一括実行される — tech-stack-unreal.md「検証コマンド」節）。代わりに参照する型・メンバ・ヘッダ include の実在を Read/Grep で静的確認し、コンパイルを通らない参照を残さない',
    implRulesLine: '2) game/Source/ForgeGame を tech-stack-unreal.md 規約で実装（マジックナンバーは GameConfig.h に集約 / DeltaSeconds必須 / Actorsは薄くロジックはSystems/（pure C++・UObject禁止）/ Enhanced Input 集約 / 資産パスは GameConfig.h の定数経由。Blueprintロジック禁止）',
    reviewRulesLine: 'コード規約違反（マジックナンバー・DeltaSeconds未使用・Actors肥大・Systems/のUObject依存・パスのハードコード・Blueprintロジック。rules/unreal-code.md）',
    codeAddExample: 'git add game/Source game/Config state/stories.yaml state/reviews',
    configPath: 'game/Source/ForgeGame/GameConfig.h',
    audioFormatLine: '全音声: ffmpeg loudnorm(-16 LUFS)＋無音トリム → WAV を出力（UE ネイティブ対応。OGG/M4A 不要）。',
    qaTarget: 'tech-stack-unreal.md「QA-PLAY の実行方法」に従い、BuildCookRun と Automation RunTests（レポートJSON・スクリーンショット）で game/ を実プレイ検証せよ',
    playInstructions: 'open game/Build/Mac/ForgeGame.app'
  }
};
const EP = ENGINE_PROFILES[engine];
if (!EP) throw new Error('args.engine が不正: ' + engine + '（contract §11: phaser|unity|unreal）');
const MANIFEST = EP.manifestPath;

const CODE_COMMIT_RULE =
  'コミット規律: git add は自分が編集した**個別ファイルパス**のみ（ディレクトリ指定・git add -A は禁止 — 単一 index を共有する並走レーン/資産トラックの staged 変更や未コミット WIP を巻き込む）。' +
  'commit は必ずパス指定形 `git commit -m "<msg>" -- <自分の編集ファイル...>` を使う（他パスの staged 巻き込みを防ぐ。**同一ファイル内の他レーン WIP は除外できない**ため、共有ファイル — config/types/stories.yaml — への自分の追記は編集後すぐ**その1ファイルだけ**を単独コミットして確定させる）。' +
  'コミット hash は `git rev-parse HEAD` ではなく `git log --format="%H %s" -20` から**自分のコミットメッセージに一致する最上（最新）の行**の hash を取り、`git show --stat <hash>` に**自分の編集ファイルが含まれることを確認**する（rev-parse HEAD は並走レーンの直後コミットを拾い得る。一致行が窓に無ければ -50 で再取得。含まれない・commit 自体が失敗した場合は古い同名コミットの hash を返さず**失敗を正直に報告**する）。' + GIT_RETRY_NOTE;
const ASSET_COMMIT_RULE =
  'コミット規律: git add は ' + EP.rawAssetDir.replace(/\/$/, '') + ' design docs state/reviews のパス限定で行い、**commit も必ずパス指定形** `git commit -m "<msg>" -- ' + EP.rawAssetDir.replace(/\/$/, '') + ' design docs state/reviews`（git add -A・素の git commit・state ディレクトリ丸ごと指定は禁止 — 並走コードレーンの stories.yaml / active.md の WIP を巻き取らない）。' + GIT_RETRY_NOTE;

// 並走レーン規律（retro-e2 案A: assignee レーン並列。コード編集と review agent のみ並列 —
// エンジン起動を伴う検証はレーン合流後のバッチ検証区間に集約（案B）。tech-stack 文書「検証」節が正本）
const LANE_RULE =
  '並走レーン規律: あなたの assignee の担当領域以外のコードを書き換えない（gameplay-engineer=ゲーム機構・システム・永続化層 / ui-engineer=UI・シーン表示層。境界は docs/architecture.md）。' +
  '共有ファイル（' + EP.configPath + '・共有型定義）は**自 story に必要な定数/型の追記のみ**（既存行の変更・削除は禁止 — 並走レーンと衝突する。例外: story の acceptance/指示が明示するバランス調整はその対象定数の**値変更のみ**許可）。' +
  'やむを得ず他レーン担当領域の既存シーン/配線ファイルに触れる場合（例: Result 到達時の persist 配線）は**ピンポイント Edit のみ・ファイル全面 Write 禁止・Edit 直前に必ず再 Read**（並走レーンのコミット済み変更を巻き戻さない）。' +
  'state/stories.yaml は自 story のブロック内のみ（status 行・注記）をピンポイント Edit（ファイル全面書き直し禁止 — 並走レーンの更新を消す）。' +
  'state/active.md には触らない（並走レーンと衝突する — 現在地更新はレーン合流後の直列区間の責務）。' +
  '他レーンの story が提供予定の API に依存する場合は docs/architecture.md の設計に合わせた呼び出しで実装してよい（コンパイル整合はレーン合流後のバッチ検証が最終確認する）。';

// ---------- 共通: バッチ検証（レーン合流後の直列区間。retro-e2 案B） ----------

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
    '4) 修正した場合は state/reviews/batch-verify.md に「phase / 原因 story / 修正内容 / ISO8601 日時」を追記し（日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）、コミット規律のパス指定形で git commit（メッセージ: "batch-verify fix (' + phaseName + ')"）。state/active.md の現在地を「' + phaseName + ' バッチ検証完了」に更新（直列区間 — レーン規律の対象外）。' + CODE_COMMIT_RULE + '\n' +
    '構造化返却: ok（最終合格で true。到達できなければ false を正直に）/ fixedNotes / unresolved。',
    { label: 'batch-verify-' + phaseName.toLowerCase(), phase: phaseName, agentType: 'gameplay-engineer', schema: BATCH_VERIFY_SCHEMA, effort: 'high' }
  );
  if (bv === null) {
    unresolvedFindings.push('[BLOCKER] ' + phaseName + ': バッチ検証 agent が結果を返さなかった（ビルド健全性未確認のまま続行 — 後段 QA が検出する）');
    return false;
  }
  // 修正内容は人間可視チャネルへ載せる（CR-CODE を通らない直接コミットのため、log() だけでは
  // review-mode=full の全履歴提示から漏れる — adversarial H-3）
  for (const n of (bv.fixedNotes || [])) {
    unresolvedFindings.push(phaseName + '[batch-verify修正・CR-CODE非経由] ' + n);
  }
  for (const u of (bv.unresolved || [])) {
    unresolvedFindings.push('[BLOCKER] ' + phaseName + '[batch-verify] ' + u);
  }
  if (bv.ok !== true || (bv.unresolved || []).length > 0) {
    if (bv.ok !== true && (bv.unresolved || []).length === 0) {
      unresolvedFindings.push('[BLOCKER] ' + phaseName + ': バッチ検証が合格に未到達（詳細は state/reviews/batch-verify.md）');
    }
    log('batch-verify(' + phaseName + '): 不合格または未解決あり（エスカレーション）');
    return false;
  }
  log('batch-verify(' + phaseName + '): 合格');
  return true;
}

// ---------- 共通: story実装 + CR-CODE ループ（review-loops.md: MAX_ITER 2） ----------

async function implementStoryWithReview(story, phaseName) {
  const sid = String(story.id || 's-unknown').toLowerCase();
  const assignee = ENGINEERS.indexOf(story.assignee) >= 0 ? story.assignee : 'gameplay-engineer';

  const impl = await agentR(
    laneContextWarn +
    'story ' + story.id + '「' + story.title + '」を実装せよ。\n' +
    '読むこと: state/stories.yaml（該当story）、design/gdd.md、design/concept.md（ピラー ' + (story.pillar || 'P-xx') + '）、docs/architecture.md、docs/conventions.md、' + EP.techStackDoc + '。\n' +
    '手順:\n' +
    '1) state/stories.yaml の ' + story.id + ' を status: in-progress に更新\n' +
    EP.implRulesLine + '\n' +
    '3) ' + EP.laneVerifyLine + '\n' +
    '4) ' + story.id + ' を status: review に更新\n' +
    '5) git commit -m "' + story.id + ': ' + story.title + '" し、そのコミットhashを commitHash として報告する。' + CODE_COMMIT_RULE + '\n' +
    LANE_RULE + '\n' +
    'acceptance: ' + story.acceptance,
    { label: 'impl-' + sid, phase: phaseName, agentType: assignee, schema: IMPL_SCHEMA, effort: 'high' }
  );
  if (impl === null) {
    unresolvedFindings.push(story.id + ': 実装agentが結果を返さなかった（未実装の可能性）');
    return false;
  }
  let commitHash = impl.commitHash ? String(impl.commitHash) : null;

  let approved = false;
  for (let iter = 1; iter <= 2; iter++) {
    const reviewPrompt =
      'CR-CODE レビュー（story ' + story.id + '、iteration ' + iter + '）。\n' +
      (commitHash
        ? '対象: `git show ' + commitHash + '` の diff **のみ**（並走する資産生成トラックの変更や他storyの差分はレビュー対象外）。\n'
        : '対象: game/ 配下の story ' + story.id + ' に対応する直近の実装変更（コミットhash不明のため state/reviews/' + sid + '.md と実装ファイルから対象を特定）。\n') +
      '観点は ' + DOCS + '/gates.md の CR-CODE 節に従う。加えて ' + EP.techStackDoc + ' の' + EP.reviewRulesLine + 'を確認。\n' +
      'acceptance「' + story.acceptance + '」がコード上で満たされるかも確認。\n' +
      '前提（並走レーン設計）: 他レーンの story が提供予定の API への参照は、docs/architecture.md の設計に合致していれば「実体未実装」だけを理由に blocker としない（コンパイル整合はレーン合流後のバッチ検証が保証する。設計との不一致・誤用は通常どおり指摘してよい）。**このレビューは読み取り専用 — エンジン起動・ビルド/テストコマンドの実行禁止**（並走レーン中の単一インスタンスロック/dist 競合）。\n' +
      'findings は severity（blocker=設計欠陥 / major / minor）付きで返せ。0件なら空配列。';
    const reviews = await parallel([
      () => agentR(reviewPrompt, { label: 'cr-' + sid + '-' + iter, phase: phaseName, agentType: 'pr-review-toolkit:code-reviewer', schema: CODE_REVIEW_SCHEMA }),
      () => agentR(reviewPrompt + '\n特に黙殺されたエラー・握り潰された失敗パス・catchして無視している箇所を重点的に洗え。',
        { label: 'sfh-' + sid + '-' + iter, phase: phaseName, agentType: 'pr-review-toolkit:silent-failure-hunter', schema: CODE_REVIEW_SCHEMA })
    ]);
    const validReviews = reviews.filter(Boolean);
    if (validReviews.length === 0) {
      // 両レビュアー失敗 = レビュー不成立。findings 0 件を APPROVE と誤認しない（prototype.js と同じガード）
      unresolvedFindings.push(story.id + ': CR-CODE iteration ' + iter + ' のレビューペアが両方失敗（レビュー未実施 — 自動 APPROVE しない）');
      verdictHistory.push({ gate: 'CR-CODE', artifact: sid, iteration: iter, verdict: 'CONCERNS', findings: ['レビューペア両方失敗（レビュー未実施）'] });
      log('CR-CODE ' + story.id + ' iteration ' + iter + ': レビューペア両方失敗 — 次 iteration へ');
      continue;
    }
    if (validReviews.length < 2) {
      unresolvedFindings.push(story.id + ': CR-CODE iteration ' + iter + ' でレビューペアの片方が結果を返さなかった（片側判定で続行）');
    }
    const findings = validReviews.reduce(function (acc, r) { return acc.concat(r.findings || []); }, []);
    const hasBlocker = findings.some(function (f) { return f.severity === 'blocker'; });
    const verdict = findings.length === 0 ? 'APPROVE' : (hasBlocker ? 'REJECT' : 'CONCERNS');
    verdictHistory.push({
      gate: 'CR-CODE',
      artifact: sid,
      iteration: iter,
      verdict: verdict,
      findings: findings.map(function (f) { return '[' + f.severity + '] ' + f.summary; })
    });
    log('CR-CODE ' + story.id + ' iteration ' + iter + ': ' + verdict + '（findings ' + findings.length + '件）');

    if (verdict === 'APPROVE') {
      approved = true;
      await agentR(
        'story ' + story.id + ' の CR-CODE iteration ' + iter + ' が APPROVE（findings 0件）。後処理をせよ:\n' +
        '1) state/stories.yaml の ' + story.id + ' を status: done に更新\n' +
        '2) state/reviews/' + sid + '.md に ' + DOCS + '/review-loops.md の追記形式で「CR-CODE iteration ' + iter + ' — APPROVE」を追記（日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）\n' +
        '3) ' + CODE_COMMIT_RULE + '\n' +
        LANE_RULE,
        { label: 'close-' + sid, phase: phaseName, agentType: assignee, effort: 'low' }
      );
      break;
    }

    const isLast = iter === 2;
    const fix = await agentR(
      'story ' + story.id + ' の CR-CODE iteration ' + iter + ' 判定: ' + verdict + '。findings(JSON):\n' + JSON.stringify(findings) + '\n' +
      '対応せよ:\n' +
      '1) 各finding に修正で対応するか、見送るなら理由を明記（黙殺禁止）\n' +
      '2) 修正後の検証: ' + EP.laneVerifyLine + '\n' +
      '3) state/reviews/' + sid + '.md に ' + DOCS + '/review-loops.md の追記形式で iteration 記録（verdict・指摘要約・対応/見送り＋理由・ISO8601日時。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）を追記\n' +
      '4) git commit -m "' + story.id + ': fix CR-CODE iteration ' + iter + '" し、そのコミットhashを commitHash として報告する。' + CODE_COMMIT_RULE + '\n' +
      LANE_RULE + '\n' +
      (isLast
        ? '5) MAX_ITER到達のため state/stories.yaml の ' + story.id + ' を status: done に更新し、未対応findingがあれば注記に残す（エスカレーションはCheckpointで人間に提示される）'
        : '5) state/stories.yaml の status は review のまま（次iterationで再レビュー）'),
      { label: 'fix-' + sid + '-' + iter, phase: phaseName, agentType: assignee, schema: IMPL_SCHEMA, effort: 'high' }
    );
    if (fix && fix.commitHash) commitHash = String(fix.commitHash);

    if (isLast) {
      // blocker（設計欠陥＝REJECT相当）残存は [BLOCKER] プレフィクスで区別し、CD-CHECKPOINT が
      // 要約冒頭で個別警告する（gates.md CD-CHECKPOINT 観点3。minor と同列に埋没させない）
      unresolvedFindings.push(
        (hasBlocker ? '[BLOCKER] ' : '') +
        story.id + ': CR-CODE MAX_ITER(2)到達で非APPROVE。残findings: ' +
        findings.map(function (f) { return '[' + f.severity + '] ' + f.summary; }).join(' / ')
      );
    }
  }
  if (!approved) {
    // 状態ファイル＝真実（CLAUDE.md 絶対規約5）: レビューペア両方失敗等で fix agent の status 更新
    // （fix プロンプト step5）が走らなかった経路でも、story を review/in-progress のまま放置しない
    // （adversarial W-2）。エスカレーション注記つきで確定させる（prototype.js の bookkeep と同型）
    await agentR(
      'state/stories.yaml の ' + story.id + ' の status を確認し、done でなければ done に更新して\n' +
      '「# note: CR-CODE unresolved — state/reviews/' + sid + '.md 参照」の注記を acceptance 行の下にコメントで追加せよ\n' +
      '（MAX_ITER 到達エスカレーション。既に done かつ注記済みなら何もしない）。' + CODE_COMMIT_RULE + '\n' + LANE_RULE,
      { label: 'bookkeep-' + sid, phase: phaseName, agentType: assignee, effort: 'low' }
    );
  }
  return approved;
}

// ---------- 共通: 資産バッチ生成 + AR-ASSET ループ（MAX 3 + fallback 1） ----------

async function assetBatchLoop(kind, producerAgent, producerBrief, replanStories) {
  const reviewFile = 'state/reviews/assets-' + kind + '.md';
  const budgetRule =
    'API キー: **API を呼ぶ Bash に限り**冒頭で `set -a; source .env 2>/dev/null; set +a` を実行してから curl する（検証・後処理 — ffmpeg/npx 等 — の Bash では source しない: サードパーティ子プロセスへのキー継承を避ける。キー値の echo・ログ出力禁止 — contract §10）。API エラー（401/403/429/5xx）は握り潰さず HTTP ステータスと共に報告。' +
    '予算規律（' + DOCS + '/assets-config.md）: 各生成の前に ' + MANIFEST + ' の cost_usd 合算＋今回の見込みコストを state/budget.txt と比較。' +
    '超過見込みなら生成を停止し budgetExceeded: true で報告（Checkpointで人間に提示される）。ルーティングは state/asset-routing.json が真実（生成中の再判定禁止。shippable:false ルートで生成した資産は必ず notes で報告）。' +
    '全生成を ' + MANIFEST + ' に1行1資産で追記（provider/model/prompt/seed/cost_usd/plan_tier/sha256/license/generated_at。3D資産は kind/polycount/bone_count/rigged/format/units/bbox_authoring_m/validator も必須。クレジット換算見積は cost_estimated:true）。' +
    '**Primary が API 失敗（4xx/5xx/timeout）の場合、fallback を 1 段も試さずにローカル縮退/プレースホルダ/must-replace 化することを禁止**（品質不合格による再生成は従来どおり Primary 固定 — この規則は API 失敗時のルート切替の話）。state/asset-routing.json の fallbacks を上から順に全段試行し、各試行の『ルート名 + HTTP ステータス（または失敗理由）』を degradedRoutes に必ず列挙する（例: "model_character: meshy:direct→422 / fal:meshy-v6→429 / tripo:direct→403 → local縮退"）。全段失敗の場合のみローカル縮退可（retro-e3 指摘7）。';
  const replanNote = (replanStories && replanStories.length > 0)
    ? 'Replan由来の資産story(JSON・design/assets.md に反映済みのはず。エントリが漏れていれば design/assets.md に追記した上で生成対象に含めよ):\n' + JSON.stringify(replanStories)
    : '';

  let failedAssets = null; // null = 初回（全未生成分が対象）
  let lastVerdict = 'REJECT';

  for (let iter = 1; iter <= 4; iter++) {
    const isFallback = iter === 4;
    const target = failedAssets === null
      ? '対象: design/assets.md のうち ' + MANIFEST + ' に未記載の全' + kind + '資産。' + (replanNote ? '\n' + replanNote : '')
      : '対象: 前回不合格の資産のみ。failedAssets(JSON):\n' + JSON.stringify(failedAssets) + '\n各 retryInstruction に従って再生成せよ。';
    const route = isFallback
      ? '【fallback回】AR-ASSET 3回不合格のため、state/asset-routing.json の fallback プロバイダへ切替えて再生成せよ（' + DOCS + '/assets-config.md のルーティング表参照）。'
      : '';

    const gen = await agentR(
      producerBrief + '\n' + target + '\n' + route + '\n' + budgetRule + '\n' + ASSET_COMMIT_RULE + '\n' +
      '生成後パイプライン（' + DOCS + '/assets-config.md「生成後パイプライン」節）を全段実施してから報告せよ。',
      { label: 'gen-' + kind + '-' + iter, phase: 'AssetGen', agentType: producerAgent, schema: ASSET_GEN_SCHEMA, effort: 'high' }
    );
    if (gen === null) {
      unresolvedFindings.push('AssetGen(' + kind + '): 生成agentが iteration ' + iter + ' で結果を返さなかった');
      break;
    }
    // 開示事項の機械回収（budgetExceeded に関係なく常に拾う — 自由文で握り潰さない）
    for (const d of (gen.degradedRoutes || [])) {
      unresolvedFindings.push('AssetGen(' + kind + ')[縮退] ' + d);
    }
    if (gen.notes && String(gen.notes).trim().length > 0) {
      unresolvedFindings.push('AssetGen(' + kind + ')[開示] ' + gen.notes);
    }
    if (gen.budgetExceeded) {
      unresolvedFindings.push('AssetGen(' + kind + '): 予算超過見込みで生成停止（未生成 ' + (typeof gen.remainingPlanned === 'number' ? gen.remainingPlanned : '不明') + ' 件。state/budget.txt 参照）');
      log('AssetGen(' + kind + '): 予算超過見込みで停止');
      break;
    }
    if (!gen.generated || gen.generated.length === 0) {
      if (failedAssets === null) {
        // 初回の「生成 0 件」は (a) 全資産生成済み と (b) API 全滅 の2通り。remainingPlanned で区別
        if ((typeof gen.remainingPlanned === 'number' && gen.remainingPlanned > 0)) {
          unresolvedFindings.push('AssetGen(' + kind + '): 生成 0 件だが未生成対象が ' + gen.remainingPlanned + ' 件残存（API 全滅の疑い。notes: ' + (gen.notes || 'なし') + '）');
          lastVerdict = 'REJECT';
          break;
        }
        log('AssetGen(' + kind + '): 生成対象なし（全資産生成済み — remainingPlanned 0 を確認）');
        lastVerdict = 'APPROVE';
        break;
      }
      // regen/fallback 回で 0 件 = 不合格資産が未対応のまま。空リストの再レビュー（=空虚な APPROVE）に流さない（red-team 指摘）
      unresolvedFindings.push('AssetGen(' + kind + '): iteration ' + iter + ' の再生成が 0 件（不合格 ' + failedAssets.length + ' 件が未対応のまま残存）');
      break;
    }

    const review = await agentR(
      'AR-ASSET 判定（' + kind + ' バッチ、iteration ' + iter + '）。' + DOCS + '/gates.md の AR-ASSET 節に従え。\n' +
      '対象資産(JSON): ' + JSON.stringify(gen.generated || []) + '\n' +
      '照合先: design/art-bible.json（style_block/palette/解像度）・design/assets.md（サイズ/向き/フレーム数）。\n' +
      '- 画像は実ファイルを開き、ゲーム内サイズへ縮小した可読性とアルファ縁品質を確認\n' +
      '- 音声は長さ・ラウドネス・仕様一致を確認。BGMはループ検証済みであることを生成報告とファイルで確認\n' +
      '- 3D（MDL/ANM）は gates.md AR-ASSET の 3D 観点で機械検査（gltf validate / ポリ数・ボーン数 / authoring スケール / スタイルは ' + EP.rawAssetDir + 'previews/ のレンダリング。エンジン取込後項目は※節どおり Integrate 側の責務）\n' +
      '- ' + reviewFile + ' に ' + DOCS + '/review-loops.md の追記形式で iteration 記録を追記（追記は判定者たるあなたの責務。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）\n' +
      '応答の1行目は「AR-ASSET: APPROVE|CONCERNS|REJECT」とし、構造化返却の verdict にも同じ判定を入れよ。\n' +
      '不合格資産には retryInstruction（プロンプト修正案）を必ず付けよ。\n' +
      '**再生成では直らない開示事項**（gates.md AR-ASSET 観点6: shippable:false ルート由来 / fal 経由 Meshy のライセンス継承未検証 / cost_estimated:true / must_replace 等）は failedAssets ではなく disclosures に入れよ（failedAssets に入れると無意味な再生成ループが走る。品質合格＋開示事項ありは APPROVE + disclosures）。',
      { label: 'ar-asset-' + kind + '-' + iter, phase: 'AssetGen', agentType: 'art-reviewer', schema: ASSET_REVIEW_SCHEMA }
    );
    if (review === null) {
      unresolvedFindings.push('AssetGen(' + kind + '): art-reviewer が iteration ' + iter + ' で結果を返さなかった');
      break;
    }
    for (const d of (review.disclosures || [])) {
      unresolvedFindings.push('[AR-ASSET][' + kind + '][開示] ' + d);
    }
    lastVerdict = review.verdict;
    verdictHistory.push({
      gate: 'AR-ASSET',
      artifact: 'assets-' + kind,
      iteration: iter,
      verdict: review.verdict,
      findings: (review.failedAssets || []).map(function (f) { return f.file + '（' + f.reason + '）'; })
        .concat((review.disclosures || []).map(function (d) { return '[開示] ' + d; }))
    });
    log('AR-ASSET(' + kind + ') iteration ' + iter + ': ' + review.verdict + '（不合格 ' + (review.failedAssets || []).length + '件）');

    if (review.verdict === 'APPROVE') {
      lastVerdict = 'APPROVE';
      break;
    }
    if ((review.failedAssets || []).length === 0) {
      // 非APPROVE なのに failedAssets が空 = バッチ全体指摘（スタイルロック違反等）か reviewer プロトコル不整合。
      // APPROVE に変換して素通りさせない（red-team 指摘）— 再生成対象を特定できないためエスカレーションで抜ける
      unresolvedFindings.push('AssetGen(' + kind + '): iteration ' + iter + ' が ' + review.verdict + ' だが failedAssets が空（バッチ全体指摘の可能性 — 人間確認が必要）');
      break;
    }
    failedAssets = review.failedAssets;
    if (isFallback) {
      unresolvedFindings.push(
        'AssetGen(' + kind + '): fallback後も不合格資産あり: ' +
        failedAssets.map(function (f) { return f.file + '（' + f.reason + '）'; }).join(' / ')
      );
    }
  }
  return lastVerdict;
}

// ===== Phase: Replan =====
phase('Replan');
log('full-build 開始 / review-mode: ' + reviewMode + ' / feedback: ' + feedbackPath +
  '（全 verdict は verdictHistory に蓄積して返す。full ではスキルが完了後に全件を人間へ提示する）');

const replanResults = await parallel([
  () => agentR(
    'Phase 3 再計画（tech-director）。\n' +
    '読むこと: ' + feedbackPath + '、state/stories.yaml、design/gdd.md、design/concept.md、design/assets.md、docs/architecture.md、' + DOCS + '/contract.md（§7 stories.yaml スキーマ・§8 安定ID）。\n' +
    '手順:\n' +
    '1) checkpoint-b-feedback の各項目を story に落とす。新storyのIDは既存の最大 S-xx の続番（振り直し・削除禁止）、phase: build、status: todo、pillar は design/concept.md の P-xx を必ず参照、assignee は contract §2 のagent名、acceptance は検証可能な文で書く。バランス調整系 story は**変更対象の定数名を acceptance に明示**し、同一定数を複数 story・複数 assignee に割り当てない（並走レーンの共有ファイル規律 — 実装側の値変更例外はこの明示が条件）\n' +
    '2) 資産（画像/SFX/BGM/MDL/ANM）に関わる feedback は design/assets.md にもエントリ追加/修正で反映せよ（AssetGen フェーズは design/assets.md を生成対象の真実とする。assignee が art-director / audio-designer の story は生成対象リストとしても渡される。**既生成資産の再生成**が必要な場合は design/assets.md の該当行の状態を must-replace または rejected に変更すること — MANIFEST 記載済み資産の再生成はこの状態変更が唯一のトリガー）\n' +
    '3) 既存の phase: build 未完了storyと合わせ、依存順（先に必要なものが先）に整理して state/stories.yaml を更新\n' +
    '4) state/active.md を更新（現在地: Phase3 Replan完了。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）\n' +
    '返却: phase: build かつ status が done 以外の全story（実装すべき順）。',
    { label: 'replan-stories', phase: 'Replan', agentType: 'tech-director', schema: STORY_LIST_SCHEMA, effort: 'high' }
  ),
  () => agentR(
    'Phase 3 GDD改訂判断（game-designer）。\n' +
    '読むこと: ' + feedbackPath + '、design/gdd.md、design/concept.md。\n' +
    'checkpoint-b-feedback がゲームデザイン（数値・ルール・フロー）の変更を要求している場合のみ、design/gdd.md の該当節を更新せよ。\n' +
    '制約: ピラー（P-xx）の追加・削除・変更は禁止。変更した節には feedback 由来である旨を注記。変更不要なら何も書き換えず「変更不要」と返す。',
    { label: 'replan-gdd', phase: 'Replan', agentType: 'game-designer' }
  )
]);

let replan = replanResults[0];
if (!replan || !Array.isArray(replan.stories)) {
  log('Replan: tech-director の構造化返却が得られず。stories.yaml から再抽出する');
  replan = await agentR(
    'state/stories.yaml を読み、phase: build かつ status が done 以外の全storyを実装すべき順で返せ。ファイルの変更はするな。',
    { label: 'replan-extract', phase: 'Replan', agentType: 'tech-director', schema: STORY_LIST_SCHEMA, effort: 'low' }
  );
}
if (!replan || !Array.isArray(replan.stories)) {
  throw new Error('Replan失敗: state/stories.yaml から build story を取得できなかった');
}

const codeStories = replan.stories.filter(function (s) { return ENGINEERS.indexOf(s.assignee) >= 0; });
// art-director story を 3D（MDL/ANM）と画像に振り分ける（STORY_LIST_SCHEMA に assetKind が無いため
// title/acceptance の語彙で判定。3D 語彙を含む story は models バッチの replanStories へ渡す）
const artStories = replan.stories.filter(function (s) { return s.assignee === 'art-director'; });
const MODEL_WORDS = /MDL-|ANM-|3D|モデル|リグ|メッシュ|FBX|GLB/;
const modelStories = artStories.filter(function (s) { return MODEL_WORDS.test((s.title || '') + ' ' + (s.acceptance || '')); });
const imageStories = artStories.filter(function (s) { return modelStories.indexOf(s) < 0; });
const audioStories = replan.stories.filter(function (s) { return s.assignee === 'audio-designer'; });
log('Replan完了: build story ' + replan.stories.length + '件（うちコード ' + codeStories.length + '件 / 画像 ' + imageStories.length + '件 / 3D ' + modelStories.length + '件 / 音声 ' + audioStories.length + '件）');

// ===== Phase: Build ∥ AssetGen =====
// フェーズ遷移マーカーは thunk 内では呼ばない（並走で交錯するため）。グルーピングは agent opts の phase ラベルで行う。
const assetGenThunks = [
  () => assetBatchLoop(
    'images', 'art-director',
    '画像資産の一括生成（art-director）。読むこと: design/assets.md、design/art-bible.json、state/asset-routing.json、' + DOCS + '/assets-config.md、' + MANIFEST + '、state/budget.txt。\n' +
    'スタイル一貫性プロトコル厳守: 全プロンプトに art-bible.json の style_block を機械的に前置、seed を記録、hero は character_reference を共用。' +
    'スプライトは全数アルファチャンネルを機械検証（白背景PNGの出荷禁止）。' + (EP.assets3d ? '（3Dエンジンのため atlas 化は不要。UI・テクスチャ用途のみ）' : 'atlas化まで実施。'),
    imageStories
  ),
  () => assetBatchLoop(
    'audio', 'audio-designer',
    '音声資産の一括生成（audio-designer）。読むこと: design/assets.md、design/art-bible.md（トーン参照）、state/asset-routing.json、' + DOCS + '/assets-config.md、' + MANIFEST + '、state/budget.txt。\n' +
    'SFX: ElevenLabs SFX v2 REST直（duration_seconds 明示。公式MCP経由は禁止）。共通語彙で4変種→ベスト選別。\n' +
    'BGM: Eleven Music REST（model music_v2、composition_plan でセクション長指定、force_instrumental: true、seed記録）。' +
    '**ループ検証必須**: 2連結してシームのクリック/RMS段差をスキャンし、失敗したら再生成。合格までMANIFESTに出荷可として記録しない。\n' +
    EP.audioFormatLine,
    audioStories
  )
];
if (EP.assets3d) {
  assetGenThunks.push(() => assetBatchLoop(
    'models', 'art-director',
    '3Dモデル/アニメ資産（MDL/ANM）の一括生成（art-director）。読むこと: design/assets.md（3Dモデル/アニメ節）、design/art-bible.json（3Dスタイル方針・コンセプト画）、state/asset-routing.json（model_* / anim ルート。Primary: Meshy 直API（キー有効時）→ 第二候補: fal 経由 fal-ai/meshy/*。Meshy 直の rigging/animation が 403 の場合は当該資産種別のみ fal 経由へ切替えて必ず報告）、' + DOCS + '/assets-config.md（3Dルーティング表・生成後パイプライン3D節）、' + MANIFEST + '、state/budget.txt。\n' +
    'スタイル一貫性: コンセプト画（reference_images / character_reference）を image-to-3D の入力に使う。リグ付きは FBX / 静的は GLB。\n' +
    '生成後パイプラインのうち **Unity/UE を起動しない段まで**を実施: スキーマ検証（GLB: gltf-transform validate / FBX: Blender headless で GLB 変換して同 validate）→ ポリ数/ボーン数/非多様体検査 → authoring-time 寸法計測を MANIFEST の bbox_authoring_m に記録 → Blender headless レンダリングでプレビュー画像を ' + EP.rawAssetDir + 'previews/ に出力。**エンジン取込は行わない**（エンジンは単一インスタンスロックのため、取込・取込後バウンディングボックス再検証は Polish 前の直列区間で engineer が実施 — tech-stack 文書参照）。\n' +
    'キー無し縮退（Blender プロシージャル+Rigify / エンジン内プリミティブ）の場合は全て must_replace: true で記録。\n' +
    '対象には design/assets.md の状態が must-replace / rejected の既生成 MDL/ANM（Replan が再生成指定した資産）も含める（MANIFEST 記載済みでも状態が示す限り再生成対象）。',
    modelStories // Replan由来の3D資産story（MODEL_WORDS 判定）+ design/assets.md の状態変更が対象選定の真実
  ));
}
// assignee レーン分割（retro-e2 案A）: gameplay と ui を並走、レーン内は依存順（Replan の返却順）を維持。
// assignee 不明/不正は implementStoryWithReview 側の既定（gameplay-engineer）と一致させて gameplay レーンへ
const gameplayStories = codeStories.filter(function (s) { return s.assignee !== 'ui-engineer'; });
const uiStories = codeStories.filter(function (s) { return s.assignee === 'ui-engineer'; });
await parallel([
  // --- Build: assignee 2レーン並走（レーン内順次 + CR-CODE ループ。エンジン検証はレーン中なし — 合流後の batchVerify） ---
  async () => {
    for (const story of gameplayStories) {
      await implementStoryWithReview(story, 'Build');
    }
    log('Build gameplayレーン完了: ' + gameplayStories.length + ' story を処理');
  },
  async () => {
    for (const story of uiStories) {
      await implementStoryWithReview(story, 'Build');
    }
    log('Build uiレーン完了: ' + uiStories.length + ' story を処理');
  },
  // --- AssetGen: 画像と音声（+3Dモデル）を並走、各々 AR-ASSET ループ + 予算チェック ---
  async () => {
    await parallel(assetGenThunks);

    // 全資産生成後: バッチ一貫性チェック（style drift 検出）
    for (let pass = 1; pass <= 2; pass++) {
      const drift = await agentR(
        'AR-ASSET バッチ一貫性チェック（style drift 検出、pass ' + pass + '）。\n' +
        EP.rawAssetDir + ' の全画像資産' + (EP.assets3d ? 'と3Dモデルのレンダリングプレビュー' : '') + ' を ' + MANIFEST + ' の生成順に並べ、design/art-bible.json（palette/style_block）に照らして時系列のパレット逸脱・画風ブレ・シルエット可読性の劣化を検出せよ。' +
        '判定観点は ' + DOCS + '/gates.md の AR-ASSET 節。state/reviews/assets-batch.md に記録を追記（追記は判定者たるあなたの責務。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。\n' +
        '応答の1行目は「AR-ASSET: APPROVE|CONCERNS|REJECT」とし、構造化返却の verdict にも同じ判定を入れよ。\n' +
        'drift している資産には retryInstruction を付けよ。',
        { label: 'ar-batch-drift-' + pass, phase: 'AssetGen', agentType: 'art-reviewer', schema: ASSET_REVIEW_SCHEMA }
      );
      if (!drift) {
        unresolvedFindings.push('AssetGen: バッチ一貫性チェックが結果を返さなかった（pass ' + pass + '）');
        break;
      }
      verdictHistory.push({
        gate: 'AR-ASSET',
        artifact: 'assets-batch',
        iteration: pass,
        verdict: drift.verdict,
        findings: (drift.failedAssets || []).map(function (f) { return f.file + '（' + f.reason + '）'; })
      });
      log('AR-ASSET batch drift pass ' + pass + ': ' + drift.verdict + '（対象 ' + (drift.failedAssets || []).length + '件）');
      if (drift.verdict === 'APPROVE' || (drift.failedAssets || []).length === 0) break;
      if (pass === 2) {
        unresolvedFindings.push(
          'AssetGen: style drift が再生成後も残存: ' +
          drift.failedAssets.map(function (f) { return f.file; }).join(', ')
        );
        break;
      }
      const regen = await agentR(
        'style drift 指摘資産の再生成（art-director）。failedAssets(JSON):\n' + JSON.stringify(drift.failedAssets) + '\n' +
        'design/art-bible.json の style_block/palette へ厳密に寄せて retryInstruction 通り再生成。state/asset-routing.json のルート・予算チェック（MANIFEST合算 vs state/budget.txt）・MANIFEST追記は通常通り。\n' +
        ASSET_COMMIT_RULE,
        { label: 'regen-drift', phase: 'AssetGen', agentType: 'art-director', schema: ASSET_GEN_SCHEMA, effort: 'high' }
      );
      if (!regen || regen.budgetExceeded) {
        unresolvedFindings.push('AssetGen: drift再生成が未完（予算超過または失敗）');
        break;
      }
    }
  }
]);

// ===== バッチ検証（Build レーン合流後の直列区間 — retro-e2 案B。エンジン起動はここから直列） =====
let buildVerifyOk = true;
if (codeStories.length > 0) {
  buildVerifyOk = await batchVerify('Build',
    'ここまで Build レーン（gameplay ' + gameplayStories.length + '件 / ui ' + uiStories.length + '件）が並走でコード story を実装しており、' +
    'レーン中はエンジン検証を実行していない（' + EP.techStackDoc + '「検証」節の検証バッチ化）。全 story のコミット済みコードを一括検証・修正せよ。');
}
// 後段プロンプトへの警告注入用（integrate3d の縮退注入と同じパターン — レビュー指摘 F3）
const BUILD_VERIFY_WARN = buildVerifyOk ? '' : '【警告: Build バッチ検証が不合格のまま — state/reviews/batch-verify.md 参照。ビルドが壊れている前提で作業せよ】\n';

// ===== 3D 資産のエンジン取込（直列区間。エンジンは単一インスタンスロック — tech-stack 文書） =====
let integrate3d = null; // FullQA が縮退報告を QA プロンプトへ注入するため関数スコープ外で保持
if (EP.assets3d) {
  const INTEGRATE_SCHEMA = {
    type: 'object',
    required: ['ok', 'degradations'],
    properties: {
      ok: { type: 'boolean', description: 'エンジン取込後検証（gates.md AR-ASSET ※節）がすべて合格したか' },
      degradations: { type: 'array', items: { type: 'string' }, description: '縮退・警告の一覧（Humanoid→Generic 縮退、スケール補正、取込警告等）。無ければ空配列' },
      summary: { type: 'string' },
    },
  };
  integrate3d = await agentR(
    BUILD_VERIFY_WARN +
    '生成済み 3D 資産（MDL/ANM）を game/ に取り込め（engine: ' + engine + '。直列区間 — 他に Unity/UE を起動する処理は走っていない）。\n' +
    '読むこと: ' + MANIFEST + '（今回追記分）、design/assets.md、' + EP.techStackDoc + '「資産の取り扱い」、' + DOCS + '/gates.md の AR-ASSET ※節（エンジン取込後検証はあなたの責務）。\n' +
    (engine === 'unity'
      ? '手順: ' + EP.rawAssetDir + ' の合格資産を game/Assets/Resources/Generated/{models,textures,audio}/ にコピーして Unity にインポートさせ（Resources.Load 方式 — contract §11 / tech-stack-unity.md「資産の取り扱い」。AssetKeys の値は Resources 相対パス）、リグ付き FBX は ModelImporter の animationType を設定して Avatar 生成を確認（Avatar.isValid を機械確認。失敗は Generic へ縮退し degradations に必ず含める）。取込後バウンディングボックスでスケール検証。プレースホルダを実資産に差し替え、資産定数（' + EP.configPath + '）に登録。\n'
      : '手順: Interchange（Python: unreal.InterchangeManager）で ' + EP.rawAssetDir + ' の合格資産を game/Content/Generated/ にインポートし、リグ付きはリターゲット成功を確認（失敗は degradations に必ず含める）。取込後バウンディングボックスでスケール検証（1 unit = 1cm）。資産定数（' + EP.configPath + '）に登録。\n') +
    '検証結果は ' + MANIFEST + ' の validator にも記録。検証: ' + EP.verifyCmd + ' が exit 0。\n' +
    'コミット規律（取込専用）: 触ったパスのみ明示して git add（' + (engine === 'unity' ? '例: git add game/Assets game/_generated state' : '例: git add game/Content game/_generated game/Source game/Config state') + '。git add -A 禁止 — MANIFEST の validator 追記と取込資産を漏らさない）。' + GIT_RETRY_NOTE + '\n' +
    '構造化返却: ok / degradations / summary。',
    { label: 'integrate-3d-assets', phase: 'AssetGen', agentType: 'gameplay-engineer', effort: 'high', schema: INTEGRATE_SCHEMA }
  );
  if (integrate3d === null) {
    unresolvedFindings.push('AssetGen(3D): エンジン取込 agent が結果を返さなかった（未取込の可能性）');
  } else {
    for (const d of (integrate3d.degradations || [])) {
      unresolvedFindings.push('AssetGen(3D)[Integrate] ' + d);
    }
    if (integrate3d.ok === false) {
      unresolvedFindings.push('AssetGen(3D)[Integrate] エンジン取込後検証が不合格（詳細は degradations と ' + MANIFEST + ' の validator）');
    }
  }
}

// ===== Phase: Polish =====
phase('Polish');
const polishPlan = await agentR(
  BUILD_VERIFY_WARN +
  'Phase 3 Polish 計画（game-designer）。\n' +
  '読むこと: ' + EP.configPath + '、design/gdd.md（数値の初期値＋調整レンジ）、design/concept.md（ピラー P-xx）、' + feedbackPath + '、state/stories.yaml、' + DOCS + '/contract.md（§7 スキーマ・§8 ID・§11 エンジン）。\n' +
  '手順:\n' +
  '1) バランス確認: ' + EP.configPath + ' の現在値を gdd の意図・レンジと実装済みゲームに照らして点検。調整は ' + EP.configPath + ' の値変更**だけ**で完結する具体的な指定（定数名→新値と理由）にする\n' +
  '2) juice/手触り polish story を起案: 画面シェイク・ヒットストップ・音の手応え・tween/イージング等。**ピラー P-xx に寄与するもののみ**（寄与を story の pillar で明示）。各storyに実操作で検証可能な acceptance\n' +
  '3) state/stories.yaml に続番IDで追加（phase: build、status: todo、assignee は gameplay-engineer / ui-engineer）。バランス調整 story は**変更対象の定数名を acceptance に明示**し、同一定数を複数 story・複数 assignee に割り当てない（並走レーンの共有ファイル競合を避ける — 実装側 LANE_RULE の値変更例外はこの明示が条件）\n' +
  '返却: 追加した polish story 一覧（実装すべき順。バランス調整もstory化して含める）。',
  { label: 'polish-plan', phase: 'Polish', agentType: 'game-designer', schema: STORY_LIST_SCHEMA, effort: 'high' }
);

const polishStories = (polishPlan && Array.isArray(polishPlan.stories) ? polishPlan.stories : [])
  .filter(function (s) { return ENGINEERS.indexOf(s.assignee) >= 0; });
if (polishPlan === null) {
  unresolvedFindings.push('Polish: game-designer が計画を返さなかった（polish未実施）');
}
log('Polish story ' + polishStories.length + '件を実装する（assignee レーン並走）');
// Build バッチ検証不合格のまま Polish に入る場合、レーン実装 agent にも警告を届ける（adversarial L-11 —
// 「他レーン WIP 由来のエラーは無視してよい」の指示が既存破損を覆い隠さないように）
laneContextWarn = BUILD_VERIFY_WARN;
const polishGameplay = polishStories.filter(function (s) { return s.assignee !== 'ui-engineer'; });
const polishUi = polishStories.filter(function (s) { return s.assignee === 'ui-engineer'; });
await parallel([
  async () => {
    for (const story of polishGameplay) {
      await implementStoryWithReview(story, 'Polish');
    }
  },
  async () => {
    for (const story of polishUi) {
      await implementStoryWithReview(story, 'Polish');
    }
  }
]);
// Polish レーン合流後のバッチ検証（この後の FullQA が最初のエンジン起動にならないようにする）
let polishVerifyOk = true;
if (polishStories.length > 0) {
  polishVerifyOk = await batchVerify('Polish',
    'ここまで Polish レーン（gameplay ' + polishGameplay.length + '件 / ui ' + polishUi.length + '件）が並走で polish story を実装しており、' +
    'レーン中はエンジン検証を実行していない。全 polish story のコミット済みコードを一括検証・修正せよ。');
}
const QA_VERIFY_WARN = (buildVerifyOk && polishVerifyOk) ? '' :
  '【警告: バッチ検証（' + (!buildVerifyOk ? 'Build' : '') + (!buildVerifyOk && !polishVerifyOk ? '・' : '') + (!polishVerifyOk ? 'Polish' : '') + '）が不合格のまま — state/reviews/batch-verify.md の診断を先に読んでから QA せよ】\n';

// ===== Phase: FullQA =====
phase('FullQA');
let audit = null;
let qaVerdict = null;
let qaBugs = [];
let qaFailedAcceptance = [];
let qaSummary = '';

await parallel([
  // 資産監査（MANIFESTコスト合算・予算比較・ライセンスフラグ抽出）
  // 書き先は state/reviews/assets-audit.md（qa/report.md は並走する QA-PLAY の qa-lead 記録専有）
  async () => {
    audit = await agentR(
      '資産監査（qa-lead）。読むこと: ' + MANIFEST + '、state/budget.txt、' + DOCS + '/assets-config.md（「ハード禁止事項」「Checkpointで人間に提示するライセンスフラグ」節）。\n' +
      '1) MANIFEST の cost_usd を合算し totalAssetCost、state/budget.txt を budgetUsd として比較（overBudget）\n' +
      '2) ライセンスフラグ抽出: assets-config.md の提示項目（ElevenLabs Studio Games条項 / Ideogram AI生成表記条項 / 純AI出力の著作権不確定性と人間関与記録の有無）に加え、MANIFEST 内の license が commercial-ok 以外・must_replace: true・placeholder-nc の残存を列挙\n' +
      '3) スプライトの白背景PNG混入を**全数**機械検査する（MANIFEST 記載の全画像資産に対し ImageMagick/Pillow でアルファチャンネル有無と不透明背景を検査。抜き取りではなく全数 — 検査は軽量。違反は mustReplaceAssets 相当としてフラグに含める）\n' +
      '3b) 3D 資産（MDL/ANM）の MANIFEST 必須フィールド（plan_tier / bbox_authoring_m / validator / license）の記録漏れと、shippable:false ルート由来・cost_estimated:true の資産を列挙する（構造化返却の provenanceGaps に入れる）\n' +
      '結果を state/reviews/assets-audit.md に追記せよ（qa/report.md には書かない — 並走する QA-PLAY の記録専有のため。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。',
      { label: 'asset-audit', phase: 'FullQA', agentType: 'qa-lead', schema: AUDIT_SCHEMA }
    );
    if (audit === null) {
      unresolvedFindings.push('FullQA: 資産監査が結果を返さなかった（コスト/ライセンス未確認）');
    } else if (audit.overBudget) {
      unresolvedFindings.push('FullQA: 資産コストが予算超過（$' + audit.totalAssetCost + ' / 予算 $' + audit.budgetUsd + '）');
    }
    if (audit && Array.isArray(audit.mustReplaceAssets) && audit.mustReplaceAssets.length > 0) {
      unresolvedFindings.push('FullQA: must_replace 資産が残存: ' + audit.mustReplaceAssets.join(', '));
    }
    if (audit && Array.isArray(audit.provenanceGaps) && audit.provenanceGaps.length > 0) {
      for (const g of audit.provenanceGaps) {
        unresolvedFindings.push('FullQA[provenance] ' + g);
      }
    }
  },
  // QA-PLAY（review-loops.md: MAX_ITER 2 = QA1→修正→QA2→非APPROVEならエスカレーション）
  async () => {
    for (let round = 1; round <= 2; round++) {
      const qa = await agentR(
        QA_VERIFY_WARN +
        'フルQA（qa-lead、round ' + round + '/2）。' + DOCS + '/gates.md の QA-PLAY 節（engine=' + engine + ' の実行手段）に従い、' + EP.qaTarget + '。\n' +
        (integrate3d && (integrate3d.degradations || []).length > 0
          ? '【Integrate からの縮退報告あり — 該当箇所は重点検証（特にリグ縮退時はアニメ再生の目視確認必須）】: ' + integrate3d.degradations.join(' / ') + '\n'
          : '') +
        '範囲: state/stories.yaml の**全story**（phase: prototype / build の両方）の acceptance を1つずつ実操作で回帰検証。\n' +
        '加えて: build成功と consoleエラー0 / コアループ1周（開始→挑戦→結果→リスタート）/ 必須シーン遷移 Title→Menu→Game→Result→Menu の1周（contract §11。Menu の必須要素 = プレイ開始・アウトゲーム表示・設定・終了導線 の実在込み — gates.md QA-PLAY 観点2。Title/Menu/Game/Result 各画面のスクリーンショットを撮る（Game は開始直後の空盤面不可 — コアループの主要オブジェクトが写るフレームで撮る。gates.md 視覚証跡））/ メタ進行の永続化（gates.md 観点5: 保存→再起動相当→復元一致、破損セーブ→.bak 退避＋[SaveCorruption] 明示エラー1回＋既定値復旧）/ 実プレイ感が design/concept.md のピラー P-xx を裏切っていないか。\n' +
        '視覚証跡の機械検知＋目視（gates.md 視覚証跡の目視義務）: 全スクリーンショットに magick の mean 検査（<0.02 / >0.98 = SUSPECT_BLANK → 撮影方式を切替えて再撮影）を行い、必ず Read で目視して「何が写っているか」を qa/report.md の目視所見表に記録。\n' +
        '証跡を qa/evidence/ に保存し、qa/report.md に結果を書け（round ' + round + ' として追記）。state/reviews/qa.md に iteration 記録を追記（追記は判定者たるあなたの責務。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。\n' +
        '応答の1行目は「QA-PLAY: APPROVE|CONCERNS|REJECT」とし、構造化返却の verdict にも同じ判定を入れよ。\n' +
        'バグは severity（blocker/major/minor）と担当（gameplay-engineer/ui-engineer）付きで返せ。failedAcceptance には不合格の story ID を全て入れ、非APPROVE時は summary に判定理由を書け。evidencePaths に保存した証跡パス、screenshotsVisuallyConfirmed に目視実施の有無（未実施なら false を正直に）を入れよ。\n' +
        '判定: 重大バグ0かつacceptance全通過のみ APPROVE。',
        { label: 'qa-play-' + round, phase: 'FullQA', agentType: 'qa-lead', schema: QA_SCHEMA, effort: 'high' }
      );
      if (qa === null) {
        unresolvedFindings.push('FullQA: qa-lead が round ' + round + ' で結果を返さなかった');
        break;
      }

      // 証跡実在＋目視宣言の独立機械検証（qa-lead の自己申告を workflow が別 agent で確認）
      {
        const evCheck = await agentR(
          [
            '読み取り専用の検証タスク。以下の証跡パス一覧について、各ファイルの実在と非0サイズを Bash（`test -s`・`stat`）で機械検証せよ。ファイルの作成・変更・削除は禁止。',
            '証跡パス(JSON): ' + JSON.stringify(qa.evidencePaths || []),
            '加えて qa/evidence/ 直下の実ファイル一覧を ls で確認し extraFilesInEvidenceDir に返せ。',
          ].join('\n'),
          { label: 'verify-evidence-' + round, phase: 'FullQA', effort: 'low', schema: EVIDENCE_CHECK_SCHEMA }
        );
        const missing = [];
        if (!evCheck) {
          missing.push('証跡検証 agent が結果を返さなかった');
        } else {
          // 網羅性突合: evidencePaths の各パスが checks に exists && nonEmpty で現れることを要求
          // （検証 agent が checks:[] や部分回答を返した場合に合格擬装させない）
          const byPath = {};
          for (const c of (evCheck.checks || [])) byPath[c.path] = c;
          for (const p of (qa.evidencePaths || [])) {
            const c = byPath[p];
            if (!c) missing.push(p + '（検証結果に現れず — 未検証）');
            else if (!c.exists || !c.nonEmpty) missing.push(p + '（' + (!c.exists ? '不存在' : '0バイト') + '）');
          }
        }
        if ((qa.evidencePaths || []).length === 0) missing.push('evidencePaths が空（証跡なしの判定は無効 — qa-lead.md）');
        if (qa.screenshotsVisuallyConfirmed !== true) missing.push('スクリーンショットの Read 目視が未実施（screenshotsVisuallyConfirmed=false）');
        if (missing.length > 0) {
          if (qa.verdict === 'APPROVE') {
            qa.verdict = 'CONCERNS';
            log('FullQA round ' + round + ': 証跡/目視の機械検証不合格 → APPROVE を CONCERNS に降格');
          }
          unresolvedFindings.push('FullQA round ' + round + ' 証跡/目視の機械検証不合格: ' + missing.join(' / '));
        }
      }

      qaVerdict = qa.verdict;
      qaBugs = qa.bugs || [];
      qaFailedAcceptance = Array.isArray(qa.failedAcceptance) ? qa.failedAcceptance : [];
      qaSummary = qa.summary || '';
      verdictHistory.push({
        gate: 'QA-PLAY',
        artifact: 'qa',
        iteration: round,
        verdict: qa.verdict,
        findings: qaBugs.map(function (b) { return '[' + b.severity + '] ' + b.summary; })
          .concat(qaFailedAcceptance.map(function (id) { return 'acceptance不合格: ' + id; }))
      });
      log('QA-PLAY round ' + round + ': ' + qa.verdict + '（バグ ' + qaBugs.length + '件 / acceptance不合格 ' + qaFailedAcceptance.length + '件）');
      if (qa.verdict === 'APPROVE') break; // 合格は verdict === APPROVE のみ（バグ件数での合格ショートカット禁止）
      if (round === 2) break; // review 2回上限到達 → エスカレーション

      // 修正はコード規律に合わせて順次（同一ファイル競合を避ける）
      const order = ['gameplay-engineer', 'ui-engineer'];
      for (const eng of order) {
        const mine = qaBugs.filter(function (b) { return (b.assignee || 'gameplay-engineer') === eng; });
        const myAcceptance = qaFailedAcceptance;
        if (mine.length === 0 && myAcceptance.length === 0) continue;
        await agentR(
          'QA-PLAY round ' + round + ' で検出された問題を修正せよ（QA-PLAY は review 2回上限。修正後 round ' + (round + 1) + ' で再判定される）。\n' +
          'bugs(JSON):\n' + JSON.stringify(mine) + '\n' +
          '不合格acceptance(story ID): ' + JSON.stringify(myAcceptance) + '（自分の担当分のみ対応。担当外は触らない）\n' +
          '参照: qa/report.md（再現手順・証跡）、state/stories.yaml（該当acceptance）、' + EP.techStackDoc + '（規約: チューニングは ' + EP.configPath + ' のみで）。\n' +
          '修正後 ' + EP.verifyCmd + ' を exit 0 にし、修正内容を qa/report.md の該当バグに追記せよ。修正原因がエンジン/テストランナー起因の一般則（環境の落とし穴）だった場合は、tech-stack 文書の「既知の落とし穴」節へ即時追記せよ（無ければ新設 — gates.md QA-PLAY）。\n' +
          'git commit -m "QA-PLAY round ' + round + ' fix (' + eng + ')" すること。' + CODE_COMMIT_RULE,
          { label: 'qa-fix-' + round + '-' + eng, phase: 'FullQA', agentType: eng, effort: 'high' }
        );
      }
    }
    if (qaVerdict !== 'APPROVE') {
      unresolvedFindings.push(
        'FullQA: QA-PLAY が上限（review 2回）到達でも非APPROVE（' + (qaVerdict || '判定なし') + '）。' +
        (qaSummary ? ' 理由: ' + qaSummary + '。' : '') +
        (qaFailedAcceptance.length ? ' 不合格acceptance: ' + qaFailedAcceptance.join(', ') + '。' : '') +
        ' 残バグ: ' + (qaBugs.length ? qaBugs.map(function (b) { return '[' + b.severity + '] ' + b.summary; }).join(' / ') : 'qa/report.md 参照')
      );
    }
  }
]);

// ===== Phase: Final =====
phase('Final');
let cd = null;
for (let attempt = 1; attempt <= 2; attempt++) {
  cd = await agentR(
    'CD-CHECKPOINT 最終判定（creative-director、attempt ' + attempt + '/2）。' + DOCS + '/gates.md の CD-CHECKPOINT 節に従え。\n' +
    '対象: Checkpoint C（完成品受け渡し）の提示物一式。\n' +
    '読むこと: design/brief.md、design/concept.md（ピラー）、design/gdd.md、qa/report.md、qa/evidence/、state/stories.yaml、' + MANIFEST + '、state/reviews/ 配下。\n' +
    '未解決事項(JSON・正直に全て開示すること。**[BLOCKER] 始まりの項目と縮退（Humanoid→Generic / プレースホルダ / Fallback / shippable:false）は summary の冒頭で個別に警告し、箇条書きに埋没させない** — gates.md CD-CHECKPOINT 観点3): ' + JSON.stringify(unresolvedFindings) + '\n' +
    '資産監査(JSON): ' + JSON.stringify(audit) + '\n' +
    '返すもの:\n' +
    '- verdict（APPROVE/CONCERNS/REJECT。REJECTは人間に見せる前に直すべき点を mustFix に）\n' +
    '- summary: 人間が5分で判断できる要約（何を作ったか / 何を判断してほしいか / 既知の課題・妥協点を隠さず列挙。[BLOCKER]・縮退は冒頭で個別警告）\n' +
    '- playInstructions: 起動手順（' + EP.playInstructions + '）と操作方法・見どころ\n' +
    '応答の1行目は「CD-CHECKPOINT: APPROVE|CONCERNS|REJECT」とし、構造化返却の verdict にも同じ判定を入れよ。\n' +
    '判定を state/reviews/checkpoint-c.md に ' + DOCS + '/review-loops.md の形式で追記せよ（追記は判定者たるあなたの責務。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。',
    { label: 'cd-checkpoint-' + attempt, phase: 'Final', agentType: 'creative-director', schema: CD_SCHEMA, effort: 'high' }
  );
  if (cd === null) {
    unresolvedFindings.push('Final: creative-director が CD-CHECKPOINT 判定を返さなかった');
    break;
  }
  verdictHistory.push({
    gate: 'CD-CHECKPOINT',
    artifact: 'checkpoint-c',
    iteration: attempt,
    verdict: cd.verdict,
    findings: cd.mustFix || []
  });
  log('CD-CHECKPOINT attempt ' + attempt + ': ' + cd.verdict);
  if (cd.verdict !== 'REJECT') break;
  if (attempt === 2) {
    unresolvedFindings.push('Final: CD-CHECKPOINT が再判定でも REJECT。mustFix: ' + (cd.mustFix || []).join(' / '));
    break;
  }
  await agentR(
    'CD-CHECKPOINT が REJECT。人間に見せる前に以下を修正せよ（review-loops.md: 修正後1回だけ再判定される）。mustFix(JSON):\n' + JSON.stringify(cd.mustFix || []) + '\n' +
    '提示物（要約・qa/report.md・成果物の整合）を直し、コード修正が必要なら該当engineerの規約（' + EP.techStackDoc + '）に従って最小限で行い typecheck/build 相当（' + EP.verifyCmd + '）を通せ。\n' +
    '変更した場合は git commit すること。' + CODE_COMMIT_RULE,
    { label: 'cd-fix', phase: 'Final', agentType: 'tech-director', effort: 'high' }
  );
}

// 状態の確定（state/stage.txt には触れない — stage 遷移は /forge-build スキルの責務）
await agentR(
  'Phase 3 終了処理（tech-director）。\n' +
  'state/active.md を更新: 現在地=Checkpoint C 提示待ち / 次アクション=人間の受領判断（review-mode: ' + reviewMode + '）/ 未解決事項(JSON): ' + JSON.stringify(unresolvedFindings) + '\n' +
  '日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う（推測記入禁止）。\n' +
  '注意: state/stage.txt は更新しない（stage 遷移は /forge-build スキルが行う）。',
  { label: 'finalize-state', phase: 'Final', agentType: 'tech-director', effort: 'low' }
);

// ---- 戻り値（Checkpoint C 素材。人間提示はスキル側の責務）----------------
return {
  summary: cd && cd.summary
    ? cd.summary
    : 'CD-CHECKPOINT の要約が得られなかった。qa/report.md と state/reviews/ を直接確認のこと。',
  playInstructions: cd && cd.playInstructions
    ? cd.playInstructions
    : EP.playInstructions + ' で起動。操作は design/gdd.md を参照。',
  qaReportPath: 'qa/report.md',
  totalAssetCost: audit && typeof audit.totalAssetCost === 'number' ? audit.totalAssetCost : null,
  licenseFlags: audit && Array.isArray(audit.licenseFlags) ? audit.licenseFlags : [],
  unresolvedFindings: unresolvedFindings,
  verdictHistory: verdictHistory,
  verdict: cd && cd.verdict ? cd.verdict : 'CONCERNS'
};
