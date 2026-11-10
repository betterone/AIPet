using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Momo.Editor
{
    public static class VisualStyleStudyBuilder
    {
        [MenuItem("Momo/Build Visual Style Study")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX,"Builds/VisualStyleStudy/MomoVisualStudy.app");
        }

        static void Build(BuildTarget target,string output)
        {
            // This sample has its own entry point and output. Do not replace PetHome/build settings.
            const string path = "Assets/Momo/Scenes/VisualStyleStudy.unity";
            ImportedCatImporter.Prepare();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Visual comparison — no pet save").AddComponent<VisualStyleStudy>();
            EditorSceneManager.SaveScene(scene, path);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { path }, target = target,
                locationPathName = output, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Visual study build failed: " + report.summary.result);
            Debug.Log("VISUAL STYLE STUDY BUILD SUCCEEDED");
        }
    }
}
