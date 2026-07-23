// InputReader.cs — 全入力の集約点（tech-stack-unity.md 規約4: Input System・アクションはコード生成）。
// マウスのみ操作（gdd 操作仕様）: 左クリック=設置/選択/購入、右クリック=選択解除、Esc=一時停止トグル。
// Components/Ui はこの Reader を購読し、生の Input API・Mouse/Keyboard を直接読まない。
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ForgeGame.Input
{
    /// <summary>
    /// マウス左/右クリックと Esc を Input System のコード生成アクションで抽象化する。
    /// ポインタ座標はスクリーン座標で公開し、ワールド変換は Components 層（カメラ保持側）が行う。
    /// </summary>
    public sealed class InputReader
    {
        private readonly InputAction click;   // 左クリック（設置/選択/購入）
        private readonly InputAction cancel;  // 右クリック（選択解除）
        private readonly InputAction pause;   // Esc（Game 中のみ一時停止トグル）
        private readonly InputAction point;   // ポインタ座標（スクリーン）
        private readonly InputAction anyKey;  // 任意キー（Title の「クリック/任意キーで開始」用 — S-02）

        public InputReader()
        {
            var map = new InputActionMap("Gameplay");
            click = map.AddAction("Click", InputActionType.Button, "<Mouse>/leftButton");
            cancel = map.AddAction("Cancel", InputActionType.Button, "<Mouse>/rightButton");
            pause = map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            point = map.AddAction("Point", InputActionType.Value, "<Mouse>/position");
            anyKey = map.AddAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            Map = map;
        }

        /// <summary>Components 層が Enable/Disable のライフサイクルを制御する。</summary>
        public InputActionMap Map { get; }

        public void Enable() => Map.Enable();
        public void Disable() => Map.Disable();

        public bool ClickPressedThisFrame => click.WasPressedThisFrame();
        public bool CancelPressedThisFrame => cancel.WasPressedThisFrame();
        public bool PausePressedThisFrame => pause.WasPressedThisFrame();
        public bool AnyKeyPressedThisFrame => anyKey.WasPressedThisFrame();

        // S-16 CR-CODE iter2 #1（major）対応: 押下が継続しているフレームも検知する（クリック/ドラッグで
        // スライダーを操作するための連続入力）。WasPressedThisFrame は押下開始フレームのみ true になる
        // エッジトリガのため、これとは別に「今このフレームで押されているか」の状態も公開する。
        public bool ClickHeld => click.IsPressed();

        /// <summary>現在のポインタスクリーン座標（x, y）。</summary>
        public UnityEngine.Vector2 PointerScreenPosition =>
            point.activeControl is Vector2Control ? point.ReadValue<UnityEngine.Vector2>() : UnityEngine.Vector2.zero;
    }
}
