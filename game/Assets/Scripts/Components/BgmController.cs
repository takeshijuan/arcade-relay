// BgmController.cs — Game シーン滞在中の BGM-01 ループ再生（Components 層・Unity 依存・S-19）。
// design/assets.md BGM-01「防衛シーン全編で流れ続けるBGM」/ docs/architecture.md §3「演出」節。
// GameConfig.AssetKeys.BgmMainTheme を Resources.Load してループ再生するだけの薄い配線
// （規約3: Components は薄く。判定ロジックは持たない）。音量初期値は GameFlow.CurrentAudioSettings
// （S-16。未ロード時は AudioSettingsData.CreateDefault() へフォールバック — Ui/MenuScreen.cs と同型の契約）。
// 資産未生成/未取込（Resources.Load が null）は再生をスキップし1回 Warning（黙殺しない —
// Components/AudioCuePlayer と同方針。回復不能ではなく「未生成のまま進行できる」既知の縮退のため Warning に留める）。
// 実プレイ中の音量スライダー操作（Ui/MenuScreen・Ui/PausePanel）は設定パネル内蔵の別 AudioSource
// （プレビュー/確認用途）を持つため、本コントローラの AudioSource へは反映されない。既知の制約であり、
// Game シーンで鳴っている BGM 本体の音量をリアルタイム変更する経路の統合は本ストーリーのスコープ外
// （設定パネルは Menu/Pause から到達するため Game シーンの BGM に生値で影響を与える設計は別途要検討）。
// [KNOWN LIMITATION — 要 Checkpoint 開示（CR-CODE S-19 iter1 minor #1）]: PausePanel の音量スライダーは
// GameFlow.CurrentAudioSettings とプレビュー用 AudioSource は更新するが、この Game シーン BGM 本体の
// AudioSource には反映されないため、プレイ中に BGM をミュート/減音しても実際の BGM 音量は次回 Game
// シーンロードまで変化しない（ユーザー観点で「操作が効かない」ように見える既知ギャップ）。呼び出し元
// workflow は Checkpoint 提示時にこれを未解決事項として明示すること（本ファイル単体では解決できない —
// 修正には Game シーン内で Start() 後も設定変更を購読する経路の追加設計が必要で、本ストーリーのスコープ外）。
using UnityEngine;

namespace ForgeGame.Components
{
    /// <summary>Game シーンに1つだけ置く。</summary>
    public sealed class BgmController : MonoBehaviour
    {
        private AudioSource audioSource;

        /// <summary>テスト観測用（S-19 PlayMode テスト）。</summary>
        public AudioSource AudioSourceForTest => audioSource;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
        }

        private void Start()
        {
            AudioClip clip = Resources.Load<AudioClip>(GameConfig.AssetKeys.BgmMainTheme);
            if (clip == null)
            {
                Debug.LogWarning($"[BgmController] AudioClip not found for key \"{GameConfig.AssetKeys.BgmMainTheme}\" (not yet generated/imported) — BGM loop skipped.");
                return;
            }

            audioSource.clip = clip;
            audioSource.volume = (GameFlow.CurrentAudioSettings ?? AudioSettingsData.CreateDefault()).BgmVolume;
            audioSource.Play();
        }
    }
}
