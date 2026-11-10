using System.Collections.Generic;
using UnityEngine;

namespace Momo
{
    // Bone-attached convex surfaces for near-contact, independent of touch hit areas.
    public static class ContactGeometry
    {
        public static MeshCollider Convex(string name,Transform parent,List<Vector3> world,List<Mesh> owned)
        {
            var selected=new HashSet<int>();
            for(int i=0;i<64;i++)
            {
                float y=1-2*(i+.5f)/64,angle=i*2.39996323f,r=Mathf.Sqrt(1-y*y);
                var dir=new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);int best=0;float dot=float.MinValue;
                for(int j=0;j<world.Count;j++){float d=Vector3.Dot(world[j],dir);if(d>dot){dot=d;best=j;}}
                if(world.Count>0)selected.Add(best);
            }
            var vertices=new List<Vector3>();foreach(int i in selected)vertices.Add(parent.InverseTransformPoint(world[i]));
            if(vertices.Count<4)throw new System.Exception("Contact surface missing: "+name);
            var triangles=new List<int>();for(int i=1;i<vertices.Count-1;i++)triangles.AddRange(new[]{0,i,i+1});
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();owned.Add(mesh);
            var obj=new GameObject(name);obj.layer=2;obj.transform.SetParent(parent,false);
            var collider=obj.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;collider.isTrigger=true;return collider;
        }
        public static void SeparateBone(Transform bone,Collider surface,IEnumerable<Collider> hands,float maximumShift)
        {
            Vector3 start=bone.position;
            for(int i=0;i<6;i++)
            {
                Physics.SyncTransforms();Vector3 correction=Vector3.zero;float deepest=0;
                foreach(var h in hands)
                    if(h!=null&&h.enabled&&h.gameObject.activeInHierarchy&&Physics.ComputePenetration(h,h.transform.position,h.transform.rotation,surface,surface.transform.position,surface.transform.rotation,out var axis,out float depth)&&depth>deepest){deepest=depth;correction=-axis*(depth+.003f);}
                if(deepest<.003f)break;
                bone.position=start+Vector3.ClampMagnitude(bone.position+correction-start,maximumShift);
            }
            Physics.SyncTransforms();
        }
        public static float Separation(IEnumerable<Collider> hand,IEnumerable<Collider> cat,out Vector3 correction)
        {
            correction=Vector3.zero;float maximum=0;
            foreach(var h in hand)foreach(var c in cat)
            {
                if(h==null||c==null||!h.enabled||!c.enabled||!h.gameObject.activeInHierarchy||!c.gameObject.activeInHierarchy)continue;
                if(!h.bounds.Intersects(c.bounds))continue;
                if(Physics.ComputePenetration(h,h.transform.position,h.transform.rotation,c,c.transform.position,c.transform.rotation,out var axis,out float depth)&&depth>maximum)
                {maximum=depth;correction=axis*(depth+.003f);}
            }
            return maximum;
        }
    }
}
