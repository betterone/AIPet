using System;

namespace Momo.Core
{
    public enum FeedingPhase { None, Scoop, TurnPalm, Offer, Notice, Approach, Sniff, Eat, Pause, Thanks, Retract }
    // A transient foreground session. Simulation alone moves the pet and commits nutrition/bond.
    public sealed class HandFeeding
    {
        readonly Simulation sim;
        public FeedingPhase Phase {get;private set;}
        public bool Active=>Phase!=FeedingPhase.None;
        public bool Holding=>Phase>=FeedingPhase.Offer&&Phase<FeedingPhase.Retract;
        public bool Eating=>Phase==FeedingPhase.Eat;
        public int Bites {get;private set;}
        public float PhaseTime {get;private set;}
        public float Nutrition {get;private set;}
        public string Hint {get;private set;}
        Point aimed;
        float total,stable,biteTime;
        public HandFeeding(Simulation simulation){sim=simulation;}
        void Change(FeedingPhase phase,string hint){Phase=phase;PhaseTime=0;Hint=hint;}
        public bool Grab()
        {
            if(Active)return false;
            total=stable=biteTime=Nutrition=0;Bites=0;
            Change(FeedingPhase.Scoop,"右手抓起一小把猫粮……");return true;
        }
        public void Withdraw()
        {if(!Active||Phase==FeedingPhase.Retract)return;sim.EndHandFeeding();Change(FeedingPhase.Retract,"先把猫粮收好，下次再来。");}
        public void Cancel(){sim.EndHandFeeding();Phase=FeedingPhase.None;PhaseTime=0;Hint=null;}
        bool WillNotice(Point target,bool visible)
        {return visible&&Room.Walkable(target)&&Point.Distance(sim.State.pet,target)<=4.5f;}
        public void Update(float dt,Point target,bool steady,bool visible,bool contact)
        {
            if(!Active||!Room.Finite(dt)||dt<=0)return;
            dt=Math.Min(dt,.1f);PhaseTime+=dt;total+=dt;
            if(Phase==FeedingPhase.Retract){if(PhaseTime>=.55f)Cancel();return;}
            if(total>90){Withdraw();return;}
            if(Phase==FeedingPhase.Scoop){if(PhaseTime>=1.1f)Change(FeedingPhase.TurnPalm,"轻轻翻过手，掌心朝上。");return;}
            if(Phase==FeedingPhase.TurnPalm){if(PhaseTime>=.7f)Change(FeedingPhase.Offer,"拿着猫粮走近它，拖动右手调整位置。");return;}
            if(sim.HandFeedingActive&&(sim.State.intent==Activity.Hide||sim.State.energy<15||sim.State.activity==Activity.Sleep))
            {sim.EndHandFeeding();Change(FeedingPhase.Offer,"它想先休息，别着急。");}
            bool moved=Point.Distance(aimed,target)>.025f;
            stable=steady&&!moved?stable+dt:0;
            if(Phase==FeedingPhase.Offer)
            {
                aimed=target;
                if(steady&&WillNotice(target,visible)&&sim.BeginHandFeeding(target))
                {sim.WaitForHandFood();Change(FeedingPhase.Notice,"它注意到你手里的猫粮，先看看你。");}
                else if(sim.State.hunger<25)Hint="它现在不太饿，先闻闻就好。可以摸摸它。";
                else if(sim.State.activity==Activity.Sleep)Hint="它正在睡觉，等醒来再喂吧。";
                else Hint="走近一点，在空地把右手停稳，等它自己过来。";
                return;
            }
            if(Phase==FeedingPhase.Thanks)
            {if(PhaseTime>2){sim.EndHandFeeding();Change(FeedingPhase.Retract,"吃好啦，下次再来。");}return;}
            if(!sim.HandFeedingActive)return;
            if(!visible||!Room.Walkable(target)||Point.Distance(sim.State.pet,target)>6)
            {sim.EndHandFeeding();Change(FeedingPhase.Offer,"先走近一点，让它能看见你的手。");return;}
            if(Phase==FeedingPhase.Notice)
            {aimed=target;if(PhaseTime>=.7f+(1-sim.State.personality.sociability)*.9f){sim.AimHandFood(target);Change(FeedingPhase.Approach,"它自己走过来了，把手放低一点。");}return;}
            if(Phase==FeedingPhase.Approach)
            {
                if(moved){aimed=target;sim.AimHandFood(target);}
                if(Point.Distance(sim.State.pet,target)<.035f&&steady)
                {sim.WaitForHandFood();Change(FeedingPhase.Sniff,"先闻一闻……手停稳，它才放心。");}
                return;
            }
            if(moved||!steady||!contact)
            {
                if(Phase!=FeedingPhase.Pause){sim.WaitForHandFood();Change(FeedingPhase.Pause,"它抬头看了看。把手放稳，再继续。");}
                if(moved){aimed=target;sim.AimHandFood(target);}
                biteTime=0;return;
            }
            if(Phase==FeedingPhase.Pause)
            {if(stable>=.65f&&Point.Distance(sim.State.pet,target)<.2f){sim.WaitForHandFood();Change(FeedingPhase.Sniff,"它又凑过来闻了闻。");}return;}
            if(Phase==FeedingPhase.Sniff)
            {if(PhaseTime>=1.1f){biteTime=0;Change(FeedingPhase.Eat,"它正从右手掌心吃猫粮。轻轻摸头，左手会回应。");}return;}
            if(Phase==FeedingPhase.Eat)
            {
                biteTime+=dt;
                if(biteTime>=1.4f)
                {
                    biteTime=0;float consumed=sim.HandFeedBite(target);
                    if(consumed>0){Nutrition+=consumed;Bites++;}
                    if(Bites>=4||consumed<=0||sim.State.hunger<=8)
                        Change(FeedingPhase.Thanks,sim.State.affection>=25?"吃好了，它安心地停在掌边。":"吃好了，让它慢慢熟悉你的手。");
                }
            }
        }
    }
}
