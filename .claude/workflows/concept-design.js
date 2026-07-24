// ArcadeRelay Phase 1: brief → 企画・設計一式（concept / gdd / art-bible / assets）
// 起動元: /forge-concept（contract.md §4）。args = {briefPath, reviewMode, engine?}（engine は contract §11 の3値。省略時 phaser）
// レビューループは .claude/docs/review-loops.md の対応表・MAX_ITER に厳密準拠。
// ゲート判定プロンプトは .claude/docs/gates.md をIDで参照（本文コピー禁止＝ドリフト防止）。

export const meta = {
  name: 'concept-design',
  description: 'Phase 1: brief から企画・設計一式を produce→review→revise ループで自律生成し、Checkpoint A 素材を返す',
  phases: [
    { title: 'Concept', detail: 'game-designer が design/concept.md を起草 → DR-CONCEPT レビューループ（最大3回）' },
    { title: 'GDD', detail: 'game-designer が design/gdd.md を起草 → DR-GDD レビューループ（最大3回）' },
    { title: 'ArtBible', detail: 'key image 候補4枚生成 → art-reviewer ランク付け → art-bible.md/.json 起草 → AR-BIBLE レビューループ（最大3回）' },
    { title: 'Assets', detail: 'art-director が design/assets.md の骨格＋画像セクションを先行作成 → audio-designer が音声セクションを追記（直列2段）' },
    { title: 'Final', detail: 'creative-director が CD-CHECKPOINT 判定。REJECT なら指示に従い1回だけ修正して再判定' }
  ]
};

// ---------------------------------------------------------------------------
// スキーマ定義
// ---------------------------------------------------------------------------

const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
    findings: {
      type: 'array',
      items: { type: 'string' },
      description: '優先度順の具体的指摘。APPROVE の場合は空配列'
    }
  },
  required: ['verdict', 'findings']
};

const KEY_IMAGE_SCHEMA = {
  type: 'object',
  properties: {
    candidates: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          path: { type: 'string', description: 'design/refs/ 配下の候補ファイルパス（リポジトリ相対）' },
          kind: { type: 'string', enum: ['image', 'style-description'] },
          note: { type: 'string', description: 'スタイル方向性の一言メモ' }
        },
        required: ['path', 'kind']
      }
    },
    degraded: { type: 'boolean', description: '画像生成キー無しでローカル縮退（スタイル記述のみ）になったか' }
  },
  required: ['candidates', 'degraded']
};

const RANK_SCHEMA = {
  type: 'object',
  properties: {
    ranking: {
      type: 'array',
      items: { type: 'string' },
      description: '候補ファイルパスを良い順に並べたもの'
    },
    rationale: { type: 'string', description: 'ランク付けの根拠（1位の採用理由を含む）' }
  },
  required: ['ranking', 'rationale']
};

const CD_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['APPROVE', 'CONCERNS', 'REJECT'] },
    findings: {
      type: 'array',
      items: { type: 'string' },
      description: '人間に提示すべき懸念・既知の課題（CONCERNS/REJECT 時）'
    },
    fixes: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          assignee: { type: 'string', enum: ['game-designer', 'art-director', 'audio-designer'] },
          artifact: { type: 'string', description: '修正対象の成果物パス（contract.md §6 のパスのみ）' },
          instruction: { type: 'string', description: '具体的な修正指示' }
        },
        required: ['assignee', 'artifact', 'instruction']
      },
      description: 'REJECT 時の修正指示リスト。APPROVE/CONCERNS では空配列'
    }
  },
  required: ['verdict', 'findings', 'fixes']
};

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
// reviewLoop ヘルパー（review-loops.md の共通形を実装）
// producer作業 → reviewer判定（state/reviews/<artifact>.md への追記は reviewer agent の責務）→
// 非APPROVE なら producer に revise 指示（最終 iteration でも revise してからエスカレーション）→
// 最大 maxIter 回 → 未解決指摘は unresolved に蓄積（パイプラインは止めない）
// 全 verdict は verdictHistory に蓄積し戻り値へ含める（review-mode=full の完了後提示素材）
// ---------------------------------------------------------------------------

async function reviewLoop(opts) {
  const {
    gateId,          // 'DR-CONCEPT' 等（contract.md §5）
    reviewArtifact,  // state/reviews/<artifact>.md の <artifact>（例: 'concept'）
    artifactPaths,   // レビュー対象ファイルパスの配列
    producerType,    // revise を行う agent 名（contract.md §2）
    reviewerType,    // 判定を行う agent 名（contract.md §2）
    phaseTitle,      // agent opts の phase ラベル
    maxIter,         // review-loops.md の MAX_ITER
    producerContextPaths, // revise 時に producer が参照すべきファイルパス
    unresolved,      // 未解決指摘の蓄積先（呼び出し元の配列）
    verdictHistory   // 全 verdict の蓄積先（呼び出し元の配列）
  } = opts;

  const reviewFile = 'state/reviews/' + reviewArtifact + '.md';
  let finalVerdict = 'REJECT';

  for (let i = 1; i <= maxIter; i++) {
    const review = await agentR(
      [
        'あなたは Gate ' + gateId + ' の判定者である。',
        '1. .claude/docs/gates.md の「' + gateId + '」セクションを読み、その観点で対象を批評せよ。',
        '2. 対象成果物: ' + artifactPaths.join(' / ') + '（各ファイルを自分で読むこと。関連コンテキスト: ' + producerContextPaths.join(' / ') + '）。',
        '3. 判定を ' + reviewFile + ' に .claude/docs/review-loops.md の追記形式で必ず追記せよ（## ' + gateId + ' iteration ' + i + ' — <verdict>、日時ISO8601、指摘要約。「対応:」欄は空で残す。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。',
        '4. 応答の1行目は「' + gateId + ': APPROVE|CONCERNS|REJECT」とし、構造化返却の verdict / findings にも同じ判定と指摘を入れよ。',
        'findings は優先度順・修正可能な具体指摘のみ。APPROVE なら空配列。'
      ].join('\n'),
      { agentType: reviewerType, label: gateId + ' review #' + i, phase: phaseTitle, schema: REVIEW_SCHEMA }
    );

    if (!review) {
      log(gateId + ' iteration ' + i + ': reviewer が応答せず（skip/error）。判定不能として続行');
      unresolved.push(gateId + ': iteration ' + i + ' で reviewer が応答せず判定不能');
      break;
    }

    finalVerdict = review.verdict;
    const count = review.findings ? review.findings.length : 0;
    verdictHistory.push({
      gate: gateId,
      artifact: reviewArtifact,
      iteration: i,
      verdict: review.verdict,
      findings: review.findings || []
    });
    log(gateId + ' iteration ' + i + ': ' + review.verdict + (count ? '（指摘 ' + count + ' 件）' : ''));

    if (review.verdict === 'APPROVE') {
      return { verdict: 'APPROVE', iterations: i };
    }

    // 非APPROVE は最終 iteration でも revise を1回実行してからエスカレーション（review-loops.md の共通形）
    const revised = await agentR(
      [
        'Gate ' + gateId + ' の判定が ' + review.verdict + ' だった。成果物を revise せよ。',
        '1. ' + reviewFile + ' を読み、最新の「## ' + gateId + ' iteration ' + i + '」の指摘を確認せよ。',
        '2. 対象: ' + artifactPaths.join(' / ') + ' を修正せよ。参照コンテキスト: ' + producerContextPaths.join(' / ') + '。',
        '3. 各指摘への対応/見送り＋理由を ' + reviewFile + ' の該当 iteration の「対応:」欄に追記せよ（黙殺禁止）。'
      ].join('\n'),
      { agentType: producerType, label: gateId + ' revise #' + i, phase: phaseTitle, effort: 'high' }
    );

    if (!revised) {
      log(gateId + ' iteration ' + i + ': revise が実行されず（producer skip/error）');
      unresolved.push(gateId + ': iteration ' + i + ' の revise が実行されず（producer skip/error）');
      break;
    }

    if (i === maxIter) {
      const fs = review.findings || [];
      for (const f of fs) unresolved.push(gateId + ': ' + f);
      if (fs.length === 0) unresolved.push(gateId + ': MAX_ITER 到達（' + review.verdict + '、指摘詳細は ' + reviewFile + ' 参照）');
      log(gateId + ': MAX_ITER=' + maxIter + ' 到達。最終 revise は実施済み（再判定なし）・未解決指摘を Checkpoint A へエスカレーション（パイプラインは継続）');
    }
  }

  return { verdict: finalVerdict, iterations: maxIter };
}

// ---------------------------------------------------------------------------
// 本体（Workflowランナーはトップレベルを実行する。default export は使わない）
// ---------------------------------------------------------------------------

// args 正規化: 起動側/ランナーが JSON 文字列で渡してくるケースの防御（E2 で実測。
// パース不能な文字列は明示エラーに倒す — 黙って既定値に落とさない）
const ARGS = (typeof args === 'string') ? JSON.parse(args) : (args || {});
const briefPath = ARGS.briefPath;
const reviewMode = ARGS.reviewMode || 'lean';
if (!briefPath) throw new Error('args.briefPath が必要（通常 design/brief.md）');

// エンジンプロファイル（contract.md §11。値は各 tech-stack 文書と一致させること）
// engine 未指定のみ phaser 既定。空文字・不正値は下の throw に倒す（無言フォールバック禁止）
const engine = (ARGS.engine !== undefined && ARGS.engine !== null) ? ARGS.engine : 'phaser';
const ENGINE_PROFILES = {
  phaser: {
    stack: 'Phaser 3 + TS',
    techStackDoc: '.claude/docs/tech-stack.md',
    assets3d: false
  },
  unity: {
    stack: 'Unity 6 LTS + C#（URP・3D）',
    techStackDoc: '.claude/docs/tech-stack-unity.md',
    assets3d: true
  },
  unreal: {
    stack: 'Unreal Engine 5.x + C++（3D）',
    techStackDoc: '.claude/docs/tech-stack-unreal.md',
    assets3d: true
  }
};
const EP = ENGINE_PROFILES[engine];
if (!EP) throw new Error('args.engine が不正: ' + engine + '（contract §11: phaser|unity|unreal）');

log('concept-design 開始: brief=' + briefPath + ' / engine=' + engine + ' / review-mode=' + reviewMode +
  '（全 verdict は verdictHistory に蓄積して返す。full ではスキルが完了後に全件を人間へ提示する）');

const unresolved = [];
const verdictHistory = [];

// ---- Phase 1: Concept -------------------------------------------------
phase('Concept');

const conceptDraft = await agentR(
  [
    'design/concept.md を起草せよ。',
    '1. brief: ' + briefPath + ' を読む。',
    '2. テンプレート .claude/docs/templates/concept.md が存在すればその構成に厳密に従う。',
    '3. ピラー P-01〜 を3〜5個定義する（contract.md §8。互いに独立・意思決定の裁定に使える具体性を持たせる）。',
    '4. 面白さの仮説（1文・反証可能）、コアループ（30秒で説明可能・1画面で成立）、スコープ（数時間の自律実装で到達可能）を含める。',
    '5. .claude/docs/gates.md の DR-CONCEPT 観点を先回りして満たすこと。'
  ].join('\n'),
  { agentType: 'game-designer', label: 'concept 起草', phase: 'Concept', effort: 'high' }
);
if (!conceptDraft) throw new Error('design/concept.md の起草に失敗（game-designer が応答せず）');

await reviewLoop({
  gateId: 'DR-CONCEPT',
  reviewArtifact: 'concept',
  artifactPaths: ['design/concept.md'],
  producerType: 'game-designer',
  reviewerType: 'design-reviewer',
  phaseTitle: 'Concept',
  maxIter: 3,
  producerContextPaths: [briefPath],
  unresolved,
  verdictHistory
});

// ---- Phase 2: GDD ------------------------------------------------------
phase('GDD');

const gddDraft = await agentR(
  [
    'design/gdd.md を起草せよ。',
    '1. ' + briefPath + ' と design/concept.md を読む。',
    '2. テンプレート .claude/docs/templates/gdd.md が存在すればその構成に厳密に従う。',
    '3. 全システムが concept.md のピラー P-xx を参照すること。数値は初期値＋調整レンジで書く（「後で決める」禁止）。',
    '4. 勝利/敗北条件・リスタート・ゲームフロー（必須シーン集合 Boot→Title→Menu→Game→Result→{Game|Menu} — contract §11。Menu の必須要素込み）を定義。各システムは ' + EP.stack + ' で数時間で実装できる粒度に分解。',
    '5. 「メタ進行（アウトゲーム）」節を必ず埋める（templates/gdd.md: ハイスコア/ベストタイム+統計=必須、brief の「アウトゲーム / やり込み」志向に沿って選択要素を2つ以上採用。各要素に P-xx・初期値+調整レンジ・ACH/UNL/UPG の安定ID・セーブ対象キーと初回起動時の初期状態）。',
    '6. .claude/docs/gates.md の DR-GDD 観点（6観点）を先回りして満たすこと。技術前提は ' + EP.techStackDoc + ' に整合させる。'
  ].join('\n'),
  { agentType: 'game-designer', label: 'gdd 起草', phase: 'GDD', effort: 'high' }
);
if (!gddDraft) throw new Error('design/gdd.md の起草に失敗（game-designer が応答せず）');

await reviewLoop({
  gateId: 'DR-GDD',
  reviewArtifact: 'gdd',
  artifactPaths: ['design/gdd.md'],
  producerType: 'game-designer',
  reviewerType: 'design-reviewer',
  phaseTitle: 'GDD',
  maxIter: 3,
  producerContextPaths: [briefPath, 'design/concept.md'],
  unresolved,
  verdictHistory
});

// ---- Phase 3: ArtBible -------------------------------------------------
phase('ArtBible');

const keyGen = await agentR(
  [
    'key image 候補を4枚生成せよ。',
    '1. ' + briefPath + ' / design/concept.md / design/gdd.md を読み、ゲームのトーンとピラー P-xx を掴む。',
    '2. state/asset-routing.json を読み、そこに記載のプロバイダルートで生成する（生成中のルート再判定禁止。.claude/docs/assets-config.md 準拠）。予算は state/budget.txt を尊重し、超過見込みなら生成を止めて縮退する。',
    '3. 4枚は互いにスタイル方向性を変える（例: パレット・画風・密度）。生成物は design/refs/key-image-candidate-1.png 〜 key-image-candidate-4.png に保存。',
    '4. ルーティングがローカル縮退（画像生成キー無し）の場合は、画像の代わりに design/refs/key-image-candidate-1.md 〜 -4.md にスタイル記述（style block 案・hexパレット・参照語彙）を書き、プレースホルダ方針（Checkpoint A 以降に実画像へ差し替える前提・must_replace）を各ファイルに明記せよ。',
    '5. 構造化返却: candidates（path / kind / note）と degraded（縮退したか）。'
  ].join('\n'),
  { agentType: 'art-director', label: 'key image 候補生成', phase: 'ArtBible', schema: KEY_IMAGE_SCHEMA, effort: 'high' }
);

let keyImageCandidates = [];
if (keyGen && keyGen.candidates) {
  keyImageCandidates = keyGen.candidates;
  if (keyGen.degraded) log('key image 生成はローカル縮退（スタイル記述のみ・プレースホルダ方針）');
} else {
  log('key image 候補生成に失敗（art-director が応答せず）。スタイル記述ベースで続行');
  unresolved.push('AR-BIBLE: key image 候補の生成が実行されず。art-bible はスタイル記述のみで起草');
}

const candidatePaths = keyImageCandidates.map(function (c) { return c.path; });

let topCandidate = null;
if (candidatePaths.length > 0) {
  const rank = await agentR(
    [
      'key image 候補をランク付けせよ。',
      '候補: ' + candidatePaths.join(' / ') + '（各ファイルを自分で読む/見ること）。',
      '観点: .claude/docs/gates.md の AR-BIBLE の 2（ゲーム内可読性）と 3（生成再現性）を流用し、design/concept.md のピラー P-xx との整合も見る。',
      '結果を state/reviews/art-bible.md に「key image ランク付け」の見出しで追記し（順位・根拠・日時ISO8601。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）、構造化返却の ranking に良い順で path を入れよ。'
    ].join('\n'),
    { agentType: 'art-reviewer', label: 'key image ランク付け', phase: 'ArtBible', schema: RANK_SCHEMA }
  );
  if (rank && rank.ranking && rank.ranking.length > 0) {
    topCandidate = rank.ranking[0];
    log('key image 上位候補: ' + topCandidate);
  } else {
    topCandidate = candidatePaths[0];
    log('ランク付けに失敗。候補1（' + topCandidate + '）を暫定基準として続行');
    unresolved.push('AR-BIBLE: key image ランク付けが実行されず。候補1を暫定基準に採用');
  }
}

const bibleDraft = await agentR(
  [
    'design/art-bible.md と design/art-bible.json を書け。',
    (topCandidate
      ? '基準 key image: ' + topCandidate + '（art-reviewer による最上位候補。最終承認は Checkpoint A で人間が行うため、他候補も art-bible.md 内に列挙して差し替え可能にしておくこと）。'
      : '実画像候補が無いため、design/concept.md / design/gdd.md のトーンからスタイルを言語で確定させ、プレースホルダ方針（実画像は Checkpoint A 以降に生成・人間承認）を art-bible.md に明記せよ。'),
    '1. テンプレート .claude/docs/templates/art-bible.md が存在すればその構成に従う。',
    '2. design/art-bible.json は .claude/docs/assets-config.md「スタイル一貫性プロトコル」のスキーマ（style_block / palette / style_codes / reference_images / character_reference / resolution）に厳密準拠。曖昧形容詞のみの指定は不可。',
    '3. 解像度・タイルサイズ・透過方針は ' + EP.techStackDoc + ' と整合させる。',
    (EP.assets3d
      ? '4. 3D エンジンのため「## 3D スタイル方針」節（ポリゴン予算・テクスチャ解像度/PBR・リグ方針・スケール規約）を必ず埋める（テンプレートのガイドと assets-config.md の 3D ルーティング表に従う）。'
      : '4.（2D のため 3D スタイル方針節はテンプレート指示通り削除する。）'),
    '5. .claude/docs/gates.md の AR-BIBLE 観点を先回りして満たすこと。'
  ].join('\n'),
  { agentType: 'art-director', label: 'art-bible 起草', phase: 'ArtBible', effort: 'high' }
);
if (!bibleDraft) throw new Error('design/art-bible.md/.json の起草に失敗（art-director が応答せず）');

await reviewLoop({
  gateId: 'AR-BIBLE',
  reviewArtifact: 'art-bible',
  artifactPaths: ['design/art-bible.md', 'design/art-bible.json'],
  producerType: 'art-director',
  reviewerType: 'art-reviewer',
  phaseTitle: 'ArtBible',
  maxIter: 3,
  producerContextPaths: ['design/concept.md', 'design/gdd.md', 'state/asset-routing.json'],
  unresolved,
  verdictHistory
});

// ---- Phase 4: Assets（art-director 先行 → audio-designer 追記の直列2段）----
phase('Assets');

const assetFieldRules = [
  '全資産エントリに必須: id（安定ID・振り直し禁止。contract.md §8 の資産ID形式）/ サイズ（画像はpx・音声は秒）/ プロンプト草案 / 提供者ルート（state/asset-routing.json のルーティングに従い明記）/ 参照ピラー P-xx（design/concept.md）。',
  'テンプレート .claude/docs/templates/assets.md が存在すればその構成に従う。'
];

// 段1: art-director が骨格（ヘッダ + 見出し）を単独で作成し、画像（+3Dモデル）セクションを起草
const imageSection = await agentR(
  [
    (EP.assets3d
      ? 'design/assets.md を作成せよ。まず文書ヘッダと「## 画像」「## 音声」「## 3Dモデル」「## スケルタルアニメーション」の見出し骨格を作り、その上で「## 画像」「## 3Dモデル」「## スケルタルアニメーション」セクションを起草する。'
      : 'design/assets.md を作成せよ。まず文書ヘッダと「## 画像」「## 音声」の2見出しの骨格を作り、その上で「## 画像」セクションを起草する。'),
    '1. design/gdd.md と design/art-bible.md / design/art-bible.json を読み、必要な全画像資産（スプライト/キャラ/UI/背景/タイル）を洗い出す。',
    '2. 各プロンプト草案には art-bible.json の style_block を前置する前提で書く。サイズ・透過方針は ' + EP.techStackDoc + ' / art-bible.json の resolution と整合させる。',
    (EP.assets3d
      ? '3. 3Dモデル（MDL-xx）は gdd の登場エンティティから洗い出し、kind / ポリ予算 / リグ / 必要アニメ（ANM-xx）を assets-config.md の 3D ルーティング表・art-bible の 3D スタイル方針と整合させて書く（contract §8: MDL/ANM は安定ID）。'
      : '3.（2D のため 3Dモデル/アニメーション節は作らない。）'),
    '4. 「## 音声」セクションは見出しのみ置き、中身は書かない（この後 audio-designer が追記する）。'
  ].filter(Boolean).concat(assetFieldRules).join('\n'),
  { agentType: 'art-director', label: 'assets.md 骨格＋画像セクション', phase: 'Assets', effort: 'high' }
);
if (!imageSection) unresolved.push('assets.md: 骨格＋画像セクションの起草が実行されず（art-director skip/error）');

// 段2: audio-designer が音声セクションを追記（画像セクションには触れない）
const audioSection = await agentR(
  [
    'design/assets.md の「## 音声」セクションを起草せよ。',
    '1. design/gdd.md と design/concept.md を読み、必要な全音声資産（SFX/BGM）を洗い出す。',
    '2. SFX は duration_seconds を明示、BGM はループ前提・ジャンル/BPM/キーを固定して書く（.claude/docs/assets-config.md 準拠）。',
    '3. design/assets.md は art-director が骨格と「## 画像」セクションを作成済みのはず。「## 音声」セクションのみ Edit で追記し、他セクションには一切触れない。万一ファイルが存在しない場合は、文書ヘッダと「## 画像」「## 音声」の骨格を作ってから音声セクションを書く。'
  ].concat(assetFieldRules).join('\n'),
  { agentType: 'audio-designer', label: 'assets.md 音声セクション', phase: 'Assets', effort: 'high' }
);
if (!audioSection) unresolved.push('assets.md: 音声セクションの起草が実行されず（audio-designer skip/error）');

const assetsMerge = await agentR(
  [
    'design/assets.md の整合パスを行え（内容の書き換えは最小限）。',
    '1. 重複見出し・骨格の乱れがあれば構造のみ修復する。音声セクションのエントリ内容には手を入れない。',
    '2. 全エントリが必須フィールド（id / サイズ / プロンプト草案 / 提供者ルート / P-xx参照）を持つか検査し、欠落は文書末尾に「## 欠落チェック」として列挙する（勝手に埋めない）。',
    '3. id の重複・振り直しが無いか確認する。'
  ].join('\n'),
  { agentType: 'art-director', label: 'assets.md 整合パス', phase: 'Assets' }
);
if (!assetsMerge) {
  unresolved.push('assets.md: 整合パスが実行されず。重複見出し・欠落フィールドが残っている可能性');
}

// ---- Phase 5: Final（CD-CHECKPOINT）------------------------------------
phase('Final');

const cdPromptLines = [
  'あなたは Gate CD-CHECKPOINT の判定者である。Checkpoint A の提示前最終判定を行え。',
  '1. .claude/docs/gates.md の「CD-CHECKPOINT」セクションを読み、その観点で判定する。',
  '2. 対象: ' + briefPath + ' / design/concept.md / design/gdd.md / design/art-bible.md / design/art-bible.json / design/assets.md。レビュー履歴 state/reviews/ 配下も確認する。',
  '3. 判定を state/reviews/checkpoint-a.md に .claude/docs/review-loops.md の追記形式で追記せよ。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う（推測記入禁止）。',
  '4. 応答の1行目は「CD-CHECKPOINT: APPROVE|CONCERNS|REJECT」。構造化返却の verdict / findings / fixes にも同じ内容を入れよ。',
  '5. REJECT の場合、fixes に修正指示（assignee は game-designer / art-director / audio-designer のいずれか、artifact は contract.md §6 のパス）を入れよ。APPROVE/CONCERNS では fixes は空配列。',
  '6. 最後に state/active.md を更新せよ（現在地: Phase1 完了・Checkpoint A 待ち / 次アクション: 人間による key image と企画設計一式の承認 / 未解決事項: レビューで残った指摘。日時は `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を使う — 推測記入禁止）。'
];

let cd = await agentR(cdPromptLines.join('\n'), {
  agentType: 'creative-director',
  label: 'CD-CHECKPOINT 判定',
  phase: 'Final',
  schema: CD_SCHEMA,
  effort: 'high'
});

if (!cd) {
  log('CD-CHECKPOINT: creative-director が応答せず。判定不能のまま Checkpoint A へ');
  unresolved.push('CD-CHECKPOINT: 判定が実行されず（creative-director skip/error）');
} else {
  log('CD-CHECKPOINT: ' + cd.verdict);
  verdictHistory.push({
    gate: 'CD-CHECKPOINT',
    artifact: 'checkpoint-a',
    iteration: 1,
    verdict: cd.verdict,
    findings: cd.findings || []
  });

  if (cd.verdict === 'REJECT') {
    // review-loops.md: REJECT なら指示に従い修正後、1回だけ再判定
    const fixes = (cd.fixes || []).filter(function (f) {
      return f && f.assignee && f.artifact && f.instruction;
    });
    if (fixes.length > 0) {
      // fix 自体は並列可。ただし state/reviews/checkpoint-a.md への「対応:」追記は
      // 競合防止のため fix 完了後に game-designer が1回でまとめて行う（ここでは追記しない）
      const fixResults = await parallel(fixes.map(function (f) {
        return function () {
          return agentR(
            [
              'CD-CHECKPOINT で REJECT された。以下の指示に従い ' + f.artifact + ' を修正せよ。',
              '指示: ' + f.instruction,
              '判定の全文は state/reviews/checkpoint-a.md を読むこと（読むだけ。同ファイルへの追記は行わない。対応記録は修正完了後に別 agent がまとめて行う）。',
              '修正内容の要約を応答で報告せよ。'
            ].join('\n'),
            { agentType: f.assignee, label: 'CD修正: ' + f.artifact, phase: 'Final', effort: 'high' }
          );
        };
      }));
      const doneCount = fixResults.filter(Boolean).length;
      log('CD-CHECKPOINT 修正: ' + doneCount + '/' + fixes.length + ' 件完了');

      const fixRecord = await agentR(
        [
          'CD-CHECKPOINT REJECT 後の修正が完了した。state/reviews/checkpoint-a.md の該当 iteration の「対応:」欄に、以下の各修正指示への対応内容をまとめて1回の追記で記録せよ（黙殺禁止。未実施・未達の指示は「未対応」と理由を明記）。'
        ].concat(fixes.map(function (f, idx) {
          return (idx + 1) + '. [' + f.assignee + '] ' + f.artifact + ' — ' + f.instruction;
        })).concat([
          '各 artifact の現状を読み、実際に何が変わったかを確認してから記録すること。'
        ]).join('\n'),
        { agentType: 'game-designer', label: 'CD修正の対応記録', phase: 'Final' }
      );
      if (!fixRecord) {
        unresolved.push('CD-CHECKPOINT: 修正の対応記録が実行されず（state/reviews/checkpoint-a.md の「対応:」欄が空のまま）');
      }
    } else {
      log('CD-CHECKPOINT: REJECT だが fixes が空。修正なしで再判定へ');
    }

    const cd2 = await agentR(cdPromptLines.join('\n').replace('追記形式で追記せよ。', '追記形式で追記せよ（iteration 2 として）。'), {
      agentType: 'creative-director',
      label: 'CD-CHECKPOINT 再判定',
      phase: 'Final',
      schema: CD_SCHEMA,
      effort: 'high'
    });
    if (cd2) {
      cd = cd2;
      log('CD-CHECKPOINT 再判定: ' + cd.verdict);
      verdictHistory.push({
        gate: 'CD-CHECKPOINT',
        artifact: 'checkpoint-a',
        iteration: 2,
        verdict: cd2.verdict,
        findings: cd2.findings || []
      });
    } else {
      unresolved.push('CD-CHECKPOINT: 再判定が実行されず。初回 REJECT のまま Checkpoint A へ');
    }
  }

  if (cd.verdict !== 'APPROVE') {
    for (const f of (cd.findings || [])) unresolved.push('CD-CHECKPOINT: ' + f);
  }
}

// ---- 戻り値（Checkpoint A 素材。人間提示はスキル側の責務）----------------
const verdict = cd ? cd.verdict : 'CONCERNS';
const artifacts = [
  'design/concept.md',
  'design/gdd.md',
  'design/art-bible.md',
  'design/art-bible.json',
  'design/assets.md'
];

return {
  summary:
    'Phase 1（企画・設計）完了。concept / gdd / art-bible / assets を produce→review→revise ループで作成。' +
    'CD-CHECKPOINT 判定: ' + verdict + '。' +
    '重要: 最終 key image の承認は Checkpoint A で人間が行う。keyImageCandidates（' + keyImageCandidates.length + '件）から採用・差し戻しを判断すること。' +
    (unresolved.length > 0 ? ' 未解決指摘 ' + unresolved.length + ' 件あり（unresolvedFindings 参照）。' : ' 未解決指摘なし。') +
    ' 全レビュー判定履歴は verdictHistory（' + verdictHistory.length + '件）に含む（review-mode=full ではスキルが全件提示する）。',
  artifacts: artifacts,
  keyImageCandidates: keyImageCandidates,
  unresolvedFindings: unresolved,
  verdictHistory: verdictHistory,
  verdict: verdict
};
