// Workflow DSL スタブハーネス — .claude/workflows/*.js を注入グローバル（agent/parallel/pipeline/
// phase/log/args/budget）のスタブ下で丸ごと実行する。
//
// 検証対象は「スクリプトが自力で決める部分」だけ: プロンプト配線・分岐・エスカレーション蓄積・
// レーン分配。本物の Workflow ランタイム挙動（キャッシュ・並列上限・schema 強制リトライ）は
// 再現しない。parallel はスタブでも DSL と同じく「例外を null に潰す」（thunk が投げても落ちない）。
//
// 実行: node --test .claude/tests/workflows/
import { readFile } from 'node:fs/promises';

// JSON Schema から最小の妥当値を合成する（required のみ・enum は先頭値・boolean は false —
// budgetExceeded/overBudget 等の「true = 異常」フラグを既定で発火させないため）
export function fromSchema(schema) {
  if (!schema || !schema.type) return 'ok';
  switch (schema.type) {
    case 'object': {
      const o = {};
      for (const k of (schema.required || [])) o[k] = fromSchema((schema.properties || {})[k]);
      return o;
    }
    case 'array': return [];
    case 'string': return schema.enum ? schema.enum[0] : 'x';
    case 'number': return 0;
    case 'boolean': return false;
    default: return 'x';
  }
}

// routes: [{ match: RegExp(label), reply: object | (call) => object }] — 先勝ち。
// 不一致は fromSchema(opts.schema) のデフォルト応答。
export async function runWorkflow(path, { args, routes = [] } = {}) {
  const src = await readFile(path, 'utf8');
  const body = src.replace(/^export const meta/m, 'const meta');
  const calls = [];
  const logs = [];
  const phases = [];

  async function agent(prompt, opts = {}) {
    const call = { label: String(opts.label || ''), prompt: String(prompt), opts };
    calls.push(call);
    for (const r of routes) {
      if (r.match.test(call.label)) {
        const v = typeof r.reply === 'function' ? r.reply(call) : r.reply;
        return v === null ? null : structuredClone(v);
      }
    }
    return fromSchema(opts.schema);
  }
  const parallel = (thunks) => Promise.all(
    thunks.map(async (t) => { try { return await t(); } catch { return null; } })
  );
  const pipeline = async (items, ...stages) => {
    const out = [];
    let i = 0;
    for (const item of items) {
      let v = item;
      try { for (const s of stages) v = await s(v, item, i); } catch { v = null; }
      out.push(v); i++;
    }
    return out;
  };
  const phase = (t) => { phases.push(String(t)); };
  const log = (m) => { logs.push(String(m)); };
  const budget = { total: null, spent: () => 0, remaining: () => Infinity };

  const fn = new Function(
    'agent', 'parallel', 'pipeline', 'phase', 'log', 'args', 'budget',
    '"use strict"; return (async () => {\n' + body + '\n})();'
  );
  const result = await fn(agent, parallel, pipeline, phase, log, args, budget);
  return { result, calls, logs, phases };
}

export const callsBy = (calls, re) => calls.filter((c) => re.test(c.label));
export const promptsBy = (calls, re) => callsBy(calls, re).map((c) => c.prompt);
