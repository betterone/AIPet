using UnityEngine;
using Momo.Core;

namespace Momo
{
    // Presentation only: viewing never moves the pet or changes its saved world.
    public sealed class WindowView : MonoBehaviour
    {
        // Once inside, the viewer stands in the middle of the room rather than at its window.
        public static readonly Vector3 Eye = new Vector3(0, 1.55f, 0);
        public static readonly Vector2 Home = new Vector2(0, 0);
        public static readonly Vector3 OutsideEye = new Vector3(0,2,-19);
        public enum ViewPhase { Outside, Approaching, Inside, Retreating }
        public ViewPhase Phase {get;private set;}
        public bool CanInteract {get{return Phase==ViewPhase.Inside;}}
        public bool Transitioning {get{return Phase==ViewPhase.Approaching||Phase==ViewPhase.Retreating;}}
        Camera lens;
        readonly PositionTracking tracking=new PositionTracking();
        Vector2 moveInput;
        bool carePosture;
        bool careOverride,careReturning;
        Vector3 careOrigin;
        Quaternion careRotation;
        float careFov,careReturnTime;
        public void BeginCareView()
        {
            if(careOverride)return;careOrigin=ViewerPosition;careRotation=lens.transform.rotation;careFov=lens.fieldOfView;
            careOverride=true;careReturning=false;PausePosition();
        }
        public void FrameCare(Vector3 position,Vector3 focus,float dt)
        {
            if(!careOverride)return;float t=1-Mathf.Exp(-3*Mathf.Min(dt,.1f));
            lens.transform.position=Vector3.Lerp(lens.transform.position,position,t);
            lens.transform.rotation=Quaternion.Slerp(lens.transform.rotation,Quaternion.LookRotation(focus-lens.transform.position),t);
            lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,68,t);
        }
        public void EndCareView()
        {if(!careOverride)return;careOverride=false;careReturning=true;careReturnTime=0;}
        float careHeight=Eye.y;
        public void SetCarePosture(bool value){carePosture=value;}
        readonly PhysicalApproach physicalApproach=new PhysicalApproach();
        float positionYaw,positionPitch,lastTrackedYaw,unwrappedTrackedYaw;
        bool trackingAligned,positionWanted,positionPaused;
        public System.Func<System.Collections.Generic.IEnumerable<Vector3>> Residents;
        public Vector3 CatPosition {get;set;}=new Vector3(1000,0,1000);
        public Vector3 ViewerPosition=>lens!=null?lens.transform.position:Eye;
        public bool PositionEnabled=>positionWanted;
        public bool PositionAvailable=>tracking.Supported;
        public float PhysicalDistance {get;private set;}
        public string PositionHint=>!positionWanted?"":tracking.Status==4?"未获相机权限，可以继续用方向键走动。":tracking.Status!=2?"走近感应暂不可用，请用方向键走动。":PhysicalDistance>=.75f?"先回到刚才站的位置，远处用方向键走过去。":PhysicalDistance>=.5f?"轻轻凑近就好，远处用方向键。":"走近感应已开启 · 小范围移动手机靠近猫";
        public void SetMove(Vector2 direction){moveInput=Vector2.ClampMagnitude(direction,1);}
        public void TogglePosition()
        {
            if(positionWanted){positionWanted=false;tracking.Stop();if(MotionAvailable)Input.gyro.enabled=MotionEnabled;ResumeView();return;}
            if(!tracking.Supported)return;
            positionWanted=true;trackingAligned=false;PhysicalDistance=0;tracking.Start();
            if(MotionAvailable)Input.gyro.enabled=false;
        }
        public void SetPositionPaused(bool value){if(positionPaused==value)return;positionPaused=value;if(value)PausePosition();else ResumePosition();}
        public void PausePosition(){tracking.Stop();trackingAligned=false;physicalApproach.Reset();moveInput=Vector2.zero;}
        public void ResumePosition(){if(positionWanted&&CanInteract){trackingAligned=false;tracking.Start();}}
        public void ReturnToEntry(){carePosture=false;careHeight=Eye.y;lens.transform.position=Eye;moveInput=Vector2.zero;Recenter();trackingAligned=false;}
        public void MoveBy(Vector3 delta)
        {
            var from=new Point(ViewerPosition.x,ViewerPosition.z);var catPoint=new Point(CatPosition.x,CatPosition.z);
            int steps=Mathf.Max(1,Mathf.CeilToInt(new Vector2(delta.x,delta.z).magnitude/.08f));
            for(int i=0;i<steps;i++)
            {
                var next=Room.Move(from,new Point(delta.x/steps,delta.z/steps),true);
                if(Point.Distance(next,catPoint)<.85f&&Point.Distance(next,catPoint)<Point.Distance(from,catPoint))break;
                bool blocked=false;
                if(Residents!=null)foreach(var resident in Residents())
                {var body=new Point(resident.x,resident.z);if(Point.Distance(next,body)<1.03f&&Point.Distance(next,body)<Point.Distance(from,body)){blocked=true;break;}}
                if(blocked)break;
                from=next;
            }
            lens.transform.position=new Vector3(from.x,careHeight,from.z);
        }
        void Walk(float dt)
        {
            Vector3 delta=Quaternion.AngleAxis(displayed.x,Vector3.up)*new Vector3(moveInput.x,0,moveInput.y)*(JoystickWalkSpeed*Mathf.Min(dt,.1f));
            MoveBy(delta);
        }
        void TrackPosition(bool held)
        {
            if(!positionWanted||positionPaused)return;
            if(!tracking.Enabled)tracking.Start();
            if(!tracking.Poll()){trackingAligned=false;physicalApproach.Reset();return;}
            if(!trackingAligned)
            {physicalApproach.Reset();unwrappedTrackedYaw=lastTrackedYaw=tracking.Angles.x;positionYaw=target.x-unwrappedTrackedYaw;positionPitch=target.y-tracking.Angles.y;trackingAligned=true;}
            unwrappedTrackedYaw+=Mathf.DeltaAngle(lastTrackedYaw,tracking.Angles.x);lastTrackedYaw=tracking.Angles.x;
            Point step=physicalApproach.Sample(new Point(tracking.Position.x,tracking.Position.z));PhysicalDistance=physicalApproach.Distance;
            // Consume blocked deltas too, so recovering tracking never releases stored motion.
            MoveBy(Quaternion.AngleAxis(positionYaw,Vector3.up)*new Vector3(step.x,0,step.z));
            if(held){positionYaw=target.x-unwrappedTrackedYaw;positionPitch=target.y-tracking.Angles.y;}
            else{manualYaw=positionYaw+unwrappedTrackedYaw;motionYaw=0;basePitch=positionPitch+tracking.Angles.y;ApplyLook();}
        }
        Quaternion reference,lastMotion;
        Vector2 anchor, target;
        float manualYaw,basePitch,motionYaw;
        bool calibrated, wasHeld;
        Vector2 displayed;
        Vector3 journeyStart;
        Quaternion journeyRotation;
        float journeyTime,journeyFov,sensorReadyAt,outsideTime;
        float desiredFov;
        bool approachedThisSession;
        const string SensitivityKey="Momo.ViewSensitivityV5";
        public const float DefaultSensitivity=2.2f;
        public float Sensitivity {get;private set;}
        public bool MotionEnabled { get; private set; }
        // On some recent iOS devices Unity reports supportsGyroscope late. The iOS player
        // has the legacy gyro API, so enable it there and let ReadDevice reject bad samples.
        public bool MotionAvailable { get { return Application.platform==RuntimePlatform.IPhonePlayer || (Application.isEditor && SystemInfo.supportsGyroscope); } }
        public Vector2 Angles { get { return target; } }
        public float FieldOfView { get { return desiredFov; } }
        // Full deflection is faster than the former four-button walk. Analogue input
        // keeps a slow, controllable range around the stick centre.
        public const float JoystickWalkSpeed=2.4f;
        public const float TouchYawGain=1.3f;
        public const float PinchDegreesPerScreenHeight=220f;
        public void Build(Camera camera,bool startOutside=true)
        {
            lens=camera;lens.orthographic=false;lens.fieldOfView=72;desiredFov=72;
            lens.transform.position=Eye;displayed=target=anchor=Home;manualYaw=Home.x;basePitch=Home.y;
            lens.transform.rotation=Rotation(target);
            Phase=startOutside?ViewPhase.Outside:ViewPhase.Inside;
            if(startOutside){lens.transform.position=OutsideEye;lens.transform.rotation=Quaternion.identity;lens.fieldOfView=62;}
            // Start the corrected response at the new default, including existing installations.
            Sensitivity=Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey,DefaultSensitivity),1.5f,3f);
            // Exploration starts with touch look. Enabling motion is an explicit choice.
            MotionEnabled=PlayerPrefs.GetInt("Momo.MotionViewV5",0)!=0;
            if(MotionAvailable){Input.gyro.enabled=MotionEnabled;Input.gyro.updateInterval=1f/60;}
            sensorReadyAt=Time.realtimeSinceStartup+.4f;
        }
        // Apply pitch in the viewer's local frame after yaw, so Find cat and physical tilt
        // remain correct even while looking through the rear half of the 360-degree room.
        public static Quaternion Rotation(Vector2 angles) { return Quaternion.AngleAxis(angles.x,Vector3.up)*Quaternion.AngleAxis(angles.y,Vector3.right); }
        public static Vector2 Clamp(Vector2 angles)
        {return new Vector2(angles.x,Mathf.Clamp(angles.y,-55,65));}
        public static Vector2 Aim(Vector3 position)
        {
            Vector3 direction=position-Eye;
            return Clamp(new Vector2(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,
                -Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg));
        }
        // Unity gyro is right handed; the final half-turn aligns the rear-facing viewing ray.
        public static Quaternion DeviceRotation(Quaternion attitude)
        {return Quaternion.Euler(90,0,0)*new Quaternion(attitude.x,attitude.y,-attitude.z,-attitude.w).normalized*Quaternion.Euler(0,0,180);}
        public static Vector2 Heading(Quaternion rotation)
        {
            Vector3 direction=rotation*Vector3.forward;
            return new Vector2(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,
                -Mathf.Asin(Mathf.Clamp(direction.y,-1,1))*Mathf.Rad2Deg);
        }
        public static Vector2 MotionDelta(Quaternion baseline,Quaternion current)
        {
            // Gravity-aligned headings keep left/right stable when the phone is held tilted.
            Vector2 a=Heading(baseline),b=Heading(current);
            // The viewing ray follows the physical left/right turn on the device.
            return new Vector2(Mathf.DeltaAngle(a.x,b.x),b.y-a.y);
        }
        bool ReadDevice(out Quaternion value)
        {
            value=Quaternion.identity;
            if(!MotionAvailable||!MotionEnabled||Time.realtimeSinceStartup<sensorReadyAt)return false;
            Quaternion q=Input.gyro.attitude;
            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            if(float.IsNaN(norm)||float.IsInfinity(norm)||norm<.5f)return false;
            value=DeviceRotation(q);
            // Heading is undefined when the rear camera points almost straight up/down.
            return Mathf.Abs((value*Vector3.forward).y)<.97f;
        }
        void ApplyLook(){target=Clamp(new Vector2(manualYaw+motionYaw,basePitch));}
        public void Rebase(){calibrated=false;anchor=target;if(positionWanted&&trackingAligned){positionYaw=target.x-unwrappedTrackedYaw;positionPitch=target.y-tracking.Angles.y;}}
        public void ResumeView(){Rebase();sensorReadyAt=Time.realtimeSinceStartup+.4f;}
        public void AutoApproach(float dt)
        {
            if(approachedThisSession||Phase!=ViewPhase.Outside)return;
            outsideTime+=dt;
            if(outsideTime>=.85f)Approach();
        }
        public void Approach(){if(Phase==ViewPhase.Outside){approachedThisSession=true;BeginJourney(ViewPhase.Approaching);}}
        public void StepBack(){if(Phase==ViewPhase.Inside){PausePosition();BeginJourney(ViewPhase.Retreating);}}
        void BeginJourney(ViewPhase phase)
        {Phase=phase;journeyTime=0;journeyStart=lens.transform.position;journeyRotation=lens.transform.rotation;journeyFov=lens.fieldOfView;Rebase();}
        public void Recenter(){manualYaw=Home.x;motionYaw=0;basePitch=Home.y;ApplyLook();Rebase();}
        public void Find(Vector3 petPosition)
        {
            Vector3 dir=petPosition+Vector3.up*.45f-ViewerPosition;
            Vector2 aim=Clamp(new Vector2(Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg,-Mathf.Atan2(dir.y,new Vector2(dir.x,dir.z).magnitude)*Mathf.Rad2Deg));manualYaw=aim.x;motionYaw=0;basePitch=aim.y;ApplyLook();Rebase();
        }
        public void FrameNearCompanion()
        {basePitch=22;ApplyLook();Rebase();}
        public void ToggleMotion()
        {
            MotionEnabled=!MotionEnabled;PlayerPrefs.SetInt("Momo.MotionViewV5",MotionEnabled?1:0);PlayerPrefs.Save();
            if(MotionAvailable)Input.gyro.enabled=MotionEnabled&&!positionWanted;
            ResumeView();
        }
        public void CycleSensitivity()
        {Sensitivity=NextSensitivity(Sensitivity);PlayerPrefs.SetFloat(SensitivityKey,Sensitivity);PlayerPrefs.Save();Rebase();}
        public static float NextSensitivity(float value)
        {return value<1.85f?2.2f:value<2.6f?3f:1.5f;}
        public void Drag(Vector2 pixels)
        {
            // Match the horizontal panorama gesture in both directions: moving the
            // scene up reveals more floor, and moving it down reveals more ceiling.
            float degrees=lens.fieldOfView/Mathf.Max(1,Screen.height)*TouchYawGain;
            manualYaw-=pixels.x*degrees;
            basePitch+=pixels.y*degrees;
            ApplyLook();Rebase();
        }
        public void Pinch(float distanceDelta)
        {
            if(!CanInteract)return;
            // Spread fingers to zoom in, bring them together to see more of the room.
            desiredFov=Mathf.Clamp(desiredFov-distanceDelta/Mathf.Max(1,Screen.height)*PinchDegreesPerScreenHeight,48,92);
        }
        public void Tick(float dt,bool held)
        {
            if(careOverride)return;
            if(careReturning)
            {
                careReturnTime+=Mathf.Min(dt,.1f);float t=Mathf.Clamp01(careReturnTime/.55f);
                lens.transform.position=Vector3.Lerp(lens.transform.position,careOrigin,t);lens.transform.rotation=Quaternion.Slerp(lens.transform.rotation,careRotation,t);lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,careFov,t);
                if(t>=1){careReturning=false;ResumeView();ResumePosition();}return;
            }
            if(Transitioning)
            {
                journeyTime+=Mathf.Min(dt,.1f);float t=Mathf.Clamp01(journeyTime/1.8f);t=t*t*t*(t*(t*6-15)+10);
                bool entering=Phase==ViewPhase.Approaching;
                lens.transform.position=Vector3.Lerp(journeyStart,entering?Eye:OutsideEye,t);
                lens.transform.rotation=Quaternion.Slerp(journeyRotation,entering?Rotation(Home):Quaternion.identity,t);
                lens.fieldOfView=Mathf.Lerp(journeyFov,entering?desiredFov:62,t);
                if(journeyTime>=1.8f){Phase=entering?ViewPhase.Inside:ViewPhase.Outside;displayed=target=anchor=Home;manualYaw=Home.x;motionYaw=0;basePitch=Home.y;ResumeView();}
                return;
            }
            if(!CanInteract)return;
            careHeight=Mathf.Lerp(careHeight,carePosture?1.05f:Eye.y,1-Mathf.Exp(-8*Mathf.Min(dt,.1f)));
            var eye=lens.transform.position;eye.y=careHeight;lens.transform.position=eye;
            if(!positionPaused)Walk(dt);TrackPosition(held);
            if(positionWanted){RenderLook(dt,32);return;}
            if(held)
            {
                if(!wasHeld){manualYaw=displayed.x-motionYaw;basePitch=displayed.y;ApplyLook();}
                wasHeld=true;Rebase();RenderLook(dt,32);return;
            }
            if(wasHeld){wasHeld=false;Rebase();}
            if(ReadDevice(out Quaternion current))
            {
                if(!calibrated)
                {
                    reference=lastMotion=current;anchor=target;manualYaw=target.x-motionYaw;basePitch=target.y;calibrated=true;
                }
                // Incremental yaw does not wrap at 180°, so a real turn can cover the room.
                Vector2 step=MotionDelta(lastMotion,current),fromReference=MotionDelta(reference,current);
                motionYaw+=step.x*Sensitivity;basePitch=Clamp(new Vector2(0,anchor.y+fromReference.y*Sensitivity)).y;
                lastMotion=current;ApplyLook();
            }
            else Rebase();
            RenderLook(dt,32);
        }
        public void RenderDrag(float dt)
        {RenderLook(dt,22);}
        void RenderLook(float dt,float speed)
        {
            float smoothing=1-Mathf.Exp(-speed*Mathf.Min(dt,.1f));
            displayed=Vector2.Lerp(displayed,target,smoothing);lens.transform.rotation=Rotation(displayed);
            lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,desiredFov,1-Mathf.Exp(-36*Mathf.Min(dt,.1f)));
        }
        void OnDestroy(){tracking.Stop();if(MotionAvailable)Input.gyro.enabled=false;}
    }
}
