Shader "Momo/Imported Cat Fur"
{
    Properties
    {
        _MainTex ("Coat", 2D) = "white" {}
        [Normal] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,1)) = 0.4
        _Glossiness ("Smoothness", Range(0,1)) = 0.18
        _Metallic ("Metallic", Range(0,1)) = 0
        _BathDirt ("Garden dust", Range(0,1)) = 0
        _BathWet ("Wet coat", Range(0,1)) = 0
        _BathWaterLevel ("Bath waterline", Float) = -10
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        // The source GLB explicitly uses doubleSided for its generated surface.
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap;
        half _BumpScale, _Glossiness, _Metallic, _BathDirt, _BathWet;
        float _BathWaterLevel;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float3 worldPos; float3 worldNormal; float facing : VFACE; INTERNAL_DATA };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;
            half dust = _BathDirt * (0.16 + 0.32 * smoothstep(0.35, 0.75, frac(sin(dot(IN.uv_MainTex, float2(43.12, 17.76))) * 143.12)));
            o.Albedo = lerp(o.Albedo, half3(0.38, 0.29, 0.19), dust) * (1 - _BathWet * 0.18);
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale);
            o.Normal.z *= IN.facing >= 0 ? 1 : -1;
            float underwater = 1-smoothstep(_BathWaterLevel-.015,_BathWaterLevel+.008,IN.worldPos.y);
            o.Albedo = lerp(o.Albedo,o.Albedo*half3(.72,.91,.93),underwater*.36);
            o.Metallic = _Metallic; o.Smoothness = lerp(_Glossiness, 0.5, _BathWet); o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
