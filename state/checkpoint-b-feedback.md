# Checkpoint B フィードバック（2026-07-22）

- 判断: Phase 3（build）へは進まず一旦停止。stage=prototype で保存。
- 人間の指示: 「ここまでの作業に基づいてハーネスへのフィードバックをして欲しい。監査者として、クオリティ・パフォーマンス・あらゆる面から辛口の指摘を」
- Phase 3 を後日実行する場合、この監査の指摘が Replan の入力を兼ねる。

## Phase 3 Replan への具体入力（ハーネス監査 retro-e3 由来・2026-07-22T06:40:22Z 追記）

1. **環境ビジュアルの本仕上げを最優先** — Checkpoint B の盤面が暗い（地形/背景未生成・カメラ本配置未了）。IMG-01/02 生成 + カメラ/ライトの本配置を build 序盤の story に置く（retro-e3 指摘2）
2. **MDL-04 Warbeast は fallback チェーンを全段試行して再生成** — Primary 失敗時に placeholder 直行禁止（新規約）。試行ルート + HTTP コードを degradedRoutes に列挙（Tripo は preflight 認証 200 済み・残量は使用時確認）
3. **design/assets.md:82 の取込先記述を修正** — 「game/Assets/Generated/」→「game/Assets/Resources/Generated/」（contract §11 改定に追随。Replan の文書更新で対応）
4. 残資産: IMG-05/06/07・BGM-01・SFX-05 配線・P-03 撃破演出・UPG 効果反映（S-10〜S-20 既存 todo のとおり）
