// prototype.js の並列化まわりの分岐・配線テスト（DSL スタブハーネス）
import test from 'node:test';
import assert from 'node:assert/strict';
import { runWorkflow, callsBy, promptsBy } from './harness.mjs';

const WF = new URL('../../workflows/prototype.js', import.meta.url).pathname;
const ARGS = { reviewMode: 'lean', engine: 'phaser' };
const R = (match, reply) => ({ match, reply });

const SETUP = {
  prototypeStories: [
    { id: 'S-01', title: 'メタ進行永続化', assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-02', title: 'Title シーン', assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-03', title: 'Menu シーン', assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' },
    { id: 'S-04', title: 'コアループ', assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: 'a' },
  ],
  titleStoryId: 'S-02',
  menuStoryId: 'S-03',
  metaPersistenceStoryId: 'S-01',
};
const CROSSCHECK = {
  found: [
    { id: 'S-02', exists: true, assignee: 'ui-engineer', phase: 'prototype' },
    { id: 'S-03', exists: true, assignee: 'ui-engineer', phase: 'prototype' },
    { id: 'S-01', exists: true, assignee: 'gameplay-engineer', phase: 'prototype' },
  ],
};
const QA_OK = { verdict: 'APPROVE', criticalBugs: [], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true };
const EV_OK = { checks: [{ path: 'qa/evidence/e.png', exists: true, nonEmpty: true }], extraFilesInEvidenceDir: [] };

function baseRoutes(batchReply, qaReply) {
  return [
    R(/^setup-scaffold-stories$/, SETUP),
    R(/^setup-crosscheck-stories$/, CROSSCHECK),
    R(/^qa-play-round/, qaReply || QA_OK),
    R(/^verify-evidence-round/, EV_OK),
    R(/^batch-verify-/, batchReply),
  ];
}

test('happy path: レーン合流後に batch-verify 1回・Integrate/QA に警告なし', async () => {
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }) });
  assert.equal(callsBy(calls, /^batch-verify-build$/).length, 1);
  // batch-verify は全 implement 完了後（合流後）に呼ばれる
  const order = calls.map((c) => c.label);
  const lastImpl = Math.max(...['implement-S-01', 'implement-S-02', 'implement-S-03', 'implement-S-04'].map((l) => order.indexOf(l)));
  assert.ok(order.indexOf('batch-verify-build') > lastImpl, 'batch-verify がレーン完了前に走っている');
  assert.ok(!promptsBy(calls, /^integrate-assets$/)[0].includes('警告'));
  assert.ok(result.unresolvedFindings.every((f) => !f.includes('batch-verify')));
});

test('レーン分配: gameplay=S-01,S-04 / ui=S-02,S-03・レーン内順序保存', async () => {
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }) });
  const order = calls.map((c) => c.label).filter((l) => l.startsWith('implement-'));
  assert.deepEqual([...order].sort(), ['implement-S-01', 'implement-S-02', 'implement-S-03', 'implement-S-04']);
  assert.ok(order.indexOf('implement-S-01') < order.indexOf('implement-S-04'), 'gameplay レーン内順序');
  assert.ok(order.indexOf('implement-S-02') < order.indexOf('implement-S-03'), 'ui レーン内順序');
});

test('batch-verify 不合格: Integrate と QA に警告注入 + BLOCKER 蓄積', async () => {
  const routes = baseRoutes({ ok: false, fixedNotes: [], unresolved: ['ui 参照切れ'] });
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(promptsBy(calls, /^integrate-assets$/)[0].includes('警告: Build バッチ検証が不合格'));
  assert.ok(promptsBy(calls, /^qa-play-round1$/)[0].includes('警告: Build バッチ検証が不合格'));
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[batch-verify] ui 参照切れ')));
});

test('H-3/L-9: fixedNotes は knownIssues へ・ok:true+unresolved は不合格扱い', async () => {
  const routes = baseRoutes({ ok: true, fixedNotes: ['孤立 import を修正'], unresolved: ['残存'] });
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.knownIssues.some((k) => k.includes('batch-verify修正・CR-CODE非経由')));
  assert.ok(promptsBy(calls, /^integrate-assets$/)[0].includes('警告'), 'L-9 退行: unresolved 残存で警告が消えた');
});

test('プロンプト配線: produce/revise は laneVerify（own-file 限定）・bookkeep はパス指定 commit・reviewer 読み取り専用', async () => {
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }) });
  for (const p of promptsBy(calls, /^implement-/)) {
    assert.ok(p.includes('自分の編集ファイル起因'), 'produce の laneVerify が own-file 限定でない');
    assert.ok(p.includes('並走レーン規律'), 'produce に LANE_RULE が無い');
  }
  for (const p of promptsBy(calls, /^bookkeep-/)) {
    assert.ok(p.includes(': status done" -- state/stories.yaml'), 'bookkeep が素の git commit のまま（F-3 退行）');
    assert.ok(p.includes('触らない'), 'bookkeep の active.md 禁止が無い');
  }
  for (const p of promptsBy(calls, /^cr-code-/)) {
    assert.ok(p.includes('読み取り専用'), 'レビュアーにエンジン起動禁止が無い');
  }
  const gen = promptsBy(calls, /^generate-assets-images-prototype$/)[0];
  assert.ok(gen.includes('state/reviews'), '資産コミットの state 限定（H-4）が無い');
});

test('M-8a: 同一バグが round を跨いでも fix-qa label が round 一意', async () => {
  const qaReply = (call) => call.label.endsWith('round1')
    ? { verdict: 'CONCERNS', criticalBugs: [{ title: 'クラッシュ', detail: 'd', assignee: 'gameplay-engineer' }], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true }
    : QA_OK;
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }, qaReply) });
  assert.equal(callsBy(calls, /^fix-qa-r1-gameplay-engineer$/).length, 1, 'fix-qa label が round スコープでない');
});
