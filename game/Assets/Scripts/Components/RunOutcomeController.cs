// RunOutcomeController.cs — Game シーンの勝敗判定・RunResult確定・永続化・Result遷移オーケストレータ（S-06）。
// gdd「勝敗条件」節: 勝利=全 WAVE_COUNT 消化かつ coreHp>0（残存敵0）/ 敗北=coreHp<=0。
// 判定成立の瞬間にスコア算出(ScoreSystem.ComputeFinalScore)→メタ進行確定(MetaProgression.ApplyRunResult)→
// 永続化(Persistence.Save)を1回だけ実行し（docs/architecture.md §3 データフロー）、GameConfig.Presentation.
// WinResultDelaySec の演出待機後に Result シーンへ遷移する（gdd「勝敗条件」節の1秒演出待機）。
// 待機も Update() の Time.deltaTime で駆動する状態機械にし（規約2: delta-time 必須。coroutine の
// WaitForSeconds は実時間駆動でテストの StepForTest 決定論から外れるため使わない）、finalized ガードで
// 二重保存・二重遷移を防ぐ（連打・複数フレームでの再判定に対して1回性を保証）。
// Components は薄く: 判定条件（AllWavesSpawned/ActiveEnemyCount/IsDefeated）は既存 Systems/Components が
// 公開する状態を読むだけで、新たな判定ロジックを Systems/ に足す必要はない（勝敗の合成条件のみここで組む）。
using System;
using UnityEngine;
using ForgeGame.Persistence;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Components
{
    /// <summary>Game シーンに1つだけ置く。他コントローラへの参照は SerializeField で配線する。</summary>
    public sealed class RunOutcomeController : MonoBehaviour
    {
        [SerializeField] private WaveSpawnController waveSpawnController;
        [SerializeField] private CoreView coreView;
        [SerializeField] private BuildSpotController buildSpotController;

        private ISaveStore saveStore;
        private bool started;

        private float elapsedSec;
        private bool finalized;
        private bool transitioned;
        private float postFinalizeTimer;
        private RunResult finalizedResult;

        /// <summary>テスト用の読み取り専用状態公開。</summary>
        public bool IsFinalized => finalized;
        public bool HasTransitioned => transitioned;

        /// <summary>テスト用の Controller 注入群。Awake/Start 実行前（非アクティブ状態）に呼ぶこと（規約9）。</summary>
        public void SetWaveSpawnControllerForTest(WaveSpawnController controller) => waveSpawnController = controller;
        public void SetCoreViewForTest(CoreView view) => coreView = view;
        public void SetBuildSpotControllerForTest(BuildSpotController controller) => buildSpotController = controller;

        /// <summary>テスト用の ISaveStore 注入。Start() 実行後の注入は無警告で無効化されるため即時失敗させる
        /// （GameBootstrap.SetSaveStoreForTest と同じ黙示無効化禁止パターン）。</summary>
        public void SetSaveStoreForTest(ISaveStore store)
        {
            if (started)
            {
                throw new InvalidOperationException(
                    "[RunOutcomeController] SetSaveStoreForTest called after Start(); injection would be silently ignored. " +
                    "Inject before activating the GameObject (inactive → inject → SetActive(true)).");
            }
            saveStore = store ?? throw new ArgumentNullException(
                nameof(store),
                "[RunOutcomeController] SetSaveStoreForTest(null) would silently fall back to the default " +
                "FileSaveStore (SaveStores.CreateDefault()) in Start() via '??='; pass a real stub or do not call this method.");
        }

        private void Start()
        {
            started = true;
            // 本番既定は SaveStores.CreateDefault()（=FileSaveStore・S-07）。GameBootstrap.cs と同一の
            // ファクトリを参照するため、差し替え漏れの経路は構造的に無い（CR-CODE iter3 #2 の推奨反映）。
            saveStore ??= SaveStores.CreateDefault();

            if (waveSpawnController == null)
            {
                // 規約12: 配線破損は Start で1回 LogError。未注入では勝敗判定ができない。
                Debug.LogError("[RunOutcomeController] WaveSpawnController is not wired; win/loss cannot be determined.");
            }
            if (coreView == null)
            {
                Debug.LogError("[RunOutcomeController] CoreView is not wired; CoreHpRemaining cannot be read.");
            }
            if (buildSpotController == null)
            {
                Debug.LogError("[RunOutcomeController] BuildSpotController is not wired; kill/AoE-kill/spot stats cannot be recorded.");
            }

            if (waveSpawnController != null)
            {
                waveSpawnController.OnCoreDefeated += HandleCoreDefeated;
            }
        }

        private void OnDestroy()
        {
            if (waveSpawnController != null)
            {
                waveSpawnController.OnCoreDefeated -= HandleCoreDefeated;
            }
        }

        private void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        /// <summary>
        /// PlayMode テスト用の直接駆動口。Update() と同じ内部処理（StepSimulation）を呼ぶだけで挙動差異は無い
        /// （規約9 のテスト用シーム。演出待機分も含めて deltaTime を任意刻みで進められる）。
        /// </summary>
        public void StepForTest(float deltaTime) => StepSimulation(deltaTime);

        private void StepSimulation(float deltaTime)
        {
            if (transitioned) return;

            if (finalized)
            {
                postFinalizeTimer += deltaTime;
                if (postFinalizeTimer >= GameConfig.Presentation.WinResultDelaySec)
                {
                    transitioned = true;
                    GameFlow.GoToResult(finalizedResult);
                }
                return;
            }

            elapsedSec += deltaTime;

            if (waveSpawnController == null || coreView == null) return;

            if (coreView.IsDefeated)
            {
                FinalizeRun(isWin: false);
                return;
            }

            if (waveSpawnController.WaveSystem.AllWavesSpawned && waveSpawnController.WaveSystem.ActiveEnemyCount == 0)
            {
                FinalizeRun(isWin: true);
            }
        }

        /// <summary>OnCoreDefeated（WaveSpawnController が敗北成立で1回だけ発火）の購読先。</summary>
        private void HandleCoreDefeated() => FinalizeRun(isWin: false);

        /// <summary>
        /// 勝敗成立の瞬間に1回だけ呼ばれる（finalized ガードで冪等）。RunResult を確定し、
        /// ScoreSystem.ComputeFinalScore → MetaProgression.ApplyRunResult → Persistence.Save の順に
        /// 一度だけ実行する（docs/architecture.md §3。Result への実際のシーン遷移は演出待機後 StepSimulation が行う）。
        /// </summary>
        private void FinalizeRun(bool isWin)
        {
            if (finalized) return;
            finalized = true;
            postFinalizeTimer = 0f;

            int killCount = buildSpotController != null ? buildSpotController.EnemyHealth.KillCount : 0;
            int aoeKillCount = buildSpotController != null ? buildSpotController.EnemyHealth.AoeKillCount : 0;
            int usedBuildSpots = buildSpotController != null ? buildSpotController.BuildSpots.Towers.Count : 0;
            int coreHpRemaining = coreView != null ? coreView.CurrentHp : 0;

            var result = new RunResult
            {
                IsWin = isWin,
                CoreHpRemaining = coreHpRemaining,
                KillCount = killCount,
                AoeKillCount = aoeKillCount,
                UsedBuildSpots = usedBuildSpots,
                ClearTimeSec = elapsedSec,
            };
            finalizedResult = result;

            // gdd「勝敗条件」節: 勝敗いずれの場合もスコア算出とメタ進行の確定を Result 遷移前に一度だけ実行する。
            int finalScore = ScoreSystem.ComputeFinalScore(result);
            SaveData prev = GameFlow.CurrentSaveData ?? SaveData.CreateDefault();
            SaveData next = MetaProgression.ApplyRunResult(prev, result);
            // プロセス内の真実（CurrentSaveData）を Save より先に確定する（CR-CODE iter1 #2）。
            // 訂正（CR-CODE iter3 #1）: finalized/finalizedResult は本メソッド冒頭（150行目）で
            // 既に確定済みのため、ここで Save が例外を投げても StepSimulation の finalized 分岐
            // （113-119行目）は次フレーム以降 postFinalizeTimer を消化して通常どおり Result へ
            // 遷移する（Unity は unhandled exception をログするだけで Update を止めない）。
            // 「例外時に Result 遷移まで到達しない」は誤りで、実際は保存に失敗しても
            // プレイヤーには成功と区別のつかない Result 画面が表示されうる。これを避けるため
            // Save のみ try/catch し、失敗を1回ログした上で GameFlow.SaveFailed に伝播する
            // （GameFlow.Recovered と同型のプロセス内フラグ。Result/Menu UI 側の表示配線は
            // ui-engineer 担当領域のため本ストーリーでは行わない — ★TODO(S-20): 「破損復旧トースト・
            // 未記録表示など UI 仕上げ」実装時に Result/Menu UI が GameFlow.SaveFailed を購読して
            // 「保存に失敗した」旨の通知表示を行うこと。FileSaveStore 自体は S-07 で導入済みのため
            // このフラグ自体は本コミット時点で稼働している — 未実装なのは S-20 側の購読/表示のみ）。
            GameFlow.SetCurrentSaveData(next);
            try
            {
                saveStore.Save(next);
                // CR-CODE iter1 #1: GameFlow.SaveFailed は「直近の Save 呼び出しが失敗したか」を表す
                // 契約（GameFlow.cs コメント）のため、成功時は必ずリセットする。static フィールドは
                // シーンリロードをまたいで生存するため、リセットを怠ると直前ランの失敗フラグが
                // 以降の成功ランにも残存し続ける（false negative の恒久化）。
                GameFlow.SetSaveFailed(false);
            }
            catch (Exception ex)
            {
                // 回復不能条件を Warning へ降格しない（rules/unity-code.md）。例外を握り潰さず、
                // 明示エラーログ1回＋プロセス内フラグ伝播のみ行う（Result 遷移自体は止めない —
                // finalized はこの時点で既に確定済みで、遷移を止めても保存は復旧しないため）。
                Debug.LogError($"[SaveFailure] RunOutcomeController failed to persist SaveData after run finalize (isWin={isWin}): {ex}");
                GameFlow.SetSaveFailed(true);
            }

            // CR-CODE iter2 #1: プレゼンテーション呼び出し（SFX 再生）はスコア算出・メタ進行確定・
            // 永続化の完了後に移動した。以前は永続化ブロックより前段にあり、AudioCuePlayer.PlayOneShot
            // が例外を投げると finalized=true 済みのまま Save 一式がスキップされ、保存失敗が Result
            // 画面上で成功と区別つかなくなる同種の failure mode を再導入していた（183行目のコメントが
            // 警告する失敗モード）。ここに置けば SFX 側の例外があっても保存/メタ進行確定は既に完了済み。
            // design/assets.md SFX-06「勝利判定成立イベント」トリガー、SFX-04「敗北判定成立イベント」
            // トリガー（予算制約によりコア被弾音を敗北時のコア崩壊音として流用する設計）。
            // 演出キューの選択は FeedbackCueSystem に委譲する（S-13）。再生位置は CoreView があればその位置、
            // 無ければ原点。
            Vector3 outcomeSfxPosition = coreView != null ? coreView.transform.position : Vector3.zero;
            FeedbackCue outcomeCue = FeedbackCueSystem.SelectRunOutcomeCue(isWin);
            AudioCuePlayer.PlayOneShot(outcomeCue.AssetKey, outcomeSfxPosition, pitch: outcomeCue.PitchMultiplier);

            Debug.Log($"[RunOutcomeController] run finalized isWin={isWin} finalScore={finalScore} killCount={killCount}");
        }
    }
}
