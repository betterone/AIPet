using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Momo.Core;
namespace Momo
{
    public sealed class WandChallengeStage : MonoBehaviour
    {
        public const int Layer=29;
        const float BaseY=80;
        MomoApp app;
        public Camera Camera {get;private set;}
        public CatView Cat {get;private set;}
        public WandChallengeSession Session {get;private set;}
        public bool Active=>gameObject.activeSelf;
        Transform world,feather,nearRing,farRing,nearPulse,farPulse,facilities;
        RectTransform safe,result;
        TMP_Text score,combo,clock,hint,stage,nearLabel,farLabel;
        Canvas canvas;
        public Canvas Interface=>canvas;
        int ViewWidth=>Camera.targetTexture!=null?Camera.targetTexture.width:Screen.width;
        int ViewHeight=>Camera.targetTexture!=null?Camera.targetTexture.height:Screen.height;
        bool settled;
        int previousFacility,previousSkill;
        float heading=180;
        uint seed;
        int layout;
        string routeName;int builtBreed=-1,builtLayout=-1;bool builtImported;
        Material teal,gold;AudioSource feedbackAudio;int observedCatches,observedMisses;float feedbackUntil;
        public void Build(MomoApp owner)
        {
            app=owner;feedbackAudio=gameObject.AddComponent<AudioSource>();feedbackAudio.playOnAwake=false;feedbackAudio.spatialBlend=0;feedbackAudio.volume=.28f;feedbackAudio.clip=Resources.Load<AudioClip>("CatAudio/Purr1");
            world=new GameObject("Wand play garden").transform;world.SetParent(transform,false);world.localPosition=new Vector3(0,BaseY,0);
            var lawn=Art.Mat("Play garden mint","#BFD9C5");teal=Unlit("#368F9E");gold=Unlit("#D9863D");
            Art.Cube("Play lawn",world,new Vector3(0,-.08f,0),new Vector3(7,.15f,8),lawn);
            var fence=Art.Mat("Play fence blue","#6699AD");
            for(int i=-6;i<=6;i++){Art.Cube("Garden fence",world,new Vector3(i*.45f,.45f,2.6f),new Vector3(.24f,.9f,.12f),fence);}
            Art.Cube("Fence rail",world,new Vector3(0,.55f,2.63f),new Vector3(6,.1f,.1f),fence);
            Art.Cylinder("Play patch",world,new Vector3(0,.008f,0),new Vector3(3.3f,.008f,3),Art.Mat("Play patch sage","#D9E6CF"));
            var light=new GameObject("Play sunlight",typeof(Light)).GetComponent<Light>();light.transform.SetParent(world,false);light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(42,-24,0);light.intensity=.8f;light.cullingMask=1<<Layer;light.shadows=LightShadows.Soft;
            Camera=new GameObject("Your play view",typeof(Camera)).GetComponent<Camera>();Camera.transform.SetParent(transform,false);Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=GameHudStyle.Hex("#DAE9E3");Camera.cullingMask=1<<Layer;Camera.nearClipPlane=.1f;Camera.farClipPlane=30;Camera.fieldOfView=48;
            feather=Art.Pivot("Catchable feather",world,new Vector3(0,.18f,-.65f));
            var stem=Art.Mat("Feather stem","#926E50");Art.Cylinder("Feather quill",feather,Vector3.zero,new Vector3(.025f,.16f,.025f),stem);
            var a=Art.Shape("Blue feather",PrimitiveType.Sphere,feather,new Vector3(-.045f,.12f,0),new Vector3(.12f,.3f,.06f),Art.Mat("Feather blue","#328DA1"));a.transform.localRotation=Quaternion.Euler(0,0,25);
            var b=Art.Shape("Coral feather",PrimitiveType.Sphere,feather,new Vector3(.055f,.1f,0),new Vector3(.10f,.25f,.055f),Art.Mat("Feather coral","#DA895F"));b.transform.localRotation=Quaternion.Euler(0,0,-25);
            nearRing=Ring("Near landing",teal);farRing=Ring("Far landing",gold);nearPulse=Ring("Near timing",teal);farPulse=Ring("Far timing",gold);
            facilities=new GameObject("Play facilities").transform;facilities.SetParent(world,false);
            var ui=new GameObject("Wand challenge interface",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));ui.transform.SetParent(transform,false);canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            var scaler=ui.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(390,844);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            safe=Rect("Play safe area",ui.transform,Vector2.zero,Vector2.zero,Vector2.zero);safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.sizeDelta=Vector2.zero;
            Label("Play title",safe,"逗猫连扑",19,new Vector2(20,-15),new Vector2(240,30));
            Button("回家",safe,new Vector2(-82,-12),new Vector2(64,44),new Vector2(1,1),()=>Close());
            score=Label("Play score",safe,"0",44,new Vector2(20,-60),new Vector2(180,78));score.fontStyle=FontStyles.Bold;
            clock=Label("Play time",safe,"60 秒",18,new Vector2(-122,-74),new Vector2(104,30),new Vector2(1,1));clock.alignment=TextAlignmentOptions.Right;
            combo=Label("Play combo",safe,"",15,new Vector2(22,-144),new Vector2(340,28));
            stage=Label("Play stage",safe,"",12,new Vector2(22,-182),new Vector2(340,40));
            nearLabel=Label("Near choice",safe,"稳稳抓 · 10",12,Vector2.zero,new Vector2(110,28));nearLabel.alignment=TextAlignmentOptions.Center;
            farLabel=Label("Far choice",safe,"试远处 · 20",12,Vector2.zero,new Vector2(110,28));farLabel.alignment=TextAlignmentOptions.Center;farLabel.color=GameHudStyle.Hex("#995B28");
            var card=Rect("Play instruction",safe,new Vector2(18,18),new Vector2(-36,114),Vector2.zero);card.anchorMax=new Vector2(1,0);card.pivot=Vector2.zero;
            var image=card.gameObject.AddComponent<Image>();image.color=GameHudStyle.Paper;image.raycastTarget=false;GameHudStyle.Surface(image,false,true);
            hint=Label("Play hint",card,"",16,new Vector2(16,-12),new Vector2(320,86));hint.enableAutoSizing=true;hint.fontSizeMin=12;hint.fontSizeMax=16;
            SetLayers(ui.transform);SetLayers(world);app.room.sun.cullingMask&=~(1<<Layer);app.room.cameraView.cullingMask&=~(1<<Layer);gameObject.SetActive(false);
        }
        static Material Unlit(string hex){var m=new Material(Shader.Find("Sprites/Default"));m.color=GameHudStyle.Hex(hex);return m;}
        Transform Ring(string name,Material material)
        {
            var g=new GameObject(name,typeof(LineRenderer));g.transform.SetParent(world,false);var line=g.GetComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=80;line.startWidth=line.endWidth=.024f;line.sharedMaterial=material;
            for(int i=0;i<80;i++){float a=i*Mathf.PI*2/80;line.SetPosition(i,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}return g.transform;
        }
        public void Open(int selectedLayout,uint selectedSeed,string name,bool retry=false)
        {
            app.CancelBath();app.CancelFeeding();app.CancelWandPlay();app.company?.Cancel();app.hud.ClosePanel();app.hud.Walk.Clear();app.hands.ResetPose();app.window.PausePosition();
            if(Cat==null){Cat=new GameObject("Your play companion").AddComponent<CatView>();Cat.transform.SetParent(world,false);}
            if(builtBreed!=app.simulation.State.breedIndex||builtImported!=app.UseImportedCat){Cat.Build(BreedArchetypes.Get(app.simulation.State.breedIndex),app.UseImportedCat);builtBreed=app.simulation.State.breedIndex;builtImported=app.UseImportedCat;}
            previousFacility=app.Progress.facilityLevel;previousSkill=app.Progress.Cat(app.FocusedCatId).skillLevel;
            layout=selectedLayout;seed=selectedSeed;routeName=name;
            Session=app.Progress.Begin(app.FocusedCatId,layout,seed,app.PlayTuning,DateTime.Now,retry);app.Save();settled=false;observedCatches=observedMisses=0;feedbackUntil=0;
            if(result!=null){result.gameObject.SetActive(false);Destroy(result.gameObject);result=null;}
            if(builtLayout!=layout){foreach(Transform child in facilities){child.gameObject.SetActive(false);Destroy(child.gameObject);}BuildFacility(facilities,layout);builtLayout=layout;}
            gameObject.SetActive(true);SetLayers(world);app.room.cameraView.enabled=false;heading=180;Cat.transform.rotation=Quaternion.Euler(0,180,0);Reframe();Render(0);app.hud.Refresh();
        }
        public static void BuildFacility(Transform parent,int level)
        {
            if(level==0)return;
            if(level==1){Art.Cylinder("Pounce mat",parent,new Vector3(0,.018f,0),new Vector3(2.7f,.01f,2.5f),Art.Mat("Pounce mat lake","#B3CFD3"));return;}
            var board=Art.Mat("Toy cardboard","#C69D6D");var edge=Art.Mat("Toy cardboard edge","#AB7D4E");
            if(level==2)
            {
                Art.Cube("Low cardboard box",parent,new Vector3(0,.19f,.08f),new Vector3(.58f,.38f,.4f),board);
                Art.Cube("Cardboard seam",parent,new Vector3(0,.385f,.08f),new Vector3(.025f,.012f,.4f),edge);return;
            }
            foreach(float x in new[]{-.64f,.64f})Art.Cube("Tunnel side",parent,new Vector3(x,.55f,.13f),new Vector3(.08f,1.1f,.65f),board);
            Art.Cube("Tunnel roof",parent,new Vector3(0,1.14f,.13f),new Vector3(1.36f,.08f,.65f),board);
        }
        public void Reframe()
        {
            var a=Screen.safeArea;safe.anchorMin=Camera.targetTexture!=null?Vector2.zero:new Vector2(a.xMin/Screen.width,a.yMin/Screen.height);safe.anchorMax=Camera.targetTexture!=null?Vector2.one:new Vector2(a.xMax/Screen.width,a.yMax/Screen.height);
            Camera.rect=new Rect(0,0,1,1);Camera.aspect=(float)ViewWidth/ViewHeight;
            float distance=Mathf.Max(4.5f,1.65f/(Mathf.Tan(24*Mathf.Deg2Rad)*Camera.aspect));
            Vector3 focus=new Vector3(0,BaseY+.40f,.1f);Camera.transform.position=focus+new Vector3(0,1.6f,-distance);Camera.transform.LookAt(focus);
            Canvas.ForceUpdateCanvases();hint.rectTransform.sizeDelta=new Vector2(safe.rect.width-68,86);
        }
        public void Tick(float dt,bool input=true)
        {
            if(!Active||Session==null)return;Session.Advance(dt);
            if(input&&Session.Active)
            {
                if(Input.touchCount>1||(Input.touchCount==1&&Input.GetTouch(0).phase==TouchPhase.Canceled))Session.PointerCancel();
                else
                {
                    Vector2 p=Input.mousePosition;bool down=Input.GetMouseButtonDown(0),held=Input.GetMouseButton(0),up=Input.GetMouseButtonUp(0);
                    if(Input.touchCount==1){var t=Input.GetTouch(0);p=t.position;down=t.phase==TouchPhase.Began;up=t.phase==TouchPhase.Ended;held=!up;}
                    HandlePointer(p,down,held,up,app.PointerOverUi(p));
                }
            }
            if(!Session.Active&&!settled&&Session.Result!=null)Settle();Render(dt);
            if(input&&Input.GetKeyDown(KeyCode.Escape))Close();
        }
        public void HandlePointer(Vector2 p,bool down,bool held,bool up,bool overUi)
        {
            if(Session==null||!Session.Active)return;
            if(overUi){if(up)Session.PointerCancel();return;}
            if(!Camera.pixelRect.Contains(p)){if(up)Session.PointerCancel();return;}
            var ray=Camera.ScreenPointToRay(p);if(!new Plane(Vector3.up,new Vector3(0,BaseY+.18f,0)).Raycast(ray,out float t))return;
            Vector3 v=ray.GetPoint(t);Point point=new Point(v.x,v.z);
            if(down)Session.PointerDown(point);if(held)Session.PointerMove(point);if(up)Session.PointerUp(point);
        }
        void Render(float dt)
        {
            if(Session==null)return;
            if(observedCatches!=Session.Catches||observedMisses!=Session.Misses)
            {if(observedCatches!=Session.Catches&&app.simulation.State.sound&&!feedbackAudio.isPlaying)feedbackAudio.Play();observedCatches=Session.Catches;observedMisses=Session.Misses;feedbackUntil=Time.unscaledTime+2.2f;}
            var before=Cat.transform.position;Vector3 position=new Vector3(Session.Cat.x,BaseY,Session.Cat.z);Cat.transform.position=position;
            Vector3 delta=position-before;if(delta.sqrMagnitude>.000001f)heading=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            Cat.transform.rotation=Quaternion.RotateTowards(Cat.transform.rotation,Quaternion.Euler(0,heading,0),Mathf.Max(.001f,dt)*480);
            if(Session.Phase==WandPhase.Recover||Session.Phase==WandPhase.Finished)heading=180;
            Vector3 target=Session.Phase==WandPhase.Recover||Session.Phase==WandPhase.Finished?Camera.transform.position:new Vector3(Session.Attention.x,BaseY+.12f,Session.Attention.z);
            Cat.TickPlay(Session.Phase,Session.AnimationProgress,dt>0?delta.magnitude:0,target);
            feather.localPosition=new Vector3(Session.Feather.x,.15f,Session.Feather.z);
            feather.localRotation=Quaternion.Euler(0,0,Session.Phase==WandPhase.Recover?Mathf.Sin(Time.unscaledTime*12)*14:0);
            bool playing=Session.Active;
            nearRing.gameObject.SetActive(playing);farRing.gameObject.SetActive(playing);nearLabel.gameObject.SetActive(playing);farLabel.gameObject.SetActive(playing);
            PositionRing(nearRing,Session.Near,app.PlayTuning.nearRadius);PositionRing(farRing,Session.Far,app.PlayTuning.farRadius);
            bool ready=Session.Phase==WandPhase.Ready;nearPulse.gameObject.SetActive(ready);farPulse.gameObject.SetActive(ready);
            float shrink=Mathf.Lerp(1.9f,.70f,Session.PhaseProgress);
            PositionRing(nearPulse,Session.Near,app.PlayTuning.nearRadius*shrink);PositionRing(farPulse,Session.Far,app.PlayTuning.farRadius*shrink);
            nearLabel.text="稳稳抓 · "+app.PlayTuning.nearScore;farLabel.text="试远处 · "+app.PlayTuning.farScore;
            WorldLabel(nearLabel,Session.Near);WorldLabel(farLabel,Session.Far);
            score.text=Session.Score.ToString();clock.text=Mathf.CeilToInt(Session.Remaining)+" 秒";
            combo.text=Session.Combo+" 连击   ·   "+Math.Max(0,app.PlayTuning.missLimit-Session.Misses)+" 次机会";
            stage.text=(Session.Stage==3?"默契时刻":Session.Stage==2?"连着扑":Session.Stage==1?"追起来":"先找找节奏")+"  /  "+routeName;
            hint.text=Session.Phase==WandPhase.Chase&&Time.unscaledTime>=feedbackUntil?(Session.Dragging?"带着羽毛移动，等它伏低。":"拖动羽毛带它追起来。大圈稳，小圈分更高。") : Session.Feedback;
        }
        void PositionRing(Transform ring,Point p,float radius){ring.localPosition=new Vector3(p.x,.035f,p.z);ring.localScale=Vector3.one*radius;}
        void WorldLabel(TMP_Text label,Point p)
        {
            Vector3 viewport=Camera.WorldToViewportPoint(new Vector3(p.x,BaseY+.05f,p.z));
            Vector2 size=safe.anchorMax-safe.anchorMin;
            var anchor=new Vector2((viewport.x-safe.anchorMin.x)/size.x,(viewport.y-safe.anchorMin.y)/size.y);
            label.rectTransform.pivot=new Vector2(.5f,1);label.rectTransform.anchorMin=label.rectTransform.anchorMax=anchor;label.rectTransform.anchoredPosition=Vector2.down*8;
        }
        void Settle()
        {
            settled=true;var r=Session.Result;
            if(app.Progress.Settle(r,app.PlayTuning,DateTime.Now))
            {app.simulation.Record("wand-challenge","一起扑抓羽毛 "+r.Catches+" 次，留下了新的配合。");app.Save();app.PlayGarden?.Refresh();}
            result=Rect("Play results",safe,new Vector2(18,16),new Vector2(-36,320),Vector2.zero);result.anchorMax=new Vector2(1,0);result.pivot=Vector2.zero;
            var image=result.gameObject.AddComponent<Image>();image.color=GameHudStyle.Paper;image.raycastTarget=true;GameHudStyle.Surface(image,false,true);
            string unlocked=app.Progress.facilityLevel>previousFacility?"搭好了"+PlayProgress.FacilityName(app.Progress.facilityLevel)+"！":app.Progress.Cat(r.CatId).skillLevel>previousSkill?"学会了"+PlayProgress.SkillName(app.Progress.Cat(r.CatId).skillLevel)+"！":"这一局的配合";
            float width=safe.rect.width-68;
            Label("Result title",result,unlocked,20,new Vector2(16,-12),new Vector2(width,34));
            Label("Result record",result,"最佳 "+app.Progress.Best(r.CatId,r.Layout,r.Seed,r.Rules)+" 分  ·  最长 "+r.BestCombo+" 连击",14,new Vector2(16,-54),new Vector2(width,30));
            Label("Result miss",result,r.Catches==0?"先拖动羽毛，带它追起来，再等它伏低。":r.ClosestMiss??"每一次配合都抓住了，试试更远的小圈。",13,new Vector2(16,-92),new Vector2(width,48));
            Label("Result goal",result,app.Progress.NextGoal(r.CatId,app.PlayTuning),13,new Vector2(16,-146),new Vector2(width,46));
            Button("再玩一局",result,new Vector2(16,-204),new Vector2(width,46),new Vector2(0,1),()=>Open(layout,seed,routeName,true));
            Button("回家看看",result,new Vector2(16,-262),new Vector2((width-10)/2,44),new Vector2(0,1),()=>Close());
            Button("试试新设施",result,new Vector2(26+(width-10)/2,-262),new Vector2((width-10)/2,44),new Vector2(0,1),()=>Open(app.Progress.facilityLevel,27183,"自由练习",true));
            SetLayers(result);
        }
        public void Close(bool interrupted=false)
        {
            if(!Active)return;if(Session!=null&&Session.Active){Session.Abort();app.Progress.Abort(DateTime.Now);app.Save();}
            gameObject.SetActive(false);app.room.cameraView.enabled=true;app.window.ResumePosition();app.hud.Visibility.Wake();app.hud.Refresh();
            if(interrupted)app.Notify("刚才的游戏已停下。想玩时，随时再开一局。");else app.Notify(app.Progress.NextGoal(app.FocusedCatId,app.PlayTuning));
        }
        static void SetLayers(Transform root){foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;}
        RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size,Vector2 anchor)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        TMP_Text Label(string name,Transform parent,string value,int size,Vector2 pos,Vector2 dimensions,Vector2? anchor=null)
        {var r=Rect(name,parent,pos,dimensions,anchor??new Vector2(0,1));var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=UiFont.Get();t.fontSize=size;t.text=value;t.color=GameHudStyle.Ink;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;return t;}
        void Button(string title,Transform parent,Vector2 pos,Vector2 dimensions,Vector2 anchor,UnityEngine.Events.UnityAction action)
        {
            var r=Rect(title,parent,pos,dimensions,anchor);var image=r.gameObject.AddComponent<Image>();image.color=GameHudStyle.Mist;GameHudStyle.Surface(image,false,true);
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            var label=Label(title+" label",r,title,15,Vector2.zero,Vector2.zero);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.sizeDelta=Vector2.zero;label.alignment=TextAlignmentOptions.Center;
        }
    }
}
