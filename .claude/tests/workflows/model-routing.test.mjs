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
const PROTO_QA_OK = { verdict: 'APPROVE', criticalBugs: [], failedAcceptance: [], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true };
const EV_OK = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: '-rw-r--r--  1 u  g  1234 Sep  2 03:00 qa/evidence/e.png' }], extraFilesInEvidenceDir: [] };
const BATCH_OK = { ok: true, fixedNotes: [], unresolved: [] };
const protoRoutes = (extra = [], batch = BATCH_OK, qa = PROTO_QA_OK, ev = EV_OK) => extra.concat([
  R(/^setup-scaffold-stories/, SETUP),
  R(/^setup-crosscheck-stories/, CROSSCHECK),
  R(/^qa-play-round/, qa),
  R(/^verify-evidence-round/, ev),
  R(/^batch-verify-/, batch),
]);
const PROTO_ARGS = { reviewMode: 'lean', engine: 'phaser' };

const gp = (id, title) => ({ id, title: title || id, assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: 'a' });
const ui = (id, title) => ({ id, title: title || id, assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' });
const FB_QA_OK = { verdict: 'APPROVE', bugs: [], failedAcceptance: [], evidencePaths: [EV_PATH], screenshotsVisuallyConfirmed: true };
const fbRoutes = (extra = [], batch = BATCH_OK, qa = FB_QA_OK) => extra.concat([
  R(/^replan-stories$/, { stories: [gp('S-01'), ui('S-02'), gp('S-03')] }),
  R(/^polish-plan$/, { stories: [gp('S-10'), ui('S-11')] }),
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

test('verify-evidence: rawLine にパス名を含む実行出力が無い check は不合格扱い（APPROVE が CONCERNS に降格）', async () => {
  const fake = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: 'ok' }], extraFilesInEvidenceDir: [] };
  const p = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, fake) });
  assert.ok(p.result.unresolvedFindings.some((f) => f.includes('rawLine に当該パスの実行出力なし')), JSON.stringify(p.result.unresolvedFindings));
  assert.ok(p.result.verdictHistory.some((v) => v.gate === 'QA-PLAY' && v.verdict === 'CONCERNS'), '擬装 rawLine で APPROVE が通った');
  const f = await runWorkflow(WF('full-build.js'), { args: FB_ARGS, routes: [R(/^verify-evidence-/, fake)].concat(fbRoutes()) });
  assert.ok(f.result.unresolvedFindings.some((x) => x.includes('rawLine に当該パスの実行出力なし')));
  // basename 一致では別ディレクトリの実在ファイルの行を流用できる（Codex P2）— フルパス一致を要求
  const collide = { checks: [{ path: EV_PATH, exists: true, nonEmpty: true, rawLine: '-rw-r--r-- 1 u g 1234 Sep 2 03:00 qa/evidence/old/e.png' }], extraFilesInEvidenceDir: [] };
  const c = await runWorkflow(WF('prototype.js'), { args: PROTO_ARGS, routes: protoRoutes([], BATCH_OK, PROTO_QA_OK, collide) });
  assert.ok(c.result.unresolvedFindings.some((x) => x.includes('rawLine に当該パスの実行出力なし')), 'basename 衝突の rawLine が通った');
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
