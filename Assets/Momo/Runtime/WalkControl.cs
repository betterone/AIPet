using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Momo
{
    public sealed class WalkControl : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        public WalkPad Pad;public Vector2 Direction;int pointer=int.MinValue;
        Image surface;Color resting;
        void Awake(){surface=GetComponent<Image>();if(surface!=null)resting=surface.color;}
        void LateUpdate(){if(surface!=null)surface.color=pointer!=int.MinValue&&Pad!=null&&Pad.Owns(pointer)?new Color(.52f,.76f,.62f,.75f):resting;}
        public void OnPointerDown(PointerEventData data){pointer=data.pointerId;Pad.Set(pointer,Direction);}
        public void OnPointerUp(PointerEventData data){Pad.Release(data.pointerId);pointer=int.MinValue;}
        public void OnPointerExit(PointerEventData data){Pad.Release(data.pointerId);pointer=int.MinValue;}
        void OnDisable(){if(pointer!=int.MinValue&&Pad!=null)Pad.Release(pointer);pointer=int.MinValue;}
    }
}
