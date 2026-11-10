using System;
using System.Collections.Generic;

namespace Momo.Core
{
    public enum Activity { Idle, Walk, Sleep, Eat, Drink, Groom, Play, Explore, Watch, Hide, Greet }
    [Serializable] public struct Point
    {
        public float x, z;
        public Point(float x, float z) { this.x = x; this.z = z; }
        public static float Distance(Point a, Point b) { return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.z-b.z)*(a.z-b.z)); }
        public static Point Toward(Point a, Point b, float distance)
        { float d = Distance(a,b); return d <= distance ? b : new Point(a.x+(b.x-a.x)*distance/d,a.z+(b.z-a.z)*distance/d); }
    }
    [Serializable] public class Personality
    {
        public float curiosity = .65f, sociability = .6f, playfulness = .65f, independence = .45f;
        public static Personality FromStyle(int style)
        {
            if (style == 1) return new Personality { curiosity=.4f, sociability=.85f, playfulness=.35f, independence=.25f };
            if (style == 2) return new Personality { curiosity=.8f, sociability=.3f, playfulness=.5f, independence=.85f };
            return new Personality();
        }
    }
    [Serializable] public class LifeEvent
    {
        public long id;
        public double time;
        public string kind, text;
        public int actorId, otherCatId;
        public LifeEvent(long id, double time, string kind, string text) { this.id=id; this.time=time; this.kind=kind; this.text=text; }
    }
    [Serializable] public class WorldState
    {
        public int schema = 1;
        public string name = "Momo";
        public bool adopted;
        public int coat;
        public int breedIndex;
        public Personality personality = new Personality();
        public uint random = 827139;
        public double seconds, remainder;
        public long lastUtcTicks, createdUtcTicks, nextEventId = 1;
        public Point pet = new Point(0,0), target = new Point(0,0), ball = new Point(1,-1.2f);
        public List<Point> route = new List<Point>();
        public Activity activity = Activity.Idle, intent = Activity.Idle;
        public float energy = 72, hunger = 25, thirst = 18, boredom = 42, affection = 20;
        public float dirt; // Optional in old saves: zero is clean. Foreground garden activity only.
        public float food = 90, water = 90, ballPreference, wandPreference, windowPreference;
        public float[] playHabits = new float[24];
        public float[] cooldowns = new float[11];
        public double remaining = 100, lastPetAt = -1000, lastPlayAt = -1000, lastWandAt = -1000;
        public int petStreak;
        public bool hasHandFed;
        public double lastHandFeedAt;
        public bool ballTrapped, sound = true;
        public List<LifeEvent> events = new List<LifeEvent>();
        public long revision;
    }
    // Shared navigation geometry for cats, the observer and both rooms. Units are metres.
    public static class Room
    {
        public const float MinX=-5.35f, MaxX=11.35f, MinZ=-4.15f, MaxZ=20.35f;
        public const int MaxRoutePoints=128;
        public static readonly Point IndoorBag=new Point(-2.6f,6.3f), OutdoorBag=new Point(10.65f,12.5f);
        public static readonly Point[] ExploreSpots={new Point(0,2),new Point(9,0),new Point(3,9),new Point(8,8),new Point(3,13),new Point(-.5f,18.8f),new Point(10,19)};
        public static string Area(Point p)=>p.z>11.35f?"后院":p.z>5.35f?(p.x>6?"休息房":"小厨房"):(p.x>6?"玩耍区":"生活区");
        public static readonly Point Bed=new Point(-4,-2.8f), Food=new Point(4.5f,2.9f), Water=new Point(4.5f,1.7f), Window=new Point(.3f,3.85f), Box=new Point(3.7f,-3.2f);
        public struct Solid
        {
            public float left,right,bottom,top;
            public Solid(float l,float r,float b,float t){left=l;right=r;bottom=b;top=t;}
            public bool Contains(Point p)=>p.x>left&&p.x<right&&p.z>bottom&&p.z<top;
        }
        // Inflated for body clearance. Keep original furniture coordinates save-compatible.
        public static readonly Solid[] Solids={
            new Solid(-6,-3.5f,1.05f,2.95f), new Solid(2.9f,4.55f,-5,-2.65f),
            new Solid(5.55f,6.45f,1.15f,5),new Solid(5.55f,6.45f,-5,-1.15f),
            new Solid(8.25f,11.15f,2.0f,3.6f),
            new Solid(8.05f,8.65f,-3.7f,-1.5f),new Solid(9.95f,10.55f,-3.7f,-1.5f),
            // Real wall openings connect the new rooms and garden. All geometry is shared.
            new Solid(-6,1.85f,4.65f,5.35f),new Solid(4.15f,7.85f,4.65f,5.35f),new Solid(10.15f,12,4.65f,5.35f),
            new Solid(5.55f,6.45f,5.35f,6.85f),new Solid(5.55f,6.45f,9.15f,11.35f),
            new Solid(-6,1.85f,10.65f,11.35f),new Solid(4.15f,7.85f,10.65f,11.35f),new Solid(10.15f,12,10.65f,11.35f),
            new Solid(-5.5f,-3.35f,7.0f,10.3f),new Solid(-.75f,1.25f,7.0f,8.9f),
            new Solid(10.05f,11.5f,6.1f,8.6f),new Solid(7.2f,8.8f,9.25f,10.4f),
            new Solid(-5.1f,-2.0f,13.15f,15.85f),new Solid(6.7f,9.0f,16.05f,18.55f),
            new Solid(-4.15f,-2.85f,17.35f,18.65f),new Solid(9.15f,10.45f,13.75f,15.05f),
            new Solid(-3.15f,-2.05f,5.75f,6.85f),new Solid(10.1f,11.2f,11.95f,13.05f)
        };
        // Cats can enter their bed and tunnel; a standing observer must walk around them.
        public static readonly Solid[] ObserverSolids={new Solid(-5,-3,-3.65f,-1.95f),new Solid(8.05f,10.55f,-3.7f,-1.5f),new Solid(-1.5f,2.05f,15.1f,18.5f)};
        public static readonly Point[] HideSpots={new Point(-4.6f,3.35f),new Point(4.95f,-3.2f),new Point(10.9f,3.9f),new Point(9.3f,-3.4f),new Point(-3,9.7f),new Point(9.4f,9.8f),new Point(-4.7f,16.4f),new Point(9.6f,18.9f)};
        public static readonly Point[] PeekSpots={new Point(-3.25f,3.25f),new Point(4.9f,-2.3f),new Point(7.7f,2.5f),new Point(9.3f,-1.15f),new Point(-2.5f,8.5f),new Point(9.2f,8.8f),new Point(-1.5f,16.4f),new Point(9.5f,17.5f)};
        static readonly List<Point> nodes=MakeNodes();
        static readonly float[,] links=MakeLinks();
        public static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static bool InBounds(Point p)=>Finite(p.x)&&Finite(p.z)&&p.x>=MinX&&p.x<=MaxX&&p.z>=MinZ&&p.z<=MaxZ;
        public static bool Walkable(Point p)
        {if(!InBounds(p))return false;foreach(var r in Solids)if(r.Contains(p))return false;return true;}
        public static Point Nearest(Point p)
        {
            if(Walkable(p))return p;
            if(!Finite(p.x)||!Finite(p.z))return new Point(0,0);
            Point best=new Point(Math.Max(MinX,Math.Min(MaxX,p.x)),Math.Max(MinZ,Math.Min(MaxZ,p.z)));
            if(Walkable(best))return best;
            float d=float.MaxValue;
            foreach(var r in Solids)
            foreach(var q in new[]{new Point(r.left-.04f,p.z),new Point(r.right+.04f,p.z),new Point(p.x,r.bottom-.04f),new Point(p.x,r.top+.04f)})
                if(Walkable(q)&&Point.Distance(p,q)<d){best=q;d=Point.Distance(p,q);}
            return Walkable(best)?best:new Point(0,0);
        }
        static bool Clip(float p,float q,ref float low,ref float high)
        {if(Math.Abs(p)<.000001f)return q>=0;float t=q/p;if(p<0){if(t>high)return false;low=Math.Max(low,t);}else{if(t<low)return false;high=Math.Min(high,t);}return true;}
        static bool Intersects(Point a,Point b,Solid r)
        {
            float low=0,high=1,dx=b.x-a.x,dz=b.z-a.z;
            return Clip(-dx,a.x-r.left,ref low,ref high)&&Clip(dx,r.right-a.x,ref low,ref high)&&Clip(-dz,a.z-r.bottom,ref low,ref high)&&Clip(dz,r.top-a.z,ref low,ref high);
        }
        public static bool Clear(Point a,Point b)
        {if(!Walkable(a)||!Walkable(b))return false;foreach(var r in Solids)if(Intersects(a,b,r))return false;return true;}
        static List<Point> MakeNodes()
        {
            var list=new List<Point>();
            foreach(var r in Solids)foreach(float x in new[]{r.left-.04f,r.right+.04f})foreach(float z in new[]{r.bottom-.04f,r.top+.04f})
            {var p=new Point(x,z);if(Walkable(p))list.Add(p);}
            return list;
        }
        static float[,] MakeLinks()
        {
            var a=new float[nodes.Count,nodes.Count];
            for(int i=0;i<nodes.Count;i++)for(int j=0;j<nodes.Count;j++)a[i,j]=i!=j&&Clear(nodes[i],nodes[j])?Point.Distance(nodes[i],nodes[j]):float.MaxValue;
            return a;
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
                for(int v=0;v<n;v++)if(!done[v]&&v!=u)
                {
                    float edge=u>=2&&v>=2?links[u-2,v-2]:Clear(all[u],all[v])?Point.Distance(all[u],all[v]):float.MaxValue;
                    if(edge<float.MaxValue&&dist[u]+edge<dist[v]){dist[v]=dist[u]+edge;prev[v]=u;}
                }
            }
            if(prev[1]<0)return new List<Point>();var path=new List<Point>();
            for(int v=1;v!=0;v=prev[v])path.Add(all[v]);path.Reverse();return path;
        }
        // Swept movement, with wall sliding, prevents tunnelling even after a slow frame.
        public static Point Move(Point start,Point delta,bool observer=false)
        {
            bool CanMove(Point a,Point b)
            {if(!Clear(a,b))return false;if(observer)foreach(var r in ObserverSolids)if(Intersects(a,b,r))return false;return true;}
            start=Nearest(start);int steps=Math.Max(1,(int)(Point.Distance(new Point(0,0),delta)/.08f)+1);
            for(int i=0;i<steps;i++)
            {var next=new Point(start.x+delta.x/steps,start.z+delta.z/steps);if(CanMove(start,next))start=next;
             else{next=new Point(start.x+delta.x/steps,start.z);if(CanMove(start,next))start=next;next=new Point(start.x,start.z+delta.z/steps);if(CanMove(start,next))start=next;}}
            return start;
        }
    }
    [Serializable]
    public class CatBreedProfile
    {
        public int id;
        public string defaultName;
        public string breedName;
        public string tag;
        public string quote;
        public string description;
        public Personality personality;
        public string hexCoat;
        public string hexSecondary;
        public string hexMarking;
        public string hexEyeLeft;
        public string hexEyeRight;
        public string hexNose;
        public float headWidth = 1f, headHeight = 1f;
        public float bodyLength = 1f, bodyGirth = 1f;
        public float legScaleX = 1f, legScaleY = 1f;
        public float tailLength = 1f, tailThickness = 1f;
        public float earScale = 1f;
        public int patternType; // 0=Tabby, 1=Solid, 2=Points, 3=Tuxedo, 4=Calico, 5=Ragdoll
        public string[] reactions;
    }
    public static class BreedArchetypes
    {
        public static readonly CatBreedProfile[] All = new[]
        {
            new CatBreedProfile
            {
                id=0, defaultName="Mikan", breedName="Ginger Tabby", tag="Playful & Bold",
                quote="A sunbeam full of mischief and purrs.",
                description="Warm orange tabby stripes with snowy white socks. Loves chasing anything that rolls.",
                personality=new Personality{playfulness=.90f, curiosity=.80f, sociability=.80f, independence=.30f},
                hexCoat="#E59850", hexSecondary="#FFF5E6", hexMarking="#B26023",
                hexEyeLeft="#E49B38", hexEyeRight="#E49B38", hexNose="#EE9B8F",
                headWidth=1.02f, headHeight=1.0f, bodyLength=1.0f, bodyGirth=1.02f, legScaleY=1.0f, tailThickness=1.02f, earScale=1.0f,
                patternType=0,
                reactions=new[]{"Mikan raises tail high with an energetic tremble!","Mikan playfully bats your hand with a soft paw.","Mikan purrs warmly and leans in for a headbutt."}
            },
            new CatBreedProfile
            {
                id=1, defaultName="Earl", breedName="British Blue", tag="Gentle & Calm",
                quote="Quiet dignity and round cheeks for cozy afternoons.",
                description="Lustrous slate-blue coat with plush chubby cheeks. Enjoys lazy stretches and gentle slow blinks.",
                personality=new Personality{playfulness=.25f, curiosity=.40f, sociability=.75f, independence=.60f},
                hexCoat="#6C7A86", hexSecondary="#7F8E9C", hexMarking="#55626D",
                hexEyeLeft="#E69C24", hexEyeRight="#E69C24", hexNose="#5D6874",
                headWidth=1.20f, headHeight=1.10f, bodyLength=0.96f, bodyGirth=1.20f, legScaleY=0.88f, tailThickness=1.18f, earScale=0.92f,
                patternType=1,
                reactions=new[]{"Earl closes round eyes and offers a soothing slow blink.","Earl lets out a soft sigh and sinks into a comfortable crouch.","Earl gently turns a chubby cheek into your palm."}
            },
            new CatBreedProfile
            {
                id=2, defaultName="Chai", breedName="Siamese Point", tag="Curious Explorer",
                quote="Quick as a whisper, alert to every corner.",
                description="Silk cream fur with deep chocolate points and sapphire blue eyes. Radiates intelligent affection.",
                personality=new Personality{playfulness=.70f, curiosity=.95f, sociability=.90f, independence=.20f},
                hexCoat="#EAE0D0", hexSecondary="#FAF3E8", hexMarking="#3D2B22",
                hexEyeLeft="#2A6EBB", hexEyeRight="#2A6EBB", hexNose="#3D2B22",
                headWidth=0.90f, headHeight=0.98f, bodyLength=1.06f, bodyGirth=0.88f, legScaleY=1.14f, tailLength=1.20f, tailThickness=0.80f, earScale=1.26f,
                patternType=2,
                reactions=new[]{"Chai's ears swivel eagerly, following your every touch.","Chai stretches a slender neck forward, sniffing gently.","Chai gives an inquisitive chirp and rubs against your fingers."}
            },
            new CatBreedProfile
            {
                id=3, defaultName="Oreo", breedName="Tuxedo Cat", tag="Goofy Acrobat",
                quote="Dressed in black tie, ready for unexpected adventures.",
                description="Classic black-and-white mask with clean white paws. Full of quirky hops and silly charm.",
                personality=new Personality{playfulness=.95f, curiosity=.85f, sociability=.60f, independence=.50f},
                hexCoat="#22252A", hexSecondary="#FFFFFF", hexMarking="#181A1E",
                hexEyeLeft="#A6C844", hexEyeRight="#A6C844", hexNose="#EFA59C",
                headWidth=0.98f, headHeight=0.98f, bodyLength=1.02f, bodyGirth=0.95f, legScaleY=1.04f, tailThickness=0.92f, earScale=1.05f,
                patternType=3,
                reactions=new[]{"Oreo's ears give a quick twitch before pouncing playfully!","Oreo arches its back dramatically and does a happy spin.","Oreo nudges your finger with a pink-spotted nose."}
            },
            new CatBreedProfile
            {
                id=4, defaultName="Yuki", breedName="Snow White", tag="Poised & Regal",
                quote="Pure winter stillness with a warm amber gaze.",
                description="Snowy coat, round cheeks and honey-amber eyes. Gracious and discerning.",
                personality=new Personality{playfulness=.35f, curiosity=.60f, sociability=.50f, independence=.85f},
                hexCoat="#F8F8FC", hexSecondary="#FFFFFF", hexMarking="#EDEDF5",
                hexEyeLeft="#3A8EDB", hexEyeRight="#E29828", hexNose="#F2A8AC",
                headWidth=0.96f, headHeight=1.02f, bodyLength=1.02f, bodyGirth=0.92f, legScaleY=1.08f, tailLength=1.12f, tailThickness=0.90f, earScale=1.02f,
                patternType=1,
                reactions=new[]{"Yuki regards you quietly, amber eyes luminous in the light.","Yuki gracefully inclines its head, accepting your gentle stroke.","Yuki emits a delicate, barely audible purr of approval."}
            },
            new CatBreedProfile
            {
                id=5, defaultName="Shadow", breedName="Bombay Black", tag="Quiet Guardian",
                quote="Silent velvet steps and a devoted heart.",
                description="Glossy raven-black coat with striking golden eyes. Loves peaceful companionship.",
                personality=new Personality{playfulness=.50f, curiosity=.70f, sociability=.85f, independence=.50f},
                hexCoat="#1A1B1E", hexSecondary="#141517", hexMarking="#0D0E10",
                hexEyeLeft="#32B25E", hexEyeRight="#32B25E", hexNose="#1A1B1E",
                headWidth=1.0f, headHeight=0.98f, bodyLength=1.0f, bodyGirth=0.96f, legScaleY=1.02f, tailThickness=0.95f, earScale=1.0f,
                patternType=1,
                reactions=new[]{"Shadow's golden eyes glow warmly as you stroke its chin.","Shadow leans firmly into your hand, closing eyes with trust.","Shadow's tail tip sways slowly in peaceful contentment."}
            },
            new CatBreedProfile
            {
                id=6, defaultName="Mochi", breedName="Calico Mosaic", tag="Spirited & Sweet",
                quote="A colorful patch of sunshine with a mind of its own.",
                description="Vibrant collage of black, warm ginger, and pure cream patches. Affectionate and spirited.",
                personality=new Personality{playfulness=.60f, curiosity=.75f, sociability=.70f, independence=.65f},
                hexCoat="#FAF6EF", hexSecondary="#E08035", hexMarking="#2B2828",
                hexEyeLeft="#C2A332", hexEyeRight="#C2A332", hexNose="#E89B96",
                headWidth=1.02f, headHeight=1.0f, bodyLength=0.98f, bodyGirth=1.04f, legScaleY=0.98f, tailThickness=1.05f, earScale=1.0f,
                patternType=4,
                reactions=new[]{"Mochi purrs cheerfully and rolls slightly onto its side.","Mochi rubs both cheeks against your hand with delightful zest.","Mochi sits up proudly, taking a moment to groom a speckled paw."}
            },
            new CatBreedProfile
            {
                id=7, defaultName="Cotton", breedName="Ragdoll Bicolor", tag="Plush & Devoted",
                quote="A gentle cloud that melts completely into your touch.",
                description="Soft cream coat with taupe ear masks and a giant fluffy tail. Renowned for boundless trust.",
                personality=new Personality{playfulness=.30f, curiosity=.50f, sociability=.98f, independence=.10f},
                hexCoat="#FAF2E6", hexSecondary="#FFFFFF", hexMarking="#8C7C70",
                hexEyeLeft="#3277C6", hexEyeRight="#3277C6", hexNose="#F2ADA8",
                headWidth=1.12f, headHeight=1.06f, bodyLength=1.12f, bodyGirth=1.22f, legScaleY=0.92f, legScaleX=1.15f, tailLength=1.25f, tailThickness=1.55f, earScale=0.95f,
                patternType=5,
                reactions=new[]{"Cotton completely relaxes, flopping down like a soft cloud.","Cotton purrs deeply like a little motorboat in your hands.","Cotton blinks big sapphire eyes and rests its chin on the floor."}
            },
            new CatBreedProfile
            {
                id=8, defaultName="Huahua", breedName="Dragon Li Tabby", tag="Athletic & Wise",
                quote="Ancient wilderness spirit, loyal to the very end.",
                description="Mackerel brown tabby markings over agile, lithe muscles. Alert, healthy, and deeply devoted.",
                personality=new Personality{playfulness=.75f, curiosity=.90f, sociability=.60f, independence=.80f},
                hexCoat="#947A5B", hexSecondary="#E0D3BE", hexMarking="#423223",
                hexEyeLeft="#8BA338", hexEyeRight="#8BA338", hexNose="#BC6E5C",
                headWidth=1.04f, headHeight=1.0f, bodyLength=1.08f, bodyGirth=0.98f, legScaleY=1.06f, tailThickness=0.98f, earScale=1.02f,
                patternType=0,
                reactions=new[]{"Huahua's athletic posture softens as it accepts your stroke.","Huahua tracks your fingers with sharp, intelligent yellow-green eyes.","Huahua gives a solid, reassuring headbutt against your palm."}
            }
        };
        public static CatBreedProfile Get(int index)
        {
            if(index<0||index>=All.Length) return All[0];
            return All[index];
        }
    }
}
