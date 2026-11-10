using System;
using System.Collections.Generic;
using UnityEngine;
using Momo.Core;
namespace Momo
{
    public sealed class PlayGarden : MonoBehaviour
    {
        MomoApp app;
        Transform fixtures;
        int built=-1,activeCat,activeFacility;
        float cooldown=14,age,poseAge;
        Point anchor;
        Transform outingMarker;string outingHint;
        public static readonly Point Mat=new Point(7.2f,-.1f),Box=new Point(8.35f,-2.5f),Tunnel=new Point(9.3f,-2.6f);
        public void Build(MomoApp owner)
        {
            app=owner;transform.SetParent(app.room.transform,false);fixtures=new GameObject("Play things you built").transform;fixtures.SetParent(transform,false);
            var stand=Art.Pivot("Home feather stand",transform,new Vector3(6.9f,.03f,.65f));
            Art.Cylinder("Wand cup",stand,new Vector3(0,.12f,0),new Vector3(.24f,.12f,.24f),Art.Mat("Wand cup blue","#659CAB"));
            var wand=Art.Shape("Home feather wand",PrimitiveType.Cylinder,stand,new Vector3(0,.44f,0),new Vector3(.026f,.34f,.026f),Art.Mat("Wand wood","#B28A61"));wand.transform.localRotation=Quaternion.Euler(0,0,-14);
            Art.Sphere("Feather waiting for you",stand,new Vector3(.12f,.82f,0),new Vector3(.16f,.33f,.08f),Art.Mat("Home feather blue","#388C9C"));
            var hit=stand.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,.4f,0);hit.size=new Vector3(.5f,.9f,.5f);stand.gameObject.AddComponent<TouchTarget>().kind="play-wand";
            Refresh();
        }
        public void Refresh()
        {
            if(app==null||built==app.Progress.facilityLevel)return;built=app.Progress.facilityLevel;
            foreach(Transform child in fixtures){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            for(int level=1;level<=built;level++)
            {
                var root=new GameObject(PlayProgress.FacilityName(level)).transform;root.SetParent(fixtures,false);
                Point p=level==1?Mat:level==2?Box:Tunnel;root.localPosition=new Vector3(p.x,0,p.z);
                WandChallengeStage.BuildFacility(root,level);
                // The box occupies an already-solid furniture seat; the tunnel's
                // sides match the existing playroom poles. No new invisible walls.
                if(level==1)root.localScale=Vector3.one*.65f;
                var hit=root.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,.3f,0);hit.size=new Vector3(1,.6f,.8f);root.gameObject.AddComponent<TouchTarget>().kind="play-facility-"+level;
            }
        }
        public void Tick(float dt)
        {
            Refresh();
            var outing=app.household.Outing;
            if(outing!=null&&outing.Hint!=outingHint){outingHint=outing.Hint;app.Notify(outingHint);}
            bool exploring=outing!=null&&outing.Active;
            if(exploring&&outingMarker==null)
            {
                outingMarker=Art.Pivot("Garden discovery glow",transform,Vector3.zero);
                Art.Cylinder("Discovery circle",outingMarker,new Vector3(0,.035f,0),new Vector3(1.4f,.02f,1.4f),Art.Mat("Discovery gold","#ECC76D"));
                Art.Sphere("Scent spark",outingMarker,new Vector3(0,.65f,0),new Vector3(.15f,.15f,.15f),Art.Mat("Scent cream","#FFF0BB"));
            }
            if(outingMarker!=null){outingMarker.gameObject.SetActive(exploring);if(exploring)outingMarker.localPosition=new Vector3(outing.Target.x,0,outing.Target.z);}
            if(exploring){Cancel();return;}
            if(app.ChallengeActive||app.BathActive||app.FeedingActive||app.WandActive||app.LearnedWandActive||app.boutique.gameObject.activeSelf){Cancel();return;}
            cooldown-=dt;
            if(activeCat==0)
            {
                if(cooldown>0||built==0)return;cooldown=45;
                foreach(var resident in app.household.State.cats)
                {
                    var s=resident.life;if(!s.adopted||s.energy<30||s.activity==Activity.Sleep||s.activity==Activity.Walk||s.activity==Activity.Eat||s.activity==Activity.Drink||s.intent==Activity.Hide)continue;
                    if(Point.Distance(s.pet,Mat)>7)continue;
                    activeFacility=Math.Min(built,Math.Max(1,app.Progress.Cat(resident.id).skillLevel));
                    Point target=activeFacility==1?Mat:activeFacility==2?new Point(7.55f,-2.4f):Tunnel;
                    if(Point.Distance(s.pet,target)>6)continue;
                    activeCat=resident.id;anchor=app.household.FindFree(target,resident.id);age=poseAge=0;
                    app.household.SetHumanBusy(activeCat,true);app.household.Brain(activeCat).Request(Activity.Greet,anchor);break;
                }
                return;
            }
            age+=dt;var cat=app.household.Cat(activeCat);var life=cat.life;
            if(age>14||life.activity==Activity.Sleep||life.energy<25){Cancel();return;}
            if(Point.Distance(life.pet,anchor)>.18f)return;
            poseAge+=dt;var view=app.cats[activeCat];
            int skills=app.Progress.Cat(activeCat).skillLevel;float duration=skills>=2?3.4f:1.7f;
            float localAge=poseAge%1.7f;
            if(skills>=2&&poseAge>1.7f){cat.heading=Mathf.Repeat(cat.heading+dt*55,360);view.HouseholdHeading=cat.heading;}
            var phase=localAge<.7f?WandPhase.Ready:localAge<1.18f?WandPhase.Pounce:WandPhase.Recover;
            float progress=phase==WandPhase.Ready?localAge/.7f:phase==WandPhase.Pounce?(localAge-.7f)/.48f:(localAge-1.18f)/.5f;
            view.TickPlay(phase,Mathf.Clamp01(progress),0,view.transform.position+view.transform.forward*.6f);
            if(poseAge>duration){app.household.Brain(activeCat).Record("play-garden","在"+PlayProgress.FacilityName(activeFacility)+"旁，试了试一起学会的扑抓。");Cancel();}
        }
        public void Cancel(){if(activeCat!=0){app.household.SetHumanBusy(activeCat,false);activeCat=0;}cooldown=Mathf.Max(cooldown,12);}
    }
}
