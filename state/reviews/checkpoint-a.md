# レビュー履歴 — Checkpoint A（CD-CHECKPOINT）

## CD-CHECKPOINT iteration 1 — APPROVE
- 日時: 2026-07-10T16:25:00+09:00
- 対象: design/brief.md / design/concept.md / design/gdd.md / design/art-bible.md / design/art-bible.json / design/assets.md（engine=unity・review-mode=lean）
- 判定観点（gates.md CD-CHECKPOINT）:
  1. **ビジョン一貫性 — 合格**: concept の一文/コアループ/MDA/設計判断が「単体ヒット自動攻撃・固定俯瞰カメラ・基本1敵種」で一貫。gdd の全11システムが P-01〜P-04 のいずれかを参照し、逆にピラーも全てシステムへ落ちている。assets.md の全資産（IMG/SFX/BGM/MDL/ANM）が P-xx を参照。brief の「盛らない宣言」と資産上限（画像12点内=4点／SFX6+BGM1=過不足なし／3Dモデル4点内=2生成+1マテリアルバリアント／予算$100・見積$6-10に対し概算$3程度）を全て遵守。ピラーからの逸脱・brief 外の追加なし。
  2. **提示品質 — 合格（条件付き）**: 何を作ったか（企画=concept、設計=gdd、アート=art-bible/json、資産計画=assets）／何を判断してほしいか（key image candidate-1 の承認、企画設計一式の承認）／既知の課題（下記 findings）が文書内に揃う。人間へ提示する Checkpoint A 要約には本判定 findings の既知課題4点を転記すること（producer 責務。CD は転記確認のみ）。
  3. **正直さ — 合格**: key image の欠陥（人間キャラ4体混入→再クロップ対応済み）、P-02×P-01/P-03 の均衡が最大設計リスク＝Checkpoint B 検証事項、DR-GDD iteration3 申し送り(a)(b)(c)、AR-BIBLE の style_codes pending・ヘヴィ変種色相近接の再計測申し送り、が全て文書に明示され隠蔽なし。
- 前段ゲート状況: DR-CONCEPT iter3 APPROVE / DR-GDD iter3 APPROVE / AR-BIBLE iter2 APPROVE。MAX_ITER 到達の未解決 REJECT/CONCERNS 無し。
- findings（人間に提示すべき既知の課題。隠蔽ではなく設計上の申し送り）:
  1. key image candidate-1 は元画にスコープ外の「倒れた人間キャラクター」4体が混入。style_block/reference_images/character_reference は人間キャラを含まない再クロップ（crop-01/02/03）＋明示除外で汚染回避済み。Checkpoint A で人間がこの key image を承認するか、差し替え候補（candidate-4 次点。要再調整）を選ぶ判断が必要。
  2. 最大設計リスク: 自動攻撃（単体ヒット・DPS上限）が強すぎれば群れが一掃され P-01/P-03 の「囲まれる緊張」が崩れ、弱すぎれば P-02 の無双感が消える。この均衡は Checkpoint B のプロトタイプ実プレイで検証する（数値調整レンジは gdd 規定済み）。
  3. DR-GDD 申し送り: (a) SFX 発火点は攻撃ヒット/ウェーブ開始/被弾の3点のみ gdd 明記、ダッシュ/取得/購入は実装者自明配置 (b) ヒットフラッシュ/VFX 寿命は形容詞のみで数値未確定 (c) カメラ画角でスポーンリング直径27m全景を収められるかは幾何未検証→プロトタイプ実測。
  4. AR-BIBLE 申し送り: style_codes の Ideogram 実コードは初回生成バッチで捕捉・pin 予定（現状 pending）。ヘヴィ変種差し色 #6B1030 とクリスタル・マゼンタの色相差8.9°は近接だが輝度/形状/発生頻度で分離、AR-ASSET で再計測。
- 対応: —（CD 判定のため対応欄なし。findings は Checkpoint A 提示物の「既知の課題」欄へ producer が転記する）
