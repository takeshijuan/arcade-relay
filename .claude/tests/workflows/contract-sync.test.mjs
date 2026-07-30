// contract §8 の資産 ID 種別・状態語彙と、workflow スクリプト内の手書き再掲との機械同期検証（TODOS W-3）。
// workflow スクリプトはファイルを読めない（contract §4）ため、単一情報源化はこのテストが担う:
// contract §8 に種別追加・語彙変更が入った時、スクリプトの ASSET_TAG / MODEL_WORDS / タグ指示が
// 追随していなければここで落ちる（黙った振り分けドリフトを防ぐ）。
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url).pathname; // .claude/
const read = (p) => readFile(root + p, 'utf8');

test('W-3: contract §8 の資産 ID 種別が full-build.js のタグ判定・タグ指示に全て現れる', async () => {
  const contract = await read('docs/contract.md');
  const assetLine = contract.split('\n').find((l) => l.includes('資産:') && l.includes('IMG-01'));
  assert.ok(assetLine, 'contract §8 の資産 ID 行が見つからない');
  const kinds = [...new Set([...assetLine.matchAll(/`([A-Z]{2,4})-01`/g)].map((m) => m[1]))];
  assert.ok(kinds.length >= 5, 'contract §8 から資産 ID 種別をパースできない: ' + JSON.stringify(kinds));

  const src = await read('workflows/full-build.js');
  const tag = src.match(/const ASSET_TAG = \/(.+?)\/[a-z]*;/);
  assert.ok(tag, 'full-build.js に ASSET_TAG が無い');
  for (const k of kinds) {
    assert.ok(tag[1].includes(k), 'ASSET_TAG が contract §8 の種別 ' + k + ' を含まない（contract 変更にスクリプトが未追随）');
  }
  const mw = src.match(/const MODEL_WORDS = \/(.+?)\/[a-z]*;/);
  assert.ok(mw && mw[1].includes('MDL-') && mw[1].includes('ANM-'), 'MODEL_WORDS（語彙 fallback）が MDL-/ANM- を含まない');
  // Replan プロンプトのタグ指示が §8 の全種別を列挙している（parsed kinds から派生 —
  // 固定リテラルだと contract に種別追加後もタグ指示の古い列挙が素通りする）
  const tagInstr = src.match(/title の先頭に資産種別タグ ([^（]+)（contract §8/);
  assert.ok(tagInstr, 'Replan プロンプトにタグ指示文が見つからない');
  for (const k of kinds) {
    assert.ok(tagInstr[1].includes('[' + k + ']'), 'Replan タグ指示が contract §8 の種別 ' + k + ' を列挙していない');
  }
});

test('license_note の転記必須規約が両 workflow の生成プロンプトと FullQA 監査に配線されている', async () => {
  // assets-config.md「Provenance」が license_note を必須化した — 生成 agent はプロンプトの
  // フィールド列挙に従うため、列挙から消えると記録も監査も静かに空になる（監査指摘6の再発防止）
  const cfg = await read('docs/assets-config.md');
  assert.ok(cfg.includes('license_note'), 'assets-config.md から license_note 規約が消えている');
  const fb = await read('workflows/full-build.js');
  assert.ok((fb.match(/license_note/g) || []).length >= 2, 'full-build.js の生成プロンプト/監査に license_note 配線が無い');
  const proto = await read('workflows/prototype.js');
  assert.ok(proto.includes('license_note'), 'prototype.js の生成プロンプトに license_note 配線が無い');
});

test('視覚証跡の SUSPECT_LOW_CONTRAST 閾値が gates.md と両 workflow プロンプトで一致する', async () => {
  const gates = await read('docs/gates.md');
  const g = gates.match(/([0-9.]+) 未満は低コントラスト疑い（SUSPECT_LOW_CONTRAST）/);
  assert.ok(g, 'gates.md の SUSPECT_LOW_CONTRAST 閾値行をパースできない');
  const threshold = g[1];
  for (const f of ['workflows/full-build.js', 'workflows/prototype.js']) {
    const src = await read(f);
    assert.ok(src.includes('stddev < ' + threshold + ' = SUSPECT_LOW_CONTRAST'),
      f + ' の QA プロンプト閾値が gates.md（' + threshold + '）とドリフト');
  }
});

test('W-3: contract §8 の資産状態語彙（5値）とスクリプトの再掲が一致する', async () => {
  const contract = await read('docs/contract.md');
  const m = contract.match(/資産状態語彙（この5値のみ）: `([^`]+)`/);
  assert.ok(m, 'contract §8 の状態語彙行をパースできない');
  const vocab = m[1].split('|').map((s) => s.trim());
  // canary: 語彙が変わったらここで落とし、下の再掲箇所（must-replace / rejected を手書きする
  // Replan/AssetGen プロンプト）の追随を促す
  assert.deepEqual([...vocab].sort(), ['approved', 'generated', 'must-replace', 'planned', 'rejected'].sort(),
    'contract §8 の状態語彙が変更された — workflow スクリプトの手書き再掲（must-replace/rejected）を追随させよ');
  // 再生成トリガー（must-replace / rejected への状態変更）の再掲は full-build.js（Replan/AssetGen）が持つ
  const fb = await read('workflows/full-build.js');
  assert.ok(fb.includes('must-replace') && fb.includes('rejected'),
    'full-build.js の状態語再掲（must-replace/rejected）が消えている — 再生成トリガー規約の退行');
  const proto = await read('workflows/prototype.js');
  assert.ok(proto.includes('must-replace'),
    'prototype.js の must-replace 再掲（fallback 縮退の状態規約）が消えている');
});
