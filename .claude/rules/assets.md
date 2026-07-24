---
paths: ["game/assets/**", "game/_generated/**"]
---

# assets — 生成資産配置時の強制規約

生成・配置ルールの正本は `.claude/docs/assets-config.md`。ここは配置時に効くチェックリスト。
対象ディレクトリはエンジン別（contract.md §11）: `game/assets/`（phaser）/ `game/_generated/`（unity・unreal の raw 生成物＋MANIFEST）。以下「MANIFEST」は当該エンジンの正本パスを指す。

## Do / Don't

- **Do**: ファイル名は kebab-case + 種別プレフィクス。`sprite-`（キャラ/オブジェクト画像）/ `tile-`（タイル/背景）/ `ui-`（UI画像）/ `sfx-`（効果音）/ `bgm-`（音楽）/ `model-`（3Dモデル）/ `anim-`（スケルタルアニメーション）
  - 例: `sprite-hero-idle.png` `tile-forest-ground.png` `ui-button-start.png` `sfx-jump.ogg` `bgm-stage-1.ogg` `model-hero.fbx` `anim-hero-run.fbx`
- **Don't**: `Hero.png` `enemy_01.png` `jump sound.ogg` のような camelCase・snake_case・空白・プレフィクス無しの名前を使わない
- **Do**: 資産の追加・差し替え・リタッチのたびに MANIFEST（当該エンジンの正本パス — contract §6）へ1行追記する。必須フィールド: `file` `provider` `model` `prompt` `seed` `style_codes` `cost_usd` `plan_tier` `sha256` `license` `generated_at`。プレースホルダは加えて `"must_replace": true`
- **Don't**: MANIFEST 追記なしの資産配置禁止（予算強制・Steam AI 開示・著作権防御が全部これに依存する）。既存行の書き換え・削除も禁止（追記のみ）
- **Don't**: `"must_replace": true` の資産を残したまま出荷（Checkpoint C / stage=done）しない。ビルド前に MANIFEST を走査し、残っていれば差し替えてから進む
- **Don't**: 白背景（不透明背景）PNG のスプライト/UI 画像を配置しない。全画像はアルファチャンネル必須で、配置前に機械検証する
- **Do**: 音声は OGG（Vorbis 128–160kbps）と M4A/AAC（Safari 用）の両形式を同名で配置し、MANIFEST には両ファイルを記録する（phaser。unity/unreal はエンジン推奨形式 — assets-config.md「生成後パイプライン」音声行 / 各 tech-stack 文書「資産の取り扱い」）
- **Don't**: ライセンス不明・非商用（`bria-rmbg` 出力、ElevenLabs Free、audiocraft 出力等）の資産を `license: commercial-ok` として記録しない
- **Do**: 3Dモデル（`model-*`）は配置前に assets-config.md 3D 節の機械検証（スキーマ検証: GLB=gltf-transform validate / FBX=Blender 変換後 validate、ポリゴン数・ボーン数・マテリアル数・authoring 寸法）を通し、MANIFEST の 3D フィールド（`kind` `polycount` `bone_count` `rigged` `format` `bbox_authoring_m` `validator` `plan_tier`。クレジット換算見積なら `cost_estimated: true`）を必ず記録する
- **Don't**: エンジン取込先（unity: `game/Assets/Resources/Generated/` / unreal: `game/Content/Generated/`）への直接生成禁止。raw は必ず `game/_generated/` に置き、MANIFEST 追記後に取り込む

## 正誤例

### 命名

```
NG: game/assets/Hero.png
NG: game/assets/sprites/enemy_slime.png
NG: game/assets/audio/Jump Sound.ogg
OK: game/assets/sprites/sprite-hero-idle.png
OK: game/assets/sprites/sprite-enemy-slime.png
OK: game/assets/audio/sfx-jump.ogg + game/assets/audio/sfx-jump.m4a
OK: game/assets/audio/bgm-stage-1.ogg + game/assets/audio/bgm-stage-1.m4a
```

### MANIFEST.jsonl 追記

```json
{"file":"assets/sprites/sprite-hero-idle.png","provider":"fal:ideogram-v3-transparent","model":"ideogram-v3","prompt":"<style_block> + hero idle pose","seed":12345,"style_codes":["ABC123"],"cost_usd":0.06,"plan_tier":"prepaid","sha256":"e3b0c442...","license":"commercial-ok","generated_at":"2026-07-03T12:00:00Z"}
```

プレースホルダの場合（出荷禁止フラグ必須）:

```json
{"file":"assets/audio/bgm-stage-1.ogg","provider":"local:audiocraft","model":"musicgen","prompt":"...","seed":7,"style_codes":[],"cost_usd":0,"plan_tier":"local","sha256":"...","license":"placeholder-nc","must_replace":true,"generated_at":"2026-07-03T12:00:00Z"}
```

### アルファ検証（配置前に必ず実行）

```bash
# NG: 検証せず配置
# OK: アルファチャンネルの有無を機械検証してから配置
python3 -c "
from PIL import Image; import sys
im = Image.open('game/assets/sprites/sprite-hero-idle.png')
assert im.mode == 'RGBA' and im.getextrema()[3][0] < 255, 'no alpha / opaque background'
"
```
