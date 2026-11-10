using System;

namespace Momo.Core
{
    public enum BathPhase { None, Notice, Approach, Wet, Soap, Rinse, Dry, Complete, Ended }
    public enum BathTool { Shower, Soap, Towel }
    public enum BathZone { Back, Left, Right }
    public struct BathContact
    {
        public bool touching, head;
        public BathTool tool;
        public BathZone zone;
        public int patch;
        public float speed; // Actual movement along the body, metres / real second.
    }

    // Foreground-only. No partial bath state, locks, wetness or animations enter a checkpoint.
    public sealed class BathSession
    {
        public static readonly Point Station=new Point(0,16.8f);
        public static readonly Point Exit=new Point(0,14.7f);
        public const int Patches=4;
        readonly WorldSimulation home;
        readonly Simulation sim;
        readonly float[,] coverage=new float[3,Patches];
        readonly float startingDirt;
        float elapsed, phaseTime, roughTime, quietTime, idleTime;
        public int CatId {get;private set;}
        public BathPhase Phase {get;private set;}
        public bool Active=>Phase!=BathPhase.None&&Phase!=BathPhase.Ended;
        public bool Washing=>Phase>=BathPhase.Wet&&Phase<=BathPhase.Dry;
        public bool Paused {get;private set;}
        public bool Committed {get;private set;}
        public int Pauses {get;private set;}
        public string Hint {get;private set;}
        public BathTool Tool=>Phase==BathPhase.Soap?BathTool.Soap:Phase==BathPhase.Dry?BathTool.Towel:BathTool.Shower;
        public bool Ready=>Washing&&Progress>=.9999f;
        public float Wetness=>Phase<BathPhase.Wet||Phase>=BathPhase.Complete?0:Phase==BathPhase.Wet?Progress:Phase==BathPhase.Dry?1-Progress:1;
        public float Foam=>Phase==BathPhase.Soap?Progress:Phase==BathPhase.Rinse?1-Progress:0;
        public float Progress {get{float sum=0;foreach(float c in coverage)sum+=c;return sum/(3*Patches);}}
        public float ZoneProgress(BathZone zone){float sum=0;for(int p=0;p<Patches;p++)sum+=coverage[(int)zone,p];return sum/Patches;}
        public float PatchProgress(BathZone zone,int patch)=>coverage[(int)zone,patch];
        public BathZone TargetZone {get{for(int z=0;z<3;z++)if(ZoneProgress((BathZone)z)<.9999f)return (BathZone)z;return BathZone.Right;}}
        public int TargetPatch {get{for(int p=0;p<Patches;p++)if(coverage[(int)TargetZone,p]<.9999f)return p;return Patches-1;}}
        public string StepName=>Phase==BathPhase.Wet?"1 / 4 · 打湿":Phase==BathPhase.Soap?"2 / 4 · 搓泡泡":Phase==BathPhase.Rinse?"3 / 4 · 冲净":Phase==BathPhase.Dry?"4 / 4 · 擦干":Phase==BathPhase.Complete?"洗好啦":"等它自己走来";

        internal BathSession(WorldSimulation world,int catId)
        {
            home=world;CatId=catId;sim=world.Brain(catId);startingDirt=sim.State.dirt;
            home.SetHumanBusy(catId,true);sim.BathingActive=true;
            Change(BathPhase.Notice,"它在看看洗澡盘，给它一点时间。");
        }
        void Change(BathPhase phase,string hint)
        {Phase=phase;phaseTime=roughTime=quietTime=idleTime=0;Hint=hint;Array.Clear(coverage,0,coverage.Length);}
        public bool AdvanceStage()
        {
            if(!Ready||Paused||Phase==BathPhase.Dry)return false;
            Change(Phase+1,Phase==BathPhase.Wet?"换上沐浴露，顺着身体慢慢搓出泡泡。":Phase==BathPhase.Soap?"用花洒冲掉泡泡，避开头脸。":"换上毛巾，轻轻擦干身体。");return true;
        }
        public void Update(float realSeconds,BathContact contact)
        {
            if(!Active||!Room.Finite(realSeconds)||realSeconds<=0)return;
            float dt=Math.Min(realSeconds,.1f);elapsed+=dt;phaseTime+=dt;
            if(Phase==BathPhase.Complete){if(phaseTime>=1.5f)Cancel("洗好啦，让它自在走一走。");return;}
            var s=sim.State;
            if(s.energy<15||s.hunger>=90||s.thirst>=90||s.activity==Activity.Sleep||s.intent==Activity.Hide)
            {Cancel("它想先吃喝或休息，这次先到这里。");return;}
            if(elapsed>300){Cancel("先歇一会儿，下次再洗吧。");return;}
            if(Phase==BathPhase.Notice)
            {
                if(phaseTime>=.9f+(1-s.personality.sociability)*.8f)
                {home.Navigate(CatId,Station,Activity.Greet);Change(BathPhase.Approach,"它自己走来了，留出洗澡盘的入口。");}
                return;
            }
            if(Phase==BathPhase.Approach)
            {
                if(elapsed>=30){Cancel("它还没准备好过来，下次再邀请吧。");return;}
                if(Point.Distance(s.pet,Station)<.05f&&s.activity!=Activity.Walk)
                {
                    s.route.Clear();s.target=s.pet;s.activity=s.intent=Activity.Idle;s.remaining=80;
                    Change(BathPhase.Wet,"按住花洒慢慢移动，先打湿身体，避开头脸。");
                }
                return;
            }
            idleTime=contact.touching?0:idleTime+dt;
            if(idleTime>=90){Cancel("先把工具收好，下次再继续。");return;}
            bool valid=contact.touching&&Room.Finite(contact.speed)&&contact.speed>=0;
            bool rough=valid&&(contact.head||contact.speed>1.4f);
            if(Paused)
            {
                quietTime=!contact.touching?quietTime+dt:0;
                if(quietTime>=2){Paused=false;roughTime=0;Hint="它放松了一点，慢慢来就好。";}
                return;
            }
            roughTime=rough?roughTime+dt:0;
            if(rough)Hint="轻一点，避开它的头脸。";
            if(roughTime>=2)
            {
                Pauses++;if(Pauses>=3){Cancel("它想离开了，今天先洗到这里。");return;}
                Paused=true;quietTime=0;Hint="它缩了缩身子。松开手，等它放松。";return;
            }
            if(!valid||rough||Ready||contact.tool!=Tool||contact.patch<0||contact.patch>=Patches||!Enum.IsDefined(typeof(BathZone),contact.zone))return;
            if((Tool==BathTool.Soap||Tool==BathTool.Towel)&&contact.speed<.02f)return;
            float seconds=Tool==BathTool.Shower?1f:1.5f;
            coverage[(int)contact.zone,contact.patch]=Math.Min(1,coverage[(int)contact.zone,contact.patch]+dt/seconds);
            Hint=Ready?"这一遍好了，换下一件工具吧。":"顺着身体慢慢移动，照顾到每一片毛。";
            if(Ready&&Phase==BathPhase.Dry)
            {
                s.dirt=0;Committed=true;
                if(startingDirt>=20){sim.Record("bath","在后院洗掉了身上的灰尘，又变得干干净净。");s.events[s.events.Count-1].actorId=CatId;}
                Change(BathPhase.Complete,"擦干啦。它又变得干干净净，可以自在去玩了。");
            }
        }
        public void Cancel(string reason="这次先到这里，下次再洗。")
        {
            if(!Active)return;Phase=BathPhase.Ended;Paused=false;Hint=reason;sim.BathingActive=false;
            home.SetHumanBusy(CatId,false);
            // Never relocate the body or persist a temporary bath activity.
            sim.State.route.Clear();sim.State.target=sim.State.pet;sim.State.activity=sim.State.intent=Activity.Idle;sim.State.remaining=10;
            home.Navigate(CatId,Exit,Activity.Idle);
        }
    }
}
