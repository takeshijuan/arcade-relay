---
paths: ["game/Source/**"]
---

# unreal-code — game/Source の C++ 編集時の強制規約（engine=unreal）

正本: `.claude/docs/tech-stack-unreal.md`。違反は CR-CODE ゲートで CONCERNS 以上になる。
思想は phaser 版 `gameplay-code.md` と同一（マジックナンバー禁止 / delta-time / エンジン非依存コア / 入力抽象化 / 資産キー集約）を UE C++ イディオムに翻訳したもの。

## Do / Don't

- **Do**: ゲームパラメータ（速度・HP・スコア・時間）は `Source/ForgeGame/GameConfig.h` の `namespace GameConfig` 内 `constexpr` 定数に集約する。チューニングは GameConfig.h の編集だけで完結すること
- **Don't**: Actor / Component の本文に数値リテラルを直書きしない（配列 index・`0`/`1` 初期値など意味を持たない値は除く）
- **Do**: 移動・タイマーは `Tick(float DeltaSeconds)` の `DeltaSeconds` でスケールする（物理は FixedTick 相当の物理サブステップに委ねる）
- **Don't**: フレーム毎の固定加算を書かない。固定フレームレート前提の実装は禁止
- **Don't**: `Source/ForgeGame/Systems/` で `UObject` / `AActor` を継承しない・`UWorld` / `SpawnActor` / `FindObject` を呼ばない。Systems はエンジン非依存層（純粋 C++。`FVector`/`FMath` 等のコア値型・数学型は可）。UObject 依存は `Source/ForgeGame/Actors/`（シーン配線層）に閉じ込める
- **Do**: Actors 層はライフサイクルと配線のみ。判定・状態遷移・スコア計算のロジックは `Systems/` の純粋関数/クラスへ
- **Do**: 入力は Enhanced Input（`UInputAction` / `UInputMappingContext`）を使い、マッピングは1箇所（PlayerController または専用コンポーネント）に集約する
- **Don't**: 旧 `BindAxis("MoveForward")` 系の文字列ベース入力を使わない。Actor ごとに入力バインドを散らさない
- **Do**: 資産参照は `GameConfig.h` に集約した `FSoftObjectPath` / `TSoftObjectPtr` 定数、または UPROPERTY 経由の直参照。パス文字列は定数1箇所のみ
- **Don't**: `LoadObject<UStaticMesh>(nullptr, TEXT("/Game/Meshes/Hero"))` のようなパス文字列を実装本文に直書きしない
- **Do**: 永続化 I/O（`USaveGame` 派生・`UGameplayStatics::SaveGameToSlot/LoadGameFromSlot`）は `Source/ForgeGame/Persistence/` のみで行う。メタ進行ロジックは `Systems/Meta/` の pure C++（純粋 struct + 純粋関数）に置く（tech-stack-unreal.md「セーブ / 永続化」）
- **Don't**: `Systems/` に `USaveGame` を置かない（UObject 派生のため規約違反）。`Actors/`・`Ui/` から SaveGameToSlot を直接呼ばない
- **Don't**: **セーブ破損時に黙って初期化しない** — ロード失敗・`SaveVersion` 不正・スキーマ検証失敗（必須フィールド欠落・型不正）は必ず (1) `.bak.sav` 退避 (2) `UE_LOG(LogForgeGame, Error, TEXT("[SaveCorruption] ..."))` 1回 (3) 既定値再生成＋`bRecovered` 伝播、の3点セット（contract §6）。失敗を握り潰して既定値を返すだけの実装・フィールド単位で既定値に埋める実装は CR-CODE で CONCERNS 以上

## 正誤例

### マジックナンバー

```cpp
// NG: Actor に数値直書き
AddActorWorldOffset(GetActorForwardVector() * 600.f * DeltaSeconds);
if (Score > 1000) { LevelUp(); }

// OK: Source/ForgeGame/GameConfig.h に集約
namespace GameConfig
{
    namespace Player { constexpr float MoveSpeed = 600.f; }   // cm/s（UE単位）
    namespace Score  { constexpr int32 LevelUpThreshold = 1000; }
}

// 使用側
AddActorWorldOffset(GetActorForwardVector() * GameConfig::Player::MoveSpeed * DeltaSeconds);
if (Score > GameConfig::Score::LevelUpThreshold) { LevelUp(); }
```

### Systems/ のエンジン非依存

```cpp
// NG: Source/ForgeGame/Systems/CombatSystem.h
#include "GameFramework/Actor.h"                     // 禁止（Systems での UObject/Actor 依存）
class ACombatSystem : public AActor { /* ... */ };

// OK: Source/ForgeGame/Systems/CombatSystem.h — 純粋 C++
#include "ForgeGame/Types.h"
namespace CombatSystem
{
    FEntityState ApplyHit(const FEntityState& Target, int32 Damage);  // 値を受けて値を返す
}
```

### 入力集約（Enhanced Input）

```cpp
// NG: 文字列ベースの旧入力・Actor 散在
InputComponent->BindAxis("MoveForward", this, &AHero::MoveForward);

// OK: Enhanced Input。UInputAction をプロパティで受け、PlayerController 1箇所でマッピング
UPROPERTY(EditDefaultsOnly, Category="Input") TObjectPtr<UInputAction> MoveAction;
EnhancedInput->BindAction(MoveAction, ETriggerEvent::Triggered, this, &AHero::OnMove);
```
