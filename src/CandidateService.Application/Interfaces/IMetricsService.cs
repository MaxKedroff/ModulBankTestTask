namespace CandidateService.Application.Interfaces
{
    public interface IMetricsService
    {
        void IncrementOperationCreated();
        void IncrementOperationSubmitted();
        void IncrementOperationCompleted();
        void IncrementOperationRejected();
        void IncrementProviderRetry();
        void SetPendingOperations(int count);
    }
}
