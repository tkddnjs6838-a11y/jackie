Shader "Game/StylizedWater"
{
    // Opaque water for the flat liquid plane (MapBuilder). No depth texture is needed, so it stays
    // cheap on mobile: ripples are procedural normals, color shifts with the viewing angle, and the
    // sun leaves sparkling highlights.
    Properties
    {
        _BaseColor("Water Color", Color) = (0.13,0.55,0.78,1)
        _DeepColor("Looking-down Color", Color) = (0.09,0.40,0.62,1)
        _SkyColor("Reflected Sky", Color) = (0.72,0.88,1,1)
        _RippleScale("Ripple Density", Range(0.05,4)) = 1.6
        _RippleSpeed("Ripple Speed", Range(0,3)) = 0.7
        _RippleStrength("Ripple Strength", Range(0,1)) = 0.12
        _Gloss("Highlight Sharpness", Range(8,512)) = 300
        _Sparkle("Highlight Strength", Range(0,4)) = 1.2
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
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _DeepColor, _SkyColor;
            half _RippleScale, _RippleSpeed, _RippleStrength, _Gloss, _Sparkle;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half fog:TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.fog=ComputeFogFactor(p.positionCS.z);
                return o;
            }
            // slope of a few crossing sine waves; cheap stand-in for a scrolling normal map
            float2 Ripples(float2 xz, float t)
            {
                float2 d=0;
                static const float2 dirs[4]={float2(1,0.3),float2(-0.4,1),float2(0.7,-0.8),float2(-1,-0.2)};
                static const float freqs[4]={1.0,1.7,2.9,4.3};
                [unroll] for(int k=0;k<4;k++)
                {
                    float2 dir=normalize(dirs[k]);
                    float f=freqs[k]*_RippleScale;
                    d+=dir*cos(dot(xz,dir)*f+t*(1+k*0.37))/freqs[k];
                }
                return d;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 slope=Ripples(i.positionWS.xz,_Time.y*_RippleSpeed)*_RippleStrength;
                half3 n=normalize(half3(-slope.x,1,-slope.y));
                half3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                half facing=saturate(dot(n,view));
                half fresnel=pow(1-facing,4);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow=lerp(0.5,1,light.shadowAttenuation);
                // looking straight down shows the deeper tone; low angles (most of the screen) stay bright
                half3 water=lerp(_DeepColor.rgb,_BaseColor.rgb,saturate((1-facing)*1.2));
                // mostly its own color, so the water stays a fresh turquoise instead of going murky in shade
                half3 lit=water*(0.5+0.5*(SampleSH(half3(0,1,0))+light.color*saturate(light.direction.y)*shadow));
                half3 color=lerp(lit,_SkyColor.rgb,fresnel*0.45);
                half3 h=normalize(light.direction+view);
                color+=light.color*pow(saturate(dot(n,h)),_Gloss)*_Sparkle*shadow;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
