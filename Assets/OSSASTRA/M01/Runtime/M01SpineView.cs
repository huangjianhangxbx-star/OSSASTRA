using System;
using Spine.Unity;
using UnityEngine;

namespace Ossastra.M01
{
    public sealed class M01SpineView : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation skeleton;
        [SerializeField] private string idle = "idle1_1", move = "run", attack = "atk1";
        private string lastClip;
        private Guid lastAction;
        private float originalScaleX;
        private float originalTimeScale;
        public string CurrentClip => lastClip;

        public bool Initialize()
        {
            if (skeleton == null) skeleton = GetComponent<SkeletonAnimation>();
            if (skeleton == null || skeleton.skeletonDataAsset == null) return false;
            skeleton.Initialize(false);
            var data = skeleton.skeletonDataAsset.GetSkeletonData(false);
            if (data == null || data.FindAnimation(idle) == null || data.FindAnimation(move) == null ||
                data.FindAnimation(attack) == null || skeleton.Skeleton == null) return false;
            originalScaleX = Mathf.Abs(skeleton.Skeleton.ScaleX);
            originalTimeScale = skeleton.timeScale;
            return true;
        }

        public void SetPaused(bool paused)
        {
            if (skeleton != null) skeleton.timeScale = paused ? 0 : originalTimeScale;
        }

        public void Present(bool actionActive, Guid actionId, bool moving, float facing)
        {
            if (skeleton == null || skeleton.Skeleton == null || skeleton.AnimationState == null) return;
            var clip = actionActive && !moving ? attack : moving ? move : idle;
            var replay = clip == attack && actionId != lastAction;
            skeleton.Skeleton.ScaleX = originalScaleX * facing;
            if (clip != lastClip || replay)
            {
                skeleton.AnimationState.SetAnimation(0, clip, clip != attack);
                lastClip = clip;
            }
            lastAction = actionId;
        }
    }
}
