using CandidateService.Domain.Enums;

namespace CandidateService.Domain.Entities
{
    public class OperationEvent
    {
        public int EventId { get; set; }
        public string Type { get; private set; }
        public string? FromStatus { get; private set; }
        public string ToStatus { get; private set; }
        public string Message { get; private set; }
        public DateTime OccurredAt { get; private set; }

        private OperationEvent() { }


        public OperationEvent(int eventId, OperationStatus toStatus, OperationStatus? fromStatus, string message, DateTime occurredAt)
        {
            EventId = eventId;
            Type = toStatus.ToString();
            FromStatus = fromStatus?.ToString();
            ToStatus = toStatus.ToString();
            Message = message;
            OccurredAt = occurredAt;
        }
    }
}
