using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Momo.Editor
{
    public static class UiFontImporter
    {
        const string Source = "Assets/Momo/Resources/Fonts/NotoSansCJKsc-Regular.otf";
        const string Target = "Assets/Momo/Resources/Fonts/MomoUi.asset";

        public static void Prepare()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
                throw new System.Exception("TextMeshPro Essential Resources are missing.");
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Target) != null) return;
            var source = AssetDatabase.LoadAssetAtPath<Font>(Source);
            if (source == null) throw new System.Exception("Bundled Noto Sans CJK font is missing.");
            var font = TMP_FontAsset.CreateFontAsset(source, 72, 8,
                GlyphRenderMode.SDFAA, 2048, 2048);
            if (font == null) throw new System.Exception("Unable to create Momo UI font asset.");
            font.name = "MomoUi";
            var material = font.material;
            var atlas = font.atlasTexture;
            AssetDatabase.CreateAsset(font, Target);
            material.name = "MomoUi SDF";
            atlas.name = "MomoUi Atlas";
            AssetDatabase.AddObjectToAsset(material, font);
            AssetDatabase.AddObjectToAsset(atlas, font);
            AssetDatabase.SaveAssets();
            Debug.Log("Momo UI distance-field font prepared.");
        }
    }
}
