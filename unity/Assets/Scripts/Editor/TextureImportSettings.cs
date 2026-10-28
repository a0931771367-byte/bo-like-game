using UnityEditor;
using UnityEngine;

namespace BoLike.Editor
{
    /// <summary>
    /// Import settings for the game's art, applied automatically so the repo needs no hand-edited .meta files:
    /// sprites at 100 px per unit, mipmapped, trilinear, high-quality compression (ASTC 4x4 on iOS), full-rect mesh (needed for tiling), with the pivot at the
    /// feet for characters, the centre for icons and UI, and the top-left for backgrounds.
    /// Matches departments/art/unity-art-import-spec.md.
    /// </summary>
    public class TextureImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/Resources/")) return;
            bool sprites = path.StartsWith("Assets/Resources/Sprites/");
            bool icons = path.StartsWith("Assets/Resources/Icons/");
            bool ui = path.StartsWith("Assets/Resources/UI/");
            bool bg = path.StartsWith("Assets/Resources/Bg/");
            if (!(sprites || icons || ui || bg)) return;

            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100;
            // Mipmaps + trilinear keep edges smooth when the editor Game view (or a small phone) shows art below 1:1.
            imp.mipmapEnabled = true;
            imp.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Trilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = bg ? 4096 : 1024;
            // High-quality compression: ASTC 4x4 on iOS is visually lossless for this art; characters' edges stay clean.
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.crunchedCompression = false;
            var ios = imp.GetPlatformTextureSettings("iPhone");
            ios.overridden = true;
            ios.format = TextureImporterFormat.ASTC_4x4;
            ios.maxTextureSize = imp.maxTextureSize;
            ios.compressionQuality = 100;
            imp.SetPlatformTextureSettings(ios);

            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)(sprites ? SpriteAlignment.BottomCenter : bg ? SpriteAlignment.TopLeft : SpriteAlignment.Center);
            s.spriteGenerateFallbackPhysicsShape = false;
            // 9-slice frames from the art department use a 56 px border on every side.
            if (ui && System.IO.Path.GetFileName(path).StartsWith("card_")) s.spriteBorder = new Vector4(56, 56, 56, 56);
            imp.SetTextureSettings(s);
        }
    }
}
