using System.Runtime.InteropServices;
using UnityEngine;
namespace Momo
{
    public sealed class PositionTracking
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int MomoPositionSupported();
        [DllImport("__Internal")] static extern void MomoPositionStart();
        [DllImport("__Internal")] static extern void MomoPositionStop();
        [DllImport("__Internal")] static extern int MomoPositionRead([Out] float[] pose);
#endif
        public bool Supported {
            get {
#if UNITY_IOS && !UNITY_EDITOR
                return MomoPositionSupported()!=0;
#else
                return false;
#endif
            }
        }
        public bool Enabled {get;private set;}
        public int Status {get;private set;}
        public Vector3 Position {get;private set;}
        public Vector2 Angles {get;private set;}
        readonly float[] sample=new float[6];float lastStamp=-1,lastFrameAt;
        public void Start(){if(!Supported){Status=5;return;}Enabled=true;Status=1;lastStamp=-1;
#if UNITY_IOS && !UNITY_EDITOR
            MomoPositionStart();
#endif
        }
        public void Stop(){Enabled=false;Status=0;
#if UNITY_IOS && !UNITY_EDITOR
            MomoPositionStop();
#endif
        }
        public bool Poll()
        {
            if(!Enabled)return false;
#if UNITY_IOS && !UNITY_EDITOR
            Status=MomoPositionRead(sample);
#endif
            if(Status!=2)return false;
            for(int i=0;i<6;i++)if(float.IsNaN(sample[i])||float.IsInfinity(sample[i])){Status=3;return false;}
            if(sample[5]!=lastStamp){lastStamp=sample[5];lastFrameAt=Time.realtimeSinceStartup;}
            if(Time.realtimeSinceStartup-lastFrameAt>.3f){Status=3;return false;}
            Position=new Vector3(sample[0],sample[1],sample[2]);Angles=new Vector2(sample[3],sample[4]);return true;
        }
    }
}
