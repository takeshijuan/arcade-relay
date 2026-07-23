// ISaveStore.cs — 永続化 I/O 層の契約（contract §11 / tech-stack-unity.md セーブ節）。
// この層だけが persistentDataPath / File / PlayerPrefs を触ってよい。
// Systems/Meta（純粋 reducer）はこの契約を知らず、値の変換だけを担う。
using ForgeGame.Systems.Meta;

namespace ForgeGame.Persistence
{
    /// <summary>セーブの読込/書込の抽象。テストではインメモリ実装を注入できる。</summary>
    public interface ISaveStore
    {
        /// <summary>
        /// セーブを読み込む。存在しなければ既定値。破損時は .bak 退避＋[SaveCorruption] ログ＋
        /// 既定値再生成の上で recovered=true を返す（黙って初期化しない — contract §6）。
        /// </summary>
        LoadResult Load();

        /// <summary>セーブを永続化する（実装はアトミック書込 — tech-stack-unity.md）。</summary>
        void Save(SaveData data);
    }
}
