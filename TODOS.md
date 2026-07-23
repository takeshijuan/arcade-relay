# TODOS

## Harness (ArcadeRelay)

### 依存グラフ並列（retro-e2 案C — 案A+B の次段）
**What:** stories.yaml に `depends_on: [S-xx]` を宣言し、独立 story を assignee 跨ぎで最大 N 並列化する（実装済みの assignee 2レーンの一般化）。
**Why:** 案A+B（assignee レーン並走 + 検証バッチ化）は 2026-07-21 実装済み。さらに縮めるには依存グラフが要る。
**Context:** 設計案は `.claude/docs/retro-e2.md` 並列化節の案C。worktree 分離は Unity では非推奨（Library 複製コスト + 単一インスタンスロック）。同一ツリー並列には競合レビュー（同一ファイル編集検出）の Setup 機械化が必要。
**Effort:** L
**Priority:** P3
**Depends on:** E3 ランでの案A+B 実測（レーン競合率・batch-verify 失敗率）

### Unity 職能スキル群（Timeline / Animator / VFX / UI 装飾）
**What:** Unity の各機能（Timeline, Animator, Particle/VFX Graph, UI 装飾/トゥイーン）ごとの専門スキルを作り、Build Phase の該当 story で起動する。
**Why:** ユーザーフィードバック「AA レベルには程遠い。特にエフェクト・UI・UX 面。Unity の各機能のスキルを作ってそれぞれ依頼するのがいい」。汎用 gameplay/ui-engineer では表現の引き出しが浅い。
**Context:** スキル候補と分担案は `.claude/docs/retro-e2.md` の craft-skill 節。
**Effort:** XL
**Priority:** P2
**Depends on:** None

### W-3: GEN_SCHEMA の assetKind 列挙を contract §8 と機械同期
**What:** workflow スクリプトの GEN_SCHEMA（資産生成の構造化返却）の assetKind 語彙を contract §8 の資産 ID 種別（IMG/SFX/BGM/MDL/ANM）と単一情報源化する。
**Why:** adversarial レビュー INVESTIGATE 項目 W-3。現在は各 workflow に手書きで、contract 変更時にドリフトし得る。
**Effort:** S
**Priority:** P2
**Depends on:** None

### P-1: クールダウン系の generation-ID 化
**What:** AutoAttackDriver 等のクールダウン管理を pooled 再利用に耐える generation-ID（rent 世代カウンタ）方式へ移行するか、現行リセット方式で十分かを調査する。
**Why:** adversarial レビュー INVESTIGATE 項目 P-1。pooled 再利用で前 life のタイマー参照が理論上残り得る（現行テストでは非再現）。
**Effort:** M
**Priority:** P3
**Depends on:** None

### Workflow resume キャッシュと再実行 story の二重適用ガード（adversarial M-8b）
**What:** Workflow ツールのキャッシュキー仕様（(prompt, opts) 一致）を前提に、途中編集→resume で実装済み story の impl agent が再実行された場合の「config への同一定数二重追記」を防ぐガード（プロンプトに「既に自 story の定数が存在すれば追記しない」等の冪等化）を検討する。
**Why:** 並列化後は agent 数が増え resume 確率が上がる。二重追記は batchVerify の重複定義エラーとして無実の story に誤帰属し得る（adversarial レビュー M-8b・INVESTIGATE）。
**Effort:** S
**Priority:** P3
**Depends on:** None

### 並走レーン規律の実走検証（E3 検証負債）
**What:** レーン規律（LANE_RULE / laneVerify / パス指定 commit / hash 実証検証）の agent 遵守率と batch-verify 失敗率・レーン競合率を E3 ランで実測し、逸脱があればプロンプトではなく機械強制（hook / ツール制限）へ昇格する。
**Why:** DSL スタブテストはスクリプト側分岐を検証するが、プロンプト強制の遵守はライブ実行でしか測れない（/ship coverage 監査 GAPS 4・6）。
**Effort:** M
**Priority:** P2
**Depends on:** E3 ラン実施

### P-5: UrpShaderUtil warn-once の観測性
**What:** UrpShaderUtil の shader フォールバック warn-once が縮退発生の観測を妨げないか調査し、必要なら発生カウントを QA レポートへ集約する。
**Why:** adversarial レビュー INVESTIGATE 項目 P-5。warn-once は spam 防止と観測性のトレードオフ。
**Effort:** S
**Priority:** P3
**Depends on:** None

## Completed

### Build Phase の並列化（retro-e2 案A+B）
**What:** prototype.js / full-build.js の story 実装を assignee 2レーン（gameplay/ui）並走にし、エンジン検証をレーン合流後のバッチ検証区間（直列・story 単位切り分け付き）へ集約した。
**Why:** ユーザーフィードバック「Build Phase はとても時間がかかっている」。E2 実測 Build ≈ 6h / Phase 3 ≈ 9h+9h の主因が story 直列 × story ごとの Unity 検証（3〜8 分）だった。期待短縮 5〜6 割。DSL スタブテスト（.claude/tests/workflows/・15件）で batchVerify 全分岐とレーン分配を機械検証済み。
**Completed:** v0.3.0.0 (2026-07-21)
