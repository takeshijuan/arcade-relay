// IAudioSettingsStore.cs — 音量設定（BGM/SFX）永続化 I/O 層の契約（S-16）。
// contract §11 の「永続化 I/O は Persistence/ のみで行う」規約に従い ISaveStore と同型の抽象パターンを
// 踏襲する。対象はメタ進行 SaveData（save_version 管理・contract §6 の破損時3点セット厳格プロトコル対象）
// ではなく UI 設定（音量）のため、別ファイル・別インターフェースに分離する。
// このストアは「メタ進行セーブデータ」ではないため contract §6 の .bak 退避＋[SaveCorruption]ログ＋
// recovered伝播の3点セットは適用義務が無いが、黙示初期化はしない: 読込失敗時は必ず1回 Warning を出す
// （FileAudioSettingsStore.cs 参照）。
using ForgeGame;

namespace ForgeGame.Persistence
{
    /// <summary>音量設定の読込/書込の抽象。テストではインメモリ実装を注入できる。</summary>
    public interface IAudioSettingsStore
    {
        /// <summary>設定を読み込む。存在しない/読込失敗時は既定値（BgmVolume=SfxVolume=1.0）を返す。</summary>
        AudioSettingsData Load();

        /// <summary>設定を永続化する。</summary>
        void Save(AudioSettingsData data);
    }
}
