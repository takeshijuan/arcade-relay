# ArcadeRelay レビューループ定義

> **全成果物は produce→review→revise ループを通過してから次工程へ進む。**
> workflow スクリプトはこの表に従ってループを実装する。ゲート・プロンプト本文は gates.md をIDで参照。

## ループの共通形

```
artifact = produce(producer)
for i in 1..MAX_ITER:
    verdict = review(reviewer, GATE-ID)      # 1行目 = <GATE-ID>: APPROVE|CONCERNS|REJECT
    append verdict → state/reviews/<artifact>.md   # ※追記は reviewer agent の責務（verdict を返す前に自身で追記する）。workflow は追記しない
    if APPROVE: break
    artifact = revise(producer, verdictの指摘)
if MAX_ITER到達かつ非APPROVE:
    エスカレーション（未解決指摘一覧を付けて次のCheckpointで人間に提示。パイプラインは止めない）
```

- レビュー履歴は必ず `state/reviews/<artifact>.md` に**追記**する（iteration番号・verdict・指摘要約・日時）
- reviser は指摘への対応/非対応を明記する（黙殺禁止）

## 対応表

| 成果物 | producer | reviewer | Gate ID | MAX_ITER | 合格基準 |
|---|---|---|---|---|---|
| design/concept.md | game-designer | design-reviewer | DR-CONCEPT | 3 | APPROVE |
| design/gdd.md | game-designer | design-reviewer | DR-GDD | 3 | APPROVE |
| design/art-bible.md + .json | art-director | art-reviewer | AR-BIBLE | 3 | APPROVE |
| 生成資産バッチ | art-director / audio-designer | art-reviewer | AR-ASSET | 3/資産 | APPROVE（3回不合格→fallbackプロバイダへ切替後さらに1回） |
| story実装 (game/ コード diff。対象パスは contract §11) | gameplay-engineer / ui-engineer | 既存 code-review | CR-CODE | 2 | findings解消 or 正当理由の明記 |
| 動く game/ | (全engineer) | qa-lead | QA-PLAY | 2 | 重大バグ0・acceptance全通過 |
| Checkpoint提示物 | (フェーズ全体) | creative-director | CD-CHECKPOINT | 1 | APPROVE（REJECTなら指示に従い修正後1回だけ再判定） |

## state/reviews/<artifact>.md の追記形式

```markdown
## <GATE-ID> iteration <n> — <verdict>
- 日時: <ISO8601>
- 指摘要約: （CONCERNSの場合、優先度順）
- 対応: （reviseした側が記入。対応済み/見送り＋理由）
```

## review-mode による変調（contract.md §9）

- `full`: ループは自動。workflow が全 verdict 履歴（gate/artifact/iteration/verdict/指摘要約）を蓄積して戻り値に含め、スキルが完了後の Checkpoint 提示で全件を人間に提示（実行中の都度提示は行わない）
- `lean`（既定）: ループは自動。MAX_ITER到達の未解決指摘のみCheckpointで人間へ
- `solo`: 同上（Checkpoint自体も停止しないため、未解決指摘は最終報告に記載）
