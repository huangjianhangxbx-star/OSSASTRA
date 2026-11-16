using System;
using UnityEngine;
using Ossastra.Commerce.Runtime;

namespace Ossastra.WorldTransition
{
    /// <summary>Task-only quest proof, serialized in the existing Commerce snapshot.</summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class SVWorld01QuestMarker : MonoBehaviour, ICommerceSnapshotParticipant
    {
        public const string Key = "ossastra.sv-world-01.quest-marker";
        public const string MarkerId = "sv-world-01.quest-proof";
        public CommerceService commerce;

        private readonly QuestMarkerState state = new QuestMarkerState(MarkerId);
        private bool registered;
        public string SnapshotKey => Key;
        public bool Reached => state.Reached;
        public string Diagnostic { get; private set; } = "尚未登记";
        public event Action Changed;

        private void Awake()
        {
            try
            {
                if (commerce == null) throw new InvalidOperationException("CommerceService 缺失");
                state.Changed += NotifyChanged;
                commerce.RegisterSnapshotParticipant(this);
                registered = true;
                Diagnostic = "已登记共同快照";
            }
            catch (Exception ex)
            {
                Diagnostic = ex.Message;
                Debug.LogError("[SV-WORLD-01] Quest marker: " + Diagnostic, this);
                enabled = false;
            }
        }

        private void OnDestroy() => state.Changed -= NotifyChanged;
        private void NotifyChanged() => Changed?.Invoke();

        public bool MarkReached()
        {
            if (!registered || commerce == null || !commerce.Ready
                || commerce.SaveStatus == null || !commerce.SaveStatus.CanEdit)
                return false;
            return state.MarkReached();
        }

        public CommerceSnapshotSection Capture()
        {
            return new CommerceSnapshotSection
            {
                key = Key,
                schema = 1,
                payload = state.CaptureJson()
            };
        }

        public bool TryPrepareRestore(CommerceSnapshotSection section,
            CommerceRuntimeSnapshot aggregate, out object prepared, out string error)
        {
            prepared = null;
            if (section == null || section.key != Key || section.schema != 1)
            {
                error = "任务标记 section 键或版本不匹配";
                return false;
            }
            var okay = state.TryPrepareRestore(section.payload,
                out var candidate, out error);
            if (okay) prepared = candidate;
            return okay;
        }

        public void PublishRestore(object prepared)
        {
            if (prepared is PreparedQuestMarker candidate)
                state.PublishRestore(candidate);
        }
    }
}
