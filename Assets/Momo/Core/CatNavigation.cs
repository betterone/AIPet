using System;
using System.Collections.Generic;

namespace Momo.Core
{
    // Existing Room obstacles already include ~.33m of body clearance. Add the
    // remainder of the conservative, rotation-safe torso envelope used by the cats.
    public static class CatNavigation
    {
        const float Extra = .45f;
        static readonly Room.Solid[] solids = Expand();
        static readonly List<Point> nodes = Nodes();
        static readonly float[,] links = Links();
        static Room.Solid[] Expand()
        {
            var result=new Room.Solid[Room.Solids.Length+4];
            for(int i=0;i<Room.Solids.Length;i++){var r=Room.Solids[i];result[i]=new Room.Solid(r.left-Extra,r.right+Extra,r.bottom-Extra,r.top+Extra);}
            // The walk-in basin is new navigation furniture. Keep Room's legacy save
            // validator unchanged; household settling migrates any old position here.
            float z=BathSession.Station.z;
            result[result.Length-4]=new Room.Solid(-1.98f,-.32f,z-2.18f,z+2.18f);
            result[result.Length-3]=new Room.Solid(.32f,1.98f,z-2.18f,z+2.18f);
            result[result.Length-2]=new Room.Solid(-1.98f,1.98f,z+.52f,z+2.18f);
            // Raised supply tray footprint, inflated by the complete .78m body envelope.
            result[result.Length-1]=new Room.Solid(.38f,2.52f,z-2.20f,z+.10f);
            return result;
        }
        public static bool Walkable(Point p)
        {
            if(!Room.InBounds(p))return false;
            foreach(var r in solids)if(r.Contains(p))return false;
            return true;
        }
        static bool Clip(float p,float q,ref float lo,ref float hi)
        {if(Math.Abs(p)<.000001f)return q>=0;float t=q/p;if(p<0){if(t>hi)return false;lo=Math.Max(lo,t);}else{if(t<lo)return false;hi=Math.Min(hi,t);}return true;}
        public static bool Clear(Point a,Point b)
        {
            if(!Walkable(a)||!Walkable(b))return false;
            foreach(var r in solids)
            {float lo=0,hi=1,dx=b.x-a.x,dz=b.z-a.z;if(Clip(-dx,a.x-r.left,ref lo,ref hi)&&Clip(dx,r.right-a.x,ref lo,ref hi)&&Clip(-dz,a.z-r.bottom,ref lo,ref hi)&&Clip(dz,r.top-a.z,ref lo,ref hi))return false;}
            return true;
        }
        public static Point Nearest(Point p)
        {
            if(Walkable(p))return p;
            if(!Room.Finite(p.x)||!Room.Finite(p.z))p=new Point(0,0);
            float best=float.MaxValue;Point result=new Point(0,0);
            foreach(var r in solids)
                foreach(var q in new[]{new Point(r.left-.02f,p.z),new Point(r.right+.02f,p.z),new Point(p.x,r.bottom-.02f),new Point(p.x,r.top+.02f)})
                    if(Walkable(q)&&Point.Distance(p,q)<best){best=Point.Distance(p,q);result=q;}
            foreach(var q in nodes)if(Point.Distance(p,q)<best){best=Point.Distance(p,q);result=q;}
            return result;
        }
        static List<Point> Nodes()
        {
            var result=new List<Point>();foreach(var r in solids)foreach(float x in new[]{r.left-.02f,r.right+.02f})foreach(float z in new[]{r.bottom-.02f,r.top+.02f})
            {var p=new Point(x,z);if(Walkable(p))result.Add(p);}return result;
        }
        static float[,] Links()
        {
            var result=new float[nodes.Count,nodes.Count];
            for(int i=0;i<nodes.Count;i++)for(int j=0;j<nodes.Count;j++)result[i,j]=i!=j&&Clear(nodes[i],nodes[j])?Point.Distance(nodes[i],nodes[j]):float.MaxValue;
            return result;
        }
        public static List<Point> Path(Point start,Point end)
        {
            start=Nearest(start);end=Nearest(end);if(Clear(start,end))return new List<Point>{end};
            var all=new List<Point>{start,end};all.AddRange(nodes);int n=all.Count;
            var dist=new float[n];var prev=new int[n];var done=new bool[n];
            for(int i=0;i<n;i++){dist[i]=float.MaxValue;prev[i]=-1;}dist[0]=0;
            for(int k=0;k<n;k++)
            {
                int u=-1;for(int i=0;i<n;i++)if(!done[i]&&(u<0||dist[i]<dist[u]))u=i;
                if(u<0||dist[u]==float.MaxValue)break;if(u==1)break;done[u]=true;
                for(int v=0;v<n;v++)if(!done[v]&&v!=u){float edge=u>=2&&v>=2?links[u-2,v-2]:Clear(all[u],all[v])?Point.Distance(all[u],all[v]):float.MaxValue;
                    if(edge<float.MaxValue&&dist[u]+edge<dist[v]){dist[v]=dist[u]+edge;prev[v]=u;}}
            }
            if(prev[1]<0)return new List<Point>();var path=new List<Point>();for(int v=1;v!=0;v=prev[v])path.Add(all[v]);path.Reverse();return path;
        }
        // Replan around stationary/slow neighbours when local steering cannot make
        // progress. The same swept disk test validates every edge, including arcs.
        public static List<Point> AroundCats(Point start,Point end,List<Point> bodies,List<float> clearances)
        {
            bool Legal(Point p)
            {if(!Walkable(p))return false;for(int i=0;i<bodies.Count;i++)if(Point.Distance(p,bodies[i])<clearances[i])return false;return true;}
            bool Edge(Point a,Point b)
            {if(!Clear(a,b))return false;for(int i=0;i<bodies.Count;i++)if(SegmentDistance(bodies[i],a,b)<clearances[i])return false;return true;}
            if(!Legal(end))return new List<Point>();
            var all=new List<Point>{start,end};foreach(var p in nodes)if(Legal(p))all.Add(p);
            for(int j=0;j<bodies.Count;j++)for(int i=0;i<20;i++)
            {double angle=i*Math.PI/10;var body=bodies[j];float reach=clearances[j]+.045f;var p=new Point(body.x+(float)Math.Cos(angle)*reach,body.z+(float)Math.Sin(angle)*reach);if(Legal(p))all.Add(p);}
            int n=all.Count;var dist=new float[n];var prev=new int[n];var done=new bool[n];
            for(int i=0;i<n;i++){dist[i]=float.MaxValue;prev[i]=-1;}dist[0]=0;
            for(int k=0;k<n;k++)
            {
                int u=-1;for(int i=0;i<n;i++)if(!done[i]&&(u<0||dist[i]<dist[u]))u=i;
                if(u<0||dist[u]==float.MaxValue)break;if(u==1)break;done[u]=true;
                for(int v=0;v<n;v++)if(!done[v]&&v!=u)
                {float cost=Point.Distance(all[u],all[v]);if(dist[u]+cost<dist[v]&&Edge(all[u],all[v])){dist[v]=dist[u]+cost;prev[v]=u;}}
            }
            if(prev[1]<0)return new List<Point>();var result=new List<Point>();for(int v=1;v!=0;v=prev[v])result.Add(all[v]);result.Reverse();return result;
        }
        public static float SegmentDistance(Point p,Point a,Point b)
        {
            float x=b.x-a.x,z=b.z-a.z,len=x*x+z*z;
            float t=len<.000001f?0:Math.Max(0,Math.Min(1,((p.x-a.x)*x+(p.z-a.z)*z)/len));
            return Point.Distance(p,new Point(a.x+t*x,a.z+t*z));
        }
    }
}
