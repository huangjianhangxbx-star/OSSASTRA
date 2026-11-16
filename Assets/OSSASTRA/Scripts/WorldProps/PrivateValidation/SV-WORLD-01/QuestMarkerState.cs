using System;
using Newtonsoft.Json;

namespace Ossastra.WorldTransition
{
    public sealed class PreparedQuestMarker
    {
        internal PreparedQuestMarker(string markerId, bool reached)
        {
            MarkerId = markerId;
            Reached = reached;
        }

        public string MarkerId { get; }
        public bool Reached { get; }
    }

    public sealed class QuestMarkerState
    {
        sealed class Payload
        {
            public int schema;
            public string markerId;
            public bool? reached;
        }

        public QuestMarkerState(string markerId)
        {
            if (string.IsNullOrWhiteSpace(markerId))
                throw new ArgumentException("Marker ID is required", nameof(markerId));
            MarkerId = markerId;
        }

        public string MarkerId { get; }
        public bool Reached { get; private set; }
        public event Action Changed;

        public bool MarkReached() => SetReached(true);
        public bool ResetForValidation() => SetReached(false);
        bool SetReached(bool value)
        {
            if (Reached == value)
                return false;
            Reached = value;
            Changed?.Invoke();
            return true;
        }

        public string CaptureJson() => JsonConvert.SerializeObject(new Payload
        {
            schema = 1,
            markerId = MarkerId,
            reached = Reached
        });

        public bool TryPrepareRestore(string payload, out PreparedQuestMarker prepared, out string error)
        {
            prepared = null;
            error = null;
            try
            {
                var decoded = JsonConvert.DeserializeObject<Payload>(payload);
                if (decoded == null || decoded.schema != 1 ||
                    !string.Equals(decoded.markerId, MarkerId, StringComparison.Ordinal) ||
                    !decoded.reached.HasValue)
                    throw new ArgumentException("Quest marker payload does not match schema or ID");
                prepared = new PreparedQuestMarker(MarkerId, decoded.reached.Value);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public void PublishRestore(PreparedQuestMarker prepared)
        {
            Reached = prepared.Reached;
        }
    }
}
