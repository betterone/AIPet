using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Xml.Linq;
using UnityEditor.iOS.Xcode;

namespace Momo.Editor
{
    public static class ProjectBuilder
    {
        [MenuItem("Momo/Prepare Project")]
        public static void Prepare()
        {
            UiFontImporter.Prepare();
            ImportedCatImporter.Prepare();
            CompanionHandBaker.Prepare();
            PrepareCatAudio();
            Directory.CreateDirectory("Assets/Momo/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Momo Entry").AddComponent<MomoApp>();
            EditorSceneManager.SaveScene(scene,"Assets/Momo/Scenes/PetHome.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Momo/Scenes/PetHome.unity",true)};
            PlayerSettings.companyName="Momo Studio";PlayerSettings.productName="Momo";PlayerSettings.bundleVersion="0.3.0";PlayerSettings.iOS.buildNumber="18";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS,"com.momo.aipet.prototype");
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.iOS.targetOSVersionString="16.0";
            PlayerSettings.iOS.cameraUsageDescription="通过相机在本机感知小范围移动，让你走近虚拟猫咪；不拍照、不保存或上传画面。";
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=1000;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=false;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.stripEngineCode=false;
            // Runtime-created materials must survive player shader stripping.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
            if(shaders!=null)
            {
                var standard=Shader.Find("Standard");bool found=false;
                for(int i=0;i<shaders.arraySize;i++)if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==standard)found=true;
                if(!found){int i=shaders.arraySize;shaders.InsertArrayElementAtIndex(i);shaders.GetArrayElementAtIndex(i).objectReferenceValue=standard;graphics.ApplyModifiedProperties();}
            }
            AssetDatabase.SaveAssets();Debug.Log("Momo project prepared.");
        }
        static void PrepareCatAudio()
        {
            // Short, frequently played 3D calls should be ready without decode latency.
            foreach(var name in new[]{"Mikan","Earl","Chai","Oreo","Yuki"})
            {
                var importer=AssetImporter.GetAtPath("Assets/Momo/Resources/CatAudio/Meow"+name+".mp3") as AudioImporter;
                if(importer==null)throw new System.Exception("Missing regular meow: "+name);
                var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData=true;
                importer.defaultSampleSettings=settings;
                importer.forceToMono=true;
                importer.SaveAndReimport();
            }
        }
        [MenuItem("Momo/Build macOS Prototype")]
        public static void BuildMac(){Prepare();Build(BuildTarget.StandaloneOSX,"Builds/macOS/Momo.app");}
        [MenuItem("Momo/Build WebGL Prototype")]
        public static void BuildWebGL()
        {
            Prepare();
            // EdgeOne rejects individual files above 25 MiB. Ship a production
            // WebGL player with Brotli and omit the browser's pending cat assets.
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback=true;
            ReimportWebGlShowcaseTextures();
            var stashed=StageWebGlPendingCatAssets();
            const string webGlOutput="Builds/WebGL";
            // Unity preserves obsolete files when a build changes compression mode.
            // A stale loader would keep requesting the old, oversized data package.
            if(Directory.Exists(webGlOutput))Directory.Delete(webGlOutput,true);
            try { Build(BuildTarget.WebGL,webGlOutput,BuildOptions.None); }
            finally { RestoreWebGlPendingCatAssets(stashed); }
        }

        static readonly string[] WebGlPendingCatFolders =
        {
            "Assets/Momo/Resources/ImportedCat",
            "Assets/Momo/Resources/ImportedCats/Ragdoll",
            "Assets/Momo/Resources/ImportedCats/White",
            "Assets/Momo/Resources/ImportedCats/Black"
        };
        const string WebGlStagingRoot="Assets/Momo/WebGLBuildStaging";
        static readonly string[] WebGlShowcaseTextureFolders =
        {
            "Assets/Momo/Resources/ImportedCats/BritishBlue",
            "Assets/Momo/Resources/ImportedCats/Calico"
        };

        static void ReimportWebGlShowcaseTextures()
        {
            foreach(string folder in WebGlShowcaseTextureFolders)
            foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{folder}))
            {
                var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if(importer==null)continue;
                var webgl=importer.GetPlatformTextureSettings("WebGL");
                if(webgl.overridden&&webgl.maxTextureSize==1024&&webgl.format==TextureImporterFormat.ETC2_RGBA8&&webgl.compressionQuality==50)continue;
                webgl.name="WebGL";webgl.overridden=true;webgl.maxTextureSize=1024;
                webgl.format=TextureImporterFormat.ETC2_RGBA8;webgl.compressionQuality=50;
                importer.SetPlatformTextureSettings(webgl);importer.SaveAndReimport();
            }
        }

        static List<string> StageWebGlPendingCatAssets()
        {
            if(!AssetDatabase.IsValidFolder(WebGlStagingRoot))AssetDatabase.CreateFolder("Assets/Momo","WebGLBuildStaging");
            var staged=new List<string>();
            foreach(string source in WebGlPendingCatFolders)
            {
                if(!AssetDatabase.IsValidFolder(source))continue;
                string destination=WebGlStagingRoot+"/"+Path.GetFileName(source);
                string error=AssetDatabase.MoveAsset(source,destination);
                if(!string.IsNullOrEmpty(error))throw new System.Exception("Could not exclude pending WebGL asset: "+error);
                staged.Add(source);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            return staged;
        }
        static void RestoreWebGlPendingCatAssets(List<string> staged)
        {
            for(int i=staged.Count-1;i>=0;i--)
            {
                string source=staged[i];
                string stagedPath=WebGlStagingRoot+"/"+Path.GetFileName(source);
                string error=AssetDatabase.MoveAsset(stagedPath,source);
                if(!string.IsNullOrEmpty(error))Debug.LogError("Could not restore pending WebGL asset: "+error);
            }
            AssetDatabase.DeleteAsset(WebGlStagingRoot);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        [MenuItem("Momo/Export iOS Device Xcode Project")]
        public static void BuildIOS()
        {
            Prepare();PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS,ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.sdkVersion=iOSSdkVersion.DeviceSDK;Build(BuildTarget.iOS,"Builds/iOS");
        }
        [MenuItem("Momo/Export iOS Simulator Xcode Project")]
        public static void BuildIOSSimulator()
        {
            Prepare();PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS,ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.sdkVersion=iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.simulatorSdkArchitecture=AppleMobileArchitectureSimulator.ARM64;
            Build(BuildTarget.iOS,"Builds/iOS-Simulator");
        }
        [MenuItem("Momo/Export Window View iOS Project")]
        public static void BuildIOSWindow()
        {
            Prepare();PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS,ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.sdkVersion=iOSSdkVersion.DeviceSDK;Build(BuildTarget.iOS,"Builds/iOS-Window");
        }
        static void Build(BuildTarget target,string path,BuildOptions options=BuildOptions.Development)
        {
            WindowViewChecks.Run();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Momo/Scenes/PetHome.unity"},locationPathName=path,target=target,options=options});
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Momo build failed: "+report.summary.result);
            if(target==BuildTarget.iOS)
            {
                string plistPath=Path.Combine(path,"Info.plist");var plist=XDocument.Load(plistPath);
                var dict=plist.Root.Element("dict");
                foreach(var key in dict.Elements("key"))
                    if(key.Value=="NSMotionUsageDescription"){key.NextNode.Remove();key.Remove();break;}
                dict.Add(new XElement("key","NSMotionUsageDescription"),new XElement("string","Turn your phone to look around your companion's room. Motion stays on this device."));
                // Mono emits an empty DTD subset as [] that Apple's plist reader rejects.
                plist.DocumentType?.Remove();
                plist.Save(plistPath);
                string pbxPath=PBXProject.GetPBXProjectPath(path);var pbx=new PBXProject();pbx.ReadFromFile(pbxPath);
                string unityTarget=pbx.GetUnityFrameworkTargetGuid();
                pbx.AddFrameworkToProject(unityTarget,"ARKit.framework",true);
                pbx.AddFrameworkToProject(unityTarget,"AVFoundation.framework",false);
                pbx.WriteToFile(pbxPath);
            }
            Debug.Log("Momo build succeeded: "+path);
        }
    }
}
