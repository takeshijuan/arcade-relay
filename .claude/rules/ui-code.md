---
paths: ["game/src/ui/**"]
---

# ui-code — game/src/ui 編集時の強制規約

HUD・メニューはプレイヤーの1秒未満の判断を支えるためにある。gameplay-code.md の規約（config.ts 集約・delta-time・ASSET_KEYS）も同時に適用される。

## Do / Don't

- **Do**: プレイ中に読む HUD 要素（HP・スコア・残時間）は一目で読めるサイズ・コントラストにする。フォントサイズ・色・座標は `src/config.ts` の `UI` 定数に置き、背景と区別できる縁取りか半透明パネルを敷く
- **Don't**: HUD をゲームプレイ領域の中央や操作対象の導線上に置かない。装飾で可読性を犠牲にしない
- **Do**: UI の表示値は game state（`systems/` の状態）から毎回導出する。UI クラスは「受け取って描く」だけ
- **Don't**: UI 側に `score` 等の状態コピーを持って二重管理しない（加算忘れ・不整合の温床）
- **Do**: 画面に出すテキストは `src/config.ts` の `STRINGS` 定数経由にする（将来のローカライズ線。フォーマットは関数化）
- **Don't**: `this.add.text(x, y, 'Game Over')` のような文字列直書き禁止
- **Do**: 「Press Z to jump」等の入力プロンプトのキー表示は、実際のキーバインド定義（入力モジュールの割当）から導出する
- **Don't**: プロンプト文字列にキー名をハードコードしない（リマップ・モバイル対応で表示と実キーがズレる）

## 正誤例

### UI 状態は導出（二重管理禁止）

```ts
// NG: UI が独自にスコアを保持・加算
export class Hud {
  private score = 0;
  addScore(n: number) { this.score += n; this.text.setText(`SCORE ${this.score}`); }
}

// OK: game state を受け取って描くだけ
import { STRINGS } from '../config';
import type { GameState } from '../types';
export class Hud {
  update(state: GameState) {
    this.scoreText.setText(STRINGS.HUD_SCORE(state.score));
  }
}
```

### テキストは STRINGS 経由

```ts
// NG
this.add.text(400, 300, 'Game Over');

// OK: src/config.ts
export const STRINGS = {
  GAME_OVER: 'Game Over',
  HUD_SCORE: (n: number) => `SCORE ${n}`,
} as const;

// 使用側
this.add.text(UI.RESULT_X, UI.RESULT_Y, STRINGS.GAME_OVER, UI.RESULT_STYLE);
```

### 入力プロンプトは実キーバインドから導出

```ts
// NG: キー名ハードコード（リマップすると嘘になる）
this.add.text(x, y, 'Press Z to jump');

// OK: 入力モジュールの割当から表示を作る
// src/config.ts
export const KEY_BINDINGS = { JUMP: 'Z', DASH: 'X' } as const;
export const STRINGS = {
  PROMPT_JUMP: (key: string) => `Press ${key} to jump`,
} as const;

// 使用側（入力モジュールと同じ定義を参照）
this.add.text(x, y, STRINGS.PROMPT_JUMP(KEY_BINDINGS.JUMP), UI.PROMPT_STYLE);
```

### HUD 可読性

```ts
// NG: 小さく低コントラスト、位置も数値直書き
this.add.text(10, 10, `${score}`, { fontSize: '10px', color: '#888888' });

// OK: config の UI 定数 + 縁取りで背景から分離
// src/config.ts
export const UI = {
  HUD_MARGIN: 16,
  HUD_STYLE: { fontSize: '24px', color: '#ffffff', stroke: '#000000', strokeThickness: 4 },
} as const;
```
