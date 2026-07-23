// FeedbackCueSystem.cs — 演出フィードバックのキュー選択（純粋 C#・エンジン非依存。S-13）。
// gdd「演出フィードバック」節（P-03）: 入力はタワー発射/敵撃破/コア被弾/ウェーブ開始/勝敗確定の各イベント、
// 出力は「どの SFX をどう鳴らすか」の選択のみ（演出キュー）。実際の再生（AudioSource生成・PlayClipAtPoint等）
// は Components/AudioCuePlayer が担う（規約3: Systems は判定・選択のみで GameObject/AudioSource/シーンAPI を
// 使わない — rules/unity-code.md）。
// SFX-02（タワー発射音）は Bastion Cannon / Arc Emitter の共通音（design/assets.md 6点予算制約）のため、
// タワー種別ごとにピッチを僅かに変えて役割差（P-02: 単体高火力 vs 範囲低火力）を演出する
// （design/assets.md SFX-02 行の「実装側でピッチ・音量を僅かに変えて役割差を演出する余地を残す」を実装）。
using System;

namespace ForgeGame.Systems
{
    /// <summary>
    /// 選択された演出キュー（再生すべき資産キー + ピッチ倍率）。実際の再生は Components/AudioCuePlayer が担う。
    /// pitchMultiplier 省略時は 1f（変調なし）。
    /// </summary>
    public readonly struct FeedbackCue
    {
        public readonly string AssetKey;
        public readonly float PitchMultiplier;

        public FeedbackCue(string assetKey, float pitchMultiplier = 1f)
        {
            if (string.IsNullOrEmpty(assetKey))
                throw new ArgumentException("assetKey は必須（GameConfig.AssetKeys 経由の値を渡すこと）。", nameof(assetKey));
            if (pitchMultiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(pitchMultiplier), pitchMultiplier, "pitchMultiplier は正の値である必要がある。");

            AssetKey = assetKey;
            PitchMultiplier = pitchMultiplier;
        }
    }

    /// <summary>
    /// gdd「演出フィードバック」節の5イベント（発射/撃破/コア被弾/ウェーブ開始/勝敗確定）それぞれに対応する
    /// 演出キューを返す純粋関数群。呼び出し側（Components 層）が返り値の AssetKey/PitchMultiplier を使って
    /// 実際に再生する。MonoBehaviour/シーン API は使わない（rules/unity-code.md 規約3）。
    /// </summary>
    public static class FeedbackCueSystem
    {
        /// <summary>
        /// タワー発射（TowerCombatSystem のダメージ適用イベント発生 = design/gdd.md「タワー自動攻撃」節の
        /// 発射エフェクトトリガー）。SFX-02 共通音をタワー種別ごとのピッチで役割差別化する。
        /// </summary>
        public static FeedbackCue SelectTowerFiredCue(TowerType sourceTowerType)
        {
            float pitch = sourceTowerType == TowerType.BastionCannon
                ? GameConfig.Presentation.BastionCannonFirePitch
                : GameConfig.Presentation.ArcEmitterFirePitch;
            return new FeedbackCue(GameConfig.AssetKeys.SfxTowerFire, pitch);
        }

        /// <summary>
        /// 敵撃破（EnemyHealthSystem の撃破確定＝HP&lt;=0 判定）。final-hit の帰属先タワー種別に関わらず
        /// 同一 SFX を使う（design/assets.md SFX-03 は Marauder/Warbeast・タワー種別共通指定のため）。
        /// </summary>
        public static FeedbackCue SelectEnemyDefeatedCue() => new FeedbackCue(GameConfig.AssetKeys.SfxEnemyDefeat);

        /// <summary>コア被弾（CoreDefenseSystem のダメージ適用イベント。敵のゴール到達時）。</summary>
        public static FeedbackCue SelectCoreHitCue() => new FeedbackCue(GameConfig.AssetKeys.SfxCoreHit);

        /// <summary>
        /// ウェーブ開始告知（WaveSpawnSystem のウェーブ予告表示イベント。各ウェーブの準備フェーズ開始時に1回）。
        /// </summary>
        public static FeedbackCue SelectWaveStartCue() => new FeedbackCue(GameConfig.AssetKeys.SfxWaveStart);

        /// <summary>
        /// 勝敗確定イベント。勝利=SFX-06（勝利ジングル）、敗北=SFX-04（コア被弾音をコア崩壊音として流用—
        /// design/assets.md SFX-04「予算制約により敗北成立時のコア崩壊SFXも本ファイルを流用する」を反映）。
        /// </summary>
        public static FeedbackCue SelectRunOutcomeCue(bool isWin) =>
            isWin
                ? new FeedbackCue(GameConfig.AssetKeys.SfxVictoryJingle)
                : new FeedbackCue(GameConfig.AssetKeys.SfxCoreHit);
    }
}
