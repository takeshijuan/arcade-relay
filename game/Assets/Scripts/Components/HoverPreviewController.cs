// HoverPreviewController.cs — ビルドスポット/設置タワーへのマウスホバーでハイライト+射程プレビュー円を
// 薄く表示する非破壊入力コンポーネント（S-18・Game シーン専用配置）。
// gdd「操作仕様」マウスホバー行: 「ビルドスポット/タワーのハイライト表示（射程プレビュー円を薄く表示）。
// 入力処理としては非破壊（状態変更なし）。P-03 の視認性向上のみ」。
//
// 実装判断（gdd 未確定部分の解釈）: 「射程プレビュー円（該当タワーの RANGE_M 半径）」は「該当タワー」が
// 存在する場合にのみ意味を持つ数値のため、射程プレビュー円は設置済みタワーが乗っているスポットをホバーした
// ときだけ表示する（タワー種別未確定の空きスポットに架空の射程を表示して P-01「一手必中の配置」の判断材料を
// 誤らせないため）。ハイライト輪自体は空き/占有どちらのスポットをホバーしても表示し、「今カーソルが当たって
// いる対象」を常に一瞥可読にする（P-03）。
//
// クリック判定と同じ「カメラ投影+スクリーンpx距離」方式（Ui/HudPanel.FindClickedSpot と同型）で
// InputReader のポインタ座標からホバー対象スポットを判定する。ロジックは単純な距離比較のみで、状態は
// BuildSpotController.BuildSpots から毎フレーム読むだけ（表示専任・規約3: Components は薄く）。
using UnityEngine;
using ForgeGame.Input;
using ForgeGame.Systems;

namespace ForgeGame.Components
{
    /// <summary>Game シーンに1つだけ置く。BuildSpotController への参照は SerializeField で配線する。</summary>
    public sealed class HoverPreviewController : MonoBehaviour
    {
        [SerializeField] private BuildSpotController buildSpotController;

        private InputReader inputReader;
        private Camera worldCamera;
        private GameObject previewGo;
        private Transform highlightRing;
        private Transform rangeCircle;
        private int hoveredSpotIndex = -1;
        // CR-CODE S-18 iter1 minor指摘: SetReferencesForTest 経由の注入かどうかを区別するフラグ
        // （Ui/HudPanel.referencesSetForTest と同じ先例）。テストが意図的に null を注入したケースを
        // 配線バグの誤検知から除外する。
        private bool referencesSetForTest;

        /// <summary>テスト用の読み取り専用状態公開（表示専任の原則。内部状態そのものは複製しない）。</summary>
        public GameObject PreviewGameObject => previewGo;
        public GameObject RangeCircleGameObject => rangeCircle != null ? rangeCircle.gameObject : null;
        public int HoveredSpotIndex => hoveredSpotIndex;

        /// <summary>テスト用の参照注入。Awake() 実行前（非アクティブ状態）に呼ぶこと（規約9）。
        /// camera 省略時は Awake で Camera.main を使う（本番シーン配線と同じ経路）。</summary>
        public void SetReferencesForTest(BuildSpotController build, Camera camera = null)
        {
            buildSpotController = build;
            worldCamera = camera;
            referencesSetForTest = true;
        }

        private void Awake()
        {
            inputReader = new InputReader();
            if (worldCamera == null) worldCamera = Camera.main;
            BuildPreviewVisuals();

            // CR-CODE S-18 iter1 minor指摘: buildSpotController/worldCamera 未配線は StepHover を毎フレーム
            // 無警告で no-op させホバープレビュー機能全体を消失させる。BuildSpotController.Start の
            // waveSpawnController null チェック・HudPanel.Awake の pausePanel null チェックと同じ
            // 「配線 null = 配線バグ → 1回だけ LogError」先例に合わせる。本番 Game.unity では常にインスペクタ
            // 配線済み（buildSpotController: fileID 400000022）のため、referencesSetForTest 経由の意図的な
            // テスト省略を除き null は配線バグ以外に発生し得ない。
            if (buildSpotController == null && !referencesSetForTest)
            {
                Debug.LogError("[HoverPreviewController] BuildSpotController is not wired; hover preview cannot detect towers/spots.");
            }
            if (worldCamera == null && !referencesSetForTest)
            {
                Debug.LogError("[HoverPreviewController] No camera available (Camera.main is null); hover preview cannot project pointer position.");
            }
        }

        private void OnEnable() => inputReader?.Enable();

        private void OnDisable() => inputReader?.Disable();

        private void Update() => StepHover();

        /// <summary>PlayMode テスト用の直接駆動口（BuildSpotController.StepForTest と同様の規約9シーム）。</summary>
        public void StepForTest() => StepHover();

        private void StepHover()
        {
            if (buildSpotController == null || worldCamera == null)
            {
                SetPreviewActive(false);
                hoveredSpotIndex = -1;
                return;
            }

            int spot = FindHoveredSpot(inputReader.PointerScreenPosition);
            if (spot < 0)
            {
                SetPreviewActive(false);
                hoveredSpotIndex = -1;
                return;
            }

            hoveredSpotIndex = spot;
            UpdatePreviewTransform(spot);
            SetPreviewActive(true);
        }

        private int FindHoveredSpot(Vector2 pointerScreen)
        {
            int best = -1;
            float bestDistSq = GameConfig.Ui.BuildSpotClickPickRadiusPx * GameConfig.Ui.BuildSpotClickPickRadiusPx;
            Vector3[] spots = GameConfig.Build.SpotPositions;
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 screenPoint = worldCamera.WorldToScreenPoint(spots[i]);
                if (screenPoint.z <= 0f) continue; // カメラの後方は無視

                float distSq = ((Vector2)screenPoint - pointerScreen).sqrMagnitude;
                if (distSq <= bestDistSq)
                {
                    bestDistSq = distSq;
                    best = i;
                }
            }
            return best;
        }

        private void UpdatePreviewTransform(int spotIndex)
        {
            Vector3 position = GameConfig.Build.SpotPositions[spotIndex];
            position.y += GameConfig.Presentation.HoverPreviewYOffsetM;
            previewGo.transform.position = position;

            bool hasTower = TryFindTowerAtSpot(spotIndex, out TowerInstance tower);
            rangeCircle.gameObject.SetActive(hasTower);
            if (hasTower)
            {
                float rangeM = tower.Type == TowerType.BastionCannon
                    ? GameConfig.BastionCannon.RangeM
                    : GameConfig.ArcEmitter.RangeM;
                float diameter = rangeM * 2f;
                // CR-CODE S-18 iter1 major指摘: Y に 1f を直書きすると CreateFlatDisc が設定した薄板厚
                // （HoverPreviewThicknessM/defaultCylinderHeightUnits）を上書きし、既定 Cylinder（高さ2unit）
                // 前提で ~2m 厚の円柱になってしまう（acceptance「射程プレビュー円…薄く表示」に反する）。
                // localScale.y は生成時に設定済みの薄板厚をそのまま保持する。
                rangeCircle.localScale = new Vector3(diameter, rangeCircle.localScale.y, diameter);
            }
        }

        private bool TryFindTowerAtSpot(int spotIndex, out TowerInstance tower)
        {
            var towers = buildSpotController.BuildSpots.Towers;
            for (int i = 0; i < towers.Count; i++)
            {
                if (towers[i].SpotIndex == spotIndex)
                {
                    tower = towers[i];
                    return true;
                }
            }
            tower = default;
            return false;
        }

        private void SetPreviewActive(bool active)
        {
            if (previewGo != null) previewGo.SetActive(active);
        }

        private void BuildPreviewVisuals()
        {
            previewGo = new GameObject("HoverPreview");
            previewGo.transform.SetParent(transform, false);

            highlightRing = CreateFlatDisc("HoverHighlightRing", previewGo.transform,
                GameConfig.Presentation.HoverHighlightRadiusM * 2f, GameConfig.Placeholder.HoverHighlightColor);
            // 射程プレビュー円は初期直径0（未使用時）でスケールし、ホバー中にタワーが見つかった場合のみ
            // UpdatePreviewTransform が実際の RANGE_M へ広げる。
            rangeCircle = CreateFlatDisc("HoverRangeCircle", previewGo.transform, 0f, GameConfig.Placeholder.HoverRangeColor);
            rangeCircle.gameObject.SetActive(false);

            previewGo.SetActive(false);
        }

        private static Transform CreateFlatDisc(string name, Transform parent, float diameterM, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // 既定 Cylinder は半径0.5(直径1)・高さ2unitのため、X/Z を目標直径へ、Y を薄い板厚へスケールする
            // （PlaceholderFactory.CreateGroundedPrimitive と同じ「既定プリミティブ形状を基準にスケール算出」
            // 方針。生成直後で sharedMesh 差し替えが無いため実測せず既定値2unitを前提にできる）。
            const float defaultCylinderHeightUnits = 2f;
            float thicknessScale = GameConfig.Presentation.HoverPreviewThicknessM / defaultCylinderHeightUnits;
            go.transform.localScale = new Vector3(diameterM, thicknessScale, diameterM);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localPosition = Vector3.zero;

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateTransparentMaterial(color);
            return go.transform;
        }

        // CR-CODE S-18 iter1 minor指摘: BuildPreviewVisuals はハイライト輪と射程円の2枚のディスクを生成する
        // ため、キャッシュ無しでは CreateTransparentMaterial が Awake 1回につき2回呼ばれ、シェーダ欠落時に
        // 同一 LogError が2回出て「1回だけ明示ログ」の主張と食い違う。シェーダ解決結果をキャッシュして
        // 検索とログを1回に括り出す。
        private static Shader cachedTransparentShader;
        private static bool transparentShaderLookupAttempted;

        private static Material CreateTransparentMaterial(Color color)
        {
            if (!transparentShaderLookupAttempted)
            {
                transparentShaderLookupAttempted = true;
                cachedTransparentShader = Shader.Find("Universal Render Pipeline/Lit");
                if (cachedTransparentShader == null)
                {
                    // 配線破損は1回だけ明示ログ（規約12。PlaceholderFactory.GetOrCreateMaterial と同じ先例）。
                    Debug.LogError("[HoverPreviewController] \"Universal Render Pipeline/Lit\" シェーダが見つからない。ホバープレビューの見た目が欠落した状態になる可能性がある。");
                }
            }

            if (cachedTransparentShader == null) return null;

            var material = new Material(cachedTransparentShader) { color = color };
            // URP Lit を半透明(Alpha Blend)表示へ切替える。以下はゲームパラメータではなく URP シェーダの
            // プロパティ/ブレンドモードAPI定数のため GameConfig 化しない（規約1対象外 — PlaceholderFactory の
            // シェーダ名文字列と同種の「エンジンAPIそのもの」）。
            material.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
            material.SetFloat("_Blend", 0f);   // 0=Alpha
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }
    }
}
