// SaveStores.cs — 本番用 ISaveStore の唯一の生成点（S-07・CR-CODE iter3 #2 の推奨反映）。
// GameBootstrap / RunOutcomeController など複数箇所が個別に `new FileSaveStore()` を書くと
// 差し替え漏れの経路が生まれる（S-01/S-06 の scaffold で実際に指摘された）ため、
// ここに一元化し両呼び出し箇所はこのファクトリだけを参照する。
namespace ForgeGame.Persistence
{
    /// <summary>本番既定の ISaveStore を返す静的ファクトリ。テストは各自 InMemorySaveStore 等を直接注入する。</summary>
    public static class SaveStores
    {
        /// <summary>persistentDataPath/save.json を使う本番既定ストア。</summary>
        public static ISaveStore CreateDefault() => new FileSaveStore();
    }
}
