// Alpha metadata is replayed with sorting overrides, but cannot relabel a covered opaque pixel.
#ifndef OSSASTRA_SIDE_VIEW_OPAQUE_DEPTH_INCLUDED
#define OSSASTRA_SIDE_VIEW_OPAQUE_DEPTH_INCLUDED
Texture2D<float> _CameraDepthTexture;
void SideViewRespectOpaqueDepth(float4 position)
{
    float nativeDepth = _CameraDepthTexture.Load(int3(int2(position.xy), 0));
    #if defined(UNITY_REVERSED_Z)
    clip(position.z - nativeDepth + 0.000002);
    #else
    clip(nativeDepth - position.z + 0.000002);
    #endif
}
#endif
