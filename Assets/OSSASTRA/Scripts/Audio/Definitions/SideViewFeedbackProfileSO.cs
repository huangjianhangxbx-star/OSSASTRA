// 角色专用的真实接触反馈。只保存对正式声音、接触画面和相机表现的配置。
using System;
using System.Linq;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Audio
{
    public enum SideViewContactResult { Hit, Guard, Evade, Surface }
    public enum SideViewMotionKind { Jump, Land, Grab, WallJump, Climb, Drop }

    [Serializable]
    public sealed class SideViewFeedbackSettings
    {
        public bool overrideSound;
        public SfxEventDefinitionSO sound;
        public float soundDelayMs, soundStartMs;
        public float volume = 1, pitchMin = 1, pitchMax = 1;
        public bool overrideEffect;
        public GameObject effect;
        public float effectDelayMs, effectScale = 1, effectLifetime = .2f;
        public Vector3 localOffset;
        public CombatEffectDirection direction = CombatEffectDirection.Arriving;
        public bool followTarget;
        public bool overrideShake;
        public float shake;
        public float strongLandingSpeed = 8, strongLandingMultiplier = 1;
        [NonSerialized] private float? frozenSoundVolume;

        internal SideViewFeedbackSettings Copy() => (SideViewFeedbackSettings)MemberwiseClone();
        internal SideViewFeedbackSettings Freeze()
        {
            var copy = Copy();
            copy.frozenSoundVolume = sound == null ? (float?)null : sound.volume;
            return copy;
        }
        internal float SourceVolume => frozenSoundVolume ?? sound.volume;

        internal static int SampleOffset(float milliseconds, int clipSamples, int sampleRate)
        {
            double sample = Math.Floor((double)milliseconds * sampleRate / 1000.0);
            return sample >= 0 && sample < clipSamples ? (int)sample : -1;
        }
    }

    [Serializable]
    public sealed class SideViewCombatFeedbackBinding
    {
        public string selector = "body.basic";
        public int stage;
        public string channel, deliveryMode, material;
        public SideViewContactResult result;
        public SideViewFeedbackSettings settings = new SideViewFeedbackSettings();

        internal SideViewCombatFeedbackBinding Copy()
        {
            var copy = (SideViewCombatFeedbackBinding)MemberwiseClone();
            copy.settings = settings?.Freeze();
            return copy;
        }
    }

    [Serializable]
    public sealed class SideViewMotionFeedbackBinding
    {
        public SideViewMotionKind kind;
        public string material;
        public SideViewFeedbackSettings settings = new SideViewFeedbackSettings();
    }

    [CreateAssetMenu(menuName = "OSSASTRA/Audio/横版角色反馈", fileName = "SideViewFeedback")]
    public sealed class SideViewFeedbackProfileSO : ScriptableObject
    {
        public SideViewCombatFeedbackBinding[] combat = Array.Empty<SideViewCombatFeedbackBinding>();
        public SideViewMotionFeedbackBinding[] movement = Array.Empty<SideViewMotionFeedbackBinding>();

        public SideViewCombatFeedbackBinding ResolveCombat(string selector, int stage, SideViewContactResult result,
            string material, string channel = null, string deliveryMode = null)
            => ResolveCombat(combat, selector, stage, result, material, channel, deliveryMode);

        internal static SideViewCombatFeedbackBinding ResolveCombat(SideViewCombatFeedbackBinding[] rows,
            string selector, int stage, SideViewContactResult result, string material, string channel, string deliveryMode)
        {
            SideViewCombatFeedbackBinding winner = null;
            int best = -1;
            foreach (var row in rows ?? Array.Empty<SideViewCombatFeedbackBinding>())
            {
                if (row == null || row.settings == null || row.result != result) continue;
                int score = CombatSoundProfileSO.MatchScore(row.selector, row.stage, row.channel, row.deliveryMode,
                    null, row.material, selector, stage, channel, deliveryMode, null, material);
                if (score <= best) continue;
                best = score;
                winner = row;
            }
            return winner;
        }

        internal SideViewCombatFeedbackBinding[] FreezeCombat()
            => (combat ?? Array.Empty<SideViewCombatFeedbackBinding>()).Where(row => row != null).Select(row => row.Copy()).ToArray();

        public SideViewMotionFeedbackBinding ResolveMovement(SideViewMotionKind kind, string material)
        {
            SideViewMotionFeedbackBinding fallback = null;
            foreach (var row in movement ?? Array.Empty<SideViewMotionFeedbackBinding>())
            {
                if (row == null || row.settings == null || row.kind != kind) continue;
                if (string.IsNullOrEmpty(row.material)) { if (fallback == null) fallback = row; }
                else if (row.material == material) return row;
            }
            return fallback;
        }
    }
}
