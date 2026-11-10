using UnityEngine;
using UnityEngine.Rendering;

namespace Momo
{
    public sealed class BathCloth : MonoBehaviour
    {
        const int X=10,Z=8;
        Mesh mesh;Material cotton;Vector3[] vertices;Vector2[] uv;float clock,contact;float[] heights;
        public float Bend {get;private set;}
        public void Build()
        {
            cotton=new Material(Resources.Load<Shader>("Bath/Cloth")){name="Absorbent cotton towel"};
            mesh=new Mesh{name="Soft towel folds"};mesh.MarkDynamic();vertices=new Vector3[(X+1)*(Z+1)];uv=new Vector2[vertices.Length];heights=new float[vertices.Length];var tris=new int[X*Z*6];int a=0;
            for(int z=0;z<=Z;z++)for(int x=0;x<=X;x++)uv[z*(X+1)+x]=new Vector2(x/(float)X,z/(float)Z);
            for(int z=0;z<Z;z++)for(int x=0;x<X;x++){int i=z*(X+1)+x;tris[a++]=i;tris[a++]=i+X+1;tris[a++]=i+1;tris[a++]=i+1;tris[a++]=i+X+1;tris[a++]=i+X+2;}
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tris;
            var g=new GameObject("Draped cotton surface",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(transform,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=g.GetComponent<MeshRenderer>();renderer.sharedMaterial=cotton;renderer.shadowCastingMode=ShadowCastingMode.Off;Tick(.1f,null,false,0);
        }
        public void Tick(float dt,Collider body,bool touching,float dampness)
        {
            if(mesh==null)return;clock+=dt;contact=Mathf.MoveTowards(contact,touching?1:0,dt*8);Bend=0;var normal=transform.up;
            for(int i=0;i<vertices.Length;i++)
            {
                float x=(uv[i].x-.5f)*.25f,z=(uv[i].y-.5f)*.23f;
                var local=new Vector3(x,.006f*Mathf.Cos(x*65+z*25)+.005f*Mathf.Sin(z*57+clock*1.5f),z);
                var world=transform.TransformPoint(local);float edge=Mathf.Pow(Mathf.Abs(uv[i].x-.5f)*2,2);
                world+=Vector3.down*(.025f*edge*(1-contact));
                if(body!=null&&contact>0&&body.Raycast(new Ray(world+normal*.075f,-normal),out var hit,.13f))
                {float h=transform.InverseTransformPoint(hit.point+hit.normal*.006f).y;world=transform.TransformPoint(new Vector3(local.x,Mathf.Lerp(local.y,Mathf.Clamp(h,-.035f,.035f),contact),local.z));}
                var target=transform.InverseTransformPoint(world);target.x=x;target.z=z;target.y=Mathf.Clamp(target.y,-.035f,.035f);vertices[i]=Vector3.Lerp(vertices[i],target,Mathf.Clamp01(dt*14));heights[i]=vertices[i].y;
            }
            for(int z=1;z<Z;z++)for(int x=1;x<X;x++)
            {int i=z*(X+1)+x;float smooth=(heights[i]*4+heights[i-1]+heights[i+1]+heights[i-X-1]+heights[i+X+1])/8;vertices[i].y=Mathf.Max(heights[i]-.004f,smooth);}
            foreach(var v in vertices)Bend=Mathf.Max(Bend,Mathf.Abs(v.y));
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();cotton.SetFloat("_Dampness",Mathf.Clamp01(dampness));
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(cotton!=null)Destroy(cotton);}
    }
}
