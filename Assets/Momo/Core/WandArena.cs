using System;
using System.Collections.Generic;
namespace Momo.Core
{
    public static class WandArena
    {
        public static bool BoxClear(Point a,Point b)
        {
            // Inflated for the cat body, not just its root point.
            for(int i=0;i<=80;i++){float t=i/80f,x=a.x+(b.x-a.x)*t,z=a.z+(b.z-a.z)*t;if(Math.Abs(x)<.57f&&Math.Abs(z-.08f)<.45f)return false;}return true;
        }
        public static Point Safe(Point p,int layout)
        {
            if(layout==2&&Math.Abs(p.x)<.58f&&Math.Abs(p.z-.08f)<.46f)
            {float dx=.59f-Math.Abs(p.x),dz=.47f-Math.Abs(p.z-.08f);return dx<dz?new Point(p.x<0?-.60f:.60f,p.z):new Point(p.x,p.z<.08f?-.40f:.56f);}
            if(layout==3&&p.z>-.55f&&p.z<.8f&&Math.Abs(p.x)>.25f)return new Point(Math.Max(-.25f,Math.Min(.25f,p.x)),p.z);
            return p;
        }
        public static List<Point> Route(Point a,Point b,int layout)
        {
            if(layout==3)
            {
                b=Safe(b,layout);var path=new List<Point>();
                if(a.z<-.55f&&b.z>=-.55f)path.Add(new Point(0,-.65f));
                if(a.z>.8f&&b.z<=.8f)path.Add(new Point(0,.86f));
                if(b.z<-.55f&&a.z>=-.55f)path.Add(new Point(0,-.65f));
                if(b.z>.8f&&a.z<=.8f)path.Add(new Point(0,.86f));
                path.Add(b);return path;
            }
            if(layout!=2||BoxClear(a,b))return new List<Point>{b};
            var corners=new[]{new Point(-.62f,-.43f),new Point(.62f,-.43f),new Point(-.62f,.59f),new Point(.62f,.59f)};
            List<Point> best=null;float distance=float.MaxValue;
            foreach(var c in corners)foreach(var d in corners)
            {
                if(!BoxClear(a,c)||!BoxClear(c,d)||!BoxClear(d,b))continue;
                float n=Point.Distance(a,c)+Point.Distance(c,d)+Point.Distance(d,b);if(n<distance){distance=n;best=new List<Point>{c,d,b};}
            }
            return best??new List<Point>{a};
        }
        public static Point Follow(Point from,List<Point> route,float distance)
        {foreach(var p in route){float d=Point.Distance(from,p);if(distance<=d)return Point.Toward(from,p,distance);distance-=d;from=p;}return from;}
        public static float Length(Point from,List<Point> route){float n=0;foreach(var p in route){n+=Point.Distance(from,p);from=p;}return n;}
    }
}
