"""Reproducible thin entry adaptation from this project's pinned URP14 sources.
Does not alter package cache; own CBUFFER is shared by every geometry pass.
"""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
urp = root/'Library/PackageCache/com.unity.render-pipelines.universal@14.2.0-t1'
out = root/'Assets/OSSASTRA/Scripts/WorldRendering/VoxelSurface/Rendering'
out.mkdir(parents=True, exist_ok=True)
provenance = '// Adapted from Unity URP14.2.0-t1, Unity Companion License; see docs/ossastra/ENV-01.\n'
src=(urp/'Shaders/LitInput.hlsl').read_text(encoding='utf-8-sig')
src=src.replace('UNIVERSAL_LIT_INPUT_INCLUDED','OSSASTRA_VOXEL_INPUT_INCLUDED')
fields=['_VoxelMainShape','_VoxelIndirectDiffuse','_VoxelEnvironmentSpecular','_VoxelDirectSpecular','_VoxelHighlightStrength','_VoxelHighlightKnee','_VoxelHighlightCeiling','_VoxelDiagnostic','_VoxelVertexColors','_RoomSightRoofMode','_RoomSightCutRoof']
prev='_Surface'; extra=''
for field in fields:
    extra+=f'UNITY_DEFINE_PROP(float, {field}, UNITY_GDRP_NEXT_INDEX({prev}))\n';prev=field
src=src.replace('CBUFFER_END',extra+'CBUFFER_END',1)
(out/'VoxelSurfaceInput.hlsl').write_text(provenance+src,encoding='utf-8')
src=(urp/'Shaders/LitForwardPass.hlsl').read_text(encoding='utf-8-sig')
src=src.replace('UNIVERSAL_FORWARD_LIT_PASS_INCLUDED','OSSASTRA_VOXEL_FORWARD_INCLUDED')
src=src.replace('float4 positionOS   : POSITION;','float4 positionOS   : POSITION;\n    half4 color : COLOR;')
src=src.replace('float4 positionCS               : SV_POSITION;','half4 color : TEXCOORD11;\n    float4 positionCS               : SV_POSITION;')
src=src.replace('output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);','output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);\n    output.color = input.color;')
# Keep the pinned URP vertex/input path, but there is only one owned fragment.
src=src[:src.index('void LitPassFragment(')]
src+='''#if defined(VOXEL_ROOM_SIGHT)
#include "Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightClip.hlsl"
#endif
#define ENVIRONMENT_NATIVE_LIT
#include "VoxelSurfaceLighting.hlsl"

// Vertex colours affect RGB only: alpha coverage, shadow and depth keep source texture alpha.
void VoxelPassFragment(Varyings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
, out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(VOXEL_ROOM_SIGHT)
    if (_RoomSightRoofMode > .5) RoomSightPreviewRoofClip(input.positionCS.xy, _RoomSightCutRoof);
    else if (_EnvironmentFullSurface < .5) clip(RoomSightPartialContains(input.positionWS) ? 1 : -1);
#endif
#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, GetWorldSpaceNormalizeViewDir(input.positionWS));
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv UNITY_GDRP_MATERIAL_PAGE_OFFSET_ARGUMENT);
#endif
    SurfaceData surface;
    InitializeStandardLitSurfaceData(input.uv, surface);
    surface.albedo *= lerp(half3(1,1,1), input.color.rgb, saturate(_VoxelVertexColors));
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
    InputData data;
    InitializeInputData(input, surface.normalTS, data);
    data.bakedGI *= EnvironmentIndoorFactor(input.positionWS);
    SETUP_DEBUG_TEXTURE_DATA(data, input.uv, _BaseMap);
#ifdef _DBUFFER
    ApplyDecalToSurfaceData(input.positionCS, surface, data);
#endif
    half4 color = VoxelUniversalFragmentPBR(data, surface);
    if (_VoxelDiagnostic < .5) color.rgb = MixFog(color.rgb, data.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
#if defined(VOXEL_ROOM_SIGHT)
    if (_RoomSightRoofMode < .5)
    {
        // History never carries new dynamic lighting; legal coverage still applies to diagnostics.
        if (_EnvironmentFullSurface < .5 && RoomSightIsHistoryOnly(input.positionWS))
            color.rgb = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb
                *lerp(half3(1,1,1),input.color.rgb,saturate(_VoxelVertexColors));
        color = EnvironmentFogColor(color,input.positionWS,0);
    }
#endif
    outColor = color;
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()),0,0,0);
#endif
}
#endif
'''
(out/'VoxelSurfaceForward.hlsl').write_text(provenance+src,encoding='utf-8')
base=(root/'Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/Rendering/RoomSightLit.shader').read_text(encoding='utf-8-sig')
props=base[base.index('    Properties'):base.index('    SubShader')]
properties='''
        [Header(Voxel Surface Response)]
        _VoxelMainShape("Main light shape",Range(0,1))=0
        _VoxelIndirectDiffuse("Indirect diffuse",Range(0,2))=1
        _VoxelEnvironmentSpecular("Environment reflection",Range(0,2))=1
        _VoxelDirectSpecular("Direct specular",Range(0,2))=1
        _VoxelHighlightStrength("Highlight protection",Range(0,1))=0
        _VoxelHighlightKnee("Highlight knee",Range(0.01,16))=4
        _VoxelHighlightCeiling("Highlight ceiling",Range(0.02,32))=8
        [Enum(Final,0,Albedo,1,WorldNormal,2,MainDiffuse,3,IndirectDiffuse,4,DirectSpecular,5,Reflection,6,Shadow,7)]
        _VoxelDiagnostic("Diagnostic",Float)=0
        [ToggleUI] _VoxelVertexColors("Multiply vertex RGB",Float)=0
'''
props=props[:props.rfind('}')]+properties+'}\n'
pragmas=base[base.index('            #pragma target'):base.index('            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"')]
pragmas=pragmas.replace('#pragma fragment RoomSightLitFragment','#pragma fragment VoxelPassFragment')
# No skinning/DOTS/GDRP claim for explicit static voxel scope. Standard GPU instancing retained.
pragmas='\n'.join(x for x in pragmas.splitlines() if not ('DOTS.hlsl' in x or 'ENABLE_VS_SKINNING' in x))
native=(urp/'Shaders/Lit.shader').read_text(encoding='utf-8-sig')
def nativepass(name):
    at=native.index('Name "'+name+'"');start=native.rfind('Pass',0,at);opening=native.index('{',start);depth=1;end=opening+1
    while depth:
        depth+= (native[end]=='{')-(native[end]=='}');end+=1
    txt=native[start:end].replace('"Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"','"VoxelSurfaceInput.hlsl"')
    return '\n'.join(x for x in txt.splitlines() if not ('DOTS.hlsl' in x or 'ENABLE_VS_SKINNING' in x))
for legal in (False,True):
    name='SightVoxelLit' if legal else 'VoxelLit'
    text=provenance+f'Shader "Ossastra/WorldRendering/{name}"\n{{\n'+props+'\nSubShader {\nTags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit"}\n'
    text+='Pass { Name "ForwardLit" Tags {"LightMode"="UniversalForwardOnly"}\nBlend[_SrcBlend][_DstBlend],[_SrcBlendAlpha][_DstBlendAlpha]\nZWrite[_ZWrite] Cull[_Cull] AlphaToMask[_AlphaToMask]\nHLSLPROGRAM\n'+pragmas+'\n'
    if legal:text+='#define VOXEL_ROOM_SIGHT\n'
    text+='#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR\n#include "VoxelSurfaceInput.hlsl"\n#include "VoxelSurfaceForward.hlsl"\nENDHLSL\n}\n'
    for namepass in ['ShadowCaster','DepthOnly','DepthNormals']:
        text+=nativepass(namepass)+'\n'
    text+='}\nCustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"\nFallback Off\n}\n'
    (out/(name+'.shader')).write_text(text,encoding='utf-8')
