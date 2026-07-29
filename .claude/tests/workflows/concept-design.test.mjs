// concept-design.js の分岐・配線テスト（DSL スタブハーネス）
// 監査追随（2026-07-29）: CD-CHECKPOINT REJECT 時の個別 fix 失敗の無記録解消（fixResults zip）
import test from 'node:test';
import assert from 'node:assert/strict';
import { runWorkflow, callsBy } from './harness.mjs';

const WF = new URL('../../workflows/concept-design.js', import.meta.url).pathname;
const ARGS = { briefPath: 'design/brief.md', reviewMode: 'lean', engine: 'phaser' };
const R = (match, reply) => ({ match, reply });

test('happy path: 全ゲート既定 APPROVE で unresolved 無し・4ゲートが verdictHistory に載る', async () => {
  const { result, calls } = await runWorkflow(WF, { args: ARGS, routes: [] });
  assert.equal(result.verdict, 'APPROVE');
  assert.deepEqual(result.unresolvedFindings, [], JSON.stringify(result.unresolvedFindings));
  const gates = result.verdictHistory.map((v) => v.gate);
  for (const g of ['DR-CONCEPT', 'DR-GDD', 'AR-BIBLE', 'CD-CHECKPOINT']) {
    assert.ok(gates.includes(g), g + ' が verdictHistory に無い');
  }
  assert.equal(callsBy(calls, /^CD-CHECKPOINT 再判定/).length, 0, 'APPROVE なのに再判定が走っている');
});

test('CD REJECT: 個別 fix の失敗が指示内容付きで unresolvedFindings に載る（成功分は載らない）', async () => {
  const routes = [
    R(/^CD-CHECKPOINT 判定/, {
      verdict: 'REJECT', findings: ['骨子が弱い'], fixes: [
        { assignee: 'game-designer', artifact: 'design/concept.md', instruction: 'ピラーを3つに絞る' },
        { assignee: 'art-director', artifact: 'design/art-bible.md', instruction: 'パレットを機械可読にする' },
      ],
    }),
    R(/^CD修正: design\/concept\.md/, null), // 接頭辞マッチ = '-retry' 付き label も null（agentR 2回 null）
    R(/^CD-CHECKPOINT 再判定/, { verdict: 'CONCERNS', findings: [], fixes: [] }),
  ];
  const { result, calls } = await runWorkflow(WF, { args: ARGS, routes });
  assert.ok(
    result.unresolvedFindings.some((f) =>
      f.includes('CD-CHECKPOINT: 修正指示「[game-designer] design/concept.md — ピラーを3つに絞る」の fix agent が失敗')),
    'fix 失敗が無記録: ' + JSON.stringify(result.unresolvedFindings)
  );
  assert.ok(
    !result.unresolvedFindings.some((f) => f.includes('design/art-bible.md — パレット')),
    '成功した fix まで失敗として記録されている'
  );
  assert.equal(callsBy(calls, /^CD修正の対応記録/).length, 1, 'fix 後の対応記録 agent が走らない');
  assert.equal(result.verdict, 'CONCERNS', '再判定の結果が反映されない');
});
