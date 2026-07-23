// AudioCuePlayer.cs — 演出キュー（FeedbackCueSystem の選択結果）をワンショット再生するヘルパー
// （Components 層・Unity 依存）。design/assets.md の各 SFX 行の「トリガー」節に記載された既存イベント
// 発火点（設置成功・ダメージ適用・撃破・コア被弾・ウェーブ開始・勝敗確定）から、直接または
// Systems/FeedbackCueSystem（どの演出を鳴らすかの選択。S-13）経由で呼ばれる。
// GameConfig.AssetKeys 経由のキーのみを受け取り、Resources.Load('文字列直書き')はしない（規約5）。
// 未生成資産（Resources.Load が null）は再生をスキップし1回 Warning（黙殺しない）。
// AudioListener 不在時（Boot/Title/Menu/Game/Result の実シーンは ForgeScaffold の Main Camera に必ず
// 付与済みだが、PlayMode テストの一部は Camera 無しの単体 GameObject を手組みするフィクスチャのため
// Listener が存在しない）は再生を試みずスキップする。呼んでしまうと Unity が毎フレーム
// "There are no audio listeners in the scene" を出し続け、テストの LogAssert（規約 —
// console エラー0検査）を無関係に落とす（実シーンでは常にリスナーが存在するため本番挙動は変わらない）。
using UnityEngine;

namespace ForgeGame.Components
{
    internal static class AudioCuePlayer
    {
        // PlayClipAtPoint と同じ「発生源から全方位へ広がらず距離減衰する3D空間音源」として振る舞わせるための
        // AudioSource.spatialBlend 指定（0=2D, 1=3D）。チューニング対象ではなく固定モード値だが、
        // rules/unity-code.md の直書き禁止方針に合わせて名前付き定数化する（S-13 CR-CODE iter1 #2）。
        private const float SpatialBlend3D = 1f;

        /// <summary>
        /// resourceKey の AudioClip を worldPosition で1回再生する。pitch を省略（既定1f=変調なし）した
        /// 場合は Unity 標準の使い捨てヘルパー（AudioSource.PlayClipAtPoint）へそのまま委譲する。
        /// pitch != 1f（S-13: FeedbackCueSystem がタワー種別ごとに発射音のピッチを変える場合等）は
        /// PlayClipAtPoint にピッチ指定 API が無いため、使い捨て AudioSource を自前生成し再生完了後に
        /// 自壊させる（PlayClipAtPoint の内部実装と同型のワンショット再生パターン）。
        /// pitch が GameConfig.Presentation.MinAudiblePitchForDestroyTimer 未満（0 以下の不正入力、および
        /// 0 &lt; pitch &lt; 下限のほぼ聴取不能域の両方）の場合は無音同然の再生・破棄タイマーの異常延伸を
        /// 招くため再生せず Warning 1回で表面化させる（S-13 CR-CODE iter1 #1 / iter2 #2。ガード閾値を
        /// 破棄タイマーのクランプ下限と一致させることで、クランプ自体は理論上到達不能なバックストップに
        /// なる — 黙殺しない方針。ファイル冒頭コメント）。
        /// </summary>
        internal static void PlayOneShot(string resourceKey, Vector3 worldPosition, float volume = 1f, float pitch = 1f)
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null) return;

            if (pitch < GameConfig.Presentation.MinAudiblePitchForDestroyTimer)
            {
                Debug.LogWarning($"[AudioCuePlayer] Pitch ({pitch}) below audible threshold requested for key \"{resourceKey}\" — cue skipped.");
                return;
            }

            AudioClip clip = Resources.Load<AudioClip>(resourceKey);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioCuePlayer] AudioClip not found for key \"{resourceKey}\" (not yet generated/imported) — cue skipped.");
                return;
            }

            if (Mathf.Approximately(pitch, 1f))
            {
                AudioSource.PlayClipAtPoint(clip, worldPosition, volume);
                return;
            }

            var oneShotGo = new GameObject($"OneShotAudio_{resourceKey}");
            oneShotGo.transform.position = worldPosition;
            var source = oneShotGo.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = SpatialBlend3D;
            source.Play();
            Object.Destroy(oneShotGo, clip.length / Mathf.Max(GameConfig.Presentation.MinAudiblePitchForDestroyTimer, pitch));
        }
    }
}
