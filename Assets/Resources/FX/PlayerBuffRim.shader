Shader "Game/PlayerBuffRim"
{
    Properties { _GlowColor("Effect color", Color)=(1,0.7,0.2,1) _Strength("Strength", Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _GlowColor; half _Strength;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; };
            V Vert(A i) { V o; o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 Frag(V i):SV_Target
            {
                half edge=1-saturate(dot(normalize(i.normalWS),normalize(GetWorldSpaceViewDir(i.positionWS))));
                half rim=smoothstep(.22,.82,edge);
                return half4(_GlowColor.rgb*_Strength,rim);
            }
            ENDHLSL
        }
    }
}
