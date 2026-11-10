using Momo.Core;
using System.Text.Json.Nodes;

static class BathTests
{
    static WorldSimulation Home(long epoch,int count=1)
    {
        var s=Simulation.New(epoch);s.adopted=true;s.pet=s.target=new Point(0,16.8f);s.dirt=70;s.energy=90;s.hunger=s.thirst=10;s.affection=40;s.remaining=1000;
        var w=new WorldSimulation(WorldSimulation.Migrate(s));
        for(int i=1;i<count;i++){var c=w.Adopt("Other "+i,i,epoch);c.life.remaining=1000;c.socialCooldown=1000;}
        return w;
    }
    static BathSession Start(WorldSimulation w)
    {
        if(!w.TryBeginBath(1,out var why))throw new Exception(why);
        for(int i=0;i<40&&!w.Bath.Washing;i++){w.Advance(.6,true);w.Bath.Update(.1f,default);}
        if(!w.Bath.Washing)throw new Exception("Bath never reached wet phase");return w.Bath;
    }
    static void Stage(BathSession b)
    {
        var tool=b.Tool;
        for(int z=0;z<3;z++)for(int p=0;p<BathSession.Patches;p++)for(int n=0;n<17;n++)
            b.Update(.1f,new BathContact{touching=true,tool=tool,zone=(BathZone)z,patch=p,speed=.2f});
    }
    public static void Run(Action<string,Action> test,Action<bool,string> assert,long epoch)
    {
        test("bath requires awake available cat and one station owner",()=>{
            foreach(var block in new Action<WorldState>[] {s=>s.activity=Activity.Sleep,s=>s.activity=Activity.Eat,s=>s.energy=20,s=>s.hunger=90,s=>s.intent=Activity.Hide})
            {var w=Home(epoch);block(w.Focused.life);assert(!w.TryBeginBath(1,out var why)&&!string.IsNullOrEmpty(why),"unavailable cat accepted");}
            var home=Home(epoch,2);Start(home);assert(!home.TryBeginBath(2,out _),"second bath accepted");assert(!home.TryInteract(1,2,SocialKind.Sniff),"bath joined social session");
            home.SetHumanBusy(1,false);assert(!home.TryInteract(1,2,SocialKind.Sniff),"frame update released bath owner");
        });
        test("bath advances only matching tool and actual distributed contact",()=>{
            var w=Home(epoch);var b=Start(w);assert(!b.AdvanceStage(),"skipped wet stage");
            for(int i=0;i<80;i++)b.Update(.1f,new BathContact{touching=true,tool=BathTool.Towel,patch=0,speed=.2f});
            assert(b.Progress==0,"wrong tool progressed");
            for(int i=0;i<80;i++)b.Update(.1f,new BathContact{touching=true,tool=BathTool.Shower,patch=0,speed=0});
            assert(b.Progress<.09f&&!b.Ready,"one spot completed whole body");Stage(b);assert(b.Ready&&b.AdvanceStage(),"wet coverage did not unlock soap");
            for(int i=0;i<50;i++)b.Update(.1f,new BathContact{touching=true,tool=BathTool.Soap,patch=0,speed=0});
            assert(b.Progress==0,"stationary soap progressed");
            b.Update(.1f,new BathContact{touching=true,tool=BathTool.Soap,patch=-1,speed=.2f});assert(b.Progress==0,"invalid patch accepted");
        });
        test("complete bath cleans only its cat and records exactly once",()=>{
            var w=Home(epoch,2);w.Cat(2).life.dirt=45;var b=Start(w);
            for(int stage=0;stage<4;stage++){Stage(b);if(stage<3)assert(b.AdvanceStage(),"could not change tool");}
            assert(b.Committed&&w.Focused.life.dirt==0&&w.Cat(2).life.dirt==45,"wrong cat cleaned");
            assert(w.Focused.life.events.Count(e=>e.kind=="bath")==1,"completion event missing");
            for(int i=0;i<30;i++)b.Update(.1f,default);
            assert(!b.Active&&!w.Brain(1).BathingActive&&w.Focused.life.events.Count(e=>e.kind=="bath")==1,"completion repeated or lock leaked");
        });
        test("clean cat can bathe without farming bond or memories",()=>{
            var w=Home(epoch);w.Focused.life.dirt=0;var b=Start(w);float bond=w.Focused.life.affection;
            for(int s=0;s<4;s++){Stage(b);if(s<3)b.AdvanceStage();}
            assert(b.Committed&&w.Focused.life.affection==bond&&!w.Focused.life.events.Any(e=>e.kind=="bath"),"clean wash farmed reward");
        });
        test("rough contact pauses twice and third time ends without cleaning",()=>{
            var w=Home(epoch);var b=Start(w);
            for(int pause=0;pause<3;pause++)
            {
                for(int i=0;i<22;i++)b.Update(.1f,new BathContact{touching=true,head=true,speed=.2f});
                if(pause<2){assert(b.Paused,"rough contact did not pause");for(int i=0;i<21;i++)b.Update(.1f,default);assert(!b.Paused,"quiet did not restore");}
            }
            assert(!b.Active&&!b.Committed&&w.Focused.life.dirt==70&&!w.Brain(1).BathingActive,"rough exit awarded clean or leaked lock");
        });
        test("bath cancellation and background release every transient state",()=>{
            var w=Home(epoch);var b=Start(w);Stage(b);b.AdvanceStage();Stage(b);w.CancelTransient();
            assert(!b.Active&&!w.Brain(1).BathingActive&&w.Focused.life.dirt==70,"cancel cleaned or kept ownership");WorldSimulation.Validate(w.State);
            w.Resume(epoch+TimeSpan.FromHours(6).Ticks);assert(w.Focused.life.dirt==70&&!w.Brain(1).BathingActive,"offline resumed partial bath");
        });
        test("bath invitation follows navigation and times out without teleporting",()=>{
            var w=Home(epoch);var s=w.Focused.life;s.pet=s.target=new Point(0,0);assert(w.TryBeginBath(1,out _),"invitation refused");var start=s.pet;
            for(int i=0;i<310;i++)w.Bath.Update(.1f,default);
            assert(!w.Bath.Active&&Point.Distance(start,s.pet)<.001f&&s.dirt==70,"timeout teleported or cleaned");
            w=Home(epoch);s=w.Focused.life;s.pet=s.target=new Point(0,14);assert(w.TryBeginBath(1,out _),"nearby invite refused");
            for(int i=0;i<150&&!w.Bath.Washing;i++){var before=s.pet;w.Advance(.6,true);assert(Point.Distance(before,s.pet)<=.109f,"bath teleported body");w.Bath.Update(.1f,default);}
            assert(w.Bath.Washing&&Point.Distance(s.pet,BathSession.Station)<.05f,"cat failed to walk into station");
        });
        test("garden dirt uses foreground activity and never offline or indoor time",()=>{
            var w=Home(epoch);var s=w.Focused.life;s.dirt=0;s.activity=s.intent=Activity.Play;s.remaining=1000;w.Advance(60*Simulation.Speed,true);
            assert(Math.Abs(s.dirt-2)<.02f,"wrong foreground dirt rate");float dirt=s.dirt;w.Resume(epoch+TimeSpan.FromHours(6).Ticks);assert(s.dirt==dirt,"offline dirt");
            w=Home(epoch);s=w.Focused.life;s.dirt=0;w.SetHumanBusy(w.Focused.id,true);w.Advance(60*Simulation.Speed,true);assert(s.dirt==0,"idle dirt");
            s.pet=s.target=new Point(0,0);s.activity=s.intent=Activity.Play;s.remaining=1000;w.Advance(60*Simulation.Speed,true);assert(s.dirt==0,"indoor dirt");
        });
        test("old household saves default to clean and omit transient bath",()=>{
            var w=Home(epoch);Start(w);var codec=new Codec();var json=JsonNode.Parse(codec.EncodeHome(w.State));json["cats"][0]["life"].AsObject().Remove("dirt");
            var restored=new WorldSimulation(codec.DecodeHome(json.ToJsonString()));assert(restored.Focused.life.dirt==0&&restored.Bath==null&&!restored.Brain(1).BathingActive,"old save dirty or resumed bath");
            var roundtrip=new WorldSimulation(codec.DecodeHome(codec.EncodeHome(w.State)));assert(roundtrip.Focused.life.dirt==70&&roundtrip.Bath==null,"partial wash persisted");
            foreach(float value in new[]{-1,101,float.NaN}){w.Focused.life.dirt=value;bool rejected=false;try{WorldSimulation.Validate(w.State);}catch(ArgumentException){rejected=true;}assert(rejected,"invalid dirt accepted");}
        });
        test("checkpoint during invitation strips only temporary bath movement",()=>{
            var w=Home(epoch,2);var s=w.Focused.life;s.pet=s.target=new Point(0,14);w.TryBeginBath(1,out _);
            for(int i=0;i<22;i++)w.Bath.Update(.1f,default);
            assert(s.activity==Activity.Walk,"fixture did not start approach");
            var codec=new Codec();var path=Path.Combine(Path.GetTempPath(),"momo-bath-"+Guid.NewGuid());var store=new HouseholdStore(path,codec,codec);
            store.Save(w.State,1);var restored=new WorldSimulation(store.Load());
            assert(restored.Focused.life.activity==Activity.Idle&&restored.Focused.life.route.Count==0&&Point.Distance(restored.Focused.life.pet,restored.Focused.life.target)<.001f,"saved temporary bath route");
            assert(s.activity==Activity.Walk&&s.route.Count>0&&w.Bath.Active,"snapshot altered live approach");
            assert(restored.Cat(2).life.name==w.Cat(2).life.name&&restored.Focused.life.dirt==70,"snapshot lost identity or dirt");
        });
        test("other cats avoid the bath reservation and continue life",()=>{
            var w=Home(epoch,2);Start(w);var other=w.Cat(2);other.life.pet=other.life.target=new Point(3,16.8f);other.life.activity=other.life.intent=Activity.Idle;
            float hunger=other.life.hunger;w.Navigate(2,new Point(-2,16.8f));
            for(int i=0;i<300;i++){w.Advance(.6,true);w.Bath.Update(.1f,default);assert(Point.Distance(other.life.pet,BathSession.Station)>=1.67f,"cat crossed wash reservation");}
            assert(other.life.hunger>hunger,"other cat frozen");w.CancelTransient();assert(!w.Brain(1).BathingActive,"cancel lock");
        });
        test("adoption finds a safe position while an observer is present",()=>{
            var w=Home(epoch);w.ObserverPosition=new Point(0,2.2f);var cat=w.Adopt("New companion",1,epoch);
            assert(w.Cat(cat.id)!=null&&w.Brain(cat.id)!=null&&Point.Distance(cat.life.pet,w.ObserverPosition.Value)>=1.2f,"adoption accessed an unattached brain or overlapped observer");WorldSimulation.Validate(w.State);
        });
        test("walk-in basin routes through the open entrance and migrates old wall positions",()=>{
            var from=new Point(3,16.8f);var path=CatNavigation.Path(from,BathSession.Station);
            assert(path.Count>1,"cat cut through the basin wall");
            foreach(var p in path){assert(CatNavigation.Clear(from,p),"basin path intersects a wall");from=p;}
            assert(Point.Distance(from,BathSession.Station)<.001f,"cannot reach basin bottom");
            assert(CatNavigation.Clear(BathSession.Station,BathSession.Exit),"entrance blocked on departure");
            assert(!CatNavigation.Walkable(new Point(2.3f,15.7f)),"cat body could pass through the raised supply tray");
            assert(Room.Walkable(new Point(2.3f,15.7f)),"supply tray invalidated legacy checkpoints");
            var w=Home(epoch);w.Focused.life.pet=w.Focused.life.target=new Point(1.15f,16.8f);
            WorldSimulation.Validate(w.State); // A pre-basin checkpoint remains readable.
            var restored=new WorldSimulation(w.State);
            assert(CatNavigation.Walkable(restored.Focused.life.pet),"legacy resident left inside new wall");
        });
        test("bath never reserves across a neighbour already in its margin",()=>{
            var w=Home(epoch,2);var other=w.Cat(2);other.life.pet=other.life.target=new Point(0,15.18f);
            assert(!w.TryBeginBath(1,out _)&&!w.Brain(1).BathingActive,"reserved across existing neighbour");
            w.Navigate(2,new Point(3,16.8f));for(int i=0;i<80;i++)w.Advance(.6,true);
            assert(Point.Distance(other.life.pet,BathSession.Station)>2.9f,"neighbour could not leave the wash corner");
            assert(w.TryBeginBath(1,out _),"vacated wash corner remained locked");
        });
    }
}
