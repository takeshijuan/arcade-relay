# 取込先: AI生成資産（GLB/テクスチャ/OGG）を Unity にインポートさせる置き場。
# raw と MANIFEST は _generated/（Assets 外）に残す（provenance の正本・contract §6/§11）。
#
# 実取込先は Assets/Resources/Generated/（このディレクトリではない）。
# GameConfig.AssetKeys の値（例: "Generated/models/model-bastion-cannon"）は Resources.Load(key) の
# 引数としてそのまま使う設計（tech-stack-unity.md 規約5「動的ロードのパス/アドレスは AssetKeys 経由」）
# のため、実行時ビルドにも同梱される Resources フォルダ配下に置く必要がある（この Assets/Generated/
# フォルダ単体では Resources.Load から解決できない）。詳細は game/Assets/Scripts/Components/
# GeneratedModelFactory.cs・AudioCuePlayer.cs・Editor/ForgeAssetIntegration.cs のコメントを参照。
