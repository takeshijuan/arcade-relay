// InMemorySaveStore.cs — ファイル I/O を伴わないインメモリ実装（永続化層）。
// 用途: テストの注入ダブル専用（本番既定は FileSaveStore.cs / SaveStores.CreateDefault() — S-07）。
using ForgeGame.Systems.Meta;

namespace ForgeGame.Persistence
{
    /// <summary>プロセス内メモリにのみ保持する SaveStore。破損は発生しないため recovered は常に false。</summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        private SaveData current;

        public InMemorySaveStore(SaveData initial = null)
        {
            current = initial;
        }

        public LoadResult Load()
        {
            if (current == null) current = SaveData.CreateDefault();
            return new LoadResult(current.Clone(), false);
        }

        public void Save(SaveData data)
        {
            current = data.Clone();
        }
    }
}
