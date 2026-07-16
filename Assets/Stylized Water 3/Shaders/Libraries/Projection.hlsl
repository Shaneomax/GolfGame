#ifndef WATER_PROJECTION_INCLUDED
#define WATER_PROJECTION_INCLUDED

float2 WorldToProjectionUV(float3 positionWS, float2 boundsMin, float boundsSize)
{
    return (positionWS.xz - boundsMin) / max(0.0001, boundsSize);
}

float ProjectionEdgeMask(float3 positionWS, float2 boundsMin, float boundsSize, float fadeWidth)
{
    float2 uv = (positionWS.xz - boundsMin) / max(0.0001, boundsSize);
    float2 edgeDist = min(uv, 1.0 - uv) * boundsSize;
    float dist = min(edgeDist.x, edgeDist.y);
    return saturate(dist / max(0.0001, fadeWidth));
}

#endif
