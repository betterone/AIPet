Shader "Momo/Bath Cloth"
{
    Properties { [HideInInspector] _MainTex("Weave coordinates",2D)="white" {} _Dampness("Absorbed water",Range(0,1))=0 }
    SubShader
    {
        Tags {"RenderType"="Opaque"} Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        struct Input {float2 uv_MainTex;};
        half _Dampness;
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            float weave=(sin(i.uv_MainTex.x*570)*sin(i.uv_MainTex.y*520))*.018;
            float stripe=smoothstep(.055,.065,i.uv_MainTex.x)*(1-smoothstep(.09,.10,i.uv_MainTex.x));
            float damp=_Dampness*(1-smoothstep(.12,.6,length(i.uv_MainTex-.5)));
            o.Albedo=lerp(half3(.91,.87,.74),half3(.43,.66,.58),stripe*.7)*(1-damp*.2)+weave;
            o.Smoothness=.07+damp*.08;o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
