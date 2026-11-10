using UnityEditor;
using UnityEngine;

namespace Momo.Editor
{
    public sealed class CompanionHandImporter : AssetPostprocessor
    {
        const string Folder="Assets/Momo/Resources/CompanionHand/";
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith("Assets/Momo/Models/CompanionHand/"))return;
            var i=(ModelImporter)assetImporter;i.animationType=ModelImporterAnimationType.Generic;i.importAnimation=false;
            i.importCameras=false;i.importLights=false;i.materialImportMode=ModelImporterMaterialImportMode.None;
            i.skinWeights=ModelImporterSkinWeights.Standard;i.isReadable=true;
        }
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Folder))return;
            var i=(TextureImporter)assetImporter;i.maxTextureSize=1024;i.mipmapEnabled=true;i.isReadable=false;
            i.textureCompression=TextureImporterCompression.CompressedHQ;
            var ios=i.GetPlatformTextureSettings("iPhone");ios.name="iPhone";ios.overridden=true;ios.maxTextureSize=1024;ios.format=TextureImporterFormat.ASTC_6x6;i.SetPlatformTextureSettings(ios);
        }
    }
}
