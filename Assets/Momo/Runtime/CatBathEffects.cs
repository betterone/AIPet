using System.Collections.Generic;
using UnityEngine;
using Momo.Core;

namespace Momo
{
    // Per-renderer property blocks leave shared coat assets and other residents untouched.
    [DefaultExecutionOrder(140)]
    public sealed class CatBathEffects : MonoBehaviour
    {
        CatView cat;
        Renderer[] coats;
        MaterialPropertyBlock block;
        void Awake(){block=new MaterialPropertyBlock();}
        float dirt,wet,foam;
        BathSession session;
        public float Wet=>wet;
        public float Foam=>foam;
        BathBubbleField bubbles;
        float waterHeight=-10;
        public int BubblePatches=>bubbles?.CoveredPatches??0;
        public int BubbleCount=>bubbles?.VisibleCount??0;
        public float BubbleBindingError=>bubbles?.BindingError??0;
        public float BubbleRadius=>bubbles?.LargestRadius??0;
        public void TickBathVisual(float dt,BathSession bath,BathWaterView water,bool rubbing,Vector3 touch)
        {
            waterHeight=water!=null?water.WorldHeight:-10;
            if(bubbles==null&&bath.Phase==BathPhase.Soap)bubbles=new BathBubbleField(this);
            bubbles?.Tick(dt,bath,water,rubbing,touch);
        }
        public void Set(WorldState state,BathSession bath)
        {cat=GetComponent<CatView>();dirt=state.dirt;session=bath;wet=bath?.Wetness??0;foam=bath?.Foam??0;}
        public void ClearCare(WorldState state){Set(state,null);waterHeight=-10;bubbles?.Dispose();bubbles=null;LateUpdate();}
        public void ResetAppearance()
        {
            coats=null;bubbles?.Dispose();bubbles=null;
        }
        Bounds WashBounds()
        {
            var bounds=Body.bounds;
            float front=Mathf.Max(bounds.min.z+.15f,Mathf.Min(bounds.max.z,cat.HeadPosition.z-.24f));
            bounds.SetMinMax(bounds.min,new Vector3(bounds.max.x,bounds.max.y,front));return bounds;
        }
        public Collider Body
        {
            get{if(cat==null)cat=GetComponent<CatView>();foreach(var c in cat.ContactSolids)if(c!=null&&c.name=="Body contact surface")return c;return null;}
        }
        public Vector3 Anchor(BathZone zone,int patch)
        {
            var body=Body;if(body==null)return transform.position+Vector3.up*.5f;
            var bounds=WashBounds();
            // Station heading is north; patch bands partition the torso, excluding the head.
            float z=Mathf.Lerp(bounds.min.z+.035f,bounds.max.z-.035f,(patch+.5f)/BathSession.Patches);
            Vector3 origin,direction;
            if(zone==BathZone.Back){origin=new Vector3(bounds.center.x,bounds.max.y+.3f,z);direction=Vector3.down;}
            else{float side=zone==BathZone.Left?-1:1;origin=new Vector3(bounds.center.x+side*(bounds.extents.x+.3f),bounds.center.y+.03f,z);direction=Vector3.right*-side;}
            return body.Raycast(new Ray(origin,direction),out var hit,2)?hit.point:bounds.center;
        }
        public bool Sample(Ray ray,out BathContact contact,out Vector3 point,out Vector3 normal)
        {
            contact=default;point=normal=Vector3.zero;float nearest=float.MaxValue;Collider chosen=null;
            foreach(var c in cat.ContactSolids)
                if(c!=null&&c.Raycast(ray,out var hit,5)&&hit.distance<nearest){chosen=c;nearest=hit.distance;point=hit.point;normal=hit.normal;}
            if(chosen==null)return false;
            contact.touching=true;contact.head=chosen.name=="Head contact surface";
            var body=Body;if(body==null)return false;var bounds=WashBounds();
            var localNormal=transform.InverseTransformDirection(normal);
            contact.zone=localNormal.y>.55f?BathZone.Back:localNormal.x<0?BathZone.Left:BathZone.Right;
            contact.patch=Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(bounds.min.z,bounds.max.z,point.z)*BathSession.Patches),0,BathSession.Patches-1);
            return true;
        }
        void LateUpdate()
        {
            if(cat==null)return;
            if(coats==null||coats.Length==0||coats[0]==null)
            {
                var all=GetComponentsInChildren<Renderer>();var list=new List<Renderer>();
                foreach(var r in all)if(!r.name.StartsWith("Bath foam")&&r.GetComponentInParent<CatBathEffects>()==this)list.Add(r);
                coats=list.ToArray();
            }
            foreach(var r in coats)
            {
                if(r==null||r.sharedMaterial==null)continue;var m=r.sharedMaterial;r.GetPropertyBlock(block);
                float visualDirt=session!=null&&session.Washing&&session.Phase>=BathPhase.Rinse?dirt*(session.Phase==BathPhase.Rinse?1-session.Progress:0):dirt;
                float dust=Mathf.InverseLerp(10,100,visualDirt);
                block.SetFloat("_BathDirt",dust);block.SetFloat("_BathWet",wet);
                block.SetFloat("_BathWaterLevel",waterHeight);
                if(m.HasProperty("_Color"))
                {
                    var color=Color.Lerp(m.GetColor("_Color"),new Color(.43f,.34f,.23f),dust*.35f)*(1-wet*.16f);

                    block.SetColor("_Color",color);
                }
                block.SetFloat("_Glossiness",Mathf.Lerp(m.HasProperty("_Glossiness")?m.GetFloat("_Glossiness"):.15f,.5f,wet));r.SetPropertyBlock(block);
            }
            bubbles?.Render();
        }
        void OnDestroy(){bubbles?.Dispose();}
    }
}
