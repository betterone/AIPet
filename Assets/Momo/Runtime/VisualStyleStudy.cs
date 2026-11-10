using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Momo.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Momo
{
    // A reversible art-direction sample. Uses the production cat/Simulation, never SaveStore/PlayerPrefs.
    public sealed class VisualStyleStudy : MonoBehaviour
    {
        RoomView current, proposed;
        CatView cat;
        Simulation simulation;
        Camera view;
        bool toy = true, close, reviewing;
        string output;
        readonly List<Vector3> overviewPoints = new List<Vector3>();
        Bounds overviewBounds;
        bool PhoneUi => Application.isMobilePlatform || Array.IndexOf(Environment.GetCommandLineArgs(),"--visual-style-phone")>=0;
        float UiScale => PhoneUi?Screen.width/390f:1;
        float UiTop => (Screen.height-Screen.safeArea.yMax)/UiScale+12;
        float UiBottom => Screen.safeArea.yMin/UiScale;

        void Start()
        {
            Application.targetFrameRate = 30;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            current = new GameObject("A — current room").AddComponent<RoomView>(); current.Build();
            Cutaway(current); current.cameraView.enabled = false;
            proposed = new GameObject("B — sunny toy home").AddComponent<RoomView>(); proposed.Build();
            Cutaway(proposed); proposed.cameraView.enabled = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) Destroy(listener);
            proposed.gameObject.AddComponent<ToyHomeStyle>().Apply(proposed,true);
            bool first=true;
            foreach(var r in proposed.GetComponentsInChildren<Renderer>())
            {
                var b=r.bounds;if(first){overviewBounds=b;first=false;}else overviewBounds.Encapsulate(b);
                for(int corner=0;corner<8;corner++)overviewPoints.Add(b.center+Vector3.Scale(b.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
            }
            view = new GameObject("Same lens for A and B").AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.allowHDR = true; view.allowMSAA = true; view.nearClipPlane = .1f; view.farClipPlane = 100;
            cat = new GameObject("Same production tabby").AddComponent<CatView>(); cat.Build(BreedArchetypes.Get(0), true);
            if (cat.Imported == null || !cat.Imported.Ready) throw new Exception("Production tabby is required; no fallback comparison.");
            ResetLife(); ApplyStyle(true); Frame(false);
            reviewing = Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--visual-style-review") >= 0;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../artifacts/visual-style-study"));
            if (reviewing) StartCoroutine(Review());
        }

        static void Cutaway(RoomView room)
        {
            // The same removable front facade in BOTH samples makes the interior comparable.
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Outside wall") || t.name.StartsWith("Deep window") ||
                    t.name.StartsWith("Outer window") || t.name == "Window lintel" ||
                    t.name == "Broad outside sill" || t.name == "Open sage shutter" || t.name == "Shutter slat" ||
                    t.name == "Playroom front wall") t.gameObject.SetActive(false);
        }

        void ApplyStyle(bool next)
        {
            toy=next;current.gameObject.SetActive(!toy);proposed.gameObject.SetActive(toy);
            current.cameraView.enabled=false;proposed.cameraView.enabled=false;
            // RoomView creates its light outside its root. Explicitly isolate BOTH lighting rigs.
            current.sun.enabled=!toy;proposed.sun.enabled=toy;
            RenderSettings.ambientMode=toy?AmbientMode.Trilight:AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.68f,.72f,.75f);
            RenderSettings.ambientSkyColor=new Color(.47f,.56f,.64f);
            RenderSettings.ambientEquatorColor=new Color(.38f,.42f,.36f);
            RenderSettings.ambientGroundColor=new Color(.2f,.2f,.16f);
            var sun=toy?proposed.sun:current.sun;
            sun.intensity=toy?1.1f:1.15f;sun.shadowStrength=toy?.55f:.35f;
            sun.color=toy?new Color(1,.96f,.84f):new Color(1,.91f,.72f);
            sun.transform.rotation=toy?Quaternion.Euler(48,-32,0):Quaternion.Euler(40,-30,0);
            view.backgroundColor=toy?new Color(.72f,.85f,.89f):new Color(.89f,.87f,.81f);
            QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowDistance=50;
        }
        void ResetLife()
        {
            var state=Simulation.New(DateTime.UtcNow.Ticks);state.adopted=true;state.pet=new Point(.4f,-.6f);state.target=state.pet;
            state.activity=Activity.Idle;state.remaining=100;simulation=new Simulation(state);
            cat.transform.position=new Vector3(.4f,0,-.6f);cat.transform.rotation=Quaternion.Euler(0,160,0);
        }
        void Frame(bool detail)
        {
            close=detail;
            view.orthographic=!detail;
            if(detail){view.fieldOfView=37;view.transform.position=cat.transform.position+new Vector3(2,1.8f,-3.5f);view.transform.LookAt(cat.transform.position+Vector3.up*.6f);}
            else
            {
                view.transform.position=overviewBounds.center+new Vector3(14,18,-22);view.transform.LookAt(overviewBounds.center);
                float extent=0,aspect=(float)Screen.width/Screen.height;
                foreach(var p in overviewPoints)
                {
                    var q=view.transform.InverseTransformPoint(p);
                    extent=Mathf.Max(extent,Mathf.Abs(q.y),Mathf.Abs(q.x)/aspect);
                }
                view.orthographicSize=extent*1.06f;
            }
        }
        void Update()
        {
            if(simulation==null||reviewing)return;
            simulation.Advance(Mathf.Min(Time.deltaTime,.1f)*Simulation.Speed);cat.Tick(simulation.State,Time.deltaTime);
            if(close)Frame(true);
            if(Input.GetKeyDown(KeyCode.Tab))ApplyStyle(!toy);
            if(Input.GetKeyDown(KeyCode.C))Frame(!close);
            if(Input.GetKeyDown(KeyCode.R))ResetLife();
            if(Input.GetKeyDown(KeyCode.Escape))Application.Quit();
            if(Input.GetMouseButtonDown(0)&&Input.mousePosition.y<Screen.height-(UiTop+112)*UiScale&&Input.mousePosition.y>(UiBottom+62)*UiScale)
            {
                var ray=view.ScreenPointToRay(Input.mousePosition);
                if(Physics.Raycast(ray,out var hit,100)&&hit.collider.GetComponentInParent<CatView>()!=null){simulation.Pet();cat.React();}
                else if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance))
                {var p=ray.GetPoint(distance);if(Room.Walkable(new Point(p.x,p.z)))simulation.Call(new Point(p.x,p.z));}
            }
        }
        void OnGUI()
        {
            if(reviewing)return;
            var oldMatrix=GUI.matrix;float scale=UiScale,width=Screen.width/scale,height=Screen.height/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float panel=Mathf.Min(width-24,540),button=(panel-24)/3,top=UiTop;
            var title=new GUIStyle(GUI.skin.label){fontSize=16,alignment=TextAnchor.MiddleCenter};
            var control=new GUIStyle(GUI.skin.button){fontSize=14};
            var caption=new GUIStyle(GUI.skin.label){fontSize=12,alignment=TextAnchor.MiddleCenter};
            GUI.Box(new Rect(12,top,panel,108),GUIContent.none);
            GUI.Label(new Rect(20,top+4,panel-16,30),"MOMO / "+(toy?"SUNNY TOY HOME":"CURRENT ROOM"),title);
            if(GUI.Button(new Rect(20,top+44,button,48),toy?"Compare: A":"Compare: B",control))ApplyStyle(!toy);
            if(GUI.Button(new Rect(24+button,top+44,button,48),close?"Whole home":"Meet the cat",control))Frame(!close);
            if(GUI.Button(new Rect(28+button*2,top+44,button,48),"Reset",control))ResetLife();
            GUI.Box(new Rect(12,height-UiBottom-62,width-24,54),GUIContent.none);
            GUI.Label(new Rect(16,height-UiBottom-60,width-32,24),"Tap cat to pet · Tap floor to call",caption);
            GUI.Label(new Rect(16,height-UiBottom-37,width-32,24),"Visual sample · progress is temporary",caption);
            GUI.matrix=oldMatrix;
        }
        IEnumerator Review()
        {
            Directory.CreateDirectory(output);string marker=Path.Combine(output,"PASS.txt");if(File.Exists(marker))File.Delete(marker);
            Screen.SetResolution(1440,1000,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.6f);
            foreach(bool detail in new[]{false,true})
            {
                Frame(detail);
                foreach(bool style in new[]{false,true})
                {
                    ApplyStyle(style);ResetLife();cat.Imported.PreviewPose("Idle",.4f,.25f);
                    yield return new WaitForEndOfFrame();
                    Capture((style?"toy":"current")+(detail?"-close":"-wide"));
                }
            }
            Screen.SetResolution(600,1000,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.4f);
            ApplyStyle(true);Frame(false);cat.Imported.PreviewPose("Idle",.4f,.25f);yield return new WaitForEndOfFrame();Capture("toy-portrait");
            foreach(var p in overviewPoints)
            {var q=view.WorldToViewportPoint(p);if(q.x<0||q.x>1||q.y<0||q.y>1)throw new Exception("Portrait overview clips scenery.");}
            int lights=0;foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.enabled&&light.gameObject.activeInHierarchy)lights++;
            if(lights!=1)throw new Exception("Comparison lighting rigs are not isolated.");
            // Same production behavior and model: prove the sample instantiates and advances it.
            ResetLife();simulation.Call(new Point(2,0));bool walked=false;
            for(int i=0;i<180;i++){simulation.Advance(Simulation.Speed/30);cat.Tick(simulation.State,1f/30);walked|=cat.Imported.LastSpeed>.02f;}
            if(!walked)throw new Exception("Production cat did not move in the study.");
            var summary="Actual Unity Player captures: same tabby, Idle phase, lens and position in A/B; matched cutaway facade; landscape overview/detail and portrait. Production Simulation + CatView movement passed. No SaveStore, PlayerPrefs or production scene modification. Desktop render only; not mobile performance or human visual acceptance.\n";
            File.WriteAllText(marker,summary);Debug.Log("VISUAL STYLE REVIEW COMPLETE " + output);Application.Quit();
        }
        void Capture(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);
        }
    }
}
