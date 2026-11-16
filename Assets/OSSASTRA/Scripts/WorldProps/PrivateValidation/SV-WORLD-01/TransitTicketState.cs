using System;

namespace Ossastra.WorldTransition
{
    public enum TransitBeginResult
    {
        Started,
        InvalidTarget,
        Busy
    }

    public sealed class TransitTicket
    {
        internal TransitTicket(Guid id, string sourceMapId, string sourceGateId,
            string destinationScene, string destinationGateId)
        {
            Id = id;
            SourceMapId = sourceMapId;
            SourceGateId = sourceGateId;
            DestinationScene = destinationScene;
            DestinationGateId = destinationGateId;
        }

        public Guid Id { get; }
        public string SourceMapId { get; }
        public string SourceGateId { get; }
        public string DestinationScene { get; }
        public string DestinationGateId { get; }
    }

    public sealed class TransitTicketState
    {
        public TransitTicket Pending { get; private set; }

        public TransitBeginResult TryBegin(string sourceMapId, string sourceGateId,
            string destinationScene, string destinationGateId, out TransitTicket ticket)
        {
            ticket = null;
            if (string.IsNullOrWhiteSpace(destinationScene)
                || string.IsNullOrWhiteSpace(destinationGateId))
            {
                return TransitBeginResult.InvalidTarget;
            }

            if (Pending != null)
            {
                return TransitBeginResult.Busy;
            }

            ticket = new TransitTicket(Guid.NewGuid(), sourceMapId, sourceGateId,
                destinationScene, destinationGateId);
            Pending = ticket;
            return TransitBeginResult.Started;
        }

        public bool TryConsumeArrival(string scene, string gateId, out TransitTicket ticket)
        {
            ticket = null;
            var pending = Pending;
            if (pending == null
                || !string.Equals(scene, pending.DestinationScene, StringComparison.Ordinal)
                || !string.Equals(gateId, pending.DestinationGateId, StringComparison.Ordinal))
            {
                return false;
            }

            Pending = null;
            ticket = pending;
            return true;
        }

        public bool Abort(Guid ticketId)
        {
            if (Pending == null || Pending.Id != ticketId)
            {
                return false;
            }

            Pending = null;
            return true;
        }
    }
}
