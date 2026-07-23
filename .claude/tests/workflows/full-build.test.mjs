// full-build.js の並列化まわりの分岐・配線テスト（DSL スタブハーネス）
import test from 'node:test';
import assert from 'node:assert/strict';
import { runWorkflow, callsBy, promptsBy } from './harness.mjs';

const WF = new URL('../../workflows/full-build.js', import.meta.url).pathname;
const ARGS = { reviewMode: 'lean', engine: 'phaser', checkpointBFeedbackPath: 'state/checkpoint-b-feedback.md' };

const gp = (id, title) => ({ id, title: title || id, assignee: 'gameplay-engineer', pillar: 'P-01', acceptance: 'a' });
const ui = (id, title) => ({ id, title: title || id, assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' });
const R = (match, reply) => ({ match, reply });

const QA_OK = { verdict: 'APPROVE', bugs: [], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true };
const EV_OK = { checks: [{ path: 'qa/evidence/e.png', exists: true, nonEmpty: true }], extraFilesInEvidenceDir: [] };

function baseRoutes(batchReply) {
  return [
    R(/^replan-stories$/, { stories: [gp('S-01'), ui('S-02'), gp('S-03')] }),
    R(/^polish-plan$/, { stories: [gp('S-10'), ui('S-11')] }),
    R(/^qa-play-/, QA_OK),
    R(/^verify-evidence-/, EV_OK),
    R(/^batch-verify-/, batchReply),
  ];
}

test('happy path: batch-verify が Build/Polish の合流点で各1回・BLOCKER なし', async () => {
  const { result, calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }) });
  assert.equal(callsBy(calls, /^batch-verify-build$/).length, 1);
  assert.equal(callsBy(calls, /^batch-verify-polish$/).length, 1);
  assert.ok(!result.unresolvedFindings.some((f) => f.includes('batch-verify')), JSON.stringify(result.unresolvedFindings));
  // 警告は注入されない
  assert.ok(!promptsBy(calls, /^polish-plan$/)[0].includes('警告'));
  assert.ok(!promptsBy(calls, /^qa-play-1$/)[0].includes('バッチ検証'));
});

test('レーン分配: 全 code story がちょうど1レーン・レーン内順序保存・LANE_RULE 注入', async () => {
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }) });
  const implLabels = callsBy(calls, /^impl-s-0[123]$/).map((c) => c.label);
  assert.deepEqual([...implLabels].sort(), ['impl-s-01', 'impl-s-02', 'impl-s-03']);
  // gameplay レーン内の相対順序: S-01 が S-03 より先
  assert.ok(implLabels.indexOf('impl-s-01') < implLabels.indexOf('impl-s-03'));
  for (const p of promptsBy(calls, /^impl-s-0[123]$/)) {
    assert.ok(p.includes('並走レーン規律'), 'impl プロンプトに LANE_RULE が無い');
    assert.ok(!p.includes('npm run build` を実行しない') || p.includes('自分の編集ファイル起因'), 'phaser laneVerify が own-file 限定になっていない');
  }
  // close-（APPROVE 後処理）にも LANE_RULE（stories.yaml 保護）
  for (const p of promptsBy(calls, /^close-/)) assert.ok(p.includes('並走レーン規律'));
  // CR-CODE レビュアーは読み取り専用 + レーン前提
  for (const p of promptsBy(calls, /^cr-/)) {
    assert.ok(p.includes('読み取り専用'), 'レビュアーにエンジン起動禁止が無い');
    assert.ok(p.includes('実体未実装'), 'レビュアーにレーン前提が無い');
  }
});

test('batch-verify null: BLOCKER 蓄積 + Polish/Integrate/QA へ警告注入 + Polish 実装レーンにも伝播', async () => {
  const { result, calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes(null) });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[BLOCKER]') && f.includes('バッチ検証 agent が結果を返さなかった')));
  assert.ok(promptsBy(calls, /^polish-plan$/)[0].includes('警告: Build バッチ検証が不合格'));
  assert.ok(promptsBy(calls, /^qa-play-1$/)[0].includes('バッチ検証'));
  // L-11: Polish の実装 agent にも警告が届く
  for (const p of promptsBy(calls, /^impl-s-1[01]$/)) assert.ok(p.includes('警告: Build バッチ検証が不合格'), 'Polish 実装レーンに警告が来ない');
});

test('batch-verify ok:false + unresolved: 個別 BLOCKER + 警告', async () => {
  const routes = baseRoutes({ ok: false, fixedNotes: [], unresolved: ['S-01 の型不整合'] });
  const { result, calls } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[batch-verify] S-01 の型不整合')));
  assert.ok(promptsBy(calls, /^qa-play-1$/)[0].includes('バッチ検証'));
});

test('batch-verify ok:false + unresolved 空: 合成 BLOCKER', async () => {
  const { result } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: false, fixedNotes: [], unresolved: [] }) });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[BLOCKER]') && f.includes('合格に未到達')));
});

test('L-9: ok:true でも unresolved 非空なら警告が消えない', async () => {
  const routes = baseRoutes({ ok: true, fixedNotes: [], unresolved: ['残存問題'] });
  const { result, calls } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[batch-verify] 残存問題')));
  assert.ok(promptsBy(calls, /^qa-play-1$/)[0].includes('バッチ検証'), 'ok:true+unresolved で警告が空になっている（L-9 退行）');
});

test('H-3: fixedNotes は人間可視チャネル（unresolvedFindings）へ載る', async () => {
  const routes = baseRoutes({ ok: true, fixedNotes: ['S-02 由来の未定義参照を修正'], unresolved: [] });
  const { result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('batch-verify修正・CR-CODE非経由') && f.includes('S-02 由来')));
});

test('polish story 0 件なら Polish batch-verify は走らない', async () => {
  const routes = [
    R(/^replan-stories$/, { stories: [gp('S-01')] }),
    R(/^polish-plan$/, { stories: [] }),
    R(/^qa-play-/, QA_OK),
    R(/^verify-evidence-/, EV_OK),
    R(/^batch-verify-/, { ok: true, fixedNotes: [], unresolved: [] }),
  ];
  const { calls } = await runWorkflow(WF, { args: ARGS, routes });
  assert.equal(callsBy(calls, /^batch-verify-build$/).length, 1);
  assert.equal(callsBy(calls, /^batch-verify-polish$/).length, 0);
});

test('コミット規律: hash 実証検証と共有ファイル単独コミットが impl プロンプトに載る / 資産コミットは state/reviews に限定', async () => {
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }) });
  const impl = promptsBy(calls, /^impl-s-01$/)[0];
  assert.ok(impl.includes('git show --stat'), 'hash 実証検証（M-6）が無い');
  assert.ok(impl.includes('単独コミット'), '共有ファイル即時単独コミット（M-5）が無い');
  const gen = promptsBy(calls, /^gen-images-1$/)[0];
  assert.ok(gen.includes('state/reviews'), '資産コミットの state 限定（H-4）が無い');
  assert.ok(!gen.includes('design docs state`'), '資産コミットが state ディレクトリ丸ごとのまま');
});

// ---- retro-e3 追随: agentR リトライ ----

test('agentR リトライ: batch-verify 初回 null は -retry で回復し BLOCKER/警告なし', async () => {
  let firstCalls = 0;
  const bv = (call) => {
    if (call.label.endsWith('-retry')) return { ok: true, fixedNotes: [], unresolved: [] };
    firstCalls++;
    return null;
  };
  const { result, calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes(bv) });
  assert.equal(firstCalls, 2, 'Build/Polish 各1回の初回呼び出しでない');
  assert.equal(callsBy(calls, /^batch-verify-build-retry$/).length, 1, 'Build の -retry が発行されない');
  assert.equal(callsBy(calls, /^batch-verify-polish-retry$/).length, 1, 'Polish の -retry が発行されない');
  // リトライで回復した結果が使われる（null 扱いの BLOCKER や後段への警告注入が無い）
  assert.ok(!result.unresolvedFindings.some((f) => f.includes('バッチ検証 agent が結果を返さなかった')));
  assert.ok(!promptsBy(calls, /^qa-play-1$/)[0].includes('バッチ検証'), '回復済みなのに QA へ警告が注入されている');
});

test('agentR リトライ: CR レビューペアが2回 null なら従来エスカレーション（自動 APPROVE しない）', async () => {
  const routes = [
    R(/^cr-s-01-1/, null),   // 接頭辞マッチ = '-retry' 付き label も null（2回 null ケース）
    R(/^sfh-s-01-1/, null),
  ].concat(baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }));
  const { result, calls } = await runWorkflow(WF, { args: ARGS, routes });
  assert.equal(callsBy(calls, /^cr-s-01-1-retry$/).length, 1, 'code-reviewer 側の -retry が発行されない');
  assert.equal(callsBy(calls, /^sfh-s-01-1-retry$/).length, 1, 'silent-failure-hunter 側の -retry が発行されない');
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('S-01: CR-CODE iteration 1 のレビューペアが両方失敗')),
    'リトライ後も null なら従来のエスカレーションに到達しない'
  );
  assert.ok(result.verdictHistory.some((v) => v.gate === 'CR-CODE' && v.artifact === 's-01' && v.iteration === 1 && v.verdict === 'CONCERNS'));
  // iteration 2 は通常レビュー（既定応答 findings 0 = APPROVE）で回復する
  assert.ok(result.verdictHistory.some((v) => v.gate === 'CR-CODE' && v.artifact === 's-01' && v.iteration === 2 && v.verdict === 'APPROVE'));
});

// ---- retro-e3 追随: fallback 必須文言 / date -u 統一 / 取込先 ----

test('fallback 必須文言: gen 系プロンプトに全段試行とルート名+HTTP 列挙義務が入る', async () => {
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }) });
  const gens = callsBy(calls, /^gen-(images|audio|models)-/);
  assert.ok(gens.length >= 2, '生成バッチ（images/audio）が起動していない');
  for (const c of gens) {
    assert.ok(c.prompt.includes('1 段も試さずにローカル縮退'), c.label + ' に「1段も試さずローカル縮退禁止」が無い');
    assert.ok(c.prompt.includes('全段試行'), c.label + ' に fallback 全段試行の義務が無い');
    assert.ok(c.prompt.includes('ルート名 + HTTP ステータス'), c.label + ' にルート名+HTTPコードの列挙義務が無い');
    assert.ok(
      c.opts.schema.properties.degradedRoutes.description.includes('ルート名+HTTPコード必須'),
      c.label + ' の ASSET_GEN_SCHEMA degradedRoutes description が更新されていない'
    );
  }
});

test('date -u 統一: replan/batch-verify/QA/CD/finalize プロンプトが date -u コマンドの実行出力を指定する', async () => {
  const { calls } = await runWorkflow(WF, { args: ARGS, routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }) });
  const DATE_CMD = 'date -u +%Y-%m-%dT%H:%M:%SZ';
  for (const re of [/^replan-stories$/, /^batch-verify-build$/, /^qa-play-1$/, /^cd-checkpoint-1$/, /^finalize-state$/]) {
    const p = promptsBy(calls, re)[0];
    assert.ok(p && p.includes(DATE_CMD), String(re) + ' に date -u 指定が無い（推測記入の余地）');
  }
});

test('取込先: engine=unity の integrate-3d が Assets/Resources/Generated/ を指し旧 Assets/Generated/ は残存ゼロ', async () => {
  const { calls } = await runWorkflow(WF, {
    args: { reviewMode: 'lean', engine: 'unity', checkpointBFeedbackPath: 'state/checkpoint-b-feedback.md' },
    routes: baseRoutes({ ok: true, fixedNotes: [], unresolved: [] }),
  });
  const p = promptsBy(calls, /^integrate-3d-assets$/)[0];
  assert.ok(p && p.includes('game/Assets/Resources/Generated/'), 'integrate-3d が新取込先を指していない');
  for (const c of calls) {
    assert.ok(!c.prompt.includes('Assets/Generated/'), c.label + ' に旧取込先 Assets/Generated/ が残存');
  }
});
