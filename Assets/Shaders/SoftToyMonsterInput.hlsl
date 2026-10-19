// Keep the installed URP Lit lighting, shadow and batching paths intact.
#ifndef SOFT_TOY_MONSTER_INPUT
#define SOFT_TOY_MONSTER_INPUT
#define InitializeStandardLitSurfaceData InitializeOriginalMonsterSurfaceData
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    InitializeOriginalMonsterSurfaceData(uv, surface);
    half3 source=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
    half l=dot(source,half3(.2126,.7152,.0722));
    // Preserve dark facial marks and bright eye regions. Replace muddy body paint with an even tone.
    half body=smoothstep(.025,.10,l)*(1-smoothstep(.55,.78,l));
    half3 clean=lerp(source,half3(.65,.65,.65),body*.88);
    surface.albedo=clean*_BaseColor.rgb;
    surface.smoothness=.72;
    surface.emission += surface.albedo * .38;
    surface.alpha=.88;
}
inline void InitializeToyEyeSurfaceData(float2 uv, float2 eyeMask, out SurfaceData surface)
{
    InitializeStandardLitSurfaceData(uv,surface);
    // Authored four-component UVs have w=0. Ordinary two-component UVs have w=1.
    half eye=(1-smoothstep(.88,1,eyeMask.x))*(1-step(.5,eyeMask.y));
    surface.albedo=lerp(surface.albedo,half3(.012,.021,.028),eye);
    surface.emission=surface.albedo*.38;
    surface.smoothness=lerp(.72,.84,eye);
    surface.alpha=lerp(.88,1,eye);
}
#endif
