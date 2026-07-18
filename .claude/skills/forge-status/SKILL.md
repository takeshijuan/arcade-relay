---
name: forge-status
description: ArcadeRelay の現在地表示（読み取り専用）。.claude/docs/pipeline.yaml と state/ とファイル実体を突き合わせ、現在stage・完了/欠落成果物・次に実行すべきコマンド・state/active.md の要約を表示する。何も書き込まない。
argument-hint: "（引数なし）"
allowed-tools: Read, Glob, Grep, Bash
---

# /forge-status — 現在地と次アクションの表示

**読み取り専用スキル。** ファイル書き込み・修復・サブスキル起動は一切しない。矛盾を見つけても報告と提案のみ。

## Phase 1: 状態ファイル読み込み

以下を読む（無いものは「未設定/未着手」として扱う）:

- `state/stage.txt` — 現在stage（`brief|concept|prototype|build|done` の1語。無ければ「未着手」）
- `state/engine.txt` — `phaser|unity|unreal`（無ければ「未設定（既定 phaser）」— contract §11）
- `state/engine-info.json` — エンジン preflight 済みか（unity/unreal のみ。binary 実在も確認）
- `state/review-mode.txt` — `full|lean|solo`（無ければ「未初期化（既定 lean）」）
- `state/budget.txt` — 予算上限USD（無ければ「未初期化（既定 20）」）
- `state/active.md` — セッションハンドオフ（現在地/次アクション/未解決事項）
- `state/asset-routing.json` — preflight済みか（`degraded` フラグも確認）
- `.claude/docs/pipeline.yaml` — stage順序・各stageの required artifacts・command の情報源

## Phase 2: 成果物の実体チェック

pipeline.yaml の各 stage の `artifacts.required` を実体と突き合わせる（存在かつ非空を合格とする）。**`engine:` フィールド付きの成果物は、`state/engine.txt` の値（無ければ phaser）に一致する行だけを対象にする**（pipeline.yaml 冒頭コメント参照）:

```bash
ENGINE="$(cat state/engine.txt 2>/dev/null || echo phaser)"
# 共通成果物
for f in design/brief.md \
         design/concept.md design/gdd.md design/art-bible.md design/art-bible.json design/assets.md \
         docs/architecture.md docs/conventions.md state/stories.yaml qa/report.md; do
  [ -s "$f" ] && echo "OK  $f" || echo "--  $f"
done
# エンジン別成果物（pipeline.yaml の engine フィールド）
case "$ENGINE" in
  phaser) for f in game/package.json game/assets/MANIFEST.jsonl; do [ -s "$f" ] && echo "OK  $f" || echo "--  $f"; done ;;
  unity)  for f in game/ProjectSettings/ProjectVersion.txt game/_generated/MANIFEST.jsonl; do [ -s "$f" ] && echo "OK  $f" || echo "--  $f"; done ;;
  unreal) for f in game/ForgeGame.uproject game/_generated/MANIFEST.jsonl; do [ -s "$f" ] && echo "OK  $f" || echo "--  $f"; done ;;
  *) echo "!! state/engine.txt が不正: '$ENGINE'（contract §11 の3値のみ）— エンジン別成果物は判定不能" ;;
esac
```

（上のリストは pipeline.yaml 現行値。**pipeline.yaml を読んだ結果を正とし**、変更されていればそちらに従う。）

## Phase 3: 整合判定と「次コマンド」決定

1. **現在stage**: `state/stage.txt` の値。stage値は「そのフェーズが完了済み」を意味する（contract.md §1）。
2. **矛盾検出**: stage値が要求する成果物（当該stageまでの全 required）に欠落があれば警告する（例: stage=`concept` なのに `design/gdd.md` 欠落 → 「stage表記と実体が不一致。/forge-concept の再実行が必要」）。逆に stage未達なのに先の成果物が存在する場合も注記する。
3. **次に実行すべきコマンド**（pipeline.yaml の `next` → その stage の `command`）:

| 現在stage | 次コマンド |
|---|---|
| 未着手 | `/forge`（preflightから開始）または `/forge-brainstorm` |
| `brief` | `/forge-concept`（`state/asset-routing.json` が無ければ先に `/forge` で preflight） |
| `concept` | `/forge-prototype` |
| `prototype` | `/forge-build` |
| `build` | `/forge-build`（受領確認の再実施。`/forge` で再開しても Phase 5 = /forge-build 再実行に入る） |
| `done` | なし（完成。起動コマンドはエンジン別 — phaser: `cd game && npm run dev` / unity: `open game/Build/ForgeGame.app` / unreal: `open game/Build/Mac/ForgeGame.app`） |

いずれの位置からも `/forge` は冪等に再開できることを添える。

## Phase 4: 補助情報の集計

存在するものだけ集計する（無ければスキップ）:

```bash
# ストーリー進捗（state/stories.yaml）
grep -c 'status: done'        state/stories.yaml
grep -c 'status: in-progress' state/stories.yaml
grep -c 'status: todo'        state/stories.yaml

# 資産コスト実績 vs 予算（MANIFEST はエンジン別正本 — phaser: game/assets/ / unity・unreal: game/_generated/）
MANIFEST="game/assets/MANIFEST.jsonl"; [ "$ENGINE" != "phaser" ] && MANIFEST="game/_generated/MANIFEST.jsonl"
jq -s 'map(.cost_usd // 0) | add' "$MANIFEST"
cat state/budget.txt

# 出荷前に差し替え必須の資産
jq -c 'select(.must_replace == true) | .file' "$MANIFEST"

# 未解決レビュー（最新 iteration が非APPROVEのまま終わっているファイル）
grep -l 'CONCERNS\|REJECT' state/reviews/*.md 2>/dev/null
```

`state/reviews/*.md` は grep でヒットしたファイルの**末尾の iteration 見出し**（`## <GATE-ID> iteration <n> — <verdict>`）を読み、最終verdictが APPROVE でないものだけを未解決として扱う。

## Phase 5: 表示

以下のフォーマットで出力する（該当なしの行は省略）:

```
# ArcadeRelay Status

現在stage : <stage>（<stageの意味>）    engine: <engine>    review-mode: <mode>    予算: $<実績> / $<上限>
preflight : <済（degraded: false）| 未実施>    エンジンpreflight : <済（<version>）| 未実施 | 不要（phaser）>

## パイプライン進捗
[x] brief      ブレスト            /forge-brainstorm
[x] concept    企画・設計 (CP-A)    /forge-concept
[ ] prototype  プロトタイプ (CP-B)  /forge-prototype   ← 現在地
[ ] build      本実装 (CP-C)        /forge-build
[ ] done       完成

## 成果物
OK/-- の一覧（Phase 2 の結果。欠落は「欠: <path>」として現在stageとの矛盾有無を明記）

## 未解決事項
- ストーリー: done <n> / in-progress <n> / todo <n>
- 未解決レビュー: <artifact名と最終verdict>
- must_replace 資産: <一覧>

## 次のアクション
→ <次コマンド>（理由1行）
※ /forge でどこからでも再開可能

## state/active.md 要約
<現在地/次アクション/未解決事項を3〜5行に要約。無ければ「未作成」>
```
