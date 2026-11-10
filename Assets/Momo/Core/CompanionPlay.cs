using System;
namespace Momo.Core
{
    public enum CompanyPhase { Quiet, Invite, GoingToHide, Hidden, Peeking, Returning, Found }
    public enum CatVoice { None, Invite, Peek, Found, Purr }
    // Foreground-only social behaviour. All locomotion still belongs to Simulation.
    public sealed class CompanionPlay
    {
        readonly Simulation sim;
        public CompanyPhase Phase {get;private set;}
        public bool Active=>Phase!=CompanyPhase.Quiet;
        public bool IsPeeking=>Phase==CompanyPhase.Peeking||Phase==CompanyPhase.Invite;
        public CatVoice Cue {get;private set;}
        public int Relocations {get;private set;}
        float elapsed,cooldown=30,seen;
        int spot=-1;
        bool hinted;
        public CompanionPlay(Simulation simulation){sim=simulation;}
        public bool Ready=>sim.State.energy>=40&&sim.State.hunger<80&&sim.State.thirst<80&&sim.State.activity!=Activity.Sleep&&sim.State.activity!=Activity.Eat&&sim.State.activity!=Activity.Drink&&sim.State.intent!=Activity.Hide;
        public bool Start(Point viewer)
        {
            if(!Ready||Active)return false;
            Phase=CompanyPhase.Invite;elapsed=seen=0;Relocations=0;Cue=CatVoice.Invite;
            sim.RestFromCompany();return true;
        }
        void Change(CompanyPhase phase){Phase=phase;elapsed=seen=0;hinted=false;}
        void Hide(Point viewer)
        {
            int next=-1;float score=float.MaxValue;
            for(int i=0;i<Room.HideSpots.Length;i++)
            {
                if(i==spot)continue;var p=Room.HideSpots[i];
                float d=Point.Distance(sim.State.pet,p),v=Point.Distance(viewer,p);
                float candidate=d+(Room.Clear(viewer,p)?4:0)+(v<1.5f?8:0);
                if(candidate<score){score=candidate;next=i;}
            }
            spot=next;Relocations++;Change(CompanyPhase.GoingToHide);sim.MoveForCompany(Room.HideSpots[spot]);
        }
        public void Found(Point viewer)
        {
            if(!Active||Phase==CompanyPhase.Found)return;Change(CompanyPhase.Found);Cue=CatVoice.Found;
            sim.EnjoyFound();sim.MoveForCompany(Room.Nearest(Point.Toward(viewer,sim.State.pet,1.0f)));
        }
        public void Cancel()
        {
            if(Active&&(sim.State.intent==Activity.Greet||(Phase==CompanyPhase.Invite&&sim.State.activity==Activity.Idle)))
            {sim.RestFromCompany();if(sim.State.energy<40||sim.State.hunger>=80||sim.State.thirst>=80)sim.State.remaining=0;}
            Phase=CompanyPhase.Quiet;cooldown=120+(1-sim.State.personality.playfulness)*90;Cue=CatVoice.None;elapsed=seen=0;
        }
        public void Update(float dt,Point viewer,bool visible,bool observing,bool canPlay)
        {
            Cue=CatVoice.None;if(dt<=0||!Room.Finite(dt))return;dt=Math.Min(dt,.1f);
            if(!canPlay){if(Active)Cancel();return;}
            if(!Active)
            {
                cooldown=Math.Max(0,cooldown-dt);
                if(cooldown<=0&&Ready&&(sim.State.activity==Activity.Idle||sim.State.activity==Activity.Watch||sim.State.activity==Activity.Groom))
                {cooldown=120+(1-sim.State.personality.playfulness)*90;Start(viewer);}
                return;
            }
            if(!Ready){Cancel();return;}elapsed+=dt;
            // Prevent regular utility selection from replacing an unfinished foreground gesture.
            sim.State.remaining=Math.Max(sim.State.remaining,120);
            float distance=Point.Distance(viewer,sim.State.pet);
            if(Phase==CompanyPhase.Invite){if(elapsed>=1.8f)Hide(viewer);return;}
            if(Phase==CompanyPhase.GoingToHide)
            {if(Point.Distance(sim.State.pet,Room.HideSpots[spot])<.3f)Change(CompanyPhase.Hidden);else if(elapsed>16){Change(CompanyPhase.Returning);sim.MoveForCompany(Room.PeekSpots[spot]);}return;}
            if(Phase==CompanyPhase.Hidden||Phase==CompanyPhase.Peeking)
            {
                seen=visible&&observing&&distance<1.8f?seen+dt:0;
                if(seen>=1){Found(viewer);return;}
                if(visible&&distance<2.5f&&distance>=1.8f&&elapsed>2&&Relocations<2){Hide(viewer);return;}
                if(!hinted&&elapsed>=6){hinted=true;Cue=CatVoice.Peek;sim.MoveForCompany(Room.PeekSpots[spot]);Phase=CompanyPhase.Peeking;}
                if(elapsed>=12){Change(CompanyPhase.Returning);sim.MoveForCompany(Room.PeekSpots[spot]);Cue=CatVoice.Invite;}
                return;
            }
            if(Phase==CompanyPhase.Returning)
            {
                seen=visible&&observing&&distance<2.2f?seen+dt:0;
                if(seen>=1){Found(viewer);return;}
                if(elapsed>10)Cancel();return;
            }
            if(Phase==CompanyPhase.Found)
            {if(elapsed>1.2f&&!hinted){hinted=true;Cue=CatVoice.Purr;}if(elapsed>4)Cancel();}
        }
    }
}
