using UnityEngine;
using Momo.Core;

namespace Momo
{
    public static class BathStation
    {
        public static void Build(Transform parent)
        {
            var root=Art.Pivot("Garden bathing corner",parent,new Vector3(BathSession.Station.x,0,BathSession.Station.z));
            var cream=Art.Mat("Bath milk ceramic","#F8EEDD",.4f);var mint=Art.Mat("Bath lake green","#76ACA7",.4f);
            var bottom=Art.Mat("Bath nonslip floor","#D1DED2",.2f);var wood=Art.Mat("Bath towel tray","#DDB584");
            // Paws remain on the ground. The surface above them is water, never a walking collider.
            Art.Cube("Grounded bath floor",root,new Vector3(0,-.024f,0),new Vector3(2.4f,.05f,2.8f),cream);
            Art.Cube("Nonslip basin bottom",root,new Vector3(0,.002f,0),new Vector3(2.17f,.003f,2.5f),bottom);
            var grips=Art.Pivot("Batched nonslip grips",root,Vector3.zero);
            for(int x=-6;x<=6;x++)for(int z=-7;z<=7;z++)
                Art.Cube("Nonslip floor grip",grips,new Vector3(x*.15f,.005f,z*.15f),new Vector3(.018f,.004f,.055f),mint);
            StaticBatchingUtility.Combine(grips.gameObject);
            foreach(float x in new[]{-1.15f,1.15f})
            {
                Art.Cube("Basin side wall",root,new Vector3(x,.32f,0),new Vector3(.1f,.64f,2.8f),cream,true);
                Art.Cube("Rounded mint handrail",root,new Vector3(x,.66f,0),new Vector3(.12f,.05f,2.8f),mint);
            }
            Art.Cube("Basin north wall",root,new Vector3(0,.32f,1.35f),new Vector3(2.4f,.64f,.1f),cream,true);
            Art.Cube("North mint handrail",root,new Vector3(0,.66f,1.35f),new Vector3(2.4f,.05f,.12f),mint);
            var gate=Art.Shape("Lowering transparent entry gate",PrimitiveType.Cube,root,new Vector3(0,.004f,-1.3f),new Vector3(2.18f,.008f,.06f),mint).transform;
            Art.Cube("Bath entry mat",root,new Vector3(0,.014f,-1.6f),new Vector3(1.4f,.025f,.45f),wood);
            Art.Cylinder("Bath drain",root,new Vector3(.7f,.006f,.85f),new Vector3(.16f,.003f,.16f),Art.Mat("Drain brushed metal","#78928D",.5f));
            var pad=root.gameObject.AddComponent<BoxCollider>();pad.center=new Vector3(0,-.015f,0);pad.size=new Vector3(2.4f,.04f,2.8f);
            root.gameObject.AddComponent<TouchTarget>().kind="bath";
            foreach(var wall in root.GetComponentsInChildren<Collider>())if(wall.name.Contains("wall"))wall.gameObject.AddComponent<TouchTarget>().kind="bath";
            var supplies=Art.Pivot("Reachable bath supplies",root,new Vector3(1.45f,0,-1.05f));
            supplies.gameObject.AddComponent<BathToolRack>().Build();
            var supplyTarget=supplies.gameObject.AddComponent<BoxCollider>();supplyTarget.center=new Vector3(0,.88f,0);supplyTarget.size=new Vector3(.58f,.34f,.73f);supplies.gameObject.AddComponent<TouchTarget>().kind="bath";
            root.gameObject.AddComponent<BathWaterView>().Build(gate);
        }
    }

    [DefaultExecutionOrder(180)]
    public sealed class BathController : MonoBehaviour
    {
        public enum HandlingStep { None, Return, Withdraw, Reach, Pump, Pass, Carry }
        MomoApp app;
        CompanionHandView right,left;
        Transform marker;
        Material skin,markerMaterial;
        Vector2 pointer,previousPointer;
        Vector3 previousContact,contactPoint,contactNormal,anchor,normal,grip,velocity;
        Vector3 motionStart,leftMotionStart;bool leftWasActive;Quaternion leftMotionRotation;Quaternion motionRotation,toolRotationStart,toolRelative;
        bool held,sampled,screenMoved,framed,toolInHand,soapLoaded;
        BathSession lastSession,visualSession;
        BathPhase observedPhase;
        BathTool equipped,wanted;
        float handlingTime,workingClock,rubSplash;
        public BathWaterView Basin {get;private set;}
        public BathToolRack Rack {get;private set;}
        public BathSpray Spray {get;private set;}
        public HandlingStep HandlingPhase {get;private set;}
        public bool Handling=>HandlingPhase!=HandlingStep.None;
        public int Pickups {get;private set;}
        public int Returns {get;private set;}
        public float MaximumPickupGap {get;private set;}
        public BathSession Session=>app?.household?.Bath;
        public bool Active=>Session!=null&&Session.Active;
        public bool ContactValid {get;private set;}
        public float ContactPenetration {get;private set;}
        public bool ToolReachValid {get;private set;}
        public float ToolGap {get;private set;}
        public Vector3 ContactPoint=>contactPoint;
        public string HandlingHint=>HandlingPhase==HandlingStep.Return?"先把工具放回原处。":HandlingPhase==HandlingStep.Withdraw?"把手收回来。":HandlingPhase==HandlingStep.Reach?"伸手拿起工具。":HandlingPhase==HandlingStep.Pump?"按一下泵，取少量沐浴露。":HandlingPhase==HandlingStep.Pass?"接好湿润的海绵。":HandlingPhase==HandlingStep.Carry?"慢慢靠近它，再开始照料。":"";
        BathZone toolZone;int toolPatch;
        static float Ease(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        static readonly Vector3 Mount=new Vector3(0,.085f,.1f);
        public void Build(MomoApp owner)
        {
            app=owner;Basin=app.room.GetComponentInChildren<BathWaterView>();Rack=app.room.GetComponentInChildren<BathToolRack>();
            skin=Art.Mat("Bath companion hands","#FFFFFF",.12f);skin.mainTexture=Resources.Load<Texture2D>("CompanionHand/Skin");skin.SetFloat("_SpecularHighlights",0);skin.SetFloat("_GlossyReflections",0);
            right=Hand(false);left=Hand(true);
            Spray=new GameObject("Physical gentle shower").AddComponent<BathSpray>();Spray.transform.SetParent(transform,false);Spray.Build();
            markerMaterial=Art.Mat("Bath contact guide","#C4DD9A",.1f);marker=Art.Shape("Bath target sparkle",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.035f,markerMaterial).transform;HideHands();
        }
        CompanionHandView Hand(bool isLeft){var g=new GameObject(isLeft?"Bath left hand":"Bath right hand");g.transform.SetParent(transform,false);var h=g.AddComponent<CompanionHandView>();h.Build(skin,isLeft);return h;}
        public void HandlePointer(Vector2 position,bool down,bool holding,bool up,bool overUi)
        {
            if(down){sampled=false;previousPointer=position;}
            screenMoved=Vector2.Distance(previousPointer,position)>.3f;previousPointer=position;pointer=position;held=holding&&!up&&!overUi;if(!held)sampled=false;
        }
        public void ReleasePointer(){held=sampled=screenMoved=false;ContactValid=false;}
        public void Cancel(bool quiet=false)
        {
            Session?.Cancel();ReleasePointer();Basin?.Clear();ResetHandling();lastSession=visualSession=null;
            if(framed){app.window.EndCareView();framed=false;}
            if(app.cat!=null){app.cat.SetBathPose(false,false);app.cat.GetComponent<CatBathEffects>()?.ClearCare(app.simulation.State);}
            if(!quiet)app.Notify("这次先到这里，让它自在走一走。");
        }
        void ResetHandling(){HandlingPhase=HandlingStep.None;toolInHand=soapLoaded=false;observedPhase=BathPhase.None;HideHands();Rack?.ResetTools();Spray?.Clear();}
        void BeginHandling(HandlingStep step)
        {HandlingPhase=step;handlingTime=0;motionStart=right.transform.position;motionRotation=right.transform.rotation;toolRotationStart=Rack.Tool(equipped).rotation;toolRelative=Quaternion.Inverse(motionRotation)*toolRotationStart;leftMotionStart=left.transform.position;leftMotionRotation=left.transform.rotation;leftWasActive=left.gameObject.activeSelf;ReleasePointer();}
        void StartReach(){if(!right.gameObject.activeSelf){var camera=app.room.cameraView;right.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward*.25f-camera.transform.right*.18f-Vector3.up*.38f,Quaternion.LookRotation(camera.transform.forward,Vector3.down));}equipped=wanted;soapLoaded=equipped!=BathTool.Soap;BeginHandling(HandlingStep.Reach);}
        public void Tick(float dt)
        {
            dt=Mathf.Clamp(dt,0,.1f);workingClock+=dt;rubSplash-=dt;var b=Session;var effect=app.cat!=null?app.cat.GetComponent<CatBathEffects>():null;
            if(b==null||!b.Active)
            {
                Basin?.Tick(dt,b,effect,false,Vector3.zero,0);
                if(lastSession!=null){if(framed){app.window.EndCareView();framed=false;}app.cat.SetBathPose(false,false);ResetHandling();lastSession=visualSession=null;app.Notify(b?.Hint??"");app.Save();}
                return;
            }
            if(b!=visualSession){ResetHandling();visualSession=b;}
            lastSession=b;
            if(!app.window.CanInteract||app.hud.PanelOpen){Cancel(true);return;}
            if(!framed){app.window.BeginCareView();framed=true;app.hud.Walk?.Clear();}
            if(b.Washing&&b.Phase!=observedPhase)
            {observedPhase=b.Phase;wanted=b.Tool;if(HandlingPhase!=HandlingStep.Return){if(toolInHand)BeginHandling(HandlingStep.Return);else StartReach();}}
            if((b.Ready||b.Phase==BathPhase.Complete)&&toolInHand&&!Handling)BeginHandling(HandlingStep.Return);
            FrameCamera(b,effect,dt);
            BathContact input=default;ContactValid=false;
            if(Handling)
            {
                handlingTime+=dt;RenderHandling();AdvanceHandling(b);
            }
            else if(b.Washing&&!b.Ready)
            {
                if(held&&!b.Paused)
                {
                    Physics.SyncTransforms();
                    if(effect.Sample(app.room.cameraView.ScreenPointToRay(pointer),out input,out contactPoint,out contactNormal))
                    {
                        input.tool=b.Tool;input.speed=sampled&&screenMoved?Vector3.Distance(contactPoint,previousContact)/Mathf.Max(.001f,dt):0;
                        previousContact=contactPoint;sampled=true;ContactValid=!input.head;
                    }
                    else
                    {
                        var ray=app.room.cameraView.ScreenPointToRay(pointer);var plane=new Plane(Vector3.up,new Vector3(0,Mathf.Max(.02f,Basin.WorldHeight),0));
                        contactPoint=plane.Raycast(ray,out float enter)&&enter<3?ray.GetPoint(enter):ray.GetPoint(.9f);contactNormal=Vector3.up;sampled=false;
                    }
                }
                else if(held&&b.Paused)input.touching=true;
                RenderWorking(effect,1);
            }
            else HideHands();
            bool flowing=!Handling&&toolInHand&&equipped==BathTool.Shower&&b.Washing&&!b.Ready&&!b.Paused&&held;
            if(equipped==BathTool.Shower&&toolInHand&&!Handling)
            {velocity=BathSpray.AimVelocity(Rack.Outlet,anchor);Rack.AimOutlet(velocity);velocity=-Rack.Face.up*velocity.magnitude;}
            Spray.Tick(dt,Rack.Outlet,velocity,flowing,effect,Basin);
            if(input.touching&&!input.head&&!Handling)
            {
                if(!ToolReachValid)input.touching=false;
                else if(b.Tool==BathTool.Shower)
                {
                    if(!Spray.BodyHit)input.touching=false;
                    else if(effect.Sample(new Ray(Spray.HitPoint+Spray.HitNormal*.025f,-Spray.HitNormal),out var impact,out var point,out var hitNormal)&&!impact.head){input.zone=impact.zone;input.patch=impact.patch;contactPoint=point;}
                    else input.touching=false;
                }
                else if(!ToolReachValid||(b.Tool==BathTool.Soap&&!soapLoaded))input.touching=false;
                else{input.zone=toolZone;input.patch=toolPatch;}
            }
            if(flowing&&Spray.HeadHit){input.touching=true;input.head=true;input.tool=BathTool.Shower;}
            if(Handling)input=default;
            b.Update(dt,input);
            bool contact=input.touching&&!input.head&&(b.Tool==BathTool.Shower||input.speed>=.02f);
            Basin.Tick(dt,b,effect,false,Vector3.zero,0);
            if(contact&&b.Tool!=BathTool.Shower&&rubSplash<=0){Basin.Disturb(contactPoint,.22f);rubSplash=.35f;}
            effect.TickBathVisual(dt,b,Basin,contact,contactPoint);
            Rack.Cloth.Tick(dt,effect.Body,contact&&b.Tool==BathTool.Towel,b.Phase==BathPhase.Dry?b.Progress*.7f:0);
            var sponge=Rack.Sponge;sponge.localScale=Vector3.Lerp(sponge.localScale,new Vector3(1,contact&&b.Tool==BathTool.Soap?.75f:1,1),Mathf.Clamp01(dt*12));
            app.cat.SetBathPose(b.Washing,b.Paused||input.head||input.speed>1.4f);
            if(contact)app.cat.LookAt(contactPoint,.35f);
            Rack.UpdateHose(toolInHand&&equipped==BathTool.Shower?app.room.cameraView:null,effect.Body);
            if(b!=previousSession){previousSession=b;savedCompletion=false;}if(b.Committed&&b.Phase==BathPhase.Complete&&!savedCompletion){savedCompletion=true;app.Save();}
            screenMoved=false;
        }
        BathSession previousSession;bool savedCompletion;
        void FrameCamera(BathSession b,CatBathEffects effect,float dt)
        {
            if(!b.Washing&&b.Phase!=BathPhase.Complete){app.window.FrameCare(new Vector3(0,1.55f,BathSession.Station.z-3.4f),new Vector3(0,.4f,BathSession.Station.z),dt);return;}
            bool supply=Handling&&HandlingPhase!=HandlingStep.Carry;
            if(supply){app.window.FrameCare(Rack.CameraPosition,Rack.CameraFocus,dt);return;}
            if(held&&!Handling)return;
            var focus=b.Ready||b.Phase==BathPhase.Complete?new Vector3(0,.42f,BathSession.Station.z):effect.Anchor(b.TargetZone,b.TargetPatch);
            var offset=b.TargetZone==BathZone.Back?new Vector3(0,.65f,-.65f):new Vector3(b.TargetZone==BathZone.Left?-.64f:.64f,.38f,-.12f);
            var pos=b.Ready||b.Phase==BathPhase.Complete?new Vector3(1.65f,1.6f,BathSession.Station.z-2.2f):focus+offset;
            if(HandlingPhase==HandlingStep.Carry){float t=Ease(handlingTime/.9f);pos=Vector3.Lerp(Rack.CameraPosition,pos,t);focus=Vector3.Lerp(Rack.CameraFocus,focus,t);}
            app.window.FrameCare(pos,focus,dt);
        }
        void AdvanceHandling(BathSession b)
        {
            switch(HandlingPhase)
            {
                case HandlingStep.Return:
                    if(handlingTime<.85f)return;
                    if(Vector3.Distance(grip,Rack.Dock(equipped))>.045f&&handlingTime<2.5f)return;
                    Rack.DockTool(equipped);Returns++;toolInHand=false;BeginHandling(HandlingStep.Withdraw);break;
                case HandlingStep.Withdraw:
                    if(handlingTime>=.45f){HandlingPhase=HandlingStep.None;if(b.Washing&&!b.Ready)StartReach();else HideHands();}break;
                case HandlingStep.Reach:
                    if(handlingTime<1.0f||Vector3.Distance(grip,Rack.Dock(equipped))>.035f)return;
                    MaximumPickupGap=Mathf.Max(MaximumPickupGap,Vector3.Distance(grip,Rack.Dock(equipped)));Pickups++;toolInHand=true;
                    BeginHandling(equipped==BathTool.Soap?HandlingStep.Pump:HandlingStep.Carry);break;
                case HandlingStep.Pump:
                    if(Rack.PumpStrokes>pumpBefore)soapLoaded=true;
                    if(handlingTime>=1.2f&&soapLoaded){Rack.PressPump(0,Vector3.zero,false);BeginHandling(HandlingStep.Pass);}break;
                case HandlingStep.Pass:
                    if(handlingTime>.5f&&Vector3.Distance(grip,Rack.Sponge.position)<.035f)BeginHandling(HandlingStep.Carry);break;
                case HandlingStep.Carry:
                    if(handlingTime>=1.1f){HandlingPhase=HandlingStep.None;ReleasePointer();}break;
            }
        }
        int pumpBefore;
        void RenderHandling()
        {
            var camera=app.room.cameraView;right.gameObject.SetActive(true);marker.gameObject.SetActive(false);left.gameObject.SetActive(false);
            float t=Ease(handlingTime/.85f);var forward=Vector3.ProjectOnPlane(Rack.CameraFocus-camera.transform.position,Vector3.up).normalized;
            var rotation=Quaternion.LookRotation(forward,Vector3.down);
            if(HandlingPhase==HandlingStep.Withdraw)
            {
                var rest=camera.transform.position+camera.transform.forward*.22f+camera.transform.right*.20f-Vector3.up*.56f;
                right.transform.SetPositionAndRotation(Vector3.Lerp(motionStart,rest,Ease(handlingTime/.45f)),Quaternion.Slerp(motionRotation,rotation,Ease(handlingTime/.45f)));right.Pose(12,camera);
            }
            else if(HandlingPhase==HandlingStep.Return||HandlingPhase==HandlingStep.Reach)
            {
                if(HandlingPhase==HandlingStep.Return)rotation=Rack.DockRotation(equipped)*Quaternion.Inverse(toolRelative);
                var dock=Rack.Dock(equipped);var target=dock+Vector3.up*.085f-forward*.1f;
                right.transform.SetPositionAndRotation(Vector3.Lerp(motionStart,target,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.045f,Quaternion.Slerp(motionRotation,rotation,t));
                right.Pose(HandlingPhase==HandlingStep.Reach?Mathf.Lerp(8,55,t):55,camera);
                if(t>.8f)SolveMount(right,dock,rotation,55,camera);
                grip=right.transform.TransformPoint(Mount);
                if(toolInHand){Rack.Tool(equipped).SetPositionAndRotation(grip,right.transform.rotation*toolRelative);}
                if(HandlingPhase==HandlingStep.Reach&&equipped==BathTool.Soap)pumpBefore=Rack.PumpStrokes;
            }
            else if(HandlingPhase==HandlingStep.Pump||HandlingPhase==HandlingStep.Pass)
            {
                left.gameObject.SetActive(true);var receiver=Rack.SpoutPosition-Vector3.up*.074f;
                SolveMount(left,receiver,rotation,36,camera);var sponge=left.transform.TransformPoint(Mount);Rack.Sponge.SetPositionAndRotation(sponge,Quaternion.identity);
                if(HandlingPhase==HandlingStep.Pump)
                {
                    float press=Mathf.Sin(Mathf.PI*Mathf.Clamp01((handlingTime-.25f)/.70f));
                    var target=Rack.PumpPosition-Vector3.up*(press*.022f-.003f);
                    right.transform.SetPositionAndRotation(target-forward*.14f+Vector3.up*.06f,rotation);
                    for(int i=0;i<5;i++){right.PosePump(camera);right.transform.position+=target-right.IndexPad;}
                    right.PosePump(camera);grip=right.IndexPad;
                    bool aligned=Vector3.Distance(grip,target)<.020f&&Vector3.Distance(sponge,receiver)<.035f;
                    Rack.PressPump(aligned?press:0,sponge+Vector3.up*.03f,aligned);
                }
                else
                {
                    var target=receiver+Vector3.up*.085f-forward*.1f;right.transform.SetPositionAndRotation(Vector3.Lerp(motionStart,target,t),Quaternion.Slerp(motionRotation,rotation,t));right.Pose(45,camera);
                    if(t>.65f)SolveMount(right,receiver,rotation,45,camera);grip=right.transform.TransformPoint(Mount);
                }
            }
            else if(HandlingPhase==HandlingStep.Carry)
            {
                var effect=app.cat.GetComponent<CatBathEffects>();RenderWorking(effect,Ease(handlingTime/.9f));
            }
        }
        void SolveMount(CompanionHandView hand,Vector3 point,Quaternion rotation,float curl,Camera camera)
        {
            hand.transform.SetPositionAndRotation(point+Vector3.up*.085f-rotation*Vector3.forward*.1f,rotation);
            for(int i=0;i<4;i++){hand.Pose(curl,camera);hand.transform.position+=point-hand.transform.TransformPoint(Mount);}
            hand.Pose(curl,camera);
        }
        void LateUpdate()
        {
            if(!Active)return;if(Handling)RenderHandling();else if(Session.Washing&&!Session.Ready)RenderWorking(app.cat.GetComponent<CatBathEffects>(),1);else HideHands();
            if(toolInHand&&equipped==BathTool.Shower&&!Handling){velocity=BathSpray.AimVelocity(Rack.Outlet,anchor);Rack.AimOutlet(velocity);velocity=-Rack.Face.up*velocity.magnitude;Spray.Render(Rack.Outlet,velocity);}
            Rack.UpdateHose(toolInHand&&equipped==BathTool.Shower?app.room.cameraView:null,app.cat.GetComponent<CatBathEffects>()?.Body);
        }
        void RenderWorking(CatBathEffects effect,float amount)
        {
            var b=Session;if(b==null||!b.Washing)return;var camera=app.room.cameraView;ContactPenetration=0;
            anchor=held?contactPoint:effect.Anchor(b.TargetZone,b.TargetPatch);normal=held?contactNormal:b.TargetZone==BathZone.Back?Vector3.up:b.TargetZone==BathZone.Left?Vector3.left:Vector3.right;
            if(normal.sqrMagnitude<.1f)normal=Vector3.up;
            var tangent=Vector3.ProjectOnPlane(camera.transform.forward,normal).normalized;if(tangent.sqrMagnitude<.01f)tangent=Vector3.forward;
            right.gameObject.SetActive(true);float curl=b.Tool==BathTool.Shower?68:28;
            var side=Vector3.ProjectOnPlane(camera.transform.right,normal).normalized*(b.Tool==BathTool.Shower?.09f:0);
            var target=anchor+side+normal*(b.Tool==BathTool.Shower?.285f:.120f)-tangent*(b.Tool==BathTool.Shower?.215f:.1f);
            right.transform.SetPositionAndRotation(Vector3.Lerp(motionStart,target,amount),Quaternion.Slerp(motionRotation,Quaternion.LookRotation(tangent,-normal),amount));
            right.Pose(curl,camera);Resolve(right,camera,curl);grip=right.transform.TransformPoint(Mount);
            if(amount>.98f)
            {
                var wantedGrip=b.Tool==BathTool.Shower?anchor+side+normal*.22f-tangent*.115f:anchor+normal*.028f;
                for(int i=0;i<3;i++){right.transform.position+=wantedGrip-grip;right.Pose(curl,camera);Resolve(right,camera,curl);grip=right.transform.TransformPoint(Mount);}
            }
            Rack.Tool(equipped).SetPositionAndRotation(grip,HandlingPhase==HandlingStep.Carry?Quaternion.Slerp(toolRotationStart,Quaternion.LookRotation(tangent,normal),amount):Quaternion.LookRotation(tangent,normal));
            Physics.SyncTransforms();var closest=effect.Body.ClosestPoint(grip);ToolGap=b.Tool==BathTool.Shower?Vector3.Distance(Rack.Outlet,anchor):Vector3.Distance(grip,closest);
            ContactPenetration=ContactGeometry.Separation(right.ContactShapes,app.cat.ContactSolids,out var correction);
            ToolReachValid=ToolGap<(b.Tool==BathTool.Shower?.4f:.055f)&&ContactPenetration<.012f;
            if(b.Tool!=BathTool.Shower&&ToolReachValid)
            {if(effect.Sample(new Ray(closest+normal*.2f,-normal),out var c,out var p,out var n)&&!c.head){toolZone=c.zone;toolPatch=c.patch;}else ToolReachValid=false;}
            bool assist=held&&b.Tool!=BathTool.Shower&&amount>.98f;left.gameObject.SetActive(assist);
            if(HandlingPhase==HandlingStep.Carry&&leftWasActive&&amount<.9f)
            {
                left.gameObject.SetActive(true);var rest=camera.transform.position+camera.transform.forward*.22f-camera.transform.right*.24f-Vector3.up*.56f;
                left.transform.SetPositionAndRotation(Vector3.Lerp(leftMotionStart,rest,Mathf.Clamp01(amount*1.5f)),Quaternion.Slerp(leftMotionRotation,Quaternion.LookRotation(camera.transform.forward,Vector3.down),amount));left.Pose(12,camera);
            }
            if(assist){var support=effect.Anchor(b.TargetZone==BathZone.Left?BathZone.Back:BathZone.Left,b.TargetPatch);var supportNormal=(support-effect.Body.bounds.center).normalized;SolveMount(left,support+supportNormal*.11f,Quaternion.LookRotation(tangent,-supportNormal),10,camera);Resolve(left,camera,10);}
            marker.gameObject.SetActive(!held&&!Handling&&!b.Ready&&!b.Paused);marker.position=effect.Anchor(b.TargetZone,b.TargetPatch)+normal*.025f;
        }
        void Resolve(CompanionHandView hand,Camera camera,float curl)
        {for(int i=0;i<4;i++){Physics.SyncTransforms();float depth=ContactGeometry.Separation(hand.ContactShapes,app.cat.ContactSolids,out var shift);if(depth<=.004f)break;hand.transform.position+=Vector3.ClampMagnitude(shift,.06f);hand.Pose(curl,camera);}}
        void HideHands(){if(right!=null)right.gameObject.SetActive(false);if(left!=null)left.gameObject.SetActive(false);if(marker!=null)marker.gameObject.SetActive(false);}
        void OnDestroy(){if(skin!=null)Destroy(skin);if(markerMaterial!=null)Destroy(markerMaterial);}
    }
}
