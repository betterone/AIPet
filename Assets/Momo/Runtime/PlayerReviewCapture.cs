using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Momo
{
    // Development review only: explicit render targets do not depend on a visible
    // desktop window or WaitForEndOfFrame, which can stall on a locked Mac.
    public static class PlayerReviewCapture
    {
        static RenderTexture target;
        public static int Width=>Application.isBatchMode&&target!=null?target.width:Screen.width;
        public static int Height=>Application.isBatchMode&&target!=null?target.height:Screen.height;
        public static void Resize(MomoApp app,int width,int height)
        {
            if(!Application.isBatchMode){Screen.SetResolution(width,height,FullScreenMode.Windowed);return;}
            var camera=app.room.cameraView;
            if(target!=null){camera.targetTexture=null;target.Release();Object.Destroy(target);}
            target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;camera.aspect=(float)width/height;
            var canvas=app.hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.002f;
            var scaler=app.hud.GetComponent<CanvasScaler>();scaler.enabled=false;canvas.scaleFactor=Mathf.Min(width/390f,height/844f);
            Canvas.ForceUpdateCanvases();
        }
        public static Vector3 ScreenPoint(MomoApp app,Vector3 point)
        {return Application.isBatchMode?app.room.cameraView.WorldToScreenPoint(point):point;}
        public static void Capture(MomoApp app,string path)
        {
            Texture2D texture;
            if(Application.isBatchMode)
            {
                Canvas.ForceUpdateCanvases();app.room.cameraView.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=previous;
            }
            else texture=ScreenCapture.CaptureScreenshotAsTexture();
            if(texture==null)throw new System.Exception("Review render unavailable");File.WriteAllBytes(path,texture.EncodeToPNG());Object.Destroy(texture);
        }
    }
}
