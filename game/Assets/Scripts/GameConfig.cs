// GameConfig.cs — Crystal Bastion（仮）全ゲームパラメータの唯一の集約点。
// 正本: design/gdd.md「数値表」「メタ進行（アウトゲーム）」節。値は GDD の初期値をそのまま写す。
// マジックナンバー禁止（tech-stack-unity.md 規約1）: 数値・パス文字列はここだけに書く。
// チューニングはこのファイルの編集だけで完結させること。
// using UnityEngine は Color 等の値型のみに使用（Vector3/Mathf と同様、rules/unity-code.md が許容する範囲）。
using UnityEngine;

namespace ForgeGame
{
    /// <summary>全ゲームパラメータの静的集約。全て GDD 数値表の初期値。</summary>
    public static class GameConfig
    {
        /// <summary>コアクリスタル（防衛対象）。</summary>
        public static class Core
        {
            public const int HpMax = 100;                 // CORE_HP_MAX（80〜140）
        }

        /// <summary>ラン内経済。</summary>
        public static class Economy
        {
            public const int StartingGold = 100;          // STARTING_GOLD（80〜150）
            public const int WaveClearGoldReward = 20;    // WAVE_CLEAR_GOLD_REWARD（15〜35）
        }

        /// <summary>ビルドスポットと売却。</summary>
        public static class Build
        {
            public const int NumBuildSpots = 6;           // NUM_BUILD_SPOTS（5〜8）
            public const float SellRefundRate = 0.5f;     // TOWER_SELL_REFUND_RATE（0.4〜0.7）

            // ビルドスポット配置（S-05 実装判断）: gdd はビルドスポットの総数のみ規定し具体的な世界座標は
            // 未確定のため、S-04 の GameConfig.Path 判断（一本道=X軸・Z=0固定の直線）に倣い、経路を挟んで
            // 南北交互に配置する固定俯瞰1画面レイアウトとして確定する。X方向は経路の始点・終点付近を避け
            // （SpotMarginFraction）均等間隔で配置し、Z方向は経路からの一定オフセット（SpotOffsetZ。
            // BASTION_CANNON_RANGE_M=6 / ARC_EMITTER_RANGE_M=4.5 いずれの射程でも経路上の敵を捕捉できる距離）。
            public const float SpotOffsetZ = 3f;            // 経路(Z=0)からのオフセット距離(m)
            public const float SpotMarginFraction = 0.12f;  // 経路端から避けるマージン（PathLengthM に対する比率）

            public static readonly Vector3[] SpotPositions = CreateSpotPositions();

            private static Vector3[] CreateSpotPositions()
            {
                var positions = new Vector3[NumBuildSpots];
                int columns = Mathf.Max(1, Mathf.CeilToInt(NumBuildSpots / 2f));
                float margin = Wave.PathLengthM * SpotMarginFraction;
                float xStart = Path.StartPoint.x + margin;
                float xEnd = Path.EndPoint.x - margin;

                for (int i = 0; i < NumBuildSpots; i++)
                {
                    int column = i / 2;
                    float t = columns <= 1 ? 0.5f : (float)column / (columns - 1);
                    float x = Mathf.Lerp(xStart, xEnd, t);
                    float z = (i % 2 == 0) ? SpotOffsetZ : -SpotOffsetZ;
                    positions[i] = new Vector3(x, 0f, z);
                }

                return positions;
            }
        }

        /// <summary>ウェーブ進行・経路。</summary>
        public static class Wave
        {
            public const int WaveCount = 8;               // WAVE_COUNT（6〜10）
            public const float WavePrepSec = 15f;         // WAVE_PREP_SEC（10〜25）
            public const float SpawnIntervalBase = 1.0f;  // SPAWN_INTERVAL_BASE 秒（0.6〜1.5）
            public const float PathLengthM = 40f;         // PATH_LENGTH_M（30〜60）
        }

        /// <summary>
        /// 経路の世界座標（S-04 実装判断）。gdd は経路を「一本道の始点→ゴール」と長さ（PathLengthM）のみ規定し、
        /// 具体的な形状は未確定のため、固定俯瞰1画面に収める直線（X軸に沿う・Z=0固定）として確定する。
        /// タワー配置（S-05）は経路を変更しない（gdd「Marauder」行）ため、この2点だけで経路全体が決まる。
        /// </summary>
        public static class Path
        {
            public static readonly Vector3 StartPoint = new Vector3(-Wave.PathLengthM / 2f, 0f, 0f);
            public static readonly Vector3 EndPoint = new Vector3(Wave.PathLengthM / 2f, 0f, 0f); // コア設置位置
        }

        /// <summary>
        /// ウェーブ構成表（gdd「難易度曲線」節・WAVE 1〜8 の敵構成列 + 出現間隔倍率をそのまま集約。S-12）。
        /// SpawnIntervalMultiplier は SpawnIntervalBase に掛ける係数（gdd 表の「出現間隔◯%」列）。
        /// WAVE 4 は gdd 表に出現間隔の明記が無い（「両種増数」のみ）— WAVE 2 の「出現間隔は変化なし」と
        /// 同じ省略パターンと解釈し、直前の WAVE 3（90%）を継承する（S-12 実装判断。P-03 の「漸増する圧力」を
        /// 裏切らない=途中で緩めない解釈が gdd の単調減少の並びと整合するため）。
        /// </summary>
        public static class WaveComposition
        {
            public readonly struct WaveDef
            {
                public readonly int MarauderCount;
                public readonly int WarbeastCount;
                public readonly float SpawnIntervalMultiplier; // SpawnIntervalBase に掛ける係数（gdd「難易度曲線」表）

                public WaveDef(int marauderCount, int warbeastCount, float spawnIntervalMultiplier)
                {
                    MarauderCount = marauderCount;
                    WarbeastCount = warbeastCount;
                    SpawnIntervalMultiplier = spawnIntervalMultiplier;
                }
            }

            // index 0 = WAVE 1 … index 7 = WAVE 8（gdd「難易度曲線」表と一致）。
            public static readonly WaveDef[] Waves =
            {
                new WaveDef(6, 0, 1.0f),    // WAVE 1: 出現間隔 SPAWN_INTERVAL_BASE のまま
                new WaveDef(8, 0, 1.0f),    // WAVE 2: 出現間隔は変化なし
                new WaveDef(6, 2, 0.9f),    // WAVE 3: 出現間隔90%（Warbeast 初出現）
                new WaveDef(10, 2, 0.9f),   // WAVE 4: gdd に明記無し→WAVE 3 を継承（上記コメント参照）
                new WaveDef(8, 4, 0.8f),    // WAVE 5: 出現間隔80%
                new WaveDef(10, 5, 0.75f),  // WAVE 6: 出現間隔75%
                new WaveDef(12, 6, 0.7f),   // WAVE 7: 出現間隔70%
                new WaveDef(14, 8, 0.65f),  // WAVE 8: 出現間隔65%
            };
        }

        /// <summary>タワーA: 単体高火力（P-02）。アップグレードはダメージのみ増加、役割不変。</summary>
        public static class BastionCannon
        {
            public const int Cost = 50;                   // BASTION_CANNON_COST（40〜70）
            public const int DamageLv1 = 25;              // BASTION_CANNON_DAMAGE_LV1（18〜35）
            public const int DamageLv2 = 40;              // BASTION_CANNON_DAMAGE_LV2（30〜55）
            public const int DamageLv3 = 65;              // BASTION_CANNON_DAMAGE_LV3（50〜85）
            public const float FireInterval = 1.2f;       // BASTION_CANNON_FIRE_INTERVAL 秒（0.9〜1.5）
            public const float RangeM = 6f;               // BASTION_CANNON_RANGE_M（5〜8）
            public const int UpgradeLv2Cost = 40;         // BASTION_CANNON_UPGRADE_LV2_COST（30〜55）
            public const int UpgradeLv3Cost = 70;         // BASTION_CANNON_UPGRADE_LV3_COST（55〜90）
        }

        /// <summary>タワーB: 範囲低火力（P-02）。アップグレードはダメージのみ増加、役割不変。</summary>
        public static class ArcEmitter
        {
            public const int Cost = 40;                   // ARC_EMITTER_COST（30〜60）
            public const int DamageLv1 = 8;               // ARC_EMITTER_DAMAGE_LV1 /tick（5〜12）
            public const int DamageLv2 = 14;              // ARC_EMITTER_DAMAGE_LV2（10〜20）
            public const int DamageLv3 = 22;              // ARC_EMITTER_DAMAGE_LV3（16〜32）
            public const float TickInterval = 0.8f;       // ARC_EMITTER_TICK_INTERVAL 秒（0.6〜1.0）
            public const float RadiusM = 4f;              // ARC_EMITTER_RADIUS_M（2.5〜4）。CR-CODE S-22 iter2: 3f は
                                                           // Build.SpotOffsetZ(3f) と同値で命中区間幅が0mに縮退するため是正
            public const float RangeM = 4.5f;             // ARC_EMITTER_RANGE_M（4〜6）
            public const int UpgradeLv2Cost = 35;         // ARC_EMITTER_UPGRADE_LV2_COST（25〜50）
            public const int UpgradeLv3Cost = 60;         // ARC_EMITTER_UPGRADE_LV3_COST（45〜85）
        }

        /// <summary>タワー共通のレベル上限。</summary>
        public static class Tower
        {
            public const int MaxLevel = 3;                // Lv1→Lv2→Lv3（P-02: 役割を変えない強化段階）
        }

        /// <summary>敵: Marauder（速い・低HP）。</summary>
        public static class Marauder
        {
            public const int Hp = 30;                     // MARAUDER_HP（20〜45）
            public const float SpeedMps = 3.5f;           // MARAUDER_SPEED_MPS（3〜4.5）
            public const int GoldReward = 5;              // MARAUDER_GOLD_REWARD（3〜8）
            public const int CoreDamage = 1;              // MARAUDER_CORE_DAMAGE（1〜2）
        }

        /// <summary>敵: Warbeast（遅い・高HP）。</summary>
        public static class Warbeast
        {
            public const int Hp = 90;                     // WARBEAST_HP（70〜130）
            public const float SpeedMps = 1.8f;           // WARBEAST_SPEED_MPS（1.5〜2.5）
            public const int GoldReward = 12;             // WARBEAST_GOLD_REWARD（8〜18）
            public const int CoreDamage = 3;              // WARBEAST_CORE_DAMAGE（2〜5）
        }

        /// <summary>スコア算出式（gdd スコア・進行節）。</summary>
        public static class Score
        {
            public const int CoreWeight = 500;            // SCORE_CORE_WEIGHT（400〜700）
            public const int KillWeight = 8;              // SCORE_KILL_WEIGHT（5〜12）
            public const float TimeParSec = 300f;         // SCORE_TIME_PAR_SEC（240〜360）
            public const int TimeWeight = 2;              // SCORE_TIME_WEIGHT（1〜4）
        }

        /// <summary>メタ進行（essence 通貨・ラン間アップグレード・実績閾値）。</summary>
        public static class Meta
        {
            // essence 算出式（gdd 通貨節）
            public const int EssenceBaseLoss = 5;         // ESSENCE_BASE_LOSS（3〜8）
            public const int EssenceKillDivisor = 5;      // ESSENCE_KILL_DIVISOR（4〜8）
            public const int EssenceWinBonus = 15;        // ESSENCE_WIN_BONUS（10〜25）

            // ラン間アップグレード（UPG-xx）。Lv 上限は全て 3。
            public const int UpgradeMaxLevel = 3;
            public const int Upg01GoldPerLevel = 15;      // UPG-01 初期資金 +15/Lv（10〜25）
            public const float Upg02DiscountPerLevel = 0.03f; // UPG-02 割引 -3%/Lv（-2%〜-5%）
            public const float Upg03EssenceRatePerLevel = 0.08f; // UPG-03 essence +8%/Lv（5%〜15%）
            public const int UpgradePurchaseCostPerLevel = 30; // UPG_PURCHASE_COST_PER_LV essence（20〜50）

            // 実績閾値（ACH-xx）
            public const int CenturySlayerKills = 100;    // ACH-03 累計撃破（80〜150）
            public const int AoeSpecialistKills = 20;     // ACH-04 単一ラン AoE撃破（15〜30）
            public const int FrugalMaxSpots = 4;          // ACH-05 倹約防衛: 使用スポット <= 4（3〜5）
        }

        /// <summary>セーブ / 永続化（contract §6・tech-stack-unity.md セーブ節）。</summary>
        public static class Save
        {
            public const int CurrentVersion = 1;          // save_version の現行版
            public const string FileName = "save.json";   // persistentDataPath 配下
            public const string CorruptionLogPrefix = "[SaveCorruption]"; // 破損時明示ログ接頭辞

            // S-16: 音量設定（BGM/SFX）はメタ進行 SaveData とは別ファイルに分離保存する
            // （Persistence/IAudioSettingsStore.cs 冒頭コメント参照）。
            public const string AudioSettingsFileName = "audio-settings.json"; // persistentDataPath 配下
        }

        /// <summary>
        /// 固定俯瞰カメラの構図定数（S-21・唯一の所有 story。他 story は参照のみ・値変更しない）。
        /// 経路はワールド原点を中点とする直線（S-04 実装判断・Path.StartPoint/EndPoint がZ=0固定）のため、
        /// カメラは常に原点（≒経路中点）を注視する高さ・後退量で位置を決め、回転はその注視点から逆算する
        /// （Build.SpotPositions と同様、座標のドリフトを防ぐため GameConfig 内で完結させる）。
        /// PATH_LENGTH_M 全長・NUM_BUILD_SPOTS・コア(EndPoint)を1画面に収める。カメラは移動・ズーム入力を
        /// 持たない（gdd 操作仕様: 固定俯瞰）。
        /// </summary>
        public static class Camera
        {
            public const float HeightM = 26f;        // カメラ高さ(m)。PATH_LENGTH_M=40m全長+マージンを画角に収める見下ろし高さ
            public const float BackOffsetM = 26f;     // 経路中点(≒ワールド原点)からのZ後退量(m)。HeightM と等しく45度俯瞰にする
            public const float FieldOfViewDeg = 55f;  // 垂直画角(度)。40m全長+マージンを1画面に収める

            // 経路はZ=0固定（S-04）のため後退方向はワールドZ軸そのまま。
            public static readonly Vector3 Position = new Vector3(0f, HeightM, -BackOffsetM);
            // 注視点はワールド原点固定。位置(HeightM/BackOffsetM)から逆算するため、値を変えても向きは追随する。
            public static readonly Vector3 EulerAngles =
                new Vector3(Mathf.Atan2(HeightM, BackOffsetM) * Mathf.Rad2Deg, 0f, 0f);
        }

        /// <summary>
        /// URP ディレクショナルライト + 環境光の定数（S-21）。盤面が暗転しない明るさに設定する
        /// （QA-PLAY のスクリーンショット平均輝度 0.02 超が前提の初期値）。
        /// </summary>
        public static class Lighting
        {
            public const float DirectionalIntensityLux = 1.4f;    // Directional Light 強度
            // 見下ろし + 斜め横からの光（カメラと同じ俯角にはせず陰影のメリハリを保つ）。
            public static readonly Vector3 DirectionalEulerAngles = new Vector3(55f, -35f, 0f);
            public static readonly Color DirectionalColor = new Color(1f, 0.96f, 0.90f, 1f); // 暖色寄り白色光
            public static readonly Color AmbientColor = new Color(0.42f, 0.44f, 0.48f, 1f);  // Flat環境光（暗転回避）
        }

        /// <summary>
        /// 地形（地面/経路プレーン）のサイズ実装判断（S-21）。gdd は PATH_LENGTH_M と NUM_BUILD_SPOTS のみを
        /// 規定するため、地面/経路の具体的サイズは Build.SpotPositions と同種の実装判断としてここで確定する。
        /// </summary>
        public static class Environment
        {
            public const float GroundMarginM = 10f;       // 経路端(PathLengthM)からの地面外周マージン(m)
            public const float GroundWidthZFullM = 20f;   // 地面のZ方向全幅(m)。ビルドスポット(Z=±SpotOffsetZ)を覆う
            public const float PathStripWidthZM = 4f;      // 経路帯のZ方向全幅(m)。ビルドスポットのオフセットより狭く保ち重ならない
            public const float TileWorldSizeM = 2f;         // タイル1枚が表す世界サイズ(m)。mainTextureScale 算出に使う
            public const float PathHeightOffsetM = 0.01f;   // 地面プレーンとのZファイティング回避用の微小Yオフセット(m)
        }

        /// <summary>純装飾値（演出用・調整レンジ不要 — gdd モーション方式節）。</summary>
        public static class Presentation
        {
            public const float EnemyBobAmplitude = 0.08f; // 歩行を模す上下ボブの振幅（m）
            public const float EnemyBobFrequency = 6f;    // ボブの角周波数（rad/s）
            public const float WinResultDelaySec = 1f;    // 勝敗成立→Result 遷移前の演出待機
            public const float CoreHitFlashSec = 0.15f;   // コア被弾ヒットフラッシュ時間
            public const float TitlePromptBlinkPeriodSec = 1.2f; // Title「開始」プロンプトの明滅周期（S-02）
            public const float TitlePromptMinAlpha = 0.35f; // Title「開始」プロンプト明滅の最小アルファ（S-02・CR-CODE iter1 #1）
            public const float TitlePromptMaxAlpha = 1f;    // Title「開始」プロンプト明滅の最大アルファ（S-02・CR-CODE iter1 #1）

            // S-13: FeedbackCueSystem がタワー発射音（SFX-02・Bastion Cannon/Arc Emitter 共通の1音。
            // design/assets.md 6点予算制約）をタワー種別で役割差別化するためのピッチ倍率（調整レンジ不要の
            // 装飾値 — design/assets.md SFX-02「実装側でピッチ・音量を僅かに変えて役割差を演出する余地を残す」）。
            public const float BastionCannonFirePitch = 0.9f;  // 「重い一撃」を低めのピッチで表現（P-02）
            public const float ArcEmitterFirePitch = 1.15f;    // 「速射・範囲制圧」を高めのピッチで表現（P-02）

            // S-13 CR-CODE iter1 #1: AudioCuePlayer のカスタムワンショット経路（pitch!=1f）の破棄タイマー
            // 分母クランプ閾値。0 以下の pitch（不正入力）で分母がゼロ近傍になり破棄が極端に遅延することを
            // 防ぐための下限値であり、正常系のチューニング対象ではない（それでも規約1に従い名前付き定数化）。
            public const float MinAudiblePitchForDestroyTimer = 0.01f;

            // 想定サイズ（art-bible.json bbox_authoring_m 目安）。MDL-01/02/03/04/05 は全て Integrate
            // （Resources.Load 経由の実モデル取込・S-19）済みで、取込済みモデルの authoring スケールとも
            // 一致（ForgeAssetIntegration 検証済み）。実モデル未取込/未生成時（Resources.Load が null を
            // 返す場合）のみ、各 View がこの高さでプレースホルダへフォールバックする（想定外の縮退経路）。
            public const float MarauderHeightM = 0.85f;   // MDL-03 想定サイズ（0.7〜1.0m）
            public const float WarbeastHeightM = 1.5f;    // MDL-04 想定サイズ（1.3〜1.7m。取込済み。Resources.Load 失敗時のみプレースホルダへフォールバック）
            public const float CoreHeightM = 2.5f;        // MDL-05 想定サイズ（2.2〜2.8m）
            public const float BastionCannonHeightM = 3.5f; // MDL-01 想定サイズ（3.2〜3.8m・S-05）
            public const float ArcEmitterHeightM = 2.2f;     // MDL-02 想定サイズ（2.0〜2.5m・S-05）
            public const float BuildSpotPadHeightM = 0.2f;   // ビルドスポット設置パッドのプレースホルダ厚み（S-05。対応する MDL 無し）

            // Game シーンの一時停止（S-17・gdd「操作仕様」Esc行）: Time.timeScale の意味付き定数
            // （0f/1f を各所へ直書きしない — 規約1。Systems/ は delta-time 駆動のため PausedTimeScale=0 で自動停止する）。
            public const float NormalTimeScale = 1f;
            public const float PausedTimeScale = 0f;

            // S-18: ビルドスポット/設置タワーへのマウスホバー時のハイライト+射程プレビュー円
            // （gdd「操作仕様」マウスホバー行・P-01「一手必中の配置」の判断補助・P-03 視認性向上。入力は非破壊）。
            // ホバー対象判定は Ui/HudPanel.FindClickedEmptySpot と同じ「カメラ投影+スクリーンpx距離」方式のため、
            // ピッキング半径は既存の Ui.BuildSpotClickPickRadiusPx をそのまま流用する（同一スポット群への
            // 同じ許容誤差が自然なため、重複定数を作らない実装判断）。
            public const float HoverHighlightRadiusM = 1.3f; // ハイライト輪の半径(m)。占有有無に関わらず共通（ビルドスポット足元を囲む目安）
            public const float HoverPreviewThicknessM = 0.03f; // ハイライト輪・射程プレビュー円の板厚(m)（地面すれすれの薄い円盤）
            public const float HoverPreviewYOffsetM = 0.02f;   // 地面/経路プレーンとのZファイティング回避用Yオフセット(m)

            // S-24: タワー発射モーション（gdd「モーション方式」節: 発射時のみ砲身/エミッタ部分〔子Transform＝
            // TowerView の visual〕を対象敵方向へ Quaternion.LookRotation で回転させ、発射瞬間に軽いリコイル
            // 〔スケールのショートパンチ演出〕をコードでトリガーする）。初期値+調整レンジ付き定数（演出強度の
            // チューニング対象のため Presentation 内でも他の高さ/ピッチ定数と同様レンジを明記する）。
            public const float TowerFireRecoilDurationSec = 0.18f; // リコイル演出の総持続時間（punch→復帰）（調整レンジ 0.1〜0.3秒）
            public const float TowerFireRecoilScalePunch = 0.18f;  // リコイル最大時のスケール増分（相対倍率。0.18=+18%）（調整レンジ 0.08〜0.3）

            // S-25: 敵撃破演出（gdd「モーション方式」節: 撃破時の演出はメッシュのスケルタルアニメではなく
            // パーティクルVFX＋SFX＋対象メッシュの非表示化〔またはディゾルブ〕で表現する）。本実装は
            // EnemyView のルート transform を等方スケールダウンして非表示化する形で表現する（対応する
            // 専用シェーダ演出は未発注のため「即非表示」ではなくスケールダウンを採用）。撃破確定〜GameObject
            // 破棄までの持続時間。初期値+調整レンジ付き定数（P-03「溶ける実感」の演出強度チューニング対象）。
            public const float EnemyDefeatShrinkDurationSec = 0.2f; // 撃破演出（スケールダウン）の総持続時間（調整レンジ 0.1〜0.3秒）

            // S-26: コア被弾ヒットフラッシュ+スケールパルス（gdd「コアクリスタル仕様」/「モーション方式」節。
            // 被弾時のみコードでヒットフラッシュ〔マテリアルカラー一時変更〕とスケールパルスを適用）。
            // 持続時間は既存の CoreHitFlashSec（本 story 以前から定義済み・値変更なし）を流用し、本定数は
            // パルス最大時のスケール増分のみを新設する（TowerFireRecoilScalePunch と同型・調整レンジ 0.08〜0.3）。
            public const float CoreHitScalePulse = 0.15f; // コア被弾時のスケールパルス最大増分（相対倍率。0.15=+15%）（調整レンジ 0.08〜0.3）
        }

        /// <summary>
        /// 3Dモデルが未取込/未生成（Resources.Load 失敗時）にのみ使う単色プレースホルダの色
        /// （art-bible.json palette から転記）。MDL-01〜05 は全て Integrate 済み（S-19）のため、
        /// 通常時は実モデル使用でこの色は未使用（想定外の縮退経路でのみ参照される）。
        /// </summary>
        public static class Placeholder
        {
            // art-bible.json palette: 敵主色（Marauder / Warbeast 本体） #973FA5
            public static readonly Color EnemyColor = new Color(0x97 / 255f, 0x3F / 255f, 0xA5 / 255f, 1f);
            // art-bible.json palette: コアエネルギー色（クリスタル発光） #ABFFFF
            public static readonly Color CoreColor = new Color(0xAB / 255f, 0xFF / 255f, 0xFF / 255f, 1f);
            // art-bible.json palette: プレイヤー主色（タワー本体） #0B6C58（S-05）
            public static readonly Color TowerColor = new Color(0x0B / 255f, 0x6C / 255f, 0x58 / 255f, 1f);
            // art-bible.json palette: ストラクチャー基調（石壇） #A9CBD5 — ビルドスポット設置パッド用に流用（S-05）
            public static readonly Color BuildSpotColor = new Color(0xA9 / 255f, 0xCB / 255f, 0xD5 / 255f, 1f);
            // design/assets.md IMG-01(tile-grass) 指定ベース色 #9DC03A — IMG-01(TileGrass)未取込/未生成時のフォールバック（S-21）
            public static readonly Color GroundColor = new Color(0x9D / 255f, 0xC0 / 255f, 0x3A / 255f, 1f);
            // design/assets.md IMG-02(tile-dirt-path) 指定ベース色 #A26836 — IMG-02(TileDirtPath)未取込/未生成時のフォールバック（S-21）
            public static readonly Color PathColor = new Color(0xA2 / 255f, 0x68 / 255f, 0x36 / 255f, 1f);
            // art-bible.json palette: タワーアクセント（窓・発光ガラス） #15ACC1 を流用（S-18）。
            // アルファはそれぞれ「薄く表示」の意図で低めに固定（ハイライト輪 > 射程プレビュー円の順で目立たせる）。
            public static readonly Color HoverHighlightColor = new Color(0x15 / 255f, 0xAC / 255f, 0xC1 / 255f, 0.45f);
            public static readonly Color HoverRangeColor = new Color(0x15 / 255f, 0xAC / 255f, 0xC1 / 255f, 0.18f);
        }

        /// <summary>
        /// UI 共通パラメータ（フォントサイズ・色・基準解像度 — tech-stack-unity.md 規約1/14）。
        /// Title/Menu/HUD/Result 共通。色は design/art-bible.json パレットのUI系役割（UIパネル背景/UIテキスト）に一致。
        /// </summary>
        public static class Ui
        {
            public const float ReferenceWidth = 1920f;    // CanvasScaler 基準解像度（横）
            public const float ReferenceHeight = 1080f;   // CanvasScaler 基準解像度（縦）

            public const int TitleFontSize = 96;          // シーンタイトル大見出し
            public const int SubtitleFontSize = 32;       // 通知・小見出し
            public const int BodyFontSize = 28;           // 本文・ボタン・説明文

            // CanvasScaler / テキストボックス共通レイアウト係数（S-02・CR-CODE iter1 #2。Title/Menu/HUD/Result 共通で使い回す）
            public const float CanvasScalerMatchWidthOrHeight = 0.5f; // 幅/高さのバランス（0=幅基準, 1=高さ基準）
            public const float TextBoxWidthFraction = 0.8f;           // テキストボックス幅 = ReferenceWidth * この係数
            public const float TextLineHeightFactor = 1.6f;           // テキストボックス高さ = fontSize * この係数

            // Title シーンの縦位置（アンカーY。0=下端, 1=上端 — S-02）
            public const float TitleTextAnchorY = 0.68f;
            public const float TitleRecoveryNoticeAnchorY = 0.86f;
            public const float TitlePromptAnchorY = 0.18f;

            // art-bible.json palette: UIパネル背景 #12262A / UIテキスト・前景 #EAF6F5
            public static readonly Color PanelBackground = new Color(0x12 / 255f, 0x26 / 255f, 0x2A / 255f, 0.92f);
            public static readonly Color TextPrimary = new Color(0xEA / 255f, 0xF6 / 255f, 0xF5 / 255f, 1f);
            // art-bible.json palette: タワーアクセント（窓・発光ガラス） #15ACC1
            public static readonly Color AccentTeal = new Color(0x15 / 255f, 0xAC / 255f, 0xC1 / 255f, 1f);
            // art-bible.json palette #9DC03A（暖色寄りの緑）: 復旧通知など「安全に戻った」トーンに使用
            public static readonly Color RecoveryNoticeColor = new Color(0x9D / 255f, 0xC0 / 255f, 0x3A / 255f, 1f);

            // Menu シーンの縦位置レイアウト（S-03）: 上から均等ステップで要素を積む簡易レイアウト。
            // 実績一覧/統計/アウトゲーム表示・設定パネルの精緻化は S-15/S-16（build phase）の責務。
            public const float MenuTopAnchorY = 0.95f;    // 先頭行（見出し）のアンカーY
            public const float MenuRowStepY = 0.058f;     // 行間ステップ（0=下端, 1=上端）
            public const float MenuButtonWidth = 420f;    // 「プレイ開始」「タイトルへ戻る」ボタン幅
            public const float MenuButtonHeight = 64f;    // 同ボタン高さ
            public const float MenuSliderWidth = 420f;    // 音量スライダー幅
            public const float MenuSliderHeight = 20f;    // 音量スライダー高さ
            public const float MenuDefaultVolume = 1f;    // 音量スライダー初期値（実効反映・永続化は S-16 の責務）
            public const float MenuAchievementLockedAlpha = 0.4f; // 実績未解放行の減光アルファ
            // 音量スライダーのラベル配置（CR-CODE iter1 #2。行高は TextLineHeightFactor を再利用）
            public const float MenuSliderLabelOffsetX = -16f; // ラベル右端からスライダー左端までのギャップ
            public const float MenuSliderLabelWidth = 160f;   // ラベルテキストボックス幅

            // S-15: Menu アウトゲームパネル拡張（実績進捗バー・UPG-01〜03 購入UI）。
            // 既存 MenuRowStepY（0.058）のままだと UPG購入3行を追加した場合に末尾（タイトルへ戻る）が
            // 画面外（アンカーY<0）へ落ちるため、本 story 専用の詰めたステップを追加する
            // （既存 MenuTopAnchorY/MenuRowStepY の値は変更しない — 共有ファイル規律。MenuScreen.cs は
            // このステップへ丸ごと切り替える。旧定数は他所で未使用のため残置は無害）。
            public const float MenuRowStepDenseY = 0.045f;      // S-15 適用後の行間ステップ
            public const float MenuUpgRowLabelOffsetX = -160f;  // UPG行: Lv表示テキストの中心からの左オフセット(px)
            public const float MenuUpgRowButtonOffsetX = 220f;  // UPG行: 購入ボタンの中心からの右オフセット(px)
            public const float MenuUpgButtonWidth = 200f;       // 購入ボタン幅
            public const float MenuUpgButtonHeight = 46f;       // 購入ボタン高さ
            public const int MenuUpgButtonFontSize = 22;        // 購入ボタンラベルのフォントサイズ（狭いボタンに収める）
            public const float MenuAchievementProgressBarWidth = 260f;   // ACH-03/04 進捗バー幅
            public const float MenuAchievementProgressBarHeight = 10f;  // ACH-03/04 進捗バー高さ
            public const float MenuAchievementProgressBarOffsetY = -22f; // 実績テキスト中心からの下オフセット(px)
            public const float MenuEssenceIconSize = 40f;       // essence アイコン(IMG-04)の表示サイズ(px)
            public const float MenuEssenceIconOffsetX = -300f;  // essence アイコンの中心からの左オフセット(px)

            // CR-CODE iter1 #1 対応（S-20）: SaveFailedNoticeText を RecoveryNoticeText と同じ行に
            // サブ行として重ねる（NextY() の行を追加すると Menu 末尾行が画面外へ落ちるため — 既存
            // MenuRowStepDenseY のままだと BackToTitleButton が既に anchorY≒0.05 まで詰まっている）。
            // CR-CODE iter2 #1/#4 対応: -34px は PlayButton 矩形(center 928.8px, box 896.8-960.8px @1080基準)
            // と SaveFailedNoticeText の文字ボックス(center 943.4px, box 921-965.8px)を ~40px 幅で重ねていた。
            // この密詰めレイアウトでは RecoveryNoticeText 行と PlayButton 行の間に空き（負のギャップ）が無く、
            // どんな高さのボックスを挟んでも一方と全く重ならない配置は数学的に不可能（行間隔48.6pxに対し
            // テキスト半分22.4px+ボタン半分32px=54.4px>48.6pxのため、テキスト-ボタン隣接行は必ず約5.8px
            // 重なる — これは RecoveryNoticeText と PlayButton 自身の間でも既に発生しており2回のCR-CODEで
            // 未指摘＝許容範囲の基準値）。offsetY=-4px に縮小し、PlayButtonとの重なりを基準値相当の
            // 約9.8pxまで縮小した（-34pxの約40pxから75%削減）。RecoveryNoticeTextとの重なりは実運用上
            // ほぼ起こらない（Recovered は Menu 初回表示で消費されるため SaveFailed と同時表示は
            // 正準フローでは発生しない — iteration1 対応コメント参照）。
            public const float MenuSaveFailedNoticeOffsetY = -4f; // RecoveryNoticeText 行からの下オフセット(px)

            // Game シーン HUD レイアウト（S-08）: 画面左上に資金/コアHP/ウェーブを固定表示（一瞥可読 — 役割宣言の鉄則）。
            public const float HudMarginXFraction = 0.02f; // 左端からのマージン（画面幅比）
            public const float HudTopAnchorY = 0.95f;       // 先頭行（資金）のアンカーY
            public const float HudRowStepY = 0.055f;        // 行間ステップ（0=下端, 1=上端）
            public const int HudFontSize = 34;              // 資金/コアHP/ウェーブ表示のフォントサイズ
            public const float HudTextBoxWidthFraction = 0.32f; // HUDテキストボックス幅（ReferenceWidth比）

            // タワー種別選択メニュー（Ui/TowerSelectPanel・S-08）: ビルドスポット左クリックで画面中央に開く2択パネル。
            public const float BuildSpotClickPickRadiusPx = 70f; // ビルドスポットのクリック判定半径（スクリーンpx）
            public const float TowerSelectPanelWidth = 560f;     // パネル全体の幅
            public const float TowerSelectPanelHeight = 300f;    // パネル全体の高さ
            public const float TowerSelectHeaderAnchorY = 0.68f; // パネル内見出しのアンカーY（パネルローカル）
            public const float TowerSelectButtonAnchorY = 0.38f; // パネル内ボタン列のアンカーY（パネルローカル）
            public const float TowerSelectButtonWidth = 220f;    // 種別選択ボタン1個の幅
            public const float TowerSelectButtonHeight = 170f;   // 種別選択ボタン1個の高さ
            public const float TowerSelectButtonSpacingX = 240f; // 2ボタンの中心間距離
            // 2ボタンを中心線から左右対称に配置するオフセット（CR-CODE iter1 #2。TowerSelectButtonSpacingX の半分）
            public const float TowerSelectButtonOffsetX = TowerSelectButtonSpacingX / 2f;
            public const float TowerSelectInsufficientAlpha = 0.35f; // 資金不足時のボタン減光アルファ
            // 見出しボックス幅 = パネル幅 * この係数（CR-CODE iter1 #2。TowerSelectPanel.cs 本文への 0.9f 直書きを解消）
            public const float TowerSelectHeaderWidthFraction = 0.9f;

            // タワーアップグレード/売却パネル（Ui/TowerActionPanel・S-23）: 設置済みタワー左クリックで画面中央に
            // 開く操作パネル（TowerSelectPanel と同型の ScreenSpaceCamera Canvas 子要素）。既存の
            // TowerSelect* 定数は変更せず、この新規パネル専用の定数として追加する（story acceptance の制約）。
            public const float TowerActionPanelWidth = 560f;      // パネル全体の幅
            public const float TowerActionPanelHeight = 340f;     // パネル全体の高さ（情報2行+ボタン列のぶん Select より高い）
            public const float TowerActionHeaderAnchorY = 0.86f;  // 見出し（種別名+Lv）のアンカーY（パネルローカル）
            public const float TowerActionCurrentInfoAnchorY = 0.68f; // 現在ダメージ行のアンカーY
            public const float TowerActionNextInfoAnchorY = 0.54f;    // 次Lvダメージ/強化費 or 「最大強化済み」行のアンカーY
            public const float TowerActionButtonAnchorY = 0.22f;      // 強化/売却ボタン列のアンカーY
            public const float TowerActionButtonWidth = 220f;         // ボタン1個の幅
            public const float TowerActionButtonHeight = 130f;        // ボタン1個の高さ
            public const float TowerActionButtonSpacingX = 240f;      // 2ボタンの中心間距離
            // 2ボタンを中心線から左右対称に配置するオフセット（TowerSelectButtonOffsetX と同型の導出）
            public const float TowerActionButtonOffsetX = TowerActionButtonSpacingX / 2f;
            public const float TowerActionInfoWidthFraction = 0.9f;   // 情報テキスト行の幅 = パネル幅 * この係数

            // Result シーンレイアウト（S-09）: Menu と同じ「先頭行→行間ステップ」パターンを踏襲する。
            // 表示要素が少なく1画面で結果を伝える必要があるため、行間は Menu よりやや広めに取る（一瞥可読）。
            public const float ResultTopAnchorY = 0.86f;   // 先頭行（勝敗見出し）のアンカーY
            public const float ResultRowStepY = 0.09f;     // 行間ステップ（0=下端, 1=上端）
            public const int ResultOutcomeFontSize = 64;   // 勝敗見出しのフォントサイズ

            // art-bible.json palette: 勝利トーン #9DC03A / 敗北トーン #C71A23 / ハイスコア更新の強調 #A26836
            public static readonly Color ResultWinColor = new Color(0x9D / 255f, 0xC0 / 255f, 0x3A / 255f, 1f);
            public static readonly Color ResultLossColor = new Color(0xC7 / 255f, 0x1A / 255f, 0x23 / 255f, 1f);
            public static readonly Color ResultHighScoreHighlightColor = new Color(0xA2 / 255f, 0x68 / 255f, 0x36 / 255f, 1f);

            // Game シーンの一時停止オーバーレイ（Ui/PausePanel・S-17）: 画面中央に「再開/設定/タイトルへ戻る」の
            // 縦積みパネルを表示する。Menu と同じ「先頭行→行間ステップ」パターンを踏襲。
            public const float PauseOverlayBackgroundAlpha = 0.75f; // 全画面を暗くする背景の不透明度（プレイ領域を隠すが完全な黒転はしない）
            public const float PauseHeaderAnchorY = 0.72f;          // 見出し（"一時停止" / "設定"）のアンカーY
            public const float PauseTopRowAnchorY = 0.56f;          // 先頭行（再開ボタン / BGM音量スライダー）のアンカーY
            public const float PauseRowStepY = 0.13f;               // 行間ステップ（0=下端, 1=上端）
            public const float PauseButtonWidth = 420f;             // 再開/設定/タイトルへ戻る/戻るボタン幅
            public const float PauseButtonHeight = 64f;             // 同高さ

            // S-17 CR-CODE iter1 #4: PauseCanvas と HudPanel の HudCanvas は同一条件（ScreenSpaceCamera・
            // 同一 worldCamera・planeDistance=1）のため Canvas 描画順が未規定になり得る。sortingOrder を
            // 明示して一時停止オーバーレイを常に最前面（TowerSelectPanel 等の HUD 子パネルより上）に固定する。
            public const int PauseCanvasSortingOrder = 10;
        }

        /// <summary>
        /// 資産参照キーの唯一の置き場（tech-stack-unity.md 規約5）。
        /// 動的ロードのパス/アドレスはここ経由。インスペクタ直参照(SerializeField)は各 Component で可。
        /// パスは design/assets.md の取込先（Assets/Generated/ 配下）に対応。
        /// </summary>
        public static class AssetKeys
        {
            // 3Dモデル（MDL-01〜05・取込先 Assets/Generated/models/）
            public const string ModelBastionCannon = "Generated/models/model-bastion-cannon";
            public const string ModelArcEmitter = "Generated/models/model-arc-emitter";
            public const string ModelMarauder = "Generated/models/model-marauder";
            public const string ModelWarbeast = "Generated/models/model-warbeast";
            public const string ModelCoreCrystal = "Generated/models/model-core-crystal";

            // UI テクスチャ/アイコン（IMG-01〜08・取込先 Assets/Generated/textures/）
            public const string TileGrass = "Generated/textures/tile-grass";
            public const string TileDirtPath = "Generated/textures/tile-dirt-path";
            public const string IconTowerSelect = "Generated/textures/icon-tower-select";
            public const string IconEssence = "Generated/textures/icon-essence";
            public const string IconAchievements = "Generated/textures/icon-achievements";
            public const string IconUpgrades = "Generated/textures/icon-upgrades";
            public const string IconEnemyIndicator = "Generated/textures/icon-enemy-indicator";
            public const string IconCoreHp = "Generated/textures/icon-core-hp";

            // 音声（SFX-01〜06 / BGM-01・取込先 Assets/Generated/audio/）
            public const string SfxTowerPlace = "Generated/audio/sfx-tower-place";
            public const string SfxTowerFire = "Generated/audio/sfx-tower-fire";
            public const string SfxEnemyDefeat = "Generated/audio/sfx-enemy-defeat";
            public const string SfxCoreHit = "Generated/audio/sfx-core-hit";
            public const string SfxWaveStart = "Generated/audio/sfx-wave-start";
            public const string SfxVictoryJingle = "Generated/audio/sfx-victory-jingle";
            public const string BgmMainTheme = "Generated/audio/bgm-main-theme";
        }

        /// <summary>シーン名の唯一の置き場（contract §11 必須シーン集合）。SceneManager.LoadScene 引数に使う。</summary>
        public static class Scenes
        {
            public const string Boot = "Boot";
            public const string Title = "Title";
            public const string Menu = "Menu";
            public const string Game = "Game";
            public const string Result = "Result";
        }
    }
}
