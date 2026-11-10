using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Momo.Editor
{
    public sealed class CatBehaviorStudyImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath != "Assets/Momo/Resources/StudyCat/animal-cat.fbx") return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                int split = clip.name.LastIndexOf('|');
                if (split >= 0) clip.name = clip.name.Substring(split + 1);
                clip.loopTime = true;
                clip.wrapMode = WrapMode.Loop;
            }
            importer.clipAnimations = clips;
        }
    }

    public static class CatBehaviorStudyBuilder
    {
        const string ScenePath = "Assets/Momo/Scenes/CatBehaviorStudy.unity";
        const string OutputPath = "Builds/CatBehaviorStudy/MomoCatStudy.app";

        [MenuItem("Momo/Build Cat Behavior Study")]
        public static void BuildMac()
        {
            AssetDatabase.Refresh();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Momo/Resources/StudyCat/animal-cat.fbx");
            if (source == null) throw new Exception("Kenney cat FBX was not imported.");
            var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/Momo/Resources/StudyCat/animal-cat.fbx");
            foreach (var asset in clips) if (asset is AnimationClip clip) Debug.Log("CAT STUDY IMPORT CLIP " + clip.name + " legacy=" + clip.legacy + " length=" + clip.length);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Cat behavior study entry").AddComponent<CatBehaviorStudy>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Cat behavior study build failed: " + report.summary.result);
            Debug.Log("CAT STUDY BUILD SUCCEEDED " + OutputPath);
        }
    }
}
