// モデル階層ルーティング（.claude/docs/model-routing.md）の機械同期テスト。
// 階層表 ↔ agents/*.md frontmatter、workflow の全 agent() 呼び出しの「セッションモデル非継承」不変条件、
// mechanical 階層のラベル、段階的エスカレーション、CLAUDE.md の自動 import 縮小、tokenUsage 計測。
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { runWorkflow, callsBy, promptsBy } from './harness.mjs';

const root = new URL('../../', import.meta.url).pathname; // .claude/
const read = (p) => readFile(root + p, 'utf8');
const WF = (n) => root + 'workflows/' + n;
const R = (match, reply) => ({ match, reply });
const MODELS = ['haiku', 'sonnet', 'opus'];
const HARNESS_AGENTS = [
  'creative-director', 'tech-director', 'game-designer', 'art-director', 'audio-designer',
  'gameplay-engineer', 'ui-engineer', 'design-reviewer', 'art-reviewer', 'qa-lead',
];
const MECHANICAL = /^(verify-evidence-|setup-crosscheck-stories|bookkeep-|close-|finalize-state|replan-extract)/;

async function routingTable() {
  const doc = await read('docs/model-routing.md');
  const rows = [...doc.matchAll(/^\| ([a-z-]+) \| (judge|producer|mechanical) \| (opus|sonnet|haiku) \|$/gm)];
  const table = {};
  for (const m of rows) table[m[1]] = { tier: m[2], model: m[3] };
  return table;
}

// ---- fixtures（既存テストと同じ最小応答） ----
const PROTO_ARGS = { reviewMode: 'lean', engine: 'phaser' };
const SETUP = {
  prototypeStories: [
    { id: 'S-01', title: 'メタ進行永続化', assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-02', title: 'Title シーン', assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-03', title: 'Menu シーン', assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-04', title: '環境', assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: '可視の地面/背景・ライト・カメラ構図と画面レイアウトが確定している' },
  ],
  titleStoryId: 'S-02', menuStoryId: 'S-03', metaPersistenceStoryId: 'S-01', environmentStoryId: 'S-04',
};
const CROSSCHECK = { found: [
  { id: 'S-02', exists: true, assignee: 'ui-engineer', phase: 'prototype' },
  { id: 'S-03', exists: true, assignee: 'ui-engineer', phase: 'prototype' },
  { id: 'S-01', exists: true, assignee: 'gameplay-engineer', phase: 'prototype' },
  { id: 'S-04', exists: true, assignee: 'gameplay-engineer', phase: 'prototype', acceptance: '可視の地面/背景・ライト・カメラ構図と画面レイアウトが確定している' },
] };
const PROTO_QA_OK = { verdict: 'APPROVE', criticalBugs: [], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true };
const EV_OK = { checks: [{ path: 'qa/evidence/e.png', exists: true, nonEmpty: true }], extraFilesInEvidenceDir: [] };
const BATCH_OK = { ok: true, fixedNotes: [], unresolved: [] };
const protoRoutes = (extra = [], batch = BATCH_OK, qa = PROTO_QA_OK) => extra.concat([
  R(/^setup-scaffold-stories/, SETUP),
  R(/^setup-crosscheck-stories/, CROSSCHECK),
  R(/^qa-play-round/, qa),
  R(/^verify-evidence-round/, EV_OK),
  R(/^batch-verify-/, batch),
]);

const FB_ARGS = { reviewMode: 'lean', engine: 'phaser', checkpointBFeedbackPath: 'state/checkpoint-b-feedback.md' };
const gp = (id) => ({ id, title: id, assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: 'a' });
const ui = (id) => ({ id, title: id, assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' });
const FB_QA_OK = { verdict: 'APPROVE', bugs: [], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true };
const fbRoutes = (extra = [], batch = BATCH_OK, qa = FB_QA_OK) => extra.concat([
  R(/^replan-stories$/, { stories: [gp('S-01'), ui('S-02')] }),
  R(/^polish-plan$/, { stories: [gp('S-10')] }),
  R(/^qa-play-/, qa),
  R(/^verify-evidence-/, EV_OK),
  R(/^batch-verify-/, batch),
]);
const CD_ARGS = { briefPath: 'design/brief.md', reviewMode: 'lean', engine: 'phaser' };

const RUNS = [
  { name: 'prototype.js', args: PROTO_ARGS, routes: protoRoutes() },
  { name: 'full-build.js', args: FB_ARGS, routes: fbRoutes() },
  { name: 'concept-design.js', args: CD_ARGS, routes: [] },
];

// ---- 階層表 ↔ frontmatter ----

test('階層表: harness 10 体すべてが model-routing.md の割当表にあり、agents/*.md の model frontmatter と一致する', async () => {
  const table = await routingTable();
  for (const a of HARNESS_AGENTS) {
    assert.ok(table[a], 'model-routing.md の割当表に ' + a + ' が無い');
    const fm = (await read('agents/' + a + '.md')).match(/^model:\s*(\S+)\s*$/m);
    assert.ok(fm, a + '.md に model frontmatter が無い（セッションモデル継承 = オーケストレータ帯で実作業）');
    assert.equal(fm[1], table[a].model, a + ': frontmatter model が階層表とドリフト');
  }
  const files = (await readdir(root + 'agents')).filter((f) => f.endsWith('.md')).map((f) => f.replace(/\.md$/, ''));
  assert.deepEqual([...files].sort(), [...HARNESS_AGENTS].sort(), 'agents/ の実体が contract §2 の 10 体と一致しない');
});

test('階層表: workflow の TIER 定数が model-routing.md §1 の tier→model と一致する', async () => {
  const doc = await read('docs/model-routing.md');
  const tierModel = {};
  for (const m of doc.matchAll(/^\| `(judge|producer|mechanical)` \| `(opus|sonnet|haiku)` \|/gm)) tierModel[m[1]] = m[2];
  assert.deepEqual(Object.keys(tierModel).sort(), ['judge', 'mechanical', 'producer']);
  for (const wf of ['prototype.js', 'full-build.js', 'concept-design.js']) {
    const src = await read('workflows/' + wf);
    const m = src.match(/const TIER = \{ judge: '(\w+)', producer: '(\w+)', mechanical: '(\w+)' \};/);
    assert.ok(m, wf + ' に TIER 定数が無い');
    assert.deepEqual({ judge: m[1], producer: m[2], mechanical: m[3] }, tierModel, wf + ' の TIER が階層表とドリフト');
  }
});

// ---- 不変条件: セッションモデル非継承 ----

for (const run of RUNS) {
  test('不変条件(' + run.name + '): 全 agent() 呼び出しが harness agentType か明示 model を持つ（オーケストレータ帯で実作業しない）', async () => {
    const { calls } = await runWorkflow(WF(run.name), { args: run.args, routes: run.routes });
    assert.ok(calls.length > 5, 'agent 呼び出しが少なすぎる（fixture 不整合）: ' + calls.length);
    for (const c of calls) {
      const o = c.opts || {};
      const harness = HARNESS_AGENTS.includes(o.agentType);
      const explicit = MODELS.includes(o.model);
      assert.ok(harness || explicit, run.name + ' label=' + c.label + ': agentType=' + o.agentType + ' model=' + o.model + ' はセッションモデルを継承する');
      if ('model' in o) assert.ok(MODELS.includes(o.model), run.name + ' label=' + c.label + ': model が不正/undefined（' + o.model + '）');
      if (String(o.agentType || '').startsWith('pr-review-toolkit:')) {
        assert.equal(o.model, 'sonnet', run.name + ' label=' + c.label + ': 外部 reviewer は producer 階層（sonnet）を明示する');
      }
    }
  });

  test('mechanical(' + run.name + '): 実在確認・突合・状態更新ラベルは haiku + effort low', async () => {
    const { calls } = await runWorkflow(WF(run.name), { args: run.args, routes: run.routes });
    const mech = calls.filter((c) => MECHANICAL.test(c.label));
    if (run.name !== 'concept-design.js') assert.ok(mech.length > 0, run.name + ' に mechanical ラベルが1つも無い');
    for (const c of mech) {
      assert.equal(c.opts.model, 'haiku', run.name + ' label=' + c.label + ': mechanical は haiku');
      assert.equal(c.opts.effort, 'low', run.name + ' label=' + c.label + ': mechanical は effort low');
    }
  });

  test('tokenUsage(' + run.name + '): phase 境界ごとに記録され戻り値に含まれる', async () => {
    const { result, phases } = await runWorkflow(WF(run.name), { args: run.args, routes: run.routes });
    assert.ok(Array.isArray(result.tokenUsage), run.name + ' の戻り値に tokenUsage が無い');
    assert.deepEqual(result.tokenUsage.map((t) => t.phase), phases, run.name + ': tokenUsage の phase 列が phase() 呼び出し列と一致しない');
    for (const t of result.tokenUsage) assert.equal(typeof t.outputTokensBefore, 'number');
  });
}

// ---- 段階的エスカレーション ----

test('エスカレーション(prototype): CR-CODE fix は iter1=producer 継承・iter2=judge(opus) + 根本原因注記', async () => {
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
  assert.equal(f2.opts.agentType, 'gameplay-engineer', 'エスカレーションでも役割宣言（agentType）は据え置き');
  assert.ok(f2.prompt.startsWith('【エスカレーション'), 'iter2 の fix プロンプトに根本原因注記が前置されない');
  assert.ok(!f1.prompt.includes('【エスカレーション'), 'iter1 に注記が混入');
});

test('エスカレーション(prototype): QA fix（重大バグ・acceptance）は judge(opus)', async () => {
  const qa = { verdict: 'CONCERNS', criticalBugs: [{ title: 'クラッシュ', detail: 'd', assignee: 'ui-engineer' }], failedAcceptance: ['S-01: x'], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true };
  const { calls } = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, qa) });
  const bug = callsBy(calls, /^fix-qa-r1-ui-engineer-0$/)[0];
  const acc = callsBy(calls, /^fix-qa-acceptance-r1$/)[0];
  assert.ok(bug && acc);
  assert.equal(bug.opts.model, 'opus');
  assert.equal(bug.opts.agentType, 'ui-engineer');
  assert.equal(acc.opts.model, 'opus');
});

test('エスカレーション(prototype): batch-verify 不合格 → judge で1回再試行・fixedNotes 合算・合格なら警告なし', async () => {
  const batch = (call) => call.label.endsWith('-escalate')
    ? { ok: true, fixedNotes: ['2回目: 参照修正'], unresolved: [] }
    : { ok: false, fixedNotes: ['1回目: import 整理'], unresolved: ['ui 参照切れ'] };
  const { calls, result } = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], batch) });
  const esc = callsBy(calls, /^batch-verify-build-escalate$/)[0];
  assert.ok(esc, 'escalate 呼び出しが無い');
  assert.equal(esc.opts.model, 'opus');
  assert.equal(esc.opts.agentType, 'gameplay-engineer');
  assert.ok(esc.prompt.includes('ui 参照切れ'), '前回の未解決が再試行プロンプトに渡らない');
  assert.ok(result.knownIssues.some((k) => k.includes('1回目: import 整理')), '1回目の fixedNotes が消えた');
  assert.ok(result.knownIssues.some((k) => k.includes('2回目: 参照修正')), '2回目の fixedNotes が消えた');
  assert.ok(!promptsBy(calls, /^integrate-assets$/)[0].includes('警告'), '再試行で合格したのに警告が残る');
  assert.ok(!result.unresolvedFindings.some((f) => f.includes('batch-verify')), JSON.stringify(result.unresolvedFindings));
});

test('エスカレーション(prototype): batch-verify 合格なら escalate は走らない / 再試行 null は BLOCKER 記録', async () => {
  const ok = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes() });
  assert.equal(callsBy(ok.calls, /-escalate/).length, 0);
  const batch = (call) => call.label.includes('-escalate') ? null : { ok: false, fixedNotes: [], unresolved: ['x'] };
  const ng = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], batch) });
  assert.ok(ng.result.unresolvedFindings.some((f) => f.includes('エスカレーション再試行 agent が結果を返さなかった')));
  assert.ok(ng.result.unresolvedFindings.some((f) => f.includes('[batch-verify] x')), '初回の unresolved が消えた');
});

test('エスカレーション(prototype): 初回 null（agentR リトライ後も）は judge で再試行し回復する / 両方 null は BLOCKER 1件', async () => {
  const batch = (call) => call.label.endsWith('-escalate') ? { ok: true, fixedNotes: ['judge が修復'], unresolved: [] } : null;
  const rec = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], batch) });
  assert.ok(callsBy(rec.calls, /^batch-verify-build-escalate$/)[0], 'null 後の escalate が無い');
  assert.ok(rec.result.knownIssues.some((k) => k.includes('judge が修復')));
  assert.ok(!rec.result.unresolvedFindings.some((f) => f.includes('batch-verify')), JSON.stringify(rec.result.unresolvedFindings));
  const dead = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], () => null) });
  const bl = dead.result.unresolvedFindings.filter((f) => f.includes('バッチ検証'));
  assert.equal(bl.length, 1, '両方 null で BLOCKER が二重記録: ' + JSON.stringify(bl));
  assert.ok(bl[0].includes('結果を返さなかった'));
});

test('エスカレーション(full-build): CR-CODE fix iter2=opus・QA fix=opus・batch-verify escalate=opus・fixedNotes 合算', async () => {
  const batch = (call) => call.label.endsWith('-escalate')
    ? { ok: true, fixedNotes: ['2回目'], unresolved: [] }
    : { ok: false, fixedNotes: ['1回目'], unresolved: [] };
  const qa = { verdict: 'CONCERNS', bugs: [{ summary: 'b', severity: 'major', assignee: 'gameplay-engineer' }], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true, summary: 'ng' };
  const routes = fbRoutes([
    R(/^cr-s-01-|^sfh-s-01-/, { findings: [{ summary: 'x', severity: 'major' }] }),
  ], batch, qa);
  const { calls, result } = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes });
  const f1 = callsBy(calls, /^fix-s-01-1$/)[0];
  const f2 = callsBy(calls, /^fix-s-01-2$/)[0];
  assert.ok(f1 && f2);
  assert.ok(!('model' in f1.opts));
  assert.equal(f2.opts.model, 'opus');
  assert.ok(f2.prompt.startsWith('【エスカレーション'));
  const qf = callsBy(calls, /^qa-fix-1-gameplay-engineer$/)[0];
  assert.ok(qf); assert.equal(qf.opts.model, 'opus');
  for (const ph of ['build', 'polish']) {
    const esc = callsBy(calls, new RegExp('^batch-verify-' + ph + '-escalate$'))[0];
    assert.ok(esc, ph + ' の escalate が無い'); assert.equal(esc.opts.model, 'opus');
  }
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[batch-verify修正・CR-CODE非経由] 1回目')));
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[batch-verify修正・CR-CODE非経由] 2回目')));
  assert.ok(!result.unresolvedFindings.some((f) => f.includes('[BLOCKER]') && f.includes('batch-verify')), '再試行で合格したのに BLOCKER が残る');
});

test('エスカレーション(concept-design): reviewLoop の最終 iteration revise は judge(opus)・途中は producer 継承', async () => {
  const routes = [R(/^DR-CONCEPT review #/, { verdict: 'CONCERNS', findings: ['ピラーが無内容'] })];
  const { calls } = await runWorkflow(WF('concept-design.js'), { args: CD_ARGS, routes });
  const r1 = callsBy(calls, /^DR-CONCEPT revise #1$/)[0];
  const r3 = callsBy(calls, /^DR-CONCEPT revise #3$/)[0];
  assert.ok(r1 && r3, 'revise が3周走らない');
  assert.ok(!('model' in r1.opts));
  assert.equal(r3.opts.model, 'opus');
  assert.equal(r3.opts.agentType, 'game-designer');
  assert.ok(r3.prompt.startsWith('【エスカレーション'));
  assert.ok(!r1.prompt.includes('【エスカレーション'));
});

// ---- 文脈節減 ----

test('文脈節減: CLAUDE.md の @ 自動 import は contract.md のみ', async () => {
  const claude = await readFile(root + '../CLAUDE.md', 'utf8');
  const imports = [...claude.matchAll(/@\.claude\/docs\/[\w./-]+/g)].map((m) => m[0]);
  assert.deepEqual(imports, ['@.claude/docs/contract.md'], '自動 import が増えている（全 agent・全ターンの文脈に常駐する — model-routing.md §4）: ' + JSON.stringify(imports));
  assert.ok(claude.includes('model-routing.md'), 'CLAUDE.md がモデル階層規約を参照していない');
});
