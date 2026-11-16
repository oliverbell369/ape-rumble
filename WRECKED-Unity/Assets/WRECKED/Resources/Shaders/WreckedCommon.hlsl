#ifndef WRECKED_COMMON_INCLUDED
#define WRECKED_COMMON_INCLUDED

// All colours in WRECKED are authored as display (sRGB) values, like the web version.
// In a Linear colour-space project the frame buffer converts linear -> sRGB, so convert first.
float3 WreckedOut(float3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;
#else
    return pow(max(c, 0.0), 2.2);
#endif
}

float WreckedHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

float WreckedNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(WreckedHash(i), WreckedHash(i + float2(1, 0)), f.x),
                lerp(WreckedHash(i + float2(0, 1)), WreckedHash(i + float2(1, 1)), f.x), f.y);
}

#endif
