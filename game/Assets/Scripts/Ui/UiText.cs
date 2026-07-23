// UiText.cs — UI 層の共通テキスト定数/フォーマッタ（CR-CODE iter1 #3 対応・S-20）。
// Title/Menu が同一の「セーブ復旧」文言を、Menu/Result が同一の「ベストクリアタイム表示（未記録=-1→--）」
// 判定式をそれぞれ独立にリテラル/インライン式として二重定義しており、drift が機械検知されない指摘を受けた。
// 表示文字列/整形ロジックのみを持つ（状態を持たない・Systems 層のロジックではない）— UiCanvasHelper と
// 同じ「UI 層の薄い共通ヘルパ」の位置づけ。
namespace ForgeGame.Ui
{
    public static class UiText
    {
        /// <summary>
        /// セーブ破損からの復旧通知文言（contract §6 の recovered 伝播）。TitleScreen と MenuScreen の
        /// 両方が同一文言で表示する（Title/Menu の両方で「一度表示」— docs/architecture.md）。
        /// </summary>
        public const string SaveCorruptionRecoveredMessage = "セーブデータの破損を検知したため、初期状態から復旧しました。";

        /// <summary>
        /// 直近の Persistence.Save 失敗通知文言の共通接頭辞（GameFlow.SaveFailed。CR-CODE iter1 #1 対応）。
        /// Menu/Result で文脈語を変えて使うため、固定文言ではなく呼び出し側が文脈語を渡す形にする。
        /// </summary>
        public static string BuildSaveFailedMessage(string contextLabel) =>
            $"{contextLabel}の保存に失敗しました。反映されていない可能性があります。";

        /// <summary>
        /// ベストクリアタイム表示の共通整形（未記録は SaveData.bestClearTimeSec == -1 — MetaProgression の
        /// 既定値契約）。MenuScreen の HighScoreText 行・ResultScreen の BestClearTimeText 行が共有する。
        /// </summary>
        public static string FormatBestClearTime(float bestClearTimeSec) =>
            bestClearTimeSec < 0f ? "--" : $"{bestClearTimeSec:0.0}s";
    }
}
