using System;
namespace Momo.Core
{
    // A short cooperative exploration round. No offline rewards or save migration:
    // the round belongs to the current visit, while its memory is saved normally.
    public sealed class GardenOuting
    {
        readonly WorldSimulation world;
        public readonly int CatId;
        public bool Active {get;private set;}=true;
        public bool Completed {get;private set;}
        public int Station {get;private set;}
        public string Hint {get;private set;}="";
        public static readonly Point[] Stops={new Point(3,13),new Point(3,19.5f),new Point(10,19)};
        public Point Target {get;private set;}
        float elapsed,staying;
        public GardenOuting(WorldSimulation owner,int id)
        {world=owner;CatId=id;Go();}
        void Go()
        {
            Target=world.FindFree(Stops[Station],CatId,0,true);
            world.SetHumanBusy(CatId,true);world.Navigate(CatId,Target);
            Hint="后院寻趣 "+(Station+1)+"/3 · 跟着光圈走，陪它停下来闻一闻。";
        }
        public void Tick(float dt)
        {
            if(!Active)return;
            var s=world.Cat(CatId).life;elapsed+=dt;
            if(world.State.focusedCatId!=CatId||s.energy<25||s.hunger>=85||s.thirst>=85||s.activity==Activity.Sleep||s.intent!=Activity.Explore||elapsed>900)
            {Cancel();return;}
            bool together=world.ObserverPosition.HasValue&&Point.Distance(world.ObserverPosition.Value,Target)<3;
            staying=together&&Point.Distance(s.pet,Target)<.4f?staying+dt:0;
            if(staying<12)return; // two real seconds of shared discovery
            staying=0;Station++;
            if(Station<Stops.Length){Go();return;}
            Active=false;Completed=true;world.SetHumanBusy(CatId,false);
            s.boredom=Math.Max(0,s.boredom-18);
            // Memory/relief are the reward. No repeatable currency or bond farming.
            world.Brain(CatId).Record("garden-outing","和你一起逛完后院，在三个角落闻到了新鲜气味。");
            Hint="后院寻趣完成！它闻够了新气味，回头等你一起走。";
            if(world.ObserverPosition.HasValue)world.Call(CatId,world.ObserverPosition.Value);
        }
        public void Cancel()
        {
            if(!Active)return;Active=false;world.SetHumanBusy(CatId,false);
            var s=world.Cat(CatId).life;
            if(s.intent==Activity.Explore)world.Brain(CatId).RestFromCompany();
            Hint="这轮寻趣先休息，准备好后可以再出发。";
        }
    }
}
