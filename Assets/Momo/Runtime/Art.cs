using UnityEngine;
using UnityEngine.Rendering;

namespace Momo
{
    public static class Art
    {
        public static Material Mat(string name,string hex,float smooth=.15f)
        {ColorUtility.TryParseHtmlString(hex,out Color color);var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Glossiness",smooth);return m;}
        public static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material material,bool collider=false)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)Object.Destroy(g.GetComponent<Collider>());return g;
        }
        public static Transform Pivot(string name,Transform parent,Vector3 pos)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=pos;return g.transform;}
        public static void Cube(string name,Transform p,Vector3 pos,Vector3 size,Material m,bool collider=false){Shape(name,PrimitiveType.Cube,p,pos,size,m,collider);}
        public static void Sphere(string name,Transform p,Vector3 pos,Vector3 size,Material m){Shape(name,PrimitiveType.Sphere,p,pos,size,m);}
        public static void Cylinder(string name,Transform p,Vector3 pos,Vector3 size,Material m){Shape(name,PrimitiveType.Cylinder,p,pos,size,m);}
        public static void Ear(Transform p,Vector3 pos,Material outer,Material inner)
        {
            var root=Pivot("Ear",p,pos);
            var mesh=new Mesh { vertices=new[]{new Vector3(-.17f,0,0),new Vector3(.17f,0,0),new Vector3(.03f,.34f,.015f),new Vector3(0,.04f,-.15f)},triangles=new[]{0,2,1,0,3,2,1,2,3,0,1,3} };mesh.RecalculateNormals();
            var g=new GameObject("Ear shell",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=outer;
            var inside=new GameObject("Inner ear",typeof(MeshFilter),typeof(MeshRenderer));inside.transform.SetParent(root,false);inside.transform.localPosition=new Vector3(0,.05f,.012f);inside.transform.localScale=Vector3.one*.65f;inside.GetComponent<MeshFilter>().sharedMesh=mesh;inside.GetComponent<MeshRenderer>().sharedMaterial=inner;
        }
    }
    public sealed class RoomView : MonoBehaviour
    {
        public Transform ball,food,water,feather;
        Transform ballBasket,windowCushion,bedBlanket;
        Rigidbody ballBody;
        Momo.Core.Point observedBall;
        bool ballInitialized;
        public Light sun;
        public Camera cameraView;
        Material cream,wood,darkWood,terracotta,sage,pale,ivory;
        public void Build()
        {
            cream=Art.Mat("Warm plaster","#E9DDC8");wood=Art.Mat("Honey oak","#B38C66");darkWood=Art.Mat("Oak edge","#80694F");terracotta=Art.Mat("Clay","#B5674A");sage=Art.Mat("Sage linen","#8B9C86");pale=Art.Mat("Canvas","#D4C5AB");ivory=Art.Mat("Milk ceramic","#FFF3D8");
            var root=transform;
            Art.Cube("Diorama base",root,new Vector3(0,-.24f,0),new Vector3(12.2f,.45f,10.2f),wood);
            Art.Cube("Floor",root,new Vector3(0,-.035f,0),new Vector3(12,.07f,10f),pale,true);
            var grain=Art.Mat("Floor join","#C5B69B");
            for(int i=-11;i<=11;i++)Art.Cube("Floorboard joint",root,new Vector3(i*.5f,.006f,0),new Vector3(.008f,.006f,9.9f),grain);
            // North walls are split into real doorways by HomeExpansion.
            Art.Cube("Side wall",root,new Vector3(-6,1.65f,0),new Vector3(.18f,3.3f,10f),cream);
            BuildPlayRoom(root);
            HomeExpansion.Build(root);
            foreach(var c in GetComponentsInChildren<Collider>())if(c.name=="Floor"||c.name=="Playroom floor"){var floorTarget=c.gameObject.AddComponent<TouchTarget>();floorTarget.kind="floor";}
            Art.Cube("Side skirting",root,new Vector3(-5.88f,.13f,0),new Vector3(.08f,.25f,10f),ivory);
            Art.Cylinder("Round woven rug",root,new Vector3(.1f,.017f,-.35f),new Vector3(5.0f,.012f,4.4f),Art.Mat("Rug","#DAC08F"));
            Art.Cylinder("Rug center",root,new Vector3(.1f,.032f,-.35f),new Vector3(4.6f,.008f,4.0f),Art.Mat("Rug weave","#E2CCA4"));
            Window(root);Sofa(root);Bed(root);Bowl(root,new Vector3(4.5f,.08f,2.9f),false);Bowl(root,new Vector3(4.5f,.08f,1.7f),true);Box(root);Plants(root);Decor(root);FamiliarCorners(root);ViewingWindow(root);
            ball=Art.Shape("Red ball",PrimitiveType.Sphere,root,new Vector3(1.4f,.16f,-1.8f),Vector3.one*.31f,Art.Mat("Red rubber","#BD5142",.3f),true).transform;
            var tag=ball.gameObject.AddComponent<TouchTarget>();tag.kind="ball";
            ballBody=ball.gameObject.AddComponent<Rigidbody>();ballBody.mass=.12f;ballBody.linearDamping=.9f;ballBody.angularDamping=.6f;ballBody.interpolation=RigidbodyInterpolation.Interpolate;ballBody.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            foreach(var boundary in new[]{new Vector3(11.6f,.3f,8),new Vector3(-5.6f,.3f,8),new Vector3(3,.3f,20.6f),new Vector3(3,.3f,-4.4f)})
            {bool side=boundary.x!=3;var edge=Art.Shape("Invisible toy boundary",PrimitiveType.Cube,root,boundary,side?new Vector3(.1f,.6f,25):new Vector3(17.2f,.6f,.1f),wood,true);edge.GetComponent<Renderer>().enabled=false;}
            for(int solidIndex=2;solidIndex<Momo.Core.Room.Solids.Length;solidIndex++)
            {var solid=Momo.Core.Room.Solids[solidIndex];var edge=Art.Shape("Toy furniture boundary",PrimitiveType.Cube,root,new Vector3((solid.left+solid.right)/2,.25f,(solid.bottom+solid.top)/2),new Vector3(solid.right-solid.left,.5f,solid.top-solid.bottom),wood,true);edge.GetComponent<Renderer>().enabled=false;}
            Art.Cylinder("Ball seam",ball,Vector3.zero,new Vector3(1.015f,.05f,1.015f),ivory);
            feather=Art.Pivot("Feather wand",root,new Vector3(0,.5f,0));
            var stem=Art.Shape("Wand stem",PrimitiveType.Cylinder,feather,new Vector3(0,.08f,0),new Vector3(.035f,.36f,.035f),darkWood);
            stem.transform.localRotation=Quaternion.Euler(0,0,24);
            var featherA=Art.Shape("Sage feather",PrimitiveType.Sphere,feather,new Vector3(-.13f,.34f,0),new Vector3(.13f,.40f,.07f),sage);
            featherA.transform.localRotation=Quaternion.Euler(0,0,32);
            var featherB=Art.Shape("Clay feather",PrimitiveType.Sphere,feather,new Vector3(.10f,.38f,.02f),new Vector3(.12f,.34f,.07f),terracotta);
            featherB.transform.localRotation=Quaternion.Euler(0,0,-28);
            feather.gameObject.SetActive(false);
            var cameraObj=new GameObject("Window into Momo's home",typeof(Camera),typeof(AudioListener));cameraView=cameraObj.GetComponent<Camera>();cameraView.tag="MainCamera";
            cameraView.orthographic=true;cameraView.orthographicSize=5.7f;cameraView.transform.position=new Vector3(10,10,-13);cameraView.transform.LookAt(new Vector3(0,.6f,0));cameraView.clearFlags=CameraClearFlags.SolidColor;cameraView.backgroundColor=new Color(.89f,.87f,.81f);cameraView.nearClipPlane=.1f;cameraView.farClipPlane=60;
            sun=new GameObject("Afternoon light",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(40,-30,0);sun.color=new Color(1,.91f,.72f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.35f;sun.shadowBias=.03f;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.68f,.72f,.75f);QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.Medium;QualitySettings.shadowDistance=35;QualitySettings.antiAliasing=4;
        }
        void Window(Transform p)
        {
            var glass=Art.Mat("Blue sky","#AAC6C4");glass.EnableKeyword("_EMISSION");glass.SetColor("_EmissionColor",new Color(.15f,.24f,.25f));
            Art.Cube("Window sky",p,new Vector3(.2f,2,4.87f),new Vector3(2.6f,1.75f,.05f),glass);
            foreach(float x in new[]{-1.15f,.2f,1.55f})Art.Cube("Window mullion",p,new Vector3(x,2,4.79f),new Vector3(.10f,1.88f,.12f),ivory);
            foreach(float y in new[]{1.1f,2,2.9f})Art.Cube("Window frame",p,new Vector3(.2f,y,4.79f),new Vector3(2.8f,.10f,.13f),ivory);
            Art.Cube("Window sill",p,new Vector3(.2f,1.02f,4.64f),new Vector3(3,.12f,.55f),wood);
            Art.Cube("Sun patch",p,new Vector3(.65f,.012f,2.7f),new Vector3(1.8f,.009f,2.25f),Art.Mat("Sunlight on oak","#EBD8AE"));
            for(int i=0;i<2;i++)for(int j=0;j<2;j++)Art.Cube("Sun window reflection",p,new Vector3(.15f+i*.93f,.018f,2.18f+j*1.05f),new Vector3(.04f,.008f,1),pale);
        }
        void ViewingWindow(Transform p)
        {
            // Four solid wall sections leave a real opening, with deep reveals and a sill.
            var facade=Art.Mat("Exterior warm plaster","#C4B59D");
            foreach(float x in new[]{-11f,11f})Art.Cube("Outside wall",p,new Vector3(x,2,-5f),new Vector3(16.4f,24,.38f),facade,true);
            Art.Cube("Outside wall above",p,new Vector3(0,9.75f,-5f),new Vector3(5.6f,12,.38f),facade,true);
            Art.Cube("Outside wall below",p,new Vector3(0,-5.875f,-5f),new Vector3(5.6f,12.25f,.38f),facade,true);
            foreach(float x in new[]{-2.8f,2.8f})
            {
                Art.Cube("Deep window jamb",p,new Vector3(x,2,-5.05f),new Vector3(.18f,3.65f,.64f),darkWood,true);
                Art.Cube("Outer window trim",p,new Vector3(x,2,-5.42f),new Vector3(.30f,3.95f,.1f),wood);
            }
            foreach(float y in new[]{.25f,3.75f})Art.Cube("Window lintel",p,new Vector3(0,y,-5.06f),new Vector3(5.8f,.18f,.66f),darkWood,true);
            Art.Cube("Broad outside sill",p,new Vector3(0,.18f,-5.4f),new Vector3(6.1f,.2f,1.0f),wood,true);
            foreach(float x in new[]{-3.45f,3.45f})
            {
                Art.Cube("Open sage shutter",p,new Vector3(x,2,-5.32f),new Vector3(.95f,3.35f,.12f),sage);
                for(int i=0;i<9;i++)Art.Cube("Shutter slat",p,new Vector3(x,.6f+i*.34f,-5.41f),new Vector3(.85f,.08f,.1f),wood);
            }
        }
        void Sofa(Transform p)
        {
            var r=Art.Pivot("Linen sofa",p,new Vector3(-4.7f,0,2.0f));
            foreach(float x in new[]{-.7f,.7f})foreach(float z in new[]{-.55f,.55f})Art.Cylinder("Sofa foot",r,new Vector3(x,.14f,z),new Vector3(.12f,.14f,.12f),darkWood);
            Art.Cube("Sofa base",r,new Vector3(0,.53f,0),new Vector3(1.7f,.35f,1.45f),sage,true);
            Art.Cube("Sofa back",r,new Vector3(-.69f,.96f,0),new Vector3(.32f,.9f,1.45f),sage);
            foreach(float z in new[]{-.67f,.67f})Art.Cube("Sofa arm",r,new Vector3(0,.72f,z),new Vector3(1.7f,.48f,.22f),sage);
            for(int i=0;i<2;i++)Art.Sphere("Soft seat cushion",r,new Vector3(.13f,.71f,-.32f+i*.64f),new Vector3(1.37f,.23f,.66f),Art.Mat("Cushion "+i,"#9CAC95"));
            var cushion=Art.Shape("Small pillow",PrimitiveType.Cube,r,new Vector3(-.3f,.95f,.32f),new Vector3(.25f,.48f,.48f),terracotta);cushion.transform.localRotation=Quaternion.Euler(10,0,-15);
        }
        void Bed(Transform p)
        {
            Art.Cylinder("Woven cat basket",p,new Vector3(-4,.13f,-2.8f),new Vector3(1.3f,.13f,1.1f),wood);
            Art.Sphere("Soft round bed",p,new Vector3(-4,.23f,-2.8f),new Vector3(1.2f,.27f,1),ivory);
        }
        void Bowl(Transform p,Vector3 pos,bool isWater)
        {
            Art.Cylinder(isWater?"Water bowl":"Food bowl",p,pos,new Vector3(.57f,.09f,.57f),isWater?sage:terracotta);
            var contents=Art.Shape("Bowl contents",PrimitiveType.Cylinder,p,pos+Vector3.up*.08f,new Vector3(.44f,.008f,.44f),Art.Mat(isWater?"Water":"Kibble",isWater?"#89B7B5":"#785138"));
            if(isWater)water=contents.transform;else food=contents.transform;
        }
        void BuildPlayRoom(Transform p)
        {
            var blue=Art.Mat("Playroom blue","#87AFAE");var linen=Art.Mat("Blue linen","#BED3CC");
            Art.Cube("Playroom floor",p,new Vector3(9,-.035f,0),new Vector3(6,.07f,10),wood,true);
            Art.Cube("Playroom base",p,new Vector3(9,-.24f,0),new Vector3(6.2f,.45f,10.2f),wood);
            // Shared north doorway leads to the quiet room.
            Art.Cube("Playroom right wall",p,new Vector3(12,1.65f,0),new Vector3(.18f,3.3f,10),blue);
            Art.Cube("Playroom front wall",p,new Vector3(9,1.65f,-5),new Vector3(6.1f,3.3f,.18f),blue);
            foreach(float z in new[]{-3.25f,3.25f})Art.Cube("Door partition",p,new Vector3(6,1.65f,z),new Vector3(.18f,3.3f,3.5f),cream);
            Art.Cube("Door lintel",p,new Vector3(6,3.05f,0),new Vector3(.25f,.5f,3),wood);
            foreach(float z in new[]{-1.5f,1.5f})Art.Cube("Door jamb",p,new Vector3(6,1.4f,z),new Vector3(.27f,2.8f,.12f),wood);
            Art.Cylinder("Playroom round mat",p,new Vector3(9,.02f,0),new Vector3(4,.015f,4),linen);
            Art.Cube("Low hide cabinet",p,new Vector3(9.7f,.72f,2.8f),new Vector3(2.2f,1.44f,.9f),wood,true);
            for(int i=0;i<3;i++)Art.Cube("Cabinet cubby",p,new Vector3(9.0f+i*.7f,.7f,2.33f),new Vector3(.52f,.7f,.035f),darkWood);
            // Soft play tunnel: open at both ends, with enough clearance to see a peeking face.
            foreach(float x in new[]{8.35f,10.25f})Art.Cube("Tunnel side",p,new Vector3(x,.55f,-2.6f),new Vector3(.16f,1.1f,1.5f),linen,true);
            Art.Cube("Tunnel roof",p,new Vector3(9.3f,1.15f,-2.6f),new Vector3(2.05f,.16f,1.5f),linen);
            foreach(float x in new[]{8.9f,9.7f})Art.Sphere("Soft play cushion",p,new Vector3(x,.13f,.6f),new Vector3(.65f,.23f,.65f),sage);
        }
        void Box(Transform p)
        {
            var r=Art.Pivot("Cardboard hideaway",p,new Vector3(3.7f,0,-3.2f));var box=Art.Mat("Cardboard","#BF986C");
            Art.Cube("Box base",r,new Vector3(0,.05f,0),new Vector3(1.05f,.08f,.9f),box);
            foreach(float x in new[]{-.5f,.5f})Art.Cube("Box side",r,new Vector3(x,.33f,0),new Vector3(.06f,.6f,.9f),box);
            Art.Cube("Box back",r,new Vector3(0,.33f,-.42f),new Vector3(1,.6f,.06f),box);
            var flap=Art.Shape("Open box flap",PrimitiveType.Cube,r,new Vector3(-.7f,.62f,0),new Vector3(.4f,.04f,.9f),box);flap.transform.localRotation=Quaternion.Euler(0,0,20);
        }
        void Plants(Transform p)
        {
            var leaf=Art.Mat("Leaves","#57795A");
            foreach(var pos in new[]{new Vector3(4.5f,0,4.45f),new Vector3(-5,0,-4.05f)})
            {
                Art.Cylinder("Terracotta planter",p,pos+Vector3.up*.26f,new Vector3(.55f,.27f,.55f),terracotta);
                for(int i=0;i<7;i++)
                {
                    float a=i*2.4f;var stem=Art.Shape("Leaf",PrimitiveType.Sphere,p,pos+new Vector3(Mathf.Cos(a)*.24f,.8f+i*.07f,Mathf.Sin(a)*.22f),new Vector3(.24f,.65f,.1f),leaf);stem.transform.rotation=Quaternion.Euler(20,a*Mathf.Rad2Deg,i%2==0?35:-35);
                }
            }
        }
        void Decor(Transform p)
        {
            Art.Cube("Picture frame",p,new Vector3(-4.7f,2.25f,4.85f),new Vector3(.8f,1,.12f),wood);
            Art.Cube("Picture paper",p,new Vector3(-4.7f,2.25f,4.77f),new Vector3(.67f,.86f,.03f),ivory);
            Art.Sphere("Picture sun",p,new Vector3(-4.7f,2.35f,4.73f),new Vector3(.36f,.36f,.02f),terracotta);
            Art.Cube("Shelf",p,new Vector3(-5.68f,2.15f,-.6f),new Vector3(.52f,.08f,1.5f),wood);
            for(int i=0;i<5;i++)Art.Cube("Book",p,new Vector3(-5.63f,2.38f,-1.15f+i*.19f),new Vector3(.27f,.4f,.14f),i%2==0?sage:terracotta);
            Art.Cylinder("Scratching post base",p,new Vector3(-1.2f,.05f,4.1f),new Vector3(.55f,.04f,.55f),wood);
            Art.Cylinder("Scratching post",p,new Vector3(-1.2f,.5f,4.1f),new Vector3(.16f,.45f,.16f),pale);
        }
        void FamiliarCorners(Transform p)
        {
            var toy=Art.Pivot("A familiar red ball",p,new Vector3(2.1f,0,-3.4f));
            Art.Cylinder("Woven toy basket",toy,new Vector3(0,.13f,0),new Vector3(.48f,.14f,.48f),wood);
            Art.Sphere("Kept red ball",toy,new Vector3(-.1f,.31f,0),Vector3.one*.22f,Art.Mat("Kept red rubber","#BD5142",.3f));
            Art.Sphere("Kept cream ball",toy,new Vector3(.13f,.29f,.04f),Vector3.one*.18f,ivory);
            ballBasket=toy;ballBasket.gameObject.SetActive(false);

            var cushion=Art.Pivot("A quiet window cushion",p,new Vector3(.35f,0,4.05f));
            Art.Cylinder("Window cushion base",cushion,new Vector3(0,.065f,0),new Vector3(.82f,.07f,.48f),sage);
            Art.Sphere("Window cushion softness",cushion,new Vector3(0,.12f,0),new Vector3(.75f,.14f,.41f),Art.Mat("Window cushion","#AAB99B"));
            windowCushion=cushion;windowCushion.gameObject.SetActive(false);

            var blanket=Art.Pivot("A shared blanket",p,new Vector3(-4,.29f,-2.8f));
            Art.Cylinder("Blanket",blanket,Vector3.zero,new Vector3(.9f,.018f,.72f),Art.Mat("Soft blanket","#D9A998"));
            Art.Cylinder("Blanket center",blanket,new Vector3(0,.013f,0),new Vector3(.75f,.007f,.58f),Art.Mat("Soft blanket fold","#E6BBAA"));
            bedBlanket=blanket;bedBlanket.gameObject.SetActive(false);
        }
        public void ShowFeather(bool visible)
        {if(feather!=null)feather.gameObject.SetActive(visible);}
        public void MoveFeather(Momo.Core.Point point)
        {if(feather!=null)feather.position=new Vector3(point.x,.46f+Mathf.Sin(Time.unscaledTime*7f)*.06f,point.z);}
        Renderer[] occluders;
        public bool Unobstructed(Vector3 viewer,Vector3 pet)
        {
            if(occluders==null)occluders=GetComponentsInChildren<Renderer>(true);
            Vector3 delta=pet-viewer;float distance=delta.magnitude;var ray=new Ray(viewer,delta.normalized);
            foreach(var r in occluders)
                if(r!=null&&r.enabled&&r.gameObject.activeInHierarchy&&r.bounds.IntersectRay(ray,out float enter)&&enter>0&&enter<distance-.25f)return false;
            return true;
        }
        public void UpdateWorld(Momo.Core.WorldState s,float dt)
        {
            if(!ballInitialized)
            {ballBody.position=new Vector3(s.ball.x,.17f,s.ball.z);observedBall=s.ball;ballInitialized=true;}
            else if(Momo.Core.Point.Distance(s.ball,observedBall)>.01f)
            {
                Vector3 delta=new Vector3(s.ball.x,.17f,s.ball.z)-ballBody.position;
                if(delta.magnitude>2.5f){ballBody.position=new Vector3(s.ball.x,.17f,s.ball.z);ballBody.linearVelocity=Vector3.zero;}
                else ballBody.linearVelocity=new Vector3(delta.x,0,delta.z)*1.5f;
            }
            Vector3 bp=ballBody.position;
            if(bp.y<-.5f||bp.x<Momo.Core.Room.MinX-.1f||bp.x>Momo.Core.Room.MaxX+.1f||bp.z<Momo.Core.Room.MinZ-.1f||bp.z>Momo.Core.Room.MaxZ+.1f)
            {bp=new Vector3(Mathf.Clamp(bp.x,Momo.Core.Room.MinX,Momo.Core.Room.MaxX),.17f,Mathf.Clamp(bp.z,Momo.Core.Room.MinZ,Momo.Core.Room.MaxZ));ballBody.position=bp;ballBody.linearVelocity=Vector3.zero;}
            s.ball=new Momo.Core.Point(bp.x,bp.z);s.ballTrapped=!Momo.Core.Room.Walkable(s.ball);observedBall=s.ball;
            food.localScale=new Vector3(.44f*Mathf.Sqrt(s.food/100),.008f,.44f*Mathf.Sqrt(s.food/100));
            water.localScale=new Vector3(.44f*Mathf.Sqrt(s.water/100),.008f,.44f*Mathf.Sqrt(s.water/100));
            ballBasket.gameObject.SetActive(s.ballPreference>=4);
            windowCushion.gameObject.SetActive(s.windowPreference>=1);
            bedBlanket.gameObject.SetActive(s.affection>=50);
            float day=(float)(s.seconds/3600+9)%24;float brightness=day>=7&&day<19?.9f:.42f;sun.intensity=Mathf.Lerp(sun.intensity,brightness,dt*.3f);
        }
    }
    public sealed class TouchTarget : MonoBehaviour { public string kind; }
}
