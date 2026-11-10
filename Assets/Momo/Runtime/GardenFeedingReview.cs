using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using Momo.Core;

namespace Momo
{
    // Opt-in production-player regression. All scenario data uses an isolated temporary save.
    public sealed class GardenFeedingReview : MonoBehaviour
    {
        MomoApp app;
        string directory;
        void Awake(){Application.runInBackground=true;}
        IEnumerator Start()
        {
            directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/garden-feeding"));Directory.CreateDirectory(directory);
            foreach(var marker in new[]{"PASS.txt","FAIL.txt"}){var p=Path.Combine(directory,marker);if(File.Exists(p))File.Delete(p);}
            var run=Run();
            while(true)
            {
                object next;
                try{if(!run.MoveNext())break;next=run.Current;}
                catch(Exception ex){File.WriteAllText(Path.Combine(directory,"FAIL.txt"),ex.ToString());Debug.LogException(ex);Application.Quit(1);yield break;}
                yield return next;
            }
        }
        void Check(bool valid,string reason){if(!valid)throw new Exception(reason);}
        void Step(float dt=.1f)
        {
            app.AdvanceWorld(dt);
            app.cat.Tick(app.simulation.State,dt);app.room.UpdateWorld(app.simulation.State,dt);
            app.window.CatPosition=app.cat.transform.position;app.window.Tick(dt,false);
            int previousBites=app.feeding.Bites;
            app.UpdateHandFeeding(dt);app.hud.Refresh();
            if(app.feeding.Bites>previousBites)
            {
                var direction=Vector3.ProjectOnPlane(app.hands.FoodPoint-app.cat.transform.position,Vector3.up).normalized;
                Check(Vector3.Distance(app.cat.MouthPosition,app.hands.FoodPoint)<.075f&&Vector3.Dot(app.cat.transform.forward,direction)>.9f&&app.hands.FoodContactClear,"Bite committed without facing, mouth contact and hand clearance");
            }
            foreach(var hand in app.hands.GetComponentsInChildren<CompanionHandView>(true))
                Check(hand.AnatomyValid&&hand.MaximumLengthError<.0001f&&hand.MaximumWristAngle<=19&&!float.IsNaN(hand.MaximumWristAngle),"Hand side, arm length or wrist angle invariant failed: "+hand.MaximumWristAngle);
        }
        void Place(Point viewer,Point pet,float hunger=60)
        {
            app.CancelFeeding();app.company.Cancel();app.window.ReturnToEntry();
            pet=CatNavigation.Nearest(pet); // Fixtures must obey the new whole-body clearance.
            app.room.cameraView.transform.position=new Vector3(viewer.x,WindowView.Eye.y,viewer.z);
            var s=app.simulation.State;s.pet=s.target=pet;s.route.Clear();s.activity=s.intent=Activity.Idle;s.remaining=1000;s.hunger=hunger;s.thirst=15;s.energy=90;
            app.cat.transform.position=new Vector3(pet.x,0,pet.z);app.window.Find(app.cat.transform.position);
            for(int i=0;i<20;i++){app.window.Tick(.1f,false);app.cat.Tick(s,.1f);}
            Canvas.ForceUpdateCanvases();app.hud.Refresh();Physics.SyncTransforms();
        }
        IEnumerator Run()
        {
            app=GetComponent<MomoApp>();app.AdoptBreed("Momo",0);app.enabled=false;
            app.window.Approach();for(int i=0;i<120;i++)app.window.Tick(1f/60,false);
            PlayerReviewCapture.Resize(app,390,844);yield return new WaitForSecondsRealtime(.3f);
            // Both actual observers and the actual rendered cat must cross the production doors.
            app.window.ReturnToEntry();app.window.CatPosition=new Vector3(1000,0,1000);
            app.window.MoveBy(new Vector3(3,0,0));app.window.MoveBy(new Vector3(0,0,18));
            Check(app.window.ViewerPosition.z>17.9f,"Player cannot walk out to the garden");
            var s=app.simulation.State;s.pet=new Point(0,2);s.target=s.pet;s.remaining=1000;s.activity=s.intent=Activity.Idle;
            app.cat.transform.position=new Vector3(0,0,2);app.simulation.Call(new Point(3,19));
            float routeLength=0;Point previous=s.pet;foreach(var q in s.route){routeLength+=Point.Distance(previous,q);previous=q;}
            int travelFrames=Mathf.CeilToInt(routeLength/.18f/(float)Simulation.Speed/.1f)+90;
            for(int i=0;i<travelFrames;i++){Step();if(i%8==0)yield return null;}
            Check(s.pet.z>18.9f&&app.cat.transform.position.z>18.8f,"Simulation/render failed cross-room journey: pet="+s.pet.x+","+s.pet.z+" target="+s.target.x+","+s.target.z+" route="+s.route.Count+" frame budget="+travelFrames);
            foreach(var c in app.room.GetComponentsInChildren<Collider>())
                if(c.name.EndsWith("floor",StringComparison.OrdinalIgnoreCase))Check(c.GetComponent<TouchTarget>()?.kind=="floor","Untagged expansion floor: "+c.name);
            foreach(var size in new[]{new Vector2Int(390,844),new Vector2Int(320,700),new Vector2Int(768,1024)})
            {
                PlayerReviewCapture.Resize(app,size.x,size.y);yield return new WaitForSecondsRealtime(.25f);
                Place(new Point(3,6.3f),new Point(2,8.9f));yield return Capture("kitchen-"+size.x);CheckHud();
                Place(new Point(9,6),new Point(8.9f,8.7f));yield return Capture("quiet-room-"+size.x);CheckHud();
                Place(new Point(3,13),new Point(3.4f,16));yield return Capture("garden-"+size.x);CheckHud();
            }
            PlayerReviewCapture.Resize(app,390,844);yield return new WaitForSecondsRealtime(.25f);
            // Clicking the real sack collider uses the same pointer route as a finger.
            Place(new Point(-1.5f,6),new Point(-1.3f,9.2f));
            app.window.Find(new Vector3(Room.IndoorBag.x,.3f,Room.IndoorBag.z));for(int i=0;i<30;i++)app.window.Tick(.1f,false);
            Physics.SyncTransforms();var bag=app.room.cameraView.WorldToScreenPoint(new Vector3(Room.IndoorBag.x,.4f,Room.IndoorBag.z));
            app.HandlePointer(bag,true,true,false,false);app.HandlePointer(bag,false,false,true,false);
            Check(app.feeding.Active,"Clicking the indoor bag did not grab food: "+app.Toast);
            yield return Capture("hand-scoop-390");
            app.window.Find(app.cat.transform.position);for(int i=0;i<15;i++)app.window.Tick(.1f,false);
            for(int i=0;i<18;i++){Step();yield return null;if(i==12)yield return Capture("hand-turn-390");}
            yield return Capture("right-hand-offer-390");
            Physics.SyncTransforms();var palm=app.room.cameraView.WorldToScreenPoint(app.hands.Palm);
            var original=app.hands.Palm;app.HandlePointer(palm,true,true,false,false);
            app.HandlePointer((Vector2)palm+Vector2.right*15,false,true,false,false);app.HandlePointer((Vector2)palm+Vector2.right*15,false,false,true,false);
            Step();yield return null;
            Check(app.LastGesture.StartsWith("food-hand")&&Vector3.Distance(original,app.hands.Palm)>.025f,"Dragging the visible right hand failed: "+app.LastGesture+" distance="+Vector3.Distance(original,app.hands.Palm));
            app.hands.Drag(Vector2.left*15);Step();yield return null;
            int frames=0;
            while(!app.feeding.Eating&&frames++<180){Step();if(frames%3==0)yield return null;}
            yield return Capture("contact-wait");
            foreach(var view in app.hands.GetComponentsInChildren<CompanionHandView>(false))foreach(var h in view.ContactShapes)foreach(var c in app.cat.ContactSolids)
                if(Physics.ComputePenetration(h,h.transform.position,h.transform.rotation,c,c.transform.position,c.transform.rotation,out var axis,out float depth)&&depth>.004f)Debug.Log("CONTACT CLIP "+h.name+" / "+h.transform.parent.name+" vs "+c.name+" depth="+depth+" axis="+axis);
            Debug.Log("CONTACT WAIT cat="+app.cat.transform.position+" target="+app.hands.CatTarget.x+","+app.hands.CatTarget.z+" mouth="+app.cat.MouthPosition+" food="+app.hands.FoodPoint+" forward="+app.cat.transform.forward+" clear="+app.hands.FoodContactClear+" penetration="+app.hands.RemainingPenetration+" state="+s.activity+" destination="+s.target.x+","+s.target.z+" steady="+app.hands.Steady+" accessible="+app.hands.Accessible);
            Check(app.feeding.Eating,"Cat never started eating: "+app.feeding.Phase+" / mouth "+Vector3.Distance(app.cat.MouthPosition,app.hands.Palm));
            for(int i=0;i<45&&app.feeding.Bites<1;i++){Step();yield return null;}
            Check(app.feeding.Bites==1,"Rendered mouth never made real food contact");
            Physics.SyncTransforms();var head=app.room.cameraView.WorldToScreenPoint(app.cat.HeadPosition);float bond=s.affection;
            app.HandlePointer(head,true,true,false,false);app.HandlePointer(head,false,false,true,false);
            Check(s.affection>bond,"Tapping the actual cat head did not pet it");
            for(int i=0;i<5;i++){Step();yield return null;}yield return Capture("feeding-and-petting-390");CheckHud();
            PlayerReviewCapture.Resize(app,320,700);yield return new WaitForSecondsRealtime(.2f);Step();yield return Capture("feeding-and-petting-320");CheckHud();
            PlayerReviewCapture.Resize(app,390,844);yield return new WaitForSecondsRealtime(.2f);Step();
            int before=app.feeding.Bites;
            app.window.MoveBy(new Vector3(0,0,-.2f));Step();
            Check(!app.feeding.Eating&&app.feeding.Bites==before,"Moving viewer did not pause eating");
            int guard=0;
            while(app.feeding.Active&&guard++<260){Step();if(guard%3==0)yield return null;}
            Check(app.feeding.Bites==4,"Indoor hand feed did not complete: "+app.feeding.Phase+" / "+app.feeding.Bites+" / mouth "+Vector3.Distance(app.cat.MouthPosition,app.hands.Palm));
            Check(app.hud.Satiety.FeedbackCount>=4&&app.hud.Bond.FeedbackCount>=1,"Actual gains did not animate the status icons");
            var codec=new UnityCodec();var restored=new Simulation(codec.Decode(codec.Encode(s)));
            Check(!restored.HandFeedingActive&&restored.State.hasHandFed&&restored.State.lastHandFeedAt==s.lastHandFeedAt,"Unity save lost feed history or resumed an unfinished feeding session");
            yield return new WaitForSecondsRealtime(.4f);yield return Capture("fed-status-390");
            Place(new Point(8.7f,12.5f),new Point(8.7f,15.8f));
            app.GrabFood(Room.OutdoorBag);Check(app.feeding.Active,"Outdoor bag is unreachable");
            guard=0;while(app.feeding.Bites<1&&guard++<210){Step();if(guard%3==0)yield return null;}
            Check(app.feeding.Bites>0,"Garden feeding never reached a real bite: "+app.feeding.Phase+" pet="+s.pet.x+","+s.pet.z+" target="+s.target.x+","+s.target.z+" hand target="+app.hands.CatTarget.x+","+app.hands.CatTarget.z+" accessible="+app.hands.Accessible+" mouth="+Vector3.Distance(app.cat.MouthPosition,app.hands.Palm));
            yield return new WaitForSecondsRealtime(.4f);yield return Capture("outdoor-feeding-390");app.hud.ToggleSettings();
            Check(!app.FeedingActive&&!app.simulation.HandFeedingActive,"Opening settings did not release feeding");app.hud.ClosePanel();
            // A full cat declines. The offered food remains visible and can be withdrawn.
            Place(new Point(3,13),new Point(3.3f,15),10);app.feeding.Grab();
            for(int i=0;i<55;i++){Step();if(i%8==0)yield return null;}
            Check(app.feeding.Bites==0&&!app.simulation.HandFeedingActive,"Full cat was forced to eat");yield return Capture("full-cat-declines-390");app.CancelFeeding();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--hand-motion-review")>=0)yield return RecordHandMotion();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--contact-skins-review")>=0)yield return ReviewContactSkins();
            foreach(var hand in app.hands.GetComponentsInChildren<CompanionHandView>(true))Debug.Log("FINAL HAND wrist max="+hand.MaximumWristAngle+" length error="+hand.MaximumLengthError);
            PlayerReviewCapture.Resize(app,1280,1000);yield return new WaitForSecondsRealtime(.2f);
            var camera=app.room.cameraView;app.hud.gameObject.SetActive(false);
            foreach(var r in app.room.GetComponentsInChildren<Renderer>())if(r.name.StartsWith("Outside wall",StringComparison.Ordinal))r.enabled=false; // Layout-only cutaway.
            camera.orthographic=true;camera.orthographicSize=17;
            camera.transform.position=new Vector3(24,32,36);camera.transform.LookAt(new Vector3(3,0,8));
            yield return Capture("four-rooms-and-garden-overview");
            File.WriteAllText(Path.Combine(directory,"PASS.txt"),"Production observer and rendered cat cross into the garden; all expansion floors are interactive; three viewport layouts; real sack hit -> right hand scoop/turn/offer -> sniff/eat; left-hand petting; movement pauses and resumes feeding; indoor four bites and outdoor bite; actual stat animation; full-cat refusal; panel cancellation. Isolated review fixtures only. Device touch, naturalness and performance remain human/device checks.\n");
            Debug.Log("GARDEN FEEDING REVIEW COMPLETE");Application.Quit();
        }
        IEnumerator ReviewContactSkins()
        {
            foreach(int breed in new[]{0,4,5,6,7,-1})
            {
                app.CancelFeeding();app.cat.Build(BreedArchetypes.Get(Math.Max(0,breed)),breed>=0);yield return null;
                Place(new Point(8.7f,12.5f),new Point(8.7f,15.8f));app.GrabFood(Room.OutdoorBag);
                Check(app.feeding.Active&&app.feeding.Bites==0,"Appearance feeding did not start: "+breed+" / "+app.Toast);
                int guard=0;while(app.feeding.Bites<1&&guard++<900){Step(1f/30);yield return null;}
                yield return Capture("contact-skin-"+breed);
                Check(app.feeding.Bites>0,"Appearance cannot reach food: "+breed+" / "+app.feeding.Phase+" mouth="+Vector3.Distance(app.cat.MouthPosition,app.hands.FoodPoint));
                app.PetCompanion();float maximum=0;
                for(int frame=0;frame<48;frame++)
                {
                    Step(1f/30);yield return null;
                    foreach(var hand in app.hands.GetComponentsInChildren<CompanionHandView>(false))maximum=Mathf.Max(maximum,ContactGeometry.Separation(hand.ContactShapes,app.cat.ContactSolids,out var correction));
                }
                yield return Capture("contact-pet-skin-"+breed);
                Check(maximum<=.006f,"Hand contact surface penetration: "+breed+" / "+maximum);
                Debug.Log("CONTACT SKIN "+breed+" bite verified, maximum hand proxy penetration="+maximum);
            }
            app.CancelFeeding();
        }
        IEnumerator RecordHandMotion()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/feeding-contact/motion-final"));Directory.CreateDirectory(folder);
            PlayerReviewCapture.Resize(app,390,844);yield return null;
            Place(new Point(-1.5f,6),new Point(-1.3f,9.2f));app.GrabFood(Room.IndoorBag);
            bool petted=false;int frame=0;float maximumContact=0;FeedingPhase worstPhase=FeedingPhase.None;
            for(;frame<480;frame++)
            {
                Step(1f/30);
                if(frame%30==0)Debug.Log("MOTION CONTACT frame="+frame+" phase="+app.feeding.Phase+" bites="+app.feeding.Bites+" mouth="+Vector3.Distance(app.cat.MouthPosition,app.hands.FoodPoint)+" slot="+Point.Distance(app.simulation.State.pet,app.hands.CatTarget)+" clear="+app.hands.FoodContactClear+" accessible="+app.hands.Accessible+" steady="+app.hands.Steady);
                if(!petted&&app.feeding.Bites>=1){app.PetCompanion();petted=true;}
                yield return null;
                foreach(var hand in app.hands.GetComponentsInChildren<CompanionHandView>(false))
                {float depth=ContactGeometry.Separation(hand.ContactShapes,app.cat.ContactSolids,out var correction);if(depth>maximumContact){maximumContact=depth;worstPhase=app.feeding.Phase;}}
                PlayerReviewCapture.Capture(app,Path.Combine(folder,frame.ToString("D4")+".png"));
            }
            Debug.Log("MOTION maximum contact penetration="+maximumContact+" phase="+worstPhase);
            Check(maximumContact<=.006f,"Continuous hand/cat penetration: "+maximumContact+" / "+worstPhase);
            Check(petted&&app.feeding.Bites==4,"Continuous hand sequence did not complete petting and meal: "+app.feeding.Phase+" / "+app.feeding.Bites);
            app.CancelFeeding();
            for(int i=0;i<60;i++,frame++)
            {Step(1f/30);yield return null;PlayerReviewCapture.Capture(app,Path.Combine(folder,frame.ToString("D4")+".png"));}
            Check(Mathf.Abs(app.room.cameraView.transform.position.y-WindowView.Eye.y)<.005f,"Camera did not stand back up after care");
            Debug.Log("HAND MOTION: 540 frames, fixed 1/30-second production steps; complete meal, petting and posture recovery. Not performance evidence.");
        }
        IEnumerator Capture(string name)
        {if(Application.isBatchMode)yield return null;else yield return new WaitForEndOfFrame();PlayerReviewCapture.Capture(app,Path.Combine(directory,name+".png"));}
        void CheckHud()
        {
            Canvas.ForceUpdateCanvases();
            foreach(var t in app.hud.GetComponentsInChildren<TMP_Text>(false))
            {
                t.ForceMeshUpdate();Check(!t.isTextTruncated,"Truncated text: "+t.name+" / "+t.text);
                var c=new Vector3[4];t.rectTransform.GetWorldCorners(c);for(int i=0;i<4;i++)c[i]=PlayerReviewCapture.ScreenPoint(app,c[i]);
                Check(c[0].x>=-.5f&&c[2].x<=PlayerReviewCapture.Width+.5f&&c[0].y>=-.5f&&c[2].y<=PlayerReviewCapture.Height+.5f,"HUD outside viewport: "+t.name+" / "+t.text+" / "+c[0]+" / "+c[2]);
            }
        }
    }
}
