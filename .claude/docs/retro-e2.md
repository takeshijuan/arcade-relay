# E2 評価ラン ふりかえり（2026-07-10〜15 / unity / Crystal Vanguard）— ハーネス改善バックログ

> E2（unity 実走）で観測した問題と、人間フィードバック（2026-07-15）を受けた改善検討。
> ここは**提案の置き場**であり規約ではない。採用時は contract.md を先に更新し参照側を追随させる（絶対規約1）。

## 結果サマリ

- 完走: brief → Checkpoint A → プロトタイプ（S-01〜11）→ Checkpoint B → 本実装+ブラッシュアップ（S-12〜33）→ Checkpoint C 受領
- QA: 33/33 acceptance・EditMode 181/181・PlayMode 142/142・重大バグ0・実コスト $2.95/$100
- E1 で機能しなかったゲート群（視覚証跡目視・縮退エスカレーション・開示チャネル・Setup 機械検証）は全て実働を確認

## 人間フィードバック起点の検討課題（優先）

### 1. Build Phase の並列化（所要時間が長すぎる）

観測: Phase 2 Build ≈ 6h（11 stories 直列）、Phase 3 ≈ 9h+9h（22 stories 直列）。1 story あたり実装+CR-CODE 2周+検証で 30〜60 分。直列化の理由は「同一コードベースのコンフリクト回避」だが、実測ではボトルネックは (a) story の直列実行 (b) story ごとの Unity 検証（EditMode+Build で毎回 3〜8 分）。

検討案（コスト小→大）:
- **A. assignee レーン並列（推奨・最小変更）**: gameplay-engineer と ui-engineer の story を2レーン並走。所有パスがほぼ排他（Systems+Components vs Ui+Scenes）で、共有は GameConfig.cs/Types.cs のみ（既に「自ストーリーに必要な定数の追記のみ」規約あり）。E2 でも AssetGen 並走との git 競合はリトライで実害なしだった。workflow 変更: buildStories を assignee で分割し parallel() 2レーン・レーン内直列。期待短縮 30〜40%。
- **B. 検証のバッチ化**: story ごとの EditMode+Build を「実装2〜3件ごと」または「レーン合流点」にまとめる（コンパイル検証は EditMode 1回で全体を兼ねるため冗長度が高い）。失敗時の切り分けが粗くなるトレードオフは、失敗時のみ二分探索で個別再検証する規約で緩和。期待短縮 20〜30%。
- **C. 依存グラフ並列**: tech-director が stories.yaml に `depends_on: [S-xx]` を宣言し、独立 story を最大N並列。git worktree 分離は Unity では Library 複製コスト（数GB・初回インポート数分）と単一インスタンスロックの制約が重く、**worktree 分離は非推奨**。同一ツリー並列は A の一般化として実装可能だが競合レビュー（同一ファイル編集検出）を Setup で機械化する必要あり。
- 注意: Unity 起動を伴う工程（検証・取込・QA）は現行どおり**必ず直列**（単一インスタンスロック — tech-stack-unity.md）。並列化はコード編集と review agent に限る。
- **実装済み（2026-07-21）**: 案A+B を prototype.js / full-build.js に実装した。assignee 2レーン並走（レーン内直列・LANE_RULE で担当領域/共有ファイル/stories.yaml のピンポイント Edit を強制）+ レーン中はエンジン検証禁止（EP.laneVerifyLine — phaser は typecheck のみ可）+ レーン合流後の batchVerify（直列・失敗は story コミット単位で切り分け、記録は state/reviews/batch-verify.md）。正本規約は各 tech-stack 文書の検証節に追記済み。案C（依存グラフ並列）は未実装のまま（次回検討）。

### 2. AA 品質ギャップ — Unity 機能別スキル/専門知識の分割（エフェクト・UI・UX）

観測: ブラッシュアップ後も見た目は「プロトタイプ+装飾」域。原因は engineer agent が汎用 C# 実装者であり、Unity の高品質表現機能（Timeline / Animator ステートマシン設計 / VFX Graph / Shader Graph / Cinemachine / UI Toolkit アニメーション / DOTween 級のイージング設計）の**専門知識をプロンプトに持っていない**こと。E2 では Bloom+ParticleSystem+コードtweenの素朴な組合せに留まり、Menu 装飾では可読性劣化（プレイ開始文字の埋没）も発生した。

検討案:
- **A. 機能別ナレッジ文書 + 参照注入（最小変更・contract 変更不要）**: `.claude/docs/unity-craft/` に機能別ガイド（timeline.md / animator.md / vfx.md / ui-motion.md / cinemachine.md / shader.md）を置き、tech-stack-unity.md から参照。ui-engineer / gameplay-engineer の「参照ドキュメント」に該当ガイドを追加し、polish story の acceptance に「該当ガイドの技法を最低1つ適用」を要求。
- **B. スキル化（ユーザー案）**: `/unity-vfx` `/unity-ui-motion` 等のスキルを新設し、workflow の Polish フェーズから agent(prompt, {agentType}) でなく Skill 起動で依頼。**contract §3 がスキル6個固定のため contract 改訂が必須**。スキルは人間も単独起動できる利点（「この画面だけ磨いて」）がある。
- **C. 専門 agent 追加（vfx-artist / ux-designer）**: contract §2 の10体固定の改訂が必要。レビュー系（AR-ASSET/QA-PLAY）との責務境界の再設計コストが最大。
- 推奨順: A を即時（次ラン前）、B は A の効果測定後に contract 改訂込みで、C は保留。
- 併せて gates.md QA-PLAY に「UI 文字の可読性の機械検査」（装飾適用後のテキスト領域コントラスト計測）を追加すべき — E2 で装飾が可読性を壊した実例が出たため（Menu「プレイ開始」）。

## E2 で観測したその他の問題（ハーネス改善候補）

3. **長時間 run の中断耐性**: API セッション上限で Phase2×2・Phase3×1 回中断。resume はプロンプト連鎖（commitHash 埋め込み等）によりキャッシュ分岐し、大量再実行が発生した。対策案: (a) workflow プロンプトから可変値（commit hash・累積 findings）を外し「state ファイルを読め」に置換してキャッシュ命中率を上げる (b) フェーズ単位のチェックポイント返却（Setup 後・Build 後に一旦 return し、スキルが次フェーズを別 run として起動する分割実行モード）。
4. **署名断（1Password）への運用**: 署名エージェント断でコミット不能でも実装は継続できたが、(a) CR-CODE の「commit hash 固定レビュー」が機能せず process-blocker が多発 (b) 未コミット250件が worktree 全損リスクに。対策案: 検証コマンド節に「署名不能時は `git stash create` で無署名スナップショット SHA を作り、レビュー対象をそれに固定する」を追加（履歴を汚さず対象固定可能・バイパスにならない）。
5. **bookkeep と agent 規約の衝突**: CR-CODE MAX_ITER 到達時の「done+注記」更新指示を implementer が Must-NOT-Do を根拠に拒否（S-01/S-08 が review 固着）。review-loops.md のエスカレーション仕様と agent 定義の整合を取る（どちらかに「MAX_ITER 到達エスカレーション時は done+注記が正」と明記）。
6. **AR-ASSET の iteration 番号管理**: workflow が「iteration 1」とハードコードした依頼を出し、既存履歴（iteration 4 まで）と矛盾 → reviewer が自力補正した。workflow は state/reviews/<artifact>.md の既存 iteration 数を数えてから依頼すべき（または「次番号は履歴から自分で採番せよ」に統一）。
7. **Workflow args の文字列化**: ランナーが args を JSON 文字列で渡すケースを実測 → 3スクリプトに正規化を実装済み（恒久化済み・完了）。
8. **資産ファイル命名の系統逸脱**: design/assets.md がテンプレ例の `img-` 系で起票され、rules/assets.md の `sprite-/ui-/...` プレフィクスと不一致のまま全画像に波及。templates/assets.md のファイル名ガイドに正プレフィクスの例を明記して起票時点で防ぐ。
9. **Ideogram 表記条項の MANIFEST 記録**: ライセンスフラグとしては提示されたが MANIFEST 行の license_note に未記録のまま完走。assets-config.md の Provenance 節に「プロバイダ固有の表記条項は license_note に転記必須」を追加。
10. **must-replace の解消経路**: MDL-02（quadruped リグ）は Meshy 422/Tripo 403 で詰み、承認済み代替で完走した。ルーティング表に「quadruped リグは Tripo が第一想定（Meshy は humanoid のみ実績）」の注記と、Tripo クレジット残高の preflight 表示を追加すると意思決定が早い。

## 完了済み（E2 中にハーネスへ反映した恒久修正）

- workflows の args 正規化 / Setup の Title・Menu・メタ進行 story 機械検証+実体突合 / AR-ASSET disclosures チャネル / 証跡実在の独立検証と verdict 降格 / [BLOCKER] 前置 / 生成レーンの .env source 規約 / パス限定 git add の徹底
