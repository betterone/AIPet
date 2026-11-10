Shader "Momo/Bath Spray"
{
    Properties { _Clock("Flow time",Float)=0 }
    SubShader
    {
        Tags {"Queue"="Transparent+5" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            float _Clock;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(v2f i):SV_Target
            {
                float edge=1-smoothstep(.12,.5,abs(i.uv.y-.5));
                float bead=.38+.62*smoothstep(.2,.8,sin(i.uv.x*95-_Clock*45)*.5+.5);
                return half4(.79,.93,.96,edge*bead*i.color.a*.52);
            }
            ENDCG
        }
    }
}
