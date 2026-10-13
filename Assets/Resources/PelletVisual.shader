Shader "Game/PelletVisual"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float facing=saturate(dot(normalize(input.normalWS),normalize(GetWorldSpaceViewDir(input.positionWS))));
                half3 color=lerp(half3(.18,.055,.012),half3(1,.78,.22),smoothstep(.25,.55,facing));
                color=lerp(color,half3(1,.94,.65),smoothstep(.8,1,facing));
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
