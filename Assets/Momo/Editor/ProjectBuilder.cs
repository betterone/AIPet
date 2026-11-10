using System.IO;
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
            Prepare();Build(BuildTarget.WebGL,"Builds/WebGL");
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
        static void Build(BuildTarget target,string path)
        {
            WindowViewChecks.Run();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Momo/Scenes/PetHome.unity"},locationPathName=path,target=target,options=BuildOptions.Development});
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
