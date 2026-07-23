// GameFlow.cs — シーン遷移の唯一の入口（contract §11 正準フロー: Boot→Title→Menu→Game→Result→{Game|Menu}）。
// GameConfig.Scenes の名前定数のみを使い、Components/Ui にシーン名文字列を直書きさせない（docs/architecture.md
// 「シーン間のデータ受け渡し…実装方式は S-01（GameFlow）で確定する」への回答）。
// Unity 依存（SceneManager）のため Systems/ ではなく Components/ に置く。ライフサイクルを持たない静的ルータ。
using UnityEngine.SceneManagement;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Components
{
    /// <summary>
    /// シーン遷移 API + プロセス内メモリでのセッション間キャリー（RunResult・recovered フラグ・SaveData）。
    /// 永続化はしない（SaveData の保存/復元は Persistence 層の責務）。ここは「今のプレイセッション」限りの
    /// 受け渡しにのみ使う。
    /// </summary>
    public static class GameFlow
    {
        /// <summary>Boot でのセーブロード時に破損復旧が発生したか。Title/Menu が読んで通知表示に使う。</summary>
        public static bool Recovered { get; private set; }

        /// <summary>
        /// 直近の Persistence.Save 呼び出しが例外で失敗したか（CR-CODE iter3 #1・S-06）。Recovered と同型の
        /// プロセス内フラグ。Save 呼び出し側（現状 RunOutcomeController の Result 遷移前保存）が例外を
        /// catch した際に SetSaveFailed(true) を呼ぶ。Result/Menu UI 側でのこのフラグの購読・通知表示は
        /// ui-engineer 担当領域のため未実装 — ★TODO(S-20): 「破損復旧トースト・未記録表示など UI 仕上げ」
        /// 実装時に Result/Menu UI が GameFlow.SaveFailed を購読して「保存に失敗した」旨を表示すること
        /// （黙って初期化/成功を装う禁止 — contract §6 の recovered パターンをエラー系にも踏襲する）。
        /// FileSaveStore 自体は S-07 で導入済み（本フラグの発生源）— このフラグは S-07 の責務ではなく
        /// S-20 側の UI 実装が未着手であるために孤児化していた（CR-CODE 指摘。state/stories.yaml の S-20
        /// acceptance が recovered トーストのみで SaveFailed 通知に触れていない点は S-20 担当
        /// [ui-engineer]/game-designer 側での acceptance 更新が必要 — gameplay-engineer の担当領域外の
        /// ため本ストーリーでは state/stories.yaml S-20 ブロックの編集は行わない）。
        /// </summary>
        public static bool SaveFailed { get; private set; }

        /// <summary>
        /// 直近ロード/保存された SaveData のプロセス内キャリー（S-03）。Menu 等のアウトゲーム表示が
        /// 実績/統計/essence/UPG Lv を読む唯一の経路。未設定（Boot を経由していない PlayMode 単体テスト等）
        /// は null のまま — 消費側は `GameFlow.CurrentSaveData ?? SaveData.CreateDefault()` で既定値に
        /// フォールバックすること（黙示初期化ではなく、単に「まだロードされていない」ことの表現）。
        /// 契約（CR-CODE iter1 #4）: このプロパティを更新する契機は Boot でのロード時に限らない。
        /// 正準フロー Result→Menu は Boot を通らないため、Persistence.Save を実施する側（Result 保存
        /// ストーリー）は保存直後に「保存した新しい SaveData」で必ず SetCurrentSaveData を呼ぶこと。
        /// これを怠ると Menu はラン前の古い essence/統計/実績を表示し続ける（TODO: Result 保存ストーリー
        /// 実装時にこの契約を満たす呼び出しを追加する — 未割当。docs/architecture.md 参照）。
        /// </summary>
        public static SaveData CurrentSaveData { get; private set; }

        /// <summary>
        /// SaveData の最新値を伝播するために呼ぶ（GameBootstrap のロード直後、および Persistence.Save
        /// 実施側の保存直後の両方が呼び出し契機 — 上記 CurrentSaveData のコメント参照）。
        /// </summary>
        public static void SetCurrentSaveData(SaveData data) => CurrentSaveData = data;

        /// <summary>
        /// 音量設定（BGM/SFX）のプロセス内キャリー（S-16）。CurrentSaveData と同型のパターン: Boot での
        /// ロード直後（GameBootstrap）、および Persistence.Save 実施側（MenuScreen の音量変更ハンドラ）の
        /// 保存直後の両方が呼び出し契機。未設定（Boot 非経由の単体テスト等）は null のまま — 消費側は
        /// `GameFlow.CurrentAudioSettings ?? AudioSettingsData.CreateDefault()` でフォールバックすること
        /// （黙示初期化ではなく、単に「まだロードされていない」ことの表現 — CurrentSaveData と同じ契約）。
        /// </summary>
        public static AudioSettingsData CurrentAudioSettings { get; private set; }

        /// <summary>CurrentAudioSettings の最新値を伝播するために呼ぶ（上記コメント参照）。</summary>
        public static void SetCurrentAudioSettings(AudioSettingsData data) => CurrentAudioSettings = data;

        /// <summary>Game→Result 間でキャリーする直近 RunResult。</summary>
        public static RunResult LastRunResult { get; private set; }

        /// <summary>LastRunResult が有効か（Result シーンへまだ遷移していない/リセット済みなら false）。</summary>
        public static bool HasRunResult { get; private set; }

        /// <summary>Boot が読んだセーブの recovered フラグを設定する（Title/Menu ストーリーが購読）。</summary>
        public static void SetRecovered(bool recovered) => Recovered = recovered;

        /// <summary>Persistence.Save 失敗フラグを設定する（呼び出し側が例外を catch した上で呼ぶ。上記 SaveFailed 参照）。</summary>
        public static void SetSaveFailed(bool saveFailed) => SaveFailed = saveFailed;

        public static void GoToBoot() => SceneManager.LoadScene(GameConfig.Scenes.Boot);

        public static void GoToTitle() => SceneManager.LoadScene(GameConfig.Scenes.Title);

        public static void GoToMenu() => SceneManager.LoadScene(GameConfig.Scenes.Menu);

        public static void GoToGame() => SceneManager.LoadScene(GameConfig.Scenes.Game);

        /// <summary>RunResult を確定させ Result シーンへ遷移する（勝敗成立→RunResult確定後に呼ぶ想定）。</summary>
        public static void GoToResult(RunResult result)
        {
            LastRunResult = result;
            HasRunResult = true;
            SceneManager.LoadScene(GameConfig.Scenes.Result);
        }

        /// <summary>Result シーンが直近 RunResult を取得する。取得後の破棄は呼び出し側に委ねる（ClearRunResult）。</summary>
        public static bool TryGetLastRunResult(out RunResult result)
        {
            result = LastRunResult;
            return HasRunResult;
        }

        /// <summary>もう一度プレイ（Result→Game）等、キャリー済み RunResult を消費し終えたら呼ぶ。</summary>
        public static void ClearRunResult()
        {
            LastRunResult = default;
            HasRunResult = false;
        }
    }
}
