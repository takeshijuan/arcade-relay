# ArcadeRelay — 自律ゲーム制作ハーネス

ブレスト1回 → エージェント群が数時間の自律作業で、遊べるゲームを丸ごと作り上げるハーネス。対応エンジンは3つ（contract §11・`state/engine.txt`）: **phaser**（ブラウザ2D・Phaser 3 + TypeScript + Vite・既定）/ **unity**（3D・Unity 6 LTS）/ **unreal**（3D・UE 5.x）。人間は3つのチェックポイント（A: 企画設計承認 / B: プロトタイプfeedback / C: 完成受領）でのみ介入する。

## 使い方（入口）

ArcadeRelay のコマンド名前空間は互換性のため `/forge` のまま維持する。

- `/forge` — マスター入口。preflight → ブレスト → 3フェーズを順に自律実行
- `/forge-status` — 現在地と次アクションの表示
- 個別実行: `/forge-brainstorm` `/forge-concept` `/forge-prototype` `/forge-build`

## 絶対規約

1. **命名・ID・パスは contract に従う** — 発明禁止。 @.claude/docs/contract.md
2. **全成果物は produce→review→revise ループを通す** — 合格基準は @.claude/docs/review-loops.md
3. **ゲーム実装はエンジン別 tech-stack 規約に従う** — phaser: @.claude/docs/tech-stack.md / unity: `.claude/docs/tech-stack-unity.md` / unreal: `.claude/docs/tech-stack-unreal.md`（engine は `state/engine.txt`。無ければ phaser）
4. **資産生成はルーティング表に従う** — @.claude/docs/assets-config.md
5. **状態はファイルが真実** — 会話ではなく `state/` を読む。作業後は `state/active.md` を更新
6. **ピラー（P-xx）が北極星** — 全ての設計・実装・QA判断は `design/concept.md` のピラーに照らす

## パイプライン全体像

@.claude/docs/pipeline.yaml がフェーズ定義。ゲート判定プロンプトは @.claude/docs/gates.md 。

## このリポジトリの構造

- `.claude/` — ハーネス本体（agents / skills / workflows / hooks / rules / docs / tests）
- `design/` `docs/` `qa/` `state/` — パイプラインが生成する成果物・状態
- `game/` — 生成される自己完結ゲームプロジェクト（中身は engine 別: Vite+TS+Phaser / Unity / UE — contract §11）
