using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Momo.Core;

namespace Momo
{
    // A single pooled mesh: bubbles bind to evaluated skin vertices, rather than a static hull.
    // Only the chosen vertices are skinned on CPU. No per-frame BakeMesh, objects, or material clones.
    public sealed class BathBubbleField : IDisposable
    {
        const int PerPatch=20,Attached=3*4*PerPatch,Free=24,Segments=10,Rings=7;
        const int Verts=(Segments+1)*(Rings+1),Faces=Segments*Rings*6;
        struct Site {public Vector3 point,normal;public BoneWeight weights;public Transform rigid;public int zone,patch;public bool valid;}
        struct Bubble {public Site site;public float radius,threshold,size,applied,washed;public Vector3 center,normal,velocity;public float life;public bool detached,floating;}
        readonly Transform owner;
        readonly Collider body;
        readonly Mesh mesh;
        readonly GameObject gameObject;
        readonly Material film;
        readonly Bubble[] bubbles=new Bubble[Attached+Free];
        readonly int[] order=new int[Attached+Free];
        readonly Vector3[] vertices=new Vector3[(Attached+Free)*Verts],normals=new Vector3[(Attached+Free)*Verts];
        readonly Color[] colors=new Color[(Attached+Free)*Verts];
        readonly Vector3[] unit=new Vector3[Verts];
        readonly Matrix4x4[] skinMatrices;
        readonly Matrix4x4[] bindposes;
        readonly Transform[] bones;
        readonly MeshRenderer renderer;
        float clock,emit;
        int freeIndex;
        readonly Comparison<int> compare;
        Vector3 viewer;
        public int CoveredPatches {get;private set;}
        public int VisibleCount {get;private set;}
        public float BindingError {get;private set;}
        public float LargestRadius {get;private set;}
        static float Hash(int n){float f=Mathf.Sin(n*127.1f+17.7f)*43758.5453f;return f-Mathf.Floor(f);}
        public BathBubbleField(CatBathEffects effect)
        {
            compare=CompareDistance;owner=effect.transform;body=effect.Body;var cat=owner.GetComponent<CatView>();var skin=cat.Imported?.Skin;
            var groups=new List<Site>[12];for(int i=0;i<groups.Length;i++)groups[i]=new List<Site>();
            var bounds=effect.Body.bounds;float front=Mathf.Max(bounds.min.z+.15f,Mathf.Min(bounds.max.z,cat.HeadPosition.z-.24f));
            void Add(Site site,Vector3 world,Vector3 normal)
            {
                if(world.z<bounds.min.z+.008f||world.z>front-.008f||world.y<bounds.center.y-.06f||world.y>bounds.max.y+.06f)return;
                var n=owner.InverseTransformDirection(normal);int zone=n.y>.52f?0:n.x<-.48f?1:n.x>.48f?2:-1;
                if(zone<0||n.y<-.1f)return;
                site.valid=true;site.zone=zone;site.patch=Mathf.Clamp((int)(Mathf.InverseLerp(bounds.min.z,front,world.z)*4),0,3);groups[zone*4+site.patch].Add(site);
            }
            if(skin!=null)
            {
                bones=skin.bones;bindposes=skin.sharedMesh.bindposes;skinMatrices=new Matrix4x4[bones.Length];UpdateBones();
                var source=skin.sharedMesh;var points=source.vertices;var ns=source.normals;var weights=source.boneWeights;
                var baked=new Mesh();skin.BakeMesh(baked,true);var evaluated=baked.vertices;var evaluatedNormals=baked.normals;
                for(int i=0;i<points.Length;i++)Add(new Site{point=points[i],normal=ns[i],weights=weights[i]},skin.transform.TransformPoint(evaluated[i]),skin.transform.TransformDirection(evaluatedNormals[i]).normalized);
                // useScale=true compensates for renderer scale; TransformPoint then applies it once.
                // Compare independent Unity skinning against the selected-vertex evaluator.
                for(int i=0;i<points.Length;i+=Mathf.Max(1,points.Length/100))
                {Evaluate(new Site{point=points[i],normal=ns[i],weights=weights[i]},out var p,out var n);BindingError=Mathf.Max(BindingError,Vector3.Distance(p,skin.transform.TransformPoint(evaluated[i])));}
                UnityEngine.Object.Destroy(baked);
            }
            else foreach(var filter in owner.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.name!="Torso"&&filter.name!="Chest")continue;
                var points=filter.sharedMesh.vertices;var ns=filter.sharedMesh.normals;
                for(int i=0;i<points.Length;i++)Add(new Site{point=points[i],normal=ns[i],rigid=filter.transform},filter.transform.TransformPoint(points[i]),filter.transform.TransformDirection(ns[i]).normalized);
            }
            for(int group=0;group<12;group++)
            {
                var candidates=groups[group];if(candidates.Count>0)CoveredPatches++;
                for(int k=0;k<PerPatch;k++)
                {
                    int i=group*PerPatch+k;order[i]=i;
                    // Three irregular clusters per body patch, sampled on the actual mesh.
                    Site site=default;
                    if(candidates.Count>0)
                    {
                        var center=candidates[(int)(Hash(group*61+k/7+900)*candidates.Count)];Evaluate(center,out var cluster,out _);
                        float best=float.MaxValue;
                        var desired=cluster+(group/4==0?owner.right:Vector3.up)*(Hash(i+92)-.5f)*.13f+owner.forward*(Hash(i+183)-.5f)*.12f;
                        for(int j=0;j<Mathf.Min(candidates.Count,100);j++)
                        {var candidate=candidates[(int)(Hash(i*131+j+14)*candidates.Count)];Evaluate(candidate,out var p,out _);float d=Vector3.SqrMagnitude(p-desired);if(d<best){best=d;site=candidate;}}
                    }
                    float radius=k%7==0?Mathf.Lerp(.026f,.044f,Hash(i+25)):Mathf.Lerp(.007f,.022f,Hash(i+25));
                    bubbles[i]=new Bubble{site=site,radius=radius,threshold=Hash(i+150)*.8f};LargestRadius=Mathf.Max(LargestRadius,radius);
                }
            }
            for(int i=Attached;i<bubbles.Length;i++)order[i]=i;
            for(int y=0;y<=Rings;y++)for(int x=0;x<=Segments;x++)
            {float a=x*Mathf.PI*2/Segments,b=y*Mathf.PI/Rings;unit[y*(Segments+1)+x]=new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));}
            var triangles=new int[bubbles.Length*Faces];int at=0;
            for(int k=0;k<bubbles.Length;k++)for(int y=0;y<Rings;y++)for(int x=0;x<Segments;x++)
            {int i=k*Verts+y*(Segments+1)+x;triangles[at++]=i;triangles[at++]=i+1;triangles[at++]=i+Segments+1;triangles[at++]=i+1;triangles[at++]=i+Segments+2;triangles[at++]=i+Segments+1;}
            film=new Material(Resources.Load<Shader>("Bath/Bubble")){name="Thin soap films"};
            mesh=new Mesh{name="Pooled spherical soap bubbles"};mesh.MarkDynamic();mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.triangles=triangles;
            gameObject=new GameObject("Bath foam spherical films",typeof(MeshFilter),typeof(MeshRenderer));gameObject.transform.SetParent(owner,false);
            gameObject.GetComponent<MeshFilter>().sharedMesh=mesh;renderer=gameObject.GetComponent<MeshRenderer>();renderer.sharedMaterial=film;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void UpdateBones(){if(bones==null)return;for(int i=0;i<bones.Length;i++)skinMatrices[i]=bones[i].localToWorldMatrix*bindposes[i];}
        void Evaluate(Site site,out Vector3 point,out Vector3 normal)
        {
            if(site.rigid!=null){point=site.rigid.TransformPoint(site.point);normal=site.rigid.TransformDirection(site.normal).normalized;return;}
            if(skinMatrices==null){point=owner.position;normal=Vector3.up;return;}
            var w=site.weights;
            point=skinMatrices[w.boneIndex0].MultiplyPoint3x4(site.point)*w.weight0+skinMatrices[w.boneIndex1].MultiplyPoint3x4(site.point)*w.weight1+skinMatrices[w.boneIndex2].MultiplyPoint3x4(site.point)*w.weight2+skinMatrices[w.boneIndex3].MultiplyPoint3x4(site.point)*w.weight3;
            normal=(skinMatrices[w.boneIndex0].MultiplyVector(site.normal)*w.weight0+skinMatrices[w.boneIndex1].MultiplyVector(site.normal)*w.weight1+skinMatrices[w.boneIndex2].MultiplyVector(site.normal)*w.weight2+skinMatrices[w.boneIndex3].MultiplyVector(site.normal)*w.weight3).normalized;
        }
        void Release(Vector3 point,float radius,Vector3 speed)
        {int i=Attached+(freeIndex++%Free);bubbles[i]=new Bubble{center=point,radius=radius,size=1,velocity=speed,life=2.5f+Hash(freeIndex)*2,detached=true};}
        public void Tick(float dt,BathSession session,BathWaterView water,bool rubbing,Vector3 touch)
        {
            clock+=dt;emit-=dt;UpdateBones();VisibleCount=0;
            bool soap=session!=null&&session.Phase==BathPhase.Soap,rinse=session!=null&&session.Phase==BathPhase.Rinse;
            float level=water!=null?water.WorldHeight:-10;film.SetFloat("_WaterHeight",level);
            for(int i=0;i<Attached;i++)
            {
                var b=bubbles[i];Evaluate(b.site,out var p,out var n);b.center=p+n*b.radius*.38f;b.normal=n;
                float distance=Vector3.Distance(p,touch);float influence=rubbing?Mathf.Clamp01((.20f-distance)/.10f):0;
                if(soap)b.applied=Mathf.Min(1,b.applied+dt*2.1f*influence);
                if(rinse)b.washed=Mathf.Min(1,b.washed+dt*2.7f*influence);
                float patch=session!=null?session.PatchProgress((BathZone)b.site.zone,b.site.patch):0;
                float coverage=!b.site.valid?0:soap?Mathf.Min(b.applied,patch):rinse?patch>=.999f?0:Mathf.Max(0,b.applied-b.washed):0;
                float desired=Mathf.Clamp01((coverage-b.threshold)*6);
                if(rinse&&b.size>.2f&&desired<b.size-.04f&&!b.detached&&i%5==0)
                {var slip=Vector3.ProjectOnPlane(Vector3.down+Vector3.right*(p.x>=body.bounds.center.x?.4f:-.4f),n).normalized;Release(b.center,b.radius,slip*.12f+n*.02f);b.detached=true;}
                b.size=Mathf.MoveTowards(b.size,desired,dt*3);bubbles[i]=b;
            }
            if(soap&&rubbing&&emit<=0)
            {Release(touch+Vector3.up*.06f,Mathf.Lerp(.02f,.045f,Hash(freeIndex)),new Vector3((Hash(freeIndex+40)-.5f)*.08f,.065f,.015f));emit=.4f;}
            for(int i=Attached;i<bubbles.Length;i++)
            {
                var b=bubbles[i];if(b.life<=0){b.size=0;bubbles[i]=b;continue;}b.life-=dt;
                if(rinse)b.velocity.y-=dt*.16f;
                b.center+=b.velocity*dt;
                if(rinse&&body!=null&&body.bounds.Contains(b.center)&&Vector3.Distance(body.ClosestPoint(b.center),b.center)<.001f)
                {var outwards=(b.center-body.bounds.center).normalized;if(body.Raycast(new Ray(b.center+outwards*.5f,-outwards),out var h,1))b.center=h.point+h.normal*b.radius*.5f;}
                if(b.floating&&water!=null)
                {
                    b.center.y=Mathf.Max(.01f,level)+b.radius*.45f;
                    if(session!=null&&session.Phase>=BathPhase.Dry){var drain=water.DrainPosition-b.center;drain.y=0;b.velocity=Vector3.Lerp(b.velocity,drain.normalized*.16f,dt*3);b.life-=dt;}
                }
                if(level>0&&b.center.y<level+b.radius*.45f){b.center.y=level+b.radius*.45f+Mathf.Sin(clock*3+i)*.003f;b.velocity=new Vector3(b.velocity.x*.98f,0,b.velocity.z*.98f);b.floating=true;}
                b.size=b.life>.15f?1:Mathf.Lerp(1.15f,1,Mathf.Clamp01(b.life/.15f));bubbles[i]=b;
            }
        }
        public void Render()
        {
            if(gameObject==null)return;UpdateBones();VisibleCount=0;
            var camera=Camera.main;viewer=camera!=null?camera.transform.position:owner.position+Vector3.back*2;
            // Sorting spheres within the single draw keeps thin films translucent as they overlap.
            Array.Sort(order,compare);
            for(int k=0;k<order.Length;k++)
            {
                int id=order[k];var b=bubbles[id];
                if(id<Attached){Evaluate(b.site,out var p,out var n);b.center=p+n*b.radius*.38f;bubbles[id]=b;}
                if(b.size>.01f)VisibleCount++;
                var center=owner.InverseTransformPoint(b.center);float radius=b.radius*b.size;
                var color=new Color(Hash(id+9),id<Attached&&b.radius<.023f?.32f:0,1,id>=Attached?Mathf.Clamp01(b.life/.15f):b.size);
                for(int j=0;j<Verts;j++){int i=k*Verts+j;var normal=unit[j];vertices[i]=center+normal*radius;normals[i]=normal;colors[i]=color;}
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.RecalculateBounds();renderer.enabled=VisibleCount>0;
        }
        int CompareDistance(int a,int b)=>(bubbles[b].center-viewer).sqrMagnitude.CompareTo((bubbles[a].center-viewer).sqrMagnitude);
        public void Dispose(){UnityEngine.Object.Destroy(gameObject);UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(film);}
    }
}
