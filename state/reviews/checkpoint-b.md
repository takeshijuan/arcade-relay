# CD-CHECKPOINT 履歴 — Checkpoint B（プロトタイプ縦串提示 / Crystal Bastion）

## CD-CHECKPOINT iteration 1 — CONCERNS
- 日時: 2026-07-22T13:40:00Z
- 対象提示物: design/brief.md / design/concept.md（P-01〜P-04）/ design/gdd.md / state/stories.yaml / qa/report.md（QA-PLAY iteration2 APPROVE）/ game/_generated/MANIFEST.jsonl / state/reviews/ 配下履歴 / qa/evidence/qa-visual-{title,menu,game,result}.png
- 前提ゲート状況: QA-PLAY it2 APPROVE（build/EditMode48・PlayMode50×2 全 exit0・全pass、独立再実行で安定）。CR-CODE 全 story APPROVE。AR-ASSET（画像/音声/モデル）APPROVE。旧 [BLOCKER] PlayMode 9件失敗は b3ad6e8 で解消済み・QA-PLAY で独立再検証済み（active な [BLOCKER] なし）。
- 判定観点（gates.md CD-CHECKPOINT）ごとの結論:
  1. ビジョン一貫性 = 合格。縦串はコアループ（設置→自動攻撃→経済→勝敗→Result→即リトライ）と必須5シーン遷移（Title→Menu→Game→Result→Menu）を機械検証で成立。P-01（一手必中の配置）・P-02（二種の役割分担）・P-04（負けても伸びる防衛網＝永続化/破損復旧/実績表示）は PlayMode で裏付け済み。P-03（溶ける実感）の撃破VFX/演出は S-13/S-19（build phase）へ意図的に繰り延べ。ピラードリフト・スコープ逸脱なし（brief 資産上限内・盛らない宣言遵守）。
  2. 提示品質 = 要改善（本 CONCERNS の主因）。QA報告・active.md・Integrate報告に妥協点が正直に記録されているが lean モードでは自動 surface されないため散在する。人間が縦串を正しく評価（＝「presentation が意図的に薄い prototype」と認識した上で Checkpoint B feedback を出す）できるよう、下記 known-issues を提示物冒頭へ個別転記する必要がある。
  3. 正直さ = 合格（隠蔽なし）。縮退・プレースホルダ・cost_estimated・非多様体頂点等はすべて MANIFEST/review 履歴/QA報告に開示済み。全資産ルートは primary（shippable:true）で fallback/shippable:false 不使用。active な [BLOCKER] なし。
- 指摘要約（優先度順・Checkpoint B「既知の課題」欄へ冒頭転記すること。いずれも revise 不要の縮退/繰り延べの開示）:
  1. [縮退/プレースホルダ] MDL-04 Warbeast 未生成 → EnemyView は単色 Capsule プレースホルダ（WAVE3 初出＝build phase S-12 のため縦串の WAVE1 では未出現）。IMG-01/02（地形タイル）・IMG-05/06/07（実績/アップグレード/敵インジケータのアイコン）未生成でプレースホルダ/未使用。BGM-01 未生成で BGM 再生なし。SFX-05（ウェーブ開始）は境界イベント未実装で未配線（S-13 で解消見込み・資産自体は取込/検証済み）。
  2. [ピラー未検証] P-03「溶ける実感」（本作のコアファンタジー中枢）の撃破VFX・ディゾルブ・派手な演出は S-13/S-19（build phase）未実装。縦串では敵が溶けるカタルシスを人間はまだ体感できない。Checkpoint B ではメカ成立の確認に留まり、P-03 の実プレイ検証は build phase 持ち越し。
  3. [ピラー未検証] P-04 の核「ラン間アップグレードが暗記配置の単純流用を防ぐ」は設計上の賭け（concept 仮説3）。UPG 効果反映は S-14（build phase）未実装のため、初期条件がラン毎に変わる体感はまだ検証不能。土台（永続化・実績確定・敗北時 essence 加算）は成立。
  4. [開示] 3Dモデル4体（MDL-01/02/03/05）・画像3点・SFX6点は cost_estimated:true（Meshy 直API クレジット→USD 換算および ElevenLabs クレジット→USD 換算が保守見積）。累計 約$2.80/$20 で残枠十分。MDL-01/02 は color_correction.applied:true（決定論HSVリマップの後処理を実施＝純AI一発生成ではない）。非多様体頂点は全4体で非ゼロだが gltf-validate エラー0・単一メッシュ島で実害なし。MDL-05 パレット距離50〜99 は emissive設計/AO付きレンダー由来の測定限界（資産欠陥ではない・非ブロッカー）。
  5. [環境制約] 実機ビルド（game/Build/ForgeGame.app）でのマウス実操作クリアは本サンドボックス（画面収録権限なし）で未実施。QA は PlayMode InputTestFixture 擬似発行で判定。人間が Checkpoint B で実機プレイして体感を確認することが本チェックポイントの主目的。
- 判定意味: CONCERNS = 提示可。上記 1〜5 を Checkpoint B 提示物「既知の課題」欄へ転記し冒頭で個別提示すること（箇条書きに埋没させない）。パイプラインは止めない。build phase（S-10〜S-20）で P-03/P-04 効果反映・全資産統合・難易度曲線完成を実装し再検証する。
- 対応:
