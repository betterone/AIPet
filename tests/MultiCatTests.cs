using Momo.Core;
using System.Text.Json;
using System.Security.Cryptography;

static class MultiCatTests
{
    static WorldSimulation Home(long epoch,int count=5)
    {
        var legacy=Simulation.New(epoch);legacy.adopted=true;legacy.name="Original";legacy.pet=legacy.target=new Point(-2,0);
        var world=new WorldSimulation(WorldSimulation.Migrate(legacy));
        for(int i=1;i<count;i++)world.Adopt("Cat "+(i+1),i,epoch);
        foreach(var c in world.State.cats){c.life.remaining=10000;c.socialCooldown=10000;}
        return world;
    }
    static void Place(WorldSimulation world,int id,Point at,Point? target=null)
    {
        var c=world.Cat(id);c.life.pet=c.life.target=at;c.life.route.Clear();c.life.activity=c.life.intent=Activity.Idle;c.life.remaining=1000;c.life.energy=75;c.life.hunger=30;c.life.thirst=20;c.blockedSeconds=0;
        if(target.HasValue){c.life.target=target.Value;c.life.route=CatNavigation.Path(at,target.Value);c.life.activity=Activity.Walk;c.life.intent=Activity.Explore;}
    }
    static float MinGap(WorldSimulation world)
    {
        float min=float.MaxValue;var cats=world.State.cats;
        for(int i=0;i<cats.Count;i++)for(int j=i+1;j<cats.Count;j++)min=Math.Min(min,Point.Distance(cats[i].life.pet,cats[j].life.pet)-cats[i].radius-cats[j].radius);
        return min;
    }
    public static void Run(Action<string,Action> test,Action<bool,string> assert,long epoch)
    {
        test("household migration preserves identity memories and legacy bytes",()=>{
            var codec=new Codec();var dir=Path.Combine(Path.GetTempPath(),"momo-household-"+Guid.NewGuid());var old=new SaveStore(dir,codec);var life=Simulation.New(epoch);
            life.adopted=true;life.name="My cat";life.affection=63;life.wandPreference=4;new Simulation(life).Record("test","Real retained memory");old.Save(life);
            string path=Path.Combine(dir,"world-1.save");byte[] bytes=File.ReadAllBytes(path);
            var store=new HouseholdStore(dir,codec,codec);var home=store.Load();assert(store.MigratedLegacy&&home.cats.Count==1,"not explicitly migrated");
            assert(home.cats[0].life.name==life.name&&home.cats[0].life.affection==63&&home.cats[0].life.events[^1].text=="Real retained memory","lost identity or memory");
            store.Save(home);assert(bytes.SequenceEqual(File.ReadAllBytes(path)),"modified legacy checkpoint");
            assert(store.Load().schema==3&&store.Load().cats[0].life.wandPreference==4,"household roundtrip lost fields");
        });
        test("household checkpoints recover corruption and never roll back silently to legacy",()=>{
            var codec=new Codec();var dir=Path.Combine(Path.GetTempPath(),"momo-household-"+Guid.NewGuid());var store=new HouseholdStore(dir,codec,codec);var world=Home(epoch);
            store.Save(world.State);world.State.food=77;store.Save(world.State);
            File.WriteAllText(Path.Combine(dir,"household-0.save"),"bad");assert(store.Load().revision==1&&store.LastWarning!=null,"no checkpoint recovery");
            File.WriteAllText(Path.Combine(dir,"household-1.save"),"bad");new SaveStore(dir,codec).Save(Simulation.New(epoch));
            bool failed=false;try{store.Load();}catch(IOException){failed=true;}assert(failed,"corrupt household silently reset to old cat");
        });
        test("five cats have independent identity personality habits and random streams",()=>{
            var world=Home(epoch);assert(world.State.cats.Select(c=>c.id).Distinct().Count()==5,"duplicate identities");
            assert(world.State.cats.Select(c=>c.life.random).Distinct().Count()==5,"shared random seed");
            float profile=BreedArchetypes.Get(1).personality.sociability;world.Cat(2).life.personality.sociability=0;
            assert(BreedArchetypes.Get(1).personality.sociability==profile,"modified archetype template");
            float bond=world.Cat(2).life.affection;world.Brain(1).Pet();assert(world.Cat(2).life.affection==bond,"bond leaked to another cat");
            bool full=false;try{world.Adopt("Sixth",5,epoch);}catch(InvalidOperationException){full=true;}assert(full&&world.State.cats.Count==5,"capacity not enforced");
        });
        test("adoption preserves shared settings and legacy physical-ball edge tolerance",()=>{
            var old=Simulation.New(epoch);old.ball=new Point(Room.MinX-.1f,0);old.sound=false;old.adopted=true;
            var w=new WorldSimulation(WorldSimulation.Migrate(old));var added=w.Adopt("Second",1,epoch);
            assert(!added.life.sound&&Point.Distance(added.life.ball,w.State.ball)<.001f,"adoption reset shared sound or toy");WorldSimulation.Validate(w.State);
        });
        test("household rejects duplicate ids future schemas and invalid relationships",()=>{
            foreach(var change in new Action<HouseholdState>[] {s=>s.schema=4,s=>s.cats[1].id=s.cats[0].id,s=>s.cats[0].radius=float.NaN,s=>s.focusedCatId=100,s=>s.relationships.Add(new CatRelationship{from=1,to=100}),s=>s.remainder=double.NaN})
            {var h=Home(epoch).State;change(h);bool rejected=false;try{WorldSimulation.Validate(h);}catch(ArgumentException){rejected=true;}assert(rejected,"invalid household accepted");}
        });
        test("body-clearance navigation connects house and garden without clipping corners",()=>{
            var spots=Room.ExploreSpots.Select(CatNavigation.Nearest).ToArray();
            foreach(var a in spots)foreach(var b in spots){var path=CatNavigation.Path(a,b);assert(path.Count>0,"body envelope cannot reach a room");var last=a;foreach(var q in path){assert(CatNavigation.Clear(last,q),"body path clips furniture");last=q;}}
        });
        test("five cats converge on one bowl with distinct reservations and zero overlap",()=>{
            var w=Home(epoch);foreach(var c in w.State.cats)w.Navigate(c.id,Room.Food,Activity.Eat);
            var targets=w.State.cats.Select(c=>c.life.target).ToArray();
            for(int i=0;i<5;i++)for(int j=i+1;j<5;j++)assert(Point.Distance(targets[i],targets[j])>=1.56f,"shared destination reservation");
            for(int n=0;n<5000;n++){w.Advance(.1);assert(MinGap(w)>=-.0001f,"cat bodies overlapped at bowl");foreach(var c in w.State.cats)assert(CatNavigation.Walkable(c.life.pet),"body inside furniture");}
            assert(w.State.cats.Count(c=>c.life.hunger<20)>=3,"cats never reached food: "+string.Join("; ",w.State.cats.Select(c=>$"{c.id} {c.life.pet.x:F2},{c.life.pet.z:F2} {c.life.activity}/{c.life.intent} wait={c.waitingFor} hunger={c.life.hunger:F1}")));WorldSimulation.Validate(w.State);
        });
        test("head-on doorway traffic yields without tunnelling or permanent deadlock",()=>{
            var w=Home(epoch,2);Place(w,1,new Point(4,0),new Point(8,0));Place(w,2,new Point(8,0),new Point(4,0));
            bool firstArrived=false,secondArrived=false;
            for(int n=0;n<2400;n++)
            {
                var before=w.State.cats.Select(c=>c.life.pet).ToArray();w.Advance(.1);
                assert(MinGap(w)>=-.0001f,"doorway penetration");
                for(int i=0;i<2;i++)assert(Point.Distance(before[i],w.State.cats[i].life.pet)<=.031f,"cat teleported while yielding");
                if(Environment.GetEnvironmentVariable("MOMO_TRAFFIC_TRACE")=="1"&&n%100==0)Console.WriteLine($"TRAFFIC {n*.1:F1}: {w.Cat(1).life.pet.x:F2},{w.Cat(1).life.pet.z:F2} / {w.Cat(2).life.pet.x:F2},{w.Cat(2).life.pet.z:F2}");
                firstArrived|=w.Cat(1).life.pet.x>7.8f;secondArrived|=w.Cat(2).life.pet.x<4.2f;
                if(firstArrived&&secondArrived){Console.WriteLine($"Doorway exchange: {n*.1/6:F2} real seconds");return;}
            }
            throw new Exception("Doorway deadlock: "+string.Join("; ",w.State.cats.Select(c=>$"{c.id}: {c.life.pet.x:F2},{c.life.pet.z:F2} -> {c.life.target.x:F2},{c.life.target.z:F2}")));
        });
        test("long foreground frames sweep through intermediate collision checks",()=>{
            var w=Home(epoch,2);Place(w,1,new Point(-2,0),new Point(2,0));Place(w,2,new Point(0,0));w.Advance(12);
            assert(MinGap(w)>=0&&w.MotionSteps==120,"slow frame skipped collision steps");
        });
        test("autonomous cats route around the observer body without walking into the camera",()=>{
            var w=Home(epoch,1);Place(w,1,new Point(-2,0),new Point(2,0));w.ObserverPosition=new Point(0,0);
            bool arrived=false;for(int i=0;i<1800;i++){w.Advance(.1);assert(Point.Distance(w.Cat(1).life.pet,w.ObserverPosition.Value)>=1.029f,"cat entered observer body");if(w.Cat(1).life.pet.x>1.9f){arrived=true;break;}}
            assert(arrived,"observer avoidance deadlocked cat");
        });
        test("a person entering an existing destination triggers a safe replacement goal",()=>{
            var w=Home(epoch,1);Place(w,1,new Point(0,2),new Point(3,19));w.ObserverPosition=new Point(3,18);
            for(int i=0;i<2400;i++){w.Advance(.1);assert(Point.Distance(w.Cat(1).life.pet,w.ObserverPosition.Value)>=1.029f,"walked through observer");if(w.Cat(1).life.pet.z>18.9f)return;}
            throw new Exception("Human blocked old goal permanently");
        });
        test("sniff invitation coordinates two cats and records only completed interaction",()=>{
            var w=Home(epoch,2);Place(w,1,new Point(-1,0));Place(w,2,new Point(1,0));foreach(var c in w.State.cats){c.socialCooldown=0;c.life.personality.sociability=.9f;}
            assert(w.TryInteract(1,2,SocialKind.Sniff),"invitation failed");assert(!w.Cat(1).life.events.Any(e=>e.kind=="cat-social"),"invented event on invitation");
            for(int i=0;i<500;i++){w.Advance(.1);assert(MinGap(w)>=0,"sniff body penetration");}
            assert(w.Relationship(1,2).meetings==1&&w.Relationship(2,1).meetings==1,"completed sniff not remembered");
            assert(w.Cat(1).life.events.Any(e=>e.otherCatId==2&&e.actorId==1),"missing event participants");
        });
        test("recipient can decline chase and refuses while asleep or eating",()=>{
            var w=Home(epoch,2);Place(w,1,new Point(-1,0));Place(w,2,new Point(1,0));foreach(var c in w.State.cats)c.socialCooldown=0;
            w.Cat(2).life.personality.playfulness=.05f;w.Cat(2).life.energy=40;
            assert(w.TryInteract(1,2,SocialKind.Chase),"no invitation");w.Advance(5);
            assert(w.Sessions.Count==0&&!w.Cat(1).life.events.Any(e=>e.kind=="cat-social"),"forced unwilling recipient to play");
            w.Cat(1).socialCooldown=w.Cat(2).socialCooldown=0;w.Cat(2).life.activity=Activity.Sleep;
            assert(!w.TryInteract(1,2,SocialKind.Sniff),"woke sleeping recipient");w.Cat(2).life.activity=Activity.Eat;assert(!w.TryInteract(1,2,SocialKind.Sniff),"interrupted meal");
        });
        test("human interruption releases both session roles and preserves urgent needs",()=>{
            var w=Home(epoch,2);Place(w,1,new Point(-1,0));Place(w,2,new Point(1,0));foreach(var c in w.State.cats)c.socialCooldown=0;
            assert(w.TryInteract(1,2,SocialKind.Sniff),"no invitation");w.Advance(5);w.SetHumanBusy(2,true);
            assert(w.Sessions.Count==0&&!w.Cat(1).life.events.Any(e=>e.kind=="cat-social"),"interrupted session leaked or fabricated success");
            assert(w.Cat(1).life.intent!=Activity.Greet,"other cat remained waiting");
        });
        test("shared toy has one invited pursuer while observers keep their own state",()=>{
            var w=Home(epoch);w.RollBall(new Point(0,0));assert(w.State.cats.Count(c=>c.life.intent==Activity.Play)==1,"multiple toy owners");
            foreach(var c in w.State.cats)assert(Point.Distance(c.life.ball,w.State.ball)<.001f,"each cat has a different ball");
        });
        test("long rest and watching reserve separate locations",()=>{
            foreach(var activity in new[]{Activity.Sleep,Activity.Watch})
            {
                var w=Home(epoch);foreach(var c in w.State.cats)w.Brain(c.id).Request(activity,activity==Activity.Sleep?Room.Bed:Room.Window);
                for(int i=0;i<w.State.cats.Count;i++)for(int j=i+1;j<w.State.cats.Count;j++)
                    assert(Point.Distance(w.State.cats[i].life.target,w.State.cats[j].life.target)>1.59f,"shared stationary destination");
            }
        });
        test("foreground opportunities replay identically across frame partitions",()=>{
            var a=Home(epoch,2);var b=Home(epoch,2);a.ObserverPosition=b.ObserverPosition=new Point(3,13);
            a.Advance(300,true);for(int i=0;i<3000;i++)b.Advance(.1,true);
            assert(new Codec().EncodeHome(a.State)==new Codec().EncodeHome(b.State),"foreground replay changed with frame size");
        });
        test("foreground cats disperse and visit the garden without commands",()=>{
            var w=Home(epoch);var reached=new HashSet<int>();
            for(int i=0;i<2400;i++)
            {
                w.Advance(.6,true);
                foreach(var c in w.State.cats)if(c.life.pet.z>11.8f)reached.Add(c.id);
                assert(MinGap(w)>=.014f,"autonomous cats overlapped");
            }
            assert(reached.Count>=3,"too few cats explored garden: "+reached.Count);
            WorldSimulation.Validate(w.State);
        });
        test("calling routes through rooms and foreground companionship respects needs",()=>{
            var w=Home(epoch,1);Place(w,1,new Point(0,0));
            w.Cat(1).life.personality.sociability=1;w.Cat(1).life.personality.independence=0;
            w.ObserverPosition=new Point(3,13);
            assert(w.Call(1,w.ObserverPosition.Value),"call across rooms rejected");
            bool approached=false;for(int i=0;i<2400;i++){w.Advance(.1,true);if(Point.Distance(w.Cat(1).life.pet,w.ObserverPosition.Value)<3){approached=true;break;}}
            assert(approached,"cat failed to approach across rooms");
            w.ObserverPosition=new Point(5,13);bool followed=false;
            for(int i=0;i<900;i++){w.Advance(.1,true);if(Point.Distance(w.Cat(1).life.pet,w.ObserverPosition.Value)<2.5f){followed=true;break;}}
            assert(followed,"greeted cat did not follow moving player");
            w.Cat(1).life.hunger=90;assert(!w.Call(1,new Point(3,13)),"call interrupted urgent hunger");w.Cat(1).life.hunger=30;
            Place(w,1,new Point(0,0));w.Cat(1).life.activity=w.Cat(1).life.intent=Activity.Sleep;w.Cat(1).life.remaining=900;
            w.Advance(30,true);assert(w.Cat(1).life.activity==Activity.Sleep,"company woke sleeping cat");
        });
        test("garden outing requires both player and cat and completes all three stops",()=>{
            var w=Home(epoch,1);Place(w,1,new Point(3,13));
            assert(w.StartOuting(1),"outing unavailable");
            w.ObserverPosition=new Point(0,0);w.Advance(15,true);
            assert(w.Outing.Station==0,"reward without player");
            for(int station=0;station<3;station++)
            {
                var target=w.Outing.Target;w.ObserverPosition=new Point(target.x+2,target.z);
                int guard=0;while(w.Outing.Active&&w.Outing.Station==station&&guard++<5000)w.Advance(.1,true);
                assert(w.Outing.Station>station,"outing stuck at "+station);
            }
            assert(w.Outing.Completed&&!w.Outing.Active,"outing did not complete");
            assert(w.Cat(1).life.events.Any(e=>e.kind=="garden-outing"),"missing shared memory");
            WorldSimulation.Validate(w.State);
        });
        test("garden outing cancels for care and releases the cat",()=>{
            var w=Home(epoch,1);Place(w,1,new Point(3,13));w.StartOuting(1);
            w.Cat(1).life.hunger=90;w.Advance(.1,true);
            assert(!w.Outing.Active&&!w.Outing.Completed,"urgent hunger ignored");
            w.CancelTransient();WorldSimulation.Validate(w.State);
        });
        test("call is a voluntary response with range rest and temperament gates",()=>{
            var w=Home(epoch,2);Place(w,1,new Point(0,0));Place(w,2,new Point(2,0));
            w.Cat(1).life.personality.sociability=0;w.Cat(1).life.personality.independence=1;
            assert(!w.Call(1,new Point(0,2))&&w.Cat(1).life.activity==Activity.Idle,"forced independent cat to obey");
            w.Cat(2).life.activity=Activity.Sleep;assert(!w.Call(2,new Point(0,0)),"woke sleeping cat");
            w.Cat(1).life.personality.sociability=1;w.Cat(1).life.personality.independence=0;
            assert(w.Call(1,new Point(0,2)),"willing cat did not respond");
        });
        test("fixed-step household replay is independent of frame partition",()=>{
            var a=Home(epoch);var b=Home(epoch);a.Navigate(1,new Point(3,13));b.Navigate(1,new Point(3,13));a.Advance(60.7);for(int i=0;i<607;i++)b.Advance(.1);
            assert(new Codec().EncodeHome(a.State)==new Codec().EncodeHome(b.State),"household frame partition divergence");
        });
        test("offline resume settles safe positions and cannot replay time",()=>{
            var w=Home(epoch);w.Resume(epoch+TimeSpan.FromHours(6).Ticks);
            assert(w.Sessions.Count==0&&MinGap(w)>=0,"offline restore overlapping or transient session");foreach(var c in w.State.cats)assert(CatNavigation.Walkable(c.life.pet)&&c.life.route.Count==0,"unsafe resumed position or stale route");
            string before=new Codec().EncodeHome(w.State);w.Resume(epoch+TimeSpan.FromHours(6).Ticks);w.Resume(epoch);assert(before==new Codec().EncodeHome(w.State),"offline time replay");WorldSimulation.Validate(w.State);
        });
        test("offline care resumes from a safe approach instead of standing inside a bowl",()=>{
            var w=Home(epoch,2);var c=w.Cat(1);c.life.pet=c.life.target=Room.Food;c.life.activity=c.life.intent=Activity.Eat;c.life.hunger=50;c.life.remaining=150;
            w.Resume(epoch+TimeSpan.FromSeconds(1).Ticks);
            assert(c.life.activity!=Activity.Eat&&c.waitingFor==Activity.Eat,"restored eating on top of the bowl");
            w.Advance(10);assert(c.life.activity==Activity.Eat&&Point.Distance(c.life.pet,WorldSimulation.BowlSeat(Activity.Eat))<.01f,"did not resume beside bowl");
        });
        test("accepted chase and shared rest finish without overlapping or locking cats",()=>{
            foreach(var kind in new[]{SocialKind.Chase,SocialKind.RestTogether})
            {
                var w=Home(epoch,2);Place(w,1,new Point(-1,0));Place(w,2,new Point(1,0));
                foreach(var c in w.State.cats){c.socialCooldown=0;c.life.personality.playfulness=1;c.life.personality.sociability=.9f;}
                assert(w.TryInteract(1,2,kind),"could not invite "+kind);
                for(int i=0;i<1600;i++){w.Advance(.1);assert(MinGap(w)>=0,"body penetration during "+kind);}
                assert(w.Sessions.Count==0&&w.Relationship(1,2).meetings==1,"paired action did not finish: "+kind);
                if(kind==SocialKind.Chase)assert(w.Relationship(2,1).games==1,"chase result not remembered");
            }
        });
        test("touch ends a shared rest session without waking sleeping cats",()=>{
            var w=Home(epoch,2);Place(w,1,new Point(-1,0));Place(w,2,new Point(1,0));foreach(var c in w.State.cats)c.socialCooldown=0;
            assert(w.TryInteract(1,2,SocialKind.RestTogether),"no shared-rest invitation");w.Advance(20);
            assert(w.Cat(1).life.activity==Activity.Sleep&&w.Cat(2).life.activity==Activity.Sleep,"cats never settled");
            w.SetHumanBusy(1,true);w.Brain(1).Pet();assert(w.Sessions.Count==0&&w.Cat(1).life.activity==Activity.Sleep&&w.Cat(2).life.activity==Activity.Sleep,"touch woke a sleeping cat");
        });
        test("nearby food and water share physical access and waiting cats really reach their bowl",()=>{
            var w=Home(epoch);for(int i=0;i<5;i++)w.Navigate(i+1,i%2==0?Room.Food:Room.Water,i%2==0?Activity.Eat:Activity.Drink);
            for(int n=0;n<10000;n++)
            {
                w.Advance(.1);assert(MinGap(w)>=0,"queue collision");
                assert(w.State.cats.Count(c=>c.life.intent==Activity.Eat||c.life.intent==Activity.Drink)<=1,"adjacent bowls have conflicting owners");
                foreach(var c in w.State.cats)
                    if(c.life.activity==Activity.Eat||c.life.activity==Activity.Drink)
                        assert(Point.Distance(c.life.pet,WorldSimulation.BowlSeat(c.life.activity))<.01f,"ate or drank remotely from a waiting slot");
            }
            assert(w.State.cats.All(c=>c.life.events.Any(e=>e.kind=="care")),"a queued cat starved behind the same owner");
        });
        test("ten minutes of five-cat foreground life keep safe bodies and bounded state",()=>{
            var w=Home(epoch);foreach(var c in w.State.cats){c.life.remaining=1;c.socialCooldown=c.id*13;}
            for(int i=0;i<36000;i++){w.Advance(.1);assert(MinGap(w)>=-.0001f,"long-run body collision");}
            WorldSimulation.Validate(w.State);assert(w.State.cats.All(c=>c.life.events.Count<=128),"unbounded life histories");
            assert(w.State.cats.Select(c=>c.life.random).Distinct().Count()==5,"random streams became synchronized");
        });
        test("twenty varied five-cat households remain collision-free across independent activity",()=>{
            for(int seed=1;seed<=20;seed++)
            {
                var w=Home(epoch);var random=new Random(seed*104729);
                foreach(var c in w.State.cats)
                {
                    c.life.pet=c.life.target=w.FindFree(new Point(Room.MinX+(float)random.NextDouble()*(Room.MaxX-Room.MinX),Room.MinZ+(float)random.NextDouble()*(Room.MaxZ-Room.MinZ)),c.id);
                    c.life.route.Clear();c.life.remaining=1;c.life.energy=30+random.Next(55);c.life.hunger=20+random.Next(65);c.life.thirst=20+random.Next(60);c.life.boredom=20+random.Next(70);c.socialCooldown=0;
                }
                for(int n=0;n<2000;n++){w.Advance(.1);assert(MinGap(w)>=-.0001f,"varied household collision, seed "+seed);}
                WorldSimulation.Validate(w.State);
            }
        });
        test("household JSON roundtrip retains relationships and clears transient sessions",()=>{
            var w=Home(epoch);w.Relationship(1,2).meetings=7;w.Relationship(2,1).comfort=.7f;
            var codec=new Codec();var loaded=new WorldSimulation(codec.DecodeHome(codec.EncodeHome(w.State)));
            assert(loaded.State.cats.Count==5&&loaded.Relationship(1,2).meetings==7&&loaded.Relationship(2,1).comfort==.7f&&loaded.Sessions.Count==0,"relationship roundtrip");
        });
    }
}
