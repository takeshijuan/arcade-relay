// InMemoryAudioSettingsStore.cs — ファイル I/O を伴わないインメモリ実装（テスト注入専用・S-16）。
// InMemorySaveStore.cs と同じ用途（本番既定は FileAudioSettingsStore.cs / AudioSettingsStores.CreateDefault()）。
using System;
using ForgeGame;

namespace ForgeGame.Persistence
{
    /// <summary>プロセス内メモリにのみ保持する AudioSettingsStore。</summary>
    public sealed class InMemoryAudioSettingsStore : IAudioSettingsStore
    {
        private AudioSettingsData current;

        public InMemoryAudioSettingsStore(AudioSettingsData initial = null)
        {
            current = initial;
        }

        public AudioSettingsData Load()
        {
            if (current == null) current = AudioSettingsData.CreateDefault();
            return new AudioSettingsData { BgmVolume = current.BgmVolume, SfxVolume = current.SfxVolume };
        }

        public void Save(AudioSettingsData data)
        {
            // CR-CODE iter1 #5（minor）対応（iter2 #4 で番号をs-16.md記載順に修正: 旧#6→#5）:
            // FileAudioSettingsStore.Save は null を ArgumentNullException で明示拒否している。
            // テストダブルである本実装が data.BgmVolume 参照時の暗黙 NullReferenceException に頼ると、
            // 注入するストア実装によって呼び出し側が受け取る例外型が変わってしまう（契約の不一致）。
            // 同じ明示ガードを揃える。
            if (data == null) throw new ArgumentNullException(nameof(data));
            current = new AudioSettingsData { BgmVolume = data.BgmVolume, SfxVolume = data.SfxVolume };
        }
    }
}
