// GameInput — single input aggregation point (rule 4). Actions are BUILT IN CODE (no
// .inputactions asset editing). Components/Ui read this facade; they never call Input.GetKey.
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForgeGame.InputLayer
{
    /// <summary>
    /// Owns the InputActionMaps for gameplay and menu/UI. Enable() before use, Disable() on teardown.
    /// Gameplay: Move (WASD/arrows, Vector2), Dash (Space). Menu/Title/Result: Navigate, Adjust,
    /// Submit (Enter/Space), Cancel (Esc), TabPrev/TabNext (Q/E).
    /// </summary>
    public sealed class GameInput
    {
        public readonly InputAction Move;
        public readonly InputAction Dash;
        public readonly InputAction Navigate;
        public readonly InputAction Adjust;
        public readonly InputAction Submit;
        public readonly InputAction Cancel;
        public readonly InputAction TabPrev;
        public readonly InputAction TabNext;

        private readonly InputActionMap _gameplay = new InputActionMap("Gameplay");
        private readonly InputActionMap _ui = new InputActionMap("UI");

        public GameInput()
        {
            Move = _gameplay.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            Dash = _gameplay.AddAction("Dash", InputActionType.Button, "<Keyboard>/space");

            Navigate = _ui.AddAction("Navigate", InputActionType.Value);
            Navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            Adjust = _ui.AddAction("Adjust", InputActionType.Value);
            Adjust.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");

            Submit = _ui.AddAction("Submit", InputActionType.Button);
            Submit.AddBinding("<Keyboard>/enter");
            Submit.AddBinding("<Keyboard>/space");

            Cancel = _ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            TabPrev = _ui.AddAction("TabPrev", InputActionType.Button, "<Keyboard>/q");
            TabNext = _ui.AddAction("TabNext", InputActionType.Button, "<Keyboard>/e");
        }

        public void EnableGameplay() { _gameplay.Enable(); }
        public void DisableGameplay() { _gameplay.Disable(); }
        // UI マップ側の個別 Disable は Dispose() が一括で行う（Ship-review AUTO-FIX: 呼び出し箇所ゼロの
        // DisableUi() を削除。UI 画面は破棄時に Dispose() を呼ぶ運用で統一されている）。
        public void EnableUi() { _ui.Enable(); }

        public void Dispose()
        {
            _gameplay.Disable();
            _ui.Disable();
            _gameplay.Dispose();
            _ui.Dispose();
        }
    }
}
