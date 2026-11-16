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
            var key = vitals.IsAlive ? GetLiveKey() : "death";
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

        private string GetLiveKey()
        {
            switch (brain.Phase)
            {
                case SideViewEnemyPhase.Windup: return brain.SecondMove && Find("windup2") != null ? "windup2" : "windup";
                case SideViewEnemyPhase.Active:
                    if (brain.Bumped && Find("bump") != null) return "bump";
                    return brain.SecondMove && Find("active2") != null ? "active2" : "active";
                case SideViewEnemyPhase.Recovery: return brain.SecondMove && Find("recover2") != null ? "recover2" : "recover";
                case SideViewEnemyPhase.Alert: return "alert";
                default: return "idle";
            }
        }

        private Sequence Find(string key)
        {
            foreach (var clip in clips)
                if (clip.key == key) return clip;
            return null;
        }
    }
}
