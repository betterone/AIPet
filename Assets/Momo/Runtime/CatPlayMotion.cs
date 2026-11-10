using System.Collections.Generic;
using UnityEngine;
using Momo.Core;
namespace Momo
{
    // Authored real-time ground chase/crouch/reach/recovery poses. No root
    // motion: the challenge or household alone owns position and heading.
    public sealed class CatPlayMotion
    {
        readonly Transform root,body,head;
        readonly Transform[] bones;
        readonly Vector3[] positions,scales;
        readonly Quaternion[] rotations;
        readonly Transform[] upper=new Transform[4],lower=new Transform[4],end=new Transform[4],paws=new Transform[4];
        readonly Vector3[] neutral=new Vector3[4],ankleOffset=new Vector3[4];
        readonly bool imported;
        float gait;
        public CatPlayMotion(Transform root,bool imported)
        {
            this.root=root;this.imported=imported;
            var map=new Dictionary<string,Transform>();foreach(var t in root.GetComponentsInChildren<Transform>())if(t!=root)map[t.name]=t;
            bones=new List<Transform>(map.Values).ToArray();positions=new Vector3[bones.Length];rotations=new Quaternion[bones.Length];scales=new Vector3[bones.Length];
            for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;scales[i]=bones[i].localScale;}
            map.TryGetValue(imported?"Pelvis":"Body",out body);map.TryGetValue(imported?"Head":"Head",out head);
            for(int i=0;i<4;i++)
            {
                string prefix=i<2?"Fore":"Hind",side=i%2==0?".L":".R";
                if(imported){map.TryGetValue(prefix+"Upper"+side,out upper[i]);map.TryGetValue(prefix+"Lower"+side,out lower[i]);map.TryGetValue(prefix+"Paw"+side,out paws[i]);end[i]=paws[i];if(i>=2)map.TryGetValue("HindAnkle"+side,out end[i]);}
                else{map.TryGetValue("Leg "+i,out upper[i]);paws[i]=upper[i];end[i]=upper[i];}
                if(paws[i]!=null){neutral[i]=root.InverseTransformPoint(paws[i].position);if(end[i]!=null)ankleOffset[i]=root.InverseTransformVector(end[i].position-paws[i].position);}
            }
        }
        public void Restore()
        {for(int i=0;i<bones.Length;i++){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];bones[i].localScale=scales[i];}}
        public void Apply(WandPhase phase,float progress,float distance,Vector3 attention)
        {
            Restore();
            gait+=distance/.66f;
            float crouch=phase==WandPhase.Ready?1:phase==WandPhase.Pounce?1-progress:phase==WandPhase.Recover?(1-progress)*.4f:0;
            float spring=phase==WandPhase.Pounce?Mathf.Sin(progress*Mathf.PI):0;
            if(body!=null){body.position+=Vector3.down*(.10f*crouch)+Vector3.up*(.085f*spring);body.rotation=Quaternion.AngleAxis(-7*spring,root.right)*body.rotation;}
            for(int i=0;i<4;i++)
            {
                if(upper[i]==null)continue;
                float cycle=Mathf.Repeat(gait+(i==0||i==3?0:.5f),1),swing=cycle>.62f?(cycle-.62f)/.38f:0;
                Vector3 goal=root.TransformPoint(neutral[i]);
                if(phase==WandPhase.Chase&&distance>.00001f)
                {goal+=root.forward*((cycle<.62f?.5f-cycle/.62f:-.5f+swing)*.23f)+Vector3.up*(Mathf.Sin(swing*Mathf.PI)*.07f);}
                else if(phase==WandPhase.Pounce)
                {goal+=root.forward*((i<2?.18f:-.06f)*spring)+Vector3.up*((i<2?.09f:.06f)*spring);}
                if(!imported){upper[i].position=goal;upper[i].rotation=Quaternion.AngleAxis((phase==WandPhase.Pounce?spring:0)*(i<2?-32:25),root.right)*upper[i].rotation;continue;}
                if(lower[i]==null||end[i]==null)continue;
                var rotation=paws[i].rotation;Solve(upper[i],lower[i],end[i],goal+root.TransformVector(ankleOffset[i]),root.forward*(i<2?-1:1));paws[i].rotation=rotation;
            }
            if(head!=null)
            {
                Vector3 d=root.InverseTransformDirection(attention-head.position).normalized;
                float yaw=Mathf.Clamp(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-30,30);
                head.rotation=Quaternion.AngleAxis(yaw,root.up)*Quaternion.AngleAxis(crouch*8,root.right)*head.rotation;
            }
        }
        static void Solve(Transform upper,Transform lower,Transform end,Vector3 goal,Vector3 pole)
        {
            Vector3 a=upper.position,b=lower.position,c=end.position,delta=goal-a;
            float l1=Vector3.Distance(a,b),l2=Vector3.Distance(b,c);if(delta.sqrMagnitude<.000001f||l1<.001f||l2<.001f)return;
            float reach=Mathf.Clamp(delta.magnitude,Mathf.Abs(l1-l2)+.0001f,l1+l2-.0001f);
            Vector3 axis=delta.normalized,bend=Vector3.ProjectOnPlane(pole,axis).normalized;
            float along=(l1*l1+reach*reach-l2*l2)/(2*reach);
            Vector3 joint=a+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
            upper.rotation=Quaternion.FromToRotation(b-a,joint-a)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,goal-lower.position)*lower.rotation;
        }
    }
}
