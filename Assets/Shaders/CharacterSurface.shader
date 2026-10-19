Shader "Game/CharacterSurface"
{
    Properties
    {
        _BaseMap("Original Face Texture", 2D) = "white" {}
        _BodyColor("Body Tone", Color) = (0.43,0.45,0.48,1)
        _RimColor("Soft Rim Color", Color) = (0.13,0.23,0.32,1)
        _RimStrength("Soft Rim Strength", Range(0,1)) = 0
        _Cleanup("Baked Shading Cleanup", Range(0,1)) = 0.95
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BodyColor;
            half _Cleanup;
            half4 _RimColor;
            half _RimStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.uv=TRANSFORM_TEX(i.uv,_BaseMap); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                half l=dot(original,half3(0.2126,0.7152,0.0722));
                // Preserve dark pupils/mouth and light eyes. Remove baked polygon shading from grey cloth.
                half body=smoothstep(0.012,0.027,l)*(1-smoothstep(0.22,0.4,l));
                half3 albedo=lerp(original,_BodyColor.rgb,body*_Cleanup);
                half3 n=normalize(i.normalWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half diffuse=saturate(dot(n,light.direction)*0.7+0.3);
                half3 illumination=max(SampleSH(n),half3(0.20,0.20,0.20));
                illumination+=light.color*diffuse*lerp(0.4,1,light.shadowAttenuation);
                // Camera-facing fill and a restrained edge light keep black cloth readable.
                half3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                half facing=saturate(dot(n,view));
                half rim=pow(1-facing,3)*_RimStrength;
                half3 fill=_RimColor.rgb*(rim+facing*0.08*_RimStrength);
                return half4(albedo*illumination+fill*body,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
