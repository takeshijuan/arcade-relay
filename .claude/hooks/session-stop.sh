#!/bin/bash
# ArcadeRelay hook: Stop
# state/ が存在する場合のみ、セッション終了を state/session-log.txt に1行追記する。
# advisory hook: いかなる場合も exit 0。標準出力には何も出さない。

ROOT="${CLAUDE_PROJECT_DIR:-}"
if [ -z "$ROOT" ]; then
  ROOT="$(cd "$(dirname "$0")/../.." 2>/dev/null && pwd)"
fi
[ -z "$ROOT" ] && exit 0
[ -d "$ROOT/state" ] || exit 0

STAGE="none"
if [ -f "$ROOT/state/stage.txt" ]; then
  S="$(head -1 "$ROOT/state/stage.txt" 2>/dev/null | tr -d '[:space:]')"
  [ -n "$S" ] && STAGE="$S"
fi

echo "session end: $(date -u +%Y-%m-%dT%H:%M:%SZ) stage=$STAGE" >> "$ROOT/state/session-log.txt" 2>/dev/null

exit 0
