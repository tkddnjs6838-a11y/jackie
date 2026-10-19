Shader "Game/Crater Lava"
{
 Properties { _Color("Tint",Color)=(1,1,1,1) _Cooling("Cooling",Range(0,1))=0 _Stream("Free falling stream",Float)=0 _Pool("World pool",Float)=0 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _Color;float _Cooling;float _Stream;float _Pool;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD1;float3 normalWS:TEXCOORD2;float2 uv:TEXCOORD0;};
   Varyings vert(Attributes i){Varyings o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   float fbm(float2 p){return noise(p)*.57+noise(p*2.07+13.1)*.28+noise(p*4.13-7.8)*.15;}
   half4 frag(Varyings i):SV_Target
   {
    float2 p=lerp(float2(i.uv.x*4,i.uv.y*1.5),i.positionWS.xz*.52,saturate(_Pool));p.y-=_Time.y*.38;
    float2 warp=float2(fbm(p*.65+_Time.y*.07),fbm(p*.7+8.2));p+=(warp-.5)*2.1;
    float n=fbm(p*2);float folds=sin(p.y*11+fbm(p*1.4)*9)*.5+.5;
    float edge=lerp(pow(abs(i.uv.x*2-1),3),0,saturate(_Stream));
    float shore=min(min(distance(i.positionWS.xz,float2(-12.4,10.7)),distance(i.positionWS.xz,float2(14.2,-10.7))),min(distance(i.positionWS.xz,float2(11.4,13.5)),distance(i.positionWS.xz,float2(-14.2,-12.4))));
    edge=lerp(edge,smoothstep(2.2,4.3,shore),saturate(_Pool));
    float cooled=saturate(edge*.6+_Cooling*.65);
    float fine=fbm(p*5.1+warp*3);
    float boundary=abs(fbm(p*1.7+warp*4)-.48);
    float vein=1-smoothstep(.018,.075,boundary);
    vein=max(vein*.88,(1-smoothstep(.01,.027,abs(fine-.51)))*.3);
    float heat=saturate(vein*(1-cooled*.65));
    float skin=1-heat;
    float3 hot=lerp(float3(.85,.012,.001),float3(2.5,.27,.006),heat);
    hot=lerp(hot,float3(3,.72,.035),smoothstep(.86,.99,vein)*(1-cooled)*.55);
    float grain=fbm(p*17);
    float3 cold=lerp(float3(.012,.014,.016),float3(.065,.061,.057),grain);
    float3 normal=normalize(i.normalWS);
    Light light=GetMainLight();float diffuse=saturate(dot(normal,light.direction))*.5+.35;
    float3 view=normalize(GetWorldSpaceViewDir(i.positionWS));float spec=pow(saturate(dot(normal,normalize(light.direction+view))),32)*.23;
    float3 color=lerp(hot*(.74+.26*diffuse),cold*diffuse,skin)+spec*(1-skin)*float3(1,.5,.15);
    return half4(color*_Color.rgb,1);
   }
   ENDHLSL
  }
 }
}
