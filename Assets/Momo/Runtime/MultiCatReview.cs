using System;
using System.Collections;
using System.IO;
using System.Text;
using Momo.Core;
using TMPro;
using UnityEngine;

namespace Momo
{
    // Explicit development flag only. Uses the same app/input/world/render/save paths
    // as production, with a fresh temporary save selected by MomoApp.
    public sealed class MultiCatReview : MonoBehaviour
    {
        MomoApp app;string directory;
        float minimumGap=float.MaxValue,maximumPositionError,maximumStep;
        int checkedFrames;readonly StringBuilder report=new StringBuilder();
        void Awake(){Application.runInBackground=true;}
        IEnumerator Start()
        {
            directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/multicat"));Directory.CreateDirectory(directory);
            foreach(var name in new[]{"PASS.txt","FAIL.txt"}){var file=Path.Combine(directory,name);if(File.Exists(file))File.Delete(file);}
            var run=Run();while(true)
            {
                object next;try{if(!run.MoveNext())break;next=run.Current;}
                catch(Exception e){File.WriteAllText(Path.Combine(directory,"FAIL.txt"),e.ToString());Debug.LogException(e);Application.Quit(1);yield break;}
                yield return next;
            }
        }
        void Check(bool value,string message){if(!value)throw new Exception(message);}
        void Place(int id,Point p,Point? destination=null)
        {
            app.household.CancelFor(id);var c=app.household.Cat(id);var s=c.life;
            s.pet=s.target=p;s.route.Clear();s.activity=s.intent=Activity.Idle;s.remaining=10000;s.energy=75;s.hunger=40;s.thirst=20;c.socialCooldown=10000;c.blockedSeconds=0;
            if(destination.HasValue){s.target=destination.Value;s.route=CatNavigation.Path(p,s.target);s.activity=Activity.Walk;s.intent=Activity.Explore;}
            app.cats[id].transform.position=new Vector3(p.x,0,p.z);
        }
        void Step(float dt=1f/30)
        {
            var old=new System.Collections.Generic.Dictionary<int,Vector3>();foreach(var pair in app.cats)old[pair.Key]=pair.Value.transform.position;
            app.AdvanceWorld(dt);app.RenderCats(dt);app.window.CatPosition=app.cat.transform.position;
            app.window.Tick(dt,false);app.hud.Refresh();Physics.SyncTransforms();
            var cats=app.household.State.cats;
            for(int i=0;i<cats.Count;i++)
            {
                var c=cats[i];var pos=app.cats[c.id].transform.position;
                maximumPositionError=Mathf.Max(maximumPositionError,Vector3.Distance(pos,new Vector3(c.life.pet.x,0,c.life.pet.z)));
                maximumStep=Mathf.Max(maximumStep,Vector3.Distance(old[c.id],pos));
                Check(CatNavigation.Walkable(c.life.pet),"Rendered body entered furniture: "+c.id);
                for(int j=i+1;j<cats.Count;j++)minimumGap=Mathf.Min(minimumGap,Point.Distance(c.life.pet,cats[j].life.pet)-c.radius-cats[j].radius);
            }
            Check(minimumGap>=-.0001f,"Rendered cat body envelopes overlap");Check(maximumPositionError<.0001f,"View moved outside authoritative trajectory");checkedFrames++;
        }
        void CameraAt(Vector3 position,Vector3 target,float fov=58)
        {var c=app.room.cameraView;c.transform.position=position;c.transform.LookAt(target);c.fieldOfView=fov;}
        IEnumerator Run()
        {
            app=GetComponent<MomoApp>();app.enabled=false;app.CloseBoutique();app.AdoptBreed("Momo",0);
            for(int i=1;i<5;i++)app.AdoptBreed(BreedArchetypes.Get(i).defaultName,i);
            Check(app.CatCount==5&&app.cats.Count==5,"Production adoption did not create five resident actors");
            app.FocusCat(1,false);app.CancelFeeding();app.company.Cancel();app.window.Approach();for(int i=0;i<120;i++)app.window.Tick(1f/60,false);
            foreach(var c in app.household.State.cats)Check(c.life.adopted,"New resident not adopted");
            var layout=new[]{new Point(-2.5f,0),new Point(0,0),new Point(2.5f,0),new Point(-1.3f,2.3f),new Point(1.3f,2.3f)};
            for(int i=0;i<5;i++)Place(i+1,layout[i]);
            foreach(var size in new[]{new Vector2Int(390,844),new Vector2Int(320,700),new Vector2Int(768,1024)})
            {
                PlayerReviewCapture.Resize(app,size.x,size.y);yield return new WaitForSecondsRealtime(.3f);
                app.hud.Refresh();CameraAt(new Vector3(0,9,-1),new Vector3(0,.4f,1),80);
                foreach(var actor in app.cats.Values){var onScreen=app.room.cameraView.WorldToViewportPoint(actor.transform.position+Vector3.up*.5f);Check(onScreen.z>0&&onScreen.x>.04f&&onScreen.x<.96f&&onScreen.y>.14f&&onScreen.y<.84f,"Five-cat overview does not frame every cat");}
                yield return Capture("five-cats-"+size.x);
                app.hud.Household();yield return null;Canvas.ForceUpdateCanvases();CheckHud();yield return Capture("household-"+size.x);app.hud.ClosePanel();
            }
            // The actual collider under a finger switches focus; empty floor never commands movement.
            PlayerReviewCapture.Resize(app,390,844);yield return new WaitForSecondsRealtime(.25f);
            foreach(int id in new[]{2,3,4,5,1})
            {
                var view=app.cats[id];CameraAt(view.transform.position+new Vector3(0,2,-3),view.HeadPosition,50);Physics.SyncTransforms();
                var screen=app.room.cameraView.WorldToScreenPoint(view.HeadPosition);
                app.HandlePointer(screen,true,true,false,false);app.HandlePointer(screen,false,false,true,false);
                Check(app.FocusedCatId==id,"Touch hit wrong cat: expected "+id+", got "+app.FocusedCatId);
            }
            app.FocusCat(1,false);var before=app.simulation.State.target;
            CameraAt(new Vector3(0,5,-4),new Vector3(0,0,1));Physics.SyncTransforms();
            var floor=app.room.cameraView.WorldToScreenPoint(new Vector3(0,0,3.1f));app.HandlePointer(floor,true,true,false,false);app.HandlePointer(floor,false,false,true,false);
            Check(Point.Distance(before,app.simulation.State.target)<.001f,"Empty floor still commands movement");
            // Walking is constrained by every cat, including an unfocused one.
            Place(5,new Point(9,0));app.FocusCat(1,false);
            CameraAt(new Vector3(6.5f,WindowView.Eye.y,0),new Vector3(9,.5f,0));
            app.window.CatPosition=app.cat.transform.position;app.window.MoveBy(new Vector3(4,0,0));
            Check(Vector3.Distance(new Vector3(app.window.ViewerPosition.x,0,app.window.ViewerPosition.z),app.cats[5].transform.position)>=1.029f,"Observer walked through an unfocused cat");
            CameraAt(new Vector3(0,WindowView.Eye.y,-3),new Vector3(6,.5f,0));
            // Opposing doorway traffic with all five actors still present in the house.
            Place(1,new Point(4,0),new Point(8,0));Place(2,new Point(8,0),new Point(4,0));
            Place(3,new Point(-2,0));Place(4,new Point(0,2.3f));Place(5,new Point(9,8.7f));
            int frames=0;bool firstArrived=false,secondArrived=false;while(frames++<1200&&(!firstArrived||!secondArrived))
            {Step();firstArrived|=app.household.Cat(1).life.pet.x>7.8f;secondArrived|=app.household.Cat(2).life.pet.x<4.2f;if(frames%6==0)yield return null;}
            Check(frames<1200,"Production doorway traffic deadlocked");
            CameraAt(new Vector3(6,6,-4),new Vector3(6,0,0));yield return Capture("doorway-yield-390");report.AppendLine("Doorway swap completed in "+frames+" production frames.");
            // Cooperative sniffing, with the other three remaining autonomous.
            Place(1,new Point(-1,0));Place(2,new Point(1,0));Place(3,new Point(9,0));Place(4,new Point(3,8.7f));Place(5,new Point(3,14));
            foreach(int id in new[]{1,2}){var c=app.household.Cat(id);c.socialCooldown=0;c.life.personality.sociability=.9f;}
            app.household.SetHumanBusy(1,false);app.household.SetHumanBusy(2,false);
            Check(app.household.TryInteract(1,2,SocialKind.Sniff),"Could not invite sniff");
            for(int i=0;i<80;i++){Step();if(i%6==0)yield return null;}
            CameraAt(new Vector3(0,2,-4),new Vector3(0,.5f,0),50);yield return Capture("sniff-390");
            for(int i=0;i<100;i++){Step();if(i%6==0)yield return null;}
            Check(app.household.Relationship(1,2).meetings==1,"Production sniff did not finish and remember partner");
            // All five request the same feeding location; inspect every rendered position.
            foreach(var c in app.household.State.cats){c.life.hunger=70;app.household.Navigate(c.id,Room.Food,Activity.Eat);}
            for(int i=0;i<700;i++){Step();if(i%12==0)yield return null;}
            CameraAt(new Vector3(2,7,-2),new Vector3(3,.4f,2),64);yield return Capture("shared-food-390");
            app.Save();Check(!app.SaveFailed,"Household save failed");
            var codec=new UnityCodec();var store=new HouseholdStore(app.SavePath,codec,codec);var loaded=store.Load();
            Check(loaded.cats.Count==5&&loaded.relationships.Count>=2,"Production Unity save lost cats/relationships");
            var restored=new WorldSimulation(loaded);restored.Resume(loaded.lastUtcTicks+TimeSpan.FromHours(6).Ticks);WorldSimulation.Validate(restored.State);
            Check(restored.Sessions.Count==0,"Offline resume kept stale interaction");
            // Measure the actual app Update loop, not the accelerated scenario driver.
            app.hud.ClosePanel();CameraAt(new Vector3(0,WindowView.Eye.y,-3.9f),new Vector3(3,.5f,2));app.enabled=true;
            var frameTimes=new System.Collections.Generic.List<float>();
            for(int i=0;i<300;i++)
            {
                yield return null;frameTimes.Add(Time.unscaledDeltaTime);
                foreach(var resident in app.household.State.cats)
                {var pos=app.cats[resident.id].transform.position;Check(Vector3.Distance(pos,new Vector3(resident.life.pet.x,0,resident.life.pet.z))<.0001f,"Live app desynchronized visual position");}
            }
            app.enabled=false;frameTimes.Sort();float total=0;foreach(float t in frameTimes)total+=t;
            report.AppendLine((Application.isBatchMode?"Offscreen app loop (not display FPS): ":"Live desktop app Update: ")+(frameTimes.Count/total).ToString("F1")+" fps average, p95 frame="+(frameTimes[(int)(frameTimes.Count*.95f)]*1000).ToString("F1")+"ms across "+frameTimes.Count+" frames.");
            report.AppendLine("Five production adoptions; three viewport sizes; roster text and containment; five real collider selections; empty floor; observer vs unfocused cat; doorway; sniff; shared food; atomic Unity save and six-hour resume passed.");
            report.AppendLine("Checked frames="+checkedFrames+" minimum body gap="+minimumGap.ToString("F5")+" maximum render/logic error="+maximumPositionError.ToString("F6")+" maximum step="+maximumStep.ToString("F5"));
            report.AppendLine("Desktop Player evidence only. Torso envelopes are conservative approximations; authored mutual grooming/rubbing/jumps and physical-device thermal/UX acceptance remain separate.");
            File.WriteAllText(Path.Combine(directory,"PASS.txt"),report.ToString());Debug.Log("MULTICAT REVIEW PASSED\n"+report);Application.Quit(0);
        }
        void CheckHud()
        {
            Canvas.ForceUpdateCanvases();foreach(var text in app.hud.GetComponentsInChildren<TMP_Text>())
            {
                if(!text.gameObject.activeInHierarchy||string.IsNullOrEmpty(text.text))continue;
                Check(!text.isTextTruncated,"Truncated household UI: "+text.text);
                var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                foreach(var corner in corners){var p=PlayerReviewCapture.ScreenPoint(app,corner);Check(p.x>=-1&&p.x<=PlayerReviewCapture.Width+1&&p.y>=-1&&p.y<=PlayerReviewCapture.Height+1,"Household UI outside viewport: "+text.text);}
            }
        }
        IEnumerator Capture(string name)
        {if(Application.isBatchMode)yield return null;else yield return new WaitForEndOfFrame();PlayerReviewCapture.Capture(app,Path.Combine(directory,name+".png"));yield return null;}
    }
}
