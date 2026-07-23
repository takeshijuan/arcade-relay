# CD-CHECKPOINT 履歴 — Checkpoint A（企画・設計承認 / Crystal Bastion）

## CD-CHECKPOINT iteration 1 — CONCERNS
- 日時: 2026-07-21T18:40:00+09:00
- 対象提示物: design/brief.md / design/concept.md / design/gdd.md / design/art-bible.md + design/art-bible.json / design/assets.md / key image `design/refs/key-image-candidate-1.png`（+ crop-01〜04）
- 前提ゲート状況: DR-CONCEPT it2 APPROVE / DR-GDD it2 APPROVE / AR-BIBLE it2 APPROVE（3成果物すべて合格・MAX_ITER 未達の未解決指摘なし）
- 判定観点（gates.md CD-CHECKPOINT）ごとの結論:
  1. ビジョン一貫性 = 合格。ピラー P-01〜P-04 は brief の Core Fantasy（築いた防衛網がウェーブを溶かす快感）と1対1で対応。concept 全システム・gdd 全12システム・assets 全エントリが P-xx を参照。スコープは brief 上限内（画像8点≤8 / SFX6=6 / BGM1=1 / MDL5≤5 / ANM0）で盛らない宣言に整合。逸脱・ピラードリフトなし。
  2. 提示品質 = 要改善（本 CONCERNS の主因）。企画設計一式は完成・可読だが、後述の carry-forward リスク（下記指摘要約 1〜4）が review 履歴と文書脚注に散在。review-mode=lean では MAX_ITER 未解決指摘のみ自動提示されるため、これらは自動では人間に surface されない。Checkpoint A 提示物の「既知の課題」欄へ明示転記が必要。
  3. 正直さ = 合格（隠蔽なし）。Phase 1 は設計のみで資産未生成のため実縮退（Humanoid→Generic / 実資産→プレースホルダ / Primary→Fallback / shippable:false 使用）は発生していない。carry-forward リスクは各文書・review 履歴に正直に記録済み。[BLOCKER] 該当なし。
- 指摘要約（優先度順・Checkpoint A「既知の課題」欄へ冒頭転記すること。いずれも revise 不要の forward-looking リスク）:
  1. [提示必須] key image 承認の前提開示: candidate-1 の敵集団クロップ（character_reference=crop-04）は3体とも四足姿勢で、gdd/art-bible が要求する Marauder=小型二足シルエットを示していない。AR-BIBLE it1 で検出され「crop からは色・素材・表面処理のみ継承／二足は MDL-03 コンセプト画プロンプトで明示指定」という文書対応のみで解決済み（画像は再生成していない）。→ Marauder（MDL-03）3D 生成時に四足へ引きずられ二足シルエットが出ないリスクが残る。人間が key image を承認する際、「Marauder は生成時に二足指定で作り直す前提」を明示提示する。
  2. [提示必須] P-04 の核「ラン間アップグレードが暗記配置の単純流用を防ぐ」は設計上の賭け（concept 仮説3 / DR-GDD 申し送り ii）。決定論8波+固定スポットで毎ラン再配分が実際に強制されるかは未検証で、プロトタイプ/QA-PLAY（Phase 2/3）で反証検証する前提。
  3. [提示必須] P-03「溶ける実感」の撃破演出はディゾルブシェーダ任意（無ければ即非表示に縮退）。particle+SFX で溶けるカタルシスが成立するかは実プレイ未検証（DR-GDD 申し送り i）。
  4. [提示必須] 3Dモデル5体は Meshy 直API（model_prop）生成予定で、直API クレジット→USD 換算は未検証見積（MANIFEST に cost_estimated:true 付与予定）。概算 $4.46（画像+モデル）で予算 $20 に残枠十分だが、コストは見積であることを開示。ルートはすべて shippable:true（縮退ルート不使用）。
- 判定意味: CONCERNS = 提示可。ただし上記 1〜4 を Checkpoint A 提示物「既知の課題」欄へ転記し冒頭で個別提示すること（箇条書きに埋没させない）。パイプラインは止めない。
- 対応:

## CD-CHECKPOINT (Checkpoint A) — 人間承認
- 日時: 2026-07-21T08:05:00Z（承認記録）
- 判断: 承認（key image = candidate-1 トイジオラマ調）
- 開示済み前提: Marauder は 3D 生成時に二足シルエットを明示指定 / P-04 暗記化リスクは Phase 2 で反証検証 / P-03 ディゾルブ任意 / コスト見積 $4.46（cost_estimated）
