using System;
using System.Collections.Generic;
using UnityEngine;

namespace Momo
{
    // Positive-scale left/right skins. Bone +Z follows the phalanx, +Y is palmar.
    // Fixed-length wrist -> elbow -> shoulder IK; no skin stretching or reflected joints.
    public sealed class CompanionHandView : MonoBehaviour
    {
        Transform indexEnd;
        Transform forearm,upper,end,shoulder,thumb,index,handRoot,twistMid,twistWrist,body;
        Quaternion handFrame;
        Vector3 wristOffset;
        readonly List<Transform> joints=new List<Transform>();
        readonly List<Quaternion> rests=new List<Quaternion>();
        readonly List<int> segments=new List<int>();
        readonly List<bool> thumbs=new List<bool>();
        float lowerLength,upperLength;
        bool left,modelLeft;
        public readonly List<Collider> ContactShapes=new List<Collider>();
        public string ModelKey {get;private set;}
        public float MaximumLengthError {get;private set;}
        public float MaximumWristAngle {get;private set;}
        public bool AnatomyValid {get;private set;}
        public Vector3 ThumbLocal=>transform.InverseTransformPoint(thumb.position);
        public Vector3 IndexLocal=>transform.InverseTransformPoint(index.position);
        public void Build(Material skin,bool isLeft)
        {
            left=isLeft;
            // User-confirmed visual binding: swap the two source skins.
            // Entry side follows the action; thumb opposition follows the loaded skin.
            modelLeft=!isLeft;ModelKey=modelLeft?"Left":"Right";
            var resource=Resources.Load<GameObject>("CompanionHand/"+ModelKey);
            if(resource==null)throw new Exception("Anatomical companion arm prefab is missing");
            var model=Instantiate(resource,transform,false);model.name=(left?"Left action / ":"Right action / ")+ModelKey+" skin";
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {r.sharedMaterial=skin;r.updateWhenOffscreen=true;r.quality=SkinQuality.Bone4;r.localBounds=new Bounds(Vector3.zero,Vector3.one*4);}
            foreach(var b in model.GetComponentsInChildren<Transform>())
            {
                if((b.localScale-Vector3.one).sqrMagnitude>.00001f)throw new Exception("Reflected/scaled hand joint: "+b.name);
                switch(b.name){case "forearm.R":forearm=b;break;case "upper_arm.R":upper=b;break;case "ShoulderEnd":shoulder=b;break;case "WristAnchor":end=b;break;case "HandRoot":handRoot=b;break;case "ForearmTwistMid":twistMid=b;break;case "ForearmTwistWrist":twistWrist=b;break;case "ShoulderRoot":body=b;break;case "thumb.02.R":thumb=b;break;case "f_index.01.R":index=b;break;case "f_index.03.R":indexEnd=b;break;}
                if(!b.name.StartsWith("f_")&&!b.name.StartsWith("thumb."))continue;
                joints.Add(b);rests.Add(b.localRotation);thumbs.Add(b.name.StartsWith("thumb."));
                segments.Add(b.name.Contains(".01.")?0:b.name.Contains(".02.")?1:2);
            }
            if(forearm==null||upper==null||end==null||shoulder==null||handRoot==null||twistMid==null||twistWrist==null||body==null||joints.Count!=15)throw new Exception("Arm deform rig is incomplete");
            handFrame=Quaternion.Inverse(transform.rotation)*handRoot.rotation;wristOffset=transform.InverseTransformPoint(end.position);
            lowerLength=Vector3.Distance(forearm.position,end.position);upperLength=Vector3.Distance(upper.position,forearm.position);
            AnatomyValid=modelLeft?ThumbLocal.x>IndexLocal.x:ThumbLocal.x<IndexLocal.x;
            if(!AnatomyValid)throw new Exception("Thumb is on the wrong side of the palm");
            BuildContactShapes();
            Debug.Log("HAND BINDING role="+(left?"left":"right")+" skin="+ModelKey+" thumb="+ThumbLocal+" index="+IndexLocal+" lower="+lowerLength+" upper="+upperLength);
        }
        void BuildContactShapes()
        {
            var palm=new GameObject("Palm solid");palm.layer=2;palm.transform.SetParent(handRoot,false);
            // HandRoot is the wrist, 0.115 units behind the palm origin.
            var box=palm.AddComponent<BoxCollider>();box.center=new Vector3(0,-.003f,.095f);box.size=new Vector3(.125f,.035f,.15f);box.isTrigger=true;ContactShapes.Add(box);
            for(int i=0;i<joints.Count;i++)
            {
                var bone=joints[i];float length=segments[i]==2?.035f:.045f;
                foreach(Transform child in bone)if(child.name.StartsWith("f_")||child.name.StartsWith("thumb.")){length=Vector3.Distance(child.position,bone.position);break;}
                var obj=new GameObject("Finger solid");obj.layer=2;obj.transform.SetParent(bone,false);
                var c=obj.AddComponent<CapsuleCollider>();c.direction=2;c.radius=thumbs[i]?.013f:.010f;c.height=length+c.radius*2;c.center=Vector3.forward*(length*.5f);c.isTrigger=true;ContactShapes.Add(c);
            }
            var sleeve=new GameObject("Forearm solid");sleeve.layer=2;sleeve.transform.SetParent(forearm,false);
            var arm=sleeve.AddComponent<CapsuleCollider>();arm.direction=2;arm.radius=.038f;arm.height=lowerLength;arm.center=Vector3.forward*(lowerLength*.5f);arm.isTrigger=true;ContactShapes.Add(arm);
        }
        public Vector3 IndexPad=>indexEnd!=null?indexEnd.TransformPoint(new Vector3(0,0,.020f)):index.position;
        public void PosePump(Camera camera)
        {
            Pose(48,camera);
            for(int i=0;i<joints.Count;i++)if(joints[i].name.StartsWith("f_index"))joints[i].localRotation=rests[i]*Quaternion.AngleAxis(-8,Vector3.right);
        }
        public void Pose(float curl,Camera camera,bool calibration=false)
        {
            // A first-person shoulder stays beside the viewer's body, rather than
            // chasing the wrist in screen space. Keep the palm target reachable.
            var horizontal=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            if(horizontal.sqrMagnitude<.01f)horizontal=Vector3.forward;
            var sideways=Vector3.Cross(Vector3.up,horizontal);
            Vector3 owner=camera.transform.position+sideways*(left?-.18f:.18f)-Vector3.up*.22f-horizontal*.06f;
            if(calibration)owner=transform.position+sideways*(left?-.18f:.18f)-Vector3.up*.12f-horizontal*.68f;
            Vector3 wrist=transform.TransformPoint(wristOffset),elbow=Vector3.zero;
            Vector3 wantedForward=transform.forward,palmNormal=transform.up;
            for(int iteration=0;iteration<4;iteration++)
            {
                wrist=transform.TransformPoint(wristOffset);
                var delta=wrist-owner;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upperLength-lowerLength)+.02f,upperLength+lowerLength-.012f);
                if(delta.magnitude>distance+.0001f){transform.position+=owner+delta.normalized*distance-wrist;wrist=transform.TransformPoint(wristOffset);}
                var axis=(wrist-owner).normalized;
                var pole=Vector3.ProjectOnPlane(-Vector3.up+sideways*(left?-.35f:.35f)-horizontal*.15f,axis).normalized;
                float along=(upperLength*upperLength-lowerLength*lowerLength+distance*distance)/(2*distance);
                elbow=owner+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,upperLength*upperLength-along*along));
                var foreDirection=(wrist-elbow).normalized;
                var handForward=Vector3.RotateTowards(foreDirection,wantedForward,18*Mathf.Deg2Rad,0);
                transform.rotation=Quaternion.LookRotation(handForward,palmNormal);
            }
            // Re-solve once after the final wrist orientation adjustment.
            wrist=transform.TransformPoint(wristOffset);var reach=wrist-owner;float length=reach.magnitude;var reachAxis=reach.normalized;
            var bend=Vector3.ProjectOnPlane(-Vector3.up+sideways*(left?-.35f:.35f)-horizontal*.15f,reachAxis).normalized;
            float projection=(upperLength*upperLength-lowerLength*lowerLength+length*length)/(2*Mathf.Max(.001f,length));
            elbow=owner+reachAxis*projection+bend*Mathf.Sqrt(Mathf.Max(0,upperLength*upperLength-projection*projection));
            var fore=(wrist-elbow).normalized;
            var neutral=Vector3.ProjectOnPlane(sideways*(left?1:-1),fore).normalized;
            if(neutral.sqrMagnitude<.01f)neutral=Vector3.ProjectOnPlane(Vector3.up,fore).normalized;
            var desired=Vector3.ProjectOnPlane(transform.up,fore).normalized;
            float roll=Vector3.SignedAngle(neutral,desired,fore);
            body.position=owner;body.rotation=Quaternion.identity;
            upper.position=owner;upper.rotation=Quaternion.LookRotation(elbow-owner,neutral);
            forearm.position=elbow;forearm.rotation=Quaternion.AngleAxis(roll*.25f,fore)*Quaternion.LookRotation(fore,neutral);
            twistMid.position=Vector3.Lerp(elbow,wrist,.35f);twistMid.rotation=Quaternion.AngleAxis(roll*.65f,fore)*Quaternion.LookRotation(fore,neutral);
            twistWrist.position=Vector3.Lerp(elbow,wrist,.8f);twistWrist.rotation=Quaternion.LookRotation(fore,desired);
            handRoot.position=wrist;handRoot.rotation=transform.rotation*handFrame;
            MaximumWristAngle=Mathf.Max(MaximumWristAngle,Vector3.Angle(transform.forward,fore));
            MaximumLengthError=Mathf.Max(MaximumLengthError,Mathf.Abs(Vector3.Distance(forearm.position,end.position)-lowerLength),Mathf.Abs(Vector3.Distance(upper.position,forearm.position)-upperLength));
            for(int i=0;i<joints.Count;i++)joints[i].localRotation=rests[i];
            for(int i=0;i<joints.Count;i++)
            {
                var joint=joints[i];int segment=segments[i];
                float angle=thumbs[i]?(segment==0?4:segment==1?8:5)*Mathf.Clamp01(curl/45):curl*(segment==0?.32f:segment==1?.55f:.35f);
                joint.localRotation=rests[i]*Quaternion.AngleAxis(-angle,Vector3.right);
                if(thumbs[i]&&segment==0)
                    joint.rotation=Quaternion.AngleAxis((modelLeft?-1:1)*(!left&&curl<60?-18:Mathf.Lerp(5,18,Mathf.Clamp01(curl/70))),transform.up)*joint.rotation;
            }
        }
    }
}
