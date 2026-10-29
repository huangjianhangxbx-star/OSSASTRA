/* 脚本：MapFlickerTraceSample
 * 功能：保存研究场某次正式网格回调或相机阶段的有界诊断值。
 * 边界：仅记录标量和只读摘要；不复制网格几何，不把CPU记录当成GPU可见性证明。
 * 协作：Recorder在正式回调采样；Buffer按值保留；只有显式导出才写文件。
 */
using System;
using UnityEngine;

namespace Ossastra.MapResearchIntegration.Diagnostics
{
    [Serializable]
    public struct MapFlickerTraceSample
    {
        public string provenance;
        public int frame;
        public double realtime;
        public string actorId;
        public string currentActorId, controlSessionKind;
        public int controlRevision;
        public int actorInstanceId, bodyInstanceId, skeletonInstanceId, rendererInstanceId, meshInstanceId;
        public int expectedCameraId, renderCameraId, meshFrame, slotPoseFrame;
        public float actualYaw, targetYaw;
        public string faceId, directionSource;
        public bool mirrored;
        public int directionRevision, hiddenSlotCount;
        public Vector3 rootPosition, rootScale, visualPosition, visualScale, worldDirection, viewDirection;
        public Quaternion visualRotation;
        public string actionId, phaseId;
        public int lifeGeneration, attackStage;
        public bool actionActive;
        public string mainAnimation, structureAnimation;
        public float mainTime, structureTime;
        public bool mainLoop, structureLoop;
        public int structureTrack;
        public int beforeApplyCalls, poseCompleteCalls, meshCalls;
        public int poseVisibleSlotCount, poseZeroAlphaSlotCount;
        public bool meshAvailable, bodyEnabled, rendererEnabled, rendererIsVisible;
        public int vertexCount, subMeshCount, vertexColorCount, vertexAlphaZeroCount, vertexAlphaPositiveCount, vertexAlphaAbove15Count;
        public float vertexAlphaMin, vertexAlphaMax, vertexAlphaMean, skeletonAlpha;
        public Vector3 meshBoundsCenter, meshBoundsExtents, rendererBoundsCenter, rendererBoundsExtents;
        public float zSpacing;
        public string updateMode, updateTiming;
        public int materialCount, firstMaterialId, firstTextureId, renderQueue;
        public string firstShader;
        public float materialCutoff, materialPmaInput;
        public bool cpuEmptyOrAllZeroAlpha;
        public string samplingError;
    }

    [Serializable]
    public struct MapFlickerTraceMarker
    {
        public int frame;
        public double realtime;
        public string label;
    }
}
