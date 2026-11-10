using UnityEngine;
using UnityEngine.Rendering;
using Momo.Core;

namespace Momo
{
    // One small dynamic surface and eight decaying wave sources, no fluid physics or water collider.
    public sealed class BathWaterView : MonoBehaviour
    {
        const int Columns=28,Rows=34;
        struct Ripple {public Vector3 point;public float born,force;}
        readonly Ripple[] ripples=new Ripple[8];int next;
        Vector3[] vertices;
        Mesh mesh;MeshRenderer surface;
        Transform gate;
        ParticleSystem droplets;
        Mesh dropMesh;
        Material dropMaterial;
        static readonly int[] RippleIds={Shader.PropertyToID("_Ripple0"),Shader.PropertyToID("_Ripple1"),Shader.PropertyToID("_Ripple2"),Shader.PropertyToID("_Ripple3"),Shader.PropertyToID("_Ripple4"),Shader.PropertyToID("_Ripple5"),Shader.PropertyToID("_Ripple6"),Shader.PropertyToID("_Ripple7")};
        Material water,glass;
        float clock,splashTime;
        public float Height {get;private set;}
        public float WorldHeight=>Height>.008f?transform.position.y+Height:-10;
        public int Disturbances {get;private set;}
        public float WaveSpread {get;private set;}
        public bool GateOpen=>Height<.012f;
        public void Build(Transform door)
        {
            gate=door;
            water=new Material(Resources.Load<Shader>("Bath/Water")){name="Moving bath water"};
            glass=new Material(water){name="Clear walk-in bath gate"};glass.SetColor("_Tint",new Color(.6f,.84f,.81f,.10f));
            gate.GetComponent<Renderer>().sharedMaterial=glass;
            mesh=new Mesh{name="Subdivided bath surface"};mesh.MarkDynamic();
            vertices=new Vector3[(Columns+1)*(Rows+1)];var uv=new Vector2[vertices.Length];var indices=new int[Columns*Rows*6];int at=0;
            for(int z=0;z<=Rows;z++)for(int x=0;x<=Columns;x++){int i=z*(Columns+1)+x;vertices[i]=new Vector3(Mathf.Lerp(-1.08f,1.08f,x/(float)Columns),0,Mathf.Lerp(-1.26f,1.26f,z/(float)Rows));uv[i]=new Vector2(x/(float)Columns,z/(float)Rows);}
            for(int z=0;z<Rows;z++)for(int x=0;x<Columns;x++){int i=z*(Columns+1)+x;indices[at++]=i;indices[at++]=i+Columns+1;indices[at++]=i+1;indices[at++]=i+1;indices[at++]=i+Columns+1;indices[at++]=i+Columns+2;}
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateNormals();
            var g=new GameObject("Actual water surface",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(transform,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;
            surface=g.GetComponent<MeshRenderer>();surface.sharedMaterial=water;surface.shadowCastingMode=ShadowCastingMode.Off;surface.receiveShadows=false;
            var drops=new GameObject("Tiny contact splashes",typeof(ParticleSystem));drops.transform.SetParent(transform,false);droplets=drops.GetComponent<ParticleSystem>();droplets.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=droplets.main;main.playOnAwake=false;main.loop=false;main.startLifetime=.32f;main.startSpeed=0;main.gravityModifier=.7f;main.startSize=.018f;main.maxParticles=48;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=droplets.emission;emission.enabled=false;var shape=droplets.shape;shape.enabled=false;
            dropMesh=new Mesh{name="Low poly water droplet"};dropMesh.vertices=new[]{Vector3.up*.7f,Vector3.right*.4f,Vector3.forward*.4f,Vector3.left*.4f,Vector3.back*.4f,Vector3.down*.5f};dropMesh.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};dropMesh.RecalculateNormals();
            dropMaterial=new Material(water);dropMaterial.SetColor("_Tint",new Color(.62f,.86f,.92f,.65f));var dr=drops.GetComponent<ParticleSystemRenderer>();dr.renderMode=ParticleSystemRenderMode.Mesh;dr.mesh=dropMesh;dr.sharedMaterial=dropMaterial;dr.shadowCastingMode=ShadowCastingMode.Off;dr.receiveShadows=false;
            Clear();
        }
        public void Clear(){droplets?.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Height=0;if(surface!=null)surface.enabled=false;if(gate!=null)gate.localScale=new Vector3(2.18f,.008f,.06f);for(int i=0;i<ripples.Length;i++)ripples[i].force=0;}
        public void Disturb(Vector3 world,float force=.8f)
        {if(Height<.02f)return;ripples[next]=new Ripple{point=transform.InverseTransformPoint(world),born=clock,force=force};next=(next+1)%ripples.Length;Disturbances++;
            if(force>.3f&&droplets!=null)for(int i=0;i<3;i++)
            {
                float a=Disturbances*2.4f+i*2.1f;var velocity=new Vector3(Mathf.Cos(a)*.10f,.22f,Mathf.Sin(a)*.10f);
                var e=new ParticleSystem.EmitParams{position=new Vector3(world.x,WorldHeight+.02f,world.z),velocity=velocity,startSize=.012f+i*.004f};droplets.Emit(e,1);
            }}
        public Vector3 DrainPosition=>transform.TransformPoint(new Vector3(.7f,.006f,.85f));
        public bool ContainsWater(Vector3 world)
        {var p=transform.InverseTransformPoint(world);return Height>.015f&&Mathf.Abs(p.x)<1.09f&&Mathf.Abs(p.z)<1.27f;}
        public bool TraceLiquid(Ray ray,float distance,out Vector3 point,out Vector3 normal,out bool water)
        {
            Vector3 chosen=Vector3.zero,n=Vector3.zero;bool found=false,isWater=false;float nearest=distance;
            void PlaneHit(Vector3 origin,Vector3 facing,int axis,bool surface)
            {
                var plane=new Plane(facing,origin);
                if(!plane.Raycast(ray,out float t)||t<.0001f||t>nearest)return;var p=ray.GetPoint(t);var local=transform.InverseTransformPoint(p);
                bool inside=axis==1?Mathf.Abs(local.x)<=1.10f&&Mathf.Abs(local.z)<=1.29f:local.y>=.003f&&local.y<=.65f&&(axis==0?Mathf.Abs(local.z)<=1.29f:Mathf.Abs(local.x)<=1.10f);
                if(!inside)return;nearest=t;chosen=p;n=facing;found=true;isWater=surface;
            }
            if(Height>.01f)PlaneHit(transform.position+Vector3.up*Height,Vector3.up,1,true);
            PlaneHit(transform.position+Vector3.up*.006f,Vector3.up,1,false);
            PlaneHit(transform.position+Vector3.left*1.10f,Vector3.right,0,false);PlaneHit(transform.position+Vector3.right*1.10f,Vector3.left,0,false);
            PlaneHit(transform.position+Vector3.forward*1.29f,Vector3.back,2,false);
            if(!GateOpen)PlaneHit(transform.position+Vector3.back*1.29f,Vector3.forward,2,false);
            point=chosen;normal=n;water=isWater;return found;
        }
        public void Tick(float dt,BathSession bath,CatBathEffects coat,bool touching,Vector3 contact,float speed)
        {
            dt=Mathf.Clamp(dt,0,.1f);clock+=dt;splashTime-=dt;
            bool filled=bath!=null&&bath.Active&&bath.Washing&&bath.Tool!=BathTool.Towel;
            float target=filled&&coat?.Body!=null?Mathf.Clamp(coat.Body.bounds.center.y-transform.position.y,.28f,.57f):0;
            Height=Mathf.MoveTowards(Height,target,dt*(filled?.28f:.75f));
            if(gate!=null){float h=Height>.008f?.66f:.008f;gate.localScale=new Vector3(2.18f,h,.06f);gate.localPosition=new Vector3(0,h*.5f,-1.30f);}
            if(surface==null)return;surface.enabled=Height>.008f;if(!surface.enabled)return;
            if(touching&&splashTime<=0){Disturb(contact,Mathf.Clamp(.4f+speed,.4f,1.1f));splashTime=.22f;}
            // Small breathing/paw ripples remain even when the finger rests.
            if(!touching&&splashTime<=0){Disturb(transform.position+new Vector3(Mathf.Sin(clock)*.17f,0,.27f),.18f);splashTime=1.8f;}
            float low=10,high=-10;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];float y=.0035f*Mathf.Sin(p.x*7+p.z*4+clock*1.7f)+.0025f*Mathf.Sin(p.z*9-p.x*3-clock*2.1f);
                foreach(var r in ripples){float age=clock-r.born;if(r.force==0||age<0||age>3)continue;float d=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(r.point.x,r.point.z));float front=d-age*.62f;y+=r.force*.024f*Mathf.Exp(-front*front*65-age*.9f)*Mathf.Sin(front*29);}
                float edge=Mathf.Clamp01(Mathf.Min(1.08f-Mathf.Abs(p.x),1.26f-Mathf.Abs(p.z))*12);float draining=!filled&&Height>.015f?1:0;float drainDistance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(.7f,.85f));
                p.y=Mathf.Max(.006f,Height+y*edge-draining*.025f*Mathf.Exp(-drainDistance*drainDistance*15));vertices[i]=p;low=Mathf.Min(low,p.y);high=Mathf.Max(high,p.y);
            }
            WaveSpread=high-low;mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();water.SetFloat("_Clock",clock);
            for(int i=0;i<ripples.Length;i++){var r=ripples[i];var world=transform.TransformPoint(r.point);water.SetVector(RippleIds[i],new Vector4(world.x,world.z,r.born,r.force));}
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(water!=null)Destroy(water);if(glass!=null)Destroy(glass);if(dropMesh!=null)Destroy(dropMesh);if(dropMaterial!=null)Destroy(dropMaterial);}
    }
}
