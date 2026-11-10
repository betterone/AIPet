using System;
namespace Momo.Core
{
    // Tracks a small physical circle. Screen movement never changes this physical anchor.
    public sealed class PhysicalApproach
    {
        Point origin,last;
        bool aligned;
        public float Distance {get;private set;}
        public void Reset(){aligned=false;Distance=0;last=new Point(0,0);}
        public Point Sample(Point position)
        {
            if(!Room.Finite(position.x)||!Room.Finite(position.z)){Reset();return new Point(0,0);}
            if(!aligned){origin=position;last=new Point(0,0);aligned=true;return last;}
            var raw=new Point(position.x-origin.x,position.z-origin.z);Distance=Point.Distance(new Point(0,0),raw);
            var bounded=Point.Toward(new Point(0,0),raw,.75f);
            var delta=new Point(bounded.x-last.x,bounded.z-last.z);last=bounded;return delta;
        }
    }
}
