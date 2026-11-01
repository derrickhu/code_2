using UnityEditor;

namespace Liangshan.EditorTools
{
    /// <summary>Resources/Art 下的图一律按 Sprite 导入：不缩放到 2 的幂，1 个世界单位 = 图片长边。</summary>
    public sealed class ArtImporter : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = UnityEngine.SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
            ti.GetSourceTextureWidthAndHeight(out int w, out int h);
            if (w > 0) ti.spritePixelsPerUnit = System.Math.Max(w, h);
            ti.SetPlatformTextureSettings(MiniGame(w, h, assetPath.Contains("/Resources/Art/ui/")));
        }

        /// <summary>
        /// 不压缩时整套图 200MB，微信总包上限 30MB，手机内存也撑不住。
        /// 整屏背景用 8x8（画面平滑看不出块），角色小图用 6x6 并限到 512。
        /// 界面皮肤按原尺寸、4x4：九宫格边按图片像素算，缩图会让边框错位。
        /// </summary>
        static TextureImporterPlatformSettings MiniGame(int w, int h, bool skin)
        {
            if (skin)
                return new TextureImporterPlatformSettings
                {
                    name = "WeixinMiniGame",
                    overridden = true,
                    maxTextureSize = 1024,
                    format = TextureImporterFormat.ASTC_4x4,
                    textureCompression = TextureImporterCompression.Compressed,
                    compressionQuality = 50,
                };
            bool backdrop = (long)w * h >= 1000L * 1000L;
            return new TextureImporterPlatformSettings
            {
                name = "WeixinMiniGame",
                overridden = true,
                maxTextureSize = backdrop ? 2048 : 512,
                format = backdrop ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 50,
            };
        }
    }
}
