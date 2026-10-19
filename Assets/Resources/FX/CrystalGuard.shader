Shader "Game/CrystalGuard"
{
    Properties { _Color("Tint", Color) = (.3,.85,1,.3) _Flash("Hit flash", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Flash;
            CBUFFER_END
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float d=abs(i.uv.x-.5)*2+abs(i.uv.y-.5)*2;
                float rim=smoothstep(.82,1,d);
                float inner=1-smoothstep(.012,.035,abs(d-.72));
                half3 tint=lerp(_Color.rgb,half3(.85,1,1),rim*.7+_Flash*.3);
                return half4(tint*(1+rim*.4+_Flash),saturate(_Color.a+rim*.35+inner*.09+_Flash*.28));
            }
            ENDHLSL
        }
    }
}
