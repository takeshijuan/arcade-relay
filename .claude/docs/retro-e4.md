# E4 ラン監査（はるかぜ農園・phaser・Phase 1〜3 完走・受領）— レトロスペクティブ

> 2026-09-08。E4 = v0.5.0.0 モデル階層ハーネス（メインセッション＝オーケストレータ / judge=opus・producer=sonnet・mechanical=haiku）の初実走。
> Phase 1 → Checkpoint A（修正指示 1 回: ちびキャラ・Retro Diffusion ルート・64px 維持）→ Phase 2 → Checkpoint B（無修正）→ Phase 3 ×3 → Checkpoint C（修正依頼 2 回・3 回目で受領）。
> 一次情報: run ブランチの state/active.md・tokenUsage・qa/report.md・MANIFEST（run 成果物は別リポジトリへ退避。本体には harness のみ）。
> 処方は v0.5.1.0（本 retro と同 PR）で反映済み — 各指摘の末尾に対応コミットの要旨を記す。

## 実測サマリ

| 区間 | 実測 | 備考 |
|---|---|---|
| Phase 1（企画設計・3 ゲート） | 2h33m・出力 647k tokens | E3: 75 分。DR-CONCEPT/DR-GDD/AR-BIBLE の revise 回数が多かった（ちびキャラ方針の修正指示を含む） |
| Phase 2（プロトタイプ） | 3h18m・1,870k tokens | E3: Phase 2 全体 ≈ 6.5h（Setup 24 分 + 実装 3.0h + 尾部 3h+）→ **約半分** |
| Phase 3 ×3（本実装・Checkpoint C 修正 2 回） | 5h00m + 3h54m + 4h38m・3,283k + 1,583k + 1,741k tokens | E3 は Phase 3 未実施。単一 run の比較基準は無い |
| 合計 | workflow agent **499 本** + オーケストレータ Task ≈35 本 / 出力 **9,124,549 tokens** / 約 19h | E3: agent 156 本・5.37M tokens（Phase 2 まで） |
| 資産コスト | **$5.18** / 予算 $100（MANIFEST 118 行・全 commercial-ok・must_replace 0） | E3: $2.80（Phase 2 まで）/ E2: $6.44 |
| 品質 | 全 68 story done・vitest 524・Checkpoint C 受領 | 受領時の開示: Pillow 縮退資産・柵の隙間・ElevenLabs Studio Games 条項 |

**結論: v0.5.0.0 の設計（オーケストレータ分離・階層割当・段階エスカレーション・tokenUsage・検証コマンドのオーケストレータ Bash 実行）は機能した。** 外部 reviewer=sonnet も CR-CODE として有効（S-02 で CRITICAL 検出）。一方で、mechanical 階層の「自己申告の突合」と「対象コミットの固定」に設計どおり動かない経路が 2 つ見つかり、いずれもオーケストレータの手作業で回収した。それを機械化するのが本 retro の主題。

## 機能した点（継続）

- **段階エスカレーション**: CR-CODE 最終 fix / QA fix / batch-verify 再試行が judge 階層で走り、producer 階層で残った指摘を解消した。
- **オーケストレータ Bash 検証**: typecheck/build/vitest の exit code を自分で観測し、QA 証跡の `test -s` で実在を確認できた（下記 1 の擬陽性をこれで回収）。
- **外部 reviewer=sonnet**: `pr-review-toolkit:code-reviewer` + `silent-failure-hunter` を sonnet に落としても検出力は維持（S-02 CRITICAL・S-64 minor 等）。design doc「見送り・未検証」から検証済みへ。
- **落とし穴の即時昇格**（retro-e3 処方）: QA fix / batch-verify が `tech-stack.md`「既知の落とし穴（engine=phaser）」に 5 件を追記した。ただし run ブランチにだけ残った（下記 7）。

## 辛口指摘（優先度順）と処方

### 1.【重大・擬陽性】verify-evidence の rawLine 突合が 78/78 件不合格 → QA-PLAY APPROVE を CONCERNS に誤降格
v0.5.0.0 で入れた「`ls -l <path>` の生出力行にフルパスが含まれること」の突合を、haiku の検証 agent は basename で `ls -l` を実行して満たせず、FullQA の証跡 78 件が全件「rawLine に当該パスの実行出力なし」→ APPROVE が CONCERNS に降格。実在はオーケストレータの `test -s` で全件確認済みだった。**不一致は不存在の証明ではない**のに、workflow は不存在と同じ扱いをしていた。
**処方**: `verifyEvidence()` — コマンドを `stat "%N %z"`（行頭にパス）の 1 コマンドに固定して指示し、不一致は是正指示付きで 1 回だけ再検証（`-recheck`）。それでも不一致なら降格せず `[VERIFY-UNCERTAIN]` で unresolvedFindings に載せ、オーケストレータの `test -s` 結果で置換する（スキル Phase 2）。降格は不存在・0 バイト・checks 欠落・evidencePaths 空・目視未実施のみ。

### 2.【重大・再発】CR-CODE の対象コミット指定ミス（S-48 / S-50 / S-62）
実装 agent が `state/reviews/*.md` や `stories.yaml` だけのコミット hash を commitHash として返し、reviewer は `git show <hash>` の diff（実装ゼロ）に APPROVE / CONCERNS を付けた。E3 でも同族（S-30 の「実装本体が一度もレビュー対象コミットに入っていない」）が起きており、プロンプト強制（`git show --stat` で自分の編集ファイルを確認せよ）では止まらなかった。
**処方**: (a) 返却に `changedFiles` を必須化し workflow がコード対象パス（contract §11）の包含を確認、(b) reviewer に「対象確認が先」を義務化し不一致なら `targetMismatch:true` を返させる、(c) workflow は 1 回だけ `locate-commit`（担当 engineer・`git log -- <codePathspec>`）で実装コミットを再特定し `-relocated` label で同 iteration をやり直す、(d) 特定不能は `[BLOCKER]`（レビュー未成立）で自動 APPROVE も fix も走らせない。

### 3.【重大・無駄な 2 周目】QA-PLAY 非 APPROVE で修正対象の配列が空 → fix が一度も走らず round 2 に到達
qa-lead が中程度バグを qa/report.md にだけ書き `criticalBugs` / `failedAcceptance` を空で返したため、QA fix が 1 件も起動せず HEAD 同一のまま round 2 → 同じ CONCERNS。review 2 回上限が無意味に消費された。
**処方**: 非 APPROVE かつ配列が全て空なら qa-lead.md 違反として記録し、summary と qa/report.md を起点に judge 階層の fix を 1 回試行。prototype の qaSchema に `bugs`（major/minor）を追加し major は assignee 単位でバッチ修正。qa-lead.md に「非 APPROVE の理由を構造化返却から落とさない」。

### 4.【中・規約違反を強制していた】design 系 agent の Bash 非保持で `date -u` 不能
creative-director / design-reviewer / game-designer は Bash を持たず、contract §7「時刻は `date -u` の実行出力を貼る」を満たせない（推測記入するしかない）。E3 で 5 時間ズレを出して作った規約が、規約側の欠陥で守れなかった。
**処方**: 3 体に Bash を付与し、用途を `date -u` と読み取り専用 git に限定（各 agent.md「Bash の使用範囲」）。代替案は不成立 — Workflow script 内の `new Date()` は実行系が throw、`args` 経由の起動時刻注入は 10 時間超の Phase 3 で実時系列とズレる。

### 5.【中・手作業回収】CD-CHECKPOINT の API 529 が agentR の 1 回即時リトライで回復せず
Phase 3 #2 で `cd-checkpoint-1` と `fix-s-50-2` が API 500/529 で null → オーケストレータが独立 CR-CODE と creative-director 再判定を Task で起こして尾部を再構成した（手順は記録されず属人的）。
**処方**: agentR に `retries`（既定 1・CD/finalize は 2）。Workflow 実行系は timer を持たず script 内バックオフは不可なので、待機を伴う回復は forge スキル Phase 2 に手順化（既定文検知 → `sleep 120` → creative-director Task 1 回 → 戻り値置換・提示に明記）。

### 6.【中・文書乖離】assets-config.md の Retro Diffusion 記載が現行 API と違う
`RD_FAST` / `RD_PLUS` / `RD_ANIMATION` は現行 API に無く HTTP 422（恒久）。`RD_FLUX` の出力上限は 384px（512 指定は失敗）。`return_spritesheet:true` は単一画像しか返さず、歩行シートは個別フレーム → Pillow 連結に縮退。日本語ロゴ文字は描画不能（4 回再現）。art-director は run 中にこれを実測で学んだが、文書は誤ったまま次ランへ渡る状態だった。
**処方**: ルーティング表の行を実測に合わせて書換（E4 実測 2026-09 と明記）。スプライトシートの正規ルート確立は TODOS へ（PixelLab / 個別フレーム + 連結の手順化）。

### 7.【低・知識の取りこぼし】昇格させた落とし穴が run ブランチにだけ残った
retro-e3 処方どおり QA fix が `tech-stack.md` に 5 件追記したが、run 成果物と一緒に sample repo へ退避され、本体 main には入っていなかった。run 完了時に harness 側の差分を本体へ持ち帰る手順が無い。
**処方**: 本 PR で 5 件を main へ移植。forge-build の受け渡し手順に「stage=done 時に `git diff main -- .claude/` を確認し、tech-stack / rules の追記を harness PR として切り出す」を追記。

### 8.【低・観測不能】検証コマンド例の `${PIPESTATUS[0]}` が zsh で空
スキルの例は bash 構文。macOS 既定の zsh では `EXIT=` が空文字になり、exit code を観測したつもりで観測していない。E4 で `${pipestatus[1]}` に読み替えて回避。
**処方**: forge-build / forge-prototype の例に zsh 形を併記し、「`EXIT=` が空なら観測失敗として扱い判定を出さない」を明記。

## PR 前レビュー 4 面（code-reviewer / silent-failure-hunter / adversarial+OSS 監査 / Codex）で見つかった穴と追加処方

初版の処方に対する指摘（blocker 1・critical 2・high 4・major 3・P2 4、ほか minor）は 3 つに収束した。いずれも同 PR で反映済み。

- **fail-open の再導入**（処方 1 の recheck）: 再検証結果が初回分類を丸ごと置換し、初回で確定した不存在が消えていた（batch-verify で一度直した fail-open の再来）。→ 再検証は不一致分のみを対象にし、初回の missing は保持（resolvedPrior と同じ fail closed）。加えて rawLine は `<path> <bytes>` として解析し、`<path> MISSING` / サイズ 0 は申告 exists/nonEmpty と矛盾すれば降格（パス文字列を含むだけの行を合格にしない）。
- **決定的根拠を log にしか使っていない**（処方 2）: 申告 changedFiles にコード対象パスが無い事実を workflow が持ちながら判定に使わず、reviewer の任意フィールド `targetMismatch` に全依存していた。commitHash 空でもガードが外れていた。→ changedFiles / observedCodeFiles を必須化し、コード対象パスが無い・hash 無しは reviewer 起動前に再特定、findings 0 件でも対象未証明なら APPROVE にしない、再特定 hash は hex 検証、agent 失敗と found:false を区別。
- **workflow 自身の降格を qa-lead 違反と誤記録**（処方 3）: 証跡/目視で降格した APPROVE が summary fix（opus）を空振りさせ、E5 の観測指標（違反 0）を汚す。→ 自己申告 verdict を保持し、降格のみならコード修正を起こさず次 round の qa-lead に降格理由を渡す。minor のみの非 APPROVE は「不整合・理由不明」として別文言で記録し summary fix を試行。

そのほか: 無記録経路の解消（bookkeep / finalize-state / QA round 2 の agent 失敗・REJECT 空指示・engineer 外 assignee・locate の失敗理由）、スキル文面の「置換」→「追記」（劣化記録の消去を許可していた）、`[VERIFY-UNCERTAIN]` は行が名指しするパスを `test -s`、full-build 戻り値に evidencePaths、gen-* の冪等ガード、テストの空振り（-retry2 検証）修正、contract §11 ↔ ENGINE_PROFILES の同期テスト、`.gitignore`（`.gstack/`・`game/.tmp/`）。

既知の限界（未対応・記録のみ）: unity の `Assets/Tests/**`・シーン・`.asmdef` のみ、unreal の `*.Build.cs`・`Config/*.ini` のみのコミットは contract §11 のコード対象パスに当たらず、単独 story なら「対象未証明」→ 再特定 → `[BLOCKER]` になる（実装と同じコミットに含める規約で回避。必要なら contract §11 の対象パスを広げる）。検証 agent が `<path> <bytes>` を捏造する最安の攻撃は workflow 内では検出できない — 信頼境界はオーケストレータの `test -s`（設計どおり）。ラン中のハーネス upgrade + resume は impl 以降のプレフィクスを外し課金再実行になり得るため避ける（gen-* の冪等ガードは緩和）。

## E5 で観測すること

- `verify-evidence-*-recheck` の是正率（recheck で rawLine が揃う割合。低ければコマンド指示をさらに機械的に）。
- `targetMismatch` の発生率と `locate-commit` の成功率（0 件なら (a) の changedFiles 必須化だけで足りている）。
- `qa-fix-*-summary` / `fix-qa-r*-summary` の発動回数（発動 = qa-lead.md 違反の再発。0 が目標）。
- `cd-checkpoint-*-retry2` の発動と、スキル側 CD 回復手順の起動回数。
- phase 別 tokenUsage を E4（上表）と比較。E4 は Checkpoint C 修正 2 回分を含むため Phase 3 は #1（5h00m・3,283k）を基準にする。

## 継続・見送り

- 外部 reviewer=sonnet: 継続（検証済み）。
- prototype QA fix のバッチ化: major は v0.5.1.0 で assignee 単位バッチ、criticalBugs は bug 単位（M-8a/b の resume 安全設計）を据え置き。
- contract.md の常駐部分分割（§6/§10/§11）: 未着手（TODOS）。
- ピクセルアート・アニメーションシートの正規生成ルート: 未着手（TODOS 新規）。
