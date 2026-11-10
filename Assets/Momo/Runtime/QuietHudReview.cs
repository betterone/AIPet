using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Momo.Core;

namespace Momo
{
    // Opt-in real Player checks: every run gets a separate temporary household.
    public sealed class QuietHudReview : MonoBehaviour
    {
        MomoApp app;string directory,error;int checks;
        void Awake(){Application.runInBackground=true;Application.logMessageReceived+=OnLog;}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void OnLog(string message,string stack,LogType kind){if(kind==LogType.Exception||kind==LogType.Error&&stack.Contains("Momo."))error=message;}
        IEnumerator Start()
        {
            directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/quiet-hud"));Directory.CreateDirectory(directory);
            foreach(string marker in new[]{"PASS.txt","FAIL.txt"}){string path=Path.Combine(directory,marker);if(File.Exists(path))File.Delete(path);}
            var run=Run();while(true)
            {
                object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception ex){File.WriteAllText(Path.Combine(directory,"FAIL.txt"),ex.ToString());Debug.LogException(ex);Application.Quit(1);yield break;}
                yield return next;
            }
        }
        void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        void Step(bool holding=false)
        {
            app.AdvanceWorld(.1f);app.RenderCats(.1f);app.window.CatPosition=app.cat.transform.position;app.window.Tick(.1f,false);
            app.bath.Tick(.1f);app.hud.TickVisibility(.1f,holding);app.hud.Refresh();
        }
        void Frames(int count,bool holding=false){for(int i=0;i<count;i++)Step(holding);}
        Button Button(string name)=>app.hud.GetComponentsInChildren<Button>().First(b=>b.name==name);
        bool CanHit(Button button)
        {
            if(!button.interactable)return false;
            for(Transform p=button.transform;p!=null;p=p.parent){var cg=p.GetComponent<CanvasGroup>();if(cg!=null&&(!cg.interactable||!cg.blocksRaycasts))return false;}
            return true;
        }
        int ButtonCount()=>app.hud.GetComponentsInChildren<Button>().Count(CanHit);
        Vector2 UiPoint(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();if(Application.isBatchMode)app.room.cameraView.Render();
            var canvas=app.hud.GetComponent<Canvas>();return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        }
        Rect ScreenRect(RectTransform rect)
        {
            var canvas=app.hud.GetComponent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        List<RaycastResult> Hits(Vector2 point)
        {var list=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},list);return list;}
        void CheckText()
        {
            Canvas.ForceUpdateCanvases();foreach(var text in app.hud.GetComponentsInChildren<TMP_Text>())
            {text.ForceMeshUpdate();Check(!text.isTextTruncated,"Truncated text: "+text.name+" / "+text.text);}
        }
        IEnumerator Capture(string name)
        {app.hud.Refresh();Canvas.ForceUpdateCanvases();yield return null;PlayerReviewCapture.Capture(app,Path.Combine(directory,name+".png"));}
        IEnumerator Run()
        {
            app=GetComponent<MomoApp>();app.AdoptBreed("Mikan",0);app.enabled=false;app.hud.SetAutoHide(true,false);app.Notify(null);
            app.window.Approach();for(int i=0;i<100;i++)app.window.Tick(.1f,false);
            var s=app.simulation.State;s.pet=s.target=new Point(0,1.6f);s.route.Clear();s.activity=s.intent=Activity.Idle;s.remaining=1000;s.energy=90;s.hunger=40;s.thirst=15;
            app.household.Focused.heading=180;app.RenderCats(.1f);app.window.Find(app.cat.HeadPosition);Frames(20,true);
            foreach(var size in new[]{new Vector2Int(390,844),new Vector2Int(320,700),new Vector2Int(768,1024)})
            {
                PlayerReviewCapture.Resize(app,size.x,size.y);yield return null;app.hud.Visibility.Wake();Frames(20,true);
                Check(ButtonCount()==3,"Exploration should have three buttons, found "+ButtonCount());
                Check(!app.hud.Satiety.gameObject.activeSelf&&!app.hud.Bond.gameObject.activeSelf,"Status cards still occupy exploration view");
                Check(!app.hud.Walk.Floating&&Mathf.Abs(app.hud.Walk.Visual.alpha-WalkPad.IdleOpacity)<.01f,"Fixed joystick base missing at rest");CheckText();yield return Capture("explore-"+size.x);
                app.Notify("请用左下方向盘走动，其他空白处拖动看四周。");app.hud.Refresh();
                var hint=app.hud.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Moment card");var walkRect=(RectTransform)app.hud.Walk.transform;
                var discPoint=UiPoint(app.hud.Walk.FloatingDisc);
                Check(hint.gameObject.activeInHierarchy,"Hint fixture did not display");
                Check(ScreenRect(hint).yMin>ScreenRect(walkRect).yMax&&!ScreenRect(hint).Overlaps(ScreenRect(walkRect)),"Hint covers joystick input area at "+size.x);
                Check(Hits(discPoint).Any(h=>h.gameObject.GetComponentInParent<WalkPad>()==app.hud.Walk),"Hint obstructs joystick touch at "+size.x);
                CheckText();yield return Capture("hint-clear-"+size.x);app.Notify(null);
                Frames(75);Check(app.hud.Quiet&&ButtonCount()==1,"Idle view did not leave just the menu");
                Check(Mathf.Abs(app.hud.Walk.Visual.alpha-WalkPad.IdleOpacity)<.01f&&app.hud.Walk.FloatingDisc.anchoredPosition==new Vector2(88,-88),"Idle hud hid or moved the joystick base");
                Check(Button("菜单").interactable,"Menu inaccessible when idle");
                var hiddenAction=Button("互动");Check(!Hits(UiPoint((RectTransform)hiddenAction.transform)).Any(h=>h.gameObject.GetComponentInParent<Button>()==hiddenAction),"Hidden action intercepts world input");
                yield return Capture("quiet-"+size.x);
            }
            PlayerReviewCapture.Resize(app,390,844);yield return null;Physics.SyncTransforms();
            // The first tap wakes rather than giving accidental care or switching focus.
            float bond=s.affection;int focus=app.FocusedCatId;
            Vector2 catPoint=app.room.cameraView.WorldToScreenPoint(app.cat.transform.position+Vector3.up*.55f);
            app.HandlePointer(catPoint,true,true,false,false);app.HandlePointer(catPoint,false,false,true,false);app.hud.Refresh();
            Check(!app.hud.Quiet&&s.affection==bond&&app.FocusedCatId==focus&&app.LastGesture=="wake / tap","Wake tap performed a world interaction");
            app.HandlePointer(catPoint,true,true,false,false);app.HandlePointer(catPoint,false,false,true,false);
            Check(s.affection>bond,"Second tap did not pet the cat");
            // A drag works in the same gesture that wakes the interface.
            Frames(75);var angles=app.window.Angles;var start=new Vector2(PlayerReviewCapture.Width*.7f,PlayerReviewCapture.Height*.55f);
            app.HandlePointer(start,true,true,false,false);app.HandlePointer(start+new Vector2(60,0),false,true,false,false);app.HandlePointer(start+new Vector2(60,0),false,false,true,false);
            Check(Mathf.Abs(app.window.Angles.x-angles.x)>1,"Wake drag lost camera movement");
            // The fixed direction pad remains reachable while secondary buttons are hidden.
            Frames(75);var pad=app.hud.Walk;var rect=(RectTransform)pad.transform;var centre=UiPoint(rect);
            Check(Hits(centre).Any(h=>h.gameObject.GetComponentInParent<WalkPad>()==pad),"Fixed joystick cannot be hit");
            var data=new PointerEventData(EventSystem.current){pointerId=71,position=centre,pointerPressRaycast=new RaycastResult{module=app.hud.GetComponent<GraphicRaycaster>()}};
            pad.OnPointerDown(data);Check(!pad.Moving&&!app.hud.Quiet,"Thumb down should wake without moving");
            data.position+=Vector2.up*(pad.Radius*app.hud.GetComponent<Canvas>().scaleFactor);pad.OnDrag(data);var before=app.window.ViewerPosition;Frames(2,true);
            Check(pad.Moving&&Vector2.Distance(new Vector2(before.x,before.z),new Vector2(app.window.ViewerPosition.x,app.window.ViewerPosition.z))>.05f,"Fixed joystick drag did not move observer");
            var look=app.window.Angles;app.HandlePointer(start,true,true,false,false);app.HandlePointer(start+Vector2.right*30,false,true,false,false);app.HandlePointer(start+Vector2.right*30,false,false,true,false);
            Check(pad.Pressed&&pad.Moving&&Mathf.Abs(app.window.Angles.x-look.x)>1,"Look gesture stole held joystick");
            yield return Capture("fixed-joystick-390");pad.OnPointerUp(data);before=app.window.ViewerPosition;Frames(5);
            Check(!pad.Moving&&Vector2.Distance(new Vector2(before.x,before.z),new Vector2(app.window.ViewerPosition.x,app.window.ViewerPosition.z))<.001f&&Mathf.Abs(pad.Visual.alpha-WalkPad.IdleOpacity)<.01f&&pad.Thumb.anchoredPosition==Vector2.zero,"Release drifted or failed to return to the fixed base");
            // Menu remains reachable, all previous features have an explicit entry.
            Frames(75);yield return null;var menu=Button("菜单");var menuPoint=UiPoint((RectTransform)menu.transform);var menuHits=Hits(menuPoint);
            Check(menuHits.Any(h=>h.gameObject.GetComponentInParent<Button>()==menu),"Quiet menu cannot be reached at "+menuPoint+" hits="+string.Join(",",menuHits.Select(h=>h.gameObject.name)));menu.onClick.Invoke();
            Check(app.hud.PanelOpen&&!app.hud.Quiet,"Menu did not wake and open");foreach(string name in new[]{"一起玩","找猫","回忆","拍照","设置"})Check(Button(name)!=null,"Missing menu action: "+name);
            CheckText();yield return Capture("menu-390");Button("一起玩").onClick.Invoke();Check(app.hud.PanelOpen&&Button("逗猫连扑")!=null,"Menu play entry only closed the panel");app.hud.ClosePanel();
            app.hud.SetAutoHide(false,false);Frames(100);Check(!app.hud.Quiet&&ButtonCount()==3,"Always visible setting did not hold");app.hud.ViewSettings();Check(Button("界面显示")!=null,"No visibility preference entry");CheckText();yield return Capture("display-setting-390");app.hud.ClosePanel();app.hud.SetAutoHide(true,false);
            // Care is pinned even after long simulated idle, with a compact explicit exit.
            app.window.ReturnToEntry();app.room.cameraView.transform.position=new Vector3(-1.5f,1.55f,6);s.pet=s.target=new Point(-1.3f,9.2f);s.route.Clear();s.activity=s.intent=Activity.Idle;s.hunger=60;s.energy=90;
            app.GrabFood(Room.IndoorBag);app.hud.TickVisibility(30);app.hud.Refresh();Check(app.FeedingActive&&!app.hud.Quiet&&Button("收起猫粮").gameObject.activeInHierarchy,"Feeding exit hid");yield return Capture("feeding-essential-390");app.CancelFeeding();
            app.window.ReturnToEntry();app.room.cameraView.transform.position=new Vector3(0,1.55f,14.2f);s.pet=s.target=CatNavigation.Nearest(new Point(2.5f,14.7f));s.route.Clear();s.activity=s.intent=Activity.Idle;s.hunger=s.thirst=10;s.energy=90;app.RenderCats(.1f);app.BeginBath();
            for(int i=0;i<160&&app.BathActive&&!app.household.Bath.Washing;i++){Step();if(i%6==0)yield return null;}
            Check(app.BathActive&&app.household.Bath.Washing,"Bath did not start for compact-care check");Frames(20);app.hud.TickVisibility(30);app.hud.Refresh();Check(!app.hud.Quiet&&Button("结束洗澡").gameObject.activeInHierarchy,"Bath exit hid");
            Check(app.hud.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Bath care card").rect.height<=110,"Bath card still oversized");CheckText();yield return Capture("bath-essential-390");
            app.hud.HomeMenu();Check(!app.BathActive&&app.hud.PanelOpen&&!pad.Moving,"Opening menu did not cancel care and movement");
            Check(error==null,"Runtime error: "+error);
            File.WriteAllText(Path.Combine(directory,"PASS.txt"),checks+" production HUD checks passed: 3 exploration buttons / 1 idle menu plus always-visible fixed joystick; real UI raycasts; fixed joystick press, drag, recenter, release and look ownership; safe wake tap and immediate wake drag; menu actions; non-persisted display preference; pinned care; hint banners stay above the whole joystick input area with real touch reachability; 320/390/768 layouts. Isolated save, desktop evidence only.\n");Application.Quit(0);
        }
    }
}
