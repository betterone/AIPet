using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Momo.Core;

namespace Momo
{
    // Actual player/render checks, isolated from the user's save and appearance setting.
    public sealed class ImportedCatReview : MonoBehaviour
    {
        IEnumerator Start()
        {
            if (!MomoApp.ImportedReview) { Destroy(this); yield break; }
            Application.runInBackground = true;
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../artifacts/imported-cat/player"));
            Directory.CreateDirectory(directory);
            File.Delete(Path.Combine(directory, "PASS.txt"));
            var app = GetComponent<MomoApp>();
            yield return null;
            app.CloseBoutique(); app.hud.ClosePanel();
            app.cat.Build(BreedArchetypes.Get(app.simulation.State.breedIndex), true);
            yield return null;
            var imported = app.cat.Imported;
            if (imported == null || !imported.Ready) throw new Exception("Imported cat failed to instantiate.");
            if (imported.Skin.sharedMesh.GetIndexCount(0) / 3 > 45000) throw new Exception("Mobile face budget exceeded.");
            if (imported.Skin.bones.Length < 20) throw new Exception("Missing skeleton.");
            // Rendering mesh need not remain CPU-readable in shipping player; count uses index metadata.
            app.enabled = false;
            app.hud.gameObject.SetActive(false);
            Screen.SetResolution(1000, 900, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.5f);
            app.cat.transform.position = Vector3.zero;
            app.cat.transform.rotation = Quaternion.identity;
            var camera = app.room.cameraView;
            app.window.enabled = false;
            camera.enabled = true;
            camera.transform.position = new Vector3(1.7f, 1.35f, 3.1f);
            camera.transform.LookAt(new Vector3(0, .5f, 0)); camera.fieldOfView = 34;
            foreach (string pose in new[] { "Idle", "Walk", "Sit", "Sleep", "Eat", "Paw", "Groom", "Stretch" })
            {
                imported.PreviewPose(pose, .4f, .25f);
                yield return new WaitForEndOfFrame();
                yield return Capture(directory, pose);
                var baked = new Mesh(); imported.Skin.BakeMesh(baked);
                var worldBounds = new Bounds(imported.Skin.transform.TransformPoint(baked.bounds.center), Vector3.zero);
                for (int corner = 0; corner < 8; corner++)
                    worldBounds.Encapsulate(imported.Skin.transform.TransformPoint(baked.bounds.center + Vector3.Scale(baked.bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                if (worldBounds.size.y < .25f || worldBounds.size.y > 1.7f)
                    throw new Exception("Wrong runtime cat scale in " + pose + ": " + worldBounds.size);
                var onScreen = camera.WorldToViewportPoint(worldBounds.center);
                if (onScreen.z < 0 || onScreen.x < .05f || onScreen.x > .95f || onScreen.y < .05f || onScreen.y > .95f)
                    throw new Exception("Cat not framed in " + pose);
                foreach (var vertex in baked.vertices)
                    if (!float.IsFinite(vertex.x) || !float.IsFinite(vertex.y) || !float.IsFinite(vertex.z))
                        throw new Exception("Non-finite skinned vertex in " + pose);
                Debug.Log("IMPORTED POSE " + pose + " bounds=" + baked.bounds + " world=" + worldBounds);
                if (pose == "Idle")
                {
                    foreach (var t in imported.GetComponentsInChildren<Transform>())
                        if (t.name == "Head" || t.name == "Pelvis" || t.name == "TabbyMesh" || t.name == "Imported model scale" || t.name == "Imported tabby")
                            Debug.Log("IMPORTED TRANSFORM " + t.name + " position=" + t.position + " rotation=" + t.eulerAngles + " scale=" + t.lossyScale);
                }
                Destroy(baked);
            }
            // The simulation moves in 0.9-unit jumps every five simulated seconds.
            // Verify its rendered path has a steady physical speed across those ticks.
            app.cat.transform.position = Vector3.zero;
            var s = Simulation.New(DateTime.UtcNow.Ticks);
            s.activity = Activity.Walk; s.intent = Activity.Watch;
            s.route.Add(new Point(0, 4));
            var simulation = new Simulation(s);
            float minimumSpeed = float.MaxValue, maximumSpeed = 0;
            Transform paw = null;
            foreach (var bone in imported.GetComponentsInChildren<Transform>())
                if (bone.name == "ForePaw.L") paw = bone;
            if (paw == null) throw new Exception("Walk foot bone missing.");
            bool stanceSample = false;
            Vector3 stanceStart = Vector3.zero;
            float maximumStanceDrift = 0;
            for (int i = 0; i < 90; i++)
            {
                simulation.Advance(Simulation.Speed / 30);
                imported.Tick(s, 1f / 30);
                if (i >= 15 && i < 75)
                { minimumSpeed = Mathf.Min(minimumSpeed, imported.LastSpeed); maximumSpeed = Mathf.Max(maximumSpeed, imported.LastSpeed); }
                // Fore left has a quarter-cycle offset in the baked gait.
                float phase = Mathf.Repeat(imported.WalkPhase + .25f, 1);
                if (i >= 15 && i <= 26)
                    Debug.Log("GAIT FRAME " + i + " phase=" + phase.ToString("F3") +
                        " root=" + app.cat.transform.position + " paw=" + paw.position);
                if (phase > .1f && phase < .58f)
                {
                    if (!stanceSample) { stanceStart = paw.position; stanceSample = true; }
                    maximumStanceDrift = Mathf.Max(maximumStanceDrift,
                        Vector2.Distance(new Vector2(stanceStart.x, stanceStart.z), new Vector2(paw.position.x, paw.position.z)));
                }
                else stanceSample = false;
                camera.transform.position = app.cat.transform.position + new Vector3(1.7f, 1.35f, 3.1f);
                camera.transform.LookAt(app.cat.transform.position + new Vector3(0, .5f, 0));
                yield return Capture(directory, "walk-" + i.ToString("D3"));
            }
            Debug.Log("IMPORTED GAIT speed=" + minimumSpeed + ".." + maximumSpeed + " stanceDrift=" + maximumStanceDrift);
            if (app.cat.transform.position.z < 2.9f || imported.ActivePose != "Walk") throw new Exception("Walk did not follow simulation.");
            if (minimumSpeed < .75f || maximumSpeed / minimumSpeed > 1.18f)
                throw new Exception("Simulation ticks still cause visible walk speed pulses.");
            if (maximumStanceDrift > .075f)
                throw new Exception("Forepaw slides during stance: " + maximumStanceDrift);
            s.pet = new Point(0, app.cat.transform.position.z);
            s.route.Clear(); s.activity = Activity.Idle;
            for (int i = 0; i < 60; i++) imported.Tick(s, 1f / 30);
            if (imported.LastSpeed > .02f || imported.ActivePose != "Idle") throw new Exception("Stopped cat still walking.");
            imported.LookAt(camera.transform.position, 1); imported.React(); imported.Tick(s, .033f);
            yield return Capture(directory, "pet-look");
            // Appearance switch must leave the actual companion state intact.
            string before = JsonUtility.ToJson(app.simulation.State);
            app.cat.Build(BreedArchetypes.Get(app.simulation.State.breedIndex), false); yield return null;
            if (app.cat.Imported != null) throw new Exception("Original appearance restore failed.");
            app.cat.Build(BreedArchetypes.Get(app.simulation.State.breedIndex), true); yield return null;
            if (app.cat.Imported == null || before != JsonUtility.ToJson(app.simulation.State)) throw new Exception("Appearance changed pet state.");
            File.WriteAllText(Path.Combine(directory, "PASS.txt"), "Imported model/skin/clips, 8 rendered poses, finite deformation, displacement-driven walk, stop, pet/look, original-model restore and unchanged pet state passed. Visual naturalness and device performance are separate acceptance items.\n");
            Debug.Log("IMPORTED CAT REVIEW COMPLETE"); Application.Quit();
        }
        static IEnumerator Capture(string directory, string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG()); Destroy(image);
        }
    }
}
