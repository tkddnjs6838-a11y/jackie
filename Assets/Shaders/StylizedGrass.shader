Shader "Game/StylizedGrass"
{
    // Single-triangle grass blades (MapBuilder.ScatterGrass). Vertex color R is the height along the
    // blade (0 root, 1 tip): it drives the root-to-tip gradient and how far the wind bends the blade.
    Properties
    {
        _BaseColor("Root Color", Color) = (0.23,0.48,0.17,1)
        _TipColor("Tip Color", Color) = (0.58,0.79,0.35,1)
        _WindStrength("Wind Strength", Range(0,0.4)) = 0.09
        _WindSpeed("Wind Speed", Range(0,5)) = 1.6
        _WindScale("Wind Gust Size", Range(0.01,1)) = 0.18
        _Translucency("Back-light Glow", Range(0,1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _TipColor;
            half _WindStrength, _WindSpeed, _WindScale, _Translucency;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half height:TEXCOORD1; half fog:TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Varyings o;
                float3 p=TransformObjectToWorld(i.positionOS.xyz);
                half h=i.color.r;
                // two crossing waves give gusts that roll across the field instead of a uniform sway
                float t=_Time.y*_WindSpeed;
                float gust=sin(t+p.x*_WindScale+p.z*_WindScale*0.6)+0.5*sin(t*1.7+p.z*_WindScale*1.9);
                p.xz+=float2(0.8,0.45)*gust*_WindStrength*h*h;
                o.positionWS=p;
                o.positionCS=TransformWorldToHClip(p);
                o.height=h;
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 albedo=lerp(_BaseColor.rgb,_TipColor.rgb,i.height);
                // blades are flat cards seen from both sides: light them like the ground they grow from
                // so they never flip dark, with a soft glow when the sun is behind them
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shadow=lerp(0.35,1,light.shadowAttenuation);
                half3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                half up=saturate(light.direction.y*0.6+0.4);
                half back=pow(saturate(dot(-view,light.direction)),3)*_Translucency*i.height;
                half3 color=albedo*(SampleSH(half3(0,1,0))+light.color*(up+back)*shadow);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
