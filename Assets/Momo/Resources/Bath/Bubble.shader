Shader "Momo/Bath Bubble"
{
    Properties { _Tint("Film tint",Color)=(0.91,0.98,1,1) _Opacity("Film opacity",Range(0,1))=.22 _WaterHeight("Water height",Float)=-10 }
    SubShader
    {
        Tags {"Queue"="Transparent+10" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;fixed4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;fixed4 color:COLOR;};
            float4 _Tint;float _Opacity,_WaterHeight;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);o.color=v.color;return o;}
            half4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal),v=normalize(_WorldSpaceCameraPos-i.world);
                float rim=pow(1-saturate(dot(n,v)),2.8);
                float3 up=normalize(float3(-.45,.8,-.4)+v*.5);
                float shine=pow(saturate(dot(n,up)),55);
                float second=pow(saturate(dot(n,normalize(float3(.6,.1,.5)+v*.6))),90)*.5;
                float film=sin(dot(n,v)*23+i.color.r*6.28)*.5+.5;
                half3 rainbow=lerp(half3(.78,.92,1),half3(1,.82,.9),film);
                half3 c=lerp(_Tint.rgb,rainbow,rim*.55)+shine*.8+second*.6;
                float submerged=lerp(.3,1,smoothstep(_WaterHeight-.025,_WaterHeight+.02,i.world.y));
                return half4(c,saturate((_Opacity+i.color.g+rim*.68+shine*.7+second)*i.color.a*submerged));
            }
            ENDCG
        }
    }
}
