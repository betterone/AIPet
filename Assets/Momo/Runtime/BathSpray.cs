using UnityEngine;
using UnityEngine.Rendering;

namespace Momo
{
    // Small ballistic jets, clipped at the first actual skin, basin wall or water impact.
    // Droplets use the same explicit real-time tick as care, including development replays.
    public sealed class BathSpray : MonoBehaviour
    {
        const int Jets=9,Samples=18;
        readonly LineRenderer[] jets=new LineRenderer[Jets];
        readonly Vector3[] points=new Vector3[Samples];
        readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[96];
        readonly LineRenderer[] runoff=new LineRenderer[3];
        Material material;ParticleSystem beads;Mesh drop;
        float clock,emit,splash,drip,runoffTime;
        Vector3 lastOrigin,lastPoint,lastNormal;bool wasFlowing;
        CatBathEffects coat;
        BathWaterView basin;
        public bool Flowing {get;private set;}
        public bool BodyHit {get;private set;}
        public bool HeadHit {get;private set;}
        public Vector3 HitPoint {get;private set;}
        public Vector3 HitNormal {get;private set;}
        public int Impacts {get;private set;}
        public int Drops {get;private set;}
        public float Curvature {get;private set;}
        public float RunoffRemaining=>runoffTime;
        public Vector3 JetPoint(int sample)=>jets[0].GetPosition(Mathf.Clamp(sample,0,jets[0].positionCount-1));
        public void Build()
        {
            material=new Material(Resources.Load<Shader>("Bath/Spray")){name="Clear moving shower water"};
            for(int i=0;i<Jets;i++){jets[i]=Line("Fine shower jet "+i,.0032f,.0018f);jets[i].positionCount=Samples;jets[i].enabled=false;}
            for(int i=0;i<3;i++){runoff[i]=Line("Water running down coat "+i,.004f,.0015f);runoff[i].positionCount=5;runoff[i].enabled=false;}
            beads=new GameObject("Falling shower beads",typeof(ParticleSystem)).GetComponent<ParticleSystem>();beads.transform.SetParent(transform,false);beads.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var m=beads.main;m.playOnAwake=false;m.loop=false;m.startLifetime=.55f;m.startSpeed=0;m.gravityModifier=.65f;m.startSize=.009f;m.maxParticles=96;m.simulationSpace=ParticleSystemSimulationSpace.World;
            var em=beads.emission;em.enabled=false;var sh=beads.shape;sh.enabled=false;
            drop=new Mesh{name="Rounded drop mesh"};
            drop.vertices=new[]{Vector3.up*.55f,Vector3.right*.35f,Vector3.forward*.35f,Vector3.left*.35f,Vector3.back*.35f,Vector3.down*.45f};drop.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};drop.RecalculateNormals();
            var renderer=beads.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=drop;renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        LineRenderer Line(string name,float start,float end)
        {
            var l=new GameObject(name,typeof(LineRenderer)).GetComponent<LineRenderer>();l.transform.SetParent(transform,false);l.sharedMaterial=material;l.startWidth=start;l.endWidth=end;l.numCapVertices=2;l.shadowCastingMode=ShadowCastingMode.Off;l.receiveShadows=false;l.startColor=new Color(1,1,1,.55f);l.endColor=new Color(1,1,1,.18f);return l;
        }
        public static Vector3 AimVelocity(Vector3 source,Vector3 target)
        {float t=Mathf.Clamp(Vector3.Distance(source,target)/2,.075f,.22f);return Vector3.ClampMagnitude((target-source-Physics.gravity*(.5f*t*t))/t,3.2f);}
        public void Clear()
        {Flowing=BodyHit=HeadHit=wasFlowing=false;drip=runoffTime=0;foreach(var l in jets)if(l!=null)l.enabled=false;foreach(var l in runoff)if(l!=null)l.enabled=false;if(beads!=null)beads.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void Bead(Vector3 point,Vector3 velocity,float size=.009f)
        {beads.Emit(new ParticleSystem.EmitParams{position=point,velocity=velocity,startSize=size},1);Drops++;}
        public void Tick(float dt,Vector3 source,Vector3 velocity,bool flowing,CatBathEffects effect,BathWaterView water)
        {
            dt=Mathf.Clamp(dt,0,.1f);clock+=dt;emit-=dt;splash-=dt;runoffTime-=dt;
            coat=effect;basin=water;Flowing=flowing;BodyHit=HeadHit=false;Curvature=0;
            if(wasFlowing&&!flowing){drip=.22f;for(int i=0;i<3;i++)Bead(lastOrigin+Vector3.down*i*.008f,Vector3.down*(.08f+i*.04f),.008f);}
            wasFlowing=flowing;lastOrigin=source;drip=Mathf.Max(0,drip-dt);
            RenderJets(source,velocity,flowing);
            if(BodyHit)
            {lastPoint=HitPoint;lastNormal=HitNormal;runoffTime=.45f;if(emit<=0){Bead(lastPoint+lastNormal*.012f,Vector3.down*.1f+lastNormal*.07f);emit=.06f;}}
            RenderRunoff();
            if(beads!=null)
            {
                beads.Simulate(dt,false,false,false);beads.Pause(false);int count=beads.GetParticles(particles);
                for(int i=0;i<count;i++)
                {
                    var p=particles[i];if(basin!=null&&basin.ContainsWater(p.position)&&p.position.y<=basin.WorldHeight+.004f)
                    {if(splash<=0){basin.Disturb(p.position,.22f);splash=.12f;}p.remainingLifetime=0;particles[i]=p;}
                }
                beads.SetParticles(particles,count);
            }
            material.SetFloat("_Clock",clock);
        }
        public void Render(Vector3 source,Vector3 velocity)
        {if(coat==null)return;bool body=BodyHit,head=HeadHit;var p=HitPoint;var n=HitNormal;RenderJets(source,velocity,Flowing,false);BodyHit=body;HeadHit=head;HitPoint=p;HitNormal=n;RenderRunoff();}
        void RenderJets(Vector3 source,Vector3 velocity,bool flowing,bool events=true)
        {
            var axis=velocity.normalized;var side=Vector3.Cross(axis,Vector3.up).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;var up=Vector3.Cross(axis,side);
            for(int jet=0;jet<Jets;jet++)
            {
                var line=jets[jet];line.enabled=flowing;if(!flowing)continue;
                float a=jet*2.399963f,r=jet==0?0:.032f;
                var origin=source+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*r;
                var v=velocity+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*.11f;
                Vector3 previous=origin;int used=1;points[0]=origin;
                for(int j=1;j<Samples;j++)
                {
                    float t=j*.022f;var next=origin+v*t+Physics.gravity*(.5f*t*t);bool hit=Trace(previous,next,out var point,out var normal,out var kind);
                    points[used++]=hit?point:next;
                    if(hit)
                    {
                        if(kind==1&&!BodyHit){BodyHit=true;HitPoint=point;HitNormal=normal;}
                        if(kind==2)HeadHit=true;
                        if(events&&jet==0){Impacts++;if(kind==3&&splash<=0){basin.Disturb(point,.6f);splash=.15f;}if(emit<=0){Bead(point+normal*.009f,normal*.12f+Vector3.up*.08f);emit=.07f;}}
                        break;
                    }
                    previous=next;
                }
                line.positionCount=used;for(int i=0;i<used;i++)line.SetPosition(i,points[i]);
                if(jet==0&&used>2)Curvature=Vector3.Distance(points[used/2],Vector3.Lerp(points[0],points[used-1],(used/2)/(float)(used-1)));
            }
        }
        bool Trace(Vector3 from,Vector3 to,out Vector3 point,out Vector3 normal,out int kind)
        {
            point=normal=Vector3.zero;kind=0;var delta=to-from;float nearest=delta.magnitude;bool found=false;var ray=new Ray(from,delta.normalized);
            if(coat!=null)foreach(var c in coat.GetComponent<CatView>().ContactSolids)
                if(c!=null&&c.Raycast(ray,out var h,nearest+.0001f)){nearest=h.distance;point=h.point;normal=h.normal;kind=c.name=="Head contact surface"?2:1;found=true;}
            if(basin!=null&&basin.TraceLiquid(ray,nearest,out var p,out var n,out bool water))
            {point=p;normal=n;kind=water?3:4;found=true;}
            return found;
        }
        void RenderRunoff()
        {
            for(int k=0;k<runoff.Length;k++)
            {
                var line=runoff[k];line.enabled=runoffTime>0&&coat?.Body!=null;if(!line.enabled)continue;
                var body=coat.Body;var p=lastPoint+Vector3.forward*((k-1)*.014f);var normal=lastNormal;line.SetPosition(0,p+normal*.004f);
                for(int i=1;i<5;i++)
                {
                    var side=Vector3.right*(p.x>=body.bounds.center.x?1:-1);var tangent=Vector3.ProjectOnPlane(Vector3.down+side*.55f,normal).normalized;
                    var desired=p+tangent*.033f;var outward=(desired-body.bounds.center).normalized;
                    if(body.Raycast(new Ray(desired+outward*.35f,-outward),out var h,.65f)){p=h.point;normal=h.normal;}
                    if(basin!=null&&p.y<basin.WorldHeight)p.y=basin.WorldHeight;
                    line.SetPosition(i,p+normal*.004f);
                }
            }
        }
        void OnDestroy(){if(material!=null)Destroy(material);if(drop!=null)Destroy(drop);}
    }
}
