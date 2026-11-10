using Momo.Core;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
static class WandTests
{
    public static void Catch(WandChallengeSession s,bool far=false,float timing=.75f)
    {
        if(s.Phase==WandPhase.Recover||s.Phase==WandPhase.Pounce)s.Advance(2);
        s.PointerDown(new Point(-1,-.8f));
        for(int i=1;i<=7&&s.Phase==WandPhase.Chase;i++){s.PointerMove(new Point(-1+i*.17f,-.8f));s.Advance(.05);}
        var target=far?s.Far:s.Near;s.PointerMove(target);
        s.Advance(Math.Max(0,(timing-s.PhaseProgress)*s.ReadyDuration));s.PointerUp(target);
    }
    public static void Run(Action<string,Action> test,Action<bool,string> check,long epoch)
    {
        var now=new DateTime(2026,11,2);
        test("wand near and far perfect catches score from explicit input",()=>{
            var near=new WandChallengeSession(1,1,0,7);Catch(near);check(near.Score==15&&near.Catches==1&&near.Perfects==1,"near judgment");
            var far=new WandChallengeSession(1,1,0,7);Catch(far,true);check(far.Score==30&&far.LastGrade==CatchGrade.PerfectFar,"far judgment");
        });
        test("wand inclusive timing boundaries and just-outside timing",()=>{
            foreach(float t in new[]{.45f,.65f,.85f,.95f}){var s=new WandChallengeSession(1,1,0,7);Catch(s,false,t);check(s.Catches==1,"inclusive boundary missed: "+t);check(s.Perfects==((t==.65f||t==.85f)?1:0),"perfect boundary wrong");}
            foreach(float t in new[]{.449f,.951f}){var s=new WandChallengeSession(1,1,0,7);Catch(s,false,t);check(s.Catches==0&&s.Misses==1,"outside timing accepted");}
        });
        test("wand normal timing and outside landing cannot earn perfect points",()=>{
            var s=new WandChallengeSession(1,1,0,9);Catch(s,false,.5f);check(s.Score==10&&s.Perfects==0,"normal scored wrong");
            s.Advance(2);s.PointerDown(new Point(-1,-.8f));for(int i=1;i<=6;i++){s.PointerMove(new Point(-1+i*.17f,-.8f));s.Advance(.05);}s.Advance(.6);s.PointerUp(new Point(0,0));
            check(s.Misses==1&&s.Score==10&&s.Combo==0,"off target scored");
        });
        test("wand taps stationary holding invalid coordinates and teleports do not farm",()=>{
            var s=new WandChallengeSession(1,1,0,7);s.PointerDown(new Point(0,0));s.Advance(3);s.PointerUp(new Point(0,0));check(s.Score==0&&s.Misses==0,"stationary earned or punished");
            s.PointerDown(new Point(-1,-.8f));s.PointerMove(new Point(1,.8f));s.Advance(.3);check(s.Phase==WandPhase.Chase,"single teleport armed catch");s.PointerCancel();s.PointerDown(new Point(float.NaN,0));s.PointerMove(new Point(0,float.PositiveInfinity));check(!s.Dragging,"invalid input accepted");
        });
        test("wand early late and three misses stop a run without deleting earned points",()=>{
            var s=new WandChallengeSession(1,1,0,12);Catch(s);s.Advance(2);
            for(int i=0;i<3;i++){Catch(s,false,.1f);if(s.Active)s.Advance(2);}check(!s.Active&&s.Result!=null&&s.Result.Misses==3&&s.Score==15&&s.ClosestMiss!=null,"miss finish");
            var timeout=new WandChallengeSession(1,1,0,12);timeout.PointerDown(new Point(-1,-.8f));for(int i=1;i<=5;i++){timeout.PointerMove(new Point(-1+i*.17f,-.8f));timeout.Advance(.05);}timeout.Advance(2);check(timeout.Misses==1,"ready window never expires");
        });
        test("wand stage thresholds change after 3 6 and 10 and clear on miss",()=>{
            var s=new WandChallengeSession(1,1,0,33,new PlayTuning{roundSeconds=120});for(int i=1;i<=10;i++){Catch(s);check(s.Catches==i,"failed sequence");if(i==3)check(s.Stage==1,"stage one");if(i==6)check(s.Stage==2,"stage two");}check(s.Stage==3&&s.BestCombo==10,"stage three");s.Advance(2);Catch(s,false,.1f);check(s.Combo==0&&s.Stage==0&&s.BestCombo==10,"miss stage reset");
        });
        test("wand sixty real seconds and abort have different settlement outcomes",()=>{
            var s=new WandChallengeSession(1,1,0,9);s.Advance(59.9);check(s.Active,"early end");s.Advance(.1);check(s.Phase==WandPhase.Finished&&s.Result.Catches==0,"real duration");
            var aborted=new WandChallengeSession(2,1,0,9);Catch(aborted);aborted.Abort();aborted.Advance(100);check(aborted.Result==null&&aborted.Phase==WandPhase.Aborted,"abort manufactured result");
        });
        test("wand timing grades agree at 15 30 60 and 120 fps",()=>{
            foreach(int fps in new[]{15,30,60,120}){
                var s=new WandChallengeSession(1,1,0,79);s.PointerDown(new Point(-1,-.8f));for(int i=1;i<=5;i++){s.PointerMove(new Point(-1+i*.17f,-.8f));double left=.06;while(left>1e-8){double step=Math.Min(left,1.0/fps);s.Advance(step);left-=step;}}
                double wait=(.75-s.PhaseProgress)*s.ReadyDuration;while(wait>1e-8){double step=Math.Min(wait,1.0/fps);s.Advance(step);wait-=step;}s.PointerUp(s.Far);check(s.Score==30,"frame rate changed timing: "+fps);
            }
        });
        test("wand real pointer sampling at low and high fps accepts the same sweep",()=>{
            foreach(int fps in new[]{15,30,60,120}){
                var s=new WandChallengeSession(1,1,0,73);s.PointerDown(new Point(-1,-.8f));
                int samples=(int)Math.Ceiling(.3*fps);for(int i=1;i<=samples;i++){s.PointerMove(new Point(-1+.85f*i/samples,-.8f));s.Advance(.3/samples);}
                check(s.Phase==WandPhase.Ready,"low fps discarded actual sweep: "+fps);s.PointerMove(s.Near);s.Advance((.75-s.PhaseProgress)*s.ReadyDuration);s.PointerUp(s.Near);check(s.Score==15,"sampling rate changed catch: "+fps);
            }
        });
        test("wand same seed reproduces targets and box paths avoid inflated obstacle",()=>{
            for(uint seed=1;seed<=30;seed++){
                var a=new WandChallengeSession(1,1,2,seed);var b=new WandChallengeSession(1,1,2,seed);check(Point.Distance(a.Near,b.Near)==0&&Point.Distance(a.Far,b.Far)==0,"nondeterministic targets");
                var route=WandArena.Route(new Point(-.9f,-.7f),new Point(.9f,.8f),2);var last=new Point(-.9f,-.7f);foreach(var q in route){check(WandArena.BoxClear(last,q),"box clipped");last=q;}check(Point.Distance(last,new Point(.9f,.8f))<.001f,"box path unreachable");
            }
        });
        test("play exactly once settlement and incomplete sessions survive serialization safely",()=>{
            var p=new PlayProgress();var tuning=new PlayTuning();var s=p.Begin(1,0,3,tuning,now);Catch(s);Catch(s);Catch(s);s.Advance(60);check(p.Settle(s.Result,tuning,now)&&!p.Settle(s.Result,tuning,now),"duplicate settlement");check(p.completedRuns==1&&p.qualifiedRuns==1&&p.Cat(1).catches==3,"wrong progress");
            var pending=p.Begin(1,0,3,tuning,now,true);Catch(pending);var codec=new Codec();var h=WorldSimulation.Migrate(Simulation.New(epoch));h.play=p;var restored=codec.DecodeHome(codec.EncodeHome(h));restored.play.Abort(now);check(restored.play.completedRuns==1&&restored.play.activeRunId==0,"incomplete restored as completed");
        });
        test("play unlocks exact configured thresholds and isolates each cat and record",()=>{
            var p=new PlayProgress();var t=new PlayTuning();for(int i=0;i<20;i++){var s=p.Begin(1,0,7,t,now);for(int j=0;j<5;j++)Catch(s);s.Advance(60);check(p.Settle(s.Result,t,now),"settle rejected");if(i==1)check(p.facilityLevel==1,"mat threshold");if(i==7)check(p.facilityLevel==2,"box threshold");}check(p.facilityLevel==3&&p.Cat(1).skillLevel==3&&p.Cat(2).catches==0,"progress bleed");check(p.Best(1,0,7,1)>0&&p.Best(1,0,8,1)==0&&p.Best(2,0,7,1)==0&&p.Best(1,0,7,2)==0,"record keys mixed");
        });
        test("retuning thresholds never revokes earned facilities or skills",()=>{
            var p=new PlayProgress{facilityLevel=3,qualifiedRuns=20,completedRuns=20};p.Cat(1).catches=100;p.Cat(1).skillLevel=3;p.Recommendation(now);
            var t=new PlayTuning{facilityThresholds=new[]{200,800,2000},skillThresholds=new[]{1000,4000,10000}};var s=p.Begin(1,3,17,t,now);s.Advance(60);check(p.Settle(s.Result,t,now),"retuned run did not settle");check(p.facilityLevel==3&&p.Cat(1).skillLevel==3,"retuning revoked earned content");PlayProgress.Validate(p,new HashSet<int>{1});
        });
        test("daily choices freeze on first discovery keep seven days and ignore world speed",()=>{
            var p=new PlayProgress();p.Visit(now);var today=p.Recommendation(now);p.facilityLevel=3;check(p.Recommendation(now).layout==today.layout,"today mutated after unlock");uint seed=today.seed;p.Visit(now.AddDays(7));check(p.daily.Count==7&&p.daily.All(d=>string.CompareOrdinal(d.date,"2026-11-03")>=0),"history not bounded");check(p.Recommendation(now).seed==seed&&p.daily.Count==7,"date rollback changed seed or history");p.Visit(now.AddDays(7));check(p.log.Count(e=>e.kind=="visit"&&e.date=="2026-11-09")==1,"duplicate visit");
        });
        test("schema two checksum-valid files migrate without changing retained bytes",()=>{
            var codec=new Codec();var h=WorldSimulation.Migrate(Simulation.New(epoch));h.cats[0].life.name="Kept";h.cats[0].life.affection=67;var body=JsonNode.Parse(codec.EncodeHome(h))!.AsObject();body["schema"]=2;body.Remove("play");string json=body.ToJsonString();string raw=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant()+"\n"+json;
            string dir=Path.Combine(Path.GetTempPath(),"momo-schema3-"+Guid.NewGuid());Directory.CreateDirectory(dir);string path=Path.Combine(dir,"household-0.save");File.WriteAllText(path,raw);var store=new HouseholdStore(dir,codec,codec);var loaded=store.Load();check(loaded.schema==3&&loaded.play.completedRuns==0&&loaded.cats[0].life.name=="Kept"&&loaded.cats[0].life.affection==67,"migration lost fields");check(File.ReadAllText(path)==raw,"load mutated old checkpoint");store.Save(loaded);check(store.Load().schema==3,"migration save roundtrip");check(Directory.GetFiles(dir,"*.schema2-*.bak").Any(f=>File.ReadAllText(f)==raw),"schema two bytes not backed up before save");
        });
        test("play validation rejects corrupted ledgers unknown cats and invalid tuning",()=>{
            var h=WorldSimulation.Migrate(Simulation.New(epoch));h.play.Cat(100);bool failed=false;try{WorldSimulation.Validate(h);}catch(ArgumentException){failed=true;}check(failed,"unknown cat accepted");
            failed=false;try{new PlayTuning{roundSeconds=float.NaN}.Validate();}catch(ArgumentException){failed=true;}check(failed,"invalid tuning accepted");
            var p=new PlayProgress();for(int i=0;i<600;i++)p.Log("visit",0,0,0,now);check(p.log.Count==512,"unbounded telemetry");PlayProgress.Validate(p,new HashSet<int>{1});
        });
    }
}
