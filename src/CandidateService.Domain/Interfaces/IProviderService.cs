using CandidateService.Domain.Entities;

namespace CandidateService.Domain.Interfaces
{
    public interface IProviderService
    {
        Task<ProviderPaymentResponse> SendPaymentAsync(Operation operation);

    }

    public class ProviderPaymentResponse
    {
        public bool Success { get; set; }
        public string? ProviderPaymentId { get; set; }
        public string? ErrorMessage { get; set; }
        public int StatusCode { get; set; }
    }
}
