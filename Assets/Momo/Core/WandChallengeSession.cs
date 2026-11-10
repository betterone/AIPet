using System;

namespace Momo.Core
{
    [Serializable] public sealed class PlayTuning
    {
        public int rulesVersion=1, missLimit=3, nearScore=10, farScore=20, qualifyingCatches=3;
        public float roundSeconds=60, chaseDistance=.65f, minimumChaseSeconds=.22f, readySeconds=1.35f;
        public float nearRadius=.42f, farRadius=.27f, perfectMultiplier=1.5f;
        public int[] facilityThresholds={2,8,20}, skillThresholds={10,40,100}, comboThresholds={3,6,10};
        public void Validate()
        {
            if(rulesVersion<1||missLimit<1||nearScore<1||farScore<nearScore||qualifyingCatches<1||!Positive(roundSeconds)||roundSeconds>300||!Positive(chaseDistance)||!Positive(minimumChaseSeconds)||!Positive(readySeconds)||!Positive(nearRadius)||!Positive(farRadius)||!Positive(perfectMultiplier))throw new ArgumentException("Invalid play tuning");
            foreach(var a in new[]{facilityThresholds,skillThresholds,comboThresholds}){if(a==null||a.Length!=3)throw new ArgumentException("Missing play thresholds");int last=0;foreach(int n in a){if(n<=last)throw new ArgumentException("Unordered play thresholds");last=n;}}
        }
        static bool Positive(float f)=>Room.Finite(f)&&f>0;
        public static int Level(int count,int[] thresholds){int level=0;foreach(int t in thresholds)if(count>=t)level++;return level;}
    }
    public enum WandPhase { Chase, Ready, Pounce, Recover, Finished, Aborted }
    public enum CatchGrade { None, Near, Far, PerfectNear, PerfectFar, Miss }
    public sealed class WandChallengeResult
    {
        public long Id {get;internal set;}
        public int CatId {get;internal set;}
        public int Layout {get;internal set;}
        public int Rules {get;internal set;}
        public uint Seed {get;internal set;}
        public int Score {get;internal set;}
        public int Catches {get;internal set;}
        public int Perfects {get;internal set;}
        public int BestCombo {get;internal set;}
        public int Misses {get;internal set;}
        public string ClosestMiss {get;internal set;}
    }
    // Real-time, independent of the six-times-speed pet simulation. Pointer
    // coordinates are in a small local ground plane, never household positions.
    public sealed class WandChallengeSession
    {
        public readonly PlayTuning Tuning;
        public readonly long Id;
        public readonly int CatId,Layout;
        public readonly uint Seed;
        uint random;
        double elapsed,phaseAge,dragStarted;
        float travel;int movementSamples;
        Point pounceFrom,pounceTo;
        System.Collections.Generic.List<Point> pounceRoute;float pounceLength,pounceDuration=.48f;
        bool dragging;
        float closestError=float.MaxValue;
        public WandPhase Phase {get;private set;}=WandPhase.Chase;
        public Point Cat {get;private set;}=new Point(0,0);
        public Point Feather {get;private set;}=new Point(0,-.65f);
        public Point Near {get;private set;}
        public Point Far {get;private set;}
        public Point Attention=>Phase==WandPhase.Pounce||Phase==WandPhase.Recover?pounceTo:Feather;
        public Point Motion {get;private set;}
        public CatchGrade LastGrade {get;private set;}
        public string Feedback {get;private set;}="拖动羽毛带它追起来，再收进圈里松手。";
        public string ClosestMiss {get;private set;}
        public int Score {get;private set;}
        public int Catches {get;private set;}
        public int Perfects {get;private set;}
        public int Combo {get;private set;}
        public int BestCombo {get;private set;}
        public int Misses {get;private set;}
        public int Stage=>PlayTuning.Level(Combo,Tuning.comboThresholds);
        public bool Active=>Phase!=WandPhase.Finished&&Phase!=WandPhase.Aborted;
        public bool Dragging=>dragging;
        public float Remaining=>(float)Math.Max(0,Tuning.roundSeconds-elapsed);
        public float PhaseProgress=>(float)Math.Min(1,phaseAge/(Phase==WandPhase.Ready?ReadyDuration:Phase==WandPhase.Pounce?pounceDuration:.32));
        public float AnimationProgress=>Phase==WandPhase.Pounce&&Stage>=2?(PhaseProgress>=1?1:(PhaseProgress*2)%1):PhaseProgress;
        public float ChaseProgress=>Math.Min(1,travel/Tuning.chaseDistance);
        public float ReadyDuration=>Math.Max(.95f,Tuning.readySeconds-Stage*.1f);
        public WandChallengeResult Result {get;private set;}
        public WandChallengeSession(long id,int catId,int layout,uint seed,PlayTuning tuning=null)
        {
            Tuning=tuning??new PlayTuning();Tuning.Validate();
            if(id<1||catId<1||layout<0||layout>3||seed==0)throw new ArgumentException("Invalid challenge identity");
            Id=id;CatId=catId;Layout=layout;Seed=random=seed;if(layout==2)Cat=new Point(-.8f,-.6f);Targets();
        }
        static Point Clamp(Point p)=>new Point(Math.Max(-1.2f,Math.Min(1.2f,p.x)),Math.Max(-.95f,Math.Min(.95f,p.z)));
        float Next(){random^=random<<13;random^=random>>17;random^=random<<5;return (random&0xffffff)/16777216f;}
        void Targets()
        {
            float side=Next()<.5f?-1:1;
            Near=new Point(side*(.48f+Next()*.22f),-.48f);
            Far=new Point(-side*(.64f+Next()*.2f),.67f);
            if(Layout==3){Near=new Point(-.1f,-.85f);Far=new Point(.1f,.94f);}
        }
        public void PointerDown(Point p)
        {
            if(!Active||(Phase!=WandPhase.Chase&&Phase!=WandPhase.Ready)||!Room.Finite(p.x)||!Room.Finite(p.z))return;
            dragging=true;dragStarted=elapsed;travel=0;movementSamples=0;Feather=Clamp(p);
        }
        public void PointerMove(Point p)
        {
            if(!dragging||!Active||!Room.Finite(p.x)||!Room.Finite(p.z))return;
            var next=Clamp(p);float moved=Point.Distance(Feather,next);if(moved>.005f){travel+=moved;movementSamples++;}Feather=next;
        }
        public void PointerCancel(){dragging=false;travel=0;if(Phase==WandPhase.Ready){Phase=WandPhase.Chase;phaseAge=0;}}
        public void PointerUp(Point p)
        {
            if(!dragging||!Active)return;PointerMove(p);dragging=false;
            if(Phase!=WandPhase.Ready)
            {if(travel>.08f)Miss("再带它追一小段，等猫伏低再松手。",2);return;}
            float near=Point.Distance(Feather,Near)/Tuning.nearRadius,far=Point.Distance(Feather,Far)/Tuning.farRadius;
            bool distant=far<near;float distance=Math.Min(near,far),time=PhaseProgress;
            if(distance>1.00001f){Miss("落点偏了：先把羽毛带进扑抓圈。",distance-1);return;}
            if(time<.45f-.00001f){Miss("松早了：等光圈收进抓取圈。",.45f-time);return;}
            if(time>.95f+.00001f){Miss("晚了一点：光圈重合时就可以松手。",time-.95f);return;}
            bool perfect=time>=.65f-.00001f&&time<=.85f+.00001f;
            LastGrade=perfect?(distant?CatchGrade.PerfectFar:CatchGrade.PerfectNear):(distant?CatchGrade.Far:CatchGrade.Near);
            int stage=Stage;float multiplier=stage==3?1.5f:stage==2?1.25f:stage==1?1.1f:1;
            Score+=(int)Math.Round((distant?Tuning.farScore:Tuning.nearScore)*(perfect?Tuning.perfectMultiplier:1)*multiplier,MidpointRounding.AwayFromZero);
            Catches++;if(perfect)Perfects++;Combo++;BestCombo=Math.Max(BestCombo,Combo);
            Feedback=perfect?"抓得漂亮！再来一次。":"抓住了！光圈重合时松手，可以更漂亮。";
            pounceFrom=Cat;var target=distant?Far:Near;
            float reach=Point.Distance(Cat,target);
            pounceTo=WandArena.Safe(reach>.45f?Point.Toward(target,Cat,.45f):Cat,Layout);pounceRoute=WandArena.Route(Cat,pounceTo,Layout);
            if(Stage>=2&&Layout<=1&&Point.Distance(Cat,pounceTo)>.5f)
            {var mid=new Point((Cat.x+pounceTo.x)*.5f+(Seed%2==0?.18f:-.18f),(Cat.z+pounceTo.z)*.5f);pounceRoute.Insert(0,mid);}
            pounceLength=WandArena.Length(Cat,pounceRoute);pounceDuration=Math.Max(.48f,pounceLength/2.4f);
            Phase=WandPhase.Pounce;phaseAge=0;
        }
        void Miss(string message,float error)
        {
            Misses++;Combo=0;LastGrade=CatchGrade.Miss;Feedback=message;dragging=false;
            if(error<closestError){closestError=error;ClosestMiss=message;}
            if(Misses>=Tuning.missLimit){Finish();return;}
            Phase=WandPhase.Recover;phaseAge=0;pounceTo=Cat;
        }
        public void Advance(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            // Fixed substeps make phase boundaries and travel independent of frame rate.
            while(seconds>1e-9&&Active)
            {
                double step=Math.Min(seconds,1.0/120);seconds-=step;elapsed+=step;phaseAge+=step;var before=Cat;
                if(elapsed+1e-8>=Tuning.roundSeconds){Finish();break;}
                if(Phase==WandPhase.Chase)
                {
                    if(dragging){var destination=WandArena.Safe(Feather,Layout);
                        Cat=WandArena.Follow(Cat,WandArena.Route(Cat,destination,Layout),(float)step*(1.05f+Stage*.23f));}
                    if(dragging&&movementSamples>=2&&travel+1e-6>=Tuning.chaseDistance&&elapsed-dragStarted+1e-8>=Tuning.minimumChaseSeconds)
                    {Phase=WandPhase.Ready;phaseAge=0;Feedback="它伏低了。把羽毛收进圈，光圈重合时松手！";}
                }
                else if(Phase==WandPhase.Ready&&phaseAge>=ReadyDuration){Miss("晚了一点：光圈重合时就可以松手。",.05f);}
                else if(Phase==WandPhase.Pounce)
                {
                    float t=Math.Min(1,(float)(phaseAge/pounceDuration)),ease=t*t*(3-2*t);
                    if(Stage>=2){float half=t<.5f?t*2:(t-.5f)*2;float smooth=half*half*(3-2*half);ease=t<.5f?smooth*.5f:.5f+smooth*.5f;}
                    Cat=WandArena.Follow(pounceFrom,pounceRoute,pounceLength*ease);
                    if(t>=1){Phase=WandPhase.Recover;phaseAge=0;}
                }
                else if(Phase==WandPhase.Recover&&phaseAge>=.32){Phase=WandPhase.Chase;phaseAge=0;travel=0;Targets();}
                Motion=new Point(Cat.x-before.x,Cat.z-before.z);
            }
        }
        void Finish()
        {
            Phase=WandPhase.Finished;dragging=false;
            Result=new WandChallengeResult{Id=Id,CatId=CatId,Layout=Layout,Rules=Tuning.rulesVersion,Seed=Seed,Score=Score,Catches=Catches,Perfects=Perfects,BestCombo=BestCombo,Misses=Misses,ClosestMiss=ClosestMiss};
        }
        public void Abort(){if(!Active)return;Phase=WandPhase.Aborted;dragging=false;Result=null;}
    }
}
