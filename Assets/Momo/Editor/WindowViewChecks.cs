using System;
using UnityEngine;

namespace Momo.Editor
{
    public static class WindowViewChecks
    {
        static void Near(float actual,float expected,string name)
        {if(Mathf.Abs(actual-expected)>.02f)throw new Exception(name+": "+actual+" != "+expected);}
        public static void Run()
        {
            var baseline=Quaternion.Euler(23,-41,12);
            Vector2 zero=WindowView.MotionDelta(baseline,baseline);
            Near(zero.x,0,"Calibration yaw");Near(zero.y,0,"Calibration pitch");
            var deviceBase=WindowView.DeviceRotation(RawAttitude(baseline));
            var right=WindowView.MotionDelta(deviceBase,WindowView.DeviceRotation(RawAttitude(Quaternion.AngleAxis(20,Vector3.up)*baseline)));
            Near(right.x,20,"Portrait horizontal direction");Near(right.y,0,"No pitch from yaw");
            var opposite=WindowView.MotionDelta(deviceBase,WindowView.DeviceRotation(RawAttitude(Quaternion.AngleAxis(-20,Vector3.up)*baseline)));
            Near(opposite.x,-20,"Opposite portrait turn");
            var rightRay=WindowView.Rotation(WindowView.Home+right*WindowView.DefaultSensitivity)*Vector3.forward;
            var leftRay=WindowView.Rotation(WindowView.Home+opposite*WindowView.DefaultSensitivity)*Vector3.forward;
            if(rightRay.x<=0||leftRay.x>=0)throw new Exception("Physical turn must aim at matching room side");
            Near(right.x*WindowView.DefaultSensitivity,44,"Default gain reaches room side with small turn");
            Near(WindowView.NextSensitivity(1.5f),2.2f,"Low to default speed");
            Near(WindowView.NextSensitivity(2.2f),3f,"Default to high speed");
            Near(WindowView.NextSensitivity(3f),1.5f,"High to low speed");
            var down=WindowView.MotionDelta(deviceBase,WindowView.DeviceRotation(RawAttitude(Quaternion.Euler(38,-41,12))));
            Near(down.y,15,"Portrait pitch direction");Near(down.x,0,"No yaw from pitch");
            var roll=WindowView.MotionDelta(deviceBase,WindowView.DeviceRotation(RawAttitude(baseline*Quaternion.Euler(0,0,25))));
            Near(roll.x,0,"Roll does not pan");Near(roll.y,0,"Roll does not tilt");
            var wrap=WindowView.MotionDelta(Quaternion.Euler(0,179,0),Quaternion.Euler(0,-179,0));Near(wrap.x,2,"Heading wrap");
            foreach(float pitch in new[]{-45f,0,45f})
            {
                var neutral=Quaternion.Euler(pitch,15,20);
                var move=WindowView.MotionDelta(neutral,Quaternion.AngleAxis(20,Vector3.up)*neutral);
                Near(move.x,20,"Tilted grip horizontal turn");Near(move.y,0,"Tilted grip has no vertical drift");
            }
            var limit=WindowView.Clamp(new Vector2(720,-100));Near(limit.x,720,"Full turn yaw");Near(limit.y,-55,"Pitch limit");
            Near(WalkPad.InputForOffset(Vector2.zero,38).magnitude,0,"Joystick centre is still");
            Near(WalkPad.InputForOffset(new Vector2(38,0),38).x,1,"Joystick rim reaches full speed");
            var precise=WalkPad.InputForOffset(new Vector2(10,0),38);
            if(precise.x<=0||precise.x>=.25f)throw new Exception("Joystick gentle range is not precise: "+precise.x);
            int corners=0;
            // A cat directly below the viewer has no horizontal viewing ray; the player
            // intentionally caps physical pitch before that singularity.
            foreach(float x in new[]{-5.2f,0,5.2f})foreach(float z in new[]{-4f,1f,4f})
            {
                Vector3 pet=new Vector3(x,.45f,z);
                var forward=WindowView.Rotation(WindowView.Aim(pet))*Vector3.forward;
                Near(Vector3.Angle(forward,pet-WindowView.Eye),0,"Find cat covers room");corners++;
            }
            CheckPointer();
            Debug.Log("WINDOW VIEW CHECKS PASSED: portrait turn, speed tiers, full yaw, pitch limits, "+corners+" room positions, entry/retreat, horizontal and vertical touch drag, prompt pinch zoom, release displacement, UI exclusion. Physical sensor input remains a device check.");
        }
        static Quaternion RawAttitude(Quaternion world)
        {var q=Quaternion.Inverse(Quaternion.Euler(90,0,0))*world*Quaternion.Inverse(Quaternion.Euler(0,0,180));return new Quaternion(q.x,q.y,-q.z,-q.w);}
        static void CheckPointer()
        {
            var fixture=new GameObject("Window interaction checks");
            try
            {
                var app=fixture.AddComponent<MomoApp>();app.simulation=new Momo.Core.Simulation(Momo.Core.Simulation.New(DateTime.UtcNow.Ticks));
                app.room=fixture.AddComponent<RoomView>();
                var lens=new GameObject("Check camera");lens.transform.parent=fixture.transform;
                app.room.cameraView=lens.AddComponent<Camera>();app.window=lens.AddComponent<WindowView>();app.window.Build(app.room.cameraView);
                if(app.window.CanInteract)throw new Exception("Outside view must not interact with pet");
                app.window.AutoApproach(.80f);if(app.window.Phase!=WindowView.ViewPhase.Outside)throw new Exception("Window approached too early");
                app.window.AutoApproach(.10f);if(app.window.Phase!=WindowView.ViewPhase.Approaching)throw new Exception("Window did not approach automatically");
                for(int i=0;i<20;i++)app.window.Tick(.1f,false);
                if(!app.window.CanInteract)throw new Exception("Automatic approach did not enter window view");
                app.window.StepBack();for(int i=0;i<20;i++)app.window.Tick(.1f,false);
                app.window.AutoApproach(3);if(app.window.Phase!=WindowView.ViewPhase.Outside)throw new Exception("Step back must not auto-repeat");
                app.window.Approach();for(int i=0;i<20;i++)app.window.Tick(.1f,false);
                if(!app.window.CanInteract)throw new Exception("Approach did not enter window view");
                Near(Vector3.Distance(lens.transform.position,WindowView.Eye),0,"Approach position");
                app.window.StepBack();for(int i=0;i<20;i++)app.window.Tick(.1f,false);
                if(app.window.Phase!=WindowView.ViewPhase.Outside)throw new Exception("Retreat did not reach outside");
                Near(Vector3.Distance(lens.transform.position,WindowView.OutsideEye),0,"Outside position");
                app.window.Approach();for(int i=0;i<20;i++)app.window.Tick(.1f,false);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Floor";floor.transform.parent=fixture.transform;
                floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(12,.1f,10f);Physics.SyncTransforms();
                Vector3 screen=app.room.cameraView.WorldToScreenPoint(new Vector3(0,0,0));
                var start=new Vector2(screen.x,screen.y);var end=start+new Vector2(Screen.height*.2f,0);
                var before=app.simulation.State.target;
                app.HandlePointer(start,true,true,false,false);app.HandlePointer(end,false,true,false,false);app.HandlePointer(end,false,false,true,false);
                Near(app.window.Angles.x,-18.72f,"Higher-gain horizontal drag angle");
                Near(app.window.Angles.y,0,"Touch does not change vertical look");
                if(app.LastGesture==null||!app.LastGesture.Contains("floor / pan"))throw new Exception("Floor drag must be a pan: "+app.LastGesture);
                if(Momo.Core.Point.Distance(before,app.simulation.State.target)>.001f)throw new Exception("Panning called the cat");
                app.window.Recenter();
                var vertical=start+new Vector2(0,Screen.height*.15f);
                app.HandlePointer(start,true,true,false,false);app.HandlePointer(vertical,false,true,false,false);app.HandlePointer(vertical,false,false,true,false);
                Near(app.window.Angles.y,14.04f,"Vertical touch drag angle");
                if((WindowView.Rotation(app.window.Angles)*Vector3.forward).y>=0)throw new Exception("Upward scene drag must reveal the floor");
                if(Momo.Core.Point.Distance(before,app.simulation.State.target)>.001f)throw new Exception("Vertical panning called the cat");
                app.window.Drag(new Vector2(0,Screen.height*3));Near(app.window.Angles.y,65,"Vertical touch clamp");
                app.window.Recenter();
                app.HandlePointer(start,true,true,false,false);app.HandlePointer(end,false,false,true,false);
                Near(app.window.Angles.x,-18.72f,"Release displacement is not lost");
                app.window.Recenter();app.HandlePointer(start,true,true,false,true);app.HandlePointer(end,false,false,true,false);
                Near(app.window.Angles.x,0,"UI-started drag ignored");
                float beforeZoom=app.window.FieldOfView;app.window.Pinch(Screen.height*.1f);
                Near(app.window.FieldOfView,beforeZoom-22f,"Pinch out zooms in promptly");
                app.window.Tick(.1f,true);
                if(app.room.cameraView.fieldOfView>=beforeZoom-20)throw new Exception("Pinch zoom must react promptly while fingers are held");
                app.window.Pinch(-Screen.height*2);Near(app.window.FieldOfView,92,"Pinch zoom-out limit");
                app.window.ReturnToEntry();app.window.MoveBy(new Vector3(7,0,0));
                Near(app.window.ViewerPosition.x,7,"Move through doorway");
                Vector3 annexPet=new Vector3(10,0,1);app.window.Find(annexPet);
                Near(Vector3.Angle(WindowView.Rotation(app.window.Angles)*Vector3.forward,annexPet+Vector3.up*.45f-app.window.ViewerPosition),0,"Find from moved observer");
                app.window.ReturnToEntry();app.window.MoveBy(new Vector3(5,0,3));var wallStart=app.window.ViewerPosition;
                app.window.MoveBy(new Vector3(5,0,0));
                if(app.window.ViewerPosition.x>5.56f)throw new Exception("Observer crossed room partition");
                app.window.ReturnToEntry();app.window.CatPosition=new Vector3(0,0,1.2f);app.window.MoveBy(new Vector3(0,0,1));
                if(app.window.ViewerPosition.z>.4f)throw new Exception("Observer entered cat body");
                app.window.ReturnToEntry();app.window.CatPosition=new Vector3(100,0,100);
                app.window.SetMove(Vector2.up);app.window.Tick(.1f,false);var heldPosition=app.window.ViewerPosition;
                Near(heldPosition.z,WindowView.JoystickWalkSpeed*.1f,"Full joystick displacement");app.window.SetMove(Vector2.zero);app.window.Tick(.1f,false);
                Near(Vector3.Distance(heldPosition,app.window.ViewerPosition),0,"Release direction stops");

            }
            finally{UnityEngine.Object.DestroyImmediate(fixture);}
        }
    }
}
