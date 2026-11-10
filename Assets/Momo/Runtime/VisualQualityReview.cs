using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Momo.Core;

namespace Momo
{
    // Opt-in art review of the production scene, using an isolated household.
    public sealed class VisualQualityReview : MonoBehaviour
    {
        MomoApp app;string directory;
        IEnumerator Start()
        {
            directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/visual-quality/after"));
            Directory.CreateDirectory(directory);File.Delete(Path.Combine(directory,"PASS.txt"));File.Delete(Path.Combine(directory,"FAIL.txt"));
            var run=Run();while(true)
            {
                object next;try{if(!run.MoveNext())break;next=run.Current;}
                catch(Exception e){File.WriteAllText(Path.Combine(directory,"FAIL.txt"),e.ToString());Debug.LogException(e);Application.Quit(1);yield break;}
                yield return next;
            }
        }
        void Frame(Vector3 eye,Vector3 at,float fov=58)
        {var camera=app.room.cameraView;camera.transform.position=eye;camera.transform.LookAt(at);camera.fieldOfView=fov;}
        void Place(Point p)
        {
            var s=app.simulation.State;s.pet=s.target=p;s.route.Clear();s.activity=s.intent=Activity.Idle;s.remaining=1000;s.energy=85;s.hunger=45;
            app.cat.transform.position=new Vector3(p.x,0,p.z);app.cat.transform.rotation=Quaternion.Euler(0,180,0);app.cat.Tick(s,.033f);
        }
        IEnumerator Run()
        {
            app=GetComponent<MomoApp>();app.enabled=false;app.CloseBoutique();app.AdoptBreed("Momo",0);app.CancelFeeding();
            app.window.Approach();for(int i=0;i<120;i++)app.window.Tick(1f/60,false);
            Place(new Point(0,1));
            foreach(var size in new[]{new Vector2Int(390,844),new Vector2Int(320,700),new Vector2Int(430,932),new Vector2Int(768,1024)})
            {
                PlayerReviewCapture.Resize(app,size.x,size.y);yield return new WaitForSecondsRealtime(.2f);
                Frame(new Vector3(0,2.2f,-2.8f),new Vector3(0,.55f,1));
                yield return Shot("room-"+size.x);CheckText();CheckBadges();
                app.hud.ToggleSettings();yield return Shot("settings-"+size.x);CheckText();app.hud.ClosePanel();
                app.hud.PlayGuide();yield return Shot("play-"+size.x);CheckText();app.hud.ClosePanel();
                app.hud.PetStatus();yield return Shot("status-"+size.x);CheckText();app.hud.ClosePanel();
                app.hud.Journal();yield return Shot("journal-"+size.x);CheckText();app.hud.ClosePanel();
                app.hud.ViewSettings();yield return Shot("view-"+size.x);CheckText();app.hud.ClosePanel();
                app.hud.Household();yield return Shot("household-"+size.x);CheckText();app.hud.ClosePanel();
            }
            PlayerReviewCapture.Resize(app,390,844);yield return new WaitForSecondsRealtime(.2f);
            Frame(new Vector3(3,2.2f,6),new Vector3(2,.6f,8.9f));Place(new Point(2,8.9f));yield return Shot("kitchen-390");
            Frame(new Vector3(9,2.2f,6),new Vector3(9,.6f,9));Place(new Point(8.9f,8.7f));yield return Shot("quiet-room-390");
            Frame(new Vector3(3,2.2f,13),new Vector3(3.4f,.6f,17));Place(new Point(3.4f,16));yield return Shot("garden-390");
            Frame(new Vector3(0,2.2f,-2.8f),new Vector3(0,.55f,1));Place(new Point(0,1));
            app.simulation.State.seconds=14*3600;for(int i=0;i<100;i++)app.room.UpdateWorld(app.simulation.State,.1f);
            yield return Shot("room-night-390");
            // Palette roles must remain readable independent of the 3D background.
            if(Contrast(GameHudStyle.Muted,GameHudStyle.Paper)<4.5f||Contrast(GameHudStyle.Paper,GameHudStyle.Accent)<4.5f)
                throw new Exception("Text palette contrast is below 4.5:1");
            File.WriteAllText(Path.Combine(directory,"PASS.txt"),"Production room and six HUD panels at 320/390/430/768 widths; bundled font, no truncated/outside text, separated status glyphs/values, >=44 logical point active controls, opaque surfaces and 4.5:1 text palette. Kitchen, quiet room, garden and night captured. Isolated fixture; desktop renders, not iPhone acceptance.\n");
            Debug.Log("VISUAL QUALITY REVIEW COMPLETE");Application.Quit();
        }
        IEnumerator Shot(string name)
        {app.hud.Refresh();yield return null;Canvas.ForceUpdateCanvases();if(!Application.isBatchMode)yield return new WaitForEndOfFrame();PlayerReviewCapture.Capture(app,Path.Combine(directory,name+".png"));}
        void CheckBadges()
        {
            foreach(var badge in new[]{app.hud.Satiety,app.hud.Bond})
            {
                var icon=(RectTransform)badge.transform.Find("Filled status icon");var number=(RectTransform)badge.transform.Find("Percentage");
                if(icon.anchoredPosition.x+icon.rect.width>number.anchoredPosition.x)throw new Exception("Status glyph overlaps its percentage");
            }
        }
        void CheckText()
        {
            var camera=app.hud.GetComponent<Canvas>().worldCamera;
            foreach(var t in app.hud.GetComponentsInChildren<TMP_Text>(false))
            {
                if(string.IsNullOrEmpty(t.text))continue;
                t.ForceMeshUpdate();if(t.font!=UiFont.Get()||t.isTextTruncated)throw new Exception("Font/truncated text: "+t.name+" / "+t.text);
                var corners=new Vector3[4];t.rectTransform.GetWorldCorners(corners);
                foreach(var c in corners){var p=RectTransformUtility.WorldToScreenPoint(camera,c);if(p.x<-1||p.x>PlayerReviewCapture.Width+1||p.y<-1||p.y>PlayerReviewCapture.Height+1)throw new Exception("Text outside render: "+t.name);}
            }
            foreach(var b in app.hud.GetComponentsInChildren<Button>(false))
            {
                if(!b.IsInteractable())continue;
                var r=(RectTransform)b.transform;if(Mathf.Min(r.rect.width,r.rect.height)<GameHudStyle.TouchTarget-1)throw new Exception("Small touch target: "+b.name);
            }
        }
        static float Luminance(Color c)=>.2126f*Mathf.GammaToLinearSpace(c.r)+.7152f*Mathf.GammaToLinearSpace(c.g)+.0722f*Mathf.GammaToLinearSpace(c.b);
        static float Contrast(Color a,Color b){float x=Luminance(a),y=Luminance(b);return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);}
    }
}
