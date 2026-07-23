using CandidateService.Domain.Enums;

namespace CandidateService.Domain.Entities
{
    public class Operation
    {
        public string Id { get; private set; }
        public decimal Amount { get; private set; }
        public string Currency { get; private set; }
        public string Description { get; private set; }
        public OperationStatus Status { get; private set; }
        public string? ProviderPaymentId { get; set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public int RetryCount { get; private set; }
        public DateTime? NextRetryAt { get; private set; }
        public bool IsProcessing { get; set; }
        public List<OperationEvent> Events { get; private set; }

        public Operation(string id, decimal amount, string currency, string description)
        {
            Id = id;
            Amount = amount;
            Currency = currency;
            Description = description;
            Status = OperationStatus.CREATED;
            CreatedAt = DateTime.UtcNow;
            Events = new List<OperationEvent>();
            IsProcessing = false;
            RetryCount = 0;

            AddEvent(OperationStatus.CREATED, null, "Operation created");
        }

        public void MarkAsProcessing()
        {
            if (Status != OperationStatus.CREATED)
                throw new InvalidOperationException($"Cannot process operation in {Status} status");

            Status = OperationStatus.PROCESSING;
            UpdatedAt = DateTime.UtcNow;
            IsProcessing = true;
            AddEvent(OperationStatus.PROCESSING, OperationStatus.CREATED, "Processing started");
        }

        public void SetProviderPaymentId(string providerPaymentId)
        {
            if (string.IsNullOrEmpty(providerPaymentId))
                throw new ArgumentException("Provider payment ID cannot be empty");

            if (ProviderPaymentId != null && ProviderPaymentId != providerPaymentId)
                throw new InvalidOperationException($"Provider payment ID mismatch. Expected: {ProviderPaymentId}, Got: {providerPaymentId}");

            ProviderPaymentId = providerPaymentId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Complete(string providerPaymentId)
        {
            SetProviderPaymentId(providerPaymentId);
            Status = OperationStatus.COMPLETED;
            UpdatedAt = DateTime.UtcNow;
            IsProcessing = false;
            AddEvent(OperationStatus.COMPLETED, OperationStatus.PROCESSING, "Payment completed");
        }

        public void Reject(string providerPaymentId, string? message = null)
        {
            SetProviderPaymentId(providerPaymentId);
            Status = OperationStatus.REJECTED;
            UpdatedAt = DateTime.UtcNow;
            IsProcessing = false;
            AddEvent(OperationStatus.REJECTED, OperationStatus.PROCESSING, message ?? "Payment rejected");
        }

        public void MarkRetryScheduled()
        {
            RetryCount++;
            NextRetryAt = CalculateNextRetryTime();
            UpdatedAt = DateTime.UtcNow;
        }

        public void ResetProcessing()
        {
            IsProcessing = false;
            UpdatedAt = DateTime.UtcNow;
        }

        private DateTime CalculateNextRetryTime()
        {
            var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(RetryCount, 6)));
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
            return DateTime.UtcNow.Add(baseDelay).Add(jitter);
        }

        private void AddEvent(OperationStatus toStatus, OperationStatus? fromStatus, string message)
        {
            var eventId = Events.Count + 1;
            Events.Add(new OperationEvent(eventId, toStatus, fromStatus, message, DateTime.UtcNow));
        }
    }
}
