using CandidateService.Domain.Entities;

namespace CandidateService.Domain.Interfaces
{
    public interface IOperationRepository
    {
        Task<Operation?> GetByIdAsync(string id);
        Task<IEnumerable<Operation>> GetProcessingOperationsAsync();
        Task<Operation> CreateAsync(Operation operation);
        Task UpdateAsync(Operation operation);
        Task<bool> ExistsAsync(string id);
        Task<IEnumerable<OperationEvent>> GetEventsAsync(string operationId);
    }
}
