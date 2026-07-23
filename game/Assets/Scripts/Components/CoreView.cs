// CoreView.cs — コアクリスタルのシーン表現 + HP状態の唯一の保持者（Game シーン・S-04）。
// ロジックは Systems/CoreDefenseSystem.cs（純粋 C#）へ委譲する。ここはライフサイクルと
// Transform/マテリアル反映のみ（規約3: Components は薄く）。
// MDL-05（CoreCrystal）取込済み（Integrate）。未取込/未生成時のみ単色 Cylinder プレースホルダへ
// フォールバックする（assets-config.md プレースホルダ運用）。被弾時 SFX-04（コア被弾音。敗北成立時の
// コア崩壊音としても design/assets.md の指定どおり流用）を ApplyDamage で再生する。
// S-26: gdd「コアクリスタル仕様」/「モーション方式」節が明記する被弾時のヒットエフェクト
// （マテリアルカラー一時変更のヒットフラッシュ + スケールパルス）を実装する。TowerView.PlayFireMotion /
// EnemyView.PlayDefeatMotion と同型の Time.deltaTime 駆動パターン（Update() で経過秒数を進め、
// GameConfig.Presentation.CoreHitFlashSec で既定色/既定スケールへ線形に復帰する）。
// CR-CODE S-26 iter1 対応: 色の一時変更は Renderer.material への書き込みではなく
// MaterialPropertyBlock 経由に変更した（.material は初アクセスでレンダラごとにマテリアルを
// インスタンス化し、GameObject.Destroy 後もそのインスタンスは解放されず、シーン再読込
// （リスタート）ごとに累積リークする — PlaceholderFactory.materialCache が明示的に回避している
// のと同じリーク種別）。MaterialPropertyBlock はマテリアルをインスタンス化せず、共有マテリアルの
// 上にオーバーレイ表示するだけなので破棄不要。
// CR-CODE S-26 iter2 対応: 主色プロパティ名はマテリアルのシェーダで異なる。PlaceholderFactory /
// HoverPreviewController / EnvironmentView が使う "Universal Render Pipeline/Lit" は "_BaseColor" だが、
// MDL-05（model-core-crystal.glb）は glTFast（com.unity.cloud.gltfast、URP 経路）で取込済みのため
// ShaderGraphMaterialGenerator 系シェーダグラフが使われ、主色プロパティ名は glTF 準拠の
// "baseColorFactor"（GLTFast.Materials.MaterialProperty.BaseColor の実体。
// Library/PackageCache/com.unity.cloud.gltfast.../Runtime/Scripts/Material/MaterialProperty.cs で確認）。
// "_BaseColor" 固定書き込みは実モデル経路で存在しないプロパティへの no-op となり、
// 被弾ヒットフラッシュが視覚的に一切発生しない silent failure になっていた。各 Renderer の
// sharedMaterial.HasProperty で実際に持つプロパティ名を Awake 時に解決し、以後はそれを使う。
using UnityEngine;
using ForgeGame.Systems;

namespace ForgeGame.Components
{
    /// <summary>Game シーンに1つだけ置く。</summary>
    public sealed class CoreView : MonoBehaviour
    {
        // PlaceholderFactory 系（URP/Lit）の主色プロパティ。
        private static readonly int UrpLitBaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        // glTFast（URP シェーダグラフ経路）取込マテリアルの主色プロパティ
        // （GLTFast.Materials.MaterialProperty.BaseColor と同じ ID）。
        private static readonly int GltfBaseColorPropertyId = Shader.PropertyToID("baseColorFactor");

        public int CurrentHp { get; private set; }
        public bool IsDefeated => CoreDefenseSystem.IsDefeated(CurrentHp);

        private Renderer[] visualRenderers;
        private Color[] visualBaseColors;
        // 各 Renderer に現在適用中の色（MaterialPropertyBlock 経由。.material を読み返さずここで追跡する）。
        private Color[] currentTintColors;
        // 各 Renderer の sharedMaterial が実際に持つ主色プロパティ ID（Awake で解決）。
        // どちらも持たない場合は resolvedColorProperty[i]=false とし、その Renderer への色書き込みはスキップする。
        private int[] colorPropertyIds;
        private bool[] resolvedColorProperty;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseScale;
        // 負値=非再生中。0以上ならヒットフラッシュ/スケールパルス演出の経過秒数（Time.deltaTime で進める）。
        private float hitFlashElapsedSec = -1f;

        /// <summary>ヒットフラッシュ中に実際に適用されている色（テスト観測用の読み取り専用プロパティ。
        /// TowerView.VisualTransform と同型のテストシーム）。MaterialPropertyBlock 経由で適用した値を
        /// そのまま追跡して返す（Renderer.material は読まない＝インスタンス化しない）。
        /// Renderer 未生成時は既定色を返す。</summary>
        public Color CurrentTintColor => currentTintColors != null && currentTintColors.Length > 0
            ? currentTintColors[0]
            : GameConfig.Placeholder.CoreColor;

        private void Awake()
        {
            CurrentHp = GameConfig.Core.HpMax;
            transform.position = GameConfig.Path.EndPoint;
            baseScale = transform.localScale;
            propertyBlock = new MaterialPropertyBlock();

            GameObject visual = GeneratedModelFactory.TryCreateGroundedModel(
                GameConfig.AssetKeys.ModelCoreCrystal, transform, "CoreModel");
            if (visual == null)
            {
                visual = PlaceholderFactory.CreateGroundedPrimitive(
                    PrimitiveType.Cylinder, transform, GameConfig.Presentation.CoreHeightM,
                    GameConfig.Placeholder.CoreColor, "CorePlaceholderVisual");
            }

            // sharedMaterial 読み取りのみ（.material にはアクセスしないためインスタンス化しない）。
            visualRenderers = visual.GetComponentsInChildren<Renderer>();
            visualBaseColors = new Color[visualRenderers.Length];
            colorPropertyIds = new int[visualRenderers.Length];
            resolvedColorProperty = new bool[visualRenderers.Length];
            bool anyResolved = false;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                Material sharedMat = visualRenderers[i].sharedMaterial;
                if (sharedMat != null && sharedMat.HasProperty(UrpLitBaseColorPropertyId))
                {
                    colorPropertyIds[i] = UrpLitBaseColorPropertyId;
                    resolvedColorProperty[i] = true;
                }
                else if (sharedMat != null && sharedMat.HasProperty(GltfBaseColorPropertyId))
                {
                    colorPropertyIds[i] = GltfBaseColorPropertyId;
                    resolvedColorProperty[i] = true;
                }

                visualBaseColors[i] = resolvedColorProperty[i]
                    ? sharedMat.GetColor(colorPropertyIds[i])
                    : GameConfig.Placeholder.CoreColor;
                anyResolved |= resolvedColorProperty[i];
            }
            currentTintColors = (Color[])visualBaseColors.Clone();

            if (!anyResolved && visualRenderers.Length > 0)
            {
                // どの Renderer も既知の主色プロパティ（_BaseColor / baseColorFactor）を持たない。
                // ヒットフラッシュは localScale パルスのみになり色変化は視覚的 no-op になるため、
                // 無言で見過ごさず1回明示ログする（GeneratedModelFactory の Renderer 欠落ログと同じ思想）。
                Debug.LogError("[CoreView] no renderer material exposes a known base color property " +
                    "(_BaseColor / baseColorFactor); hit flash color tint will be a visual no-op.");
            }
        }

        /// <summary>ゴール到達した敵の攻撃をコアへ適用する（CoreDefenseSystem.ApplyDamage への薄い配線）。
        /// 演出キューの選択は FeedbackCueSystem に委譲する（S-13）。被弾ヒットフラッシュ+スケールパルス
        /// （S-26）を開始する。</summary>
        public void ApplyDamage(EnemyType attacker)
        {
            CurrentHp = CoreDefenseSystem.ApplyDamage(CurrentHp, attacker);
            FeedbackCue cue = FeedbackCueSystem.SelectCoreHitCue();
            AudioCuePlayer.PlayOneShot(cue.AssetKey, transform.position, pitch: cue.PitchMultiplier);
            hitFlashElapsedSec = 0f;
        }

        private void Update()
        {
            if (hitFlashElapsedSec < 0f) return;

            // CR-CODE S-26 iter1 対応: 経過秒数を加算する前に t/intensity を計算してから適用する
            // 順序へ変更した。従来は「加算してから適用」だったため、被弾直後の最初の Update() で
            // 既に Time.deltaTime 分進んだ状態から描画され、コメントが述べる「0秒時点で最大フラッシュ」
            // が一度も描画されないまま終わりうる不具合があった（さらに1フレーム目の deltaTime が
            // CoreHitFlashSec 以上だと intensity=0 のまま演出全体が無音で終わる）。この順序なら
            // hitFlashElapsedSec=0 の最初のフレームで必ず intensity=1（最大フラッシュ/パルス）が
            // 適用されてから次フレーム用に時間を進めるため、フレームレートに関わらず最低1フレームは
            // 変化が可視化される。
            float duration = GameConfig.Presentation.CoreHitFlashSec;
            float t = duration > 0f ? Mathf.Clamp01(hitFlashElapsedSec / duration) : 1f;

            // 0秒時点で最大フラッシュ/最大パルス、durationで既定色・既定スケールへ線形に復帰する
            // （TowerView.PlayFireMotion のショートパンチと同型パターン）。フラッシュ色は
            // GameConfig.Placeholder の既存パレット値（EnemyColor＝攻撃してきた敵の主色）を流用し、
            // 新規のパレット値は追加しない。
            float intensity = 1f - t;
            ApplyTint(intensity);
            transform.localScale = baseScale * (1f + intensity * GameConfig.Presentation.CoreHitScalePulse);

            if (t >= 1f)
            {
                transform.localScale = baseScale;
                hitFlashElapsedSec = -1f;
                return;
            }

            hitFlashElapsedSec += Time.deltaTime;
        }

        /// <summary>MaterialPropertyBlock 経由で intensity 分の色を各 Renderer へ適用し、
        /// currentTintColors（テスト観測用）を更新する。.material には触れないためマテリアルを
        /// インスタンス化しない（CR-CODE S-26 iter1 対応）。書き込み先プロパティは Awake で解決した
        /// Renderer ごとの colorPropertyIds を使う（CR-CODE S-26 iter2 対応: _BaseColor 固定書き込みは
        /// glTFast 取込マテリアルで no-op だったため）。既知の主色プロパティを持たない Renderer は
        /// スキップする（currentTintColors の追跡値は更新するため、テストシームは意図値を返し続ける）。</summary>
        private void ApplyTint(float intensity)
        {
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                Color tint = Color.Lerp(visualBaseColors[i], GameConfig.Placeholder.EnemyColor, intensity);
                currentTintColors[i] = tint;
                if (!resolvedColorProperty[i]) continue;
                visualRenderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(colorPropertyIds[i], tint);
                visualRenderers[i].SetPropertyBlock(propertyBlock);
            }
        }

        // CR-CODE S-04 iter1 対応: 部分リセット用 ResetForNewRun() は削除した。docs/architecture.md の
        // ゲームフロー表（Result「もう一度」→ Game）はリスタートを SceneManager.LoadScene によるシーン
        // 再生成と定義しており、CoreView の HP のみを戻す部分リセット API は
        // WaveSpawnController 側（defeatEventFired ラッチ・ウェーブ消化状態・enemyViews）が追随していない
        // ため片手落ちで危険（呼ぶと2ラン目に敵が湧かず敗北判定も二度と発火しない）。
        // リスタートは常にシーン再読み込みに一本化し、Awake() の初期化のみで HP は再初期化される。
    }
}
