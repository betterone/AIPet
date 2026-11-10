using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Momo
{
    // Small code-native UI assets: shared, anti-aliased and independent of system emoji/fonts.
    public static class GameHudStyle
    {
        public static readonly Color Ink=Hex("#243F40"),Muted=Hex("#526965"),Paper=Hex("#F8FBF7"),
            Accent=Hex("#34796F"),Mist=Hex("#E8EFEB"),Gold=Hex("#D9BA76"),Line=Hex("#CCD9D3");
        public static readonly Color Scrim=new Color(.08f,.14f,.15f,.46f);
        public const float TouchTarget=44;
        public const int CaptionSize=12,BodySize=14,ActionSize=16,TitleSize=23;
        static Sprite rounded,circle;
        static readonly Dictionary<string,Sprite> icons=new Dictionary<string,Sprite>();
        public static Color Hex(string hex){ColorUtility.TryParseHtmlString(hex,out var c);return c;}
        public static void Surface(Image image,bool disk=false,bool shadow=false)
        {
            image.sprite=disk?Circle:Rounded;image.type=disk?Image.Type.Simple:Image.Type.Sliced;
            if(disk)image.alphaHitTestMinimumThreshold=.1f;
            if(shadow&&image.GetComponent<Shadow>()==null)
            {var s=image.gameObject.AddComponent<Shadow>();s.effectColor=new Color(.08f,.16f,.16f,.12f);s.effectDistance=new Vector2(0,-2);}
        }
        public static Sprite Rounded=>rounded??(rounded=Shape(false));
        public static Sprite Circle=>circle??(circle=Shape(true));
        static Sprite Shape(bool disk)
        {
            const int n=96;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name=disk?"HUD circle":"HUD soft corners",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[n*n];float radius=disk?47:22;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                var p=new Vector2(Mathf.Abs(x+.5f-48),Mathf.Abs(y+.5f-48))-Vector2.one*(47-radius);
                float d=new Vector2(Mathf.Max(p.x,0),Mathf.Max(p.y,0)).magnitude+Mathf.Min(Mathf.Max(p.x,p.y),0)-radius;
                pixels[y*n+x]=new Color32(255,255,255,(byte)(Mathf.Clamp01(.5f-d)*255));
            }
            texture.SetPixels32(pixels);texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,disk?Vector4.zero:Vector4.one*24);
        }
        public static Sprite Icon(string name)
        {
            if(icons.TryGetValue(name,out var found))return found;
            var segments=new List<Vector4>();var rings=new List<Vector4>();var fills=new List<Vector4>();
            System.Action<float,float,float,float> line=(x,y,a,b)=>segments.Add(new Vector4(x,y,a,b));
            System.Action<float,float,float> ring=(x,y,r)=>rings.Add(new Vector4(x,y,r,0));
            System.Action<float,float,float,float> ellipse=(x,y,rx,ry)=>fills.Add(new Vector4(x,y,rx,ry));
            System.Action<float,float,float,float> box=(l,t,r,b)=>{line(l,t,r,t);line(r,t,r,b);line(r,b,l,b);line(l,b,l,t);};
            switch(name)
            {
                case "more":ellipse(18,32,3,3);ellipse(32,32,3,3);ellipse(46,32,3,3);break;
                case "food": line(12,30,18,48);line(18,48,46,48);line(46,48,52,30);line(12,30,52,30);ellipse(23,23,4,4);ellipse(33,19,4,4);ellipse(43,23,4,4);break;
                case "heart": ellipse(24,26,10,10);ellipse(40,26,10,10);line(16,31,32,49);line(32,49,48,31);line(20,32,32,44);line(32,44,44,32);ellipse(32,34,11,9);break;
                case "paw": case "pet": ellipse(32,39,12,9);ellipse(18,25,4,5);ellipse(27,18,4,5);ellipse(38,18,4,5);ellipse(47,26,4,5);break;
                case "book": line(32,19,32,48);line(32,20,15,16);line(15,16,15,45);line(15,45,32,49);line(32,20,49,16);line(49,16,49,45);line(49,45,32,49);break;
                case "camera": box(12,23,52,48);line(21,23,24,17);line(24,17,40,17);line(40,17,43,23);ring(32,35,8);ellipse(46,28,1.5f,1.5f);break;
                case "gear": ring(32,32,14);ring(32,32,5);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;line(32+Mathf.Cos(a)*16,32+Mathf.Sin(a)*16,32+Mathf.Cos(a)*21,32+Mathf.Sin(a)*21);}break;
                case "find": ring(32,32,14);line(32,10,32,20);line(32,44,32,54);line(10,32,20,32);line(44,32,54,32);ellipse(32,32,3,3);break;
                case "reset": ring(32,32,15);line(14,20,14,31);line(14,20,25,20);break;
                case "close": line(20,20,44,44);line(44,20,20,44);break;
                case "up": line(21,38,32,27);line(32,27,43,38);break;
                case "down": line(21,27,32,38);line(32,38,43,27);break;
                case "left": line(38,21,27,32);line(27,32,38,43);break;
                case "right": line(27,21,38,32);line(38,32,27,43);break;
                case "home": line(12,29,32,13);line(32,13,52,29);line(18,26,18,50);line(18,50,46,50);line(46,50,46,26);box(27,36,37,50);break;
                case "phone": box(23,12,42,52);line(28,46,36,46);line(14,22,10,32);line(10,32,14,42);line(51,22,55,32);line(55,32,51,42);break;
                case "sound": box(13,26,22,38);line(22,26,34,17);line(34,17,34,47);line(34,47,22,38);line(43,23,48,32);line(48,32,43,41);break;
                case "wand": line(16,50,43,23);line(43,10,43,18);line(43,28,43,36);line(30,23,38,23);line(48,23,56,23);break;
                case "ball": ring(32,32,19);line(15,25,49,39);line(16,21,50,35);break;
                case "sun": ring(32,32,10);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;line(32+Mathf.Cos(a)*17,32+Mathf.Sin(a)*17,32+Mathf.Cos(a)*22,32+Mathf.Sin(a)*22);}break;
                default: ring(32,32,16);ellipse(32,32,3,3);break;
            }
            const int n=96;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="HUD icon "+name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};var pixels=new Color32[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                var p=new Vector2((x+.5f)*64/n,64-(y+.5f)*64/n);float d=100;
                foreach(var l in segments){var a=new Vector2(l.x,l.y);var b=new Vector2(l.z,l.w);var ab=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);d=Mathf.Min(d,(p-a-ab*t).magnitude-1.65f);}
                foreach(var r in rings)d=Mathf.Min(d,Mathf.Abs((p-new Vector2(r.x,r.y)).magnitude-r.z)-1.65f);
                foreach(var f in fills){var q=p-new Vector2(f.x,f.y);d=Mathf.Min(d,(new Vector2(q.x/f.z,q.y/f.w).magnitude-1)*Mathf.Min(f.z,f.w));}
                pixels[y*n+x]=new Color32(255,255,255,(byte)(Mathf.Clamp01(.5f-d*n/64)*255));
            }
            texture.SetPixels32(pixels);texture.Apply();found=Sprite.Create(texture,new Rect(0,0,n,n),new Vector2(.5f,.5f));icons.Add(name,found);return found;
        }
    }
}
