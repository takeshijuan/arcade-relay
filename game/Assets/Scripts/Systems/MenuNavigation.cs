// MenuNavigation — pure index math for the Menu tab bar / item focus (gdd「Menu 画面構成」・
// 「操作仕様」: Q/E はタブを循環＝ラップ、W/S は項目フォーカスを先頭/末尾で止める＝ラップしない).
// Engine-independent: no UnityEngine scene API, no MonoBehaviour (rules/unity-code.md).
namespace ForgeGame.Systems
{
    public static class MenuNavigation
    {
        /// <summary>
        /// Cycle the active tab index by one step. Wraps around at both ends (gdd: Q/E のみで循環切替).
        /// direction is typically +1 (TabNext/E) or -1 (TabPrev/Q).
        /// </summary>
        public static int CycleTab(int currentTab, int tabCount, int direction)
        {
            if (tabCount <= 0)
            {
                return 0;
            }
            int next = (currentTab + direction) % tabCount;
            if (next < 0)
            {
                next += tabCount;
            }
            return next;
        }

        /// <summary>
        /// Move the focused item index by one step within the current tab's item list. Clamps at
        /// the first/last item — does NOT wrap (gdd: フォーカスは項目リストの先頭/末尾で止まる).
        /// itemCount == 0 (e.g. the display-only 統計 tab) always returns 0 (no focusable item).
        /// </summary>
        public static int MoveFocus(int currentFocus, int itemCount, int direction)
        {
            if (itemCount <= 0)
            {
                return 0;
            }
            int next = currentFocus + direction;
            if (next < 0)
            {
                next = 0;
            }
            if (next > itemCount - 1)
            {
                next = itemCount - 1;
            }
            return next;
        }
    }
}
