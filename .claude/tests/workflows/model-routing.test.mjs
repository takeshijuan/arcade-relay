// モデル階層ルーティング（.claude/docs/model-routing.md）の機械同期テスト。
// 階層表 ↔ agents/*.md frontmatter、workflow の全 agent() 呼び出しの「セッションモデル非継承」不変条件、
// mechanical 階層のラベル、段階エスカレーション（fail closed マージ含む）、証跡検証の rawLine 突合、
// CLAUDE.md の自動 import 縮小、tokenUsage 計測（phase 列 + 終端サンプル）。
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { runWorkflow, callsBy, promptsBy } from './harness.mjs';

const root = new URL('../../', import.meta.url).pathname; // .claude/
const read = (p) => readFile(root + p, 'utf8');
const WF = (n) => root + 'workflows/' + n;
const R = (match, reply) => ({ match, reply });
const MODELS = ['haiku', 'sonnet', 'opus'];
// mechanical 階層のラベル（model-routing.md §1「対象ラベル」と同期）: 読み取り専用の実在確認・突合と直列区間の状態更新のみ。
// 並走レーン中の git/stories.yaml 更新（bookkeep-/close-）と実装順判断（replan-extract）は含めない
const MECHANICAL = /^(verify-evidence-|setup-crosscheck-stories|finalize-state)/;
// 各 workflow の phaseT 境界（meta.phases のうち代表宣言される phase）+ 終端サンプル 'end'
const EXPECTED_PHASES = {
  'prototype.js': ['Setup', 'Build', 'Integrate', 'QA', 'Final'],
  'full-build.js': ['Replan', 'Build', 'Polish', 'FullQA', 'Final'],
  'concept-design.js': ['Concept', 'GDD', 'ArtBible', 'Assets', 'Final'],
};

async function routingTable() {
  const doc = await read('docs/model-routing.md');
  const rows = [...doc.matchAll(/^\| ([a-z-]+) \| (judge|producer|mechanical) \| (opus|sonnet|haiku) \|$/gm)];
  const table = {};
  for (const m of rows) table[m[1]] = { tier: m[2], model: m[3] };
  return table;
}
const HARNESS_AGENTS = Object.keys(await routingTable()); // 割当表が正本（contract §2 の 10 体と agents/ 実体を下で突合）

// ---- fixtures（既存テスト prototype.test.mjs / full-build.test.mjs の baseRoutes と同形） ----
const ENV_ACC = '可視の地面/背景・ライト・カメラ構図と画面レイアウトが確定している';
const SETUP = {
  prototypeStories: [
    { id: 'S-01', title: 'メタ進行永続化', assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-02', title: 'Title シーン', assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-03', title: 'Menu シーン', assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-04', title: 'コアループと環境ビジュアル', assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: ENV_ACC },
  ],
  titleStoryId: 'S-02', menuStoryId: 'S-03', metaPersistenceStoryId: 'S-01', environmentStoryId: 'S-04',
};
const CROSSCHECK = { found: [
  { id: 'S-02', exists: true, assignee: 'ui-engineer', phase: 'prototype' },
  { id: 'S-03', exists: true, assignee: 'ui-engineer', phase: 'prototype' },
  { id: 'S-01', exists: true, assignee: 'gameplay-engineer', phase: 'prototype' },
  { id: 'S-04', exists: true, assignee: 'gameplay-engineer', phase: 'prototype', acceptance: ENV_ACC },
] };
const EV_PATH = 'qa/evidence/e.png';
const PROTO_QA_OK = { verdict: 'APPROVE', criticalBugs: [], bugs: [], failedAcceptance: [], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true };
const EV_OK = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: 'qa/evidence/e.png 1234' }], extraFilesInEvidenceDir: [] }; // rawLine = stat "%N %z" の行
const BATCH_OK = { ok: true, fixedNotes: [], unresolved: [] };
// retro-e4: 実装は changedFiles（コード対象パス）、reviewer は observedCodeFiles（対象証明）を返すのが正常系
// 3 engine のコード対象パス（contract §11）を全て含め、unity 経路の RUNS でも対象証明が成立する fixture にする
const CODE_FILES = ['game/src/systems/x.ts', 'game/Assets/Scripts/Systems/X.cs', 'game/Source/ForgeGame/Systems/X.cpp'];
const PROTO_IMPL_OK = { commitHash: 'abc1234', changedFiles: CODE_FILES, summary: 's' };
const PROTO_FIX_OK = { commitHash: 'f1x0001', changedFiles: CODE_FILES, summary: 's' }; // CR-CODE fix もコード対象パスを申告する（fix コミットが次 iteration の対象）
const PROTO_CR_OK = { verdict: 'APPROVE', findings: [], observedCodeFiles: CODE_FILES };
const protoRoutes = (extra = [], batch = BATCH_OK, qa = PROTO_QA_OK, ev = EV_OK) => extra.concat([
  R(/^setup-scaffold-stories/, SETUP),
  R(/^setup-crosscheck-stories/, CROSSCHECK),
  R(/^implement-/, PROTO_IMPL_OK),
  R(/^fix-(?!qa-)/, PROTO_FIX_OK),
  R(/^cr-(code|silent)-/, PROTO_CR_OK),
  R(/^qa-play-round/, qa),
  R(/^verify-evidence-round/, ev),
  R(/^batch-verify-/, batch),
]);
const PROTO_ARGS = { reviewMode: 'lean', engine: 'phaser' };

const gp = (id, title) => ({ id, title: title || id, assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: 'a' });
const ui = (id, title) => ({ id, title: title || id, assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' });
const FB_QA_OK = { verdict: 'APPROVE', bugs: [], failedAcceptance: [], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true };
const FB_IMPL_OK = { commitHash: 'abc1234', changedFiles: CODE_FILES };
const FB_FIX_OK = { commitHash: 'f1x0001', changedFiles: CODE_FILES }; // CR-CODE fix もコード対象パスを申告する（QA fix は qa-fix- で別）
const FB_CR_OK = { findings: [], observedCodeFiles: CODE_FILES };
const fbRoutes = (extra = [], batch = BATCH_OK, qa = FB_QA_OK) => extra.concat([
  R(/^replan-stories$/, { stories: [gp('S-01'), ui('S-02'), gp('S-03')] }),
  R(/^polish-plan$/, { stories: [gp('S-10'), ui('S-11')] }),
  R(/^impl-/, FB_IMPL_OK),
  R(/^fix-/, FB_FIX_OK),
  R(/^(cr|sfh)-/, FB_CR_OK),
  R(/^qa-play-/, qa),
  R(/^verify-evidence-/, EV_OK),
  R(/^batch-verify-/, batch),
]);
const FB_ARGS = { reviewMode: 'lean', engine: 'phaser', checkpointBFeedbackPath: 'state/checkpoint-b-feedback.md' };
const CD_ARGS = { briefPath: 'design/brief.md', reviewMode: 'lean', engine: 'phaser' };

// 不変条件は phaser だけでなく unity 経路（3D 資産トラック・Integrate 3D）も通す
const RUNS = [
  { name: 'prototype.js', tag: 'phaser', args: PROTO_ARGS, routes: protoRoutes() },
  { name: 'prototype.js', tag: 'unity', args: { reviewMode: 'lean', engine: 'unity' }, routes: protoRoutes() },
  { name: 'full-build.js', tag: 'phaser', args: FB_ARGS, routes: fbRoutes() },
  { name: 'full-build.js', tag: 'unity', args: Object.assign({}, FB_ARGS, { engine: 'unity' }), routes: fbRoutes() },
  { name: 'concept-design.js', tag: 'phaser', args: CD_ARGS, routes: [] },
];

// ---- 階層表 ↔ frontmatter ----

test('階層表: 割当表の agent が contract §2 の 10 体（agents/ 実体）と一致し、各 frontmatter の model が表と一致する', async () => {
  const table = await routingTable();
  const files = (await readdir(root + 'agents')).filter((f) => f.endsWith('.md')).map((f) => f.replace(/\.md$/, ''));
  assert.deepEqual([...files].sort(), [...HARNESS_AGENTS].sort(), 'agents/ の実体と model-routing.md の割当表が一致しない');
  assert.equal(HARNESS_AGENTS.length, 10, 'contract §2 の harness agent は 10 体');
  for (const a of HARNESS_AGENTS) {
    const fm = (await read('agents/' + a + '.md')).match(/^model:\s*(\S+)\s*$/m);
    assert.ok(fm, a + '.md に model frontmatter が無い（セッションモデル継承 = オーケストレータ帯で実作業）');
    assert.equal(fm[1], table[a].model, a + ': frontmatter model が階層表とドリフト');
  }
});

test('階層表: workflow の TIER 定数が model-routing.md §1 の tier→model と一致する', async () => {
  const doc = await read('docs/model-routing.md');
  const tierModel = {};
  for (const m of doc.matchAll(/^\| `(judge|producer|mechanical)` \| `(opus|sonnet|haiku)` \|/gm)) tierModel[m[1]] = m[2];
  assert.deepEqual(Object.keys(tierModel).sort(), ['judge', 'mechanical', 'producer']);
  for (const wf of Object.keys(EXPECTED_PHASES)) {
    const src = await read('workflows/' + wf);
    const m = src.match(/const TIER = \{ judge: '(\w+)', producer: '(\w+)', mechanical: '(\w+)' \};/);
    assert.ok(m, wf + ' に TIER 定数が無い');
    assert.deepEqual({ judge: m[1], producer: m[2], mechanical: m[3] }, tierModel, wf + ' の TIER が階層表とドリフト');
  }
});

test('階層表: 本文で `date -u` の実行出力を要求する agent は frontmatter tools に Bash を持つ（retro-e4: design 系 3 体の Bash 非保持で時刻が推測記入になった）', async () => {
  for (const a of HARNESS_AGENTS) {
    const md = await read('agents/' + a + '.md');
    if (!md.includes('date -u')) continue;
    const tools = md.match(/^tools:\s*(.+)$/m);
    assert.ok(tools, a + '.md に tools frontmatter が無い');
    assert.ok(tools[1].split(',').map((s) => s.trim()).includes('Bash'), a + ': `date -u` を要求するのに Bash が無い');
  }
  for (const a of ['creative-director', 'design-reviewer', 'game-designer']) {
    const md = await read('agents/' + a + '.md');
    assert.ok(/^tools:.*\bBash\b/m.test(md), a + ': Bash 付与（retro-e4）が外れている');
    assert.ok(md.includes('## Bash の使用範囲'), a + ': 「Bash の使用範囲」節（date -u と読み取り git に限定）が無い');
  }
});

// ---- 不変条件・mechanical・tokenUsage（各 workflow を 1 回だけ実行して 3 観点を検証） ----

for (const run of RUNS) {
  test('不変条件/mechanical/tokenUsage(' + run.name + ' ' + run.tag + ')', async () => {
    const { calls, result, phases } = await runWorkflow(WF(run.name), { args: run.args, routes: run.routes });
    assert.ok(calls.length > 5, 'agent 呼び出しが少なすぎる（fixture 不整合）: ' + calls.length);
    let mech = 0;
    for (const c of calls) {
      const o = c.opts || {};
      const harness = HARNESS_AGENTS.includes(o.agentType);
      const explicit = MODELS.includes(o.model);
      assert.ok(harness || explicit, run.name + ' label=' + c.label + ': agentType=' + o.agentType + ' model=' + o.model + ' はセッションモデルを継承する');
      if ('model' in o) assert.ok(MODELS.includes(o.model), run.name + ' label=' + c.label + ': model が不正/undefined（' + o.model + '）');
      if (String(o.agentType || '').startsWith('pr-review-toolkit:')) {
        assert.equal(o.model, 'sonnet', run.name + ' label=' + c.label + ': 外部 reviewer は producer 階層（sonnet）を明示する');
      }
      if (MECHANICAL.test(c.label)) {
        mech++;
        assert.equal(o.model, 'haiku', run.name + ' label=' + c.label + ': mechanical は haiku');
        assert.equal(o.effort, 'low', run.name + ' label=' + c.label + ': mechanical は effort low');
      }
      if (/^(bookkeep-|close-|replan-extract)/.test(c.label)) {
        assert.ok(!('model' in o), run.name + ' label=' + c.label + ': 並走 git 更新/実装順判断は frontmatter 階層のまま（model 上書き禁止）');
      }
    }
    if (run.name !== 'concept-design.js') assert.ok(mech > 0, run.name + ' に mechanical ラベルが1つも無い');
    // tokenUsage: 代表 phase 境界の列 + 終端サンプル（Final の右端点 — 無いと最終 phase の消費が算出不能）
    assert.deepEqual(phases, EXPECTED_PHASES[run.name], run.name + ': phaseT 境界の列が期待と一致しない');
    assert.ok(Array.isArray(result.tokenUsage), run.name + ' の戻り値に tokenUsage が無い');
    assert.deepEqual(result.tokenUsage.map((t) => t.phase), EXPECTED_PHASES[run.name].concat(['end']));
    for (const t of result.tokenUsage) assert.equal(typeof t.outputTokensBefore, 'number');
  });
}

test('tokenUsage(prototype): Setup 不合格の早期 return にも tokenUsage（終端サンプル）が載る', async () => {
  const routes = [R(/^setup-scaffold-stories/, null), R(/^setup-fix-required-stories/, null)];
  const { result } = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes });
  assert.ok(Array.isArray(result.tokenUsage) && result.tokenUsage.at(-1).phase === 'end', JSON.stringify(result.tokenUsage));
});

// ---- 段階エスカレーション: CR-CODE fix ----

test('段階エスカレーション(prototype): CR-CODE fix は iter1=producer 継承・iter2=judge(opus) + 根本原因注記（前回 fix 実行済みが条件）', async () => {
  const routes = protoRoutes([
    R(/^cr-code-S-01-iter/, { verdict: 'CONCERNS', findings: ['マジックナンバー'] }),
    R(/^cr-silent-S-01-iter/, { verdict: 'APPROVE', findings: [] }),
  ]);
  const { calls } = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes });
  const f1 = callsBy(calls, /^fix-S-01-iter1$/)[0];
  const f2 = callsBy(calls, /^fix-S-01-iter2$/)[0];
  assert.ok(f1 && f2, 'fix が2周走らない');
  assert.ok(!('model' in f1.opts), 'iter1 の fix が model を上書きしている（producer 継承のはず）');
  assert.equal(f2.opts.model, 'opus');
  assert.equal(f2.opts.agentType, 'gameplay-engineer', '段階エスカレーションでも役割宣言（agentType）は据え置き');
  assert.ok(f2.prompt.startsWith('【段階エスカレーション'), 'iter2 の fix プロンプトに根本原因注記が前置されない');
  assert.ok(!f1.prompt.includes('【段階エスカレーション'), 'iter1 に注記が混入');
});

test('段階エスカレーション(prototype): iter1 のレビューペアが両方失敗 → iter2 が初回 fix なら judge へ上げない', async () => {
  const routes = protoRoutes([
    R(/^cr-code-S-01-iter1/, null), R(/^cr-silent-S-01-iter1/, null), // 接頭辞 = -retry も null
    R(/^cr-code-S-01-iter2/, { verdict: 'CONCERNS', findings: ['x'] }),
    R(/^cr-silent-S-01-iter2/, { verdict: 'APPROVE', findings: [] }),
  ]);
  const { calls } = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes });
  assert.equal(callsBy(calls, /^fix-S-01-iter1$/).length, 0, 'review 失敗 iteration で fix が走った');
  const f2 = callsBy(calls, /^fix-S-01-iter2$/)[0];
  assert.ok(f2, 'iter2 の fix が走らない');
  assert.ok(!('model' in f2.opts), '初回 fix なのに judge へ上げている（注記「前回の修正で解消しなかった」が偽になる）');
  assert.ok(!f2.prompt.includes('【段階エスカレーション'));
});

test('段階エスカレーション(full-build): CR-CODE fix iter2=opus（fix 実行済み時のみ）・両 reviewer 失敗後の初回 fix は producer', async () => {
  const escalated = fbRoutes([R(/^cr-s-01-|^sfh-s-01-/, { findings: [{ summary: 'x', severity: 'major' }] })]);
  const a = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: escalated });
  const f1 = callsBy(a.calls, /^fix-s-01-1$/)[0];
  const f2 = callsBy(a.calls, /^fix-s-01-2$/)[0];
  assert.ok(f1 && f2);
  assert.ok(!('model' in f1.opts));
  assert.equal(f2.opts.model, 'opus');
  assert.ok(f2.prompt.startsWith('【段階エスカレーション'));
  const firstFail = fbRoutes([
    R(/^cr-s-01-1|^sfh-s-01-1/, null),
    R(/^cr-s-01-2|^sfh-s-01-2/, { findings: [{ summary: 'x', severity: 'major' }] }),
  ]);
  const b = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: firstFail });
  assert.equal(callsBy(b.calls, /^fix-s-01-1$/).length, 0);
  const g2 = callsBy(b.calls, /^fix-s-01-2$/)[0];
  assert.ok(g2 && !('model' in g2.opts), '初回 fix なのに judge へ上げている');
});

test('段階エスカレーション(concept-design): reviewLoop の最終 iteration revise は judge(opus)・途中は producer 継承', async () => {
  const routes = [R(/^DR-CONCEPT review #/, { verdict: 'CONCERNS', findings: ['ピラーが無内容'] })];
  const { calls } = await runWorkflow(WF('concept-design.js'), { args: CD_ARGS, routes });
  const r1 = callsBy(calls, /^DR-CONCEPT revise #1$/)[0];
  const r3 = callsBy(calls, /^DR-CONCEPT revise #3$/)[0];
  assert.ok(r1 && r3, 'revise が3周走らない');
  assert.ok(!('model' in r1.opts));
  assert.equal(r3.opts.model, 'opus');
  assert.equal(r3.opts.agentType, 'game-designer');
  assert.ok(r3.prompt.startsWith('【段階エスカレーション'));
  assert.ok(!r1.prompt.includes('【段階エスカレーション'));
});

// ---- 段階エスカレーション: QA fix ----

test('段階エスカレーション(prototype): QA fix（重大バグ・acceptance）は judge(opus) + judge 注記', async () => {
  const qa = { verdict: 'CONCERNS', criticalBugs: [{ title: 'クラッシュ', detail: 'd', assignee: 'ui-engineer' }], failedAcceptance: ['S-01: x'], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true };
  const { calls } = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qa) });
  const bug = callsBy(calls, /^fix-qa-r1-ui-engineer-0$/)[0];
  const acc = callsBy(calls, /^fix-qa-acceptance-r1$/)[0];
  assert.ok(bug && acc);
  assert.equal(bug.opts.model, 'opus'); assert.equal(bug.opts.agentType, 'ui-engineer');
  assert.equal(acc.opts.model, 'opus');
  assert.ok(bug.prompt.startsWith('【judge 階層で実施'));
});

test('段階エスカレーション(full-build): QA fix は judge(opus)・acceptance 未通過は担当 assignee のレーンにだけ渡る', async () => {
  // S-02 は ui-engineer の story。バグは無く acceptance だけ落ちた → ui レーンのみ fix 起動（gameplay に重複起動しない）
  const qa = { verdict: 'CONCERNS', bugs: [], failedAcceptance: ['S-02'], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true, summary: 'ng' };
  const { calls } = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, qa) });
  const uiFix = callsBy(calls, /^qa-fix-1-ui-engineer$/)[0];
  assert.ok(uiFix, 'ui レーンの fix が走らない');
  assert.equal(uiFix.opts.model, 'opus');
  assert.ok(uiFix.prompt.startsWith('【judge 階層で実施'));
  assert.ok(uiFix.prompt.includes('"S-02"'));
  assert.equal(callsBy(calls, /^qa-fix-1-gameplay-engineer$/).length, 0, '担当外レーンに acceptance 修正が重複起動した');
  // 所有者不明（Replan/Polish 一覧に無い prototype story や完了済み build story）は両レーンへ — 片方の既定に倒すと
  // UI 担当の回帰が UI レーンに届かない（Codex P1）
  const qaUnknown = Object.assign({}, qa, { failedAcceptance: ['S-77: prototype 由来の回帰'] });
  const u = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, qaUnknown) });
  for (const eng of ['gameplay-engineer', 'ui-engineer']) {
    const fx = callsBy(u.calls, new RegExp('^qa-fix-1-' + eng + '$'))[0];
    assert.ok(fx && fx.prompt.includes('"S-77'), eng + ' レーンに所有者不明の acceptance が渡らない');
  }
});

// ---- QA 非 APPROVE の修正条件（retro-e4） ----

test('QA 非 APPROVE で修正対象が空（retro-e4）: summary 起点の fix が judge で 1 回走り、qa-lead のプロトコル違反として記録される', async () => {
  // E4: 中程度バグが qa/report.md にだけ書かれ、fix が一度も走らず HEAD 同一のまま round 2 に到達した
  const qaEmpty = { verdict: 'CONCERNS', criticalBugs: [], bugs: [], failedAcceptance: [], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true, summary: '中程度の表示崩れ' };
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qaEmpty) });
  const sfix = callsBy(p.calls, /^fix-qa-r1-summary$/);
  assert.equal(sfix.length, 1, 'summary 起点 fix が走らない');
  assert.equal(sfix[0].opts.model, 'opus'); assert.equal(sfix[0].opts.agentType, 'gameplay-engineer');
  assert.ok(sfix[0].prompt.startsWith('【judge 階層で実施') && sfix[0].prompt.includes('中程度の表示崩れ'), 'summary が fix プロンプトに渡らない');
  assert.ok(p.result.unresolvedFindings.some((f) => f.includes('qa-lead.md 違反')), 'プロトコル違反が記録されない');
  assert.equal(callsBy(p.calls, /^fix-qa-r2-summary$/).length, 0, '最終 round では fix を走らせない');
  const fbEmpty = { verdict: 'CONCERNS', bugs: [], failedAcceptance: [], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true, summary: 'HUD 数値が更新されない' };
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, fbEmpty) });
  const fb = callsBy(f.calls, /^qa-fix-1-summary$/);
  assert.equal(fb.length, 1, 'full-build の summary 起点 fix が走らない');
  assert.equal(fb[0].opts.model, 'opus');
  assert.ok(fb[0].prompt.includes('HUD 数値が更新されない'));
  assert.equal(callsBy(f.calls, /^qa-fix-1-(gameplay|ui)-engineer$/).length, 0, '空配列なのに担当レーン fix が走った');
  assert.equal(callsBy(f.calls, /^qa-fix-2-summary$/).length, 0, '最終 round では fix を走らせない');
  assert.ok(f.result.unresolvedFindings.some((x) => x.includes('qa-lead.md 違反')));
  // 修正対象があるときは summary 起点 fix を起こさない（既存経路のまま）
  const qaAcc = Object.assign({}, qaEmpty, { failedAcceptance: ['S-01: x'] });
  const a = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qaAcc) });
  assert.equal(callsBy(a.calls, /^fix-qa-r1-summary$/).length, 0);
  assert.equal(callsBy(a.calls, /^fix-qa-acceptance-r1$/).length, 1);
});

test('QA major バグ（prototype・retro-e4）: bugs の major は assignee 単位で 1 呼び出しにバッチし judge で修正、minor は修正対象外、最終 round 残存は記録', async () => {
  const qa = { verdict: 'CONCERNS', criticalBugs: [], failedAcceptance: [], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true, summary: 'ng',
    bugs: [
      { title: 'HUD ずれ', detail: 'd1', severity: 'major', assignee: 'ui-engineer' },
      { title: 'メニュー戻り', detail: 'd2', severity: 'major', assignee: 'ui-engineer' },
      { title: '色味', detail: 'd3', severity: 'minor', assignee: 'gameplay-engineer' },
    ] };
  const { calls, result } = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qa) });
  const ui = callsBy(calls, /^fix-qa-r1-bugs-ui-engineer$/);
  assert.equal(ui.length, 1, 'ui の major 2 件が 1 呼び出しにバッチされない');
  assert.equal(ui[0].opts.model, 'opus'); assert.equal(ui[0].opts.agentType, 'ui-engineer');
  assert.ok(ui[0].prompt.includes('HUD ずれ') && ui[0].prompt.includes('メニュー戻り'));
  assert.equal(callsBy(calls, /^fix-qa-r1-bugs-gameplay-engineer$/).length, 0, 'minor だけの assignee に fix が走った');
  assert.equal(callsBy(calls, /^fix-qa-r1-summary$/).length, 0, 'major があるのに summary 起点 fix が走った');
  assert.ok(result.unresolvedFindings.some((f) => f.includes('未解決の major バグ: HUD ずれ')), '最終 round 後の major 残存が記録されない');
});

// ---- 段階エスカレーション: batch-verify（fail closed マージ） ----

test('batch-verify(prototype): ok:false → judge で1回再試行（-escalate, opus）・fixedNotes 合算・resolvedPrior で解消した項目だけ落ち残りは引き継ぐ', async () => {
  const batch = (call) => call.label.endsWith('-escalate')
    ? { ok: false, fixedNotes: ['2回目: 参照修正'], unresolved: ['c'], resolvedPrior: ['a'] }
    : { ok: false, fixedNotes: ['1回目: import 整理'], unresolved: ['a', 'b'] };
  const { calls, result } = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], batch) });
  const esc = callsBy(calls, /^batch-verify-build-escalate$/);
  assert.equal(esc.length, 1, 'escalate は1回だけ（agentR の null 再試行を重ねない）');
  assert.equal(esc[0].opts.model, 'opus');
  assert.equal(esc[0].opts.agentType, 'gameplay-engineer');
  assert.ok(esc[0].prompt.includes('"a"') && esc[0].prompt.includes('"b"'), '前回の未解決が再試行プロンプトに渡らない');
  assert.ok(esc[0].opts.schema && esc[0].opts.schema.required.includes('resolvedPrior'), '再試行 schema に resolvedPrior が無い');
  assert.ok(result.knownIssues.some((k) => k.includes('1回目: import 整理')), '1回目の fixedNotes が消えた');
  assert.ok(result.knownIssues.some((k) => k.includes('2回目: 参照修正')), '2回目の fixedNotes が消えた');
  const bl = result.unresolvedFindings.filter((f) => f.includes('[batch-verify]'));
  assert.ok(bl.some((f) => f.endsWith(' b')), '初回の未解決 b（未解消）が引き継がれない: ' + JSON.stringify(bl));
  assert.ok(bl.some((f) => f.endsWith(' c')), '再試行の新規未解決 c が消えた');
  assert.ok(!bl.some((f) => f.endsWith(' a')), 'resolvedPrior で解消した a が残っている');
  assert.ok(promptsBy(calls, /^integrate-assets$/)[0].includes('警告'), '未解決残存なのに警告が消えた');
});

test('batch-verify(prototype): 再試行が ok:true でも resolvedPrior 未列挙の初回 unresolved は残り不合格のまま（fail closed）/ 全列挙なら合格', async () => {
  const stubborn = (call) => call.label.endsWith('-escalate')
    ? { ok: true, fixedNotes: [], unresolved: [], resolvedPrior: [] }
    : { ok: false, fixedNotes: [], unresolved: ['compiler still broken'] };
  const ng = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], stubborn) });
  assert.ok(ng.result.unresolvedFindings.some((f) => f.includes('[batch-verify] compiler still broken')), '初回の BLOCKER が schema-valid な再試行応答で消えた');
  assert.ok(promptsBy(ng.calls, /^integrate-assets$/)[0].includes('警告'));
  const honest = (call) => call.label.endsWith('-escalate')
    ? { ok: true, fixedNotes: ['直した'], unresolved: [], resolvedPrior: ['compiler still broken'] }
    : { ok: false, fixedNotes: [], unresolved: ['compiler still broken'] };
  const ok = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], honest) });
  assert.ok(!ok.result.unresolvedFindings.some((f) => f.includes('batch-verify')), JSON.stringify(ok.result.unresolvedFindings));
  assert.ok(!promptsBy(ok.calls, /^integrate-assets$/)[0].includes('警告'), '再試行で合格したのに警告が残る');
});

test('batch-verify(prototype): ok:true + unresolved 残存も judge 再試行の対象 / 合格なら escalate は走らない', async () => {
  const partial = (call) => call.label.endsWith('-escalate')
    ? { ok: true, fixedNotes: [], unresolved: [], resolvedPrior: ['残存'] }
    : { ok: true, fixedNotes: [], unresolved: ['残存'] };
  const a = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], partial) });
  assert.equal(callsBy(a.calls, /^batch-verify-build-escalate$/).length, 1, 'ok:true+unresolved が judge 再試行されない');
  const b = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes() });
  assert.equal(callsBy(b.calls, /-escalate/).length, 0);
});

test('batch-verify(prototype): 初回 null（agentR リトライ後も）は judge で再試行し回復 / 再試行 null は初回結果で続行し記録 / 両方 null は BLOCKER 1件', async () => {
  const recover = (call) => call.label.endsWith('-escalate') ? { ok: true, fixedNotes: ['judge が修復'], unresolved: [], resolvedPrior: [] } : null;
  const rec = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], recover) });
  assert.ok(callsBy(rec.calls, /^batch-verify-build-escalate$/)[0], 'null 後の escalate が無い');
  assert.ok(rec.result.knownIssues.some((k) => k.includes('judge が修復')));
  assert.ok(!rec.result.unresolvedFindings.some((f) => f.includes('batch-verify')), JSON.stringify(rec.result.unresolvedFindings));
  const escNull = (call) => call.label.includes('-escalate') ? null : { ok: false, fixedNotes: [], unresolved: ['x'] };
  const ng = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], escNull) });
  assert.ok(ng.result.unresolvedFindings.some((f) => f.includes('再試行 agent（judge 階層）が結果を返さなかった')));
  assert.ok(ng.result.unresolvedFindings.some((f) => f.includes('[batch-verify] x')), '初回の unresolved が消えた');
  const dead = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], () => null) });
  const bl = dead.result.unresolvedFindings.filter((f) => f.includes('バッチ検証'));
  assert.equal(bl.length, 1, '両方 null で BLOCKER が二重記録: ' + JSON.stringify(bl));
  assert.ok(bl[0].includes('結果を返さなかった'));
});

test('batch-verify(full-build): Build/Polish とも escalate=opus・fixedNotes 合算・未解消の初回 unresolved を引き継ぐ', async () => {
  const batch = (call) => call.label.endsWith('-escalate')
    ? { ok: false, fixedNotes: ['2回目'], unresolved: [], resolvedPrior: [] }
    : { ok: false, fixedNotes: ['1回目'], unresolved: ['初回の残り'] };
  const { calls, result } = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], batch) });
  for (const ph of ['build', 'polish']) {
    const esc = callsBy(calls, new RegExp('^batch-verify-' + ph + '-escalate$'));
    assert.equal(esc.length, 1, ph + ' の escalate が1回でない'); assert.equal(esc[0].opts.model, 'opus');
  }
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[batch-verify修正・CR-CODE非経由] 1回目')));
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[batch-verify修正・CR-CODE非経由] 2回目')));
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[batch-verify] 初回の残り')), '初回の unresolved が judge 応答で消えた');
});

// ---- 証跡検証（mechanical 階層）の自己申告擬装防止 ----

test('verify-evidence: rawLine 不一致は是正指示付きで 1 回再検証し、それでも不一致なら降格せず [VERIFY-UNCERTAIN] で orchestrator に引き渡す（E4 擬陽性 78/78 件の再発防止）', async () => {
  const fake = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: 'ok' }], extraFilesInEvidenceDir: [] };
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, fake) });
  const re = callsBy(p.calls, /^verify-evidence-round1-recheck$/);
  assert.equal(re.length, 1, '再検証が 1 回発行されない');
  assert.ok(re[0].prompt.includes('【再検証】') && re[0].prompt.includes('stat -f'), '再検証プロンプトに是正指示（コマンド固定）が無い');
  assert.equal(re[0].opts.model, 'haiku'); assert.equal(re[0].opts.effort, 'low');
  assert.ok(p.result.unresolvedFindings.some((f) => f.startsWith('[QA-PLAY][VERIFY-UNCERTAIN]') && f.includes(EV_PATH)), JSON.stringify(p.result.unresolvedFindings));
  assert.ok(p.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'APPROVE'), 'rawLine 不一致だけで APPROVE が降格された（E4 擬陽性の再発）');
  assert.ok(!p.result.unresolvedFindings.some((f) => f.includes('機械検証不合格')), '不一致が不合格として記録された');
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^verify-evidence-/, fake)].concat(fbRoutes()) });
  assert.equal(callsBy(f.calls, /^verify-evidence-1-recheck$/).length, 1);
  assert.ok(f.result.unresolvedFindings.some((x) => x.startsWith('[VERIFY-UNCERTAIN] FullQA round 1')), JSON.stringify(f.result.unresolvedFindings));
  assert.ok(f.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'APPROVE'));
  // basename 一致（別ディレクトリの実在ファイルの行）も「未検証」— 合格にも不合格にもしない（Codex P2 の抜け道は塞いだまま）
  const collide = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: '-rw-r--r-- 1 u g 1234 Sep 2 03:00 qa/evidence/old/e.png' }], extraFilesInEvidenceDir: [] };
  const c = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, collide) });
  assert.ok(c.result.unresolvedFindings.some((x) => x.startsWith('[QA-PLAY][VERIFY-UNCERTAIN]')), 'basename 衝突の rawLine が確認済み扱いになった');
  assert.ok(!c.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'));
  // 再検証で是正されれば何も残さない
  const healed = (call) => (call.label.endsWith('-recheck') ? EV_OK : fake);
  const h = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, healed) });
  assert.ok(!h.result.unresolvedFindings.some((x) => x.includes('VERIFY-UNCERTAIN')), '是正後も未検証が残った');
  // 不存在・0 バイト・checks に現れない は従来どおり不合格 → APPROVE を CONCERNS に降格（再検証は不要）
  const gone = { checks: [{ path: EV_PATH, exists: false, nonEmpty: false, rawLine: EV_PATH + ' MISSING' }], extraFilesInEvidenceDir: [] };
  const g = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, gone) });
  assert.ok(g.result.unresolvedFindings.some((x) => x.includes('不存在')));
  assert.ok(g.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'), '不存在で降格されない');
  assert.equal(callsBy(g.calls, /-recheck$/).length, 0, '不存在に再検証は不要');
  const empty = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^verify-evidence-/, { checks: [], extraFilesInEvidenceDir: [] })].concat(fbRoutes()) });
  assert.ok(empty.result.unresolvedFindings.some((x) => x.includes('検証結果に現れず')));
  assert.ok(empty.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'));
});

// ---- 文脈節減 ----

test('文脈節減: CLAUDE.md の @ 自動 import は contract.md のみ・モデル階層規約を参照する', async () => {
  const claude = await readFile(root + '../CLAUDE.md', 'utf8');
  const imports = [...claude.matchAll(/@\.claude\/docs\/[\w./-]+/g)].map((m) => m[0]);
  assert.deepEqual(imports, ['@.claude/docs/contract.md'], '自動 import が増えている（全 agent・全ターンの文脈に常駐する — model-routing.md §4）: ' + JSON.stringify(imports));
  assert.ok(claude.includes('model-routing.md'), 'CLAUDE.md がモデル階層規約を参照していない');
});

test('文脈節減: 外部 reviewer / engineer が自動 import に頼らず gates.md・review-loops.md へ到達できる（パス明示）', async () => {
  for (const wf of ['prototype.js', 'full-build.js']) {
    const src = await read('workflows/' + wf);
    assert.ok(src.includes(".claude/docs/gates.md") || src.includes("DOCS.gates") || src.includes("DOCS + '/gates.md"), wf + ' の CR-CODE プロンプトに gates.md のパスが無い');
    assert.ok(!/review-loops\.md の追記形式: iteration/.test(src) || src.includes("DOCS.reviewLoops + ' の追記形式"), wf + ': reviewer への review-loops.md 参照がベース名のみ（パス無し）');
  }
  for (const a of ['gameplay-engineer', 'ui-engineer']) {
    const body = await read('agents/' + a + '.md');
    assert.ok(/参照ドキュメント[\s\S]*\.claude\/docs\/gates\.md/.test(body), a + '.md の参照ドキュメント節に gates.md が無い（QA-PLAY 観点2・落とし穴規約の到達経路）');
  }
});

// ---- retro-e4 レビュー追随（PR 前 4 面レビュー: code-reviewer / silent-failure-hunter / adversarial+OSS / Codex） ----

test('verify-evidence(fail closed): 初回 missing + 不一致 の混在 — 再検証は不一致分だけを対象にし、初回の不存在は消えない → CONCERNS', async () => {
  const A = 'qa/evidence/a.png', B = 'qa/evidence/b.png';
  const qa = Object.assign({}, PROTO_QA_OK, { evidencePaths: [A, B] });
  const ev = (call) => call.label.endsWith('-recheck')
    ? { checks: [{ path: A, exists: true, nonEmpty: true, rawLine: A + ' 1234' }, { path: B, exists: true, nonEmpty: true, rawLine: B + ' 1234' }], extraFilesInEvidenceDir: [] } // 再検証 agent が A も「実在」と言い直しても
    : { checks: [{ path: A, exists: false, nonEmpty: false, rawLine: A + ' MISSING' }, { path: B, exists: true, nonEmpty: true, rawLine: 'basename 1234' }], extraFilesInEvidenceDir: [] };
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qa, ev) });
  const re = callsBy(p.calls, /^verify-evidence-round1-recheck$/)[0];
  assert.ok(re, '再検証が走らない');
  assert.ok(re.prompt.includes(JSON.stringify([B])) && !re.prompt.includes(A), '再検証の対象が不一致分（B）に絞られていない');
  assert.ok(p.result.unresolvedFindings.some((f) => f.includes(A + '（不存在')), '初回の不存在が再検証で消えた（fail open）: ' + JSON.stringify(p.result.unresolvedFindings));
  assert.ok(p.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.iteration === 1 && v.verdict === 'CONCERNS'), '不存在があるのに降格されない');
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^verify-evidence-/, ev)].concat(fbRoutes([], BATCH_OK, Object.assign({}, FB_QA_OK, { evidencePaths: [A, B] }))) });
  assert.ok(f.result.unresolvedFindings.some((x) => x.includes(A + '（不存在')));
  assert.ok(f.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'));
});

test('verify-evidence(rawLine 解析): "<path> MISSING" / サイズ 0 は申告 exists:true・nonEmpty:true と矛盾 → missing（降格）。ls -l 形式はフルパス末尾一致なら受理・別ディレクトリは未検証・サイズ 0 は降格', async () => {
  const gone = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: EV_PATH + ' MISSING' }], extraFilesInEvidenceDir: [] };
  const g = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, gone) });
  assert.ok(g.result.unresolvedFindings.some((x) => x.includes('rawLine が MISSING')), JSON.stringify(g.result.unresolvedFindings));
  assert.ok(g.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'));
  assert.equal(callsBy(g.calls, /-recheck$/).length, 0);
  const zero = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: EV_PATH + ' 0' }], extraFilesInEvidenceDir: [] };
  const z = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^verify-evidence-/, zero)].concat(fbRoutes()) });
  assert.ok(z.result.unresolvedFindings.some((x) => x.includes('rawLine のサイズ 0')));
  assert.ok(z.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'));
  // ls -l 形式（末尾がフルパス・第 5 フィールドがサイズ）は実行証拠として受理（E4 の haiku が選んだ形式 — 毎 round の再検証を量産しない）
  const lsStyle = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: '-rw-r--r--@ 1 u g 1234 Sep 2 03:00 ' + EV_PATH }], extraFilesInEvidenceDir: [] };
  const l = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, lsStyle) });
  assert.equal(callsBy(l.calls, /-recheck$/).length, 0, 'ls -l 形式が未検証扱いになった');
  assert.ok(!l.result.unresolvedFindings.some((x) => x.includes('VERIFY-UNCERTAIN')));
  assert.ok(l.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'APPROVE'));
  // ls -l でも別ディレクトリの同名ファイル（basename 一致）は未検証 → 再検証、サイズ 0 は矛盾で降格
  const lsOther = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: '-rw-r--r-- 1 u g 1234 Sep 2 03:00 qa/evidence/old/e.png' }], extraFilesInEvidenceDir: [] };
  const lo = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, lsOther) });
  assert.equal(callsBy(lo.calls, /^verify-evidence-round1-recheck$/).length, 1);
  assert.ok(lo.result.unresolvedFindings.some((x) => x.includes('[VERIFY-UNCERTAIN]')));
  const lsZero = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: '-rw-r--r-- 1 u g 0 Sep 2 03:00 ' + EV_PATH }], extraFilesInEvidenceDir: [] };
  const lz = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, lsZero) });
  assert.ok(lz.result.unresolvedFindings.some((x) => x.includes('rawLine のサイズ 0')));
  assert.ok(lz.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'));
});

test('CR-CODE(事前再特定): 申告 changedFiles にコード対象パスが無い → reviewer 起動前に locate-commit-<sid>-pre（人間可視チャネルに記録）/ 特定不能は reviewer を起こさず [BLOCKER]・bookkeep で確定', async () => {
  const badImpl = { commitHash: 'cafe01', changedFiles: ['state/reviews/s-01.md', 'state/stories.yaml'] };
  const { calls, result } = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^impl-s-01$/, badImpl), R(/^locate-commit-s-01-pre$/, { found: true, commitHash: 'deadbeef' })].concat(fbRoutes()) });
  const order = calls.map((c) => c.label);
  const preIdx = order.indexOf('locate-commit-s-01-pre');
  assert.ok(preIdx >= 0 && preIdx < order.findIndex((l) => /^cr-s-01-/.test(l)), '再特定が reviewer より先に走らない');
  assert.ok(promptsBy(calls, /^cr-s-01-1-relocated$/)[0].includes('git show deadbeef'), '再特定した hash でレビューされない');
  assert.ok(result.unresolvedFindings.some((f) => f.includes('S-01: 実装 agent の申告 changedFiles にコード対象パス')), '申告不一致が人間可視チャネルに載らない');
  assert.ok(result.verdictHistory.some((v) => v.artifact === 's-01' && v.verdict === 'APPROVE'));
  const nf = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^impl-s-01$/, badImpl), R(/^locate-commit-s-01-pre$/, { found: false, reason: 'コード変更のコミット無し' })].concat(fbRoutes()) });
  assert.equal(callsBy(nf.calls, /^(cr|sfh)-s-01-/).length, 0, '対象を固定できないのに reviewer を起こした');
  assert.ok(nf.result.unresolvedFindings.some((f) => f.startsWith('[BLOCKER] S-01: CR-CODE のレビュー対象コミットを固定できない') && f.includes('コード変更のコミット無し')), JSON.stringify(nf.result.unresolvedFindings));
  assert.ok(!nf.result.verdictHistory.some((v) => v.artifact === 's-01' && v.verdict === 'APPROVE'));
  assert.equal(callsBy(nf.calls, /^bookkeep-s-01/).length, 1, '未成立 story の status 確定（bookkeep）が走らない');
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([R(/^implement-S-01$/, Object.assign({}, badImpl, { summary: 's' })), R(/^locate-commit-S-01-pre$/, { found: false, reason: 'none' })]) });
  assert.equal(callsBy(p.calls, /^cr-(code|silent)-S-01-/).length, 0);
  assert.ok(p.result.unresolvedFindings.some((f) => f.startsWith('[BLOCKER] [CR-CODE][S-01] レビュー対象コミットを固定できない')));
  assert.ok(p.result.knownIssues.some((k) => k.includes('[CR-CODE][S-01] 実装 agent の申告 changedFiles')));
  assert.ok(p.result.verdictHistory.some((v) => v.gate === 'CR-CODE' && v.artifact === 'S-01' && v.verdict === 'CONCERNS'), 'prototype の未成立が verdictHistory に残らない');
});

test('CR-CODE(対象証明): commitHash 無しは再特定へ / 再特定 agent が同一 hash の包含を確認すれば observedCodeFiles 空でも APPROVE / 不正 hash は採用しない', async () => {
  const noHash = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^impl-s-01$/, { commitHash: '', changedFiles: [] }), R(/^locate-commit-s-01-pre$/, { found: false, reason: 'none' })].concat(fbRoutes()) });
  assert.ok(noHash.result.unresolvedFindings.some((f) => f.includes('S-01: 実装 agent が commitHash を返さなかった')));
  assert.ok(noHash.result.unresolvedFindings.some((f) => f.startsWith('[BLOCKER] S-01')));
  assert.equal(callsBy(noHash.calls, /^(cr|sfh)-s-01-/).length, 0);
  assert.ok(!noHash.result.verdictHistory.some((v) => v.artifact === 's-01' && v.verdict === 'APPROVE'));
  // 申告にコード対象パスが無い → 事前再特定 → agent が**同一 hash**にコード対象パスが含まれると確認 → -relocated でレビュー →
  // reviewer が observedCodeFiles を空で返しても再特定 agent の確認で APPROVE（同一 hash を拒否しない）
  const unproven = { findings: [], observedCodeFiles: [] };
  const stateOnly = { commitHash: 'abc1234', changedFiles: ['state/reviews/s-01.md'] };
  const u = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^impl-s-01$/, stateOnly), R(/^(cr|sfh)-s-01-/, unproven), R(/^locate-commit-s-01-pre$/, { found: true, commitHash: 'abc1234' })].concat(fbRoutes()) });
  assert.equal(callsBy(u.calls, /^locate-commit-s-01-pre$/).length, 1, '申告不一致で事前再特定が走らない');
  assert.equal(callsBy(u.calls, /^cr-s-01-1-relocated$/).length, 1, '同 hash 確認後のレビューが -relocated label で走らない');
  assert.equal(callsBy(u.calls, /^cr-s-01-1$/).length, 0, '再特定前の label でレビューが走った');
  assert.ok(u.result.verdictHistory.some((v) => v.artifact === 's-01' && v.iteration === 1 && v.verdict === 'APPROVE'), '同一 hash の包含確認が対象証明として認められない');
  assert.ok(!u.result.unresolvedFindings.some((f) => f.startsWith('[BLOCKER] S-01')));
  const garbage = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^cr-s-01-1$/, { findings: [], targetMismatch: true, observedCodeFiles: [] }), R(/^locate-commit-s-01-1$/, { found: true, commitHash: 'ではない' })].concat(fbRoutes()) });
  assert.equal(callsBy(garbage.calls, /-relocated$/).length, 0, '不正な hash で再レビューが走った');
  assert.ok(garbage.result.unresolvedFindings.some((f) => f.includes('不正な hash')));
  const pu = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([
    R(/^implement-S-01$/, Object.assign({}, stateOnly, { summary: 's' })),
    R(/^cr-(code|silent)-S-01-/, { verdict: 'APPROVE', findings: [], observedCodeFiles: [] }),
    R(/^locate-commit-S-01-pre$/, { found: true, commitHash: 'abc1234' }),
  ]) });
  assert.equal(callsBy(pu.calls, /^cr-code-S-01-iter1-relocated$/).length, 1);
  assert.ok(pu.result.verdictHistory.some((v) => v.artifact === 'S-01' && v.iteration === 1 && v.verdict === 'APPROVE'), 'prototype: 同一 hash の包含確認が対象証明として認められない');
  assert.ok(!pu.result.unresolvedFindings.some((f) => f.includes('[BLOCKER] [CR-CODE][S-01]')));
});

test('QA(自己申告の分離): qa-lead の APPROVE を workflow が証跡/目視で降格した場合は summary fix も qa-lead.md 違反記録も起こさず、次 round に降格理由を渡す', async () => {
  const qa = Object.assign({}, PROTO_QA_OK, { screenshotsVisuallyConfirmed: false, summary: '' });
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qa) });
  assert.equal(callsBy(p.calls, /^fix-qa-r1-summary$/).length, 0, 'workflow 降格に summary fix が起きた（opus 空振り）');
  assert.ok(!p.result.unresolvedFindings.some((f) => f.includes('qa-lead.md 違反')), '違反でないのに違反と記録');
  assert.ok(p.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS' && v.findings.some((x) => x.includes('自己申告 APPROVE'))), '降格の事実が履歴に残らない');
  const r2 = promptsBy(p.calls, /^qa-play-round2$/)[0];
  assert.ok(r2 && r2.includes('前 round（1）は workflow の証跡/目視機械検証で不合格'), 'round 2 に降格理由が渡らない');
  assert.ok(!promptsBy(p.calls, /^qa-play-round1$/)[0].includes('前 round'), 'round 1 に注記が入っている');
  const fq = Object.assign({}, FB_QA_OK, { screenshotsVisuallyConfirmed: false, summary: '' });
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, fq) });
  assert.equal(callsBy(f.calls, /^qa-fix-1-summary$/).length, 0);
  assert.ok(!f.result.unresolvedFindings.some((x) => x.includes('qa-lead.md 違反')));
  assert.ok(f.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.selfVerdict === 'APPROVE' && v.verdict === 'CONCERNS'), 'selfVerdict が履歴に無い');
  assert.ok(promptsBy(f.calls, /^qa-play-2$/)[0].includes('前 round（1）は workflow の証跡/目視機械検証で不合格'));
});

test('QA(minor のみ / engineer 外 assignee): minor だけの非 APPROVE は「不整合」として記録し summary fix に minor 一覧を渡す（違反ではない）/ engineer レーン外の assignee は脱落を [BLOCKER] で記録', async () => {
  const qa = Object.assign({}, PROTO_QA_OK, { verdict: 'CONCERNS', summary: 'minor color', bugs: [{ title: '色味', detail: 'd', severity: 'minor', assignee: 'ui-engineer' }] });
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qa) });
  const s = callsBy(p.calls, /^fix-qa-r1-summary$/);
  assert.equal(s.length, 1);
  assert.ok(s[0].prompt.includes('minor バグのみ') && s[0].prompt.includes('色味'));
  assert.ok(p.result.unresolvedFindings.some((f) => f.includes('minor バグのみで非APPROVE')) && !p.result.unresolvedFindings.some((f) => f.includes('qa-lead.md 違反')));
  assert.ok(p.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.findings.some((x) => x === '[minor] 色味')), 'bugs が verdictHistory に載らない');
  const stray = Object.assign({}, PROTO_QA_OK, { verdict: 'CONCERNS', summary: 'x', bugs: [{ title: 'SE 欠落', detail: 'd', severity: 'major', assignee: 'audio-designer' }] });
  const ps = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, stray) });
  assert.ok(ps.result.unresolvedFindings.some((f) => f.includes('[BLOCKER] [QA-PLAY] round 1: major バグ「SE 欠落」') && f.includes('audio-designer')), JSON.stringify(ps.result.unresolvedFindings));
  assert.equal(callsBy(ps.calls, /^fix-qa-r1-bugs-/).length, 0);
  const fbStray = Object.assign({}, FB_QA_OK, { verdict: 'CONCERNS', summary: 'x', bugs: [{ summary: 'SE 欠落', severity: 'major', assignee: 'audio-designer' }] });
  const fs = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, fbStray) });
  assert.ok(fs.result.unresolvedFindings.some((f) => f.includes('[BLOCKER] FullQA round 1: バグ「SE 欠落」')));
  assert.equal(callsBy(fs.calls, /^qa-fix-1-(gameplay|ui)-engineer$/).length, 0);
});

test('無記録経路の解消: bookkeep null・finalize-state 3 回 null（full-build）/ round 2 QA agent null で round 1 の記録が残る・REJECT 空指示（prototype）が unresolvedFindings に載る', async () => {
  const fb = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^cr-s-01-/, null), R(/^sfh-s-01-/, null), R(/^bookkeep-s-01/, null), R(/^finalize-state/, null)].concat(fbRoutes()) });
  assert.ok(fb.result.unresolvedFindings.some((f) => f.includes('S-01: CR-CODE 未APPROVE後の status 確定 agent（bookkeep）が失敗')), JSON.stringify(fb.result.unresolvedFindings));
  assert.equal(callsBy(fb.calls, /^finalize-state/).length, 3);
  assert.ok(fb.result.unresolvedFindings.some((f) => f.startsWith('[BLOCKER] Final: state/active.md 更新 agent（finalize-state）が 3 回とも失敗')));
  const qa1 = Object.assign({}, PROTO_QA_OK, { verdict: 'CONCERNS', summary: 'crash', criticalBugs: [{ title: '落下で即死', detail: 'd', assignee: 'gameplay-engineer' }] });
  const qaRoute = (call) => (call.label.startsWith('qa-play-round1') ? qa1 : null);
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qaRoute) });
  assert.ok(p.result.unresolvedFindings.some((f) => f.includes('round 2 の QA agent が失敗') && f.includes('直近の成立判定は round 1 の CONCERNS')), JSON.stringify(p.result.unresolvedFindings));
  assert.ok(p.result.knownIssues.some((k) => k.includes('QA-PLAY が MAX 2 周で APPROVE に到達せず')), 'round 1 の非 APPROVE が round 2 null で消えた');
  assert.ok(p.result.unresolvedFindings.some((f) => f.includes('非APPROVE要約: crash')));
  const cdRej = { verdict: 'REJECT', summary: 's', playInstructions: 'p', evidencePaths: [], knownIssues: [], rejectInstructions: [] };
  const r = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([R(/^cd-checkpoint-b/, cdRej)]) });
  assert.ok(r.result.unresolvedFindings.some((f) => f.includes('REJECT だが rejectInstructions が空')));
  assert.equal(callsBy(r.calls, /^cd-reject-fix/).length, 0);
});

test('contract §11 同期: ENGINE_PROFILES の codePathspec が contract の CR-CODE コード対象パス列（phaser/unity/unreal）と一致する（両 workflow）', async () => {
  const contract = await read('docs/contract.md');
  const rows = {};
  for (const m of contract.matchAll(/^\| `(phaser|unity|unreal)` \| (?:2D|3D) \|.*\| `([^`]+)`(?:（[^）]*）)? \|$/gm)) rows[m[1]] = m[2];
  assert.deepEqual(Object.keys(rows).sort(), ['phaser', 'unity', 'unreal'], 'contract §11 の engine 表をパースできない: ' + JSON.stringify(rows));
  for (const wf of ['full-build.js', 'prototype.js']) {
    const src = await read('workflows/' + wf);
    const specs = [...src.matchAll(/codePathspec: '([^']+)'/g)].map((m) => m[1]);
    assert.equal(specs.length, 3, wf + ' の codePathspec が 3 engine 分無い');
    for (const eng of ['phaser', 'unity', 'unreal']) {
      const contractPath = rows[eng].replace(/\/\*\*$/, '');
      assert.ok(specs.includes(contractPath), wf + ' の codePathspec に contract §11 の ' + eng + ' コード対象パス（' + contractPath + '）が無い');
    }
  }
});

// ---- retro-e4 第 2 ラウンド追随（f878f9e への code-reviewer 再レビュー F1〜F6・未固定だった fix のピン止め） ----

test('F1: reviewer が observedCodeFiles を空で返しても、実装/fix の申告 changedFiles か再特定 agent の確認がコード対象パスを示せば APPROVE（再特定・BLOCKER を起こさない）', async () => {
  const emptyObs = { findings: [], observedCodeFiles: [] };
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^(cr|sfh)-s-01-/, emptyObs)].concat(fbRoutes()) });
  assert.equal(callsBy(f.calls, /^locate-commit-s-01-/).length, 0, '申告済みなのに再特定が走った');
  assert.ok(f.result.verdictHistory.some((v) => v.artifact === 's-01' && v.iteration === 1 && v.verdict === 'APPROVE'));
  assert.ok(!f.result.unresolvedFindings.some((x) => x.includes('[BLOCKER] S-01')));
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([R(/^cr-(code|silent)-S-01-/, { verdict: 'APPROVE', findings: [], observedCodeFiles: [] })]) });
  assert.equal(callsBy(p.calls, /^locate-commit-S-01-/).length, 0);
  assert.ok(p.result.verdictHistory.some((v) => v.artifact === 'S-01' && v.verdict === 'APPROVE'));
  // 申告にコード対象パスが無く事前再特定で見つかった後は、reviewer が observedCodeFiles 空でも再特定 agent の確認で APPROVE 可
  const badImpl = { commitHash: 'cafe01', changedFiles: ['state/reviews/s-01.md'] };
  const r = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^impl-s-01$/, badImpl), R(/^locate-commit-s-01-pre$/, { found: true, commitHash: 'deadbeef' }), R(/^(cr|sfh)-s-01-/, emptyObs)].concat(fbRoutes()) });
  assert.ok(r.result.verdictHistory.some((v) => v.artifact === 's-01' && v.verdict === 'APPROVE'), '再特定で確認済みの対象が未証明扱いになった');
  // fix コミットの申告にコード対象パスが無ければ、次 iteration の対象証明は reviewer の observedCodeFiles に戻る（証明無し → 再特定 → BLOCKER）
  const q = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [
    R(/^cr-s-01-1$/, { findings: [{ summary: 'x', severity: 'minor' }], observedCodeFiles: CODE_FILES }),
    R(/^fix-s-01-1$/, { commitHash: 'f1x0001', changedFiles: ['state/reviews/s-01.md'] }),
    R(/^(cr|sfh)-s-01-2/, emptyObs),
  ].concat(fbRoutes()) });
  assert.equal(callsBy(q.calls, /^locate-commit-s-01-2$/).length, 1, 'fix コミットの申告にコード対象パスが無いのに対象証明済み扱い');
  assert.ok(!q.result.verdictHistory.some((v) => v.artifact === 's-01' && v.verdict === 'APPROVE'));
  // E4 の実害ケース（申告が state ファイルのみ・reviewer も未証明・再特定不能）は引き続き BLOCKER
  const e4 = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^impl-s-01$/, badImpl), R(/^locate-commit-s-01-pre$/, { found: false, reason: 'none' }), R(/^(cr|sfh)-s-01-/, emptyObs)].concat(fbRoutes()) });
  assert.ok(e4.result.unresolvedFindings.some((x) => x.startsWith('[BLOCKER] S-01')));
  assert.ok(!e4.result.verdictHistory.some((v) => v.artifact === 's-01' && v.verdict === 'APPROVE'));
});

test('F2: qa-lead 自己申告 APPROVE + minor バグのみ を workflow が降格した場合、minor を judge fix に流さない（full-build）', async () => {
  const qa = Object.assign({}, FB_QA_OK, { screenshotsVisuallyConfirmed: false, summary: '', bugs: [{ summary: '色味', severity: 'minor', assignee: 'ui-engineer' }] });
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, qa) });
  assert.equal(callsBy(f.calls, /^qa-fix-1-/).length, 0, 'minor のみ + workflow 降格で judge fix が走った');
  assert.equal(callsBy(f.calls, /^qa-play-2$/).length, 1, 'round 2 が走らない');
  assert.ok(!f.result.unresolvedFindings.some((x) => x.includes('qa-lead.md 違反')));
  // 自己申告 APPROVE でも major があれば（矛盾した返却）従来どおり修正レーンへ
  const qaMajor = Object.assign({}, qa, { bugs: [{ summary: 'HUD ずれ', severity: 'major', assignee: 'ui-engineer' }] });
  const g = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, qaMajor) });
  assert.equal(callsBy(g.calls, /^qa-fix-1-ui-engineer$/).length, 1);
});

test('F3: prototype の bugs に enum 外 severity（blocker）— 修正レーンに乗り、最終 round の残存も記録され、enum 外は knownIssues に注記', async () => {
  const qa = Object.assign({}, PROTO_QA_OK, { verdict: 'CONCERNS', summary: 'x', bugs: [
    { title: '落下即死', detail: 'd', severity: 'blocker', assignee: 'gameplay-engineer' },
    { title: '色', detail: 'd', severity: 'major', assignee: 'ui-engineer' },
  ] });
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qa) });
  const gp1 = callsBy(p.calls, /^fix-qa-r1-bugs-gameplay-engineer$/);
  assert.equal(gp1.length, 1, 'blocker が修正レーンに乗らない');
  assert.ok(gp1[0].prompt.includes('落下即死'));
  assert.ok(p.result.knownIssues.some((k) => k.includes('severity「blocker」が enum 外')));
  assert.ok(p.result.unresolvedFindings.some((f) => f.includes('未解決の major バグ: 落下即死')), '最終 round の blocker 残存が記録されない');
  assert.equal(callsBy(p.calls, /^fix-qa-r1-summary$/).length, 0);
});

test('F4: 戻り値 evidencePaths — prototype は QA 申告値 ∪ CD 選定（CD の部分集合だけにならない）/ full-build は QA 申告値そのもの', async () => {
  const qa = Object.assign({}, PROTO_QA_OK, { evidencePaths: [EV_PATH, 'qa/evidence/two.png', 'qa/evidence/three.png'] });
  const ev = { checks: qa.evidencePaths.map((pp) => ({ path: pp, exists: true, nonEmpty: true, rawLine: pp + ' 10' })), extraFilesInEvidenceDir: [] };
  const cd = { verdict: 'APPROVE', summary: 's', playInstructions: 'p', evidencePaths: ['qa/evidence/two.png', 'qa/evidence/cd-only.png'], knownIssues: [] };
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([R(/^cd-checkpoint-b/, cd)], BATCH_OK, qa, ev) });
  assert.deepEqual([...p.result.evidencePaths].sort(), [EV_PATH, 'qa/evidence/cd-only.png', 'qa/evidence/three.png', 'qa/evidence/two.png'].sort());
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes() });
  assert.deepEqual(f.result.evidencePaths, FB_QA_OK.evidencePaths, 'full-build の戻り値 evidencePaths が QA 申告値でない');
});

test('F5: 未固定だった fix のピン止め — criticalBugs の enum 外 assignee / 片側 reviewer の findings 保持 / prototype 未成立の CR-CODE 行 / concept CD 再判定 retries 2 / gen-* 冪等ガード / CD プロンプトの [VERIFY-UNCERTAIN] 注記', async () => {
  const qa = Object.assign({}, PROTO_QA_OK, { verdict: 'CONCERNS', summary: 'x', criticalBugs: [{ title: 'SE 欠落', detail: 'd', assignee: 'audio-designer' }] });
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qa) });
  const fx = callsBy(p.calls, /^fix-qa-r1-gameplay-engineer-0$/);
  assert.equal(fx.length, 1, 'enum 外 assignee の重大バグが gameplay-engineer に倒れない');
  assert.equal(fx[0].opts.agentType, 'gameplay-engineer');
  assert.ok(p.result.knownIssues.some((k) => k.includes('assignee「audio-designer」が engineer 以外')));
  const side = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [
    R(/^cr-s-01-1$/, { findings: [{ summary: '未使用 import', severity: 'minor' }], observedCodeFiles: [] }),
    R(/^sfh-s-01-1$/, { findings: [], targetMismatch: true, observedCodeFiles: [] }),
    R(/^locate-commit-s-01-1$/, { found: false, reason: 'none' }),
  ].concat(fbRoutes()) });
  const row = side.result.verdictHistory.find((v) => v.gate === 'CR-CODE' && v.artifact === 's-01' && v.iteration === 1);
  assert.ok(row && row.verdict === 'CONCERNS' && row.findings.some((x) => x.includes('[minor] 未使用 import')), '片側 reviewer の findings が履歴から消えた: ' + JSON.stringify(row));
  const ps = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([
    R(/^cr-code-S-01-iter1$/, { verdict: 'CONCERNS', findings: ['未使用 import'], observedCodeFiles: [] }),
    R(/^cr-silent-S-01-iter1$/, { verdict: 'CONCERNS', findings: [], targetMismatch: true, observedCodeFiles: [] }),
    R(/^locate-commit-S-01-iter1$/, { found: false, reason: 'none' }),
  ]) });
  const prow = ps.result.verdictHistory.find((v) => v.gate === 'CR-CODE' && v.artifact === 'S-01' && v.iteration === 1);
  assert.ok(prow && prow.verdict === 'CONCERNS' && prow.findings.some((x) => x === '未使用 import'), 'prototype の未成立行/片側 findings が無い: ' + JSON.stringify(prow));
  const c = await runWorkflow(WF('concept-design.js'), { args: CD_ARGS, routes: [
    R(/^CD-CHECKPOINT 判定/, { verdict: 'REJECT', findings: ['骨子が弱い'], fixes: [{ assignee: 'game-designer', artifact: 'design/concept.md', instruction: 'ピラーを絞る' }] }),
    R(/^CD-CHECKPOINT 再判定-retry2$/, { verdict: 'CONCERNS', findings: [], fixes: [] }),
    R(/^CD-CHECKPOINT 再判定/, null),
  ] });
  assert.deepEqual(callsBy(c.calls, /^CD-CHECKPOINT 再判定/).map((x) => x.label), ['CD-CHECKPOINT 再判定', 'CD-CHECKPOINT 再判定-retry', 'CD-CHECKPOINT 再判定-retry2']);
  assert.equal(c.result.verdict, 'CONCERNS');
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes() });
  assert.ok(promptsBy(f.calls, /^gen-images-1$/)[0].includes('冪等ガード'), 'gen-* に冪等ガードが無い');
  assert.ok(promptsBy(f.calls, /^cd-checkpoint-1$/)[0].includes('[VERIFY-UNCERTAIN] 項目はオーケストレータが後段で'), 'CD プロンプトに [VERIFY-UNCERTAIN] 注記が無い');
  assert.ok(promptsBy(p.calls, /^cd-checkpoint-b$/)[0].includes('[VERIFY-UNCERTAIN] 項目はオーケストレータが後段で'));
});

test('F6: 対象コミット不成立で確定した story の bookkeep は「MAX_ITER 到達」ではなく不成立の理由を stories.yaml に残す', async () => {
  const badImpl = { commitHash: 'cafe01', changedFiles: ['state/reviews/s-01.md'] };
  const nf = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^impl-s-01$/, badImpl), R(/^locate-commit-s-01-pre$/, { found: false, reason: 'none' })].concat(fbRoutes()) });
  const bk = promptsBy(nf.calls, /^bookkeep-s-01$/)[0];
  assert.ok(bk && bk.includes('レビュー対象コミット不成立') && !bk.includes('MAX_ITER 到達'), 'bookkeep の理由文言が実態と違う');
  const max = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^(cr|sfh)-s-01-/, { findings: [{ summary: 'x', severity: 'major' }], observedCodeFiles: CODE_FILES })].concat(fbRoutes()) });
  assert.ok(promptsBy(max.calls, /^bookkeep-s-01$/)[0].includes('MAX_ITER 到達'));
});

// ---- retro-e4 Codex 第 2 ラウンド追随（P1: APPROVE と矛盾する返却の正規化 / P2: rawLine 一次情報 / P2: findings 付き未証明レビュー） ----

test('Codex P1: qa-lead が APPROVE と同時に major/重大バグや不合格 acceptance を返したら CONCERNS に正規化して修正レーンへ（両 workflow）', async () => {
  const pq = Object.assign({}, PROTO_QA_OK, { bugs: [{ title: 'HUD ずれ', detail: 'd', severity: 'major', assignee: 'ui-engineer' }] });
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, pq) });
  assert.ok(p.result.unresolvedFindings.some((f) => f.includes('APPROVE と同時に重大/major バグ')), JSON.stringify(p.result.unresolvedFindings));
  assert.equal(callsBy(p.calls, /^fix-qa-r1-bugs-ui-engineer$/).length, 1, '矛盾した APPROVE の major が修正されない');
  assert.ok(!p.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.iteration === 1 && v.verdict === 'APPROVE'), 'major 付き APPROVE が履歴に APPROVE で残った');
  const pc = Object.assign({}, PROTO_QA_OK, { criticalBugs: [{ title: 'クラッシュ', detail: 'd', assignee: 'gameplay-engineer' }] });
  const c = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, pc) });
  assert.equal(callsBy(c.calls, /^fix-qa-r1-gameplay-engineer-0$/).length, 1);
  const fq = Object.assign({}, FB_QA_OK, { bugs: [{ summary: 'HUD ずれ', severity: 'major', assignee: 'ui-engineer' }] });
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, fq) });
  assert.ok(f.result.unresolvedFindings.some((x) => x.includes('APPROVE と同時に blocker/major バグ')));
  assert.equal(callsBy(f.calls, /^qa-fix-1-ui-engineer$/).length, 1);
  assert.ok(f.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.iteration === 1 && v.verdict === 'CONCERNS' && v.selfVerdict === 'APPROVE'));
  // minor のみの APPROVE は正規化しない（qa-lead.md: minor は APPROVE を妨げない）
  const fm = Object.assign({}, FB_QA_OK, { bugs: [{ summary: '色味', severity: 'minor', assignee: 'ui-engineer' }] });
  const m = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: fbRoutes([], BATCH_OK, fm) });
  assert.ok(m.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'APPROVE'));
  assert.equal(callsBy(m.calls, /^qa-fix-/).length, 0);
});

test('Codex P2(rawLine 一次情報): 解析できた rawLine が実在を示せば申告 exists/nonEmpty:false でも降格しない・再検証も不要', async () => {
  const contra = { checks: [{ path: EV_PATH, exists: false, nonEmpty: false, rawLine: EV_PATH + ' 1234' }], extraFilesInEvidenceDir: [] };
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, contra) });
  assert.equal(callsBy(p.calls, /-recheck$/).length, 0);
  assert.ok(!p.result.unresolvedFindings.some((x) => x.includes('機械検証不合格') || x.includes('VERIFY-UNCERTAIN')), JSON.stringify(p.result.unresolvedFindings));
  assert.ok(p.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'APPROVE'), '実在を示す rawLine があるのに申告 boolean で降格された');
  assert.ok(p.logs.some((l) => l.includes('生出力を採用')), '矛盾が log に残らない');
  // rawLine が解析不能なら申告 boolean に頼る（不存在申告 → 降格）
  const noRaw = { checks: [{ path: EV_PATH, exists: false, nonEmpty: false, rawLine: 'n/a' }], extraFilesInEvidenceDir: [] };
  const n = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^verify-evidence-/, noRaw)].concat(fbRoutes()) });
  assert.ok(n.result.unresolvedFindings.some((x) => x.includes(EV_PATH + '（不存在）')));
  assert.ok(n.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'));
});

test('Codex P2(対象未証明 + findings): findings 付きでも対象証明が無いレビューは fix を起こさず再特定 → 不能なら BLOCKER（side findings は履歴に残す）', async () => {
  const q = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [
    R(/^cr-s-01-1$/, { findings: [{ summary: 'x', severity: 'minor' }], observedCodeFiles: CODE_FILES }),
    R(/^fix-s-01-1$/, { commitHash: 'f1x0001', changedFiles: ['state/reviews/s-01.md'] }), // fix コミットの申告にコード対象パス無し
    R(/^(cr|sfh)-s-01-2/, { findings: [{ summary: 'y', severity: 'major' }], observedCodeFiles: [] }),
  ].concat(fbRoutes()) });
  assert.equal(callsBy(q.calls, /^locate-commit-s-01-2$/).length, 1, '対象未証明の findings 付きレビューで再特定が走らない');
  assert.equal(callsBy(q.calls, /^fix-s-01-2$/).length, 0, '対象未証明のレビュー findings で fix agent が起動した');
  const row = q.result.verdictHistory.find((v) => v.gate === 'CR-CODE' && v.artifact === 's-01' && v.iteration === 2);
  assert.ok(row && row.verdict === 'CONCERNS' && row.findings.some((x) => x.includes('[major] y')), 'side findings が履歴から消えた: ' + JSON.stringify(row));
  assert.ok(q.result.unresolvedFindings.some((x) => x.startsWith('[BLOCKER] S-01') && x.includes('対象未証明')));
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([
    R(/^cr-code-S-01-iter1$/, { verdict: 'CONCERNS', findings: ['x'], observedCodeFiles: [] }),
    R(/^fix-S-01-iter1$/, { commitHash: 'f1x0001', changedFiles: ['state/reviews/s-01.md'], summary: 's' }),
    R(/^cr-(code|silent)-S-01-iter2/, { verdict: 'CONCERNS', findings: ['y'], observedCodeFiles: [] }),
    R(/^locate-commit-S-01-iter2$/, { found: false, reason: 'none' }),
  ]) });
  assert.equal(callsBy(p.calls, /^locate-commit-S-01-iter2$/).length, 1);
  assert.equal(callsBy(p.calls, /^fix-S-01-iter2$/).length, 0);
  assert.ok(p.result.unresolvedFindings.some((x) => x.includes('[BLOCKER] [CR-CODE][S-01]') && x.includes('対象未証明')));
});
