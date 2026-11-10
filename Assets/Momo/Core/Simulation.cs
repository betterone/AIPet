using System;
using System.Collections.Generic;

namespace Momo.Core
{
    public sealed class Simulation
    {
        public const double TickSeconds = 5;
        public const double Speed = 6;
        public const double MaxOfflineRealSeconds = 30*86400;
        public readonly WorldState State;
        // The household owns all foreground locomotion. Legacy single-cat/offline
        // simulation still uses its original deterministic five-second movement.
        public bool ExternalLocomotion;
        public Func<Activity,Point,Point> ResolveDestination;
        public Func<Activity,bool> CanChoose;
        public Func<Activity,Activity> ResolveIntent;
        public Func<Point,Point,System.Collections.Generic.List<Point>> FindPath;
        public bool HandFeedingActive {get;private set;}
        public bool BathingActive {get;internal set;}
        bool handFeedRewarded;
        public string Reason { get; private set; } = "Settling into home";
        public readonly Dictionary<Activity,float> Scores = new Dictionary<Activity,float>();
        static readonly Activity[] choices = {Activity.Sleep,Activity.Eat,Activity.Drink,Activity.Groom,Activity.Play,Activity.Explore,Activity.Watch,Activity.Idle,Activity.Hide};
        public Simulation(WorldState state) { State=state; Validate(state); }
        public static WorldState New(long utcTicks, uint seed=827139)
        { return new WorldState{lastUtcTicks=utcTicks,createdUtcTicks=utcTicks,random=seed==0?1:seed}; }
        public static void Validate(WorldState s)
        {
            if(s==null||s.schema!=1||s.personality==null||s.playHabits==null||s.playHabits.Length!=24||s.cooldowns==null||s.cooldowns.Length!=11||s.events==null||s.events.Count>128||s.route==null||s.route.Count>Room.MaxRoutePoints) throw new ArgumentException("Unsupported or incomplete save");
            if(s.lastUtcTicks<=0||s.lastUtcTicks>DateTime.MaxValue.Ticks||s.createdUtcTicks<=0||s.createdUtcTicks>s.lastUtcTicks||s.random==0||s.nextEventId<1||s.revision<0||s.seconds<0||double.IsNaN(s.seconds)||double.IsInfinity(s.seconds)||double.IsNaN(s.remainder)||double.IsInfinity(s.remainder)||s.remainder<0||s.remainder>=TickSeconds||double.IsNaN(s.remaining)||double.IsInfinity(s.remaining)||s.remaining<0||s.remaining>10000||!Enum.IsDefined(typeof(Activity),s.activity)||!Enum.IsDefined(typeof(Activity),s.intent))throw new ArgumentException("Invalid simulation state");
            foreach(float v in new[]{s.energy,s.hunger,s.thirst,s.boredom,s.affection,s.dirt,s.food,s.water,s.ballPreference,s.wandPreference,s.windowPreference})if(!Room.Finite(v)||v<0||v>100)throw new ArgumentException("Invalid needs");
            foreach(float v in new[]{s.personality.curiosity,s.personality.sociability,s.personality.playfulness,s.personality.independence})if(!Room.Finite(v)||v<0||v>1)throw new ArgumentException("Invalid personality");
            foreach(float v in s.playHabits)if(!Room.Finite(v)||v<0||v>1)throw new ArgumentException("Invalid habits");
            foreach(float v in s.cooldowns)if(!Room.Finite(v)||v<0)throw new ArgumentException("Invalid cooldown");
            if(double.IsNaN(s.lastHandFeedAt)||double.IsInfinity(s.lastHandFeedAt)||s.lastHandFeedAt<0||s.lastHandFeedAt>s.seconds)throw new ArgumentException("Invalid hand feeding history");
            if(!Room.Walkable(s.pet)||!Room.Walkable(s.target)||!Room.Finite(s.ball.x)||!Room.Finite(s.ball.z)||s.ball.x<Room.MinX-.15f||s.ball.x>Room.MaxX+.15f||s.ball.z<Room.MinZ-.15f||s.ball.z>Room.MaxZ+.15f)throw new ArgumentException("Invalid positions");
            foreach(var p in s.route)if(!Room.Walkable(p))throw new ArgumentException("Invalid path");
            long lastId=0;
            foreach(var e in s.events)
            {
                if(e==null||e.id<=lastId||e.id>=s.nextEventId||e.time<0||e.time>s.seconds||double.IsNaN(e.time)||double.IsInfinity(e.time)||string.IsNullOrEmpty(e.kind)||string.IsNullOrEmpty(e.text)||e.text.Length>1024)throw new ArgumentException("Invalid event history");
                lastId=e.id;
            }
            if(string.IsNullOrWhiteSpace(s.name)||s.name.Length>24)throw new ArgumentException("Invalid pet name");
        }
        float Random()
        { uint x=State.random; x^=x<<13;x^=x>>17;x^=x<<5;State.random=x;return (x&0xffffff)/16777216f; }
        static float Clamp(float v,float min=0,float max=100){return Math.Max(min,Math.Min(max,v));}
        public int Hour { get { return (int)((State.seconds/3600+9)%24); } }
        public int Day { get { return 1+(int)(State.seconds/86400); } }
        public void Advance(double simulatedSeconds)
        {
            if(double.IsNaN(simulatedSeconds)||double.IsInfinity(simulatedSeconds)||simulatedSeconds<0||simulatedSeconds>MaxOfflineRealSeconds*Speed)throw new ArgumentOutOfRangeException(nameof(simulatedSeconds));
            State.remainder+=simulatedSeconds;
            while(State.remainder>=TickSeconds){ State.remainder-=TickSeconds; Tick(); }
        }
        public double Resume(long utcTicks)
        {
            if(utcTicks<=State.lastUtcTicks)return 0; // Clock rollback must not replay time later.
            double elapsed=TimeSpan.FromTicks(utcTicks-State.lastUtcTicks).TotalSeconds;
            double bounded=Math.Min(elapsed,MaxOfflineRealSeconds);
            Advance(bounded*Speed);
            State.lastUtcTicks=utcTicks;
            if(elapsed>MaxOfflineRealSeconds)Record("clock","A long time away. Settled safely back into home.");
            return elapsed;
        }
        public void Checkpoint(long utcTicks){State.lastUtcTicks=Math.Max(State.lastUtcTicks,utcTicks);}
        public void Record(string kind,string text)
        {
            State.events.Add(new LifeEvent(State.nextEventId++,State.seconds,kind,text));
            if(State.events.Count>128)State.events.RemoveAt(0);
        }
        void Tick()
        {
            const float dt=(float)TickSeconds;
            var s=State;s.seconds+=dt;
            s.hunger=Clamp(s.hunger+dt*.018f);s.thirst=Clamp(s.thirst+dt*.022f);s.boredom=Clamp(s.boredom+dt*.025f);
            s.energy=Clamp(s.energy-dt*.012f);
            // Relationships should soften over weeks, not disappear after a few hours away.
            s.affection=Clamp(s.affection-dt*.00000012f);
            s.ballPreference=Clamp(s.ballPreference-dt*.0000003f);s.wandPreference=Clamp(s.wandPreference-dt*.0000003f);s.windowPreference=Clamp(s.windowPreference-dt*.0000003f);
            for(int i=0;i<24;i++)s.playHabits[i]=Math.Max(0,s.playHabits[i]-dt*.00000003f);
            for(int i=0;i<11;i++)s.cooldowns[i]=Math.Max(0,s.cooldowns[i]-dt);
            // Basic care is automatic; absence never kills or punishes the pet.
            if(s.food<10)s.food=90;if(s.water<10)s.water=90;
            if(s.activity==Activity.Walk)
            {
                if(ExternalLocomotion)return;
                if(s.intent==Activity.Play&&!s.ballTrapped&&Point.Distance(s.target,s.ball)>.8f)
                {s.target=Room.Nearest(s.ball);s.route=Room.Path(s.pet,s.target);}
                if(s.route.Count==0){Begin(s.intent);return;}
                // The expanded room needs a natural but decisive walk pace; otherwise choices
                // are consumed by transit and personality differences stop being observable.
                s.pet=Point.Toward(s.pet,s.route[0],dt*.18f);
                if(Point.Distance(s.pet,s.route[0])<.01f)s.route.RemoveAt(0);
                if(s.route.Count==0)Begin(s.intent);
                return;
            }
            if(HandFeedingActive||BathingActive)return; // Foreground care owns this brief turn; needs still advance.
            s.remaining=Math.Max(0,s.remaining-dt);
            switch(s.activity)
            {
                case Activity.Sleep:s.energy=Clamp(s.energy+dt*.16f);break;
                case Activity.Eat:s.hunger=Clamp(s.hunger-dt*.6f);s.food=Clamp(s.food-dt*.07f);break;
                case Activity.Drink:s.thirst=Clamp(s.thirst-dt*.75f);s.water=Clamp(s.water-dt*.08f);break;
                case Activity.Play:s.boredom=Clamp(s.boredom-dt*.24f);s.energy=Clamp(s.energy-dt*.016f);break;
                case Activity.Explore:s.boredom=Clamp(s.boredom-dt*.10f);break;
                case Activity.Watch:s.boredom=Clamp(s.boredom-dt*.07f);break;
            }
            bool satisfied=(s.activity==Activity.Sleep&&s.energy>90)||(s.activity==Activity.Eat&&s.hunger<8)||(s.activity==Activity.Drink&&s.thirst<8);
            if(s.remaining<=0||satisfied){s.cooldowns[(int)s.activity]=s.activity==Activity.Sleep?180:90;Choose();}
        }
        public void RefreshScores()
        {
            var s=State;var p=s.personality;bool night=Hour<6||Hour>=22;
            Scores[Activity.Sleep]=(100-s.energy)/100f*.95f+(night?.16f:0);
            Scores[Activity.Eat]=s.hunger/100f;
            Scores[Activity.Drink]=s.thirst/100f;
            Scores[Activity.Play]=s.ballTrapped?-1:s.boredom/100f*.65f+p.playfulness*.22f+s.ballPreference*.002f+s.playHabits[Hour]*.15f;
            Scores[Activity.Explore]=s.boredom/100f*.3f+p.curiosity*.26f;
            Scores[Activity.Watch]=.16f+p.independence*.16f+s.windowPreference*.002f+(!night?.06f:0);
            Scores[Activity.Groom]=.23f;
            Scores[Activity.Idle]=.18f;
            Scores[Activity.Hide]=.10f+p.independence*.08f;
            foreach(var a in choices) if(s.cooldowns[(int)a]>0)Scores[a]-=.45f;
            if(CanChoose!=null)foreach(var choice in choices)if(!CanChoose(choice))Scores[choice]=-100;
            if(s.energy<15)Scores[Activity.Sleep]+=1;
            if(s.hunger>90)Scores[Activity.Eat]+=1;
            if(s.thirst>90)Scores[Activity.Drink]+=1;
        }
        void Choose()
        {
            RefreshScores();Activity best=Activity.Idle;float score=-100;
            foreach(var a in choices){float v=Scores[a]+Random()*.20f;if(v>score){best=a;score=v;}}
            Reason=best==Activity.Sleep?"Rest after a busy day":best==Activity.Play?"Curiosity, playfulness & remembered play":best==Activity.Eat?"A little hungry":best==Activity.Drink?"Time for water":best==Activity.Watch?"A quiet, familiar spot":"A small change of pace";
            Point target=State.pet;
            switch(best)
            {
                case Activity.Sleep:target=Room.Bed;break;
                case Activity.Eat:target=Room.Food;break;
                case Activity.Drink:target=Room.Water;break;
                case Activity.Play:target=Room.Nearest(State.ball);break;
                case Activity.Watch:target=Room.Window;break;
                case Activity.Hide:target=Room.Box;break;
                case Activity.Explore:target=Random()<.55f?Room.ExploreSpots[(int)(Random()*Room.ExploreSpots.Length)]:Room.Nearest(new Point(Room.MinX+Random()*(Room.MaxX-Room.MinX),Room.MinZ+Random()*(Room.MaxZ-Room.MinZ)));break;
            }
            Go(best,target);
        }
        void Go(Activity intent, Point target)
        {
            if(ResolveIntent!=null)intent=ResolveIntent(intent);
            State.intent=intent;State.target=ResolveDestination!=null?ResolveDestination(intent,target):Room.Nearest(target);
            State.route=FindPath!=null?FindPath(State.pet,State.target):Room.Path(State.pet,State.target);
            if(Point.Distance(State.pet,State.target)<(HandFeedingActive?.015f:.08f))Begin(intent);
            else if(State.route.Count>0)State.activity=Activity.Walk;
            else {State.activity=Activity.Idle;State.remaining=30;Reason="Taking another path";}
        }
        public void Arrive()
        {State.route.Clear();State.target=State.pet;Begin(State.intent);}
        public void Request(Activity intent,Point target){Go(intent,target);}
        void Begin(Activity a)
        {
            State.activity=a;State.intent=a;
            State.remaining=a==Activity.Sleep?900:a==Activity.Watch?360:a==Activity.Eat||a==Activity.Drink?150:80+Random()*160;
            switch(a)
            {
                case Activity.Sleep:Record("rest","Curled up in the little bed.");break;
                case Activity.Play:
                    if(State.ballTrapped){State.remaining=10;break;}
                    State.ball=Room.Nearest(new Point(State.ball.x+(Random()-.5f)*1.8f,State.ball.z+(Random()-.5f)*1.8f));
                    Record("play","Batted the little red ball across the room.");break;
                case Activity.Watch:Record("window","Watched the world from the window.");State.windowPreference=Clamp(State.windowPreference+.3f);break;
                case Activity.Eat:Record("care","Stopped for a little snack.");break;
                case Activity.Drink:Record("care","Had a drink of fresh water.");break;
                case Activity.Explore:Record("explore","Discovered another quiet corner.");break;
            }
        }
        public string Pet()
        {
            if(State.seconds-State.lastPetAt<2)return "A gentle touch is enough.";
            if(State.seconds-State.lastPetAt<25)State.petStreak++;else State.petStreak=0;
            State.lastPetAt=State.seconds;
            if(State.petStreak>=3){Go(Activity.Hide,Room.Box);return "A little space, please.";}
            State.affection=Clamp(State.affection+1.5f);
            if(State.activity!=Activity.Sleep&&!HandFeedingActive){State.route.Clear();State.target=State.pet;State.activity=State.intent=Activity.Greet;State.remaining=80;}
            Record("together",State.activity==Activity.Sleep?"A gentle stroke while sleeping.":"Shared a quiet moment together.");
            return State.activity==Activity.Sleep?"A sleepy little purr.":"That feels lovely.";
        }
        public string InvitePlay(Point ball)
        {
            if(!Room.Finite(ball.x)||!Room.Finite(ball.z))throw new ArgumentException("Invalid toy input");
            ball=new Point(Clamp(ball.x,Room.MinX,Room.MaxX),Clamp(ball.z,Room.MinZ,Room.MaxZ));
            State.ball=ball;State.ballTrapped=!Room.Walkable(ball);
            Record("toy",State.ballTrapped?"The ball rolled out of reach.":"You rolled the red ball.");
            if(State.ballTrapped)return "The ball is out of reach. Tap it to bring it back.";
            if(State.energy<20||State.activity==Activity.Sleep)return "Maybe after this nap.";
            if(State.seconds-State.lastPlayAt>60)
            {State.ballPreference=Clamp(State.ballPreference+2);State.playHabits[Hour]=Clamp(State.playHabits[Hour]+.035f,0,1);State.affection=Clamp(State.affection+.5f);State.lastPlayAt=State.seconds;}
            State.boredom=Clamp(State.boredom+15);Reason="You invited a little play";Go(Activity.Play,ball);return "Something caught my eye.";
        }
        public string PlayWithWand(Point feather)
        {
            if(!Room.Finite(feather.x)||!Room.Finite(feather.z))throw new ArgumentException("Invalid feather position");
            if(State.activity==Activity.Sleep || State.energy<20)return "它现在想休息，羽毛还在等你。";
            Point target=Room.Nearest(feather);
            bool eager=State.personality.playfulness>=.45f;
            if(!eager)target=Room.Nearest(Point.Toward(State.pet,target,.9f));
            if(State.wandPreference<=0 || State.seconds-State.lastWandAt>60)
            {
                State.wandPreference=Clamp(State.wandPreference+(eager?2f:1f));
                State.playHabits[Hour]=Clamp(State.playHabits[Hour]+.025f,0,1);
                State.affection=Clamp(State.affection+.4f);
                State.lastWandAt=State.seconds;
            }
            State.boredom=Clamp(State.boredom-4);
            Reason="Following the feather";
            Go(Activity.Greet,target);
            Record("toy",eager?"追着羽毛逗猫棒跑了几步。":"观察羽毛逗猫棒，轻轻试探了一下。");
            return eager?"它盯紧羽毛，准备扑过来。":"它先观察了一会儿，轻轻拍了下羽毛。";
        }
        public bool InviteFromCat()
        {
            if(State.energy<30 || (State.activity!=Activity.Idle && State.activity!=Activity.Watch && State.activity!=Activity.Groom))return false;
            State.ball=Room.Nearest(new Point(State.pet.x+.6f,State.pet.z-.3f));
            State.ballTrapped=false;
            Reason="Inviting you to play";
            Go(Activity.Play,State.ball);
            Record("invitation","主动把红球拨过来，像在邀请你一起玩。");
            return true;
        }
        public void MoveForCompany(Point point)
        {Reason="Playing peekaboo with you";Go(Activity.Greet,point);}
        public void RestFromCompany()
        {State.route.Clear();State.target=State.pet;State.activity=State.intent=Activity.Idle;State.remaining=80;}
        public void EnjoyFound()
        {State.affection=Clamp(State.affection+.5f);State.boredom=Clamp(State.boredom-5);Record("together","躲在家具后探头，等你找到后又凑了过来。");}
        public void Call(Point point)
        {if(State.activity==Activity.Sleep)return;Reason="Following your touch";Go(Activity.Greet,Room.Nearest(point));}
        public bool BeginHandFeeding(Point target)
        {
            if(HandFeedingActive||BathingActive||State.hunger<25||State.energy<15||State.activity==Activity.Sleep||State.activity==Activity.Eat||State.activity==Activity.Drink||State.intent==Activity.Hide||!Room.Walkable(target))return false;
            var path=Room.Path(State.pet,target);if(path.Count==0)return false;
            HandFeedingActive=true;handFeedRewarded=false;AimHandFood(target);return true;
        }
        public void AimHandFood(Point target)
        {if(HandFeedingActive){Reason="Following the food in your palm";Go(Activity.Greet,target);}}
        public void WaitForHandFood()
        {if(HandFeedingActive){State.route.Clear();State.target=State.pet;State.activity=State.intent=Activity.Greet;State.remaining=80;}}
        public float HandFeedBite(Point target)
        {
            if(!HandFeedingActive||!Room.Walkable(target)||Point.Distance(State.pet,target)>.22f||State.activity==Activity.Walk||State.hunger<=8)return 0;
            float amount=Math.Min(5,State.hunger-8);State.hunger=Clamp(State.hunger-amount);
            if(!handFeedRewarded)
            {
                handFeedRewarded=true;
                if(!State.hasHandFed||State.seconds-State.lastHandFeedAt>=360)
                {State.affection=Clamp(State.affection+1);State.hasHandFed=true;State.lastHandFeedAt=State.seconds;}
                Record("hand-feed","从你的掌心吃到猫粮，慢慢熟悉你的手。");
            }
            return amount;
        }
        public void EndHandFeeding()
        {if(!HandFeedingActive)return;HandFeedingActive=false;if(State.intent==Activity.Greet)RestFromCompany();}
        public void WakeGently()
        {if(State.activity==Activity.Sleep){State.activity=Activity.Groom;State.remaining=50;Record("together","Woke to a familiar presence.");}}
    }
}
