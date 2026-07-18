---
paths: ["game/Assets/**/*.cs"]
---

# unity-code — game/Assets の C# 編集時の強制規約（engine=unity）

正本: `.claude/docs/tech-stack-unity.md`。違反は CR-CODE ゲートで CONCERNS 以上になる。
思想は phaser 版 `gameplay-code.md` と同一（マジックナンバー禁止 / delta-time / エンジン非依存コア / 入力抽象化 / 資産キー集約）を Unity イディオムに翻訳したもの。

## Do / Don't

- **Do**: ゲームパラメータ（速度・重力・HP・スコア・時間・色・サイズ）は `Assets/Scripts/GameConfig.cs` の静的定数クラスに集約する。チューニングは GameConfig.cs の編集だけで完結すること
- **Don't**: MonoBehaviour や System の本文に数値リテラルを直書きしない（配列 index・`0`/`1` 初期値など意味を持たない値は除く）
- **Do**: 移動・タイマー・クールダウンは `Update()` 内で `Time.deltaTime`、物理は `FixedUpdate()` 内で `Time.fixedDeltaTime` でスケールする
- **Don't**: フレーム毎の固定加算（`x += 5f`）を書かない。60fps 前提の実装は禁止
- **Don't**: `Assets/Scripts/Systems/` で `MonoBehaviour` を継承しない・シーン API（`GameObject.Find` / `Instantiate` / `GetComponent`）を呼ばない。Systems はエンジン非依存層（純粋 C# クラス。`Vector3`/`Mathf` 等の値型・数学型は可）。MonoBehaviour 依存は `Assets/Scripts/Components/`（シーン配線層）に閉じ込める
- **Do**: Components 層はライフサイクルと配線のみ（System の生成・入力の受け渡し・Transform への反映）。判定・状態遷移・スコア計算のロジックは `Systems/` の純粋クラスへ
- **Do**: 入力は Input System（`com.unity.inputsystem`）を使い、`Assets/Scripts/Input/` の1モジュールに集約する
- **Don't**: 旧 `Input.GetKey` / `Input.GetAxis` を使わない。MonoBehaviour ごとに入力読み取りを散らさない
- **Do**: 資産参照（プレハブ・マテリアル・AudioClip 等の動的ロード）は `GameConfig.cs` の `AssetKeys` 定数経由。インスペクタ直参照（`[SerializeField]`）は可
- **Don't**: `Resources.Load("Hero")` のようなパス文字列直書き禁止
- **Do**: 永続化 I/O（`Application.persistentDataPath`・`File`・`PlayerPrefs`）は `Assets/Scripts/Persistence/` のみで行う。メタ進行ロジックは `Systems/Meta/` の pure C#（値を受けて値を返す reducer）に置く（tech-stack-unity.md「セーブ / 永続化」）
- **Don't**: `Systems/`・`Components/`・`Ui/` から File I/O / PlayerPrefs を直接呼ばない
- **Don't**: **セーブ破損時に黙って初期化しない** — パース失敗・`save_version` 欠落・未来版・スキーマ検証失敗（必須フィールド欠落・型不正）は必ず (1) `.bak` 退避 (2) `Debug.LogError("[SaveCorruption] ...")` 1回 (3) 既定値再生成＋`recovered` フラグ伝播、の3点セット（contract §6）。catch して既定値を返すだけの実装・フィールド単位で既定値に埋める実装は CR-CODE で CONCERNS 以上
- **Don't**: HUD/メニューの Canvas を `RenderMode.ScreenSpaceOverlay` にしない（QA の RenderTexture 撮影に写らない — tech-stack-unity.md 規約14。`ScreenSpaceCamera` + `worldCamera` 固定）

## 正誤例

### マジックナンバー

```csharp
// NG: コンポーネントに数値直書き
transform.position += Vector3.forward * 5f * Time.deltaTime;
if (score > 1000) LevelUp();

// OK: GameConfig.cs に集約
public static class GameConfig
{
    public static class Player { public const float MoveSpeed = 5f; }        // m/s
    public static class Score  { public const int LevelUpThreshold = 1000; }
}

// 使用側
transform.position += Vector3.forward * GameConfig.Player.MoveSpeed * Time.deltaTime;
if (score > GameConfig.Score.LevelUpThreshold) LevelUp();
```

### Systems/ のエンジン非依存

```csharp
// NG: Assets/Scripts/Systems/CombatSystem.cs
public class CombatSystem : MonoBehaviour            // 禁止（Systems での MonoBehaviour 継承）
{
    void Update() { /* ... */ }
}

// OK: Assets/Scripts/Systems/CombatSystem.cs — 純粋 C#。状態を受け取り新しい状態を返す
public static class CombatSystem
{
    public static EntityState ApplyHit(EntityState target, int damage) =>
        target with { Hp = System.Math.Max(0, target.Hp - damage) };
}
```

### 入力集約（Input System）

```csharp
// NG: 各 MonoBehaviour で散在
if (Input.GetKeyDown(KeyCode.Space)) Jump();          // 旧API・散在の二重違反

// OK: Assets/Scripts/Input/InputReader.cs に集約し、Components はイベント/状態を購読する
public sealed class InputReader
{
    private readonly GameInputActions actions = new();  // Input System 生成クラス
    public bool JumpPressed => actions.Gameplay.Jump.WasPressedThisFrame();
}
```
