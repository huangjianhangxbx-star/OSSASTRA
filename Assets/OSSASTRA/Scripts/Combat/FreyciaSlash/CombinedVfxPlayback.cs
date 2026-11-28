/*
 * 脚本：CombinedVfxPlayback
 * 功能：从动画事件或调试台启动一份组合特效，并按角色表现时间推进及回收。
 * 组合层自行安排时序。挂点位置来自 Spine；此入口不写入动作或命中状态。
 */
using System;
using Ossastra.CharacterAnimation;
using UnityEngine;
using AnimationDriver = Ossastra.CharacterAnimation.CharacterAnimationController;
using ActorRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Combat
{
    /// <summary>用角色表现时间驱动动画事件召唤的组合特效，播放完成后收回实例。</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(260)]
    public sealed class CombinedVfxPlayback : MonoBehaviour
    {
        private CombinedVfx effect;
        private AnimationDriver owner;
        private ActorRoot actor;
        private AnimationCalibrationBinding binding;
        private Matrix4x4 spawnPlacement;
        private Matrix4x4 calibratedScale;
        private int startFrame;

        public static bool TryStart(GameObject instance, AnimationDriver owner, AnimationCalibrationBinding binding, int seed)
        {
            if (instance == null || !instance.TryGetComponent<CombinedVfx>(out var effect))
                return false;

            var playback = instance.AddComponent<CombinedVfxPlayback>();
            playback.effect = effect;
            playback.owner = owner;
            playback.actor = owner.GetComponentInParent<ActorRoot>();
            playback.binding = binding.Copy();
            playback.startFrame = Time.frameCount;
            var transform = instance.transform;
            var placement = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            playback.calibratedScale = placement.inverse * transform.localToWorldMatrix;
            playback.spawnPlacement = placement * playback.FacingMatrix() * playback.calibratedScale;
            effect.Begin(playback.SamplePlacement, seed);
            return true;
        }

        private void LateUpdate()
        {
            if (effect == null || owner == null || effect.IsComplete)
            {
                Destroy(gameObject);
                return;
            }

            if (Time.frameCount == startFrame || actor != null && (actor.IsSimulationSuspended || actor.IsLocalHitStop))
                return;

            effect.Advance(Time.deltaTime);
            if (effect.IsComplete)
                Destroy(gameObject);
        }

        private Matrix4x4 SamplePlacement(string anchor)
        {
            var skeleton = owner == null ? null : owner.Skeleton;
            var model = skeleton == null ? null : skeleton.Skeleton;
            if (model == null)
                return spawnPlacement;

            if (!string.IsNullOrWhiteSpace(anchor))
            {
                const string slotPrefix = "slot:";
                var bone = anchor.StartsWith(slotPrefix, StringComparison.Ordinal)
                    ? model.FindSlot(anchor.Substring(slotPrefix.Length))?.Bone
                    : model.FindBone(anchor);
                if (bone != null)
                {
                    // 骨骼基向量已包含 Spine 翻面，叠加角色朝向符号会再次反转。
                    var pose = bone.AppliedPose;
                    var boneMatrix = Matrix4x4.identity;
                    boneMatrix.SetColumn(0, new Vector4(pose.A, pose.C, 0f, 0f));
                    boneMatrix.SetColumn(1, new Vector4(pose.B, pose.D, 0f, 0f));
                    boneMatrix.SetColumn(3, new Vector4(pose.WorldX, pose.WorldY, 0f, 1f));
                    return skeleton.transform.localToWorldMatrix * boneMatrix * calibratedScale;
                }
            }

            if (!binding.follow)
                return spawnPlacement;

            return Matrix4x4.TRS(owner.AnchorPosition(binding), owner.AnchorRotation(binding), Vector3.one)
                   * FacingMatrix() * calibratedScale;
        }

        private Matrix4x4 FacingMatrix()
            => Matrix4x4.Scale(new Vector3((actor?.DataBus?.ViewFacingX ?? 1f) < 0f ? -1f : 1f, 1f, 1f));
    }
}
