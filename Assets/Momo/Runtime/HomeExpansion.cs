using UnityEngine;
using Momo.Core;

namespace Momo
{
    // Four connected rooms and an enclosed garden. Navigation footprints live in Core.Room.
    public static class HomeExpansion
    {
        static Material plaster,wood,mint,cream,leaf,clay;
        static Transform root;
        static void Box(string name,Vector3 p,Vector3 s,Material m,bool collider=false)=>Art.Cube(name,root,p,s,m,collider);
        static void Floor(string name,Vector3 p,Vector3 s,Material m)
        {var g=Art.Shape(name,PrimitiveType.Cube,root,p,s,m,true);g.AddComponent<TouchTarget>().kind="floor";}
        public static void Build(Transform parent)
        {
            root=parent;plaster=Art.Mat("Kitchen lemon plaster","#F5E7BC");wood=Art.Mat("Honey oak","#DDA55F");mint=Art.Mat("Garden mint","#A9CFB3");cream=Art.Mat("Milk ceramic","#FFF5D8");leaf=Art.Mat("Garden grass","#98C873");clay=Art.Mat("Clay","#DE7957");
            Floor("Kitchen floor",new Vector3(0,-.035f,8),new Vector3(12,.07f,6),cream);
            Floor("Quiet room floor",new Vector3(9,-.035f,8),new Vector3(6,.07f,6),mint);
            Floor("Garden floor",new Vector3(3,-.04f,16),new Vector3(18,.08f,10),leaf);
            Box("Kitchen west wall",new Vector3(-6,1.65f,8),new Vector3(.18f,3.3f,6),plaster);
            Box("Quiet east wall",new Vector3(12,1.65f,8),new Vector3(.18f,3.3f,6),mint);
            SplitWall(5);SplitWall(11);
            foreach(float z in new[]{5.75f,10.25f})Box("Kitchen quiet partition",new Vector3(6,1.65f,z),new Vector3(.18f,3.3f,1.5f),plaster);
            Portal(new Vector3(6,0,8),true);
            Box("Kitchen cupboards",new Vector3(-4.65f,.5f,8.65f),new Vector3(1.35f,1,2.6f),wood,true);
            Box("Cream counter top",new Vector3(-4.65f,1.06f,8.65f),new Vector3(1.5f,.12f,2.7f),cream);
            for(int i=0;i<3;i++)Box("Cupboard inset",new Vector3(-3.96f,.51f,7.85f+i*.8f),new Vector3(.035f,.68f,.65f),mint);
            Art.Cylinder("Sink",root,new Vector3(-4.65f,1.13f,9.2f),new Vector3(.65f,.02f,.65f),Art.Mat("Sink water","#83B5C9"));
            Box("Kitchen island",new Vector3(.25f,.45f,7.95f),new Vector3(1.3f,.9f,1.2f),clay,true);
            Box("Island top",new Vector3(.25f,.95f,7.95f),new Vector3(1.5f,.12f,1.4f),cream);
            Art.Cylinder("Fruit bowl",root,new Vector3(.25f,1.06f,7.95f),new Vector3(.48f,.08f,.48f),wood);
            for(int i=0;i<3;i++)Art.Sphere("Island fruit",root,new Vector3(.12f+i*.12f,1.18f,7.95f),Vector3.one*.17f,clay);
            Box("Quiet reading bench",new Vector3(10.8f,.45f,7.35f),new Vector3(.8f,.9f,1.8f),wood,true);
            Box("Reading cushion",new Vector3(10.75f,.98f,7.35f),new Vector3(.9f,.18f,1.75f),cream);
            Box("Quiet bookcase",new Vector3(8,.55f,9.85f),new Vector3(.9f,1.1f,.5f),wood,true);
            for(int i=0;i<5;i++)Box("Bedtime book",new Vector3(7.7f+i*.14f,.75f,9.55f),new Vector3(.1f,.6f,.3f),i%2==0?clay:mint);
            Art.Cylinder("Quiet room rug",root,new Vector3(8.3f,.014f,7.6f),new Vector3(2.1f,.012f,1.9f),cream);
            // Two paths and deliberately different obstacles make the courtyard a place to explore.
            for(int i=0;i<9;i++)foreach(float x in new[]{3f,9.6f})
                Box("Garden stepping stone",new Vector3(x+Mathf.Sin(i*.9f)*.25f,.012f,11.7f+i*.9f),new Vector3(.72f,.045f,.65f),cream);
            FlowerBed(new Vector3(-3.55f,0,14.5f),new Vector3(2.4f,.4f,2));
            FlowerBed(new Vector3(7.85f,0,17.3f),new Vector3(1.6f,.4f,1.8f));
            foreach(float x in new[]{-6f,12f})Box("Garden side fence",new Vector3(x,.65f,16),new Vector3(.14f,1.3f,10),wood,true);
            Box("Garden back fence",new Vector3(3,.65f,21),new Vector3(18,1.3f,.14f),wood,true);
            for(int i=0;i<26;i++)Box("Garden fence cap",new Vector3(-5.7f+i*.68f,.73f,20.9f),new Vector3(.1f,1.45f,.16f),cream);
            FoodBag(Room.IndoorBag,"indoor-food");FoodBag(Room.OutdoorBag,"outdoor-food");
            BathStation.Build(parent);
        }
        static void SplitWall(float z)
        {
            foreach(var segment in new[]{new Vector2(-6,1.5f),new Vector2(4.5f,7.5f),new Vector2(10.5f,12)})
                Box("Garden room wall",new Vector3((segment.x+segment.y)/2,1.65f,z),new Vector3(segment.y-segment.x,3.3f,.18f),plaster);
            Portal(new Vector3(3,0,z),false);Portal(new Vector3(9,0,z),false);
        }
        static void Portal(Vector3 p,bool side)
        {
            var t=Art.Pivot("Open garden doorway",root,p);if(side)t.localRotation=Quaternion.Euler(0,90,0);
            foreach(float x in new[]{-1.5f,1.5f})Art.Cube("Door frame",t,new Vector3(x,1.35f,0),new Vector3(.14f,2.7f,.25f),wood);
            Art.Cube("Door header",t,new Vector3(0,2.8f,0),new Vector3(3.2f,.25f,.28f),clay);
            Art.Cube("Wall above door",t,new Vector3(0,3.15f,0),new Vector3(3,.3f,.18f),plaster);
            var door=Art.Pivot("Open door leaf",t,new Vector3(-1.45f,0,-.16f));door.localRotation=Quaternion.Euler(0,180,0);
            Art.Cube("Mint open door",door,new Vector3(.7f,1.3f,0),new Vector3(1.4f,2.6f,.08f),mint);
            Art.Sphere("Door handle",door,new Vector3(1.2f,1.25f,-.08f),Vector3.one*.09f,wood);
        }
        static void FlowerBed(Vector3 p,Vector3 size)
        {
            Box("Raised flower bed",p+Vector3.up*.2f,size,clay,true);
            Box("Garden soil",p+Vector3.up*.41f,new Vector3(size.x-.15f,.04f,size.z-.15f),Art.Mat("Garden soil","#8B6848"));
            for(int i=0;i<6;i++)
            {
                var q=p+new Vector3((i%3-1)*size.x*.27f,.6f,(i/3-.5f)*size.z*.5f);
                Art.Sphere("Round garden leaf",root,q,new Vector3(.42f,.5f,.4f),leaf);
                Art.Sphere("Garden blossom",root,q+Vector3.up*.25f,Vector3.one*.22f,i%2==0?cream:clay);
            }
        }
        static void FoodBag(Point point,string kind)
        {
            var t=Art.Pivot("Cat food bag "+kind,root,new Vector3(point.x,0,point.z));
            Art.Sphere("Soft food sack",t,new Vector3(0,.37f,0),new Vector3(.66f,.74f,.5f),cream);
            Art.Cube("Folded bag top",t,new Vector3(0,.75f,0),new Vector3(.56f,.13f,.22f),clay);
            Art.Cube("Food bag label",t,new Vector3(0,.38f,-.235f),new Vector3(.48f,.38f,.025f),mint);
            Art.Sphere("Bag paw pad",t,new Vector3(0,.35f,-.26f),new Vector3(.17f,.14f,.025f),wood);
            for(int i=0;i<3;i++)Art.Sphere("Bag paw toe",t,new Vector3((i-1)*.095f,.47f,-.26f),new Vector3(.07f,.08f,.025f),wood);
            var collider=t.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,.4f,0);collider.size=new Vector3(.78f,.9f,.65f);
            t.gameObject.AddComponent<TouchTarget>().kind=kind;
        }
    }
}
