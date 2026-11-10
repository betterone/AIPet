using TMPro;
using UnityEngine;

namespace Momo
{
    // One bundled face for Latin and Chinese. TMP's distance-field atlas remains
    // legible at phone sizes and avoids depending on an iOS-installed font.
    public static class UiFont
    {
        public static TMP_FontAsset Get()
        {
            var asset = Resources.Load<TMP_FontAsset>("Fonts/MomoUi");
            if (asset == null) throw new System.Exception("Bundled Momo UI font asset is missing.");
            return asset;
        }
    }
}
