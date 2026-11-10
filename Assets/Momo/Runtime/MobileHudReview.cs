using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;

namespace Momo
{
    // Desktop Player review at phone logical sizes, using a temporary save.
    public sealed class MobileHudReview : MonoBehaviour
    {
        void Awake() { Application.runInBackground = true;Application.logMessageReceived+=StopFailedReview; }
        void OnDestroy(){Application.logMessageReceived-=StopFailedReview;}
        void StopFailedReview(string message,string stack,LogType kind){if(kind==LogType.Exception)Application.Quit(1);}

        IEnumerator Start()
        {
            if (!MomoApp.MobileHudReviewMode) { Destroy(this); yield break; }
            var app = GetComponent<MomoApp>();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../../../artifacts/mobile-hud"));
            Directory.CreateDirectory(directory);
            File.Delete(Path.Combine(directory, "PASS.txt"));
            yield return null;
            app.AdoptBreed("Momo",0);
            app.simulation.State.pet = new Momo.Core.Point(0,2.2f);
            app.simulation.State.target = app.simulation.State.pet;
            app.cat.transform.position = new Vector3(0,0,2.2f);
            app.cat.transform.rotation = Quaternion.Euler(0,180,0);
            app.enabled = false;
            app.window.Approach();
            for (int i = 0; i < 120; i++) app.window.Tick(1f / 60, false);
            app.window.Find(app.cat.transform.position);for(int i=0;i<40;i++)app.window.Tick(1f/60,false);
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(320, 700),new Vector2Int(430,932) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.35f);
                app.hud.Refresh();
                yield return Capture(directory, "room-" + size.x);
                Check(app.hud, size);
                app.hud.ToggleSettings();
                app.hud.Refresh();
                yield return Capture(directory, "settings-" + size.x);
                Check(app.hud, size);
                app.hud.ClosePanel();
                app.hud.ViewSettings();app.hud.Refresh();yield return Capture(directory,"view-"+size.x);Check(app.hud,size);app.hud.ClosePanel();
                app.hud.PetStatus();app.hud.Refresh();yield return Capture(directory,"status-"+size.x);Check(app.hud,size);app.hud.ClosePanel();
                app.hud.Journal();app.hud.Refresh();yield return Capture(directory,"journal-"+size.x);Check(app.hud,size);app.hud.ClosePanel();
                app.hud.PlayGuide();
                app.hud.Refresh();
                yield return Capture(directory, "play-" + size.x);
                Check(app.hud, size);
                app.hud.ClosePanel();
            }
            CheckJoystick(app);
            if (!UiFont.Get().TryAddCharacters("遇见你的猫 萌宠小店 橘色虎斑 英短蓝猫", out string missing)
                || !string.IsNullOrEmpty(missing))
                throw new Exception("Chinese characters are missing from the bundled font: " + missing);
            app.PetCompanion();
            app.RollBallForPlay();
            app.CallCompanion();
            Screen.SetResolution(390, 844, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.35f);
            app.StartWandPlay();
            for(int i=0;i<30;i++)app.window.Tick(1f/60,false);
            if(!app.WandActive || !app.room.feather.gameObject.activeSelf)
                throw new Exception("Feather wand did not appear");
            Vector2 start=new Vector2(Screen.width*.5f, Screen.height*.52f);
            Vector2 end=start+new Vector2(70,35);
            app.HandlePointer(start,true,true,false,false);
            app.HandlePointer(end,false,true,false,false);
            app.hud.Refresh();
            yield return Capture(directory,"wand-390");
            app.HandlePointer(end,false,false,true,false);
            if(app.WandActive || app.room.feather.gameObject.activeSelf || app.PlayStepsCompleted!=4 || app.SaveFailed || app.simulation.State.wandPreference<=0)
                throw new Exception("Four guided interactions did not update real pet state or close the wand");
            Physics.SyncTransforms();
            Vector2 petPoint=app.room.cameraView.WorldToScreenPoint(app.cat.transform.position+Vector3.up*.55f);
            app.HandlePointer(petPoint,true,true,false,false);
            yield return new WaitForSecondsRealtime(.4f);
            Vector2 strokeEnd=petPoint+new Vector2(0,38);
            app.HandlePointer(strokeEnd,false,true,false,false);
            for(int i=0;i<20;i++)app.cat.Tick(app.simulation.State,1f/60);
            yield return Capture(directory,"stroke-390");
            app.HandlePointer(strokeEnd,false,false,true,false);
            if(app.LastGesture==null || !app.LastGesture.Contains("cat / stroke"))
                throw new Exception("Continuous pet stroke was not recognized");
            app.simulation.State.activity=Momo.Core.Activity.Idle;
            app.simulation.State.energy=75;
            if(!app.TryInviteFromCat(Time.unscaledTime+46f) || app.simulation.State.events[app.simulation.State.events.Count-1].kind!="invitation")
                throw new Exception("Cat did not initiate its own play moment");
            app.room.UpdateWorld(app.simulation.State,.033f);
            yield return Capture(directory,"invitation-390");
            File.WriteAllText(Path.Combine(directory, "PASS.txt"),
                "HUD, settings, view controls, pet status, journal and four-action play sheet rendered at 390x844, 320x700 and 430x932 with the bundled font and no truncated/out-of-screen text. The analogue joystick's real UI drag moved the thumb and observer, then stopped on release. Wand drag, continuous pet stroke, and cat-initiated play updated pet state. Physical iPhone rendering still requires device confirmation.\n");
            Debug.Log("MOBILE HUD REVIEW COMPLETE");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--hud-interactive")>=0)
            {app.enabled=true;app.window.Find(app.cat.transform.position);yield break;}
            Application.Quit();
        }

        static void Check(MomoHud hud, Vector2Int size)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var button in hud.GetComponentsInChildren<UnityEngine.UI.Button>(false))
            {
                if(!button.IsInteractable())continue;
                if(hud.PanelOpen){bool inModal=false;for(var t=button.transform;t!=null;t=t.parent)if(t.name=="Panel shade")inModal=true;if(!inModal)continue;}
                var rect=(RectTransform)button.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point},hits);
                if(!hits.Exists(h=>h.gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button))throw new Exception("Unreachable UI button: "+button.name+" / "+point+" / hits "+string.Join(",",hits.ConvertAll(h=>h.gameObject.name)));
            }
            foreach (var text in hud.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (string.IsNullOrEmpty(text.text)) continue;
                if (text.font != UiFont.Get()) throw new Exception("UI text did not use the bundled font: " + text.name);
                if (text.isTextTruncated) throw new Exception("UI text truncated at " + size + ": " + text.text);
                var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                if (corners[0].x < -1 || corners[2].x > Screen.width + 1 ||
                    corners[0].y < -1 || corners[2].y > Screen.height + 1)
                    throw new Exception("UI text outside screen at " + size + ": " + text.name);
            }
        }

        static void CheckJoystick(MomoApp app)
        {
            var pad=app.hud.Walk;var rect=(RectTransform)pad.transform;
            Canvas.ForceUpdateCanvases();app.window.ReturnToEntry();
            Vector2 centre=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            float screenRadius=pad.Radius*app.hud.GetComponent<Canvas>().scaleFactor;
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {pointerId=74,position=centre+Vector2.up*screenRadius};
            Vector3 before=app.window.ViewerPosition;pad.OnPointerDown(data);
            if(pad.Floating){data.position+=Vector2.up*screenRadius;pad.OnDrag(data);}
            Vector2 thumbCentre=pad.Thumb.parent==pad.FloatingDisc?Vector2.zero:new Vector2(rect.rect.width*.5f,-rect.rect.height*.5f);
            if(!pad.Moving||pad.Thumb.anchoredPosition.y<=thumbCentre.y)throw new Exception("Joystick drag did not move the thumb");
            app.window.Tick(.1f,false);
            if(Vector3.Distance(before,app.window.ViewerPosition)<WindowView.JoystickWalkSpeed*.09f)
                throw new Exception("Joystick drag did not move the observer");
            pad.OnPointerUp(data);Vector3 stopped=app.window.ViewerPosition;app.window.Tick(.1f,false);
            if(pad.Moving||Vector3.Distance(stopped,app.window.ViewerPosition)>.001f)
                throw new Exception("Joystick release did not stop movement");
        }

        static IEnumerator Capture(string directory, string name)
        {
            yield return new WaitForSecondsRealtime(.15f);
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            Destroy(image);
        }
    }
}
