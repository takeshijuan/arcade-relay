// GameBootstrap.cs — Boot シーンの入口（正準フロー Boot→Title の起点）。
// セーブをロードし（本番既定は SaveStores.CreateDefault()=FileSaveStore — S-07）、recovered フラグを
// GameFlow 経由で Title/Menu へ伝播した上で GameFlow.GoToTitle() で遷移する（シーン名文字列の直書きは
// しない — GameFlow がシーンルータの唯一の入口）。
using System;
using UnityEngine;
using ForgeGame.Persistence;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Components
{
    /// <summary>Boot シーンに1つだけ置く。ライフサイクルと遷移配線のみ（ロジックは持たない）。</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        // 本番既定は SaveStores.CreateDefault()（=FileSaveStore・S-07）。RunOutcomeController.cs と
        // 同一ファクトリを参照するため、差し替え漏れの経路は構造的に無い（CR-CODE iter3 #2 の推奨反映）。
        private ISaveStore saveStore;
        // S-16: 音量設定（BGM/SFX）の起動時ロード用ストア。saveStore と同じ「Start() 実行前限定」注入契約。
        private IAudioSettingsStore audioSettingsStore;
        private bool started;

        /// <summary>テスト/将来ストーリーからの ISaveStore 注入。Start() 実行前（非アクティブ状態）に呼ぶこと
        /// （規約9: Awake/Start 配線罠 — 非アクティブ生成→注入→アクティブ化の手順に従う）。
        /// Start() 実行後の注入は無警告で無効化されると、既定ストアの結果が使われたままテストが
        /// false-pass しうるため、started 後は即時失敗させる（黙示無効化の禁止）。
        /// store が null の場合も同様に即時失敗させる — null を通すと Start() の
        /// `saveStore ??= SaveStores.CreateDefault()` が無警告で既定ストア（FileSaveStore・実ファイル I/O）
        /// へフォールバックし、スタブ注入を意図したテストが既定ストアに対して走って
        /// false-pass しうるため（黙示無効化の禁止、CR-CODE iter2 指摘1対応）。</summary>
        public void SetSaveStoreForTest(ISaveStore store)
        {
            if (started)
            {
                throw new InvalidOperationException(
                    "[GameBootstrap] SetSaveStoreForTest called after Start(); injection would be silently ignored. " +
                    "Inject before activating the GameObject (inactive → inject → SetActive(true)).");
            }
            saveStore = store ?? throw new ArgumentNullException(
                nameof(store),
                "[GameBootstrap] SetSaveStoreForTest(null) would silently fall back to the default " +
                "FileSaveStore (SaveStores.CreateDefault()) in Start() via '??='; pass a real stub or do not call this method.");
        }

        /// <summary>
        /// S-16: 音量設定 IAudioSettingsStore のテスト注入。SetSaveStoreForTest と同一契約
        /// （Start() 実行前限定・null 拒否 — 黙示無効化の禁止）。
        /// </summary>
        public void SetAudioSettingsStoreForTest(IAudioSettingsStore store)
        {
            if (started)
            {
                throw new InvalidOperationException(
                    "[GameBootstrap] SetAudioSettingsStoreForTest called after Start(); injection would be silently ignored. " +
                    "Inject before activating the GameObject (inactive → inject → SetActive(true)).");
            }
            audioSettingsStore = store ?? throw new ArgumentNullException(
                nameof(store),
                "[GameBootstrap] SetAudioSettingsStoreForTest(null) would silently fall back to the default " +
                "FileAudioSettingsStore (AudioSettingsStores.CreateDefault()) in Start() via '??='; pass a real stub or do not call this method.");
        }

        private void Start()
        {
            started = true;
            saveStore ??= SaveStores.CreateDefault();
            LoadResult result = saveStore.Load();
            GameFlow.SetRecovered(result.Recovered);
            GameFlow.SetCurrentSaveData(result.Data); // Menu 等のアウトゲーム表示が読む（S-03）

            // S-16: 音量設定（BGM/SFX）の起動時ロード。読込のみ（初回起動=ファイル無しは既定値へ無音フォールバック
            // — IAudioSettingsStore.cs 冒頭コメントの通り軽量ストアのため LogAssert を汚す副作用は発生しない）。
            audioSettingsStore ??= AudioSettingsStores.CreateDefault();
            GameFlow.SetCurrentAudioSettings(audioSettingsStore.Load());
            if (result.Recovered)
            {
                // 破損復旧の通知表示（トースト等）は Title/Menu の UI ストーリーが GameFlow.Recovered を購読して行う。
                Debug.Log("[Bootstrap] save recovered from corruption; defaults restored.");
            }
            GameFlow.GoToTitle();
        }
    }
}
