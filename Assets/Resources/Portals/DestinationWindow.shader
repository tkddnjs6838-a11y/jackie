Shader "Game/DestinationWindow"
{
    Properties
    {
        _Vista("Destination panorama",Cube)=""{}
        _Tint("Rim",Color)=(.05,.65,1,1)
        _Forward("Destination forward",Vector)=(0,0,1,0)
        _Completion("Finish gate",Float)=0
        _HasVista("Has panorama",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Off ZWrite On
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURECUBE(_Vista); SAMPLER(sampler_Vista);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint,_Forward;float _Completion,_HasVista;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD1;float2 uv:TEXCOORD0;};
            V Vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.world=TransformObjectToWorld(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                float2 q=i.uv*2-1;float r=length(q);clip(1-r);
                float3 local=TransformWorldToObjectDir(normalize(i.world-GetCameraPositionWS()));
                float3 f=normalize(_Forward.xyz);float3 right=normalize(cross(float3(0,1,0),f));
                float3 ray=normalize(right*(q.x*.75+local.x*.25)+float3(0,1,0)*(q.y*.75-.22+local.y*.25)+f);
                half3 scene=SAMPLE_TEXTURECUBE_LOD(_Vista,sampler_Vista,ray,0).rgb;
                float angle=atan2(q.y,q.x);
                float wave=.5+.5*sin(angle*5-_Time.y*2.2);
                half3 fallback=lerp(half3(.015,.04,.10),_Tint.rgb*.3,saturate(1-r));
                scene=lerp(fallback,scene,_HasVista*(1-_Completion));
                float edge=smoothstep(.82,.98,r);
                scene=lerp(scene,_Tint.rgb*(.65+wave*.55),edge);
                float core=smoothstep(.96,.985,r)*(1-smoothstep(.99,1,r));
                scene+=core*half3(.6,.9,1);
                return half4(scene,1);
            }
            ENDHLSL
        }
    }
}
