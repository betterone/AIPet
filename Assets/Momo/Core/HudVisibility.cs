using System;

namespace Momo.Core
{
    // Real foreground seconds only; autonomous pet events never count as player input.
    public sealed class HudVisibility
    {
        public const float QuietAfter=6;
        public bool AutoHide=true;
        public float IdleSeconds {get;private set;}
        bool pinned;
        public bool Quiet=>AutoHide&&!pinned&&IdleSeconds>=QuietAfter;
        public bool Wake(){bool wasQuiet=Quiet;IdleSeconds=0;return wasQuiet;}
        public void Advance(float realSeconds,bool holding,bool essential)
        {
            if(!Room.Finite(realSeconds)||realSeconds<0)return;
            pinned=essential;
            IdleSeconds=holding||essential||!AutoHide?0:Math.Min(3600,IdleSeconds+realSeconds);
        }
    }
}
