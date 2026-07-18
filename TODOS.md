# TODOS

## Game (E2 arena survivor)

### MDL-02（スウォーマー）の正式リグ化
**What:** must-replace 印付きの MDL-02（敵スウォーマー）をリグ・アニメ付きモデルに差し替える。
**Why:** 現状は S-21 のコードモーション代替（プロシージャル・ボブ/回転）で出荷しており、`game/_generated/MANIFEST.jsonl` に must-replace が残っている。rules/assets.md は must-replace 残存での出荷を禁止しており、Checkpoint C で人間開示済みの暫定容認状態。
**Context:** Meshy rigging が quadruped 形状で 422、Tripo 直 API はクレジット不足で 403 だった（E2 AssetGen ログ・state/reviews 参照）。Tripo クレジット補充後に `TRIPO_API_KEY` ルートで再リグ→ANM 再生成→Integrate 再実行が最短。
**Effort:** M
**Priority:** P1
**Depends on:** Tripo クレジット補充（人間）

### Ideogram AI 生成表記の MANIFEST/開示追記
**What:** Ideogram 生成画像（IMG-01〜06 の該当分）について「アプリ内 AI 生成表記」条項への対応注記を MANIFEST とリリース開示文へ追記する。
**Why:** assets-config.md「Checkpoint で人間に提示するライセンスフラグ」に列挙済みだが、機械可読の追記が未実施。Steam AI 開示文の自動生成が MANIFEST を情報源とするため欠落すると開示漏れになる。
**Effort:** S
**Priority:** P1
**Depends on:** None

### バッチモード・テスト生成パス（カバレッジギャップ 30 件）
**What:** /ship Step 7 のカバレッジ監査で特定した約 30 件の未テスト分岐（主に Components 層の配線ガード・縮退経路）へ EditMode/PlayMode テストを追加する。
**Why:** diff カバレッジ 86% で出荷したが、未カバー分岐の多くはエラー縮退経路＝退行が無症状化しやすい箇所。
**Context:** ギャップ一覧は /ship レビュー記録（~/.gstack/projects/<slug>/<branch>-reviews.jsonl）と PR 本文のカバレッジ節を参照。
**Effort:** L
**Priority:** P2
**Depends on:** None

### S-19 SFX テスト債務
**What:** S-19（音声統合）の SFX 配線に対する自動テスト（AudioSource 配線・音量バス・autoplay 対応）を追加する。
**Why:** S-19 は CR-CODE 指摘で「テスト債務」として見送り理由付きで出荷しており、state/reviews/s-19.md に記録が残っている。
**Effort:** M
**Priority:** P2
**Depends on:** None

### S-20/S-22 南側可視性の残存制約
**What:** ARENA_RADIUS 全域の南側（カメラ手前側）可視性を完全カバーする（現状は部分対応）。
**Why:** Checkpoint C 申し送り事項。カメラレンジ拡張は見た目（key image 構図）とのトレードオフで art-director 協議が要る。
**Effort:** M
**Priority:** P3
**Depends on:** art-director 協議

### CrystalSceneTests の flaky 管理
**What:** PlayMode フルスイート中に 1 回だけ発生した CrystalSceneTests の failure（再現せず）を flaky 管理項目として監視し、次回発生時は原因調査を打ち切らない。
**Why:** S-27 CR-CODE iteration 2 で「timing 起因とみられる」として棄却されかけた項目。マスクされた欠陥の温床になり得るため qa-lead 引き継ぎ済み（state/active.md）。
**Effort:** S
**Priority:** P3
**Depends on:** None

## Harness (ArcadeRelay)

### Build Phase の並列化（retro-e2 提案）
**What:** full-build.js の story 実装レーンを依存グラフベースで並列化する（現在は Unity 単一インスタンスロックに引きずられ実装レーンまで直列）。
**Why:** ユーザーフィードバック「Build Phase はとても時間がかかっている」。実装（コード編集）は並列可能で、Unity 起動を要する検証だけを直列キューに載せれば大幅短縮できる。
**Context:** 設計案は `.claude/docs/retro-e2.md` の並列化節。ロック境界（検証コマンド実行のみ）と worktree 分離の要否を検討する。
**Effort:** L
**Priority:** P2
**Depends on:** None

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

### P-5: UrpShaderUtil warn-once の観測性
**What:** UrpShaderUtil の shader フォールバック warn-once が縮退発生の観測を妨げないか調査し、必要なら発生カウントを QA レポートへ集約する。
**Why:** adversarial レビュー INVESTIGATE 項目 P-5。warn-once は spam 防止と観測性のトレードオフ。
**Effort:** S
**Priority:** P3
**Depends on:** None

## Completed
