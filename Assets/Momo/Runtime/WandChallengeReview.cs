using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Momo.Core;
namespace Momo
{
    // Opt-in isolated saves, real production input, real rendered cat models.
    public sealed class WandChallengeReview : MonoBehaviour
    {
        MomoApp app;string directory;int checks;int width=390,height=844;RenderTexture reviewTarget;
        void Awake(){Application.runInBackground=true;}
        IEnumerator Start()
        {
            foreach(var arg in Environment.GetCommandLineArgs())if(arg.StartsWith("--play-review-size=")){var parts=arg.Substring(19).Split('x');width=int.Parse(parts[0]);height=int.Parse(parts[1]);}
            directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../../artifacts/wand-challenge/"+width+"x"+height));Directory.CreateDirectory(directory);
            foreach(var f in new[]{"PASS.txt","FAIL.txt"}){var path=Path.Combine(directory,f);if(File.Exists(path))File.Delete(path);}
            yield return null;var run=Run();while(true){object current;try{if(!run.MoveNext())break;current=run.Current;}catch(Exception ex){File.WriteAllText(Path.Combine(directory,"FAIL.txt"),ex.ToString());Debug.LogException(ex);Application.Quit(1);yield break;}yield return current;}
        }
        void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        Vector2 Point(Point p)=>app.challenge.Camera.WorldToScreenPoint(new Vector3(p.x,80.18f,p.z));
        void DragToReady()
        {
            var s=app.challenge.Session;var stage=app.challenge;
            if(s.Phase==WandPhase.Pounce||s.Phase==WandPhase.Recover)stage.Tick(2,false);
            stage.HandlePointer(Point(new Point(-1,-.8f)),true,true,false,false);
            for(int i=1;i<=7&&s.Phase==WandPhase.Chase;i++){stage.HandlePointer(Point(new Point(-1+i*.17f,-.8f)),false,true,false,false);stage.Tick(.05f,false);}
            Check(s.Phase==WandPhase.Ready,"Production drag did not build a chase: "+s.Phase+" / "+s.ChaseProgress);
        }
        void Hit(bool far)
        {
            DragToReady();var s=app.challenge.Session;var target=far?s.Far:s.Near;var p=Point(target);
            app.challenge.HandlePointer(p,false,true,false,false);
            app.challenge.Tick(Mathf.Max(0,(.75f-s.PhaseProgress)*s.ReadyDuration),false);
            app.challenge.HandlePointer(p,false,false,true,false);app.challenge.Tick(.001f,false);
            Check(s.LastGrade==CatchGrade.PerfectNear||s.LastGrade==CatchGrade.PerfectFar,"Production release failed: "+s.Feedback);
        }
        void Capture(string name)
        {
            var camera=app.challenge.Camera;Canvas.ForceUpdateCanvases();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=reviewTarget;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG());RenderTexture.active=previous;Destroy(texture);
        }
        IEnumerator Run()
        {
            app=GetComponent<MomoApp>();PlayerReviewCapture.Resize(app,width,height);
            reviewTarget=new RenderTexture(width,height,24);reviewTarget.Create();app.challenge.Camera.targetTexture=reviewTarget;
            var canvas=app.challenge.Interface;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=app.challenge.Camera;canvas.planeDistance=.12f;canvas.GetComponent<CanvasScaler>().enabled=false;canvas.scaleFactor=Mathf.Min(width/390f,height/844f);
            app.AdoptBreed("Momo",0);app.enabled=false;app.window.Approach();for(int i=0;i<100;i++)app.window.Tick(.1f,false);
            app.hud.PlayHub();yield return null;
            var today=app.hud.GetComponentsInChildren<Button>().First(b=>b.name=="今日路线");today.onClick.Invoke();yield return null;
            Check(app.ChallengeActive&&!app.room.cameraView.enabled,"Challenge did not take the view");Check(!app.hud.GetComponentsInChildren<WalkPad>().Any(),"Household controls visible in challenge");
            Check(app.challenge.Interface.GetComponentsInChildren<TMP_Text>().All(t=>t.gameObject.layer==WandChallengeStage.Layer),"Challenge UI excluded from render camera");
            Capture("start");
            foreach(var text in app.challenge.Interface.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Near choice"||t.name=="Far choice")){var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);var center=app.challenge.Camera.WorldToScreenPoint((corners[0]+corners[2])/2);Check(center.x>0&&center.x<width&&center.y>140&&center.y<height-160,"Landing label outside phone screen");}
            float energy=app.simulation.State.energy;app.challenge.HandlePointer(Point(new Point(0,0)),true,true,false,true);Check(!app.challenge.Session.Dragging,"UI button also started a chase");
            DragToReady();yield return null;Capture("ready");app.challenge.Session.PointerCancel();
            for(int i=0;i<10;i++){Hit(i%2==1);yield return null;Check(app.challenge.Session.Combo==i+1,"Combo lost");}
            Check(app.challenge.Session.Stage==3,"Third stage unavailable");Capture("stage-three");app.challenge.Tick(60,false);yield return null;
            Check(app.simulation.State.energy==energy,"Challenge consumed household care energy");
            Check(app.Progress.completedRuns==1&&app.Progress.qualifiedRuns==1,"Result not settled");Capture("result");
            foreach(var text in app.challenge.Interface.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Challenge text overflows: "+text.name+" / "+text.text);}
            int rounds=app.Progress.completedRuns;app.challenge.Tick(.1f,false);Check(app.Progress.completedRuns==rounds,"Result settled twice");
            var retry=app.challenge.GetComponentsInChildren<Button>().First(b=>b.name=="再玩一局");var existingSkin=app.challenge.Cat.Imported;retry.onClick.Invoke();Check(app.challenge.Cat.Imported==existingSkin,"Retry rebuilt the cat skin");Check(app.challenge.Session.Score==0&&app.challenge.Session.Remaining>59,"Retry did not reset immediately");
            Hit(false);app.SuspendForReview(true);app.SuspendForReview(false);Check(app.Progress.completedRuns==rounds&&app.Progress.activeRunId==0,"Interrupted run awarded progress");Check(app.room.cameraView.enabled&&!app.ChallengeActive,"Home view not restored");
            app.Progress.facilityLevel=3;app.PlayGarden.Refresh();Check(app.PlayGarden.GetComponentsInChildren<TouchTarget>().Count(t=>t.kind.StartsWith("play-facility-"))==3,"Home facilities missing");
            for(int layout=0;layout<=3;layout++)
            {
                app.StartChallenge(layout,77123,"场地检查");for(int i=0;i<3;i++){Hit(i%2==1);app.challenge.Tick(.25f,false);yield return null;}
                Capture("layout-"+layout);foreach(var p in new[]{app.challenge.Session.Near,app.challenge.Session.Far}){var q=app.challenge.Camera.WorldToViewportPoint(new Vector3(p.x,80.05f,p.z));Check(q.z>0&&q.x>.04f&&q.x<.96f&&q.y>.18f&&q.y<.8f,"Landing outside visible play area");}app.challenge.Close();
            }
            for(int breed=0;breed<9;breed++)
            {
                app.simulation.State.breedIndex=breed;
                for(int variant=0;variant<(ImportedCatCatalog.ForBreed(breed)==null?2:1);variant++)
                {
                    app.household.Focused.appearance=variant;app.StartChallenge(0,717,"动作检查");Hit(false);yield return null;
                    var view=app.challenge.Cat;Check(view.GetComponentsInChildren<Renderer>().Any(),"Missing cat renderer "+breed);
                    foreach(var skin in view.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {var mesh=new Mesh();skin.BakeMesh(mesh);Check(mesh.vertices.All(v=>Room.Finite(v.x)&&Room.Finite(v.y)&&Room.Finite(v.z)),"Nonfinite skin "+breed);Check(mesh.bounds.size.magnitude<20,"Exploded play skin "+breed);Destroy(mesh);}
                    Capture("cat-"+breed+"-"+variant);app.challenge.Close();yield return null;
                }
            }
            app.simulation.State.breedIndex=0;app.Save();Check(!app.SaveFailed,"Unity household codec failed");
            var loaded=new HouseholdStore(app.SavePath,new UnityCodec(),new UnityCodec()).Load();WorldSimulation.Validate(loaded);Check(loaded.schema==3&&loaded.play.completedRuns==rounds,"Play progress lost on Unity save roundtrip");
            File.WriteAllText(Path.Combine(directory,"PASS.txt"),checks+" production challenge checks passed. Rendered at "+width+"x"+height+". Human fun, animation acceptance and device performance remain separate.\n");Debug.Log("WAND REVIEW PASS "+checks);Application.Quit(0);
        }
    }
}
