using System;
using System.Collections;
using System.IO;
using Momo.Core;
using UnityEngine;

namespace Momo
{
    // Opt-in Development Player review; MomoApp selects a fresh isolated save.
    public sealed class NewCatsReview : MonoBehaviour
    {
        MomoApp app; string directory;
        readonly int[] breeds = { 1, 7, 4, 5, 6 };
        readonly string[] poses = { "Idle", "Walk", "Sit", "Sleep", "Eat", "Paw", "Groom", "Stretch" };
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../artifacts/new-cats/player"));
            Directory.CreateDirectory(directory);
            foreach (string file in new[] { "PASS.txt", "FAIL.txt" }) File.Delete(Path.Combine(directory, file));
            var run = Run();
            while (true)
            {
                object next;
                try { if (!run.MoveNext()) break; next = run.Current; }
                catch (Exception error) { File.WriteAllText(Path.Combine(directory, "FAIL.txt"), error.ToString()); Debug.LogException(error); Application.Quit(1); yield break; }
                yield return next;
            }
        }
        IEnumerator Run()
        {
            app = GetComponent<MomoApp>(); app.enabled = false;
            app.CloseBoutique(); app.AdoptBreed(BreedArchetypes.Get(breeds[0]).defaultName, breeds[0]);
            foreach (int breed in breeds)
            {
                Check(app.boutique.cats[breed].Imported != null, "Boutique lacks imported breed " + breed);
                if (breed != breeds[0]) app.AdoptBreed(BreedArchetypes.Get(breed).defaultName, breed);
            }
            Check(app.CatCount == 5, "Adoption lost residents");
            app.CloseBoutique(); app.hud.gameObject.SetActive(false);
            foreach (var actor in app.cats.Values) actor.gameObject.SetActive(false);
            var camera = app.room.cameraView; app.window.enabled = false;
            foreach (int breed in breeds)
            {
                CatState resident = null;
                foreach (var c in app.household.State.cats) if (c.life.breedIndex == breed) resident = c;
                Check(resident != null, "Missing adopted breed " + breed);
                var view = app.cats[resident.id]; view.gameObject.SetActive(true);
                view.transform.position = Vector3.zero; view.transform.rotation = Quaternion.identity;
                var imported = view.Imported;
                Check(imported != null && imported.Ready && imported.LoadedResourcePath == ImportedCatCatalog.ForBreed(breed), "Wrong home model " + breed);
                Check(imported.Skin.sharedMesh.GetIndexCount(0) / 3 <= 45000, "Face budget exceeded " + breed);
                Check(imported.Skin.bones.Length >= 20, "Missing rig " + breed);
                foreach (string pose in poses)
                {
                    for (int i = 0; i < 18; i++) imported.TickStudy(1f / 30, pose, pose == "Walk" ? .02f : 0, pose == "Walk" ? .6f : 0);
                    yield return null;
                    PlayerReviewCapture.Resize(app, 640, 800);
                    camera.transform.position = new Vector3(1.7f, 1.3f, 3.2f); camera.transform.LookAt(new Vector3(0, .5f, 0)); camera.fieldOfView = 37;
                    yield return Capture("breed-" + breed + "-" + pose);
                    var baked = new Mesh(); imported.Skin.BakeMesh(baked);
                    var worldBounds = new Bounds(imported.Skin.transform.TransformPoint(baked.bounds.center), Vector3.zero);
                    for (int c = 0; c < 8; c++) worldBounds.Encapsulate(imported.Skin.transform.TransformPoint(baked.bounds.center + Vector3.Scale(baked.bounds.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                    var size = worldBounds.size;
                    Debug.Log("NEW CAT POSE " + breed + " " + pose + " bounds=" + worldBounds);
                    Check(size.y > .2f && size.y < 1.8f && size.x < 2 && size.z < 3.0f, "Exploding skin in " + breed + " " + pose + ": " + size);
                    foreach (var v in baked.vertices) Check(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), "Nonfinite skin " + breed + " " + pose);
                    Destroy(baked);
                }
                // Rebuilding an existing saved resident also resolves the new breed appearance.
                string before = JsonUtility.ToJson(resident);
                view.Build(BreedArchetypes.Get(breed), resident.appearance == 1); yield return null;
                Check(view.Imported != null && view.Imported.LoadedResourcePath == ImportedCatCatalog.ForBreed(breed), "Saved resident did not resolve replacement");
                Check(before == JsonUtility.ToJson(resident), "Model replacement modified resident identity");
                view.gameObject.SetActive(false);
            }
            app.Save(); Check(!app.SaveFailed, "Save failed");
            var codec = new UnityCodec(); var loaded = new HouseholdStore(app.SavePath, codec, codec).Load();
            Check(loaded.cats.Count == 5, "Reload lost residents");
            File.WriteAllText(Path.Combine(directory, "PASS.txt"), "Five breed replacements loaded in Boutique and adopted home actors; 40 rendered poses; <=45k triangles, rigs, finite skin and bounded deformation; existing-resident rebuild preserves identity; five-cat save/reload passed. Desktop Player evidence. Device performance and subjective animation quality require separate review.\n");
            Debug.Log("NEW CATS REVIEW PASSED"); Application.Quit(0);
        }
        IEnumerator Capture(string name)
        {
            if (Application.isBatchMode) yield return null; else yield return new WaitForEndOfFrame();
            PlayerReviewCapture.Capture(app, Path.Combine(directory, name + ".png"));
        }
    }
}
