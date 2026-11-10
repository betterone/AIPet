using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Momo.Core;

namespace Momo
{
    public sealed class MomoHud : MonoBehaviour
    {
        MomoApp app;
        RectTransform safe,panel,chrome,captionCard,identity;
        TMP_FontAsset font;
        TMP_Text nameText,timeText,caption,debugText;
        GameObject debugPanel;
        Button peekButton,settings,quickAction;
        CanvasGroup identityFade,actionFade,menuFade;
        TMP_Text quickLabel;
        float gainsUntil,guideUntil;
        bool introduced;
        const string AutoHideKey="Momo.HudAutoHideV1";
        public readonly HudVisibility Visibility=new HudVisibility();
        public bool Quiet=>Visibility.Quiet;
        Image modeDot;
        BoutiqueHud boutiqueHud;
        public WalkPad Walk {get;private set;}
        public NeedBadge Satiety {get;private set;}
        public NeedBadge Bond {get;private set;}
        Button withdraw,recenter;
        RectTransform bathCard;
        TMP_Text bathStep,bathHint,bathZones;
        Button bathNext,bathEnd;
        public bool PanelOpen=>panel!=null;
        static Color Ink=>GameHudStyle.Ink;
        static Color Muted=>GameHudStyle.Muted;
        static Color Paper=>GameHudStyle.Paper;
        static Color Accent=>GameHudStyle.Accent;
        static Color Mist=>GameHudStyle.Mist;

        public void Build(MomoApp app)
        {
            this.app=app;font=UiFont.Get();
            Visibility.AutoHide=PlayerPrefs.GetInt(AutoHideKey,1)!=0;
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(390,844);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if(EventSystem.current==null)new GameObject("Touch events",typeof(EventSystem),typeof(StandaloneInputModule));
            safe=Rect("Safe area",transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);ApplySafeArea();
            var nameButton=Button("猫咪状态",safe,new Vector2(16,-12),new Vector2(138,44),new Vector2(0,1),Household,Paper);
            identity=(RectTransform)nameButton.transform;RemoveLabel(nameButton);
            identityFade=identity.gameObject.AddComponent<CanvasGroup>();
            Icon("paw",identity,new Vector2(10,-12),new Vector2(20,20),new Vector2(0,1),Accent);
            nameText=Text("Name",identity,"Momo",16,Ink,new Vector2(38,-10),new Vector2(90,24),new Vector2(0,1));nameText.fontStyle=FontStyles.Bold;
            nameText.enableAutoSizing=true;nameText.fontSizeMin=13;nameText.fontSizeMax=16;nameText.textWrappingMode=TextWrappingModes.NoWrap;
            timeText=Text("Time",identity,"",12,Muted,Vector2.zero,Vector2.zero,new Vector2(0,1));timeText.gameObject.SetActive(false);
            settings=IconButton("菜单","more",safe,new Vector2(-60,-12),44,new Vector2(1,1),HomeMenu);
            menuFade=settings.gameObject.AddComponent<CanvasGroup>();
            chrome=Rect("Room controls",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            quickAction=Button("互动",chrome,new Vector2(-72,20),new Vector2(56,56),new Vector2(1,0),()=>{if(app.WandActive)app.CancelWandPlay();else PlayGuide();},Paper,true);
            quickLabel=quickAction.GetComponentInChildren<TMP_Text>();actionFade=quickAction.gameObject.AddComponent<CanvasGroup>();
            BuildWalkControls();
            Satiety=Badge("饱腹","food",new Vector2(16,-64),GameHudStyle.Hex("#589478"));
            Bond=Badge("亲密","heart",new Vector2(108,-64),GameHudStyle.Hex("#CB7588"));
            Satiety.transform.localScale=Bond.transform.localScale=Vector3.one*.85f;
            withdraw=IconButton("收起猫粮","close",chrome,new Vector2(-60,-68),44,new Vector2(1,1),()=>app.WithdrawFood());
            captionCard=Surface("Moment card",chrome,new Vector2(16,200),new Vector2(-32,44),Vector2.zero,new Color(Paper.r,Paper.g,Paper.b,.88f));captionCard.anchorMax=new Vector2(1,0);captionCard.GetComponent<Image>().raycastTarget=false;
            caption=Text("Moment",captionCard,"",12,Ink,new Vector2(12,-6),new Vector2(330,32),new Vector2(0,1));caption.enableAutoSizing=true;caption.fontSizeMin=11;caption.fontSizeMax=12;
            recenter=IconButton("回正视角","reset",chrome,new Vector2(-64,208),48,new Vector2(1,0),app.RecenterView);
            Text("Recenter label",recenter.transform,"回正",12,Muted,new Vector2(0,-47),new Vector2(48,22),new Vector2(0,1)).alignment=TextAlignmentOptions.Center;
            recenter.gameObject.SetActive(false);
            modeDot=Surface("Motion indicator",settings.transform,new Vector2(-8,-2),new Vector2(8,8),new Vector2(1,1),GameHudStyle.Gold,false,true).GetComponent<Image>();modeDot.raycastTarget=false;
            peekButton=Button("走进房间",safe,new Vector2(-88,30),new Vector2(176,52),new Vector2(.5f,0),()=>app.window.Approach(),Accent);
            peekButton.GetComponentInChildren<TMP_Text>().color=Paper;
            bathCard=Surface("Bath care card",safe,new Vector2(16,14),new Vector2(-32,108),Vector2.zero,Paper,true);bathCard.anchorMax=new Vector2(1,0);
            bathStep=Text("Bath step",bathCard,"",16,Ink,new Vector2(12,-8),new Vector2(330,24),new Vector2(0,1));bathStep.fontStyle=FontStyles.Bold;
            bathZones=Text("Bath body coverage",bathCard,"",11,Accent,new Vector2(12,-36),new Vector2(330,20),new Vector2(0,1));
            bathHint=Text("Bath hint",bathCard,"",12,Ink,new Vector2(12,-66),new Vector2(330,30),new Vector2(0,1));bathHint.enableAutoSizing=true;bathHint.fontSizeMin=10;bathHint.fontSizeMax=12;
            bathNext=Button("换上沐浴露",bathCard,new Vector2(-144,8),new Vector2(132,44),new Vector2(1,0),()=>{app.bath.ReleasePointer();app.household.Bath.AdvanceStage();},Mist);
            bathEnd=IconButton("结束洗澡","close",safe,new Vector2(-60,-68),44,new Vector2(1,1),()=>{app.CancelBath(false);app.Save();});
            bathCard.gameObject.SetActive(false);bathEnd.gameObject.SetActive(false);
            debugPanel=Surface("Debug",safe,new Vector2(16,-100),new Vector2(358,440),new Vector2(0,1),new Color(.1f,.19f,.17f,.96f)).gameObject;
            debugText=Text("Diagnostics",debugPanel.transform,"",12,Paper,new Vector2(14,-12),new Vector2(330,416),new Vector2(0,1));debugPanel.SetActive(false);
            boutiqueHud=new GameObject("Boutique interface").AddComponent<BoutiqueHud>();boutiqueHud.Build(app,font);
            if(app.LoadError!=null)ErrorPanel();
        }

        NeedBadge Badge(string label,string icon,Vector2 position,Color color)
        {
            var rect=Rect(label+" status",chrome,new Vector2(0,1),new Vector2(0,1),position,new Vector2(96,48));
            var badge=rect.gameObject.AddComponent<NeedBadge>();badge.Build(font,icon,label,color);return badge;
        }
        public void ShowGain(float food,float bond){Satiety?.Flash(food);Bond?.Flash(bond);if(food>0||bond>0)gainsUntil=Time.unscaledTime+4;}
        Button DockButton(string title,string glyph,Transform parent,UnityEngine.Events.UnityAction action,bool primary)
        {
            var b=Button(title,parent,Vector2.zero,new Vector2(primary?84:80,primary?84:66),Vector2.zero,action,primary?Accent:new Color(1,1,1,.01f),primary);
            RemoveLabel(b);var r=(RectTransform)b.transform;
            Icon(glyph,r,new Vector2(primary?23:26,primary?-12:-6),new Vector2(primary?38:28,primary?38:28),new Vector2(0,1),primary?Paper:Accent);
            Text("Label",r,title,12,primary?Paper:Ink,new Vector2(0,primary?-54:-38),new Vector2(primary?84:80,22),new Vector2(0,1)).alignment=TextAlignmentOptions.Center;
            return b;
        }
        void BuildWalkControls()
        {
            var pad=Surface("Walk pad",chrome,new Vector2(0,12),new Vector2(176,176),Vector2.zero,Color.clear);
            pad.pivot=new Vector2(0,1);pad.anchoredPosition=new Vector2(0,188);
            Walk=pad.gameObject.AddComponent<WalkPad>();Walk.View=app.window;
            var disc=Surface("Fixed walk disc",pad,new Vector2(88,-88),new Vector2(96,96),new Vector2(0,1),new Color(.86f,.93f,.89f,.48f),false,true);disc.pivot=new Vector2(.5f,.5f);disc.GetComponent<Image>().raycastTarget=false;
            var visual=disc.gameObject.AddComponent<CanvasGroup>();visual.alpha=WalkPad.IdleOpacity;visual.blocksRaycasts=false;visual.interactable=false;
            var thumb=Surface("Movement thumb",disc,Vector2.zero,new Vector2(38,38),new Vector2(.5f,.5f),new Color(Paper.r,Paper.g,Paper.b,.88f),false,true);thumb.pivot=new Vector2(.5f,.5f);thumb.GetComponent<Image>().raycastTarget=false;
            Icon("paw",thumb,new Vector2(10,-10),new Vector2(18,18),new Vector2(0,1),Accent);
            Walk.Thumb=thumb;Walk.Radius=38;Walk.Floating=false;Walk.FloatingDisc=disc;Walk.Visual=visual;Walk.Activity=()=>Visibility.Wake();
        }
        void ApplySafeArea()
        {var a=Screen.safeArea;safe.anchorMin=new Vector2(a.xMin/Screen.width,a.yMin/Screen.height);safe.anchorMax=new Vector2(a.xMax/Screen.width,a.yMax/Screen.height);}
        RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 pos,Vector2 size)
        {var g=new GameObject(name,typeof(RectTransform));var r=g.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(0,1);r.anchoredPosition=pos;r.sizeDelta=size;return r;}
        RectTransform Surface(string name,Transform parent,Vector2 pos,Vector2 size,Vector2 anchor,Color color,bool shadow=false,bool disk=false)
        {var r=Rect(name,parent,anchor,anchor,pos,size);if(anchor.y==0)r.pivot=Vector2.zero;var image=r.gameObject.AddComponent<Image>();image.color=color;GameHudStyle.Surface(image,disk,shadow);return r;}
        TMP_Text Text(string name,Transform parent,string value,int size,Color color,Vector2 pos,Vector2 dimensions,Vector2 anchor)
        {var r=Rect(name,parent,anchor,anchor,pos,dimensions);if(anchor.y==0)r.pivot=Vector2.zero;var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Truncate;t.margin=Vector4.zero;return t;}
        void Icon(string glyph,Transform parent,Vector2 pos,Vector2 size,Vector2 anchor,Color color)
        {var r=Rect("Icon "+glyph,parent,anchor,anchor,pos,size);if(anchor.y==0)r.pivot=Vector2.zero;var i=r.gameObject.AddComponent<Image>();i.sprite=GameHudStyle.Icon(glyph);i.color=color;i.raycastTarget=false;}
        Button Button(string title,Transform parent,Vector2 pos,Vector2 size,Vector2 anchor,UnityEngine.Events.UnityAction action,Color color,bool disk=false)
        {
            var r=Surface(title,parent,pos,size,anchor,color,true,disk);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();b.onClick.AddListener(()=>{Visibility.Wake();if(MomoApp.MobileHudReviewMode)Debug.Log("HUD action: "+title);action();});
            var t=Text("Label",r,title,15,Ink,Vector2.zero,Vector2.zero,new Vector2(0,1));t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=new Vector2(8,3);t.rectTransform.offsetMax=new Vector2(-8,-3);t.alignment=TextAlignmentOptions.Center;
            var c=b.colors;c.normalColor=Color.white;c.highlightedColor=new Color(.94f,.98f,.94f);c.pressedColor=new Color(.76f,.88f,.78f);c.disabledColor=new Color(.85f,.89f,.85f,.55f);c.fadeDuration=.1f;b.colors=c;return b;
        }
        static void RemoveLabel(Button button)
        {var t=button.transform.Find("Label");if(t!=null){t.gameObject.SetActive(false);Destroy(t.gameObject);}}
        Button IconButton(string title,string glyph,Transform parent,Vector2 pos,float size,Vector2 anchor,UnityEngine.Events.UnityAction action)
        {var b=Button(title,parent,pos,Vector2.one*size,anchor,action,Paper,true);RemoveLabel(b);Icon(glyph,b.transform,new Vector2(size*.22f,-size*.22f),Vector2.one*(size*.56f),new Vector2(0,1),Accent);return b;}

        void Panel(string title,float height=560)
        {
            ClosePanel();app.CancelBath();app.CancelFeeding();Walk?.Clear();var shade=Rect("Panel shade",safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);shade.gameObject.AddComponent<Image>().color=GameHudStyle.Scrim;
            float width=Mathf.Min(390,Mathf.Max(280,safe.rect.width-24));height=Mathf.Min(height,safe.rect.height-28);
            panel=Surface(title,shade,new Vector2(0,14),new Vector2(width,height),new Vector2(.5f,0),Paper,true);panel.pivot=new Vector2(.5f,0);
            Surface("Sheet handle",panel,new Vector2(-18,-12),new Vector2(36,4),new Vector2(.5f,1),GameHudStyle.Line).GetComponent<Image>().raycastTarget=false;
            Text("Title",panel,title,23,Ink,new Vector2(22,-34),new Vector2(width-90,38),new Vector2(0,1)).fontStyle=FontStyles.Bold;
            var close=IconButton("关闭","close",panel,new Vector2(-64,-30),44,new Vector2(1,1),ClosePanel);
            close.GetComponent<Image>().color=Mist;
        }
        public void ClosePanel(){if(panel!=null){var shade=panel.parent.gameObject;shade.SetActive(false);Destroy(shade);panel=null;Visibility.Wake();}}
        public void HomeMenu()
        {
            if(!app.HasCat||app.LoadError!=null)return;if(PanelOpen){ClosePanel();return;}Panel("小家",600);
            Body("Home moment","第 "+app.simulation.Day+" 天 · "+app.simulation.State.name+"和你的小家",84,28,13);
            Row("一起玩","抚摸、滚球、逗猫棒和后院洗澡。","paw",120,()=>{ClosePanel();PlayGuide();});
            Row("找猫","看向当前关注的猫。","find",190,()=>{ClosePanel();app.FindCat();});
            Row("回忆","看看一起生活留下的小片段。","book",260,Journal);
            Row("拍照","保存此刻的房间画面。","camera",330,()=>{ClosePanel();app.Screenshot();});
            Row("设置","声音、视角和界面显示方式。","gear",400,()=>{ClosePanel();ToggleSettings();});
            Body("Home controls","左下拖动走动，其他空白处拖动看四周。",484,30,12);Done();
        }
        public void TickVisibility(float dt,bool holding=false)
        {
            bool shopping=app.boutique!=null&&app.boutique.gameObject.activeInHierarchy;
            Visibility.Advance(dt,holding||Walk.Pressed||Walk.Moving,PanelOpen||shopping||(app.household.Outing!=null&&app.household.Outing.Active)||app.BathActive||app.FeedingActive||app.WandActive||app.window.Transitioning);
            Walk.TickVisual(dt);
        }
        public void SetAutoHide(bool value,bool persist=true)
        {Visibility.AutoHide=value;Visibility.Wake();if(persist){PlayerPrefs.SetInt(AutoHideKey,value?1:0);PlayerPrefs.Save();}}
        static void Fade(CanvasGroup group,bool visible,float dt)
        {group.alpha=Mathf.MoveTowards(group.alpha,visible?1:0,Mathf.Max(0,dt)/.3f);group.interactable=group.blocksRaycasts=visible;}
        bool PositionWarning=>app.window.PositionEnabled&&(app.window.PhysicalDistance>=.5f||app.window.PositionHint.Contains("未获")||app.window.PositionHint.Contains("暂不可用"));
        string ContextHint()
        {
            if(app.FeedingActive)return app.feeding.Hint;
            if(PositionWarning)return app.window.PositionHint;
            if(!string.IsNullOrEmpty(app.Toast))return app.Toast;
            if(app.household.Outing!=null&&app.household.Outing.Active)return app.household.Outing.Hint;
            if(Quiet||Walk.Pressed)return "";
            if(app.WandActive)return "拖动羽毛，松手看看它的回应。";
            if(Time.unscaledTime<guideUntil)return "左下拖动走动 · 其他空白处拖动看四周";
            bool Near(Point p,float height,float distance)
            {
                var v=app.window.ViewerPosition;var screen=app.room.cameraView.WorldToViewportPoint(new Vector3(p.x,height,p.z));
                return Point.Distance(new Point(v.x,v.z),p)<distance&&screen.z>0&&screen.x>.15f&&screen.x<.85f&&screen.y>.12f&&screen.y<.85f&&Room.Clear(new Point(v.x,v.z),Point.Toward(p,new Point(v.x,v.z),.85f));
            }
            if(Near(BathSession.Station,.08f,2.8f))return "轻点洗澡盘，邀它洗个澡。";
            if(Near(Room.IndoorBag,.4f,2.2f)||Near(Room.OutdoorBag,.4f,2.2f))return "轻点猫粮袋，抓一把喂它。";
            if(app.cat!=null&&Near(app.simulation.State.pet,.7f,2.3f))return "轻点猫咪，或顺着身体慢慢抚摸。";
            return "";
        }
        TMP_Text Body(string name,string value,float y,float height,int size=14)
        {return Text(name,panel,value,size,Muted,new Vector2(22,-y),new Vector2(panel.rect.width-44,height),new Vector2(0,1));}
        void Done()
        {var b=Button("回到房间",panel,new Vector2(20,18),new Vector2(panel.rect.width-40,48),Vector2.zero,ClosePanel,Accent);b.GetComponentInChildren<TMP_Text>().color=Paper;}
        Button Row(string title,string subtitle,string glyph,float y,UnityEngine.Events.UnityAction action,string value=null)
        {
            var b=Button(title,panel,new Vector2(18,-y),new Vector2(panel.rect.width-36,62),new Vector2(0,1),action,Mist);RemoveLabel(b);
            Icon(glyph,b.transform,new Vector2(12,-15),new Vector2(30,30),new Vector2(0,1),Accent);
            float width=panel.rect.width-106-(value==null?0:50);
            Text("Action title",b.transform,title,16,Ink,new Vector2(54,-7),new Vector2(width,24),new Vector2(0,1)).fontStyle=FontStyles.Bold;
            Text("Action description",b.transform,subtitle,12,Muted,new Vector2(54,-34),new Vector2(panel.rect.width-112,22),new Vector2(0,1));
            if(value!=null)Text("Current value",b.transform,value,12,Accent,new Vector2(-66,-11),new Vector2(50,22),new Vector2(1,1)).alignment=TextAlignmentOptions.Right;
            return b;
        }
        public void PlayGuide()
        {
            if(!app.simulation.State.adopted||app.LoadError!=null)return;if(app.WandActive)app.CancelWandPlay();if(PanelOpen){ClosePanel();return;}Panel("一起玩一会儿",714);
            Body("Play intro",app.Progress.NextGoal(app.FocusedCatId,app.PlayTuning),84,32,12);
            Row("摸摸它","轻点猫咪，或顺着背慢慢滑动。","pet",198,()=>{ClosePanel();app.window.Find(app.cat.transform.position);app.PetCompanion();app.Save();},app.TriedPlayStep(1)?"试过了":null);
            Row("滚球邀请","把红球滚过去，看看它愿不愿意追。","ball",272,()=>{ClosePanel();app.RollBallForPlay();},app.TriedPlayStep(2)?"试过了":null);
            Row("逗猫连扑","拖动、收圈、松手，试试能连着抓几次。","wand",124,PlayHub);
            Row("轻声唤它","它可以走近，也可以继续自己的事。","paw",346,()=>{ClosePanel();app.CallCompanion();},app.TriedPlayStep(4)?"试过了":null);
            Row("后院洗澡","去后院的浅洗澡盘，邀它洗掉灰尘。","pet",420,()=>{ClosePanel();app.window.Find(new Vector3(BathSession.Station.x,.2f,BathSession.Station.z));app.Notify("走到后院浅洗澡盘旁，轻点它，叫猫咪来洗澡。");});
            Row("后院寻趣","带它逛三个角落，一起停下来发现新气味。","paw",494,()=>{ClosePanel();app.StartGardenOuting();});
            Body("Play progress","小厨房和后院各有猫粮袋。走近点袋子，用右手喂它，左手轻轻摸头。",570,62,12);Done();
        }
        public void PlayHub()
        {
            if(!app.HasCat||app.LoadError!=null)return;app.Progress.Visit(DateTime.Now);
            Panel("逗猫连扑",620);
            Body("Next play goal",app.Progress.NextGoal(app.FocusedCatId,app.PlayTuning),84,46,13);
            var daily=app.Progress.Recommendation(DateTime.Now);
            Row("今日路线",daily.date+" · "+PlayProgress.FacilityName(daily.layout),"wand",142,()=>app.StartDailyChallenge(DateTime.Now));
            Row("自由练习","选一个玩耍角落，随时开始。","paw",216,PlayLayouts);
            Row("过去七天","回头试试前几天的路线。","book",290,PlayHistory);
            Row("自由逗猫","拿起羽毛，慢慢陪它玩。","heart",364,()=>{ClosePanel();app.StartWandPlay();});
            var c=app.Progress.Cat(app.FocusedCatId);
            Body("Learned play",app.simulation.State.name+" · "+PlayProgress.SkillName(c.skillLevel)+"\n拖羽毛带它追逐，等它伏低。把羽毛收进圈，光圈重合时松手。",448,78,13);Done();
        }
        void PlayLayouts()
        {
            Panel("选个玩耍角落",580);Body("Layout intro","每个角落都有自己的纪录。",84,30,13);
            for(int level=0;level<=app.Progress.facilityLevel;level++)
            {int selected=level;Row(PlayProgress.FacilityName(level),level==0?"宽敞的平地，先找找配合的节奏。":level==1?"踩着扑抓垫，练习稳稳落点。":level==2?"引它绕过纸箱，再接上扑抓。":"羽毛在隧道口时隐时现，换一个追逐角度。","wand",128+level*74,()=>app.StartChallenge(selected,27183,PlayProgress.FacilityName(selected)));}
            Done();
        }
        void PlayHistory()
        {
            Panel("过去七天的路线",650);Body("History intro","每条路线都能反复练习。",82,26,12);
            var entries=app.Progress.daily.OrderByDescending(d=>d.date).ToArray();
            for(int i=0;i<entries.Length;i++){var d=entries[i];var button=Row(d.date,PlayProgress.FacilityName(d.layout),"wand",116+i*64,()=>app.StartChallenge(d.layout,d.seed,d.date));((RectTransform)button.transform).sizeDelta=new Vector2(panel.rect.width-36,58);}
            Done();
        }
        public void ToggleSettings()
        {
            if(!app.simulation.State.adopted||app.LoadError!=null)return;if(PanelOpen){ClosePanel();return;}Panel("我的小家",660);
            Row("游戏声音","猫叫、呼噜和轻轻的互动声。","sound",94,()=>{app.simulation.State.sound=!app.simulation.State.sound;app.Save();ClosePanel();ToggleSettings();},app.simulation.State.sound?"开启":"关闭");
            Row("视角与移动","感应、看向速度和回到入口。","phone",166,ViewSettings);
            Row("萌宠小店","认识新伙伴，家里最多住五只猫。","paw",238,()=>{ClosePanel();app.OpenBoutique();});
            bool breedModel=ImportedCatCatalog.ForBreed(app.simulation.State.breedIndex)!=null;
            Row("猫咪外观",breedModel?"当前："+app.simulation.State.name+"的新品种外观":app.UseImportedCat?"当前：灰虎斑":"当前：原品种","pet",310,()=>{ClosePanel();app.ToggleCatModel();if(!breedModel)ToggleSettings();},breedModel?"看看":"切换");
            Row("声音来源","实录猫叫与呼噜的创作者和许可。","book",382,SoundCredits);
            Row("试玩记录","将本机的游玩与成长记录导出。","book",454,app.ExportPlayLog);
            Body("Privacy","资料留在本机 · 感应画面不保存或上传",530,30,11);Done();
        }
        public void Household()
        {
            if(!app.simulation.State.adopted)return;
            Panel("家里的猫咪",620);
            Body("Household intro","点名字去找它；点关注中的猫，查看它的状态。",82,34,12);
            float top=120;
            foreach(var resident in app.household.State.cats.Where(c=>c.life.adopted))
            {
                int id=resident.id;
                Row(resident.life.name,app.CatSummary(id),"paw",top,()=>{if(id==app.FocusedCatId){PetStatus();return;}ClosePanel();app.FocusCat(id);app.Save();},id==app.FocusedCatId?"关注中":null);top+=70;
            }
            if(app.CatCount<WorldSimulation.Capacity)
                Row("认识新伙伴","去萌宠小店，再带一只猫回家。","heart",top,()=>{ClosePanel();app.OpenBoutique();});
            Done();
        }
        public void ViewSettings()
        {
            Panel("视角与移动",660);Body("View instructions","左下方向盘常驻：拖动爪印走动，松手停止。其他空白处拖动看四周，双指调整远近。",86,42);
            var motion=Row("转头感应","转动手机改变观看方向。","phone",140,()=>{app.window.ToggleMotion();ViewSettings();},app.window.MotionEnabled?"开启":"关闭");motion.interactable=app.window.MotionAvailable&&!app.window.PositionEnabled;
            var position=Row("走近感应","小范围移动手机，靠近猫咪。","find",212,()=>{app.window.TogglePosition();ViewSettings();},app.window.PositionEnabled?"开启":"关闭");position.interactable=app.window.PositionAvailable;
            Row("看向速度","轻点切换，找到舒服的转动速度。","reset",284,()=>{app.window.CycleSensitivity();ViewSettings();},app.window.Sensitivity.ToString("F1")+"倍");
            Row("界面显示","自动淡出时，轻点唤回；拖动仍可看四周。","phone",356,()=>{SetAutoHide(!Visibility.AutoHide);ViewSettings();},Visibility.AutoHide?"自动":"常显");
            float w=(panel.rect.width-48)/2;
            Button("回正视角",panel,new Vector2(18,-438),new Vector2(w,46),new Vector2(0,1),()=>{app.RecenterView();ClosePanel();},Mist);
            Button("回到入口",panel,new Vector2(30+w,-438),new Vector2(w,46),new Vector2(0,1),()=>{app.window.ReturnToEntry();ClosePanel();},Mist);
            Button("退到窗外",panel,new Vector2(18,-498),new Vector2(panel.rect.width-36,46),new Vector2(0,1),()=>{ClosePanel();app.window.StepBack();},Mist);
            Body("Motion privacy","走近感应会使用相机追踪位置，画面留在本机。",558,32,11);Done();
        }
        void SoundCredits()
        {Panel("猫咪的声音",520);Body("Recording credits","常规叫声（用户提供）\nMikan · u_r9sp2mg8xc\nEarl · yodguard\nChai / Yuki · sound_garage\nOreo · lazychillzone\n\n原叫声：Luke100000\n呼噜：the_semen_incident\n原有两份录音为 CC0 1.0。\n新增文件的来源记录随项目保存。",88,354,14);Done();}
        public void PetStatus()
        {
            if(!app.simulation.State.adopted||app.LoadError!=null)return;Panel("它的此刻",610);var s=app.simulation.State;
            Body("Bond",Relationship(s),88,60,15);Meter("亲密",s.affection,148);Meter("精力",s.energy,204);Meter("饱腹",100-s.hunger,260);Meter("饮水",100-s.thirst,316);Meter("好心情",100-s.boredom,372);
            Body("Cleanliness",s.dirt<20?"毛毛干干净净的。":s.dirt<60?"身上沾了些灰，可以去后院洗个澡。":"在后院玩脏了，身上有些灰尘。",430,34,12);
            Body("Care note","它会自己吃喝休息；不洗澡也没有惩罚。",472,28,11);Done();
        }
        public void BathGuide()
        {
            if(!app.HasCat||app.LoadError!=null)return;Panel("后院洗个澡",380);
            var s=app.simulation.State;
            Body("Bath invitation",s.name+" · "+(s.dirt<20?"毛毛很干净，也可以陪它洗一洗。":"身上沾了灰尘，邀它来洗个澡吧。"),88,60,15);
            Body("Bath instructions","等它自己走进来，再用花洒打湿、搓泡泡、冲净和毛巾擦干。慢慢来，避开头脸。",156,62,13);
            Button("叫它来洗澡",panel,new Vector2(20,70),new Vector2(panel.rect.width-40,48),Vector2.zero,()=>{ClosePanel();app.BeginBath();},Accent).GetComponentInChildren<TMP_Text>().color=Paper;
            Body("Bath no pressure","困倦或想独处时，它也可以拒绝。",232,26,11);Done();
        }
        void Meter(string name,float value,float y)
        {
            Text(name,panel,name,14,Ink,new Vector2(22,-y),new Vector2(80,24),new Vector2(0,1));
            Text(name+" value",panel,Mathf.RoundToInt(Mathf.Clamp(value,0,100))+" / 100",12,Muted,new Vector2(-110,-y),new Vector2(88,24),new Vector2(1,1)).alignment=TextAlignmentOptions.Right;
            float width=panel.rect.width-44;Surface(name+" track",panel,new Vector2(22,-y-30),new Vector2(width,8),new Vector2(0,1),Mist).GetComponent<Image>().raycastTarget=false;
            Surface(name+" fill",panel,new Vector2(22,-y-30),new Vector2(Mathf.Max(8,width*Mathf.Clamp01(value/100)),8),new Vector2(0,1),Accent).GetComponent<Image>().raycastTarget=false;
        }
        public void Journal()
        {
            if(!app.simulation.State.adopted)return;Panel("一起的小回忆",610);var s=app.simulation.State;
            Body("Shared life",Relationship(s),88,55,15);Body("Noticed",Observation(s),150,58,13);
            Text("Recent label",panel,"最近发生的事",12,Accent,new Vector2(22,-224),new Vector2(panel.rect.width-44,24),new Vector2(0,1)).fontStyle=FontStyles.Bold;
            var events=s.events.AsEnumerable().Reverse().Take(3).ToArray();if(events.Length==0)Body("Memories","第一份回忆，会从一次轻轻的招呼开始。",270,90,14);
            for(int i=0;i<events.Length;i++)
            {
                var e=events[i];var card=Surface("Memory "+i,panel,new Vector2(18,-254-i*92),new Vector2(panel.rect.width-36,82),new Vector2(0,1),Mist);
                Text("Memory day",card,"第 "+(1+(int)(e.time/86400))+" 天",10,Accent,new Vector2(14,-8),new Vector2(card.rect.width-28,18),new Vector2(0,1));
                Text("Memory text",card,MemoryText(e.text),13,Ink,new Vector2(14,-30),new Vector2(card.rect.width-28,44),new Vector2(0,1));
            }
            Done();
        }
        static string MemoryText(string text)
        {
            switch(text)
            {
                case "Curled up in the little bed.":return "蜷进小床，安安静静地休息。";
                case "Batted the little red ball across the room.":return "拨着小红球，在房间里玩了一会儿。";
                case "Watched the world from the window.":return "坐在窗边，看外面的世界。";
                case "Stopped for a little snack.":return "走到食盆旁，吃了一点东西。";
                case "Had a drink of fresh water.":return "喝了几口清水。";
                case "Discovered another quiet corner.":return "又发现了一个安静的小角落。";
                case "A gentle stroke while sleeping.":return "睡着时，收到了一次轻轻的抚摸。";
                case "Shared a quiet moment together.":return "一起度过了一个安静的片刻。";
                case "The ball rolled out of reach.":return "红球滚到了暂时够不到的地方。";
                case "You rolled the red ball.":return "你轻轻滚动红球，发出了一次邀请。";
                case "Woke to a familiar presence.":return "醒来时，身边有熟悉的陪伴。";
                case "A long time away. Settled safely back into home.":return "离开了一段时间，又回到了熟悉的小家。";
                default:return text;
            }
        }
        static string Relationship(WorldState s)
        {return s.affection<25?"还是新朋友。每一次轻轻的招呼，都在慢慢认识彼此。":s.affection<45?"你的出现，正在变成它熟悉的一部分。":s.affection<70?"一些小小的习惯，让你们越来越亲近。":"这个小家里，已经有了彼此的位置。";}
        static string Observation(WorldState s)
        {
            if(s.wandPreference>=3&&s.wandPreference>=s.ballPreference)return "羽毛逗猫棒，渐渐成了它熟悉的游戏。";
            if(s.ballPreference>=4)return "小红球，正在变成你们熟悉的邀请。";
            if(s.windowPreference>=1)return "它开始喜欢窗边安静的时光。";
            return "相处的日子还在展开，留意它自己选择的小习惯。";
        }
        void ErrorPanel()
        {Panel("原来的存档已保留",420);Body("Error","暂时无法读取宠物存档。请保留应用并联系开发者恢复，原文件没有被覆盖。\n\n"+app.LoadError,100,225,14);}
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.B))app.OpenBoutique();
            if(Debug.isDebugBuild&&MomoApp.MobileHudReviewMode&&Input.GetMouseButtonDown(0))
            {
                var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Input.mousePosition},hits);
                Debug.Log("HUD pointer "+Input.mousePosition+" / hits "+string.Join(",",hits.Select(h=>h.gameObject.name)));
            }
        }
        public void Refresh()
        {
            bool shopping=app.boutique!=null&&app.boutique.gameObject.activeInHierarchy;boutiqueHud.Refresh(shopping);safe.gameObject.SetActive(!shopping&&!app.ChallengeActive);if(shopping||app.ChallengeActive)return;
            ApplySafeArea();Canvas.ForceUpdateCanvases();float width=safe.rect.width;identity.sizeDelta=new Vector2(Mathf.Min(138,width-100),44);
            nameText.rectTransform.sizeDelta=new Vector2(identity.sizeDelta.x-48,24);
            bool feedingNow=app.FeedingActive;
            // Reserve the entire thumb input area, not only the visible joystick disc.
            var walkRect=(RectTransform)Walk.transform;
            float walkTop=walkRect.anchoredPosition.y+(1-walkRect.pivot.y)*walkRect.rect.height;
            captionCard.anchoredPosition=new Vector2(16,walkTop+12);
            caption.rectTransform.sizeDelta=new Vector2(width-56,32);
            var state=app.simulation.State;int nameLimit=app.CatCount>1?4:5;nameText.text=(state.name.Length>nameLimit?state.name.Substring(0,nameLimit)+"…":state.name)+(app.CatCount>1?" · "+app.CatCount:"");
            bool bathing=app.BathActive;
            bathCard.gameObject.SetActive(bathing&&!PanelOpen);bathEnd.gameObject.SetActive(bathing&&!PanelOpen);
            if(bathing)
            {
                var b=app.household.Bath;float cardWidth=width-60;
                bathStep.rectTransform.sizeDelta=new Vector2(cardWidth,24);bathZones.rectTransform.sizeDelta=new Vector2(cardWidth,20);
                bathHint.rectTransform.sizeDelta=new Vector2(b.Ready?cardWidth-148:cardWidth,30);
                bathStep.text=b.StepName+(b.Paused?" · 等一等":"");
                bathZones.text=b.Washing?"背部 "+Mathf.RoundToInt(b.ZoneProgress(BathZone.Back)*100)+"%    左侧 "+Mathf.RoundToInt(b.ZoneProgress(BathZone.Left)*100)+"%    右侧 "+Mathf.RoundToInt(b.ZoneProgress(BathZone.Right)*100)+"%":"一次只照顾一只猫，让它自己决定靠近。";
                bathHint.text=app.bath.Handling?app.bath.HandlingHint:b.Hint;
                bathNext.GetComponentInChildren<TMP_Text>().text=b.Phase==BathPhase.Wet?"换上沐浴露":b.Phase==BathPhase.Soap?"换花洒冲净":b.Phase==BathPhase.Rinse?"换毛巾擦干":b.Phase==BathPhase.Dry?"顺着身体轻轻擦干":"慢慢等它准备好";
                bathNext.gameObject.SetActive(b.Washing&&b.Ready);bathNext.interactable=b.Ready&&!app.bath.Handling&&!b.Paused;
            }
            bool inside=app.window.CanInteract;chrome.gameObject.SetActive(inside&&!PanelOpen&&!bathing);
            if(inside&&!introduced){introduced=true;guideUntil=Time.unscaledTime+6;Visibility.Wake();}
            float dt=Time.unscaledDeltaTime;bool awake=!Quiet||!inside;
            Fade(identityFade,awake&&!PanelOpen,dt);Fade(actionFade,awake,dt);
            menuFade.alpha=Mathf.MoveTowards(menuFade.alpha,Quiet?.45f:1,dt/.3f);
            quickAction.gameObject.SetActive(!feedingNow);quickLabel.text=app.WandActive?"收起":"互动";
            peekButton.gameObject.SetActive(app.window.Phase==WindowView.ViewPhase.Outside&&!PanelOpen);peekButton.interactable=state.adopted;
            settings.interactable=state.adopted&&!PanelOpen;modeDot.gameObject.SetActive(app.window.MotionEnabled||app.window.PositionEnabled);
            Satiety.Set(100-state.hunger,true);Bond.Set(state.affection,false);
            Satiety.gameObject.SetActive(feedingNow||Time.unscaledTime<gainsUntil);Bond.gameObject.SetActive(feedingNow||Time.unscaledTime<gainsUntil);
            withdraw.gameObject.SetActive(app.feeding!=null&&app.feeding.Active);
            caption.text=ContextHint();captionCard.gameObject.SetActive(!string.IsNullOrEmpty(caption.text)&&(!Walk.Pressed||feedingNow||PositionWarning));
            debugPanel.SetActive(app.DebugVisible);
            if(app.DebugVisible){app.simulation.RefreshScores();debugText.text="DEVELOPER / F1\n\n"+state.activity+" → "+state.intent+"\n"+app.simulation.Reason+"\n\nEnergy "+state.energy.ToString("F0")+" / Hunger "+state.hunger.ToString("F0")+"\nBond "+state.affection.ToString("F1")+"\nCheckpoint "+state.revision+" / "+(app.SaveFailed?"SAVE FAILED":"local")+"\n"+string.Join("\n",app.simulation.Scores.Take(4).Select(k=>k.Key+" "+k.Value.ToString("F2")));}
        }
    }
}
