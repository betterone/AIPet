using UnityEditor;
using UnityEngine;

namespace Momo.Editor
{
    public sealed class ImportedCatImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Momo/Resources/ImportedCat/";
        const string BreedsFolder = "Assets/Momo/Resources/ImportedCats/";
        static bool IsCatAsset(string path) => path.StartsWith(Folder) || path.StartsWith(BreedsFolder);
        void OnPreprocessModel()
        {
            if (!IsCatAsset(assetPath) || !assetPath.EndsWith(".fbx")) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.isReadable = true; // Selected skin vertices drive bath bubble attachments.
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                int split = clip.name.LastIndexOf('|');
                if (split >= 0) clip.name = clip.name.Substring(split + 1);
                clip.loopTime = true; clip.wrapMode = WrapMode.Loop;
            }
            importer.clipAnimations = clips;
        }
        void OnPreprocessTexture()
        {
            if (!IsCatAsset(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048; importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.EndsWith("Normal.png")) importer.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("MetalRough.png")) importer.sRGBTexture = false;
            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.name = "iPhone"; ios.overridden = true; ios.maxTextureSize = 2048;
            ios.format = TextureImporterFormat.ASTC_6x6; ios.compressionQuality = 80;
            importer.SetPlatformTextureSettings(ios);
            // EdgeOne Pages limits a published file to 25 MiB. The web showcase
            // has two close-up cats, so 1K compressed maps preserve the silhouette
            // and coat while keeping its single WebGL data package deployable.
            var webgl = importer.GetPlatformTextureSettings("WebGL");
            webgl.name = "WebGL"; webgl.overridden = true; webgl.maxTextureSize = 1024;
            webgl.format = TextureImporterFormat.ETC2_RGBA8; webgl.compressionQuality = 50;
            importer.SetPlatformTextureSettings(webgl);
        }
        [MenuItem("Momo/Prepare Imported Cat")]
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            PrepareMaterial(Folder, "Tabby.fbx", "TabbyMaterial.mat");
            foreach (string breed in new[] { "BritishBlue", "Ragdoll", "White", "Black", "Calico" })
                PrepareMaterial(BreedsFolder + breed + "/", "Model.fbx", "Material.mat");
            AssetDatabase.SaveAssets();
            Debug.Log("IMPORTED CATS PREPARED: six models, legacy clips, shared fur materials, iOS ASTC textures.");
        }
        static void PrepareMaterial(string folder, string modelName, string materialName)
        {
            var importer=AssetImporter.GetAtPath(folder+modelName) as ModelImporter;
            if(importer!=null&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + modelName);
            if (model == null) throw new System.Exception("Processed cat model is missing: " + folder + modelName);
            string path = folder + materialName;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.shader = Shader.Find("Momo/Imported Cat Fur");
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "BaseColor.png");
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Normal.png"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale", .15f); material.SetFloat("_Metallic", 0); material.SetFloat("_Glossiness", .08f);
            EditorUtility.SetDirty(material);
            // Runtime assigns this shared material explicitly; no duplicated material instances.
        }
    }
}
