using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Momo
{
    // A small filled icon, legible percentage and a single, coalesced gain cue.
    public sealed class NeedBadge : MonoBehaviour
    {
        RectTransform iconRoot,deltaRoot;
        Image fill,ring;
        TMP_Text value,gain;
        Color tint;
        float shown=-1,target,flashAge=10,pending;
        public string Percent=>value.text;
        public int FeedbackCount {get;private set;}
        public void Build(TMP_FontAsset font,string glyph,string label,Color color)
        {
            tint=color;
            var back=gameObject.AddComponent<Image>();GameHudStyle.Surface(back,false,true);back.color=GameHudStyle.Paper;back.raycastTarget=false;
            iconRoot=Child("Filled status icon",transform,new Vector2(10,-10),new Vector2(26,26));
            var empty=iconRoot.gameObject.AddComponent<Image>();empty.sprite=GameHudStyle.Icon(glyph);empty.color=GameHudStyle.Line;empty.raycastTarget=false;
            var f=Child("Status fill",iconRoot,Vector2.zero,new Vector2(26,26));fill=f.gameObject.AddComponent<Image>();fill.sprite=empty.sprite;fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Vertical;fill.fillOrigin=0;fill.raycastTarget=false;
            var r=Child("Gain halo",transform,new Vector2(5,-5),new Vector2(36,36));ring=r.gameObject.AddComponent<Image>();ring.sprite=GameHudStyle.Icon("reset");ring.color=Color.clear;ring.raycastTarget=false;
            value=Text("Percentage",font,transform,new Vector2(42,-4),new Vector2(48,23),16);value.fontStyle=FontStyles.Bold;value.alignment=TextAlignmentOptions.Left;
            var labelText=Text("Status label",font,transform,new Vector2(43,-27),new Vector2(46,17),11);labelText.alignment=TextAlignmentOptions.Left;labelText.color=GameHudStyle.Muted;labelText.text=label;
            deltaRoot=Child("Earned gain",transform,new Vector2(0,-48),new Vector2(96,22));
            gain=deltaRoot.gameObject.AddComponent<TextMeshProUGUI>();gain.font=font;gain.fontSize=12;gain.fontStyle=FontStyles.Bold;gain.alignment=TextAlignmentOptions.Center;gain.raycastTarget=false;gain.color=Color.clear;
        }
        static RectTransform Child(string name,Transform p,Vector2 at,Vector2 size)
        {var g=new GameObject(name,typeof(RectTransform));var r=(RectTransform)g.transform;r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=at;r.sizeDelta=size;return r;}
        static TMP_Text Text(string name,TMP_FontAsset font,Transform p,Vector2 at,Vector2 size,int px)
        {var r=Child(name,p,at,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=px;t.color=GameHudStyle.Ink;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
        public void Set(float amount,bool food)
        {
            target=Mathf.Clamp(amount,0,100);if(shown<0)shown=target;
            fill.color=food?(target<15?GameHudStyle.Hex("#CF7155"):target<35?GameHudStyle.Hex("#D6A24D"):tint):Color.Lerp(GameHudStyle.Hex("#DDB6BD"),tint,target/100);
        }
        public void Flash(float amount)
        {if(amount<=0)return;if(flashAge>1.6f)pending=0;pending+=amount;flashAge=0;FeedbackCount++;}
        void Update()
        {
            float dt=Time.unscaledDeltaTime;shown=Mathf.Lerp(shown,target,1-Mathf.Exp(-dt*8));value.text=Mathf.RoundToInt(shown)+"%";fill.fillAmount=shown/100;
            flashAge+=dt;float strength=1-Mathf.Clamp01(flashAge/1.6f);
            iconRoot.localScale=Vector3.one*(1+Mathf.Sin(Mathf.Clamp01(flashAge/.55f)*Mathf.PI)*.14f);
            ring.color=new Color(tint.r,tint.g,tint.b,strength*.5f);
            gain.text="+"+pending.ToString("0.#")+"%";gain.color=new Color(tint.r,tint.g,tint.b,strength);
            deltaRoot.anchoredPosition=new Vector2(0,-48+Mathf.Clamp01(flashAge/1.6f)*12);
        }
    }
}
