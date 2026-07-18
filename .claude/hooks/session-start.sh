#!/bin/bash
# ArcadeRelay hook: SessionStart
# 現在の stage（state/stage.txt）と pipeline.yaml から「現在地と次アクション」を数行で表示する。
# advisory hook: いかなる場合も exit 0。依存ツール不要（grep/sed/awk のみ。macOS bash 3.2 互換）。

# --- プロジェクトルート解決（CLAUDE_PROJECT_DIR 優先、無ければスクリプト位置から逆算） ---
ROOT="${CLAUDE_PROJECT_DIR:-}"
if [ -z "$ROOT" ]; then
  ROOT="$(cd "$(dirname "$0")/../.." 2>/dev/null && pwd)"
fi
[ -z "$ROOT" ] && exit 0

STAGE_FILE="$ROOT/state/stage.txt"
PIPELINE="$ROOT/.claude/docs/pipeline.yaml"
ACTIVE="$ROOT/state/active.md"

# --- stage 未設定なら未開始案内のみ ---
if [ ! -f "$STAGE_FILE" ]; then
  echo "[ArcadeRelay] 未開始。/forge で開始してください。"
  exit 0
fi

STAGE="$(head -1 "$STAGE_FILE" 2>/dev/null | tr -d '[:space:]')"
if [ -z "$STAGE" ]; then
  echo "[ArcadeRelay] state/stage.txt が空です。/forge-status で確認するか /forge で開始してください。"
  exit 0
fi

# --- pipeline.yaml から指定 stage ブロックを抽出するヘルパー（yq 不要の awk 抽出） ---
extract_block() { # $1 = stage名。標準出力にブロック本文（見つからなければ空）
  awk -v s="$1" '
    /^[[:space:]]*-[[:space:]]*stage:[[:space:]]*/ {
      cur=$0; sub(/^[[:space:]]*-[[:space:]]*stage:[[:space:]]*/,"",cur); sub(/[[:space:]#].*$/,"",cur);
      inblk = (cur==s); next
    }
    inblk { print }
  ' "$PIPELINE"
}
field() { # $1 = ブロック本文, $2 = フィールド名
  echo "$1" | grep -m1 "^[[:space:]]*$2:" | sed -e "s/^[[:space:]]*$2:[[:space:]]*//" -e 's/[[:space:]]*#.*$//' -e 's/^"//' -e 's/"[[:space:]]*$//'
}

# --- pipeline.yaml 欠落は stage 未定義と区別して案内 ---
if [ ! -f "$PIPELINE" ]; then
  echo "[ArcadeRelay] stage: $STAGE"
  echo "[ArcadeRelay] pipeline.yaml（.claude/docs/pipeline.yaml）が見つかりません。ハーネスが壊れています。/forge-status で確認してください。"
  exit 0
fi

BLOCK="$(extract_block "$STAGE")"
if [ -z "$BLOCK" ]; then
  echo "[ArcadeRelay] stage: $STAGE"
  echo "[ArcadeRelay] pipeline.yaml に stage '$STAGE' の定義が見つかりません。state/stage.txt が不正な可能性。/forge-status で確認してください。"
  exit 0
fi

TITLE="$(field "$BLOCK" title)"
NEXT="$(field "$BLOCK" next)"

# --- 現在地と次アクション（stage値=そのフェーズ完了済み。次アクションは next stage の command） ---
echo "[ArcadeRelay] stage: $STAGE${TITLE:+ — $TITLE}（このフェーズは完了済み）"
if [ "$STAGE" = "done" ] || [ "$NEXT" = "null" ] || [ -z "$NEXT" ]; then
  echo "[ArcadeRelay] パイプライン完了。成果物は game/ を参照（起動方法はエンジン別 — /forge-status で確認）。"
else
  NEXT_BLOCK="$(extract_block "$NEXT")"
  NEXT_COMMAND="$(field "$NEXT_BLOCK" command)"
  if [ -n "$NEXT_COMMAND" ] && [ "$NEXT_COMMAND" != "null" ]; then
    echo "[ArcadeRelay] 次アクション: ${NEXT_COMMAND} を実行（next stage: ${NEXT}）"
  elif [ -z "$NEXT_BLOCK" ]; then
    echo "[ArcadeRelay] pipeline.yaml に next stage '${NEXT}' の定義が見つかりません。/forge-status で確認してください。"
  else
    # next stage に command が無い（= next: done）。受け渡しのみ残っている
    echo "[ArcadeRelay] 次アクション: /forge で最終報告・受け渡し（next stage: ${NEXT}）"
  fi
fi

# --- state/active.md の冒頭プレビュー ---
if [ -f "$ACTIVE" ]; then
  echo ""
  echo "--- state/active.md（先頭20行） ---"
  head -20 "$ACTIVE" 2>/dev/null
  echo "-----------------------------------"
fi

exit 0
