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
    { id: 'S-04', title: 'コアループと環境ビジュアル', assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: '可視の地面/背景・ライト・カメラ構図と画面レイアウトが確定している' }, // 全 engine の必須環境要素を充足（validateSetup の acceptance 検証）
  ],
  titleStoryId: 'S-02',
  menuStoryId: 'S-03',
  metaPersistenceStoryId: 'S-01',
  environmentStoryId: 'S-04', // retro-e3: 環境ビジュアル story が SETUP_SCHEMA required に追加された
};
const CROSSCHECK = {
  found: [
    { id: 'S-02', exists: true, assignee: 'ui-engineer', phase: 'prototype' },
    { id: 'S-03', exists: true, assignee: 'ui-engineer', phase: 'prototype' },
    { id: 'S-01', exists: true, assignee: 'gameplay-engineer', phase: 'prototype' },
    { id: 'S-04', exists: true, assignee: 'gameplay-engineer', phase: 'prototype', acceptance: '可視の地面/背景・ライト・カメラ構図と画面レイアウトが確定している' }, // 環境 story は実体 acceptance も突合される
  ],
};
const QA_OK = { verdict: 'APPROVE', criticalBugs: [], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true };
const EV_OK = { checks: [{ path: 'qa/evidence/e.png', exists: true, nonEmpty: true }], extraFilesInEvidenceDir: [] };

// route は接頭辞マッチにする（agentR の '-retry' 付き label でも同じ route が当たるように — retro-e3 指摘5）
function baseRoutes(batchReply, qaReply) {
  return [
    R(/^setup-scaffold-stories/, SETUP),
    R(/^setup-crosscheck-stories/, CROSSCHECK),
    R(/^qa-play-round/, qaReply || QA_OK),
    R(/^verify-evidence-round/, EV_OK),
    R(/^batch-verify-/, batchReply),
  ];
}
const BATCH_OK = { ok: true, fixedNotes: [], unresolved: [] };

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

// ---- retro-e3 追随: agentR リトライ ----

test('agentR リトライ: 初回 null は -retry label + resume ガード前置で1回だけ再試行し回復する', async () => {
  let crosscheckCalls = 0;
  const routes = [
    R(/^setup-scaffold-stories/, SETUP),
    R(/^setup-crosscheck-stories/, (call) => {
      crosscheckCalls++;
      return call.label.endsWith('-retry') ? CROSSCHECK : null;
    }),
    R(/^qa-play-round/, QA_OK),
    R(/^verify-evidence-round/, EV_OK),
    R(/^batch-verify-/, BATCH_OK),
  ];
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.equal(crosscheckCalls, 2, 'null 後にちょうど1回だけ再試行される');
  assert.equal(callsBy(calls, /^setup-crosscheck-stories$/).length, 1);
  const retry = callsBy(calls, /^setup-crosscheck-stories-retry$/);
  assert.equal(retry.length, 1, '-retry 付き label で再呼び出しされない');
  // 盲目再実行の禁止: リトライは resume ガード（完了済み作業の確認・重複実行禁止）を前置した上で元プロンプトを保持する
  assert.ok(retry[0].prompt.startsWith('【リトライ実行】'), 'リトライに resume ガードが前置されない');
  assert.ok(retry[0].prompt.endsWith(callsBy(calls, /^setup-crosscheck-stories$/)[0].prompt), 'リトライが元プロンプトを保持しない');
  // 回復して実装フェーズへ続行している（エスカレーションに落ちない）
  assert.equal(callsBy(calls, /^implement-/).length, 4);
  assert.ok(!result.unresolvedFindings.some((f) => f.includes('独立突合')));
});

test('agentR リトライ: 2回 null は従来のエスカレーション（生成 agent 失敗）に到達する', async () => {
  const routes = [R(/^generate-assets-images-prototype/, null)].concat(baseRoutes(BATCH_OK));
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.equal(callsBy(calls, /^generate-assets-images-prototype$/).length, 1);
  assert.equal(callsBy(calls, /^generate-assets-images-prototype-retry$/).length, 1, 'リトライは1回だけ（2回目以降は無い）');
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('[AR-ASSET][assets-images-prototype] 生成 agent が失敗')),
    'リトライ後も null なら従来どおりエスカレーションする'
  );
});

// ---- retro-e3 追随: Setup 環境ビジュアル story の機械検証 ----

test('Setup 環境検証: environmentStoryId 不在は差し戻し→修正応答で続行・crosscheck は必須4 story を突合', async () => {
  const routes = [
    R(/^setup-scaffold-stories/, { ...SETUP, environmentStoryId: 'S-99' }),
    R(/^setup-fix-required-stories/, SETUP),
    R(/^setup-crosscheck-stories/, CROSSCHECK),
    R(/^qa-play-round/, QA_OK),
    R(/^verify-evidence-round/, EV_OK),
    R(/^batch-verify-/, BATCH_OK),
  ];
  const { calls } = await runWorkflow(WF, { args: ARGS, routes });
  const fix = promptsBy(calls, /^setup-fix-required-stories$/);
  assert.equal(fix.length, 1, '差し戻し（setup-fix-required-stories）が1回発行されない');
  assert.ok(fix[0].includes('environmentStoryId=S-99'), '欠落 ID が差し戻し文言に載らない');
  assert.ok(fix[0].includes('環境の最低限ビジュアル story'), '差し戻し文言に環境 story の必須要件が無い');
  assert.ok(fix[0].includes('背景・画面レイアウト'), '差し戻し文言の必須環境要素が engine=phaser のセットでない（unity 要件の流用は修正を validator に落とす）');
  // 独立突合（crosscheck）は Title/Menu/メタ/環境の4 story を対象にする
  const cc = promptsBy(calls, /^setup-crosscheck-stories$/)[0];
  for (const id of ['S-01', 'S-02', 'S-03', 'S-04']) {
    assert.ok(cc.includes('"' + id + '"'), 'crosscheck 対象に ' + id + ' が無い');
  }
  assert.equal(callsBy(calls, /^implement-/).length, 4, '修正応答で実装フェーズへ続行しない');
});

test('Setup 環境検証: 環境 story の assignee が gameplay-engineer 以外は差し戻し対象', async () => {
  const routes = [
    R(/^setup-scaffold-stories/, { ...SETUP, environmentStoryId: 'S-02' }), // S-02 は ui-engineer
    R(/^setup-fix-required-stories/, SETUP),
    R(/^setup-crosscheck-stories/, CROSSCHECK),
    R(/^qa-play-round/, QA_OK),
    R(/^verify-evidence-round/, EV_OK),
    R(/^batch-verify-/, BATCH_OK),
  ];
  const { calls } = await runWorkflow(WF, { args: ARGS, routes });
  const fix = promptsBy(calls, /^setup-fix-required-stories$/);
  assert.equal(fix.length, 1);
  assert.ok(fix[0].includes('環境ビジュアル story S-02 の assignee が gameplay-engineer でない'), 'assignee 不正の指摘が差し戻し文言に無い');
  assert.equal(callsBy(calls, /^implement-/).length, 4, '修正応答で実装フェーズへ続行しない');
});

test('Setup 環境検証: acceptance が必須環境要素を欠くと差し戻し（欠落要素を列挙）', async () => {
  const thinEnv = {
    ...SETUP,
    prototypeStories: SETUP.prototypeStories.map((st) =>
      st.id === 'S-04' ? { ...st, acceptance: '背景を可視化する' } : st), // レイアウト言及なし（phaser 要件は 背景+画面レイアウト）
  };
  const routes = [
    R(/^setup-scaffold-stories/, thinEnv),
    R(/^setup-fix-required-stories/, SETUP),
    R(/^setup-crosscheck-stories/, CROSSCHECK),
    R(/^qa-play-round/, QA_OK),
    R(/^verify-evidence-round/, EV_OK),
    R(/^batch-verify-/, BATCH_OK),
  ];
  const { calls } = await runWorkflow(WF, { args: ARGS, routes });
  const fix = promptsBy(calls, /^setup-fix-required-stories$/);
  assert.equal(fix.length, 1, 'acceptance 記述不足で差し戻しが発行されない');
  assert.ok(fix[0].includes('必須環境要素を欠く: 画面レイアウト'), '欠落要素（画面レイアウト）が差し戻し文言に列挙されない');
  assert.equal(callsBy(calls, /^implement-/).length, 4, '修正応答で実装フェーズへ続行しない');
});

test('Setup 環境検証: stories.yaml 実体の phase が prototype 以外は独立突合で不合格', async () => {
  const ccBuildPhase = {
    found: CROSSCHECK.found.map((f) => (f.id === 'S-04' ? { ...f, phase: 'build' } : f)),
  };
  const routes = [
    R(/^setup-scaffold-stories/, SETUP),
    R(/^setup-crosscheck-stories/, ccBuildPhase),
    R(/^qa-play-round/, QA_OK),
    R(/^verify-evidence-round/, EV_OK),
    R(/^batch-verify-/, BATCH_OK),
  ];
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.equal(callsBy(calls, /^implement-/).length, 0, 'phase 不一致のまま実装フェーズへ進んでいる');
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('phase が build')),
    'phase 不一致が unresolvedFindings に記録されない'
  );
});

test('Setup 環境検証: 突合応答のフィールド省略は検証スキップではなく不合格', async () => {
  const ccOmitted = {
    found: CROSSCHECK.found.map((f) =>
      f.id === 'S-04' ? { id: 'S-04', exists: true } : f), // assignee/phase/acceptance を省略
  };
  const routes = [
    R(/^setup-scaffold-stories/, SETUP),
    R(/^setup-crosscheck-stories/, ccOmitted),
    R(/^qa-play-round/, QA_OK),
    R(/^verify-evidence-round/, EV_OK),
    R(/^batch-verify-/, BATCH_OK),
  ];
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.equal(callsBy(calls, /^implement-/).length, 0, 'フィールド省略のまま実装フェーズへ進んでいる');
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('検証不能')),
    'フィールド省略（検証不能）が unresolvedFindings に記録されない'
  );
});

// ---- retro-e3 追随: fallback 必須文言 / date -u 統一 / 取込先 ----

test('fallback 必須文言: 生成系プロンプトに全段試行とルート名+HTTP 列挙義務が入る', async () => {
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes(BATCH_OK) });
  const gens = callsBy(calls, /^generate-assets-/);
  assert.ok(gens.length >= 2, '生成バッチ（images/audio）が起動していない');
  for (const c of gens) {
    assert.ok(c.prompt.includes('1 段も試さずにローカル縮退'), c.label + ' に「1段も試さずローカル縮退禁止」が無い');
    assert.ok(c.prompt.includes('全段試行'), c.label + ' に fallback 全段試行の義務が無い');
    assert.ok(c.prompt.includes('ルート名 + HTTP ステータス'), c.label + ' にルート名+HTTPコードの列挙義務が無い');
    assert.ok(
      c.opts.schema.properties.degradedRoutes.description.includes('ルート名+HTTPコード必須'),
      c.label + ' の GEN_SCHEMA degradedRoutes description が更新されていない'
    );
  }
});

test('date -u 統一: 時刻記入プロンプトが date -u コマンドの実行出力を指定する', async () => {
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes(BATCH_OK) });
  const DATE_CMD = 'date -u +%Y-%m-%dT%H:%M:%SZ';
  for (const re of [/^setup-scaffold-stories$/, /^batch-verify-build$/, /^integrate-assets$/, /^qa-play-round1$/, /^cd-checkpoint-b$/]) {
    const p = promptsBy(calls, re)[0];
    assert.ok(p && p.includes(DATE_CMD), String(re) + ' に date -u 指定が無い（推測記入の余地）');
  }
});

test('取込先: engine=unity の scaffold/Integrate は Assets/Resources/Generated/ を指し旧 Assets/Generated/ は残存ゼロ', async () => {
  const { calls } = await runWorkflow(WF, {
    args: { reviewMode: 'lean', engine: 'unity' },
    routes: baseRoutes(BATCH_OK),
  });
  assert.ok(promptsBy(calls, /^setup-scaffold-stories$/)[0].includes('Assets/Resources/Generated/'), 'scaffold が新取込先を指していない');
  assert.ok(promptsBy(calls, /^integrate-assets$/)[0].includes('game/Assets/Resources/Generated/'), 'Integrate が新取込先を指していない');
  for (const c of calls) {
    assert.ok(!c.prompt.includes('Assets/Generated/'), c.label + ' に旧取込先 Assets/Generated/ が残存');
  }
});
