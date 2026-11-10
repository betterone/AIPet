using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Momo.Core;

namespace Momo
{
    public sealed class UnityCodec : IStateCodec, IHouseholdCodec
    { public string EncodeHome(HouseholdState state){return JsonUtility.ToJson(state);} public HouseholdState DecodeHome(string json){return JsonUtility.FromJson<HouseholdState>(json);} public string Encode(WorldState state){return JsonUtility.ToJson(state);} public WorldState Decode(string json){return JsonUtility.FromJson<WorldState>(json);} }
    public sealed class MomoApp : MonoBehaviour
    {
        public Simulation simulation;
        public RoomView room;
        public CatView cat;
        public MomoHud hud;
        public WindowView window;
        public PetBoutiqueStage boutique;
        public WandChallengeStage challenge;
        public PlayGarden PlayGarden {get;private set;}
        public PlayTuning PlayTuning {get;private set;}=new PlayTuning();
        public PlayProgress Progress=>household.State.play;
        public bool ChallengeActive=>challenge!=null&&challenge.Active;
        public bool PointerOverUi(Vector2 p)=>OverUi(p);
        public void StartChallenge(int layout=0,uint seed=27183,string title="自由练习")
        {if(!HasCat||LoadError!=null)return;learnedPoseCatId=0;household.Outing?.Cancel();PlayGarden.Cancel();challenge.Open(layout,seed,title);}
        public void StartDailyChallenge(DateTime local)
        {var d=Progress.Recommendation(local);StartChallenge(d.layout,d.seed,d.date+" · "+PlayProgress.FacilityName(d.layout));}
        public void ExportPlayLog()
        {
            try
            {
                string json=JsonUtility.ToJson(Progress,true);
                if(BrowserBridge.DownloadText("Momo-play-log.json",json))
                {Notify("试玩记录已下载到浏览器。");return;}
                var path=Path.Combine(Application.persistentDataPath,"Momo-play-log.json");File.WriteAllText(path,json);Notify("试玩记录已保存到本机应用目录。");Debug.Log("Momo play log: "+path);
            }
            catch(Exception ex){Notify("暂时无法保存试玩记录，请检查剩余空间。");Debug.LogWarning(ex.Message);}
        }
        HouseholdStore store;
        public WorldSimulation household;
        public readonly System.Collections.Generic.Dictionary<int,CatView> cats=new System.Collections.Generic.Dictionary<int,CatView>();
        // A fresh household retains one unadopted record only to preserve the
        // legacy save shape. It is not a resident and must never make the user
        // look as though they already have a cat.
        public int CatCount
        {
            get
            {
                if(household==null)return 0;
                int count=0;foreach(var resident in household.State.cats)if(resident.life.adopted)count++;
                return count;
            }
        }
        public bool HasCat=>CatCount>0;
        public int FocusedCatId=>household?.State.focusedCatId??1;
        int pressedCatId;
        public string CatSummary(int id)
        {
            var c=household.Cat(id);if(c.waitingFor==Activity.Eat)return "在附近等食盆空出来";if(c.waitingFor==Activity.Drink)return "在附近等水盆空出来";foreach(var session in household.Sessions)if(session.first==id||session.second==id)
                return session.phase==SocialPhase.Invitation?"正在留意同伴":session.kind==SocialKind.Chase?"和同伴追逐":session.kind==SocialKind.Sniff?"和同伴打招呼":"在同伴身边休息";
            return c.life.activity==Activity.Sleep?"正在休息":c.life.activity==Activity.Walk?"在"+Room.Area(c.life.pet)+"走动":c.life.activity==Activity.Eat?"正在吃东西":c.life.activity==Activity.Drink?"正在喝水":"在"+Room.Area(c.life.pet)+"自在待着";
        }
        public CatAudio voice;
        public CompanionPlay company;
        public HandFeeding feeding;
        public CompanionHands hands;
        public BathController bath;
        public bool BathActive=>bath!=null&&bath.Active;
        Vector3 foodBagPosition;
        public bool FeedingActive=>feeding!=null&&feeding.Active;
        bool suspended,ready,saveFailed;
        float saveTimer,toastTimer;
        Vector2 downPoint;
        Vector2 lastPoint;
        bool panning;
        bool mouseLooking;
        bool keyboardWalking;
        bool wakingPointer;
        readonly System.Collections.Generic.HashSet<int> movementTouches=new System.Collections.Generic.HashSet<int>();
        bool pinching;
        float lastPinchDistance;
        bool wandMode,wandDragging,wandStrikePending,catInviteShown,awaitingInvitation,focusNewCatOnEntry;
        float wandModeStartedAt,wandStrikeDeadline,lastPlayerActionAt,catStrokeStartedAt,catStrokeTravel;
        Point wandTarget;
        int learnedPoseCatId;float learnedPoseAge;
        public bool LearnedWandActive=>learnedPoseCatId!=0;
        int playSteps;
        public int PlayStepsCompleted => ((playSteps & 1) != 0 ? 1 : 0) + ((playSteps & 2) != 0 ? 1 : 0) + ((playSteps & 4) != 0 ? 1 : 0) + ((playSteps & 8) != 0 ? 1 : 0);
        public bool TriedPlayStep(int step) => (playSteps & step) != 0;
        public bool WandActive => wandMode;
        public string PlayHint
        {
            get
            {
                if(wandMode)return "拖动羽毛，松手看看它会不会扑过去。";
                if(household?.Outing!=null&&household.Outing.Active)return household.Outing.Hint;
                if(awaitingInvitation)return "它把红球带过来了。拖动红球，回应它的邀请。";
                if(simulation.State.activity==Activity.Sleep)return "它睡着了。轻点它，或等它醒来一起玩。";
                if(!TriedPlayStep(1))return "试着沿着它的背慢慢滑动手指。";
                if(!TriedPlayStep(2))return "红球在地上。拖动它，看看猫会怎么追。";
                if(!TriedPlayStep(8))return "点「一起玩」拿逗猫棒，让它跟着羽毛走。";
                if(!TriedPlayStep(4))return "点「一起玩」轻声唤它，给它回应的时间。";
                return "小厨房和后院有猫粮袋。走近抓一把，看看它想不想吃。";
            }
        }
        public string LastGesture {get;private set;}
        string pressedKind;
        public string Toast {get;private set;}
        public string LoadError {get;private set;}
        public double AwaySeconds {get;private set;}
        public bool DebugVisible;
        public bool FastTime;
        public string SavePath { get {return store.DirectoryPath;} }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(SceneManager.GetActiveScene().name=="CatBehaviorStudy" || SceneManager.GetActiveScene().name=="VisualStyleStudy")return;
            if(FindFirstObjectByType<MomoApp>()==null)new GameObject("Momo • a little life").AddComponent<MomoApp>();
        }
        public static bool WandReviewMode=>Debug.isDebugBuild&&Array.IndexOf(Environment.GetCommandLineArgs(),"--wand-challenge-review")>=0;
        public static bool HandAnatomyReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--hand-anatomy-review") >= 0;
        public static bool VisualQualityReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--visual-quality-review") >= 0;
        public static bool MultiCatReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--multi-cat-review") >= 0;
        public static bool NewCatsReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--new-cats-review") >= 0;
        public static bool BoutiqueReview => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--boutique-review") >= 0;
        public static bool ImportedReview => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--imported-cat-review") >= 0;
        public static bool GardenReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--garden-feeding-review") >= 0;
        public static bool BathReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--bath-review") >= 0;
        public static bool QuietHudReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--quiet-hud-review") >= 0;
        public static bool ExplorationReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--exploration-review") >= 0;
        public static bool MobileHudReviewMode => Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--mobile-hud-review") >= 0;
        public bool UseImportedCat => household!=null&&household.Focused.appearance>=0?household.Focused.appearance==1:PlayerPrefs.GetInt("Momo.ImportedCat", 1) == 1;
        void Start()
        {
            Application.targetFrameRate=30;Screen.sleepTimeout=SleepTimeout.SystemSetting;
            var codec=new UnityCodec();
            var tuning=Resources.Load<TextAsset>("PlayTuning");if(tuning!=null)PlayTuning=JsonUtility.FromJson<PlayTuning>(tuning.text);PlayTuning.Validate();
            store=new HouseholdStore(WandReviewMode || HandAnatomyReviewMode || VisualQualityReviewMode || BoutiqueReview || ImportedReview || MobileHudReviewMode || ExplorationReviewMode || GardenReviewMode || BathReviewMode || QuietHudReviewMode || MultiCatReviewMode || NewCatsReviewMode ? Path.Combine(Application.temporaryCachePath,"MomoReview",DateTime.UtcNow.Ticks.ToString()) : Path.Combine(Application.persistentDataPath,"Momo"),codec,codec);
            HouseholdState home=null;
            try{home=store.Load();}catch(Exception ex){LoadError=ex.Message;Debug.LogError("Momo save could not be read: "+ex.Message);}
            if(home==null)home=WorldSimulation.Migrate(Simulation.New(DateTime.UtcNow.Ticks));
            household=new WorldSimulation(home);
            Progress.Abort(DateTime.Now);if(HasCat)Progress.Visit(DateTime.Now);
            foreach(var resident in home.cats)if(resident.appearance<0)resident.appearance=resident.id==1&&PlayerPrefs.GetInt("Momo.ImportedCat",1)==1?1:0;
            simulation=household.Brain(home.focusedCatId);var state=simulation.State;
            company=new CompanionPlay(simulation);feeding=new HandFeeding(simulation);
            if(HasCat)AwaySeconds=household.Resume(DateTime.UtcNow.Ticks);
            room=new GameObject("Pet Home").AddComponent<RoomView>();room.Build();
            room.gameObject.AddComponent<ToyHomeStyle>().Apply(room);
            window=room.cameraView.gameObject.AddComponent<WindowView>();window.Build(room.cameraView);window.Residents=ResidentPositions;
            // Before the first adoption, the room intentionally has no resident
            // actor. The user chooses their first cat in the boutique first.
            foreach(var resident in home.cats)if(resident.life.adopted)CreateCat(resident);
            cats.TryGetValue(home.focusedCatId,out cat);
            hands=new GameObject("Your companion hands").AddComponent<CompanionHands>();hands.Build();
            voice=new GameObject("Cat voice recordings").AddComponent<CatAudio>();voice.Build(state.breedIndex);
            boutique=new GameObject("Pet Boutique 3D Stage").AddComponent<PetBoutiqueStage>();boutique.Build(this);
            hud=new GameObject("A quiet interface").AddComponent<MomoHud>();hud.Build(this);
            bath=new GameObject("Bath care tools").AddComponent<BathController>();bath.Build(this);
            PlayGarden=new GameObject("Growing play garden").AddComponent<PlayGarden>();PlayGarden.Build(this);
            challenge=new GameObject("Wand challenge").AddComponent<WandChallengeStage>();challenge.Build(this);
            BrowserBridge.Prepare();
            ready=true;lastPlayerActionAt=Time.unscaledTime;
            if(LoadError==null&&HasCat){Save();if(store.LastWarning!=null)Notify(store.LastWarning);else if(AwaySeconds>120)Notify("Welcome back. A little life went on while you were away.");}
            else if(LoadError==null&&!HasCat){OpenBoutique();}
            if(BoutiqueReview)gameObject.AddComponent<BoutiqueReview>();
            if(ImportedReview)gameObject.AddComponent<ImportedCatReview>();
            if(MobileHudReviewMode)gameObject.AddComponent<MobileHudReview>();
            if(ExplorationReviewMode)gameObject.AddComponent<ExplorationReview>();
            if(GardenReviewMode)gameObject.AddComponent<GardenFeedingReview>();
            if(BathReviewMode)gameObject.AddComponent<BathReview>();
            if(QuietHudReviewMode)gameObject.AddComponent<QuietHudReview>();
            if(MultiCatReviewMode)gameObject.AddComponent<MultiCatReview>();
            if(VisualQualityReviewMode)gameObject.AddComponent<VisualQualityReview>();
            if(HandAnatomyReviewMode)gameObject.AddComponent<HandAnatomyReview>();
            if(NewCatsReviewMode)gameObject.AddComponent<NewCatsReview>();
            if(WandReviewMode)gameObject.AddComponent<WandChallengeReview>();
        }
        public void ToggleCatModel()
        {
            CancelBath();
            if(!ModelAvailability.CanUseImportedAppearance(simulation.State.breedIndex))
            {
                // A save made on the web can still be opened on iOS. Keep its breed,
                // personality and memories, but never resurrect an excluded web model.
                household.Focused.appearance=0;cat.Build(BreedArchetypes.Get(simulation.State.breedIndex),false);Save();
                Notify("这只新增外观仅在网页版提供；它的小家和回忆都还在。");return;
            }
            if (ImportedCatCatalog.ForBreed(simulation.State.breedIndex) != null)
            { OpenBoutique(simulation.State.breedIndex); return; }
            household.Focused.appearance=UseImportedCat?0:1;
            if(FocusedCatId==1){PlayerPrefs.SetInt("Momo.ImportedCat",household.Focused.appearance);PlayerPrefs.Save();}
            cat.Build(BreedArchetypes.Get(simulation.State.breedIndex),UseImportedCat);
            Save();
            Notify(UseImportedCat ? "Home appearance: imported tabby. Your companion's memories stay with them." : "Home appearance: original breed.");
        }
        public void OpenBoutique(int initialIndex = -1)
        {
            if(boutique==null)return;
            if(ChallengeActive)challenge.Close();
            household?.Outing?.Cancel();CancelBath();
            CancelFeeding();CancelWandPlay();wandStrikePending=false;company?.Cancel();window.PausePosition();
            boutique.Open(initialIndex);
            if(hud!=null)hud.Refresh();
        }
        public void CloseBoutique()
        {
            if(boutique==null)return;
            boutique.Close();window.ResumePosition();
            if(hud!=null)hud.Refresh();
        }
        public void Adopt(string petName,int style,int coat)
        {
            AdoptBreed(petName,coat);
        }
        public void AdoptBreed(string petName,int breedIndex)
        {
            if(LoadError!=null)return;
            bool firstAdoption=!HasCat;
            if(!firstAdoption&&CatCount>=WorldSimulation.Capacity){Notify("家里已有五只猫，先陪陪它们吧。");return;}
            CancelFeeding();company?.Cancel();
            if(firstAdoption)
            {
                var s=simulation.State;var profile=BreedArchetypes.Get(breedIndex);var p=profile.personality;
                s.breedIndex=s.coat=breedIndex;s.personality=new Personality{curiosity=p.curiosity,sociability=p.sociability,playfulness=p.playfulness,independence=p.independence};
                s.name=string.IsNullOrWhiteSpace(petName)?profile.defaultName:petName.Trim();if(s.name.Length>24)s.name=s.name.Substring(0,24);s.adopted=true;
                s.pet=s.target=household.FindFree(new Point(0,2.2f),FocusedCatId);s.route.Clear();s.activity=s.intent=Activity.Idle;s.remaining=80;
                simulation.Record("first","Welcomed "+s.name+" into this little home.");
                if(cats.TryGetValue(FocusedCatId,out var placeholder))Destroy(placeholder.gameObject);
                cats.Remove(FocusedCatId);CreateCat(household.Focused);cat=cats[FocusedCatId];
            }
            else
            {
                var resident=household.Adopt(petName,breedIndex,DateTime.UtcNow.Ticks);CreateCat(resident);FocusCat(resident.id,false);
            }
            Progress.Visit(DateTime.Now);
            focusNewCatOnEntry=true;Save();CloseBoutique();hud.ClosePanel();
            lastPlayerActionAt=Time.unscaledTime;catInviteShown=false;awaitingInvitation=false;
            Notify("带"+simulation.State.name+"回家了。家里现在有 "+CatCount+" 只猫。");
        }
        void CreateCat(CatState resident)
        {
            var view=new GameObject(resident.life.name).AddComponent<CatView>();
            // Breed replacements also apply to existing saves; identity is unchanged.
            view.Build(BreedArchetypes.Get(resident.life.breedIndex),resident.appearance==1);
            view.HouseholdMotion=true;view.HouseholdHeading=resident.heading;
            view.transform.position=new Vector3(resident.life.pet.x,0,resident.life.pet.z);
            view.transform.rotation=Quaternion.Euler(0,resident.heading,0);
            view.gameObject.AddComponent<CatBathEffects>();
            cats[resident.id]=view;
        }
        public void FocusCat(int id,bool find=true)
        {
            if(household==null||!cats.ContainsKey(id))return;
            if(id!=FocusedCatId)
            {
                int previous=FocusedCatId;household.Outing?.Cancel();CancelBath();CancelFeeding();CancelWandPlay();company?.Cancel();household.SetHumanBusy(previous,false);
                household.Focus(id);simulation=household.Brain(id);simulation.State.sound=household.State.sound;cat=cats[id];company=new CompanionPlay(simulation);feeding=new HandFeeding(simulation);
                catInviteShown=awaitingInvitation=false;playSteps=0;
            }
            if(find)window.Find(cat.transform.position);
            hud?.Refresh();
        }
        System.Collections.Generic.IEnumerable<Vector3> ResidentPositions()
        {foreach(var resident in household.State.cats)if(resident.life.adopted)yield return new Vector3(resident.life.pet.x,0,resident.life.pet.z);}
        public void AdvanceWorld(float realSeconds)
        {
            if(household==null){simulation.Advance(realSeconds*Simulation.Speed);return;}
            household.ObserverPosition=!ChallengeActive&&window!=null&&window.CanInteract?(Point?)new Point(window.ViewerPosition.x,window.ViewerPosition.z):null;
            household.SetHumanBusy(FocusedCatId,(household.Outing!=null&&household.Outing.Active&&household.Outing.CatId==FocusedCatId)||BathActive||FeedingActive||wandMode||(company!=null&&company.Active)||pressedKind=="cat");
            household.Advance(realSeconds*Simulation.Speed*(FastTime?30:1),true);
        }
        public void RenderCats(float dt)
        {
            foreach(var resident in household.State.cats)
            {
                if(!resident.life.adopted||!cats.TryGetValue(resident.id,out var view))continue;
                view.HouseholdHeading=resident.heading;view.Tick(resident.life,dt);
                if(learnedPoseCatId==resident.id)
                {
                    learnedPoseAge+=dt;int skill=Progress.Cat(resident.id).skillLevel;float duration=skill>=2?3.4f:1.7f;
                    float age=learnedPoseAge%1.7f;var phase=age<.7f?WandPhase.Ready:age<1.18f?WandPhase.Pounce:WandPhase.Recover;
                    float p=phase==WandPhase.Ready?age/.7f:phase==WandPhase.Pounce?(age-.7f)/.48f:(age-1.18f)/.5f;
                    if(skill>=2&&learnedPoseAge>1.7f&&resident.life.activity!=Activity.Walk)resident.heading=Mathf.Repeat(resident.heading+dt*55,360);
                    view.TickPlay(phase,Mathf.Clamp01(p),0,new Vector3(wandTarget.x,.15f,wandTarget.z));
                    if(learnedPoseAge>=duration)learnedPoseCatId=0;
                }
                view.GetComponent<CatBathEffects>()?.Set(resident.life,household.Bath!=null&&household.Bath.Active&&household.Bath.CatId==resident.id?household.Bath:null);
                if(resident.attentionSeconds>0&&resident.attentionCatId>0&&cats.TryGetValue(resident.attentionCatId,out var other))
                    view.LookAt(other.HeadPosition,.7f);
                else if(resident.attentionSeconds>0&&resident.attentionCatId==0&&window!=null)
                    view.LookAt(window.ViewerPosition,.8f);
            }
        }
        public void PreviewCat(int breedIndex)
        {
            var profile=BreedArchetypes.Get(breedIndex);
            voice.SelectCat(breedIndex);
            cat.Build(profile,UseImportedCat);
            cat.transform.position=new Vector3(0,0,0);
            cat.transform.rotation=Quaternion.Euler(0,180,0);
            cat.React();
            Sound();
        }
        void Update()
        {
            if(!ready||suspended||LoadError!=null)return;
            float dt=Time.unscaledDeltaTime;
            if(ChallengeActive)
            {
                AdvanceWorld(dt);RenderCats(Mathf.Min(dt,.1f));saveTimer+=dt;
                if(saveTimer>=15){Save();saveTimer=0;}
                challenge.Tick(dt);hud.Refresh();return;
            }

            if(boutique!=null&&boutique.gameObject.activeInHierarchy)
            {
                Vector2 bPos=Input.mousePosition;
                bool bDown=Input.GetMouseButtonDown(0),bHeld=Input.GetMouseButton(0),bUp=Input.GetMouseButtonUp(0);
                if(Input.touchCount==1)
                {
                    Touch touch=Input.GetTouch(0);bPos=touch.position;
                    bDown=touch.phase==TouchPhase.Began;bUp=touch.phase==TouchPhase.Ended;
                    bHeld=touch.phase==TouchPhase.Moved||touch.phase==TouchPhase.Stationary||bDown;
                }
                if(simulation.State.adopted)
                {
                    AdvanceWorld(dt);RenderCats(Mathf.Min(dt,.1f));saveTimer+=dt;
                    if(saveTimer>=15){Save();saveTimer=0;}
                }
                boutique.HandlePointer(bPos,bDown,bHeld,bUp,OverUi());
                hud.Refresh();
                if(Input.GetKeyDown(KeyCode.Escape))
                {
                    if(boutique.IsZoomed)boutique.ReturnToGrid();
                    else if(simulation.State.adopted)CloseBoutique();
                }
                return;
            }

            if(simulation.State.adopted)
            {
                AdvanceWorld(dt);saveTimer+=dt;
                if(saveTimer>=15){Save();saveTimer=0;}
                // Opening the app is the act of walking up to the window. Step back remains
                // under the user's control because WindowView approaches only once per session.
                if(!hud.PanelOpen)window.AutoApproach(dt);
                if(window.CanInteract)HandleInput();else{pressedKind=null;panning=false;}
                if(wandMode&&!wandDragging&&Time.unscaledTime-wandModeStartedAt>15f)CancelWandPlay();
                if(wandStrikePending&&(Point.Distance(simulation.State.pet,wandTarget)<.65f||Time.unscaledTime>=wandStrikeDeadline))
                {wandStrikePending=false;cat.TriggerPaw();if(Progress.Cat(FocusedCatId).skillLevel>0&&Point.Distance(simulation.State.pet,wandTarget)<.65f){learnedPoseCatId=FocusedCatId;learnedPoseAge=0;}}
                if(!company.Active&&!FeedingActive&&!BathActive)TryInviteFromCat(Time.unscaledTime);
            }
            RenderCats(Mathf.Min(dt,.1f));room.UpdateWorld(simulation.State,Mathf.Min(dt,.1f));
            household.State.ball=simulation.State.ball;household.State.ballTrapped=simulation.State.ballTrapped;
            window.CatPosition=cat.transform.position;
            window.SetPositionPaused(hud.PanelOpen||BathActive);
            window.Tick(dt,hud.PanelOpen||pressedKind!=null||pinching||wandMode);
            UpdateHandFeeding(Mathf.Min(dt,.1f));
            bath?.Tick(Mathf.Min(dt,.1f));
            UpdateCompany(dt);
            PlayGarden.Tick(Mathf.Min(dt,.1f));
            voice.Locate(cat.transform.position,window.ViewerPosition,simulation.State.sound);
            FocusNewCatIfReady();
            if(panning)window.RenderDrag(dt);
            if(toastTimer>0){toastTimer-=dt;if(toastTimer<=0)Toast=null;}
            hud.TickVisibility(dt,Input.touchCount>0||Input.GetMouseButton(0)||mouseLooking||keyboardWalking);
            hud.Refresh();
            if(Input.GetKeyDown(KeyCode.F1))DebugVisible=!DebugVisible;
            if(Input.GetKeyDown(KeyCode.Escape))hud.HomeMenu();
            if(Input.GetKeyDown(KeyCode.J)&&simulation.State.adopted)hud.Journal();
            if(Input.GetKeyDown(KeyCode.P)&&simulation.State.adopted){hud.ClosePanel();Screenshot();}
        }
        bool OverUi()=>OverUi(Input.touchCount>0?Input.GetTouch(0).position:(Vector2)Input.mousePosition);
        bool OverUi(Touch touch)=>OverUi(touch.position);
        bool OverUi(Vector2 position)
        {
            if(EventSystem.current==null)return false;
            var hits=new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);return hits.Count>0;
        }
        bool WalkTouch(Touch touch)
        {
            if(touch.phase==TouchPhase.Began)
            {if(hud.Walk!=null&&hud.Walk.Hits(touch.position))movementTouches.Add(touch.fingerId);else movementTouches.Remove(touch.fingerId);}
            return movementTouches.Contains(touch.fingerId);
        }
        void UpdateKeyboardMovement()
        {
            Vector2 direction=Vector2.zero;
            if(!hud.PanelOpen&&!BathActive&&!wandMode)
            {
                if(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow))direction.x-=1;
                if(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow))direction.x+=1;
                if(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow))direction.y-=1;
                if(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow))direction.y+=1;
            }
            keyboardWalking=direction.sqrMagnitude>.001f;
            if(keyboardWalking)
            {
                // A key press takes over from the on-screen stick. Releasing it always
                // stops, so desktop input cannot leave the observer walking.
                hud.Walk?.Clear();window.SetMove(direction.normalized);hud.Visibility.Wake();
            }
            else if(Input.touchCount==0&&!hud.Walk.Moving)window.SetMove(Vector2.zero);
        }
        bool HandleMouseLook()
        {
            bool down=Input.GetMouseButtonDown(1)||Input.GetMouseButtonDown(2);
            bool held=Input.GetMouseButton(1)||Input.GetMouseButton(2);
            bool up=Input.GetMouseButtonUp(1)||Input.GetMouseButtonUp(2);
            Vector2 pointer=Input.mousePosition;
            if(down&&OverUi(pointer))return false;
            if(down){mouseLooking=true;pressedKind=null;panning=true;wakingPointer=false;lastPoint=pointer;}
            if(mouseLooking&&held){window.Drag(pointer-lastPoint);lastPoint=pointer;return true;}
            if(mouseLooking&&up){mouseLooking=false;panning=false;pressedKind=null;return true;}
            return mouseLooking;
        }
        void HandleInput()
        {
            UpdateKeyboardMovement();
            if(BathActive)
            {
                if(hud.PanelOpen||Input.touchCount>1){bath.ReleasePointer();return;}
                Vector2 p=Input.mousePosition;bool bathDown=Input.GetMouseButtonDown(0),bathHeld=Input.GetMouseButton(0),bathUp=Input.GetMouseButtonUp(0);
                if(Input.touchCount==1){var t=Input.GetTouch(0);p=t.position;bathDown=t.phase==TouchPhase.Began;bathUp=t.phase==TouchPhase.Ended||t.phase==TouchPhase.Canceled;bathHeld=!bathUp;}
                HandlePointer(p,bathDown,bathHeld,bathUp,OverUi(p));return;
            }
            if(hud.PanelOpen||Input.touchCount>2||(Input.touchCount==1&&Input.GetTouch(0).phase==TouchPhase.Canceled))
            {if(Input.touchCount==1&&Input.GetTouch(0).phase==TouchPhase.Canceled)hud.Walk?.Release(Input.GetTouch(0).fingerId);pressedKind=null;panning=false;if(wandMode)CancelWandPlay();return;}
            if(Input.touchCount==2)
            {
                Touch a=Input.GetTouch(0),b=Input.GetTouch(1);
                bool aWalk=WalkTouch(a),bWalk=WalkTouch(b);
                if(aWalk||bWalk)
                {
                    pinching=false;
                    if(aWalk&&bWalk)return;
                    Touch look=aWalk?b:a;
                    bool lookDown=look.phase==TouchPhase.Began,lookUp=look.phase==TouchPhase.Ended||look.phase==TouchPhase.Canceled;
                    HandlePointer(look.position,lookDown,!lookUp,lookUp,OverUi(look));return;
                }
                if(wandMode)CancelWandPlay();pressedKind=null;panning=false;
                if(OverUi(a)||OverUi(b)){pinching=false;return;}
                float distance=Vector2.Distance(a.position,b.position);
                if(!pinching||a.phase==TouchPhase.Began||b.phase==TouchPhase.Began){pinching=true;lastPinchDistance=distance;return;}
                window.Pinch(distance-lastPinchDistance);lastPinchDistance=distance;return;
            }
            pinching=false;
            Vector2 now=Input.mousePosition;
            bool down=Input.GetMouseButtonDown(0),held=Input.GetMouseButton(0),up=Input.GetMouseButtonUp(0);
            if(Input.touchCount==0)
            {
                if(!OverUi(now))
                {
                    float wheel=Input.mouseScrollDelta.y;
                    if(Mathf.Abs(wheel)>.001f)window.Pinch(wheel*42f);
                }
                if(HandleMouseLook())return;
            }
            if(Input.touchCount==1)
            {
                Touch touch=Input.GetTouch(0);if(WalkTouch(touch)){pressedKind=null;panning=false;return;}now=touch.position;
                down=touch.phase==TouchPhase.Began;up=touch.phase==TouchPhase.Ended;
                held=touch.phase==TouchPhase.Moved||touch.phase==TouchPhase.Stationary||down;
            }
            HandlePointer(now,down,held,up,OverUi());
        }
        // Shared touch/mouse path, also exercised by editor regression checks.
        public void HandlePointer(Vector2 now,bool down,bool held,bool up,bool overUi)
        {
            if(down){wakingPointer=!overUi&&(hud?.Visibility.Wake()??false);if(overUi)hud?.Visibility.Wake();}
            if(BathActive){bath.HandlePointer(now,down,held,up,overUi);return;}
            if(wandMode){HandleWandPointer(now,down,held,up,overUi);return;}
            if(!overUi&&room!=null&&room.cameraView!=null&&cat!=null)
            {
                var lookRay=room.cameraView.ScreenPointToRay(now);
                if(new Plane(Vector3.up,new Vector3(0,.35f,0)).Raycast(lookRay,out float lookEnter))
                    cat.LookAt(lookRay.GetPoint(lookEnter),.85f);
            }
            if(down)
            {
                pressedKind=null;panning=false;if(overUi)return;
                downPoint=lastPoint=now;pressedKind="view";pressedCatId=0;
                if(!wakingPointer&&Physics.Raycast(room.cameraView.ScreenPointToRay(downPoint),out RaycastHit hit))
                {var target=hit.collider.GetComponent<TouchTarget>();pressedKind=target!=null?target.kind:hit.collider.name=="Floor"?"floor":"view";
                    var hitCat=hit.collider.GetComponentInParent<CatView>();
                    if(hitCat!=null)foreach(var pair in cats)if(pair.Value==hitCat){pressedCatId=pair.Key;FocusCat(pair.Key,false);break;}}
                if(pressedKind=="cat-head")pressedKind="cat";
                if(pressedKind=="cat"){if(household!=null)household.SetHumanBusy(FocusedCatId,true);catStrokeStartedAt=Time.unscaledTime;catStrokeTravel=0;}
            }
            float threshold=Mathf.Max(12,Screen.height*.012f);
            if((held||up)&&pressedKind!=null)
            {
                if((pressedKind=="floor"||pressedKind=="view")&&(panning||Vector2.Distance(now,downPoint)>threshold))
                {window.Drag(now-(panning?lastPoint:downPoint));panning=true;}
                if(pressedKind=="cat")
                {
                    float segment=Vector2.Distance(now,lastPoint);
                    if(held&&!overUi&&segment>1f)
                    {catStrokeTravel+=segment;cat.Stroke(Mathf.Clamp01(1f-segment/Mathf.Max(1f,Screen.height*.08f)));
                        if(hands!=null&&hands.StrokeAge>.8f&&Vector3.Distance(window.ViewerPosition,cat.HeadPosition)<2.3f)hands.StartStroke(room.cameraView);}
                }
                if(pressedKind=="food-hand"&&held&&!overUi)hands?.Drag(now-lastPoint);
                lastPoint=now;
            }
            if(up&&pressedKind!=null)
            {
                float d=Vector2.Distance(now,downPoint);
                if(wakingPointer&&d<threshold){wakingPointer=false;pressedKind=null;panning=false;LastGesture="wake / tap";return;}
                wakingPointer=false;
                LastGesture=pressedKind+" / "+(panning?"pan":pressedKind=="cat"&&d>=threshold?"stroke":"tap")+" / "+d.ToString("F0")+"px";
                Debug.Log("Momo gesture: "+LastGesture+" / view "+window.Angles);
                var ray=room.cameraView.ScreenPointToRay(now);var plane=new Plane(Vector3.up,Vector3.zero);
                bool changed=false;
                if(!panning&&!overUi&&pressedKind=="play-wand"&&d<threshold){StartChallenge();}
                else if(!panning&&!overUi&&pressedKind!=null&&pressedKind.StartsWith("play-facility-")&&d<threshold){if(int.TryParse(pressedKind.Substring(14),out int level))StartChallenge(level,27183,PlayProgress.FacilityName(level));}
                else if(!panning&&!overUi&&pressedKind=="bath"&&d<threshold){hud.BathGuide();}
                else if(!panning&&!overUi&&(pressedKind=="indoor-food"||pressedKind=="outdoor-food")&&d<threshold)
                {GrabFood(pressedKind=="indoor-food"?Room.IndoorBag:Room.OutdoorBag);}
                else if(!panning&&!overUi&&pressedKind=="cat")
                {
                    float duration=Mathf.Max(.01f,Time.unscaledTime-catStrokeStartedAt);
                    if(d<threshold){PetCompanion();changed=true;}
                    else if(duration>=.25f&&catStrokeTravel>=threshold)
                    {
                        float speed=catStrokeTravel/duration/Mathf.Max(1,Screen.height);
                        if(speed>.8f)Notify("它抖了抖耳朵。试着更慢、更轻地摸它。");
                        else{PetCompanion();changed=true;}
                    }
                }
                else if(!FeedingActive&&!panning&&!overUi&&(pressedKind=="ball"||(pressedKind=="floor"&&d<threshold))&&plane.Raycast(ray,out float enter))
                {
                    var p=ray.GetPoint(enter);var point=new Point(p.x,p.z);
                    if(pressedKind=="ball")
                    {
                        if(d<15)point=simulation.State.ballTrapped?new Point(.7f,-.8f):new Point(simulation.State.ball.x-.7f,simulation.State.ball.z+.5f);
                        company?.Cancel();household.RollBall(point);Notify("红球滚出去了，附近的猫正在留意它。");Sound();
                        awaitingInvitation=false;lastPlayerActionAt=Time.unscaledTime;
                        if(!simulation.State.ballTrapped)playSteps|=2;
                    }
                    else
                    {
                        // Empty floor is observation space, not a movement command.
                        lastPlayerActionAt=Time.unscaledTime;
                        pressedKind=null;return;

                    }
                    changed=true;
                }
                pressedKind=null;panning=false;if(changed)Save();
            }
        }
        public void GrabFood(Point bag)
        {
            if(BathActive)CancelBath();
            if(!simulation.State.adopted||FeedingActive)return;
            var eye=window.ViewerPosition;var from=new Point(eye.x,eye.z);
            var near=Point.Toward(bag,from,.85f);
            if(Point.Distance(from,bag)>2.2f||!Room.Clear(from,near))
            {Notify("先走近猫粮袋，再点袋子抓一把。");return;}
            household.Outing?.Cancel();CancelWandPlay();company?.Cancel();hands.ResetPose();
            foodBagPosition=new Vector3(bag.x,0,bag.z);feeding.Grab();lastPlayerActionAt=Time.unscaledTime;
        }
        public void WithdrawFood(){feeding?.Withdraw();}
        public void CancelFeeding()
        {feeding?.Cancel();if(cat!=null)cat.SetHandFood(Vector3.zero,false);hands?.ResetPose();window?.SetCarePosture(false);}
        public void UpdateHandFeeding(float dt)
        {
            if(BathActive)return;
            if(hands==null||feeding==null||cat==null)return;
            window.SetCarePosture(FeedingActive||hands.StrokeAge<1.5f);
            hands.Locate(room.cameraView,dt,cat.FoodStandOff);
            if(feeding.Holding)foreach(var resident in household.State.cats)
                if(resident.id!=FocusedCatId&&resident.life.activity!=Activity.Sleep&&resident.life.hunger>=25&&Point.Distance(resident.life.pet,hands.CatTarget)<4.5f&&Room.Clear(resident.life.pet,hands.CatTarget))
                    cats[resident.id].LookAt(hands.Palm,.65f);
            if(FeedingActive&&(!window.CanInteract||hud.PanelOpen||wandMode))CancelFeeding();
            var before=feeding.Phase;float hunger=simulation.State.hunger,bond=simulation.State.affection;
            bool visible=hands.Accessible&&Room.Clear(simulation.State.pet,hands.CatTarget);
            var foodPoint=hands.FoodPoint;
            var towardFood=Vector3.ProjectOnPlane(foodPoint-cat.transform.position,Vector3.up).normalized;
            bool contact=Vector3.Distance(cat.transform.position,new Vector3(hands.CatTarget.x,0,hands.CatTarget.z))<.24f
                &&Vector3.Dot(cat.transform.forward,towardFood)>.9f
                &&Vector3.Distance(cat.MouthPosition,foodPoint)<.075f&&hands.FoodContactClear;
            feeding.Update(dt,hands.CatTarget,hands.Steady,visible,contact);
            bool attend=feeding.Phase==FeedingPhase.Sniff||feeding.Phase==FeedingPhase.Eat||feeding.Phase==FeedingPhase.Pause||feeding.Phase==FeedingPhase.Thanks;
            cat.SetHandFood(foodPoint,attend&&Point.Distance(simulation.State.pet,hands.CatTarget)<.3f,false,feeding.Holding&&visible&&Point.Distance(simulation.State.pet,hands.CatTarget)<3);
            if(feeding.Holding&&visible&&Point.Distance(simulation.State.pet,hands.CatTarget)<4.5f)cat.LookAt(foodPoint,1);
            if((feeding.Phase==FeedingPhase.Sniff&&before!=FeedingPhase.Sniff)||(feeding.Phase==FeedingPhase.Offer&&before==FeedingPhase.TurnPalm))window.FrameNearCompanion();
            if(feeding.Phase==FeedingPhase.Thanks&&before!=FeedingPhase.Thanks)
            {cat.React();if(simulation.State.affection>=25)cat.Stroke(1);voice.Play(CatVoice.Purr,simulation.State.sound);}
            float nutrition=hunger-simulation.State.hunger,gain=simulation.State.affection-bond;
            if(nutrition>0||gain>0){hud.ShowGain(nutrition,gain);voice.Play(CatVoice.Purr,simulation.State.sound);Save();}
            hands.Render(room.cameraView,cat,feeding,foodBagPosition,dt);
        }
        public void StartWandPlay()
        {
            if(!simulation.State.adopted||LoadError!=null)return;
            household.Outing?.Cancel();
            CancelBath();
            CancelFeeding();hud.ClosePanel();
            window.Find(cat.transform.position);
            pressedKind=null;panning=false;pinching=false;
            company?.Cancel();
            wandMode=true;wandDragging=false;wandModeStartedAt=lastPlayerActionAt=Time.unscaledTime;
            wandTarget=Room.Nearest(new Point(simulation.State.pet.x+.6f,simulation.State.pet.z-.45f));
            room.MoveFeather(wandTarget);room.ShowFeather(true);
            Notify("逗猫棒拿好了。按住画面拖动羽毛，松开看看它怎么回应。");
        }
        public void FocusNewCatIfReady()
        {
            if(!focusNewCatOnEntry||!window.CanInteract)return;
            focusNewCatOnEntry=false;
            window.Find(cat.transform.position);
        }
        public void CancelWandPlay()
        {
            wandMode=false;wandDragging=false;
            if(room!=null)room.ShowFeather(false);
        }
        public bool TryInviteFromCat(float now)
        {
            if(company!=null&&company.Active||household.Outing!=null&&household.Outing.Active)return false;
            if((household.State.toyOwnerId!=0&&household.State.toyOwnerId!=FocusedCatId)||catInviteShown||wandMode||pressedKind!=null||panning||pinching||hud.PanelOpen||!window.CanInteract||now-lastPlayerActionAt<=45f||!simulation.InviteFromCat())return false;
            household.State.toyOwnerId=FocusedCatId;household.State.ball=simulation.State.ball;household.State.ballTrapped=simulation.State.ballTrapped;
            catInviteShown=true;awaitingInvitation=true;lastPlayerActionAt=now;
            cat.TriggerPaw();Notify("它把红球拨到你面前，像是在邀请你一起玩。");Save();
            return true;
        }
        void HandleWandPointer(Vector2 now,bool down,bool held,bool up,bool overUi)
        {
            if(down&&!overUi){wandDragging=true;wandModeStartedAt=lastPlayerActionAt=Time.unscaledTime;}
            if(!wandDragging)return;
            var ray=room.cameraView.ScreenPointToRay(now);
            if(new Plane(Vector3.up,new Vector3(0,.46f,0)).Raycast(ray,out float enter))
            {
                Vector3 world=ray.GetPoint(enter);
                wandTarget=Room.Nearest(new Point(world.x,world.z));
                room.MoveFeather(wandTarget);
                cat.LookAt(new Vector3(wandTarget.x,.65f,wandTarget.z),1f);
            }
            foreach(var resident in household.State.cats)
                if(resident.life.activity!=Activity.Sleep&&resident.life.energy>=20&&Point.Distance(resident.life.pet,wandTarget)<5&&Room.Clear(resident.life.pet,wandTarget))
                    cats[resident.id].LookAt(new Vector3(wandTarget.x,.65f,wandTarget.z),.7f);
            if(!up)return;
            bool readyToPlay=!overUi&&simulation.State.activity!=Activity.Sleep&&simulation.State.energy>=20;
            if(!overUi)
            {
                string response=simulation.PlayWithWand(wandTarget);
                Notify(response);
                if(readyToPlay)
                {
                    playSteps|=8;wandStrikePending=true;wandTarget=simulation.State.target;
                    wandStrikeDeadline=Time.unscaledTime+5f;Sound();Save();
                }
            }
            CancelWandPlay();
            lastPlayerActionAt=Time.unscaledTime;
        }
        public void PetCompanion()
        {
            lastPlayerActionAt=Time.unscaledTime;
            if(simulation.State.adopted&&Vector3.Distance(window.ViewerPosition,cat.HeadPosition)>2.3f){Notify("先走近它，再轻轻摸摸头。");return;}
            hands?.StartStroke(room.cameraView);
            if(company!=null&&company.Active){company.Found(new Point(window.ViewerPosition.x,window.ViewerPosition.z));voice.Play(CatVoice.Found,simulation.State.sound);}
            var profile=cat.CurrentProfile??BreedArchetypes.Get(simulation.State.breedIndex);
            if(simulation.State.adopted)
            {
                float affection=simulation.State.affection;
                string response=simulation.Pet();
                if(simulation.State.affection>affection)
                {hud.ShowGain(0,simulation.State.affection-affection);playSteps|=1;cat.React();voice.Play(CatVoice.Purr,simulation.State.sound);Notify(response=="A sleepy little purr."?"它睡梦中轻轻呼噜了一声。":"它眯起眼睛，靠近了你的手。");}
                else Notify(response=="A little space, please."?"它往旁边退了一步，想先有点自己的空间。":"它眨眨眼，已经收到你的招呼。");
            }
            else if(profile.reactions!=null&&profile.reactions.Length>0)
            {cat.React();Sound();Notify(profile.reactions[UnityEngine.Random.Range(0,profile.reactions.Length)]);}
            else {cat.React();Sound();Notify("它轻轻靠近你的手。");}
        }
        string BallResponse()
        {
            if(simulation.State.ballTrapped)return "红球滚进角落了。轻点红球，把它带回来。";
            if(simulation.State.activity==Activity.Sleep||simulation.State.energy<20)return "它现在想休息，红球还在等它。";
            return "它盯住红球，准备追过去。";
        }
        public void RollBallForPlay()
        {
            if(!simulation.State.adopted)return;
            awaitingInvitation=false;lastPlayerActionAt=Time.unscaledTime;
            var pet=simulation.State.pet;
            var destination=Momo.Core.Room.Nearest(new Point(pet.x+.9f,pet.z+.35f));
            company?.Cancel();household.RollBall(destination);
            if(!simulation.State.ballTrapped)playSteps|=2;
            window.Find(cat.transform.position);
            Notify(BallResponse());Sound();Save();
        }
        public void StartGardenOuting()
        {
            CancelWandPlay();company?.Cancel();CancelFeeding();PlayGarden.Cancel();
            if(BathActive){Notify("先完成这次洗澡，再一起去寻趣。");return;}
            if(household.StartOuting(FocusedCatId))Notify(household.Outing.Hint);
            else Notify("等它吃饱、休息好，再邀它一起去后院。");
        }
        public void CallCompanion()
        {
            if(!simulation.State.adopted)return;
            household.Outing?.Cancel();
            lastPlayerActionAt=Time.unscaledTime;
            if(simulation.State.activity==Activity.Sleep)
            {Notify("它正在睡觉，醒来后再叫它吧。");return;}
            company?.Cancel();
            Vector3 eye=window.ViewerPosition;
            cat.LookAt(eye,.8f);bool approached=household.Call(FocusedCatId,new Point(eye.x,eye.z));
            playSteps|=4;
            window.Find(cat.transform.position);
            Notify(approached?"它注意到了你的呼唤，愿意走近看看。":"它听到了，想先做自己的事。");Save();
        }
        public void TeaseCompanion()
        {
            var profile=cat.CurrentProfile??BreedArchetypes.Get(simulation.State.breedIndex);
            if(room!=null&&room.cameraView!=null)cat.LookAt(room.cameraView.transform.position,1f);
            cat.TriggerYawn();Sound();
            Notify(profile.defaultName+" swivels its ears forward, following you closely.");
        }
        public void StretchCompanion()
        {
            cat.TriggerStretch();Sound();
            var profile=cat.CurrentProfile??BreedArchetypes.Get(simulation.State.breedIndex);
            Notify(profile.defaultName+" arches its back in a luxurious, contented stretch.");
        }
        public void FindCat()
        {
            window.Find(cat.transform.position);
            bool visible=room.Unobstructed(window.ViewerPosition,cat.transform.position+Vector3.up*.6f);
            voice.Play(CatVoice.Peek,simulation.State.sound);
            Notify(visible?"猫咪在这边，按方向键慢慢靠近它。":"它在"+Room.Area(new Point(cat.transform.position.x,cat.transform.position.z))+"，穿过门洞、绕开家具找找它。" );
        }
        public void RecenterView(){window.Recenter();Notify("Window centered. Hold your phone comfortably.");}
        public void Notify(string text){Toast=text;toastTimer=7;}
        public void Sound(){voice?.Play(CatVoice.Invite,simulation.State.sound);}
        void UpdateCompany(float dt)
        {
            if(household.Outing!=null&&household.Outing.Active)return;
            if(company==null||!simulation.State.adopted||BathActive)return;
            if(!company.Active&&pressedKind!=null)return;
            Vector3 viewer=window.ViewerPosition,pet=cat.transform.position+Vector3.up*.6f;
            Vector3 screen=room.cameraView.WorldToViewportPoint(pet);
            bool visible=room.Unobstructed(viewer,pet);
            bool observing=screen.z>0&&screen.x>.08f&&screen.x<.92f&&screen.y>.1f&&screen.y<.9f;
            company.Update(dt,new Point(viewer.x,viewer.z),visible,observing,window.CanInteract&&!hud.PanelOpen&&!wandMode&&!FeedingActive);
            if(company.IsPeeking||company.Phase==CompanyPhase.Found)cat.LookAt(viewer,1);
            if(company.Cue!=CatVoice.None)
            {
                voice.Locate(cat.transform.position,viewer,simulation.State.sound);voice.Play(company.Cue,simulation.State.sound);
                if(company.Cue==CatVoice.Invite){awaitingInvitation=false;Notify("它轻轻叫了一声，回头看你，像在邀请你跟上。" );}
                if(company.Cue==CatVoice.Peek)Notify("听，它又探出头了。慢慢靠近看看。" );
                if(company.Cue==CatVoice.Found){cat.React();Notify("找到啦。它又凑过来，想和你亲近一会儿。" );Save();}
            }
        }
        public void Save()
        {
            if(LoadError!=null||!simulation.State.adopted)return;
            try{household.State.sound=simulation.State.sound;household.Checkpoint(DateTime.UtcNow.Ticks);store.Save(household.State,BathActive?household.Bath.CatId:0);saveFailed=false;}
            catch(Exception ex){saveFailed=true;Notify("Could not save. Keep the app open and free some storage.");Debug.LogError("Momo save failed: "+ex.Message);}
        }
        public bool SaveFailed {get{return saveFailed;}}
        public void BeginBath()
        {
            if(!HasCat||BathActive||LoadError!=null)return;
            var eye=window.ViewerPosition;var from=new Point(eye.x,eye.z);
            if(!window.CanInteract||Point.Distance(from,BathSession.Station)>2.8f||!Room.Clear(from,Point.Toward(BathSession.Station,from,.9f)))
            {Notify("走到后院洗澡盘旁，再叫它过来。");return;}
            household.Outing?.Cancel();CancelFeeding();CancelWandPlay();company?.Cancel();pressedKind=null;panning=pinching=false;hud.Walk?.Clear();movementTouches.Clear();
            household.SetHumanBusy(FocusedCatId,false);
            household.ObserverPosition=from; // Use the current view after care returns, not the previous frame near the cat.
            if(!household.TryBeginBath(FocusedCatId,out var why)){Notify(why);return;}
            hands.ResetPose();lastPlayerActionAt=Time.unscaledTime;Notify(household.Bath.Hint);hud.Refresh();
        }
        public void CancelBath(bool quiet=true){bath?.Cancel(quiet);}
        void Suspend(bool value)
        {
            if(!ready||suspended==value)return;
            if(value&&ChallengeActive)challenge.Close(true);
            if(value)learnedPoseCatId=0;
            pressedKind=null;panning=false;mouseLooking=false;keyboardWalking=false;pinching=false;wandStrikePending=false;CancelBath();CancelFeeding();CancelWandPlay();company?.Cancel();hud.Walk?.Clear();movementTouches.Clear();window.ResumeView();
            if(value){household?.CancelTransient();window.PausePosition();Save();suspended=true;}
            else
            {
                suspended=false;hud.Visibility.Wake();window.ResumePosition();lastPlayerActionAt=Time.unscaledTime;if(simulation.State.adopted&&LoadError==null){AwaySeconds=household.Resume(DateTime.UtcNow.Ticks);RenderCats(.033f);Save();if(AwaySeconds>120)Notify("Welcome back. Your companion has been keeping busy.");}
            }
        }
        void OnApplicationPause(bool pause){Suspend(pause);}
        void OnApplicationFocus(bool focus){Suspend(!focus);}
        public void SuspendForReview(bool value){if(Debug.isDebugBuild&&(BathReviewMode||WandReviewMode))Suspend(value);}
        void OnApplicationQuit(){if(ready){household.Outing?.Cancel();if(ChallengeActive)challenge.Close();CancelBath();CancelFeeding();window.PausePosition();company?.Cancel();Save();}}
        void OnDestroy(){if(voice!=null)Destroy(voice.gameObject);if(hands!=null)Destroy(hands.gameObject);}
        public void PreviewOffline()
        {household.Resume(household.State.lastUtcTicks+TimeSpan.FromHours(6).Ticks);RenderCats(.033f);simulation.Record("debug","Developer test: advanced six hours.");Save();Notify("Developer test: six hours simulated.");}
        public void Screenshot()
        {
            string name="Momo-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png";
            if(BrowserBridge.DownloadScreenshot(name))
            {Notify("照片已下载到浏览器。");return;}
            string path=Path.Combine(Application.persistentDataPath,name);ScreenCapture.CaptureScreenshot(path);Notify("Photo saved on this device.");Debug.Log("Momo photo: "+path);
        }
    }
}
