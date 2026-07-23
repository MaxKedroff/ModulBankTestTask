namespace CandidateService.Application.DTOs
{
    public class EventResponse
    {
        public int EventId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }
}
