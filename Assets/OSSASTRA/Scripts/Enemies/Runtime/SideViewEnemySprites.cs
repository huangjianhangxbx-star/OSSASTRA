/*
 * 脚本：SideViewEnemySprites
 * 功能：依据敌人策略、正式受击和死亡事实播放参考帧。
 * 职责：保持动画阶段衔接、帧朝向和爬行表面朝向；不决定敌人状态或伤害。
 */
using System;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Enemies
{
    [DisallowMultipleComponent]
    public sealed class SideViewEnemySprites : MonoBehaviour
    {
        [Serializable]
        public sealed class Sequence
        {
            public string key;
            public string sourceName;
            public float framesPerSecond;
            public bool loop;
            public int loopStart;
            public Sprite[] frames;
            public float limitSeconds;
        }

        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Sequence[] clips = Array.Empty<Sequence>();
        [SerializeField] private bool sourceFacesRight;
        private SideViewEnemyBrain brain;
        private CombatVitals vitals;
        private Sequence current;
        private float elapsed;

        public void Configure(SpriteRenderer renderer, Sequence[] sequences, bool facesRight)
        {
            visual = renderer;
            clips = sequences;
            sourceFacesRight = facesRight;
        }

        private void Awake()
        {
            brain = GetComponent<SideViewEnemyBrain>();
            vitals = GetComponent<CombatVitals>();
        }

        private void LateUpdate()
        {
            if (visual == null || brain == null || vitals == null || vitals.Health == null) return;
            var key = vitals.IsAlive ? brain.AnimationKey : "death";
            if (vitals.IsHitReacting && brain.Phase < SideViewEnemyPhase.Windup && Find("hurt") != null) key = "hurt";
            var next = Find(key) ?? (vitals.IsAlive ? Find("idle") : null);
            if (next == null || next.frames.Length == 0)
            {
                visual.enabled = false;
                return;
            }
            if (!ReferenceEquals(next, current))
            {
                var continues = current != null && current.key.StartsWith("windup", StringComparison.Ordinal)
                    && next.key.StartsWith("active", StringComparison.Ordinal) && next.sourceName == current.sourceName;
                current = next;
                if (!continues) elapsed = 0;
            }
            else elapsed += Time.deltaTime;
            var frame = Mathf.FloorToInt(elapsed * current.framesPerSecond);
            visual.enabled = vitals.IsAlive || (current.limitSeconds > 0
                ? elapsed < current.limitSeconds : frame < current.frames.Length);
            visual.flipX = (brain.Direction.x > 0) != sourceFacesRight;
            if (frame >= current.frames.Length)
                frame = current.loop ? current.loopStart + (frame - current.loopStart) % (current.frames.Length - current.loopStart)
                    : current.frames.Length - 1;
            visual.transform.localRotation = Quaternion.Euler(0, 0, brain.VisualAngle);
            visual.sprite = current.frames[frame];
        }

        private Sequence Find(string key)
        {
            foreach (var clip in clips)
                if (clip.key == key) return clip;
            return null;
        }
    }
}
