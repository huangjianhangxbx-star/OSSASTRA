// Optional MRT payload only. Requires RoomSightClip, and never grants visibility.
#ifndef OSSASTRA_ROOM_STYLE_COVERAGE_INCLUDED
#define OSSASTRA_ROOM_STYLE_COVERAGE_INCLUDED
#if defined(_ROOM_STYLE_COVERAGE) || defined(_ROOM_STYLE_WINDOW_ONLY)
float _RoomStyleEnvironment, _RoomStyleCharacter;
float4 RoomStyleCoverage(float3 logicalWorld)
{
    #if defined(_ROOM_STYLE_WINDOW_ONLY)
    // This variant is bound to StyleCoverage ONLY. It cannot change label target0.
    // Difference is strictly current camera aperture minus current player sight;
    // no history, closed opening, other building or blocked camera ray is admitted.
    clip(_RoomSightPassMode == 1 && RoomSightWindowContains(logicalWorld) && !RoomSightEyeContains(logicalWorld) ? 1 : -1);
    #endif
    bool actor = _RoomStyleCharacter > .5;
    if (_RoomPositivePermissionMode < .5 || (!actor && _RoomStyleEnvironment < .5)) return 0;
    // Air is a property of the actual permitted surface location, not camera room state.
    return float4(1, actor ? 1 : 0, RoomSightBuildingContains(logicalWorld) ? 0 : 1, 1);
}
#endif
struct RoomStyleOutput
{
    half4 color : SV_Target0;
#if defined(_ROOM_STYLE_COVERAGE)
    float4 style : SV_Target1;
#endif
};
RoomStyleOutput RoomStylePack(half4 color, float3 logicalWorld)
{
    RoomStyleOutput output; output.color = color;
#if defined(_ROOM_STYLE_WINDOW_ONLY)
    output.color = RoomStyleCoverage(logicalWorld);
#endif
#if defined(_ROOM_STYLE_COVERAGE)
    output.style = RoomStyleCoverage(logicalWorld);
#endif
    return output;
}
#endif
