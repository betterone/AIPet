using System.Collections.Generic;
using UnityEngine;

namespace Momo
{
    // Shared presentation only. Production keeps walls, collider dimensions and saved coordinates.
    public sealed class ToyHomeStyle : MonoBehaviour
    {
        public bool Applied { get; private set; }
        readonly Dictionary<string, Material> palette = new Dictionary<string, Material>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        Material Mat(string hex)
        {
            if (!palette.TryGetValue(hex, out var m))
            {
                m = Art.Mat("Toy home " + hex, hex, .08f); palette.Add(hex, m); ownedMaterials.Add(m);
            }
            return m;
        }

        public void Apply(RoomView room, bool cutaway = false)
        {
            if (Applied) return;
            Applied = true;
            var colors = new Dictionary<string, string> {
                {"Warm plaster", "#E8E5D5"}, {"Kitchen lemon plaster", "#E8E5D5"},
                {"Exterior warm plaster", "#D2CCB8"}, {"Garden mint", "#BED3C8"},
                {"Garden grass", "#91B58B"}, {"Sink water", "#91B8C3"},
                {"Water", "#91B8C3"}, {"Kibble", "#80634A"},
                {"Window cushion", "#BED3C8"}, {"Soft blanket", "#D3A99A"},
                {"Soft blanket fold", "#E0BFB1"}, {"Kept red rubber", "#CA7866"}, {"Honey oak", "#C49C72"}, {"Oak edge", "#947454"},
                {"Clay", "#D28A72"}, {"Sage linen", "#619AA8"}, {"Canvas", "#D8C7A9"},
                {"Milk ceramic", "#F3F2E8"}, {"Floor join", "#C7B595"}, {"Rug", "#79A69A"},
                {"Rug weave", "#A8C4B5"}, {"Cushion 0", "#84B4BE"}, {"Cushion 1", "#84B4BE"},
                {"Playroom blue", "#9CBCC2"}, {"Blue linen", "#BED3C8"}, {"Cardboard", "#C6A580"},
                {"Leaves", "#56866D"}, {"Red rubber", "#CA7866"}, {"Sunlight on oak", "#E3D7B5"}
            };
            foreach (var r in room.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && colors.TryGetValue(m.name, out var color)) r.sharedMaterial = Mat(color);
                if(r.name=="Playroom floor")r.sharedMaterial=Mat("#91B58B");
                if (cutaway && (r.name == "Side wall" || r.name == "Playroom right wall" || r.name == "Playroom back wall" || r.name == "Door partition"))
                {
                    var scale = r.transform.localScale; scale.y = 1.05f; r.transform.localScale = scale;
                    var p = r.transform.localPosition; p.y = .525f; r.transform.localPosition = p;
                }
                // Small real bevels, preserving dimensions and the existing furniture footprint.
                var filter = r.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "Cube")
                {
                    var size = r.transform.localScale;
                    if (Mathf.Min(size.x, size.y, size.z) > .045f)
                    {
                        filter.sharedMesh = RoundedBox(size, Mathf.Min(.09f, Mathf.Min(size.x, size.y, size.z) * .22f));
                        var collider = r.GetComponent<BoxCollider>(); if (collider != null) collider.size = size;
                        r.transform.localScale = Vector3.one;
                    }
                }
            }
            var root = room.transform;
            // The courtyard is now playable: decoration follows its shared obstacle footprints.
            Box("Floating earth plinth", root, new Vector3(3,-.65f,8), new Vector3(20.2f,.85f,28.4f), "#AB8968", .3f);
            Box("Meadow border", root, new Vector3(3,-.25f,8), new Vector3(20.3f,.2f,28.5f), "#91B58B", .18f);
            Tree(root,new Vector3(-3.5f,0,18),1.35f);Tree(root,new Vector3(9.8f,0,14.4f),1.2f);
            for(int i=0;i<8;i++)
            {
                Tree(root,new Vector3(-5.5f+i*2.45f,0,22),i%2==0?1.2f:.95f);
                Flower(root,new Vector3(-5.6f+i*2.4f,0,20.5f),i%2==0);
            }
            Tree(root,new Vector3(-6.8f,0,8),1.15f);Tree(root,new Vector3(12.8f,0,8),1.35f);
            // Cat-ear canopy: the signature of this little home, above the existing doorway.
            Box("Sunny portal lintel",root,new Vector3(6,2.7f,0),new Vector3(.48f,.48f,3.45f),"#D9BA76",.1f);
            foreach(float z in new[]{-1.62f,1.62f})
            {
                Box("Sunny portal post",root,new Vector3(6,1.27f,z),new Vector3(.48f,2.55f,.32f),"#D9BA76",.08f);
                var ear=new GameObject("Portal cat ear",typeof(MeshFilter),typeof(MeshRenderer));ear.transform.SetParent(root,false);
                ear.transform.localPosition=new Vector3(6,2.92f,z*.82f);
                var earMesh=new Mesh{name="Cat ear prism"};
                earMesh.vertices=new[]{new Vector3(-.22f,0,-.35f),new Vector3(-.22f,.65f,0),new Vector3(-.22f,0,.35f),new Vector3(.22f,0,-.35f),new Vector3(.22f,.65f,0),new Vector3(.22f,0,.35f)};
                earMesh.triangles=new[]{0,2,1,5,3,4,0,1,4,0,4,3,1,2,5,1,5,4,0,3,5,0,5,2};earMesh.RecalculateNormals();earMesh.RecalculateBounds();ownedMeshes.Add(earMesh);
                ear.GetComponent<MeshFilter>().sharedMesh=earMesh;ear.GetComponent<MeshRenderer>().sharedMaterial=Mat("#D9BA76");
                Box("Portal coral cheek",root,new Vector3(5.75f,2.65f,z*.76f),new Vector3(.045f,.12f,.27f),"#D28A72",.025f);
            }
            // A miniature tiled awning gives the back window the toy-cottage silhouette.
            for(int row=0;row<3;row++)for(int i=0;i<9;i++)
            {
                var tile=Box("Window awning tile",root,new Vector3(-1.5f+i*.43f,3.35f-row*.11f,4.91f-row*.33f),new Vector3(.45f,.13f,.42f),row%2==0?"#D28A72":"#DEA18B",.055f);
                tile.transform.localRotation=Quaternion.Euler(-18,0,0);
            }
            for(int i=0;i<6;i++) Flower(root,new Vector3(-1.0f+i*.47f,1.1f,4.4f),i%2==0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.62f,.70f,.76f);
            RenderSettings.ambientEquatorColor=new Color(.57f,.63f,.62f);
            RenderSettings.ambientGroundColor=new Color(.32f,.35f,.36f);
            room.sun.intensity=.9f;room.sun.shadowStrength=.38f;
            room.sun.color=new Color(1,.98f,.94f);room.sun.transform.rotation=Quaternion.Euler(48,-32,0);
            room.sun.shadowBias=.025f;room.sun.shadowNormalBias=.25f;
            QualitySettings.shadowResolution=UnityEngine.ShadowResolution.High;
            room.cameraView.backgroundColor=new Color(.76f,.85f,.87f);
        }

        GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,string hex,float radius)
        {
            var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=pos;
            g.GetComponent<MeshFilter>().sharedMesh=RoundedBox(size,radius);g.GetComponent<MeshRenderer>().sharedMaterial=Mat(hex);return g;
        }
        void Bush(Transform root,Vector3 pos)
        {
            for(int i=0;i<3;i++)
            {
                var g=Art.Shape("Low polygon shrub",PrimitiveType.Sphere,root,pos+new Vector3((i-1)*.25f,.3f+(i%2)*.15f,0),new Vector3(.7f,.7f,.65f),Mat(i%2==0?"#5E9F63":"#78B865"));
                g.GetComponent<MeshFilter>().sharedMesh=OctaSphere();
            }
        }
        void Tree(Transform root,Vector3 pos,float scale)
        {
            var t=Art.Pivot("Toy orchard tree",root,pos);t.localScale=Vector3.one*scale;
            Art.Cylinder("Tree trunk",t,new Vector3(0,.5f,0),new Vector3(.19f,.5f,.19f),Mat("#947454"));
            var crown=Art.Shape("Faceted orchard crown",PrimitiveType.Sphere,t,new Vector3(0,1.43f,0),new Vector3(1.15f,1.7f,1.15f),Mat("#56866D"));
            crown.GetComponent<MeshFilter>().sharedMesh=OctaSphere();
            foreach(float x in new[]{-.35f,.27f})Art.Sphere("Orchard fruit",t,new Vector3(x,1.45f,-.43f),Vector3.one*.18f,Mat("#D9BA76"));
        }
        void Flower(Transform root,Vector3 pos,bool yellow)
        {
            Art.Cylinder("Flower stem",root,pos+Vector3.up*.12f,new Vector3(.025f,.14f,.025f),Mat("#56866D"));
            for(int i=0;i<5;i++)
            {
                float angle=i*Mathf.PI*2/5;
                Art.Sphere("Five petal flower",root,pos+new Vector3(Mathf.Cos(angle)*.085f,.27f,Mathf.Sin(angle)*.085f),new Vector3(.13f,.065f,.13f),Mat(yellow?"#D9BA76":"#F3F2E8"));
            }
            Art.Sphere("Flower center",root,pos+Vector3.up*.29f,Vector3.one*.08f,Mat("#D28A72"));
        }

        Mesh OctaSphere()
        {
            // Two eight-sided rings and a cap: flat normals, deliberately unlike a smooth primitive.
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int ring=0;ring<3;ring++)for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI/4;float radius=ring==1?.5f:.32f;
                vertices.Add(new Vector3(Mathf.Cos(angle)*radius,(ring-1)*.35f,Mathf.Sin(angle)*radius));
            }
            for(int ring=0;ring<2;ring++)for(int i=0;i<8;i++)
            {
                int a=ring*8+i,b=ring*8+(i+1)%8,c=(ring+1)*8+i,d=(ring+1)*8+(i+1)%8;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            vertices.Add(new Vector3(0,-.5f,0));vertices.Add(new Vector3(0,.5f,0));
            for(int i=0;i<8;i++)triangles.AddRange(new[]{24,i,(i+1)%8,25,16+(i+1)%8,16+i});
            var flat=new List<Vector3>();var indices=new List<int>();foreach(var i in triangles){indices.Add(flat.Count);flat.Add(vertices[i]);}
            var mesh=new Mesh{name="Faceted tree geometry"};mesh.SetVertices(flat);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);return mesh;
        }
        Mesh RoundedBox(Vector3 size,float radius)
        {
            Vector3 half=size*.5f,inner=half-Vector3.one*radius;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            var axes=new[]{Vector3.right,Vector3.up,Vector3.forward};
            for(int face=0;face<6;face++)
            {
                int axis=face/2;float sign=face%2==0?1:-1;Vector3 normal=axes[axis]*sign;
                Vector3 u=axes[(axis+1)%3],v=axes[(axis+2)%3]*sign;
                float hu=Vector3.Dot(half,u),hv=Mathf.Abs(Vector3.Dot(half,v)),hn=half[axis];
                float[] us={-hu,-hu+radius,hu-radius,hu},vs={-hv,-hv+radius,hv-radius,hv};int start=vertices.Count;
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {
                    Vector3 p=normal*hn+u*us[x]+v*vs[y];
                    Vector3 q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    Vector3 n=(p-q).normalized;vertices.Add(q+n*radius);normals.Add(n);
                }
                for(int y=0;y<3;y++)for(int x=0;x<3;x++){int a=start+y*4+x;triangles.AddRange(new[]{a,a+1,a+4,a+1,a+5,a+4});}
            }
            var mesh=new Mesh{name="Toy bevel"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();ownedMeshes.Add(mesh);return mesh;
        }

        void OnDestroy()
        {
            foreach(var m in ownedMaterials)if(m!=null)Destroy(m);
            foreach(var m in ownedMeshes)if(m!=null)Destroy(m);
        }
    }
}
