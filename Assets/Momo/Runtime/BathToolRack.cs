using UnityEngine;
using Momo.Core;

namespace Momo
{
    // The same tools live on the tray and in the hand: no decorative duplicates.
    public sealed class BathToolRack : MonoBehaviour
    {
        public Transform Shower {get;private set;}
        public Transform Sponge {get;private set;}
        public Transform Towel {get;private set;}
        public BathCloth Cloth {get;private set;}
        public Transform Face {get;private set;}
        Transform pump,gel,head;
        LineRenderer hose;
        Material cream,mint,metal,rubber,cotton,soapFilm;
        readonly Vector3[] hosePoints=new Vector3[25];
        readonly Vector3[] docks=new Vector3[3];
        readonly Quaternion[] dockRotations=new Quaternion[3];
        public bool Dispensing=>gel!=null&&gel.gameObject.activeSelf;
        public int PumpStrokes {get;private set;}
        bool pressed;
        public Vector3 CameraPosition=>transform.TransformPoint(new Vector3(.25f,1.30f,-.55f));
        public Vector3 CameraFocus=>transform.TransformPoint(new Vector3(0,.91f,.02f));
        public Vector3 PumpPosition=>transform.TransformPoint(new Vector3(-.02f,1.102f,0));
        public Vector3 SpoutPosition=>transform.TransformPoint(new Vector3(-.02f,1.067f,-.10f));
        public Vector3 Outlet=>Face.position;
        public Vector3 Tap=>transform.TransformPoint(new Vector3(.22f,.70f,.25f));
        public Transform Tool(BathTool tool)=>tool==BathTool.Shower?Shower:tool==BathTool.Soap?Sponge:Towel;
        public Vector3 Dock(BathTool tool)=>transform.TransformPoint(docks[(int)tool]);
        public Quaternion DockRotation(BathTool tool)=>transform.rotation*dockRotations[(int)tool];
        public void Build()
        {
            cream=Art.Mat("Bath tool enamel","#ECE8D9",.36f);mint=Art.Mat("Bath tool sage","#90B7AC",.2f);
            metal=Art.Mat("Brushed shower metal","#A9B7B1",.55f);metal.SetFloat("_Metallic",.65f);
            rubber=Art.Mat("Soft shower hose","#758C86",.18f);cotton=Art.Mat("Porous washing sponge","#9BBFA9",.07f);
            soapFilm=new Material(Resources.Load<Shader>("Bath/Bubble")){name="Small viscous soap dose"};soapFilm.SetFloat("_Opacity",.55f);soapFilm.SetColor("_Tint",new Color(.85f,.94f,.68f));
            Art.Cube("Rim mounted tool tray",transform,new Vector3(0,.727f,0),new Vector3(.57f,.045f,.73f),cream);
            foreach(float x in new[]{-.275f,.275f})Art.Cube("Tray raised edge",transform,new Vector3(x,.765f,0),new Vector3(.025f,.055f,.73f),mint);
            Art.Cube("Tray back edge",transform,new Vector3(0,.765f,.35f),new Vector3(.55f,.055f,.025f),mint);
            foreach(float z in new[]{-.24f,.24f})Art.Cube("Tray bracket",transform,new Vector3(.12f,.52f,z),new Vector3(.055f,.4f,.06f),mint);
            var bottle=Art.Pivot("Upright shampoo pump bottle",transform,new Vector3(-.02f,.754f,0));
            Art.Shape("Rounded shampoo bottle",PrimitiveType.Capsule,bottle,new Vector3(0,.125f,0),new Vector3(.12f,.135f,.10f),mint);
            Art.Cube("Shampoo label",bottle,new Vector3(0,.13f,-.051f),new Vector3(.085f,.11f,.003f),cream);
            Art.Sphere("Small leaf on bottle",bottle,new Vector3(0,.14f,-.055f),new Vector3(.025f,.045f,.002f),mint);
            Art.Cylinder("Bottle neck",bottle,new Vector3(0,.278f,0),new Vector3(.044f,.025f,.044f),cream);
            pump=Art.Pivot("Pressable shampoo pump",transform,new Vector3(-.02f,1.087f,0));
            Art.Cylinder("Sliding pump stem",pump,new Vector3(0,-.026f,0),new Vector3(.018f,.025f,.018f),cream);
            Art.Cube("Pump finger plate",pump,Vector3.zero,new Vector3(.075f,.03f,.06f),cream);
            Art.Cube("Pump spout",pump,new Vector3(0,-.006f,-.047f),new Vector3(.025f,.025f,.10f),cream);
            gel=Art.Shape("Shampoo dose from spout",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.01f,soapFilm).transform;gel.gameObject.SetActive(false);
            Shower=Art.Pivot("Gripped garden shower",transform,Vector3.zero);
            Art.Shape("Rounded shower grip",PrimitiveType.Capsule,Shower,new Vector3(0,0,-.012f),new Vector3(.04f,.087f,.04f),cream).transform.localRotation=Quaternion.Euler(90,0,0);
            Art.Cube("Shower thumb switch",Shower,new Vector3(0,.024f,-.01f),new Vector3(.022f,.009f,.052f),mint);
            head=Art.Pivot("Swivelling spray head",Shower,new Vector3(0,.015f,.115f));
            Art.Cylinder("Shower head shell",head,Vector3.zero,new Vector3(.09f,.016f,.09f),metal);
            Art.Cylinder("Soft spray plate",head,new Vector3(0,-.017f,0),new Vector3(.076f,.003f,.076f),rubber);
            for(int i=0;i<13;i++){float angle=i*2.399963f,r=i==0?0:.026f;Art.Sphere("Spray outlet",head,new Vector3(Mathf.Cos(angle)*r,-.022f,Mathf.Sin(angle)*r),new Vector3(.006f,.003f,.006f),cream);}
            Face=Art.Pivot("Actual spray outlet",head,new Vector3(0,-.023f,0));
            Art.Cylinder("Hose threaded connection",transform,new Vector3(.22f,.70f,.25f),new Vector3(.038f,.035f,.038f),metal);
            Art.Cube("Water valve mounting block",transform,new Vector3(.22f,.67f,.25f),new Vector3(.07f,.055f,.075f),cream);
            var hook=Art.Pivot("Shower wall cradle",transform,new Vector3(.23f,.90f,-.22f));
            Art.Cube("Cradle back",hook,Vector3.zero,new Vector3(.055f,.13f,.055f),mint);
            Art.Cube("Cradle lower cup",hook,new Vector3(-.035f,-.03f,0),new Vector3(.08f,.025f,.08f),cream);
            Sponge=Art.Pivot("Soft washing sponge",transform,Vector3.zero);
            var sponge=Art.Shape("Soft porous sponge",PrimitiveType.Sphere,Sponge,Vector3.zero,new Vector3(.16f,.065f,.13f),cotton);
            // Shallow dimples give the sponge volume without a texture or extra colliders.
            for(int i=0;i<15;i++){float x=Mathf.Sin(i*2.4f)*.052f,z=Mathf.Cos(i*3.7f)*.04f;Art.Sphere("Sponge pore",Sponge,new Vector3(x,.030f,z),new Vector3(.009f,.004f,.009f),mint);}
            Towel=Art.Pivot("Soft drying towel",transform,Vector3.zero);Cloth=Towel.gameObject.AddComponent<BathCloth>();Cloth.Build();
            docks[0]=new Vector3(.18f,.91f,-.22f);dockRotations[0]=Quaternion.Euler(0,0,65);
            docks[1]=new Vector3(-.10f,.798f,-.22f);dockRotations[1]=Quaternion.identity;
            docks[2]=new Vector3(-.10f,.58f,-.38f);dockRotations[2]=Quaternion.Euler(85,0,0);
            hose=new GameObject("Flexible connected shower hose",typeof(LineRenderer)).GetComponent<LineRenderer>();hose.transform.SetParent(transform,false);hose.sharedMaterial=rubber;hose.positionCount=hosePoints.Length;hose.startWidth=hose.endWidth=.018f;hose.numCornerVertices=3;hose.numCapVertices=3;hose.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            ResetTools();
        }
        public void DockTool(BathTool tool){var t=Tool(tool);t.SetPositionAndRotation(Dock(tool),DockRotation(tool));t.gameObject.SetActive(true);if(tool==BathTool.Shower)head.localRotation=Quaternion.identity;}
        public void ResetTools(){Sponge.localScale=Vector3.one;for(int i=0;i<3;i++)DockTool((BathTool)i);PressPump(0,Vector3.zero,false);if(Cloth!=null)Cloth.Tick(.1f,null,false,0);UpdateHose(null,null);}
        public void PressPump(float amount,Vector3 receiver,bool dose)
        {
            amount=Mathf.Clamp01(amount);pump.localPosition=new Vector3(-.02f,1.087f-amount*.022f,0);
            if(amount>.75f&&!pressed){PumpStrokes++;pressed=true;}if(amount<.1f)pressed=false;
            gel.gameObject.SetActive(dose&&amount>.3f);
            if(gel.gameObject.activeSelf){var start=SpoutPosition-Vector3.up*amount*.022f;var end=receiver;var d=end-start;gel.position=(start+end)*.5f;gel.rotation=Quaternion.FromToRotation(Vector3.up,d);gel.localScale=new Vector3(.009f,Mathf.Max(.01f,d.magnitude),.009f);}
        }
        public void AimOutlet(Vector3 velocity)
        {var up=-velocity.normalized;var forward=Vector3.ProjectOnPlane(Shower.forward,up).normalized;head.rotation=Quaternion.RotateTowards(Shower.rotation,Quaternion.LookRotation(forward,up),35);}
        public void UpdateHose(Camera camera,Collider body)
        {
            if(hose==null)return;var start=Tap;var end=Shower.TransformPoint(new Vector3(0,0,-.10f));
            var a=start+Vector3.down*.38f;var b=camera!=null?camera.transform.position+camera.transform.forward*.30f-Vector3.up*.43f:end+Vector3.down*.32f;
            for(int i=0;i<hosePoints.Length;i++)
            {
                float t=i/(float)(hosePoints.Length-1),u=1-t;var p=u*u*u*start+3*u*u*t*a+3*u*t*t*b+t*t*t*end;
                if(body!=null&&body.bounds.Contains(p))
                {var axis=(p-body.bounds.center).normalized;if(axis.sqrMagnitude<.01f)axis=Vector3.back;var origin=body.bounds.center+axis*1.5f;if(body.Raycast(new Ray(origin,-axis),out var hit,2)&&Vector3.Distance(body.bounds.center,p)<Vector3.Distance(body.bounds.center,hit.point)+.025f)p=hit.point+hit.normal*.027f;}
                p.y=Mathf.Max(.04f,p.y);hosePoints[i]=p;
            }
            hose.SetPositions(hosePoints);
        }
        void OnDestroy(){foreach(var m in new[]{cream,mint,metal,rubber,cotton,soapFilm})if(m!=null)Destroy(m);}
    }
}
