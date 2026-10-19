Shader "Game/WaxCracks"
{
    Properties
    {
        _Damage("Damage", Range(0,1)) = 0
        _UseRestPosition("Use undeformed coordinates", Float) = 0
        _MeshCenter("Center", Vector) = (0,0,0,0)
        _MeshScale("Size", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Damage, _MeshScale, _UseRestPosition;
            float4 _MeshCenter;
            float4x4 _WorldToCrack;
            struct A { float4 positionOS:POSITION; float3 rest:TEXCOORD3; };
            struct V { float4 positionCS:SV_POSITION; float3 local:TEXCOORD0; };
            V Vert(A i)
            {
                V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.local=mul(_WorldToCrack,float4(TransformObjectToWorld(_UseRestPosition>0.5 ? i.rest : i.positionOS.xyz),1)).xyz*6.5;
                return o;
            }
            float3 Hash(float3 p)
            {
                return frac(sin(float3(dot(p,float3(127.1,311.7,74.7)),dot(p,float3(269.5,183.3,246.1)),dot(p,float3(113.5,271.9,124.6))))*43758.5453);
            }
            half4 Frag(V i):SV_Target
            {
                float3 cell=floor(i.local), f=frac(i.local), first=0, second=0;
                float d1=100,d2=100,seed=0;
                for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) for(int z=-1;z<=1;z++)
                {
                    float3 n=float3(x,y,z), h=Hash(cell+n), v=n+h-f;
                    float d=dot(v,v);
                    if(d<d1) { d2=d1; second=first; d1=d; first=v; seed=h.x; }
                    else if(d<d2) { d2=d; second=v; }
                }
                float edge=(d2-d1)/max(2*length(second-first),0.001);
                // More shell cells fracture as health falls; cracks stay hairline thin.
                clip(_Damage-(0.05+seed*0.75));
                float width=lerp(0.006,0.012,_Damage);
                clip(width-edge);
                half3 color=half3(0.035,0.025,0.018);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
