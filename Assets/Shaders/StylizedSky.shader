Shader "Game/StylizedSky"
{
    // Skybox: soft horizon-to-zenith gradient, a glow around the sun and drifting cartoon clouds.
    // The horizon color matches the map's fog so distant hills fade into the sky instead of a band.
    Properties
    {
        _ZenithColor("Top Color", Color) = (0.29,0.60,0.93,1)
        _HorizonColor("Horizon Color", Color) = (0.80,0.91,0.97,1)
        _GroundColor("Below Horizon", Color) = (0.75,0.86,0.9,1)
        _SunGlow("Sun Glow", Range(0,2)) = 0.6
        _CloudColor("Cloud Color", Color) = (1,1,1,1)
        _CloudShade("Cloud Underside", Color) = (0.78,0.85,0.95,1)
        _CloudCover("Cloud Cover", Range(0,1)) = 0.45
        _CloudSpeed("Cloud Drift", Range(0,0.1)) = 0.008
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _ZenithColor, _HorizonColor, _GroundColor, _CloudColor, _CloudShade;
            half _SunGlow, _CloudCover, _CloudSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 dir:TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.dir=i.positionOS.xyz;
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+float2(1,1)),f.x),f.y);
            }
            float Clouds(float2 p)
            {
                return Noise(p)*0.55+Noise(p*2.1+3.7)*0.28+Noise(p*4.3+9.1)*0.17;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.dir);
                half up=saturate(d.y);
                half3 sky=lerp(_HorizonColor.rgb,_ZenithColor.rgb,pow(up,0.55));
                sky=lerp(_GroundColor.rgb,sky,smoothstep(-0.08,0.02,d.y));
                Light sun=GetMainLight();
                half toSun=saturate(dot(d,sun.direction));
                sky+=sun.color*pow(toSun,24)*_SunGlow*0.35+sun.color*pow(toSun,600)*_SunGlow;
                // clouds live on a flat layer above, so they bunch up and flatten toward the horizon
                if(d.y>0.01)
                {
                    float2 uv=d.xz/(d.y+0.12)*0.9+_Time.y*_CloudSpeed;
                    float c=Clouds(uv);
                    half shape=smoothstep(1-_CloudCover,1-_CloudCover+0.12,c);
                    half shade=smoothstep(1-_CloudCover,1-_CloudCover+0.45,c);
                    half3 cloud=lerp(_CloudShade.rgb,_CloudColor.rgb,shade);
                    sky=lerp(sky,cloud,shape*smoothstep(0.01,0.18,d.y)*0.95);
                }
                return half4(sky,1);
            }
            ENDHLSL
        }
    }
}
