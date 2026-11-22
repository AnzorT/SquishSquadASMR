// Import settings for the pictures exported from the game: kept exactly as the browser drew them.
// No compression (it would change pixels), no mipmaps, no power-of-two resizing, up to 4096 px.
using UnityEditor;
using UnityEngine;

namespace PlushCrush.EditorTools
{
    sealed class UiTextureImport : AssetPostprocessor
    {
        static readonly string[] Folders =
        {
            "Assets/_Project/Resources/Screens/",
            "Assets/_Project/Art/",
            "Assets/_Project/Reference/",
        };

        void OnPreprocessTexture()
        {
            bool ours = false;
            foreach (var f in Folders) if (assetPath.StartsWith(f)) { ours = true; break; }
            if (!ours) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default; // UI Toolkit draws a Texture2D as a background
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.isReadable = false;
        }
    }
}
