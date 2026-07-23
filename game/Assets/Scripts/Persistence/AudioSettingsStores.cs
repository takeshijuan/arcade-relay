// AudioSettingsStores.cs — 本番用 IAudioSettingsStore の唯一の生成点（SaveStores.cs と同型・S-16）。
// GameBootstrap / MenuScreen が個別に `new FileAudioSettingsStore()` を書くと差し替え漏れの経路が
// 生まれるため、ここに一元化し両呼び出し箇所はこのファクトリだけを参照する。
namespace ForgeGame.Persistence
{
    /// <summary>本番既定の IAudioSettingsStore を返す静的ファクトリ。テストは各自 InMemoryAudioSettingsStore を注入する。</summary>
    public static class AudioSettingsStores
    {
        /// <summary>persistentDataPath/audio-settings.json を使う本番既定ストア。</summary>
        public static IAudioSettingsStore CreateDefault() => new FileAudioSettingsStore();
    }
}
