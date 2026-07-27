using CandidateService.Application.Exceptions;
using CandidateService.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CandidateService.Application.Commands
{
    public class ProcessReceiptCommand : IRequest
    {
        public string ProviderPaymentId { get; set; } = string.Empty;
        public string OperationId { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }

    public class ProcessReceiptCommandHandler : IRequestHandler<ProcessReceiptCommand>
    {
        private readonly IOperationRepository _repository;
        private readonly ILogger<ProcessReceiptCommandHandler> _logger;

        public ProcessReceiptCommandHandler(IOperationRepository repository, ILogger<ProcessReceiptCommandHandler> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task Handle(ProcessReceiptCommand request, CancellationToken cancellationToken)
        {
            var operation = await _repository.GetByIdAsync(request.OperationId);
            if (operation == null)
            {
                throw new NotFoundException($"Operation {request.OperationId} not found");
            }

            if (operation.Status == Domain.Enums.OperationStatus.COMPLETED || operation.Status == Domain.Enums.OperationStatus.REJECTED)
            {
                if (operation.ProviderPaymentId != null && operation.ProviderPaymentId != request.ProviderPaymentId)
                {
                    throw new ConflictException(
                    $"Provider payment ID mismatch. Expected: {operation.ProviderPaymentId}, Got: {request.ProviderPaymentId}"
                    );
                }

                _logger.LogInformation(
                "Duplicate receipt for operation {OperationId} with status {Status}. Ignoring.",
                request.OperationId,
                operation.Status
                );
                return;
            }

            if (operation.ProviderPaymentId == null)
            {
                operation.SetProviderPaymentId(request.ProviderPaymentId);
            }
            else if (operation.ProviderPaymentId != request.ProviderPaymentId)
            {
                throw new ConflictException(
                $"Provider payment ID mismatch. Expected: {operation.ProviderPaymentId}, Got: {request.ProviderPaymentId}"
                );
            }

            if (request.Result == "COMPLETED")
            {
                operation.Complete(request.ProviderPaymentId);
                _logger.LogInformation(
                "Operation {OperationId} completed. ProviderPaymentId: {ProviderPaymentId}",
                request.OperationId,
                request.ProviderPaymentId
                );
            }
            else if (request.Result == "REJECTED")
            {
                operation.Reject(request.ProviderPaymentId, request.Message);
                _logger.LogInformation(
                    "Operation {OperationId} rejected. ProviderPaymentId: {ProviderPaymentId}, Message: {Message}",
                    request.OperationId,
                    request.ProviderPaymentId,
                    request.Message
                );
            }
            else
            {
                throw new ValidationException($"Invalid result value: {request.Result}");
            }

            await _repository.UpdateAsync(operation);
        }
    }
}
