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

// ---- 監査追随（2026-07-29）: レーン例外ガード / エラー経路の無記録解消 / W-3 タグ振り分け / M-8b 冪等ガード ----

const BATCH_OK = { ok: true, fixedNotes: [], unresolved: [] };

test('レーン例外: impl が throw しても [BLOCKER] 蓄積 + 他レーンは継続（parallel の null 潰しに先行）', async () => {
  const routes = [
    R(/^impl-s-01/, () => { throw new Error('schema mismatch'); }),
  ].concat(baseRoutes(BATCH_OK));
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('[BLOCKER] Build gameplay レーンが例外中断') && f.includes('schema mismatch')),
    'レーン例外が無記録: ' + JSON.stringify(result.unresolvedFindings)
  );
  assert.equal(callsBy(calls, /^impl-s-02$/).length, 1, 'ui レーンが巻き添え停止している');
});

test('replan-gdd null: GDD 改訂判断の失敗が unresolvedFindings に載る', async () => {
  const routes = [R(/^replan-gdd/, null)].concat(baseRoutes(BATCH_OK));
  const { result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('GDD 改訂判断 agent（game-designer）が失敗')));
});

test('close null: APPROVE 後の status:done 更新失敗が記録される', async () => {
  const routes = [R(/^close-s-01/, null)].concat(baseRoutes(BATCH_OK));
  const { result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('S-01: APPROVE 後の status:done 更新 agent が失敗')));
});

test('CR fix null: fix 失敗が iteration ごとに記録される', async () => {
  const routes = [
    R(/^cr-s-01-|^sfh-s-01-/, { findings: [{ summary: 'x', severity: 'major' }] }),
    R(/^fix-s-01-/, null),
  ].concat(baseRoutes(BATCH_OK));
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('S-01: CR-CODE iteration 1 の fix agent が失敗')));
  assert.ok(result.unresolvedFindings.some((f) => f.includes('S-01: CR-CODE iteration 2 の fix agent が失敗')));
  // M-8b: リトライ多発の fix 経路にこそ冪等ガードが要る（resume 二重適用の主戦場）
  assert.ok(promptsBy(calls, /^fix-s-01-1$/)[0].includes('冪等ガード'), 'CR fix プロンプトに冪等ガードが前置されない');
});

test('drift 非APPROVE + failedAssets 空: 無記録で抜けず人間確認事項として蓄積', async () => {
  const routes = [
    R(/^ar-batch-drift-1/, { verdict: 'CONCERNS', failedAssets: [], disclosures: [] }),
  ].concat(baseRoutes(BATCH_OK));
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('バッチ一貫性チェック pass 1 が CONCERNS だが failedAssets が空')));
  assert.equal(callsBy(calls, /^ar-batch-drift-2/).length, 0, 'break せず pass 2 が走っている');
});

test('QA fix null: 修正 agent の失敗が記録され再QAへ', async () => {
  const routes = [
    R(/^qa-fix-1-gameplay-engineer/, null),
    R(/^qa-play-1/, { verdict: 'CONCERNS', bugs: [{ summary: 'b', severity: 'major', assignee: 'gameplay-engineer' }], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true, summary: 'ng' }),
  ].concat(baseRoutes(BATCH_OK));
  const { result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('FullQA: round 1 の修正 agent（gameplay-engineer）が失敗')));
});

test('W-3: 資産 story はタグ第一・タグ無しは語彙 fallback で振り分け・Replan にタグ指示・M-8b 冪等ガード前置', async () => {
  const routes = [
    R(/^replan-stories$/, { stories: [gp('S-01'), ui('S-02'),
      { id: 'S-20', title: '[MDL] 敵モデル', assignee: 'art-director', pillar: 'P-01', acceptance: 'a' },
      { id: 'S-21', title: '[IMG] 3Dロゴ風アイコン', assignee: 'art-director', pillar: 'P-01', acceptance: 'a' },
      { id: 'S-22', title: 'ヒーローのリグ調整', assignee: 'art-director', pillar: 'P-01', acceptance: 'FBX を更新する' }, // タグ無し + 3D 語彙 = fallback 経路
      { id: 'S-23', title: 'タイトルロゴ差し替え', assignee: 'art-director', pillar: 'P-01', acceptance: 'a' }, // タグ無し + 語彙なし = images
    ] }),
    R(/^polish-plan$/, { stories: [] }),
    R(/^qa-play-/, QA_OK),
    R(/^verify-evidence-/, EV_OK),
    R(/^batch-verify-/, BATCH_OK),
  ];
  const { calls } = await runWorkflow(WF, { args: { ...ARGS, engine: 'unity' }, routes });
  assert.ok(promptsBy(calls, /^replan-stories$/)[0].includes('資産種別タグ'), 'Replan にタグ指示が無い');
  const modelsPrompt = promptsBy(calls, /^gen-models-1$/)[0];
  const imagesPrompt = promptsBy(calls, /^gen-images-1$/)[0];
  assert.ok(modelsPrompt.includes('敵モデル'), '[MDL] タグ story が models バッチに来ない');
  assert.ok(!modelsPrompt.includes('3Dロゴ風'), '「3D」語彙を含む [IMG] タグ story が models へ誤配（タグ優先が効いていない）');
  assert.ok(imagesPrompt.includes('3Dロゴ風'), '[IMG] タグ story が images バッチに来ない');
  assert.ok(modelsPrompt.includes('ヒーローのリグ調整'), 'タグ無し 3D 語彙 story が MODEL_WORDS fallback で models に来ない');
  assert.ok(imagesPrompt.includes('タイトルロゴ差し替え'), 'タグ無し・語彙なし story が images に来ない');
  for (const re of [/^impl-s-01$/, /^close-s-01$/, /^integrate-3d-assets$/]) {
    assert.ok(promptsBy(calls, re)[0].includes('冪等ガード'), re + ' に冪等ガードが前置されない');
  }
});

test('レーン例外: Polish レーンと AssetGen トラックの laneSafe も実発火する', async () => {
  const routes = [
    R(/^impl-s-10/, () => { throw new Error('polish boom'); }),
    R(/^gen-images-1$/, () => { throw new Error('images boom'); }),
  ].concat(baseRoutes(BATCH_OK));
  const { result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[BLOCKER] Polish gameplay レーンが例外中断') && f.includes('polish boom')));
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[BLOCKER] AssetGen(images) トラックが例外中断') && f.includes('images boom')));
});

test('cd-fix null: REJECT 指示への修正 agent 失敗が記録される（再判定が通っても沈黙しない）', async () => {
  const routes = [
    R(/^cd-fix/, null),
    R(/^cd-checkpoint-1/, { verdict: 'REJECT', summary: 's', playInstructions: 'p', mustFix: ['直せ'] }),
    R(/^cd-checkpoint-2/, { verdict: 'CONCERNS', summary: 's', playInstructions: 'p', mustFix: [] }),
  ].concat(baseRoutes(BATCH_OK));
  const { result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('REJECT 指示への修正 agent が失敗')));
  assert.equal(result.verdict, 'CONCERNS');
});

// ---- silent-failure-hunter 指摘の回帰テスト（2026-07-29） ----

test('FullQA トラック例外: 資産監査 thunk が throw しても [BLOCKER] 蓄積 + QA-PLAY は継続', async () => {
  const routes = [
    R(/^asset-audit/, () => { throw new Error('audit boom'); }),
  ].concat(baseRoutes(BATCH_OK));
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('[BLOCKER] FullQA 資産監査トラックが例外中断') && f.includes('audit boom')),
    'FullQA 資産監査の例外が無記録: ' + JSON.stringify(result.unresolvedFindings)
  );
  assert.equal(callsBy(calls, /^qa-play-1$/).length, 1, 'QA-PLAY トラックが巻き添え停止');
});

test('FullQA トラック例外: QA-PLAY thunk が throw しても [BLOCKER] 蓄積（QA 未実施が CD に届く）', async () => {
  const routes = [
    R(/^qa-play-1/, () => { throw new Error('qa boom'); }),
  ].concat(baseRoutes(BATCH_OK));
  const { result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(result.unresolvedFindings.some((f) => f.includes('[BLOCKER] FullQA QA-PLAY トラックが例外中断') && f.includes('qa boom')));
});

test('engine=phaser で [MDL] タグ story は黙って脱落せず [BLOCKER] 蓄積', async () => {
  const routes = [
    R(/^replan-stories$/, { stories: [gp('S-01'), ui('S-02'),
      { id: 'S-20', title: '[MDL] 敵モデル', assignee: 'art-director', pillar: 'P-01', acceptance: 'a' },
    ] }),
  ].concat(baseRoutes(BATCH_OK));
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('[BLOCKER]') && f.includes('3D 資産（MDL/ANM）非対応') && f.includes('S-20')),
    '2D エンジンでの 3D story 脱落が無記録: ' + JSON.stringify(result.unresolvedFindings)
  );
  assert.equal(callsBy(calls, /^gen-models-/).length, 0, 'phaser で models バッチが走っている');
});

// ---- adversarial/Codex 指摘の回帰テスト（2026-07-30） ----

test('タグ/assignee 不整合は記録される・phaser では語彙 fallback が無効', async () => {
  const routes = [
    R(/^replan-stories$/, { stories: [gp('S-01'), ui('S-02'),
      { id: 'S-30', title: '[SFX] 打撃音', assignee: 'art-director', pillar: 'P-01', acceptance: 'a' }, // タグ/担当不整合
      { id: 'S-31', title: '3D風メタリックなロゴ', assignee: 'art-director', pillar: 'P-01', acceptance: 'a' }, // phaser: 語彙 fallback を適用しない → images 残留
    ] }),
    R(/^polish-plan$/, { stories: [] }),
  ].concat(baseRoutes(BATCH_OK));
  const { calls, result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('S-30') && f.includes('[SFX]') && f.includes('不整合')),
    'タグ/assignee 不整合が無記録: ' + JSON.stringify(result.unresolvedFindings)
  );
  assert.ok(promptsBy(calls, /^gen-images-1$/)[0].includes('3D風メタリックなロゴ'), 'phaser で語彙 fallback が発動し images から脱落している');
  assert.ok(!result.unresolvedFindings.some((f) => f.includes('S-31')), 'phaser のタグ無し story が偽 [BLOCKER] を積んでいる');
});

test('Polish: 資産系 assignee の story は黙って捨てず記録される', async () => {
  const routes = [
    R(/^polish-plan$/, { stories: [gp('S-10'),
      { id: 'S-40', title: 'ヒットエフェクト画像の追加', assignee: 'art-director', pillar: 'P-01', acceptance: 'a' },
    ] }),
  ].concat(baseRoutes(BATCH_OK));
  const { result } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('Polish') && f.includes('S-40') && f.includes('実装対象外') && f.includes('art-director')),
    'Polish の資産 story ドロップが無記録（または assignee 非表示）: ' + JSON.stringify(result.unresolvedFindings)
  );
});

// ---- code-review --fix 追随（2026-07-31）: レーン網羅の残穴・タグ正規化・冪等ガード網羅の回帰テスト ----

test('Replan: engineer 割当のタグ story は記録・非レーン assignee は [BLOCKER]・小文字タグも正規化', async () => {
  const routes = [
    R(/^replan-stories$/, { stories: [gp('S-01'), ui('S-02'),
      { id: 'S-50', title: '[IMG] HUD アイコン', assignee: 'ui-engineer', pillar: 'P-01', acceptance: 'a' }, // タグ × engineer 割当 = コードレーン行き
      { id: 'S-51', title: 'パーティクル調整', assignee: 'art-directer', pillar: 'P-01', acceptance: 'a' },   // 綴り誤り = 全レーン脱落
      { id: 'S-52', title: '[mdl] 敵モデル', assignee: 'art-director', pillar: 'P-01', acceptance: 'a' },     // 小文字タグ = 正規化して models へ
    ] }),
    R(/^polish-plan$/, { stories: [] }),
    R(/^qa-play-/, QA_OK),
    R(/^verify-evidence-/, EV_OK),
    R(/^batch-verify-/, BATCH_OK),
  ];
  const { calls, result } = await runWorkflow(WF, { args: { ...ARGS, engine: 'unity' }, routes });
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('S-50') && f.includes('コードレーン')),
    'タグ付き story の engineer 割当が無記録: ' + JSON.stringify(result.unresolvedFindings)
  );
  assert.ok(
    result.unresolvedFindings.some((f) => f.includes('[BLOCKER]') && f.includes('S-51') && f.includes('全レーンから脱落')),
    '非レーン assignee の全レーン脱落が無記録: ' + JSON.stringify(result.unresolvedFindings)
  );
  assert.ok(promptsBy(calls, /^gen-models-1$/)[0].includes('敵モデル'), '小文字タグ [mdl] が正規化されず models バッチに来ない');
});

test('冪等ガード: bookkeep（MAX_ITER 到達）と qa-fix のプロンプトにも前置される', async () => {
  const routes = [
    R(/^cr-s-01-|^sfh-s-01-/, { findings: [{ summary: 'x', severity: 'major' }] }), // 2 iteration 非APPROVE → bookkeep 経路
    R(/^qa-play-1/, { verdict: 'CONCERNS', bugs: [{ summary: 'b', severity: 'major', assignee: 'gameplay-engineer' }], failedAcceptance: [], evidencePaths: ['qa/evidence/e.png'], screenshotsVisuallyConfirmed: true, summary: 'ng' }),
  ].concat(baseRoutes(BATCH_OK));
  const { calls } = await runWorkflow(WF, { args: ARGS, routes });
  const bookkeep = promptsBy(calls, /^bookkeep-s-01$/)[0];
  assert.ok(bookkeep, 'MAX_ITER 到達で bookkeep が走らない');
  assert.ok(bookkeep.includes('冪等ガード'), 'bookkeep プロンプトに冪等ガードが前置されない');
  const qaFix = promptsBy(calls, /^qa-fix-1-gameplay-engineer$/)[0];
  assert.ok(qaFix, 'QA CONCERNS で qa-fix が走らない');
  assert.ok(qaFix.includes('冪等ガード'), 'qa-fix プロンプトに冪等ガードが前置されない');
});
