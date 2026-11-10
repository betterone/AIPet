using UnityEngine;
using Momo.Core;

namespace Momo
{
    // World-space hands: right carries food, left follows the cat's actual head contact.
    [DefaultExecutionOrder(100)]
    public sealed class CompanionHands : MonoBehaviour
    {
        Transform right,left;
        CompanionHandView rightView,leftView;
        Camera drawCamera;CatView drawCat;HandFeeding drawFeeding;Vector3 drawBag;bool drawPending;
        readonly GameObject[] pieces=new GameObject[12];
        Material skin;
        Vector2 offset;
        Vector3 previousPalm,previousViewer,strokeOrigin,renderedFood;
        bool foodSampled,sideChosen;
        public bool FoodContactClear {get;private set;}=true;
        public float RemainingPenetration {get;private set;}
        public Vector3 FoodPoint=>foodSampled?renderedFood:Palm+Vector3.up*.04f;
        float strokeAge=10,strokeStrength,offerYaw=-18,approachAngle;
        bool sampled;
        public Vector3 Palm {get;private set;}
        public Point CatTarget {get;private set;}
        public bool Steady {get;private set;}
        public bool Accessible {get;private set;}
        public float StrokeAge=>strokeAge;
        public void Build()
        {
            skin=Art.Mat("Companion skin","#FFFFFF",.12f);skin.mainTexture=Resources.Load<Texture2D>("CompanionHand/Skin");
            skin.SetFloat("_SpecularHighlights",0);skin.SetFloat("_GlossyReflections",0);
            right=Hand(false);left=Hand(true);
            rightView=right.GetComponent<CompanionHandView>();leftView=left.GetComponent<CompanionHandView>();
            var kibble=Art.Mat("Palm kibble","#94613A");
            for(int i=0;i<pieces.Length;i++)
            {
                float a=i*2.4f,r=.012f+.025f*((i%3)/2f);
                pieces[i]=Art.Shape("Palm food grain",PrimitiveType.Sphere,right,new Vector3(Mathf.Cos(a)*r,.025f+(i%2)*.010f,Mathf.Sin(a)*r+.025f),new Vector3(.019f,.014f,.017f),kibble);
            }
            right.gameObject.SetActive(false);left.gameObject.SetActive(false);
        }
        Transform Hand(bool isLeft)
        {
            var h=Art.Pivot(isLeft?"Your left hand":"Your right hand",transform,Vector3.zero);
            h.gameObject.AddComponent<CompanionHandView>().Build(skin,isLeft);
            if(!isLeft)
            {var c=h.gameObject.AddComponent<BoxCollider>();c.size=new Vector3(.28f,.14f,.38f);c.center=new Vector3(0,0,.02f);h.gameObject.AddComponent<TouchTarget>().kind="food-hand";}
            return h;
        }
        public void Drag(Vector2 delta)
        {sideChosen=false;offset+=new Vector2(delta.x/Mathf.Max(1,Screen.width)*1.5f,-delta.y/Mathf.Max(1,Screen.height)*1.7f);offset=new Vector2(Mathf.Clamp(offset.x,-.6f,.4f),Mathf.Clamp(offset.y,-.4f,.5f));}
        public void StartStroke(Camera camera,float strength=1)
        {strokeAge=0;strokeStrength=Mathf.Clamp01(strength);strokeOrigin=camera.ViewportToWorldPoint(new Vector3(-.12f,.05f,.6f));}
        public void ResetPose(){offset=Vector2.zero;sideChosen=false;foodSampled=false;sampled=false;strokeAge=10;if(right!=null){right.gameObject.SetActive(false);left.gameObject.SetActive(false);}}
        public void Locate(Camera camera,float dt,float standOff=.62f)
        {
            Vector3 viewer=camera.transform.position;
            Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            Vector3 sideways=Vector3.Cross(Vector3.up,forward);
            var origin=new Point(viewer.x,viewer.z);
            float reach=Mathf.Clamp(.74f+offset.y,.68f,.78f);
            float safeSide=reach*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*camera.aspect*.62f;
            var delta=forward*reach+sideways*Mathf.Clamp(.045f+offset.x,-safeSide,safeSide);
            Point hand=Room.Move(origin,new Point(delta.x,delta.z));
            Palm=new Vector3(hand.x,.56f,hand.z);
            // Use the reachable, rendered palm, not a target beyond the arm's reach.
            right.position=Palm;right.rotation=Quaternion.LookRotation(forward)*Quaternion.Euler(0,offerYaw,0);rightView.Pose(45,camera);
            Palm=right.position;hand=new Point(Palm.x,Palm.z);
            // The food after last frame's collision solve owns the stand slot.
            // A nominal palm and a resolved palm must never produce different targets.
            Vector3 anchor=foodSampled&&drawFeeding!=null&&drawFeeding.Holding?renderedFood:Palm+right.forward*.018f;
            var food=new Point(anchor.x,anchor.z);
            float best=float.MaxValue;float chosen=approachAngle;
            foreach(float angle in new[]{60f,-60f,35f,-35f,0f})
            {
                var direction=Quaternion.AngleAxis(angle,Vector3.up)*forward;
                var candidate=new Point(food.x+direction.x*standOff,food.z+direction.z*standOff);
                var fitted=CatNavigation.Nearest(candidate);
                float cost=Point.Distance(candidate,fitted)+Mathf.Max(0,1.07f-Point.Distance(origin,fitted))*4+(60-Mathf.Abs(angle))*.0005f;
                if(sideChosen&&Mathf.Abs(angle-approachAngle)>.1f)cost+=.035f;
                if(cost<best){best=cost;chosen=angle;}
            }
            if(viewer.y<1.055f){approachAngle=chosen;offerYaw=approachAngle<0?18:-18;sideChosen=true;}
            var standDirection=Quaternion.AngleAxis(approachAngle,Vector3.up)*forward;
            var desired=new Point(food.x+standDirection.x*standOff,food.z+standDirection.z*standOff);
            var target=CatNavigation.Nearest(desired);
            float correction=Point.Distance(target,desired);
            Accessible=viewer.y<1.18f&&correction<=.18f&&Point.Distance(origin,target)>=1.05f&&Room.Clear(food,target)&&Point.Distance(origin,hand)>.45f&&Point.Distance(origin,hand)<1.85f;
            CatTarget=target;
            float speed=sampled?Vector3.Distance(Palm,previousPalm)/Mathf.Max(dt,.001f):0;
            float viewerSpeed=sampled?Vector3.Distance(viewer,previousViewer)/Mathf.Max(dt,.001f):0;
            Steady=speed<.22f&&viewerSpeed<.15f;
            previousPalm=Palm;previousViewer=viewer;sampled=true;
        }
        public void Render(Camera camera,CatView cat,HandFeeding feeding,Vector3 bag,float dt)
        {
            strokeAge+=dt;drawCamera=camera;drawCat=cat;drawFeeding=feeding;drawBag=bag;drawPending=true;
        }
        void LateUpdate()
        {if(!drawPending&&(drawFeeding==null||!drawFeeding.Active)&&strokeAge>=1.5f)return;drawPending=false;Draw(drawCamera,drawCat,drawFeeding,drawBag);}
        void Draw(Camera camera,CatView cat,HandFeeding feeding,Vector3 bag)
        {
            bool carrying=feeding!=null&&feeding.Active;
            right.gameObject.SetActive(carrying);
            if(carrying)
            {
                Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
                Quaternion facing=Quaternion.LookRotation(forward.sqrMagnitude<.1f?Vector3.forward:forward)*Quaternion.Euler(0,offerYaw,0);
                Vector3 entry=camera.ViewportToWorldPoint(new Vector3(1.12f,-.16f,.55f));
                float age=feeding.PhaseTime;
                Vector3 p=Palm;float curl=45,flip=0;
                if(feeding.Phase==FeedingPhase.Scoop)
                {float t=Mathf.SmoothStep(0,1,age/1.1f);p=Vector3.Lerp(entry,bag+new Vector3(0,.78f,0),Mathf.Sin(t*Mathf.PI*.5f));curl=Mathf.Lerp(0,70,t);flip=170;}
                else if(feeding.Phase==FeedingPhase.TurnPalm)
                {float t=Mathf.SmoothStep(0,1,age/.7f);p=Vector3.Lerp(bag+new Vector3(0,.78f,0),Palm,t);flip=Mathf.Lerp(170,0,t);curl=Mathf.Lerp(70,45,t);}
                else if(feeding.Phase==FeedingPhase.Retract)
                {float t=Mathf.SmoothStep(0,1,age/.55f);p=Vector3.Lerp(Palm,entry,t);curl=35;}
                right.position=p;right.rotation=facing*Quaternion.Euler(0,0,flip);
                rightView.Pose(curl,camera);
                if(!feeding.Holding)Resolve(rightView,cat,camera,curl);
                cat.SeparateFeedingHead(rightView.ContactShapes,feeding.Eating?.065f:.12f);
                Palm=right.position;renderedFood=right.TransformPoint(new Vector3(0,.04f,.018f));foodSampled=true;
                Physics.SyncTransforms();RemainingPenetration=ContactGeometry.Separation(rightView.ContactShapes,cat.ContactSolids,out var unused);FoodContactClear=RemainingPenetration<=.004f;
                for(int i=0;i<pieces.Length;i++)pieces[i].SetActive(feeding.Phase>=FeedingPhase.TurnPalm&&i<(4-feeding.Bites)*3);
            }
            bool stroking=strokeAge<1.5f&&cat!=null;
            left.gameObject.SetActive(stroking);
            if(stroking)
            {
                float t=strokeAge;
                Vector3 head=cat.HeadPosition;
                Vector3 touch=cat.CrownPosition+Vector3.up*.035f-cat.transform.forward*(Mathf.Sin(Mathf.Clamp01((t-.25f)/.9f)*Mathf.PI)*.035f);
                float reach=Mathf.SmoothStep(0,1,t/.3f)*(1-Mathf.SmoothStep(0,1,(t-1.15f)/.35f));
                left.position=Vector3.Lerp(strokeOrigin,touch,reach);
                var elbow=camera.ViewportToWorldPoint(new Vector3(-.22f,.27f,.85f));
                var reachDirection=Vector3.ProjectOnPlane(left.position-elbow,Vector3.up).normalized;
                left.rotation=Quaternion.LookRotation(reachDirection.sqrMagnitude>.01f?reachDirection:cat.transform.forward)*Quaternion.Euler(0,0,180);
                leftView.Pose(10+strokeStrength*6,camera);
                Resolve(leftView,cat,camera,10+strokeStrength*6);
            }
        }
        Vector3 Resolve(CompanionHandView view,CatView cat,Camera camera,float curl)
        {
            var start=view.transform.position;
            for(int i=0;i<5;i++)
            {
                Physics.SyncTransforms();float depth=ContactGeometry.Separation(view.ContactShapes,cat.ContactSolids,out var correction);
                if(depth<=.003f)break;
                view.transform.position+=Vector3.ClampMagnitude(correction,.065f);view.Pose(curl,camera);
            }
            Physics.SyncTransforms();float remaining=ContactGeometry.Separation(view.ContactShapes,cat.ContactSolids,out var unused);
            RemainingPenetration=remaining;
            return view.transform.position-start;
        }
        void OnDestroy(){if(skin!=null)Destroy(skin);}
    }
}
