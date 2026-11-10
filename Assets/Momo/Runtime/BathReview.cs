using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Momo.Core;

namespace Momo
{
    // Only --bath-review enables this runner and its independent temporary household.
    public sealed class BathReview : MonoBehaviour
    {
        MomoApp app;
        string directory;
        int checks,soapFrames;
        string runtimeError;
        void Awake(){Application.runInBackground=true;Application.logMessageReceived+=OnLog;}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        void OnLog(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error)&&stack.Contains("Momo."))runtimeError=message;}
        IEnumerator Start()
        {
            directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/bathing"));Directory.CreateDirectory(directory);
            foreach(var name in new[]{"PASS.txt","FAIL.txt"}){string p=Path.Combine(directory,name);if(File.Exists(p))File.Delete(p);}
            var run=Run();
            while(true)
            {
                object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){File.WriteAllText(Path.Combine(directory,"FAIL.txt"),e.ToString());Debug.LogException(e);Application.Quit(1);yield break;}
                yield return next;
            }
        }
        void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
        void Step()
        {
            app.AdvanceWorld(.1f);app.RenderCats(.1f);app.window.CatPosition=app.cat.transform.position;app.window.Tick(.1f,true);app.bath.Tick(.1f);app.hud.Refresh();
            Check(Vector3.Distance(app.cat.transform.position,new Vector3(app.simulation.State.pet.x,0,app.simulation.State.pet.z))<.002f,"Bath moved presentation away from simulation");
        }
        void Place()
        {
            app.CancelBath();app.CancelFeeding();app.company.Cancel();app.window.ReturnToEntry();
            var s=app.simulation.State;s.pet=s.target=CatNavigation.Nearest(new Point(2.5f,14.7f));s.route.Clear();s.activity=s.intent=Activity.Idle;s.remaining=1000;s.dirt=70;s.energy=90;s.hunger=s.thirst=10;s.affection=50;
            foreach(var c in app.household.State.cats)if(c.id!=app.FocusedCatId){c.life.pet=c.life.target=new Point(6,13);c.life.route.Clear();c.life.activity=c.life.intent=Activity.Idle;c.life.remaining=1000;}
            app.household.Focused.heading=0;
            app.room.cameraView.transform.position=new Vector3(0,1.55f,14.2f);app.window.Find(new Vector3(0,.1f,16.8f));
            app.RenderCats(.1f);for(int i=0;i<40;i++)app.window.Tick(.1f,false);Physics.SyncTransforms();
        }
        IEnumerator Capture(string name)
        {app.hud.Refresh();Canvas.ForceUpdateCanvases();yield return null;PlayerReviewCapture.Capture(app,Path.Combine(directory,name+".png"));}
        IEnumerator Overview(string name,bool sequence)
        {
            var camera=app.room.cameraView;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            app.bath.gameObject.SetActive(false);app.hud.gameObject.SetActive(false);
            PlayerReviewCapture.Resize(app,900,1050);camera.transform.position=new Vector3(1.8f,1.75f,14.65f);camera.transform.LookAt(new Vector3(0,.35f,16.8f));camera.fieldOfView=57;
            yield return null;PlayerReviewCapture.Capture(app,Path.Combine(directory,name+".png"));
            if(sequence)for(int frame=0;frame<20;frame++)
            {
                var effect=app.cat.GetComponent<CatBathEffects>();var water=app.bath.Basin;water.Disturb(new Vector3(.65f,0,16.45f),.8f);
                water.Tick(.1f,app.household.Bath,effect,false,Vector3.zero,0);effect.TickBathVisual(.1f,app.household.Bath,water,true,effect.Anchor(BathZone.Back,1));
                yield return null;PlayerReviewCapture.Capture(app,Path.Combine(directory,"foam-motion-"+frame.ToString("D2")+".png"));
            }
            PlayerReviewCapture.Resize(app,390,844);camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;app.bath.gameObject.SetActive(true);app.hud.gameObject.SetActive(true);yield return null;
        }
        IEnumerator CheckOtherSkins(BathSession session)
        {
            for(int breed=1;breed<5;breed++)
            {
                var g=new GameObject("Bath binding review cat",typeof(CapsuleCollider),typeof(TouchTarget));
                var view=g.AddComponent<CatView>();view.Build(BreedArchetypes.Get(breed),true);view.HouseholdMotion=true;view.HouseholdHeading=0;
                var state=Simulation.New(DateTime.UtcNow.Ticks);state.pet=state.target=BathSession.Station;state.activity=state.intent=Activity.Idle;
                view.Tick(state,.1f);var effect=g.AddComponent<CatBathEffects>();effect.Set(state,session);Physics.SyncTransforms();effect.TickBathVisual(.1f,session,app.bath.Basin,false,Vector3.zero);
                for(int zone=0;zone<3;zone++)for(int patch=0;patch<4;patch++)
                    {for(int n=0;n<17;n++)effect.TickBathVisual(.1f,session,app.bath.Basin,true,effect.Anchor((BathZone)zone,patch));yield return null;}
                Check(effect.BubbleBindingError<.003f,"Imported appearance binding mismatch: "+breed+" / "+effect.BubbleBindingError);
                Check(effect.BubblePatches==12,"Imported appearance missed foam zones: "+breed+" / "+effect.BubblePatches);
                Check(effect.BubbleCount>=100,"Imported appearance has no spherical lather: "+breed);
                Debug.Log("BATH SKIN "+breed+" bound patches="+effect.BubblePatches+" spheres="+effect.BubbleCount+" error="+effect.BubbleBindingError);
                g.SetActive(false);Destroy(g);yield return null;
            }
        }
        void CheckText()
        {
            Canvas.ForceUpdateCanvases();foreach(var t in app.hud.GetComponentsInChildren<TMP_Text>())
            {t.ForceMeshUpdate();if(t.name.StartsWith("Bath")||t.name=="Label"&&t.GetComponentInParent<Button>()?.name=="结束洗澡")Check(!t.isTextTruncated,"Bath text truncated: "+t.name+" / "+t.text);}
        }
        IEnumerator Run()
        {
            app=GetComponent<MomoApp>();app.AdoptBreed("Mikan",0);app.enabled=false;
            app.household.Focused.appearance=1;app.cat.Build(BreedArchetypes.Get(0),true);yield return null;
            app.window.Approach();for(int i=0;i<100;i++)app.window.Tick(.1f,false);
            PlayerReviewCapture.Resize(app,390,844);yield return null;Place();
            // Reach the invitation through the real tray collider and production pointer path.
            var tray=app.room.cameraView.WorldToScreenPoint(new Vector3(0,.1f,16.8f));
            app.HandlePointer(tray,true,true,false,false);app.HandlePointer(tray,false,false,true,false);
            Check(app.hud.PanelOpen,"Tray click did not open bath invitation");yield return Capture("invitation-390");
            var button=app.hud.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="叫它来洗澡");Check(button!=null,"Missing invitation button");button.onClick.Invoke();
            Check(app.BathActive,"Invite did not begin bath: "+app.Toast);
            var session=app.household.Bath;
            for(int i=0;i<280&&!session.Washing;i++){Step();if(i%50==0)Debug.Log("BATH ENTRY "+i+" cat="+app.cat.transform.position+" viewer="+app.window.ViewerPosition+" route="+app.simulation.State.route.Count+" activity="+app.simulation.State.activity);if(i%4==0)yield return null;}
            Check(session.Washing,"Cat did not autonomously walk into bath: "+session.Hint);
            for(int i=0;i<60;i++){Step();if(i%2==0)yield return Capture("tool-take-"+(i/2).ToString("D2"));}yield return Capture("wet-start-390");
            var basin=app.bath.Basin;var coat=app.cat.GetComponent<CatBathEffects>();
            Check(basin!=null&&Mathf.Abs(basin.WorldHeight-coat.Body.bounds.center.y)<.01f,"Water not halfway up the torso");
            Check(basin.WaveSpread>.004f&&basin.Disturbances>0,"Water did not move");
            Check(basin.GetComponentsInChildren<Collider>().All(c=>c.name!="Actual water surface"),"Cat could stand on a water collider");
            Check(app.bath.Pickups==1&&app.bath.MaximumPickupGap<.036f,"Tool was not picked up at actual hand contact");
            Check(app.bath.Rack.PumpPosition.y>.9f&&app.bath.Rack.Sponge.position.y>.7f,"Supplies left on the ground");
            var camera=app.room.cameraView;var before=camera.transform.position;var facing=camera.transform.rotation;
            camera.transform.position=new Vector3(1.1f,1.10f,15.25f);camera.transform.LookAt(new Vector3(.6f,.45f,16.45f));camera.Render();
            float unchanged=session.Progress;var empty=camera.WorldToScreenPoint(new Vector3(.65f,basin.WorldHeight,16.25f));
            app.HandlePointer(empty,true,true,false,false);Step();
            Check(app.bath.Spray.Flowing&&!app.bath.Spray.BodyHit&&session.Progress==unchanged,"Off-body water did not flow independently or washed the cat");
            Check(Vector3.Angle(app.bath.Spray.JetPoint(1)-app.bath.Spray.JetPoint(0),-app.bath.Rack.Face.up)<12,"Spray escaped sideways from nozzle");
            yield return Capture("spray-into-water-390");int drops=app.bath.Spray.Drops;app.HandlePointer(empty,false,false,true,false);Step();
            Check(!app.bath.Spray.Flowing&&app.bath.Spray.Drops>drops,"Shower stopped without remaining droplets");
            camera.transform.SetPositionAndRotation(before,facing);app.bath.ReleasePointer();for(int settle=0;settle<20;settle++)Step();
            float originalDirt=app.simulation.State.dirt;
            // Same pointer route as actual touch: every zone and patch is raycast against the cat.
            for(int stage=0;stage<4;stage++)
            {
                int frame=0;
                while(!session.Ready&&session.Washing&&frame<1800)
                {
                    var effect=app.cat.GetComponent<CatBathEffects>();var zone=session.TargetZone;int patch=session.TargetPatch;
                    app.bath.ReleasePointer();for(int i=0;i<12;i++)Step();
                    for(int wait=0;wait<90&&app.bath.Handling;wait++)
                    {
                        float progress=session.Progress;Step();Check(session.Progress==progress,"Handling tools advanced washing");
                        if(app.bath.HandlingPhase==BathController.HandlingStep.Pump&&app.bath.Rack.Dispensing)yield return Capture("shampoo-pump-390");
                        if(stage==1)yield return Capture("soap-sequence-"+(soapFrames++).ToString("D2"));
                        else if(wait%5==0)yield return null;
                    }
                    Check(!app.bath.Handling,"Tool handling stuck at "+app.bath.HandlingPhase+" pickup gap="+app.bath.MaximumPickupGap);
                    var target=effect.Anchor(zone,patch);var screen=app.room.cameraView.WorldToScreenPoint(target);
                    app.HandlePointer(screen,true,true,false,false);
                    for(int n=0;n<18&&session.Washing&&!session.Ready;n++)
                    {
                        // Small real surface strokes, with no progress injected into the session.
                        var stroke=target+Vector3.forward*(Mathf.Sin(n*.9f)*.018f);
                        screen=app.room.cameraView.WorldToScreenPoint(stroke);app.HandlePointer(screen,false,true,false,false);Step();frame++;
                        if(stage==0&&n==6&&frame<20){Check(app.bath.Spray.BodyHit,"Shower missed actual torso");Check(app.bath.Spray.Curvature>.001f,"Water stayed a straight beam");yield return Capture("shower-body-contact-390");}
                        if(stage==1&&n==6&&frame<20)yield return Capture("sponge-contact-390");
                        if(stage==3&&n==6&&frame<20){Check(app.bath.Rack.Cloth.Bend>.004f,"Towel stayed rigid");yield return Capture("towel-contact-390");}
                        if(n==4&&frame%95<20)
                        {
                            effect.Sample(app.room.cameraView.ScreenPointToRay(screen),out var sampled,out var point,out var normal);
                            Debug.Log("Bath contact stage="+stage+" target="+zone+"/"+patch+" sampled="+sampled.touching+" "+sampled.zone+"/"+sampled.patch+" progress="+session.Progress+" reach="+app.bath.ToolReachValid+" gap="+app.bath.ToolGap+" penetration="+app.bath.ContactPenetration);
                        }
                        if(frame==251)yield return Capture("contact-debug-"+stage);
                        if(n%5==0)yield return null;
                    }
                    app.HandlePointer(screen,false,false,true,false);frame++;
                }
                Check(session.Ready||session.Phase==BathPhase.Complete,"Pointer failed stage "+stage+": "+session.Progress+" phase="+session.Phase+" hint="+session.Hint+" zone="+session.TargetZone+" patch="+session.TargetPatch);
                app.bath.ReleasePointer();if(stage<3)for(int rest=0;rest<65&&(rest<18||app.bath.Handling);rest++){Step();if(rest%4==0)yield return null;}
                yield return Capture("step-"+(stage+1)+"-390");CheckText();
                if(stage==1)
                {
                    Check(app.bath.Rack.PumpStrokes==1,"Shampoo did not dispense exactly one dose");
                    Check(coat.BubbleCount>=100,"Soap did not form volumetric clusters: "+coat.BubbleCount);
                    Check(coat.BubbleBindingError<.003f,"Bubble skin bind/scale mismatch: "+coat.BubbleBindingError);
                    Check(coat.BubbleRadius>.035f,"Bubble sizes too uniform/small");
                    Check(coat.BubblePatches==12,"Foam missing a body patch");
                    Debug.Log("BATH MATERIALS water="+basin.Height+" wave spread="+basin.WaveSpread+" spheres="+coat.BubbleCount+" binding error="+coat.BubbleBindingError);
                    yield return Overview("foam-overview",true);
                    yield return CheckOtherSkins(session);
                }
                if(stage==0)yield return Overview("water-overview",false);
                if(stage==2)Check(coat.BubbleCount<30,"Rinse left attached soap behind");
                if(stage==3)Check(basin.Height<.012f&&basin.GateOpen,"Drying began without draining/opening the entrance");
                if(stage<3)
                {
                    Check(app.simulation.State.dirt==originalDirt,"Partial bath committed dirt");
                    var next=app.hud.GetComponentsInChildren<Button>().First(b=>b.name=="换上沐浴露");Check(next.interactable,"Completed step did not enable tool switch");next.onClick.Invoke();
                }
            }
            Check(session.Committed&&app.simulation.State.dirt==0,"Complete bath did not clean");
            for(int i=0;i<30;i++){Step();if(i%4==0)yield return null;}
            Check(!app.BathActive&&!app.simulation.BathingActive,"Completion retained care lock");
            // Resume, focus switching, settings, and JSON roundtrips must discard partial effects.
            foreach(var size in new[]{new Vector2Int(320,700),new Vector2Int(390,844),new Vector2Int(768,1024)})
            {
                PlayerReviewCapture.Resize(app,size.x,size.y);yield return null;Place();Debug.Log("BATH REPEAT INVITE size="+size.x+" viewer="+app.window.ViewerPosition+" cat="+app.cat.transform.position);app.BeginBath();
                for(int i=0;i<180&&!app.household.Bath.Washing;i++){Step();if(i%6==0)yield return null;}
                for(int i=0;i<30;i++)Step();Check(app.BathActive,"Bath not active at size "+size.x+" toast="+app.Toast+" phase="+app.household.Bath.Phase+" hint="+app.household.Bath.Hint+" viewer="+app.window.ViewerPosition);CheckText();yield return Capture("care-layout-"+size.x);
                var codec=new UnityCodec();var loaded=new WorldSimulation(codec.DecodeHome(codec.EncodeHome(app.household.State)));
                Check(loaded.Bath==null&&loaded.Focused.life.dirt>=70,"Unity checkpoint resumed partial bath");
                app.hud.ToggleSettings();Check(!app.BathActive&&!app.simulation.BathingActive,"Settings did not cancel bath");app.hud.ClosePanel();
            }
            PlayerReviewCapture.Resize(app,390,844);yield return null;Place();app.BeginBath();for(int i=0;i<180&&!app.household.Bath.Washing;i++)Step();
            app.SuspendForReview(true);Check(!app.BathActive&&!app.simulation.BathingActive,"Background kept bath active");app.SuspendForReview(false);
            app.AdoptBreed("Earl",1);Check(app.CatCount==2,"Second cat adoption failed");
            Place();app.BeginBath();Check(app.BathActive,"Multi-cat bath failed");int other=app.household.State.cats.First(c=>c.id!=app.FocusedCatId).id;
            app.FocusCat(other,false);Check(!app.BathActive,"Focus switch did not cancel bath");
            // The original articulated appearance uses different body geometry from the import.
            app.FocusCat(1,false);app.household.Focused.appearance=0;app.cat.Build(BreedArchetypes.Get(0),false);yield return null;Place();app.BeginBath();
            for(int i=0;i<180&&!app.household.Bath.Washing;i++){Step();if(i%5==0)yield return null;}
            session=app.household.Bath;Check(session.Washing&&app.cat.Imported==null,"Original appearance bath did not start");
            for(int stage=0;stage<4;stage++)
            {
                int frame=0;
                while(session.Washing&&!session.Ready&&frame<1800)
                {
                    app.bath.ReleasePointer();for(int i=0;i<15;i++)Step();
                    var effect=app.cat.GetComponent<CatBathEffects>();var target=effect.Anchor(session.TargetZone,session.TargetPatch);
                    var screen=app.room.cameraView.WorldToScreenPoint(target);app.HandlePointer(screen,true,true,false,false);
                    for(int n=0;n<18&&session.Washing&&!session.Ready;n++)
                    {
                        screen=app.room.cameraView.WorldToScreenPoint(target+Vector3.forward*Mathf.Sin(n*.9f)*.018f);
                        app.HandlePointer(screen,false,true,false,false);Step();frame++;if(n%5==0)yield return null;
                        if(n==4&&frame%95<20){effect.Sample(app.room.cameraView.ScreenPointToRay(screen),out var c,out var point,out var normal);Debug.Log("Original bath stage="+stage+" target="+session.TargetZone+"/"+session.TargetPatch+" sample="+c.zone+"/"+c.patch+" head="+c.head+" progress="+session.Progress+" reach="+app.bath.ToolReachValid+" gap="+app.bath.ToolGap);}
                    }
                    app.HandlePointer(screen,false,false,true,false);frame++;
                }
                Check(session.Ready||session.Phase==BathPhase.Complete,"Original appearance failed stage "+stage+": "+session.Progress+" / "+session.Hint);
                if(stage==1)yield return Capture("original-cat-soap-390");
                if(stage<3)Check(session.AdvanceStage(),"Original appearance tool change failed");
            }
            Check(session.Committed&&app.simulation.State.dirt==0,"Original appearance failed completion");
            app.CancelBath();
            Debug.Log("BATH TOOL MOTION pickups="+app.bath.Pickups+" returns="+app.bath.Returns+" maximum pickup gap="+app.bath.MaximumPickupGap+" pump strokes="+app.bath.Rack.PumpStrokes+" impacts="+app.bath.Spray.Impacts+" beads="+app.bath.Spray.Drops);
            Check(!app.bath.Handling&&!app.bath.Spray.Flowing,"Cancelled care retained tool motion/water");
            for(int tool=0;tool<3;tool++)Check(Vector3.Distance(app.bath.Rack.Tool((BathTool)tool).position,app.bath.Rack.Dock((BathTool)tool))<.001f,"Cancelled care left a tool floating");
            Check(runtimeError==null,"Runtime error during bathing: "+runtimeError);
            File.WriteAllText(Path.Combine(directory,"PASS.txt"),checks+" bath production checks passed. Actual tray, tool pointer contact, four stages, layouts, JSON, cancellation and background. Desktop rendering only; device touch and motion acceptance remain separate.\n");
            Application.Quit(0);
        }
    }
}
