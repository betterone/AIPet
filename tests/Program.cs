using Stopwatch = System.Diagnostics.Stopwatch;
using System.Text.Json;
using System.Text.Json.Nodes;
using Momo.Core;

sealed class Codec : IStateCodec, IHouseholdCodec
{
    static readonly JsonSerializerOptions options=new(){IncludeFields=true};
    public string EncodeHome(HouseholdState s)=>JsonSerializer.Serialize(s,options);
    public HouseholdState DecodeHome(string text){try{return JsonSerializer.Deserialize<HouseholdState>(text,options);}catch(JsonException e){throw new FormatException(e.Message);}}
    public string Encode(WorldState s)=>JsonSerializer.Serialize(s,options);
    public WorldState Decode(string text){try{return JsonSerializer.Deserialize<WorldState>(text,options);}catch(JsonException e){throw new FormatException(e.Message);}}
}
class Program
{
    static readonly long Epoch=new DateTime(2026,9,10,9,0,0,DateTimeKind.Utc).Ticks;
    static int passed;
    static void Assert(bool b,string text){if(!b)throw new Exception(text);}
    static void Test(string name,Action action){var sw=Stopwatch.StartNew();action();Console.WriteLine($"PASS {name} ({sw.ElapsedMilliseconds}ms)");passed++;}
    static Simulation Fresh(uint seed=827139)=>new(Simulation.New(Epoch,seed));
    static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--validate-save")
        {
            var raw=File.ReadAllText(args[1]);var nl=raw.IndexOf('\n');
            Assert(nl==64,"missing checkpoint hash");
            var body=raw.Substring(nl+1);var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
            Assert(hash==raw.Substring(0,nl),"checkpoint checksum mismatch");
            if(JsonNode.Parse(body)?["schema"]?.GetValue<int>()>=2){var home=new Codec().DecodeHome(body);if(home.schema==2){home.schema=3;home.play=new PlayProgress();}WorldSimulation.Validate(home);}
            else new Simulation(new Codec().Decode(body));
            Console.WriteLine("Checkpoint integrity and current production simulation validation passed. No pet data printed.");return;
        }
        Test("same seed, same input reproduces the world",()=>{var a=Fresh();var b=Fresh();a.Advance(86400);b.Advance(86400);Assert(new Codec().Encode(a.State)==new Codec().Encode(b.State),"divergence");});
        Test("fragmented advancement preserves exact state",()=>{var a=Fresh();var b=Fresh();a.Advance(21603.5);for(int i=0;i<21603;i++)b.Advance(1);b.Advance(.5);Assert(new Codec().Encode(a.State)==new Codec().Encode(b.State),"time partition divergence");});
        Test("serialized resume matches continuous six hours",()=>{var a=Fresh();a.Advance(900);var codec=new Codec();var b=new Simulation(codec.Decode(codec.Encode(a.State)));a.Advance(6*3600*Simulation.Speed);b.Resume(Epoch+TimeSpan.FromHours(6).Ticks);a.Checkpoint(Epoch+TimeSpan.FromHours(6).Ticks);Assert(codec.Encode(a.State)==codec.Encode(b.State),"resume divergence");});
        Test("reopening does not replay offline time",()=>{var a=Fresh();long now=Epoch+TimeSpan.FromHours(6).Ticks;a.Resume(now);var before=new Codec().Encode(a.State);a.Resume(now);Assert(before==new Codec().Encode(a.State),"double catch-up");});
        Test("clock rollback cannot earn or replay time",()=>{var a=Fresh();a.Resume(Epoch+TimeSpan.FromHours(3).Ticks);double before=a.State.seconds;a.Resume(Epoch);a.Resume(Epoch+TimeSpan.FromHours(3).Ticks);Assert(before==a.State.seconds,"clock rollback replayed");});
        Test("finite 30-day offline catch-up and safe bounds",()=>{var a=Fresh();a.Resume(Epoch+TimeSpan.FromDays(30).Ticks);Simulation.Validate(a.State);Assert(a.State.events.Count<=128,"unbounded history");Assert(a.State.food>0&&a.State.water>0,"basic care ran out");Assert(a.State.seconds==30*86400*Simulation.Speed,"lost time");});
        Test("offline cap is explicit for absence over 30 days",()=>{var a=Fresh();a.Resume(Epoch+TimeSpan.FromDays(50).Ticks);Assert(a.State.seconds==30*86400*Simulation.Speed,"uncapped catch-up");Assert(a.State.events[^1].kind=="clock","silent clamp");});
        Test("ball trapped under sofa persists through long absence",()=>{var a=Fresh();a.InvitePlay(new Point(-4.7f,2f));var ball=a.State.ball;a.Advance(7*86400*Simulation.Speed);Assert(a.State.ballTrapped&&a.State.ball.x==ball.x&&a.State.ball.z==ball.z,"trapped ball moved");});
        Test("ball can be recovered and play invited",()=>{var a=Fresh();a.InvitePlay(new Point(-4.7f,2f));a.InvitePlay(new Point(.5f,-.5f));Assert(!a.State.ballTrapped,"recovery failed");Assert(a.State.intent==Activity.Play,"no play intent");});
        Test("navigation avoids inflated furniture bounds",()=>{var random=new Random(94);for(int i=0;i<500;i++){var from=Room.Nearest(new Point((float)random.NextDouble()*10.6f-5.3f,(float)random.NextDouble()*8.2f-4.1f));var to=Room.Nearest(new Point((float)random.NextDouble()*10.6f-5.3f,(float)random.NextDouble()*8.2f-4.1f));var path=Room.Path(from,to);Assert(path.Count>0,"unreachable legal target");foreach(var p in path){Assert(Room.Clear(from,p),"route crossed solid furniture");from=p;}Assert(Point.Distance(from,to)<.001f,"wrong destination");}});
        Test("100 seeded worlds remain valid and behaviour varies",()=>{var unique=new HashSet<string>();for(uint i=1;i<=100;i++){var a=Fresh(i);a.Advance(86400);Simulation.Validate(a.State);unique.Add(string.Join(',',a.State.events.Select(e=>e.kind)));}Assert(unique.Count>50,"identical scripts");});
        Test("repeated interactions don't farm infinite preference",()=>{var a=Fresh();a.InvitePlay(new Point(0,0));float p=a.State.ballPreference;for(int i=0;i<100;i++)a.InvitePlay(new Point(0,0));Assert(a.State.ballPreference==p,"preference farming");});
        Test("feather play respects personality and does not move the red ball",()=>{
            var eager=Fresh();var gentle=Fresh();gentle.State.personality.playfulness=.25f;
            var originalBall=eager.State.ball;
            eager.PlayWithWand(new Point(4,-3));gentle.PlayWithWand(new Point(4,-3));
            Assert(eager.State.ball.x==originalBall.x&&eager.State.ball.z==originalBall.z,"feather moved ball");
            Assert(Point.Distance(eager.State.pet,eager.State.target)>Point.Distance(gentle.State.pet,gentle.State.target),"personality did not affect pursuit");
            Assert(eager.State.wandPreference>gentle.State.wandPreference&&eager.State.events[^1].kind=="toy","feather response not recorded");
            float preference=eager.State.wandPreference;
            for(int i=0;i<20;i++)eager.PlayWithWand(new Point(3,-2));
            Assert(eager.State.wandPreference==preference,"feather preference farming");
            Simulation.Validate(eager.State);
        });
        Test("cat invitation happens only while awake and ready",()=>{
            var a=Fresh();a.State.activity=Activity.Sleep;Assert(!a.InviteFromCat(),"sleeping cat invited play");
            a.State.activity=Activity.Idle;a.State.ballTrapped=true;
            Assert(a.InviteFromCat(),"alert cat did not invite play");
            Assert(!a.State.ballTrapped&&a.State.events[^1].kind=="invitation","invitation did not restore toy or record event");
            Assert(!a.InviteFromCat(),"repeated invitation interrupted play");
            Simulation.Validate(a.State);
        });
        Test("older saves without feather fields remain playable",()=>{
            var codec=new Codec();var data=JsonNode.Parse(codec.Encode(Fresh().State))!.AsObject();
            data.Remove("wandPreference");data.Remove("lastWandAt");
            var loaded=new Simulation(codec.Decode(data.ToJsonString()));
            loaded.PlayWithWand(new Point(1,1));
            Assert(loaded.State.wandPreference>0,"first feather game did not learn from older save");
            Simulation.Validate(loaded.State);
        });
        Test("relationship and learned preferences survive a week away",()=>{var a=Fresh();a.State.affection=50;a.State.ballPreference=6;a.State.windowPreference=3;a.State.playHabits[9]=.2f;a.Advance(7*86400*Simulation.Speed);Assert(a.State.affection>49,"relationship faded too quickly");Assert(a.State.ballPreference>4&&a.State.windowPreference>1,"preference faded too quickly");Assert(a.State.playHabits[9]>.08f,"habit faded too quickly");});
        Test("gentle touch increases bond without forced sleep interruption",()=>{var a=Fresh();a.State.activity=Activity.Sleep;a.State.remaining=300;float bond=a.State.affection;a.Pet();Assert(a.State.activity==Activity.Sleep&&a.State.affection>bond,"sleep interrupted");});
        Test("excess tapping asks for space",()=>{var a=Fresh();for(int i=0;i<5;i++){a.Advance(5);a.Pet();}Assert(a.State.intent==Activity.Hide,"no boundary response");});
        Test("save survives 100 round trips and checksum corruption",()=>{var dir=Path.Combine(Path.GetTempPath(),"momo-tests-"+Guid.NewGuid());var codec=new Codec();var store=new SaveStore(dir,codec);var a=Fresh();for(int i=0;i<100;i++){a.Advance(25);store.Save(a.State);Assert(codec.Encode(store.Load())==codec.Encode(a.State),"roundtrip mismatch");}long revision=a.State.revision;File.WriteAllText(Path.Combine(dir,"world-"+(revision%2)+".save"),"partial corrupted write");var recovered=store.Load();Assert(recovered.revision==revision-1,"previous checkpoint lost");Assert(store.LastWarning!=null,"recovery silent");});
        Test("both damaged slots stop instead of silently resetting pet",()=>{var dir=Path.Combine(Path.GetTempPath(),"momo-tests-"+Guid.NewGuid());Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"world-0.save"),"bad");File.WriteAllText(Path.Combine(dir,"world-1.save"),"bad");bool threw=false;try{new SaveStore(dir,new Codec()).Load();}catch(IOException){threw=true;}Assert(threw,"silent reset");Assert(File.ReadAllText(Path.Combine(dir,"world-0.save"))=="bad","original changed");});
        Test("pending write is ignored until committed",()=>{var dir=Path.Combine(Path.GetTempPath(),"momo-tests-"+Guid.NewGuid());var store=new SaveStore(dir,new Codec());var a=Fresh();store.Save(a.State);File.WriteAllText(Path.Combine(dir,"world-0.save.pending"),"partial");Assert(store.Load().revision==1,"pending write read");});
        Test("invalid time and state are rejected",()=>{foreach(double d in new[]{double.NaN,double.PositiveInfinity,-1}){bool threw=false;try{Fresh().Advance(d);}catch(ArgumentOutOfRangeException){threw=true;}Assert(threw,"bad time accepted");}var s=Simulation.New(Epoch);s.energy=float.NaN;bool invalid=false;try{Simulation.Validate(s);}catch(ArgumentException){invalid=true;}Assert(invalid,"NaN accepted");});
        Test("corrupt finite-number fields and malformed histories are rejected",()=>{
            foreach(var mutate in new Action<WorldState>[] {s=>s.remaining=double.NaN,s=>s.remainder=double.NaN,s=>s.route.Add(new Point(100,100)),s=>s.events.Add(new LifeEvent(10,0,"bad","future id"))})
            {var s=Simulation.New(Epoch);mutate(s);bool threw=false;try{Simulation.Validate(s);}catch(ArgumentException){threw=true;}Assert(threw,"invalid persisted field accepted");}
        });
        Test("personalities change observed activity distributions",()=>{
            int playful=0,gentle=0;for(uint seed=1;seed<=12;seed++)
            {var a=Fresh(seed);var b=Fresh(seed);b.State.personality=Personality.FromStyle(1);a.Advance(86400);b.Advance(86400);playful+=a.State.events.Count(e=>e.kind=="play");gentle+=b.State.events.Count(e=>e.kind=="play");}
            Assert(playful>gentle,"personality not expressed through play");
        });
        Test("nine pet boutique profiles are complete, unique, and valid",()=>{
            Assert(BreedArchetypes.All.Length==9,"expected 9 cat profiles");
            var names=new HashSet<string>();
            var breeds=new HashSet<string>();
            for(int i=0;i<BreedArchetypes.All.Length;i++)
            {
                var p=BreedArchetypes.Get(i);
                Assert(!string.IsNullOrEmpty(p.defaultName),"missing name");
                Assert(!string.IsNullOrEmpty(p.breedName),"missing breed");
                Assert(!string.IsNullOrEmpty(p.hexEyeLeft)&&!string.IsNullOrEmpty(p.hexEyeRight),"missing eye colors");
                Assert(p.reactions!=null&&p.reactions.Length>=2,"missing reactions");
                names.Add(p.defaultName);
                breeds.Add(p.breedName);
                var s=Simulation.New(Epoch);
                s.breedIndex=i;
                s.personality=p.personality;
                s.name=p.defaultName;
                Simulation.Validate(s);
            }
            Assert(names.Count==9,"duplicate pet names");
            Assert(breeds.Count==9,"duplicate breed names");
            Assert(BreedArchetypes.Get(4).hexEyeLeft!=BreedArchetypes.Get(4).hexEyeRight,"odd-eye cat should have heterochromia");
        });
        Test("both rooms connect only through the doorway and every hide spot is reachable",()=>{
            var start=new Point(0,0);foreach(var target in Room.HideSpots.Concat(Room.PeekSpots).Append(new Point(11,4)))
            {Assert(Room.Walkable(target),"illegal hide or peek anchor");var path=Room.Path(start,target);Assert(path.Count>0&&path.Count<=16,"missing/bloated cross-room route");var from=start;foreach(var q in path){Assert(Room.Clear(from,q),"path cuts partition or furniture");from=q;}Assert(Point.Distance(from,target)<.001f,"route ends early");}
            Assert(!Room.Clear(new Point(5,3),new Point(7,3)),"partition is passable");
            Assert(Room.Clear(new Point(5,0),new Point(7,0)),"doorway is blocked");
        });
        Test("observer swept movement stops at furniture and can slide along walls",()=>{
            var wall=Room.Move(new Point(5,3),new Point(5,0));Assert(wall.x<5.56f&&Room.Walkable(wall),"observer tunneled through wall");
            var door=Room.Move(new Point(5,0),new Point(5,0));Assert(door.x>9.9f,"observer cannot enter playroom");
            var slide=Room.Move(new Point(5,3),new Point(3,-2));Assert(slide.z<1.1f&&Room.Walkable(slide),"wall sliding stuck");
        });
        Test("random two-room routes and annex ball saves stay valid",()=>{
            var rng=new Random(104);for(int i=0;i<500;i++){
                var from=Room.Nearest(new Point((float)rng.NextDouble()*16.7f-5.35f,(float)rng.NextDouble()*8.3f-4.15f));
                var to=Room.Nearest(new Point((float)rng.NextDouble()*16.7f-5.35f,(float)rng.NextDouble()*8.3f-4.15f));
                var route=Room.Path(from,to);Assert(route.Count>0&&route.Count<=16,"annex navigation disconnected");foreach(var q in route){Assert(Room.Clear(from,q),"annex route crosses solid");from=q;}
            }
            var a=Fresh();a.InvitePlay(new Point(9,0));Simulation.Validate(a.State);Assert(a.State.ball.x==9,"annex ball was clamped to old room");
            var copy=new Simulation(new Codec().Decode(new Codec().Encode(a.State)));Assert(copy.State.ball.x==9,"annex save failed");
        });
        Test("daily peekaboo preserves the ball, offers clues and emerges when ignored",()=>{
            var a=Fresh();a.State.activity=Activity.Idle;var ball=a.State.ball;var play=new CompanionPlay(a);
            Assert(play.Start(new Point(0,0)),"awake cat refused starting test");
            for(int i=0;i<20;i++)play.Update(.1f,new Point(0,0),false,false,true);
            Assert(play.Phase==CompanyPhase.GoingToHide,"invitation never led to hiding");
            a.State.pet=a.State.target;a.State.route.Clear();a.State.activity=Activity.Greet;
            play.Update(.1f,new Point(0,0),false,false,true);
            bool clue=false,emerged=false;
            for(int i=0;i<250;i++){play.Update(.1f,new Point(0,0),false,false,true);clue|=play.Cue==CatVoice.Peek;emerged|=play.Phase==CompanyPhase.Returning;}
            Assert(clue&&emerged&&!play.Active,"ignored cat stayed hidden indefinitely");
            Assert(a.State.ball.x==ball.x&&a.State.ball.z==ball.z,"peekaboo moved the ball");Assert(play.Relocations<=2,"unbounded chase");
        });
        Test("found response needs real visibility, and rest or toys cancel social play",()=>{
            var a=Fresh();a.State.activity=Activity.Idle;var play=new CompanionPlay(a);Assert(play.Start(new Point(0,0)),"start");
            for(int i=0;i<20;i++)play.Update(.1f,new Point(0,0),false,false,true);
            a.State.pet=a.State.target;a.State.route.Clear();a.State.activity=Activity.Greet;play.Update(.1f,new Point(0,0),false,false,true);
            var near=new Point(a.State.pet.x+.9f,a.State.pet.z);for(int i=0;i<15;i++)play.Update(.1f,near,false,true,true);
            Assert(play.Phase!=CompanyPhase.Found,"found through furniture");
            for(int i=0;i<12;i++)play.Update(.1f,near,true,true,true);
            Assert(play.Phase==CompanyPhase.Found&&a.State.events.Last().kind=="together","found response missing");
            float affection=a.State.affection;for(int i=0;i<100;i++)play.Found(near);Assert(a.State.affection==affection,"repeated found feedback farmed affection");
            play.Update(.1f,near,true,true,false);Assert(!play.Active,"toy/panel did not cancel");
            a.State.activity=Activity.Sleep;Assert(!play.Start(near),"sleeping cat teased");
            a.State.activity=Activity.Idle;a.State.energy=20;Assert(!play.Start(near),"tired cat teased");
        });
        Test("physical approach is one-to-one, bounded, reversible and resets without jumping",()=>{
            var motion=new PhysicalApproach();Assert(Point.Distance(motion.Sample(new Point(10,20)),new Point(0,0))==0,"first pose jumped");
            var half=motion.Sample(new Point(10,20.5f));Assert(Math.Abs(half.z-.5f)<.001f,"real half-step not 1:1");
            var limit=motion.Sample(new Point(10,22));Assert(Math.Abs(limit.z-.25f)<.001f&&motion.Distance>1.9f,"physical radius exceeded");
            Assert(Point.Distance(motion.Sample(new Point(10,23)),new Point(0,0))<.001f,"kept moving beyond radius");
            Assert(motion.Sample(new Point(10,20.4f)).z<-.3f,"returning toward origin failed");
            motion.Reset();Assert(Point.Distance(motion.Sample(new Point(300,400)),new Point(0,0))==0,"relocalization jumped");
            Assert(Point.Distance(motion.Sample(new Point(float.NaN,0)),new Point(0,0))==0,"invalid pose moved observer");
        });
        Test("cancelling company preserves a cat's independent rest and space response",()=>{
            var a=Fresh();a.State.activity=Activity.Idle;var play=new CompanionPlay(a);play.Start(new Point(0,0));
            a.State.activity=a.State.intent=Activity.Sleep;play.Update(.1f,new Point(0,0),true,true,true);
            Assert(!play.Active&&a.State.activity==Activity.Sleep,"cancellation woke cat");
            a.State.activity=a.State.intent=Activity.Idle;play.Start(new Point(0,0));a.State.intent=Activity.Hide;a.State.activity=Activity.Walk;
            play.Update(.1f,new Point(0,0),true,true,true);Assert(a.State.intent==Activity.Hide,"cancellation erased need for space");
        });
        Test("standing observer walks around the tunnel while the cat can hide inside",()=>{
            var from=new Point(9.3f,-.5f);var delta=new Point(0,-3);
            Assert(Room.Move(from,delta,true).z>-1.51f,"observer walked through tunnel roof");
            Assert(Room.Move(from,delta).z<-3.4f,"cat lost its tunnel access");
            Assert(Room.Walkable(Room.Bed),"observer clearance invalidated cat bed saves");
        });
        Test("four rooms and garden routes share legal continuous geometry",()=>{
            var anchors=Room.ExploreSpots.Concat(Room.HideSpots).Concat(Room.PeekSpots).ToArray();
            foreach(var a in anchors)foreach(var b in anchors){Assert(Room.Walkable(a)&&Room.Walkable(b),"illegal anchor");var route=Room.Path(a,b);Assert(route.Count>0&&route.Count<=Room.MaxRoutePoints,"disconnected rooms/garden");var prev=a;foreach(var q in route){Assert(Room.Clear(prev,q),"crossed wall/furniture");prev=q;}}
            var rng=new Random(20261022);for(int n=0;n<1200;n++){
                var a=Room.Nearest(new Point(Room.MinX+(float)rng.NextDouble()*(Room.MaxX-Room.MinX),Room.MinZ+(float)rng.NextDouble()*(Room.MaxZ-Room.MinZ)));
                var b=Room.Nearest(new Point(Room.MinX+(float)rng.NextDouble()*(Room.MaxX-Room.MinX),Room.MinZ+(float)rng.NextDouble()*(Room.MaxZ-Room.MinZ)));
                var route=Room.Path(a,b);Assert(route.Count>0,"legal garden point unreachable");foreach(var q in route){Assert(Room.Clear(a,q),"garden route cuts corner");a=q;}
            }
            Assert(Room.Move(new Point(3,3),new Point(0,15),true).z>17.9f,"observer cannot leave through doors");
            Assert(Room.Move(new Point(0,3),new Point(0,4),true).z<4.66f,"walked through kitchen wall");
            Assert(!Room.Clear(new Point(0,10),new Point(0,12)),"garden wall is passable");
        });
        Test("autonomous exploration actually visits the new rooms and yard",()=>{
            var areas=new HashSet<string>();var a=Fresh(1923);for(int i=0;i<20000;i++){a.Advance(5);areas.Add(Room.Area(a.State.pet));}
            Assert(areas.Contains("小厨房")&&areas.Contains("休息房")&&areas.Contains("后院"),"new areas not explored");Simulation.Validate(a.State);
        });
        Test("garden ball and pet survive save roundtrip and old fields remain optional",()=>{
            var a=Fresh();a.State.pet=a.State.target=new Point(3,19);a.InvitePlay(new Point(4,19));Simulation.Validate(a.State);
            var codec=new Codec();var copy=new Simulation(codec.Decode(codec.Encode(a.State)));Assert(copy.State.ball.z==19&&copy.State.pet.z==19,"garden position clamped");
            var legacy=JsonNode.Parse(codec.Encode(Fresh().State))!.AsObject();legacy.Remove("hasHandFed");legacy.Remove("lastHandFeedAt");
            copy=new Simulation(codec.Decode(legacy.ToJsonString()));Assert(copy.BeginHandFeeding(new Point(1,0)),"legacy first hand feed blocked");
        });
        Test("hand feeding has notice sniff four bites and one relationship reward",()=>{
            var a=Fresh();a.State.hunger=55;a.State.remaining=1000;var f=new HandFeeding(a);var target=new Point(1.5f,0);f.Grab();
            bool notice=false,sniff=false,ate=false;float bond=a.State.affection;
            for(int i=0;i<250;i++){a.Advance(.6);f.Update(.1f,target,true,true,true);notice|=f.Phase==FeedingPhase.Notice;sniff|=f.Phase==FeedingPhase.Sniff;ate|=f.Eating;}
            Assert(notice&&sniff&&ate,"missing interaction stages");Assert(f.Bites==4&&f.Nutrition==20,"wrong ration");Assert(a.State.affection>bond+.99f&&a.State.affection<bond+1.01f,"bond per bite or missing");Assert(!a.HandFeedingActive&&!f.Active,"session did not release cat");Simulation.Validate(a.State);
        });
        Test("feeding reaches the palm precisely and waits for actual mouth contact",()=>{
            var a=Fresh();a.State.hunger=55;a.State.remaining=1000;var f=new HandFeeding(a);var target=new Point(1.5f,0);f.Grab();
            for(int i=0;i<160;i++){a.Advance(.6);f.Update(.1f,target,true,true,false);}
            Assert(Point.Distance(a.State.pet,target)<.035f,"cat stopped outside mouth reach");
            Assert(!f.Eating&&f.Bites==0&&f.Nutrition==0,"cat ate before turning and touching food");
            target=new Point(target.x+.05f,target.z); // A small hand correction must move the cat too.
            for(int i=0;i<80;i++){a.Advance(.6);f.Update(.1f,target,true,true,false);}
            Assert(Point.Distance(a.State.pet,target)<.035f,"small correction left the muzzle behind");
            for(int i=0;i<80;i++){a.Advance(.6);f.Update(.1f,target,true,true,true);}
            Assert(f.Bites>0,"contact did not resume feeding");f.Cancel();
        });
        Test("feeding uses close care space without crossing the observer body",()=>{
            var a=Fresh();a.State.hunger=55;a.State.remaining=1000;
            a.State.adopted=true;a.State.pet=a.State.target=new Point(0,3);
            var home=new WorldSimulation(WorldSimulation.Migrate(a.State));home.ObserverPosition=new Point(0,0);
            var brain=home.Brain(1);var target=new Point(0,1.15f);
            Assert(brain.BeginHandFeeding(target),"care target refused");
            Assert(Point.Distance(brain.State.target,target)<.001f,"comfort reservation displaced the food slot");
            for(int i=0;i<200;i++)home.Advance(.1);
            Assert(Point.Distance(brain.State.pet,target)<.035f,"muzzle cannot reach the reserved food slot");
            Assert(Point.Distance(brain.State.pet,home.ObserverPosition.Value)>=1.03f,"feeding crossed observer body");
            brain.EndHandFeeding();
        });
        Test("moving hand and missing mouth contact never consume food",()=>{
            var a=Fresh();a.State.hunger=55;a.State.remaining=1000;var f=new HandFeeding(a);var target=new Point(1,0);f.Grab();
            for(int i=0;i<110;i++){a.Advance(.6);f.Update(.1f,target,true,true,true);if(f.Eating)break;}
            Assert(f.Eating,"did not start eating");
            for(int i=0;i<40;i++){a.Advance(.6);f.Update(.1f,target,false,true,false);}
            Assert(f.Phase==FeedingPhase.Pause&&f.Bites==0&&f.Nutrition==0,"ate from moving/unreachable hand");
            for(int i=0;i<100;i++){a.Advance(.6);f.Update(.1f,target,true,true,true);}
            Assert(f.Bites>0,"did not resume after hand settled");f.Cancel();Assert(!a.HandFeedingActive,"cancelled hand owns cat");
        });
        Test("full sleeping far and hidden cats may decline offered food",()=>{
            foreach(var scenario in new[]{0,1,2,3}){
                var a=Fresh();a.State.remaining=1000;a.State.hunger=scenario==0?10:60;if(scenario==1)a.State.activity=a.State.intent=Activity.Sleep;
                var f=new HandFeeding(a);f.Grab();var target=scenario==2?new Point(3,19):new Point(1,0);
                for(int i=0;i<120;i++)f.Update(.1f,target,true,scenario!=3,true);
                Assert(f.Bites==0&&!a.HandFeedingActive,"declining cat was forced to eat");f.Cancel();
            }
        });
        Test("petting during feeding preserves meal and actual gains cannot be replayed",()=>{
            var a=Fresh();a.State.hunger=60;var target=new Point(0,0);Assert(a.BeginHandFeeding(target),"feed refused");a.WaitForHandFood();
            float bond=a.State.affection;a.Pet();Assert(a.HandFeedingActive&&a.State.affection>bond,"petting cancelled meal");
            a.HandFeedBite(target);float rewarded=a.State.affection;a.EndHandFeeding();
            Assert(a.BeginHandFeeding(target),"second meal refused");a.WaitForHandFood();a.HandFeedBite(target);Assert(a.State.affection==rewarded,"rapid repeat fed bond");a.EndHandFeeding();
            var copy=new Simulation(new Codec().Decode(new Codec().Encode(a.State)));Assert(!copy.HandFeedingActive,"transient session serialized");Assert(copy.BeginHandFeeding(target),"restored meal refused");copy.WaitForHandFood();copy.HandFeedBite(target);Assert(copy.State.affection==rewarded,"reloading replayed feed bonus");
        });
        MultiCatTests.Run(Test,Assert,Epoch);
        WandTests.Run(Test,Assert,Epoch);
        BathTests.Run(Test,Assert,Epoch);
        HudTests.Run(Test,Assert);
        Console.WriteLine($"\n{passed} tests passed. Same C# core as the Unity player; no rendering or device claims.");
    }
}
