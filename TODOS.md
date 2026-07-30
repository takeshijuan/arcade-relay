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

### P-1: クールダウン系の generation-ID 化
**What:** AutoAttackDriver 等のクールダウン管理を pooled 再利用に耐える generation-ID（rent 世代カウンタ）方式へ移行するか、現行リセット方式で十分かを調査する。
**Why:** adversarial レビュー INVESTIGATE 項目 P-1。pooled 再利用で前 life のタイマー参照が理論上残り得る（現行テストでは非再現）。
**Effort:** M
**Priority:** P3
**Depends on:** None

### 並走レーン規律の実走検証（E3 検証負債）
**What:** レーン規律（LANE_RULE / laneVerify / パス指定 commit / hash 実証検証）の agent 遵守率と batch-verify 失敗率・レーン競合率を E3 ランで実測し、逸脱があればプロンプトではなく機械強制（hook / ツール制限）へ昇格する。
**Why:** DSL スタブテストはスクリプト側分岐を検証するが、プロンプト強制の遵守はライブ実行でしか測れない（/ship coverage 監査 GAPS 4・6）。
**Effort:** M
**Priority:** P2
**Depends on:** E3 ラン実施

### agent 返却文字列のシェルコマンドテンプレート埋め込み（adversarial 2026-07-30）
**What:** workflow プロンプトが `git commit -m "... — <bug.title>"` / `"<story.title>"` の形で agent 返却文字列をコミットコマンド例へ直埋めしており、`"` や `$( )` を含む title が引用を破る。`-F` ファイル方式への変更かサニタイズ指示を検討する。
**Why:** adversarial レビュー INVESTIGATE（2026-07-30・v0.4.1.0 出荷時に持ち越し）。既存経路で diff 起源ではないが、判定プロンプト注入対策（同 PR で修正）と同族の trust boundary。
**Effort:** S
**Priority:** P2
**Depends on:** None

### unresolvedFindings の順序非決定性と resume キャッシュ分岐（adversarial 2026-07-30）
**What:** 並走レーンの push 順は interleaving 依存で、resume 再走時に CD-CHECKPOINT / finalize プロンプトの直列化文字列が変わりキャッシュ外れ→再判定が起き得る。プロンプト焼き込み直前の安定 sort か、再判定許容の割り切りかを判断する。
**Why:** adversarial レビュー INVESTIGATE（2026-07-30）。E3 実測の「キャッシュ分岐連鎖 ≈1h 浪費」の面を並走 push サイト増加が広げる可能性。
**Effort:** S
**Priority:** P3
**Depends on:** None

### P-5: UrpShaderUtil warn-once の観測性
**What:** UrpShaderUtil の shader フォールバック warn-once が縮退発生の観測を妨げないか調査し、必要なら発生カウントを QA レポートへ集約する。
**Why:** adversarial レビュー INVESTIGATE 項目 P-5。warn-once は spam 防止と観測性のトレードオフ。
**Effort:** S
**Priority:** P3
**Depends on:** None

## Completed

### W-3: 資産種別の contract §8 機械同期（GEN_SCHEMA assetKind 改め）
**What:** 実態調査の結果 assetKind は GEN_SCHEMA に既に無く、真のドリフト面は full-build.js の MODEL_WORDS 語彙判定だった。Replan プロンプトに資産種別タグ（[MDL]/[ANM]/[IMG]/[SFX]/[BGM]）付与を義務化してタグ第一・語彙 fallback の振り分けに変更し、`.claude/tests/workflows/contract-sync.test.mjs` が contract §8 の ID 種別・状態語彙とスクリプト再掲の同期を機械検証する。2D エンジンで 3D 判定 story が両バッチから脱落するケースも [BLOCKER] 蓄積化。
**Completed:** v0.4.1.0 (2026-07-30)

### Workflow resume の二重適用ガード（adversarial M-8b）
**What:** IDEMPOTENT_RULE（作業前に既存コミット・定数・注記・MANIFEST を確認し重複追記しない）を full-build.js / prototype.js の impl / fix / close / bookkeep / integrate プロンプトへ前置。prototype.js の bookkeep に「既に done なら何もしない」冪等文言を移植し、fix-qa label を bug 単位（`fix-qa-r<N>-<assignee>-<idx>`）に一意化。
**Completed:** v0.4.1.0 (2026-07-30)

### レーン/トラック例外・エラー経路の無記録解消（監査 2026-07-29）
**What:** parallel() の例外 null 潰しでレーン中断が未解決事項に載らない穴を laneSafe（thunk 内 catch → [BLOCKER] 蓄積）で全 parallel サイト（Build/Polish/AssetGen/FullQA）に適用。full-build.js の close/fix/replan-gdd/QA fix/cd-fix/drift 空 failedAssets、prototype.js の CD 再判定 null、concept-design.js の CD fix null の無記録経路を解消。DSL テスト 31→59 件（concept-design.test.mjs / contract-sync.test.mjs 新設）。
**Completed:** v0.4.1.0 (2026-07-30)

### Build Phase の並列化（retro-e2 案A+B）
**What:** prototype.js / full-build.js の story 実装を assignee 2レーン（gameplay/ui）並走にし、エンジン検証をレーン合流後のバッチ検証区間（直列・story 単位切り分け付き）へ集約した。
**Why:** ユーザーフィードバック「Build Phase はとても時間がかかっている」。E2 実測 Build ≈ 6h / Phase 3 ≈ 9h+9h の主因が story 直列 × story ごとの Unity 検証（3〜8 分）だった。期待短縮 5〜6 割。DSL スタブテスト（.claude/tests/workflows/・15件）で batchVerify 全分岐とレーン分配を機械検証済み。
**Completed:** v0.3.0.0 (2026-07-21)
