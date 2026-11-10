using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Momo
{
    // An analogue thumb stick: the displacement supplies both direction and speed.
    // The intentionally gentle low end lets a player line up with a doorway or cat.
    public sealed class WalkPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        readonly Dictionary<int,Vector2> held=new Dictionary<int,Vector2>();
        public WindowView View;
        public RectTransform Thumb;
        public float Radius=38f;
        public bool Floating;
        public CanvasGroup Visual;
        public RectTransform FloatingDisc;
        public System.Action Activity;
        public bool Pressed=>pointer!=int.MinValue;
        public const float IdleOpacity=.65f;
        Vector2 floatingCentre;
        float releasedFor=10;
        public const float DeadZone=.08f;
        int pointer=int.MinValue;
        Vector2 input;

        public bool Moving=>input.sqrMagnitude>.0001f;
        public bool Owns(int id)=>pointer==id||held.ContainsKey(id);
        public bool Hits(Vector2 point)=>RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform,point);

        // A narrow programmatic seam for the desktop exploration checks. The live HUD
        // creates no cardinal-direction buttons.
        public void Set(int id,Vector2 direction){held[id]=Vector2.ClampMagnitude(direction,1);if(pointer==int.MinValue)ApplyLegacy();}
        public void Release(int id)
        {
            if(pointer==id){ReleasePointer();return;}
            held.Remove(id);if(pointer==int.MinValue)ApplyLegacy();
        }
        void ApplyLegacy()
        {
            Vector2 direction=Vector2.zero;foreach(var value in held.Values)direction+=value;
            SetInput(Vector2.ClampMagnitude(direction,1));
        }
        public static Vector2 InputForOffset(Vector2 offset,float radius,float deadZone=DeadZone)
        {
            if(radius<=0)return Vector2.zero;
            float magnitude=Mathf.Clamp01(offset.magnitude/radius);
            if(magnitude<=deadZone)return Vector2.zero;
            // Slow motion begins immediately outside the dead zone, then grows toward
            // the responsive full-speed edge instead of snapping between four speeds.
            float response=Mathf.Pow((magnitude-deadZone)/(1f-deadZone),1.25f);
            return offset.normalized*response;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(pointer!=int.MinValue&&pointer!=data.pointerId)return;
            pointer=data.pointerId;held.Clear();Activity?.Invoke();releasedFor=0;
            if(Floating)
            {
                var rect=(RectTransform)transform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out floatingCentre);
                if(FloatingDisc!=null)FloatingDisc.anchoredPosition=floatingCentre;
            }
            UpdatePointer(data);
        }
        public void OnDrag(PointerEventData data)
        {if(data.pointerId==pointer)UpdatePointer(data);}
        public void OnPointerUp(PointerEventData data)
        {if(data.pointerId==pointer)ReleasePointer();}
        void UpdatePointer(PointerEventData data)
        {
            Activity?.Invoke();
            var rect=(RectTransform)transform;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out Vector2 local))return;
            Vector2 centre=Floating?floatingCentre:new Vector2(rect.rect.width*.5f,-rect.rect.height*.5f);
            Vector2 offset=Vector2.ClampMagnitude(local-centre,Radius);
            if(Thumb!=null)Thumb.anchoredPosition=(Thumb.parent==FloatingDisc?Vector2.zero:centre)+offset;
            SetInput(InputForOffset(offset,Radius));
        }
        void SetInput(Vector2 value){input=Vector2.ClampMagnitude(value,1);if(View!=null)View.SetMove(input);}
        void ReleasePointer(){pointer=int.MinValue;releasedFor=0;ResetThumb();ApplyLegacy();}
        void ResetThumb()
        {if(Thumb!=null){var rect=(RectTransform)transform;Thumb.anchoredPosition=Thumb.parent==FloatingDisc?Vector2.zero:new Vector2(rect.rect.width*.5f,-rect.rect.height*.5f);}}
        public void Clear(){pointer=int.MinValue;held.Clear();SetInput(Vector2.zero);ResetThumb();if(Visual!=null)Visual.alpha=IdleOpacity;}
        public void TickVisual(float dt)
        {if(Visual==null)return;releasedFor+=Mathf.Max(0,dt);Visual.alpha=Pressed?1:Mathf.Lerp(IdleOpacity,1,Mathf.Clamp01(1-releasedFor/.35f));}
        void OnDisable(){Clear();}
    }
}
