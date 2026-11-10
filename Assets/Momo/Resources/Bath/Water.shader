Shader "Momo/Bath Water"
{
    Properties { _Tint("Water tint", Color)=(0.32,0.76,0.77,0.32) _Clock("Clock",Float)=0
        [HideInInspector] _Ripple0("Wave 0",Vector)=(0,0,0,0)
        [HideInInspector] _Ripple1("Wave 1",Vector)=(0,0,0,0)
        [HideInInspector] _Ripple2("Wave 2",Vector)=(0,0,0,0)
        [HideInInspector] _Ripple3("Wave 3",Vector)=(0,0,0,0)
        [HideInInspector] _Ripple4("Wave 4",Vector)=(0,0,0,0)
        [HideInInspector] _Ripple5("Wave 5",Vector)=(0,0,0,0)
        [HideInInspector] _Ripple6("Wave 6",Vector)=(0,0,0,0)
        [HideInInspector] _Ripple7("Wave 7",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;};
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};
            float4 _Tint;float _Clock;float4 _Ripple0,_Ripple1,_Ripple2,_Ripple3,_Ripple4,_Ripple5,_Ripple6,_Ripple7;
            float Ring(float3 p,float4 r){float age=_Clock-r.z;float d=distance(p.xz,r.xy)-age*.62;return r.w*exp(-d*d*250-age*.9)*step(0,age)*step(age,3);}
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            half4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal*float3(1.8,1,1.8)),v=normalize(_WorldSpaceCameraPos-i.world);
                float facing=saturate(abs(dot(n,v))),f=pow(1-facing,4);
                float3 l=normalize(_WorldSpaceLightPos0.xyz);
                float spec=pow(saturate(dot(n,normalize(l+v))),180);
                float drift=sin(i.world.x*18+sin(i.world.z*12+_Clock)*1.5+_Clock*1.4)*sin(i.world.z*23-i.world.x*7-_Clock*1.1);
                float glint=pow(saturate(drift),12)*.08;
                float ring=Ring(i.world,_Ripple0)+Ring(i.world,_Ripple1)+Ring(i.world,_Ripple2)+Ring(i.world,_Ripple3)+Ring(i.world,_Ripple4)+Ring(i.world,_Ripple5)+Ring(i.world,_Ripple6)+Ring(i.world,_Ripple7);
                half3 c=lerp(_Tint.rgb,half3(.78,.90,.89),f*.8)+spec*.8+glint+ring*.32;
                return half4(c,saturate(_Tint.a+f*.3+spec*.24+ring*.08));
            }
            ENDCG
        }
    }
}
