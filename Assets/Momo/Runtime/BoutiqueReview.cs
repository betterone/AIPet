using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Momo
{
    // Opt-in development player check; uses its own save directory (see MomoApp).
    public sealed class BoutiqueReview : MonoBehaviour
    {
        IEnumerator Start()
        {
            if (!MomoApp.BoutiqueReview) { Destroy(this); yield break; }
            Application.runInBackground = true;
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../artifacts/boutique"));
            Directory.CreateDirectory(directory);
            var app = GetComponent<MomoApp>();
            yield return null;
            if(app.CatCount!=0||app.cats.Count!=0||app.simulation.State.adopted)
                throw new Exception("A first-time household entered the room with a resident cat instead of starting adoption.");
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(375, 667), new Vector2Int(430, 932), new Vector2Int(768, 1024) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.8f);
                app.OpenBoutique();
                yield return new WaitForSecondsRealtime(.5f);
                CheckGrid(app);
                CheckUI();
                if(size.x==390)yield return CheckStableGridHeads(app);
                Physics.SyncTransforms();
                var point = (Vector2)app.boutique.boutiqueCamera.WorldToScreenPoint(app.boutique.cats[4].transform.position + Vector3.up * .4f);
                app.boutique.HandlePointer(point, false, false, true, false);
                if (app.boutique.IsZoomed) throw new Exception("Opening release accidentally selected a cat");
                app.boutique.HandlePointer(point, true, true, false, true);
                app.boutique.HandlePointer(point, false, false, true, false);
                if (app.boutique.IsZoomed) throw new Exception("UI-origin gesture leaked into stage");
                app.boutique.HandlePointer(point, true, true, false, false);
                app.boutique.HandlePointer(point, false, false, true, false);
                if (app.boutique.SelectedIndex != 4) throw new Exception("Grid tap did not select Yuki");
                app.boutique.ReturnToGrid(); yield return null;
                yield return Capture(directory, "grid-" + size.x);
                for (int i = 0; i < 9; i++)
                {
                    app.boutique.SelectCat(i);
                    yield return new WaitForSecondsRealtime(.4f);
                    // The review can run while the desktop Player is unfocused.
                    // A programmatic stage selection still needs the normal HUD refresh.
                    app.hud.Refresh();
                    CheckCat(app);
                    CheckUI();
                    var ui=FindFirstObjectByType<BoutiqueHud>();
                    var profileButton=Array.Find(ui.GetComponentsInChildren<Button>(true), b=>b.name=="档案");
                    if(profileButton==null)throw new Exception("Cat profile button is missing");
                    profileButton.onClick.Invoke();
                    yield return null;
                    if(!ui.ProfileOpen || ui.ProfileIndex!=i)
                    {
                        var page=ui.transform.Find("Safe area/Meet one cat");
                        var shade=page.Find("Cat profile shade");
                        throw new Exception("Cat profile did not open for "+i+
                            " (stage="+app.boutique.gameObject.activeInHierarchy+
                            ", ui="+ui.gameObject.activeInHierarchy+
                            ", detail="+page.gameObject.activeInHierarchy+
                            ", shadeSelf="+shade.gameObject.activeSelf+
                            ", shadeVisible="+shade.gameObject.activeInHierarchy+
                            ", selection="+app.boutique.SelectedIndex+")");
                    }
                    CheckProfile(ui, Momo.Core.BreedArchetypes.Get(i));
                    CheckUI();
                    if(i==4 && size.x==390)yield return Capture(directory,"profile-4-390");
                    var returnButton=Array.Find(ui.GetComponentsInChildren<Button>(true), b=>b.name=="继续认识它");
                    if(returnButton==null)throw new Exception("Cat profile return button is missing");
                    returnButton.onClick.Invoke();
                    if(ui.ProfileOpen || app.boutique.SelectedIndex!=i)throw new Exception("Cat profile did not return to selected cat");
                    if (i == 4 || size.x == 390) yield return Capture(directory, "cat-" + i + "-" + size.x);
                    app.boutique.PetSelected(); app.boutique.StretchSelected();
                    yield return new WaitForSecondsRealtime(.7f);
                    CheckCat(app);
                }
                app.boutique.ReturnToGrid();
                yield return null;
                CheckGrid(app);
                if(size.x==390)yield return CheckStableGridHeads(app);
                Debug.Log("BOUTIQUE REVIEW PASSED: " + Screen.width + "x" + Screen.height + ", grid + 9 cats + pet/stretch + UI bounds");
            }
            app.boutique.SelectCat(4); yield return null;
            app.boutique.AdoptSelected("Yuki"); yield return null;
            if (app.CatCount!=1||app.cats.Count!=1||!app.simulation.State.adopted || app.simulation.State.breedIndex != 4 || app.SaveFailed || app.boutique.gameObject.activeSelf || !app.room.cameraView.enabled)
                throw new Exception("Boutique adoption did not save and return to room");
            if (Mathf.Abs(app.simulation.State.pet.z-2.2f)>.01f || Mathf.Abs(app.cat.transform.position.z-2.2f)>.01f)
                throw new Exception("First adoption did not place the cat in view");
            Screen.SetResolution(390,844,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.35f);
            app.window.Approach();
            for(int i=0;i<120;i++)app.window.Tick(1f/60,false);
            if(!app.window.CanInteract)throw new Exception("Room did not finish opening after adoption");
            app.FocusNewCatIfReady();
            for(int i=0;i<30;i++)app.window.Tick(1f/60,false);
            app.cat.Tick(app.simulation.State,1f/60);
            app.hud.Refresh();
            Canvas.ForceUpdateCanvases();
            foreach(var label in app.hud.GetComponentsInChildren<TMP_Text>())
                if(label.isTextTruncated)throw new Exception("Room text truncated after adoption: "+label.text);
            yield return Capture(directory,"home-after-adoption-390");
            app.OpenBoutique(); yield return null;
            if (app.boutique.IsZoomed) throw new Exception("Boutique reopen must show grid");
            Debug.Log("BOUTIQUE REVIEW COMPLETE: saved adoption, room camera restored, reopen starts on grid. Isolated review save.");
            string coverage = Application.isBatchMode
                ? "Batch mode: 36 cat previews and profile open/return, UI containment at the Player's actual 640x480 resolution, grid return, saved adoption, room camera and reopen passed. Screen.SetResolution and screenshots are unavailable in batch mode.\n"
                : "4 viewport sizes, 36 cat previews and profile open/return, animated bounds, UI containment, grid return, saved adoption, room camera and reopen passed.\n";
            File.WriteAllText(Path.Combine(directory, "PASS.txt"), coverage);
            Application.Quit();
        }
        static IEnumerator Capture(string directory, string name)
        {
            // Batch mode has no presentable backbuffer; keep the behavioral and
            // layout checks running there without asking Unity for a screenshot.
            if (Application.isBatchMode) yield break;
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            if (image == null) throw new Exception("Could not capture Boutique frame: " + name);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            Destroy(image);
        }
        static void CheckCat(MomoApp app)
        {
            var camera = app.boutique.boutiqueCamera;
            if (camera.pixelRect.height < Screen.height * .30f)
                throw new Exception("Cat stage squeezed below 30% of the screen");
            foreach (var renderer in app.boutique.cats[app.boutique.SelectedIndex].GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (p.z <= camera.nearClipPlane || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1)
                        throw new Exception("Cat clipped: " + renderer.name + " at " + p);
                }
            }
        }
        static void CheckGrid(MomoApp app)
        {
            for (int i = 0; i < 9; i++)
            {
                var p = app.boutique.GetCubbyScreenPosition(i);
                if (!app.boutique.boutiqueCamera.pixelRect.Contains(p)) throw new Exception("Grid nameplate clipped: " + i);
                if (!app.boutique.cats[i].gameObject.activeInHierarchy) throw new Exception("Missing grid cat: " + i);
            }
        }
        static Transform Head(CatView cat)
        {
            foreach(var part in cat.GetComponentsInChildren<Transform>())
                if(part.name=="Head")return part;
            throw new Exception("Grid cat head not found");
        }
        static void CheckProfile(BoutiqueHud ui, Momo.Core.CatBreedProfile profile)
        {
            bool hasName=false,hasBreed=false,hasAppearance=false,hasPersonality=false;
            int filledTraits=0;
            foreach(var label in ui.GetComponentsInChildren<TMP_Text>())
            {
                if(label.name=="Profile name")hasName=label.text==profile.defaultName;
                if(label.name=="Profile breed")hasBreed=label.text.Contains(profile.breedName);
                if(label.name=="Profile appearance")hasAppearance=!string.IsNullOrWhiteSpace(label.text);
                if(label.name=="Profile summary")hasPersonality=!string.IsNullOrWhiteSpace(label.text);
                if(label.name=="Trait level" && !string.IsNullOrWhiteSpace(label.text))filledTraits++;
            }
            if(!hasName || !hasBreed || !hasAppearance || !hasPersonality || filledTraits!=4)
                throw new Exception("Cat profile content incomplete for "+profile.defaultName+
                    " (name="+hasName+", breed="+hasBreed+", appearance="+hasAppearance+
                    ", personality="+hasPersonality+", levels="+filledTraits+")");
        }
        static IEnumerator CheckStableGridHeads(MomoApp app)
        {
            float[] heights=new float[9];
            Quaternion[] rotations=new Quaternion[9];
            for(int i=0;i<9;i++)
            {
                var head=Head(app.boutique.cats[i]);
                heights[i]=head.position.y;
                rotations[i]=head.localRotation;
            }
            yield return new WaitForSecondsRealtime(1.4f);
            for(int i=0;i<9;i++)
            {
                var head=Head(app.boutique.cats[i]);
                if(Mathf.Abs(head.position.y-heights[i])>.005f || Quaternion.Angle(head.localRotation,rotations[i])>1f)
                    throw new Exception("Grid cat head did not stay still: "+i);
            }
        }
        static void CheckUI()
        {
            var ui = FindFirstObjectByType<BoutiqueHud>();
            foreach (var graphic in ui.GetComponentsInChildren<Graphic>())
            {
                Vector3[] corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners);
                foreach (var point in corners)
                    if (point.x < -.1f || point.x > Screen.width + .1f || point.y < -.1f || point.y > Screen.height + .1f)
                        throw new Exception("UI outside screen: " + graphic.name + " at " + point);
                if (graphic is TMP_Text text && text.isTextTruncated)
                    throw new Exception("Text truncated: " + text.name + " / " + text.text);
            }
        }
    }
}
