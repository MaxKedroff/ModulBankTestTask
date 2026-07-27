using CandidateService.Application.DTOs;
using CandidateService.Application.Exceptions;
using CandidateService.Domain.Interfaces;
using MediatR;

namespace CandidateService.Application.Queries
{
    public class GetOperationQuery : IRequest<OperationResponse>
    {
        public string OperationId { get; set; } = string.Empty;
    }

    public class GetOperationQueryHandler : IRequestHandler<GetOperationQuery, OperationResponse>
    {
        private readonly IOperationRepository _repository;

        public GetOperationQueryHandler(IOperationRepository repository)
        {
            _repository = repository;
        }

        public async Task<OperationResponse> Handle(GetOperationQuery request, CancellationToken cancellationToken)
        {
            var operation = await _repository.GetByIdAsync(request.OperationId);

            if (operation == null)
            {
                throw new NotFoundException($"Operation {request.OperationId} not found");
            }

            return new OperationResponse
            {
                OperationId = operation.Id,
                Amount = operation.Amount.ToString("F2"),
                Currency = operation.Currency,
                Description = operation.Description,
                Status = operation.Status.ToString(),
                ProviderPaymentId = operation.ProviderPaymentId
            };
        }
    }
}
