using System;
using System.Collections.Generic;

namespace Momo.Core
{
    public sealed class WorldSimulation
    {
        public const int Capacity=5;
        public const double MotionStep=.1;
        public readonly HouseholdState State;
        public Point? ObserverPosition;
        public readonly List<InteractionSession> Sessions=new List<InteractionSession>();
        public BathSession Bath {get;private set;}
        bool foreground;
        readonly Dictionary<int,double> nextRoam=new Dictionary<int,double>();
        readonly Dictionary<int,double> nextCompany=new Dictionary<int,double>();
        int roamSequence;
        public GardenOuting Outing {get;private set;}
        public bool StartOuting(int id)
        {
            var c=Cat(id);if(c==null||!Available(c)||Paired(id)||Outing!=null&&Outing.Active)return false;
            Outing=new GardenOuting(this,id);return true;
        }
        // Foreground opportunities use simulation time and staggered deadlines, so
        // frame partition and cat array order cannot synchronize a whole household.
        void UpdateHomeLife()
        {
            Outing?.Tick((float)MotionStep);
            foreach(var c in State.cats)
            {
                if(!nextRoam.ContainsKey(c.id))nextRoam[c.id]=State.seconds+45+c.id*31;
                if(!nextCompany.ContainsKey(c.id))nextCompany[c.id]=State.seconds+12+c.id*7;
                if(!Available(c)||Paired(c.id)||c.life.intent==Activity.Greet||c.life.intent==Activity.Play||c.life.activity==Activity.Walk)continue;
                if(ObserverPosition.HasValue&&c.id==State.focusedCatId&&State.seconds>=nextCompany[c.id])
                {
                    nextCompany[c.id]=State.seconds+90+(1-c.life.personality.sociability)*90;
                    if(Call(c.id,ObserverPosition.Value))continue;
                }
                if(State.seconds<nextRoam[c.id])continue;
                nextRoam[c.id]=State.seconds+240+c.id*19;
                // Every other outing visits the garden; different cats reserve
                // separate destinations and never all head for the same window.
                int slot=(roamSequence+c.id)%3;
                roamSequence++;
                var spots=new[]{new Point(3,13),new Point(3,19.5f),new Point(10,19)};
                var target=roamSequence%2==1?spots[slot]:Room.ExploreSpots[(roamSequence+c.id)%Room.ExploreSpots.Length];
                Navigate(c.id,target);
            }
        }
        readonly Dictionary<int,Simulation> brains=new Dictionary<int,Simulation>();
        readonly Dictionary<int,Point> yieldTargets=new Dictionary<int,Point>();
        readonly HashSet<int> humanBusy=new HashSet<int>();
        public long MotionSteps {get;private set;}
        public WorldSimulation(HouseholdState state)
        {
            Validate(state);State=state;
            foreach(var cat in state.cats)Attach(cat);
            SettlePositions();
            foreach(var c in state.cats)if(c.life.activity==Activity.Walk){c.life.target=FindFree(c.life.target,c.id,0,true);c.life.route=CatNavigation.Path(c.life.pet,c.life.target);}
        }
        public static HouseholdState Migrate(WorldState legacy)
        {
            Simulation.Validate(legacy);
            var home=new HouseholdState{lastUtcTicks=legacy.lastUtcTicks,seconds=legacy.seconds,food=legacy.food,water=legacy.water,ball=legacy.ball,ballTrapped=legacy.ballTrapped,sound=legacy.sound,focusedCatId=1,nextCatId=2};
            home.cats.Add(new CatState{id=1,life=legacy,socialCooldown=30});return home;
        }
        public static void Validate(HouseholdState home)
        {
            if(home==null||home.schema!=3||home.cats==null||home.cats.Count<1||home.cats.Count>Capacity||home.relationships==null||home.relationships.Count>Capacity*(Capacity-1)||home.revision<0||home.lastUtcTicks<=0||home.lastUtcTicks>DateTime.MaxValue.Ticks||!Finite(home.seconds)||home.seconds<0||!Finite(home.remainder)||home.remainder<0||home.remainder>=MotionStep+.0000001)throw new ArgumentException("Invalid household save");
            if(!Room.Finite(home.food)||!Room.Finite(home.water)||home.food<0||home.food>100||home.water<0||home.water>100||!Room.Finite(home.ball.x)||!Room.Finite(home.ball.z)||home.ball.x<Room.MinX-.15f||home.ball.x>Room.MaxX+.15f||home.ball.z<Room.MinZ-.15f||home.ball.z>Room.MaxZ+.15f)throw new ArgumentException("Invalid shared resources");
            var ids=new HashSet<int>();
            foreach(var cat in home.cats)
            {
                if(cat==null||cat.appearance< -1||cat.appearance>1||!Finite(cat.queuedAt)||cat.queuedAt<0||!Enum.IsDefined(typeof(Activity),cat.waitingFor)||cat.id<1||cat.id>=home.nextCatId||!ids.Add(cat.id)||!Room.Finite(cat.radius)||Math.Abs(cat.radius-.78f)>.001f||!Room.Finite(cat.heading)||!Room.Finite(cat.socialCooldown)||cat.socialCooldown<0||!Room.Finite(cat.blockedSeconds)||cat.blockedSeconds<0||!Room.Finite(cat.attentionSeconds)||cat.attentionSeconds<0)throw new ArgumentException("Invalid cat identity or body");
                Simulation.Validate(cat.life);
            }
            PlayProgress.Validate(home.play,ids);
            if(home.toyOwnerId!=0&&!ids.Contains(home.toyOwnerId))throw new ArgumentException("Missing toy owner");
            if(!ids.Contains(home.focusedCatId))throw new ArgumentException("Missing focused cat");
            var pairs=new HashSet<string>();foreach(var r in home.relationships)
                if(r==null||r.from==r.to||!ids.Contains(r.from)||!ids.Contains(r.to)||!pairs.Add(r.from+":"+r.to)||!Room.Finite(r.familiarity)||r.familiarity<0||r.familiarity>1||!Room.Finite(r.comfort)||r.comfort<0||r.comfort>1||r.meetings<0||r.games<0)throw new ArgumentException("Invalid relationship");
        }
        static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        void Attach(CatState cat)
        {
            var sim=new Simulation(cat.life){ExternalLocomotion=true};
            sim.FindPath=(start,end)=>PlanPath(cat,start,end);
            sim.ResolveDestination=(activity,target)=>Destination(cat,activity,target);
            sim.CanChoose=activity=>activity!=Activity.Play||State.toyOwnerId==0||State.toyOwnerId==cat.id;
            sim.ResolveIntent=activity=>
            {
                if(IsBowl(activity)&&ResourceBusy(cat.id,activity))
                {if(cat.waitingFor!=activity)cat.queuedAt=State.seconds;cat.waitingFor=activity;return Activity.Watch;}
                if(IsBowl(activity))cat.waitingFor=Activity.Idle;
                return activity;
            };
            brains.Add(cat.id,sim);
        }
        public static Point BowlSeat(Activity activity)
        {var bowl=activity==Activity.Eat?Room.Food:Room.Water;return new Point(bowl.x-.72f,bowl.z);}
        static bool IsBowl(Activity a)=>a==Activity.Eat||a==Activity.Drink;
        bool ResourceBusy(int self,Activity activity)
        {
            float currentRadius=Cat(self).radius;
            foreach(var c in State.cats)if(c.id!=self&&(IsBowl(c.life.intent)||Point.Distance(c.life.pet,BowlSeat(activity))<c.radius+currentRadius+.02f))return true;
            var current=Cat(self);
            foreach(var c in State.cats)if(c.id!=self&&IsBowl(c.waitingFor)&&(!IsBowl(current.waitingFor)||c.queuedAt<current.queuedAt||(c.queuedAt==current.queuedAt&&c.id<self)))return true;
            return false;
        }
        List<Point> PlanPath(CatState cat,Point start,Point end)
        {
            var route=CatNavigation.Path(start,end);if(route.Count==0)return route;
            var bodies=new List<Point>();var radii=new List<float>();
            if(Bath!=null&&Bath.Active&&Bath.CatId!=cat.id){bodies.Add(BathSession.Station);radii.Add(cat.radius+.9f);}
            if(ObserverPosition.HasValue){bodies.Add(ObserverPosition.Value);radii.Add(1.0299f);}
            foreach(var other in State.cats)if(other.id!=cat.id&&other.life.adopted){bodies.Add(other.life.pet);radii.Add(cat.radius+other.radius+.0149f);}
            bool clear=true;Point last=start;
            foreach(var p in route){for(int i=0;i<bodies.Count;i++)if(CatNavigation.SegmentDistance(bodies[i],last,p)<radii[i])clear=false;last=p;}
            if(clear)return route;
            var detour=CatNavigation.AroundCats(start,end,bodies,radii);return detour.Count>0?detour:route;
        }
        public CatState Cat(int id){foreach(var cat in State.cats)if(cat.id==id)return cat;return null;}
        public Simulation Brain(int id)=>brains[id];
        public CatState Focused=>Cat(State.focusedCatId);
        public CatState Adopt(string name,int breedIndex,long utcTicks)
        {
            if(State.cats.Count>=Capacity)throw new InvalidOperationException("这个家已经有五只猫了。");
            var life=Simulation.New(utcTicks,(uint)(827139+State.nextCatId*7919));
            var profile=BreedArchetypes.Get(breedIndex);
            life.adopted=true;life.breedIndex=life.coat=breedIndex;life.name=string.IsNullOrWhiteSpace(name)?profile.defaultName:name.Trim();if(life.name.Length>24)life.name=life.name.Substring(0,24);
            // Profiles are immutable templates; never share mutable personality objects.
            var p=profile.personality;life.personality=new Personality{curiosity=p.curiosity,sociability=p.sociability,playfulness=p.playfulness,independence=p.independence};
            life.sound=State.sound;life.ball=State.ball;life.ballTrapped=State.ballTrapped;life.food=State.food;life.water=State.water;
            life.seconds=State.cats[0].life.seconds;life.remainder=State.cats[0].life.remainder;life.lastUtcTicks=State.lastUtcTicks;life.createdUtcTicks=Math.Min(utcTicks,State.lastUtcTicks);
            var cat=new CatState{id=State.nextCatId++,life=life,appearance=0,socialCooldown=60+State.nextCatId*13,heading=180};
            life.pet=life.target=FindFree(new Point(0,2.2f),cat.id);State.cats.Add(cat);Attach(cat);
            brains[cat.id].Record("first","来到了这个有猫咪相伴的小家。");return cat;
        }
        public void Focus(int id){if(Cat(id)==null)throw new ArgumentException("Unknown cat");State.focusedCatId=id;}
        public void SetHumanBusy(int id,bool busy)
        {if(busy||(Bath!=null&&Bath.Active&&Bath.CatId==id)){CancelFor(id);humanBusy.Add(id);}else humanBusy.Remove(id);}
        public bool TryBeginBath(int id,out string reason)
        {
            reason=null;var cat=Cat(id);
            if(Bath!=null&&Bath.Active){reason="洗澡盘正在使用，一次照顾一只猫。";return false;}
            if(cat==null||!cat.life.adopted){reason="先选一只家里的猫吧。";return false;}
            var s=cat.life;
            if(s.activity==Activity.Sleep||s.energy<35){reason="它想先休息，等醒来再洗吧。";return false;}
            if(s.activity==Activity.Eat||s.activity==Activity.Drink||s.hunger>=85||s.thirst>=85){reason="让它先好好吃喝，稍后再洗。";return false;}
            if(s.intent==Activity.Hide||Brain(id).HandFeedingActive||humanBusy.Contains(id)||Paired(id)){reason="它现在想有一点自己的时间。";return false;}
            float willingness=s.personality.sociability*.55f+s.affection/100f*.25f+(1-s.personality.independence)*.2f;
            if(willingness<.3f){reason="它还想先观察一会儿，下次再邀请吧。";return false;}
            if(!Space(BathSession.Station,id)||CatNavigation.Path(s.pet,BathSession.Station).Count==0){reason="先让洗澡盘旁边空出来，再叫它过来。";return false;}
            foreach(var other in State.cats)if(other.id!=id&&other.life.adopted&&Point.Distance(other.life.pet,BathSession.Station)<other.radius+.9f)
            {reason="先让洗澡盘旁边空出来，再叫它过来。";return false;}
            if(State.toyOwnerId==id)State.toyOwnerId=0;
            cat.waitingFor=Activity.Idle;cat.blockedSeconds=0;yieldTargets.Remove(id);
            s.route.Clear();s.target=s.pet;s.activity=s.intent=Activity.Idle;
            Bath=new BathSession(this,id);return true;
        }
        bool Available(CatState c)
        {
            var s=c.life;return s.adopted&&c.waitingFor==Activity.Idle&&!humanBusy.Contains(c.id)&&!Brain(c.id).HandFeedingActive&&s.energy>=35&&s.hunger<85&&s.thirst<85&&s.activity!=Activity.Sleep&&s.activity!=Activity.Eat&&s.activity!=Activity.Drink&&s.intent!=Activity.Hide;
        }
        bool Paired(int id){foreach(var s in Sessions)if(s.first==id||s.second==id)return true;return false;}
        public CatRelationship Relationship(int from,int to)
        {
            foreach(var r in State.relationships)if(r.from==from&&r.to==to)return r;
            if(from==to||Cat(from)==null||Cat(to)==null)throw new ArgumentException("Unknown relationship");
            var result=new CatRelationship{from=from,to=to};State.relationships.Add(result);return result;
        }
        bool Space(Point p,int self,int ignore=0,bool targets=false)
        {
            if(!CatNavigation.Walkable(p))return false;
            if(Bath!=null&&Bath.Active&&Bath.CatId!=self&&Point.Distance(p,BathSession.Station)<(Cat(self)?.radius??.78f)+.9f)return false;
            if(ObserverPosition.HasValue&&Point.Distance(p,ObserverPosition.Value)<(brains.TryGetValue(self,out var incoming)&&incoming.HandFeedingActive?1.05f:1.2f))return false;
            float radius=Cat(self)?.radius??.78f;
            foreach(var c in State.cats)if(c.id!=self&&c.id!=ignore&&c.life.adopted)
            {
                float gap=radius+c.radius+.035f;
                if(Point.Distance(p,c.life.pet)<gap)return false;
                if(targets&&c.life.activity==Activity.Walk&&Point.Distance(p,c.life.target)<gap)return false;
            }
            return true;
        }
        public Point FindFree(Point around,int self,int ignore=0,bool targets=false)
        {
            var start=CatNavigation.Nearest(around);if(Space(start,self,ignore,targets))return start;
            for(int ring=1;ring<=24;ring++)for(int i=0;i<24;i++)
            {
                double angle=i*Math.PI/12;var p=new Point(start.x+(float)Math.Cos(angle)*ring*.25f,start.z+(float)Math.Sin(angle)*ring*.25f);
                if(Space(p,self,ignore,targets))return p;
            }
            throw new InvalidOperationException("No safe cat position near destination");
        }
        Point Destination(CatState cat,Activity activity,Point target)
        {
            // Stable reservation lasts as long as the intent/target. There is no
            // separate lock to leak on cancellation or a failed route.
            if(IsBowl(activity))return CatNavigation.Nearest(BowlSeat(activity));
            if(IsBowl(cat.waitingFor))
            {
                // Leave the approach/exit to both adjacent bowls unobstructed.
                var waitingSpots=new[]{new Point(0,-2),new Point(-2,0),new Point(0,2.4f),new Point(2,0),new Point(2,-2.4f)};
                return FindFree(waitingSpots[(cat.id-1)%waitingSpots.Length],cat.id,0,true);
            }
            if(activity==Activity.Play)State.toyOwnerId=cat.id;
            // Spread long stationary activities throughout the home. A bed is
            // one comfortable option, not a compulsory shared coordinate.
            if(activity==Activity.Sleep||activity==Activity.Watch)
            {
                var spots=activity==Activity.Sleep?
                    new[]{Room.Bed,new Point(8,8),new Point(-1,12.5f),new Point(10,18.8f),new Point(0,-2)}:
                    new[]{Room.Window,new Point(9,4),new Point(3,12.5f),new Point(0,19),new Point(10,19)};
                target=spots[(cat.id-1)%spots.Length];
            }
            foreach(var s in Sessions)if(s.first==cat.id||s.second==cat.id)
                return s.first==cat.id?s.firstSlot:s.secondSlot;
            return FindFree(target,cat.id,0,true);
        }
        public void Navigate(int id,Point target,Activity activity=Activity.Explore)
        {CancelFor(id);yieldTargets.Remove(id);if(!IsBowl(activity))Cat(id).waitingFor=Activity.Idle;Brain(id).Request(activity,target);}
        public void SettlePositions()
        {
            // Only spawn/resume/recovery may relocate a cat; ordinary movement never teleports.
            foreach(var c in State.cats)
            {
                Point p=FindFree(c.life.pet,c.id);
                if(Point.Distance(p,c.life.pet)>.001f){c.life.pet=c.life.target=p;c.life.route.Clear();c.life.activity=c.life.intent=Activity.Idle;c.life.remaining=30;}
            }
        }
        public void Advance(double seconds,bool isForeground=false)
        {
            if(!Finite(seconds)||seconds<0||seconds>Simulation.MaxOfflineRealSeconds*Simulation.Speed)throw new ArgumentOutOfRangeException(nameof(seconds));
            State.remainder+=seconds;foreground=isForeground;
            try{while(State.remainder+1e-10>=MotionStep)
            {State.remainder=Math.Max(0,State.remainder-MotionStep);Step((float)MotionStep);}}
            finally{foreground=false;}
            State.remainder=Math.Round(State.remainder,9);
        }
        void Step(float dt)
        {
            State.seconds+=MotionStep;MotionSteps++;
            if(State.toyOwnerId!=0&&Cat(State.toyOwnerId).life.intent!=Activity.Play)State.toyOwnerId=0;
            if(State.food<10)State.food=90;if(State.water<10)State.water=90;
            float food=State.food,water=State.water,foodUse=0,waterUse=0;
            foreach(var c in State.cats)
            {
                var s=c.life;s.sound=State.sound;s.ball=State.ball;s.ballTrapped=State.ballTrapped;s.food=food;s.water=water;
                if(s.activity==Activity.Walk&&ObserverPosition.HasValue&&Point.Distance(s.target,ObserverPosition.Value)<(Brain(c.id).HandFeedingActive?1.05f:1.2f)&&!IsBowl(s.intent))
                {
                    // A person can move into a previously valid destination.
                    if(Paired(c.id))CancelFor(c.id);
                    else{s.target=FindFree(s.target,c.id,0,true);s.route=PlanPath(c,s.pet,s.target);}
                }
                if(c.waitingFor!=Activity.Idle)
                {
                    if(humanBusy.Contains(c.id)||s.energy<15||s.intent==Activity.Hide)c.waitingFor=Activity.Idle;
                    else if(!ResourceBusy(c.id,c.waitingFor))Brain(c.id).Request(c.waitingFor,c.waitingFor==Activity.Eat?Room.Food:Room.Water);
                    else s.remaining=Math.Max(s.remaining,120);
                }
                if(Paired(c.id))s.remaining=Math.Max(s.remaining,120);
                // The ball is one shared resource, including autonomous play.
                if(s.intent==Activity.Play&&State.toyOwnerId!=0&&State.toyOwnerId!=c.id)
                {s.route.Clear();s.activity=s.intent=Activity.Idle;s.remaining=10;}
                Brain(c.id).Advance(MotionStep);
                foodUse+=Math.Max(0,food-s.food);waterUse+=Math.Max(0,water-s.water);
                if(Point.Distance(s.ball,State.ball)>.001f){State.ball=s.ball;State.ballTrapped=s.ballTrapped;}
                c.socialCooldown=Math.Max(0,c.socialCooldown-dt);c.attentionSeconds=Math.Max(0,c.attentionSeconds-dt);
            }
            State.food=Math.Max(0,food-foodUse);State.water=Math.Max(0,water-waterUse);
            UpdateSessions(dt);
            // Rotating stable priority windows keep array ordering from owning the doorway.
            int first=(int)(State.seconds/30)%State.cats.Count;
            for(int n=0;n<State.cats.Count;n++)
            {
                var c=State.cats[(first+n)%State.cats.Count];var before=c.life.pet;Move(c,dt,first);
                if(foreground&&c.life.adopted&&!Brain(c.id).BathingActive&&c.life.pet.z>11.35f&&(Point.Distance(before,c.life.pet)>.000001f||c.life.activity==Activity.Play))
                    c.life.dirt=Math.Min(100,c.life.dirt+dt/(float)Simulation.Speed/30f);
            }
            if(Bath!=null&&Bath.Washing)Cat(Bath.CatId).heading=0;
            if(MotionSteps%10==0)SocialOpportunities();
            if(foreground)UpdateHomeLife();
            // A greeted cat stays briefly, then follows a moving person at a
            // respectful distance. Only the focused cat does this.
            if(foreground&&ObserverPosition.HasValue&&MotionSteps%30==0)
            {
                var c=Focused;var viewer=ObserverPosition.Value;
                if(Available(c)&&!Paired(c.id)&&c.life.intent==Activity.Greet)
                {
                    c.attentionSeconds=6;c.attentionCatId=0;
                    float distance=Point.Distance(c.life.pet,viewer);
                    if(distance>3&&distance<12)Call(c.id,viewer);
                    else if(distance<3)c.heading=(float)(Math.Atan2(viewer.x-c.life.pet.x,viewer.z-c.life.pet.z)*180/Math.PI);
                }
            }
            foreach(var c in State.cats){if(IsBowl(c.life.activity))c.heading=90;c.life.ball=State.ball;c.life.ballTrapped=State.ballTrapped;c.life.food=State.food;c.life.water=State.water;}
        }
        bool SafeSegment(CatState cat,Point end)
        {
            if(!CatNavigation.Clear(cat.life.pet,end))return false;
            if(Bath!=null&&Bath.Active&&Bath.CatId!=cat.id&&CatNavigation.SegmentDistance(BathSession.Station,cat.life.pet,end)<cat.radius+.9f)return false;
            if(ObserverPosition.HasValue&&Point.Distance(end,ObserverPosition.Value)<1.03f&&Point.Distance(end,ObserverPosition.Value)<Point.Distance(cat.life.pet,ObserverPosition.Value))return false;
            foreach(var other in State.cats)if(other.id!=cat.id&&other.life.adopted)
                if(CatNavigation.SegmentDistance(other.life.pet,cat.life.pet,end)<cat.radius+other.radius+.015f)return false;
            return true;
        }
        void Move(CatState cat,float dt,int first)
        {
            var s=cat.life;if(!s.adopted||s.activity!=Activity.Walk)return;
            bool yielding=yieldTargets.TryGetValue(cat.id,out Point aim);
            if(!yielding){if(s.route.Count==0){Brain(cat.id).Arrive();return;}aim=s.route[0];}
            if(!yielding&&s.route.Count>1&&MotionSteps%30==cat.id%30)
            {
                // When a neighbour steps aside, abandon an obsolete long detour.
                var shorter=CatNavigation.Path(s.pet,s.target);
                float Cost(List<Point> route){float total=0;Point last=s.pet;foreach(var q in route){total+=Point.Distance(last,q);last=q;}return total;}
                if(shorter.Count>0&&Cost(shorter)<Cost(s.route)*.8f)
                {
                    bool free=true;Point last=s.pet;
                    foreach(var q in shorter){if(ObserverPosition.HasValue&&CatNavigation.SegmentDistance(ObserverPosition.Value,last,q)<1.03f)free=false;foreach(var other in State.cats)if(other.id!=cat.id&&CatNavigation.SegmentDistance(other.life.pet,last,q)<cat.radius+other.radius+.02f)free=false;last=q;}
                    if(free){s.route=shorter;aim=s.route[0];}
                }
            }
            float speed=.18f;foreach(var session in Sessions)if(session.kind==SocialKind.Chase&&session.phase==SocialPhase.Together&&(session.first==cat.id||session.second==cat.id))speed=.28f;
            Point next=Point.Toward(s.pet,aim,dt*speed);
            bool moved=SafeSegment(cat,next);
            if(!moved)
            {
                // Choose a consistent right-hand arc while preserving the body envelope.
                float dx=aim.x-s.pet.x,dz=aim.z-s.pet.z,len=(float)Math.Sqrt(dx*dx+dz*dz);
                if(len>.001f)
                {
                    var side=new Point(s.pet.x+(dx*.2f+dz*.98f)/len*dt*speed,s.pet.z+(dz*.2f-dx*.98f)/len*dt*speed);
                    if(SafeSegment(cat,side)){next=side;moved=true;}
                }
                cat.blockedSeconds+=dt;
                if(cat.blockedSeconds>2&&!yielding)
                {
                    foreach(var other in State.cats)
                    {
                        if(other.id==cat.id||humanBusy.Contains(other.id)||Paired(other.id)||(other.life.activity!=Activity.Idle&&other.life.activity!=Activity.Watch&&other.life.activity!=Activity.Explore&&other.life.activity!=Activity.Groom)||Point.Distance(s.pet,other.life.pet)>cat.radius+other.radius+.25f)continue;
                        // An awake cat can step out of a doorway after arriving,
                        // rather than parking its body across the other cat's exit.
                        float ox=other.life.pet.x-s.pet.x,oz=other.life.pet.z-s.pet.z,ol=(float)Math.Sqrt(ox*ox+oz*oz);
                        if(ol<.01f)continue;
                        foreach(var offset in new[]{new Point(oz/ol*1.9f,-ox/ol*1.9f),new Point(-oz/ol*1.9f,ox/ol*1.9f),new Point(ox/ol*2,oz/ol*2)})
                        {
                            var parking=new Point(other.life.pet.x+offset.x,other.life.pet.z+offset.z);
                            if(!Space(parking,other.id,0,true)||!CatNavigation.Clear(other.life.pet,parking))continue;
                            other.life.target=parking;other.life.route=CatNavigation.Path(other.life.pet,parking);other.life.intent=Activity.Idle;other.life.activity=Activity.Walk;other.life.remaining=120;break;
                        }
                    }
                }
                if(cat.blockedSeconds>2&&!yielding&&MotionSteps%20==cat.id%20)
                {
                    var obstacles=new List<Point>();var clearance=new List<float>();
                    if(ObserverPosition.HasValue){obstacles.Add(ObserverPosition.Value);clearance.Add(1.0299f);}
                    foreach(var other in State.cats)if(other.id!=cat.id&&other.life.adopted){obstacles.Add(other.life.pet);clearance.Add(cat.radius+other.radius+.0149f);}
                    var detour=CatNavigation.AroundCats(s.pet,s.target,obstacles,clearance);
                    if(detour.Count>0){s.route=detour;cat.blockedSeconds=0;}
                }
                if(cat.blockedSeconds>6&&!yielding)
                {
                    // Back out of a narrow passage instead of pushing the other body.
                    int rank=(State.cats.IndexOf(cat)-first+State.cats.Count)%State.cats.Count;
                    if(rank>0&&len>.001f)
                    {
                        for(int k=0;k<4;k++)
                        {
                            float side=(k%2==0?1:-1)*(k<2?1.2f:2f);
                            var retreat=new Point(s.pet.x-dx/len*1.8f+dz/len*side,s.pet.z-dz/len*1.8f-dx/len*side);
                            if(Space(retreat,cat.id)&&CatNavigation.Clear(s.pet,retreat)){yieldTargets[cat.id]=retreat;break;}
                        }
                    }
                }
            }
            else cat.blockedSeconds=Math.Max(0,cat.blockedSeconds-dt*.5f);
            if(moved)
            {
                float dx=next.x-s.pet.x,dz=next.z-s.pet.z;
                if(dx*dx+dz*dz>1e-8)cat.heading=(float)(Math.Atan2(dx,dz)*180/Math.PI);
                s.pet=next;
            }
            if(yielding&&Point.Distance(s.pet,aim)<.03f)
            {yieldTargets.Remove(cat.id);cat.blockedSeconds=0;s.route=CatNavigation.Path(s.pet,s.target);}
            else if(!yielding&&Point.Distance(s.pet,aim)<.001f)
            {s.route.RemoveAt(0);if(s.route.Count==0)Brain(cat.id).Arrive();}
        }
        public bool TryInteract(int first,int second,SocialKind kind)
        {
            var a=Cat(first);var b=Cat(second);
            if(a==null||b==null||first==second||Paired(first)||Paired(second)||!Available(a)||!Available(b)||a.socialCooldown>0||b.socialCooldown>0||Point.Distance(a.life.pet,b.life.pet)>6||!Room.Clear(a.life.pet,b.life.pet))return false;
            a.attentionCatId=b.id;b.attentionCatId=a.id;a.attentionSeconds=b.attentionSeconds=18;
            Sessions.Add(new InteractionSession{first=first,second=second,kind=kind,phase=SocialPhase.Invitation});return true;
        }
        bool Accept(CatState a,CatState b,SocialKind kind)
        {
            var r=Relationship(b.id,a.id);var p=b.life.personality;
            if(kind==SocialKind.Chase)return p.playfulness*.55f+r.comfort*.25f+b.life.energy/100f*.2f>.43f;
            if(kind==SocialKind.RestTogether)return b.life.energy<80&&r.comfort>.38f;
            return p.sociability*.5f+r.comfort*.35f+p.curiosity*.15f>.36f;
        }
        void SocialOpportunities()
        {
            foreach(var a in State.cats)
            {
                if(a.socialCooldown>0||Paired(a.id)||!Available(a))continue;
                CatState nearest=null;float distance=6;
                foreach(var b in State.cats)if(a.id!=b.id&&!Paired(b.id)&&Available(b)&&b.socialCooldown<=0)
                {float d=Point.Distance(a.life.pet,b.life.pet);if(d<distance&&Room.Clear(a.life.pet,b.life.pet)){nearest=b;distance=d;}}
                if(nearest==null)continue;
                var p=a.life.personality;
                SocialKind kind=a.life.energy<65?SocialKind.RestTogether:p.playfulness>.65f&&a.life.energy>65?SocialKind.Chase:SocialKind.Sniff;
                TryInteract(a.id,nearest.id,kind);
                // An invitation is an opportunity, not a perpetual demand for company.
                a.socialCooldown=90+(1-p.sociability)*180;
            }
        }
        bool PlanSlots(InteractionSession session,CatState a,CatState b)
        {
            float gap=a.radius+b.radius+.18f;
            Point middle=new Point((a.life.pet.x+b.life.pet.x)/2,(a.life.pet.z+b.life.pet.z)/2);
            double direction=Math.Atan2(b.life.pet.z-a.life.pet.z,b.life.pet.x-a.life.pet.x);
            for(int k=0;k<12;k++)
            {
                double angle=direction+k*Math.PI/6;float x=(float)Math.Cos(angle)*gap*.5f,z=(float)Math.Sin(angle)*gap*.5f;
                var one=new Point(middle.x-x,middle.z-z);var two=new Point(middle.x+x,middle.z+z);
                if(!Space(one,a.id,b.id,true)||!Space(two,b.id,a.id,true)||CatNavigation.Path(a.life.pet,one).Count==0||CatNavigation.Path(b.life.pet,two).Count==0)continue;
                session.firstSlot=one;session.secondSlot=two;return true;
            }
            return false;
        }
        void UpdateSessions(float dt)
        {
            for(int i=Sessions.Count-1;i>=0;i--)
            {
                var session=Sessions[i];var a=Cat(session.first);var b=Cat(session.second);session.elapsed+=dt;session.phaseTime+=dt;
                bool resting=session.kind==SocialKind.RestTogether&&session.phase==SocialPhase.Together;
                if((!resting&&(!Available(a)||!Available(b)))||humanBusy.Contains(a.id)||humanBusy.Contains(b.id)||a.life.hunger>=85||b.life.hunger>=85||a.life.thirst>=85||b.life.thirst>=85||session.elapsed>180)
                {EndSession(i,false);continue;}
                if(session.phase==SocialPhase.Invitation)
                {
                    if(session.phaseTime<4)continue;
                    if(!Accept(a,b,session.kind)||!PlanSlots(session,a,b)){EndSession(i,false);continue;}
                    session.phase=SocialPhase.Approach;session.phaseTime=0;
                    Brain(a.id).Request(Activity.Greet,session.firstSlot);Brain(b.id).Request(Activity.Greet,session.secondSlot);continue;
                }
                if(session.phase==SocialPhase.Approach)
                {
                    if(session.phaseTime>100){EndSession(i,false);continue;}
                    if(Point.Distance(a.life.pet,session.firstSlot)>.025f||Point.Distance(b.life.pet,session.secondSlot)>.025f)continue;
                    session.phase=SocialPhase.Together;session.phaseTime=0;
                    if(resting||session.kind==SocialKind.RestTogether)
                    {a.life.activity=a.life.intent=b.life.activity=b.life.intent=Activity.Sleep;}
                }
                if(session.phase!=SocialPhase.Together)continue;
                a.attentionCatId=b.id;b.attentionCatId=a.id;a.attentionSeconds=b.attentionSeconds=2;
                if(session.kind==SocialKind.Sniff)
                {
                    a.heading=(float)(Math.Atan2(b.life.pet.x-a.life.pet.x,b.life.pet.z-a.life.pet.z)*180/Math.PI);
                    b.heading=(float)(Math.Atan2(a.life.pet.x-b.life.pet.x,a.life.pet.z-b.life.pet.z)*180/Math.PI);
                }
                if(session.kind==SocialKind.Chase)
                {
                    if(session.phaseTime>55){EndSession(i,true);continue;}
                    // Pursuit uses successive reserved waypoints, never the other cat's body.
                    if(a.life.activity!=Activity.Walk)
                    {
                        Point aim=FindFree(new Point(a.life.pet.x+(a.id%2==0?2.5f:-2.5f),a.life.pet.z+1.8f),a.id,0,true);
                        session.firstSlot=aim;Brain(a.id).Request(Activity.Explore,aim);
                    }
                    if(b.life.activity!=Activity.Walk&&Point.Distance(a.life.pet,b.life.pet)>2.3f)
                    {session.secondSlot=FindFree(Point.Toward(a.life.pet,b.life.pet,2),b.id,0,true);Brain(b.id).Request(Activity.Explore,session.secondSlot);}
                }
                else if(session.phaseTime>(session.kind==SocialKind.Sniff?9:75))EndSession(i,true);
            }
        }
        void EndSession(int index,bool completed)
        {
            var session=Sessions[index];Sessions.RemoveAt(index);
            foreach(int id in new[]{session.first,session.second})
            {
                var c=Cat(id);c.socialCooldown=150+(1-c.life.personality.sociability)*180;c.attentionSeconds=0;yieldTargets.Remove(id);
                // Do not erase an independently chosen need or a user's new action.
                if(c.life.intent==Activity.Greet||(session.phase==SocialPhase.Together&&session.kind==SocialKind.Chase&&c.life.intent==Activity.Explore))
                {c.life.route.Clear();c.life.target=c.life.pet;c.life.activity=c.life.intent=Activity.Idle;c.life.remaining=30+c.id*3;}
                if(c.life.hunger>=85||c.life.thirst>=85||c.life.energy<20)c.life.remaining=0;
                if(completed)
                {
                    int other=id==session.first?session.second:session.first;var r=Relationship(id,other);r.meetings++;if(session.kind==SocialKind.Chase)r.games++;
                    r.familiarity=Math.Min(1,r.familiarity+.015f);r.comfort=Math.Min(1,r.comfort+.008f);
                    Brain(id).Record("cat-social",session.kind==SocialKind.Chase?"和"+Cat(other).life.name+"玩了一会儿追逐。":session.kind==SocialKind.Sniff?"和"+Cat(other).life.name+"靠近闻了闻。":"和"+Cat(other).life.name+"在附近休息了一会儿。");
                    var e=c.life.events[c.life.events.Count-1];e.actorId=id;e.otherCatId=other;
                }
            }
        }
        public void CancelFor(int id){for(int i=Sessions.Count-1;i>=0;i--)if(Sessions[i].first==id||Sessions[i].second==id)EndSession(i,false);}
        public void CancelTransient()
        {Outing?.Cancel();Bath?.Cancel();for(int i=Sessions.Count-1;i>=0;i--)EndSession(i,false);humanBusy.Clear();yieldTargets.Clear();foreach(var c in State.cats)Brain(c.id).EndHandFeeding();}
        public bool Call(int id,Point viewer)
        {
            var c=Cat(id);if(c==null)return false;
            if(c.life.activity==Activity.Sleep||c.life.energy<25||c.life.hunger>=85||c.life.thirst>=85||c.life.intent==Activity.Hide||Point.Distance(c.life.pet,viewer)>30||humanBusy.Contains(id)||Brain(id).HandFeedingActive)return false;
            c.attentionSeconds=12;c.attentionCatId=0;
            float willingness=c.life.personality.sociability*.55f+c.life.affection/100f*.25f+(1-c.life.personality.independence)*.2f;
            if(willingness<.38f||c.life.activity==Activity.Eat||c.life.activity==Activity.Drink)return false;
            var destination=FindFree(Point.Toward(viewer,c.life.pet,1.8f),id,0,true);
            if(CatNavigation.Path(c.life.pet,destination).Count==0)return false;
            Navigate(id,destination,Activity.Greet);return true;
        }
        public void RollBall(Point target)
        {
            State.ball=new Point(Math.Max(Room.MinX,Math.Min(Room.MaxX,target.x)),Math.Max(Room.MinZ,Math.Min(Room.MaxZ,target.z)));State.ballTrapped=!Room.Walkable(State.ball);
            CatState winner=null;float best=-1;
            foreach(var c in State.cats)
            {
                c.life.ball=State.ball;c.life.ballTrapped=State.ballTrapped;
                if(!Available(c)||Point.Distance(c.life.pet,target)>6||!Room.Clear(c.life.pet,target))continue;
                c.attentionSeconds=12;
                float score=c.life.personality.playfulness*.6f+c.life.boredom/100f*.4f-Point.Distance(c.life.pet,target)*.04f;
                if(score>best){best=score;winner=c;}
            }
            if(winner!=null&&!State.ballTrapped)
            {
                if(State.toyOwnerId!=0&&State.toyOwnerId!=winner.id)
                {var previous=Cat(State.toyOwnerId).life;previous.route.Clear();previous.target=previous.pet;previous.activity=previous.intent=Activity.Idle;previous.remaining=20;}
                State.toyOwnerId=winner.id;CancelFor(winner.id);Brain(winner.id).InvitePlay(target);
            }
        }
        public void Checkpoint(long utcTicks)
        {State.lastUtcTicks=Math.Max(State.lastUtcTicks,utcTicks);foreach(var c in State.cats)Brain(c.id).Checkpoint(utcTicks);}
        public double Resume(long utcTicks)
        {
            if(utcTicks<=State.lastUtcTicks)return 0;
            CancelTransient();State.toyOwnerId=0;double elapsed=TimeSpan.FromTicks(utcTicks-State.lastUtcTicks).TotalSeconds;
            foreach(var c in State.cats)
            {
                var brain=Brain(c.id);brain.ExternalLocomotion=false;
                var destination=brain.ResolveDestination;var path=brain.FindPath;var intent=brain.ResolveIntent;brain.ResolveDestination=null;brain.FindPath=null;brain.ResolveIntent=null;
                try{brain.Resume(utcTicks);}finally{brain.ExternalLocomotion=true;brain.ResolveDestination=destination;brain.FindPath=path;brain.ResolveIntent=intent;}
                c.life.route.Clear();c.life.target=c.life.pet;
                if(c.life.activity==Activity.Walk)c.life.activity=c.life.intent=Activity.Idle;
                c.attentionSeconds=0;c.blockedSeconds=0;c.socialCooldown=60+c.id*17;c.waitingFor=Activity.Idle;
                if(IsBowl(c.life.activity))
                {
                    var care=c.life.activity;
                    if((care==Activity.Eat?c.life.hunger:c.life.thirst)>8){c.waitingFor=care;c.queuedAt=State.seconds;}
                    c.life.activity=c.life.intent=Activity.Idle;c.life.remaining=1;
                }
            }
            State.seconds+=Math.Min(elapsed,Simulation.MaxOfflineRealSeconds)*Simulation.Speed;State.lastUtcTicks=utcTicks;State.remainder=0;
            State.toyOwnerId=0;SettlePositions();return elapsed;
        }
    }
}
