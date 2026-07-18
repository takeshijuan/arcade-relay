// MenuNavigationTests — S-03: pure index math for Menu tab cycling (wrap) and item focus
// (clamp, no wrap). Conventions.md §9: new pure Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;

namespace ForgeGame.Tests.EditMode
{
    public sealed class MenuNavigationTests
    {
        [Test]
        public void CycleTab_WrapsForwardPastLastTab()
        {
            // gdd: Q/E のみで循環切替 — 末尾タブから次へ進むと先頭タブに戻る
            Assert.AreEqual(0, MenuNavigation.CycleTab(currentTab: 3, tabCount: 4, direction: +1));
        }

        [Test]
        public void CycleTab_WrapsBackwardPastFirstTab()
        {
            Assert.AreEqual(3, MenuNavigation.CycleTab(currentTab: 0, tabCount: 4, direction: -1));
        }

        [Test]
        public void CycleTab_StepsForwardWithinRange()
        {
            Assert.AreEqual(1, MenuNavigation.CycleTab(currentTab: 0, tabCount: 4, direction: +1));
            Assert.AreEqual(2, MenuNavigation.CycleTab(currentTab: 1, tabCount: 4, direction: +1));
        }

        [Test]
        public void MoveFocus_ClampsAtLastItem_DoesNotWrap()
        {
            // gdd: フォーカスは項目リストの先頭/末尾で止まる（ラップしない）
            Assert.AreEqual(2, MenuNavigation.MoveFocus(currentFocus: 2, itemCount: 3, direction: +1));
        }

        [Test]
        public void MoveFocus_ClampsAtFirstItem_DoesNotWrap()
        {
            Assert.AreEqual(0, MenuNavigation.MoveFocus(currentFocus: 0, itemCount: 3, direction: -1));
        }

        [Test]
        public void MoveFocus_StepsWithinRange()
        {
            Assert.AreEqual(1, MenuNavigation.MoveFocus(currentFocus: 0, itemCount: 3, direction: +1));
            Assert.AreEqual(1, MenuNavigation.MoveFocus(currentFocus: 2, itemCount: 3, direction: -1));
        }

        [Test]
        public void MoveFocus_ZeroItemCount_AlwaysReturnsZero()
        {
            // 統計タブ（表示専用・フォーカス項目なし）
            Assert.AreEqual(0, MenuNavigation.MoveFocus(currentFocus: 0, itemCount: 0, direction: +1));
            Assert.AreEqual(0, MenuNavigation.MoveFocus(currentFocus: 0, itemCount: 0, direction: -1));
        }

        [Test]
        public void MoveFocus_SingleItem_StaysAtZero()
        {
            Assert.AreEqual(0, MenuNavigation.MoveFocus(currentFocus: 0, itemCount: 1, direction: +1));
            Assert.AreEqual(0, MenuNavigation.MoveFocus(currentFocus: 0, itemCount: 1, direction: -1));
        }
    }
}
