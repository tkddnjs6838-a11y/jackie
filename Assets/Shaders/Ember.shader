Shader "Game/Ember"
{
    // Soft additive dot for the dark theme's floating embers; color and fade come from the particle color.
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.color=i.color; o.uv=i.uv;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half d=saturate(1-length(i.uv-.5)*2);
                return half4(i.color.rgb*i.color.a*d*d*1.6,0);
            }
            ENDHLSL
        }
    }
}
