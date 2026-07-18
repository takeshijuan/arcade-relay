#!/bin/bash
# ArcadeRelay hook: PostToolUse (Write|Edit)
# stdin の JSON イベントから file_path を抽出し、game/ 配下のコード変更なら
# エンジン別の検証実行を促す一言を出す（phaser: .ts / unity: .cs / unreal: .cpp/.h）。それ以外は無言。
# advisory hook: いかなる場合も exit 0。jq があれば使い、無ければ grep/sed フォールバック。

INPUT="$(cat 2>/dev/null)"
[ -z "$INPUT" ] && exit 0

FILE_PATH=""
if command -v jq >/dev/null 2>&1; then
  FILE_PATH="$(printf '%s' "$INPUT" | jq -r '.tool_input.file_path // empty' 2>/dev/null)"
fi
if [ -z "$FILE_PATH" ]; then
  # フォールバック: 最初の "file_path":"..." を素朴に抜く（エスケープ無しの通常パス前提）
  FILE_PATH="$(printf '%s' "$INPUT" | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed -e 's/^"file_path"[[:space:]]*:[[:space:]]*"//' -e 's/"$//')"
fi
[ -z "$FILE_PATH" ] && exit 0

case "$FILE_PATH" in
  */game/src/*.ts|game/src/*.ts)
    echo "[ArcadeRelay] game/src/ の TypeScript を変更しました。変更後は cd game && npm run typecheck を実行して exit 0 を確認してください。"
    ;;
  */game/Assets/*.cs|game/Assets/*.cs)
    echo "[ArcadeRelay] game/Assets/ の C# を変更しました。変更後は .claude/docs/tech-stack-unity.md の「検証コマンド」（batchmode コンパイル検証）で exit 0 を確認してください。"
    ;;
  */game/Source/*.cpp|game/Source/*.cpp|*/game/Source/*.h|game/Source/*.h)
    echo "[ArcadeRelay] game/Source/ の C++ を変更しました。変更後は .claude/docs/tech-stack-unreal.md の「検証コマンド」（UnrealBuildTool ビルド）で exit 0 を確認してください。"
    ;;
esac

exit 0
