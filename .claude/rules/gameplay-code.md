---
paths: ["game/src/**"]
---

# gameplay-code — game/src 編集時の強制規約

正本: `.claude/docs/tech-stack.md`。違反は CR-CODE ゲートで CONCERNS 以上になる。

## Do / Don't

- **Do**: ゲームパラメータ（速度・重力・HP・スコア・時間・色・サイズ）は `src/config.ts` の名前付き定数に集約する。チューニングは config.ts の編集だけで完結すること
- **Don't**: Scene や system の本文に数値リテラルを直書きしない（配列 index・`0`/`1` 初期値など意味を持たない値は除く）
- **Do**: 移動・タイマー・クールダウンは `update(time, delta)` の `delta`（ms）でスケールする
- **Don't**: フレーム毎の固定加算（`x += 5`）を書かない。60fps 前提の実装は禁止
- **Don't**: `src/systems/` で `phaser` を import しない。systems はエンジン非依存層（型・数値ロジックのみ）。Phaser 依存は `scenes/` `ui/` `main.ts` に閉じ込める
- **Do**: Scene はライフサイクルと配線のみ（system の生成・入力の受け渡し・描画反映）。判定・状態遷移・スコア計算のロジックは `systems/` の純粋クラス/関数へ
- **Do**: テクスチャキー・アセットパスは `src/config.ts` の `ASSET_KEYS` 経由で参照する
- **Don't**: `this.load.image('hero', 'assets/hero.png')` のような文字列直書き禁止
- **Do**: キー/タッチ入力は入力モジュール1箇所に集約する（Scene ごとに `addKey` を散らさない）
- **Do**: 永続化 I/O（`localStorage`）は `src/persistence/` のみで行う。メタ進行ロジックは `src/systems/meta/` の純粋 reducer（値を受けて値を返す）に置く（tech-stack.md「セーブ / 永続化」）
- **Don't**: `systems/`・`scenes/`・`ui/` から `localStorage` を直接呼ばない
- **Don't**: **セーブ破損時に黙って初期化しない** — パース失敗・`save_version` 欠落・未来版・スキーマ検証失敗（必須フィールド欠落・型不正）は必ず (1) `.bak` キーへ退避 (2) `console.error('[SaveCorruption] ...')` 1回 (3) 既定値再生成＋`recovered` フラグ伝播、の3点セット（contract §6）。catch して既定値を返すだけの実装・フィールド単位で既定値に埋める実装は CR-CODE で CONCERNS 以上

## 正誤例

### マジックナンバー

```ts
// NG: Scene に数値直書き
this.player.setVelocityX(220);
if (score > 1000) this.levelUp();

// OK: config.ts に集約
// src/config.ts
export const PLAYER = { MOVE_SPEED: 220 } as const;
export const SCORE = { LEVEL_UP_THRESHOLD: 1000 } as const;

// 使用側
this.player.setVelocityX(PLAYER.MOVE_SPEED);
if (score > SCORE.LEVEL_UP_THRESHOLD) this.levelUp();
```

### delta-time

```ts
// NG: フレームレート依存（120fpsで2倍速になる）
update() {
  this.x += 5;
  this.cooldown -= 1;
}

// OK: delta（ms）でスケール
update(time: number, delta: number) {
  this.x += PLAYER.MOVE_SPEED * (delta / 1000);
  this.cooldown = Math.max(0, this.cooldown - delta);
}
```

### systems/ のエンジン非依存

```ts
// NG: src/systems/combat.ts
import Phaser from 'phaser';                          // 禁止
export class Combat { hit(s: Phaser.GameObjects.Sprite) { /* ... */ } }

// OK: src/systems/combat.ts — 型と数値ロジックのみ
import { COMBAT } from '../config';
import type { EntityState } from '../types';
export function applyHit(target: EntityState, damage: number): EntityState {
  return { ...target, hp: Math.max(0, target.hp - damage) };
}
```

### 資産参照

```ts
// NG
this.load.image('hero', 'assets/sprites/hero.png');

// OK: src/config.ts
export const ASSET_KEYS = {
  HERO: { key: 'sprite-hero', path: 'assets/sprites/sprite-hero.png' },
} as const;

// 使用側
this.load.image(ASSET_KEYS.HERO.key, ASSET_KEYS.HERO.path);
```
