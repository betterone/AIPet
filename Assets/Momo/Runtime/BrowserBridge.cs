using System.Runtime.InteropServices;
using UnityEngine;

namespace Momo
{
    // Browser-only seams stay out of the simulation and native players. WebGL users
    // download exports/screenshots instead of being shown a sandbox file-system path.
    internal static class BrowserBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void MomoBrowserPrepare();
        [DllImport("__Internal")] static extern void MomoBrowserDownloadText(string name,string contents);
        [DllImport("__Internal")] static extern void MomoBrowserDownloadBytes(string name,byte[] bytes,int length);
#endif
        internal static void Prepare()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MomoBrowserPrepare();
#endif
        }
        internal static bool DownloadText(string name,string contents)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MomoBrowserDownloadText(name,contents);return true;
#else
            return false;
#endif
        }
        internal static bool DownloadScreenshot(string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                byte[] png=image.EncodeToPNG();MomoBrowserDownloadBytes(name,png,png.Length);return true;
            }
            finally{Object.Destroy(image);}
#else
            return false;
#endif
        }
    }
}
